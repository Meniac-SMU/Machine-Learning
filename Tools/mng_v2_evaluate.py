"""Frozen ONNX probability inference, independent per-match sampling RNG, paired seeds."""
import argparse
from concurrent.futures import ThreadPoolExecutor, as_completed
import hashlib
import json
from pathlib import Path
import time
import numpy as np
import onnx
from onnx.reference import ReferenceEvaluator
from onnx.utils import Extractor
from mlagents_envs.environment import UnityEnvironment
from mlagents_envs.base_env import ActionTuple
from mlagents_envs.side_channel.engine_configuration_channel import EngineConfigurationChannel

ROOT = Path(__file__).resolve().parent.parent
PROTOCOL = "MNG-V2-FROZEN-300-v2"
BASELINES = ("uniform-valid", "recover", "balanced", "carry-shot")


def execution_sample(observation, samples, skills, phases):
    """Executed field-player tasks before the next action; no policy intervention."""
    if observation[243] > .5:  # goal pause has no tactical personnel interpretation
        return
    state = 0 if observation[101] > .5 else 1
    command = int(np.argmax(observation[116:122]))
    samples[state, command] += 1
    for slot in (1, 2, 3):
        block = observation[133 + 27 * slot:133 + 27 * (slot + 1)]
        skills[state, command] += block[:13].astype(np.int64)
        phases[state, command] += block[13:18].astype(np.int64)

def sha(path): return hashlib.sha256(Path(path).read_bytes()).hexdigest()

def probability_sample(observation, valid, probabilities, samples, availability, sums):
    """Read-only active-play denominators; state order is own/opponent/neutral."""
    if observation[243] >= .5:
        return
    state = 0 if observation[101] > .5 else 1 if observation[102] > .5 else 2
    samples[state] += 1
    availability[state] += valid
    sums[state] += probabilities

def write_decision_trace(trace, team, observation, valid, probabilities, action):
    if trace is not None:
        trace.write(json.dumps(dict(team=team, observation=observation.tolist(),
            valid=valid.tolist(), probabilities=probabilities.tolist(), action=action))+'\n')

def join_pass_context(decisions, policies):
    """Pair ordered decisions only after checking actions, masks and observation tick."""
    assert len(policies) >= len(decisions), 'Missing policy lifecycle records'
    assert all(p['matchState']=='Finished' for p in policies[len(decisions):]), 'Unmatched active decisions'
    joined=[]
    for decision, policy in zip(decisions,policies):
        context=policy.get('passContext',{})
        assert context.get('version')==1, 'Pass context unavailable in this runtime'
        assert context['tick']==policy['observationTick'], 'Pass context is not from the observed tick'
        assert decision['action']==policy['rawCommand']==policy['effectiveCommand'], 'Action mismatch'
        assert sum(1<<i for i,v in enumerate(decision['valid']) if v)==policy['maskBits'], 'Mask mismatch'
        obs=decision['observation']
        assert context['goalPause']==(obs[243]>.5), 'Pause mismatch'
        assert context['ownPossession']==(obs[101]>.5), 'Possession mismatch'
        joined.append(dict(context, parentCommandId=policy['parentCommandId'],
            state='goalPause' if context['goalPause'] else 'own' if obs[101]>.5 else 'opponent' if obs[102]>.5 else 'neutral',
            valid=bool(decision['valid'][1]), probability=decision['probabilities'][1], selected=decision['action']==1))
    return joined

def summarize_pass_context(rows):
    groups={}
    for row in rows:
        # Keep continuous metres in the joined file; do not invent safe-pass bins.
        key=row['state']+('/forward-blocked' if row['forwardBlocked'] else '/forward-open')
        group=groups.setdefault(key,dict(decisions=0,valid=0,selected=0,probabilitySum=0.,validProbabilitySum=0.))
        group['decisions']+=1; group['valid']+=row['valid']; group['selected']+=row['selected']
        group['probabilitySum']+=row['probability']
        if row['valid']: group['validProbabilitySum']+=row['probability']
    return groups

class FrozenPolicy:
    def __init__(self, source, seed):
        self.source = source
        self.rng = np.random.RandomState(seed)
        self.evaluator = None
        self.last_probabilities = np.zeros(6, dtype=np.float64)
        if source not in BASELINES:
            model = onnx.load(source)
            obs = next(x for x in model.graph.input if x.name == "obs_0")
            assert obs.type.tensor_type.shape.dim[1].dim_value == 244
            softmax = [n.output[0] for n in model.graph.node if n.op_type == "Softmax"]
            assert len(softmax) == 1, "Expected one categorical action branch"
            # Extract only the exact exported probability graph; replace ONNX's
            # process-global Multinomial with an independently seeded sampler.
            graph = Extractor(model).extract_model(["obs_0", "action_masks"], softmax)
            self.evaluator = ReferenceEvaluator(graph)

    def act(self, observation, valid):
        if self.evaluator:
            probabilities = self.evaluator.run(None, {"obs_0": observation.astype(np.float32),
                "action_masks": valid.astype(np.float32)})[0][0].astype(np.float64)
            assert np.isfinite(probabilities).all() and np.all(probabilities >= 0)
            probabilities /= probabilities.sum()
            self.last_probabilities = probabilities.copy()
            action = int(self.rng.choice(6, p=probabilities))
            assert valid[0, action], "Frozen model selected masked command"
            return action
        if self.source == "uniform-valid":
            self.last_probabilities = valid[0].astype(float) / valid[0].sum()
            return int(self.rng.choice(np.flatnonzero(valid[0])))
        action = 3 if self.source == "recover" else 4
        if self.source == "carry-shot":
            action = 2 if valid[0, 2] else 0 if observation[0, 101] > .5 else 3
        action = action if valid[0, action] else 4
        self.last_probabilities = np.eye(6)[action]
        return action

def validate_common_rules(result, team, candidate_common, opponent_common):
    expected=[candidate_common,opponent_common] if team==0 else [opponent_common,candidate_common]
    assert result['commonRulesEnabled']==expected, 'Rule assignment mismatch or unsupported old runtime'
    for t,enabled in enumerate(expected):
        if not enabled:
            assert all(result[key][t]==0 for key in ('commonPassAttempts','commonClearanceAttempts','commonNoTargetAttempts','commonRuleStrikes')), 'Legacy side received rule intervention'

def game(job):
    index, seed, team, candidate, opponent, output, executable, runtime_sha, mirror, rng_swap, decision_trace, lifecycle_trace, candidate_common, opponent_common = job
    folder = Path(output) / f"match-{index:03d}"
    folder.mkdir()
    result_path = folder / "result.json"
    rng_seed = seed + 100000
    policies = {team: FrozenPolicy(candidate, rng_seed + rng_swap)}
    if opponent != "R0-Full-v2": policies[1-team] = FrozenPolicy(opponent, rng_seed + 1 - rng_swap)
    channel = EngineConfigurationChannel()
    channel.set_configuration_parameters(time_scale=20, target_frame_rate=-1)
    env = None
    trace = (folder / 'decisions.jsonl').open('w') if decision_trace else None
    counts = np.zeros((4, 6), dtype=np.int64)
    decision_samples = np.zeros(3, dtype=np.int64)
    availability = np.zeros((3, 6), dtype=np.int64)
    probability_sums = np.zeros((3, 6), dtype=np.float64)
    rear_samples = np.zeros((2, 6), dtype=np.int64)
    rear_counts = np.zeros((2, 6), dtype=np.int64)
    execution_samples = np.zeros((2, 6), dtype=np.int64)
    execution_skills = np.zeros((2, 6, 13), dtype=np.int64)
    execution_phases = np.zeros((2, 6, 5), dtype=np.int64)
    began = time.monotonic()
    try:
        env = UnityEnvironment(file_name=str(executable), worker_id=index, base_port=7100,
            seed=seed, no_graphics=True, side_channels=[channel], timeout_wait=120,
            log_folder=str(folder), additional_args=["-mngEvaluationSeed", str(seed), "-mngPolicyTeam", str(team),
                "-mngNeuralOpponent", str(opponent != "R0-Full-v2").lower(),
                "-mngCandidateCommonRules",str(candidate_common).lower(),"-mngOpponentCommonRules",str(opponent_common).lower(),
                "-mngEvaluationOutput", str(result_path), "-mngEvidenceDir", str(folder),
                "-mngMirror", str(mirror).lower(), "-mngRunId", Path(output).name, "-mngBuildSha", runtime_sha, "-mngFullTrace", "true",
                "-mngLifecycleTrace", str(lifecycle_trace).lower()])
        env.reset()
        while not result_path.exists():
            if time.monotonic() - began > 600: raise TimeoutError("Frozen match timed out")
            for behavior in env.behavior_specs:
                current_team = int(behavior.split("team=")[-1])
                decisions, terminal = env.get_steps(behavior)
                if not len(decisions): continue
                assert len(decisions) == 1 and decisions.obs[0].shape == (1,244)
                assert np.isfinite(decisions.obs[0]).all()
                valid = ~decisions.action_mask[0] if decisions.action_mask else np.ones((1,6), dtype=bool)
                action = policies[current_team].act(decisions.obs[0], valid)
                write_decision_trace(trace, current_team, decisions.obs[0][0], valid[0],
                    policies[current_team].last_probabilities, action)
                env.set_actions(behavior, ActionTuple(discrete=np.array([[action]], dtype=np.int32)))
                if current_team == team:
                    execution_sample(decisions.obs[0][0], execution_samples, execution_skills, execution_phases)
                    obs = decisions.obs[0][0]
                    probability_sample(obs, valid[0], policies[current_team].last_probabilities,
                        decision_samples, availability, probability_sums)
                    if obs[243] < .5:
                        state = 0 if obs[101] > .5 else 1 if obs[102] > .5 else 2
                        previous = int(np.argmax(obs[116:122]))
                        rear_samples[0 if state == 0 else 1, previous] += 1
                        rear_counts[0 if state == 0 else 1, previous] += sum(obs[slot*12] > .5 and obs[slot*12+1] < obs[96] for slot in (1,2,3))
                    own = obs[101] > .5
                    danger = decisions.obs[0][0,96] < -.65
                    counts[0 if own else 1,action] += 1
                    if danger: counts[2,action] += 1
                    if not valid.all(): counts[3,action] += 1
            env.step()
        result = json.loads(result_path.read_text())
        assert result["protocol"] == PROTOCOL and result["seed"] == seed and result["policyTeam"] == team
        assert result["elapsed"] >= 299.9
        assert result["mismatches"] == result["overrides"] == result["directDecisionRewards"] == 0
        validate_common_rules(result,team,candidate_common,opponent_common)
        gf, ga = (result["redScore"],result["navyScore"]) if team == 0 else (result["navyScore"],result["redScore"])
        result.update(index=index, inferenceSeed=rng_seed + rng_swap, inferenceRngSwap=rng_swap, mirror=mirror,
            decisionSamplesByState=decision_samples.tolist(),
            availableByState=availability.tolist(), probabilitySumsByState=probability_sums.tolist(),
            rearSamples=rear_samples.tolist(), rearPlayerCounts=rear_counts.tolist(), goalsFor=gf, goalsAgainst=ga,
            score=1 if gf>ga else .5 if gf==ga else 0, conditionalCommands=counts.tolist(),
            executionSamples=execution_samples.tolist(), executionSkills=execution_skills.tolist(),
            executionPhases=execution_phases.tolist())
        if trace and lifecycle_trace:
            trace.flush()
            lifecycle_files=list(folder.glob('lifecycle-*.jsonl'))
            assert len(lifecycle_files)==1, 'Expected one worker lifecycle file'
            decisions=[r for r in map(json.loads,(folder/'decisions.jsonl').read_text().splitlines()) if r['team']==team]
            policy_rows=[r for r in map(json.loads,lifecycle_files[0].read_text().splitlines()) if r['team']==team and r['kind']=='PolicyDecision']
            if policy_rows and all(r.get('passContext',{}).get('version')==1 for r in policy_rows):
                joined=join_pass_context(decisions,policy_rows)
                with (folder/'pass-opportunities.jsonl').open('x') as handle:
                    for row in joined: handle.write(json.dumps(row)+'\n')
                result['passOpportunityGroups']=summarize_pass_context(joined)
            else:
                result['passOpportunityGroups']=None  # Older immutable runtime, never zero opportunities.
        (folder / "verified.json").write_text(json.dumps(result, indent=2))
        return result
    finally:
        if env is not None: env.close()
        if trace: trace.close()

def summarize(rows, bootstrap_seed=20260922):
    pairs = {}
    for row in rows:
        identity=(row["seed"],row["policyTeam"],row.get("inferenceRngSwap",0))
        assert identity not in pairs, "Duplicate match identity"
        pairs[identity]=row["score"]
    seeds=sorted({key[0] for key in pairs})
    for seed in seeds:
        streams={key[2] for key in pairs if key[0]==seed}
        assert all((seed,team,stream) in pairs for team in (0,1) for stream in streams), "Incomplete paired seed"
    values=np.array([np.mean([value for key,value in pairs.items() if key[0]==seed]) for seed in seeds])
    rng=np.random.RandomState(bootstrap_seed)
    samples=values[rng.randint(len(values),size=(20000,len(values)))].mean(axis=1)
    return dict(matches=len(rows),scoreRate=float(values.mean()),
        paired95=np.quantile(samples,[.025,.975]).tolist(),bootstrapSeed=bootstrap_seed,
        perTeam=[float(np.mean([r["score"] for r in rows if r["policyTeam"]==t])) for t in (0,1)],
        wins=sum(r["score"]==1 for r in rows),draws=sum(r["score"]==.5 for r in rows),losses=sum(r["score"]==0 for r in rows),
        goalsFor=sum(r["goalsFor"] for r in rows),goalsAgainst=sum(r["goalsAgainst"] for r in rows),
        shotStrikes=sum(r["shotStrikes"] for r in rows),passStrikes=sum(r["passStrikes"] for r in rows),
        rawEvents=np.sum([r["rawEvents"] for r in rows],axis=0).tolist(),
        rewardedEvents=np.sum([r["rewardedEvents"] for r in rows],axis=0).tolist(),
        conditionalCommands=np.sum([r["conditionalCommands"] for r in rows],axis=0).tolist(),
        stallActivations=sum(r["stallActivations"] for r in rows),
        episodeStallActivations=sum(r["episodeStallActivations"] for r in rows) if all("episodeStallActivations" in r for r in rows) else None,
        episodeStallActiveSeconds=sum(r["episodeStallActiveSeconds"] for r in rows) if all("episodeStallActiveSeconds" in r for r in rows) else None,
        decisionSamplesByState=np.sum([r["decisionSamplesByState"] for r in rows],axis=0).tolist() if all("decisionSamplesByState" in r for r in rows) else None,
        availableByState=np.sum([r["availableByState"] for r in rows],axis=0).tolist() if all("availableByState" in r for r in rows) else None,
        probabilitySumsByState=np.sum([r["probabilitySumsByState"] for r in rows],axis=0).tolist() if all("probabilitySumsByState" in r for r in rows) else None,
        observedRecoveries=sum(r.get("observedRecoveries",0) for r in rows),
        executionSamples=np.sum([r['executionSamples'] for r in rows],axis=0).tolist() if all('executionSamples' in r for r in rows) else None,
        executionSkills=np.sum([r['executionSkills'] for r in rows],axis=0).tolist() if all('executionSkills' in r for r in rows) else None,
        executionPhases=np.sum([r['executionPhases'] for r in rows],axis=0).tolist() if all('executionPhases' in r for r in rows) else None,
        integrity=True)

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--candidate',required=True)
    parser.add_argument('--opponent',default='R0-Full-v2')
    parser.add_argument('--output',required=True)
    parser.add_argument('--build', required=True, help='Explicit immutable EvaluationV2 build directory')
    parser.add_argument('--pairs',type=int,default=20)
    parser.add_argument('--seed',type=int,default=592201)
    parser.add_argument('--mirror-pairs',action='store_true')
    parser.add_argument('--rng-cross',action='store_true')
    parser.add_argument('--decision-trace',action='store_true')
    parser.add_argument('--lifecycle-trace',action='store_true')
    parser.add_argument('--parallel',type=int,default=8)
    parser.add_argument('--candidate-common-rules',choices=('enabled','disabled'),default='enabled')
    parser.add_argument('--opponent-common-rules',choices=('enabled','disabled'),default='enabled')
    args=parser.parse_args()
    output=Path(args.output).resolve(); output.mkdir(parents=True,exist_ok=False)
    (output/'evaluator-source.py').write_bytes(Path(__file__).read_bytes())
    build=Path(args.build).resolve()
    info=json.loads((build/'build-info.json').read_text())
    assert info.get('protocolVersion') == 3, 'Post-R6 runtime protocol required'
    executable=build/'MNG_EvaluationV2.exe'
    assert sha(executable)==info['executableSha256']
    assert sha(build/'MNG_EvaluationV2_Data/Managed/MNG.Runtime.dll')==info['runtimeSha256']
    assert sha(build/'MNG_EvaluationV2_Data/level0')==info['levelSha256']
    registry=json.loads((ROOT/'docs/soccer/training/ms-v2-model-registry.json').read_text(encoding='utf-8-sig'))
    retired={e['sha256'] for e in registry['entries']}
    ids={}
    for role,source in [('candidate',args.candidate),('opponent',args.opponent)]:
        ids[role]=source if source in BASELINES or source=='R0-Full-v2' else sha(source)
        assert ids[role] not in retired, 'Retired model denied'
    manifest=dict(protocol=PROTOCOL,runtimeSha=info['runtimeSha256'],schema=info['schema'],seconds=300,
        environmentRevision=info.get('environmentRevision'),
        passTelemetry='v1: active own/opponent/neutral denominators; with both traces, tick-checked actual target distance and lane clearance in metres; legal is not safe',
        evaluatorSha=sha(__file__),
        executionTelemetry='v1: decision-time field slots 1..3 by preceding command and possession, excluding goal pause',
        inference='ONNX exported categorical probabilities; independent NumPy seeded sampling',
        candidateCommonRules=args.candidate_common_rules,opponentCommonRules=args.opponent_common_rules,
        pairs=args.pairs,seedOffset=args.seed,parallel=args.parallel,mirrorPairs=args.mirror_pairs,rngCross=args.rng_cross,
        decisionTrace=args.decision_trace,lifecycleTrace=args.lifecycle_trace,**ids)
    (output/'manifest.json').write_text(json.dumps(manifest,indent=2))
    jobs=[]
    for pair in range(args.pairs):
        for stream in range(2 if args.rng_cross else 1):
            for team in (0,1):
                jobs.append((len(jobs),args.seed+pair,team,args.candidate,args.opponent,str(output),executable,info['runtimeSha256'],bool(args.mirror_pairs and team),stream,args.decision_trace,args.lifecycle_trace,args.candidate_common_rules=='enabled',args.opponent_common_rules=='enabled'))
    rows=[]
    with ThreadPoolExecutor(max_workers=args.parallel) as pool:
        for future in as_completed([pool.submit(game,j) for j in jobs]):
            row=future.result(); rows.append(row)
            print(f"MATCH {len(rows)}/{len(jobs)} team={row['policyTeam']} score={row['goalsFor']}:{row['goalsAgainst']}",flush=True)
    rows.sort(key=lambda x:x['index'])
    report=dict(manifest=manifest,summary=summarize(rows),matches=rows)
    report['dataSha']=hashlib.sha256(json.dumps(rows,sort_keys=True).encode()).hexdigest()
    (output/'report.json').write_text(json.dumps(report,indent=2))
    print(json.dumps(report['summary']),flush=True)

if __name__=='__main__': main()

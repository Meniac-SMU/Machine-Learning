"""PS recipient teacher collection and frozen evaluation. Full is not supported."""
import argparse
import os
from concurrent.futures import ThreadPoolExecutor, as_completed
from queue import Queue
import json
import time
from collections import Counter
from pathlib import Path
import numpy as np
import mng_psr_model as m
from mlagents_envs.environment import UnityEnvironment
from mlagents_envs.base_env import ActionTuple
from mlagents_envs.side_channel.engine_configuration_channel import EngineConfigurationChannel
from mng_player_log_guard import PlayerLogGuard
import mng_v2_evaluate as old

def build():
    config=json.loads(Path(os.environ.get('MNG_PSR_RUNTIME',str(m.EVIDENCE/'runtime.json'))).read_text());return Path(config['build'])

def runtime_args():
    return json.loads(Path(os.environ.get('MNG_PSR_RUNTIME',str(m.EVIDENCE/'runtime.json'))).read_text()).get('extraArgs',[])

def summarize(rows):
    seeds=sorted({r['seed'] for r in rows})
    assert len({(r['seed'],r['policyTeam']) for r in rows})==len(rows)
    pairs=[]
    for seed in seeds:
        pair=[r for r in rows if r['seed']==seed];assert sorted(r['policyTeam'] for r in pair)==[0,1]
        pairs.append(np.mean([r['score'] for r in pair]))
    rng=np.random.RandomState(929903);values=np.array(pairs)
    boot=values[rng.randint(len(values),size=(10000,len(values)))].mean(1)
    return dict(games=len(rows),wins=sum(r['win'] for r in rows),draws=sum(r['draw'] for r in rows),losses=sum(r['loss'] for r in rows),
                goalsFor=sum(r['goalsFor'] for r in rows),goalsAgainst=sum(r['goalsAgainst'] for r in rows),
                scoreRate=float(values.mean()),pairedSeed95=np.quantile(boot,[.025,.975]).tolist(),
                perTeam=[float(np.mean([r['score'] for r in rows if r['policyTeam']==t])) for t in (0,1)])

class Policy:
    def __init__(self,path,seed):
        self.rng=np.random.RandomState(seed);self.path=str(path);self.teacher=self.path=='teacher'
        self.rule=self.path in ('recover','carry-shot','balanced')
        self.net=None
        if self.rule:return
        if self.teacher:path=m.PARENT
        state=m.torch.load(path,map_location='cpu',weights_only=False)
        self.legacy='action_model._discrete_distribution.branches.1.weight' not in state['Policy']
        self.net=m.policy((6,) if self.legacy else (6,5));self.net.actor.load_state_dict(state['Policy'],strict=True)
    def act(self,o,mask):
        if self.rule:
            c=3 if self.path=='recover' else 4
            if self.path=='carry-shot':c=2 if mask[2] else 0 if o[101]>.5 else 3
            return [c if mask[c] else 4,0],dict(kind='rule')
        valid=m.parent_mask(o[None],mask[None])[0] if self.legacy else mask
        with m.torch.no_grad():
            ds=m.dists(self.net.actor,m.torch.tensor(o[None]),m.torch.tensor(valid[None],dtype=m.torch.float32))
            probs=[d.probs.numpy()[0].astype(float) for d in ds]
        a=[int(self.rng.choice(len(p),p=p/p.sum())) for p in probs]
        if self.legacy:a.append(0)
        label=m.contexts(o[None],mask[None])[0]
        if self.teacher and label['action']==1:a=[1,label['recipient']]
        return a,label

def match(job):
    if (m.EVIDENCE/'STOP').exists():raise InterruptedError('PS saved-stop requested before match')
    idx,slot,seed,team,candidate,opponent,out,legacy=job
    assert opponent in ('recover','carry-shot','balanced') or Path(opponent).suffix=='.pt'
    folder=Path(out)/f'match-{idx:03d}';folder.mkdir()
    runtime=build()/'EvaluationV2';info=json.loads((runtime/'build-info.json').read_text())
    assert m.sha(runtime/'MNG_EvaluationV2_Data/Managed/MNG.Runtime.dll')==info['runtimeSha256']
    result=folder/'result.json';engine=EngineConfigurationChannel();engine.set_configuration_parameters(time_scale=20,target_frame_rate=-1)
    policies={team:Policy(candidate,seed+100000),1-team:Policy(opponent,seed+100001)}
    env=None;began=time.monotonic();counts=Counter();kinds=Counter();observations=0
    try:
        args=['-mngEvaluationSeed',str(seed),'-mngPolicyTeam',str(team),'-mngNeuralOpponent','true',
              '-mngEvaluationOutput',str(result),'-mngEvidenceDir',str(folder),'-mngRunId',Path(out).name,
              '-mngBuildSha',info['runtimeSha256'],'-mngFullTrace','true','-mngLifecycleTrace','true']
        if not legacy:args+=['-mngPSRecipients']
        args+=runtime_args()
        env=UnityEnvironment(str(runtime/'MNG_EvaluationV2.exe'),worker_id=slot,base_port=int(os.environ.get('MNG_PSR_EVAL_PORT','15500')),seed=seed,
                             no_graphics=True,side_channels=[engine],timeout_wait=90,log_folder=str(folder),additional_args=args)
        env.reset()
        with (folder/'decisions.jsonl').open('w') as trace:
            while not result.exists():
                if (m.EVIDENCE/'STOP').exists():
                    (folder/'saved-stop.json').write_text(json.dumps(dict(status='saved-stopped',observations=observations,trace='decisions.jsonl')))
                    raise InterruptedError('PS saved-stop requested during match')
                if time.monotonic()-began>600:raise TimeoutError('match exceeded 600 seconds')
                for name,spec in env.behavior_specs.items():
                    assert list(spec.action_spec.discrete_branches)==([6] if legacy else [6,5])
                    t=int(name.split('team=')[-1]);d,_=env.get_steps(name)
                    if not len(d):continue
                    obs=d.obs[0][0];valid=np.concatenate([~a[0] for a in d.action_mask])
                    if legacy:valid=np.r_[valid,[True,False,False,False,False]]
                    action,label=policies[t].act(obs,valid)
                    if t==team:
                        counts[tuple(action)]+=1;kinds[label['kind']]+=1;observations+=1
                        trace.write(json.dumps(dict(team=t,seed=seed,observation=obs.tolist(),valid=valid.tolist(),
                                                    action=action,label=label))+'\n')
                    env.set_actions(name,ActionTuple(discrete=np.array([action[:1] if legacy else action],dtype=np.int32)))
                env.step()
        r=json.loads(result.read_text());errors=PlayerLogGuard(folder).scan();assert not errors,errors
        gf=r['redScore'] if team==0 else r['navyScore'];ga=r['navyScore'] if team==0 else r['redScore']
        row=dict(index=idx,seed=seed,policyTeam=team,opponent=opponent,goalsFor=gf,goalsAgainst=ga,
                 win=int(gf>ga),draw=int(gf==ga),loss=int(gf<ga),score=1 if gf>ga else .5 if gf==ga else 0,
                 commands={str(k):v for k,v in counts.items()},teacherContexts=dict(kinds),observations=observations,
                 passStrikes=r['passStrikes'],intendedReceptions=r['intendedReceptions'],playerErrors=errors,
                 rawResult=r,wallSeconds=time.monotonic()-began)
        (folder/'verified.json').write_text(json.dumps(row,indent=2));return row
    finally:
        if env:env.close()

def main():
    p=argparse.ArgumentParser();p.add_argument('--candidate',default=str(m.PARENT));p.add_argument('--opponent',default=str(m.PARENT))
    p.add_argument('--pairs',type=int,default=2);p.add_argument('--seed',type=int,default=929910)
    p.add_argument('--parallel',type=int,default=4);p.add_argument('--output',required=True);p.add_argument('--legacy',action='store_true')
    a=p.parse_args();out=Path(a.output).resolve();out.mkdir(parents=True)
    identities={x:m.sha(x) for x in (a.candidate,a.opponent) if Path(x).is_file()}
    manifest=dict(vars(a),identities=identities,teacher=a.candidate=='teacher',FullExcluded=True,
                  runtime=json.loads((build()/'EvaluationV2/build-info.json').read_text()),extraArgs=runtime_args(),basePort=int(os.environ.get('MNG_PSR_EVAL_PORT','15500')),branchContract=[6] if a.legacy else [6,5])
    (out/'manifest.json').write_text(json.dumps(manifest,indent=2));slots=Queue()
    (out/'source').mkdir()
    for name in ('mng_psr_play.py','mng_psr_model.py'):
        (out/'source'/name).write_bytes((m.ROOT/'Tools'/name).read_bytes())
    for s in range(a.parallel):slots.put(s)
    def run(idx):
        slot=slots.get()
        try:return match((idx,slot,a.seed+idx//2,idx%2,a.candidate,a.opponent,out,a.legacy))
        finally:slots.put(slot)
    rows=[]
    with ThreadPoolExecutor(max_workers=a.parallel) as pool:
        for f in as_completed([pool.submit(run,i) for i in range(a.pairs*2)]):
            r=f.result();rows.append(r);print(f"MATCH {len(rows)}/{a.pairs*2} {r['goalsFor']}:{r['goalsAgainst']} pass={r['passStrikes']} receive={r['intendedReceptions']}",flush=True)
    for x,identity in identities.items():assert m.sha(x)==identity
    summary=summarize(rows)
    (out/'report.json').write_text(json.dumps(dict(summary=summary,matches=sorted(rows,key=lambda r:r['index'])),indent=2));print(summary,flush=True)

if __name__=='__main__':main()

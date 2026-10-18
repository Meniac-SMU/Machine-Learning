"""Paired before/after runtime evaluation of the unchanged provisional model; no training."""
import json
import argparse
import traceback
import re
from pathlib import Path
from concurrent.futures import ThreadPoolExecutor, as_completed
import numpy as np
from five_tactic_evaluate import FrozenPolicy, UnityEnvironment, ActionTuple, EngineConfigurationChannel, sha
from five_match_metrics import event_metrics, aggregate

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT/'Logs/Manager5v5/defensive-state-20261014/evaluation'
MODEL = ROOT/'results/Manager5v5/MNG5_V2-20261014-r002/MNG5_Manager_v2/MNG5_Manager_v2-505386.onnx'
EXPECTED_SHA = '5d5077dd51edef29b401e97a8c8d88b7c1a0375fb9ffb4ce36ad39fd5dd7d4b2'
RUNTIME_AFTER = 'tactics-v4-defensive-state'
CONTRACT_AFTER = 'MNG5-OBS676-A8-R1-M2-v3-T7MASK-T9STATE'
BUILDS = {'before': ROOT/'Builds/Manager5v5/p9-tactics-v3-20261014/Soccer5v5Learning.exe',
          'after': ROOT/'Builds/Manager5v5/defensive-state-learning-20261014/Soccer5v5Learning.exe'}

def write(path,value): path.write_text(json.dumps(value,ensure_ascii=False,indent=2),encoding='utf-8')

def game(variant, opponent, seed, side, index):
    folder=OUT/variant/opponent/f'match-{index:03d}'
    folder.mkdir(parents=True,exist_ok=False)
    report=dict(passed=False,variant=variant,opponent=opponent,seed=seed,side=side)
    env=None
    try:
        channel=EngineConfigurationChannel()
        channel.set_configuration_parameters(time_scale=100,quality_level=0,target_frame_rate=-1,capture_frame_rate=0)
        extra=['--five-p9-trace-dir',str(folder),'--five-execution-variant','all','-job-worker-count','1']
        if opponent=='rule': extra+=['--five-rule-team',str(1-side),'--five-rule-variant','strong']
        env=UnityEnvironment(file_name=str(BUILDS[variant]),seed=seed,worker_id=300+index,
            no_graphics=True,timeout_wait=120,side_channels=[channel],log_folder=str(folder),additional_args=extra)
        env.reset()
        names={int(n.rsplit('=',1)[1]):n for n in env.behavior_specs}
        assert set(names)==({side} if opponent=='rule' else {0,1})
        for spec in env.behavior_specs.values():
            assert spec.observation_specs[0].shape==(676,) and list(spec.action_spec.discrete_branches)==[8]
        policies={t:FrozenPolicy(str(MODEL),seed+(100000 if t==side else 200000)) for t in names}
        counts=np.zeros((2,8),dtype=int); masked=np.zeros(2,dtype=int); mass=np.zeros(2)
        reward={t:0 for t in names}; terminal={}; observations=0
        for _ in range(1000):
            for t,name in names.items():
                d,end=env.get_steps(name)
                if t in terminal: continue
                if len(end):
                    assert not end.interrupted[0]
                    reward[t]+=float(end.reward[0]); obs=end.obs[0][0]
                    assert obs[20]==1 and obs[17]==0
                    own,other=[round(float(x/(1-x))) for x in obs[15:17]]
                    assert reward[t]==own-other
                    terminal[t]=dict(own=own,other=other)
                elif len(d):
                    obs=d.obs[0][0]; assert np.isfinite(obs).all()
                    reward[t]+=float(d.reward[0]); policy=policies[t]
                    p=policy.evaluator.run(None,{'obs_0':obs[None].astype(np.float32),'action_masks':np.ones((1,8),np.float32)})[0][0].astype(float)
                    p/=p.sum()
                    blocked=d.action_mask[0][0] if d.action_mask is not None else np.zeros(8,dtype=bool)
                    assert not blocked.any() or (variant=='after' and np.flatnonzero(blocked).tolist()==[6])
                    if variant=='after': assert obs[10]==0, 'Decision requested during opposing keeper possession'
                    if blocked.any(): masked[t]+=1;mass[t]+=p[6]
                    p[blocked]=0;assert p.sum()>0;p/=p.sum()
                    action=int(policy.rng.choice(8,p=p));counts[t,action]+=1;observations+=1
                    env.set_actions(name,ActionTuple(discrete=np.array([[action]],dtype=np.int32)))
            if len(terminal)==len(names):break
            env.step()
        assert len(terminal)==len(names)
        process=env._process;env.close();env=None;assert process.poll()==0
        for log in folder.glob('Player*.log'):
            assert not re.search(r'^(?:[\w.]*Exception:|Assertion failed|\[MNG FATAL\])',log.read_text(errors='replace'),re.M)
        trace,=folder.glob('trace-*.jsonl')
        rows=[json.loads(s) for s in trace.read_text(encoding='utf-8-sig').splitlines()]
        expected=RUNTIME_AFTER if variant=='after' else 'tactics-v3-expansion'
        assert all(r['playerRuntime']==expected and r['executionFeatures']==[True,True,True] for r in rows)
        expected_contract=CONTRACT_AFTER if variant=='after' else 'MNG5-OBS676-A8-R1-M2-v2'
        assert all(r['contract']==expected_contract for r in rows)
        assert all(r['boundaryCorrections']==0 for r in rows)
        terminals=[r for r in rows if r['kind']=='terminal'];assert len(terminals)==2
        primary=next(r for r in terminals if r['team']==0)
        assert all(r['elapsed']>=300 for r in terminals)
        assert sum(r['reward'] for r in terminals)==0
        metrics=event_metrics(primary['matchEvents'])
        assert [m['goals'] for m in metrics]==[primary['redScore'],primary['navyScore']]
        assert sum(m['shots'] for m in metrics)==primary['shots']
        assert sum(m['passAttempts'] for m in metrics)==primary['passAttempts']
        all_counts=np.zeros((2,8),dtype=int);streak=[0,0];max_streak=[0,0]
        for row in rows:
            if row['kind']!='decision' or row['episode']!=primary['episode']:continue
            t,a=row['team'],row['action'];all_counts[t,a]+=1
            streak[t]=streak[t]+1 if a==6 else 0;max_streak[t]=max(max_streak[t],streak[t])
            if variant=='after':
                assert streak[t]<=3
                assert row['observation'][10]==0
        entries=primary.get('defensiveFormationEntries',[0,0]);steps=primary.get('defensiveFormationSteps',[0,0])
        report.update(passed=True,score=terminal[side],metrics=metrics,counts=all_counts.tolist(),masked=masked.tolist(),
            excludedT7ProbabilityMass=mass.tolist(),maxT7Streak=max_streak,t9Entries=entries,t9Seconds=[n*.01 for n in steps],
            boundaryCorrections=0,playerRuntime=expected,terminalElapsed=primary['elapsed'])
    except Exception:
        report['error']=traceback.format_exc();raise
    finally:
        if env is not None:env.close()
        write(folder/'result.json',report)
    return report

def main():
    global OUT, RUNTIME_AFTER
    parser=argparse.ArgumentParser()
    parser.add_argument('--variants',nargs='+',choices=['before','after'],default=['before','after'])
    parser.add_argument('--revision2',action='store_true')
    args=parser.parse_args()
    if args.revision2:
        OUT=OUT.parent/'evaluation-r2'
        BUILDS['after']=ROOT/'Builds/Manager5v5/defensive-state-learning-r2-20261014/Soccer5v5Learning.exe'
        RUNTIME_AFTER='tactics-v4-defensive-state-r2'
    assert sha(MODEL)==EXPECTED_SHA
    OUT.mkdir(parents=True,exist_ok=True)
    protocol=dict(model=str(MODEL),modelSha256=sha(MODEL),seeds=list(range(65000,65012)),
        games=96,seconds=300,timeScale=100,workers=12,variants=['before','after'],opponents=['rule','self'],runtimeAfter=RUNTIME_AFTER,
        trainingStarted=False,mask='Only T7 is removed after three consecutive selections; remaining learned probabilities renormalized.',
        limitation='Frozen old policy under changed rules; runtime effect, not evidence of learning or general skill gain.')
    if (OUT/'protocol.json').exists(): assert json.loads((OUT/'protocol.json').read_text(encoding='utf-8'))==protocol
    else: write(OUT/'protocol.json',protocol)
    for variant in args.variants:
        for opponent in ('rule','self'):
            rows=[]
            with ThreadPoolExecutor(max_workers=12) as pool:
                futures=[pool.submit(game,variant,opponent,65000+i,side,i*2+side) for i in range(12) for side in (0,1)]
                for future in as_completed(futures):
                    rows.append(future.result());print(f'{variant} {opponent} {len(rows)}/24',flush=True)
            rows.sort(key=lambda r:(r['seed'],r['side']))
            delta=[r['score']['own']-r['score']['other'] for r in rows]
            counts=np.sum([r['counts'][r['side']] for r in rows],axis=0)
            summary=dict(passed=True,games=24,wins=sum(d>0 for d in delta),draws=sum(d==0 for d in delta),losses=sum(d<0 for d in delta),
                goalsFor=sum(r['score']['own'] for r in rows),goalsAgainst=sum(r['score']['other'] for r in rows),
                counts=counts.tolist(),ratios=(counts/counts.sum()).tolist(),
                metrics=aggregate([r['metrics'][r['side']] for r in rows]),
                t9Entries=sum(r['t9Entries'][r['side']] for r in rows),t9Seconds=sum(r['t9Seconds'][r['side']] for r in rows),
                maxT7Streak=max(max(r['maxT7Streak']) for r in rows),masked=sum(r['masked'][r['side']] for r in rows))
            write(OUT/variant/opponent/'summary.json',summary)
    assert sha(MODEL)==EXPECTED_SHA
    if all((OUT/v/o/'summary.json').exists() for v in BUILDS for o in ('rule','self')):
        write(OUT/'completion.json',dict(passed=True,games=96,trainingStarted=False,modelSha256=sha(MODEL)))

if __name__=='__main__':main()

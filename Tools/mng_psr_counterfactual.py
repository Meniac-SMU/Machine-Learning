"""Finite-horizon matched action experiments for better receiver demonstrations.

Identical original-parent action streams until an anchor; one intervention is
allowed ONLY in this demonstration collector, never PPO or frozen evaluation.
"""
import argparse
from concurrent.futures import ThreadPoolExecutor,as_completed
from collections import Counter
from pathlib import Path
import json
import time
import numpy as np
from mng_psr_play import Policy,build,runtime_args,m
from mlagents_envs.environment import UnityEnvironment
from mlagents_envs.base_env import ActionTuple
from mlagents_envs.side_channel.engine_configuration_channel import EngineConfigurationChannel
from mng_player_log_guard import PlayerLogGuard

def trial(seed,slot,recipient,folder,anchor=None):
    if (m.EVIDENCE/'STOP').exists():raise InterruptedError('PS saved-stop requested before trial')
    folder.mkdir();runtime=build()/'EvaluationV2';channel=EngineConfigurationChannel()
    channel.set_configuration_parameters(time_scale=20,target_frame_rate=-1)
    policies={t:Policy(m.PARENT,seed+100000+t) for t in (0,1)}
    args=['-mngPSRecipients','-mngPSCurriculum','1','-mngEvaluationSeed',str(seed),'-mngPolicyTeam','0',
        '-mngNeuralOpponent','true','-mngEvaluationOutput',str(folder/'unused-300s-result.json'),
        '-mngEvidenceDir',str(folder),'-mngLifecycleTrace','true','-mngFullTrace','false']+runtime_args()
    env=None;indices=[0,0];chosen=None;frames=[];started=time.monotonic();result=None
    try:
        env=UnityEnvironment(str(runtime/'MNG_EvaluationV2.exe'),base_port=20500,worker_id=slot,seed=seed,
            no_graphics=True,side_channels=[channel],timeout_wait=90,log_folder=str(folder),additional_args=args)
        env.reset()
        while True:
            if (m.EVIDENCE/'STOP').exists():
                (folder/'saved-stop.json').write_text(json.dumps(dict(status='saved-stopped',anchor=chosen,decisions=indices)))
                (folder/'frames.jsonl').write_text(''.join(json.dumps(f)+'\n' for f in frames))
                raise InterruptedError('PS saved-stop requested during trial')
            if time.monotonic()-started>180:raise TimeoutError('counterfactual trial')
            done=False
            for name in env.behavior_specs:
                t=int(name.split('team=')[-1]);d,_=env.get_steps(name)
                if not len(d):continue
                o=d.obs[0][0];mask=np.concatenate([~x[0] for x in d.action_mask]);elapsed=float(o[112])*300
                action,label=policies[t].act(o,mask);index=indices[t];indices[t]+=1
                if anchor is None and chosen is None and elapsed>=.5 and elapsed<=15 and o[243]<.5 and mask[7:].any() and action[0]!=1:
                    chosen=dict(team=t,index=index,time=elapsed,observation=o.tolist(),mask=mask.tolist(),baselineAction=action,
                                geometry=label,seed=seed)
                elif anchor is not None and chosen is None and t==anchor['team'] and index==anchor['index']:
                    assert np.array_equal(o,np.array(anchor['observation'],dtype=np.float32)), 'Counterfactual prefix differs'
                    assert mask.tolist()==anchor['mask'];chosen=anchor
                    assert recipient in range(1,5) and mask[6+recipient]
                    action=[1,recipient]
                if chosen and t==chosen['team']:
                    frames.append(dict(time=elapsed,observation=o.tolist(),action=action))
                    if elapsed>=chosen['time']+12:done=True;result=o.copy()
                if chosen is None and elapsed>15:done=True
                env.set_actions(name,ActionTuple(discrete=np.array([action],dtype=np.int32)))
            if done:break
            env.step()
    finally:
        if env:env.close()
    if chosen is None:
        row=dict(seed=seed,anchor=None,status='no eligible anchor');(folder/'trial.json').write_text(json.dumps(row));return row
    assert result is not None
    errors=PlayerLogGuard(folder).scan();assert not errors,errors
    (folder/'frames.jsonl').write_text(''.join(json.dumps(f)+'\n' for f in frames))
    events=[json.loads(x) for x in next(folder.glob('lifecycle-*.jsonl')).read_text().splitlines()]
    ps=[x for x in events if x['team']==chosen['team'] and x['kind']=='PolicyDecision']
    policy=ps[chosen['index']];assert policy['rawCommand']==(1 if recipient>0 else chosen['baselineAction'][0])
    strikes=[x for x in events if x['team']==chosen['team'] and x['kind']=='Strike:Pass' and x['parentCommandId']==policy['parentCommandId']]
    intended=recipient-1;received=False
    if strikes:
        assert len(strikes)==1
        received=any(f['time']>strikes[0]['time'] and f['time']<=strikes[0]['time']+2.5
            and f['observation'][101]>.5 and int(np.argmax(f['observation'][103:112]))-1==intended and f['observation'][243]<.5 for f in frames)
    scored=5*(float(result[114])-chosen['observation'][114])
    possession=float(result[101]-result[102]) if result[243]<.5 else 0.
    depth=float(result[96])*62 if result[243]<.5 else 0.
    pressure=0.
    if result[101]>.5 and result[243]<.5:
        players=result[:96].reshape(8,12);ball=result[96:98]*m.SCALE
        pressure=min(8.,float(np.linalg.norm(players[4:,1:3]*m.SCALE-ball,axis=1).min()))
    shots=sum(x['kind']=='Strike:Shot' and x['team']==chosen['team'] and chosen['time']<x['time']<=chosen['time']+12 for x in events)
    utility=2*scored+.3*possession+.003*depth+.01*pressure+.08*shots
    row=dict(seed=seed,anchor=chosen,recipient=recipient,prefixExact=True,struck=bool(strikes),received=received,
             utility=utility,goalMargin=scored,possession=possession,depth=depth,pressure=pressure,shots=shots,
             environmentDecisions=sum(indices),horizonSeconds=12,status='completed')
    (folder/'trial.json').write_text(json.dumps(row,indent=2));return row

def main():
    p=argparse.ArgumentParser();p.add_argument('--seeds',type=int,default=60);p.add_argument('--parallel',type=int,default=4)
    p.add_argument('--output',required=True);p.add_argument('--start',type=int,default=0);a=p.parse_args()
    assert a.start>=0 and a.start%4==0,'Preserve fixed seed-to-worker lanes'
    # Port/worker identity participates in reset jitter. Keep the original four
    # seed-to-worker lanes when comparing the changed execution skill.
    a.requestedParallel=a.parallel;a.parallel=min(a.parallel,4)
    out=Path(a.output).resolve();out.mkdir()
    (out/'manifest.json').write_text(json.dumps(dict(arguments=vars(a),runtime=str(build()),extraArgs=runtime_args(),
        runtimeSha=m.sha(build()/'EvaluationV2/MNG_EvaluationV2_Data/Managed/MNG.Runtime.dll'),
        parentSha=m.sha(m.PARENT),sources={n:m.sha(m.ROOT/'Tools'/n) for n in
        ('mng_psr_counterfactual.py','mng_psr_play.py','mng_psr_model.py')},FullExcluded=True),indent=2))
    (out/'source').mkdir()
    for n in ('mng_psr_counterfactual.py','mng_psr_play.py','mng_psr_model.py'):
        (out/'source'/n).write_bytes((m.ROOT/'Tools'/n).read_bytes())
    def experiment(i):
        seed=932100+a.start+i;slot=i%a.parallel;folder=out/f'seed-{seed}';folder.mkdir()
        base=trial(seed,slot,0,folder/'baseline')
        if base['anchor'] is None:return base
        options=[]
        for rec in range(1,5):
            if base['anchor']['mask'][6+rec]:options.append(trial(seed,slot,rec,folder/f'recipient-{rec}',base['anchor']))
        eligible=[r for r in options if r['struck'] and r['received'] and r['utility']>=base['utility']+.15]
        best=max(eligible,key=lambda r:r['utility']) if eligible else None
        row=dict(seed=seed,split='validation' if seed%5==0 else 'train',anchor=base['anchor'],baselineUtility=base['utility'],
            preferredRecipient=best['recipient'] if best else 0,preferredUtility=best['utility'] if best else base['utility'],
            variants=options,environmentDecisions=base['environmentDecisions']+sum(x['environmentDecisions'] for x in options))
        (folder/'comparison.json').write_text(json.dumps(row,indent=2));return row
    rows=[]
    # One sequential lane per socket slot prevents concurrent port reuse.
    def lane(slot):
        result=[]
        for i in range(slot,a.seeds,a.parallel):
            r=experiment(i);result.append(r);print(f"COUNTERFACTUAL seed={r['seed']} preferred={r.get('preferredRecipient')} delta={r.get('preferredUtility',0)-r.get('baselineUtility',0):.3f}",flush=True)
        return result
    with ThreadPoolExecutor(max_workers=a.parallel) as pool:
        for f in as_completed([pool.submit(lane,s) for s in range(a.parallel)]):rows+=f.result()
    (out/'examples.jsonl').write_text(''.join(json.dumps(r)+'\n' for r in sorted(rows,key=lambda r:r['seed'])))
    summary=dict(seeds=len(rows),positives=sum(r.get('preferredRecipient',0)>0 for r in rows),
        keeperPositives=sum(r.get('preferredRecipient',0)==1 for r in rows),
        environmentDecisions=sum(r.get('environmentDecisions',0) for r in rows),ppoTrainingSteps=0,
        utility='2*goalMargin + .3*possession + .003*attackingDepth + .01*space(capped8m) + .08*shots; 12s horizon',
        limitations='Finite-horizon utility, not proof of match victory. Train/validation split by seed. Physical strike+reception and +.15 advantage required.',
        FullExcluded=True,parentSha=m.sha(m.PARENT))
    (out/'report.json').write_text(json.dumps(summary,indent=2));print(summary,flush=True)

if __name__=='__main__':main()

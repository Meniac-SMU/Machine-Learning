import argparse
import hashlib
import json
import math
from datetime import datetime
from pathlib import Path
import numpy as np
import torch
from tensorboard.backend.event_processing.event_accumulator import EventAccumulator

ROOT=Path(__file__).resolve().parent.parent

def finite(value):
    if isinstance(value,torch.Tensor): assert torch.isfinite(value).all(), 'Nonfinite tensor'
    elif isinstance(value,dict):
        for child in value.values(): finite(child)
    elif isinstance(value,(list,tuple)):
        for child in value: finite(child)
    elif isinstance(value,float): assert math.isfinite(value), 'Nonfinite scalar'

def inspect(run):
    evidence=ROOT/'Logs/MNG-Rebuild'/run
    manifest=json.loads((evidence/'manifest.json').read_text(encoding='utf-8-sig'))
    reports=[]
    for segment in sorted(evidence.glob('to-*')):
        boots=set(); episodes=[]; latest={}; decisions=0; strikes=0; seen=set(); kicks=set()
        commands=np.zeros((2,6),dtype=np.int64)
        for path in (segment/'workers').glob('*.jsonl'):
            for line in path.read_text(encoding='utf-8-sig').splitlines():
                try: row=json.loads(line)
                except json.JSONDecodeError: continue # incomplete final write during a live read
                if row.get('event')=='worker-start': boots.add(row['worker'])
                elif row.get('event')=='episode':
                    assert row['actionIntegrityPassed']; episodes.append(row)
                elif 'schema' in row:
                    assert row['schema']==manifest['schema'] and row['build']==manifest['runtimeSha']
                    finite(row); latest[row['worker']]=row
                    for entry in row.get('trace',[]):
                        key=(row['worker'],entry['episode'],entry['sequence'])
                        if key in seen: continue
                        seen.add(key)
                        if entry['kind']=='PolicyDecision':
                            assert entry['rawCommand']==entry['effectiveCommand']; decisions+=1
                            commands[entry['team'],entry['rawCommand']]+=1
                        if entry['kind'].startswith('Strike:'):
                            k=(row['worker'],entry['episode'],entry['kickId'])
                            assert k not in kicks and entry['taskId']>0 and entry['parentCommandId']>0 and entry['source']
                            kicks.add(k); strikes+=1
        assert len(boots)==32, 'Missing training workers'
        totals=np.sum([r.get('shotStrikes',[0,0]) for r in latest.values()],axis=0).tolist() if latest else [0,0]
        reports.append(dict(segment=segment.name,workers=len(boots),completedEpisodes=len(episodes),
            redGoals=sum(r['redScore'] for r in episodes),navyGoals=sum(r['navyScore'] for r in episodes),
            redWins=sum(r['redScore']>r['navyScore'] for r in episodes),draws=sum(r['redScore']==r['navyScore'] for r in episodes),
            shotStrikes=totals,sampledDecisions=decisions,sampledStrikes=strikes,
            sampledCommands=commands.tolist(),
            observedRecoveries=np.sum([r.get('observedRecoveries',[0,0]) for r in latest.values()],axis=0).tolist() if latest else [],
            taskResults=np.sum([r['taskResults'] for r in latest.values()],axis=0).tolist() if latest else [],
            sourceTicks=np.sum([r['executionSourceTicks'] for r in latest.values()],axis=0).tolist() if latest else [],
            actionIntegrity=True))
    policy=ROOT/'results'/run/'MNG_ManagerV2'
    checkpoint=torch.load(policy/'checkpoint.pt',map_location='cpu',weights_only=False)
    finite(checkpoint)
    step=int(checkpoint['global_step']['_GlobalSteps__global_step'])
    state=checkpoint['Optimizer:value_optimizer']['state']
    updates=max((float(v['step']) for v in state.values()),default=0)
    accumulator=EventAccumulator(str(policy)); accumulator.Reload()
    scalars={}
    for tag in accumulator.Tags()['scalars']:
        values=accumulator.Scalars(tag)
        assert all(math.isfinite(v.value) for v in values)
        scalars[tag]=dict(count=len(values),lastStep=values[-1].step,last=values[-1].value,
            min=min(x.value for x in values),max=max(x.value for x in values),
            lastTenMean=float(np.mean([x.value for x in values[-10:]])))
    pool_report=None
    if (policy/'mng-v2-pool.pt').exists():
        pool=torch.load(policy/'mng-v2-pool.pt',map_location='cpu',weights_only=False)
        finite(pool); assert pool['step']==step, 'Pool/checkpoint mismatch'
        events=[json.loads(line) for line in (policy/'mng-v2-pool.jsonl').read_text().splitlines()]
        swaps=[e for e in events if e['event']=='swap']
        restored=[e for e in events if e['event']=='restored']
        for index,event in enumerate(events):
            if event['event']!='restored': continue
            saved=next(e for e in reversed(events[:index]) if e['event']=='saved')
            for key in ('step','learning_team','ghost_step','last_save','last_swap','last_team_change','pinned'):
                assert event[key]==saved[key], ('Pool restore mismatch',key)
        mixed=0; episode_count=0
        for segment in evidence.glob('to-*'):
            for path in (segment/'workers').glob('worker-*.jsonl'):
                previous=None
                for line in path.read_text().splitlines():
                    row=json.loads(line)
                    now=datetime.fromisoformat(row['utc'][:26]+'+00:00').timestamp()
                    if row['event']=='episode':
                        episode_count+=1
                        if previous is not None and any(previous<=s.get('utcSeconds',0)<=now for s in swaps): mixed+=1
                    previous=now
        pool_report=dict(step=pool['step'],pinned=len(pool['pinned']),recentSlots=len(pool['ghost']['policy_snapshots']),
            restoredSegments=len(restored),swaps=len(swaps),
            categories={name:sum(s['category']==name for s in swaps) for name in ('latest','recent','pinned')},
            learningTeams=sorted({s['learning_team'] for s in swaps}),
            completedEpisodes=episode_count,episodesOverlappingSwapEnqueue=mixed,
            timingScope='Swap enqueue UTC overlap; exact per-worker delivery time unavailable. Training matches are not frozen evaluations.')
    return dict(run=run,manifest=manifest,checkpointStep=step,optimizerUpdates=updates,pool=pool_report,
        checkpointSha=hashlib.sha256((policy/'checkpoint.pt').read_bytes()).hexdigest(),segments=reports,scalars=scalars,finite=True)

if __name__=='__main__':
    p=argparse.ArgumentParser(); p.add_argument('run'); p.add_argument('--output',required=True); a=p.parse_args()
    result=inspect(a.run)
    path=Path(a.output); assert not path.exists(), 'Preserve prior inspection'
    path.write_text(json.dumps(result,indent=2)); print(json.dumps(result,indent=2))

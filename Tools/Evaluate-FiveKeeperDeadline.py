"""Frozen-model validation of T7 two-selection cooldown, T9 avoidance and GK six seconds."""
import importlib.util
import json
import hashlib
import shutil
import argparse
from pathlib import Path
from concurrent.futures import ThreadPoolExecutor, as_completed
import numpy as np

ROOT=Path(__file__).resolve().parent.parent
BASE=ROOT/'Logs/Manager5v5/defensive-state-v5-20261014'
OUT=BASE/'evaluation'
spec=importlib.util.spec_from_file_location('defensive_eval',ROOT/'Tools/Evaluate-FiveDefensiveState.py')
ev=importlib.util.module_from_spec(spec);spec.loader.exec_module(ev)

def write(path,data):path.write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
def read(path):return json.loads(path.read_text(encoding='utf-8-sig'))

def audit(folder):
    trace,=folder.glob('trace-*.jsonl')
    rows=[json.loads(line) for line in trace.read_text(encoding='utf-8-sig').splitlines()]
    terminal=next(r for r in rows if r['kind']=='terminal' and r['team']==0)
    streak=[0,0];blocked=[0,0];blocks=0
    for row in rows:
        if row['kind']!='decision' or row['episode']!=terminal['episode']:continue
        t,a=row['team'],row['action']
        if a==6:
            assert blocked[t]==0, 'T7 selected during the two-command cooldown'
            streak[t]+=1
            assert streak[t]<=3
            if streak[t]==3:blocked[t]=2;blocks+=1
        else:
            streak[t]=0;blocked[t]=max(0,blocked[t]-1)
        assert row['recoverBlockedSelections']==blocked[t]
        assert row['observation'][10]==0
    events=terminal['matchEvents'];start=None;owner=-1;durations=[];passes=0;deadline=0;avoid=0;enters=0;exits=0
    active=set();goalEnds=0
    for event in events:
        kind=event['kind']
        if start is not None and kind in ('possession','release','goal'):
            duration=event['time']-start;durations.append(duration)
            assert duration<=6.0001, f'Keeper ownership exceeded six seconds: {duration}'
            if kind=='release' and event['reason']=='kick':
                assert any(e['kind']=='pass-kicked' and e['actor']==owner and abs(e['time']-event['time'])<.0001 for e in events), 'Unaddressed GK clearance'
            start=None
        if kind=='possession':
            owner=event['actor']
            if owner in (0,5):start=event['time']
        elif kind=='release':owner=-1
        elif kind=='pass-kicked' and event['actor'] in (0,5):
            assert event['other']//5==event['actor']//5 and event['other']%5!=0
            passes+=1
        elif kind=='keeper-deadline-pass':deadline+=1
        elif kind=='defensive-formation-avoid':avoid+=1
        elif kind=='defensive-formation-enter':
            assert owner in (0,5) and owner//5!=event['actor'];enters+=1;active.add(event['actor'])
        elif kind=='defensive-formation-exit':
            assert owner not in (0,5) or owner//5==event['actor'];exits+=1;active.discard(event['actor'])
        elif kind=='goal':goalEnds+=len(active);active.clear()
    if start is not None:
        durations.append(terminal['elapsed']-start);assert durations[-1]<=6.0001
    assert terminal['boundaryCorrections']==0
    assert enters==exits+goalEnds+len(active)
    result=dict(passed=True,t7CooldownTriggers=blocks,keeperPossessions=len(durations),
        maxKeeperHoldSeconds=max(durations,default=0),keeperPasses=passes,deadlinePasses=deadline,
        t9AvoidanceEvents=avoid,t9Entries=enters,t9Exits=exits,t9EndedByGoal=goalEnds,t9EndedByMatch=len(active),keeperHoldsOverSixSeconds=0)
    write(folder/'rules-audit.json',result)
    return result

def episode_counts(folder):
    trace,=folder.glob('trace-*.jsonl')
    rows=[json.loads(s) for s in trace.read_text(encoding='utf-8-sig').splitlines()]
    terminal=next(r for r in rows if r['kind']=='terminal' and r['team']==0)
    counts=np.zeros((2,8),dtype=int)
    for r in rows:
        if r['kind']=='decision' and r['episode']==terminal['episode']:counts[r['team'],r['action']]+=1
    write(folder/'command-counts-episode-only.json',dict(episode=terminal['episode'],counts=counts.tolist(),
        note='Only the completed 300-second match. Original result.json is preserved.'))
    return counts

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--resume',action='store_true');parser.add_argument('--audit-only',action='store_true');args=parser.parse_args()
    if args.audit_only:
        assert read(OUT/'completion.json')['passed']
        for variant in ('before','after'):
            for opponent in ('rule','self'):
                for folder in (OUT/variant/opponent).glob('match-*'):episode_counts(folder)
        for opponent in ('rule','self'):
            audits=[audit(folder) for folder in sorted((OUT/'after'/opponent).glob('match-*'))]
            assert len(audits)==24
            s=read(OUT/'after'/opponent/'summary.json')
            s['rulesAudit']={k:(max(a[k] for a in audits) if k=='maxKeeperHoldSeconds' else sum(a[k] for a in audits)) for k in audits[0] if k!='passed'}
            write(OUT/'after'/opponent/'summary.json',s)
        print('48 completed matches re-audited; no matches or training started.')
        return
    assert not OUT.exists() or args.resume, 'Preserve previous evaluation evidence'
    assert not (OUT/'completion.json').exists()
    OUT.mkdir(parents=True,exist_ok=args.resume)
    baseline=ROOT/'Logs/Manager5v5/defensive-state-20261014/evaluation-r2/after'
    if not args.resume:shutil.copytree(baseline,OUT/'before')
    for original in baseline.rglob('*'):
        if original.is_file():assert hashlib.sha256(original.read_bytes()).digest()==hashlib.sha256((OUT/'before'/original.relative_to(baseline)).read_bytes()).digest()
    ev.OUT=OUT;ev.RUNTIME_AFTER='tactics-v5-keeper-six-second';ev.CONTRACT_AFTER='MNG5-OBS676-A8-R1-M2-v4-T7COOLDOWN2-GK6'
    ev.BUILDS['after']=ROOT/'Builds/Manager5v5/keeper-six-second-20261014/Soccer5v5Learning.exe'
    assert ev.sha(ev.MODEL)==ev.EXPECTED_SHA
    protocol=dict(model=str(ev.MODEL),modelSha256=ev.EXPECTED_SHA,trainingStarted=False,
        seconds=300,timeScale=100,workers=12,seeds=list(range(65000,65012)),opponents=['rule','self'],
        gamesExecuted=48,gamesCompared=96,baseline=str(baseline),baselineReused=True,
        runtimeBefore='tactics-v4-defensive-state-r2',runtimeAfter=ev.RUNTIME_AFTER,
        criteria=['T7 fourth and fifth choices blocked; sixth available','T9 triggered only by opposing GK',
                  'GK ownership <= 6 seconds; all intentional distributions addressed to teammates'],
        limitation='Paired regression seeds, frozen old weights; not independent quality holdout or training.')
    if args.resume:assert read(OUT/'protocol.json')==protocol
    else:write(OUT/'protocol.json',protocol)
    # Keep copied baseline files unchanged; publish episode-only command ratios separately.
    for opponent in ('rule','self'):
        s=read(OUT/'before'/opponent/'summary.json');counts=np.zeros(8,dtype=int)
        for folder in (OUT/'before'/opponent).glob('match-*'):
            r=read(folder/'result.json');counts+=episode_counts(folder)[r['side']]
        s.update(counts=counts.tolist(),ratios=(counts/counts.sum()).tolist())
        write(OUT/'before'/opponent/'summary-episode-only.json',s)
    for opponent in ('rule','self'):
        rows=[];audits=[]
        jobs=[]
        for i in range(12):
            for side in (0,1):
                folder=OUT/'after'/opponent/f'match-{2*i+side:03d}'
                if (folder/'result.json').exists():
                    r=read(folder/'result.json');assert r['passed'];r['counts']=episode_counts(folder).tolist()
                    rows.append(r);audits.append(audit(folder))
                else:
                    assert not folder.exists(), 'Partial game requires a new preserved run'
                    jobs.append((i,side))
        with ThreadPoolExecutor(max_workers=12) as pool:
            futures=[pool.submit(ev.game,'after',opponent,65000+i,side,2*i+side) for i,side in jobs]
            for future in as_completed(futures):
                result=future.result();rows.append(result)
                audits.append(audit(OUT/'after'/opponent/f'match-{2*(result["seed"]-65000)+result["side"]:03d}'))
                print(f'{opponent} {len(rows)}/24',flush=True)
        rows.sort(key=lambda r:(r['seed'],r['side']))
        deltas=[r['score']['own']-r['score']['other'] for r in rows]
        counts=np.sum([r['counts'][r['side']] for r in rows],axis=0)
        summary=dict(passed=True,games=24,wins=sum(d>0 for d in deltas),draws=sum(d==0 for d in deltas),losses=sum(d<0 for d in deltas),
            goalsFor=sum(r['score']['own'] for r in rows),goalsAgainst=sum(r['score']['other'] for r in rows),
            counts=counts.tolist(),ratios=(counts/counts.sum()).tolist(),metrics=ev.aggregate([r['metrics'][r['side']] for r in rows]),
            masked=sum(r['masked'][r['side']] for r in rows),maxT7Streak=max(max(r['maxT7Streak']) for r in rows),
            t9Entries=sum(r['t9Entries'][r['side']] for r in rows),t9Seconds=sum(r['t9Seconds'][r['side']] for r in rows),
            rulesAudit={k:(max(a[k] for a in audits) if k=='maxKeeperHoldSeconds' else sum(a[k] for a in audits)) for k in audits[0] if k!='passed'})
        write(OUT/'after'/opponent/'summary.json',summary)
    assert ev.sha(ev.MODEL)==ev.EXPECTED_SHA
    write(OUT/'completion.json',dict(passed=True,gamesCompared=96,gamesExecuted=48,trainingStarted=False,modelSha256=ev.EXPECTED_SHA))

if __name__=='__main__':main()

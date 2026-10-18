"""Compare every command in the same physical situations; no learned policy or weighted score."""
import argparse, glob, json, statistics
from pathlib import Path

OBJECTIVES = {
    'central-opening': ('earlyMaxOwnAdvance', 1),
    'rear-pressure-far-receiver': ('earlyMaxOwnAdvance', 1),
    'wide-escape': ('earlyMaxOwnAdvance', 1),
    'deep-goal-block': ('ownGoals', 1),
    'wide-trap-window': ('ownPossession', 1),
    'distant-receiver-denial': ('enemyReceptions', -1),
    'nearby-loose-ball': ('earlyPossession', 1),
    'central-goal-threat': ('enemyGoals', -1),
    'central-pressure-open-wide-lanes': ('earlyMaxOwnAdvance', 1),
    'approach-to-low-block': ('ownGoals', 1),
    'cutback-interception-window': ('enemyGoals', -1),
    'split-distant-receivers': ('enemyReceptions', -1),
    'off-lane-distant-receiver-coverage': ('enemyReceptions', -1),
    'wide-transition-to-attacking-half': ('earlyMaxOwnAdvance', 1),
    'wide-direct-exit': ('enemyReceptions', -1),
    'prepared-pass-goal-threat': ('enemyGoals', -1),
}
METRICS = ['ownGoals','enemyGoals','earlyMaxOwnAdvance','earlyPossession','ownPossession',
           'enemyPossession','ownReceptions','enemyReceptions','ownShots','enemyShots','minimumMeanStamina']

def review(files):
    rows=[]
    for file in files:
        data=json.loads(Path(file).read_text(encoding='utf-8-sig'))['trials']
        if len(data)!=512:
            raise ValueError(f'Incomplete situation batch: {file}: {len(data)}')
        rows.extend(data)
    seeds=sorted({r['seed'] for r in rows})
    expected=len(seeds)*2
    contexts=[]
    for scenario,(metric,direction) in OBJECTIVES.items():
        for continuation in ['fixed-twelve-seconds','rules-after-three-seconds']:
            samples=[]
            for command in range(8):
                subset={ (r['seed'],r['mirror']):r for r in rows if r['scenario']==scenario and r['continuation']==continuation and r['candidate']==command }
                if len(subset)!=expected: raise ValueError('Missing or duplicate samples')
                samples.append(subset)
            values=[{'command':i,**{m:statistics.mean(r[m] for r in group.values()) for m in METRICS}} for i,group in enumerate(samples)]
            pairs=[]
            for command in range(8):
                for alternative in range(8):
                    if command==alternative: continue
                    differences=[direction*(samples[command][k][metric]-samples[alternative][k][metric]) for k in samples[command]]
                    seed_diffs=[statistics.mean(direction*(samples[command][s,m][metric]-samples[alternative][s,m][metric]) for m in range(2)) for s in seeds]
                    pairs.append({'command':command,'alternative':alternative,'difference':statistics.mean(differences),
                                  'favourable':sum(d>.0001 for d in differences),'tied':sum(abs(d)<=.0001 for d in differences),'unfavourable':sum(d<-.0001 for d in differences),
                                  'seedMeanFavourable':sum(d>.0001 for d in seed_diffs),'seedMeanUnfavourable':sum(d<-.0001 for d in seed_diffs)})
            contexts.append({'scenario':scenario,'continuation':continuation,'metric':metric,'direction':direction,'values':values,'pairs':pairs})
    dominance=[]
    for command in range(8):
        for alternative in range(8):
            if command==alternative: continue
            ds=[(c['values'][command][c['metric']]-c['values'][alternative][c['metric']])*c['direction'] for c in contexts]
            dominance.append({'command':command,'alternative':alternative,'better':sum(d>.0001 for d in ds),'worse':sum(d<-.0001 for d in ds),'tied':sum(abs(d)<=.0001 for d in ds)})
    return {'files':files,'trials':len(rows),'seeds':seeds,'physicalCollectionValid':all(r['corrections']==0 and r['elapsed']>=12 for r in rows),
            'interpretation':'Exploratory finite-context comparisons, seed averages cluster mirror pairs. Not a global balance pass.',
            'trainingStarted':False,'contexts':contexts,'dominance':dominance}

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--glob',required=True);p.add_argument('--output',required=True)
    a=p.parse_args();files=sorted(glob.glob(a.glob));out=Path(a.output)
    if not files: raise ValueError('No inputs')
    if out.exists(): raise ValueError('Preserve previous reports')
    report=review(files);out.write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
    print(f"trials={report['trials']} seeds={report['seeds']} physical={report['physicalCollectionValid']}")
    for c in report['contexts']:
        print(c['scenario'],c['continuation'],c['metric'], ' '.join(f"T{v['command']+1}={v[c['metric']]:.3f}" for v in c['values']))
    print('Observed universal mean dominators:',[d for d in report['dominance'] if d['worse']==0 and d['better']>0])

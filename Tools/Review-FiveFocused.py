import argparse,glob,json,statistics
from pathlib import Path

p=argparse.ArgumentParser();p.add_argument('--glob',required=True);p.add_argument('--output',required=True)
a=p.parse_args();files=sorted(glob.glob(a.glob));rows=[]
if not files: raise ValueError('No inputs')
for f in files:
    r=json.loads(Path(f).read_text(encoding='utf-8-sig'))['trials']
    if len(r)!=96:raise ValueError(f'Incomplete focused batch: {f}')
    rows+=r
seeds=sorted({r['seed'] for r in rows});contexts=[]
for scenario in ['short-wide-outlet','wide-carrier-switch','wide-trap-with-deep-outlet']:
    for continuation in ['fixed-twelve-seconds','rules-after-three-seconds']:
        values=[]
        for c in range(8):
            rs=[r for r in rows if r['candidate']==c and r['scenario']==scenario and r['continuation']==continuation]
            if len(rs)!=len(seeds)*2:raise ValueError('Missing samples')
            values.append({'command':c,'count':len(rs),
                **{f'safeBy{s}':sum(0<=r['safeExitAt']<=s for r in rs) for s in [3,6,12]},
                **{k:statistics.mean(r[k] for r in rs) for k in ['earlyMaxOwnAdvance','earlyPossession','ownPossession','ownGoals','enemyGoals','enemyReceptions','ownReceptions','minimumMeanStamina']},
                'samples':[{'seed':r['seed'],'mirror':r['mirror'],'safeExitAt':r['safeExitAt'],'ownPossession':r['ownPossession'],'enemyReceptions':r['enemyReceptions'],'enemyGoals':r['enemyGoals']} for r in rs]})
        contexts.append({'scenario':scenario,'continuation':continuation,'values':values})
out=Path(a.output)
if out.exists():raise ValueError('Preserve previous assessment')
out.write_text(json.dumps({'files':files,'trials':len(rows),'seeds':seeds,'physicalCollectionValid':all(r['elapsed']>=12 and r['corrections']==0 for r in rows),'contexts':contexts,'trainingStarted':False},indent=2),encoding='utf-8')
for c in contexts:
    print(c['scenario'],c['continuation'])
    for v in c['values']:
        print(f" T{v['command']+1}: safe3/6={v['safeBy3']}/{v['safeBy6']} of {v['count']}; earlyX={v['earlyMaxOwnAdvance']:.2f}; ownPos={v['ownPossession']:.2f}; ownRecv={v['ownReceptions']:.2f}; enemyRecv={v['enemyReceptions']:.2f}; enemyGoals={v['enemyGoals']:.2f}; stamina={v['minimumMeanStamina']:.3f}")

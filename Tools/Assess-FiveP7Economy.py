"""Prospective low-energy recovery gate: preserve the failed possession hypotheses."""
import copy
import json
import statistics
from pathlib import Path
ROOT=Path(__file__).resolve().parent.parent
BASE=ROOT/'Logs/Manager5v5/p7-20261012'
def read(path):return json.loads(path.read_text(encoding='utf-8-sig'))
base=read(BASE/'roles-final-assessment-01.json')
files=sorted((BASE/'reports-p7-economical-confirmation-01').glob('roles-economical-*.json'))
rows=[r for f in files for r in read(f)['trials']]
seeds=sorted({r['seed'] for r in rows})
keys={(r['seed'],r['mirror'],r['phase'],r['candidate'],r['continuation']) for r in rows}
complete=len(rows)==len(keys)==512 and seeds==list(range(11141,11149)) and all(r['elapsed']>=12 and r['corrections']==0 for r in rows)
comparisons=[]
for phase in ['economical-tired','economical-fresh']:
    diffs=[];secure=[];samples=[]
    for seed in seeds:
        pair=[];recovered=[]
        for mirror in range(2):
            found=[]
            for command in [4,6]:
                match=[r for r in rows if r['seed']==seed and r['mirror']==mirror and r['phase']==phase and r['candidate']==command and r['continuation']=='fixed-twelve-seconds']
                if len(match)!=1:raise ValueError('Missing or duplicate economy pair.')
                found.append(match[0])
            pair.append(found[0]['minimumMeanStamina']-found[1]['minimumMeanStamina'])
            recovered.append(0<=found[0]['firstSecureAt']<=3)
            samples.append({'seed':seed,'mirror':mirror,'candidate':found[0]['minimumMeanStamina'],'alternative':found[1]['minimumMeanStamina'],
                            'candidateSecureAt':found[0]['firstSecureAt'],'alternativeSecureAt':found[1]['firstSecureAt'],
                            'candidatePossession':found[0]['ownPossession'],'alternativePossession':found[1]['ownPossession'],
                            'candidateEnemyGoals':found[0]['enemyGoals'],'alternativeEnemyGoals':found[1]['enemyGoals']})
        diffs.append(statistics.mean(pair));secure.append(all(recovered))
    positive=sum(d>1e-4 and s for d,s in zip(diffs,secure))
    comparisons.append({'scenario':'wide-trap-window','phase':phase,'metric':'minimumMeanStamina','direction':1,
        'meanDifference':statistics.mean(diffs),'seedDifferences':diffs,'bothMirrorsSecureByThree':secure,
        'positiveSeedPairs':positive,'seedPairCount':len(seeds),'samples':samples,
        'passed':statistics.mean(diffs)>1e-4 and positive>=6})
checks=copy.deepcopy(base['checks'])
for c in checks:
    if c['scenario']=='wide-trap-with-deep-outlet' and c['kind'] in ['advantage','weakness']:c['kind']='retired-long-pass-hypothesis'
for command,other,kind in [(4,6,'advantage'),(6,4,'weakness')]:
    c=copy.deepcopy(comparisons[0]);c.update(command=base['commandNames'][command],alternative=base['commandNames'][other],kind=kind)
    if kind=='weakness':
        c['direction']=-1
        for sample in c['samples']:sample['candidate'],sample['alternative']=sample['alternative'],sample['candidate']
    checks.append(c)
required=[c for c in checks if c['kind'] in ['advantage','weakness']]
result={'complete':complete and base['complete'],'trials':base['trials']+len(rows),'originalBatchTrials':base['trials'],'newBatchTrials':len(rows),
        'originalSeeds':base['seeds'],'newSeeds':seeds,'commandNames':base['commandNames'],'checks':checks,'economyComparisons':comparisons,
        'originalRoleDefinitionPassed':False,'widePossessionHypothesisPassed':False,
        'passed':complete and base['complete'] and len(required)==16 and all(c['passed'] for c in required),
        'basis':'14 previous role comparisons plus two reciprocal low-energy recovery/cost comparisons, prospectively specified before unused 11141-11148.',
        'files':[f.relative_to(ROOT).as_posix() for f in files],'trainingStarted':False}
out=BASE/'roles-qualified-assessment-02.json'
if out.exists():raise ValueError('Preserve previous assessment.')
out.write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({k:result[k] for k in ['complete','trials','passed']},ensure_ascii=True))
for c in comparisons:print(json.dumps({k:v for k,v in c.items() if k!='samples'},ensure_ascii=True))

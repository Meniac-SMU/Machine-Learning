"""Assess the prospectively revised wide-link role without erasing failed hypotheses."""
import copy
import json
import statistics
from pathlib import Path

ROOT=Path(__file__).resolve().parent.parent
BASE=ROOT/'Logs/Manager5v5/p7-20261012'
def read(path):return json.loads(path.read_text(encoding='utf-8-sig'))

base=read(BASE/'roles-final-assessment-01.json')
files=sorted((BASE/'reports-p7-wide-role-confirmation-01').glob('roles-wide-role-confirmation-*.json'))
rows=[r for f in files for r in read(f)['trials']]
seeds=sorted({r['seed'] for r in rows})
keys={(r['seed'],r['mirror'],r['scenario'],r['candidate'],r['continuation']) for r in rows}
complete=seeds==list(range(11121,11129)) and len(rows)==len(keys)==512 and all(r['elapsed']>=12 and r['corrections']==0 for r in rows)
results=[]
for scenario in ['wide-trap-window','wide-trap-with-deep-outlet']:
    diffs=[];samples=[]
    for seed in seeds:
        pair=[]
        for mirror in range(2):
            values=[]
            for command in [4,6]:
                found=[r for r in rows if r['seed']==seed and r['mirror']==mirror and r['scenario']==scenario
                       and r['candidate']==command and r['continuation']=='fixed-twelve-seconds']
                if len(found)!=1:raise ValueError('Missing or duplicate paired trial.')
                values.append(found[0]['ownPossession'])
            pair.append(values[0]-values[1]);samples.append({'seed':seed,'mirror':mirror,'candidate':values[0],'alternative':values[1]})
        diffs.append(statistics.mean(pair))
    results.append({'scenario':scenario,'metric':'ownPossession','direction':1,'meanDifference':statistics.mean(diffs),
                    'positiveSeedPairs':sum(d>1e-4 for d in diffs),'seedPairCount':len(seeds),'seedDifferences':diffs,'samples':samples,
                    'passed':statistics.mean(diffs)>1e-4 and sum(d>1e-4 for d in diffs)>=6})
checks=copy.deepcopy(base['checks']);retired=[]
for c in checks:
    if c['scenario']=='wide-trap-with-deep-outlet' and c['kind'] in ['advantage','weakness']:
        retired.append(copy.deepcopy(c));c['kind']='retired-long-pass-hypothesis'
for command,other,kind in [(4,6,'advantage'),(6,4,'weakness')]:
    new=copy.deepcopy(results[0]);new.update(command=base['commandNames'][command],alternative=base['commandNames'][other],kind=kind)
    # For the reciprocal weakness the reported raw pair still uses the advantage candidate first.
    if kind=='weakness':
        new['direction']=-1
        for sample in new['samples']:sample['candidate'],sample['alternative']=sample['alternative'],sample['candidate']
    checks.append(new)
required=[c for c in checks if c['kind'] in ['advantage','weakness']]
result={'complete':complete and base['complete'],'trials':base['trials']+len(rows),'originalBatchTrials':base['trials'],
        'newBatchTrials':len(rows),'originalSeeds':base['seeds'],'newSeeds':seeds,'commandNames':base['commandNames'],
        'checks':checks,'additionalScenarioComparisons':results,'retiredFailedComparisons':retired,
        'originalRoleDefinitionPassed':base['passed'],'revisedRoleDefinitionPassed':complete and len(required)==16 and all(c['passed'] for c in required),
        'passed':complete and base['complete'] and len(required)==16 and all(c['passed'] for c in required),
        'basis':'14 unchanged comparisons from 11111-11114 plus two reciprocal wide-link comparisons from unused 11121-11128. Long-pass role failure retained.',
        'files':[f.relative_to(ROOT).as_posix() for f in files],'trainingStarted':False}
out=BASE/'roles-qualified-assessment-01.json'
if out.exists():raise ValueError('Preserve previous assessment.')
out.write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({k:result[k] for k in ['complete','trials','originalRoleDefinitionPassed','revisedRoleDefinitionPassed','passed']},ensure_ascii=True))
for r in results:print(json.dumps({k:v for k,v in r.items() if k!='samples'},ensure_ascii=True))

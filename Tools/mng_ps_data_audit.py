"""Count physical demonstration outcomes without inflating repeated decisions."""
import json
from collections import Counter
from pathlib import Path

root=Path(__file__).resolve().parent.parent/'Logs/MNG-Rebuild/PS-20260929/data-v2'
examples=[json.loads(l) for l in (root/'examples.jsonl').read_text().splitlines()]
attempts=[json.loads(l) for l in (root/'attempts.jsonl').read_text().splitlines()]
result={}
for split in ('train','validation','holdout'):
    rows=[r for r in examples if r['split']==split]
    positive=[r for r in rows if r['label']!='preserve']
    linked={(r['match'],r['episode'],r['tick']):r for r in attempts if r['split']==split}
    physical=[]
    for r in positive:
        a=linked[(r['match'],r['episode'],r['tick'])]
        assert a['outcome']=='observed-intended-reception'
        physical.append((r['match'],a['strike']['episode'],a['strike']['kickId']))
    result[split]=dict(examples=len(rows),positiveObservations=len(positive),
        uniquePositivePassStrikes=len(set(physical)),positiveSeeds=len({r['seed'] for r in positive}),
        labels=dict(Counter(r['label'] for r in positive)),seeds=sorted({r['seed'] for r in rows}))
for left,right in (('train','validation'),('train','holdout'),('validation','holdout')):
    assert not set(result[left]['seeds'])&set(result[right]['seeds'])
with (root/'independence-audit.json').open('x') as f:json.dump(result,f,indent=2)
print(json.dumps(result,indent=2))

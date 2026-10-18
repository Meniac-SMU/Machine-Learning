"""Read only completed frozen matches; never treats partial sets as final."""
import argparse
import json
from pathlib import Path

p=argparse.ArgumentParser();p.add_argument('root');a=p.parse_args()
root=Path(a.root);m=json.loads((root/'manifest.json').read_text());count=0;groups={}
for n,name in enumerate(m['opponents']):
    rows=[]
    for i in range(n*m['pairs']*2,(n+1)*m['pairs']*2):
        f=root/f'match-{i:03d}'/'verified.json'
        if f.exists():rows.append(json.loads(f.read_text()))
    count+=len(rows)
    if len(rows)==m['pairs']*2:
        groups[name]=dict(matches=len(rows),wins=sum(r['score']==1 for r in rows),
            draws=sum(r['score']==.5 for r in rows),losses=sum(r['score']==0 for r in rows),
            scoreRate=sum(r['score'] for r in rows)/len(rows))
print(json.dumps(dict(completed=count,total=len(m['opponents'])*m['pairs']*2,completeGroups=groups)))

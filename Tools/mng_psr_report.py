"""Join actual commanded recipients to unique physical strikes and follow-up play."""
import argparse
from collections import Counter
import json
from pathlib import Path
import numpy as np

def read(p):return [json.loads(x) for x in p.read_text().splitlines()]
def pressure(o):
    p=np.array(o[:96]).reshape(8,12);ball=np.array(o[96:98])*[62,42.32727]
    opp=p[4:][p[4:,0]>.5,1:3]*[62,42.32727]
    return float(np.linalg.norm(opp-ball,axis=1).min()) if len(opp) else None

def analyze(root,matches=None):
    root=Path(root);report=json.loads((root/'report.json').read_text()) if matches is None else dict(matches=matches);strikes=[];opportunities=Counter()
    for match in report['matches']:
        team=match['policyTeam'];folder=root/f"match-{match['index']:03d}"
        ds=read(folder/'decisions.jsonl');events=read(next(folder.glob('lifecycle-*.jsonl')))
        ps=[e for e in events if e['team']==team and e['kind']=='PolicyDecision'];assert len(ps)>=len(ds)
        byparent={}
        for i,(d,p) in enumerate(zip(ds,ps)):
            assert d['action'][0]==p['rawCommand']==p['effectiveCommand']
            # Legacy policy re-masks the expanded command branch only in Python.
            byparent[(p['episode'],p['parentCommandId'])]=i
            if d['observation'][243]<.5:
                k=d['label']['kind'];opportunities[k+'/observations']+=1
                opportunities[k+'/selected']+=d['action'][0]==1
        seen=set()
        for e in events:
            if e['team']!=team or e['kind']!='Strike:Pass':continue
            key=(e['episode'],e['kickId']);assert key not in seen;seen.add(key)
            i=byparent.get((e['episode'],e['parentCommandId']))
            if i is None or ds[i]['action'][0]!=1 or e['source']!='PolicyCommand':continue
            d=ds[i];o=d['observation'];receiver=e['intendedReceiver'];p=np.array(o[:96]).reshape(8,12)
            if d['action'][1]>0:assert receiver==d['action'][1]-1
            c=int(np.argmax(o[103:112]))-1
            progress=float((p[receiver,1]-p[c,1])*62) if c in range(4) else None
            follow=[(q,z) for q,z in zip(ps[i+1:],ds[i+1:]) if q['episode']==e['episode'] and e['time']<q['time']<=e['time']+10]
            future=[x for x in events if x['episode']==e['episode'] and e['time']<x['time']<=e['time']+10]
            goal=any(x['kind']=='Goal' for x in future)
            received=next((q['time'] for q,z in follow if q['time']<=e['time']+2.5 and z['observation'][101]>.5
                and int(np.argmax(z['observation'][103:112]))-1==receiver and z['observation'][243]<.5),None)
            after=next(((q,z) for q,z in follow if q['time']>=e['time']+4),None)
            censored=after is None or after[1]['observation'][243]>.5 or any(x['kind']=='Goal' and x['time']<=after[0]['time'] for x in future)
            retained=bool(received is not None and not censored and after[1]['observation'][101]>.5
                and not any(z['observation'][102]>.5 for q,z in follow if q['time']<=after[0]['time']))
            advance=float((after[1]['observation'][96]-o[96])*62) if not censored else None
            initialPressure=pressure(o);laterPressure=pressure(after[1]['observation']) if retained else None
            escaped=bool(retained and initialPressure is not None and initialPressure<=7
                and laterPressure is not None and laterPressure>=initialPressure+2)
            def linked(kind):
                return received is not None and any(x['kind']==kind and x['team']==team and x['time']>=received
                    and not any(z['observation'][102]>.5 for q,z in follow if q['time']<=x['time']) for x in future)
            kind='keeper' if receiver==0 else 'backward' if progress is not None and progress<0 else 'forward'
            strikes.append(dict(match=match['index'],seed=match['seed'],team=team,kickId=e['kickId'],episode=e['episode'],
                kind=kind,receiver=receiver,recipientAction=d['action'][1],teacherKind=d['label']['kind'],time=e['time'],
                progressToReceiver=progress,nearGoal=bool(np.linalg.norm((np.array(o[96:98])*[62,42.32727])-[62,0])<=24),
                received=received is not None,receptionDelay=received-e['time'] if received else None,
                window2p5Observed=bool(follow and follow[-1][0]['time']>=e['time']+2.5),censored4s=censored,
                retained4s=retained,advance4s=advance,initialPressure=initialPressure,laterPressure=laterPressure,escaped4s=escaped,
                shot10s=any(x['kind']=='Strike:Shot' and x['team']==team for x in future),
                goalFor10s=any(x['kind']=='Goal' and x['team']==team for x in future),
                goalAgainst10s=any(x['kind']=='Goal' and x['team']!=team for x in future),
                linkedShot10s=linked('Strike:Shot'),linkedGoal10s=linked('Goal')))
    groups={}
    for s in strikes:
        for k in ('all',s['kind'],s['teacherKind'],'nearGoal' if s['nearGoal'] else 'outsideGoalArea'):
            g=groups.setdefault(k,Counter());g['strikes']+=1
            for f in ('received','retained4s','escaped4s','shot10s','goalFor10s','goalAgainst10s','censored4s','linkedShot10s','linkedGoal10s'):g[f]+=int(s[f])
    return dict(analysisVersion='PS-outcomes-v2: reception plus no sampled opponent takeover for linked outcomes',groups={k:dict(v) for k,v in groups.items()},opportunities=dict(opportunities),strikes=strikes,
                limitations='Sampled possession; censored/reset outcomes excluded; temporal shots/goals are associations, not causal proof.')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('root');a=p.parse_args();r=analyze(a.root)
    (Path(a.root)/'pass-outcomes.json').write_text(json.dumps(r,indent=2));print(json.dumps(r['groups'],indent=2))

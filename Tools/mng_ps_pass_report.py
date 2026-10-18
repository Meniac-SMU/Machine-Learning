"""Unique physical PassBuild strikes and observed downstream outcomes, not pass quotas."""
import argparse
from collections import Counter
import json
from pathlib import Path
import numpy as np
from mng_ps_teacher import context


def read(path):return [json.loads(l) for l in path.read_text().splitlines()]


def analyze(root, report_file=None):
    root=Path(root);report=json.loads((Path(report_file) if report_file else root/'report.json').read_text())
    strikes=[];opportunities=Counter();missing=[];opportunity_seeds={};opportunity_matches={}
    for match in report['matches']:
        team=match['policyTeam'];folder=root/f"match-{match['index']:03d}"
        ds=[r for r in read(folder/'decisions.jsonl') if r['team']==team]
        events=read(next(folder.glob('lifecycle-*.jsonl')))
        ps=[r for r in events if r['team']==team and r['kind']=='PolicyDecision']
        assert len(ps)>=len(ds) and all(r['matchState']=='Finished' for r in ps[len(ds):])
        ps=ps[:len(ds)];by_parent={}
        for i,(d,p) in enumerate(zip(ds,ps)):
            assert d['action']==p['rawCommand']==p['effectiveCommand']
            assert sum(1<<j for j,v in enumerate(d['valid']) if v)==p['maskBits']
            assert p['observationTick']==p['tick']
            by_parent[(p['episode'],p['parentCommandId'])]=i
            obs=d['observation']
            if obs[243]>.5:continue
            ctx=context(obs,d['valid']);key=ctx['reason']
            opportunities[key+'/decisions']+=1
            opportunity_seeds.setdefault(key,set()).add(match['seed'])
            opportunity_matches.setdefault(key,set()).add(match['index'])
            if d['valid'][1]:opportunities[key+'/passLegal']+=1
            if d['action']==1:opportunities[key+'/passSelected']+=1
        seen=set()
        for strike in events:
            if strike['team']!=team or strike['kind']!='Strike:Pass':continue
            key=(strike['episode'],strike['kickId'])
            assert key not in seen;seen.add(key)
            i=by_parent.get((strike['episode'],strike['parentCommandId']))
            if i is None or ds[i]['action']!=1:
                missing.append(dict(match=match['index'],kick=key,reason='not-linked-to-PassBuild'))
                continue
            origin=ds[i]['observation'];ctx=context(origin,ds[i]['valid'])
            following=[(q,d) for q,d in zip(ps[i+1:],ds[i+1:]) if q['episode']==strike['episode'] and strike['time']<q['time']<=strike['time']+10]
            received=None;intercepted=False
            for q,d in following:
                if q['time']>strike['time']+2.5:break
                z=d['observation']
                if z[102]>.5:intercepted=True
                if z[101]>.5 and int(np.argmax(z[103:112]))-1==strike['intendedReceiver']:
                    received=q['time'];break
            after4=next(((q,d) for q,d in following if q['time']>=strike['time']+4),None)
            retained=bool(after4 and after4[1]['observation'][101]>.5)
            future_events=[x for x in events if x['episode']==strike['episode'] and strike['time']<x['time']<=strike['time']+10]
            goal_by4=any(x['kind']=='Goal' and x['time']<=strike['time']+4.5 for x in future_events)
            forward_observed=bool(after4 and after4[1]['observation'][243]<.5 and not goal_by4)
            forward=float((after4[1]['observation'][96]-origin[96])*62) if forward_observed else None
            shots=[x for x in future_events if x['team']==team and x['kind']=='Strike:Shot']
            goals_for=[x for x in future_events if x['team']==team and x['kind']=='Goal']
            goals_against=[x for x in future_events if x['team']!=team and x['kind']=='Goal']
            strikes.append(dict(match=match['index'],seed=match['seed'],team=team,opponent=match.get('opponent'),
                episode=strike['episode'],kickId=strike['kickId'],taskId=strike['taskId'],parentCommandId=strike['parentCommandId'],
                decisionIndex=i,decisionTime=ps[i]['time'],strikeTime=strike['time'],receiver=strike['intendedReceiver'],
                context=ctx,observedIntendedReception=received is not None,opponentPossessionBeforeReception=intercepted,
                receptionWindowObserved=bool(following and following[-1][0]['time']>=strike['time']+2.5),
                observationAt4Seconds=after4 is not None,
                goalPauseAt4Seconds=bool(after4 and after4[1]['observation'][243]>.5),
                forwardOutcomeObserved=forward_observed,
                receptionDelay=received-strike['time'] if received else None,ownPossessionAt4Seconds=retained,
                forwardMetresAt4Seconds=forward,shotWithin10Seconds=bool(shots),goalForWithin10Seconds=bool(goals_for),
                goalAgainstWithin10Seconds=bool(goals_against),timeToNextShot=shots[0]['time']-strike['time'] if shots else None))
    groups={}
    for r in strikes:
        c=groups.setdefault(r['context']['reason'],Counter());c['strikes']+=1
        for key in ['observedIntendedReception','opponentPossessionBeforeReception','ownPossessionAt4Seconds',
                    'shotWithin10Seconds','goalForWithin10Seconds','goalAgainstWithin10Seconds',
                    'receptionWindowObserved','observationAt4Seconds','goalPauseAt4Seconds','forwardOutcomeObserved']:
            c[key]+=int(r[key])
    return dict(scope='Unique physical strikes linked to actual PassBuild. Reception is sampled possession, not a collision-event proof. Subsequent goals/shots are associations, not causal effects.',
        matches=len(report['matches']),opportunities=dict(opportunities),groups={k:dict(v) for k,v in groups.items()},
        opportunitySeedCounts={k:len(v) for k,v in opportunity_seeds.items()},
        opportunityMatchCounts={k:len(v) for k,v in opportunity_matches.items()},
        outcomeSeedCounts={k:len({s['seed'] for s in strikes if s['context']['reason']==k}) for k in groups},
        uniquePassBuildStrikes=len(strikes),observedIntendedReceptions=sum(r['observedIntendedReception'] for r in strikes),
        unlinkedOrOtherCommandPasses=missing,strikes=strikes)


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('root');p.add_argument('--output',required=True);p.add_argument('--report');a=p.parse_args()
    result=analyze(a.root,a.report)
    with Path(a.output).open('x') as f:json.dump(result,f,indent=2)
    print(json.dumps({k:v for k,v in result.items() if k not in ('strikes','unlinkedOrOtherCommandPasses')},indent=2))

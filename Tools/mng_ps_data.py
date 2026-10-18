"""Join actual teacher actions to lifecycle events, retaining failures separately."""
import argparse
from collections import Counter
import json
from pathlib import Path
import numpy as np
from mng_ps_teacher import context


def read(path):
    return [json.loads(line) for line in path.read_text().splitlines()]


def process(root, output):
    root, output = Path(root), Path(output)
    output.mkdir(parents=True, exist_ok=False)
    report = json.loads((root/'report.json').read_text())
    from mng_ps_teacher import VERSION
    assert report['manifest']['teacher']==VERSION, 'Use the frozen teacher source for older datasets'
    examples, attempts, counts = [], [], Counter()
    for match in report['matches']:
        folder = root/f"match-{match['index']:03d}"
        team = match['policyTeam']
        decisions = [r for r in read(folder/'decisions.jsonl') if r['team']==team]
        all_events = read(next(folder.glob('lifecycle-*.jsonl')))
        events = [r for r in all_events if r['team']==team]
        policies = [r for r in events if r['kind']=='PolicyDecision']
        assert len(policies) >= len(decisions)
        assert all(r['matchState']=='Finished' for r in policies[len(decisions):])
        policies = policies[:len(decisions)]
        # Blocks of five seed pairs rotate through all five opponents per split.
        block = ((match['seed']-report['manifest']['seed'])//5) % 5
        split = 'train' if block < 3 else 'validation' if block==3 else 'holdout'
        last_kept = {}
        for i, (d, p) in enumerate(zip(decisions, policies)):
            assert d['action']==p['rawCommand']==p['effectiveCommand']
            assert sum(1<<j for j,v in enumerate(d['valid']) if v)==p['maskBits']
            assert p['observationTick']==p['tick'], 'Action delayed from observed tick'
            o = d['observation']; ctx = context(o, d['valid'])
            counts[ctx['reason']] += 1
            identity = dict(seed=match['seed'], team=team, match=match['index'],
                episode=p['episode'], tick=p['observationTick'], parentCommandId=p['parentCommandId'],
                time=p['time'], split=split, context=ctx)
            if ctx['beneficial']:
                assert d['action']==1
                tasks = {e['taskId'] for e in events if e['episode']==p['episode'] and
                         e['tick']==p['tick'] and e['slot']==ctx['carrier'] and
                         e['kind'] in ('Accepted', 'RetainedEquivalent')}
                linked = [e for e in events if e['episode']==p['episode'] and
                          (e['parentCommandId']==p['parentCommandId'] or e['taskId'] in tasks) and
                          e['slot']==ctx['carrier']]
                strike = next((e for e in linked if e['kind']=='Strike:Pass' and e['time']>=p['time']), None)
                reception = None
                if strike:
                    for j in range(i+1, len(policies)):
                        q = policies[j]
                        if q['episode']!=p['episode'] or q['time']>strike['time']+2.5:
                            break
                        z = decisions[j]['observation']
                        if q['time']>strike['time'] and z[101]>.5 and int(np.argmax(z[103:112]))-1==strike['intendedReceiver']:
                            reception = q; break
                outcome = 'observed-intended-reception' if reception else 'strike-no-observed-reception' if strike else 'no-linked-strike'
                attempt = dict(identity, outcome=outcome, strike=strike, receptionObservation=reception,
                    linkedEvents=linked, receptionEvidence='decision-time possession; not a continuous collision-event proof')
                attempts.append(attempt)
                counts[outcome]+=1
                if not reception:
                    continue  # Failed/uncertain cases are preserved, not relabelled as correct demonstrations.
                label = ctx['reason']
            else:
                if o[243]>.5:
                    continue
                label = 'preserve'
            # One sample per two seconds within each episode/role. Never split adjacent frames across folds.
            key = (p['episode'], label)
            if p['time']-last_kept.get(key, -100) < 2:
                continue
            last_kept[key]=p['time']
            examples.append(dict(identity, observation=o, valid=d['valid'], executedAction=d['action'], label=label))
    with (output/'examples.jsonl').open('x') as f:
        for r in examples: f.write(json.dumps(r)+'\n')
    with (output/'attempts.jsonl').open('x') as f:
        for r in attempts: f.write(json.dumps(r)+'\n')
    summary = dict(counts=dict(counts), samples=dict(Counter(r['split']+'/'+r['label'] for r in examples)),
                   matches=len(report['matches']), teacherScoreRate=report['summary']['scoreRate'],
                   excludedFailuresRetained=True, candidatePerformanceEvaluated=False)
    (output/'summary.json').write_text(json.dumps(summary, indent=2))
    print(json.dumps(summary, indent=2))


if __name__=='__main__':
    p=argparse.ArgumentParser(); p.add_argument('root'); p.add_argument('--output',required=True)
    a=p.parse_args(); process(a.root,a.output)

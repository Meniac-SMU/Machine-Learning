"""Read-only linkage of candidate Pass decisions to lifecycle events; no success-rate inference."""
import argparse
from collections import Counter
import json
from pathlib import Path


def lines(path):
    return [json.loads(line) for line in path.read_text().splitlines()]


def inspect(root):
    root = Path(root)
    report = json.loads((root / 'report.json').read_text())
    attempts, extra = [], 0
    for match in report['matches']:
        folder = root / f"match-{match['index']:03d}"
        team = match['policyTeam']
        decisions = [r for r in lines(folder / 'decisions.jsonl') if r['team'] == team]
        events = [r for r in lines(next(folder.glob('lifecycle-*.jsonl'))) if r['team'] == team]
        policies = [r for r in events if r['kind'] == 'PolicyDecision']
        assert len(policies) >= len(decisions)
        assert all(r['matchState'] == 'Finished' for r in policies[len(decisions):])
        extra += len(policies) - len(decisions)
        policies = policies[:len(decisions)]
        for index, (decision, policy) in enumerate(zip(decisions, policies)):
            assert decision['action'] == policy['effectiveCommand']
            if decision['action'] != 1:
                continue
            carrier = max(range(9), key=lambda n: decision['observation'][103+n])-1
            assert 0 <= carrier <= 3 and decision['observation'][101] > .5
            parent = policy['parentCommandId']
            linked = [r for r in events if r['parentCommandId'] == parent and r['slot'] == carrier]
            strike = next((r for r in linked if r['kind'] == 'Strike:Pass'), None)
            cancelled = next((r for r in linked if r['kind'].startswith(('Cancelled', 'Rejected'))
                              or r['kind'] == 'Expired'), None)
            following = policies[index+1] if index+1 < len(policies) else None
            reason = 'physical-pass-strike' if strike else (
                cancelled['matchState']+'/'+cancelled['kind'] if cancelled else
                'next-command/'+str(following['effectiveCommand']) if following else 'episode-end')
            row = dict(match=match['index'], parent=parent, carrier=carrier, time=policy['time'], reason=reason)
            if strike:
                after = next((r for r in policies[index+1:] if r['time'] > strike['time']), None)
                row.update(strikeTime=strike['time'], receiver=strike['intendedReceiver'],
                           nextDecisionAfterStrike=after['time']-strike['time'] if after else None,
                           nextCommand=after['effectiveCommand'] if after else None)
            attempts.append(row)
    assert len(attempts) == report['summary']['conditionalCommands'][0][1]
    return dict(scope='Candidate Pass decisions: linked physical strike/cancellation, otherwise next command. '
                      'Repeated Pass is not a failure. Post-result Finished requests excluded. '
                      'Reception completion is an aggregate, not inferred for each attempt.',
                runtimeSha=report['manifest']['runtimeSha'], postResultFinishedRequests=extra,
                counts=dict(Counter(r['reason'] for r in attempts)), attempts=attempts)


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('evaluation')
    parser.add_argument('--output', required=True)
    args = parser.parse_args()
    result = inspect(args.evaluation)
    with Path(args.output).open('x') as handle:
        json.dump(result, handle, indent=2)
    print(json.dumps(result['counts']))

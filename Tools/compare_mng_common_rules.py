"""Compare paired frozen MS2-vs-400k reports with/without common rules."""
import argparse
import json
from pathlib import Path
import numpy as np


def load(folder):
    report = json.loads((Path(folder) / 'report.json').read_text())
    rows = report['matches']
    assert report['summary']['integrity']
    by_key = {(r['seed'], r['policyTeam']): r for r in rows}
    assert len(by_key) == len(rows)
    counts = {k: sum(r[k][r['policyTeam']] for r in rows) for k in
              ('commonPassAttempts', 'commonClearanceAttempts', 'commonNoTargetAttempts', 'commonRuleStrikes')}
    totals = {k: sum(r[k] for r in rows) for k in
              ('passStrikes', 'shotStrikes', 'intendedReceptions', 'episodeStallActivations', 'episodeStallActiveSeconds')}
    totals.update(counts)
    totals['actualPolicyPassChoices'] = sum(sum(state[1] for state in r['conditionalCommands'][:2]) for r in rows)
    totals['disabledOpponentInterventions'] = sum(sum(r[k][1-r['policyTeam']] for k in counts) for r in rows)
    assert totals['disabledOpponentInterventions'] == 0
    linked = {'stalledPassStrikes': 0, 'clearancePassStrikes': 0, 'clearanceStrongStrikes': 0,
              'keeperStrikes': 0, 'fieldStrikes': 0}
    unstruck = {}
    for row in rows:
        match = Path(folder) / f"match-{row['index']:03d}"
        common_paths = list(match.glob('common-rules*.jsonl'))
        assert len(common_paths) <= 1, 'Multiple common-rule streams in one frozen match'
        common = [json.loads(line) for path in common_paths for line in path.read_text().splitlines()]
        common = [event for event in common if event['team'] == row['policyTeam']]
        requests = {event['parentCommandId']: event for event in common if event['reason'] in ('OwnGoalClearance', 'NoForwardProgress2s')}
        physical = [event for event in common if event['reason'] == 'PhysicalStrike']
        assert len(physical) == row['commonRuleStrikes'][row['policyTeam']]
        lifecycle = next(match.glob('lifecycle-*.jsonl'))
        events = list(map(json.loads, lifecycle.read_text().splitlines()))
        strikes = {event['kickId']: event for event in events if event['kind'].startswith('Strike:')}
        for event in physical:
            strike = strikes[event['kickId']]
            assert strike['team'] == row['policyTeam'] and strike['source'] == 'RuleCommand'
            assert strike['parentCommandId'] == event['parentCommandId']
            request = requests[event['parentCommandId']]
            if request['reason'] == 'NoForwardProgress2s':
                assert strike['kind'] == 'Strike:Pass'
                linked['stalledPassStrikes'] += 1
            else:
                linked['clearancePassStrikes' if strike['kind'] == 'Strike:Pass' else 'clearanceStrongStrikes'] += 1
            linked['keeperStrikes' if event['slot'] == 0 else 'fieldStrikes'] += 1
        completed_parents = {event['parentCommandId'] for event in physical}
        for parent, request in requests.items():
            if parent in completed_parents:
                continue
            terminal = [event['kind'] for event in events if event['parentCommandId'] == parent
                        and event['team'] == request['team'] and event['slot'] == request['slot']
                        and (event['kind'].startswith('Cancelled') or event['kind'] == 'Expired')]
            reason = terminal[-1] if terminal else 'NoTerminalRecord'
            unstruck[reason] = unstruck.get(reason, 0) + 1
    totals['physicalTraceLinks'] = linked
    totals['attemptsWithoutStrikeLastTerminal'] = unstruck
    return report, by_key, totals


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--control', required=True)
    parser.add_argument('--treatment', required=True)
    parser.add_argument('--output', required=True)
    args = parser.parse_args()
    control, before, before_totals = load(args.control)
    treatment, after, after_totals = load(args.treatment)
    assert before.keys() == after.keys()
    for field in ('runtimeSha', 'candidate', 'opponent', 'seconds', 'mirrorPairs', 'seedOffset', 'pairs'):
        assert control['manifest'][field] == treatment['manifest'][field], field
    assert control['manifest']['candidateCommonRules'] == 'disabled'
    assert treatment['manifest']['candidateCommonRules'] == 'enabled'
    assert control['manifest']['opponentCommonRules'] == treatment['manifest']['opponentCommonRules'] == 'disabled'
    seeds = sorted({seed for seed, _ in before})
    delta = np.array([np.mean([after[(seed,t)]['score']-before[(seed,t)]['score'] for t in (0,1)]) for seed in seeds])
    rng = np.random.RandomState(593823)
    bootstrap = delta[rng.randint(len(seeds), size=(20000, len(seeds)))].mean(axis=1)
    result = dict(control=dict(summary=control['summary'], totals=before_totals),
                  treatment=dict(summary=treatment['summary'], totals=after_totals),
                  pairedScoreDifference=float(delta.mean()), pairedDifference95=np.quantile(bootstrap,[.025,.975]).tolist(),
                  pairs=len(seeds), matchesPerArm=len(before),
                  caveat='Frozen weights; common execution rules are not learned policy improvement. CI clusters both sides of the same seed.')
    Path(args.output).write_text(json.dumps(result, indent=2))
    print(json.dumps(result, indent=2))


if __name__ == '__main__':
    main()

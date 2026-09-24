"""Read-only Post-R6 diagnostics. Never merges different runtime results for promotion."""
import argparse
import json
from pathlib import Path
import numpy as np

COMMANDS = ('Carry', 'Pass', 'Shot', 'Recover', 'Balanced', 'Protect')
STATES = ('own', 'opponent', 'neutral')


def read_lines(path):
    return [json.loads(line) for line in path.read_text().splitlines()]


def vectors(row, key):
    return np.array([[v[k] for k in ('x', 'y', 'z')] for v in row[key]])


def inspect(root):
    root = Path(root)
    report = json.loads((root / 'report.json').read_text())
    rows = report['matches']
    assert report['manifest']['protocol'] == 'MNG-V2-FROZEN-300-v2'
    lookup = {(r['seed'], r['policyTeam'], r['inferenceRngSwap']): r for r in rows}
    source = np.zeros((2, 4), dtype=np.int64)
    task = np.zeros((2, 9), dtype=np.int64)
    delay_counts = np.zeros(2, dtype=np.int64)
    delay_sums = np.zeros(2)
    per_seed = {}
    mirror = []
    lifecycle = {}
    noncarrier_rear = np.zeros((2, 6), dtype=np.int64)
    noncarrier_samples = np.zeros((2, 6), dtype=np.int64)
    for row in rows:
        folder = root / f"match-{row['index']:03d}"
        if (folder / 'decisions.jsonl').exists():
            for decision in read_lines(folder / 'decisions.jsonl'):
                if decision['team'] != row['policyTeam']: continue
                obs = decision['observation']
                if obs[243] > .5: continue
                own = obs[101] > .5
                state = 0 if own else 1
                command = int(np.argmax(obs[116:122]))
                carrier_slot = int(np.argmax(obs[103:112]))-1 if own else -1
                noncarrier_samples[state, command] += 1
                noncarrier_rear[state, command] += sum(slot != carrier_slot
                    and obs[slot*12] > .5 and obs[slot*12+1] < obs[96] for slot in (1,2,3))
        assert row['elapsed'] >= 299.9
        assert row['mismatches'] == row['overrides'] == row['directDecisionRewards'] == 0
        assert abs(row['neutralTieWins'][0] - row['neutralTieWins'][1]) <= 1
        assert abs(row['neutralEscapeGrants'][0] - row['neutralEscapeGrants'][1]) <= 1
        assert row['episodeStallActivations'] >= row['stallActivations']
        assert 0 <= row['episodeStallActiveSeconds'] <= row['elapsed']
        final = read_lines(next(folder.glob('v2-*.jsonl')))[-1]
        assert final['completed'] and final['build'] == report['manifest']['runtimeSha']
        for role, team in enumerate((row['policyTeam'], 1-row['policyTeam'])):
            source[role] += np.array(final['executionSourceTicksByTeam']).reshape(2, 4)[team]
            task[role] += np.array(final['taskResultsByTeam']).reshape(2, 9)[team]
            delay_counts[role] += final['kickDelayCounts'][team]
            delay_sums[role] += final['kickDelaySums'][team]
        for path in folder.glob('lifecycle-*.jsonl'):
            entries = read_lines(path)
            assert all(b['sequence'] == a['sequence']+1 for a, b in zip(entries, entries[1:]))
            for e in entries:
                key = e['matchState'] + '/' + e['kind']
                lifecycle[key] = lifecycle.get(key, 0) + 1
        per_seed.setdefault(row['seed'], {0: [], 1: []})[row['policyTeam']].append(row['score'])
        if not report['manifest']['mirrorPairs'] or row['policyTeam'] != 0:
            continue
        other = lookup[row['seed'], 1, row['inferenceRngSwap']]
        other_folder = root / f"match-{other['index']:03d}"
        assert other['neutralFirstTeam'] == 1-row['neutralFirstTeam']
        a = {(r['episode'], r['kickoff']): r for r in read_lines(folder/'spawns.jsonl')}
        b = {(r['episode'], r['kickoff']): r for r in read_lines(other_folder/'spawns.jsonl')}
        common = a.keys() & b.keys()
        errors = {}
        for key in ('positions', 'forwards'):
            deviations = []
            for identity in common:
                x, y = vectors(a[identity], key), vectors(b[identity], key)
                y = np.concatenate((y[4:], y[:4])); y[:, [0, 2]] *= -1
                deviations.append(float(np.max(np.abs(x-y))))
            errors[key] = max(deviations)
            assert errors[key] < .0001, (identity, key, errors[key])
        pair = dict(seed=row['seed'], rng=row['inferenceRngSwap'], commonKickoffs=len(common), maxErrors=errors)
        if (folder/'decisions.jsonl').exists():
            left = [r for r in read_lines(folder/'decisions.jsonl') if r['team'] == 0]
            right = [r for r in read_lines(other_folder/'decisions.jsonl') if r['team'] == 1]
            pair['initialObservationError'] = float(np.max(np.abs(np.array(left[0]['observation'])-right[0]['observation'])))
            assert pair['initialObservationError'] < .0001
            pair['firstObservationDivergenceOver1e3'] = next((i for i,(x,y) in enumerate(zip(left,right))
                if np.max(np.abs(np.array(x['observation'])-y['observation'])) > .001), None)
            pair['firstActionDifference'] = next((i for i,(x,y) in enumerate(zip(left,right)) if x['action']!=y['action']),None)
        mirror.append(pair)
    differences = np.array([np.mean(r[0])-np.mean(r[1]) for r in per_seed.values()])
    rng = np.random.RandomState(20260923)
    bootstrap = differences[rng.randint(len(differences), size=(20000, len(differences)))].mean(axis=1)
    available = np.sum([r['availableByState'] for r in rows],axis=0)
    probabilities = np.sum([r['probabilitySumsByState'] for r in rows],axis=0)
    rear_n = np.sum([r['rearSamples'] for r in rows],axis=0)
    rear_total = np.sum([r['rearPlayerCounts'] for r in rows],axis=0)
    return dict(path=str(root), manifest=report['manifest'], summary=report['summary'],
        sideDifference=dict(redMinusNavy=float(differences.mean()), seedCluster95=np.quantile(bootstrap,[.025,.975]).tolist(), independentSeeds=len(differences)),
        availabilityAndProbability={state:{command:dict(available=int(available[s,c]),
            meanProbabilityWhenAvailable=float(probabilities[s,c]/available[s,c]) if available[s,c] else None)
            for c,command in enumerate(COMMANDS)} for s,state in enumerate(STATES)},
        meanRearFieldPlayers={state:{command:dict(samples=int(rear_n[s,c]),mean=float(rear_total[s,c]/rear_n[s,c]) if rear_n[s,c] else None)
            for c,command in enumerate(COMMANDS)} for s,state in enumerate(('own','nonOwn'))},
        meanNonCarrierFieldPlayersBehindBall={state:{command:dict(samples=int(noncarrier_samples[s,c]),
            mean=float(noncarrier_rear[s,c]/noncarrier_samples[s,c]) if noncarrier_samples[s,c] else None)
            for c,command in enumerate(COMMANDS)} for s,state in enumerate(('own','nonOwn'))},
        candidateThenOpponentSourceTicks=source.tolist(), candidateThenOpponentTaskResults=task.tolist(),
        candidateThenOpponentKickDelay=dict(counts=delay_counts.tolist(), sums=delay_sums.tolist()),
        neutralEscapeGrants=np.sum([r['neutralEscapeGrants'] for r in rows],axis=0).tolist(),
        intendedReceptions=sum(r['intendedReceptions'] for r in rows),
        passAvailableForwardBlocked=sum(r['passAvailableForwardBlocked'] for r in rows),
        mirrors=mirror, fullLifecycleCounts=lifecycle, integrity=True,
        limits=['Paired seed bootstrap is exploratory, particularly with very few seeds.',
                'Spawn symmetry does not imply bitwise mirrored PhysX trajectories.',
                'Personnel statistics are observational, not causal command effects.',
                'meanRearFieldPlayers includes the carrier; the separate noncarrier field excludes it. Both are geometric positions, not assigned defensive roles.',
                'Action availability is not a safe or advantageous passing opportunity.'])


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('evaluations', nargs='+')
    parser.add_argument('--output', required=True)
    args = parser.parse_args()
    output = Path(args.output)
    assert not output.exists(), 'Preserve prior inspection'
    reports = [inspect(path) for path in args.evaluations]
    assert len({r['manifest']['runtimeSha'] for r in reports}) == 1, 'Different runtimes must not be merged'
    output.write_text(json.dumps(reports, indent=2))
    for r in reports:
        print(r['path'], r['summary']['matches'], r['summary']['scoreRate'], r['sideDifference'], 'integrity=True')

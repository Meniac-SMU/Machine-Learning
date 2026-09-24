"""Summarize frozen match telemetry without changing evaluation or training."""
import argparse
import json
from pathlib import Path
import numpy as np

TASKS = ('Accepted', 'RetainedEquivalent', 'DeferredCommit', 'RejectedStale',
         'RejectedOwner', 'RejectedInvalidState', 'CancelledPossessionLost',
         'CancelledMatchState', 'Expired')
SOURCES = ('PolicyCommand', 'RuleCommand', 'KeeperTechnique', 'SafetyEscape')
COMMANDS = ('Carry', 'PassBuild', 'Shoot', 'Recover', 'Balanced', 'ProtectBack')
SKILLS = ('None', 'MoveTo', 'Carry', 'ReceivePass', 'AimPass', 'AimShot', 'Press',
          'Cover', 'KeeperHome', 'Mark', 'SupportRun', 'KeeperClaim', 'KeeperBlock')
PHASES = ('Idle', 'Preparing', 'Committed', 'Recovering', 'Receiving')


def inspect(path):
    root = Path(path)
    report = json.loads((root / 'report.json').read_text())
    tasks = np.zeros(len(TASKS), dtype=np.int64)
    sources = np.zeros(len(SOURCES), dtype=np.int64)
    sampled = dict(decisions=0, strikes=0)
    for match in report['matches']:
        folder = root / f"match-{match['index']:03d}"
        traces = list(folder.glob('v2-*.jsonl'))
        assert len(traces) == 1
        rows = [json.loads(line) for line in traces[0].read_text().splitlines()]
        final = rows[-1]
        assert final['completed'] and final['elapsedSeconds'] >= 299.9
        assert final['schema'] == report['manifest']['schema']
        assert final['build'] == report['manifest']['runtimeSha']
        tasks += final['taskResults']
        sources += final['executionSourceTicks']
        seen = set()
        strikes = set()
        for row in rows:
            for entry in row.get('trace', []):
                identity = (entry['episode'], entry['sequence'])
                if identity in seen:
                    continue
                seen.add(identity)
                if entry['kind'] == 'PolicyDecision':
                    assert entry['rawCommand'] == entry['effectiveCommand']
                    sampled['decisions'] += 1
                if entry['kind'].startswith('Strike:'):
                    assert entry['taskId'] > 0 and entry['parentCommandId'] > 0 and entry['source']
                    key = (entry['episode'], entry['kickId'])
                    assert key not in strikes
                    strikes.add(key)
                    sampled['strikes'] += 1
    counts = np.array(report['summary']['conditionalCommands'])
    conditional = {name: dict(zip(COMMANDS, (row / max(int(row.sum()), 1)).tolist()))
                   for name, row in zip(('ownPossession', 'nonPossession', 'dangerArea', 'maskRestricted'), counts)}
    execution = None
    if report['summary'].get('executionSamples') is not None:
        execution = {}
        for state, name in enumerate(('ownPossession', 'nonPossession')):
            execution[name] = {}
            for command, label in enumerate(COMMANDS):
                n = report['summary']['executionSamples'][state][command]
                execution[name][label] = dict(samples=n,
                    meanFieldPlayersBySkill=dict(zip(SKILLS, (np.array(report['summary']['executionSkills'][state][command])/max(n,1)).tolist())),
                    meanFieldPlayersByPhase=dict(zip(PHASES, (np.array(report['summary']['executionPhases'][state][command])/max(n,1)).tolist())))
    return dict(manifest=report['manifest'], matches=len(report['matches']),
                candidateConditionalCommandFractions=conditional,
                executedPersonnelBeforeNextDecision=execution,
                bothTeamsTaskResults=dict(zip(TASKS, tasks.tolist())),
                bothTeamsExecutionSourceTicks=dict(zip(SOURCES, sources.tolist())),
                bothTeamsSafetyTickFraction=float(sources[3] / max(int(sources.sum()), 1)),
                sampledTrace=sampled, integrity=True,
                limitations=['Task and execution counters include both teams.',
                             'Trace is bounded; sampled events are not all match events.',
                             'Personnel, when available, is a decision-time observation under the preceding command; it does not prove causal effects.',
                             'Exact task delay is not exported. Earlier evaluations have no personnel telemetry.',
                             'Safety tick fraction is execution time share, not policy override rate.'])


if __name__ == '__main__':
    p = argparse.ArgumentParser()
    p.add_argument('evaluation')
    p.add_argument('--output', required=True)
    a = p.parse_args()
    output = Path(a.output)
    assert not output.exists(), 'Preserve previous inspection'
    result = inspect(a.evaluation)
    output.write_text(json.dumps(result, indent=2))
    print(json.dumps(result, indent=2))

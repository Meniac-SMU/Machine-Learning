"""Incremental full-observation and score-only trace audit during self-play."""
import json
from pathlib import Path
import numpy as np
from five_match_metrics import event_metrics


class TraceGuard:
    def __init__(self, directory, expected_runtime='tactics-v10-all-command-limits'):
        self.directory = Path(directory)
        self.expected_runtime = expected_runtime
        self.offsets = {}; self.pending = {}; self.rows = 0; self.terminals = 0
        self.counts = np.zeros((2,3,8), dtype=np.int64)
        self.checked_events = 0
        self.command_state = {}

    def scan(self):
        failures = []
        for path in self.directory.glob('trace-*.jsonl'):
            with path.open('rb') as stream:
                stream.seek(self.offsets.get(path, 0))
                data = self.pending.get(path, b'') + stream.read()
                self.offsets[path] = stream.tell()
            complete, sep, tail = data.rpartition(b'\n')
            self.pending[path] = tail if sep else data
            for line in complete.splitlines():
                try:
                    row = json.loads(line); self.rows += 1
                    assert row['contract'] == 'MNG5-OBS676-A8-R1-M2-v6-ALLMAX3-T7MAX2-COOLDOWN2-GK6'
                    assert row['playerRuntime'] == self.expected_runtime
                    if self.expected_runtime == 'tactics-v10-all-command-limits':
                        assert row['executionFeatures'] == [True, True, True], 'Training execution variant drift'
                    assert row['ruleOpponentTeam'] == -1, 'Training must be self-play only'
                    assert row['boundaryCorrections'] == 0, 'Physics boundary correction'
                    obs = row.get('observation')
                    if obs:
                        a = np.asarray(obs)
                        assert a.shape == (676,) and np.isfinite(a).all() and (np.abs(a) <= 1.00001).all()
                    if row['kind'] == 'decision':
                        assert row['action'] in range(8)
                        assert obs[10] == 0, 'Decision during opponent keeper possession'
                        key = (str(path), row['episode'], row['team'])
                        streak, blocked = self.command_state.setdefault(key, ([0]*8, [0]*8))
                        for k, command in enumerate(range(8)):
                            if row['action'] == command:
                                assert blocked[k] == 0, 'Blocked command accepted'
                                streak[k] += 1
                                assert streak[k] <= (2 if k == 6 else 3), 'Consecutive command limit exceeded'
                                if streak[k] == (2 if k == 6 else 3): blocked[k] = 2
                            else:
                                streak[k] = 0
                                blocked[k] = max(0, blocked[k]-1)
                        assert row['commandBlockedSelections'] == blocked
                        assert row['commandConsecutiveSelections'] == streak
                        state = 0 if sum(obs[5:10]) > .5 else 1 if sum(obs[10:15]) > .5 else 2
                        self.counts[row['team'],state,row['action']] += 1
                    if row['kind'] in ('terminal', 'interrupted', 'decision'):
                        expected = (row['redScore']-row['navyScore'])*(1 if row['team'] == 0 else -1)
                        assert row['reward'] == expected, 'Reward is not score difference'
                    if row['kind'] == 'terminal':
                        assert row['elapsed'] >= 300
                        self.terminals += 1
                        if row['team'] == 0:
                            m = event_metrics(row['matchEvents'])
                            assert sum(s['shots'] for s in m) == row['shots']
                            assert sum(s['passAttempts'] for s in m) == row['passAttempts']
                            assert sum(s['passSuccesses'] for s in m) == row['passReceptions']
                            assert [s['goals'] for s in m] == [row['redScore'], row['navyScore']]
                            self.checked_events += len(row['matchEvents'])
                except Exception as exc:
                    failures.append(dict(path=str(path), row=self.rows, error=repr(exc)))
        return failures

    def summary(self):
        return dict(traceFiles=len(self.offsets), rows=self.rows, terminals=self.terminals,
                    checkedEvents=self.checked_events, bothTeamsDecisionCounts=self.counts.tolist())

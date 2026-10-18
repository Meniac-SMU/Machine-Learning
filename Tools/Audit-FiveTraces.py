"""Stream completed training/evaluation traces without changing gameplay or policy."""
import argparse
from collections import Counter
import json
import math
from pathlib import Path


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--directory", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    assert not args.output.exists()
    files = sorted(args.directory.rglob("trace-*.jsonl"))
    assert files
    kinds = Counter(); actions = [Counter(), Counter()]; runs = set(); terminal_scores = []
    maximum_error = 0.0
    for path in files:
        first = set(); run = None
        with path.open(encoding="utf-8-sig") as stream:
            for line in stream:
                row = json.loads(line)
                if run is None: run = row["runId"]; assert run not in runs; runs.add(run)
                assert row["runId"] == run and row["contract"] == "MNG5-OBS676-A8-R1-M2-v2"
                assert row["boundaryCorrections"] == 0, "Physics correction occurred"
                kinds[row["kind"]] += 1
                if row["kind"] in ("terminal", "interrupted"):
                    assert row["reward"] == (row["redScore"]-row["navyScore"])*(1 if row["team"] == 0 else -1)
                if row["kind"] == "terminal":
                    assert row["elapsed"] >= 300
                    if row["team"] == 0: terminal_scores.append([row["redScore"], row["navyScore"]])
                observation = row.get("observation")
                if observation:
                    assert len(observation) == 676 and all(math.isfinite(v) and abs(v) <= 1.00001 for v in observation)
                    flags = observation[672:]
                    assert flags[0] == 1 and all(v in (0, 1) for v in flags)
                    for frame, valid in enumerate(flags):
                        error = row["sampleErrors"][frame]; sample = row["sampleTimes"][frame]
                        if valid:
                            assert 0 <= error <= .0201 and abs(row["elapsed"]-.25*frame-sample-error) < .0001
                            maximum_error = max(maximum_error, error)
                        else:
                            assert sample == error == -1
                            assert all(v == 0 for v in observation[frame*168:(frame+1)*168])
                if row["kind"] == "decision":
                    assert 0 <= row["action"] < 8
                    actions[row["team"]][row["action"]] += 1
                    key = (row["episode"], row["team"])
                    if key not in first:
                        first.add(key)
                        assert row["elapsed"] == 0 and observation[672:] == [1, 0, 0, 0]
    report = dict(passed=True, processes=len(runs), events=dict(kinds), completedGames=len(terminal_scores),
                  maxHistoryError=maximum_error, boundaryCorrections=0,
                  actionCounts=[[c[i] for i in range(8)] for c in actions],
                  terminalScores=terminal_scores)
    args.output.write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps({k:v for k,v in report.items() if k != "terminalScores"}))


if __name__ == "__main__": main()

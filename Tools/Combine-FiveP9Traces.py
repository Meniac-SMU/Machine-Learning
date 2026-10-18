"""Combine verified, non-overlapping formal P9 training trace segments."""
from collections import Counter
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
BASE = ROOT / "Logs/Manager5v5/p9-20261013"
RUN = BASE / "MNG5_V2-20261013-r001"


def sha(path):
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(4 * 1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def main():
    output = BASE / "formal-traces.json"
    assert not output.exists()
    sources = []; traces = []; runs = set(); events = Counter()
    scores = []; actions = [[0] * 8 for _ in range(2)]; maximum_error = 0
    for name, segments in (("middle-traces.json", ("initial", "to-100k")),
                           ("late-traces.json", ("to-200k",)),
                           ("resume-traces.json", ("resume-to-200k",))):
        path = BASE / name
        audit = json.loads(path.read_text(encoding="utf-8-sig"))
        files = [p for segment in segments for p in sorted((RUN / segment / "traces").glob("trace-*.jsonl"))]
        assert audit["passed"] and audit["boundaryCorrections"] == 0
        assert len(files) == audit["processes"]
        for file in files:
            with file.open(encoding="utf-8-sig") as stream:
                first = json.loads(next(stream))
            assert first["runId"] not in runs, "Overlapping trace sessions"
            runs.add(first["runId"])
            traces.append(dict(path=file.relative_to(ROOT).as_posix(), sha256=sha(file), runId=first["runId"]))
        sources.append(dict(path=path.relative_to(ROOT).as_posix(), sha256=sha(path), segments=segments))
        events.update(audit["events"])
        scores.extend(audit["terminalScores"])
        maximum_error = max(maximum_error, audit["maxHistoryError"])
        for team in (0, 1):
            for action in range(8): actions[team][action] += audit["actionCounts"][team][action]
    assert len(runs) == 16 and sum(map(sum, actions)) == events["decision"]
    report = dict(passed=True, processes=len(runs), events=dict(events), completedGames=len(scores),
                  maxHistoryError=maximum_error, boundaryCorrections=0, actionCounts=actions,
                  terminalScores=scores, sourceAudits=sources, traces=traces)
    output.write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps({k: v for k, v in report.items() if k not in ("terminalScores", "traces")}))


if __name__ == "__main__": main()

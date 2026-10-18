"""Collect real, traced teacher matches using the immutable evaluation Player."""
import argparse
from concurrent.futures import ThreadPoolExecutor, as_completed
import json
from pathlib import Path
import numpy as np
import mng_v2_evaluate as ev
from mng_ps_teacher import context, VERSION
from mng_player_log_guard import PlayerLogGuard

PARENT = ev.ROOT/'results/MNG_MS3V3-20260925-r002/MNG_ManagerV2/MNG_ManagerV2-2001834.onnx'
OriginalPolicy = ev.FrozenPolicy


class Teacher(OriginalPolicy):
    def __init__(self, source, seed):
        self.teacher = source == 'PS-teacher'
        super().__init__(str(PARENT) if self.teacher else source, seed)

    def act(self, observation, valid):
        action = super().act(observation, valid)
        if self.teacher and context(observation[0], valid[0])['beneficial']:
            action = 1
            self.last_probabilities = np.eye(6)[1]
        return action


def main():
    p = argparse.ArgumentParser()
    p.add_argument('--output', required=True)
    p.add_argument('--build', required=True)
    p.add_argument('--pairs', type=int, default=6)
    p.add_argument('--seed', type=int, default=929100)
    p.add_argument('--parallel', type=int, default=8)
    a = p.parse_args()
    out = Path(a.output).resolve(); out.mkdir(parents=True, exist_ok=False)
    build = Path(a.build).resolve()
    info = json.loads((build/'build-info.json').read_text())
    exe = build/'MNG_EvaluationV2.exe'
    assert ev.sha(exe) == info['executableSha256']
    assert ev.sha(build/'MNG_EvaluationV2_Data/Managed/MNG.Runtime.dll') == info['runtimeSha256']
    assert ev.sha(build/'MNG_EvaluationV2_Data/level0') == info['levelSha256']
    assert ev.sha(PARENT) == '31b042143e5a01be5e5c2717126e6a967b1c16400d8a4167e72580acde4dc2a1'
    for source in [Path(__file__), Path(__file__).with_name('mng_ps_teacher.py')]:
        (out/source.name).write_bytes(source.read_bytes())
    manifest = dict(teacher=VERSION, runtimeSha=info['runtimeSha256'], parentSha=ev.sha(PARENT),
                    seed=a.seed, pairs=a.pairs, purpose='demonstration collection, not candidate evaluation')
    (out/'manifest.json').write_text(json.dumps(manifest, indent=2))
    ev.FrozenPolicy = Teacher
    opponents = [str(PARENT), 'R0-Full-v2', 'recover', 'carry-shot', 'balanced']
    jobs = [(2*i+t, a.seed+i, t, 'PS-teacher', opponents[i%5], str(out), exe,
             info['runtimeSha256'], False, 0, True, True, True, True)
            for i in range(a.pairs) for t in (0, 1)]
    rows = []
    with ThreadPoolExecutor(max_workers=a.parallel) as pool:
        for f in as_completed([pool.submit(ev.game, j) for j in jobs]):
            r = f.result(); rows.append(r)
            print(f"COLLECT {len(rows)}/{len(jobs)} {r['goalsFor']}:{r['goalsAgainst']}", flush=True)
    failures = [failure for folder in out.glob('match-*') for failure in PlayerLogGuard(str(folder)).scan()]
    assert not failures, failures
    report = dict(manifest=manifest, matches=sorted(rows, key=lambda r:r['index']), summary=ev.summarize(rows))
    (out/'report.json').write_text(json.dumps(report, indent=2))


if __name__ == '__main__':
    main()

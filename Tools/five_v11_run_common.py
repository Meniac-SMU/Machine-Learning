"""Frozen runtime and model preservation for keeper turn-first validation; no trainer."""
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
BASE = ROOT/'Logs/Manager5v5/keeper-turn-first-v11-20261016'
BUILD = ROOT/'Builds/Manager5v5/keeper-turn-first-learning-v11-20261016/Soccer5v5Learning.exe'
RUNTIME = 'tactics-v11-keeper-turn-first'


def sha(path):
    digest = hashlib.sha256()
    with Path(path).open('rb') as stream:
        for block in iter(lambda: stream.read(4*1024*1024), b''):
            digest.update(block)
    return digest.hexdigest()


def read(path):
    return json.loads(Path(path).read_text(encoding='utf-8-sig'))


def write(path, value):
    Path(path).write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding='utf-8')


def snapshot(path, files):
    assert not path.exists(), 'Preserve earlier evidence'
    write(path, dict(inputs=[dict(path=p.relative_to(ROOT).as_posix(), sha256=sha(p)) for p in sorted(files)]))


def verify(path=BASE/'frozen-manifest.json'):
    manifest = read(path)
    for item in manifest['inputs']:
        assert sha(ROOT/item['path']) == item['sha256'], item['path']
    return len(manifest['inputs'])


def freeze():
    assert BUILD.is_file()
    files = [p for p in (ROOT/'Assets/_Soccer/Manager5v5').rglob('*') if p.is_file()]
    files += [p for p in BUILD.parent.rglob('*') if p.is_file()
              and 'ML-Agents/Timers/' not in p.as_posix() and p.name != 'build-usage.jsonl']
    files += [ROOT/'Tools'/name for name in ('five_v11_evaluate.py', 'five_v11_run_common.py',
              'Evaluate-FiveV11ModelPair40.py', 'Run-FiveKeeperTurnFirstComparison.py',
              'Report-FiveKeeperTurnFirst.py', 'five_match_metrics.py')]
    snapshot(BASE/'frozen-manifest.json', files)


if __name__ == '__main__':
    freeze()
    print('FROZEN', verify())

"""Read-only identity check for the frozen v8 environment and 2M preparation."""
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
MANIFEST = ROOT / 'docs/soccer/rebuild-5v5/p9-frozen-v8-manifest.json'

def sha(path):
    h = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            h.update(block)
    return h.hexdigest()

def verify():
    data = json.loads(MANIFEST.read_text(encoding='utf-8-sig'))
    assert data['environmentFrozen'] and data['targetDecisions'] == 2000000
    for item in data['inputs']:
        path = (ROOT / item['path']).resolve()
        assert path.is_relative_to(ROOT) and path.is_file(), item['path']
        assert sha(path) == item['sha256'], 'Frozen input changed: ' + item['path']
    return dict(passed=True, files=len(data['inputs']), runtime=data['runtime'], trainingStarted=False)

if __name__ == '__main__':
    print(json.dumps(verify()))

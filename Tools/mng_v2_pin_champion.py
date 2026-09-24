"""Register an approved champion in a stopped Run's permanent opponent pool."""
import argparse
import copy
import hashlib
import json
import os
from pathlib import Path
import time
import torch
from mng_v2_learn import snapshot_sha


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def pin(pool, checkpoint):
    assert pool['step'] == int(checkpoint['global_step']['_GlobalSteps__global_step'])
    snapshot = {'MNG_ManagerV2': copy.deepcopy(checkpoint['Policy'])}
    reference = pool['pinned'][0]['MNG_ManagerV2']
    assert set(reference) == set(snapshot['MNG_ManagerV2'])
    for key, value in snapshot['MNG_ManagerV2'].items():
        assert value.shape == reference[key].shape and torch.isfinite(value).all()
    identity = snapshot_sha(snapshot)
    result = copy.deepcopy(pool)
    if identity not in {snapshot_sha(p) for p in result['pinned']}:
        result['pinned'].append(snapshot)
    return result, identity


def main():
    p = argparse.ArgumentParser()
    p.add_argument('--run', required=True)
    p.add_argument('--checkpoint', required=True)
    p.add_argument('--assessment', required=True)
    p.add_argument('--output', required=True)
    a = p.parse_args()
    root = Path(__file__).resolve().parent.parent
    evidence = root / 'Logs/MNG-Rebuild' / a.run
    progress = json.loads((evidence / 'progress.json').read_text(encoding='utf-8-sig'))
    assert progress['status'] == 'saved', 'Stop the trainer before changing its pool'
    assessment = json.loads(Path(a.assessment).read_text(encoding='utf-8-sig'))
    checkpoint_path = Path(a.checkpoint).resolve()
    assert assessment['status'] == 'champion' and assessment['passed']
    assert assessment['candidateSha'] == sha(checkpoint_path.with_suffix('.onnx'))
    assert assessment['runtimeSha'] == progress['runtimeSha']
    assert checkpoint_path.parent.parent.name == a.run
    output = Path(a.output)
    assert not output.exists(), 'Preserve existing promotion evidence'
    sidecar = checkpoint_path.parent / 'mng-v2-pool.pt'
    pool = torch.load(sidecar, map_location='cpu', weights_only=False)
    assert pool['step'] == progress['step']
    updated, identity = pin(pool, torch.load(checkpoint_path, map_location='cpu', weights_only=False))
    before = sidecar.with_name(f'mng-v2-pool-{pool["step"]}-before-champion.pt')
    after = sidecar.with_name(f'mng-v2-pool-{pool["step"]}-with-champion.pt')
    assert not before.exists(), 'Champion pool update already attempted'
    assert not after.exists()
    before.write_bytes(sidecar.read_bytes())
    temporary = sidecar.with_suffix('.champion.tmp')
    torch.save(updated, temporary)
    os.replace(temporary, sidecar)
    after.write_bytes(sidecar.read_bytes())
    ghost = pool['ghost']
    event = dict(event='saved', step=pool['step'], utcSeconds=time.time(),
                 reason='approved-champion-pin', championSha=identity, pool_sha=sha(sidecar),
                 learning_team=ghost['_learning_team'], ghost_step=ghost['ghost_step'],
                 last_save=ghost['last_save'], last_swap=ghost['last_swap'],
                 last_team_change=ghost['last_team_change'],
                 pinned=[snapshot_sha(s) for s in updated['pinned']])
    with sidecar.with_suffix('.jsonl').open('a', encoding='utf-8') as f:
        f.write(json.dumps(event) + '\n')
    output.write_text(json.dumps(dict(beforeSha=sha(before), afterSha=sha(after),
        snapshotSha=identity, pinnedBefore=len(pool['pinned']), pinnedAfter=len(updated['pinned']),
        step=pool['step'], optimizerUnchanged=True, scheduleAndRngUnchanged=True), indent=2))


if __name__ == '__main__':
    main()

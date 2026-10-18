"""Authorized 2.8M -> 3.6M continuation on the current v11 runtime."""
from five_v10_run_common_r2 import read, write, sha, ROOT

BASE = ROOT/'Logs/Manager5v5/v11-3600-20261017'
BUILD = ROOT/'Builds/Manager5v5/v11-3600-learning-20261017-r002/Soccer5v5Learning.exe'
CONFIG = ROOT/'docs/soccer/rebuild-5v5/p9-v11-24env-cuda-3600.yaml'
BEHAVIOR = 'MNG5_Manager_v6'
CONTRACT = 'MNG5-OBS676-A8-R1-M2-v6-ALLMAX3-T7MAX2-COOLDOWN2-GK6'
RUNTIME = 'tactics-v11-keeper-turn-first'
PARENT = 'MNG5_V6-20261016-r002'
RUN = 'MNG5_V6-20261017-r001'
PREFLIGHT = 'P9-V11-preflight-20261017-r002'

def model_dir(run): return ROOT/'results/Manager5v5'/run/BEHAVIOR

def verify():
    inputs = read(BASE/'frozen-manifest-r002.json')['inputs']
    for item in inputs:
        assert sha(ROOT/item['path']) == item['sha256'], item['path']
    return len(inputs)

def verify_parent():
    inputs = read(BASE/'parent-preservation.json')['files']
    for item in inputs:
        assert sha(ROOT/item['path']) == item['sha256'], item['path']
    return len(inputs)

def freeze():
    assert BUILD.exists() and not (BASE/'frozen-manifest-r002.json').exists()
    files = [p for p in (ROOT/'Assets/_Soccer/Manager5v5').rglob('*')
             if p.is_file() and 'Demo' not in p.relative_to(ROOT/'Assets/_Soccer/Manager5v5').parts]
    files += [p for p in BUILD.parent.rglob('*') if p.is_file()
              and 'ML-Agents/Timers/' not in p.as_posix() and p.name != 'build-usage.jsonl']
    files += [CONFIG] + [ROOT/'Tools'/name for name in (
        'five_v11_continuation_common.py','five_v11_cuda_train.py',
        'Prepare-FiveV11Continuation.py','Run-FiveV11Training.py','Run-FiveV11Continuation.py',
        'five_v10_selfplay_train.py','five_v10_cuda_train.py','five_v10_trace_guard.py',
        'five_v10_migration.py','five_v11_trace_guard.py','five_v11_evaluate.py','five_match_metrics.py',
        'mng_player_log_guard.py','Audit-FiveCheckpoint.py')]
    write(BASE/'frozen-manifest-r002.json',dict(runtime=RUNTIME,contract=CONTRACT,
        inputs=[dict(path=p.relative_to(ROOT).as_posix(),sha256=sha(p)) for p in sorted(set(files))]))
    return verify()

if __name__ == '__main__': print('FROZEN', freeze())

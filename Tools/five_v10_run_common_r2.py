import hashlib
import json
import os
from pathlib import Path

ROOT=Path(__file__).resolve().parent.parent
BASE=ROOT/'Logs/Manager5v5/v10-2800-repair-20261016'
BUILD=ROOT/'Builds/Manager5v5/v10-2800-learning-20261016/Soccer5v5Learning.exe'
CONFIG=ROOT/'docs/soccer/rebuild-5v5/p9-v10-32env-cuda-2800.yaml'
BEHAVIOR='MNG5_Manager_v6'
CONTRACT='MNG5-OBS676-A8-R1-M2-v6-ALLMAX3-T7MAX2-COOLDOWN2-GK6'
RUNTIME='tactics-v10-all-command-limits'
RUN='MNG5_V6-20261016-r002'
PREFLIGHT='P9-V10-preflight-20261016-r002'

def read(p):return json.loads(Path(p).read_text(encoding='utf-8-sig'))
def write(p,v):
    p=Path(p);p.parent.mkdir(parents=True,exist_ok=True)
    temp=p.with_suffix('.tmp');temp.write_text(json.dumps(v,ensure_ascii=False,indent=2),encoding='utf-8');os.replace(temp,p)
def sha(p):
    h=hashlib.sha256()
    with Path(p).open('rb') as f:
        for block in iter(lambda:f.read(4*1024*1024),b''):h.update(block)
    return h.hexdigest()
def model_dir(run):return ROOT/'results/Manager5v5'/run/BEHAVIOR
def freeze():
    path=BASE/'frozen-manifest.json';assert not path.exists()
    files=[p for p in (ROOT/'Assets/_Soccer/Manager5v5').rglob('*') if p.is_file()]
    files += [p for p in BUILD.parent.rglob('*') if p.is_file() and 'ML-Agents/Timers/' not in p.as_posix() and p.name!='build-usage.jsonl']
    files += [CONFIG]+[ROOT/'Tools'/p for p in ('five_v10_run_common_r2.py','five_v10_migration.py','Prepare-FiveV10ContinuationR2.py','five_v10_selfplay_train.py','five_v10_cuda_train.py',
        'five_v10_trace_guard.py','five_v10_evaluate.py','five_match_metrics.py','mng_player_log_guard.py',
        'Run-FiveV10TrainingR2.py','Run-FiveV10ContinuationR2.py','Audit-FiveCheckpoint.py')]
    write(path,dict(runtime=RUNTIME,contract=CONTRACT,inputs=[dict(path=p.relative_to(ROOT).as_posix(),sha256=sha(p)) for p in files]))
def verify():
    manifest=read(BASE/'frozen-manifest.json')
    for item in manifest['inputs']:assert sha(ROOT/item['path'])==item['sha256'],item['path']
    return len(manifest['inputs'])

if __name__=='__main__':freeze();print('FROZEN',verify())

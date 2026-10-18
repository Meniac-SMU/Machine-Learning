"""Preserve the original run, then migrate metadata into isolated v10 continuations."""
import copy
import hashlib
import json
from pathlib import Path
import shutil
import zipfile
import torch
from five_v10_migration import migrate_pool,validate_pool

ROOT=Path(__file__).resolve().parent.parent
BASE=ROOT/'Logs/Manager5v5/v10-2800-repair-20261016'
PARENT='MNG5_V5-20261016-r001'
BEFORE='MNG5_Manager_v5'
AFTER='MNG5_Manager_v6'
CONTRACT='MNG5-OBS676-A8-R1-M2-v6-ALLMAX3-T7MAX2-COOLDOWN2-GK6'

def sha(p):
    h=hashlib.sha256()
    with p.open('rb') as f:
        for block in iter(lambda:f.read(4*1024*1024),b''):h.update(block)
    return h.hexdigest()

def write(p,v):p.write_text(json.dumps(v,ensure_ascii=False,indent=2),encoding='utf-8')

def same_state(a,b):
    if torch.is_tensor(a):return torch.equal(a,b)
    if isinstance(a,dict):return a.keys()==b.keys() and all(same_state(a[k],b[k]) for k in a)
    if isinstance(a,(list,tuple)):return len(a)==len(b) and all(same_state(x,y) for x,y in zip(a,b))
    return a==b

def migrate(run):
    original=ROOT/'results/Manager5v5'/PARENT
    target=ROOT/'results/Manager5v5'/run
    assert not any(p.is_file() for p in target.rglob('*')), 'Preserve existing continuation'
    model=target/AFTER;model.mkdir(parents=True,exist_ok=True)
    source=original/BEFORE
    assert same_state(torch.load(source/'checkpoint.pt',map_location='cpu'),
                      torch.load(source/f'{BEFORE}-2400000.pt',map_location='cpu'))
    for name in ('checkpoint.pt',f'{BEFORE}-2400000.pt',f'{BEFORE}-2400000.onnx'):
        shutil.copy2(source/name,model/name.replace(BEFORE,AFTER))
    shutil.copy2(source/f'{BEFORE}-2400000.onnx',target/f'{AFTER}.onnx')
    saved=torch.load(source/'five-pool.pt',map_location='cpu')
    assert saved['step']==2400000 and saved['optimizerUpdates']==27888
    discarded=saved['ppo_buffer'].num_experiences
    saved=migrate_pool(saved)
    validate_pool(saved)
    torch.save(saved,model/'five-pool.pt')
    shutil.copy2(model/'five-pool.pt',model/'five-pool-2400000.pt')
    status=json.loads((original/'run_logs/training_status.json').read_text())
    state=copy.deepcopy(status[BEFORE])
    state['checkpoints']=[s for s in state['checkpoints'] if s['steps']==2400000]
    assert len(state['checkpoints'])==1
    def remap(value):
        if isinstance(value,str):return value.replace(str(original),str(target)).replace(BEFORE,AFTER)
        if isinstance(value,dict):return {k:remap(v) for k,v in value.items()}
        if isinstance(value,list):return [remap(v) for v in value]
        return value
    status={AFTER:remap(state),'metadata':status['metadata']}
    (target/'run_logs').mkdir()
    write(target/'run_logs/training_status.json',status)
    assert PARENT not in json.dumps(status), 'Checkpoint retention must never point to the original run'
    receipt=dict(run=run,parentRun=PARENT,parentStep=2400000,optimizerUpdates=27888,
        policyOptimizerCheckpointSha=sha(model/'checkpoint.pt'),parentCheckpointSha=sha(source/'checkpoint.pt'),
        migratedPoolSha=sha(model/'five-pool.pt'),parentPoolSha=sha(source/'five-pool.pt'),
        oldRuleBufferExperiencesPreservedInParent=discarded,newRuleBufferExperiences=0,
        preserved='policy, critic, Adam optimizer, global step, opponent snapshots, team state, RNG states',
        modified='behavior binding and contract metadata; old-rule pending PPO transitions excluded',
        environments=32,device='cuda',target=2800000)
    write(BASE/(run+'-migration.json'),receipt)
    return receipt

def main():
    old=ROOT/'results/Manager5v5'/PARENT
    archive=ROOT/'Logs/Manager5v5/v10-2800-20261016/parent-run-at-2400000.zip'
    if archive.exists():
        record=json.loads((ROOT/'Logs/Manager5v5/v10-2800-20261016/parent-preservation.json').read_text(encoding='utf-8'))
        write(BASE/'parent-preservation.json',record)
        assert record['passed']
        for item in record['files']:assert sha(ROOT/item['path'])==item['sha256']
        manifest=record['files']
    else:
        preserve_parent(old,archive)
        manifest=json.loads((BASE/'parent-preservation.json').read_text(encoding='utf-8'))['files']
    receipts=[migrate(run) for run in ('P9-V10-preflight-20261016-r002','MNG5_V6-20261016-r002')]
    write(BASE/'authorization.json',dict(parentRun=PARENT,parentStep=2400000,targetSteps=2800000,
        environments=32,device='cuda',runtime='tactics-v10-all-command-limits',seconds=300,timeScale=100,
        reward='score only +1/-1/0',selfPlayOnly=True,saveSteps=[2600000,2800000],evaluationGames=192,
        learningRate='existing linear 0.0003 schedule with horizon extended to 2800000',
        initialLearningRate=0.0003+(1e-10-0.0003)*(2400000/2800000),scheduledMonitor=False))
    print(json.dumps(dict(preservedFiles=len(manifest),migrations=receipts),ensure_ascii=False))

def preserve_parent(old,archive):
    manifest=[]
    with zipfile.ZipFile(archive,'x',compression=zipfile.ZIP_STORED) as z:
        for path in sorted(old.rglob('*')):
            if path.is_file():
                relative=path.relative_to(ROOT).as_posix()
                manifest.append(dict(path=relative,sha256=sha(path),bytes=path.stat().st_size))
                z.write(path,relative)
    with zipfile.ZipFile(archive) as z:
        for item in manifest:
            h=hashlib.sha256()
            with z.open(item['path']) as f:
                for block in iter(lambda:f.read(4*1024*1024),b''):h.update(block)
            assert h.hexdigest()==item['sha256']
            assert sha(ROOT/item['path'])==item['sha256']
    evidence=ROOT/'Logs/Manager5v5/p9-frozen-v8-20261015'/PARENT
    records=[dict(path=p.relative_to(ROOT).as_posix(),sha256=sha(p)) for p in
             [evidence/'model-registry.json',evidence/'completion-verification-2400.json',evidence/'stage-results.json']]
    write(BASE/'parent-preservation.json',dict(passed=True,archive=str(archive),files=manifest,records=records))

if __name__=='__main__':main()

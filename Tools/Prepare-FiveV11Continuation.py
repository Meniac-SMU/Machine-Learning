"""Archive the exact 2.8M parent and copy complete optimizer/opponent state."""
import copy
import hashlib
import shutil
import zipfile
import torch
from mlagents.trainers.buffer import AgentBuffer
from five_v10_migration import same_state, validate_pool
from five_v11_continuation_common import *

def main():
    source = model_dir(PARENT)
    checkpoint = torch.load(source/'checkpoint.pt',map_location='cpu')
    assert same_state(checkpoint,torch.load(source/f'{BEHAVIOR}-2800000.pt',map_location='cpu'))
    pool = torch.load(source/'five-pool.pt',map_location='cpu')
    validate_pool(pool)
    assert pool['step'] == 2800000 and pool['optimizerUpdates'] == 32496
    archive = BASE/'parent-run-at-2800000.zip'
    manifest = []
    with zipfile.ZipFile(archive,'x',compression=zipfile.ZIP_STORED) as z:
        for path in sorted(source.parent.rglob('*')):
            if path.is_file():
                relative = path.relative_to(ROOT).as_posix()
                manifest.append(dict(path=relative,sha256=sha(path),bytes=path.stat().st_size))
                z.write(path,relative)
    with zipfile.ZipFile(archive) as z:
        for item in manifest:
            digest=hashlib.sha256()
            with z.open(item['path']) as stream:
                for block in iter(lambda:stream.read(4*1024*1024),b''): digest.update(block)
            assert digest.hexdigest() == item['sha256'] == sha(ROOT/item['path'])
    write(BASE/'parent-preservation.json',dict(passed=True,archive=str(archive),files=manifest))
    for run in [PREFLIGHT,RUN]:
        target=model_dir(run)
        assert not target.parent.exists(), 'Never overwrite an existing run'
        target.mkdir(parents=True)
        for name in ['checkpoint.pt',f'{BEHAVIOR}-2800000.pt',f'{BEHAVIOR}-2800000.onnx']:
            shutil.copy2(source/name,target/name)
        shutil.copy2(source/f'{BEHAVIOR}-2800000.onnx',target.parent/f'{BEHAVIOR}.onnx')
        migrated=copy.deepcopy(pool)
        migrated['ppo_buffer']=AgentBuffer()
        # Physics changed from the parent's v10; exclude only pending old-runtime transitions.
        for key in pool:
            if key != 'ppo_buffer': assert same_state(pool[key],migrated[key]),key
        validate_pool(migrated)
        torch.save(migrated,target/'five-pool.pt')
        shutil.copy2(target/'five-pool.pt',target/'five-pool-2800000.pt')
        status=read(source.parent/'run_logs/training_status.json')
        state=copy.deepcopy(status[BEHAVIOR])
        state['checkpoints']=[row for row in state['checkpoints'] if row['steps']==2800000]
        assert len(state['checkpoints'])==1
        def remap(v):
            if isinstance(v,str):return v.replace(str(source.parent),str(target.parent))
            if isinstance(v,dict):return {k:remap(x) for k,x in v.items()}
            if isinstance(v,list):return [remap(x) for x in v]
            return v
        status={BEHAVIOR:remap(state),'metadata':status['metadata']}
        assert PARENT not in str(status)
        write(target.parent/'run_logs/training_status.json',status)
        write(BASE/(run+'-migration.json'),dict(run=run,parentRun=PARENT,parentStep=2800000,
            optimizerUpdates=32496,parentCheckpointSha=sha(source/'checkpoint.pt'),
            checkpointSha=sha(target/'checkpoint.pt'),parentPoolSha=sha(source/'five-pool.pt'),
            poolSha=sha(target/'five-pool.pt'),oldRuntimeBufferPreservedInParent=pool['ppo_buffer'].num_experiences,
            newRuntimeBuffer=0,preserved='policy, critic, optimizer, step, opponent pool, team state and RNG',
            environments=24,target=3600000,runtime=RUNTIME))
    write(BASE/'authorization.json',dict(parentRun=PARENT,parentStep=2800000,targetSteps=3600000,
        environments=24,device='cuda',runtime=RUNTIME,seconds=300,timeScale=100,
        reward='score only +1/-1/0',selfPlayOnly=True,saveSteps=[3000000,3200000,3400000,3600000],
        demoSteps=[3200000,3600000],learningRate='Existing linear schedule; horizon extended to 3600000',
        formalEvaluationGamesPerPair=40,scheduledMonitor=False))
    print('PRESERVED',verify_parent(),flush=True)

if __name__=='__main__':main()

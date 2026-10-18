"""Explicit 2M -> PS fork. Does not relax the MS2-only fresh-run launcher."""
import argparse
import copy
import hashlib
import json
import os
from pathlib import Path
import sys
import numpy as np
np.dot(np.zeros((2, 2)), np.zeros((2, 2)))
import torch
import yaml

ROOT=Path(__file__).resolve().parent.parent
PARENT_RUN='MNG_MS3V3-20260925-r002'
RUN='MNG_MS3V3-20260929-r002'
STEP=2001834
PARENT=ROOT/'results'/PARENT_RUN/'MNG_ManagerV2'
RESULT=ROOT/'results'/RUN
EVIDENCE=ROOT/'Logs/MNG-Rebuild'/RUN
BUILD=ROOT/'Builds/MNG_V2/MS3-v3-WorkerIOFix-20260924T182752604Z/MS3V2'


def sha(path): return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def dump(path, value): Path(path).write_text(json.dumps(value,indent=2))


def prepare(bc):
    from mng_v2_learn import snapshot_sha, validate_v3_pool
    bc=Path(bc)
    report=json.loads((bc/'report.json').read_text())
    assert report['passed'], 'Offline gate did not pass; do not silently train rejected actor'
    assert not RESULT.exists() and not EVIDENCE.exists(), 'Existing Run must be preserved'
    parent=torch.load(PARENT/f'MNG_ManagerV2-{STEP}.pt',map_location='cpu',weights_only=False)
    candidate=torch.load(bc/'bc-parent-step.pt',map_location='cpu',weights_only=False)
    pool=torch.load(PARENT/'mng-v2-pool.pt',map_location='cpu',weights_only=False)
    assert pool['step']==STEP and int(parent['global_step']['_GlobalSteps__global_step'])==STEP
    assert sha(PARENT/f'MNG_ManagerV2-{STEP}.pt')=='889169f22546f2684f7a7d4525c6d8fe21dde275c267c524b0b8aa2b7dacb380'
    assert sha(PARENT/'mng-v2-pool.pt')=='659dd332666e6de386d85da71e7c6dacbf881053bad3724cb54d55a3abe1352d'
    assert set(parent)==set(candidate)
    assert all(torch.equal(v,candidate['Optimizer:critic'][k]) for k,v in parent['Optimizer:critic'].items())
    assert set(candidate['Policy'])==set(parent['Policy'])
    assert all(v.shape==parent['Policy'][k].shape and torch.isfinite(v).all() for k,v in candidate['Policy'].items())
    # Preserve param-group identity and hyperparameters, explicitly reset all Adam moments.
    candidate['Optimizer:value_optimizer']=copy.deepcopy(parent['Optimizer:value_optimizer'])
    candidate['Optimizer:value_optimizer']['state']={}
    learning_key=f"MNG_ManagerV2?team={pool['ghost']['_learning_team']}"
    pool['deployed'][learning_key]=copy.deepcopy(candidate['Policy'])
    # current_policy_snapshot is a learning-policy cache as well as latest opponent.
    # Preserve the old latest snapshot in the recent pool, then synchronize the cache.
    old_latest=copy.deepcopy(pool['ghost']['current_policy_snapshot'])
    assert any(snapshot_sha(s)==snapshot_sha(old_latest) for s in pool['ghost']['policy_snapshots'])
    pool['ghost']['current_policy_snapshot']={'MNG_ManagerV2':copy.deepcopy(candidate['Policy'])}
    (RESULT/'MNG_ManagerV2').mkdir(parents=True)
    (RESULT/'run_logs').mkdir()
    training_status=json.loads((PARENT.parent/'run_logs/training_status.json').read_text())
    # Carry ELO/version metadata, never parent's checkpoint paths (pruning could delete them).
    fork_status={'MNG_ManagerV2':{'checkpoints':[], 'elo':training_status['MNG_ManagerV2']['elo']},
                 'metadata':training_status['metadata']}
    dump(RESULT/'run_logs/training_status.json',fork_status)
    EVIDENCE.mkdir(parents=True)
    source_dir=EVIDENCE/'source';source_dir.mkdir()
    files=[Path(__file__),ROOT/'Tools/mng_v2_learn.py',ROOT/'Tools/mng_v2_formal_learn.py',
        ROOT/'Tools/mng_player_log_guard.py',ROOT/'Tools/mng_ps_bc.py',ROOT/'Tools/mng_ps_teacher.py']
    hashes={}
    for f in files:
        hashes[str(f.relative_to(ROOT))]=sha(f);(source_dir/f.name).write_bytes(f.read_bytes())
    originals={str(f.relative_to(ROOT)):sha(f) for f in PARENT.iterdir() if f.suffix in ('.pt','.onnx')}
    info=json.loads((BUILD/'build-info.json').read_text())
    parent_manifest=json.loads((ROOT/'Logs/MNG-Rebuild'/PARENT_RUN/'manifest.json').read_text(encoding='utf-8-sig'))
    manifest=dict(run=RUN,generation='v3',historicalPolicy='',schema='MNG-OBS-v2-244',
        baseActorSourceSha=parent_manifest['baseActorSourceSha'],baseSnapshotSha=pool['baseSnapshotSha'],
        immediateParent=PARENT_RUN,parentStep=STEP,parentCheckpointSha=sha(PARENT/f'MNG_ManagerV2-{STEP}.pt'),
        bcCheckpointSha=sha(bc/'bc-parent-step.pt'),datasetSha=report['datasetSha'],
        initialActorSnapshotSha=snapshot_sha({'MNG_ManagerV2':candidate['Policy']}),
        runtimeSha=info['runtimeSha256'],environmentRevision=info['environmentRevision'],
        sourceHashes=hashes,parentArtifactHashes=originals,workers=32,seed=929401,
        minAdditionalSteps=400000,maxAdditionalSteps=1000000,status='PS-training-candidate',
        optimizer='fresh Adam moments; parent critic retained',
        pool='parent pinned/recent/scheduling/RNG preserved; learning deployed and current cache replaced with BC')
    dump(EVIDENCE/'manifest.json',manifest)
    torch.save(candidate,RESULT/'MNG_ManagerV2/checkpoint.pt')
    torch.save(pool,RESULT/'MNG_ManagerV2/mng-v2-pool.pt')
    (EVIDENCE/'PS3-initial.pt').write_bytes((RESULT/'MNG_ManagerV2/checkpoint.pt').read_bytes())
    (EVIDENCE/'PS3-initial-pool.pt').write_bytes((RESULT/'MNG_ManagerV2/mng-v2-pool.pt').read_bytes())
    (RESULT/'MNG_ManagerV2/PS2-BC.onnx').write_bytes((bc/'bc-policy.onnx').read_bytes())
    dump(EVIDENCE/'progress.json',dict(parentStep=STEP,additionalSteps=0,status='prepared-not-trained'))
    print(json.dumps(manifest,indent=2))


def train(additional):
    assert 0<additional<=990000, 'Reserve batch margin below the strict 1M maximum'
    assert not (EVIDENCE/'STOP').exists(), 'Unresolved STOP marker'
    manifest=json.loads((EVIDENCE/'manifest.json').read_text())
    for name,identity in manifest['sourceHashes'].items():
        assert sha(ROOT/name)==identity, ('Prepared source changed',name)
    for name,identity in manifest['parentArtifactHashes'].items():
        assert sha(ROOT/name)==identity, ('Parent changed',name)
    info=json.loads((BUILD/'build-info.json').read_text())
    for name,key in [('MNG_MS3V2.exe','executableSha256'),('MNG_MS3V2_Data/Managed/MNG.Runtime.dll','runtimeSha256'),('MNG_MS3V2_Data/level0','levelSha256')]:
        assert sha(BUILD/name)==info[key],name
    s=torch.load(RESULT/'MNG_ManagerV2/checkpoint.pt',map_location='cpu',weights_only=False)
    pool=torch.load(RESULT/'MNG_ManagerV2/mng-v2-pool.pt',map_location='cpu',weights_only=False)
    step=int(s['global_step']['_GlobalSteps__global_step'])
    assert pool['step']==step and STEP<=step<STEP+additional
    segment=EVIDENCE/f'PS-to-{additional}';segment.mkdir(exist_ok=False)
    config=yaml.safe_load((ROOT/'Assets/_Soccer/Manager/Training/MNG_MS3V2.yaml').read_text())
    config['behaviors']['MNG_ManagerV2']['max_steps']=STEP+990000
    config['behaviors']['MNG_ManagerV2']['keep_checkpoints']=64
    (segment/'config.yaml').write_text(yaml.safe_dump(config))
    os.environ.update(MNG_V2_STOP_AT=str(STEP+additional),MNG_V2_EVIDENCE=str(EVIDENCE),
        MNG_V2_PLAYER_LOGS=str(RESULT/'run_logs'),MNG_V2_POOL_SEED='20260922',MNG_EXPERIMENT_GENERATION='v3',
        MNG_V3_INITIAL_SNAPSHOT_SHA=manifest['baseSnapshotSha'])
    os.environ.pop('MNG_V2_PINNED_POLICY',None)
    import mlagents.trainers.trainer
    from mlagents.trainers.model_saver.torch_model_saver import TorchModelSaver
    def strict_load(self, load_path, policy=None, reset_global_steps=False):
        state=torch.load(load_path,map_location='cpu',weights_only=False)
        modules=self.modules if policy is None else policy.get_modules()
        selected=self.policy if policy is None else policy
        assert set(modules)<=set(state), 'Missing required checkpoint modules'
        for name,module in modules.items():
            if isinstance(module,torch.nn.Module):
                assert all(torch.isfinite(v).all() for v in state[name].values())
                module.load_state_dict(state[name],strict=True)
            else:
                module.load_state_dict(state[name])
        if reset_global_steps:selected.set_step(0)
    TorchModelSaver._load_model=strict_load
    import mng_v2_learn
    original_advance=mng_v2_learn.MNGV2GhostTrainer.advance
    def audited_advance(self):
        restoring=self.pending_restore is not None
        expected=copy.deepcopy(self.pending_restore['deployed']) if restoring else None
        result=original_advance(self)
        if restoring:
            from mng_v2_learn import snapshot_sha
            key=f'MNG_ManagerV2?team={self._learning_team}'
            actual=snapshot_sha({'MNG_ManagerV2':self.policies[key].get_weights()})
            wanted=snapshot_sha({'MNG_ManagerV2':expected[key]})
            assert actual==wanted, 'Restored learning actor overwritten'
            internal=self.trainer.policy.actor.state_dict()
            assert snapshot_sha({'MNG_ManagerV2':internal})==wanted, 'PPO actor differs from deployed actor'
            self._audit('PS-restore-actor-verified',sha=actual)
        if self.get_step-STEP>1000000:
            (EVIDENCE/'STOP').write_text('Additional step hard cap exceeded')
            raise KeyboardInterrupt()
        return result
    mng_v2_learn.MNGV2GhostTrainer.advance=audited_advance
    sys.argv=[str(ROOT/'Tools/mng_v2_formal_learn.py'),str(segment/'config.yaml'),
        '--run-id',RUN,'--results-dir',str(ROOT/'results'),'--env',str(BUILD/'MNG_MS3V2.exe'),
        '--num-envs','32','--base-port','6500','--seed','929401','--torch-device','cuda',
        '--timeout-wait','120','--max-lifetime-restarts','0','--no-graphics','--resume',
        '--env-args','-mngEvidenceDir',str(segment/'workers'),'-mngRunId',RUN,
        '-mngBuildSha',info['runtimeSha256'],'-mngPolicyAssistMode','none','-mngFullTrace','true']
    from mng_v2_formal_learn import main
    main()
    final=torch.load(RESULT/'MNG_ManagerV2/checkpoint.pt',map_location='cpu',weights_only=False)
    final_pool=torch.load(RESULT/'MNG_ManagerV2/mng-v2-pool.pt',map_location='cpu',weights_only=False)
    end=int(final['global_step']['_GlobalSteps__global_step'])
    assert final_pool['step']==end
    assert all(torch.isfinite(v).all() for v in final['Policy'].values())
    dump(segment/'saved.json',dict(step=end,additionalSteps=end-STEP,checkpointSha=sha(RESULT/'MNG_ManagerV2/checkpoint.pt'),
        poolSha=sha(RESULT/'MNG_ManagerV2/mng-v2-pool.pt'),stopped=(EVIDENCE/'STOP').exists()))
    assert not (EVIDENCE/'player-failure.json').exists(), 'Player failed; saved and stopped'


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--prepare-bc');p.add_argument('--additional',type=int)
    a=p.parse_args()
    if a.prepare_bc:prepare(a.prepare_bc)
    elif a.additional:train(a.additional)
    else:p.error('Choose --prepare-bc or --additional')

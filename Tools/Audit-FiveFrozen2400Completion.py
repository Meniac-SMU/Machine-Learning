"""Read-only verification of the authorized 2.4M run; writes a separate receipt."""
import os
os.environ['CUDA_VISIBLE_DEVICES'] = '-1'
os.environ['OMP_NUM_THREADS'] = '1'
import hashlib
import json
import time
from pathlib import Path
from importlib.machinery import SourceFileLoader
import numpy as np
import torch

ROOT = Path(__file__).resolve().parent.parent
BASE = ROOT / 'Logs/Manager5v5/p9-frozen-v8-20261015/MNG5_V5-20261016-r001'
MODEL = ROOT / 'results/Manager5v5/MNG5_V5-20261016-r001/MNG5_Manager_v5'
BEHAVIOR = 'MNG5_Manager_v5'

def read(p):
    return json.loads(p.read_text(encoding='utf-8-sig'))

def sha(p):
    h = hashlib.sha256()
    with p.open('rb') as f:
        for chunk in iter(lambda: f.read(1024*1024), b''):
            h.update(chunk)
    return h.hexdigest()

def finite(v):
    if torch.is_tensor(v):
        assert torch.isfinite(v).all(), 'Non-finite tensor'
    elif isinstance(v, np.ndarray) and v.dtype.kind in 'fciub':
        assert np.isfinite(v).all(), 'Non-finite array'
    elif isinstance(v, dict):
        for x in v.values(): finite(x)
    elif isinstance(v, (list, tuple)):
        for x in v: finite(x)
    elif isinstance(v, float):
        assert np.isfinite(v), 'Non-finite float'

def main():
    torch.set_num_threads(1)
    status = read(BASE/'orchestrator-status.json')
    assert status['state'] == 'complete' and status['targetSteps'] == 2400000
    registry = read(BASE/'model-registry.json')
    assert registry['champion'] is None and registry['fromScratch'] and registry['selfPlayOnly']
    stages = registry['stages']
    assert [x['step'] for x in stages] == list(range(0,2400001,200000))
    checks = []
    for item in stages:
        step = item['step']; onnx = Path(item['onnx']); pt = onnx.with_suffix('.pt')
        assert sha(onnx) == item['onnxSha256'] and sha(pt) == item['ptSha256']
        parity = read(BASE/f'parity-{step:07d}.json')
        assert parity['passed'] and parity['step'] == step
        assert parity['onnxSha256'] == item['onnxSha256'] and parity['ptSha256'] == item['ptSha256']
        checkpoint = torch.load(pt,map_location='cpu'); finite(checkpoint)
        assert int(checkpoint['global_step']['_GlobalSteps__global_step']) == step
        poolpath = MODEL/f'five-pool-{step}.pt'
        pool = torch.load(poolpath,map_location='cpu'); finite(pool)
        assert pool['step'] == step and pool['optimizerUpdates'] == (parity['optimizerSteps'][0] if step else 0)
        for key in ['ppo_buffer','ghost','numpy_rng','python_rng','torch_rng','deployed','controller_queue']:
            assert key in pool
        if step:
            segment = BASE/f'to-{step:07d}-16env-cuda'
            result = read(segment/'process-result.json'); stop = read(segment/'safe-stop.json')
            assert not result['failures'] and result['traceAudit']['traceFiles'] == 16
            assert stop['steps'][BEHAVIOR] == step and not stop['requestedStop']
            assert read(segment/'exit.json')['exitCode'] == 0
            assert read(segment/'cuda-device.json')['devices'] == ['cuda:0'] and pool['cuda_rng'] is not None
        checks.append(dict(step=step,optimizerUpdates=pool['optimizerUpdates'],ptSha256=sha(pt),onnxSha256=sha(onnx),poolSha256=sha(poolpath),parityError=parity['maxProbabilityError']))
    for name in ['checkpoint.pt','five-pool.pt']:
        saved=torch.load(MODEL/name,map_location='cpu'); finite(saved)
        step = saved['step'] if name.startswith('five-') else int(saved['global_step']['_GlobalSteps__global_step'])
        assert step == 2400000
    results = read(BASE/'stage-results.json'); assert [x['step'] for x in results] == [x['step'] for x in stages]
    games = 0; evaluations=[]
    for stage in results:
        step=stage['step']; expected = ['strong']+[f'stage-{s:07d}' for s in range(0,step,400000)] if step and step%400000==0 else []
        assert set(stage['evaluations']) == set(expected)
        for opponent, summary in stage['evaluations'].items():
            out=BASE/f'eval-{step:07d}-{opponent}'
            assert summary['passed'] and summary['games']==24
            assert read(out/'final-match-audit.json')['games']==24
            assert read(BASE/f'eval-{step:07d}-{opponent}-process.json')['exitCode']==0
            games+=24
            evaluations.append(dict(step=step,opponent=opponent,**{k:summary[k] for k in ['wins','draws','losses','goalsFor','goalsAgainst']}))
    assert games==648
    frozen=SourceFileLoader('completion_frozen',str(ROOT/'Tools/Verify-FiveFrozen.py')).load_module().verify()
    assert sha(BASE/'transition-cpu-to-cuda/cpu-run-preserved.zip')=='89f418cef8f701a3a75dbd2a2ad3c27f65900dc24ccf0658d9ce6e32925495aa'
    preserved=read(BASE/'extension-1200-to-2000/phase-1200-preserved/preservation.json')
    for item in preserved: assert sha(Path(item['copy']))==item['sha256']
    # Historical named models remain identical even though the live checkpoint advances.
    for item in preserved:
        if Path(item['source']).name in ['MNG5_Manager_v5-1200000.pt','MNG5_Manager_v5-1200000.onnx','five-pool-1200000.pt']:
            assert sha(Path(item['source']))==item['sha256']
    preserved2000=read(BASE/'extension-2000-to-2400/preservation.json')
    assert sha(Path(preserved2000['archive']))==preserved2000['archiveSha256']
    for item in preserved2000['files']:
        name=Path(item['path']).name
        if name.startswith('MNG5_Manager_v5-') or name.startswith('five-pool-'):
            assert sha(MODEL.parent/item['path'])==item['sha256'], item['path']
    import yaml
    old=yaml.safe_load((ROOT/'docs/soccer/rebuild-5v5/p9-frozen-v8-16env-cuda.yaml').read_text(encoding='utf-8'))
    new=yaml.safe_load((ROOT/'docs/soccer/rebuild-5v5/p9-frozen-v8-16env-cuda-2400.yaml').read_text(encoding='utf-8'))
    old['behaviors'][BEHAVIOR]['max_steps']=2400000
    assert old==new
    resumed=[]
    for end,start in [(2200000,2000000),(2400000,2200000)]:
        segment=BASE/f'to-{end:07d}-16env-cuda'
        device=read(segment/'cuda-device.json')
        prior=next(x for x in checks if x['step']==start)
        assert device['optimizerUpdatesAtEntry']==prior['optimizerUpdates']
        metrics=[json.loads(line) for line in (segment/'optimizer.jsonl').read_text(encoding='utf-8').splitlines()]
        first=metrics[0];last=metrics[-1]
        assert first['update']==prior['optimizerUpdates']+1
        assert first['metrics']['Policy/Learning Rate']>1e-10
        assert last['metrics']['Policy/Learning Rate']<=first['metrics']['Policy/Learning Rate']
        previous=torch.load(MODEL/f'{BEHAVIOR}-{start}.pt',map_location='cpu')['Policy']
        current=torch.load(MODEL/f'{BEHAVIOR}-{end}.pt',map_location='cpu')['Policy']
        changed=any(not torch.equal(previous[k],current[k]) for k in previous)
        assert changed, 'No parameter changes during extension'
        resumed.append(dict(fromStep=start,toStep=end,optimizerAtEntry=device['optimizerUpdatesAtEntry'],firstLearningRate=first['metrics']['Policy/Learning Rate'],lastLearningRate=last['metrics']['Policy/Learning Rate'],policyParametersChanged=changed))
    result=dict(passed=True,utc=time.time(),targetSteps=2400000,checkpointCount=len(checks),evaluationGames=games,optimizerUpdates=checks[-1]['optimizerUpdates'],frozen=frozen,originalCpuArchivePreserved=True,phase1200Preserved=True,phase2000Preserved=True,extensionResumeChecks=resumed,champion=None,checkpoints=checks,evaluations=evaluations)
    (BASE/'completion-verification-2400.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({k:v for k,v in result.items() if k not in ['checkpoints','evaluations']}))

if __name__ == '__main__': main()

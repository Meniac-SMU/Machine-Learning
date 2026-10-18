"""Verify the exact 2.8M continuation, evaluations, resume state, and preservation."""
from collections.abc import Mapping
import shutil
import torch
from five_v10_run_common_r2 import *
from five_v10_migration import same_state,validate_pool
import five_v10_run_common as old_common


def equal(x,y):
    if isinstance(x,Mapping):
        return isinstance(y,Mapping) and x.keys()==y.keys() and all(equal(x[k],y[k]) for k in x)
    return same_state(x,y)


def main():
    base=BASE/RUN;model=model_dir(RUN)
    assert read(base/'orchestrator-status.json')['state']=='complete'
    assert not (base/'monitor-intervention.json').exists()
    stages=read(base/'stage-results.json');registry=read(base/'model-registry.json')
    assert [s['step'] for s in stages]==[2600000,2800000]
    assert registry['champion'] is None
    checkpoints=[]
    for step in (2400000,2600000,2800000):
        checkpoint=model/f'{BEHAVIOR}-{step}.pt';poolfile=model/f'five-pool-{step}.pt';onnx=model/f'{BEHAVIOR}-{step}.onnx'
        saved=torch.load(checkpoint,map_location='cpu');pool=torch.load(poolfile,map_location='cpu')
        assert int(saved['global_step']['_GlobalSteps__global_step'].item())==pool['step']==step
        validate_pool(pool)
        checkpoints.append(dict(step=step,optimizerUpdates=pool['optimizerUpdates'],ptSha256=sha(checkpoint),onnxSha256=sha(onnx),poolSha256=sha(poolfile)))
    previous_updates=27888
    for stage in stages:
        step=stage['step'];folder=base/f'to-{step}-32env-cuda'
        source=read(folder/'resume-input.json');verified=read(folder/'segment-verification.json')
        assert source['step']==step-200000 and source['optimizerUpdates']==previous_updates
        assert verified['passed'] and verified['step']==step and verified['optimizerUpdates']>previous_updates
        assert read(folder/'cuda-device.json')['devices']==['cuda:0']
        assert read(folder/'exit.json')['exitCode']==0
        assert not read(folder/'process-result.json')['failures']
        stop=read(folder/'safe-stop.json');assert not stop['requestedStop'] and stop['steps'][BEHAVIOR]==step
        assert read(base/f'parity-{step}.json')['passed']
        previous_updates=verified['optimizerUpdates']
    resume=read(base/'to-2800000-32env-cuda/resume-input.json')
    stop2600=base/'to-2600000-32env-cuda/five-pool-final-stop-2600000.pt'
    assert sha(stop2600)==resume['poolSha']
    a=torch.load(model/'five-pool-2600000.pt',map_location='cpu');b=torch.load(stop2600,map_location='cpu')
    assert all(equal(a[k],b[k]) for k in a if k!='cuda_rng')
    assert b['ppo_buffer'].num_experiences==resume['bufferExperiences']
    final=torch.load(model/'checkpoint.pt',map_location='cpu')
    assert equal(final,torch.load(model/f'{BEHAVIOR}-2800000.pt',map_location='cpu'))
    final_pool=torch.load(model/'five-pool.pt',map_location='cpu')
    assert final_pool['step']==2800000 and final_pool['optimizerUpdates']==previous_updates
    periodic=torch.load(model/'five-pool-2800000.pt',map_location='cpu')
    differences=[k for k in periodic if not equal(periodic[k],final_pool[k])]
    assert set(differences)<= {'cuda_rng'},differences
    final_stop=base/'to-2800000-32env-cuda/five-pool-final-stop-2800000.pt'
    if final_stop.exists():assert sha(final_stop)==sha(model/'five-pool.pt')
    else:shutil.copy2(model/'five-pool.pt',final_stop)
    evaluation=stages[1]['evaluations']
    assert set(evaluation)=={'strong',*[f'stage-{i}' for i in range(0,2400001,400000)]}
    for name,summary in evaluation.items():
        assert summary['passed'] and summary['games']==24
        assert read(base/f'eval-2800000-{name}-process.json')['exitCode']==0
        assert read(base/f'eval-2800000-{name}/protocol.json')['seconds']==300
    assert sum(s['games'] for s in evaluation.values())==192
    assert read(base/'evaluation40-status.json')['state']=='complete'
    combined=read(base/'evaluation40-results.json')
    assert combined['passed'] and combined['games']==320 and set(combined['evaluations'])==set(evaluation)
    for name,summary in combined['evaluations'].items():
        assert summary['passed'] and summary['games']==40
        assert read(base/f'supplement-{name}-process.json')['exitCode']==0
        provenance=read(base/f'eval-2800000-{name}-combined40/provenance.json')
        assert len(provenance['results'])==40 and provenance['seeds']==list(range(63000,63020))
        for item in provenance['results']:assert sha(ROOT/item['path'])==item['sha256']
    preserved=read(BASE/'parent-preservation.json')
    for item in preserved['files']+preserved['records']:assert sha(ROOT/item['path'])==item['sha256'],item['path']
    failed=read(old_common.BASE/'fatal-stop-verification.json')
    for item in failed['savedFiles']:assert sha(ROOT/item['path'])==item['sha256']
    assert sha(ROOT/'ProjectSettings/ProjectSettings.asset')==sha(old_common.BASE/'ProjectSettings-before.asset')
    receipt=dict(passed=True,run=RUN,parentRun='MNG5_V5-20261016-r001',startStep=2400000,step=2800000,
        additionalDecisions=400000,optimizerUpdates=previous_updates,additionalOptimizerUpdates=previous_updates-27888,
        environments=32,device='cuda:0',seconds=300,timeScale=100,reward='score only',champion=None,
        regularCheckpoints=checkpoints,formalEvaluationGames=320,originalEvaluationGames=192,supplementalEvaluationGames=128,
        evaluations={name:{k:s[k] for k in ('games','wins','draws','losses','goalsFor','goalsAgainst')} for name,s in combined['evaluations'].items()},
        pendingExperiencesResumedAt2600000=resume['bufferExperiences'],resumePoolSha256=sha(stop2600),
        finalStopPoolSha256=sha(final_stop),finalStopPoolDifferencesFromPeriodic=differences,
        finalStopPoolNote='Periodic checkpoint and final ONNX export may consume CUDA RNG; exact final-stop state is preserved separately and was used for resume.',
        parentFilesUnchanged=len(preserved['files']),parentEvidenceUnchanged=len(preserved['records']),
        failedSavedFilesUnchanged=len(failed['savedFiles']),originalFrozenInputs=old_common.verify(),repairFrozenInputs=verify(),
        projectSettingsUnchanged=True,scheduledMonitor=False)
    write(base/'completion-verification.json',receipt)
    print(json.dumps(receipt,ensure_ascii=False))


if __name__=='__main__':main()

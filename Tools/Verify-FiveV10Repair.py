"""Record real preflight, preservation, and opponent-pool repair evidence."""
import json
import zipfile
import hashlib
import torch
from five_v10_run_common_r2 import *
from five_v10_migration import validate_pool
import five_v10_run_common as old_common


def main():
    pre=BASE/PREFLIGHT
    result=read(BASE/'resume-verification.json')
    assert result['passed'] and read(pre/'orchestrator-status.json')['state']=='complete'
    stages=[read(pre/f'to-{step}-32env-cuda/segment-verification.json') for step in (2405000,2412000)]
    first_input=read(pre/'to-2405000-32env-cuda/resume-input.json')
    second_input=read(pre/'to-2412000-32env-cuda/resume-input.json')
    assert first_input['step']==2400000 and first_input['optimizerUpdates']==27888
    assert second_input['step']==stages[0]['step']
    assert second_input['optimizerUpdates']==stages[0]['optimizerUpdates']
    assert second_input['poolSha']==stages[0]['poolSha256']
    assert second_input['bufferExperiences']>0
    for stage in stages:
        folder=pre/f"to-{stage['step']}-32env-cuda"
        assert stage['passed'] and read(folder/'exit.json')['exitCode']==0
        assert read(folder/'cuda-device.json')['devices']==['cuda:0']
        assert not read(folder/'process-result.json')['failures']
        validate_pool(torch.load(model_dir(PREFLIGHT)/f"five-pool-{stage['step']}.pt",map_location='cpu'))
    assert 27888<stages[0]['optimizerUpdates']<stages[1]['optimizerUpdates']
    events=[json.loads(line) for line in (model_dir(PREFLIGHT)/'five-pool.jsonl').read_text().splitlines()]
    swaps=[item for item in events if item['event']=='opponent-swap']
    historical=sum(item['opponent']>=0 for item in swaps)
    current=sum(item['opponent']==-1 for item in swaps)
    assert historical>0 and current>0
    preserved=read(BASE/'parent-preservation.json')
    for item in preserved['files']+preserved['records']:
        assert sha(ROOT/item['path'])==item['sha256'],item['path']
    with zipfile.ZipFile(preserved['archive']) as archive:
        for item in preserved['files']:
            digest=hashlib.sha256()
            with archive.open(item['path']) as stream:
                for block in iter(lambda:stream.read(4*1024*1024),b''):digest.update(block)
            assert digest.hexdigest()==item['sha256']
    failed=read(old_common.BASE/'fatal-stop-verification.json')
    for item in failed['savedFiles']:assert sha(ROOT/item['path'])==item['sha256']
    assert sha(ROOT/'ProjectSettings/ProjectSettings.asset')==sha(old_common.BASE/'ProjectSettings-before.asset')
    original_inputs=old_common.verify();new_inputs=verify()
    production=torch.load(model_dir(RUN)/'five-pool.pt',map_location='cpu')
    validate_pool(production)
    assert production['step']==2400000 and production['optimizerUpdates']==27888
    assert not (BASE/RUN/'orchestrator-status.json').exists()
    receipt=dict(passed=True,repair='All historical/current opponent snapshot behavior keys migrated and validated before launch',
        migrationTests=3,sdkSwapPaths=18,stages=stages,resumePendingExperiences=second_input['bufferExperiences'],
        successfulActualSwaps=len(swaps),historicalSwaps=historical,currentSwaps=current,
        ruleEvaluation=result['ruleEvaluation'],parity=read(pre/'parity-2412000.json'),
        parentFilesUnchanged=len(preserved['files']),parentEvidenceUnchanged=len(preserved['records']),
        failedSavedFilesUnchanged=len(failed['savedFiles']),parentArchiveVerified=True,
        projectSettingsUnchanged=True,originalFrozenInputs=original_inputs,repairFrozenInputs=new_inputs,
        productionStartedAtVerification=False,productionPreparedFrom=2400000,productionRun=RUN)
    write(BASE/'repair-verification.json',receipt)
    print(json.dumps({k:receipt[k] for k in ('passed','successfulActualSwaps','historicalSwaps','currentSwaps','parentFilesUnchanged','originalFrozenInputs','repairFrozenInputs')}))


if __name__=='__main__':main()

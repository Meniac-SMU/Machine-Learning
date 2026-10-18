"""Verify completion separately from tactic-usage and strength outcomes."""
import hashlib
import json
from pathlib import Path
import time
from five_evaluation_audit import audit

ROOT=Path(__file__).resolve().parent.parent
EVIDENCE=ROOT/'Logs/Manager5v5/p9-tactics-20261014'
RUN='MNG5_V2-20261014-r002'
BASE=EVIDENCE/RUN
def read(p):return json.loads(Path(p).read_text(encoding='utf-8-sig'))
def sha(p):
    h=hashlib.sha256()
    with Path(p).open('rb') as f:
        for b in iter(lambda:f.read(4*1024*1024),b''):h.update(b)
    return h.hexdigest()
def main():
    assert not (BASE/'completion.json').exists()
    status=read(BASE/'orchestrator-status.json');assert status['state']=='complete' and status['mode']=='evaluation-only'
    assert status['selectedStep']==505386 and status['furtherTraining'] is False
    registry=read(BASE/'model-registry.json');stages=read(BASE/'stage-results.json')
    expected=[0,200000,400000,505386]
    assert [s['step'] for s in stages]==[m['step'] for m in registry['stages']]==expected
    assert registry['fromScratch'] and registry['selfPlayOnly'] and registry['champion'] is None
    updates=0;matches=0;games=0;errors=[]
    for stage,model in zip(stages,registry['stages']):
        step=stage['step'];segment=BASE/('to-0600000' if step==505386 else f'to-{step:07d}');result=read(segment/'process-result.json')
        assert not result['failures'] and result['traceAudit']['traceFiles']==32
        assert result['traceAudit']['terminals']%2==0;matches+=result['traceAudit']['terminals']//2
        if step:
            stop=read(segment/'safe-stop.json');assert stop['steps']=={'MNG5_Manager_v2':step} and stop['requestedStop']==(step==505386)
            assert result['optimizerUpdates']>updates
        else:assert result['optimizerUpdates']==0
        updates=result['optimizerUpdates']
        assert sha(model['onnx'])==model['onnxSha256']
        assert sha(Path(model['onnx']).with_suffix('.pt'))==model['ptSha256']
        assert (Path(model['onnx']).parent/f'five-pool-{step}.pt').is_file()
        parity=read(BASE/f'parity-{step:07d}.json');assert parity['passed'] and parity['step']==step
        if step:assert parity['optimizerSteps']==[updates]
        errors.append(parity['maxProbabilityError'])
        opponents=['strong']+[f'old-{n:07d}' for n in range(0,1200001,200000)]+[f'stage-{n:07d}' for n in expected if n<step]+['early-0200011']
        assert list(stage['evaluations'])==opponents
        for label in opponents:
            folder=BASE/f'eval-{step:07d}-{label}';summary=read(folder/'summary.json')
            assert summary['passed'] and summary['games']==24
            assert audit(folder,expected_runtime='tactics-v3-expansion')['passed'];games+=24
    assert games==1008
    baseline=read(BASE/'baseline-results.json');assert len(baseline)==8
    for label in baseline:
        assert baseline[label]['passed'] and baseline[label]['games']==24
        assert audit(BASE/f'baseline-1200000-{label}',expected_runtime='tactics-v3-expansion')['passed']
    freeze=read(EVIDENCE/'frozen-version.json')
    for item in freeze['source']+freeze['payload']+freeze['executionFiles']:
        assert sha(ROOT/item['path']).lower()==item['sha256'].lower(),item['path']
    preserved={}
    for label,path in [('originals','Logs/Manager5v5/initial-20261012/original-files.json'),('earlyP9','Logs/Manager5v5/p9-20261013/final-files.json')]:
        items=read(ROOT/path)
        for item in items:assert sha(ROOT/item['path']).lower()==item['sha256'].lower(),item['path']
        preserved[label]=len(items)
    old=read(ROOT/'Logs/Manager5v5/p9-selfplay-20261014/MNG5_V2-20261014-r001/model-registry.json')
    for item in old['stages']:
        assert sha(item['onnx'])==item['onnxSha256']
        assert sha(Path(item['onnx']).with_suffix('.pt'))==item['ptSha256']
    preserved['previousSelfPlayModels']=len(old['stages'])
    prior=read(ROOT/'Logs/Manager5v5/p9-selfplay-20261014/frozen-version.json')
    for item in prior['payload']:
        assert sha(ROOT/item['path']).lower()==item['sha256'].lower(),item['path']
    preserved['previousBuildFiles']=len(prior['payload'])
    for path in ('ProjectSettings/ProjectSettings.asset','ProjectSettings/EditorBuildSettings.asset','ProjectSettings/ProjectVersion.txt','Packages/manifest.json','Packages/packages-lock.json'):
        assert (ROOT/path).read_bytes()==(EVIDENCE/Path(path).name).read_bytes(),path
    cap=read(BASE/'cap-change-500000.json');assert cap['restoredStep']==400000 and cap['observedStop']==505386
    for item in cap['preserved']:
        assert sha(Path(cap['archive'])/'results'/item['path'])==item['sha256']
    discarded=read(BASE/'to-0500000/process-result.json')
    selection=read(BASE/'provisional-selection.json');assert selection['selectedStep']==505386 and not selection['furtherTraining']
    assert sha(selection['model'])==selection['onnxSha256']
    assert sha(Path(selection['model']).with_suffix('.pt'))==selection['ptSha256']
    assert sha(Path(selection['model']).parent/'five-pool.pt')==selection['poolSha256']
    assert not discarded['failures']
    report=read(BASE/'report-data.json');assert report['stages'][-1]['step']==505386 and report['games']==1008
    receipt=dict(passed=True,state='provisional-505386-evaluation-complete',run=RUN,step=505386,optimizerUpdates=updates,
        trainingCompletedMatches=matches,notSelectedRetrainSteps=71854,notSelectedRetrainMatches=discarded['traceAudit']['terminals']//2,
        selection=selection['userInstruction'],selectedOriginalStop=505386,furtherTraining=False,stageEvaluationGames=games,baselineComparisonGames=192,totalEvaluationGames=1200,
        stages=expected,preflightExcluded=True,playerRuntime=freeze['playerRuntime'],ruleSelector=freeze['ruleSelector'],
        contract=freeze['contract'],reward=freeze['reward'],preserved=preserved,largestParityError=max(errors),
        observedUsageTargetAtProvisional=report['stages'][-1]['withinTarget'],usageRatiosAtProvisional=report['stages'][-1]['actionRatios'],
        aggregateStrengthComparison=report['aggregateStrengthComparison'],
        evaluationRunnerSha256=sha(ROOT/'Tools/Evaluate-FiveTacticProvisional.py'),
        goal1200k='Not reached. User selected original 505386-step stop as provisional final; no further training.',
        completed=time.time(),champion=None,limitations='One training initialization. Standard kickoff and paired policy-sampling seeds. Technical completion is separate from strength improvement.')
    (BASE/'completion.json').write_text(json.dumps(receipt,ensure_ascii=False,indent=2),encoding='utf-8')
    output=ROOT/'docs/soccer/rebuild-5v5/p9-tactics-model-registry.json';assert not output.exists()
    output.write_text(json.dumps(dict(**registry,status='provisional-505386-evaluation-complete',completion=receipt),ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(receipt,ensure_ascii=False))
if __name__=='__main__':main()

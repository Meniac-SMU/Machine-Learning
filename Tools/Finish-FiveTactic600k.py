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
    status=read(BASE/'orchestrator-status.json');assert status['state']=='complete' and status['mode']=='train'
    assert status['authorizedStop']==600000
    registry=read(BASE/'model-registry.json');stages=read(BASE/'stage-results.json')
    expected=[0,200000,400000,600000]
    assert [s['step'] for s in stages]==[m['step'] for m in registry['stages']]==expected
    assert registry['fromScratch'] and registry['selfPlayOnly'] and registry['champion'] is None
    updates=0;matches=0;games=0;errors=[]
    for stage,model in zip(stages,registry['stages']):
        step=stage['step'];segment=BASE/f'to-{step:07d}';result=read(segment/'process-result.json')
        assert not result['failures'] and result['traceAudit']['traceFiles']==32
        assert result['traceAudit']['terminals']%2==0;matches+=result['traceAudit']['terminals']//2
        if step:
            stop=read(segment/'safe-stop.json');assert stop['steps']=={'MNG5_Manager_v2':step} and not stop['requestedStop']
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
    report=read(BASE/'report-data.json');assert report['stages'][-1]['step']==600000 and report['games']==1008
    receipt=dict(passed=True,state='authorized-600k-complete',run=RUN,step=600000,optimizerUpdates=updates,
        trainingCompletedMatches=matches,stageEvaluationGames=games,baselineComparisonGames=192,totalEvaluationGames=1200,
        stages=expected,preflightExcluded=True,playerRuntime=freeze['playerRuntime'],ruleSelector=freeze['ruleSelector'],
        contract=freeze['contract'],reward=freeze['reward'],preserved=preserved,largestParityError=max(errors),
        observedUsageTargetAt600k=report['stages'][-1]['withinTarget'],usageRatiosAt600k=report['stages'][-1]['actionRatios'],
        goal1200k='Not reached; this authorization ends at 600k. No claim of eventual success.',
        completed=time.time(),champion=None,limitations='One training initialization. Standard kickoff and paired policy-sampling seeds. Technical completion is separate from strength improvement.')
    (BASE/'completion.json').write_text(json.dumps(receipt,ensure_ascii=False,indent=2),encoding='utf-8')
    output=ROOT/'docs/soccer/rebuild-5v5/p9-tactics-model-registry.json';assert not output.exists()
    output.write_text(json.dumps(dict(**registry,status='600k-complete',completion=receipt),ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(receipt,ensure_ascii=False))
if __name__=='__main__':main()

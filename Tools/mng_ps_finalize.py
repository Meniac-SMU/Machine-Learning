"""Verify and save the completed, unsuccessful PS experiment; never promote it."""
import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path

ROOT=Path(__file__).resolve().parent.parent
LOG=ROOT/'Logs/MNG-Rebuild/PS-20260929'
RUN='MNG_MS3V3-20260929-r002'
RESULT=ROOT/'results'/RUN
E=ROOT/'Logs/MNG-Rebuild'/RUN

def read(path):return json.loads(Path(path).read_text())
def sha(path):return hashlib.sha256(Path(path).read_bytes()).hexdigest()

def main():
    p=argparse.ArgumentParser();p.add_argument('--weekly-remaining',type=int,required=True);a=p.parse_args()
    manifest=read(E/'manifest.json');inspection=read(LOG/'inspection-400000.json')
    for rel,value in manifest['parentArtifactHashes'].items():assert sha(ROOT/rel)==value,rel
    for rel,value in manifest['sourceHashes'].items():assert sha(ROOT/rel)==value,rel
    assert sha(RESULT/'MNG_ManagerV2/checkpoint.pt')==inspection['checkpointSha']
    assert sha(RESULT/'MNG_ManagerV2/mng-v2-pool.pt')==inspection['poolSha']
    assert 400000<=inspection['additionalSteps']<=1000000
    evaluation=read(LOG/'candidate-400k/report.json');baseline=read(LOG/'parent-benchmark/report.json')
    assert len(evaluation['matches'])==280 and len(baseline['matches'])==200
    assert not evaluation['playerErrors'] and not baseline['playerErrors']
    assert sha(evaluation['manifest']['candidate'])==evaluation['manifest']['candidateSha']
    comparison=read(LOG/'final-comparison.json')
    assert comparison['duel']['complete'] and not comparison['duel']['candidateWins']
    assert not comparison['rulesImprovementGate'] and not comparison['noOpponentRegressionGate']
    registry=read(ROOT/'docs/soccer/training/ms-v3-model-registry.json')
    assert registry['currentChampion']=='results/MNG_MS3V3-20260925-r002/MNG_ManagerV2/MNG_ManagerV2-2001834.onnx'
    for stage in ('MS3V2','EvaluationV2'):
        build=ROOT/'Builds/MNG_V2/MS3-v3-WorkerIOFix-20260924T182752604Z'/stage
        info=read(build/'build-info.json');name='MNG_'+stage
        assert sha(build/f'{name}_Data/Managed/MNG.Runtime.dll')==info['runtimeSha256']
    index_path=RESULT/'PS-model-index.json';index=read(index_path)
    for stage in index['stages']:
        if 'artifacts' in stage:
            for item in stage['artifacts'].values():assert sha(ROOT/item['path'])==item['sha256']
        else:assert sha(ROOT/stage['artifact'])==stage['sha256']
    bc=RESULT/'MNG_ManagerV2/PS2-BC.onnx'
    index['stages']=sorted(index['stages'],key=lambda s:({'PS2-pressure':0,'PS2-mixed':1}.get(s['stage'],2),s.get('additionalSteps',0)))
    index['stages'].insert(2,dict(stage='PS2-selected',additionalEnvironmentSteps=0,
        artifact=bc.relative_to(ROOT).as_posix(),sha256=sha(bc),gameplaySuccessJudged=False))
    index['stages'][-1].update(gameplaySuccessJudged=True,gameplaySuccess=False)
    index.update(status='completed-quality-failed',exhibitionAdded=False,
        evaluationReport='Logs/MNG-Rebuild/PS-20260929/candidate-400k/report.json',
        completion='Logs/MNG-Rebuild/PS-20260929/completion.json')
    index_path.write_text(json.dumps(index,indent=2))
    result=dict(status='completed-quality-failed',utc=datetime.now(timezone.utc).isoformat(),run=RUN,
        parentRun=manifest['immediateParent'],parentStep=manifest['parentStep'],finalStep=inspection['step'],
        additionalEnvironmentSteps=inspection['additionalSteps'],optimizerUpdates=inspection['optimizerUpdates'],
        trainingObjectiveAudit=read(LOG/'training-objective-audit.json'),trainingCompleted=True,
        candidateFrozenGames=280,parentFrozenGames=200,playerErrors=[],
        parentArtifactsUnchanged=True,preparedSourceHashesUnchanged=True,checkpointPoolUnchangedAfterEvaluation=True,
        runtimeUnchanged=True,championChanged=False,exhibitionAdded=False,passLearningSuccess=False,
        qualityFailures=['parent duel 12W 5D 23L','rules score delta -4.6875 percentage points',
            'carry-shot regression 13.75 points','balanced regression 16.25 points',
            'insufficient useful follow-up and faster-scoring evidence'],
        directVisualPlayVerified=False,visualBlocker='Computer Use app approval timed out; desktop match incomplete',
        stopReason='Minimum budget completed; observed quality failures do not justify repeating the same sparse curriculum',
        stoppedForQuota=False,weeklyRemainingPercent=a.weekly_remaining,quotaStopThresholdRemaining=45,
        finalModel=evaluation['manifest']['candidate'],finalOnnxSha=evaluation['manifest']['candidateSha'],
        finalCheckpointSha=inspection['checkpointSha'],finalPoolSha=inspection['poolSha'],
        comparison='final-comparison.json',passOutcomes='matched-pass-outcomes.json',holdout='final-holdout.json',
        analysisSourceHashes={name:sha(ROOT/'Tools'/name) for name in ('mng_ps_compare.py','mng_ps_pass_report.py','mng_ps_holdout.py','mng_ps_finalize.py')})
    with (LOG/'completion.json').open('x') as f:json.dump(result,f,indent=2)
    print(json.dumps({k:v for k,v in result.items() if k not in ('trainingObjectiveAudit','analysisSourceHashes')},indent=2))

if __name__=='__main__':main()

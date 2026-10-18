"""Preserve verified rule-change evidence without modifying any model."""
import hashlib
import json
import zipfile
from pathlib import Path
import subprocess

ROOT=Path(__file__).resolve().parent.parent
BASE=ROOT/'Logs/Manager5v5/defensive-state-20261014'
DOC=ROOT/'docs/soccer/rebuild-5v5/43_T9수비대형_T7연속제한_골키퍼가속_검증.md'
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
    assert not (BASE/'completion.json').exists()
    coverage=read(BASE/'playmode-coverage.json');assert coverage['passed']==coverage['uniqueTests']==112
    assert read(BASE/'edit-04.json')['passed']==27
    evaluation=read(BASE/'evaluation-r2/completion.json');assert evaluation['passed'] and evaluation['games']==96
    analysis=read(BASE/'evaluation-r2/analysis.json')
    for original in (BASE/'evaluation/before').rglob('*'):
        if original.is_file():
            copied=BASE/'evaluation-r2/before'/original.relative_to(BASE/'evaluation/before')
            assert sha(original)==sha(copied), 'Reused baseline changed'
    for o in ('rule','self'):
        assert analysis['audits'][f'after/{o}']['maxT7Streak']<=3
        assert analysis['audits'][f'after/{o}']['decisionDuringT9']==0
        assert analysis['audits'][f'after/{o}']['t9Entries']==analysis['audits'][f'after/{o}']['t9Exits']
    keeper_hold_diagnostic=all(analysis['audits'][f'after/{o}']['keeperHoldsOver10Seconds']==0 for o in ('rule','self'))
    models=ROOT/'results/Manager5v5/MNG5_V2-20261014-r002/MNG5_Manager_v2'
    model=models/'MNG5_Manager_v2-505386.onnx';pt=model.with_suffix('.pt')
    assert sha(model)=='5d5077dd51edef29b401e97a8c8d88b7c1a0375fb9ffb4ce36ad39fd5dd7d4b2'
    assert sha(pt)=='651be46419f9003d48740967196426f37e9937b71e8da89b5ba509ece9d9e995'
    assert sha(ROOT/'ProjectSettings/ProjectSettings.asset')==sha(BASE/'ProjectSettings.before.asset')
    assert sha(ROOT/'ProjectSettings/EditorBuildSettings.asset')==sha(BASE/'EditorBuildSettings.before.asset')
    executable=ROOT/'Builds/Manager5v5/defensive-state-learning-r2-20261014/Soccer5v5Learning.exe'
    assert read(BASE/'build-defensive-state-learning-r2-20261014/build-result.json')['exitCode']==0
    sources=[dict(path=str(p.relative_to(ROOT)).replace('\\','/'),sha256=sha(p))
             for p in sorted((ROOT/'Assets/_Soccer/Manager5v5').rglob('*')) if p.is_file()]
    payload=[dict(path=str(p.relative_to(executable.parent)).replace('\\','/'),sha256=sha(p))
             for p in sorted(executable.parent.rglob('*')) if p.is_file()]
    with zipfile.ZipFile(BASE/'before-source.zip') as z:
        manifest=json.loads(z.read('source-manifest.json').decode('utf-8-sig'))
        changes=[]
        for row in manifest:
            path=ROOT/row['path']
            if not path.exists() or sha(path).lower()!=row['sha256'].lower():changes.append(row['path'])
        before=z.read('Assets/_Soccer/Manager5v5/Runtime/FiveLearningContract.cs').decode('utf-8-sig').replace('\r\n','\n')
        after=(ROOT/'Assets/_Soccer/Manager5v5/Runtime/FiveLearningContract.cs').read_text(encoding='utf-8-sig')
        marker='public sealed class FiveScoreReward'
        assert marker in before and before.split(marker)[1]==after.split(marker)[1], 'Score reward changed'
    result=dict(passed=keeper_hold_diagnostic,implementationPassed=True,evaluationCompleted=True,
        status='implemented-evaluated-with-known-keeper-distribution-delay',
        keeperHoldDiagnostic=dict(thresholdSeconds=10,passed=keeper_hold_diagnostic,
            explanation='The original physical lock is repaired. Two finite distribution waits remain and are not relabeled as passing the ten-second diagnostic.'),
        qualityImprovementConfirmed=False,scope='Implementation and frozen-model runtime evaluation; no training or promotion',
        playerRuntime='tactics-v4-defensive-state-r2',contract='MNG5-OBS676-A8-R1-M2-v3-T7MASK-T9STATE',
        gitHead=subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip(),
        trainingStarted=False,modelSha256=sha(model),checkpointSha256=sha(pt),modelStep=505386,
        playmode=dict(uniqueTests=112,passedLatest=112,initialPassed=106,initialFailed=5,recordedHoldFailurePreserved=True,coverageFile='playmode-coverage.json'),
        editmode=dict(passed=27,failed=0),evaluation=evaluation,analysis=analysis,
        executable=str(executable),executableSha256=sha(executable),sources=sources,payload=payload,changedExistingFiles=changes,
        rewardUnchanged=True,settingsRestored=True,totalEvaluationGamesExecuted=144,rejectedFirstRevisionGames=48)
    (BASE/'completion.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({k:v for k,v in result.items() if k not in ('sources','payload','changedExistingFiles','analysis')},ensure_ascii=False,indent=2))

if __name__=='__main__':main()

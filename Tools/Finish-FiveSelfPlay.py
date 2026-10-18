"""Create a completion receipt only after every requested stage is proven complete."""
import hashlib
import json
from pathlib import Path
import time
from five_evaluation_audit import audit

ROOT=Path(__file__).resolve().parent.parent
EVIDENCE=ROOT/'Logs/Manager5v5/p9-selfplay-20261014'
RUN='MNG5_V2-20261014-r001'
BASE=EVIDENCE/RUN


def read(p):return json.loads(Path(p).read_text(encoding='utf-8-sig'))
def sha(p):
    h=hashlib.sha256()
    with Path(p).open('rb') as stream:
        for block in iter(lambda:stream.read(4*1024*1024),b''):h.update(block)
    return h.hexdigest()


def main():
    assert not (BASE/'completion.json').exists(), 'Never replace a completion receipt.'
    status=read(BASE/'orchestrator-status.json')
    assert status['state']=='complete' and status['mode']=='train'
    stages=read(BASE/'stage-results.json')
    expected=list(range(0,1200001,200000))
    assert [s['step'] for s in stages]==expected
    registry=read(BASE/'model-registry.json')
    assert registry['fromScratch'] and registry['selfPlayOnly'] and registry['champion'] is None
    assert [s['step'] for s in registry['stages']]==expected
    games=0; training_matches=0; updates=0; parity_errors=[]
    for s,model in zip(stages,registry['stages']):
        step=s['step'];segment=BASE/f'to-{step:07d}'
        result=read(segment/'process-result.json')
        assert not result['failures'] and result['traceAudit']['traceFiles']==32
        assert result['traceAudit']['terminals']%2==0
        training_matches+=result['traceAudit']['terminals']//2
        if step:
            stop=read(segment/'safe-stop.json')
            assert stop['steps']=={'MNG5_Manager_v2':step} and not stop['requestedStop']
            assert result['optimizerUpdates']>updates
        else:assert result['optimizerUpdates']==0
        updates=result['optimizerUpdates']
        assert sha(model['onnx'])==model['onnxSha256']
        assert sha(Path(model['onnx']).with_suffix('.pt'))==model['ptSha256']
        assert (Path(model['onnx']).parent/f'five-pool-{step}.pt').is_file()
        parity=read(BASE/f'parity-{step:07d}.json')
        assert parity['passed'] and parity['step']==step
        assert parity['ptSha256']==model['ptSha256'] and parity['onnxSha256']==model['onnxSha256']
        if step:assert parity['optimizerSteps']==[updates]
        parity_errors.append(parity['maxProbabilityError'])
        opponents=['strong']+[f'stage-{n:07d}' for n in expected if n<step]
        assert list(s['evaluations'])==opponents
        for opponent in opponents:
            path=BASE/f'eval-{step:07d}-{opponent}'
            summary=read(path/'summary.json')
            assert summary==s['evaluations'][opponent]
            assert summary['passed'] and summary['games']==24
            assert audit(path)['passed']
            games+=summary['games']
    assert games==672
    supplementary=read(BASE/'supplemental/status.json')
    assert supplementary['state']=='complete' and len(supplementary['completed'])==3
    assert [r['label'] for r in supplementary['completed']]==['final-vs-recover','recover-vs-strong','final-vs-old-p9']
    supplemental_games=sum(r['summary']['games'] for r in supplementary['completed'])
    assert supplemental_games==72
    freeze=read(EVIDENCE/'frozen-version.json')
    for row in freeze['source']+freeze['payload']:
        assert sha(ROOT/row['path']).lower()==row['sha256'].lower(), row['path']
    for row in read(EVIDENCE/'execution-file-manifest.json'):
        assert sha(ROOT/row['path'])==row['sha256'],row['path']
    preserved={}
    for key,path in [('originals','Logs/Manager5v5/initial-20261012/original-files.json'),
                     ('priorP9','Logs/Manager5v5/p9-20261013/final-files.json')]:
        rows=read(ROOT/path)
        for row in rows:assert sha(ROOT/row['path']).lower()==row['sha256'].lower(),row['path']
        preserved[key]=len(rows)
    for path in ('ProjectSettings/ProjectSettings.asset','ProjectSettings/EditorBuildSettings.asset',
                 'ProjectSettings/ProjectVersion.txt','Packages/manifest.json','Packages/packages-lock.json'):
        assert (ROOT/path).read_bytes()==(EVIDENCE/Path(path).name).read_bytes(),path
    receipt=dict(passed=True,state='complete',run=RUN,step=1200000,optimizerUpdates=updates,
        stages=expected,trainingCompletedMatches=training_matches,primaryEvaluationGames=games,
        supplementaryGames=supplemental_games,totalEvaluationGames=games+supplemental_games,
        preflightExcluded=True,playerRuntime=freeze['playerRuntime'],ruleSelector=freeze['ruleSelector'],
        contract=freeze['contract'],reward=freeze['reward'],preserved=preserved,
        largestParityError=max(parity_errors),frozenSourceFiles=len(freeze['source']),
        payloadFiles=len(freeze['payload']),completed=time.time(),champion=None,
        limitations='Standard kickoff, 12 paired evaluation seeds. No champion promotion or P10 integration. Outcome quality is separate from execution completion.')
    canonical=ROOT/'docs/soccer/rebuild-5v5/p9-selfplay-model-registry.json'
    assert not canonical.exists()
    (BASE/'completion.json').write_text(json.dumps(receipt,ensure_ascii=False,indent=2),encoding='utf-8')
    canonical.write_text(json.dumps(dict(**registry,status='p9-selfplay-complete',completion=receipt,
        contract=freeze['contract'],frozenVersion=str(EVIDENCE/'frozen-version.json')),ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(receipt,ensure_ascii=False))


if __name__=='__main__':main()

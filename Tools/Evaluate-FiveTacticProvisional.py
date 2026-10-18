"""Evaluate the user's selected original 505386-step model. No training is launched."""
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import time
import traceback
from five_evaluation_audit import audit

ROOT=Path(__file__).resolve().parent.parent
EVIDENCE=ROOT/'Logs/Manager5v5/p9-tactics-20261014'
RUN='MNG5_V2-20261014-r002';BEHAVIOR='MNG5_Manager_v2';STEP=505386
BASE=EVIDENCE/RUN;RESULTS=ROOT/'results/Manager5v5'/RUN;MODELS=RESULTS/BEHAVIOR
BUILD=ROOT/'Builds/Manager5v5/p9-tactics-v3-20261014/Soccer5v5Learning.exe'
OLD=ROOT/'results/Manager5v5/MNG5_V2-20261014-r001'/BEHAVIOR
EARLY=ROOT/'results/Manager5v5/MNG5_V2-20261013-r001'/BEHAVIOR/f'{BEHAVIOR}-200011.onnx'
def read(p):return json.loads(Path(p).read_text(encoding='utf-8-sig'))
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def write(p,v):
    p=Path(p);tmp=p.with_suffix('.tmp');tmp.write_text(json.dumps(v,ensure_ascii=False,indent=2),encoding='utf-8');os.replace(tmp,p)
def frozen():
    for item in read(EVIDENCE/'frozen-version.json')['executionFiles']:
        assert sha(ROOT/item['path']).lower()==item['sha256'].lower(),item['path']
def select_original():
    receipt=BASE/'provisional-selection.json'
    if receipt.exists():
        r=read(receipt);assert r['selectedStep']==STEP
        assert sha(MODELS/f'{BEHAVIOR}-{STEP}.onnx')==r['onnxSha256']
        return
    assert read(BASE/'to-0500000/exit.json')['exitCode']==0
    rejected_stop=read(BASE/'to-0500000/safe-stop.json')
    assert rejected_stop['requestedStop'] and rejected_stop['steps'][BEHAVIOR]==471854
    assert not read(BASE/'to-0500000/process-result.json')['failures']
    cap=read(BASE/'cap-change-500000.json');original=Path(cap['archive'])/'results'
    for item in cap['preserved']:assert sha(original/item['path'])==item['sha256']
    rejected=BASE/'not-selected-retrain-471854';assert not rejected.exists()
    rejected.mkdir();shutil.copy2(BASE/'orchestrator-status.json',rejected/'orchestrator-status.json')
    destination=rejected/'results'
    # Verified absolute project-local endpoints before moving this complete stopped Run.
    assert RESULTS.resolve().is_relative_to(ROOT.resolve())
    assert destination.resolve().is_relative_to(ROOT.resolve()) and not destination.exists()
    shutil.move(str(RESULTS),str(destination))
    shutil.copytree(original,RESULTS)
    for item in cap['preserved']:assert sha(RESULTS/item['path'])==item['sha256']
    write(receipt,dict(userInstruction='Use the originally stopped model beyond 500000 as final provisional version. Do not train further.',
        selectedStep=STEP,selectedSource=str(original),model=str(MODELS/f'{BEHAVIOR}-{STEP}.onnx'),
        onnxSha256=sha(MODELS/f'{BEHAVIOR}-{STEP}.onnx'),ptSha256=sha(MODELS/f'{BEHAVIOR}-{STEP}.pt'),
        poolSha256=sha(MODELS/'five-pool.pt'),optimizerUpdates=5808,
        rejectedRetrainStop=471854,rejectedRetrainAdditionalSteps=71854,rejectedArchive=str(rejected),
        supersedes='cap-change-500000.json exact-cap restoration plan',furtherTraining=False,selected=time.time()))

def main():
    frozen();select_original()
    completed=read(BASE/'stage-results.json');registry=read(BASE/'model-registry.json');started=time.time()
    status=BASE/'orchestrator-status.json';env=os.environ.copy()
    env.update(CUDA_VISIBLE_DEVICES='-1',OMP_NUM_THREADS='1',OPENBLAS_NUM_THREADS='1',MKL_NUM_THREADS='1',PYTHONUNBUFFERED='1')
    def execute(label,command):
        frozen();receipt=BASE/(label+'-process.json')
        if receipt.exists():assert read(receipt)['exitCode']==0;return
        write(status,dict(state='running',mode='evaluation-only',run=RUN,operation=label,completed=completed,
            selectedStep=STEP,furtherTraining=False,started=started,updated=time.time()))
        print('START',label,flush=True)
        with (BASE/(label+'-stdout.log')).open('w',encoding='utf-8') as out,(BASE/(label+'-stderr.log')).open('w',encoding='utf-8') as err:
            process=subprocess.Popen(command,cwd=ROOT,env=env,stdout=out,stderr=err)
            write(BASE/'active-process.json',dict(label=label,pid=process.pid,arguments=list(map(str,command)),started=time.time()))
            code=process.wait()
        write(receipt,dict(exitCode=code,ended=time.time(),arguments=list(map(str,command))))
        assert code==0,f'{label} failed: {code}'
        print('DONE',label,flush=True)
    def evaluate(candidate,opponent,label,prefix='eval',step=STEP):
        operation=f'{prefix}-{step:07d}-{label}';output=BASE/operation
        execute(operation,[sys.executable,'Tools/five_tactic_evaluate.py','--candidate',str(candidate),'--opponent',str(opponent),
            '--build',str(BUILD),'--output',str(output),'--pairs','12','--seed','63000','--workers','24','--worker','200'])
        summary=read(output/'summary.json');assert summary['passed'] and summary['games']==24
        check=audit(output,expected_runtime='tactics-v3-expansion');write(output/'final-match-audit.json',check)
        summary['opponentActionCounts']=check['opponentActionCounts'];return summary
    try:
        fixed=[('strong','rule')]+[(f'old-{s:07d}',OLD/f'{BEHAVIOR}-{s}.onnx') for s in range(0,1200001,200000)]
        model=MODELS/f'{BEHAVIOR}-{STEP}.onnx'
        if not any(s['step']==STEP for s in completed):
            assert [s['step'] for s in completed]==[0,200000,400000]
            opponents=fixed+[(f'stage-{s["step"]:07d}',s['onnx']) for s in registry['stages']]+[('early-0200011',EARLY)]
            summaries={label:evaluate(model,opponent,label) for label,opponent in opponents}
            execute(f'parity-{STEP:07d}',[sys.executable,'Tools/Audit-FiveCheckpoint.py','--checkpoint',str(model.with_suffix('.pt')),
                '--observations',str(BASE/f'eval-{STEP:07d}-strong'),'--output',str(BASE/f'parity-{STEP:07d}.json')])
            assert read(BASE/f'parity-{STEP:07d}.json')['step']==STEP
            registry['stages'].append(dict(step=STEP,onnx=str(model),onnxSha256=sha(model),ptSha256=sha(model.with_suffix('.pt'))))
            registry.update(provisionalFinalStep=STEP,furtherTraining=False)
            completed.append(dict(step=STEP,evaluations=summaries))
            write(BASE/'model-registry.json',registry);write(BASE/'stage-results.json',completed)
        baseline={}
        for label,opponent in fixed:
            baseline[label]=evaluate(OLD/f'{BEHAVIOR}-1200000.onnx',opponent,label,prefix='baseline',step=1200000)
            write(BASE/'baseline-results.json',baseline)
        write(status,dict(state='complete',mode='evaluation-only',run=RUN,completed=completed,selectedStep=STEP,
            furtherTraining=False,started=started,ended=time.time()))
        print('COMPLETE evaluation-only',STEP,flush=True)
    except BaseException:
        write(status,dict(state='failed',mode='evaluation-only',run=RUN,completed=completed,selectedStep=STEP,
            furtherTraining=False,started=started,ended=time.time(),error=traceback.format_exc()))
        raise
if __name__=='__main__':main()

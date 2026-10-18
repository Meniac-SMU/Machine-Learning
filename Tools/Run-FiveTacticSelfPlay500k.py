"""User-directed 500k cap: preserve overshoot, restore exact 400k, evaluate exact 500k."""
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import time
import traceback
from five_evaluation_audit import audit

ROOT = Path(__file__).resolve().parent.parent
EVIDENCE = ROOT / 'Logs/Manager5v5/p9-tactics-20261014'
RUN = 'MNG5_V2-20261014-r002'
BEHAVIOR = 'MNG5_Manager_v2'
BASE = EVIDENCE / RUN
RESULTS = ROOT / 'results/Manager5v5' / RUN
MODELS = RESULTS / BEHAVIOR
BUILD = ROOT / 'Builds/Manager5v5/p9-tactics-v3-20261014/Soccer5v5Learning.exe'
OLD = ROOT / 'results/Manager5v5/MNG5_V2-20261014-r001' / BEHAVIOR
EARLY = ROOT / 'results/Manager5v5/MNG5_V2-20261013-r001' / BEHAVIOR / f'{BEHAVIOR}-200011.onnx'

def read(path): return json.loads(Path(path).read_text(encoding='utf-8-sig'))
def sha(path): return hashlib.sha256(Path(path).read_bytes()).hexdigest()
def write(path, value):
    path = Path(path); temporary = path.with_suffix('.tmp')
    temporary.write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding='utf-8')
    os.replace(temporary, path)

def frozen():
    manifest = read(EVIDENCE/'frozen-version.json'); assert manifest['passed']
    for item in manifest['executionFiles']:
        assert sha(ROOT/item['path']).lower() == item['sha256'].lower(), item['path']

def restore():
    receipt = BASE/'cap-change-500000.json'
    if receipt.exists():
        assert read(receipt)['restoredStep'] == 400000
        return
    stop = read(BASE/'to-0600000/safe-stop.json')
    assert stop['requestedStop'] and stop['steps'][BEHAVIOR] == 505386
    assert not read(BASE/'to-0600000/process-result.json')['failures']
    assert read(BASE/'to-0600000/exit.json')['exitCode'] == 0
    assert read(BASE/'orchestrator-status.json')['state'] == 'failed'
    registry = read(BASE/'model-registry.json')
    assert [r['step'] for r in registry['stages']] == [0,200000,400000]
    model = registry['stages'][-1]
    assert sha(model['onnx']) == model['onnxSha256']
    assert sha(Path(model['onnx']).with_suffix('.pt')) == model['ptSha256']
    archive = BASE/'superseded-505386'
    assert not archive.exists()
    archive.mkdir()
    shutil.copytree(RESULTS, archive/'results')
    preserved = []
    for p in RESULTS.rglob('*'):
        if p.is_file():
            relative = p.relative_to(RESULTS)
            digest = sha(p); assert digest == sha(archive/'results'/relative)
            preserved.append(dict(path=relative.as_posix(),sha256=digest))
    shutil.copy2(BASE/'orchestrator-status.json', archive/'orchestrator-status.json')
    # Remove only superseded numbered checkpoints from the active directory by moving
    # them into a verified, project-local archive. The complete pre-restore copy remains.
    moved = archive/'active-numbered-checkpoints'; moved.mkdir()
    for p in list(MODELS.iterdir()):
        match = re.fullmatch(r'(?:MNG5_Manager_v2|five-pool)-(\d+)\.(?:pt|onnx)', p.name)
        if match and int(match[1]) > 400000:
            destination = moved/p.name
            assert p.resolve().is_relative_to(ROOT) and destination.resolve().is_relative_to(ROOT)
            shutil.move(str(p), str(destination))
    shutil.copy2(MODELS/f'{BEHAVIOR}-400000.pt', MODELS/'checkpoint.pt')
    shutil.copy2(MODELS/'five-pool-400000.pt', MODELS/'five-pool.pt')
    shutil.copy2(MODELS/f'{BEHAVIOR}-400000.onnx', RESULTS/f'{BEHAVIOR}.onnx')
    training_status = RESULTS/'run_logs/training_status.json'
    metadata = read(training_status)
    metadata[BEHAVIOR]['checkpoints'] = [c for c in metadata[BEHAVIOR]['checkpoints'] if c['steps'] <= 400000]
    metadata[BEHAVIOR]['final_checkpoint'] = dict(metadata[BEHAVIOR]['checkpoints'][-1],
        file_path=str(RESULTS/f'{BEHAVIOR}.onnx'))
    write(training_status, metadata)
    write(receipt, dict(userInstruction='Stop at 500000 and evaluate the 500000-step model.',
        oldCap=600000,newCap=500000,observedStop=505386,restoredStep=400000,
        reason='STOP was processed after 500000. Preserve overshoot; exclude its experience from the evaluated lineage.',
        oldOrchestratorFailure='Expected target mismatch after user STOP, not Player or optimizer failure.',
        archive=str(archive),preserved=preserved,restoredPolicySha256=sha(MODELS/'checkpoint.pt'),
        restoredPoolSha256=sha(MODELS/'five-pool.pt'),created=time.time()))

def main():
    frozen(); restore()
    completed = read(BASE/'stage-results.json'); registry = read(BASE/'model-registry.json')
    started = time.time(); status = BASE/'orchestrator-status.json'
    env = os.environ.copy()
    env.update(CUDA_VISIBLE_DEVICES='-1',OMP_NUM_THREADS='1',OPENBLAS_NUM_THREADS='1',MKL_NUM_THREADS='1',PYTHONUNBUFFERED='1')
    def execute(label, command):
        frozen(); receipt=BASE/(label+'-process.json')
        if receipt.exists():
            assert read(receipt)['exitCode']==0
            return
        write(status, dict(state='running',mode='train',run=RUN,operation=label,completed=completed,
            authorizedStop=500000,eventualTarget=1200000,started=started,updated=time.time()))
        print('START',label,flush=True)
        with (BASE/(label+'-stdout.log')).open('w',encoding='utf-8') as out, (BASE/(label+'-stderr.log')).open('w',encoding='utf-8') as err:
            process=subprocess.Popen(command,cwd=ROOT,env=env,stdout=out,stderr=err)
            write(BASE/'active-process.json',dict(label=label,pid=process.pid,arguments=list(map(str,command)),started=time.time()))
            code=process.wait()
        write(receipt,dict(exitCode=code,ended=time.time(),arguments=list(map(str,command))))
        assert code==0,f'{label} failed: {code}'
        print('DONE',label,flush=True)
    def evaluate(candidate,opponent,label,prefix='eval',step=500000):
        operation=f'{prefix}-{step:07d}-{label}';output=BASE/operation
        execute(operation,[sys.executable,'Tools/five_tactic_evaluate.py','--candidate',str(candidate),
            '--opponent',str(opponent),'--build',str(BUILD),'--output',str(output),
            '--pairs','12','--seed','63000','--workers','24','--worker','200'])
        summary=read(output/'summary.json');assert summary['passed'] and summary['games']==24
        check=audit(output,expected_runtime='tactics-v3-expansion');write(output/'final-match-audit.json',check)
        summary['opponentActionCounts']=check['opponentActionCounts']
        return summary
    try:
        powershell=shutil.which('pwsh') or shutil.which('powershell')
        execute('train-to-0500000',[powershell,'-NoProfile','-File','Tools/Run-FiveSelfPlayTraining.ps1',
            '-RunName',RUN,'-Segment','to-0500000','-StopAt','500000','-Seed','19016',
            '-Build',BUILD.relative_to(ROOT).as_posix(),'-EvidenceDirectory','Logs/Manager5v5/p9-tactics-20261014',
            '-PlayerRuntime','tactics-v3-expansion','-Resume'])
        stop=read(BASE/'to-0500000/safe-stop.json');result=read(BASE/'to-0500000/process-result.json')
        assert stop['steps']=={BEHAVIOR:500000} and not stop['requestedStop']
        assert not result['failures'] and result['traceAudit']['traceFiles']==32
        model=MODELS/f'{BEHAVIOR}-500000.onnx'
        fixed=[('strong','rule')]+[(f'old-{s:07d}',OLD/f'{BEHAVIOR}-{s}.onnx') for s in range(0,1200001,200000)]
        if not any(s['step']==500000 for s in completed):
            opponents=fixed+[(f'stage-{s["step"]:07d}',s['onnx']) for s in registry['stages']]+[('early-0200011',EARLY)]
            summaries={label:evaluate(model,opponent,label) for label,opponent in opponents}
            execute('parity-0500000',[sys.executable,'Tools/Audit-FiveCheckpoint.py','--checkpoint',str(model.with_suffix('.pt')),
                '--observations',str(BASE/'eval-0500000-strong'),'--output',str(BASE/'parity-0500000.json')])
            assert read(BASE/'parity-0500000.json')['step']==500000
            registry['stages'].append(dict(step=500000,onnx=str(model),onnxSha256=sha(model),ptSha256=sha(model.with_suffix('.pt'))))
            completed.append(dict(step=500000,evaluations=summaries))
            write(BASE/'model-registry.json',registry);write(BASE/'stage-results.json',completed)
        baseline={}
        for label,opponent in fixed:
            baseline[label]=evaluate(OLD/f'{BEHAVIOR}-1200000.onnx',opponent,label,prefix='baseline',step=1200000)
            write(BASE/'baseline-results.json',baseline)
        write(status,dict(state='complete',mode='train',run=RUN,completed=completed,authorizedStop=500000,
            eventualTarget=1200000,started=started,ended=time.time(),capChange=str(BASE/'cap-change-500000.json')))
        print('COMPLETE 500000',flush=True)
    except BaseException:
        write(status,dict(state='failed',mode='train',run=RUN,completed=completed,authorizedStop=500000,
            started=started,ended=time.time(),error=traceback.format_exc()))
        raise

if __name__=='__main__':main()

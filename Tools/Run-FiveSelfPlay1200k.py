"""Sequential, fail-closed 32-env self-play and every-prior-stage evaluation."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import time
import traceback

ROOT = Path(__file__).resolve().parent.parent
EVIDENCE = ROOT/'Logs/Manager5v5/p9-selfplay-20261014'
BUILD = 'Builds/Manager5v5/p9-selfplay-frozen-20261014/Soccer5v5Learning.exe'
BEHAVIOR = 'MNG5_Manager_v2'


def read(p): return json.loads(Path(p).read_text(encoding='utf-8-sig'))
def sha(p): return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def write(p, value):
    p = Path(p); p.parent.mkdir(parents=True, exist_ok=True)
    t=p.with_suffix('.tmp'); t.write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding='utf-8'); os.replace(t,p)


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--mode',choices=('preflight','train'),required=True)
    args=parser.parse_args()
    preflight=args.mode=='preflight'
    run='P9-SP32-preflight-20261014' if preflight else 'MNG5_V2-20261014-r001'
    seed=19015 if preflight else 19014
    base=EVIDENCE/run; base.mkdir(exist_ok=True)
    status=base/'orchestrator-status.json'
    started=time.time(); completed=[]
    env=os.environ.copy()
    env.update(CUDA_VISIBLE_DEVICES='-1',OMP_NUM_THREADS='1',OPENBLAS_NUM_THREADS='1',MKL_NUM_THREADS='1',PYTHONUNBUFFERED='1')
    powershell=shutil.which('pwsh') or shutil.which('powershell')
    def execute(label, arguments):
        if not preflight:
            for item in read(EVIDENCE/'execution-file-manifest.json'):
                assert sha(ROOT/item['path'])==item['sha256'], f'Execution code changed: {item["path"]}'
        receipt=base/(label+'-process.json')
        if receipt.exists():
            assert read(receipt)['exitCode']==0, f'Prior failed operation needs diagnosis: {label}'
            return
        write(status,dict(state='running',mode=args.mode,run=run,operation=label,completed=completed,started=started,updated=time.time()))
        print('START',label,flush=True)
        with (base/(label+'-stdout.log')).open('w',encoding='utf-8') as out, (base/(label+'-stderr.log')).open('w',encoding='utf-8') as err:
            p=subprocess.Popen(arguments,cwd=ROOT,env=env,stdout=out,stderr=err)
            write(base/'active-process.json',dict(label=label,pid=p.pid,arguments=arguments,started=time.time()))
            code=p.wait()
        write(receipt,dict(exitCode=code,ended=time.time(),arguments=arguments))
        assert code==0, f'Operation failed: {label} (exit {code})'
        print('DONE',label,flush=True)
    def train(step,resume):
        segment=f'to-{step:07d}'
        command=[powershell,'-NoProfile','-File','Tools/Run-FiveSelfPlayTraining.ps1','-RunName',run,
                 '-Segment',segment,'-StopAt',str(step),'-Seed',str(seed),'-Build',BUILD]
        if resume: command+=['-Resume']
        execute('train-'+segment,command)
        evidence=base/segment
        result=read(evidence/'process-result.json'); stop=read(evidence/'safe-stop.json') if step else None
        assert not result['failures']
        assert result['traceAudit']['traceFiles']==32, 'Expected 32 actual Player traces'
        if step:
            assert stop['steps'][BEHAVIOR]==step and not stop['requestedStop']
            assert result['optimizerUpdates']>0
        path=ROOT/'results/Manager5v5'/run/BEHAVIOR/f'{BEHAVIOR}-{step}.onnx'
        assert path.is_file() and path.with_suffix('.pt').is_file()
        return path
    def evaluate(step,candidate,opponent,label,pairs):
        output=base/f'eval-{step:07d}-{label}'
        execute(f'eval-{step:07d}-{label}',[sys.executable,'Tools/five_selfplay_evaluate.py',
            '--candidate',str(candidate),'--opponent',str(opponent),'--build',BUILD,'--output',str(output),
            '--pairs',str(pairs),'--seed','59000','--workers',str(min(24,2*pairs)),'--worker','160'])
        summary=read(output/'summary.json'); assert summary['passed'] and summary['games']==2*pairs
        return summary
    try:
        assert (ROOT/BUILD).is_file()
        assert read(EVIDENCE/'full-regression-07.json')['testResult']=='Passed'
        assert read(EVIDENCE/'edit-regression-08.json')['testResult']=='Passed'
        if not preflight:
            assert read(EVIDENCE/'P9-SP32-preflight-20261014/orchestrator-status.json')['state']=='complete'
            assert read(EVIDENCE/'frozen-version.json')['passed']
        registry=[]
        if preflight:
            first=train(5000,False)
            last=train(12000,True)
            summary=evaluate(12000,last,'rule','strong',2)
            execute('parity-12000',[sys.executable,'Tools/Audit-FiveCheckpoint.py','--checkpoint',str(last.with_suffix('.pt')),
                '--observations',str(base/'eval-0012000-strong'),'--output',str(base/'parity-12000.json')])
            assert read(base/'parity-12000.json')['step']==12000
            completed.append(dict(step=12000,evaluation=summary))
        else:
            for step in range(0,1200001,200000):
                candidate=train(step,step>0)
                opponents=[('strong','rule')]+[(f'stage-{r["step"]:07d}',r['onnx']) for r in registry]
                summaries={}
                for label,source in opponents:
                    summaries[label]=evaluate(step,candidate,source,label,12)
                execute(f'parity-{step:07d}',[sys.executable,'Tools/Audit-FiveCheckpoint.py','--checkpoint',str(candidate.with_suffix('.pt')),
                    '--observations',str(base/f'eval-{step:07d}-strong'),'--output',str(base/f'parity-{step:07d}.json')])
                assert read(base/f'parity-{step:07d}.json')['step']==step
                registry.append(dict(step=step,onnx=str(candidate),onnxSha256=sha(candidate),ptSha256=sha(candidate.with_suffix('.pt'))))
                completed.append(dict(step=step,evaluations=summaries))
                write(base/'model-registry.json',dict(run=run,fromScratch=True,selfPlayOnly=True,champion=None,stages=registry))
                write(base/'stage-results.json',completed)
        write(status,dict(state='complete',mode=args.mode,run=run,completed=completed,started=started,ended=time.time()))
        print('COMPLETE',args.mode,flush=True)
    except BaseException:
        write(status,dict(state='failed',mode=args.mode,run=run,completed=completed,started=started,ended=time.time(),error=traceback.format_exc()))
        raise


if __name__=='__main__': main()

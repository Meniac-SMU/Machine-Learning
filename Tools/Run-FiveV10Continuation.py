"""Approved continuation and preflight. Never restarts a failed segment automatically."""
import argparse
import os
import subprocess
import sys
import time
import traceback
from five_v10_run_common import *

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--mode',choices=['preflight','train'],required=True)
    mode=parser.parse_args().mode;run=PREFLIGHT if mode=='preflight' else RUN
    base=BASE/run;base.mkdir(exist_ok=True)
    status=base/'orchestrator-status.json';assert not status.exists(),'Do not duplicate or restart execution'
    stages=[]
    env=os.environ.copy();env.update(OMP_NUM_THREADS='1',OPENBLAS_NUM_THREADS='1',MKL_NUM_THREADS='1',PYTHONUNBUFFERED='1',CUDA_VISIBLE_DEVICES='0')
    def execute(label,args):
        verify();write(status,dict(state='running',run=run,mode=mode,operation=label,targetSteps=2800000,environments=32,device='cuda',updated=time.time()))
        with (base/(label+'-stdout.log')).open('w',encoding='utf-8') as out,(base/(label+'-stderr.log')).open('w',encoding='utf-8') as err:
            proc=subprocess.Popen([sys.executable,*args],cwd=ROOT,env=env,stdout=out,stderr=err,creationflags=subprocess.CREATE_NO_WINDOW)
            write(base/'active-process.json',dict(pid=proc.pid,label=label,arguments=args,started=time.time()))
            code=proc.wait()
        write(base/(label+'-process.json'),dict(exitCode=code,ended=time.time()))
        assert code==0,f'{label} failed; preserve evidence and stop'
    def train(step):
        execute(f'train-{step}',['Tools/Run-FiveV10Training.py','--run',run,'--step',str(step)])
        return read(base/f'to-{step}-32env-cuda/segment-verification.json')
    def evaluate(step,opponent,label,pairs):
        folder=base/f'eval-{step}-{label}'
        execute(f'eval-{step}-{label}',['Tools/five_v10_evaluate.py','--candidate',str(model_dir(run)/f'{BEHAVIOR}-{step}.onnx'),
            '--opponent',str(opponent),'--build',str(BUILD),'--output',str(folder),'--pairs',str(pairs),'--seed','63000',
            '--workers',str(min(32,2*pairs)),'--worker','300'])
        summary=read(folder/'summary.json');assert summary['passed'] and summary['games']==2*pairs
        return summary
    def parity(step,observations):
        execute(f'parity-{step}',['Tools/Audit-FiveCheckpoint.py','--checkpoint',str(model_dir(run)/f'{BEHAVIOR}-{step}.pt'),
            '--observations',str(observations),'--output',str(base/f'parity-{step}.json')])
        assert read(base/f'parity-{step}.json')['step']==step
    try:
        assert read(BASE/'play-training-shutdown-01.json')['passed']==22
        if mode=='preflight':
            for step in [2405000,2412000]:stages.append(train(step))
            result=evaluate(2412000,'rule','strong',2)
            parity(2412000,base/'eval-2412000-strong')
            write(BASE/'resume-verification.json',dict(passed=True,run=run,stages=stages,ruleEvaluation=result,
                cuda=True,environments=32,productionStartsAgainFromOriginal2400000=True))
        else:
            assert read(BASE/'resume-verification.json')['passed']
            parent=ROOT/'results/Manager5v5/MNG5_V5-20261016-r001/MNG5_Manager_v5'
            registry=[]
            for step in [2600000,2800000]:
                saved=train(step);print(json.dumps(dict(event='saved',step=step,updates=saved['optimizerUpdates'])),flush=True)
                evaluations={}
                if step==2800000:
                    opponents=[('strong','rule')]+[(f'stage-{i}',parent/f'MNG5_Manager_v5-{i}.onnx') for i in range(0,2400001,400000)]
                    for label,opponent in opponents:
                        evaluations[label]=evaluate(step,opponent,label,12)
                        summary=evaluations[label]
                        print(json.dumps(dict(event='evaluated',opponent=label,**{k:summary[k] for k in ['games','wins','draws','losses','goalsFor','goalsAgainst']})),flush=True)
                observations=base/'eval-2800000-strong' if step==2800000 else BASE/PREFLIGHT/'eval-2412000-strong'
                parity(step,observations)
                registry.append(saved);stages.append(dict(step=step,saved=saved,evaluations=evaluations))
                write(base/'stage-results.json',stages)
                write(base/'model-registry.json',dict(run=run,parentRun='MNG5_V5-20261016-r001',fromScratch=False,parentStep=2400000,champion=None,stages=registry))
        write(status,dict(state='complete',run=run,mode=mode,targetSteps=2412000 if mode=='preflight' else 2800000,environments=32,device='cuda',ended=time.time()))
        print('COMPLETE',mode,flush=True)
    except BaseException:
        write(status,dict(state='failed',run=run,mode=mode,error=traceback.format_exc(),ended=time.time()))
        raise

if __name__=='__main__':main()

"""Exact continuation, verified resume, formal paired evaluation and model parity."""
import argparse
import os
import subprocess
import sys
import time
import traceback
from five_v11_continuation_common import *

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--mode',choices=['preflight','train'],required=True)
    mode=parser.parse_args().mode
    run=PREFLIGHT if mode=='preflight' else RUN
    base=BASE/run
    base.mkdir(exist_ok=True)
    status=base/'orchestrator-status.json'
    assert not status.exists(), 'Preserve prior execution; no automatic retry'
    env=os.environ.copy()
    env.update(OMP_NUM_THREADS='1',OPENBLAS_NUM_THREADS='1',MKL_NUM_THREADS='1',PYTHONUNBUFFERED='1',CUDA_VISIBLE_DEVICES='0')
    stages=[]
    def execute(label,arguments):
        verify()
        write(status,dict(state='running',run=run,operation=label,targetSteps=3600000,
                         environments=24,device='cuda',updated=time.time()))
        with (base/(label+'-stdout.log')).open('w',encoding='utf-8') as out,(base/(label+'-stderr.log')).open('w',encoding='utf-8') as err:
            process=subprocess.Popen([sys.executable,*arguments],cwd=ROOT,env=env,stdout=out,stderr=err,creationflags=subprocess.CREATE_NO_WINDOW)
            write(base/'active-process.json',dict(pid=process.pid,label=label,arguments=arguments,started=time.time()))
            code=process.wait()
        write(base/(label+'-process.json'),dict(exitCode=code,ended=time.time()))
        assert code==0, f'{label} failed; evidence retained'
    def train(step):
        execute(f'train-{step}',['Tools/Run-FiveV11Training.py','--run',run,'--step',str(step)])
        return read(base/f'to-{step}-24env-cuda/segment-verification.json')
    def evaluate(step,label,opponent):
        output=base/f'eval-{step}-{label}'
        execute(f'eval-{step}-{label}',['Tools/five_v11_evaluate.py','--candidate',str(model_dir(run)/f'{BEHAVIOR}-{step}.onnx'),
            '--opponent',str(opponent),'--build',str(BUILD),'--output',str(output),
            '--pairs','20','--seed','71000','--workers','24','--worker','500','--keeper-turn-first','on'])
        summary=read(output/'summary.json')
        assert summary['passed'] and summary['games']==40
        return summary
    def parity(step,observations):
        execute(f'parity-{step}',['Tools/Audit-FiveCheckpoint.py','--checkpoint',str(model_dir(run)/f'{BEHAVIOR}-{step}.pt'),
            '--observations',str(observations),'--output',str(base/f'parity-{step}.json')])
        assert read(base/f'parity-{step}.json')['step']==step
    try:
        if mode=='preflight':
            for step in [2805000,2812000]: stages.append(train(step))
            result=evaluate(2812000,'strong','rule')
            parity(2812000,base/'eval-2812000-strong')
            write(BASE/'resume-verification.json',dict(passed=True,run=run,stages=stages,
                ruleEvaluation=result,environments=24,device='cuda',productionStartsFromOriginal2800000=True))
        else:
            assert read(BASE/'resume-verification.json')['passed']
            registry=[]
            for step in [3000000,3200000,3400000,3600000]:
                saved=train(step)
                observations=BASE/PREFLIGHT/'eval-2812000-strong'
                parity(step,observations)
                stages.append(dict(step=step,saved=saved))
                registry.append(saved)
                write(base/'stage-results.json',stages)
                write(base/'model-registry.json',dict(run=run,parentRun=PARENT,parentStep=2800000,
                    fromScratch=False,champion=None,stages=registry))
                print('SAVED',step,flush=True)
            evaluations=[]
            for step in [3200000,3600000]:
                opponents=[('strong','rule'),('stage-2800000',model_dir(PARENT)/f'{BEHAVIOR}-2800000.onnx')]
                if step==3600000: opponents.append(('stage-3200000',model_dir(RUN)/f'{BEHAVIOR}-3200000.onnx'))
                for label,opponent in opponents:
                    summary=evaluate(step,label,opponent)
                    evaluations.append(dict(step=step,opponent=label,summary=summary))
                    write(base/'evaluation-results.json',evaluations)
            write(base/'training-completion.json',dict(passed=True,step=3600000,stages=stages,
                evaluations=evaluations,formalGames=200,preservedParentFiles=verify_parent(),frozenInputs=verify()))
        write(status,dict(state='complete',run=run,mode=mode,ended=time.time()))
        print('COMPLETE',mode,flush=True)
    except BaseException:
        write(status,dict(state='failed',run=run,mode=mode,error=traceback.format_exc(),ended=time.time()))
        raise

if __name__=='__main__':main()

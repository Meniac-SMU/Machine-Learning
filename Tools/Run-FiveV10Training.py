"""One exact-cap, CUDA/32-environment segment, always resuming an isolated continuation."""
import argparse
import os
import subprocess
import sys
import time
import torch
from five_v10_run_common import *

def main():
    p=argparse.ArgumentParser();p.add_argument('--run',required=True,choices=[RUN,PREFLIGHT]);p.add_argument('--step',required=True,type=int)
    a=p.parse_args();assert a.step in ([2405000,2412000] if a.run==PREFLIGHT else [2600000,2800000])
    verify()
    evidence=BASE/a.run/f'to-{a.step}-32env-cuda';assert not evidence.exists();evidence.mkdir(parents=True)
    model=model_dir(a.run)
    checkpoint=torch.load(model/'checkpoint.pt',map_location='cpu')
    pool=torch.load(model/'five-pool.pt',map_location='cpu')
    before=int(checkpoint['global_step']['_GlobalSteps__global_step'].item())
    assert before==pool['step']<a.step and pool['contract']==CONTRACT
    assert before>=2400000
    write(evidence/'resume-input.json',dict(step=before,optimizerUpdates=pool['optimizerUpdates'],
        checkpointSha=sha(model/'checkpoint.pt'),poolSha=sha(model/'five-pool.pt'),bufferExperiences=pool['ppo_buffer'].num_experiences,
        ghostStep=pool['ghost']['ghost_step'],snapshotCount=len(pool['ghost']['policy_snapshots'])))
    env=os.environ.copy();env.update(FIVE_EVIDENCE=str(evidence),FIVE_STOP_AT=str(a.step),FIVE_EXPECTED_RUNTIME=RUNTIME,
        FIVE_PLAYER_LOGS=str(evidence/'players'),CUDA_VISIBLE_DEVICES='0',OMP_NUM_THREADS='1',OPENBLAS_NUM_THREADS='1',MKL_NUM_THREADS='1',PYTHONUNBUFFERED='1')
    (evidence/'players').mkdir();(evidence/'traces').mkdir()
    command=[sys.executable,'-X','faulthandler',str(ROOT/'Tools/five_v10_cuda_train.py'),str(CONFIG),'--env',str(BUILD),
        '--run-id',a.run,'--results-dir',str(ROOT/'results/Manager5v5'),'--num-envs','32','--seed','20015','--base-port','5805',
        '--no-graphics','--torch-device','cuda','--time-scale','100','--max-lifetime-restarts','0','--resume',
        '--env-args','--five-execution-variant','all','--five-p9-trace-dir',str(evidence/'traces'),'-job-worker-count','1',
        '--five-shutdown-file',str(evidence/'PLAYER_SHUTDOWN')]
    write(evidence/'launch.json',dict(run=a.run,step=a.step,arguments=command,started=time.time()))
    with (evidence/'stdout.log').open('w',encoding='utf-8') as out,(evidence/'stderr.log').open('w',encoding='utf-8') as err:
        process=subprocess.Popen(command,cwd=ROOT,env=env,stdout=out,stderr=err,creationflags=subprocess.CREATE_NO_WINDOW)
        write(evidence/'active-process.json',dict(pid=process.pid,started=time.time()))
        print('TRAIN_PID',process.pid,flush=True);code=process.wait()
    write(evidence/'exit.json',dict(exitCode=code,ended=time.time()))
    assert code==0,f'Training failed: {evidence}'
    result=read(evidence/'process-result.json');stop=read(evidence/'safe-stop.json')
    assert not result['failures'] and result['traceAudit']['traceFiles']==32
    assert stop['steps'][BEHAVIOR]==a.step and not stop['requestedStop']
    assert read(evidence/'cuda-device.json')['devices']==['cuda:0']
    assert result['optimizerUpdates']>pool['optimizerUpdates']
    saved=torch.load(model/f'{BEHAVIOR}-{a.step}.pt',map_location='cpu')
    saved_pool=torch.load(model/f'five-pool-{a.step}.pt',map_location='cpu')
    assert saved_pool['step']==a.step==int(saved['global_step']['_GlobalSteps__global_step'].item())
    assert saved_pool['optimizerUpdates']==result['optimizerUpdates']
    assert any(not torch.equal(checkpoint['Policy'][k],saved['Policy'][k]) for k in checkpoint['Policy'])
    restored=[json.loads(line) for line in (model/'five-pool.jsonl').read_text().splitlines() if '"restored"' in line][-1]
    assert restored['step']==before and restored['optimizerUpdates']==pool['optimizerUpdates']
    receipt=dict(passed=True,step=a.step,resumedFrom=before,optimizerUpdates=result['optimizerUpdates'],environments=32,device='cuda:0',
        onnx=str(model/f'{BEHAVIOR}-{a.step}.onnx'),onnxSha256=sha(model/f'{BEHAVIOR}-{a.step}.onnx'),
        ptSha256=sha(model/f'{BEHAVIOR}-{a.step}.pt'),poolSha256=sha(model/f'five-pool-{a.step}.pt'))
    write(evidence/'segment-verification.json',receipt);print(json.dumps(receipt),flush=True)

if __name__=='__main__':main()

"""32 simultaneous Unity communicator handshakes and actions. No trainer or optimizer."""
import argparse
from concurrent.futures import ThreadPoolExecutor
import ctypes
import hashlib
import json
from pathlib import Path
import threading
import time
import numpy as np
from mlagents_envs.environment import UnityEnvironment
from mlagents_envs.base_env import ActionTuple
from mlagents_envs.side_channel.engine_configuration_channel import EngineConfigurationChannel

def memory():
    class Status(ctypes.Structure):
        _fields_=[('length',ctypes.c_ulong),('load',ctypes.c_ulong)]+[(name,ctypes.c_ulonglong) for name in
            ('total','available','pageTotal','pageAvailable','virtualTotal','virtualAvailable','extendedAvailable')]
    status=Status();status.length=ctypes.sizeof(status)
    if not ctypes.windll.kernel32.GlobalMemoryStatusEx(ctypes.byref(status)): raise OSError('Memory query failed')
    return dict(loadPercent=status.load,totalBytes=status.total,availableBytes=status.available)

def sha(path): return hashlib.sha256(Path(path).read_bytes()).hexdigest()

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--build',required=True);parser.add_argument('--output',required=True)
    args=parser.parse_args();build=Path(args.build).resolve();output=Path(args.output).resolve()
    info=json.loads((build/'build-info.json').read_text())
    executable=build/'MNG_MS3V2.exe'
    assert sha(executable)==info['executableSha256']
    assert sha(build/'MNG_MS3V2_Data/Managed/MNG.Runtime.dll')==info['runtimeSha256']
    assert sha(build/'MNG_MS3V2_Data/level0')==info['levelSha256']
    assert info['observations']==244 and info['protocolVersion']==3
    before=memory()
    assert before['availableBytes']>=8*1024**3, 'Not enough free RAM for the 32-worker preflight'
    output.mkdir(parents=True,exist_ok=False)
    peak=[]
    barrier=threading.Barrier(32,action=lambda:peak.append(memory()),timeout=180)
    def worker(index):
        folder=output/f'worker-{index:02d}';folder.mkdir()
        channel=EngineConfigurationChannel();channel.set_configuration_parameters(time_scale=20,target_frame_rate=-1)
        env=None;decisions=0;counts={}
        try:
            env=UnityEnvironment(file_name=str(executable),worker_id=index,base_port=8500,seed=20260923+index,
                no_graphics=True,timeout_wait=120,side_channels=[channel],log_folder=str(folder),
                additional_args=['-mngEvidenceDir',str(folder),'-mngRunId','MS3-preflight-no-learning',
                    '-mngBuildSha',info['runtimeSha256'],'-mngPolicyAssistMode','none'])
            env.reset()
            assert len(env.behavior_specs)==2, 'Both self-play teams must connect'
            barrier.wait()  # All 32 environments must stay alive together before any closes.
            for _ in range(8):
                for name,spec in env.behavior_specs.items():
                    assert list(spec.action_spec.discrete_branches)==[6]
                    steps,_=env.get_steps(name)
                    if not len(steps):continue
                    assert steps.obs[0].shape==(1,244) and np.isfinite(steps.obs[0]).all()
                    valid=~steps.action_mask[0] if steps.action_mask else np.ones((1,6),dtype=bool)
                    action=3 if valid[0,3] else int(np.flatnonzero(valid[0])[0])
                    env.set_actions(name,ActionTuple(discrete=np.array([[action]],dtype=np.int32)))
                    decisions+=1;counts[name]=counts.get(name,0)+1
                env.step()
            assert len(counts)==2 and all(v>0 for v in counts.values())
            return dict(worker=index,decisions=decisions,behaviors=counts,passed=True)
        except BaseException:
            barrier.abort()
            raise
        finally:
            if env is not None:env.close()
    started=time.monotonic()
    with ThreadPoolExecutor(max_workers=32) as pool: rows=list(pool.map(worker,range(32)))
    result=dict(passed=True,workers=32,simultaneousBarrierReached=len(peak)==1,optimizerUpdates=0,
        trainingSteps=0,decisions=sum(r['decisions'] for r in rows),before=before,allWorkersAlive=peak[0],
        elapsedSeconds=time.monotonic()-started,runtimeSha=info['runtimeSha256'],rows=rows)
    (output/'report.json').write_text(json.dumps(result,indent=2))
    print(json.dumps({k:v for k,v in result.items() if k!='rows'}))

if __name__=='__main__':main()

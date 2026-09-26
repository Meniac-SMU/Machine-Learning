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
    parser.add_argument('--steps', type=int, default=8)
    parser.add_argument('--reset-every', type=int, default=0)
    parser.add_argument('--shared-evidence', action='store_true')
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
    shared = output/'shared-workers'
    if args.shared_evidence: shared.mkdir()
    peak=[]
    barrier=threading.Barrier(32,action=lambda:peak.append(memory()),timeout=180)
    def worker(index):
        folder=output/f'worker-{index:02d}';folder.mkdir()
        channel=EngineConfigurationChannel();channel.set_configuration_parameters(time_scale=20,target_frame_rate=-1)
        env=None;decisions=0;counts={}
        try:
            env=UnityEnvironment(file_name=str(executable),worker_id=index,base_port=8500,seed=20260923+index,
                no_graphics=True,timeout_wait=120,side_channels=[channel],log_folder=str(folder),
                additional_args=['-mngEvidenceDir',str(shared if args.shared_evidence else folder),'-mngRunId','MS3-preflight-no-learning',
                    '-mngBuildSha',info['runtimeSha256'],'-mngPolicyAssistMode','none'])
            env.reset()
            assert len(env.behavior_specs)==2, 'Both self-play teams must connect'
            barrier.wait()  # All 32 environments must stay alive together before any closes.
            resets = 0
            rng = np.random.RandomState(20260923 + index)
            for iteration in range(args.steps):
                if args.reset_every and iteration and iteration % args.reset_every == 0:
                    env.reset(); resets += 1
                for name,spec in env.behavior_specs.items():
                    assert list(spec.action_spec.discrete_branches)==[6]
                    steps,_=env.get_steps(name)
                    if not len(steps):continue
                    assert steps.obs[0].shape==(1,244) and np.isfinite(steps.obs[0]).all()
                    valid=~steps.action_mask[0] if steps.action_mask else np.ones((1,6),dtype=bool)
                    action=int(rng.choice(np.flatnonzero(valid[0])))
                    env.set_actions(name,ActionTuple(discrete=np.array([[action]],dtype=np.int32)))
                    decisions+=1;counts[name]=counts.get(name,0)+1
                env.step()
            assert len(counts)==2 and all(v>0 for v in counts.values())
            return dict(worker=index,decisions=decisions,behaviors=counts,explicitResets=resets,passed=True)
        except BaseException:
            barrier.abort()
            raise
        finally:
            if env is not None:env.close()
    started=time.monotonic()
    with ThreadPoolExecutor(max_workers=32) as pool: rows=list(pool.map(worker,range(32)))
    from mng_player_log_guard import PlayerLogGuard
    failures = [failure for folder in output.glob('worker-*') for failure in PlayerLogGuard(folder).scan()]
    assert not failures, failures
    if args.shared_evidence:
        assert len(list(shared.glob('worker-*.jsonl'))) == 32, 'Missing worker boot evidence'
        assert len(list(shared.glob('spawns-*.jsonl'))) == 32, 'Missing isolated spawn streams'
        assert not (shared/'spawns.jsonl').exists() and not (shared/'common-rules.jsonl').exists()
        for path in shared.glob('*.jsonl'):
            for line in path.read_text().splitlines(): json.loads(line)
        if args.reset_every:
            for path in shared.glob('worker-*.jsonl'):
                events = [json.loads(line) for line in path.read_text().splitlines()]
                episodes = [row for row in events if row['event'] == 'episode']
                assert len(episodes) >= 2 and all(row['actionIntegrityPassed'] for row in episodes), str(path)
    result=dict(passed=True,workers=32,simultaneousBarrierReached=len(peak)==1,optimizerUpdates=0,
        trainingSteps=0,decisions=sum(r['decisions'] for r in rows),before=before,allWorkersAlive=peak[0],
        elapsedSeconds=time.monotonic()-started,runtimeSha=info['runtimeSha256'],rows=rows)
    result.update(sharedEvidence=args.shared_evidence, stepsPerWorker=args.steps, resetEvery=args.reset_every, playerFailures=failures)
    (output/'report.json').write_text(json.dumps(result,indent=2))
    print(json.dumps({k:v for k,v in result.items() if k!='rows'}))

if __name__=='__main__':main()

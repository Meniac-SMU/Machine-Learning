"""A real rendered 300-second autonomous match for PS visual play testing."""
import argparse
import json
from pathlib import Path
import time
import subprocess
import numpy as np
from mlagents_envs.environment import UnityEnvironment
from mlagents_envs.base_env import ActionTuple
from mlagents_envs.side_channel.engine_configuration_channel import EngineConfigurationChannel
from mlagents_envs import env_utils
from mng_v2_evaluate import FrozenPolicy, sha, ROOT


def main():
    p=argparse.ArgumentParser();p.add_argument('--candidate',required=True);p.add_argument('--output',required=True)
    p.add_argument('--seed',type=int,default=929801);p.add_argument('--port',type=int,default=9100);a=p.parse_args()
    build=ROOT/'Builds/MNG_V2/MS3-v3-WorkerIOFix-20260924T182752604Z/EvaluationV2'
    info=json.loads((build/'build-info.json').read_text());exe=build/'MNG_EvaluationV2.exe'
    assert sha(exe)==info['executableSha256']
    assert sha(build/'MNG_EvaluationV2_Data/Managed/MNG.Runtime.dll')==info['runtimeSha256']
    out=Path(a.output).resolve();out.mkdir(parents=True,exist_ok=False)
    parent=ROOT/'results/MNG_MS3V3-20260925-r002/MNG_ManagerV2/MNG_ManagerV2-2001834.onnx'
    sources=[Path(a.candidate).resolve(),parent]
    policies={t:FrozenPolicy(str(s),a.seed+100000+t) for t,s in enumerate(sources)}
    (out/'manifest.json').write_text(json.dumps(dict(candidate=str(sources[0]),parent=str(parent),
        hashes=[sha(s) for s in sources],runtimeSha=info['runtimeSha256'],seed=a.seed,
        seconds=300,timeScale=1,teacher=False,training=False),indent=2))
    channel=EngineConfigurationChannel();channel.set_configuration_parameters(time_scale=1,target_frame_rate=60,width=1280,height=720)
    env=None;result=out/'result.json'
    trace=(out/'decisions.jsonl').open('x')
    # Explicit visible startup prevents the shell's hidden startup state from being
    # inherited by the game. This is process launch configuration, not input injection.
    def launch_visible(file_name,args):
        startup=subprocess.STARTUPINFO();startup.dwFlags|=subprocess.STARTF_USESHOWWINDOW;startup.wShowWindow=1
        return subprocess.Popen([env_utils.validate_environment_path(file_name),*args],
            startupinfo=startup,start_new_session=True,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
    env_utils.launch_executable=launch_visible
    try:
        env=UnityEnvironment(file_name=str(exe),worker_id=0,base_port=a.port,seed=a.seed,no_graphics=False,
            side_channels=[channel],timeout_wait=120,log_folder=str(out),additional_args=[
                '-screen-fullscreen','0','-screen-width','1280','-screen-height','720',
                '-mngEvaluationSeed',str(a.seed),'-mngPolicyTeam','0','-mngNeuralOpponent','true',
                '-mngCandidateCommonRules','true','-mngOpponentCommonRules','true',
                '-mngEvaluationOutput',str(result),'-mngEvidenceDir',str(out),'-mngRunId',out.name,
                '-mngBuildSha',info['runtimeSha256'],'-mngLifecycleTrace','true','-mngFullTrace','true'])
        env.reset();channel.set_configuration_parameters(time_scale=1,target_frame_rate=60)
        start=time.monotonic();last=-10
        while not result.exists():
            if time.monotonic()-start>900:raise TimeoutError('Visible match timeout')
            for name in env.behavior_specs:
                team=int(name.split('team=')[-1]);steps,_=env.get_steps(name)
                if not len(steps):continue
                valid=~steps.action_mask[0]
                action=policies[team].act(steps.obs[0],valid)
                env.set_actions(name,ActionTuple(discrete=np.array([[action]],np.int32)))
                trace.write(json.dumps(dict(team=team,action=action,observation=steps.obs[0][0].tolist(),
                    valid=valid[0].tolist(),probabilities=policies[team].last_probabilities.tolist()))+'\n')
                elapsed=300*(1-float(steps.obs[0][0,113]))
                if team==0 and elapsed-last>=10:
                    (out/'watch-status.json').write_text(json.dumps(dict(elapsed=elapsed,pid=env._process.pid,wallSeconds=time.monotonic()-start)))
                    print(f'VISIBLE {elapsed:.1f}/300',flush=True);last=elapsed
            env.step()
        r=json.loads(result.read_text());assert r['elapsed']>=299.9 and r['mismatches']==r['overrides']==0
        print('VISIBLE COMPLETE',r['redScore'],r['navyScore'],flush=True)
    finally:
        trace.close()
        if env is not None:env.close()


if __name__=='__main__':main()

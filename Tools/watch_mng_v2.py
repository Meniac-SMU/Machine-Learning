"""Show one frozen-policy match in the existing Player at real-time speed. No training."""
import json
import argparse
import ctypes
from pathlib import Path
import time
from datetime import datetime
import numpy as np
from mlagents_envs.environment import UnityEnvironment
from mlagents_envs.base_env import ActionTuple
from mlagents_envs.side_channel.engine_configuration_channel import EngineConfigurationChannel
from mng_v2_evaluate import FrozenPolicy, sha

ROOT=Path(__file__).resolve().parent.parent

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--ms2-self-play',action='store_true',help='Both sides use the MS2 policy and new common rules')
    parser.add_argument("--build", help="Explicit EvaluationV2 build directory; defaults to the preserved comparison runtime")
    args=parser.parse_args()
    build=Path(args.build).resolve() if args.build else ROOT/'Builds/MNG_V2/MS2-common-possession-final-20260923/EvaluationV2'
    info=json.loads((build/'build-info.json').read_text())
    exe=build/'MNG_EvaluationV2.exe'
    assert sha(exe)==info['executableSha256']
    assert sha(build/'MNG_EvaluationV2_Data/Managed/MNG.Runtime.dll')==info['runtimeSha256']
    assert sha(build/'MNG_EvaluationV2_Data/level0')==info['levelSha256']
    sources=[ROOT/'results/MNG_MS2V2-20260922-r002/MNG_ManagerV2/MNG_ManagerV2-200120.onnx',
             ROOT/'results/MNG_MS3V2-20260922-r005/MNG_ManagerV2/MNG_ManagerV2-402389.onnx']
    if args.ms2_self_play:sources[1]=sources[0]
    rules=[True,args.ms2_self_play]
    labels=['MS2 rules ON','MS2 rules ON' if args.ms2_self_play else '400k rules OFF']
    seed=592831
    policies={team:FrozenPolicy(str(source),seed+100000+team) for team,source in enumerate(sources)}
    prefix='MS2-rules-on-selfplay-visible-' if args.ms2_self_play else 'MS2-rules-on-vs-400k-off-visible-'
    folder=ROOT/'Logs/MNG-Rebuild'/(prefix+datetime.now().strftime('%Y%m%d-%H%M%S'))
    folder.mkdir(exist_ok=False)
    result=folder/'result.json'
    (folder/'manifest.json').write_text(json.dumps(dict(red=str(sources[0]),navy=str(sources[1]),
        policyHashes=[sha(p) for p in sources],runtimeSha=info['runtimeSha256'],seed=seed,
        timeScale=1,seconds=300,commonRulesEnabled=rules,training=False),indent=2))
    channel=EngineConfigurationChannel()
    channel.set_configuration_parameters(time_scale=1,target_frame_rate=60,capture_frame_rate=0,width=1600,height=900)
    env=None
    try:
        env=UnityEnvironment(file_name=str(exe),worker_id=0,base_port=9100,seed=seed,no_graphics=False,
            side_channels=[channel],timeout_wait=120,log_folder=str(folder),additional_args=[
            '-screen-fullscreen','0','-screen-width','1600','-screen-height','900',
            '-mngEvaluationSeed',str(seed),'-mngPolicyTeam','0','-mngNeuralOpponent','true',
            '-mngCandidateCommonRules','true','-mngOpponentCommonRules',str(rules[1]).lower(),
            '-mngEvaluationOutput',str(result),'-mngEvidenceDir',str(folder),
            '-mngRunId',folder.name,'-mngBuildSha',info['runtimeSha256']])
        env.reset()
        # A hidden Python console can pass hidden-window startup state to Unity.
        # Show only this Player's main Unity window, not its helper windows.
        user32=ctypes.windll.user32
        callback_type=ctypes.WINFUNCTYPE(ctypes.c_bool,ctypes.c_void_p,ctypes.c_void_p)
        user32.GetWindowThreadProcessId.argtypes=[ctypes.c_void_p,ctypes.POINTER(ctypes.c_ulong)]
        user32.GetClassNameW.argtypes=[ctypes.c_void_p,ctypes.c_wchar_p,ctypes.c_int]
        user32.ShowWindowAsync.argtypes=[ctypes.c_void_p,ctypes.c_int]
        user32.SetForegroundWindow.argtypes=[ctypes.c_void_p]
        user32.SetWindowTextW.argtypes=[ctypes.c_void_p,ctypes.c_wchar_p]
        def show(hwnd,param):
            owner=ctypes.c_ulong();user32.GetWindowThreadProcessId(hwnd,ctypes.byref(owner))
            name=ctypes.create_unicode_buffer(256);user32.GetClassNameW(hwnd,name,256)
            if owner.value==env._process.pid and name.value=='UnityWndClass':
                user32.ShowWindowAsync(hwnd,5)
            return True
        user32.EnumWindows(callback_type(show),0)
        # Explicitly override the evaluation controller's Awake speed after initialization.
        channel.set_configuration_parameters(time_scale=1,target_frame_rate=60,capture_frame_rate=0)
        start=time.monotonic();last=-10;shown_again=False
        while not result.exists():
            if not shown_again and time.monotonic()-start>=2:
                user32.EnumWindows(callback_type(show),0);shown_again=True
            for behavior in env.behavior_specs:
                team=int(behavior.split('team=')[-1]);steps,_=env.get_steps(behavior)
                if not len(steps):continue
                valid=~steps.action_mask[0] if steps.action_mask else np.ones((1,6),dtype=bool)
                action=policies[team].act(steps.obs[0],valid)
                env.set_actions(behavior,ActionTuple(discrete=np.array([[action]],dtype=np.int32)))
                elapsed=float(steps.obs[0][0,112])*300
                if team==0 and elapsed-last>=5:
                    status=dict(gameSeconds=elapsed,wallSeconds=time.monotonic()-start,red=labels[0],navy=labels[1],commonRulesEnabled=rules,playerPid=env._process.pid,timeScaleRequested=1)
                    (folder/'watch-status.json').write_text(json.dumps(status,indent=2))
                    print(json.dumps(status),flush=True);last=elapsed
            env.step()
        finished=json.loads(result.read_text())
        assert finished['elapsed']>=299.9
        assert finished['commonRulesEnabled']==rules
        if not rules[1]:
            assert all(finished[key][1]==0 for key in ('commonPassAttempts','commonClearanceAttempts','commonNoTargetAttempts','commonRuleStrikes'))
        print('MATCH FINISHED '+json.dumps(finished),flush=True)
        # Leave the final score visible briefly, then close this one Player cleanly.
        time.sleep(30)
    finally:
        if env is not None:env.close()

if __name__=='__main__':main()

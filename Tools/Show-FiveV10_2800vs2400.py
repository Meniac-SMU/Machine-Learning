"""Read-only policy inference for one visible 1x, 300-second demonstration."""
import json
import argparse
import threading
import time
from datetime import datetime, timezone
from pathlib import Path
import numpy as np
from five_tactic_evaluate import FrozenPolicy, sha, UnityEnvironment, ActionTuple, EngineConfigurationChannel

ROOT = Path(__file__).resolve().parent.parent
parser = argparse.ArgumentParser()
parser.add_argument('--build-directory', type=Path, default=ROOT / 'Builds/Manager5v5/all-command-limits-display-v10-20261016-r002')
parser.add_argument('--evidence-directory', type=Path)
parser.add_argument('--worker-id', type=int, default=249)
args = parser.parse_args()
run = 'display-v10-2800000-vs-2400000-' + datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ')
OUT = args.evidence_directory or ROOT / 'Logs/Manager5v5' / run
OUT.mkdir(parents=True, exist_ok=False)
MODEL = ROOT / 'results/Manager5v5/MNG5_V6-20261016-r002/MNG5_Manager_v6/MNG5_Manager_v6-2800000.onnx'
BUILD = args.build_directory / 'Soccer5v5.exe'
assert sha(MODEL) == '3cd1a76a01bd54947722262de290e9f6c13d6f5aafb6e97358670607e81d9557'
policies = {0: FrozenPolicy(str(MODEL), 164000)}
OPPONENT = ROOT / 'results/Manager5v5/MNG5_V5-20261016-r001/MNG5_Manager_v5/MNG5_Manager_v5-2400000.onnx'
assert sha(OPPONENT) == '7ccda24fb17b02443d7346a013be1081e853a6b4a515ec41d60f14f6449febfa'
policies[1] = FrozenPolicy(str(OPPONENT), 264000)
channel = EngineConfigurationChannel()
channel.set_configuration_parameters(time_scale=1, quality_level=2, target_frame_rate=60, capture_frame_rate=0)
rule_args = ['--five-red-step', '2800000', '--five-navy-step', '2400000', '--five-evaluate-once']
env = UnityEnvironment(file_name=str(BUILD), seed=64000, worker_id=args.worker_id, no_graphics=False,
    timeout_wait=3600, side_channels=[channel], log_folder=str(OUT), additional_args=rule_args + [
        '--five-execution-variant', 'all',
        '--five-p9-trace-dir', str(OUT), '--five-display-receipt', str(OUT/'live.json'),
        '-screen-fullscreen', '0', '-screen-width', '1600', '-screen-height', '1000'])
(OUT/'launch.json').write_text(json.dumps(dict(model=str(MODEL), modelSha256=sha(MODEL),
    red='2800000-step model', navy='2400000-step model', opponent=str(OPPONENT), opponentSha256=sha(OPPONENT), timeScale=1, matchSeconds=300,
    executable=str(BUILD), pid=env._process.pid, trainingStarted=False), indent=2), encoding='utf-8')
def inference():
    try:
        env.reset()
        names = {int(name.rsplit('=', 1)[1]): name for name in env.behavior_specs}
        assert set(names) == set(policies)
        while True:
            for side, name in names.items():
                decision, terminal = env.get_steps(name)
                if len(terminal): return
                if len(decision):
                    policy = policies[side]
                    p = policy.evaluator.run(None, {'obs_0': decision.obs[0][0][None].astype(np.float32),
                        'action_masks': np.ones((1, 8), np.float32)})[0][0].astype(float)
                    blocked = decision.action_mask[0][0] if decision.action_mask is not None else np.zeros(8, dtype=bool)
                    assert blocked.shape == (8,) and not blocked.all()
                    p[blocked] = 0
                    assert np.isfinite(p).all() and (p >= 0).all() and p.sum() > 0
                    p /= p.sum()
                    action = int(policy.rng.choice(8, p=p))
                    env.set_actions(name, ActionTuple(discrete=np.array([[action]], dtype=np.int32)))
            env.step()
    except Exception as error:
        if env._process.poll() is None:
            (OUT/'inference-error.txt').write_text(str(error), encoding='utf-8')
threading.Thread(target=inference, daemon=True).start()
try:
    while env._process.poll() is None:
        live = OUT/'live.json'
        if live.exists():
            try: receipt = json.loads(live.read_text(encoding='utf-8-sig'))
            except (ValueError, OSError):
                time.sleep(.1); continue
            assert receipt['playerRuntime'] == 'tactics-v10-all-command-limits'
            assert receipt['timeScale'] == 1 and abs(receipt['keeperAccelerationMultiplier']-1.2)<1e-5
            if receipt['finished'] and not (OUT/'display-complete.json').exists():
                assert receipt['elapsed'] >= 300
                (OUT/'display-complete.json').write_text(json.dumps(receipt,ensure_ascii=False,indent=2),encoding='utf-8')
                print('DISPLAY_COMPLETE',str(OUT),flush=True)
        time.sleep(1)
finally:
    env.close()

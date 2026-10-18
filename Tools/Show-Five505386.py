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
parser.add_argument('--self-match', action='store_true')
parser.add_argument('--build-directory', type=Path, default=ROOT / 'Builds/Manager5v5/stamina-turn-display-v8-20261015')
parser.add_argument('--evidence-directory', type=Path)
parser.add_argument('--worker-id', type=int, default=243)
args = parser.parse_args()
run = ('display-505386-self-' if args.self_match else 'display-505386-rule-') + datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ')
OUT = args.evidence_directory or ROOT / 'Logs/Manager5v5' / run
OUT.mkdir(parents=True, exist_ok=False)
MODEL = ROOT / 'results/Manager5v5/MNG5_V2-20261014-r002/MNG5_Manager_v2/MNG5_Manager_v2-505386.onnx'
BUILD = args.build_directory / 'Soccer5v5.exe'
assert sha(MODEL) == '5d5077dd51edef29b401e97a8c8d88b7c1a0375fb9ffb4ce36ad39fd5dd7d4b2'
policies = {0: FrozenPolicy(str(MODEL), 164000)}
if args.self_match: policies[1] = FrozenPolicy(str(MODEL), 264000)
channel = EngineConfigurationChannel()
channel.set_configuration_parameters(time_scale=1, quality_level=2, target_frame_rate=60, capture_frame_rate=0)
rule_args = [] if args.self_match else ['--five-rule-team', '1', '--five-rule-variant', 'strong']
env = UnityEnvironment(file_name=str(BUILD), seed=64000, worker_id=args.worker_id, no_graphics=False,
    timeout_wait=3600, side_channels=[channel], log_folder=str(OUT), additional_args=rule_args + [
        '--five-execution-variant', 'all',
        '--five-p9-trace-dir', str(OUT), '--five-display-receipt', str(OUT/'live.json'),
        '-screen-fullscreen', '0', '-screen-width', '1600', '-screen-height', '1000'])
(OUT/'launch.json').write_text(json.dumps(dict(model=str(MODEL), modelSha256=sha(MODEL),
    red='505386-step model', navy='505386-step model' if args.self_match else 'strong-v11', timeScale=1, matchSeconds=300,
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
                    assert set(np.flatnonzero(blocked)).issubset({1, 6})
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
        time.sleep(1)
finally:
    env.close()

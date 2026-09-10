"""Frozen Torch policy evaluation against the real Player; never updates a model."""
import argparse
from collections import defaultdict
import hashlib
import json
import math
import statistics
import time
from pathlib import Path
import numpy as np

from mlagents.torch_utils import torch, set_torch_config
from mlagents.trainers.settings import TorchSettings
from mlagents.trainers.learn import parse_command_line
from mlagents.trainers.policy.torch_policy import TorchPolicy
from mlagents.trainers.torch_entities.networks import SimpleActor
from mlagents.trainers.behavior_id_utils import get_global_agent_id
from mlagents_envs.environment import UnityEnvironment
from mlagents_envs.side_channel.engine_configuration_channel import EngineConfigurationChannel
from mlagents_envs.side_channel.environment_parameters_channel import EnvironmentParametersChannel
from mlagents_envs.side_channel.stats_side_channel import StatsSideChannel
from soccer_evaluation_seeding import seed_evaluation_sampling

ROOT = Path(__file__).resolve().parents[1]


def l2_advantageous_target_count(vector):
    """Read-only v2 Red observation diagnostic, not a steering or reward rule.

    Mirrors current Stadium constants (62, 42.32727) and vector distance scale 80.
    Checks geometry at decision time, not at the later physical strike.
    """
    if len(vector) != 43:
        raise ValueError("L2 observation diagnostic requires the 43-value v2 vector")
    x, z = float(vector[3]) * 62., float(vector[4]) * 42.32727
    own_goal_distance = math.hypot(62. - x, z)
    count = 0
    for offset in (15, 19, 23):
        dx, dz = float(vector[offset]) * 80., float(vector[offset + 1]) * 80.
        if (3. <= math.hypot(dx, dz) <= 16.
                and own_goal_distance - math.hypot(62. - x - dx, z + dz) >= 2.):
            count += 1
    return count


def project_path(value):
    path = (ROOT / value).resolve()
    if ROOT not in path.parents:
        raise ValueError("Evaluation paths must stay inside this project")
    return path


def summarize_evaluation_metrics(metrics, success_key, requested_episodes):
    """Return an exact core episode window when Unity publishes a final batch."""
    raw_episodes = len(metrics.get(success_key, ()))
    episode_count = min(raw_episodes, requested_episodes)
    rendered = {}
    for key, values in metrics.items():
        # Metrics emitted exactly once per completed episode have the same raw
        # count as Success. Event and conditional metrics keep their own count.
        selected = values[:episode_count] if len(values) == raw_episodes else values
        if selected:
            rendered[key] = {"count": len(selected), "mean": statistics.fmean(selected),
                             "sum": sum(selected)}
    return episode_count, raw_episodes, rendered


def l2_preparation_proxy(vector, discrete_action, branch_masks=None):
    """Read-only near-ball proxy, NOT proof of this agent's confirmed ownership.

    v2 exposes team possession and plate readiness, not the carrier identity.
    Exclude fully masked waiting actors and never use this diagnostic as success.
    """
    if len(vector) != 43:
        raise ValueError("L2 preparation proxy requires the 43-value v2 vector")
    if not all(math.isfinite(float(v)) for v in vector):
        return None
    if branch_masks is not None and all(all(mask[1:]) for mask in branch_masks):
        return None
    distance = math.hypot(float(vector[7]) * 80., float(vector[8]) * 80.)
    if vector[12] < .5 or vector[14] < .5 or distance > 1.8:
        return None
    masked = branch_masks is not None and all(branch_masks[3][1:])
    target_present = l2_advantageous_target_count(vector) > 0
    return {
        "Target Present": float(target_present),
        "Kick Masked": float(masked),
        "Forward Action": float(discrete_action[0] == 1),
        "Any Translation Action": float(discrete_action[0] != 0 or discrete_action[1] != 0),
        "Turn Action": float(discrete_action[2] != 0),
        "Ball Distance": distance,
    }


def run(args):
    validate_arguments(args)
    checkpoint = project_path(args.checkpoint)
    output = project_path(args.output)
    if output.exists():
        raise FileExistsError(f"Preserve existing evaluation; choose a new output: {output}")
    if output.exists():
        raise FileExistsError(f"Preserve existing evaluation; choose a new output: {output}")
    original_hash = hashlib.sha256(checkpoint.read_bytes()).hexdigest()
    settings = parse_command_line([str(project_path(args.config)), "--run-id", "evaluation-only"]).behaviors["Soccer4v4_Base"]
    settings.network_settings.deterministic = args.deterministic
    set_torch_config(TorchSettings(device="cpu"))
    torch.set_default_device("cpu")
    torch.set_num_threads(1)
    engine, parameters, stats = EngineConfigurationChannel(), EnvironmentParametersChannel(), StatsSideChannel()
    engine.set_configuration_parameters(time_scale=20, quality_level=0, target_frame_rate=-1)
    parameters.set_float_parameter("soccer_l1_phase", args.phase)
    parameters.set_float_parameter("soccer_l1_alignment", args.alignment)
    parameters.set_float_parameter("soccer_l1_spawn_difficulty", args.spawn_difficulty)
    parameters.set_float_parameter("soccer_l2_spawn_difficulty", args.spawn_difficulty)
    parameters.set_float_parameter("soccer_l2_waiting_assist", args.waiting_assist)
    parameters.set_float_parameter("soccer_l2_pass_aim_gate", args.pass_aim_gate)
    parameters.set_float_parameter("soccer_l2_pass_advice", args.pass_advice)
    metrics = defaultdict(list)
    env = None
    started = time.time()
    status, failure = "running", None
    success_key = f"Soccer/Curriculum/{args.lesson}/Success"
    terminal_agents = 0
    kick_choices = [0, 0, 0]
    kick_opportunities = 0
    eligible_kick_choices = [0, 0, 0]
    sampling_seed_applied = False
    action_trace = hashlib.sha256()

    def save():
        episodes, raw_episodes, rendered_metrics = summarize_evaluation_metrics(
            metrics, success_key, args.episodes)
        report = {"evaluation_protocol_version": 2,
                  "policy_sampling_seed": args.seed,
                  "sampling_seed_applied": sampling_seed_applied,
                  "action_trace_sha256": action_trace.hexdigest(),
                  "bitwise_reproducibility_requires_repeat_check": True,
                  "checkpoint": str(checkpoint), "sha256": original_hash,
                  "frozen": True, "optimizer_created": False, "status": status, "failure": failure,
                  "profile": args.profile, "phase": args.phase, "alignment": args.alignment, "seed": args.seed,
                  "requested_spawn_difficulty": args.spawn_difficulty,
                  "requested_waiting_assist": args.waiting_assist,
                  "requested_pass_aim_gate": args.pass_aim_gate,
                  "requested_pass_advice": args.pass_advice,
                  "preparation_diagnostics": args.preparation_diagnostics,
                  "preparation_proxy_is_not_confirmed_carrier": True,
                  "deterministic": args.deterministic, "episodes": episodes,
                  "requested_episodes": args.episodes,
                  "raw_observed_episodes": raw_episodes,
                  "discarded_final_stats_batch_episodes": raw_episodes - episodes,
                  "conditional_metrics_may_include_final_stats_batch_overshoot": raw_episodes > episodes,
                  "terminal_agents": terminal_agents, "wall_seconds": time.time() - started,
                  "kick_choices": kick_choices, "kick_opportunities": kick_opportunities,
                  "eligible_kick_choices": eligible_kick_choices,
                  "metrics": rendered_metrics}
        output.parent.mkdir(parents=True, exist_ok=True)
        output.write_text(json.dumps(report, indent=2), encoding="utf-8")
        return report

    try:
        save()
        env = UnityEnvironment(file_name=str(ROOT / "Builds/SoccerTraining/SoccerTraining.exe"),
                               seed=args.seed, base_port=args.port, worker_id=0, no_graphics=True,
                               timeout_wait=120, log_folder=str(output.parent / (output.stem + "-player")),
                               additional_args=["--training-profile", args.profile, "-job-worker-count", "1"],
                               side_channels=[engine, parameters, stats])
        env.reset()
        names = list(env.behavior_specs)
        if len(names) != 1 or not names[0].startswith("Soccer4v4_Base"):
            raise RuntimeError(f"Unexpected evaluation behaviors: {names}")
        name = names[0]
        policy = TorchPolicy(args.seed, env.behavior_specs[name], settings.network_settings,
                             SimpleActor, {"conditional_sigma": False, "tanh_squash": False})
        state = torch.load(str(checkpoint), map_location="cpu")
        policy.actor.load_state_dict(state["Policy"], strict=True)
        policy.actor.eval()
        # TorchPolicy stores seed but does not seed action sampling. Reset AFTER
        # initialization/loading so constructor random draws cannot shift actions.
        seed_evaluation_sampling(args.seed)
        sampling_seed_applied = True
        stats.get_and_reset_stats()
        reported = 0
        while len(metrics[success_key]) < args.episodes:
            if time.time() - started > args.max_seconds:
                raise TimeoutError("Evaluation time limit reached; partial results saved")
            decisions, terminals = env.get_steps(name)
            terminal_agents += len(terminals)
            policy.remove_memories([get_global_agent_id(0, int(i)) for i in terminals.agent_id])
            if len(decisions):
                action = policy.get_action(decisions, 0).action
                action_trace.update(np.asarray(decisions.agent_id, dtype="<i8").tobytes())
                action_trace.update(action.discrete.astype("<i4").tobytes())
                vector_obs = next((obs for obs in decisions.obs if obs.ndim == 2 and obs.shape[1] == 43), None)
                for index, choice in enumerate(action.discrete[:, 3]):
                    kick_choices[int(choice)] += 1
                    if args.lesson in ("L2", "L3") and args.preparation_diagnostics:
                        if vector_obs is None:
                            raise RuntimeError("Missing v2 vector for L2 preparation proxy")
                        masks = ([branch[index] for branch in decisions.action_mask]
                                 if decisions.action_mask is not None else None)
                        proxy = l2_preparation_proxy(vector_obs[index], action.discrete[index], masks)
                        if proxy is not None:
                            for key, value in proxy.items():
                                metrics["Soccer/Curriculum/L2/Preparation Proxy " + key].append(value)
                            if proxy["Kick Masked"]:
                                for key, value in proxy.items():
                                    metrics["Soccer/Curriculum/L2/Masked Preparation Proxy " + key].append(value)
                    eligible = decisions.action_mask is None or not all(decisions.action_mask[3][index, 1:])
                    if eligible:
                        kick_opportunities += 1
                        eligible_kick_choices[int(choice)] += 1
                        if args.lesson in ("L2", "L3"):
                            if vector_obs is None:
                                raise RuntimeError("Missing v2 vector for L2 observation diagnostic")
                            count = l2_advantageous_target_count(vector_obs[index])
                            metrics["Soccer/Curriculum/L2/Diagnostic Target Present At Kick Decision"].append(float(count > 0))
                            if count:
                                metrics["Soccer/Curriculum/L2/Diagnostic Kick Chosen With Target"].append(float(choice != 0))
                env.set_actions(name, action)
            env.step()
            for key, values in stats.get_and_reset_stats().items():
                if (key.startswith("Soccer/Curriculum/") or key.startswith("Soccer/Reward/")
                        or key.startswith("Soccer/Skill Advice/") or "/Skill Advice/" in key):
                    metrics[key].extend(float(value) for value, _ in values)
            count = len(metrics[success_key])
            if count >= reported + 25:
                report = save()
                print(json.dumps({"episodes": count, "success": report["metrics"][success_key]["mean"]}), flush=True)
                reported = count
        status = "complete"
    except BaseException as error:
        status, failure = "interrupted", repr(error)
        raise
    finally:
        if env is not None:
            env.close()
        if hashlib.sha256(checkpoint.read_bytes()).hexdigest() != original_hash:
            status, failure = "invalid", "Source checkpoint changed during evaluation"
        report = save()
        print(json.dumps({key: report[key] for key in ("status", "episodes", "failure", "wall_seconds")}), flush=True)


def validate_arguments(args):
    expected_profile = {
        "L0": "curriculum-l0",
        "L1": "curriculum-l1",
        "L2": "curriculum-l2",
        "L2Find": "curriculum-l2-find",
        "L2Score": "curriculum-l2-score",
        "L3": "curriculum-l3",
    }[args.lesson]
    if args.profile != expected_profile:
        raise ValueError(f"Lesson {args.lesson} requires --profile {expected_profile}")
    if args.episodes <= 0 or args.max_seconds <= 0:
        raise ValueError("episodes and max-seconds must be positive")
    if not 0 <= args.seed <= 2147483647:
        raise ValueError("Evaluation seed must be in [0, 2147483647]")


def build_parser():
    parser = argparse.ArgumentParser()
    parser.add_argument("--checkpoint", required=True)
    parser.add_argument("--config", default="Assets/_Soccer/Curriculum/L1_CarryAndShoot/Training/curriculum_l1_poca.yaml")
    parser.add_argument("--output", required=True)
    parser.add_argument("--profile", default="curriculum-l1", choices=(
        "curriculum-l0", "curriculum-l1", "curriculum-l2",
        "curriculum-l2-find", "curriculum-l2-score", "curriculum-l3"))
    parser.add_argument("--lesson", default="L1", choices=("L0", "L1", "L2", "L2Find", "L2Score", "L3"))
    parser.add_argument("--phase", type=float, default=1)
    parser.add_argument("--alignment", type=float, default=0)
    parser.add_argument("--spawn-difficulty", type=float, default=1, choices=(0, 0.5, 1))
    parser.add_argument("--waiting-assist", type=int, default=0, choices=(0, 1))
    parser.add_argument("--pass-aim-gate", type=int, default=0, choices=(0, 1))
    parser.add_argument("--pass-advice", type=int, default=0, choices=(0, 1))
    parser.add_argument("--preparation-diagnostics", action="store_true")
    parser.add_argument("--seed", type=int, default=54321)
    parser.add_argument("--episodes", type=int, default=300)
    parser.add_argument("--port", type=int, default=5905)
    parser.add_argument("--max-seconds", type=int, default=1200)
    parser.add_argument("--deterministic", action="store_true")
    return parser


if __name__ == "__main__":
    parser = build_parser()
    args = parser.parse_args()
    try:
        validate_arguments(args)
    except ValueError as error:
        parser.error(str(error))
    run(args)

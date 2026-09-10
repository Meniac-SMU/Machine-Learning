"""Read-only trainer/host inspection; optional generated JSON report inside this repo."""
import argparse
import ctypes
import json
import math
import re
import statistics
import subprocess
import time
from pathlib import Path
from tensorboard.backend.event_processing.event_accumulator import EventAccumulator

ROOT = Path(__file__).resolve().parents[1]


def l1_gate(metrics):
    phase = round(metrics.get("Soccer/Curriculum/L1/Phase", {}).get("last", 0))
    success_points = metrics.get("Soccer/Curriculum/L1/Success", {}).get("points", [])[-5:]
    expected_steps = [step for step, _ in success_points]
    aligned_steps = len(expected_steps) == 5 and len(set(expected_steps)) == 5
    requirements = {"Success": (0.75 if phase == 1 else 0.65, "min"),
                    "Possession": (0.85, "min"), "Valid Shot": (0.85, "min"),
                    "Episode Seconds": (20, "max")}
    if phase == 2:
        requirements["Shot On Target"] = (0.75, "min")
    checks = {}
    for name, (threshold, direction) in requirements.items():
        points = metrics.get("Soccer/Curriculum/L1/" + name, {}).get("points", [])[-5:]
        aligned_steps = aligned_steps and [step for step, _ in points] == expected_steps
        checks[name] = len(points) == 5 and all(
            value >= threshold if direction == "min" else value <= threshold for _, value in points)
    phase_points = metrics.get("Soccer/Curriculum/L1/Phase", {}).get("points", [])[-5:]
    aligned_steps = aligned_steps and [step for step, _ in phase_points] == expected_steps
    stable_phase = len(phase_points) == 5 and all(abs(value - phase) < 1e-5 for _, value in phase_points)
    no_legacy_rewards = not any(
        tag.startswith("Soccer/Reward/") and not tag.startswith("Soccer/Reward/Curriculum")
        and any(abs(value) > 1e-8 for _, value in data.get("points", []))
        for tag, data in metrics.items())
    difficulty_data = metrics.get("Soccer/Curriculum/L1/Spawn Difficulty")
    # Older builds did not expose this parameter and always used the full spawn range.
    difficulty_points = difficulty_data.get("points", [])[-5:] if difficulty_data else []
    difficulty = difficulty_data.get("last", -1) if difficulty_data else 1.0
    stable_difficulty = difficulty_data is None or (len(difficulty_points) == 5
        and [step for step, _ in difficulty_points] == expected_steps
        and all(abs(value - difficulty) < 1e-5 for _, value in difficulty_points))
    task_passed = (phase in (1, 2) and stable_phase and aligned_steps and stable_difficulty
                   and no_legacy_rewards and all(checks.values()))
    return {"phase": phase, "five_consecutive_summary_checks": checks,
            "aligned_summary_steps": aligned_steps, "no_legacy_rewards": no_legacy_rewards,
            "spawn_difficulty": difficulty, "stable_spawn_difficulty": stable_difficulty,
            "subtask_gate_passed": task_passed,
            "training_gate_passed": task_passed and abs(difficulty - 1.0) < 1e-5,
            "requires_frozen_evaluation_before_final_promotion": True}


def l2_gate(metrics):
    prefix = "Soccer/Curriculum/L2/"
    success = metrics.get(prefix + "Success", {}).get("points", [])[-5:]
    steps = [step for step, _ in success]
    aligned = len(steps) == 5 and all(b > a for a, b in zip(steps, steps[1:]))
    checks = {}
    for name, threshold, minimum in (("Success", .5, True), ("Possession", .85, True),
                                      ("Episode Seconds", 15., False)):
        points = metrics.get(prefix + name, {}).get("points", [])[-5:]
        aligned = aligned and [step for step, _ in points] == steps
        checks[name] = len(points) == 5 and all(math.isfinite(value)
            and (value >= threshold if minimum else 0 <= value <= threshold) for _, value in points)
    difficulty_points = metrics.get(prefix + "Spawn Difficulty", {}).get("points", [])[-5:]
    difficulty = difficulty_points[-1][1] if difficulty_points else None
    stable = (len(difficulty_points) == 5 and [step for step, _ in difficulty_points] == steps
              and difficulty in (0., .5, 1.)
              and all(math.isfinite(value) and abs(value - difficulty) < 1e-5
                      for _, value in difficulty_points))
    allowed = {"CurriculumPossessionEstablished", "CurriculumSupportShape", "CurriculumLessonSuccess",
               "CurriculumPassAttempt", "CurriculumPassReception", "CurriculumPassDelivered",
               "CurriculumPassDirection", "CurriculumPassOpportunityLost"}
    leaks = [tag for tag, data in metrics.items()
             if tag.startswith("Soccer/Reward/") and tag.rsplit("/", 1)[-1] not in allowed
             and any(not math.isfinite(value) or abs(value) > 1e-8
                     for _, value in data.get("points", []))]
    passed = aligned and stable and not leaks and all(checks.values())
    assistance = metrics.get(prefix + "Waiting Assistance", {}).get("points", [])[-5:]
    unassisted = (len(assistance) == 5 and [step for step, _ in assistance] == steps
                   and all(math.isfinite(value) and abs(value) < 1e-8 for _, value in assistance))
    pass_gate = metrics.get(prefix + "Pass Aim Gate", {}).get("points", [])[-5:]
    pass_gate_off = (len(pass_gate) == 5 and [step for step, _ in pass_gate] == steps
                    and all(math.isfinite(value) and abs(value) < 1e-8 for _, value in pass_gate))
    return {"lesson": "L2", "five_consecutive_summary_checks": checks,
            "aligned_summary_steps": aligned, "spawn_difficulty": difficulty,
            "stable_spawn_difficulty": stable, "disallowed_reward_leaks": leaks,
            "unassisted_final_window": unassisted,
            "pass_aim_gate_off_final_window": pass_gate_off,
            "subtask_gate_passed": passed,
            "training_gate_passed": passed and difficulty == 1. and unassisted and pass_gate_off,
            "requires_frozen_evaluation_before_final_promotion": True,
            "requires_l0_l1_regression_before_final_promotion": True}


def l2_find_gate(metrics):
    prefix = "Soccer/Curriculum/L2Find/"
    success = metrics.get(prefix + "Success", {}).get("points", [])[-5:]
    steps = [step for step, _ in success]
    aligned = len(steps) == 5 and all(b > a for a, b in zip(steps, steps[1:]))
    checks = {}
    for name, threshold, minimum in (("Success", .99, True),
                                      ("Possession", .99, True),
                                      ("Episode Seconds", 15., False)):
        points = metrics.get(prefix + name, {}).get("points", [])[-5:]
        aligned = aligned and [step for step, _ in points] == steps
        checks[name] = len(points) == 5 and all(math.isfinite(value)
            and (value >= threshold if minimum else 0 <= value <= threshold)
            for _, value in points)
    allowed = {"CurriculumFindProgress", "CurriculumFindHeading",
               "CurriculumFastFind", "CurriculumFindTimeoutPenalty", "CurriculumFindMissPenalty",
               "CurriculumPossessionEstablished",
               "CurriculumLessonSuccess"}
    leaks = [tag for tag, data in metrics.items()
             if tag.startswith("Soccer/Reward/") and tag.rsplit("/", 1)[-1] not in allowed
             and any(not math.isfinite(value) or abs(value) > 1e-8
                     for _, value in data.get("points", []))]
    passed = aligned and not leaks and all(checks.values())
    return {"lesson": "L2-Find", "five_consecutive_summary_checks": checks,
            "aligned_summary_steps": aligned, "disallowed_reward_leaks": leaks,
            "training_gate_passed": passed,
            "requires_fixed_300_success_rate": .99,
            "requires_frozen_evaluation_before_final_promotion": True}


def l2_score_gate(metrics):
    prefix = "Soccer/Curriculum/L2Score/"
    success = metrics.get(prefix + "Success", {}).get("points", [])[-5:]
    steps = [step for step, _ in success]
    aligned = len(steps) == 5 and all(b > a for a, b in zip(steps, steps[1:]))
    checks = {}
    for name, threshold, minimum in (("Success", .95, True),
                                      ("Possession", .95, True),
                                      ("Episode Seconds", 30., False),
                                      ("Own Goal", 0., False)):
        points = metrics.get(prefix + name, {}).get("points", [])[-5:]
        aligned = aligned and [step for step, _ in points] == steps
        checks[name] = len(points) == 5 and all(math.isfinite(value)
            and (value >= threshold if minimum else 0 <= value <= threshold)
            for _, value in points)
    allowed = {"CurriculumPossessionEstablished", "CurriculumLessonSuccess",
               "CurriculumScoreProgress", "CurriculumShotAttempt",
               "CurriculumShotOnTarget", "CurriculumGoal",
               "CurriculumLongPassGoalBonus", "CurriculumOwnGoalPenalty"}
    leaks = [tag for tag, data in metrics.items()
             if tag.startswith("Soccer/Reward/") and tag.rsplit("/", 1)[-1] not in allowed
             and any(not math.isfinite(value) or abs(value) > 1e-8
                     for _, value in data.get("points", []))]
    passed = aligned and not leaks and all(checks.values())
    return {"lesson": "L2-Score", "five_consecutive_summary_checks": checks,
            "aligned_summary_steps": aligned, "disallowed_reward_leaks": leaks,
            "training_gate_passed": passed,
            "requires_fixed_300_success_rate": .95,
            "requires_fixed_300_own_goals": 0,
            "requires_frozen_evaluation_before_final_promotion": True,
            "requires_l0_l1_l2_regression_before_final_promotion": True}


def l3_gate(metrics):
    prefix = "Soccer/Curriculum/L3/"
    success = metrics.get(prefix + "Success", {}).get("points", [])[-5:]
    steps = [step for step, _ in success]
    aligned = len(steps) == 5 and all(b > a for a, b in zip(steps, steps[1:]))
    checks = {}
    for name, threshold, minimum in (
            ("Success", .5, True), ("Possession", .85, True),
            ("Strict Pass Completed", .85, True), ("Stable Receiver Possession", .85, True),
            ("Episode Seconds", 15., False)):
        points = metrics.get(prefix + name, {}).get("points", [])[-5:]
        aligned = aligned and [step for step, _ in points] == steps
        checks[name] = len(points) == 5 and all(math.isfinite(value)
            and (value >= threshold if minimum else 0 <= value <= threshold) for _, value in points)
    difficulty_points = metrics.get("Soccer/Curriculum/L2/Spawn Difficulty", {}).get("points", [])[-5:]
    difficulty = difficulty_points[-1][1] if difficulty_points else None
    stable = (len(difficulty_points) == 5 and [step for step, _ in difficulty_points] == steps
              and difficulty in (0., .5, 1.)
              and all(math.isfinite(value) and abs(value - difficulty) < 1e-5
                      for _, value in difficulty_points))
    allowed = {"CurriculumPossessionEstablished", "CurriculumSupportShape", "CurriculumLessonSuccess",
               "CurriculumPassAttempt", "CurriculumPassReception", "CurriculumPassDelivered",
               "CurriculumPassDirection", "CurriculumReceiverProgress"}
    leaks = [tag for tag, data in metrics.items()
             if tag.startswith("Soccer/Reward/") and tag.rsplit("/", 1)[-1] not in allowed
             and any(not math.isfinite(value) or abs(value) > 1e-8
                     for _, value in data.get("points", []))]
    assistance = metrics.get("Soccer/Curriculum/L2/Waiting Assistance", {}).get("points", [])[-5:]
    unassisted = (len(assistance) == 5 and [step for step, _ in assistance] == steps
                  and all(math.isfinite(value) and abs(value) < 1e-8 for _, value in assistance))
    pass_gate = metrics.get("Soccer/Curriculum/L2/Pass Aim Gate", {}).get("points", [])[-5:]
    pass_gate_off = (len(pass_gate) == 5 and [step for step, _ in pass_gate] == steps
                     and all(math.isfinite(value) and abs(value) < 1e-8 for _, value in pass_gate))
    passed = aligned and stable and not leaks and all(checks.values())
    return {"lesson": "L3", "five_consecutive_summary_checks": checks,
            "aligned_summary_steps": aligned, "spawn_difficulty": difficulty,
            "stable_spawn_difficulty": stable, "disallowed_reward_leaks": leaks,
            "unassisted_final_window": unassisted,
            "pass_aim_gate_off_final_window": pass_gate_off,
            "training_gate_passed": passed and difficulty == 1. and unassisted and pass_gate_off,
            "requires_frozen_evaluation_before_final_promotion": True,
            "requires_l0_l1_l2_regression_before_final_promotion": True}


def host_snapshot():
    class Memory(ctypes.Structure):
        _fields_ = [("length", ctypes.c_ulong), ("load", ctypes.c_ulong)] + [
            (name, ctypes.c_ulonglong) for name in (
                "total", "available", "page_total", "page_available", "virtual_total",
                "virtual_available", "extended_available")]
    memory = Memory()
    memory.length = ctypes.sizeof(memory)
    memory_ok = ctypes.windll.kernel32.GlobalMemoryStatusEx(ctypes.byref(memory))

    def cpu_times():
        idle, kernel, user = ctypes.c_ulonglong(), ctypes.c_ulonglong(), ctypes.c_ulonglong()
        ok = ctypes.windll.kernel32.GetSystemTimes(ctypes.byref(idle), ctypes.byref(kernel), ctypes.byref(user))
        return (idle.value, kernel.value + user.value) if ok else None
    before = cpu_times()
    time.sleep(1)
    after = cpu_times()
    cpu = (100 * (1 - (after[0] - before[0]) / (after[1] - before[1]))
           if before and after and after[1] > before[1] else None)
    try:
        gpu = subprocess.check_output([
            "nvidia-smi", "--query-gpu=name,utilization.gpu,memory.used,memory.total,temperature.gpu,power.draw",
            "--format=csv,noheader,nounits"], text=True, timeout=10).strip()
    except (OSError, subprocess.SubprocessError) as error:
        gpu = str(error)
    return {"cpu_percent": cpu, "memory_percent": memory.load if memory_ok else None,
            "available_gib": memory.available / 2**30 if memory_ok else None, "gpu": gpu}


def inspect(run_id, last, at_step=None):
    if not re.fullmatch(r"[A-Za-z0-9_-]+", run_id):
        raise ValueError("Invalid run id")
    if at_step is not None and at_step < 0:
        raise ValueError("Historical step cannot be negative")
    run = ROOT / "results" / run_id
    behavior = run / "Soccer4v4_Base"
    if not behavior.is_dir() and run.is_dir():
        candidates = sorted(path for path in run.iterdir()
                            if path.is_dir() and any(path.glob("events.out.tfevents.*")))
        if len(candidates) != 1:
            raise FileNotFoundError(
                f"Expected exactly one TensorBoard behavior directory in {run}, found {len(candidates)}")
        behavior = candidates[0]
    accumulator = EventAccumulator(str(behavior), size_guidance={"scalars": 0})
    accumulator.Reload()
    metrics = {}
    for tag in accumulator.Tags()["scalars"]:
        if (tag.startswith("Soccer/Curriculum/") or tag.startswith("Soccer/Skill Advice/")
                or tag.startswith("Environment/") or "/Reward/" in tag or "/Skill Advice/" in tag):
            samples = accumulator.Scalars(tag)
            if at_step is not None:
                samples = [sample for sample in samples if sample.step <= at_step]
            chosen = samples[-last:]
            if not chosen:
                continue
            metrics[tag] = {"last": chosen[-1].value, "mean": statistics.fmean(x.value for x in chosen),
                            "points": [[x.step, x.value] for x in chosen]}
    step_samples = accumulator.Scalars("Environment/Cumulative Reward") if "Environment/Cumulative Reward" in metrics else []
    if at_step is not None:
        step_samples = [sample for sample in step_samples if sample.step <= at_step]
    rate_samples = step_samples[-last:]
    rate = ((rate_samples[-1].step - rate_samples[0].step) /
            (rate_samples[-1].wall_time - rate_samples[0].wall_time)
            if len(rate_samples) > 1 and rate_samples[-1].wall_time > rate_samples[0].wall_time else None)
    legacy = [tag for tag, data in metrics.items() if tag.startswith("Soccer/Reward/")
              and not tag.startswith("Soccer/Reward/Curriculum") and any(abs(x[1]) > 1e-8 for x in data["points"])]
    return {"run_id": run_id, "behavior": behavior.name,
            "timestamp": time.time(), "up_to_step": at_step,
            "host_is_current_not_historical": True,
            "summary_step": step_samples[-1].step if step_samples else 0,
            "summary_means_are_unweighted": True, "steps_per_second": rate,
            "legacy_reward_leaks": legacy,
            "gate": (l2_find_gate(metrics) if "Soccer/Curriculum/L2Find/Success" in metrics
                     else l2_score_gate(metrics) if "Soccer/Curriculum/L2Score/Success" in metrics
                     else l3_gate(metrics) if "Soccer/Curriculum/L3/Success" in metrics
                     else l2_gate(metrics) if "Soccer/Curriculum/L2/Success" in metrics
                     else l1_gate(metrics) if behavior.name == "Soccer4v4_Base"
                     else {"kind": "unconfigured-behavior", "promotion_decision": False}),
            "host": host_snapshot(), "metrics": metrics}


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--run-id")
    parser.add_argument("--last", type=int, default=5)
    parser.add_argument("--at-step", type=int, help="Inspect only summaries through this training step")
    parser.add_argument("--output")
    args = parser.parse_args()
    result = inspect(args.run_id, args.last, args.at_step) if args.run_id else host_snapshot()
    rendered = json.dumps(result, indent=2)
    print(rendered)
    if args.output:
        output = (ROOT / args.output).resolve()
        if ROOT not in output.parents:
            raise ValueError("Report must stay within the project")
        output.parent.mkdir(parents=True, exist_ok=True)
        output.write_text(rendered, encoding="utf-8")

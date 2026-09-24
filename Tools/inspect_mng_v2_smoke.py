"""Validate completed R4 smoke evidence without promoting a policy."""
import hashlib
import json
import math
from pathlib import Path
import sys

import onnx
import torch
from tensorboard.backend.event_processing.event_accumulator import EventAccumulator


def finite(value):
    if isinstance(value, torch.Tensor):
        assert bool(torch.isfinite(value).all()), "Nonfinite checkpoint tensor"
    elif isinstance(value, dict):
        for item in value.values(): finite(item)
    elif isinstance(value, (list, tuple)):
        for item in value: finite(item)
    elif isinstance(value, float):
        assert math.isfinite(value), "Nonfinite JSON value"


def main(run_id):
    root = Path(__file__).resolve().parent.parent
    evidence = root / "Logs/MNG-Rebuild" / run_id
    policy = root / "results" / run_id / "MNG_ManagerV2"
    report = {"run": run_id, "status": "smoke-only-ineligible", "phases": {}}
    for phase in ("workers", "resume-workers"):
        records = [json.loads(line) for p in (evidence / phase).glob("*.jsonl") for line in p.read_text(encoding="utf-8-sig").splitlines()]
        boots = [r for r in records if r.get("event") == "worker-start"]
        assert len({r["worker"] for r in boots}) == 32, (phase, "Missing workers")
        telemetry = [r for r in records if "schema" in r]
        assert len({r["worker"] for r in telemetry}) == 32, (phase, "Missing v2 telemetry")
        decisions, strikes, seen = {}, {}, set()
        for record in telemetry:
            assert record["schema"] == "MNG-OBS-v2-244"
            finite(record)
            for entry in record.get("trace", []):
                identity = (record["worker"], entry["episode"], entry["sequence"])
                if identity in seen: continue  # adjacent ring dumps deliberately overlap
                seen.add(identity)
                if entry["kind"] == "PolicyDecision":
                    assert entry["rawCommand"] == entry["effectiveCommand"]
                    assert entry["parentCommandId"] > 0
                    decisions[identity] = entry
                if entry["kind"].startswith("Strike:"):
                    assert entry["taskId"] > 0 and entry["parentCommandId"] > 0 and entry["source"]
                    kick = (record["worker"], entry["episode"], entry["kickId"])
                    assert kick not in strikes, "Duplicate physical kick"
                    strikes[kick] = entry
        assert decisions, "Missing sampled policy decisions"
        report["phases"][phase] = dict(workers=32, telemetry_records=len(telemetry),
            sampled_decisions=len(decisions), sampled_strikes=len(strikes), sampled_duplicate_kicks=0,
            sampled_raw_effective_mismatches=0, completed_episodes=sum(r["completed"] for r in telemetry))
    checkpoint = torch.load(policy / "checkpoint.pt", map_location="cpu", weights_only=False)
    first = torch.load(evidence / "first-stop/checkpoint.pt", map_location="cpu", weights_only=False)
    pool = torch.load(policy / "mng-v2-pool.pt", map_location="cpu", weights_only=False)
    finite(checkpoint)
    step = int(checkpoint["global_step"]["_GlobalSteps__global_step"])
    initial_step = int(first["global_step"]["_GlobalSteps__global_step"])
    assert step > initial_step and pool["step"] == step
    assert checkpoint["Optimizer:value_optimizer"]["state"], "Missing optimizer state"
    first_updates = max(float(x["step"]) for x in first["Optimizer:value_optimizer"]["state"].values())
    updates = max(float(x["step"]) for x in checkpoint["Optimizer:value_optimizer"]["state"].values())
    assert updates > first_updates > 0
    events = [json.loads(x) for x in (policy / "mng-v2-pool.jsonl").read_text().splitlines()]
    restored = next(x for x in events if x["event"] == "restored")
    previous = next(x for x in reversed(events[:events.index(restored)]) if x["event"] == "saved")
    for key in ("step", "learning_team", "ghost_step", "last_save", "last_swap", "last_team_change", "pinned"):
        assert restored[key] == previous[key], ("Restore mismatch", key)
    model_path = policy / f"MNG_ManagerV2-{step}.onnx"
    model = onnx.load(model_path)
    observation = next(x for x in model.graph.input if x.name == "obs_0")
    assert observation.type.tensor_type.shape.dim[1].dim_value == 244
    accumulator = EventAccumulator(str(policy)); accumulator.Reload()
    scalar_tags = accumulator.Tags()["scalars"]
    required_metrics = ("Policy/Approx KL", "Policy/Clip Fraction", "Policy/Entropy")
    assert all(tag in scalar_tags for tag in required_metrics), "Missing PPO diagnostics"
    for tag in scalar_tags:
        for scalar in accumulator.Scalars(tag): assert math.isfinite(scalar.value), tag
    metric_ranges = {tag: dict(min=min(x.value for x in accumulator.Scalars(tag)),
                              max=max(x.value for x in accumulator.Scalars(tag))) for tag in required_metrics}
    assert 0 <= metric_ranges["Policy/Clip Fraction"]["min"] <= metric_ranges["Policy/Clip Fraction"]["max"] <= 1
    report.update(initial_step=initial_step, final_step=step, optimizer_updates_before=first_updates,
        optimizer_updates_after=updates, restored_pool_and_counters=True, onnx_observations=244,
        scalar_tags=scalar_tags, metric_ranges=metric_ranges, onnx_sha256=hashlib.sha256(model_path.read_bytes()).hexdigest(),
        resume_scope="optimizer/pool/RNG/counters restored; world and in-flight trajectories restart")
    output = evidence / "smoke-verification.json"
    output.write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps(report, indent=2))


if __name__ == "__main__": main(sys.argv[1])

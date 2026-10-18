"""CPU-only strict PT/ONNX parity and recursive checkpoint finiteness audit."""
import os
os.environ["CUDA_VISIBLE_DEVICES"] = "-1"
os.environ["OMP_NUM_THREADS"] = "1"
os.environ["OPENBLAS_NUM_THREADS"] = "1"
import argparse
import json
from pathlib import Path
import subprocess
import sys
import numpy as np
import hashlib

def sha(path): return hashlib.sha256(Path(path).read_bytes()).hexdigest()
def write(path, value): path.write_text(json.dumps(value, indent=2), encoding="utf-8")


def finite(value):
    import torch
    if torch.is_tensor(value):
        assert torch.isfinite(value).all(), "Non-finite checkpoint tensor"
        return value.numel()
    if isinstance(value, dict): return sum(finite(v) for v in value.values())
    if isinstance(value, (list, tuple)): return sum(finite(v) for v in value)
    if isinstance(value, float): assert np.isfinite(value)
    return 0


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--checkpoint", type=Path, required=True)
    parser.add_argument("--observations", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--onnx-only", action="store_true")
    args = parser.parse_args()
    assert not args.output.exists()
    files = sorted(args.observations.rglob("observations.npy"))
    assert files, "Real Player observations are required"
    obs = np.concatenate([np.load(p) for p in files])
    assert obs.ndim == 2 and obs.shape[1] == 676 and np.isfinite(obs).all()
    if args.onnx_only:
        from five_evaluate import FrozenPolicy
        frozen = FrozenPolicy(str(args.checkpoint.with_suffix(".onnx")), 0)
        q = frozen.evaluator.run(None, {"obs_0": obs.astype(np.float32), "action_masks": np.ones((len(obs), 8), np.float32)})[0]
        np.save(args.output, q)
        return
    # ONNX ReferenceEvaluator (NumPy MKL) and Torch ship separate OpenMP DLLs.
    # Keep their matrix runtimes in separate processes; never suppress the conflict.
    onnx_probabilities = args.output.with_suffix(".onnx-probabilities.npy")
    subprocess.run([sys.executable, __file__, "--checkpoint", str(args.checkpoint),
                    "--observations", str(args.observations), "--output", str(onnx_probabilities), "--onnx-only"], check=True)
    import torch
    from mlagents.trainers.settings import NetworkSettings
    from mlagents.trainers.torch_entities.networks import SimpleActor
    from mlagents_envs.base_env import ActionSpec, ObservationSpec, ObservationType, DimensionProperty
    torch.set_num_threads(1)
    saved = torch.load(args.checkpoint, map_location="cpu")
    elements = finite(saved)
    assert set(saved) == {"Policy", "global_step", "Optimizer:value_optimizer", "Optimizer:critic"}
    step = int(saved["global_step"]["_GlobalSteps__global_step"].item())
    actor = SimpleActor(
        observation_specs=[ObservationSpec((676,), (DimensionProperty.NONE,), ObservationType.DEFAULT, "VectorSensor")],
        network_settings=NetworkSettings(normalize=False, hidden_units=256, num_layers=2),
        action_spec=ActionSpec.create_discrete((8,)), conditional_sigma=False, tanh_squash=False)
    actor.load_state_dict(saved["Policy"], strict=True)
    actor.eval()
    onnx = args.checkpoint.with_suffix(".onnx")
    tensor = torch.as_tensor(obs, dtype=torch.float32)
    with torch.no_grad():
        encoding, _ = actor.network_body([tensor])
        p = actor.action_model._get_dists(encoding, torch.ones(len(obs), 8)).discrete[0].probs.numpy()
    q = np.load(onnx_probabilities)
    error = float(np.max(np.abs(p-q)))
    assert error < 1e-5, f"PT/ONNX probability mismatch: {error}"
    optimizer = saved["Optimizer:value_optimizer"]["state"]
    updates = sorted(set(int(v["step"].item()) for v in optimizer.values() if "step" in v))
    report = dict(passed=True, step=step, optimizerSteps=updates, finiteTensorElements=elements,
                  realObservationRows=len(obs), maxProbabilityError=error,
                  ptSha256=sha(args.checkpoint), onnxSha256=sha(onnx), auditSha256=sha(__file__))
    write(args.output, report)
    print(json.dumps(report))


if __name__ == "__main__": main()

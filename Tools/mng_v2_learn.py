"""Project-local GhostTrainer adapter. Never modifies the installed trainer.

The sidecar restores the opponent pool and its scheduling state. Unity world
state and in-flight rollouts restart, as with stock ML-Agents resume.
"""
import copy
import hashlib
import json
import os
from pathlib import Path
import random
import sys
import time

import numpy as np
import torch
import mlagents.trainers.trainer  # initialize the package before its GhostTrainer import
from mlagents.trainers.ghost.trainer import GhostTrainer
from mlagents.trainers.behavior_id_utils import create_name_behavior_id


def snapshot_sha(snapshot):
    h = hashlib.sha256()
    for name, weights in sorted(snapshot.items()):
        h.update(name.encode())
        for key, value in sorted(weights.items()):
            h.update(key.encode())
            h.update(value.detach().cpu().contiguous().numpy().tobytes())
    return h.hexdigest()

def historical_snapshot(path, reference):
    state=torch.load(path,map_location='cpu',weights_only=False)
    snapshot={'MNG_ManagerV2':copy.deepcopy(state['Policy'])}
    expected=reference['MNG_ManagerV2']
    assert set(snapshot['MNG_ManagerV2'])==set(expected), 'Historical policy schema mismatch'
    for key,value in snapshot['MNG_ManagerV2'].items():
        assert value.shape==expected[key].shape and torch.isfinite(value).all(), 'Invalid historical policy weights'
    return snapshot


def validate_v3_pool(saved=None, initial=None):
    if os.environ.get('MNG_EXPERIMENT_GENERATION') != 'v3':
        return
    assert not os.environ.get('MNG_V2_PINNED_POLICY'), 'v3 forbids external history'
    expected = os.environ.get('MNG_V3_INITIAL_SNAPSHOT_SHA')
    assert expected, 'v3 base snapshot identity required'
    if initial is not None:
        assert snapshot_sha(initial) == expected, 'v3 initial actor mismatch'
    if saved is not None:
        assert saved.get('generation') == 'v3' and saved.get('baseSnapshotSha') == expected, 'v3 pool lineage mismatch'
        assert len(saved['pinned']) == 1 and snapshot_sha(saved['pinned'][0]) == expected, 'v3 pinned pool mismatch'


class MNGV2GhostTrainer(GhostTrainer):
    fields = ("ghost_step", "snapshot_counter", "_learning_team", "wrapped_trainer_team",
              "last_save", "last_swap", "last_team_change", "policy_elos", "current_opponent",
              "policy_snapshots", "current_policy_snapshot")

    def __init__(self, *args, **kwargs):
        validate_v3_pool()
        super().__init__(*args, **kwargs)
        if self.brain_name != "MNG_ManagerV2":
            raise ValueError("V2 adapter only accepts MNG_ManagerV2")
        # Installed GhostTrainer passes its base artifact/reward arguments in the
        # old order. The wrapped PPO trainer owns the authoritative output path.
        self.sidecar = Path(self.trainer.artifact_path) / "mng-v2-pool.pt"
        self.audit = Path(self.trainer.artifact_path) / "mng-v2-pool.jsonl"
        self.rng = np.random.RandomState(int(os.environ.get("MNG_V2_POOL_SEED", "20260922")))
        self.pinned = []
        self.pending_restore = None
        if "--resume" in sys.argv:
            if not self.sidecar.is_file():
                raise RuntimeError("V2 resume requires the saved opponent pool sidecar")
            self.pending_restore = torch.load(self.sidecar, map_location="cpu", weights_only=False)
            validate_v3_pool(saved=self.pending_restore)
            if self.pending_restore["schema"] != "MNG-OBS-v2-244":
                raise RuntimeError("V2 sidecar schema mismatch")
        checkpoint = self.trainer._checkpoint
        def checkpoint_with_pool():
            result = checkpoint()
            self._save_pool()
            return result
        self.trainer._checkpoint = checkpoint_with_pool

    def _audit(self, event, **details):
        self.audit.parent.mkdir(parents=True, exist_ok=True)
        with self.audit.open("a", encoding="utf-8") as f:
            f.write(json.dumps(dict(event=event, step=self.get_step, utcSeconds=time.time(), **details)) + "\n")

    def _save_snapshot(self):
        super()._save_snapshot()
        if self.current_policy_snapshot and not self.pinned and self.pending_restore is None:
            validate_v3_pool(initial=self.current_policy_snapshot)
            self.pinned.append(copy.deepcopy(self.current_policy_snapshot))
            self._audit("pin-step0", sha=snapshot_sha(self.pinned[0]))
            historical=os.environ.get('MNG_V2_PINNED_POLICY')
            if historical:
                snapshot=historical_snapshot(historical,self.current_policy_snapshot)
                identity=snapshot_sha(snapshot)
                if identity!=snapshot_sha(self.pinned[0]):self.pinned.append(snapshot)
                self._audit('pin-historical-reference',sha=identity,checkpoint_sha=hashlib.sha256(Path(historical).read_bytes()).hexdigest())

    def _swap_snapshots(self):
        for team in self._team_to_name_to_policy_queue:
            if team == self._learning_team:
                continue
            roll = self.rng.uniform()
            if roll < .3:
                category, probability, snapshot, index = "latest", .3, self.current_policy_snapshot, -1
            elif roll < .8:
                index = int(self.rng.randint(len(self.policy_snapshots)))
                category, probability, snapshot = "recent", .5, self.policy_snapshots[index]
            else:
                if not self.pinned:
                    raise RuntimeError("Missing pinned step0 policy")
                index = int(self.rng.randint(len(self.pinned)))
                category, probability, snapshot = "pinned", .2, self.pinned[index]
            self.current_opponent = index if category == "recent" else -1
            self._audit("swap", category=category, probability=probability,
                        sha=snapshot_sha(snapshot), learning_team=self._learning_team, ghost_step=self.ghost_step)
            for brain, queue in self._team_to_name_to_policy_queue[team].items():
                policy = self.get_policy(create_name_behavior_id(brain, team))
                policy.load_weights(snapshot[brain])
                queue.put(policy)

    def advance(self):
        if self.pending_restore is not None:
            saved = self.pending_restore
            if self.get_step != saved["step"]:
                raise RuntimeError(f"Optimizer/pool checkpoint mismatch: {self.get_step} != {saved['step']}")
            for name in self.fields:
                setattr(self, name, saved["ghost"][name])
            self.pinned = saved["pinned"]
            self.rng.set_state(saved["pool_rng"])
            self.controller._queue.clear()
            self.controller._queue.extend(saved["controller_queue"])
            self.controller._learning_team = saved["controller_team"]
            self.controller._changed_training_team = False
            np.random.set_state(saved["numpy_rng"])
            random.setstate(saved["python_rng"])
            torch.set_rng_state(saved["torch_rng"])
            if torch.cuda.is_available() and saved["cuda_rng"] is not None:
                torch.cuda.set_rng_state_all(saved["cuda_rng"])
            for behavior_id, weights in saved["deployed"].items():
                self.policies[behavior_id].load_weights(weights)
            for team, queues in self._team_to_name_to_policy_queue.items():
                for brain, queue in queues.items():
                    queue.put(self.get_policy(create_name_behavior_id(brain, team)))
            self.pending_restore = None
            self._audit("restored", learning_team=self._learning_team, ghost_step=self.ghost_step,
                        last_save=self.last_save, last_swap=self.last_swap,
                        last_team_change=self.last_team_change, pinned=[snapshot_sha(p) for p in self.pinned])
        before = self.get_step
        learning_team = self._learning_team
        ghost_before = self.ghost_step
        super().advance()
        if self.get_step > before:
            self._audit("learning-exposure", start_step=before, end_step=self.get_step,
                        learning_team=learning_team, next_learning_team=self._learning_team,
                        learning_transitions=self.get_step-before, ghost_transitions=self.ghost_step-ghost_before)

    def save_model(self):
        super().save_model()
        # Also covers explicit safe-stop paths; periodic checkpoints use the hook above.
        self._save_pool()

    def _save_pool(self):
        saved = dict(schema="MNG-OBS-v2-244", step=self.get_step,
                     generation=os.environ.get('MNG_EXPERIMENT_GENERATION', 'v2'),
                     baseSnapshotSha=os.environ.get('MNG_V3_INITIAL_SNAPSHOT_SHA'),
                     ghost={name: copy.deepcopy(getattr(self, name)) for name in self.fields},
                     pinned=self.pinned, pool_rng=self.rng.get_state(),
                     controller_queue=list(self.controller._queue), controller_team=self.controller._learning_team,
                     numpy_rng=np.random.get_state(), python_rng=random.getstate(), torch_rng=torch.get_rng_state(),
                     cuda_rng=torch.cuda.get_rng_state_all() if torch.cuda.is_available() else None,
                     deployed={key: value.get_weights() for key, value in self.policies.items()})
        validate_v3_pool(saved=saved)
        self.sidecar.parent.mkdir(parents=True, exist_ok=True)
        temporary = self.sidecar.with_suffix(".tmp")
        torch.save(saved, temporary)
        os.replace(temporary, self.sidecar)
        archive = self.sidecar.with_name(f"mng-v2-pool-{self.get_step}.pt")
        if not archive.exists():
            archive.write_bytes(self.sidecar.read_bytes())
        self._audit("saved", pool_sha=hashlib.sha256(self.sidecar.read_bytes()).hexdigest(),
                    learning_team=self._learning_team, ghost_step=self.ghost_step,
                    last_save=self.last_save, last_swap=self.last_swap, last_team_change=self.last_team_change,
                    pinned=[snapshot_sha(p) for p in self.pinned])


def install_ppo_metrics():
    """Observe the existing PPO loss inputs without changing loss or gradients."""
    from mlagents.trainers.ppo.optimizer_torch import TorchPPOOptimizer
    from mlagents.trainers.torch_entities.utils import ModelUtils
    original_update = TorchPPOOptimizer.update
    def update_with_metrics(self, *args, **kwargs):
        metrics = {}
        original_loss = ModelUtils.trust_region_policy_loss
        def measured_loss(advantages, log_probs, old_log_probs, loss_masks, epsilon):
            with torch.no_grad():
                log_ratio = log_probs - old_log_probs
                ratio = torch.exp(log_ratio)
                metrics["Policy/Approx KL"] = ModelUtils.masked_mean(
                    (ratio - 1) - log_ratio, loss_masks).item()
                metrics["Policy/Clip Fraction"] = ModelUtils.masked_mean(
                    (torch.abs(ratio - 1) > epsilon).float(), loss_masks).item()
            return original_loss(advantages, log_probs, old_log_probs, loss_masks, epsilon)
        ModelUtils.trust_region_policy_loss = staticmethod(measured_loss)
        try:
            result = original_update(self, *args, **kwargs)
        finally:
            ModelUtils.trust_region_policy_loss = staticmethod(original_loss)
        result.update(metrics)
        return result
    TorchPPOOptimizer.update = update_with_metrics


def main():
    install_ppo_metrics()
    import mlagents.trainers.trainer.trainer_factory as factory
    factory.GhostTrainer = MNGV2GhostTrainer
    from mlagents.trainers.learn import main as learn
    learn()


if __name__ == "__main__":
    main()

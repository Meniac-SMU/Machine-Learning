"""P9 stock PPO/self-play with audited initial export, resumable pool and safe stops.

No installed package, legacy experiment, reward, observation or action is modified.
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
import mlagents.trainers.trainer
from mlagents.trainers.ghost.trainer import GhostTrainer
from mlagents.trainers.behavior_id_utils import create_name_behavior_id
from mng_player_log_guard import PlayerLogGuard

BEHAVIOR = "MNG5_Manager_v2"
CONTRACT = "MNG5-OBS676-A8-R1-M2-v2"
ROOT = Path(__file__).resolve().parent.parent
UPDATES = 0


def write_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(".tmp")
    temporary.write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding="utf-8")
    for attempt in range(8):
        try:
            os.replace(temporary, path)
            return
        except PermissionError:
            if attempt == 7:
                raise
            time.sleep(.05)


def append(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("a", encoding="utf-8") as stream:
        stream.write(json.dumps(value, ensure_ascii=False) + "\n")


def snapshot_sha(weights):
    h = hashlib.sha256()
    for module, values in sorted(weights.items()):
        h.update(module.encode())
        for name, value in sorted(values.items()):
            if not torch.isfinite(value).all():
                raise ValueError("Non-finite policy weights")
            h.update(name.encode()); h.update(value.detach().cpu().contiguous().numpy().tobytes())
    return h.hexdigest()


class FiveGhostTrainer(GhostTrainer):
    fields = ("ghost_step", "snapshot_counter", "_learning_team", "wrapped_trainer_team", "last_save", "last_swap",
              "last_team_change", "policy_elos", "current_opponent", "policy_snapshots", "current_policy_snapshot")

    def __init__(self, *args, **kwargs):
        super().__init__(*args, **kwargs)
        if self.brain_name != BEHAVIOR:
            raise ValueError("P9 cannot load a legacy behavior")
        self.sidecar = Path(self.trainer.artifact_path) / "five-pool.pt"
        self.audit = self.sidecar.with_suffix(".jsonl")
        self.pending_restore = None
        self.pending_save = False
        if "--resume" in sys.argv:
            self.pending_restore = torch.load(self.sidecar, map_location="cpu")
            if self.pending_restore["contract"] != CONTRACT:
                raise ValueError("Opponent pool contract mismatch")
        checkpoint = self.trainer._checkpoint
        def with_pool():
            result = checkpoint(); self.pending_save = True; return result
        self.trainer._checkpoint = with_pool

    def advance(self):
        global UPDATES
        if self.pending_restore is not None:
            saved = self.pending_restore
            if self.get_step != saved["step"]:
                raise ValueError("Policy/optimizer/pool steps do not match")
            for name in self.fields:
                setattr(self, name, saved["ghost"][name])
            self.controller._queue.clear(); self.controller._queue.extend(saved["controller_queue"])
            self.controller._learning_team = saved["controller_team"]
            self.controller._changed_training_team = False
            np.random.set_state(saved["numpy_rng"]); random.setstate(saved["python_rng"])
            torch.set_rng_state(saved["torch_rng"])
            if torch.cuda.is_available() and saved["cuda_rng"] is not None:
                torch.cuda.set_rng_state_all(saved["cuda_rng"])
            for key, weights in saved["deployed"].items():
                self.policies[key].load_weights(weights)
            for team, queues in self._team_to_name_to_policy_queue.items():
                for brain, queue in queues.items():
                    queue.put(self.get_policy(create_name_behavior_id(brain, team)))
            UPDATES = saved["optimizerUpdates"]
            self.pending_restore = None
            append(self.audit, dict(event="restored", step=self.get_step, optimizerUpdates=UPDATES,
                                    policySha=snapshot_sha(self.current_policy_snapshot)))
        before, team, ghost = self.get_step, self._learning_team, self.ghost_step
        result = super().advance()
        if self.pending_save:
            self.save_pool(); self.pending_save = False
        if self.get_step > before:
            append(self.audit, dict(event="learning-exposure", start=before, end=self.get_step, team=team,
                                    learnerTransitions=self.get_step-before, opponentTransitions=self.ghost_step-ghost))
        return result

    def _swap_snapshots(self):
        super()._swap_snapshots()
        append(self.audit, dict(event="opponent-swap", step=self.get_step, team=self._learning_team,
                                opponent=self.current_opponent, poolCount=len(self.policy_snapshots),
                                deployed={key:snapshot_sha({BEHAVIOR:policy.get_weights()}) for key,policy in self.policies.items()}))

    def save_model(self):
        super().save_model(); self.save_pool()

    def save_pool(self):
        saved = dict(contract=CONTRACT, step=self.get_step, optimizerUpdates=UPDATES,
                     ghost={name:copy.deepcopy(getattr(self,name)) for name in self.fields},
                     controller_queue=list(self.controller._queue), controller_team=self.controller._learning_team,
                     numpy_rng=np.random.get_state(), python_rng=random.getstate(), torch_rng=torch.get_rng_state(),
                     cuda_rng=torch.cuda.get_rng_state_all() if torch.cuda.is_available() else None,
                     deployed={key:policy.get_weights() for key,policy in self.policies.items()})
        self.sidecar.parent.mkdir(parents=True, exist_ok=True)
        temporary = self.sidecar.with_suffix(".tmp"); torch.save(saved, temporary); os.replace(temporary,self.sidecar)
        archive = self.sidecar.with_name(f"five-pool-{self.get_step}.pt")
        if not archive.exists(): archive.write_bytes(self.sidecar.read_bytes())
        append(self.audit,dict(event="saved",step=self.get_step,optimizerUpdates=UPDATES,
                              sha256=hashlib.sha256(self.sidecar.read_bytes()).hexdigest()))


def main():
    global UPDATES
    if any(arg == "--force" or arg.startswith("--initialize-from") for arg in sys.argv):
        raise ValueError("P9 forbids overwriting runs or importing another lineage")
    evidence = Path(os.environ["FIVE_EVIDENCE"]).resolve()
    if not evidence.is_relative_to(ROOT): raise ValueError("Evidence must stay in project")
    evidence.mkdir(parents=True,exist_ok=True)
    stop_at = int(os.environ["FIVE_STOP_AT"])
    guard = PlayerLogGuard(os.environ["FIVE_PLAYER_LOGS"])
    torch.set_num_threads(1)
    from mlagents.trainers.ppo.optimizer_torch import TorchPPOOptimizer
    from mlagents.trainers.torch_entities.utils import ModelUtils
    from mlagents.trainers.ppo.trainer import PPOTrainer
    from mlagents.trainers.trainer_controller import TrainerController
    import mlagents.trainers.trainer.trainer_factory as factory
    import mlagents.trainers.learn as learning
    original_factory = learning.create_environment_factory
    def environment_factory(*args, **kwargs):
        import inspect
        bound = inspect.signature(original_factory).bind(*args, **kwargs)
        bound.arguments["log_folder"] = os.environ["FIVE_PLAYER_LOGS"]
        return original_factory(*bound.args, **bound.kwargs)
    learning.create_environment_factory = environment_factory
    from mlagents.trainers.model_saver.torch_model_saver import TorchModelSaver
    def strict_load(self, load_path, policy=None, reset_global_steps=False):
        if reset_global_steps or self.initialize_path is not None:
            raise ValueError("P9 only resumes the same run with its optimizer")
        # Deserialize on CPU first; do not reconstruct CUDA storages in the unpickler.
        saved = torch.load(load_path, map_location="cpu")
        modules = self.modules if policy is None else policy.get_modules()
        for name, module in modules.items():
            if isinstance(module, torch.nn.Module):
                module.load_state_dict(saved[name], strict=True)
            else:
                module.load_state_dict(saved[name])
        snapshot_sha({k:v for k,v in saved.items() if k in ("Policy", "Optimizer:critic")})
    TorchModelSaver._load_model = strict_load
    factory.GhostTrainer = FiveGhostTrainer
    original_update = TorchPPOOptimizer.update
    def update(self,*args,**kwargs):
        global UPDATES
        measured={}; original_loss=ModelUtils.trust_region_policy_loss
        def loss(advantages,log_probs,old_log_probs,loss_masks,epsilon):
            with torch.no_grad():
                log_ratio=log_probs-old_log_probs; ratio=torch.exp(log_ratio)
                measured["approxKL"]=ModelUtils.masked_mean((ratio-1)-log_ratio,loss_masks).item()
                measured["clipFraction"]=ModelUtils.masked_mean((torch.abs(ratio-1)>epsilon).float(),loss_masks).item()
            return original_loss(advantages,log_probs,old_log_probs,loss_masks,epsilon)
        ModelUtils.trust_region_policy_loss=staticmethod(loss)
        try: result=original_update(self,*args,**kwargs)
        finally: ModelUtils.trust_region_policy_loss=staticmethod(original_loss)
        UPDATES+=1
        numeric={k:float(v) for k,v in {**result,**measured}.items()}
        if not all(np.isfinite(v) for v in numeric.values()): raise ValueError("Non-finite PPO metric")
        append(evidence/"optimizer.jsonl",dict(update=UPDATES,utc=time.time(),metrics=numeric))
        return result
    TorchPPOOptimizer.update=update
    original_advance=PPOTrainer.advance; initialized=set()
    def advance(self):
        if id(self) not in initialized:
            initialized.add(id(self))
            if self.get_step==0 and "--resume" not in sys.argv:
                self.model_saver.save_checkpoint(self.brain_name,0)
                write_json(evidence/"initial.json",dict(step=0,optimizerUpdates=UPDATES,contract=CONTRACT))
            if stop_at==0: raise KeyboardInterrupt()
        return original_advance(self)
    PPOTrainer.advance=advance
    original_controller=TrainerController.advance; last=[0.0]; started=time.monotonic()
    def monitored(self,*args,**kwargs):
        result=original_controller(self,*args,**kwargs)
        steps={name:int(trainer.get_step) for name,trainer in self.trainers.items()}
        if time.monotonic()-last[0]>=5:
            failures=guard.scan()
            if failures:
                write_json(evidence/"player-failure.json",failures); (evidence/"STOP").write_text("Player failure")
            write_json(evidence/"progress.json",dict(steps=steps,stopAt=stop_at,optimizerUpdates=UPDATES,
                                                   elapsedSeconds=time.monotonic()-started,utc=time.time()))
            last[0]=time.monotonic()
        if any(step>=stop_at for step in steps.values()) or (evidence/"STOP").exists():
            write_json(evidence/"safe-stop.json",dict(steps=steps,optimizerUpdates=UPDATES,requestedStop=(evidence/"STOP").exists()))
            raise KeyboardInterrupt()
        return result
    TrainerController.advance=monitored
    learning.main()
    failures=guard.scan()
    write_json(evidence/"process-result.json",dict(optimizerUpdates=UPDATES,trainingStarted=UPDATES>0,
                                                  failures=failures,elapsedSeconds=time.monotonic()-started))
    if failures: raise RuntimeError("Player failure detected after save")


if __name__=="__main__": main()

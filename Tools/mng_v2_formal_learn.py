"""Audited formal entry point: initial checkpoint and explicit safe stop boundaries."""
import json
import os
from pathlib import Path
import time
import logging
import mng_v2_learn
from mng_player_log_guard import PlayerLogGuard


def write_progress(path, payload):
    """A Windows reader may briefly deny rename; telemetry must not stop PPO."""
    temporary = path.with_suffix(".tmp")
    temporary.write_text(json.dumps(payload))
    for attempt in range(4):
        try:
            os.replace(temporary, path)
            return True
        except PermissionError:
            if attempt < 3:
                time.sleep(0.05)
    logging.warning("Progress rename busy; retaining temporary snapshot until next update")
    return False


def main():
    from mlagents.trainers.ppo.trainer import PPOTrainer
    from mlagents.trainers.trainer_controller import TrainerController
    advance = PPOTrainer.advance
    initialized = set()
    def advance_with_initial(self):
        if id(self) not in initialized:
            initialized.add(id(self))
            if self.get_step == 0:
                self.model_saver.save_checkpoint(self.brain_name, 0)
        return advance(self)
    PPOTrainer.advance = advance_with_initial
    controller_advance = TrainerController.advance
    stop_at = int(os.environ["MNG_V2_STOP_AT"])
    evidence = Path(os.environ["MNG_V2_EVIDENCE"])
    guard = PlayerLogGuard(os.environ["MNG_V2_PLAYER_LOGS"])
    last_guard = [0.0]
    last = [0.0]
    def monitored(self, *args, **kwargs):
        result = controller_advance(self, *args, **kwargs)
        steps = {name: int(trainer.get_step) for name, trainer in self.trainers.items()}
        if time.monotonic() - last_guard[0] >= 1:
            failures = guard.scan()
            if failures:
                (evidence / "player-failure.json").write_text(json.dumps(dict(steps=steps, failures=failures), indent=2))
                (evidence / "STOP").write_text("Unity Player failure: save and stop; review player-failure.json")
            last_guard[0] = time.monotonic()
        if time.monotonic() - last[0] >= 10:
            path = evidence / "progress.json"
            write_progress(path, dict(steps=steps, stop_at=stop_at, utc=time.time()))
            last[0] = time.monotonic()
        if any(step >= stop_at for step in steps.values()) or (evidence / "STOP").exists():
            (evidence / f"safe-stop-{max(steps.values(), default=0)}.json").write_text(json.dumps(steps))
            raise KeyboardInterrupt()
        return result
    TrainerController.advance = monitored
    mng_v2_learn.main()
    failures = guard.scan()
    if failures:
        (evidence / "player-failure.json").write_text(json.dumps(dict(failures=failures, detectedAfterSave=True), indent=2))
        (evidence / "STOP").write_text("Unity Player failure: saved run blocked; review player-failure.json")


if __name__ == "__main__": main()

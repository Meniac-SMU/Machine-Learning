"""Audited formal entry point: initial checkpoint and explicit safe stop boundaries."""
import json
import os
from pathlib import Path
import time
import mng_v2_learn


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
    last = [0.0]
    def monitored(self, *args, **kwargs):
        result = controller_advance(self, *args, **kwargs)
        steps = {name: int(trainer.get_step) for name, trainer in self.trainers.items()}
        if time.monotonic() - last[0] >= 10:
            path = evidence / "progress.json"
            temporary = path.with_suffix(".tmp")
            temporary.write_text(json.dumps(dict(steps=steps, stop_at=stop_at, utc=time.time())))
            os.replace(temporary, path)
            last[0] = time.monotonic()
        if any(step >= stop_at for step in steps.values()) or (evidence / "STOP").exists():
            (evidence / f"safe-stop-{max(steps.values(), default=0)}.json").write_text(json.dumps(steps))
            raise KeyboardInterrupt()
        return result
    TrainerController.advance = monitored
    mng_v2_learn.main()


if __name__ == "__main__": main()

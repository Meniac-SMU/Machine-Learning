"""V8 entrypoint; preserve the historical trainer and use the current ABI/trace audit."""
import five_selfplay_train as trainer
from five_frozen_trace_guard import TraceGuard
trainer.BEHAVIOR = "MNG5_Manager_v5"
trainer.CONTRACT = "MNG5-OBS676-A8-R1-M2-v5-T2T7MAX2-COOLDOWN2-GK6"
trainer.TraceGuard = TraceGuard
if __name__ == "__main__":
    trainer.main()

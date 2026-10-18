"""Use the verified v6 trainer ABI with v11 runtime and execution-feature checks."""
import os
import five_v10_selfplay_train as trainer
from five_v11_trace_guard import TraceGuard
from five_v10_cuda_train import main

class V11TraceGuard(TraceGuard):
    def __init__(self, directory, expected_runtime):
        assert expected_runtime == 'tactics-v11-keeper-turn-first'
        super().__init__(directory, expected_runtime)

if __name__ == '__main__':
    assert os.environ['FIVE_EXPECTED_RUNTIME'] == 'tactics-v11-keeper-turn-first'
    trainer.TraceGuard = V11TraceGuard
    main()

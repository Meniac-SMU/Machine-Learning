"""Authorized CUDA continuation with actual PPO parameter-device evidence."""
import json
import os
from pathlib import Path
import torch
import five_v10_selfplay_train as trainer
from mlagents.trainers.ppo.optimizer_torch import TorchPPOOptimizer


def main():
    assert torch.cuda.is_available(), 'CUDA unavailable; no silent CPU fallback'
    original = TorchPPOOptimizer.update
    checked = set()

    def audited_update(self, *args, **kwargs):
        devices = {str(p.device) for p in self.policy.actor.parameters()}
        devices.update(str(p.device) for p in self.critic.parameters())
        assert devices and all(d.startswith('cuda:') for d in devices), devices
        result = original(self, *args, **kwargs)
        if id(self) not in checked:
            torch.cuda.synchronize()
            path = Path(os.environ['FIVE_EVIDENCE'])/'cuda-device.json'
            path.write_text(json.dumps(dict(passed=True, devices=sorted(devices),
                gpu=torch.cuda.get_device_name(0), torch=torch.__version__,
                cuda=torch.version.cuda, allocatedBytes=torch.cuda.memory_allocated(),
                optimizerUpdatesAtEntry=trainer.UPDATES),indent=2),encoding='utf-8')
            checked.add(id(self))
        return result

    TorchPPOOptimizer.update = audited_update
    trainer.main()


if __name__ == '__main__':
    main()

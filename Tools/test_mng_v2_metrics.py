"""Verify diagnostic interception leaves the PPO loss and gradients unchanged."""
import unittest
from unittest.mock import patch
import torch
import mng_v2_learn
from mlagents.trainers.ppo.optimizer_torch import TorchPPOOptimizer
from mlagents.trainers.torch_entities.utils import ModelUtils


class MetricsTests(unittest.TestCase):
    def test_loss_gradient_and_mask(self):
        original = ModelUtils.trust_region_policy_loss
        old = torch.zeros((3, 1))
        logs = torch.tensor([[0.0], [0.3], [3.0]], requires_grad=True)
        advantages = torch.tensor([1.0, -1.0, 2.0])
        masks = torch.tensor([True, True, False])
        baseline = original(advantages, logs, old, masks, .15)
        expected_gradient = torch.autograd.grad(baseline, logs)[0]
        def fake_update(self):
            loss = ModelUtils.trust_region_policy_loss(advantages, logs, old, masks, .15)
            self.gradient = torch.autograd.grad(loss, logs)[0]
            return {"loss": loss.item()}
        with patch.object(TorchPPOOptimizer, "update", fake_update):
            mng_v2_learn.install_ppo_metrics()
            optimizer = object.__new__(TorchPPOOptimizer)
            stats = optimizer.update()
        self.assertEqual(stats["loss"], baseline.item())
        self.assertTrue(torch.equal(optimizer.gradient, expected_gradient))
        self.assertAlmostEqual(stats["Policy/Clip Fraction"], .5)
        self.assertAlmostEqual(stats["Policy/Approx KL"], (torch.exp(torch.tensor(.3)).item()-1-.3)/2, places=6)
        self.assertIs(ModelUtils.trust_region_policy_loss, original)

    def test_exception_restores_loss_function(self):
        original = ModelUtils.trust_region_policy_loss
        def fail(self):
            raise RuntimeError("expected test failure")
        with patch.object(TorchPPOOptimizer, "update", fail):
            mng_v2_learn.install_ppo_metrics()
            with self.assertRaisesRegex(RuntimeError, "expected test failure"):
                object.__new__(TorchPPOOptimizer).update()
        self.assertIs(ModelUtils.trust_region_policy_loss, original)


if __name__ == "__main__":
    unittest.main()

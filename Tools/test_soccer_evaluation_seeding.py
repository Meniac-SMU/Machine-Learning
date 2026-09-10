import random
import unittest

import numpy as np
from mlagents.torch_utils import torch
from soccer_evaluation_seeding import seed_evaluation_sampling


class EvaluationSeedingTests(unittest.TestCase):
    def sample(self):
        return random.random(), np.random.rand(3).tolist(), torch.rand(3, device="cpu").tolist()

    def test_same_seed_reproduces_all_three_rngs_after_other_draws(self):
        seed_evaluation_sampling(17423)
        first = self.sample()
        self.sample()
        seed_evaluation_sampling(17423)
        self.assertEqual(first, self.sample())

    def test_different_seed_changes_draws(self):
        seed_evaluation_sampling(17423)
        first = self.sample()
        seed_evaluation_sampling(29471)
        self.assertNotEqual(first, self.sample())

    def test_invalid_seed_rejected(self):
        for seed in (-1, 2147483648, 1.5):
            with self.assertRaises(ValueError):
                seed_evaluation_sampling(seed)


if __name__ == "__main__":
    unittest.main()

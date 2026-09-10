"""Explicit stochastic-policy evaluation seeds, independent of model initialization."""
import random

import numpy as np
from mlagents.torch_utils import torch


def seed_evaluation_sampling(seed):
    if not isinstance(seed, int) or not 0 <= seed <= 2147483647:
        raise ValueError("Evaluation seed must be an integer in [0, 2147483647]")
    random.seed(seed)
    np.random.seed(seed)
    torch.manual_seed(seed)


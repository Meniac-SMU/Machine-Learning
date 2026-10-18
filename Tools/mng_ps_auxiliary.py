"""Train-only demonstration loss added to the existing PPO Adam step.

No rollout actions, rewards, masks, critic targets, or evaluation are replaced.
Sampling is a pure function of the persisted Adam update counter: no hidden RNG.
"""
import copy
import json
from pathlib import Path
import numpy as np
import torch

ROOT = Path(__file__).resolve().parent.parent
DATA = ROOT/'Logs/MNG-Rebuild/PS-20260929/data-v2/examples.jsonl'


def distribution(actor, obs, masks):
    encoded, _ = actor.network_body([obs])
    return actor.action_model._get_dists(encoded, masks).discrete[0]


def update_count(optimizer):
    values = [int(s['step']) for s in optimizer.state.values() if 'step' in s]
    assert not values or min(values) == max(values)
    return max(values, default=0)


class Auxiliary:
    def __init__(self, actor):
        rows = [json.loads(line) for line in DATA.read_text().splitlines()]
        rows = [r for r in rows if r['split'] == 'train']
        self.row_splits = {r['split'] for r in rows}
        device = next(actor.parameters()).device
        self.obs = torch.tensor(np.asarray([r['observation'] for r in rows]), dtype=torch.float32, device=device)
        self.masks = torch.tensor(np.asarray([r['valid'] for r in rows]), dtype=torch.float32, device=device)
        self.positive = np.array([i for i,r in enumerate(rows) if r['label'] != 'preserve'])
        self.negative = np.array([i for i,r in enumerate(rows) if r['label'] == 'preserve'])
        self.legal_negative = np.array([i for i in self.negative if rows[i]['valid'][1]])
        assert len(self.positive) >= 40 and len(self.legal_negative) >= 20
        assert self.masks[self.positive, 1].all()
        parent = copy.deepcopy(actor)
        state = torch.load(ROOT/'results/MNG_MS3V3-20260925-r002/MNG_ManagerV2/MNG_ManagerV2-2001834.pt', map_location=device, weights_only=False)
        parent.load_state_dict(state['Policy'], strict=True)
        with torch.no_grad():
            self.parents = distribution(parent, self.obs, self.masks).probs.detach()
        del parent, state

    def loss(self, actor, counter):
        rng = np.random.RandomState(929501 + counter)
        pos = rng.choice(self.positive, 128)
        neg = np.concatenate([rng.choice(self.legal_negative, 64), rng.choice(self.negative, 64)])
        positive_dist = distribution(actor, self.obs[pos], self.masks[pos])
        ce = -torch.log_softmax(positive_dist.logits, 1)[:, 1].mean()
        negative_dist = distribution(actor, self.obs[neg], self.masks[neg])
        ref = self.parents[neg]
        kl = (ref * (ref.clamp_min(1e-12).log() - torch.log_softmax(negative_dist.logits, 1))).sum(1).mean()
        loss = .1 * (ce + 2 * kl)
        assert torch.isfinite(loss)
        return loss, {'PS/Auxiliary CE': float(ce.detach()), 'PS/Auxiliary KL': float(kl.detach()),
                      'PS/Auxiliary Weight': .1, 'PS/Auxiliary Samples': 256}


def combined_update(owner, original_update, auxiliary, *args, **kwargs):
    original_step = owner.optimizer.step
    metrics = {}
    before = update_count(owner.optimizer)
    calls = 0
    def step(*step_args, **step_kwargs):
        nonlocal calls
        loss, values = auxiliary.loss(owner.policy.actor, before)
        loss.backward()  # Accumulate with PPO gradients before the SAME Adam step.
        metrics.update(values)
        calls += 1
        return original_step(*step_args, **step_kwargs)
    owner.optimizer.step = step
    try:
        result = original_update(owner, *args, **kwargs)
    finally:
        owner.optimizer.step = original_step
    assert calls == 1 and update_count(owner.optimizer) == before + 1
    result.update(metrics)
    return result


def install(log_path):
    from mlagents.trainers.ppo.optimizer_torch import TorchPPOOptimizer
    original = TorchPPOOptimizer.update
    def update(owner, *args, **kwargs):
        if not hasattr(owner, '_ps_auxiliary'):
            owner._ps_auxiliary = Auxiliary(owner.policy.actor)
        result = combined_update(owner, original, owner._ps_auxiliary, *args, **kwargs)
        with Path(log_path).open('a') as f:
            f.write(json.dumps(dict(step=owner.policy.get_current_step(), adamUpdate=update_count(owner.optimizer),
                                   metrics={k:v for k,v in result.items() if k.startswith('PS/')})) + '\n')
        return result
    TorchPPOOptimizer.update = update

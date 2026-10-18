import unittest
import copy
import numpy as np
import mng_psr_model as m
from mng_psr_train import logprob

class RecipientLearningTests(unittest.TestCase):
    def test_parent_head_is_exact_under_identical_masks(self):
        new=m.inherited();old=m.policy((6,));old.actor.load_state_dict(m.torch.load(m.PARENT,weights_only=False)['Policy'])
        rng=np.random.RandomState(129);obs=m.torch.tensor(rng.normal(size=(32,244)).astype(np.float32));mask=m.torch.ones((32,11))
        with m.torch.no_grad():
            a=m.dists(new.actor,obs,mask)[0].probs;b=m.dists(old.actor,obs,mask[:,:6])[0].probs
        self.assertTrue(m.torch.equal(a,b))

    def test_unused_recipient_has_no_ppo_gradient(self):
        net=m.inherited();obs=m.torch.zeros((8,244));mask=m.torch.ones((8,11))
        actions=m.torch.tensor([[4,i%5] for i in range(8)])
        (-logprob(m.dists(net.actor,obs,mask),actions).mean()).backward()
        branch=net.actor.action_model._discrete_distribution.branches[1]
        self.assertEqual(float(branch.weight.grad.abs().sum()),0.)
        self.assertEqual(float(branch.bias.grad.abs().sum()),0.)

    def test_joint_pass_loss_teaches_recipient_and_resumes_adam(self):
        net=m.inherited();obs=m.torch.zeros((8,244));mask=m.torch.ones((8,11));actions=m.torch.tensor([[1,1]]*8)
        optimizer=m.torch.optim.Adam(net.actor.parameters(),lr=1e-3)
        def update(actor,opt):
            opt.zero_grad();loss=-logprob(m.dists(actor,obs,mask),actions).mean();loss.backward();opt.step();return float(loss)
        before=update(net.actor,optimizer)
        actor_state=copy.deepcopy(net.actor.state_dict());opt_state=copy.deepcopy(optimizer.state_dict())
        after=update(net.actor,optimizer);self.assertLess(after,before)
        restored=m.policy();restored.actor.load_state_dict(actor_state);restored_optimizer=m.torch.optim.Adam(restored.actor.parameters(),lr=1e-3);restored_optimizer.load_state_dict(opt_state)
        self.assertEqual(after,update(restored.actor,restored_optimizer))
        self.assertTrue(all(m.torch.equal(v,restored.actor.state_dict()[k]) for k,v in net.actor.state_dict().items()))

if __name__=='__main__':unittest.main()

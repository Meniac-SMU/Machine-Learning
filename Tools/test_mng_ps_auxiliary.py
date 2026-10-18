import copy
from types import SimpleNamespace
import unittest
from mng_ps_bc import policy, PARENT
import torch
from mng_ps_auxiliary import Auxiliary, combined_update, distribution, update_count


class AuxiliaryTests(unittest.TestCase):
    def test_combined_gradient_and_exact_resume(self):
        actor=policy().actor
        actor.load_state_dict(torch.load(PARENT,map_location='cpu',weights_only=False)['Policy'],strict=True)
        aux=Auxiliary(actor)
        self.assertEqual(aux.row_splits,{'train'})
        critic=torch.nn.Linear(244,1)
        def owner(a,c):
            return SimpleNamespace(policy=SimpleNamespace(actor=a),critic=c,
                optimizer=torch.optim.Adam(list(a.parameters())+list(c.parameters()),lr=5e-5))
        one=owner(actor,critic)
        two=owner(copy.deepcopy(actor),copy.deepcopy(critic))
        obs=aux.obs[:32];masks=aux.masks[:32]
        def ppo_like(o):
            return distribution(o.policy.actor,obs,masks).probs[:,0].mean()+o.critic(obs).square().mean()
        def original(o):
            o.optimizer.zero_grad();ppo_like(o).backward();o.optimizer.step();return {}
        combined_update(one,original,aux)
        two.optimizer.zero_grad()
        expected=ppo_like(two)+aux.loss(two.policy.actor,0)[0]
        expected.backward();two.optimizer.step()
        for a,b in zip(one.policy.actor.parameters(),two.policy.actor.parameters()):
            self.assertTrue(torch.allclose(a,b,atol=1e-7,rtol=1e-6))
        for a,b in zip(one.critic.parameters(),two.critic.parameters()):
            self.assertTrue(torch.equal(a,b))
        self.assertEqual(update_count(one.optimizer),1)
        saved_actor=copy.deepcopy(one.policy.actor.state_dict())
        saved_critic=copy.deepcopy(one.critic.state_dict())
        saved_optimizer=copy.deepcopy(one.optimizer.state_dict())
        combined_update(one,original,aux)
        two.policy.actor.load_state_dict(saved_actor);two.critic.load_state_dict(saved_critic)
        two.optimizer.load_state_dict(saved_optimizer)
        combined_update(two,original,aux)
        for a,b in zip(one.policy.actor.parameters(),two.policy.actor.parameters()):self.assertTrue(torch.equal(a,b))
        for a,b in zip(one.critic.parameters(),two.critic.parameters()):self.assertTrue(torch.equal(a,b))


if __name__=='__main__':unittest.main()

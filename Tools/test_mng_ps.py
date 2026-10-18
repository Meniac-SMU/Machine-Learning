"""Meaningful invariants for the PS-only pipeline; no installed-package edits."""
import copy
import tempfile
import unittest
from pathlib import Path
import numpy as np
np.dot(np.zeros((2, 2)), np.zeros((2, 2)))
import torch
from mng_ps_bc import policy, distribution, save, PARENT, PARENT_SHA, sha
from mng_ps_teacher import context, SCALE
from mng_v2_evaluate import FrozenPolicy


class PSTests(unittest.TestCase):
    def setUp(self):
        torch.manual_seed(929)
        self.net=policy().actor

    def test_parent_identity_and_strict_load(self):
        self.assertEqual(sha(PARENT),PARENT_SHA)
        parent=torch.load(PARENT,map_location='cpu',weights_only=False)
        self.net.load_state_dict(parent['Policy'],strict=True)
        damaged=dict(parent['Policy']);damaged.pop(next(iter(damaged)))
        with self.assertRaises(RuntimeError): self.net.load_state_dict(damaged,strict=True)

    def test_actual_masks_and_gradient_finite(self):
        obs=torch.randn(6,244); masks=torch.eye(6)
        probs=distribution(self.net,obs,masks).probs
        self.assertTrue(torch.equal(probs,masks))
        loss=-torch.log_softmax(distribution(self.net,obs,masks).logits,1).diagonal().mean()
        loss.backward()
        self.assertTrue(all(torch.isfinite(p.grad).all() for p in self.net.parameters() if p.grad is not None))

    def test_parent_onnx_matches_real_actor(self):
        self.net.load_state_dict(torch.load(PARENT,map_location='cpu',weights_only=False)['Policy'],strict=True)
        f=FrozenPolicy(str(PARENT.with_suffix('.onnx')),929)
        o=np.zeros((1,244),np.float32);m=np.array([[1,1,0,1,1,1]],np.float32)
        f.act(o,m.astype(bool))
        with torch.no_grad():p=distribution(self.net,torch.tensor(o),torch.tensor(m)).probs.numpy()[0]
        np.testing.assert_allclose(p,f.last_probabilities,atol=2e-5)

    def test_adam_rng_resume_same_next_update(self):
        obs=torch.randn(32,244);masks=torch.ones(32,6)
        rng=np.random.RandomState(77)
        opt=torch.optim.Adam([p for p in self.net.parameters() if p.requires_grad],lr=1e-4)
        def step(net,optimizer,r):
            indexes=r.choice(32,16)
            loss=-torch.log_softmax(distribution(net,obs[indexes],masks[indexes]).logits,1)[:,1].mean()
            optimizer.zero_grad();loss.backward();optimizer.step()
        step(self.net,opt,rng)
        with tempfile.TemporaryDirectory(dir=str(PARENT.parents[2])) as tmp:
            path=Path(tmp)/'resume.pt';save(path,self.net,opt,rng,1,'test',[])
            step(self.net,opt,rng)
            restored=policy().actor;opt2=torch.optim.Adam([p for p in restored.parameters() if p.requires_grad],lr=1e-4)
            s=torch.load(path,map_location='cpu',weights_only=False)
            restored.load_state_dict(s['actor'],strict=True);opt2.load_state_dict(s['optimizer'])
            rng2=np.random.RandomState();rng2.set_state(s['numpy']);torch.set_rng_state(s['torch'])
            step(restored,opt2,rng2)
            for k,v in self.net.state_dict().items():self.assertTrue(torch.equal(v,restored.state_dict()[k]),k)

    def test_teacher_does_not_label_legal_pass_alone(self):
        o=np.zeros(244);o[101]=1;o[105]=1;o[126]=1
        players=o[:96].reshape(8,12)
        for i,xy in [(1,(0,0)),(2,(2,10)),(4,(35,30))]:
            players[i,0]=1;players[i,1:3]=np.array(xy)/SCALE
        c=context(o,np.ones(6,bool))
        self.assertFalse(c['beneficial'])
        players[4,1:3]=np.array([4,0])/SCALE
        self.assertTrue(context(o,np.ones(6,bool))['beneficial'])
        self.assertFalse(context(o,np.array([1,0,1,1,1,1],bool))['beneficial'])


if __name__=='__main__':unittest.main()

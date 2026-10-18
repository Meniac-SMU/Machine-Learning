"""PS retry actor: exact inherited 2M command network, new recipient branch."""
from pathlib import Path
import hashlib
import numpy as np
np.dot(np.zeros((2, 2)), np.zeros((2, 2)))
import torch
import mlagents.trainers.trainer
from mlagents.trainers.settings import NetworkSettings, TorchSettings
from mlagents.trainers.torch_entities.networks import SimpleActor, ValueNetwork
from mlagents.trainers.policy.torch_policy import TorchPolicy
from mlagents.trainers.torch_entities.model_serialization import ModelSerializer
from mlagents_envs.base_env import BehaviorSpec, ObservationSpec, ActionSpec, DimensionProperty, ObservationType
from mlagents.torch_utils import set_torch_config

ROOT = Path(__file__).resolve().parent.parent
PARENT = ROOT/'results/MNG_MS3V3-20260925-r002/MNG_ManagerV2/MNG_ManagerV2-2001834.pt'
EVIDENCE = ROOT/'Logs/MNG-Rebuild/PS-Retry-20260929'
RUN = 'MNG_MS3V3-20260929-r003'
RESULT = ROOT/'results'/RUN
SCALE = np.array([62.,42.32727])
set_torch_config(TorchSettings(device='cpu'))
torch.set_default_device('cpu')
torch.set_num_threads(1)

def sha(p): return hashlib.sha256(Path(p).read_bytes()).hexdigest()

def policy(branches=(6,5)):
    spec=BehaviorSpec([ObservationSpec((244,), (DimensionProperty.NONE,), ObservationType.DEFAULT,'vector')],ActionSpec(0,np.array(branches)))
    return TorchPolicy(929903,spec,NetworkSettings(normalize=False,hidden_units=128,num_layers=2),SimpleActor,{})

def inherited():
    assert sha(PARENT)=='889169f22546f2684f7a7d4525c6d8fe21dde275c267c524b0b8aa2b7dacb380'
    source=torch.load(PARENT,map_location='cpu',weights_only=False)
    net=policy(); state=net.actor.state_dict()
    for k,v in source['Policy'].items():
        if 'act_size_vector' in k: continue
        if k in state and state[k].shape==v.shape: state[k]=v.clone()
        elif 'act_size_vector' not in k: raise ValueError(k)
    # Default starts at the legacy receiver; new explicit recipients are learnable.
    state['action_model._discrete_distribution.branches.1.weight'].zero_()
    state['action_model._discrete_distribution.branches.1.bias'].fill_(-2.)
    state['action_model._discrete_distribution.branches.1.bias'][0]=0
    net.actor.load_state_dict(state,strict=True)
    for k,v in source['Policy'].items():
        if 'network_body' in k or 'branches.0' in k: assert torch.equal(net.actor.state_dict()[k],v)
    return net

def dists(actor, obs, mask):
    z,_=actor.network_body([obs]);return actor.action_model._get_dists(z,mask).discrete

def parent_mask(obs, mask):
    m=mask[:,:6].copy()
    # The runtime observation retains the original resolver's target one-hot.
    m[:,1] &= (obs[:,123]<.5)
    return m

def contexts(obs, mask):
    """Geometric teaching labels, NOT claims of observed successful passes."""
    rows=[]
    for o,m in zip(obs,mask):
        r=dict(action=-1,recipient=0,kind='preserve',quality=0.)
        if o[243]>.5 or o[101]<.5 or not m[1]: rows.append(r);continue
        c=int(np.argmax(o[103:112]))-1
        if c not in range(4):rows.append(r);continue
        p=o[:96].reshape(8,12);origin=p[c,1:3]*SCALE
        opp=p[4:][p[4:,0]>.5,1:3]*SCALE
        if len(opp)==0:rows.append(r);continue
        pressure=float(np.linalg.norm(opp-origin,axis=1).min())
        goal=np.array([62.,0.])
        def shot_lane(point):
            d=goal-point;length2=float(d@d)
            t=np.clip((opp-point)@d/max(length2,1e-9),0,1)
            return float(np.linalg.norm(opp-point-t[:,None]*d,axis=1).min())
        original_goal_distance=float(np.linalg.norm(goal-origin));original_shot_lane=shot_lane(origin)
        offset=opp-origin;blocked=bool(np.any((offset[:,0]>=.5)&(offset[:,0]<=8)&(abs(offset[:,1])<=3)))
        best=None
        for slot in range(4):
            if not m[7+slot]:continue
            target=p[slot,1:3]*SCALE+.2*p[slot,3:5]*9
            delta=target-origin;distance=float(np.linalg.norm(delta))
            t=np.clip((opp-origin)@delta/max(distance**2,1e-9),0,1)
            lane=float(np.linalg.norm(opp-origin-t[:,None]*delta,axis=1).min())
            space=float(np.linalg.norm(opp-target,axis=1).min())
            progress=float(delta[0]); gain=progress/9-distance/22
            escape=(pressure<=7 or blocked) and space>=pressure+2 and space>=4 and lane>=2
            fast=progress>=7 and gain>=.15 and space>=4 and lane>=2
            receiver_goal_distance=float(np.linalg.norm(goal-target));receiver_shot_lane=shot_lane(target)
            finish=original_goal_distance<=24 and receiver_goal_distance<=16 and space>=4 and lane>=2 and (
                receiver_shot_lane>=original_shot_lane+1 or
                (receiver_goal_distance<=original_goal_distance-3 and receiver_shot_lane>=original_shot_lane-.5))
            # Recommendations can reject a slow risky option; the action mask does
            # not. Near-goal passes compete on the recipient's finishing position.
            if not (escape or fast or finish) or distance/30>1.5:continue
            quality=(min(space-pressure,10) if escape else 0)+max(gain,0)*3+min(lane,6)*.4-distance*.035+(3 if finish else 0)
            kind='keeper-escape' if escape and slot==0 else 'finish-link' if finish else 'pressure-escape' if escape else 'fast-advance'
            candidate=dict(action=1,recipient=slot+1,kind=kind,quality=quality,carrier=c,
                           pressure=pressure,receiverSpace=space,lane=lane,progress=progress)
            if best is None or quality>best['quality']:best=candidate
        rows.append(best or r)
    return rows

def export(net,path):
    ModelSerializer(net).export_policy_model(str(Path(path).with_suffix('')))

class Critic(torch.nn.Module):
    def __init__(self):
        super().__init__();self.layers=torch.nn.Sequential(torch.nn.Linear(244,128),torch.nn.SiLU(),torch.nn.Linear(128,128),torch.nn.SiLU(),torch.nn.Linear(128,1))
    def forward(self,x):return self.layers(x).squeeze(-1)

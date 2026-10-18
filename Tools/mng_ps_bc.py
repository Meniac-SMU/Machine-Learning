"""Masked actor-only BC with independent counters and exactly resumable Adam/RNG."""
import argparse
import copy
import hashlib
import json
from pathlib import Path
import numpy as np
# Initialize NumPy's BLAS before Torch on this Windows/Conda installation.
# This reuses one OpenMP runtime; never set KMP_DUPLICATE_LIB_OK.
np.dot(np.zeros((2, 2)), np.zeros((2, 2)))
import torch
import mlagents.trainers.trainer
from mlagents.trainers.settings import NetworkSettings
from mlagents.trainers.torch_entities.networks import SimpleActor
from mlagents.trainers.policy.torch_policy import TorchPolicy
from mlagents.trainers.torch_entities.model_serialization import ModelSerializer
from mlagents_envs.base_env import BehaviorSpec, ObservationSpec, ActionSpec, DimensionProperty, ObservationType
from mng_v2_evaluate import FrozenPolicy

ROOT=Path(__file__).resolve().parent.parent
PARENT=ROOT/'results/MNG_MS3V3-20260925-r002/MNG_ManagerV2/MNG_ManagerV2-2001834.pt'
PARENT_SHA='889169f22546f2684f7a7d4525c6d8fe21dde275c267c524b0b8aa2b7dacb380'
from mlagents.torch_utils import set_torch_config
from mlagents.trainers.settings import TorchSettings
set_torch_config(TorchSettings(device='cpu'))
torch.set_default_device('cpu')
torch.set_num_threads(1)


def sha(path): return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def policy():
    spec=BehaviorSpec([ObservationSpec((244,), (DimensionProperty.NONE,), ObservationType.DEFAULT, 'vector')], ActionSpec(0,np.array([6])))
    return TorchPolicy(929, spec, NetworkSettings(normalize=False,hidden_units=128,num_layers=2),SimpleActor,{})


def distribution(actor, obs, masks):
    encoded,_=actor.network_body([obs])
    return actor.action_model._get_dists(encoded,masks).discrete[0]


def metrics(actor, obs, masks, parents, positive, selected):
    with torch.no_grad():
        probs=distribution(actor,obs[selected],masks[selected]).probs
        parent=parents[selected]; pos=positive[selected]
        kl=(parent*(parent.clamp_min(1e-12).log()-probs.clamp_min(1e-12).log())).sum(1)
        neg=~pos
        legal=neg & masks[selected,1].bool()
        return dict(n=int(len(selected)), positives=int(pos.sum()), legalUnnecessary=int(legal.sum()),
            positivePass=float(probs[pos,1].mean()) if pos.any() else None,
            unnecessaryPass=float(probs[legal,1].mean()) if legal.any() else None,
            preservationKL=float(kl[neg].mean()) if neg.any() else None,
            illegalProbabilityMax=float((probs*(1-masks[selected])).max()))


def save(path, actor, optimizer, rng, update, dataset_sha, history):
    state=dict(actor=actor.state_dict(),optimizer=optimizer.state_dict(),numpy=rng.get_state(),
               torch=torch.get_rng_state(),update=update,datasetSha=dataset_sha,history=history)
    torch.save(state,path)


def main():
    p=argparse.ArgumentParser(); p.add_argument('--data',required=True); p.add_argument('--output',required=True)
    p.add_argument('--updates',type=int,default=3000); p.add_argument('--resume',action='store_true')
    a=p.parse_args(); out=Path(a.output); out.mkdir(parents=True,exist_ok=a.resume)
    source=Path(a.data); data_sha=sha(source)
    assert sha(PARENT)==PARENT_SHA
    rows=[json.loads(l) for l in source.read_text().splitlines()]
    # Final holdout is deliberately not loaded into model selection or training tensors.
    rows=[r for r in rows if r['split']!='holdout']
    obs=torch.tensor([r['observation'] for r in rows],dtype=torch.float32)
    masks=torch.tensor([r['valid'] for r in rows],dtype=torch.float32)
    assert obs.shape[1]==244 and masks.shape[1]==6 and torch.isfinite(obs).all() and (masks.sum(1)>0).all()
    positive=torch.tensor([r['label']!='preserve' for r in rows])
    assert masks[positive,1].all()
    train=np.array([i for i,r in enumerate(rows) if r['split']=='train'])
    val=np.array([i for i,r in enumerate(rows) if r['split']=='validation'])
    pressure=np.array([i for i in train if rows[i]['label']=='pressure-escape'])
    positives=np.array([i for i in train if positive[i]])
    negatives=np.array([i for i in train if not positive[i]])
    legal_negatives=np.array([i for i in negatives if masks[i,1]>.5])
    assert len(legal_negatives)>=20, 'Need legal but unnecessary pass examples'
    assert len(pressure)>=20 and len(positives)>=40 and len(val)>0, 'Insufficient independent training examples'
    assert int(positive[val].sum())>=10, 'Insufficient validation examples'
    parent=torch.load(PARENT,map_location='cpu',weights_only=False)
    net=policy(); net.actor.load_state_dict(parent['Policy'],strict=True)
    with torch.no_grad(): parents=distribution(net.actor,obs,masks).probs.detach().clone()
    np.savez_compressed(out/'parent-probabilities.npz',probabilities=parents.cpu().numpy(),
        seed=np.array([r['seed'] for r in rows]),team=np.array([r['team'] for r in rows]),
        episode=np.array([r['episode'] for r in rows]),tick=np.array([r['tick'] for r in rows]))
    frozen=FrozenPolicy(str(PARENT.with_suffix('.onnx')),929)
    errors=[]
    for i in np.linspace(0,len(obs)-1,64,dtype=int):
        frozen.act(obs[i:i+1].numpy(),masks[i:i+1].numpy().astype(bool))
        errors.append(float(np.max(abs(frozen.last_probabilities-parents[i].numpy()))))
    assert max(errors)<2e-5, ('Parent ONNX mismatch',max(errors))
    optimizer=torch.optim.Adam([x for x in net.actor.parameters() if x.requires_grad],lr=1e-4)
    rng=np.random.RandomState(929301); start=0; history=[]; best=None; best_score=-float('inf');best_feasible=False
    if a.resume:
        state=torch.load(out/'resume.pt',map_location='cpu',weights_only=False)
        assert state['datasetSha']==data_sha
        net.actor.load_state_dict(state['actor'],strict=True); optimizer.load_state_dict(state['optimizer'])
        rng.set_state(state['numpy']); torch.set_rng_state(state['torch']); start=state['update']; history=state['history']
        if (out/'best.pt').exists():
            prior=torch.load(out/'best.pt',map_location='cpu',weights_only=False);best=prior['actor'];best_score=prior['score'];best_feasible=prior.get('feasible',False)
    initial=metrics(net.actor,obs,masks,parents,positive,val)
    (out/'bc-source.py').write_bytes(Path(__file__).read_bytes())
    (out/'initial.json').write_text(json.dumps(dict(parentOnnxMaxError=max(errors),validation=initial,datasetSha=data_sha),indent=2))
    for update in range(start+1,a.updates+1):
        pos_pool=pressure if update<=500 else positives
        pos=rng.choice(pos_pool,128)
        neg=np.concatenate([rng.choice(legal_negatives,64),rng.choice(negatives,64)])
        d=distribution(net.actor,obs[pos],masks[pos])
        ce=-torch.log_softmax(d.logits,dim=1)[:,1].mean()
        dneg=distribution(net.actor,obs[neg],masks[neg]); ref=parents[neg]
        kl=(ref*(ref.clamp_min(1e-12).log()-torch.log_softmax(dneg.logits,dim=1))).sum(1).mean()
        loss=ce+2*kl
        assert torch.isfinite(loss)
        optimizer.zero_grad();loss.backward();torch.nn.utils.clip_grad_norm_(net.actor.parameters(),1.);optimizer.step()
        assert all(torch.isfinite(v).all() for v in net.actor.state_dict().values())
        if update%100==0:
            m=metrics(net.actor,obs,masks,parents,positive,val)
            row=dict(update=update,phase='PS2-pressure' if update<=500 else 'PS2-mixed',loss=float(loss),validation=m)
            history.append(row);print(json.dumps(row),flush=True)
            score=m['positivePass']-2*m['preservationKL']-m['unnecessaryPass']
            feasible=(m['positivePass']>=.2 and m['unnecessaryPass']<=.15 and
                      m['positivePass']-m['unnecessaryPass']>=.1 and m['preservationKL']<=.15)
            if update>=500 and ((feasible and not best_feasible) or (feasible==best_feasible and score>best_score)):
                best=copy.deepcopy(net.actor.state_dict());best_score=score;best_feasible=feasible
                torch.save(dict(actor=best,score=score,update=update,metrics=m,feasible=feasible),out/'best.pt')
            save(out/'resume.pt',net.actor,optimizer,rng,update,data_sha,history)
            if update in (500,1500):
                save(out/f'PS2-update-{update}.pt',net.actor,optimizer,rng,update,data_sha,history)
                ModelSerializer(net).export_policy_model(str(out/f'PS2-update-{update}'))
            (out/'history.json').write_text(json.dumps(history,indent=2))
            if (out/'STOP').exists():
                (out/'stopped.json').write_text(json.dumps(dict(update=update,status='saved-not-complete')))
                return
    assert best is not None
    net.actor.load_state_dict(best,strict=True)
    final=metrics(net.actor,obs,masks,parents,positive,val)
    candidate=copy.deepcopy(parent);candidate['Policy']=copy.deepcopy(best)
    assert all(torch.equal(candidate['Optimizer:critic'][k],v) for k,v in parent['Optimizer:critic'].items())
    torch.save(candidate,out/'bc-parent-step.pt')
    ModelSerializer(net).export_policy_model(str(out/'bc-policy'))
    exported=FrozenPolicy(str(out/'bc-policy.onnx'),929)
    with torch.no_grad(): expected=distribution(net.actor,obs[:64],masks[:64]).probs.numpy()
    errors=[]
    for i in range(min(64,len(obs))):
        exported.act(obs[i:i+1].numpy(),masks[i:i+1].numpy().astype(bool))
        errors.append(float(np.max(abs(expected[i]-exported.last_probabilities))))
    assert max(errors)<2e-5
    passed=(final['positivePass']>=.2 and final['unnecessaryPass']<=.15 and
            final['positivePass']-final['unnecessaryPass']>=.1 and final['preservationKL']<=.15)
    (out/'report.json').write_text(json.dumps(dict(passed=passed,validation=final,parentUnchanged=sha(PARENT)==PARENT_SHA,
        criticUnchanged=True,additionalEnvironmentSteps=0,updates=a.updates,processedTrainingSamples=a.updates*256,
        onnxMaxError=max(errors),candidateSha=sha(out/'bc-parent-step.pt'),datasetSha=data_sha,
        gate='PPO entry only: positive>=.2, unnecessary<=.15, gap>=.1, KL<=.15',
        scope='Offline validation only. No gameplay success claim.'),indent=2))


if __name__=='__main__': main()

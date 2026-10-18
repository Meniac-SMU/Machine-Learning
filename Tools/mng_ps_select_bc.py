"""Select an already saved feasible BC checkpoint; no additional optimizer updates."""
import argparse
import copy
import json
from pathlib import Path
import numpy as np
np.dot(np.zeros((2,2)),np.zeros((2,2)))
import torch
from mng_ps_bc import policy,distribution,metrics,sha,PARENT,PARENT_SHA,ModelSerializer,FrozenPolicy


def main():
    p=argparse.ArgumentParser();p.add_argument('--state',required=True);p.add_argument('--data',required=True);p.add_argument('--output',required=True)
    a=p.parse_args();out=Path(a.output);out.mkdir(parents=True,exist_ok=False)
    s=torch.load(a.state,map_location='cpu',weights_only=False);assert s['datasetSha']==sha(a.data)
    rows=[json.loads(l) for l in Path(a.data).read_text().splitlines()];rows=[r for r in rows if r['split']=='validation']
    obs=torch.tensor([r['observation'] for r in rows]);masks=torch.tensor([r['valid'] for r in rows],dtype=torch.float32)
    positive=torch.tensor([r['label']!='preserve' for r in rows]);indices=np.arange(len(rows))
    parent=torch.load(PARENT,map_location='cpu',weights_only=False);net=policy();net.actor.load_state_dict(parent['Policy'],strict=True)
    with torch.no_grad():ref=distribution(net.actor,obs,masks).probs.detach()
    net.actor.load_state_dict(s['actor'],strict=True)
    m=metrics(net.actor,obs,masks,ref,positive,indices)
    passed=m['positivePass']>=.2 and m['unnecessaryPass']<=.15 and m['positivePass']-m['unnecessaryPass']>=.1 and m['preservationKL']<=.15
    assert passed,m
    candidate=copy.deepcopy(parent);candidate['Policy']=s['actor'];torch.save(candidate,out/'bc-parent-step.pt')
    ModelSerializer(net).export_policy_model(str(out/'bc-policy'))
    f=FrozenPolicy(str(out/'bc-policy.onnx'),929)
    with torch.no_grad():probs=distribution(net.actor,obs[:64],masks[:64]).probs.numpy()
    errors=[]
    for i in range(64):
        f.act(obs[i:i+1].numpy(),masks[i:i+1].numpy().astype(bool))
        errors.append(float(np.max(abs(probs[i]-f.last_probabilities))))
    assert max(errors)<2e-5 and sha(PARENT)==PARENT_SHA
    report=dict(passed=True,validation=m,selectedUpdate=s['update'],updates=s['update'],
        selection='Feasibility gate first; chose saved final state. Earlier unconstrained score selected an infeasible actor.',
        sourceState=str(Path(a.state).resolve()),sourceStateSha=sha(a.state),datasetSha=s['datasetSha'],
        candidateSha=sha(out/'bc-parent-step.pt'),parentUnchanged=True,criticUnchanged=True,
        additionalEnvironmentSteps=0,selectionOptimizerUpdates=0,onnxMaxError=max(errors),
        scope='PPO entry only; gameplay and holdout not yet evaluated')
    (out/'report.json').write_text(json.dumps(report,indent=2));print(json.dumps(report,indent=2))
    (out/'selection-source.py').write_bytes(Path(__file__).read_bytes())


if __name__=='__main__':main()

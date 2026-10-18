import argparse,json
from pathlib import Path
import mng_psr_model as m
import onnx
from onnx.reference import ReferenceEvaluator
p=argparse.ArgumentParser();p.add_argument('checkpoint');p.add_argument('output');a=p.parse_args()
checkpoint=Path(a.checkpoint);model_path=checkpoint.with_suffix('.onnx')
state=m.torch.load(checkpoint,map_location='cpu',weights_only=False);net=m.policy();net.actor.load_state_dict(state['Policy']);net.actor.eval()
model=onnx.load(str(model_path));nodes=[n for n in model.graph.node if n.op_type=='Softmax'];assert len(nodes)==2
for n,size in zip(nodes,(6,5)):model.graph.output.append(onnx.helper.make_tensor_value_info(n.output[0],onnx.TensorProto.FLOAT,[None,size]))
model=onnx.utils.Extractor(model).extract_model([i.name for i in model.graph.input],[n.output[0] for n in nodes])
session=ReferenceEvaluator(model)
rows=[json.loads(x) for x in (m.EVIDENCE/'counterfactual-backheel-combined.jsonl').read_text().splitlines() if x]
maximum=0.;tested=0
for r in rows:
 if not r.get('anchor'):continue
 obs=m.np.array([r['anchor']['observation']],dtype=m.np.float32);mask=m.np.array([r['anchor']['mask']],dtype=m.np.float32)
 feed={i.name:obs if i.type.tensor_type.shape.dim[-1].dim_value==244 else mask for i in model.graph.input};assert len(feed)==2
 actual=session.run([n.output[0] for n in nodes],feed)
 with m.torch.no_grad():expected=[d.probs.numpy() for d in m.dists(net.actor,m.torch.as_tensor(obs),m.torch.as_tensor(mask))]
 for got,want in zip(actual,expected):maximum=max(maximum,float(abs(got-want).max()));m.np.testing.assert_allclose(got,want,atol=1e-6,rtol=1e-6)
 tested+=1
 if tested==32:break
parent=m.torch.load(m.PARENT,map_location='cpu',weights_only=False)['Policy'];new=state['Policy']
assert all(m.torch.equal(v,new[k]) for k,v in parent.items() if 'network_body' in k)
for suffix in ('weight','bias'):
 k='action_model._discrete_distribution.branches.0.'+suffix
 assert m.torch.equal(parent[k][[0,2,3,4,5]],new[k][[0,2,3,4,5]])
report=dict(step=state['step'],checkpointSha=m.sha(checkpoint),onnxSha=m.sha(model_path),testedObservations=tested,maxProbabilityError=maximum,parentNonPassTensorsExact=True)
output=Path(a.output);assert not output.exists();output.write_text(json.dumps(report,indent=2));print(report)

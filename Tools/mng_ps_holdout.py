"""One final offline holdout audit after stopping model selection/training."""
import json
from pathlib import Path
from mng_ps_bc import policy, distribution, metrics, sha, PARENT, ROOT
import numpy as np
import torch

source=ROOT/'Logs/MNG-Rebuild/PS-20260929/data-v2/examples.jsonl'
candidate=ROOT/'results/MNG_MS3V3-20260929-r002/MNG_ManagerV2/MNG_ManagerV2-2403429.pt'
rows=[json.loads(l) for l in source.read_text().splitlines()]
rows=[r for r in rows if r['split']=='holdout']
obs=torch.tensor(np.asarray([r['observation'] for r in rows]),dtype=torch.float32)
masks=torch.tensor(np.asarray([r['valid'] for r in rows]),dtype=torch.float32)
positive=torch.tensor([r['label']!='preserve' for r in rows]);indices=np.arange(len(rows))
actor=policy().actor;actor.load_state_dict(torch.load(PARENT,map_location='cpu',weights_only=False)['Policy'],strict=True)
with torch.no_grad():parent_probs=distribution(actor,obs,masks).probs.detach().clone()
baseline=metrics(actor,obs,masks,parent_probs,positive,indices)
actor.load_state_dict(torch.load(candidate,map_location='cpu',weights_only=False)['Policy'],strict=True)
final=metrics(actor,obs,masks,parent_probs,positive,indices)
report=dict(candidate=str(candidate.relative_to(ROOT)),candidateSha=sha(candidate),datasetSha=sha(source),
    parent=baseline,candidateMetrics=final,positiveSeeds=len({r['seed'] for r in rows if r['label']!='preserve'}),
    fasterAttackExamples=sum(r['label']=='faster-attack' for r in rows),
    scope='Final offline holdout after training stopped. No model selection or threshold tuning. Not gameplay success.')
out=ROOT/'Logs/MNG-Rebuild/PS-20260929/final-holdout.json'
with out.open('x') as f:json.dump(report,f,indent=2)
print(json.dumps(report,indent=2))

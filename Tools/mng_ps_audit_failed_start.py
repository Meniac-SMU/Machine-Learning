"""Preserve the failed first PS smoke; compare tensors, not torch zip filenames."""
import json
from pathlib import Path
import torch

ROOT=Path(__file__).resolve().parent.parent
e=ROOT/'Logs/MNG-Rebuild/MNG_MS3V3-20260929-r001'
r=ROOT/'results/MNG_MS3V3-20260929-r001/MNG_ManagerV2'
a=torch.load(e/'PS3-initial.pt',map_location='cpu',weights_only=False)
b=torch.load(r/'checkpoint.pt',map_location='cpu',weights_only=False)
def equal(a,b):
    if isinstance(a,torch.Tensor):return torch.equal(a,b)
    if isinstance(a,dict):return set(a)==set(b) and all(equal(a[k],b[k]) for k in a)
    if isinstance(a,(list,tuple)):return len(a)==len(b) and all(equal(x,y) for x,y in zip(a,b))
    return a==b
assert equal(a,b)
assert int(b['global_step']['_GlobalSteps__global_step'])==2001834
record=dict(status='quarantined-development-failure',additionalSteps=0,
    reason='PS restore actor equality passed, then audit logger raised duplicate step keyword TypeError',
    checkpointStateUnchanged=True,checkpointByteDifference='torch.save embeds output filename; tensors and optimizer recursively equal',
    resumeAllowed=False,evidence='Logs/MNG-Rebuild/PS-20260929/PS3-smoke-console.log',replacement='MNG_MS3V3-20260929-r002')
(e/'quarantine.json').write_text(json.dumps(record,indent=2));print(json.dumps(record))

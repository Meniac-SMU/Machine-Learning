"""Checkpoint and exposure audit, plus frozen validation trends. No model mutation."""
import argparse
from collections import Counter
import json
from pathlib import Path
import numpy as np
np.dot(np.zeros((2,2)),np.zeros((2,2)))
import torch
from mng_ps_bc import policy,distribution,metrics,sha,PARENT,ROOT
from mng_v2_learn import snapshot_sha


def main():
    p=argparse.ArgumentParser();p.add_argument('--output',required=True);a=p.parse_args()
    e=ROOT/'Logs/MNG-Rebuild/MNG_MS3V3-20260929-r002';r=ROOT/'results/MNG_MS3V3-20260929-r002/MNG_ManagerV2'
    s=torch.load(r/'checkpoint.pt',map_location='cpu',weights_only=False)
    pool=torch.load(r/'mng-v2-pool.pt',map_location='cpu',weights_only=False)
    manifest=json.loads((e/'manifest.json').read_text());step=int(s['global_step']['_GlobalSteps__global_step'])
    assert step==pool['step'] and 2001834<=step<=3001834
    assert all(torch.isfinite(v).all() for k in ('Policy','Optimizer:critic') for v in s[k].values())
    for path,identity in manifest['parentArtifactHashes'].items():assert sha(ROOT/path)==identity,path
    assert len(pool['pinned'])==1 and snapshot_sha(pool['pinned'][0])==manifest['baseSnapshotSha']
    optimizer=s['Optimizer:value_optimizer'];updates=[float(v['step']) for v in optimizer['state'].values()]
    assert updates and min(updates)==max(updates)
    for state in optimizer['state'].values():
        assert all(torch.isfinite(v).all() for v in state.values() if torch.is_tensor(v))
    exposure=Counter();restores=[]
    for line in (r/'mng-v2-pool.jsonl').read_text().splitlines():
        row=json.loads(line)
        if row['event']=='learning-exposure':exposure[str(row['learning_team'])]+=row['learning_transitions']
        if row['event']=='PS-restore-actor-verified':restores.append(row)
    assert sum(exposure.values())==step-2001834
    examples=ROOT/'Logs/MNG-Rebuild/PS-20260929/data-v2/examples.jsonl'
    rows=[json.loads(l) for l in examples.read_text().splitlines()];rows=[r for r in rows if r['split']=='validation']
    obs=torch.tensor([r['observation'] for r in rows]);masks=torch.tensor([r['valid'] for r in rows],dtype=torch.float32)
    pos=torch.tensor([r['label']!='preserve' for r in rows]);ids=np.arange(len(rows))
    parent=torch.load(PARENT,map_location='cpu',weights_only=False);net=policy();net.actor.load_state_dict(parent['Policy'],strict=True)
    with torch.no_grad():ref=distribution(net.actor,obs,masks).probs.detach()
    net.actor.load_state_dict(s['Policy'],strict=True);m=metrics(net.actor,obs,masks,ref,pos,ids)
    report=dict(step=step,additionalSteps=step-2001834,optimizerUpdates=updates[0],learningTeamExposure=dict(exposure),
        restoreChecks=restores,checkpointSha=sha(r/'checkpoint.pt'),poolSha=sha(r/'mng-v2-pool.pt'),
        parentArtifactsUnchanged=True,finite=True,pinnedMS2Unchanged=True,validation=m,
        gameplaySuccess=False,scope='Integrity and offline retention only; not a match result')
    with Path(a.output).open('x') as f:json.dump(report,f,indent=2)
    print(json.dumps(report,indent=2))


if __name__=='__main__':main()

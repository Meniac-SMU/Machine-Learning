"""Counterfactual receiver labels on real historical observations; no Full data."""
import json
from collections import Counter
from pathlib import Path
import numpy as np
import mng_psr_model as m

def expanded_mask(o,old):
    mask=np.r_[np.array(old,dtype=bool),[True,False,False,False,False]]
    if o[101]<.5 or o[243]>.5:return mask
    c=int(np.argmax(o[103:112]))-1;p=o[:96].reshape(8,12)
    if c not in range(4) or p[c,10]>.5 or p[c,11]>0 or o[129+c]<.5:return mask
    ball=o[96:98]*m.SCALE
    for s in range(4):
        target=p[s,1:3]*m.SCALE+.2*p[s,3:5]*9
        distance=np.linalg.norm(target-ball)
        mask[7+s]=s!=c and p[s,0]>.5 and p[s,10]<.5 and o[129+s]>.5 and 5<=distance<=28
    mask[1]|=mask[7:].any();return mask

def main():
    out=m.EVIDENCE/'bootstrap';out.mkdir()
    report=json.loads((m.ROOT/'Logs/MNG-Rebuild/PS-20260929/candidate-400k/report.json').read_text())
    rng=np.random.RandomState(929903);rows=[];counts=Counter();sources=[]
    for match in report['matches']:
        if match['opponent']=='Full':continue
        p=m.ROOT/'Logs/MNG-Rebuild/PS-20260929/candidate-400k'/f"match-{match['index']:03d}"/'decisions.jsonl'
        sources.append(dict(path=str(p.relative_to(m.ROOT)),sha=m.sha(p)))
        for i,line in enumerate(p.open()):
            r=json.loads(line)
            if r['team']!=match['policyTeam']:continue
            o=np.array(r['observation'],dtype=np.float32);mask=expanded_mask(o,r['valid']);label=m.contexts(o[None],mask[None])[0]
            if label['action']!=1 and rng.rand()>.20:continue
            rows.append(dict(observation=o.tolist(),mask=mask.tolist(),label=label,seed=match['seed'],
                             split='validation' if match['seed']%5==0 else 'train'))
            counts[(label['kind'],rows[-1]['split'])]+=1
    (out/'examples.jsonl').write_text(''.join(json.dumps(r)+'\n' for r in rows))
    (out/'manifest.json').write_text(json.dumps(dict(kind='geometric counterfactual labels; not verified receptions',
        sources=sources,counts={str(k):v for k,v in counts.items()},FullExcluded=True),indent=2))
    print(dict(counts),flush=True)
    net=m.inherited();parent=m.policy((6,));parent.actor.load_state_dict(m.torch.load(m.PARENT,weights_only=False)['Policy'])
    obs=m.torch.tensor(np.array([r['observation'] for r in rows]),dtype=m.torch.float32)
    mask=m.torch.tensor(np.array([r['mask'] for r in rows]),dtype=m.torch.float32)
    pm=m.torch.tensor(m.parent_mask(obs.numpy(),mask.numpy().astype(bool)),dtype=m.torch.float32)
    with m.torch.no_grad():ref=m.dists(parent.actor,obs,pm)[0].probs
    train=np.array([i for i,r in enumerate(rows) if r['split']=='train'])
    positives={k:np.array([i for i in train if rows[i]['label']['kind']==k]) for k in ('keeper-escape','pressure-escape','fast-advance')}
    neg=np.array([i for i in train if rows[i]['label']['action']!=1]);assert len(neg)>200
    for k,ids in positives.items():assert len(ids)>=20,(k,len(ids))
    optimizer=m.torch.optim.Adam(net.actor.parameters(),lr=5e-5)
    history=[]
    for update in range(1500):
        pos=np.concatenate([rng.choice(ids,32) for ids in positives.values()]);ns=rng.choice(neg,160)
        pd=m.dists(net.actor,obs[pos],mask[pos]);nd=m.dists(net.actor,obs[ns],mask[ns])[0]
        recipients=m.torch.tensor([rows[i]['label']['recipient'] for i in pos])
        ce=-pd[0].probs[:,1].clamp_min(1e-9).log().mean()-pd[1].probs.gather(1,recipients[:,None]).clamp_min(1e-9).log().mean()
        kl=(ref[ns]*(ref[ns].clamp_min(1e-9).log()-nd.probs.clamp_min(1e-9).log())).sum(1).mean()
        loss=.3*ce+2*kl;optimizer.zero_grad();loss.backward();m.torch.nn.utils.clip_grad_norm_(net.actor.parameters(),.5);optimizer.step()
        if (update+1)%300==0:
            val=np.array([i for i,r in enumerate(rows) if r['split']=='validation']);stats={}
            with m.torch.no_grad():
                vd=m.dists(net.actor,obs[val],mask[val])
                for kind in (*positives,'preserve'):
                    sel=[j for j,i in enumerate(val) if rows[i]['label']['kind']==kind]
                    stats[kind]=dict(n=len(sel),passProbability=float(vd[0].probs[sel,1].mean()))
                    if kind!='preserve':stats[kind]['jointCorrect']=float((vd[0].probs[sel,1]*vd[1].probs[sel,m.torch.tensor([rows[val[j]]['label']['recipient'] for j in sel])]).mean())
            history.append(dict(update=update+1,validation=stats));print(history[-1],flush=True)
    state=dict(Policy=net.actor.state_dict(),step=0,parentStep=2001834,bcUpdates=1500,bcSamples=384000)
    m.torch.save(state,out/'initial.pt');m.export(net,out/'initial.onnx')
    (out/'report.json').write_text(json.dumps(dict(history=history,environmentSteps=0,bcUpdates=1500,samples=384000),indent=2))

if __name__=='__main__':main()

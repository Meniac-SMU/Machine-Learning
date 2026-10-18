"""Measured PPO against frozen 2M, with train-only on-policy receiver teaching.

Separate environment transition, BC sample and optimizer counters. No inference
intervention. Checkpoints include Adam and RNG. Simulator restarts are explicit.
"""
import argparse
from concurrent.futures import ThreadPoolExecutor
import json
import time
import traceback
from collections import Counter
import numpy as np
import mng_psr_model as m
from mng_psr_play import build,runtime_args
from mlagents_envs.environment import UnityEnvironment
from mlagents_envs.base_env import ActionTuple
from mlagents_envs.side_channel.engine_configuration_channel import EngineConfigurationChannel
from mng_player_log_guard import PlayerLogGuard

class Worker:
    def __init__(self,i,folder,curriculum):
        self.i=i;self.team=i%2;self.folder=folder/f'worker-{i:02d}';self.folder.mkdir();self.env=None
        engine=EngineConfigurationChannel();engine.set_configuration_parameters(time_scale=20,target_frame_rate=-1)
        args=['-mngPSRecipients','-mngPSCurriculum',str(curriculum),'-mngRunId',m.RUN,
              '-mngEvidenceDir',str(self.folder),'-mngPolicyAssistMode','none','-mngFullTrace','false']+runtime_args()
        self.env=UnityEnvironment(str(build()/'MS3V2/MNG_MS3V2.exe'),worker_id=i,base_port=17500,seed=929903+i,
                no_graphics=True,side_channels=[engine],timeout_wait=90,log_folder=str(self.folder),additional_args=args)
        try:self.env.reset();self.read()
        except BaseException:
            self.close();raise
    def read(self):
        self.decisions={};self.terminal={}
        for name,spec in self.env.behavior_specs.items():
            assert list(spec.action_spec.discrete_branches)==[6,5]
            t=int(name.split('team=')[-1]);d,end=self.env.get_steps(name)
            self.decisions[t]=(name,d);self.terminal[t]=end
    def obs(self,t):
        d=self.decisions[t][1];assert len(d)==1
        return d.obs[0][0],np.concatenate([~x[0] for x in d.action_mask])
    def step(self,actions):
        for t,action in enumerate(actions):
            name,d=self.decisions[t]
            if len(d):self.env.set_actions(name,ActionTuple(discrete=np.array([action],dtype=np.int32)))
        self.env.step();self.read();end=self.terminal[self.team];d=self.decisions[self.team][1]
        terminal=len(end)>0;source=end if terminal else d
        assert len(source)==1,'Missing or multiple learner transition'
        reward=float(source.reward[0]+source.group_reward[0])
        # Terminal steps may precede the next decision. No learner action is inserted.
        for _ in range(4):
            if len(self.decisions[self.team][1]):break
            for t,(name,other) in self.decisions.items():
                assert not len(other),'Asymmetric terminal requires explicit handling'
            self.env.step();self.read()
        return reward,terminal
    def close(self):
        if self.env:self.env.close()

def arrays(workers,opponent=False):
    pairs=[w.obs(1-w.team if opponent else w.team) for w in workers]
    return np.array([x[0] for x in pairs]),np.array([x[1] for x in pairs])

def tensor(x,dtype=m.torch.float32):return m.torch.as_tensor(x,dtype=dtype)

def logprob(ds,actions):
    cmd=ds[0].probs.gather(1,actions[:,0:1]).clamp_min(1e-12).log().squeeze(1)
    rec=ds[1].probs.gather(1,actions[:,1:2]).clamp_min(1e-12).log().squeeze(1)
    return cmd+(actions[:,0]==1)*rec

def sample(ds,rng):
    p=[d.probs.detach().numpy() for d in ds]
    a=np.array([[rng.choice(len(x),p=x/x.sum()) for x in branch] for branch in p],dtype=np.int64).T
    return a

def main():
    p=argparse.ArgumentParser();p.add_argument('--target',type=int,required=True);p.add_argument('--workers',type=int,default=16)
    p.add_argument('--curriculum',type=float,default=.5);p.add_argument('--resume',action='store_true')
    p.add_argument('--specialize',action='store_true',help='restore exact parent non-pass policy once, freeze it, and learn pass heads')
    p.add_argument('--verified-demos',help='Matched simulator comparison examples.jsonl; train split only')
    a=p.parse_args()
    assert 0<a.target<=990000 and a.workers<=32
    m.RESULT.mkdir(exist_ok=a.resume);folder=m.EVIDENCE/f'train-to-{a.target}';folder.mkdir()
    net=m.policy();critic=m.Critic();optimizer=m.torch.optim.Adam(list(net.actor.parameters())+list(critic.parameters()),lr=3e-5)
    rng=np.random.RandomState(929903);m.torch.manual_seed(929903)
    state=m.torch.load(m.RESULT/'checkpoint.pt' if a.resume else m.EVIDENCE/'bootstrap/initial.pt',map_location='cpu',weights_only=False)
    net.actor.load_state_dict(state['Policy'],strict=True);step=state['step'];updates=0;exposures=[0,0];samples=0;demo_samples=0
    if a.resume:
        critic.load_state_dict(state['critic']);optimizer.load_state_dict(state['optimizer']);rng.set_state(state['rng'])
        m.torch.set_rng_state(state['torchRng']);updates=state['updates'];exposures=state['teamTransitions'];samples=state['auxSamples']
        demo_samples=state.get('verifiedDemoSamples',0)
    parent=m.policy((6,));parent.actor.load_state_dict(m.torch.load(m.PARENT,weights_only=False)['Policy']);parent.actor.eval()
    # TorchPolicy construction seeds Torch; restore AFTER constructing the parent.
    if a.resume:m.torch.set_rng_state(state['torchRng'])
    specialized=a.specialize or state.get('specialized',False)
    demo_rows=[]
    if a.verified_demos:
        demo_rows=[json.loads(line) for line in open(a.verified_demos) if line.strip()]
        demo_rows=[r for r in demo_rows if r.get('split')=='train' and r.get('anchor')]
        assert sum(r['preferredRecipient']>0 for r in demo_rows)>=8,'Insufficient verified positive demonstrations'
        for r in demo_rows:
            if r['preferredRecipient']:
                v=next(v for v in r['variants'] if v['recipient']==r['preferredRecipient'])
                assert v['prefixExact'] and v['struck'] and v['received'] and v['utility']>=r['baselineUtility']+.15
    if a.specialize:
        assert not state.get('specialized',False),'Do not reset an already specialized candidate'
        net.actor.network_body.load_state_dict(parent.actor.network_body.state_dict(),strict=True)
        net.actor.action_model._discrete_distribution.branches[0].load_state_dict(parent.actor.action_model._discrete_distribution.branches[0].state_dict())
        with m.torch.no_grad():net.actor.action_model._discrete_distribution.branches[0].bias[1]+=4.
        optimizer=m.torch.optim.Adam(list(net.actor.parameters())+list(critic.parameters()),lr=3e-5)
    if specialized:
        for param in net.actor.network_body.parameters():param.requires_grad_(False)
        branch=net.actor.action_model._discrete_distribution.branches[0]
        rows=m.torch.zeros_like(branch.weight);rows[1]=1
        biases=m.torch.zeros_like(branch.bias);biases[1]=1
        branch.weight.register_hook(lambda grad:grad*rows);branch.bias.register_hook(lambda grad:grad*biases)
    manifest=dict(run=m.RUN,target=a.target,start=step,parent=str(m.PARENT),parentSha=m.sha(m.PARENT),workers=a.workers,
        curriculum=a.curriculum,opponent='frozen original 2M on every worker',seed=929903,basePort=17500,
        gamma=.99,gaeLambda=.95,clip=.15,epochs=3,batch=256,lr=3e-5,
        critic='restored checkpoint SiLU 128x2' if a.resume else 'fresh SiLU 128x2; explicit reset',
        reward='unchanged Unity extrinsic',actionOverride=False,FullExcluded=True,runtimeArgs=runtime_args(),runtimeBuild=str(build()),
        resumeSimulator='new episodes with documented seed/port offsets; policy optimizer and RNG restored',
        specialized=specialized,restoredParentNonPassThisSegment=a.specialize,
        verifiedDemonstrations=dict(path=a.verified_demos,sha=m.sha(a.verified_demos),trainCases=len(demo_rows),positives=sum(r['preferredRecipient']>0 for r in demo_rows)) if a.verified_demos else None,
        entropy='joint command plus pass-probability-weighted receiver entropy',
        sources={f.name:m.sha(f) for f in (m.ROOT/'Tools').glob('mng_psr_*.py')})
    (folder/'manifest.json').write_text(json.dumps(manifest,indent=2))
    runtime=build()/'MS3V2';runtime_info=json.loads((runtime/'build-info.json').read_text())
    runtime_sha=m.sha(runtime/'MNG_MS3V2_Data/Managed/MNG.Runtime.dll')
    assert runtime_sha==runtime_info['runtimeSha256']
    (folder/'runtime-receipt.json').write_text(json.dumps(dict(build=str(runtime),actualRuntimeSha=runtime_sha,
        buildInfo=runtime_info,extraArgs=runtime_args(),compiledLabelIsNotUnchangedBehaviorProof=True),indent=2))
    source_dir=folder/'source';source_dir.mkdir()
    for name,identity in manifest['sources'].items():
        source=m.ROOT/'Tools'/name;assert m.sha(source)==identity
        (source_dir/name).write_bytes(source.read_bytes())
    workers=[];began=time.monotonic();status='completed'
    def save(name):
        path=m.RESULT/name
        if name!='checkpoint.pt' and path.exists():path=folder/name
        if name!='checkpoint.pt':assert not path.exists(),'Preserve numbered checkpoint'
        obj=dict(Policy=net.actor.state_dict(),critic=critic.state_dict(),optimizer=optimizer.state_dict(),step=step,
            parentStep=2001834,updates=updates,teamTransitions=exposures,auxSamples=samples,verifiedDemoSamples=demo_samples,rng=rng.get_state(),torchRng=m.torch.get_rng_state(),manifest=manifest,specialized=specialized)
        m.torch.save(obj,path.with_suffix('.tmp'));path.with_suffix('.tmp').replace(path)
        return path
    if a.specialize:save(f'PS-specialized-start-{step}.pt')
    try:
        with ThreadPoolExecutor(max_workers=a.workers) as pool:
            # Constructor failures are retained and explicitly stop this experiment.
            futures=[pool.submit(Worker,i,folder,a.curriculum) for i in range(a.workers)]
            startup_errors=[]
            for f in futures:
                try:workers.append(f.result())
                except BaseException as e:startup_errors.append(str(e))
            if startup_errors:raise RuntimeError('Worker startup failed: '+repr(startup_errors))
            while step<a.target and not (m.EVIDENCE/'STOP').exists():
                observations=[];masks=[];acts=[];logs=[];values=[];rewards=[];dones=[]
                horizon=min(128,(a.target-step+a.workers-1)//a.workers)
                for _ in range(horizon):
                    o,mask=arrays(workers);po,pm=arrays(workers,True)
                    with m.torch.no_grad():
                        ds=m.dists(net.actor,tensor(o),tensor(mask));action=sample(ds,rng);lp=logprob(ds,tensor(action,m.torch.long));value=critic(tensor(o))
                        pd=m.dists(parent.actor,tensor(po),tensor(m.parent_mask(po,pm)));pa=sample(pd,rng)
                    allactions=[]
                    for i,w in enumerate(workers):
                        choices=[[int(pa[i,0]),0],[int(pa[i,0]),0]];choices[w.team]=action[i].tolist();allactions.append(choices)
                    result=list(pool.map(lambda x:x[0].step(x[1]),zip(workers,allactions)))
                    observations.append(o);masks.append(mask);acts.append(action);logs.append(lp.numpy());values.append(value.numpy())
                    rewards.append([x[0] for x in result]);dones.append([x[1] for x in result]);step+=len(workers)
                    for w in workers:exposures[w.team]+=1
                    assert step<=1000000
                no,_=arrays(workers)
                with m.torch.no_grad():nv=critic(tensor(no)).numpy()
                v=np.array(values);r=np.array(rewards);done=np.array(dones);adv=np.zeros_like(r);gae=np.zeros(len(workers))
                for t in reversed(range(horizon)):
                    nxt=nv if t==horizon-1 else v[t+1];cont=1-done[t].astype(float)
                    delta=r[t]+.99*nxt*cont-v[t];gae=delta+.99*.95*cont*gae;adv[t]=gae
                ob=tensor(np.array(observations).reshape(-1,244));ma=tensor(np.array(masks).reshape(-1,11));ac=tensor(np.array(acts).reshape(-1,2),m.torch.long)
                oldlog=tensor(np.array(logs).ravel());ret=tensor((adv+v).ravel());advantage=tensor(adv.ravel());advantage=(advantage-advantage.mean())/(advantage.std()+1e-8)
                labels=m.contexts(ob.numpy(),ma.numpy().astype(bool));kinds=Counter(x['kind'] for x in labels)
                rec=tensor([x['recipient'] for x in labels],m.torch.long);positive=tensor([x['action']==1 for x in labels],m.torch.bool)
                with m.torch.no_grad():ref=m.dists(parent.actor,ob,tensor(m.parent_mask(ob.numpy(),ma.numpy().astype(bool))))[0].probs
                last={};weight=(.03 if step<=200000 else .01) if specialized else (.06 if step<=200000 else .02)
                for epoch in range(3):
                    for ids in np.array_split(rng.permutation(len(ob)),max(1,len(ob)//256)):
                        ds=m.dists(net.actor,ob[ids],ma[ids]);ratio=(logprob(ds,ac[ids])-oldlog[ids]).exp()
                        policy_loss=-m.torch.minimum(ratio*advantage[ids],ratio.clamp(.85,1.15)*advantage[ids]).mean()
                        value_loss=.5*(critic(ob[ids])-ret[ids]).square().mean()
                        entropy=(-(ds[0].probs*ds[0].probs.clamp_min(1e-12).log()).sum(1)
                            -ds[0].probs[:,1]*(ds[1].probs*ds[1].probs.clamp_min(1e-12).log()).sum(1)).mean()
                        pos=positive[ids];aux=m.torch.tensor(0.)
                        if pos.any():
                            cp=ds[0].probs[pos,1];rp=ds[1].probs[pos].gather(1,rec[ids][pos,None]).squeeze(1)
                            if specialized:
                                # Soft teaching target, not an inference floor or cap.
                                aux=-(.12*cp.clamp_min(1e-9).log()+.88*(1-cp).clamp_min(1e-9).log()+.12*rp.clamp_min(1e-9).log()).mean()
                            else:aux=-(cp.clamp_min(1e-9).log()+rp.clamp_min(1e-9).log()).mean()
                        neg=~pos
                        if neg.any():aux=aux+2*(ref[ids][neg]*(ref[ids][neg].clamp_min(1e-9).log()-ds[0].probs[neg].clamp_min(1e-9).log())).sum(1).mean()
                        demonstration=m.torch.tensor(0.)
                        if demo_rows:
                            dr=[demo_rows[i] for i in rng.randint(len(demo_rows),size=64)]
                            demo_samples+=len(dr)
                            do=tensor(np.array([r['anchor']['observation'] for r in dr]));dm=tensor(np.array([r['anchor']['mask'] for r in dr]))
                            dd=m.dists(net.actor,do,dm);positive_demo=tensor([r['preferredRecipient']>0 for r in dr],m.torch.bool)
                            # Actual successful alternative cases get a soft teaching
                            # target, while failed alternatives retain the parent's prior.
                            with m.torch.no_grad():
                                dp=m.dists(parent.actor,do,tensor(m.parent_mask(do.numpy(),dm.numpy().astype(bool))))[0].probs[:,1]
                                desired=m.torch.where(positive_demo,.25,dp)
                            cp=dd[0].probs[:,1].clamp(1e-8,1-1e-7)
                            demonstration=-(desired*cp.log()+(1-desired)*(1-cp).log()).mean()
                            if positive_demo.any():
                                recipients=tensor([r['preferredRecipient'] for r in dr],m.torch.long)
                                demonstration-=.25*dd[1].probs[positive_demo].gather(1,recipients[positive_demo,None]).clamp_min(1e-8).log().mean()
                        loss=policy_loss+value_loss-.002*entropy+weight*aux+.04*demonstration
                        assert m.torch.isfinite(loss);optimizer.zero_grad();loss.backward();m.torch.nn.utils.clip_grad_norm_(list(net.actor.parameters())+list(critic.parameters()),.5);optimizer.step()
                        updates+=1;samples+=len(ids);last=dict(loss=float(loss),policy=float(policy_loss),value=float(value_loss),entropy=float(entropy),aux=float(aux),auxWeight=weight,verifiedDemoLoss=float(demonstration))
                if specialized:
                    parent_branch=parent.actor.action_model._discrete_distribution.branches[0]
                    keep=[0,2,3,4,5]
                    assert m.torch.equal(branch.weight[keep],parent_branch.weight[keep])
                    assert m.torch.equal(branch.bias[keep],parent_branch.bias[keep])
                    assert all(m.torch.equal(v,parent.actor.network_body.state_dict()[k]) for k,v in net.actor.network_body.state_dict().items())
                row=dict(step=step,updates=updates,wallSeconds=time.monotonic()-began,teamTransitions=exposures.copy(),
                         rewardMean=float(r.mean()),terminalTransitions=int(done.sum()),labels=dict(kinds),metrics=last)
                with (folder/'updates.jsonl').open('a') as f:f.write(json.dumps(row)+'\n')
                save('checkpoint.pt');(m.EVIDENCE/'progress.json').write_text(json.dumps(row,indent=2));print(json.dumps(row),flush=True)
                for w in workers:
                    errors=PlayerLogGuard(w.folder).scan();assert not errors,errors
        if (m.EVIDENCE/'STOP').exists():status='saved-stopped'
    except BaseException:
        status='failed-saved';(folder/'failure.txt').write_text(traceback.format_exc());save('checkpoint.pt');raise
    finally:
        for w in workers:w.close()
        save('checkpoint.pt');final_checkpoint=save(f'PS-{step}.pt')
        export_path=m.RESULT/f'PS-{step}.onnx'
        if export_path.exists():export_path=folder/export_path.name
        assert not export_path.exists(),'Preserve numbered ONNX'
        m.export(net,export_path)
        (folder/'completion.json').write_text(json.dumps(dict(status=status,additionalSteps=step,updates=updates,teamTransitions=exposures,
            checkpointSha=m.sha(m.RESULT/'checkpoint.pt'),numberedCheckpoint=str(final_checkpoint),exportedModel=str(export_path),wallSeconds=time.monotonic()-began),indent=2))

if __name__=='__main__':main()

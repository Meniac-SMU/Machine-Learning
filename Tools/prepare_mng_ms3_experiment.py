"""Prepare a bounded MS3 experiment, its immutable inputs and launch commands. Never trains."""
import argparse
import copy
import hashlib
import json
from pathlib import Path
import torch
import yaml

ROOT=Path(__file__).resolve().parent.parent
def sha(path):return hashlib.sha256(Path(path).read_bytes()).hexdigest()

def fresh_actor(state):
    assert set(state)=={'Policy','global_step'}, 'Actor-only initialization required'
    assert all(torch.isfinite(value).all() for value in state['Policy'].values())
    prepared=copy.deepcopy(state)
    step=prepared['global_step']['_GlobalSteps__global_step']
    assert isinstance(step,torch.Tensor) and step.numel()==1, 'Invalid global step'
    step.zero_()
    return prepared

def check_receipt(receipt,training,evaluation,preflight):
    assert training['runtimeSha256']==evaluation['runtimeSha256']==preflight['runtimeSha'], 'Runtime mismatch'
    assert preflight['passed'] and preflight['workers']==32 and preflight['simultaneousBarrierReached'], '32-worker preflight incomplete'
    assert preflight['trainingSteps']==preflight['optimizerUpdates']==0, 'Preflight must not train'
    assert receipt['runtimeSha']==training['runtimeSha256'], 'Receipt runtime mismatch'
    required=('D1D2','D3','D4','EditMode','PlayMode','Python')
    assert set(required)<=set(receipt['checks']), 'Incomplete readiness receipt'
    return all(receipt['checks'][key]['passed'] is True for key in required)

def main():
    p=argparse.ArgumentParser()
    for key in ('build-root','preflight','receipt','initial-actor','output','run-id'):p.add_argument('--'+key,required=True)
    p.add_argument('--historical-policy')
    p.add_argument('--generation',choices=('v2','v3'),default='v2')
    args=p.parse_args();build=Path(args.build_root).resolve();out=Path(args.output).resolve()
    training=json.loads((build/'MS3V2/build-info.json').read_text())
    evaluation=json.loads((build/'EvaluationV2/build-info.json').read_text())
    preflight=json.loads(Path(args.preflight).read_text())
    receipt=json.loads(Path(args.receipt).read_text())
    ready=check_receipt(receipt,training,evaluation,preflight)
    if args.generation=='v3':
        from mng_v3_policy_guard import validate_initial
        lineage=validate_initial(args.initial_actor,args.historical_policy,args.run_id)
        assert receipt.get('generation')=='v3' and receipt.get('artifacts'), 'v3 evidence provenance required'
        for artifact in receipt['artifacts']:
            assert sha(artifact['path'])==artifact['sha256'], 'Readiness evidence changed'
    else:
        assert args.historical_policy, 'v2 preparation requires its historical reference'
    for stage,info in [('MS3V2',training),('EvaluationV2',evaluation)]:
        for relative,key in [(f'MNG_{stage}.exe','executableSha256'),(f'MNG_{stage}_Data/Managed/MNG.Runtime.dll','runtimeSha256'),(f'MNG_{stage}_Data/level0','levelSha256')]:
            assert sha(build/stage/relative)==info[key], 'Build file changed'
    actor=Path(args.initial_actor).resolve();source_state=torch.load(actor,map_location='cpu',weights_only=False)
    state=fresh_actor(source_state)
    from mng_v2_learn import historical_snapshot
    historical=Path(args.historical_policy).resolve() if args.historical_policy else None
    if historical: historical_snapshot(historical,{'MNG_ManagerV2':state['Policy']})
    from mng_v2_learn import snapshot_sha
    base_snapshot_sha=snapshot_sha({'MNG_ManagerV2':state['Policy']})
    assert not (ROOT/'results'/args.run_id).exists() and not (ROOT/'Logs/MNG-Rebuild'/args.run_id).exists(), 'Run already exists'
    out.mkdir(parents=True,exist_ok=False)
    initial=out/'initial-policy.pt';torch.save(state,initial)
    config=yaml.safe_load((ROOT/'Assets/_Soccer/Manager/Training/MNG_MS3V2.yaml').read_text())
    config['behaviors']['MNG_ManagerV2']['max_steps']=200000
    config['behaviors']['MNG_ManagerV2']['init_path']=initial.as_posix()
    (out/'config-preview.yaml').write_text(yaml.safe_dump(config,sort_keys=False),encoding='ascii')
    gate=dict(passed=ready,runtimeSha=training['runtimeSha256'],initialPolicySha=sha(initial),maximumSteps=200000,
        receiptSha=sha(args.receipt),preflightSha=sha(args.preflight),generation=args.generation,
        baseActorSourceSha=sha(actor),baseSnapshotSha=base_snapshot_sha)
    if historical: gate['historicalPolicySha']=sha(historical)
    (out/'preparation-gate.json').write_text(json.dumps(gate,indent=2))
    sources=['Tools/MNG_V2_Train.ps1','Tools/mng_v2_learn.py','Tools/mng_v2_formal_learn.py',
        'Assets/_Soccer/Manager/Training/MNG_MS3V2.yaml']
    if args.generation=='v3':
        sources += ['Tools/prepare_mng_ms3_experiment.py','Tools/mng_v3_policy_guard.py','Tools/mng_v3_evaluate.py',
            'Tools/mng_v2_evaluate.py','Tools/inspect_mng_post_r6.py',
            'docs/soccer/training/ms-v3-model-registry.json','docs/soccer/training/ms-v3-schema.json']
        sources += [str(p.relative_to(ROOT)).replace('\\','/') for folder in ('Assets/_Soccer/Manager/Runtime','Assets/_Soccer/Core/Scripts') for p in (ROOT/folder).rglob('*.cs')]
    manifest=dict(stage=f'MS3-{args.generation} / bounded learning preparation (D5)',generation=args.generation,readyToStart=ready,trainingStarted=False,
        optimizerUpdates=0,runId=args.run_id,workers=32,trainerSeed=20260923,poolSeed=20260922,
        nominalMaximumSteps=200000,firstStopAt=100000,stepAccounting='aggregate learning-team transitions; final trainer batch may cross boundary',
        hypothesis=('MS3-v3 restart from the approved MS2 actor with fresh optimizer and pool; only initial and this new run snapshots; unchanged PPO settings.' if args.generation=='v3' else 'Bounded adaptation with initial and historical actors pinned.'),
        initialActorSource=str(actor),initialActorSourceSha=sha(actor),initialActorSourceStep=int(source_state['global_step']['_GlobalSteps__global_step']),
        initialStep=0,initialPolicySha=sha(initial),runtimeSha=training['runtimeSha256'],
        historicalPolicy=str(historical) if historical else None,historicalPolicySha=sha(historical) if historical else None,
        baseActorSourceSha=sha(actor),baseSnapshotSha=base_snapshot_sha,
        learningTeamExposure='Record actual transitions per team; 40k switches can leave one segment imbalance at the bounded 200k stop',
        environmentRevision=training['environmentRevision'],sourceHashes={f:sha(ROOT/f) for f in sources},
        evaluationBuild=str(build/'EvaluationV2'),trainingBuild=str(build/'MS3V2'),
        stopConditions=['NaN/crash/integrity violation: save and stop','100k monitoring then explicit 200k evaluation',
            'No automatic R7 or million-step extension','No pass quota, action override or command reward'],
        holdout=dict(seedStart=996001,pairs=40,status='reserved-not-run; do not use for tuning'))
    (out/'manifest.json').write_text(json.dumps(manifest,indent=2))
    def quote(value):return "'"+str(value).replace("'","''")+"'"
    guard="param([switch]$ValidateOnly)\n$ErrorActionPreference='Stop'\n$plan=Get-Content (Join-Path $PSScriptRoot 'manifest.json') -Raw | ConvertFrom-Json\nif(!$plan.readyToStart){throw 'Readiness gate is not passed'}\n"
    guard+=f"$root={quote(ROOT)}\nforeach($entry in $plan.sourceHashes.PSObject.Properties){{if((Get-FileHash (Join-Path $root $entry.Name) -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.Value){{throw 'Prepared source changed; revalidate'}}}}\n"
    base=f"& {quote(ROOT/'Tools/MNG_V2_Train.ps1')} -RunId {quote(args.run_id)} -TrainingBuild {quote(build/'MS3V2')} -MaximumSteps 200000 -Seed 20260923"
    guard+=f"if((Get-FileHash {quote(out/'preparation-gate.json')} -Algorithm SHA256).Hash.ToLowerInvariant() -ne {quote(sha(out/'preparation-gate.json'))}){{throw 'Prepared gate changed'}}\n"
    guard+=f"if((Get-FileHash {quote(args.receipt)} -Algorithm SHA256).Hash.ToLowerInvariant() -ne {quote(sha(args.receipt))}){{throw 'Readiness receipt changed'}}\n"
    guard+=f"if((Get-FileHash {quote(args.preflight)} -Algorithm SHA256).Hash.ToLowerInvariant() -ne {quote(sha(args.preflight))}){{throw 'Preflight changed'}}\n"
    (out/'launch-first-100k.ps1').write_text(guard+base+f" -StopAt 100000 -InitialPolicy {quote(initial)}{(' -HistoricalPolicy '+quote(historical)) if historical else ''} -PreparationGate {quote(out/'preparation-gate.json')} -ValidateOnly:$ValidateOnly\n",encoding='utf-8')
    monitoring=f"$monitor=Get-Content {quote(ROOT/'Logs/MNG-Rebuild'/args.run_id/'monitor-100k.json')} -Raw | ConvertFrom-Json\nif(!$monitor.passed -or $monitor.runId -ne $plan.runId -or $monitor.runtimeSha -ne $plan.runtimeSha){{throw '100k monitoring gate required'}}\n"
    (out/'resume-to-200k.ps1').write_text(guard+monitoring+base+" -StopAt 200000 -Resume -ValidateOnly:$ValidateOnly\n",encoding='utf-8')
    print(json.dumps(dict(prepared=str(out),readyToStart=ready,trainingStarted=False)))

if __name__=='__main__':main()

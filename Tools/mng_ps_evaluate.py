"""PS frozen paired tournament; shared immutable runtime and explicit opponent sets."""
import argparse
from concurrent.futures import ThreadPoolExecutor, as_completed
import json
from pathlib import Path
from queue import Queue
import threading
import numpy as np
import mng_v2_evaluate as ev
from mng_player_log_guard import PlayerLogGuard
from mng_v3_policy_guard import validate_evaluation_policy

PARENT=ev.ROOT/'results/MNG_MS3V3-20260925-r002/MNG_ManagerV2/MNG_ManagerV2-2001834.onnx'
OPPONENTS={'parent2M':str(PARENT),'Full':'R0-Full-v2','recover':'recover','carry-shot':'carry-shot',
    'balanced':'balanced','MS2':str(ev.ROOT/'results/MNG_MS2V2-20260922-r002/MNG_ManagerV2/MNG_ManagerV2-200120.onnx'),
    'PS200k':str(ev.ROOT/'results/MNG_MS3V3-20260929-r002/MNG_ManagerV2/MNG_ManagerV2-2203730.onnx')}


def main():
    p=argparse.ArgumentParser();p.add_argument('--candidate',default=str(PARENT));p.add_argument('--output',required=True)
    p.add_argument('--opponents',nargs='+',choices=list(OPPONENTS),default=['Full','recover','carry-shot','balanced','MS2'])
    p.add_argument('--pairs',type=int,default=20);p.add_argument('--parallel',type=int,default=8)
    p.add_argument('--seed',type=int,default=929600);p.add_argument('--resume',action='store_true');a=p.parse_args()
    out=Path(a.output).resolve();out.mkdir(parents=True,exist_ok=a.resume)
    build=ev.ROOT/'Builds/MNG_V2/MS3-v3-WorkerIOFix-20260924T182752604Z/EvaluationV2'
    info=json.loads((build/'build-info.json').read_text());exe=build/'MNG_EvaluationV2.exe'
    assert ev.sha(exe)==info['executableSha256']
    assert ev.sha(build/'MNG_EvaluationV2_Data/Managed/MNG.Runtime.dll')==info['runtimeSha256']
    assert ev.sha(build/'MNG_EvaluationV2_Data/level0')==info['levelSha256']
    source=Path(a.candidate).resolve();identity=ev.sha(source)
    validate_evaluation_policy(str(source))
    for name in a.opponents:validate_evaluation_policy(OPPONENTS[name])
    manifest=dict(candidate=str(source),candidateSha=identity,runtimeSha=info['runtimeSha256'],
                  seed=a.seed,pairs=a.pairs,opponents=a.opponents,teacher=False,seconds=300)
    if a.resume:
        prior=json.loads((out/'manifest.json').read_text());assert prior==manifest, 'Resume evaluation inputs changed'
        (out/'evaluator-resume-source.py').write_bytes(Path(__file__).read_bytes())
    else:
        (out/'manifest.json').write_text(json.dumps(manifest,indent=2))
        (out/'evaluator-source.py').write_bytes(Path(__file__).read_bytes())
    jobs=[];names={}
    for name in a.opponents:
        for pair in range(a.pairs):
            for team in (0,1):
                idx=len(jobs);names[idx]=name
                seed=a.seed+list(OPPONENTS).index(name)*100+pair
                jobs.append((idx,seed,team,str(source),OPPONENTS[name],str(out),exe,info['runtimeSha256'],False,0,True,True,True,True))
    rows=[];pending=[]
    for job in jobs:
        folder=out/f'match-{job[0]:03d}'
        verified=folder/'verified.json'
        if a.resume and verified.exists():
            row=json.loads(verified.read_text());assert row['seed']==job[1] and row['policyTeam']==job[2]
            row['opponent']=names[row['index']];rows.append(row)
        else:
            if folder.exists():
                failures=out/'failed-attempts';failures.mkdir(exist_ok=True)
                target=failures/(folder.name+'-first')
                assert not target.exists() and folder.resolve().parent==out.resolve()
                folder.rename(target)  # Preserve failed logs; never overwrite a failed attempt.
            pending.append(job)
    # Match identity and RNG remain fixed; reuse only a bounded set of local socket slots.
    # Unbounded match indexes previously hit a Windows-excluded port at match160.
    slots=Queue()
    for slot in range(a.parallel):slots.put(slot)
    local=threading.local();OriginalEnvironment=ev.UnityEnvironment
    def environment(*args,**kwargs):
        kwargs['worker_id']=local.slot
        return OriginalEnvironment(*args,**kwargs)
    ev.UnityEnvironment=environment
    def run(job):
        slot=slots.get();local.slot=slot
        try:return ev.game(job)
        finally:slots.put(slot)
    (out/'transport.json').write_text(json.dumps(dict(basePort=7100,slots=a.parallel,
        matchSeedUnchanged=True,portAffectsOnlyTransportAndWorkerLogNames=True,resumed=a.resume),indent=2))
    with ThreadPoolExecutor(max_workers=a.parallel) as pool:
        for f in as_completed([pool.submit(run,j) for j in pending]):
            row=f.result();row['opponent']=names[row['index']];rows.append(row)
            print(f"EVALUATE {len(rows)}/{len(jobs)} {row['opponent']} {row['goalsFor']}:{row['goalsAgainst']}",flush=True)
    assert ev.sha(source)==identity
    failures=[f for folder in out.glob('match-*') for f in PlayerLogGuard(folder).scan()]
    assert not failures,failures
    summary={name:ev.summarize([r for r in rows if r['opponent']==name]) for name in a.opponents}
    report=dict(manifest=manifest,summary=summary,matches=sorted(rows,key=lambda r:r['index']),playerErrors=failures)
    (out/'report.json').write_text(json.dumps(report,indent=2))
    print(json.dumps({k:dict(scoreRate=v['scoreRate'],wins=v['wins'],draws=v['draws'],losses=v['losses']) for k,v in summary.items()}),flush=True)


if __name__=='__main__':main()

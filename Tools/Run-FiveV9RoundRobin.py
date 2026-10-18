"""Authorized 8-model, 28-pair, 1,120-match evaluation. No training."""
import hashlib
import itertools
import json
import os
from pathlib import Path
import subprocess
import sys
import time
import traceback
from five_evaluation_audit import audit

ROOT=Path(__file__).resolve().parent.parent
BASE=ROOT/'Logs/Manager5v5/keeper-accel12-v9-20261016/round-robin'
BUILD=ROOT/'Builds/Manager5v5/keeper-accel12-learning-v9-20261016/Soccer5v5Learning.exe'
MODEL=ROOT/'results/Manager5v5/MNG5_V5-20261016-r001/MNG5_Manager_v5'
RUNTIME='tactics-v9-keeper-accel12'

def read(p): return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def write(p,v):
    t=p.with_suffix('.tmp');t.write_text(json.dumps(v,ensure_ascii=False,indent=2),encoding='utf-8');os.replace(t,p)

def main():
    BASE.mkdir(parents=True,exist_ok=True)
    assert BUILD.is_file()
    models=[dict(id='rule',name='규칙형',path='rule',sha256=None)]
    registry=read(ROOT/'Logs/Manager5v5/p9-frozen-v8-20261015/MNG5_V5-20261016-r001/model-registry.json')['stages']
    for step in range(0,2400001,400000):
        p=MODEL/f'MNG5_Manager_v5-{step}.onnx'
        h=sha(p);assert h==next(x['onnxSha256'] for x in registry if x['step']==step)
        models.append(dict(id=f's{step:07d}',name=f'{step//10000}만',step=step,path=str(p),sha256=h))
    protocol=dict(runtime=RUNTIME,models=models,matchSeconds=300,timeScale=100,workers=16,seeds=list(range(76000,76020)),
        pairs=28,matchesPerPair=40,totalMatches=1120,matchmaking='20 seeds, both sides for each distinct unordered pair',
        keeperAccelerationMultiplier=1.2,fieldAcceleration=30,keeperAcceleration=36,keeperSpeedMultiplier=1.2,
        points=dict(win=3,draw=1,loss=0),ranking='points, goal difference, goals for; report tied ranks and head-to-head',
        trainingStarted=False,build=str(BUILD),buildSha256=sha(BUILD),runnerSha256=sha(Path(__file__)),evaluatorSha256=sha(ROOT/'Tools/five_v9_evaluate.py'))
    path=BASE/'protocol.json'
    if path.exists(): assert read(path)==protocol,'Protocol changed'
    else: write(path,protocol)
    env=os.environ.copy();env.update(OMP_NUM_THREADS='1',OPENBLAS_NUM_THREADS='1',MKL_NUM_THREADS='1',PYTHONUNBUFFERED='1',CUDA_VISIBLE_DEVICES='-1')
    completed=[]
    try:
        for n,(a,b) in enumerate(itertools.combinations(models,2),1):
            candidate,opponent=(b,a) if a['id']=='rule' else (a,b)
            label=f'{n:02d}-{a["id"]}-vs-{b["id"]}';out=BASE/label;receipt=BASE/(label+'-process.json')
            write(BASE/'status.json',dict(state='running',pair=n,label=label,completedPairs=len(completed),completedGames=len(completed)*40,updated=time.time()))
            if receipt.exists():
                assert read(receipt)['exitCode']==0,'Preserve failed evidence; review before restarting'
            else:
                assert not out.exists(),'Existing partial evidence must be reviewed'
                assert sha(BUILD)==protocol['buildSha256']
                for m in models[1:]: assert sha(Path(m['path']))==m['sha256']
                cmd=[sys.executable,str(ROOT/'Tools/five_v9_evaluate.py'),'--candidate',candidate['path'],'--opponent',opponent['path'],
                    '--build',str(BUILD),'--output',str(out),'--pairs','20','--seed','76000','--workers','16','--worker','400']
                print('START',label,flush=True)
                with (BASE/(label+'-stdout.log')).open('w',encoding='utf-8') as stdout,(BASE/(label+'-stderr.log')).open('w',encoding='utf-8') as stderr:
                    p=subprocess.Popen(cmd,cwd=ROOT,env=env,stdout=stdout,stderr=stderr)
                    write(BASE/'active-process.json',dict(pid=p.pid,label=label,args=cmd,started=time.time()))
                    code=p.wait()
                write(receipt,dict(exitCode=code,ended=time.time(),args=cmd))
                assert code==0,f'Pair {label} failed; no automatic retry'
            summary=read(out/'summary.json');assert summary['passed'] and summary['games']==40
            checked=audit(out,expected_runtime=RUNTIME);write(out/'final-match-audit.json',checked);assert checked['games']==40
            games=[read(p) for p in sorted(out.glob('match-*/result.json'))]
            assert len(games)==40 and sorted((x['seed'],x['candidateTeam']) for x in games)==[(s,t) for s in range(76000,76020) for t in (0,1)]
            completed.append(dict(pair=n,label=label,modelA=a['id'],modelB=b['id'],candidate=candidate['id'],opponent=opponent['id'],summary=summary))
            write(BASE/'pair-results.json',completed)
            print(json.dumps(dict(event='pair-complete',pair=n,games=n*40,candidate=candidate['name'],opponent=opponent['name'],wins=summary['wins'],draws=summary['draws'],losses=summary['losses'])),flush=True)
        write(BASE/'status.json',dict(state='complete',pairs=28,games=1120,trainingStarted=False,ended=time.time()))
        print('COMPLETE 1120',flush=True)
    except BaseException:
        write(BASE/'status.json',dict(state='failed',completedPairs=len(completed),error=traceback.format_exc(),ended=time.time()))
        raise

if __name__=='__main__': main()

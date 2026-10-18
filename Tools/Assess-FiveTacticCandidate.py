"""Frozen-policy full-game execution ablations; no training or usage enforcement."""
import json
import os
from pathlib import Path
import subprocess
import sys
import time

ROOT=Path(__file__).resolve().parent.parent
BASE=ROOT/'Logs/Manager5v5/p9-tactics-20261014/full-game-ablations'
BUILD=ROOT/'Builds/Manager5v5/p9-tactics-v3-20261014/Soccer5v5Learning.exe'
POLICY=ROOT/'results/Manager5v5/MNG5_V2-20261014-r001/MNG5_Manager_v2'
def write(path,data):path.write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
def main():
    BASE.mkdir(exist_ok=True);completed=[];started=time.time()
    env=os.environ.copy();env.update(OMP_NUM_THREADS='1',OPENBLAS_NUM_THREADS='1',MKL_NUM_THREADS='1',CUDA_VISIBLE_DEVICES='-1')
    for variant in ('legacy','pass','cutback','block','all'):
        for label,opponent in [('strong','rule'),('old-800k',str(POLICY/'MNG5_Manager_v2-800000.onnx'))]:
            name=variant+'-'+label;output=BASE/name
            write(BASE/'status.json',dict(state='running',operation=name,completed=completed,started=started))
            if not (output/'summary.json').exists():
                assert not output.exists(),f'Incomplete failed trial requires diagnosis: {output}'
                command=[sys.executable,'Tools/five_tactic_evaluate.py','--candidate',str(POLICY/'MNG5_Manager_v2-1200000.onnx'),
                    '--opponent',opponent,'--build',str(BUILD),'--output',str(output),'--pairs','4','--seed','62000',
                    '--workers','8','--worker','240','--execution-variant',variant]
                print('START',name,flush=True)
                with (BASE/(name+'-stdout.log')).open('w') as out,(BASE/(name+'-stderr.log')).open('w') as err:
                    result=subprocess.run(command,cwd=ROOT,env=env,stdout=out,stderr=err)
                write(BASE/(name+'-process.json'),dict(exitCode=result.returncode,command=command,ended=time.time()))
                assert result.returncode==0,name
            summary=json.loads((output/'summary.json').read_text(encoding='utf-8'))
            assert summary['passed'] and summary['games']==8
            completed.append(dict(variant=variant,opponent=label,summary=summary))
            print('DONE',name,summary['wins'],summary['draws'],summary['losses'],summary['goalsFor'],summary['goalsAgainst'],flush=True)
    write(BASE/'status.json',dict(state='complete',started=started,ended=time.time(),completed=completed,
        note='Eight games per combination: execution diagnosis only. Same frozen policy on both runtime variants; not a trained-candidate quality claim.'))
if __name__=='__main__':main()

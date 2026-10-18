"""Bounded PS continuation to 400k with saved-checkpoint audits after each 100k.

The controlling agent checks account usage and can request a safe stop by writing
the shared STOP file. This runner never promotes a model or edits the exhibition.
"""
import argparse
import json
from pathlib import Path
import shutil
import subprocess
import sys

ROOT=Path(__file__).resolve().parent.parent
E=ROOT/'Logs/MNG-Rebuild/MNG_MS3V3-20260929-r002'
LOG=ROOT/'Logs/MNG-Rebuild/PS-20260929'


def main():
    p=argparse.ArgumentParser();p.add_argument('--start',type=int,default=200000);p.add_argument('--end',type=int,default=400000)
    a=p.parse_args();assert a.start%100000==a.end%100000==0 and 100000<=a.start<=a.end<=400000
    for target in range(a.start,a.end+1,100000):
        assert not (E/'STOP').exists(), 'Explicit saved-stop request'
        log=LOG/f'PS-to-{target}-console.log'
        with log.open('x') as f:
            result=subprocess.run([sys.executable,str(ROOT/'Tools/mng_ps_train_aux.py'),'--additional',str(target)],cwd=ROOT,stdout=f,stderr=subprocess.STDOUT)
        if result.returncode:
            (LOG/f'continuation-failed-{target}.json').write_text(json.dumps(dict(target=target,exitCode=result.returncode,log=str(log))))
            raise RuntimeError(f'Training failed at target {target}; preserve state, no automatic retry')
        with (LOG/f'inspection-{target}-console.log').open('x') as f:
            result=subprocess.run([sys.executable,str(ROOT/'Tools/mng_ps_inspect.py'),'--output',str(LOG/f'inspection-{target}.json')],cwd=ROOT,stdout=f,stderr=subprocess.STDOUT)
        if result.returncode:raise RuntimeError(f'Checkpoint audit failed at {target}; do not continue')
        saved=json.loads((E/f'PS-to-{target}'/'saved.json').read_text())
        assert target<=saved['additionalSteps']<target+10000
        print(json.dumps(dict(stage='PS4',target=target,saved=saved)),flush=True)
    (LOG/'training-400k-finished.json').write_text(json.dumps(dict(trainingSaved=True,target=a.end,successJudged=False)))


if __name__=='__main__':main()

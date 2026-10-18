"""Current formal evaluation entry point: fixed 40 games per model pair."""
import argparse
import subprocess
import sys
from five_v10_run_common_r2 import ROOT,BUILD,verify

p=argparse.ArgumentParser(description=__doc__)
p.add_argument('--candidate',required=True)
p.add_argument('--opponent',required=True)
p.add_argument('--output',required=True)
p.add_argument('--seed',type=int,default=63000)
p.add_argument('--workers',type=int,default=20)
p.add_argument('--worker',type=int,default=300)
a=p.parse_args()
assert 1<=a.workers<=32
verify()
subprocess.run([sys.executable,str(ROOT/'Tools/five_v10_evaluate.py'),'--candidate',a.candidate,'--opponent',a.opponent,
    '--build',str(BUILD),'--output',a.output,'--pairs','20','--seed',str(a.seed),
    '--workers',str(a.workers),'--worker',str(a.worker)],cwd=ROOT,check=True)

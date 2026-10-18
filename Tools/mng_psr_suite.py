"""Explicit non-Full secondary suite with matched opponent seed blocks."""
import argparse
import json
from pathlib import Path
import subprocess
import sys
import mng_psr_model as m

OPPONENTS={'recover':'recover','carry-shot':'carry-shot','balanced':'balanced',
           'MS2':str(m.ROOT/'results/MNG_MS2V2-20260922-r002/MNG_ManagerV2/MNG_ManagerV2-200120.pt')}

def main():
    p=argparse.ArgumentParser();p.add_argument('--candidate',required=True);p.add_argument('--name',required=True)
    p.add_argument('--parallel',type=int,default=4);p.add_argument('--pairs',type=int,default=20);a=p.parse_args()
    reports={}
    for i,(name,opponent) in enumerate(OPPONENTS.items()):
        out=m.EVIDENCE/f'{a.name}-{name}'
        subprocess.run([sys.executable,str(m.ROOT/'Tools/mng_psr_play.py'),'--candidate',a.candidate,'--opponent',opponent,
            '--seed',str(931100+i*100),'--pairs',str(a.pairs),'--parallel',str(a.parallel),'--output',str(out)],check=True)
        reports[name]=json.loads((out/'report.json').read_text())['summary']
        (m.EVIDENCE/f'{a.name}-suite-progress.json').write_text(json.dumps(reports,indent=2))
    (m.EVIDENCE/f'{a.name}-suite.json').write_text(json.dumps(reports,indent=2))

if __name__=='__main__':main()

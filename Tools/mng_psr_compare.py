"""Paired opponent-seed comparison; no success inferred from pass count alone."""
import argparse
import json
from pathlib import Path
import numpy as np
import mng_psr_model as m
from mng_psr_play import summarize
from mng_psr_suite import OPPONENTS

def paired(candidate,baseline):
    def keyed(rows):
        result={(r['seed'],r['policyTeam']):r for r in rows};assert len(result)==len(rows);return result
    c=keyed(candidate);b=keyed(baseline);assert c.keys()==b.keys()
    seeds=sorted({k[0] for k in c});values=[]
    for seed in seeds:values.append(np.mean([c[seed,t]['score']-b[seed,t]['score'] for t in (0,1)]))
    rng=np.random.RandomState(930903);v=np.array(values)
    boot=v[rng.randint(len(v),size=(10000,len(v)))].mean(1)
    return dict(delta=float(v.mean()),pairedSeed95=np.quantile(boot,[.025,.975]).tolist())

def main():
    p=argparse.ArgumentParser();p.add_argument('--candidate-name',required=True);p.add_argument('--duel',nargs='+',required=True)
    p.add_argument('--baseline-name',default='baseline');a=p.parse_args()
    rows=[]
    for name in a.duel:rows+=json.loads((m.EVIDENCE/name/'report.json').read_text())['matches']
    summary=summarize(rows);secondary={}
    for opponent in OPPONENTS:
        c=json.loads((m.EVIDENCE/f'{a.candidate_name}-{opponent}'/'report.json').read_text())
        b=json.loads((m.EVIDENCE/f'{a.baseline_name}-{opponent}'/'report.json').read_text())
        cm=json.loads((m.EVIDENCE/f'{a.candidate_name}-{opponent}'/'manifest.json').read_text())
        bm=json.loads((m.EVIDENCE/f'{a.baseline_name}-{opponent}'/'manifest.json').read_text())
        assert cm['runtime']['runtimeSha256']==bm['runtime']['runtimeSha256']
        assert cm.get('extraArgs',[])==bm.get('extraArgs',[])
        secondary[opponent]=dict(candidate=c['summary'],baseline=b['summary'],paired=paired(c['matches'],b['matches']))
    result=dict(duel=summary,duelWin=summary['wins']>summary['losses'],secondary=secondary,baseline=a.baseline_name,
                FullExcluded=True,qualitySuccess='Requires actual useful passing and completed visual check; not automatically inferred')
    (m.EVIDENCE/f'{a.candidate_name}-comparison.json').write_text(json.dumps(result,indent=2));print(json.dumps(result,indent=2))

if __name__=='__main__':main()

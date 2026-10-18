"""Frozen checkpoint duel and strict pass-chain evidence, without promotion."""
import argparse
import json
from collections import Counter
import mng_psr_model as m
from mng_psr_play import summarize
from mng_psr_report import analyze

def main():
    p=argparse.ArgumentParser();p.add_argument('--name',required=True);p.add_argument('--checkpoint',required=True)
    p.add_argument('--duel',nargs='+',required=True);a=p.parse_args();rows=[];groups={};strikes=[]
    for name in a.duel:
        folder=m.EVIDENCE/name;rows+=json.loads((folder/'report.json').read_text())['matches']
        output=folder/'pass-outcomes.json'
        if output.exists() and 'analysisVersion' not in json.loads(output.read_text()):
            historical=folder/'pass-outcomes-v1.json';assert not historical.exists();historical.write_bytes(output.read_bytes())
        result=analyze(folder);output.write_text(json.dumps(result,indent=2))
        for k,v in result['groups'].items():groups.setdefault(k,Counter()).update(v)
        strikes += [dict(s,source=name) for s in result['strikes']]
    summary=summarize(rows);state=m.torch.load(a.checkpoint,map_location='cpu',weights_only=False)
    parent=m.torch.load(m.PARENT,map_location='cpu',weights_only=False)['Policy'];new=state['Policy']
    protected=all(m.torch.equal(v,new[k]) for k,v in parent.items() if 'network_body' in k)
    for suffix in ('weight','bias'):
        k='action_model._discrete_distribution.branches.0.'+suffix
        protected &= m.torch.equal(parent[k][[0,2,3,4,5]],new[k][[0,2,3,4,5]])
    report=dict(checkpoint=a.checkpoint,checkpointSha=m.sha(a.checkpoint),additionalSteps=state['step'],
        parentSha=m.sha(m.PARENT),duel=summary,duelWinCountGate=summary['wins']>summary['losses'],
        parentNonPassTensorsExact=protected,passGroups={k:dict(v) for k,v in groups.items()},strikes=strikes,
        FullExcluded=True,promoted=False,qualitySuccess='Not inferred: useful passing, tactical benefit and visual verification required')
    (m.EVIDENCE/(a.name+'-stage-report.json')).write_text(json.dumps(report,indent=2))
    print(json.dumps({k:v for k,v in report.items() if k!='strikes'},indent=2),flush=True)

if __name__=='__main__':main()

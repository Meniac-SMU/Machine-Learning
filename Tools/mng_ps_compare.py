"""Frozen paired comparison. Does not change registry or infer visual acceptance."""
import argparse
import json
from pathlib import Path
import numpy as np

RULES=('Full','recover','carry-shot','balanced')


def paired_delta(candidate,parent):
    def index(rows):
        d={(r['seed'],r['policyTeam']):r for r in rows}
        assert len(d)==len(rows)
        return d
    c,p=index(candidate),index(parent)
    assert set(c)==set(p)
    seeds=sorted({k[0] for k in c})
    assert all((seed,team) in c for seed in seeds for team in (0,1))
    deltas=np.array([np.mean([c[(s,t)]['score']-p[(s,t)]['score'] for t in (0,1)]) for s in seeds])
    rng=np.random.RandomState(929700)
    samples=deltas[rng.randint(len(deltas),size=(20000,len(deltas)))].mean(axis=1)
    return dict(delta=float(deltas.mean()),pairedSeed95=np.quantile(samples,[.025,.975]).tolist(),
                seeds=len(seeds),samples=samples)


def compare(candidate,parent,duel_extra):
    for key in ('runtimeSha','seconds'):
        assert candidate['manifest'][key]==parent['manifest'][key]
    assert candidate['manifest']['teacher'] is False and parent['manifest']['teacher'] is False
    for report in duel_extra:
        for key in ('candidateSha','runtimeSha','seconds','teacher'):
            assert report['manifest'][key]==candidate['manifest'][key]
    cs,ps=candidate['summary'],parent['summary']
    opponents={}
    for name in ps:
        data=paired_delta([r for r in candidate['matches'] if r['opponent']==name],
                          [r for r in parent['matches'] if r['opponent']==name])
        data.pop('samples')
        opponents[name]=dict(candidate=cs[name],parent=ps[name],pairedComparison=data)
    delta=float(np.mean([opponents[n]['pairedComparison']['delta'] for n in RULES]))
    # Bootstrap independently within each opponent, keeping both sides together.
    # A single RNG stream with separate draws avoids artificial cross-opponent correlation.
    rng=np.random.RandomState(929701);independent=[]
    for name in RULES:
        cr=[r for r in candidate['matches'] if r['opponent']==name]
        pr={(r['seed'],r['policyTeam']):r for r in parent['matches'] if r['opponent']==name}
        seeds=sorted({r['seed'] for r in cr});by={s:[] for s in seeds}
        for r in cr:by[r['seed']].append(r['score']-pr[(r['seed'],r['policyTeam'])]['score'])
        values=np.array([np.mean(by[s]) for s in seeds])
        independent.append(values[rng.randint(len(values),size=(20000,len(values)))].mean(1))
    interval=np.quantile(np.mean(independent,axis=0),[.025,.975]).tolist()
    base=[r for r in candidate['matches'] if r['opponent']=='parent2M']
    assert len(base)==40
    extra=[r for report in duel_extra for r in report['matches']]
    assert all(r['opponent']=='parent2M' for r in extra)
    all_duel=base+extra
    ids=[(r['seed'],r['policyTeam']) for r in all_duel];assert len(set(ids))==len(ids)
    wins=sum(r['score']==1 for r in all_duel);losses=sum(r['score']==0 for r in all_duel)
    base_gap=abs(sum(r['score']==1 for r in base)-sum(r['score']==0 for r in base))/40
    needs_extra=(base_gap<=.05 and len(all_duel)==40) or wins==losses
    next_games=20 if base_gap<=.05 and len(all_duel)==40 else (40 if wins==losses else 0)
    return dict(opponents=opponents,rulesDelta=delta,rulesDeltaPaired95=interval,
        rulesImprovementGate=delta>=.05-1e-9,
        noOpponentRegressionGate=all(v['pairedComparison']['delta']>=-.1-1e-9 for v in opponents.values()),
        duel=dict(matches=len(all_duel),wins=wins,draws=len(all_duel)-wins-losses,losses=losses,
                  pendingAdditionalGames=next_games,complete=not needs_extra,candidateWins=wins>losses and not needs_extra),
        passUsefulnessGate='Requires separate physical-strike and downstream-outcome report',
        visualAcceptance=False,successJudged=False)


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--candidate',required=True);p.add_argument('--parent',required=True)
    p.add_argument('--duel-extra',nargs='*',default=[]);p.add_argument('--output',required=True);a=p.parse_args()
    read=lambda path:json.loads(Path(path).read_text())
    result=compare(read(a.candidate),read(a.parent),[read(x) for x in a.duel_extra])
    with Path(a.output).open('x') as f:json.dump(result,f,indent=2)
    print(json.dumps({k:v for k,v in result.items() if k!='opponents'},indent=2))

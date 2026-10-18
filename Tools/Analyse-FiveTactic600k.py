"""Post-evaluation descriptive diagnosis; no selection or reward changes."""
import json
from collections import Counter
from pathlib import Path

ROOT=Path(__file__).resolve().parent.parent
BASE=ROOT/'Logs/Manager5v5/p9-tactics-20261014/MNG5_V2-20261014-r002'


def read(p): return json.loads(p.read_text(encoding='utf-8-sig'))


def match_diagnosis(folder):
    result=read(folder/'result.json');team=result['candidateTeam']
    terminal=None; decisions=[]
    for line in next(folder.glob('trace-*.jsonl')).open(encoding='utf-8-sig'):
        if '"kind":"terminal"' in line or '"kind":"decision"' in line:
            row=json.loads(line)
            if row['kind']=='terminal' and row['team']==0: terminal=row
            elif row['kind']=='decision' and row['team']==team: decisions.append(row)
    assert terminal is not None
    decisions=[r for r in decisions if r['episode']==terminal['episode']]
    state=lambda r: 0 if sum(r['observation'][5:10])>.5 else 1 if sum(r['observation'][10:15])>.5 else 2
    transitions=[Counter() for _ in range(8)]
    for a,b in zip(decisions,decisions[1:]):
        command=a['action']; transitions[command][f'{state(a)}->{state(b)}']+=1
        diff=(b['redScore']-b['navyScore'])-(a['redScore']-a['navyScore'])
        transitions[command]['nextDecisionGoalDifference']+=diff*(1 if team==0 else -1)
        transitions[command]['intervals']+=1
    commands=[None,None];passes={}
    for event in terminal['matchEvents']:
        kind=event['kind'];actor=event['actor'];pid=event['id']
        if kind=='command':commands[actor]=event['other']
        elif kind=='pass-signal':
            assert pid not in passes
            passes[pid]=dict(team=actor//5,keeper=actor%5==0,origin=commands[actor//5],
                start=event['time'],launch=None,received=None,failure=None,launchCommand=None)
        elif kind=='pass-kicked':
            assert pid in passes
            passes[pid]['launch']=event['time'];passes[pid]['launchCommand']=commands[actor//5]
        elif kind=='pass-received':
            assert pid in passes;passes[pid]['received']=event['time']
        elif kind=='pass-failed':
            assert pid in passes;passes[pid]['failure']=event['reason']
    output=[Counter() for _ in range(8)]
    opponent=[Counter() for _ in range(8)]
    for p in passes.values():
        # GK policy is independent. Diagnose field-player preparation separately.
        if p['keeper']:continue
        target=output if p['team']==team else opponent
        entry=target[p['origin']];entry['signals']+=1
        if p['launch'] is not None:
            entry['launched']+=1;entry['preparationSeconds']+=p['launch']-p['start']
            if p['received'] is not None:entry['received']+=1
            elif p['failure'] is not None:entry['failureAfterLaunch:'+p['failure']]+=1
            else:entry['unresolvedAfterLaunch']+=1
            if p['origin']!=p['launchCommand']:entry['commandChangedBeforeLaunch']+=1
        elif p['failure'] is not None:entry['failureBeforeLaunch:'+p['failure']]+=1
        else:entry['unresolvedBeforeLaunch']+=1
    return dict(fieldPassPreparation=output,opponentFieldPassPreparation=opponent,
                nextDecisionTransitions=transitions)


def main():
    stages=read(BASE/'stage-results.json')
    output=dict(run='MNG5_V2-20261014-r002',stages=[],definitions=dict(
        fieldPassPreparation='Signals are linked by pass id and attributed to the command at preparation start. Independent GK excluded. Unresolved terminal cases remain unresolved.',
        nextDecisionTransitions='Ownership states 0=own,1=opponent,2=free at consecutive actual decisions; command at interval start. Includes goal/restart intervals. Descriptive association, not counterfactual causal value.',
        probability='Mean network probability at actual observed decisions, without changing probabilities or imposing usage targets.'))
    for stage in stages:
        step=stage['step'];rows=[]
        for label,summary in stage['evaluations'].items():
            folder=BASE/f'eval-{step:07d}-{label}'
            cached=folder/'tactic-diagnosis.json'
            if cached.exists():entry=read(cached)
            else:
                merged={key:[Counter() for _ in range(8)] for key in ('fieldPassPreparation','opponentFieldPassPreparation','nextDecisionTransitions')}
                for match in sorted(folder.glob('match-*')):
                    measured=match_diagnosis(match)
                    for key in merged:
                        for i in range(8):merged[key][i].update(measured[key][i])
                entry=dict(opponent=label,games=summary['games'],**merged)
                cached.write_text(json.dumps(entry,ensure_ascii=False,indent=2),encoding='utf-8')
            entry['meanPolicyProbabilitiesByState']=[[v/n if n else None for v in row] for row,n in zip(summary['probabilitySums'],summary['stateCounts'])]
            entry['stateCounts']=summary['stateCounts'];entry['actionCounts']=summary['actionCounts'];rows.append(entry)
        fixed=[r for r in rows if r['opponent']=='strong' or r['opponent'].startswith('old-')]
        merged={key:[Counter() for _ in range(8)] for key in ('fieldPassPreparation','opponentFieldPassPreparation','nextDecisionTransitions')}
        for row in fixed:
            for key in merged:
                for i in range(8):merged[key][i].update(row[key][i])
        totals=[sum(r['stateCounts'][s] for r in fixed) for s in range(3)]
        merged['stateCounts']=totals
        merged['actionCounts']=[[sum(r['actionCounts'][s][i] for r in fixed) for i in range(8)] for s in range(3)]
        merged['meanPolicyProbabilitiesByState']=[[sum((r['meanPolicyProbabilitiesByState'][s][i] or 0)*r['stateCounts'][s] for r in fixed)/totals[s] for i in range(8)] for s in range(3)]
        output['stages'].append(dict(step=step,fixed=merged,opponents=rows))
    path=BASE/'tactic-diagnosis.json';path.write_text(json.dumps(output,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(dict(stages=[s['step'] for s in output['stages']],output=str(path))))


if __name__=='__main__':main()

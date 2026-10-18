"""Independent final-match-only evaluation accounting and command diagnostics."""
import hashlib
import json
from collections import Counter
from pathlib import Path


def load(path): return json.loads(Path(path).read_text(encoding='utf-8-sig'))


def audit(folder, expected_runtime='attack-keeper-v2-reception'):
    folder=Path(folder)
    source_sha=hashlib.sha256((folder/'summary.json').read_bytes()).hexdigest()
    code_sha=hashlib.sha256(Path(__file__).read_bytes()).hexdigest()
    cache=folder/'final-match-audit.json'
    if cache.exists():
        old=load(cache)
        if old['summarySha']==source_sha and old['auditorSha']==code_sha and old.get('expectedRuntime')==expected_runtime: return old
    totals=dict(opponentActionCounts=[0]*8,excludedOpponentDecisions=0,games=0,
                commandEvents=[Counter() for _ in range(8)],candidateCountsVerified=True,
                unmatchedGoalReasons=Counter())
    for match in sorted(folder.glob('match-*')):
        result=load(match/'result.json')
        assert result['passed'] and result['playerRuntime']==expected_runtime
        assert result['ruleVariant']==('strong-v11' if result['opponent']=='rule' else 'none')
        with next(match.glob('trace-*.jsonl')).open(encoding='utf-8-sig') as stream:
            rows=[json.loads(line) for line in stream]
        terminal=[r for r in rows if r['kind']=='terminal' and r['team']==0]
        assert len(terminal)==1
        episode=terminal[0]['episode']; team=result['candidateTeam']
        counts=[[0]*8 for _ in range(3)]; opponent_counts=[0]*8
        for r in rows:
            if r['kind']!='decision': continue
            if r['episode']!=episode:
                if r['team']!=team: totals['excludedOpponentDecisions']+=1
                continue
            if r['team']==team:
                o=r['observation']; state=0 if sum(o[5:10])>.5 else 1 if sum(o[10:15])>.5 else 2
                counts[state][r['action']]+=1
            else: opponent_counts[r['action']]+=1
        assert counts==result['actionCounts'], f'Communicator/Player command mismatch: {match}'
        totals['opponentActionCounts']=[a+b for a,b in zip(totals['opponentActionCounts'],opponent_counts)]
        commands=[None,None]; attempts={}; last_shot=None
        local=[Counter() for _ in range(8)]
        for e in terminal[0]['matchEvents']:
            kind=e['kind']; actor=e['actor']; command=None
            if kind=='command':
                assert actor in (0,1) and 0<=e['other']<8
                commands[actor]=e['other']
                continue
            if kind=='goal':
                if actor==team:
                    if last_shot is not None and last_shot[0]==team:
                        local[last_shot[1]]['shotGoals']+=1
                    else: totals['unmatchedGoalReasons'][e['reason'] or '(no reason)']+=1
                last_shot=None
                continue
            if 0<=actor<10:
                side=actor//5; command=commands[side]
                if kind=='pass-kicked':
                    attempts[e['id']]=(side,command)
                    if side==team: local[command]['passAttempts']+=1
                elif kind=='pass-received':
                    origin_side,origin=attempts[e['id']]
                    if origin_side==team: local[origin]['passSuccesses']+=1
                elif kind=='shot':
                    last_shot=(side,command)
                    if side==team: local[command]['shots']+=1
                elif kind=='pass-failed' and side==team:
                    if e['id'] in attempts:
                        local[attempts[e['id']][1]]['failureAfterLaunch:'+e['reason']]+=1
                    else: local[command]['failureBeforeLaunch:'+e['reason']]+=1
                elif kind in ('pass-signal','shot-failed') and side==team:
                    local[command][kind]+=1
            if kind in ('possession','keeper-clearance','pass-kicked'): last_shot=None
        for metric in ('shots','shotGoals','passAttempts','passSuccesses'):
            assert sum(c[metric] for c in local)==result['candidateMetrics'][metric], (match,metric)
        for i in range(8): totals['commandEvents'][i].update(local[i])
        totals['games']+=1
    totals.update(summarySha=source_sha,auditorSha=code_sha,expectedRuntime=expected_runtime,passed=True,
                  definitions='Only the terminated episode. Pass success/failure attributed to launch command; shots/goals to shot command. Descriptive attribution, not causal credit.')
    cache.write_text(json.dumps(totals,ensure_ascii=False,indent=2),encoding='utf-8')
    return totals

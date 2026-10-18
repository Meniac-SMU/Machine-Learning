"""Independent final-episode accounting for the v9 all-pairs evaluation."""
import csv
import hashlib
import json
from pathlib import Path
import numpy as np

ROOT=Path(__file__).resolve().parent.parent
BASE=ROOT/'Logs/Manager5v5/keeper-accel12-v9-20261016/round-robin'
OUT=ROOT/'outputs/v9-round-robin-20261016'
COMMANDS=['T1 속공 전개','T2 전방 롱패스','T3 와이드 탈압박','T4 측면 컷백','T5 진로 차단 수비','T6 수신자 봉쇄','T7 전체 공 회수','T8 골문 앞 집결']
def read(p): return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def rate(a,b): return a/b if b else None
def aggregate(rows):
    w=sum(x['result']=='승' for x in rows);d=sum(x['result']=='무' for x in rows);l=sum(x['result']=='패' for x in rows)
    result=dict(games=len(rows),wins=w,draws=d,losses=l,points=3*w+d)
    for k in ['goalsFor','goalsAgainst','shots','shotGoals','passAttempts','passSuccesses','decisions']:result[k]=sum(x[k] for x in rows)
    result.update(goalDifference=result['goalsFor']-result['goalsAgainst'],winRate=rate(w,len(rows)),shotSuccessRate=rate(result['shotGoals'],result['shots']),passSuccessRate=rate(result['passSuccesses'],result['passAttempts']))
    result['commands']=[sum(x['commands'][i] for x in rows) for i in range(8)]
    return result

def main():
    assert read(BASE/'status.json')['state']=='complete'
    protocol=read(BASE/'protocol.json');pairs=read(BASE/'pair-results.json');assert len(pairs)==28
    names={x['id']:x['name'] for x in protocol['models']};rows=[];sources=[];seen=set()
    for pair in pairs:
        folder=BASE/pair['label'];assert read(folder/'final-match-audit.json')['passed']
        local=[]
        for match in sorted(folder.glob('match-*')):
            result=read(match/'result.json');assert result['passed'] and result['exitCode']==0 and result['playerRuntime']==protocol['runtime']
            traces=[json.loads(x) for x in next(match.glob('trace-*.jsonl')).read_text(encoding='utf-8-sig').splitlines()]
            terminal=[x for x in traces if x['kind']=='terminal' and x['team']==0];assert len(terminal)==1
            terminal=terminal[0];assert 300<=terminal['elapsed']<301
            decisions=[x for x in traces if x['kind']=='decision' and x['episode']==terminal['episode']]
            key=(pair['pair'],result['seed'],result['candidateTeam']);assert key not in seen;seen.add(key)
            game=(pair['pair']-1)*40+int(match.name.split('-')[1])+1
            for side in (0,1):
                is_candidate=side==result['candidateTeam'];model=pair['candidate'] if is_candidate else pair['opponent'];opponent=pair['opponent'] if is_candidate else pair['candidate']
                counts=[0]*8;streak=[0,0];blocked=[0,0]
                for decision in decisions:
                    if decision['team']!=side:continue
                    a=decision['action'];counts[a]+=1
                    for i,c in enumerate([1,6]):
                        if a==c:
                            assert blocked[i]==0;streak[i]+=1;assert streak[i]<=2
                            if streak[i]==2:blocked[i]=2
                        else:streak[i]=0;blocked[i]=max(0,blocked[i]-1)
                if is_candidate:assert counts==np.array(result['actionCounts']).sum(axis=0).tolist()
                else:assert counts==result['opponentActionCounts']
                metric=result['candidateMetrics'] if is_candidate else result['opponentMetrics']
                gf=terminal['redScore'] if side==0 else terminal['navyScore'];ga=terminal['navyScore'] if side==0 else terminal['redScore']
                assert metric['goals']==gf
                row=dict(game=game,pair=pair['pair'],seed=result['seed'],side='RED' if side==0 else 'NAVY',model=model,modelName=names[model],opponent=opponent,opponentName=names[opponent],result='승' if gf>ga else '패' if gf<ga else '무',goalsFor=gf,goalsAgainst=ga,shots=metric['shots'],shotGoals=metric['shotGoals'],shotSuccessRate=rate(metric['shotGoals'],metric['shots']),passAttempts=metric['passAttempts'],passSuccesses=metric['passSuccesses'],passSuccessRate=rate(metric['passSuccesses'],metric['passAttempts']),decisions=sum(counts),commands=counts,commandRates=[rate(x,sum(counts)) for x in counts])
                rows.append(row);local.append(row)
            sources.append(dict(game=game,path=str(match/'result.json'),sha256=sha(match/'result.json')))
        assert len(local)==80
        check=aggregate([x for x in local if x['model']==pair['candidate']])
        for k in ['wins','draws','losses','goalsFor','goalsAgainst']:assert check[k]==pair['summary'][k]
    assert len(rows)==2240 and len(seen)==1120
    totals=[dict(id=m,model=names[m],**aggregate([x for x in rows if x['model']==m])) for m in names]
    assert all(x['games']==280 for x in totals)
    assert sum(x['wins'] for x in totals)==sum(x['losses'] for x in totals)
    assert sum(x['goalsFor'] for x in totals)==sum(x['goalsAgainst'] for x in totals)
    totals.sort(key=lambda x:(-x['points'],-x['goalDifference'],-x['goalsFor']))
    for i,x in enumerate(totals):
        tied=i>0 and all(x[k]==totals[i-1][k] for k in ('points','goalDifference','goalsFor'))
        x['rank']=totals[i-1]['rank'] if tied else i+1
    pair_rows=[]
    for pair in pairs:
        for m,o in [(pair['modelA'],pair['modelB']),(pair['modelB'],pair['modelA'])]:
            pair_rows.append(dict(pair=pair['pair'],id=m,opponentId=o,model=names[m],opponent=names[o],**aggregate([x for x in rows if x['model']==m and x['opponent']==o])))
    # Resample the 20 paired seeds as clusters, keeping every matchup and both sides together.
    ids=[x['id'] for x in totals];seeds=protocol['seeds']
    seed_points=np.array([[sum(3 if r['result']=='승' else 1 if r['result']=='무' else 0 for r in rows if r['model']==m and r['seed']==s) for m in ids] for s in seeds])
    rng=np.random.default_rng(76116);boots=seed_points[rng.integers(0,20,size=(10000,20))].sum(axis=1)
    gap=boots[:,0]-boots[:,1]
    uncertainty=dict(method='10,000 cluster bootstrap samples of 20 paired seeds; all pairs and both sides stay together',seed=76116,top=ids[0],runnerUp=ids[1],observedPointGap=totals[0]['points']-totals[1]['points'],pointGap95CI=np.quantile(gap,[.025,.975]).tolist(),strictFirstPlaceFraction=float(np.mean(boots[:,0]>np.max(boots[:,1:],axis=1))))
    for m in protocol['models'][1:]:assert sha(Path(m['path']))==m['sha256']
    OUT.mkdir(parents=True,exist_ok=True)
    data=dict(protocol=protocol,commandNames=COMMANDS,ranking=totals,pairSummaries=pair_rows,teamRows=rows,uncertainty=uncertainty,validation=dict(passed=True,games=1120,teamRows=2240,models=8,pairs=28,gamesPerPair=40,gamesPerModel=280,modelHashesUnchanged=True,finalEpisodeOnly=True,commandCooldownAudit=True),sources=sources)
    (OUT/'evaluation-data.json').write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
    cols=['game','pair','seed','side','modelName','opponentName','result','goalsFor','goalsAgainst','shots','shotGoals','shotSuccessRate','passAttempts','passSuccesses','passSuccessRate','decisions']
    with (OUT/'경기별_전체지표.csv').open('w',encoding='utf-8-sig',newline='') as f:
        writer=csv.writer(f);writer.writerow(['경기 ID','대진 ID','시드','진영','모델','상대 모델','승무패','득점','실점','슈팅 수','슛 득점','슈팅 성공률 (0~1)','패스 수','성공 패스','패스 성공률 (0~1)','감독 명령 수']+[c+' 횟수' for c in COMMANDS]+[c+' 빈도 (0~1)' for c in COMMANDS])
        for row in rows:writer.writerow([row[k] for k in cols]+row['commands']+row['commandRates'])
    (BASE/'final-verification.json').write_text(json.dumps(dict(**data['validation'],uncertainty=uncertainty),ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(dict(ranking=totals,uncertainty=uncertainty),ensure_ascii=False))

if __name__=='__main__':main()

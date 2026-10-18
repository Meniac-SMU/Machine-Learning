"""Stage/opponent action use and actual shot/pass outcomes, with fixed-opponent comparisons."""
import json
import random
from pathlib import Path
from five_evaluation_audit import audit
from five_match_metrics import aggregate
from five_tactic_report_stats import match_rows, usage_interval, paired_strength

ROOT=Path(__file__).resolve().parent.parent
RUN='MNG5_V2-20261014-r002'
BASE=ROOT/'Logs/Manager5v5/p9-tactics-20261014'/RUN
OUTPUT=ROOT/'docs/soccer/rebuild-5v5/41_전술개편_셀프플레이50만_단계별결과.md'
COMMANDS=['T1 속공 전개','T2 전방 롱패스','T3 와이드 탈압박','T4 측면 컷백','T5 진로 차단 수비','T6 수신자 봉쇄','T7 전체 공 회수','T8 골문 앞 집결']
def read(p):return json.loads(Path(p).read_text(encoding='utf-8-sig'))
def pct(x):return '해당 없음' if x is None else f'{100*x:.2f}%'
def stage(s):return '초기' if s==0 else f'{s:,}회 (임시 최종)' if s==505386 else f'{s//10000}만'
def opponent(s):
    if s=='strong':return '규칙형 strong-v11'
    if s.startswith('old-'):return '이전 Run '+stage(int(s[4:]))
    if s.startswith('stage-'):return '이번 Run '+stage(int(s[6:]))
    return '초기 P9 200,011'
def counts(s):return [sum(row[i] for row in s['actionCounts']) for i in range(8)]
def main():
    status=read(BASE/'orchestrator-status.json')
    stages=read(BASE/'stage-results.json') if (BASE/'stage-results.json').exists() else []
    data=dict(run=RUN,state=status['state'],operation=status.get('operation'),stages=[],rows=[],baselineComparison=None)
    lines=['# 전술 개편 셀프 플레이 50만 임시 종료·단계별 결과','',
           f'Run `{RUN}`. 현재 상태 `{status["state"]}`. 사용자 최신 지시로 원래 중단본 **505,386회 모델**을 최종 임시 버전으로 선택했다. 추가 학습은 중단했다. 120만 장기 목표 달성 판정과 구분한다.',
           '50만 중단 신호 처리 당시 기존 실행은 505,386회였다. 정확한 50만을 만들기 위해 40만 저장본에서 재개했던 처리는 사용자 정정에 따라 471,854회에서 중단했고, 이 재학습 결과는 채택하지 않았다. 원래 505,386회 모델·optimizer·상대 풀을 보존본과 해시 대조 후 복원하여 평가만 수행했다. 두 중단 기록과 모델을 모두 보존했다. 최신 선택 근거는 `provisional-selection.json`이며 `cap-change-500000.json`의 정확한 50만 재개 계획을 대체한다.',
           '새 초기화 셀프 플레이, 득점 +1·실점 -1만, 300초·배속 100·32환경. 강제 선택·추가 보상 없음. 후보 실행은 `tactics-v3-expansion`이며 모든 상대에게 같은 실행 코드를 적용한다.','',
           '아래 단계 합산은 규칙형과 이전 완료 Run의 0·20·40·60·80·100·120만 모델 8종만 같은 경기 수로 포함한다. 이번 Run의 이전 단계 및 초기 P9 보충 비교를 합산 분모에 추가하지 않는다. 이로써 단계마다 상대 구성 변화가 사용률 추세를 바꾸는 문제를 피한다.','',
           '| 단계 | 고정 상대 경기 | 승/무/패 | 득실점 | 슈팅/슈팅 득점 | 슈팅 성공률 | 패스/안정 수신 | 패스 성공률 |',
           '|---|---:|---:|---:|---:|---:|---:|---:|']
    for entry in stages:
        fixed=[s for label,s in entry['evaluations'].items() if label=='strong' or label.startswith('old-')]
        assert len(fixed)==8 and all(s['passed'] and s['games']==24 for s in fixed)
        action=[sum(counts(s)[i] for s in fixed) for i in range(8)]; total=sum(action); ratios=[n/total for n in action]
        metrics=aggregate([s['candidateMetrics'] for s in fixed])
        summ=dict(step=entry['step'],games=sum(s['games'] for s in fixed),wins=sum(s['wins'] for s in fixed),
                  draws=sum(s['draws'] for s in fixed),losses=sum(s['losses'] for s in fixed),
                  goalsFor=sum(s['goalsFor'] for s in fixed),goalsAgainst=sum(s['goalsAgainst'] for s in fixed),
                  actionCounts=action,actionRatios=ratios,metrics=metrics,
                  withinTarget=all(.015<=r<=.5 for r in ratios),
                  reviewFlags=[COMMANDS[i] for i,r in enumerate(ratios) if r<.015 or r>.5])
        labels=[label for label in entry['evaluations'] if label=='strong' or label.startswith('old-')]
        summ['usageUncertainty']=usage_interval(match_rows(BASE,'eval',entry['step'],labels))
        data['stages'].append(summ)
        lines.append(f'| {stage(entry["step"])} | {summ["games"]} | {summ["wins"]}/{summ["draws"]}/{summ["losses"]} | {summ["goalsFor"]}:{summ["goalsAgainst"]} | {metrics["shots"]}/{metrics["shotGoals"]} | {pct(metrics["shotConversionRate"])} | {metrics["passAttempts"]}/{metrics["passSuccesses"]} | {pct(metrics["passSuccessRate"])} |')
        for label,s in entry['evaluations'].items():
            checked=audit(BASE/f'eval-{entry["step"]:07d}-{label}',expected_runtime='tactics-v3-expansion')
            data['rows'].append(dict(step=entry['step'],opponent=label,summary=s,audit=checked))
    lines+=['','## 고정 상대 집합의 명령별 선택 비율','','| 명령 | '+' | '.join(stage(s['step']) for s in data['stages'])+' |','|---|'+'---:|'*len(data['stages'])]
    for i,name in enumerate(COMMANDS):lines.append('| '+name+' | '+' | '.join(pct(s['actionRatios'][i]) for s in data['stages'])+' |')
    lines+=['','1.5%·50%는 관측 결과의 검토 기준이며 선택을 제한하는 규칙이 아니다. 임시 최종 505,386회 결과를 120만 목표의 최종 성공/실패로 바꾸지 않는다.','']
    if data['stages']:
        latest=data['stages'][-1]
        lines+=['## 최신 완료 단계 선택 비율의 불확실성','','동일 policy-sampling seed의 양 진영과 고정 상대 전체를 묶어 12개 블록을 10,000회 재표집했다. 개별 결정을 독립 표본으로 취급하지 않는다. 한 학습 초기화의 평가 구간이며 다른 초기화 재현성을 뜻하지 않는다.','','| 명령 | 관측 비율 | 95% 구간 |','|---|---:|---|']
        for i,name in enumerate(COMMANDS):
            lo,hi=latest['usageUncertainty']['intervals95'][i]
            lines.append(f'| {name} | {pct(latest["actionRatios"][i])} | {pct(lo)} ~ {pct(hi)} |')
        lines+=['']
    if (BASE/'baseline-results.json').exists() and data['stages']:
        comparisons=[];labels=list(read(BASE/'baseline-results.json'))
        latest=data['stages'][-1]['step']
        for label in labels:
            maps=[]
            for prefix,step in [('eval',latest),('baseline',1200000)]:
                grouped={}
                for p in sorted((BASE/f'{prefix}-{step:07d}-{label}').glob('match-*/result.json')):
                    r=read(p);d=r['score']['own']-r['score']['other'];v=(d,1 if d>0 else .5 if d==0 else 0)
                    grouped.setdefault(r['seed'],[]).append(v)
                assert len(grouped)==12 and all(len(v)==2 for v in grouped.values())
                maps.append({seed:tuple(sum(v[i] for v in values)/2 for i in (0,1)) for seed,values in grouped.items()})
            assert maps[0].keys()==maps[1].keys()
            differences=[tuple(maps[0][seed][i]-maps[1][seed][i] for i in (0,1)) for seed in sorted(maps[0])]
            rng=random.Random(63101);boot=sorted(sum(v[0] for v in rng.choices(differences,k=len(differences)))/len(differences) for _ in range(10000))
            comparisons.append(dict(opponent=label,goalDifferenceChange=sum(v[0] for v in differences)/len(differences),
                scoreRateChange=sum(v[1] for v in differences)/len(differences),pairedSeedBootstrap95=[boot[249],boot[9749]]))
        data['baselineComparison']=comparisons
        lines+=['## 기존 120만 정책과 같은 실행에서 비교','','새 정책과 기존 정책을 모두 개선된 선수 실행에 연결하고 같은 상대·seed·양 진영으로 비교했다. 평균 득실차와 승점률 변화는 새 정책 기준이며 95% 구간은 12개 seed 쌍 재표집이다. 경기력 향상 주장은 상대별 결과와 구간을 함께 검토한다.','','| 상대 | 경기당 득실차 변화 | 95% 구간 | 승점률 변화 |','|---|---:|---|---:|']
        for row in comparisons:lines.append(f'| {opponent(row["opponent"])} | {row["goalDifferenceChange"]:+.3f} | {row["pairedSeedBootstrap95"]} | {row["scoreRateChange"]*100:+.2f}%p |')
        if len(labels)==8:
            data['aggregateStrengthComparison']={}
            for name,subset in [('fixedEight',labels),('majorOpponents',['strong','old-0800000','old-1200000'])]:
                effect=paired_strength(match_rows(BASE,'eval',latest,subset),match_rows(BASE,'baseline',1200000,subset))
                data['aggregateStrengthComparison'][name]=effect
                label='고정 8종 전체' if name=='fixedEight' else '주요 상대 3종: 규칙형·이전80만·이전120만'
                lines+=['',f'{label}: 경기당 득실차 변화 {effect["goalDifferenceChange"]:+.3f}, 95% 구간 {effect["goalDifference95"]}; 승점률 변화 {effect["scoreRateChange"]*100:+.2f}%p, 95% 구간 {[round(v*100,3) for v in effect["scoreRate95"]]}%p. 동일 seed의 양 진영과 모든 해당 상대를 함께 재표집했다.']
    lines+=['','## 단계·상대별 상세','','선택 횟수는 완료된 경기의 통신 기록과 Player 기록을 대조했다. 패스 성공은 발사 당시 명령, 슈팅 득점은 해당 슈팅 당시 명령에 연결한다. 이 연결은 단독 인과 효과를 뜻하지 않는다.','']
    for row in data['rows']:
        s=row['summary'];c=counts(s);total=sum(c);m=s['candidateMetrics'];events=row['audit']['commandEvents']
        lines += [f'### {stage(row["step"])} 대 {opponent(row["opponent"])}','',
            f'{s["wins"]}승 {s["draws"]}무 {s["losses"]}패, 득실점 {s["goalsFor"]}:{s["goalsAgainst"]}. 슈팅 {m["shots"]}회·슈팅 득점 {m["shotGoals"]}회 ({pct(m["shotConversionRate"])}), 패스 {m["passAttempts"]}회·안정 수신 {m["passSuccesses"]}회 ({pct(m["passSuccessRate"])}).','',
            '| 명령 | 선택/비율 | 실행 초/비율 | 슈팅/득점 | 패스/성공 | 준비 신호 |','|---|---:|---:|---:|---:|---:|']
        for i,name in enumerate(COMMANDS):
            e=events[i];execution=s['executionSteps'];lines.append(f'| {name} | {c[i]} / {pct(c[i]/total)} | {execution[i]*.01:.2f} / {pct(execution[i]/sum(execution))} | {e.get("shots",0)}/{e.get("shotGoals",0)} | {e.get("passAttempts",0)}/{e.get("passSuccesses",0)} | {e.get("pass-signal",0)} |')
        lines+=['','| 명령 | 우리 공 선택/비율 | 상대 공 선택/비율 | 자유 공 선택/비율 |','|---|---:|---:|---:|']
        for i,name in enumerate(COMMANDS):
            cells=[f'{s["actionCounts"][state][i]} / {pct(s["actionCounts"][state][i]/s["stateCounts"][state] if s["stateCounts"][state] else None)}' for state in range(3)]
            lines.append('| '+name+' | '+' | '.join(cells)+' |')
        lines+=['',f'증거: `Logs/Manager5v5/p9-tactics-20261014/{RUN}/eval-{row["step"]:07d}-{row["opponent"]}`.','']
    data['games']=sum(r['summary']['games'] for r in data['rows'])
    (BASE/'report-data.json').write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
    OUTPUT.write_text('\n'.join(lines)+'\n',encoding='utf-8')
    print(json.dumps(dict(state=data['state'],stages=[s['step'] for s in data['stages']],games=data['games'],output=str(OUTPUT)),ensure_ascii=False))
if __name__=='__main__':main()

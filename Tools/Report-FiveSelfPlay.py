"""Summarize completed frozen evaluations; never changes the running experiment."""
import json
import math
import random
from pathlib import Path
from five_evaluation_audit import audit

ROOT = Path(__file__).resolve().parent.parent
RUN = 'MNG5_V2-20261014-r001'
BASE = ROOT/'Logs/Manager5v5/p9-selfplay-20261014'/RUN
OUTPUT = ROOT/'docs/soccer/rebuild-5v5/36_P9_셀프플레이120만_단계별결과.md'
COMMANDS = ['T1 속공 전개', 'T2 전방 롱패스', 'T3 와이드 탈압박', 'T4 측면 컷백',
            'T5 진로 차단 수비', 'T6 수신자 봉쇄', 'T7 전체 공 회수', 'T8 골문 앞 집결']
STATES = ['우리 공', '상대 공', '자유 공']


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def pct(value):
    return '미정' if value is None else f'{100*value:.2f}%'


def ratio(n, d):
    return n/d if d else None


def stage_name(step):
    return '0 (초기)' if step == 0 else f'{step//10000}만'


def opponent_name(label):
    return '규칙형 strong-v11' if label == 'strong' else stage_name(int(label.split('-')[1]))


def paired_growth(steps):
    measured={}
    for step in steps:
        pairs={}
        for path in sorted((BASE/f'eval-{step:07d}-strong').glob('match-*/result.json')):
            r=read(path); pairs.setdefault(r['seed'],[]).append(r['score']['own']-r['score']['other'])
        assert len(pairs)==12 and all(len(x)==2 for x in pairs.values())
        measured[step]={s:sum(x)/2 for s,x in pairs.items()}
    result=[]
    for step in steps:
        values=[measured[step][s]-measured[0][s] for s in sorted(measured[0])]
        rng=random.Random(19014)
        samples=sorted(sum(rng.choices(values,k=len(values)))/len(values) for _ in range(10000))
        result.append(dict(step=step,meanGoalDifferenceChange=sum(values)/len(values),
                           pairedSeedBootstrap95=[samples[249],samples[9749]],seedPairs=12,
                           interpretation='Exploratory paired-seed comparison with initial model against frozen strong-v11; not a promotion gate.'))
    return result


def main():
    status = read(BASE/'orchestrator-status.json')
    stages = read(BASE/'stage-results.json') if (BASE/'stage-results.json').exists() else []
    rows = []
    for stage in stages:
        for opponent, summary in stage['evaluations'].items():
            match_audit=audit(BASE/f'eval-{stage["step"]:07d}-{opponent}')
            summary=dict(summary,opponentActionCounts=match_audit['opponentActionCounts'])
            counts = [sum(state[i] for state in summary['actionCounts']) for i in range(8)]
            total = sum(counts)
            execution = summary['executionSteps']
            assert total == sum(summary['stateCounts'])
            assert summary['games'] == summary['wins']+summary['draws']+summary['losses'] == 24
            assert summary['candidateMetrics']['goals'] == summary['goalsFor']
            assert summary['opponentMetrics']['goals'] == summary['goalsAgainst']
            commands = []
            for i, name in enumerate(COMMANDS):
                commands.append(dict(name=name, decisions=counts[i], decisionRatio=ratio(counts[i],total),
                    seconds=execution[i]*.01, executionRatio=ratio(execution[i],sum(execution)),
                    states=[dict(name=STATES[s], count=summary['actionCounts'][s][i],
                                 conditionalRatio=ratio(summary['actionCounts'][s][i],summary['stateCounts'][s]),
                                 meanPolicyProbability=ratio(summary['probabilitySums'][s][i],summary['stateCounts'][s]))
                            for s in range(3)]))
            # A review flag only, not a quality gate or a target distribution.
            flagged=[c['name'] for c in commands if c['decisionRatio']>.5 or c['decisionRatio']<.02]
            entropy=-sum(c['decisionRatio']*math.log(c['decisionRatio']) for c in commands if c['decisionRatio'])/math.log(8)
            rows.append(dict(step=stage['step'], opponent=opponent, summary=summary,
                             commands=commands, reviewFlags=flagged, normalizedUsageEntropy=entropy,
                             finalMatchAudit=match_audit))
    data=dict(run=RUN, state=status['state'], operation=status.get('operation'),
              completedStages=[s['step'] for s in stages], games=sum(r['summary']['games'] for r in rows),
              reviewFlagDefinition='Observed use >50% or <2%; descriptive only, not failure or a uniformity target.',
              rows=rows)
    data['growthVsInitial']=paired_growth(data['completedStages']) if stages else []
    supplementary=BASE/'supplemental/status.json'
    data['supplementary']=read(supplementary) if supplementary.exists() else {'state':'not-run','completed':[]}
    (BASE/'report-data.json').write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
    lines=['# P9 셀프 플레이 120만 단계별 결과', '',
           f'Run: `{RUN}`. 상태: **{status["state"]}**. 완료된 평가만 아래에 포함한다. 진행 중인 단계의 성적을 완료로 계산하지 않는다.', '',
           f'현재 완료 필수 평가: **{data["games"]}경기**, 보충 평가: **{sum(r["summary"]["games"] for r in data["supplementary"]["completed"])}경기**. 선수 `attack-keeper-v2-reception`, 규칙형 `strong-v11`, 보상 득점 +1·실점 −1·나머지 0.', '',
           '학습은 새 초기 정책의 32환경 셀프 플레이이며 300초 경기를 time scale 100으로 실행한다. 단계는 감독 학습 결정 수다. 평가마다 12개 seed의 양 진영, 상대별 24경기를 사용한다. 규칙형과 이번 Run의 모든 이전 20만 단계를 상대한다. 표준 킥오프에서의 확률적 정책 평가이며 다른 시작 배치까지 일반화되었다는 뜻은 아니다.', '',
           '## 단계·상대별 경기와 기술 지표', '',
           '슈팅 성공은 실제 슛 뒤에 다른 소유·패스·걷어내기 없이 이어진 득점/슛이다. 총득점과 슈팅 득점은 다를 수 있다. 패스 성공은 실제 발사 뒤 지정 수신자의 안정 소유이며 준비 신호·취소를 제외한다. 성공률은 경기별 비율의 평균이 아니라 합산 성공/합산 시도다.', '',
           '| 단계 | 상대 | 승/무/패 | 득점:실점 | 슛 | 슛 득점/성공률 | 패스 성공/시도 | 패스 성공률 |',
           '|---|---|---:|---:|---:|---:|---:|---:|']
    for row in rows:
        s=row['summary']; m=s['candidateMetrics']
        lines.append(f'| {stage_name(row["step"])} | {opponent_name(row["opponent"])} | {s["wins"]}/{s["draws"]}/{s["losses"]} | {s["goalsFor"]}:{s["goalsAgainst"]} | {m["shots"]} | {m["shotGoals"]} / {pct(m["shotConversionRate"])} | {m["passSuccesses"]}/{m["passAttempts"]} | {pct(m["passSuccessRate"])} |')
    lines += ['', '규칙형 상대 초기 대비 경기당 득실차 변화. 양 진영을 같은 seed의 한 쌍으로 묶은 12쌍, 10,000회 bootstrap의 탐색적 95% 구간이다. 사전 승격 기준이나 다른 초기 배치의 보장은 아니다.', '',
              '| 단계 | 초기 대비 득실차 변화(골/경기) | 대응 seed 95% 구간 |', '|---|---:|---:|']
    for g in data['growthVsInitial']:
        lo,hi=g['pairedSeedBootstrap95']
        lines.append(f'| {stage_name(g["step"])} | {g["meanGoalDifferenceChange"]:+.3f} | [{lo:+.3f}, {hi:+.3f}] |')
    lines+=['', '## 명령 횟수·비율과 실제 실행 시간', '',
            '감독의 반복 선택도 결정 횟수에 포함한다. 실행 시간은 0.01초 물리 스텝 누적이며 결정 횟수와 별개다. 각 명령은 번호와 정식 이름을 함께 표기한다. 상대 감독의 횟수·비율도 같은 평가에서 기록한다.', '',
            '독립 재집계에서 완료된 경기 식별자만 사용한다. Player 종료 직전 다음 경기의 명령이 원시 상대 횟수에 포함되는 현상을 발견하여 보고서에서 제외했다. 원본 결과는 보존하며, 학습 감독의 상황별 선택 횟수는 통신 기록과 Player 기록의 정확한 일치를 검사한다.', '',
            '사용률 50% 초과 또는 2% 미만은 원인을 살펴볼 표시일 뿐 합격 기준이 아니다. 균등 사용을 요구하거나 별도 보상을 추가하지 않는다. 상황별 선택 비율과 평균 정책 확률을 함께 보아 상황의 빈도와 정책 선호를 구분한다. 이 통계만으로 특정 전술의 인과적 우열을 확정하지 않는다.', '']
    for row in rows:
        s=row['summary']; total=sum(s['stateCounts']); opponent_total=sum(s['opponentActionCounts'])
        lines += [f'### {stage_name(row["step"])} — {opponent_name(row["opponent"])}', '',
                  '상황 분포: '+', '.join(f'{name} {s["stateCounts"][i]}회 ({pct(ratio(s["stateCounts"][i],total))})' for i,name in enumerate(STATES))+'.', '',
                  '| 명령 | 선택 횟수 | 선택 비율 | 실제 실행(초) | 실행 비율 | 상대 선택 횟수/비율 |',
                  '|---|---:|---:|---:|---:|---:|']
        for i,c in enumerate(row['commands']):
            lines.append(f'| {c["name"]} | {c["decisions"]} | {pct(c["decisionRatio"])} | {c["seconds"]:.2f} | {pct(c["executionRatio"])} | {s["opponentActionCounts"][i]} / {pct(ratio(s["opponentActionCounts"][i],opponent_total))} |')
        lines += ['', '상황별 `선택 횟수 / 상황 내 선택 비율 / 평균 정책 확률`:', '',
                  '| 명령 | 우리 공 | 상대 공 | 자유 공 |', '|---|---:|---:|---:|']
        for c in row['commands']:
            cells=[f'{x["count"]} / {pct(x["conditionalRatio"])} / {pct(x["meanPolicyProbability"])}' for x in c['states']]
            lines.append('| '+c['name']+' | '+' | '.join(cells)+' |')
        m=s['opponentMetrics']
        lines += ['', f'상대 기술 지표: 슛 {m["shots"]}, 슛 득점 {m["shotGoals"]} ({pct(m["shotConversionRate"])}), 패스 {m["passSuccesses"]}/{m["passAttempts"]} ({pct(m["passSuccessRate"])}).', '',
                  '사용률 검토 표시: '+(' · '.join(row['reviewFlags']) if row['reviewFlags'] else '해당 없음')+'.', '',
                  '| 명령 | 실제 슛/슛 득점 | 실제 패스/안정 수신 | 패스 준비 신호 |',
                  '|---|---:|---:|---:|']
        for i,name in enumerate(COMMANDS):
            e=row['finalMatchAudit']['commandEvents'][i]
            lines.append(f'| {name} | {e.get("shots",0)}/{e.get("shotGoals",0)} | {e.get("passAttempts",0)}/{e.get("passSuccesses",0)} | {e.get("pass-signal",0)} |')
        lines += ['', '기술 사건은 발사 당시 명령에 귀속한다. 패스 성공도 발사 당시 명령에 연결한다. 이는 시간상 연결이며 해당 명령만의 인과적 효과라는 뜻은 아니다. 준비 실패 이유별 수치는 `final-match-audit.json`에 보존한다.', '',
                  f'원자료: `Logs/Manager5v5/p9-selfplay-20261014/{RUN}/eval-{row["step"]:07d}-{row["opponent"]}/summary.json` 및 경기별 `result.json`, `trace-*.jsonl`.', '']
    if data['supplementary']['state']=='complete':
        labels={'final-vs-recover':'120만 모델 대 T7 전체 공 회수 고정',
                'recover-vs-strong':'T7 전체 공 회수 고정 대 규칙형 strong-v11',
                'final-vs-old-p9':'120만 모델 대 이전 P9 최종200,011 모델'}
        lines += ['## 사전 지정한 보충 비교', '',
                  '[보충 평가 계획](37_전술편중_보충평가_계획.md)의 세 비교, 각24경기다. 고정 명령 감독의 결과는 학습 모델의 성능과 구분한다.', '']
        for row in data['supplementary']['completed']:
            s=row['summary']; m=s['candidateMetrics']; o=s['opponentMetrics']; checked=row['audit']
            counts=[sum(x[i] for x in s['actionCounts']) for i in range(8)]
            total=sum(counts); execution=sum(s['executionSteps'])
            opponent_total=sum(checked['opponentActionCounts'])
            lines += ['### '+labels[row['label']], '',
                      f'후보 관점: {s["wins"]}승 {s["draws"]}무 {s["losses"]}패, 득실점 {s["goalsFor"]}:{s["goalsAgainst"]}.', '',
                      f'후보: 슛 {m["shots"]}회·슈팅 득점 {m["shotGoals"]}회 ({pct(m["shotConversionRate"])}), 패스 {m["passSuccesses"]}/{m["passAttempts"]} ({pct(m["passSuccessRate"])}).', '',
                      f'상대: 슛 {o["shots"]}회·슈팅 득점 {o["shotGoals"]}회 ({pct(o["shotConversionRate"])}), 패스 {o["passSuccesses"]}/{o["passAttempts"]} ({pct(o["passSuccessRate"])}).', '',
                      '| 명령 | 후보 선택 횟수/비율 | 후보 실행(초)/비율 | 상대 선택 횟수/비율 |', '|---|---:|---:|---:|']
            for i,name in enumerate(COMMANDS):
                lines.append(f'| {name} | {counts[i]} / {pct(ratio(counts[i],total))} | {s["executionSteps"][i]*.01:.2f} / {pct(ratio(s["executionSteps"][i],execution))} | {checked["opponentActionCounts"][i]} / {pct(ratio(checked["opponentActionCounts"][i],opponent_total))} |')
            lines += ['', '| 명령 | 우리 공 선택/비율 | 상대 공 선택/비율 | 자유 공 선택/비율 |', '|---|---:|---:|---:|']
            for i,name in enumerate(COMMANDS):
                cells=[f'{s["actionCounts"][state][i]} / {pct(ratio(s["actionCounts"][state][i],s["stateCounts"][state]))}' for state in range(3)]
                lines.append('| '+name+' | '+' | '.join(cells)+' |')
            lines+=['',f'원자료: `Logs/Manager5v5/p9-selfplay-20261014/{RUN}/supplemental/{row["label"]}`.', '']
    lines += ['## 증거와 해석 경계', '',
              '원본 보존, 수신 결함 원인, 검사 기대 분리, 일반 PlayMode 105/105 및 EditMode 27/27은 [버전 고정 보고](35_수신결함_수정과_셀프플레이_동결.md)를 참조한다. [실행 계획](34_최신규칙_셀프플레이120만_실행계획.md)의 조건을 유지한다.', '',
              f'단계별 PT/ONNX 검사, optimizer/상대 풀, 보상·관측·경기 종료 검사는 `Logs/Manager5v5/p9-selfplay-20261014/{RUN}`에 보관한다. 완료 판정은 `orchestrator-status.json` 및 각 단계 `safe-stop.json`, 평가 원자료와 대조한다. 낮은 성적을 성공으로 바꾸거나 실패 증거를 삭제하지 않는다. P10 시연 연결 및 champion 승격은 별도다.', '']
    OUTPUT.write_text('\n'.join(lines),encoding='utf-8')
    print(json.dumps(dict(state=data['state'],completedStages=data['completedStages'],games=data['games'],output=str(OUTPUT)),ensure_ascii=False))


if __name__=='__main__': main()

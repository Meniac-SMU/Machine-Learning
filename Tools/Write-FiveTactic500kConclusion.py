"""Evidence-bound final Korean account of the user-capped 500k experiment."""
import json
from pathlib import Path

ROOT=Path(__file__).resolve().parent.parent
BASE=ROOT/'Logs/Manager5v5/p9-tactics-20261014/MNG5_V2-20261014-r002'
DOCS=ROOT/'docs/soccer/rebuild-5v5'
NAMES=['T1 속공 전개','T2 전방 롱패스','T3 와이드 탈압박','T4 측면 컷백','T5 진로 차단 수비','T6 수신자 봉쇄','T7 전체 공 회수','T8 골문 앞 집결']
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def pct(x):return f'{100*x:.2f}%'
def main():
    completion=read(BASE/'completion.json');assert completion['passed'] and completion['step']==505386
    report=read(BASE/'report-data.json');latest=report['stages'][-1]
    diagnosis=read(BASE/'tactic-diagnosis.json')['stages'][-1];assert diagnosis['step']==505386
    fixed=diagnosis['fixed'];strength=report['aggregateStrengthComparison'];effect=strength['fixedEight']
    stage=read(BASE/'stage-results.json')[-1];assert stage['step']==505386
    strong=stage['evaluations']['strong'];m=latest['metrics']
    usage='관측 비율 충족' if latest['withinTarget'] else '관측 비율 미달'
    improved=effect['positiveGoalDifferenceEvidence'] and effect['scoreRateChange']>0
    strength_text='고정 상대 합산에서 향상 근거가 있다. 주요 상대별 퇴행 여부도 별도로 확인해야 한다.' if improved else '기존 120만 정책보다 경기력이 향상됐다고 확정하지 않는다.'
    lines=['# 전술 개편 50만 임시 종료·평가와 남은 편중','',
        '2026-10-14. 사용자 최신 정정에 따라 **원래 중단된 505,386회 모델**을 최종 임시 버전으로 선택했고 추가 학습은 중단했다.',
        f'Run `MNG5_V2-20261014-r002`의 최종 임시 평가 모델은 **505,386회·optimizer {completion["optimizerUpdates"]:,}회**다. 저장·평가는 완료했으며 사용률 판정은 **{usage}**다. '+strength_text,
        '이번 Run의 120만 학습은 수행하지 않았다. 따라서 120만 목표의 최종 성공이나 향후 개선 가능성을 이 결과로 확정하지 않는다. champion 승격과 P10 연결은 하지 않았다.','',
        '## 상한 변경과 계보','',
        '처음 중단 신호 처리 당시 기존 실행은 505,386회였다. 정확히 50만으로 맞추려 400,000회 저장본에서 추가 학습을 진행한 처리는 사용자 정정에 따라 471,854회에서 중단했다. 이 재학습 결과는 채택하지 않았다. 처음 중단본의 정책·optimizer·상대 풀·로그 전체를 보존본 `superseded-505386`과 SHA-256으로 대조 후 복원했고, 그 505,386회 모델로 평가만 수행했다. 보존 폴더의 이름은 최초 보존 당시 이름이며 현재 채택 여부는 `provisional-selection.json`이 기준이다.',
        f'채택 모델 계보의 완료 학습 경기는 {completion["trainingCompletedMatches"]:,}개다. 제외한 재학습은 추가 71,854개 감독 결정·{completion["notSelectedRetrainMatches"]:,}완료 경기이며 `not-selected-retrain-471854`와 `to-0500000`에 보존했다. 실제 소모 계산량과 채택 모델의 505,386회 경험은 구분한다.',
        '두 orchestrator의 실패 표시는 사용자 STOP으로 계획한 종료 수치와 일치하지 않아 발생한 assertion이다. 두 Python/Player의 정상 저장·종료와 오류 목록 0을 확인했다. 최신 평가 실행기는 학습을 실행하는 경로가 없으며 마지막 지시 이후 추가 학습은 하지 않았다.','',
        '## 고정한 실행과 검증','',
        '선수 실행 `tactics-v3-expansion`, 규칙형 선택 `strong-v11`, 공개 상태 676관측·8명령 단일 선택·3초 결정, 득점 +1·실점 -1·나머지 0을 유지했다. 경기당 300초, time scale 100, 학습 32환경, CPU PPO다. 새 초기화 seed 19016에서 처음부터 셀프 플레이로 학습했으며 규칙형·기존 Run은 평가에만 사용했다.',
        '공통 패스 정렬/압박 판단, T4 측면 컷백의 전개 범위와 수신자 안정화, T8 골문 앞 집결의 소유 후 불필요한 후퇴 제거와 차단선 보완을 반영했다. T7 전체 공 회수의 3인 회수·가장 먼 필드 1명 제외, 공통 운반·슛 능력은 유지했다. 명령 마스크·선택 할당량·강제 순환·사용률 보상은 없다.',
        'PlayMode 108/108, EditMode 27/27, 실행 조합 대조 80경기, 별도 32환경 사전 저장/재개와 4평가 경기를 확인했다. 이 검증은 경기력 향상 판정과 구분한다. 초기 PlayMode 실패 2건도 보존했다. 골문 수비선 상한 오류는 수정했고, 정상 슈팅 중심 경기의 패스 0회 때문에 실패한 검사는 경기별 패스 의무를 전체 짝지은 경기 묶음의 패스 확인으로 변경했다. 이 기준 변경을 전술 유용성의 증거로 세지 않는다.',
        f'0·20·40만 및 임시 최종 505,386회 단계 평가 {completion["stageEvaluationGames"]:,}경기와 기존 120만 정책 기준 비교 {completion["baselineComparisonGames"]}경기, 합계 {completion["totalEvaluationGames"]:,}경기를 마쳤다. 임시 최종 모델은 상대 12종×12 seed×양 진영=288경기다. 각 단계 PT/ONNX 실제 관측 대조의 최대 확률 오차는 {completion["largestParityError"]:.3g}다. 동결 소스·빌드·실행 도구와 기존 모델/원본 보존도 검사했다.','',
        '## 임시 최종 505,386회 성과','',
        f'단계 추세 비교의 고정 상대 8종 192경기: **{latest["wins"]}승 {latest["draws"]}무 {latest["losses"]}패·{latest["goalsFor"]}:{latest["goalsAgainst"]}**. 슈팅 {m["shots"]:,}회 중 슈팅 연계 득점 {m["shotGoals"]:,}회({pct(m["shotConversionRate"])}), 패스 {m["passAttempts"]:,}회 중 안정 수신 {m["passSuccesses"]:,}회({pct(m["passSuccessRate"])}).',
        f'규칙형 strong-v11 상대 24경기: **{strong["wins"]}승 {strong["draws"]}무 {strong["losses"]}패·{strong["goalsFor"]}:{strong["goalsAgainst"]}**.',
        '슈팅 성공률은 다른 패스·소유 변경·걷어내기 없이 해당 팀 슛에서 연결된 득점/슛이다. 모든 득점/슛과 다를 수 있다. 패스 성공률의 분모는 실제 발사이며 준비 중 취소는 아래 별도 계측에 포함한다.','',
        '| 명령 | 선택 횟수 | 선택 비율 | 95% 구간 | 검토 |','|---|---:|---:|---|---|']
    for i,name in enumerate(NAMES):
        lo,hi=latest['usageUncertainty']['intervals95'][i];r=latest['actionRatios'][i]
        flag='하한 미달' if r<.015 else '상한 초과' if r>.5 else '관측 범위 안'
        lines.append(f'| {name} | {latest["actionCounts"][i]:,} | {pct(r)} | {pct(lo)}~{pct(hi)} | {flag} |')
    lines+=['','0·20·40만 및 임시 최종 505,386회의 전체 상대별 선택 횟수·실행 시간·슈팅·패스는 [41번 결과](41_전술개편_셀프플레이50만_단계별결과.md)에 기록했다. 고정 상대 집합은 규칙형과 이전 완료 Run의 0·20·40·60·80·100·120만이다. 이번 Run의 과거 모델과 초기 P9 보충 경기를 이 분모에 섞지 않았다.','',
        '## 기존 120만 정책과 같은 조건의 경기력 비교','',
        '양 정책과 모든 상대를 동일한 개선 선수 실행에 연결했다. 승점률은 `(승+0.5×무)/경기 수`다. 동일 12개 policy-sampling seed의 양 진영과 상대를 한 블록으로 묶어 10,000회 재표집했다. 학습 초기화는 1개이며 독립 초기화 재현성을 뜻하지 않는다.','',
        '| 집합 | 후보 평균 득실차 | 기준 평균 득실차 | 차이 / 95% 구간 | 후보/기준 승점률 |','|---|---:|---:|---|---|']
    for key,label in [('fixedEight','고정 상대 8종'),('majorOpponents','규칙형·이전80만·이전120만')]:
        e=strength[key]
        lines.append(f'| {label} | {e["candidate"]["meanGoalDifference"]:.3f} | {e["baseline"]["meanGoalDifference"]:.3f} | {e["goalDifferenceChange"]:+.3f} / [{e["goalDifference95"][0]:+.3f}, {e["goalDifference95"][1]:+.3f}] | {pct(e["candidate"]["scoreRate"])} / {pct(e["baseline"]["scoreRate"])} |')
    lines+=['',strength_text,'','## 편중 진단','',
        '학습형의 행동은 `FiveManagerAgent.OnActionReceived` → `FiveLearningContract.Apply` → `SelectCommand`로 그대로 전달된다. 실제 완료 경기의 통신 명령과 Player 명령을 대조했다. 이 경로에 강제 선택은 없고, 실제 관측한 정책 확률에서 높은 선호를 확인했다.','',
        '| 소유 상황 | 관측 결정 수 | T7 전체 공 회수 선택 | T7 전체 공 회수 평균 정책 확률 |','|---|---:|---:|---:|']
    for s,label in enumerate(['우리 공','상대 공','자유 공']):
        n=fixed['stateCounts'][s]
        lines.append(f'| {label} | {n:,} | {pct(fixed["actionCounts"][s][6]/n)} | {pct(fixed["meanPolicyProbabilitiesByState"][s][6])} |')
    lines+=['',
        'T7 전체 공 회수는 소유 전에는 3인 회수, 소유 후에는 주변 선수의 위치 유지와 공통 전진 운반·슛을 제공한다. 공격에서도 준비 비용이 작은 선택으로 남는 구조는 코드에서 확인된다. 이것이 다른 명령보다 얼마나 높은 장기 수익을 주는지는 동일 상태 대체 실험으로 추가 분리해야 한다. 관측 사용률만으로 단독 인과를 확정하지 않는다.','',
        '| 명령 | 필드 패스 준비 | 발사 | 안정 수신 | 발사 전 압박 취소 |','|---|---:|---:|---:|---:|']
    for i in [3,7]:
        p=fixed['fieldPassPreparation'][i]
        # Preserve all explicit failure labels rather than assuming every cancellation is pressure.
        pressure=sum(v for k,v in p.items() if k.startswith('failureBeforeLaunch:') and ('pressure' in k.lower()))
        lines.append(f'| {NAMES[i]} | {p.get("signals",0)} | {p.get("launched",0)} | {p.get("received",0)} | {pressure} |')
    lines+=['',
        '패스 ID를 연결하여 준비 시작 당시 명령에 귀속했고 독립 GK 패스는 제외했다. 준비 중 취소, 발사 후 실패, 종료 시 미해결을 원본 진단 JSON에 구분했다. 표본이 적은 명령은 성공률만으로 충분한 유용성을 주장할 수 없다.',
        'T4 측면 컷백은 발사 가능 구간과 전개 패스를 넓혔지만 공을 측면으로 옮기고 수신자가 도착·정렬하는 시간이 여전히 필요하다. T8 골문 앞 집결은 소유자의 불필요한 후퇴를 고쳤지만 수비수 2명의 차단선이 자기 골문에서 4~16m에 제한되고 세 번째 보호 선수도 깊은 위치를 유지한다. 중원부터 유용한 보호 명령으로 확장됐는지는 아직 부족하다. 이 구조적 제한과 실제 패스 준비 취소는 확인된 사실이며, 사용률 하락 전체의 단독 원인이라는 주장은 하지 않는다.',
        '최근 8개 자기대전 정책을 보관하는 설정도 유지했다. 비슷한 압박 정책에만 적응했을 가능성은 후속 검증 가설이다. 현재 결과만으로 상대 풀을 원인으로 확정하지 않는다.','',
        '## 다음 수정에 남길 항목','',
        '추가 학습이나 코드를 이 보고 이후 자동 실행하지 않는다. 아래는 임시 최종 결과를 바탕으로 다음 후보에서 검증할 방향이다.','',
        '- T4 측면 컷백: 도착 완료 후 패스를 기다리기보다 전개 중 안전한 대각선 연결과 측면 출구를 만들어, 높은 압박에서도 준비를 끝낼 수 있는 동선을 먼저 비교한다. 발사 수 증가와 함께 준비 중 상실·후속 슛·득실차를 검사한다.',
        '- T8 골문 앞 집결: 골문 근처의 고정 깊이보다 현재 위협과 도착 시간을 기준으로 중앙 차단·접근 지연·세컨드볼·탈출 출구를 나눈다. 무조건 수비선을 올리는 변경은 이전 골문 보호 회귀를 다시 일으킬 수 있으므로 해당 실패 장면을 포함한다.',
        '- T7 전체 공 회수의 압박을 실제 득점 위험으로 돌려줄 수 있도록 T2 전방 롱패스·T3 와이드 탈압박의 출구 도착 시간을 비교하고, T1 속공 전개의 전방 지원이 소유 후 정지 배치보다 이익인지 검사한다. T5 진로 차단 수비도 낮아진 활용 원인을 함께 점검한다.',
        '- 보상과 자유 선택을 유지하면서 동일 Run 과거 정책의 보존 시기 폭, 정상 킥오프 외 상황 경험이 필요한지 별도 비교한다. 전술 상황 평가를 일반 경기 사용률 분모에 섞거나, 비율을 맞추기 위해 상대 구성을 변경하지 않는다.',
        '- 최종 채택은 일반 경기의 명령별 1.5% 이상·최대 50%와 기준 대비 경기력 향상을 함께 확인한다. 초기 무작위 정책의 고른 비율, 특정 장면의 기능 통과, 학습량 증가 자체를 성공으로 기록하지 않는다.','',
        '증거: `completion.json`, `provisional-selection.json`, `to-0600000/safe-stop.json`, `to-0500000/safe-stop.json`, `stage-results.json`, `baseline-results.json`, `report-data.json`, `tactic-diagnosis.json`. 모두 본 Run의 로그 폴더에 보존했다. [모델 등록부](p9-tactics-model-registry.json).']
    path=DOCS/'42_전술개편_50만종료와_평가분석.md';path.write_text('\n'.join(lines)+'\n',encoding='utf-8')
    print(str(path))
if __name__=='__main__':main()

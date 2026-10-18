"""Write a project-local handoff from the completed, passing rule-opponent evidence."""
from datetime import datetime,timezone
import hashlib
import json
from pathlib import Path
import numpy as np

ROOT=Path(__file__).resolve().parent.parent
BASE=ROOT/"Logs/Manager5v5/rule-strong-20261013"
DOC=ROOT/"docs/soccer/rebuild-5v5"
def read(p):return json.loads(p.read_text(encoding="utf-8-sig"))
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
def record(s):return f'{s["wins"]}승 {s["draws"]}무 {s["losses"]}패 / {s["goalsFor"]}:{s["goalsAgainst"]}'

def main():
    result=read(BASE/"holdout-assessment.json");assert result["passed"] and result["games"]==208
    tests=[read(BASE/(n+".json")) for n in ("strong-v7-edit","strong-v7-play","strong-v7-native-holdout")]
    assert all(t["testResult"]=="Passed" for t in tests)
    labels={"final":"P9 최종200,011개 모델","middle":"P9 중간100,042개 모델","initial":"P9 초기 모델",
            "fixed-0":"T1 속공 전개 고정","fixed-3":"T4 측면 컷백 고정","fixed-6":"T7 전체 공 회수 고정"}
    rows=[];diagnostics=[];matches=[];sides=[]
    for name,label in labels.items():
        pair=result["comparisons"][name]
        rows.append(f'| {label} | {pair["strong"]["games"]} | {record(pair["strong"])} | {record(pair["legacy"])} |')
        for p in (BASE/"holdout-v7"/(name+"-strong")).glob("match-*/rule-diagnostics.json"):
            diagnostics.extend(read(p));matches.append(read(p.with_name("result.json")))
        if name=="final":
            for team,label in ((0,"RED"),(1,"NAVY")):
                selected=[r for r in matches if 1-r["candidateTeam"]==team]
                differences=[r["score"]["other"]-r["score"]["own"] for r in selected]
                sides.append(f'| {label} | {sum(x>0 for x in differences)}승 {sum(x==0 for x in differences)}무 {sum(x<0 for x in differences)}패 | {sum(r["score"]["other"] for r in selected)}:{sum(r["score"]["own"] for r in selected)} |')
    times=[d["forecastMilliseconds"] for d in diagnostics if d.get("forecastOutcomes")]
    timing=dict(decisions=len(times),p50=float(np.percentile(times,50)),p95=float(np.percentile(times,95)),maximum=max(times))
    counts=np.asarray([r["opponentActionCounts"] for r in matches]).sum(axis=0)
    names=matches[0]["commandNames"]
    choices=[f'| {name} | {int(count)} | {count/sum(counts)*100:.2f}% |' for name,count in zip(names,counts)]
    legacy_stall=max(p["legacy"]["maximumObservedStall"] for p in result["comparisons"].values())
    final=result["comparisons"]["final"];effect=final["pairedImprovement"];direct=result["directStrongAgainstLegacy"]
    build="Builds/Manager5v5/rule-strong-v7-final-20261013/Soccer5v5Learning.exe"
    registry=dict(schemaVersion=1,status="validated",ruleVersion="strong-v7",defaultVariant="strong",baselineVariant="legacy",
        contract="MNG5-OBS676-A8-R1-M2-v2",behaviorName="MNG5_Manager_v2",decisionSeconds=3,matchSeconds=300,
        executable=build,executableSha256=sha(ROOT/build),
        compiledSourceArchive="Logs/Manager5v5/rule-strong-20261013/build-rule-strong-v7-final-20261013/source-built.zip",
        compiledSourceArchiveSha256=sha(BASE/"build-rule-strong-v7-final-20261013/source-built.zip"),
        selectorSources=[dict(path=p,sha256=sha(ROOT/p)) for p in (
            "Assets/_Soccer/Manager5v5/Runtime/FiveRuleManager.cs","Assets/_Soccer/Manager5v5/Runtime/FiveStrongRule.cs",
            "Assets/_Soccer/Manager5v5/Runtime/FiveRuleSettings.cs","Assets/_Soccer/Manager5v5/Runtime/FiveRuleForecast.cs")],
        evaluation="Logs/Manager5v5/rule-strong-20261013/holdout-assessment.json",evaluationGames=208,
        evaluationSha256=sha(BASE/"holdout-assessment.json"),forecastMilliseconds=timing,
        nativeOpponentArguments=["--five-rule-team","0 or 1","--five-rule-variant","strong"],
        customSettingsValidated=False,additionalTrainingStarted=False,p10Completed=False,
        verifiedUtc=datetime.now(timezone.utc).isoformat())
    (DOC/"rule-opponent-registry.json").write_text(json.dumps(registry,ensure_ascii=False,indent=2),encoding="utf-8")
    report=f'''# 강한 규칙형 감독 완료 검증과 인계

2026-10-13 사용자가 요청한 규칙형 감독 개선을 `strong-v7`으로 완료했다. 최신 P9 모델보다 강한 상대인지 미사용 seed208경기로 확인했고, 사전에 정한 대응 득실차·최종 모델 상대 승수·기존 규칙형 직접 대결·경기 건강성 기준을 통과했다. 기존 `legacy` 감독과 P9 모델/학습/평가 원본을 보존했다. 추가 강화학습은 실행하지 않았다.

## 독립 경기 결과

모든 수치는 **규칙형 감독의 관점**이다. 같은 Player, 같은 상대 ONNX, 같은 seed, 양 진영 교대로 비교했다. 최종 모델은 확률 분포에 따라 행동을 표본 추론하며, 정책별 독립 RNG를 고정했다.

| 상대 | 각 규칙형 경기 수 | strong-v7 승무패 / 득실 | legacy 승무패 / 득실 |
|---|---:|---|---|
{chr(10).join(rows)}

기존 규칙형과 직접 대결한32경기는 **{record(direct)}**였다. 44000~44015의 동일 시작 배치 변형을 진영 교대해 사용했다. 나머지는43000/43100/43200/43300/43400/43500부터의 고정 킥오프 경기다.

최종 모델 상대에서 strong-v7의 기존 규칙형 대비 **경기당 득실차 개선은 {effect['mean']:+.3f}골**, seed 단위 대응 bootstrap95% 구간은 **[{effect['bootstrap95'][0]:+.3f}, {effect['bootstrap95'][1]:+.3f}]**이다. 같은 seed의 양 진영을 한 묶음으로20,000회 재표집했다. 개발 배치는 이 확인 성적에 포함하지 않았다. 유한한 고정 모델/경기 설정의 결과이며 미래에 추가 학습된 감독까지 항상 이긴다는 뜻은 아니다.

최종 모델 상대의 새 규칙형 진영별 성적도 함께 확인했다.

| 새 규칙형 진영 | 승무패 | 득실 |
|---|---|---|
{chr(10).join(sides)}

## 구현

- 실제 감독 인터페이스는 그대로8개 중1개,3초 결정,300초 경기다. 외부 학습형의676관측·8행동·R1+M2·전술 마스크 없음 계약을 유지했다.
- 공개 선수 위치·속도·방향·체력, 공과 소유권, 자기 팀 패스/명령 상태로 판단한다. 상대의 실제 명령·목표·패스 예약·숨은 쿨다운은 예측 상태에 복사하지 않는다.
- 패스 후보는 실제 선수 실행기의 거리·전방 범위·동료 방해·이동하는 상대의 차단 조건으로 검토한다. 골문을 벗어나는 공과 중앙 슛 위협을 구별하고, 회수 압박이 오래 정체되면 배치를 바꾼다.
- 자기 소유 시 T1 속공 전개·T2 전방 롱패스·T3 와이드 탈압박·T4 측면 컷백의4후보를 상대2가정과 비교한다. 상대 소유 시 전체8후보를 상대2가정과 비교한다. 명령 후보마다 별도 PhysicsScene에서3초를 계산한다.
- 상대 가정은 자기 소유 시 T7 전체 공 회수/T4 측면 컷백, 상대 소유 시 T1 속공 전개/T4 측면 컷백이다. 예측 득점·소유·전진·슛 위험을 평가해 `0.7×최악 결과 + 0.3×평균 결과 + 0.08×기존 판단 점수`가 가장 큰 후보를 선택한다. 이 점수는 감독 내부 비교용이며 학습 보상을 바꾸지 않는다.
- 자기 패스가 준비·비행 중이면 이어가고, 중립 공과 장기 정체는 명시 규칙으로 처리한다. 본 경기에는 최종 명령 하나만 전달한다. 결정 사이에 다른 전술을 덧붙이거나 선수 능력을 올리지 않는다.
- 예측 물리는0.02초 간격의 근사이며, 실제 경기의0.01초 물리·선수 기술·독립GK·체력·패스·슛 규칙은 유지한다. 도달 가능 경로 검사는 본 경기에서 기존 `Physics.RaycastAll`을 그대로 호출하고, 예측 경기만 자기 물리 장면의 Raycast를 사용한다.
- 선택 이유·8후보 점수·패스 후보 수·위협·예측 가정별 결과·계산 시간을 trace에 기록한다. 생성한 예측 장면은 경기가 닫힐 때 해제한다.

## 검증과 실행 비용

EditMode **{tests[0]['passed']}개**, PlayMode **{tests[1]['passed']}개**, 별도 직접 대결 검증 **{tests[2]['passed']}개 시험/32경기**가 통과했다. 테스트는 패스 가능성, 골문 위협, 전술 결정 경계, 기존 규칙형 보존, 학습 인터페이스, 두 진영의 예측 골, 실제 위치·속도·방향·체력·소유권·시간·점수·이벤트 보존, 예측 장면 해제를 포함한다.

확인208경기에서 물리 경계 보정은0건이다. 새 규칙형이 참여한 Player 경기의 최대 관측 정체는{max(r['maximumObservedStall'] for r in matches):.3f}초, 직접 대결의 물리 단계별 최대 정체는{direct['maximumStall']:.3f}초였다. Player 정체 값은 결정/종료 시점 표본이며 매 물리 단계 최대와 구별한다. 모든 Player는300초 종료·관측 유한성/범위·마스크 없음·득실과 보상 일치·정상 프로세스 종료·불변 payload 해시 검사를 통과했다.

비교용legacy Player88경기의 최대 관측 정체는{legacy_stall:.3f}초였다. 새 규칙형의 정체 기준과 구별해 기존 동작 그대로 기록했다.

최대4개 Player를 병렬 실행한 평가에서 예측 계산{timing['decisions']:,}회의 중앙값은 **{timing['p50']:.1f}ms**,95백분위는 **{timing['p95']:.1f}ms**, 최대는 **{timing['maximum']:.1f}ms**였다. 규칙형이 강해진 대신 매 결정의 계산량이 커졌다. 이 값은 해당 PC/병렬 평가 조건의 측정이며 실시간 시연 프레임 성능을 보증하지 않는다. P10 내장 모델 선택·화면/조작 시연은 별도 단계다.

새 규칙형 Player88경기의 실제 명령 선택은 아래와 같다. 상황에 따른 결과를 기록하며 균등 사용을 강제하지 않았다.

| 명령 | 선택 횟수 | 비율 |
|---|---:|---:|
{chr(10).join(choices)}

## 보존한 실패와 개발 이력

단순 점수형v1~v4를 개발 비교한 뒤 v5를 첫 후보로 고정했으나, 첫 독립208경기에서 최종 모델 상대7승12무13패·65:77, 득실 개선95% 구간[-0.875,+1.469], T7 전체 공 회수 고정 상대34.397초 정체로 실패했다. 기존 규칙형 직접 대결20승6무6패만으로 성공이라고 기록하지 않았다. 해당 소스·빌드·경기·판정은 `holdout-v5-assessment.json` 등에 그대로 보존했다.

이후 공격 예측v6, 공격·수비 예측v7을 개발 seed로 확인하고 새43000계열 확인 seed로 검증했다. NUnit 호출 오류, 결정 경계 테스트 시점 오류, 골 재개 대기를 포함하지 않은 테스트 상한, SDK 타이머 파일의 불변 payload 오분류, 예측 TerrainCollider 누락도 각 실패 로그와 수정 후 검증으로 구별했다.

## 사용과 재현

일반 `FiveMatch`의 기본 규칙형은strong이며, `ruleVariants`에서legacy를 명시 선택할 수 있다. 학습/평가 Player는 `--five-rule-team 0` 또는 `1`과 `--five-rule-variant strong`으로 해당 팀을 HeuristicOnly로 구성한다. 나머지 팀에는 검증된 P9 계약의 외부 정책을 연결한다. 기존 P9 자기대전 학습 풀과 체크포인트를 자동 변경하거나 새 학습을 시작하지 않았다.

```powershell
.\\Tools\\Run-FiveRuleEvaluation.ps1 -BuildDirectory Builds/Manager5v5/rule-strong-v7-final-20261013 -OutputDirectory Logs/Manager5v5/new-rule-evaluation -Candidate results/Manager5v5/MNG5_V2-20261013-r001/MNG5_Manager_v2/MNG5_Manager_v2-200011.onnx -Variant strong -Pairs 8 -Seed 46000
```

비교하려면 새 출력 폴더에서 `-Variant legacy`를 사용한다. `-Settings`는 개발용 선택 점수 변경이며, 이 완료 결과는 **설정 override가 없는 동결 기본값**에만 해당한다. P9 당시 실행 파일과 평가 기록의 규칙형은legacy다. 기존 전시/시연 exe를 이 새 평가 Player와 혼동하지 않는다.

- [규칙형 상대 등록부](rule-opponent-registry.json)
- [사전 계획과 변경 이력](25_강한규칙형감독_개발계획.md)
- [확인 판정 원자료](../../../Logs/Manager5v5/rule-strong-20261013/holdout-assessment.json)
- [완료 영수증](../../../Logs/Manager5v5/rule-strong-20261013/completion.json)
- [예측 구현](../../../Assets/_Soccer/Manager5v5/Runtime/FiveRuleForecast.cs)
- [기본 규칙/진단](../../../Assets/_Soccer/Manager5v5/Runtime/FiveStrongRule.cs)
- [평가 실행 도구](../../../Tools/Run-FiveRuleEvaluation.ps1)

원본693개, P9 모델/기록, 실행 전 프로젝트 설정의 보존과 최종 소스/빌드의 일치는 완료 영수증에서 해시로 확인한다. Builder가 다시 저장한 `FiveLearning.unity`는19개 오브젝트의 로컬fileID와 직렬화 순서만 바뀌었고, 참조 관계와 모든 설정이 같음을 별도 비교했다. Unity가 자동 추가한iOS 발열/FPS 설정3줄은 실행 후 사본을 남기고 시작 설정으로 복원했다. 실패를 포함한 증거는 `Logs/Manager5v5/rule-strong-20261013`에 둔다. commit/push는 하지 않았다.
'''
    (DOC/"26_강한규칙형감독_완료검증과인계.md").write_text(report,encoding="utf-8")
    print(json.dumps(dict(report=str(DOC/"26_강한규칙형감독_완료검증과인계.md"),timing=timing),ensure_ascii=False))

if __name__=="__main__":main()

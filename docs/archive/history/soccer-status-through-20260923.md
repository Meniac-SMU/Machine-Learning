> 보관일: 2026-09-24. 작성 당시의 계획·판정·승인 이력이며 현재 실행 지시가 아닙니다. 현행 기준: [현재 상태](../../soccer/current-status.md).

# Soccer 현재 상태

## 2026-09-23 현행 기준 — MS2-v3 출발 / MS3-v3 D5 준비 완료

**[MS3-v3 D4·D5 완료 보고서](../../soccer/training/ms3-v3-d4-d5-report-20260923.md)가 현행 진입점이다.** 사용자가 최신 경기를 직접 확인했고 부족한 패스 빈도는 향후 학습 경과로 판단하기로 했다. 기존 MS2 승인 actor와 현행 규칙을 MS2-v3 출발 기준으로 삼으며, MS3-v3는 새 optimizer·step0·새 풀로 재시작한다. 기존400k는 초기값/상대/평가기준/champion/기본시연에서 제외하고 원본만 보존한다. 활성 registry는 `training/ms-v3-model-registry.json`, champion은 아직 없다. 내부 MNG_ManagerV2/244관측 등의 ABI 이름은 유지한다.

MS3-v3 학습 전 안정화 D1~D5 완료. 최신 runtime 동결80경기 무결성 통과,32환경 동시512결정/학습0, Python37/37, 실행 dry-run 및 잘못된 입력4종 거부 통과. 변경 없는 게임 코드의 최신 Edit357/Play54(진단2제외) 증거를 재사용했고171개 C#·설정 및 동결 원본121개를 보존했다. 진영 차이는 동일 정책 지정 후보Red35%/Navy55%, 차이95%구간[-55,+17.5]%p로 미확정이며 완전한 공정성 승인으로 해석하지 않는다. Full 승점률68.75%,carry-shot25%를 출발 기준선으로 기록한다.

준비물 `Logs/MNG-Rebuild/MS3-v3-D5-prepared-20260923`, 예약 Run `MNG_MS3V3-20260923-r001`, readyToStart=true/학습0/optimizer0. 다음은 **MS3-v3 초기 자기대전(R6) 첫100k→점검→200k 평가**다. 현재 최대200k 준비이며400k·R7/R8·수백만 step 자동 연장은 허용하지 않는다. R1~R4 기반 재사용/R5 MS2 계보 채택 완료, **새 v3 R6는 미시작**,R7~R8 미진입. 아래 v2 완료 기록은 역사 이력이며 v3 학습 완료로 이관하지 않는다.

## 2026-09-23 게임 규칙 완료 이력 — MS2 전방 패스·중앙 드리블 재정비

2026-09-23 최종 사용자 규칙: 자기 골문 앞 적극 걷어내기는 모든 자동 선수 유형에 유지한다. 2초 정체/0.1m 전진 판정에 따른 강제 패스는 폐지한다. 감독이 PassBuild를 선택한 경우에만 전방 동료를 향한 패스를 강제 준비하며, 상대가 실제 패스 경로를 막으면 취소한다. 상대 골문 중심에서 기존 슈팅 범위24m 이내에서는 PassBuild를 선택하지 않는다. 기존 물리 패스 범위5~28m, 경로 여유1.25m, 예측 수신점의 중앙 쪽1.5m 편향을 재사용한다. 전진 드리블 목표는 중앙 쪽으로 기존 횡이동 폭3m만큼 치우치되 중앙선을 넘기지 않는다. MNG 정책형·규칙형·fallback은 같은 기술 경로를 사용한다. Core 자동 선수는 정체 강제 패스 없이 자기 골문 앞 걷어내기를 유지한다. 감독 선택 이후의 준비/수신 예산은 각2초이며 성공·소유를 만들어내지 않는다. 수동 입력 소유권, 관측/행동 차원과 보상액은 유지한다.

**수정·검증 완료.** [MS2 전방 패스·중앙 드리블 보고서](../../soccer/training/ms2-forward-pass-center-report-20260923.md)에 상세 결과를 기록했다. 최종 Edit357/357, Play54통과/실패0/수동진단2제외, Python31/31, 5환경 Builder와 새 Player 빌드 통과. 동결 MS2 자체300초8경기 무결성 통과, 정체 강제 패스0회, 양 팀 실제 패스18회(걷어내기17/감독 선택1)다. 기존 모델의 PassBuild 선택은3회로 낮으며 성능 개선·공정성을 이 소표본으로 승인하지 않는다.

현행 runtime `MNG-MS2-forward-pass-center-20260923`, DLL SHA `30aa9b0c9ce224de2d9e9b66457f258fc1bdaef2f3a8ef6069bb6f2194ac775a`, Player `Builds/MNG_V2/MS2-forward-pass-center-20260923/{MS3V2,EvaluationV2}`. 동결 입력121개와 프로젝트 설정2개 SHA 보존. MS2-v2 기준 경기 규칙 재정비 완료, R1~R6 이력 완료/R7~R8 미진입. 이전 runtime의 D1~D5 이력을 보존하며 이번 D3 기술 회귀는 재통과, D4는 smoke만 실행, D5 현행 manifest는 갱신 전이다. 이전80경기 및 D5 준비를 새 runtime의 승인으로 사용하지 않는다. 신규 학습·optimizer·모델 승격0.

## 2026-09-23 이전 runtime 이력 — MS2 공통 패스·골문 앞 수비 규칙 보강

사용자 지시에 따라 **2초 정체 시 동료 패스 강제**, **역할에 관계없는 자기 골문 앞 전방 패스/강한 걷어내기**를 구현했다. 전진 판정은 사용자가 확정한 상대 골문 방향 최고 위치0.1m 갱신이다. MNG 학습형·규칙형·fallback과 Core 자동 선수 공통이며 수동 입력 소유권은 유지한다. 자세한 구현·실패 수정·평가·다음 단계는 [MS2 공통 경기 규칙 보강 보고서](../manager/ms-v2/ms2-common-possession-report-20260923.md)를 따른다.

- 새 규칙보다 먼저 소유를 끊던 기존0.12초 정지 해제와의 충돌을 해결했다. 제어 범위 안의 실제 공에만2초 판단+2초 준비 시간을 제공하며, 실제 타격과 요청을 분리 계측한다.
- 동결 비교80경기: 같은 MS2 준비200120 정책의 규칙OFF→ON을 동일400k상대와 비교했다. 실제 패스1→50, 지정수신0→19, 승점률42.5→43.75%, 득실105:128→116:123. 승점률 차이95% CI −17.5~+20%p로 승리 성능 향상은 확정하지 않는다. 상대400k의 새 규칙 개입0이다.
- 최종 runtime `MNG-MS2-common-possession-stop-20260923`, SHA `ef88e3d02db480f8f455a9ff761fcf8442a0cc8db5bd0fd4fbc7cdd972de5c22`. 최종 Player는 `Builds/MNG_V2/MS2-common-possession-final-20260923/{MS3V2,EvaluationV2}`다. 중간 버전32경기는 보존하되 최종 성적에 포함하지 않는다.
- MS/R/D 위치: **MS2-v2 준비(R5) 완료 기준 공통 규칙 보강·동결 비교 완료 → MS3-v2 학습 진입 준비 갱신 전**. R1~R6 및 이전runtime의 D1~D5 완료 이력은 보존한다. 새 규칙으로 이전D5 manifest는 현행 학습 승인으로 사용할 수 없다. R7/R8·MS3 전체 완료·대규모 학습·신규optimizer·모델 승격은 미실행이다.

아래 기록은 각 당시 runtime의 완료 이력이다.

## 2026-09-23 최신 — MS3-v2 안정화 / D3~D5 완료, 한정 학습 직전 준비

최신 상세 결과는 [MS3-v2 안정화 D3~D5 보고서](../manager/ms-v2/ms3-stability-d3-d5-report-20260923.md)다. **D3 패스 기술 검증, D4 접촉·리셋 원인 분리와 수정·재평가, D5 첫100k/최대200k 한정 학습 준비를 완료했다. 실제 학습·optimizer 갱신·모델 승격은0이다.** 아래 D1D2/Post-R6 보류 기록은 당시 runtime의 이력이다.

- **공통 수정:** MNG 공·선수 solver를 양 진영48/16으로 통일했다(기존6/1). Rigidbody만 회전 초기화한 뒤 Transform 동기화로 방향이 오염되는 reset 오류를 실패 검사로 재현하고, Transform·Rigidbody 전체 pose를 함께 복원했다. MNG PPO·규칙형·선수가 공유한다. 별도 Core 전역 물리·질량·속도·보상·244관측·6행동은 유지한다.
- **검증:** Soccer EditMode349/349, MNG+Core PlayMode39/39 일반 검사(진단2개 Ignore), Python29/29, 공통5환경 Builder, 최종 Player2종 빌드 통과. 패스22상황 중 안전14개는 실제strike·지정수신 성공, 나머지8개는 위험/취소 기대 결과 통과. 실패한 reset/진단 fixture 이력은 보존했다.
- **최종 동결80경기:** 같은400k정책40경기 Red55%/Navy57.5%, 차이−2.5%p, seed cluster95% [−30,+25]%p. 초기 관측·kickoff 미러 최대오차2.385e−7. 큰 방향성 격차는 이번 표본에서 재현되지 않았지만 완전한 동등성 입증은 아니다. 규칙형8경기100%, Recover전용81.25%, 같은Run200k18.75%, MS2초기75%, 무작위 대 규칙형0%다. 무결성 위반0.
- **정책 한계:** 400k는 자기 소유 Pass허용4,313회에도 선택0이며 history200k에 열세다. 패스 강제나 Recover 의미 변경은 하지 않았다. 기존400k를 새 환경의 champion으로 재승인하지 않고 원본 registry를 보존했다.
- **최종 runtime:** `MNG-P1-MS3-contact-reset-20260923`, `Builds/MNG_V2/MS3-stability-contact-reset-20260923/{MS3V2,EvaluationV2}`, 공통 runtime SHA `53de9775c503106bc11472394ffdf612367a3935771a0b1b5992acb3bb0ebab4`. 이전 Player 성적과 합산하지 않는다.
- **D5 준비:** 최종32환경 동시 연결·512결정 통과,14.922초, 동시 RAM55%/가용14.10GiB. `Logs/MNG-Rebuild/MS3-D5-prepared-20260923`의 manifest/gate/actor/config/첫100k·200k재개 스크립트를 작성·검증했다. 예약 Run `MNG_MS3V2-20260923-r001`의 실제 Run/results는 생성하지 않았다. 원본 MS2 actor의 Policy를 보존한 step0 사본+새optimizer+초기/history400k고정 상대의 새pool을 준비했다. 원본 r005 resume가 아니다.
- **MS/R/D 위치:** MS0/MS1 완료 이력 → MS2-v2 R5 준비200,120 완료 → MS3-v2 R6 초기402,389 완료 이력 → **MS3-v2 R6 이후 안정화 D1~D5 완료 및 한정 학습 직전**. R1~R6 실행 이력 완료, R7~R8 미진입. MS3 전체·수백만 step 확대·새 모델 승격은 미완료다.

다음에는 별도 한정 실험의100k에서 실제 진영 노출·패스/후방/정체·고정 대진을 점검하고, 통과할 때만 최대200k까지 진행한다. 수백만 step으로 곧장 확대하지 않는다. 새 holdout996001~40쌍은 미실행 예약이다. 사용자 요청대로 사용량5시간/주간 비율 제한은 폐지된 상태이며 RAM/NaN/crash/무결성·정상저장 조건은 유지한다. 수동 화면 검수와 실제 장시간 학습 검증은 이번 완료 범위에 없다.

## 2026-09-23 최신 — MS3-v2 안정화 / 진영 로직 보정(D1)·진단 계측 정비(D2) 완료

이번 사용자가 지정한 D1·D2를 기존 구현과 대조해 남은 오류를 수정하고 검증했다. 상세 증거와 변경 이유는 [MS3-v2 안정화 D1·D2 보고서](../manager/ms-v2/ms3-stability-d1-d2-report-20260923.md)에 있다. 아래 Post-R6 전체 평가 결과는 이전 runtime의 이력이며 이번 소스의 새 성적이 아니다.

- **D1:** 경계 탈출 후보의 근접 거리 동률에서 팀 회전에 따라 선택이 달라지는 오류를 재현하고, 실제 최단거리 기준의 동률 범위로 수정했다. MNG 규칙형 감독과 PPO에 공통 적용한다. 색상별 능력 보정은 하지 않았다.
- **D2:** active 자기/상대/중립 판단 분모, 미측정 정체의 `null` 처리, 실제 패스 목표 거리·통로 여유·전방 막힘 문맥과 Pass 확률 연결을 보완했다. tick/명령/mask/소유/pause가 불일치하면 연결을 거부한다. 허용을 안전한 패스로 단정하지 않는다.
- **최종 검사:** Soccer EditMode349/349, 대상 PlayMode9/9, Python8/8, MNG Builder·평가 Player 빌드 통과. 합성1,000선택 및 실제400k ONNX의 기록 관측128개 재추론에서 계측 전후 명령 동일·입력 불변이다. PlayMode에는 패스22물리 상황과 공통 경기 환경 회귀가 포함된다.
- **D2 Player 연결 smoke:** 무작위 대 규칙형300초×2경기, 결정1,202건 연결, active1,136/goal pause66, Pass 허용85/선택14/strike0, 전방 막힘 허용46. 명령 불일치·override·직접 명령 보상0. 품질 평가나 공정성 승인이 아니다.
- **현행 소스/검증 Player:** 환경 revision `MNG-P1-MS3-stability-D1D2-20260923`, `Builds/MNG_V2/MS3-stability-D1D2-20260923/EvaluationV2`; runtime SHA `a03fc8a568cc52bff698227a542df2714805868dc4a6ab8c8d42de49021e5f26`. 244관측·6행동·보상·물리 프로필은 유지한다. 기존 모델/registry와 과거 Player는 보존했다.
- **MS 전체 위치:** MS0/MS1 완료 이력 → MS2-v2 준비 학습 완료(R5,200,120) → MS3-v2 초기 자기대전 실행 완료(R6,402,389) → 현재 **MS3-v2의 후속 안정화 진행 중**이다. MS3 전체의 확장 준비 완료를 뜻하지 않는다.
- **R 진행:** 공통 개편 R1~R4 및 학습·평가 R5~R6 완료 이력, R7~R8 미진입.
- **D 진행:** 이번 D1·D2 보완·검증 완료. 이전 D3 기술 회귀 통과 이력은 유지한다. D4는 진단을 수행했지만 동적 진영 격차가 남아 승인 보류, D5는 미진입이다. 이번에는 D4 새 원인분리 실험과 학습/optimizer 갱신/모델 승격을 실행하지 않았다.

다음 기술 과제는 **MS3-v2 안정화 / 동적 대칭·전술 평가(D4)**의 첫 접촉 replay·생성/물리 등록 순서 분리다. D1·D2 검사 통과만으로 기존400k를 resume하거나 수백만 step 학습으로 확장하지 않는다. 단계 명칭은 MS를 최상위로 하며 R/D는 파생 작업의 기존 추적 ID로만 병기한다.

## 2026-09-23 Post-R6 수정·검증 종료 — 대규모 학습 준비 진입 보류

상세 변경, 실패 원인과 재검사, 전체80경기 표, 재현 도구, 미해결 경계는 [Post-R6 수정·검증 보고서](../manager/ms-v2/ms-post-r6-implementation-report-20260923.md)에 있다. 이번 사용자의 승인으로 진단 전용 범위를 확장해 실제 코드를 수정·검증했다. **학습·optimizer update·모델 승격은0**이며 D5 준비/새 학습 Run/config를 만들지 않았다. 아래 과거 ‘진단만’과 ‘현재’ 표기는 각 작성 시점의 이력이다.

- **최종 코드:** 팀 상대 좌표와 중립 우선권, 경기 전체 정체 집계, 패스 후보/실행 목표 일치, 전면판 수신·회전·준비, 실제 패스 출발 후 수신 예산, 벽 접선 회피, 취소 사유, 경기 시간 기준 드리블을 수정했다. 244관측·6행동·보상·물리 프로필·Recover 인원 배정은 유지한다. 패스 선택 강제/전략 mask/출력 override는 추가하지 않았다.
- **규칙형 적용:** MNG 규칙형 감독은 같은 Planner/Executor/BallControl 수정을 공유한다. 별도 Rule 팀 FSM과 Core fallback의 같은 lane/지원 좌표 오류도 수정했다. Rule의 선택 전략이나 보상을 새로 바꾸지는 않았다.
- **검증:** 전체 MNG+공통 PlayMode38/38, 마지막 clock 수정 후 영향 회귀14/14, 최종 전체 Soccer EditMode346/346, Python5/5, 공통5환경 Builder parity 및 커리큘럼 contract PASS. 패스22상황의 안전14개는 실제 킥+지정 수신에 성공했고 위험/명령 교체8개도 기대대로 동작했다. 최종 규칙형3×300초는 pass24/완료9, 밀집233/90006=0.259%로 기존<1% 기준을 통과했다. 수신 중 새 명령 교체도 가능하다.
- **최종 빌드:** `Builds/MNG_V2/PostR6-20260923-r4/EvaluationV2`, runtime SHA `3ab38c2c3ca952246e4dce7888a3fc657ecfcc770439a652fd0bd6c2dc2a4df6`; 환경 revision `MNG-P1-postR6-20260923`, protocol3, frozen protocol v2. 기존 빌드/모델/registry는 보존한다.
- **동결80경기:** 기존400k의 동일정책40경기 점수율46.25%; 규칙형8경기100%(39:5), Recover 전용8경기50%(13:12), 같은 Run200k8경기37.5%(12:18), MS2 준비 완료200k8경기56.25%(15:21). 무작위 대 규칙형8경기는0%(3:47). 작은8경기 비교로 새 모델을 승격하지 않는다. 개발 중 서로 다른 runtime의208경기를 한 승률로 합치지 않는다.
- **패스:** 최종 동일정책40경기에서 허용4072회, 허용 시 평균 선택 확률0.003595%, 실제 선택/strike0. 무작위71명령→3strike→0수신; 세 킥 모두0.24~0.46초 뒤 다른 명령으로 바뀌었다. 같은 대진의 규칙형은53strike/17수신이었다. 기술 통과와 정책의 학습된 선택은 구분한다.
- **Recover/정체:** 소유 시68.65%, 비소유94.79%가 Recover다. 경기 전체 정체571회/1804.885초=경기당45.12초; 옛 마지막-round 카운터131회와 직접 비교하지 않는다. carrier 제외 필드 선수의 공 뒤 위치는 직전 Recover 아래 소유 평균1.532명/비소유1.824명이며 전술 커버 역할이나 인과 효과로 해석하지 않는다.
- **진영은 미해결 경계가 남는다:** 최종40경기 Red32.5%/Navy60%, 차이-27.5%p,10 seed cluster95%[-42.5,-7.5]%p. spawn 최대오차9.54e-7m·초기 관측2.38e-7, 최초 속도 차이는 약3~3.5초 접촉 구간이며 그 이전 양 역할 명령40/40일치. 하위 물리 자산 설정 차이는 검사 범위에서0이나 native 물리 등록/접촉 처리 순서의 인과 효과는 미분리다. 색상별 능력 보정이나 승률을 맞추는 임의 수정은 하지 않았다. **동적 공정성 승인·대규모 학습 준비는 보류**한다.
- **다음 필수 확인:** 기록된 초기 명령을 고정한 첫 접촉 replay에서 팀/생성·물리 등록 순서 조건을 분리한 뒤 새 seed로 확인한다. 그 전에는1M 이상 학습이나 기존 r005 자동 resume를 하지 않는다. 한정 학습은 이 경계가 정리된 후 별도 manifest로 판단한다. 사람의 경기 화면 검수는 이번 자동 검사와 별도다.
- **사용량 규칙 폐지:** 5시간·주간 잔여량을 기준으로 한 사전 중단/진입/재개 규칙은 모두 폐지했다. 아래 과거 사용량 수치·중단 이력과 이전 예약 지시는 현재 실행 조건이 아니다. RAM/NaN/crash/정상 저장·원본 보존은 유지한다. 실제 서비스 quota나 credit을 변경한 것은 아니다.
- 증거: `Logs/MNG-Rebuild/PostR6-validation-20260923/`의 XML·`r4-final-diagnostics.json`·`r4-pass-transition-audit.json`·`r4-mirror-first-divergence.json`·`physical-asset-audit-reviewed.json` 및 `Logs/MNG-Rebuild/PostR6-r4-*-20260923/report.json`. source 시작 snapshot과 최종 보존 검사를 함께 보관한다.

## 2026-09-23 R6 후속 진단 완료 — 작은 로직·계측 수정 선행 권고

상세 근거·파일별 수정안·검증 순서는 [R6 이후 진단과 최소 수정 계획](../manager/ms-v2/ms-post-r6-diagnosis-plan-20260923.md)이다. 현재 사용자의 범위는 진단·계획이다. 새 경기/Unity 실행/학습을 시작하지 않았으며 runtime/보상/모델/씬은 수정하지 않았다.

- **패스 미사용의 첫 단절은 정책 선택이다.** 기존400k 정규+holdout360경기의 로그600결정/경기, 총216,000결정 중 Pass 허용38,281(17.72%)인데 최종216,360명령의 Pass 선택0, 실제 pass strike0이다. 마지막1결정/경기는 허용 누적 로그에서 빠져 있으므로 전체 최종 허용 횟수로 오인하지 않는다. 허용은 안전/유리한 패스를 보장하지 않으며 Softmax 확률 자체는 미계측이다. 무작위40경기에서는 Pass 선택263→실제 킥15→완료3, R5 최종은28→1→0이었다. 이는 서로 다른 단계의 횟수이지 독립 시도당 성공률은 아니다. 실제 조준점과 후보 점수화 지점의 차이, 준비/수신 교체를 작은 fixture로 조사한다. 패스 강제·명령 보상·보상 증액은 기본안이 아니다.
- **Recover는 소유 시 적극 공격 명령이기도 하다.** 소유 시 운반1+지원2, 비소유 시 압박2+커버1이 기존 P1 계약이다. 높은 비율만으로 회수 반복 오류라 하지 않는다. 일반 MS 회수 보상 지급은0이므로 과도한 회수 보상으로 설명하지 않는다. 수비 대칭 수정 뒤 후방 위험/역습 실점까지 비교하며, 명령 균등 사용을 목표로 삼지 않는다.
- **초기 배치 난수와 별개인 비대칭을 실제 R6 DLL에서 재현했다.** 정확한180도 team swap 입력에서도 ProtectBack MidLeft 목표가 Red(-16.84,-14.08), Navy(16.84,-14.08)로 나와 대응 측면 좌표가 반전되지 않았다. 공통 `FormationAnchor`/`DefensiveTarget`의 RoleLane에 sign이 없고 P1이 이 경로를 호출한다. 중립 동거리 소유권은 후보 배열을 뒤집어도 Red가 획득했다. contested escape reset 후 첫 sequence1은 무소유일 때 Red 우선 경로다. 중앙 슛 fallback도 world y가 동일했다. 이것은 로직 비대칭의 증거이며 과거 진영 승률 차이의 인과 기여량을 입증한 것은 아니다.
- **의도한 자산 능력 차이는 검사 범위에서 발견하지 못했다.**8명/동일 슬롯의 루트 물리·쿨다운·역할·motor profile 비교 차이0, 초기 x/z 위치는180도 대응한다. 양 보상 asset 모두 Base다. 동일 mirrored fixture의244관측 최대차이0. 모든 자식 collider/PhysX/실행순서의 동적 대칭을 검증한 것은 아니다. 기존 same-seed 진영 교대는 정확한 mirrored spawn이 아니며 정책 RNG 역할도 분리 재검증한다. 표본 난수로 인한 성적 차이 가능성은 남는다.
- **이전 정체 통계 해석 정정:** `stallActivations`는 goal/round reset 때0이 되는 tracker 값을 경기 종료 시 읽는다. 기존112→125 등의 수치는 경기 전체 정체 합계가 아니므로 증가/감소 판단 근거로 사용하지 않는다. episode 누적·active seconds를 별도 계측하는 수정이 필요하다. 기존 득실/승격 기록/패스0 사실은 보존한다.
- 조사 증거: `Logs/MNG-Rebuild/PostR6-diagnosis-20260923/{saved-evidence.json,managed-logic-v2.json,summary-and-preservation.json}`. 기존12대진520경기 로그를 읽었으며 새 경기0. DLL 순수 함수 검증은 Unity/PhysX 실행 검증과 다르다. R6 최종 snapshot의Assets/ProjectSettings/config/registry1795파일 SHA 변화0. 재현 도구는 `Tools/diagnose_mng_post_r6.py`, `Tools/Diagnose-MNGPostR6.ps1`이다.
- **다음 권고는 D1(팀 좌표/중립 우선권)과 D2(정체·패스 계측) 우선 구현, D3 패스 기술 fixture, D4 작은 mirrored/RNG 교차 비교다.** 이후에만 새 runtime에서 기존 정책을 재평가하고 최대200k 한정 가설 검증을 검토한다.1M 이상 자동 연장은 권하지 않는다. 아직 수정/회귀 빌드/새 평가/학습을 수행한 것은 아니다. 기존 champion은 과거 runtime의 결과로 유지한다.

## 2026-09-22 R5/R6 완료 — 첫 v2 champion, 추가 학습은 진단 후 판단

### 결론과 현재 모델

- R5 개발·준비 학습과 R6 개발·정식 self-play 400k·평가를 완료했다. R5는 fresh network/optimizer로 200,120 step, R6는 R5 actor만 가져와 새 optimizer로 402,389 step까지 진행했다. 계보상 경험은602,509이며 중단한 진단 Run이나 R4 smoke 경험은 포함하지 않는다.
- 사전 선언한 첫 champion gate를 통과해 `results/MNG_MS3V2-20260922-r005/MNG_ManagerV2/MNG_ManagerV2-402389.onnx`를 첫 v2 champion 및 registry 기본 후보로 등록했다. SHA-256 `653d1f98126bd111ce4c15c26c0382d2170a2be312c8631aded44cff38d1614f`. 판정은 `Logs/MNG-Rebuild/R6-final-assessment.json`, 자격 manifest는 `R6-champion-manifest.json`이다.
- **바로 1M까지 연장하는 것은 권장하지 않는다.** 재개 기술 경로는 정상이나, 200k 대비 우위의 불확실성·회수 편중·패스 부재·진영 효과를 먼저 진단해야 한다. 600k 이후 R7은 실행하지 않았다. champion 통과는 모든 과거 모델·단순 전술에 대한 우위나 실험 전체 종료를 뜻하지 않는다.
- 학습/평가 프로세스는 모두 정상 종료했다. 최종 pool은 초기 reference+champion의 pinned2, recent30이며 optimizer·가중치·RNG·스케줄은 champion 추가 전과 동일하다. 원본 pool과 갱신본을 각각 보존했다. `R6-champion-pool-proof.json`, `R6-champion-pool-verification.json` 참조.

### 구현과 발견한 문제

- 32환경 정식 실행기, 실제 step0 저장, 100k 안전 중단·동일 optimizer/pool 재개, 200k 리그 실행기, ONNX frozen300초 평가, paired bootstrap20,000회, 준비/승격 gate, 학습·평가 inspector, champion pinned 등록 도구를 구현했다. 원본 ONNX 확률 graph와 독립 NumPy seed sampling을 사용하며 runtime/정책/평가기 SHA와 seed를 기록한다.
- 첫 준비 시도 `MNG_MS2V2-20260922-r001`은44,849에서 일반 MS의 실제 회수 계측 누락을 발견해 정상 중단했다. 기존 회수 보상 카운터는 M2 전용이어서 일반 MS의 실제 회수가0으로 보인 것이 원인이다. 진단 Run은 보존·선택 제외했고 초기값으로 재사용하지 않았다.
- `observedRecoveries`를 관찰 전용으로 추가했다. 기존 상대 안정 소유.30초→자기 안정 소유.20초 기준을 재사용하며 중립 허용, 골 정지/새 episode 자격 초기화, 중복 소유 제외다. **보상 수치·판정·물리·244관측·6action은 변경하지 않았다.** 일반 MS 회수 보상은 기존0→변경0이다.
- 새 runtime SHA는 `8505e2cfbab3dcc5a24f60685caac4302c3e4e64a979c310bcf67eb08ad1b598`, 빌드는 `Builds/MNG_V2/R5R6-20260922-r2`다. 실제 field3인 skill/phase를 판단 직전 관측에서 집계하는 읽기 전용 평가 계측도 추가했다. 동일 seed2경기의 득실·명령·사건·정체가 추가 전후 일치했다.

### R5 준비 결과

각 대진은 Full-v2 상대,20 seed쌍/40경기/300초다. 점수율은 `(승+0.5×무)/경기수`다.

| 정책 | 승/무/패 | 점수율 | 득실 |
|---|---|---:|---:|
| 무작위 유효 명령 | 0/0/40 | .0000 | 6:253 |
| fresh step0 | 0/0/40 | .0000 | 14:225 |
| step100,680 | 16/4/20 | .4500 | 89:88 |
| step200,120 | 33/4/3 | .8750 | 141:55 |
| 단순 carry-shot | 33/6/1 | .9000 | 123:28 |

최종 Red/Navy .90/.85, paired95% [.775,.9625], 실제 슛117/95·회수114/104로 준비 gate를 통과했다. 최종 pass strike1·완료 패스0이다. 준비 통과 정책의 ONNX SHA는 `3e8cd87b21ce50d1dd45384ce9f28145442123dbfb41aed6ee0ae6ce12ec5fd7`이며 R6 step0와 동일함을 확인했다. R6 초기 optimizer 상태 항목은0이었다.

### R6 학습 안정성

| 점검 구간 | 실제 저장 step | 누적 optimizer 갱신 | 구간 완료 경기 |
|---|---:|---:|---:|
| 100k | 100,110 | 1,152 | 152 |
| 200k | 202,112 | 2,298 | 150 |
| 300k | 300,147 | 3,384 | 149 |
| 400k | 402,389 | 4,560 | 149 |

- 매 구간32 worker, 총600경기, resume3회, 상대 교체39회(latest11/recent19/pinned9), 진영 교대9회. 로그 기준 최대 Approx KL .002029, 최대 clip fraction .026367, 최종 entropy .329275. checkpoint/optimizer/pool/로그 유한성 검사 및 명령 무결성 검사 통과.
- 모든 학습 완료 경기가 상대 교체 enqueue 구간과 겹쳤다. 정확한 worker 전달 시각은 미계측이며 이 승패/ELO를 frozen 승격 성적으로 사용하지 않는다. Unity 월드와 미전달 trajectory는 resume 시 다시 시작하므로 uninterrupted 실행과 bit-identical하다는 주장은 하지 않는다.
- 300k 구간 시작 시 CUDA 초기화 경고1회가 있었으나 현재 trainer의 GPU 등록, 별도 CUDA 접근, step 증가 및 후속 저장 유한성을 확인했다. 강제 CPU 전환이나 설정 변경은 하지 않았다.

### R6 고정 리그와 승격

일반 대전은 seed592201..592220의 양 진영40경기, holdout은 새seed882201..882240의 양 진영80경기다. 모두300초, 같은 runtime/추론 모드이며 경기 도중 정책 교체가 없다.

| 후보/상대 | 경기 | 승/무/패 | 점수율 | 득실 | Red/Navy | paired95% |
|---|---:|---|---:|---:|---|---|
| 200k / step0 | 40 | 22/7/11 | .6375 | 127:82 | .650/.625 | [.5125,.7625] |
| 200k / Full-v2 | 40 | 31/7/2 | .8625 | 151:39 | .825/.900 | [.750,.950] |
| 400k / step0 | 40 | 24/9/7 | .7125 | 110:67 | .875/.550 | [.600,.825] |
| 400k / 200k | 40 | 18/7/15 | .5375 | 99:84 | .725/.350 | [.400,.675] |
| 400k / Full-v2 | 40 | 34/2/4 | .8750 | 141:36 | .950/.800 | [.7875,.950] |
| 400k / step0 신규 holdout | 80 | 47/14/19 | .6750 | 269:179 | .8125/.5375 | [.5875,.75625] |

승격 기준: 최소400k, holdout≥.60/하한>.50, 양 진영≥.50, 득점≥실점, 역사 평균≥.55/각상대≥.45, 무작위 이상, 무결성. 실제 역사 평균.625·최저.5375로 전부 통과했다. R0는 초기 .875와 동일해 .05 초과 회귀 추가검사 조건에 해당하지 않았다. 200k 상대의 신뢰구간은 .50을 포함하므로 모든 과거 모델에 대한 확정 우위를 주장하지 않는다.

### 단순 기준과 행동 한계

| 상대 | step0 점수율/득실 | 400k 점수율/득실 | 400k Red/Navy |
|---|---|---|---|
| uniform-valid | 1.000 / 235:5 | 1.000 / 285:5 | 1.000/1.000 |
| recover | .375 / 64:90 | .450 / 63:67 | .525/.375 |
| balanced | .8625 / 131:42 | .875 / 161:49 | .875/.875 |
| carry-shot | .300 / 68:135 | .500 / 74:70 | .575/.425 |

- 400k의 정규+holdout360경기에서 실제 pass strike는 전부0이다. 초기 정책 상대 비소유 Recover 선택은200k87.14%→400k94.67%, 소유 Recover67.41%→67.83%다. 같은 대진 실제 슛171→160, 회수205→209, 정체112→125로 점수율 증가와 행동 지표가 일관되게 좋아진 것은 아니다.
- 400k 대 step0의 비소유 실제 평균 Press 인원은 Recover1.91/Balanced.92/ProtectBack0, Cover는.95/1.86/2.86이다. 정책 명령의 P1 인원 의미가 실행 관측에 나타나지만 인과적 전술 우위 실험은 아니다.
- 해당40경기의 양 팀 기술 실행 tick 중 SafetyEscape 비중은3.13%이며 원명령 강제 교체율과 다른 지표다. raw/effective 불일치·override·직접 blocked-pass 판단 보상은0이다. KeeperTechnique와 정체 해소 기술은 공통 실행 코드에 남아 있고 출처별로 기록한다.
- 같은 대진의 양 팀 task 결과는Accepted183,526, DeferredCommit19, RejectedInvalidState8,737, 소유 상실 취소798, 경기 상태 취소1,376이다. stale/owner 거절은0. 전체 임무/출처 카운터와 bounded trace 표본을 구분하며, 정확한 임무 지연·세부 거절 원인은 추가 진단 대상이다. on-target/blocked/save 계측과 수동 화면 검수는 완료하지 않았다.
- 진영 진단80경기는 승격 표본에서 제외했다. 동일 step0 정책 대전 Red/Navy .55/.30(차이 paired95%[-.025,.525]), 동일400k .675/.40(차이[.05,.50])이다. 이 seed 묶음에서 공통 진영 효과가 관찰되지만 환경 결함이나 학습만의 원인으로 확정하지 않는다.
- 200k→400k의 초기 상대 점수율 차이+.075의 paired95%는[-.10,.25], R0 차이+.0125는[-.10,.15]다. 단순 carry-shot 상대는 초기 대비+.20이지만 차이 구간[0,.3875]로 경계다. 다중 비교 전체 우위나 한 학습 seed의 독립 재현을 주장하지 않는다.

### 수정·연장 판단과 보존

1. **필수 실행 오류는 해소됐고 저장 상태에서 기술적 재개는 가능하다.** 모델/optimizer/pool/초기 reference/champion을 모두 보존했다. 현재 학습은 종료 상태다.
2. **성능 목적의 자동 연장은 보류한다.** 다음 작업은 PassBuild 유효 mask 기회 빈도와 유효할 때 선택확률, 실제 패스 수락→실행→수신의 단절 지점, 양 진영의 mirror spawn/좌표·관측·추론 RNG 조건을 분리하는 진단이다. 단순히 패스 보상을 올리거나 명령을 강제로 바꾸지 않는다.
3. 원인이 계측/평가에만 있다면 runtime/보상을 유지한 동일 checkpoint 재개를 검토한다. 관측·물리·보상/전술 의미를 수정하면 새 환경/실험으로 분리하고 checkpoint 호환·재평가를 확인한다. 개선 근거가 확보되면 다음200k 단위로 판단하며1M 자동 진행은 하지 않는다.
4. 검증: MNG EditMode122/122, pool3·PPO metrics2·평가기4·gate5·champion pin2 테스트 통과, 정식 빌드 성공, 최종 ONNX/PyTorch 확률 최대오차1.79e-7. 정규평가800경기+추가 진영 진단80경기, 총21개 대진의 Player 로그에서Exception/Assertion 실패0. 수동 시각 승인·독립 학습 seed 재현은 미완료다.
5. 시작 snapshot의 기존 자산·설정275개와 은퇴 모델25개 SHA 유지. ProjectSettings/EditorBuildSettings 원본 바이트 유지. 원본 Run·PT·ONNX·실패/진단 로그 보존, commit/push 없음. `R5R6-preservation-check.json`, `R5R6-final-player-log-audit.json` 참조.

주요 증거는 `Logs/MNG-Rebuild/MNG_MS3V2-20260922-r005/inspect-{100k,200k,300k,400k}.json`, `R6-final-assessment.json`, `R6-progression-paired.json`, `R6-side-bias-analysis.json`, 각 대진의 `report.json`/`behavior-audit.json`이다. 최종 소스 snapshot은 `Logs/MNG-Rebuild/R6-final-source-20260922/source-sha256.json`이다.

## 2026-09-22 R5/R6 정식 실행 — 아래는 진행 당시 기록, 위 완료 결과가 현행

- 사용자 후속 요청으로 R5 준비 및 gate 통과 후 R6 400k self-play의 개발·학습·평가가 승인됐다. R7 이후는 이번 실행 범위가 아니다.
- 정식 실행기 `Tools/MNG_V2_Train.ps1`, 초기 정책 저장·100k 안전 중단 어댑터, ONNX frozen 300초 paired 평가기, 준비/승격 판정 함수와 inspector를 추가했다. 추론은 원본 ONNX의 categorical 확률 graph와 독립 seed 샘플링이며, 환경 seed·추론 seed·모델/runtime SHA를 평가 manifest에 동결한다.
- 첫 정식 시도 `MNG_MS2V2-20260922-r001`은 일반 MS의 회수 사건 계측 누락을 발견해 44,849 step에서 정상 저장·중단했다. optimizer update 480, 학습 완료 경기 57개, 득실 28:256이며 무결성/finite 검사는 통과했다. 회수 보상 카운터는 M2 커리큘럼에서만 지급되고 일반 MS 회수를 나타내지 않는다는 것이 원인이다. 이 Run은 진단 전용이며 다음 초기값으로 사용하지 않았다.
- 보상/물리/관측/action 변경 없이 `observedRecoveries`를 별도 추가했다. 기존 M2 상대 소유 .30초→자기 소유 .20초 기준, 중립 허용, 골 정지/새 episode에서 자격 초기화, 중복 자기 소유 제외다. 실제 회수와 기존 회수 보상 지급 0을 구분한다. MNG EditMode 122/122 통과. 상세 보상 경계는 [보상표](../../soccer/rewards.md)를 따른다.
- 정식 r2 빌드는 `Builds/MNG_V2/R5R6-20260922-r2`이며 공통 runtime SHA는 `8505e2cfbab3dcc5a24f60685caac4302c3e4e64a979c310bcf67eb08ad1b598`이다. `MNG_MS2V2-20260922-r002`는 fresh network/optimizer, Full-v2, 32환경으로 100,680에서 정상 저장 후 동일 설정/optimizer로 200,120까지 완료했다. optimizer minibatch 갱신은 1,113→2,220회다. 첫/후반 완료 경기 143/142개, raw/effective·출처·finite 검사 통과. 원본 source/segment별 Player 로그와 checkpoint를 보존했다.
- 기준 무작위 정책의 새 버전 고정 40경기: 0승0무40패, 점수율 0, 득실 6:253, 실제 회수233, 슛34, 무결성 통과. `Logs/MNG-Rebuild/R5-r2-uniform-vs-full/report.json`에 증거를 보존했다. 이는 정식 학습 정책의 성능이 아니다.
- R5 고정 평가(각 20 seed쌍/40경기/300초/Full-v2): step0는 0승0무40패·점수율0·득실14:225, step100,680은 16승4무20패·0.45·89:88, step200,120은 33승4무3패·0.875·141:55다. 최종 Red/Navy 점수율 .90/.85, paired bootstrap95% [.775,.9625], 양 진영 실제 슛117/95·회수114/104로 준비 gate 통과. 최종 실제 pass strike는 1회, 완료 패스는 0으로 패스 활용은 부족하다. 같은 조건의 단순 carry-shot 정책은 .90·123:28이므로 신경망이 이 기준보다 확실히 우월하다고 주장하지 않는다.
- R5 판정은 `Logs/MNG-Rebuild/R5-r2-200k-approved-gate.json`, 각 고정 경기 보고서는 `R5-r2-{step0,100k,200k,uniform,carryshot}-vs-full/report.json`에 있다. 최종 ONNX SHA `3e8cd87b21ce50d1dd45384ce9f28145442123dbfb41aed6ee0ae6ce12ec5fd7`를 reference로 등록했다. 원본 ONNX 확률과 frozen PyTorch actor의 64입력×6행동 수치 대조 최대 절대오차1.49e-7 통과.
- R6 정식 Run `MNG_MS3V2-20260922-r005`를 승인된 준비 actor만 가져와 새 optimizer로 시작했다. step0 ONNX SHA가 R5 최종과 동일하고 optimizer 상태 항목0임을 검사했다. 32환경, latest/recent/pinned30/50/20, 100k마다 정상 저장 점검, 200k/400k 독립 리그를 적용한다. 400k 이전 일반 성능 정체만으로 종료하지 않으며 무결성 중단선은 유지한다. 사용량 기준은 2026-09-23 폐지했다. R6 학습 중 기록은 승격용 고정 평가가 아니다.
- R6 상대 교체 audit에는 UTC·step·구간·SHA·진영을 기록한다. 완료 episode와 교체 enqueue 시각의 겹침을 별도 집계하며 정확한 worker 전달 시각은 미계측이다. frozen 외부 평가는 경기 도중 정책을 바꾸지 않는다.
- R6 첫 구간은 100,110 step, optimizer 1,152회, 완료 경기152개에서 정상 저장했다. 32 worker, finite/명령/킥 출처 검사 통과, 최대 KL .002029·clip fraction .026367, 최종 entropy .56027이다. 상대 교체9회(latest4/recent4/pinned1), 양 진영 학습 및 step0 pinned1을 확인했다. 모든 완료 경기가 상대 교체 enqueue 구간과 겹쳤으므로 학습 경기 승패를 frozen 리그 결과로 해석하지 않는다. `inspect-100k.json` 보존 후 동일 optimizer·pool을 복원해 200k 구간을 시작했다.
- R6 step0 단순 기준 대전(각40경기) 점수율/득실: uniform-valid 1.00·235:5, recover .375·64:90, balanced .8625·131:42, carry-shot .30·68:135. carry-shot 상대 진영별 .40/.20, paired95% [.20,.4125]이며 준비 gate 통과가 모든 단순 전술 상대의 우위를 뜻하지 않는다. 소유 상태 회수 선택48.99%·패스 .95%, 실제 pass strike2회로 개선 관찰 대상이다. `R6-step0-vs-*/report.json`과 `behavior-audit.json`에 보존했다.
- R6 두 번째 구간은 202,112 step, optimizer2,298회에서 정상 저장했다. 누적 완료 경기302개·상대 교체19회·recent19/pinned1, 첫 resume counter 일치, finite/무결성 통과. 최종 entropy .44683, 최대 KL .002029/clip .026367이다. `inspect-200k.json` 확인 후 300k 구간으로 재개했다. 시작 시 CUDA 초기화 경고1회가 있었으나 현재 trainer PID의 GPU 등록·CUDA 접근·step 증가를 확인했다. ONNX/PyTorch 확률 대조 최대오차1.64e-7 통과.
- R6 200k 대 step0 frozen40경기는22승7무11패, 점수율 .6375, 득실127:82, Red/Navy .65/.625, paired95% [.5125,.7625]다. 개발 seed 개선 신호이며 holdout 승격 결과가 아니다. 실제 슛171·회수205·패스0, 정체112. 비소유 회수 선택87.14%, 소유 회수67.41%, 패스 선택0으로 전술 편중은 남는다. 실제 비소유 평균 Press 인원은 Recover1.90/Balanced.93/ProtectBack0이다. `R6-200k-vs-step0/{report,behavior-audit}.json` 보존.
- R6 200k 대 Full-v2 frozen40경기는31승7무2패·.8625·151:39, Red/Navy .825/.90, paired95% [.75,.95]다. 초기 reference .875·141:55 대비 점수율은 -.0125로 유지 수준이고 득실은 개선됐다. 실제 슛249·회수227·패스0·정체102, 무결성 통과. 신경망 상대 개선과 R0 유지 신호를 확인했으므로 정상 최소400k까지 계속하며, 더 높은 step이나 reward만으로 승격하지 않는다.
- R6 학습은 100,110→202,112→300,147→402,389에서 각 구간 정상 저장했고 최종400k 범위를 완료했다. optimizer1,152→2,298→3,384→4,560, 완료 학습 경기152+150+149+149=600, 32 worker/구간 및3회 pool 복원 검사 통과. 최종 recent30/pinned1, 교체39회(latest11/recent19/pinned9), 양 진영, 로그 기준 최대 KL .002029·clip .026367, 최종 entropy .329275, finite/명령/킥 출처 검사 통과. 모든600경기가 상대 교체 enqueue 구간과 겹쳐 학습 승패를 frozen 성적으로 쓰지 않는다. R5 준비 경험200,120과 R6 추가402,389를 구분하며 계보상602,509다.
- 최종 ONNX/PyTorch 대조 최대오차1.79e-7 통과. `inspect-400k.json` 확인 후 trainer를 종료하고 8개 평가 환경으로 사전 선언한 기본360경기를 시작했다. R0 점수율이 초기보다 .05 초과 하락하면 새seed792201..792220에서 초기/최종 모델 각40경기를 추가 확인한다. champion일 때만 영구 풀에 추가하는 `mng_v2_pin_champion.py`는 원본 pool을 보존하고 optimizer·스케줄·RNG를 유지하며 중복 방지2테스트를 통과했다. 아직 실제 champion 등록은 하지 않았다.
- 현재 진행 파일은 `Logs/MNG-Rebuild/MNG_MS3V2-20260922-r005/progress.json`, 초기 정책 증거는 같은 폴더 `initial-policy-proof.json`이다. R6 400k 학습은 완료됐고 최종 성능 평가·champion 승격은 아직 진행 중이다. `Tools/MNG_V2_League.ps1`은 상세 평가 시작 전 후보/상대 SHA·경기 수·seed를 동결하고 대진을 순차 실행한다. 행동 감사는 양 팀 합계 임무/출처 카운터와 후보 조건부 명령 비율을 구분한다.
- R6 상세 평가용으로 기존244관측에서 판단 직전 실제 필드3인의 skill/phase를 직전 명령·소유 상태별 집계하도록 읽기 전용 계측을 추가했다. 정책 입력·action·RNG·runtime은 불변이다. 기존 step0 carry-shot 2경기를 재현해 득실·명령·조건부 명령·사건·회수·킥·정체가 모두 일치했고 평가 테스트4개가 통과했다. 비소유 평균 압박 인원은 이 작은 표본에서 Recover1.92/Balanced.93/ProtectBack0이며 성능 gate를 대체하지 않는다. `R6-execution-telemetry-parity/parity.json` 보존. 이전40경기 baseline에는 이 추가 인원 계측이 없고, 정확한 임무 지연은 여전히 미계측이다.

## 2026-09-22 MS v2 R1~R4 구현 — 이전 완료 기록

- 후속 사용자 승인 범위인 R1~R4 구현·자동 검증·빌드·32환경 smoke/resume를 완료했다. R5 정식 준비 학습과 R6 장기 self-play는 시작하지 않았다. 다음 단계는 아래 미완료 평가 운영 도구를 보완한 뒤 R5를 진행하는 것이다.
- R1: `Logs/MNG-Rebuild/R1-20260922-115145/source-sha256.json`에 HEAD와 실제 dirty/untracked 파일 3,429개의 SHA를 기록하고 소스를 복사·해시 대조했다. 기존 Run은 제자리 보존했다. [은퇴 registry](../../soccer/training/ms-v2-model-registry.json)에 구 MS3 PT/ONNX 25개를 `retired-architecture-v1`로 등록했으며, SHA 기반 v2 선택 거부를 확인했다. [244 관측 명세](../../soccer/training/ms-v2-schema.json)를 동결했다.
- R2: 명시적 `UseRuntimeV2` 경로에 임무 수명(준비 취소, 발사 중 최신 1개 대기, 종료 후 재검증), 실행 관측 111개 append, raw/rewarded/capped 계측, 실제 킥의 task/command/receiver/source와 bounded 진단 trace를 구현했다. v1 경로의 133 관측·Builder·기존 씬은 보존했다. 기존 보상 숫자/판정은 유지했다.
- R3: v2 Planner는 회수/균형/보호의 추격 압박을 2/1/0명으로 분리한다. 소유 시 운반/수신/리바운드/후방 배치, 보호 횡이동과 요청 속도를 구분하고 자동 keeper 배급·정체 슛·boundary 공격 킥을 제거했다. keeper 방어 기술과 충돌 회피는 유지하며 출처를 계측한다. 새 씬은 `Assets/_Soccer/Manager/Curriculum/MS_V2`에 별도로 생성했다.
- 검증: R2 MNG EditMode 108/108, 압박 단계 14/14, 공격 단계 MNG 121/121, 전체 Soccer EditMode 259/259, 전체 MNG PlayMode 29/29 통과. v2 초기 snapshot의 Awake 순서 문제를 수정했고 v2 동적 검사를 재통과했다. 마지막 임무/모델 검사 보완 후 MNG EditMode 121/121 및 v2 PlayMode 1/1 재통과. 초기 실패 로그는 삭제하지 않았다.
- R4 빌드: 300초 MS2V2/MS3V2/EvaluationV2 Windows Player 3개를 `Builds/MNG_V2`에 새로 생성했다. 공통 runtime SHA는 `d0253a7c29b55397f12a8342e1507839f8b9af9141c38dcc1e669cd7febaa582`이며 각 디렉터리의 `build-info.json`에 실행 파일/level/schema SHA가 있다. EvaluationV2는 아직 학습된 후보가 없는 평가 기반 씬/Player다. 독립 frozen-model 리그·paired seed·승격 자동화는 후속 구현 사항이다.
- R4 self-play: 프로젝트 내부 GhostTrainer adapter에 latest/recent/pinned 30/50/20 혼합과 pool/RNG/진영/counter sidecar, optimizer checkpoint와 동시 저장을 구현했다. 단위 검증 3/3으로 다음 50회 상대 선택 재현, checkpoint step 불일치 거부, pinned 가중치 분리를 확인했다. 추가 KL/clip 계측의 loss/gradient 불변 및 예외 후 복구 검증 2/2도 통과했다.
- R4 선행 smoke `MNG_MS3V2-20260922-r003`: 32 worker씩 최초/재개 연결, `18,562 → 33,378` step, optimizer minibatch update `150 → 303`, 완료 300초 경기 `28 + 20`개. 저장된 pool/진영/counter와 재개 상태가 일치하고 최종 ONNX 입력은 244다. 수집된 ring trace의 중복 킥/raw-effective 불일치는 0, checkpoint/전체 TensorBoard scalar는 finite다. `Logs/MNG-Rebuild/MNG_MS3V2-20260922-r003/smoke-verification.json` 참조. 이 Run에는 KL/clip 기본 로그가 없으므로 해당 진단 통과를 주장하지 않는다.
- R4 최종 KL/clip 포함 smoke `MNG_MS3V2-20260922-r004` 완료: 최초/재개 각각 32 worker, `19,034 → 33,850` step, optimizer minibatch update `153 → 303`, 완료 300초 경기 `28 + 20`개. 수집된 의사결정 trace 54,540개/실제 킥 223개에서 raw/effective 불일치와 중복 킥 0, schema/finite/pool·counter 재개 검사 통과. 기록된 summary의 approximate KL 범위 `0.000153~0.002278`, clip fraction `0~0.047647`, entropy `1.16308~1.17509`이며 모두 finite다. 최종 ONNX SHA는 `e6c24e7b3006b5a9f18cd7b2134ad5357932bc68e045dc44e12f43a77600e214`. 상세 증거는 `Logs/MNG-Rebuild/MNG_MS3V2-20260922-r004/smoke-verification.json`, 최초 checkpoint는 `first-stop`, 최초 Player 로그는 `first-run-logs`에 보존했다. 목표 step 초과는 32환경 trajectory 처리 단위에 따른 자연 종료다. trainer exit 0, 잔류 worker 0 확인.
- 최초 r001 adapter 경로 오류, r002 너무 짧아 optimizer update가 없었던 시도도 증거로 보존했고 성공 증거로 사용하지 않았다. smoke Run 전체를 registry에서 정식 후보 제외했다. 최종 Run은 worker 재시작 허용 0으로 실행했다. RAM 사용 76%, 여유 7.61GiB를 확인했으며 사용량 조회상 주간 잔여 91%, 5시간 값은 제공되지 않았다.
- 재개의 범위는 optimizer·상대 pool·선택 RNG·진영/swap counter 복원이다. Unity 월드와 미전달 trajectory는 다시 시작하므로 중단 없는 실행과 bit-identical하다고 주장하지 않는다. ring trace는 bounded 표본이며, on-target/blocked/save는 현재 unavailable로 명시한다.
- 보존: 시작 snapshot 기준 기존 Manager 자산과 ProjectSettings/EditorBuildSettings 및 은퇴 PT/ONNX SHA를 재검사했다. Unity가 자동 추가한 define은 원본 바이트로 복원했다. 보존 증거는 `Logs/MNG-Rebuild/final-preservation-check.json`이다. 자동 commit/push, 기존 Run 삭제/덮어쓰기 없음.
- 보상 수치·물리 프로필 변경 없음. 자동 테스트 통과는 수동 화면 승인이나 학습된 감독의 경기력 증명이 아니다. 정식 평가·champion 승격·장기 학습 완료를 주장하지 않는다.
- 최종 구현 소스 보존 위치: `Logs/MNG-Rebuild/R4-source-20260922/source-sha256.json`. PPO는 기존과 같이 6개 팀 명령의 선택/전환을 학습하며 선수 이동·목표/수신자 선택·조준·물리 킥은 공통 기술 코드가 실행한다. R5는 smoke checkpoint를 초기 정책으로 채택하지 않고 fresh network/optimizer로 시작해야 한다. 정식 100k 모니터링/200k 독립 평가 실행기와 reference/candidate/champion 운영을 준비한 후 진행하며, 이번 작업에서 해당 운영까지 완성했다고 간주하지 않는다.

## 2026-09-22 MS 구조 개편·재학습 계획 — 계획 작성 당시 상태

- 사용자 제안을 검토해 [개편 주 계획](../manager/ms-v2/ms-rebuild-plan-20260922.md), [P1·런타임 계약](../manager/ms-v2/ms-rebuild-runtime-contract-20260922.md), [학습·평가·승격 계약](../manager/ms-v2/ms-rebuild-training-validation-20260922.md)을 작성했다. 이번 작업은 계획과 지침 작성만이며 코드/모델/씬/빌드/학습을 변경·실행하지 않았다.
- 기존 300k까지 MS3 모델은 현행 선택 자격 폐기. 실제 파일은 감사 이력으로 보존하며 기존 100k/200k로 대체하거나 재학습 초기값으로 사용하지 않는다. 구현 단계에서 은퇴 registry와 Builder 차단을 반영해야 하며 아직 미구현이다.
- 기본 경로는 기술 보조 유지 + P1 전략 분리 + 임무 상태 관측/추적 + 실제 사건 계측을 구현한 뒤 fresh MS2-v2 준비, MS3-v2 self-play다. 관측 v2 시작 설계는244 float로 문서화했으며 아직 적용하지 않았다.
- 새 실험은32환경·100k 점검·200k 상세평가·정상 self-play 최소400k, 진척 시1M이다. 과거 모델 우위로 champion을 승격하고, 1M 엄격 평가 후200k씩2M/3M까지 단계 확장한다. 무결성 중단선 유지. 사용량 기준은 2026-09-23 폐지했다. 구 MS3의 완료/모델 선택/모니터링 규칙은 과거 기록으로 남긴다.
- 다음 작업은 별도 구현 요청 후 주 계획 R1부터 순차 실행이다. 계획 체크리스트와 제안 수치는 완료된 개발·실험 결과가 아니다.

## 2026-09-22 감독 RL 구조 감사 — 이전 상태

- 사용자 요청에 따라 활성 MNG 경로의 관측·마스크·Planner·Executor·보상·self-play trainer·평가/승격과 기존 JSON/TensorBoard를 확인했다. [구조 감사 및 수정안](../manager/ms-v2/rl-architecture-audit-20260922.md)에 확정 사실과 미검증 원인 가설, 후속 구현 순서를 기록했다. 런타임/보상/YAML/모델 변경과 신규 학습·경기는 수행하지 않았다.
- 과거 3개 정책 상대 300k의 합산 점수율은 `44.58%`다. 초기 상대 `55%`는 표본상 근소 우세로서 확정적 일반 우위를 뜻하지 않는다. 기존 MS3 완료는 R0 gate/파이프라인 완료로 유지하며 역사 정책 리그 우세는 미달이다. 100k/200k R0 평가는 120초, 300k는 300초이므로 동일 조건의 학습 곡선으로 사용하지 않는다.
- 전술 카운터는 `Award(...) != 0`일 때 증가하므로 실제 사건 전체가 아닌 보상 지급 사건 수다. 과거 유효 슛 `+6.1%` 해석은 이 한계를 포함한다. 승패·득실 원본은 유지한다. 명령과 무관한 이중 압박·keeper 배급·정체 슛, 새 임무 수락 거절 상태의 관측 누락도 확인했다. P0 raw/effective 일치는 유지되지만 모든 실제 기술 실행을 PPO가 선택했다는 뜻은 아니다.
- 후속 권장 순서는 동일 조건 리그/계측 정비 → 작은 단위의 P1/실행 상태 관측 → 역사 강자 보존 self-play → 진척에 따른 1M 이후 확장이다. 수백만 step만으로 과거 모델 압도를 보장하지 않는다. 재집계 및 53개 Runtime/YAML source SHA 대조 증거는 `Logs/MNG-MS/architecture-audit-20260922/`에 보존했다. 후속 구현은 이번 작업에서 시작하지 않았다.

## 2026-09-21 MS3 PPO self-play 완료 — 이전 상태

- 완료 후 checkpoint 직접 대전을 추가했다. 초기 P0·100k·200k를 각각 300k와 양 진영 20경기씩, 대진당 40경기·총 120경기로 평가했다. 300k는 초기 P0에 score rate `0.55 대 0.45`, 득실 `115:88`로 이겼지만 100k에는 `0.40 대 0.60`, 득실 `93:98`, 200k에는 `0.3875 대 0.6125`, 득실 `86:104`로 졌다. 전 대진 action 불일치·override·직접 blocked-pass 보상은 0이다. 따라서 300k의 R0-Full 완료 gate는 유지하지만 모든 self-play checkpoint에 대한 직접 우세는 입증되지 않았다. 상세 결과는 [MS3 checkpoint 직접 대전](../manager/ms-v1/ms3-checkpoint-duel-results-20260921.md)을 따른다.
- MS3를 공식 Run `MNG_MS3-20260921-r002`로 완료했다. MS2 P0 `step99968`의 정책 가중치만 불러오고 새 optimizer로 시작했으며, Red/Navy PPO가 같은 `MNG_Manager` BehaviorName과 TeamId 0/1로 32개 Windows Player 환경에서 self-play했다. P1은 포함하지 않았다.
- 공식 Run은 aggregate `300,028` step에서 자연 종료했다. 50k 간격의 PT·ONNX·inspector·행동 무결성 증거를 보존했고, 484개 완료 episode에서 32 worker와 양 팀 연결을 확인했다. snapshot 상대 교체는 11회, 학습 진영 교대는 2회였으며 raw/effective action 불일치, 강제 override, 직접 blocked-pass 판단 보상, 무결성 위반은 모두 0이다.
- R0-Full 상세평가는 100k에서 score rate `0.725`와 득실 `23:7`, 200k에서 `0.625`와 `20:10`이었다. 최종 300k 평가는 300초·양 진영·ONNX/무작위 각 20경기인 총 80경기로 수행했다. 최종 ONNX는 합산 `0.875`, Red `0.825`, Navy `0.925`, 득실 `137:29`였고 무작위 `0.1625`보다 `+0.7125` 높았다. 같은 seed의 P0 합산 `0.75`보다 `+0.125` 높다.
- 최종 전술 지표는 유효 슛이 P0의 분당 `1.72`에서 `1.825`로 `6.1%` 증가해 5% 개선 gate를 통과했다. 5m 전진은 `4.975 -> 4.965`로 유지 수준이었다. 평가의 유효 `PassBuild` 명령은 0회이므로 우연히 기록된 완료 패스를 패스 판단 학습의 근거로 사용하지 않는다.
- 선택 모델은 `results/MNG_MS3-20260921-r002/MNG_Manager/MNG_Manager-300028.onnx`이며 SHA-256은 `1d0916383887939430401de9058c68ab1d3b841b654569d2aaf6c3db81df2899`다. 번호가 붙은 PT SHA-256은 `ee905613f218c3b863b7111b5076adf845ec43fc6c2721e9c7466bb59b68d8ce`다. 이 모델이 300k 최소 완료 gate를 모두 통과했으므로 400k~1M 연장은 하지 않고 동결했다.
- 사람 확인용 빌드는 `Builds/MNG_MS/MS3-Final-Review/MNG_MS3_Final_Review.exe`다. Red/Navy 모두 같은 동결 PPO, `policyAssistMode=None`, 300초, 1배속이며 25초 headless smoke에서 정책 2·규칙형 0, 양 팀 각각 60판단, override/직접 보상 0, 예외 0을 확인했다. 완료 근거는 [MS3 완료 보고서](../manager/ms-v1/ms3-completion-20260921.md), 육안 절차는 [MS3 사용자 확인 체크리스트](../manager/ms-v1/ms3-user-checklist.md), 기계 판독 결과는 `Logs/MNG-MS/MS3-completion-20260921/ms3-completion.json`을 따른다.

## 2026-09-21 MS3 PPO self-play 구현·학습 착수 — 이전 상태

- 사용자가 MS2 P0를 최종 완료가 아닌 임시 합격으로 판단했다. P1과 P0 사람 확인 승인을 건너뛰고 P0 `step99968` PT에서 MS3를 시작한다. MS3가 우선이며 MS2의 임시 합격 표시는 유지한다.
- 중단으로 남았던 P0 검수 빌드는 통과 ONNX로 다시 생성했다. `review-build-info.json`은 Run `MNG_MS2-20260921-r005`, candidate `step99968`, ONNX SHA-256 `9d4f49...e21dd`, R0-Full, `policyAssistMode=None`, 1배속, build success/error 0이다. 22초 headless smoke에서 Red PPO/Navy R0-Full로 정상 부팅했고 60판단까지 override와 직접 패스 보상 0, 런타임 예외 0을 확인했다. 사용자 눈 확인은 새 지시에 따라 MS3 진입 gate로 사용하지 않는다.
- MS3는 32환경, 정상 300초 경기, Red/Navy PPO self-play, 새 optimizer, 최소 300k·최대 1M으로 설계했다. 50k마다 checkpoint와 행동 무결성·ELO·상대 교체를 모니터링하고 100k마다 R0-Full 양 진영 고정 평가로 score·득실·유효 슛·5m 전진·완료 패스·명령 분포를 P0와 비교한다.
- 구현·Unity 검증·source snapshot·Windows Player build를 끝낸 뒤 공식 Run을 시작한다. 현재 시점에는 MS3 학습 결과를 주장하지 않는다. 상세 기준은 [MS3 개발·학습·평가 계획](../manager/ms-v1/ms3-development-and-training-plan-20260921.md)이다.

## 2026-09-21 MS2 P0 no-assist proof 완료·사용량 중단 — 이전 상태

- 공식 Run `MNG_MS2-20260921-r005`는 동일 MS1 `step7443` 정책 가중치에서 새 optimizer로 시작해 R0-Full·32개 Windows Player·seed `192001`로 학습했다. 최대 500k 계약을 유지하되 첫 평가 지점인 `step99968`이 통과하여 조기 종료 규칙에 따라 더 학습하지 않는다. P1 전략·Planner 변경은 포함하지 않았다.
- 학습 증거는 32 worker 시작, 고유 프로세스 32개, 완료 episode 148개다. 모든 episode에서 `policyAssistMode=None`, raw/effective 명령 불일치 0, pass override 0, 직접 blocked-pass 판단 보상 0, integrity flag 실패 0이다. `Tools/MNG_MS2_P0_CheckEvidence.ps1` 결과는 `Logs/MNG-MS/MNG_MS2-20260921-r005/p0-action-integrity.json`에 보존했다.
- 승격 ONNX는 `MNG_Manager-99968.onnx`, SHA-256 `9d4f49c31e696a52691d230a74abe25e5e04699a4e4293bbf351c2e7d51e21dd`다. 승격 PT는 `MNG_Manager-99968.pt`, SHA-256 `9ab8536762c0838960ec807fc943cc6e1cb4d166c4324db2d0406bb88ee3c0ca`다. Ctrl+C 종료 중 생긴 0바이트 `MNG_Manager-102912.pt`는 불완전 파일이므로 후보나 재개점으로 사용하지 않는다.
- seed `491001`의 전체 no-assist 고정 평가는 ONNX 합산 score rate `0.75`, Red `0.70`, Navy `0.80`, 득실 `98:35`, 무작위 `0.1625`, 격차 `+0.5875`였다. 독립 seed `592001` 재현 평가는 ONNX `0.925`, Red `0.95`, Navy `0.90`, 득실 `123:31`, 무작위 `0.0875`, 격차 `+0.8375`였다. 두 평가 모두 각각 총 80경기이며 raw/effective 불일치, override, 직접 보상은 전부 0이다.
- 두 seed가 합산 0.50, 진영별 0.40, 득점>=실점, 무작위 대비 +0.20과 행동 불변식을 모두 통과했다. 따라서 P0 학습·대규모 평가는 완료되었고 이 번호가 붙은 PT만 MS3 초기 정책으로 사용할 수 있다. MS3와 P1은 아직 시작하지 않는다.
- 마지막 사용량은 5시간 94% 사용(잔여 6%), 주간 46% 사용(잔여 54%)으로 5시간 중단선에 도달했다. 활성 trainer·Player·평가 프로세스는 0개다. 통과 ONNX를 넣은 최종 사람 확인 빌드와 단일 smoke만 남았으며, 정확한 재개 절차는 [P0 완료·중단 인계](../manager/ms-v1/ms2-p0-stop-handoff-20260921.md)에 기록했다.

## 2026-09-21 MS2 100k 확장 완료·코드 보조 감사 — 이전 상태

- 사용자 지시에 따라 기존 완료 Run `MNG_MS2-20260920-r002`를 덮어쓰지 않고 `MNG_MS2-20260920-r003`을 새로 실행했다. 선택 MS1 `step7443` 가중치와 새 optimizer, R0-Full, 독립 Windows Player 16개, seed `192002`, port `5920..5935`로 aggregate `100,056` step까지 자연 종료했다.
- 최종 ONNX SHA-256은 `4a27152d12d7ddb065463329d0d5cd4b58ee15540e466b317f8d6949f7c1f489`, PT는 `5b229d17150f6c105c4cc3bbde94af530b4a7a2589f3e0300e6b53bb50c339fd`, optimizer checkpoint는 `aa806b31ab3b3c33d3e2f290ee119c45c4bf6f7296df6184af7a0bd375888728`다. 20k 간격 checkpoint와 inspector를 보존했으며 마지막 reward summary는 `3.10`, 최근 20개 비가중 평균은 `0.874`, legacy reward leak은 0이다.
- 기존 런타임의 막힌 전진 강제 패스 보조가 켜진 R0-Full 300초 최종 40경기는 `16승 13무 11패`, 득실 `51:50`, score rate `0.5625`였다. Red `0.550`, Navy `0.575`, 같은 조건 무작위 `0.125`, 격차 `+0.4375`로 기존 MS2 gate를 통과했다.
- 코드 감사에서 MS2 학습·평가가 `-mngMS1LearnPassChoice true`를 전달하지 않아 PPO 요청 명령을 `PassBuild`로 사후 교체한다는 사실을 확인했다. 최종 원시 PassBuild는 3회였지만 적용 PassBuild는 807회였고 override는 804회였다. 이 때문에 완료 패스 보상이 PPO가 실제 선택한 다른 명령에 귀속될 수 있다.
- 같은 100k 모델·R0-Full·고정 seed에서 강제 패스 전환만 끈 300초 통제 평가는 `36승 1무 3패`, 득실 `123:35`, score rate `0.9125`였다. Red `0.900`, Navy `0.925`, 동일 no-assist 무작위 `0.1625`, 격차 `+0.7500`이며 raw/effective 명령 일치와 override 0을 확인했다. PPO 유효 슛 `374 대 240`, 5m 전진 `1014 대 591`로 무작위보다 높았다.
- 동일 no-assist 120초 진단에서 checkpoint score rate는 20k `0.675`, 60k `0.750`, 100k `0.800`으로 올랐다. 따라서 PPO가 전진·슛·회수 중심 명령 판단을 실제로 학습했다는 증거가 있다. 반면 최종 직접 PassBuild는 24,004판단 중 3회, 완료 패스 1회라 패스 판단을 학습했다고 보지 않는다.
- MS2 경기 성능 단계는 완료로 유지한다. 다만 100k Run도 강제 패스 보조가 켜진 상태에서 학습했으므로, MS3 전에 사후 action 교체와 명령 선택 직접 보상을 끈 16환경 proof Run을 같은 MS1 초기화에서 새 optimizer로 한 번 수행하는 것을 권장한다. Planner의 2인 압박·공격 지원·정체 슛·골키퍼 배급은 그 다음에 한 항목씩 명령 종속으로 바꾼다.
- 상세 수치·코드 근거·수정안·다음 승인 기준은 [MS2 100k 확장 및 코드 보조 감사](../manager/ms-v1/ms2-100k-and-code-assist-audit-20260921.md)에 기록했다. 이번 작업에서는 런타임 행동 코드를 변경하지 않았고 MS3도 시작하지 않았다.

## 2026-09-20 MS2 초기 완료 — 이전 완료 상태

- MS2 정상 R0 대전을 완료했다. 선택 정책은 `MNG_MS2-20260920-r002-step19912`이며 ONNX SHA-256은 `7fce2603698df4f3b586f19b27f41f3c7dc143eca8c966324a2013c476c1e1e2`, PT SHA-256은 `04fd43f2e3383d923b60a60483b386e78862cd78013539d087ad9a43f81b9e3f`다. MS3는 반드시 이 번호가 붙은 PT에서 새 optimizer로 시작한다.
- R0-Full 300초 최종 평가에서 ONNX는 Red `7승 7무 6패`, Navy `8승 5무 7패`로 양 진영 score rate가 각각 `0.525`였다. 합산 `15승 12무 13패`, 득실 `45:44`, score rate `0.525`다. 같은 build·seed의 무작위 유효명령은 합산 `2승 6무 32패`, 득실 `14:85`, score rate `0.125`였다. 격차는 `+0.40`으로 모든 MS2 gate를 통과했다.
- MS2는 선택 MS1 PT에서 새 optimizer로 시작해 정상 킥오프 120초·R0-Full·독립 Windows Player 16개로 학습했다. step `19,912`, `39,976`, `59,920`을 보존했으며 가장 이른 19,912가 120초 진단과 300초 최종평가를 모두 통과했으므로 추가 학습·후기 checkpoint 승격을 하지 않았다.
- 최종 정책의 원시 명령은 `448,13,175,6715,14019,2634`, 실제 적용 명령은 `289,843,175,6507,13640,2550`이다. 공통 차단 패스 보조가 830회 개입했으므로 직접 PassBuild 학습이 강해졌다고 과장하지 않는다. 같은 보조를 공유한 무작위 감독보다 `+40%p` 높고 양 진영·득실 조건을 통과했으므로 고수준 명령 판단의 학습 효과는 입증됐다.
- 전체 Unity EditMode `242/242`가 통과했고 최종 평가 Player는 build error 0, 네 평가 프로세스 exit 0, 결과 누락 0이다. 완료 증거는 [MS2 완료 보고서](../manager/ms-v1/ms2-completion-20260920.md), 확인 항목은 [MS2 사용자 확인 체크리스트](../manager/ms-v1/ms2-user-checklist.md), 기계 판독 manifest는 `Logs/MNG-MS/MS2-completion-20260920/ms2-completion.json`에 있다. MS3는 아직 시작하지 않았다.

## 2026-09-20 MS2 사전평가 통과·16환경 학습 중 사용량 중단 — 해소된 이전 상태

- 동결 MS1 후보 `MNG_MS1-20260920-r007-step7443`를 R0-Medium/Full, Red/Navy, ONNX/무작위 유효명령의 8조건으로 먼저 평가했다. 조건당 120초 10경기다. Full 합산 score rate는 ONNX `0.475`, 무작위 `0.150`으로 `+0.325`였고, Red `0.45 vs 0.15`, Navy `0.50 vs 0.15`였다. 미리 고정한 `Full +0.10`, 진영별 최대 열세 `-0.10` 기준을 통과했다. Medium은 ONNX 양쪽 `1.00`, 무작위 Red `0.90`·Navy `0.85`였다. 원본과 집계는 `Logs/MNG-MS/MS2-preflight-20260920-r001`에 보존했다.
- 이 결과는 Easy R0에서 정책과 무작위의 차이가 작았던 원인이 지나친 코드 보조만은 아니며, 상대가 강해지면 학습 정책의 판단 차이가 드러난다는 근거다. 다만 Full 사전평가의 ONNX 득실은 `4:7`, score rate는 최종 기준 `0.50`보다 `0.025` 낮아 MS2 학습이 필요했다.
- MS2는 Medium을 생략하고 정상 킥오프 120초·R0-Full로 구현했다. 선택 MS1 PT SHA-256 `ac8eb65dd7c633a6cb5e4dfa267daa92d63c8df5bfecb4031a4fe28c5cb4d3c3`의 정책 가중치와 새 optimizer로 시작하며, 새 보상이나 전술은 추가하지 않았다. source snapshot 387개 파일은 `Logs/MNG-MS/MS2-source-v1`, 학습 Player 매니페스트는 `Builds/MNG_MS/MS2/build-info.json`이다. Unity build는 `Succeeded`, error 0이다.
- 공식 Run `MNG_MS2-20260920-r002`는 정확히 16개 환경, seed `192001`, port `5900..5915`로 연결됐다. PT/ONNX는 step `19,912`, `39,976`, `59,920`에 저장됐다. 사용자가 5시간 중단 기준 도달을 알린 뒤 trainer 종료 신호가 중첩 PowerShell을 통과하지 않아, 마지막 완전 저장 checkpoint `59,920`을 보존하고 해당 Run의 trainer Python과 16개 Player만 지정 종료했다. 화면상 약 75k까지 진행됐지만 저장되지 않은 59,920 이후 구간은 유효 성과로 계산하지 않는다. 현재 관련 Player/trainer/port는 모두 0개다.
- step `19,912`의 120초 고정 진단은 ONNX score rate `0.50`, 무작위 `0.15`, 득실 `7:6`, Red/Navy 각각 `0.50`으로 진단 gate를 통과했다. **300초·20 seed×양 진영 최종 평가는 아직 실행하지 않았으므로 MS2는 완료가 아니다.** 정확한 재개 순서와 해시는 [MS2 사용량 중단 체크포인트](../manager/ms-v1/ms2-stop-handoff-20260920.md)에 기록했다.

## 2026-09-20 MS1 완료 — 최신 상태

- MS1의 16환경 병렬 PPO 학습, 고정 평가와 모델 선택을 완료했다. 최종 후보는 `MNG_MS1-20260920-r007-step7443`이며 ONNX SHA-256은 `311f72b3d625d76156ba581ae060610b701d68bd8e69ff2a7b3afee8cc9b4e81`이다. MS2는 시작하지 않았다.
- 최종 `MNG-MS1-v8` holdout에서 정책은 공격 `38/40`, 수비/전환 `38/40`, Pass 하위집합 실제 완료 패스 `4/20`을 기록했다. 동일 seed 무작위 유효명령은 각각 `31/40`, `36/40`, `4/20`이다. 공격·수비 최소값과 baseline 열세 허용치, 패스 최소 2회와 비열등 조건을 모두 만족했다.
- 패스는 사용자 결정에 따라 보조 gate만 소폭 낮췄다. 실제 2.5m 이동과 동료 0.15초 소유라는 물리적 완료 의미는 유지하고, 요구량을 `3 -> 2`, 무작위 대비 요구를 `+1 -> 0`으로 바꿨다. 공격·수비 판정, R0-Easy `0.35×/1.5초`, 새 holdout seed `405001/406001`은 유지했다.
- 프로젝트 전체 EditMode `242/242`가 통과했다. 최종 평가 Windows Player는 build error 0이며 `MNG_MS1_Protocol_v8.json`과 선택 ONNX의 hash가 평가 결과·빌드 매니페스트에서 일치한다.
- 최종 훈련용 Player와 사람 확인용 1배속 Player를 빌드했다. 둘 다 Unity `Succeeded`, error 0이며 protocol·source manifest·실행 파일·level·런타임 DLL hash가 매니페스트와 일치한다. 검수 Player는 `Builds/MNG_MS/MS1-Review/MNG_MS1_Review.exe`이고, 12초 headless smoke에서 R0-Easy·timeScale 1·seed 405001 bootstrap과 ONNX 정책 명령을 확인했으며 런타임 예외는 없었다.
- r008·r009의 패스 선택 보정 실험은 최종 후보보다 패스/주 gate 성능이 나아지지 않아 선택하지 않았다. 모든 Run·PT·ONNX·평가 로그는 보존했다. 다음 단계는 선택한 **r007의 번호가 붙은 PT 7443**에서 MS2를 초기화해야 하며 r007의 15k 최종 `checkpoint.pt`를 대신 사용하면 안 된다.
- 재개 후 생성형 Scene의 임의 fileID와 Unity가 첫 빌드에서 추가한 Sentis define으로 source가 바뀌는 문제를 발견했다. Builder를 생성/검증 단계로 분리하고 최종 source snapshot v19 385개 파일을 만든 뒤 다시 빌드했으며, 두 최종 빌드 뒤 source manifest 전 파일이 일치했다. 전체 EditMode `242/242`와 16환경 launcher `-ValidateOnly`도 통과했다. 완료 증거는 [MS1 완료 보고서](../manager/ms-v1/ms1-completion-20260920.md), 사람 확인 절차는 [MS1 사용자 확인 체크리스트](../manager/ms-v1/ms1-user-checklist.md), 기계 판독 manifest는 `Logs/MNG-MS/MS1-completion-20260920/ms1-completion.json`에 있다.

## 2026-09-20 MS1 r001 완료·공통 패스 개선·r002 시작 전 사용량 중단 — 최신 상태

- 첫 공식 16환경 fresh PPO `MNG_MS1-20260920-r001`을 aggregate `99,980` step까지 완료했다. 최근 reward는 20k `0.7506`에서 100k `0.8785`로 상승했고 PT/ONNX/checkpoint와 20k 간격 inspect 증거를 보존했다.
- 고정 평가에서 공격은 강했지만 Pass 유형에서 r001이 Pass 명령을 0회 선택했고 실제 완료도 0회였다. 사용자 지시에 따라 패스 보조 gate를 `4/20 -> 3/20`으로 낮추고, 전진 통로 차단 시 R0가 패스를 먼저 검토하며, 공통 패스 목표를 전진 3.5m·경기장 중앙 쪽 1.5m로 바꿨다. 실제 완료 패스 보상은 `+0.04 -> +0.05`, 60초 상한 `+0.10`은 유지한다.
- 최종 평가의 잘못된 하위집합 집계를 수정했다. 정책당 120 episode 중 첫 공격 40·수비 40을 주 gate에 쓰고 정확한 Pass 유형 20개를 패스 보조 gate에 쓴다.
- 공통 EditMode `63/63`, MS1 계약 `6/6`, Builder Validate, Windows Player build error 0을 통과했다. 공식 r002 입력은 source snapshot v4 344개 파일과 `MNG_MS1_PassRepair.yaml`이며, 기존 Player는 `Builds/MNG_MS/MS1-pre-passrepair-20260920`에 보존했다.
- r002는 r001 99,980 PT 가중치에서 새 optimizer로 시작하는 16환경·40k 한정 보정이다. 마지막 사용량이 5시간 사용 95%(잔여 5%), 주간 사용 15%(잔여 85%)로 중단선에 도달해 실제 r002 Run은 시작하지 않았다. reset credit은 사용하지 않았다.
- 정확한 해시, 평가 수치, 재개 명령은 [MS1 패스 개선 체크포인트](../manager/ms-v1/ms1-pass-repair-checkpoint-20260920.md)에 기록했다.

## 2026-09-20 MS1 구현·Player 빌드 완료, 사용량 중단 — 이전 상태

- MS1의 첫 구현 단위를 완료했다. `MNG_MS1Controller`가 fresh Red PPO 대 Navy R0-Easy 경기를 30초 episode로 운영하고, 공격 50%와 수비/전환 50%를 결정론적으로 교대한다. 수비 내부는 Neutral 50%, Direct 25%, Wide 25%다. 선수 이동·패스·슛·골키퍼 기술과 133 관측·6명령 계약은 기존 MNG 공통 계층을 그대로 사용한다.
- `MNG_MSBuilder`가 `MNG_MS1_Train.unity`를 생성·검증했으며 Unity 6000.3.16f1 컴파일과 Builder Validate가 통과했다. EditMode MS1 계약은 `3/3` 통과했다.
- 16개 독립 Windows Player 학습용 `Builds/MNG_MS/MS1/MNG_MS1.exe` 빌드가 error 0으로 성공했다. 최종 source snapshot 330개 파일과 exe/level/YAML/protocol SHA-256은 `Logs/MNG-MS/MS1-final-source`와 `Builds/MNG_MS/MS1/build-info.json`에 보존했다.
- PPO 설정과 실행기는 aggregate 최대 100k, `--num-envs 16`, seed 191001, port 5800..5815로 고정했다. 실제 16환경 Trainer Run, PPO update, checkpoint, PT/ONNX, 공격·수비 진단 및 최종 평가는 아직 시작하지 않았다.
- 마지막 사용량은 5시간 92% 사용(잔여 8%), 주간 45% 사용(잔여 55%)이다. 활성 중단선인 **5시간 잔여 9% 이하**에 도달하여 새 학습 Run을 시작하지 않고 저장·중단했다. reset credit은 사용하지 않았다.
- 정확한 완료 증거, 해시, 미실행 범위와 재개 순서는 [MS1 사용량 중단 체크포인트](../manager/ms-v1/ms1-checkpoint-20260920.md)에 기록했다.

## 2026-09-20 MS0 사용자 확인 · MS1 계획 고정 — 최신 상태

- 사용자가 MS0를 확인했고 다음 단계 MS1 개발 계획 수립을 지시했다. 세부 단일 기준은 [MS1 개발·학습 계획](../manager/ms-v1/ms1-development-plan-20260920.md)이다.
- MS1은 과거 M1 공격 전용 단계들을 재개하지 않는다. 공격 50%와 수비/전환 50%를 30초 episode로 섞은 한 정책을 fresh PPO로 학습한다. 기존 133 관측·6명령과 코드 기반 선수 기술을 유지한다.
- 기본 상대는 R0-Easy `0.35×/1.5초`이며 Rescue `0.20×/2.0초`는 Easy가 학습 경험을 거의 주지 못할 때 첫 최대 20k에만 사용한다. 최종 평가는 Easy로 고정한다.
- 사용자 지시에 따라 MS1 학습은 **독립 Windows Player 16개**를 `--num-envs 16`으로 실행한다. aggregate 상한은 100k이며 16을 곱하지 않는다. 16개가 불안정하면 8/2개로 자동 축소하지 않고 정상 저장·중단한다.
- 학습 전 random-valid 진단 baseline을 만들고 fresh PPO의 첫 정상 export는 초기 참고값으로 구분한다. 20k마다 공격·수비 checkpoint 진단을 하며 공격 40 episode 중 24, 수비 40 중 20, 각 random 대비 +10%p, 정확한 Pass 유형 완료 패스 3/20을 모두 만족한 후보만 MS1을 통과한다. 최종 평가는 주 gate 80개와 Pass 유형 20개를 한 결정론적 120 episode 실행에서 수집한다.
- 이번 상태 갱신은 MS1 구현·Player build·학습 Run을 실행한 결과가 아니다. 구현 순서, 파일 책임, 16환경 실행, gate와 중단 처리를 고정한 계획 단계다. 계획 확인 시점 사용량은 5시간 잔여 53%, 주간 잔여 62%로 중단선에 도달하지 않았고 reset credit은 사용하지 않았다.

## 2026-09-19 MS0 준비 완료 — 이전 완료 상태

- MS0를 완료했다. R0 상대와 self-play 전용 Scene, Rescue/Easy/Medium/Full 상대 Profile, MS 전용 Builder·Windows Player·병렬 trainer launcher·source/build manifest를 구현했다. 선수 이동·패스·슛·골키퍼·물리와 133 관측/6명령 계약은 기존 MNG 공통 계층을 재사용한다.
- Unity 검증은 컴파일·Builder Validate, EditMode `8/8`, PlayMode `2/2`, 세 YAML의 설치 trainer 파싱을 통과했다. R0Smoke와 SelfPlaySmoke Windows Player는 build error 0이며 source/exe/level/config/protocol hash가 기록되었다.
- 병렬 smoke는 1→2→8 workers에서 서로 다른 port·process·spawn seed·worker JSONL과 episode 완료를 확인했다. 합격 Run들의 최종 checkpoint 합계는 `4,177 / 5,000`이다.
- `MNG_MS0R0-20260919-r001`은 첫 저장 뒤 step 482에서 같은 Run으로 resume하여 최종 checkpoint 1,284, PPO update 2회, PT/ONNX 각 7개를 남겼다. `checkpoint.pt`에 optimizer 상태가 존재한다.
- `MNG_MS0SelfPlay-20260919-r001`은 TeamId 0·1, 정책 감독 2·Rule 0으로 최종 checkpoint 2,050, PPO update 7회, PT/ONNX 각 7개를 남겼다. 별도 DEBUG 진단 Run에서 ghost snapshot 교체와 학습 팀 `1→0→1` 전환을 확인했다.
- DEBUG 진단 Run 1,280 step은 합격 모델·seed·4,177 합계에서 제외했지만 실제 전체 실행량은 summary 기준 5,457로 MS0 과정 상한을 457 step 초과했다. 기능 검증을 위한 단일 진단 편차이며 숨기거나 다음 단계 예산으로 이월하지 않는다.
- MS0의 smoke ONNX는 연결 증거이며 강한 정책이 아니다. self-play 54 episode는 Red 승 1, Navy 승 0, 무 53이었다. MS1 공격·수비 학습, MS2 Full R0 성능, MS3 self-play 향상은 아직 시작하지 않았다.
- 완료 증거는 [MS0 완료 보고서](../manager/ms-v1/ms0-completion-20260919.md), 사람이 볼 항목은 [MS0 사용자 확인 체크리스트](../manager/ms-v1/ms0-user-checklist.md)에 있다. 완료 manifest는 `Logs/MNG-MS/MS0-completion/ms0-completion.json`, 최종 source 기준은 `Logs/MNG-MS/MS0-final-source-v2/source-sha256.json`이다.
- 당시 사용량 중단 기준은 2026-09-23에 폐지했다. 완료 직전 확인값은 5시간 잔여 57%, 주간 잔여 63%로 중단선에 도달하지 않았다. reset credit은 사용하지 않았고 자동 재개·commit·push를 만들지 않았다.

## 2026-09-19 MS0 구현 중단 체크포인트 — 해소된 이전 상태

- 사용자가 MS0 완료 작업을 재개했으며 당시 사용량 중단 기준은 2026-09-23에 폐지했다. 재개 직후 실제 잔여량은 각각 100%, 69%이며 reset credit은 사용하지 않는다.
- 사용자의 중단 지시에 따라 새 빌드·학습을 시작하지 않고 상태를 저장했다. 마지막 확인에서 5시간 사용량 75%(잔여 25%), 주간 사용량 99%(잔여 1%)로 **주간 잔여량 2% 이하 중단선에 도달**했다. reset credit은 사용하지 않았고 자동 재개도 만들지 않았다.
- MS0의 런타임 연결은 구현했다. Red PPO 대 Full R0, Rescue/Easy/Medium/Full 상대 프로필, R0 이동 속도·판단 주기 조절, worker 포트별 spawn seed, worker별 JSONL, self-play 양 팀 PPO, self-play 최종 보상 승 `+0.5`/무 `0`/패 `-0.5`를 포함한다. 공통 이동·기술·판정은 기존 MNG 계층을 재사용한다.
- `MNG_MSBuilder`가 네 상대 프로필과 `MNG_MS_Train`, `MNG_MS_Evaluation`, `MNG_MS_SelfPlay` Scene을 생성·검증했다. 생성된 Scene/Profile은 현재 작업 트리에 있으며 Unity 6000.3.16f1 최종 재컴파일은 성공했다. 정확한 결과는 `Logs/MNG-MS/MS0/final-recompile-status.json`에 저장했다.
- EditMode MS 계약은 `8/8` 통과했다. 첫 PlayMode는 trainer 없는 Academy 자동 step 때문에 `0/2`, 두 번째는 R0 Scene `1/1` 통과 후 다음 테스트 시작 전 남은 Agent가 한 틱 실행되어 합계 `1/2`였다. 두 테스트 끝에서 Agent를 비활성화하도록 수정했고 **그 수정의 컴파일만 통과했으며 최종 PlayMode 재실행은 중단선 때문에 수행하지 않았다.**
- `Tools/MNG_MS_Snapshot.ps1`, `MNG_MS_Build.ps1`, `MNG_MS_Train.ps1`과 3개 smoke YAML·MS0 프로토콜을 추가했다. 초기 source snapshot은 289개 파일과 Git patch/status를 `Logs/MNG-MS/MS0`에 보존했다. 이후 컴파일 수정이 있었으므로 이 초기 manifest를 최종 Player 소스 manifest로 쓰면 안 된다.
- 이 중단 시점에는 Windows Player 빌드, 1→2→8 worker smoke, PPO update, 중단·resume, PT/ONNX, self-play snapshot/team switch가 미실행이었고 누적 MS0 학습 step은 `0`이었다. 이후 재개 작업에서 모두 실행되어 위 최신 완료 상태로 해소되었다. 당시의 정확한 재개 순서는 [MS0 중단 체크포인트](../manager/ms-v1/ms0-checkpoint-20260919.md)에 보존한다.

## 2026-09-19 R0 사용자 완료 판정 · MS 계획 수립 — 이전 계획 상태

- 사용자가 MS0 구현 시작과 함께 중단 기준에 `주간 잔여량 2% 이하`를 추가했다. 기존 5시간 잔여량 10% 이하 기준과 병행하며 먼저 도달한 기준을 적용한다.
- 사용자가 현재 R0를 완료로 판정했다. 아래 과거 기록의 `사용자 육안 합격 전 미완성`·`승인 대기`는 이 결정으로 해소되었다. 신규 테스트를 실행해 승인한 것이 아니라 사용자의 명시적 완료 판정을 기록한 것이다.
- 기존 M 단계의 순차 학습을 중단하고 **MS(Manager-Simple)**를 신설했다. [계획](../manager/ms-v1/ms-plan.md), [개발 인계](../manager/ms-v1/ms-agent-handoff.md), [평가 계약](../manager/ms-v1/ms-validation.md)이 다음 감독 RL 개발의 기준이다.
- MS0 준비(공통 기술/감독 영향/빌드/병렬/연결) → MS1 공격·수비 혼합 판단 → MS2 원래 강도 R0와 대전 → MS3 PPO self-play의 네 단계다. 각 단계는 조기 통과·학습 상한·정체 중단 조건을 갖는다.
- 이 시점의 변경은 Markdown 문서만이었다. 이후 위 최신 체크포인트 범위까지 MS0 구현이 진행되었다.
- 정적 조사에서 기존 `Tools/MNG_Train.ps1`은 환경 수 1 제한, Build/Train 도구는 기존 M 단계만 지원함을 확인했다. 최소 2개 독립 환경, 기본 후보 8개/검증 후 16개와 MS 전용 build/launcher가 MS0 개발 과제다.
- 다음 개발 세션은 MS 인계의 T0부터 시작한다. R0 재승인이나 과거 M1 gate를 다시 요구하지 않되, 실제 dirty source snapshot과 MS 연결·물리·종료·병렬 필수 검증은 수행한다. 본 세션에서는 개발·Unity 실행·환경 설치·학습을 시작하지 않았다.
- 문서 검증: 신규 MS 문서 3개의 범위/미구현 표시와 수정 문서 13개의 로컬 Markdown 링크·문자 깨짐·`git diff --check`를 확인했다. 코드 실행 결과나 신규 R0 회귀 통과를 의미하지 않는다.

## 2026-09-19 MNG R0 Planner v9 2인 공격·킥 세기 증가 회귀 통과 — 이전 상태

- 당시 사용량 비율 중단 기준은 2026-09-23에 모두 폐지했다. 사용자 지시 없이 reset credit이나 재개 자동화를 사용하지 않는다.
- 상대 골대30m 구역에서 자기 팀 필드 선수가 공을 소유하면, `MNG_TeamPlanner` v9이 운반자 외 가장 가까운 제어 가능 필드 선수1명을 공격 지원자로 추가한다. 지원자는 공보다 공격 방향2.75m·안쪽 측면3.75m 위치에서 패스·리바운드를 준비하고, `PassBuild`의 지정 수신자는 기존 수신 임무를 우선한다. 따라서 규칙형 R0와 PPO 학습형 모두 최소2명이 공격에 참가하되 같은 점에 겹쳐 운반을 방해하지 않는다.
- MNG는 raw Force 대신 실제 킥 플레이트 접촉의 목표 출구속도를 사용한다. 패스·슛 힘 `+1000`을 질량3kg·fixedDeltaTime0.02초 기준 `+6.6667m/s`로 환산해 패스 `14~28→20.6667~34.6667m/s`, 슛 `28→34.6667m/s`, 공 최고속도와 관측 정규화 `30→36.6667m/s`로 변경했다. 기존 Scene/Prefab의 오래된 직렬화 상한이 새 계약을 자르지 않도록 `MNG_BallControl.Awake`에서 Rigidbody 상한을 공통 런타임 값으로 설정한다.
- 검증은 최종 컴파일 성공, MNG EditMode `75/75`, 실제 킥 물리 `2/2`이며 슛10/20m와 패스5/10/20m가 각각 `20/20`, MNG PlayMode 전체 `25/25`, Builder `MNG M0 VALIDATION PASS`다. 독립 R0 3×300초는 점수 `8:2`,6명령 전부, 소유 전환693, 밀집 `255/90006=0.2833%`, 후방 운반 `582/9777=5.95%`, 패스 킥9/완료2, 슛52/유효48로 통과했다. 최종 관측 정규화까지 포함한 전체 PlayMode 재표본 R0도 점수 `6:8`, 소유 전환758, 밀집 `84/90006=0.0933%`, 후방 운반 `611/10439=5.85%`, 패스 킥11/완료3, 슛69/유효58이다.
- 증거는 `Logs/MNG-R0-AttackSupport-KickPower-Final2Compile.log`, `MNG-R0-AttackSupport-KickPower-Final3EditMode.xml`, `MNG-R0-AttackSupport-KickPower-Physical.xml`, `MNG-R0-AttackSupport-KickPower-R0Gate.xml`, `MNG-R0-AttackSupport-KickPower-FinalPlayMode.xml`, `MNG-R0-AttackSupport-KickPower-BuilderValidation.log`다. 기존 Run·PT·ONNX는 보존했고 새 학습은 시작하지 않았다. 물리·관측 정규화가 바뀌었으므로 M1을 재개할 때 r001~r003을 승격/단순 resume하지 않고 fresh policy로 다시 검증한다. R0는 자동 회귀 통과, 사용자 육안 합격 전 미완성이다.

## 2026-09-19 MNG R0 Planner v8 2인 압박·골문 복귀·정체 안전 슈팅 회귀 통과 — 이전 상태

- `MNG_TeamPlanner`를 v8로 올렸다. 상대가 공을 소유하면 감독 명령이 `ProtectBack`이어도 비골키퍼2명을 배정한다. 1차는 예측 공 위치를 직접 압박하고, 2차는 공의 자기 골대 쪽2.75m·측면2.25m 지점에서 패스·돌파를 차단한다. 두 선수는 서로 다른 목표를 사용하며 나머지 선수의 마크·커버도 유지한다.
- 공이 자기 골대 전방30m 안에 있고 자기 팀 소유가 아니면 MidLeft/MidRight는 공격 진영에 남지 않는다. 압박자로 뽑힌 미드필더는 공으로 복귀하고, 나머지는 골대 전방12m 차단선으로 복귀한다. 위협이 해제되면 기존 명령·포메이션 목표를 다시 사용한다.
- 전역 공 정체가1.20초에 활성화되면 첫2.5초 동안 자기 운반자 또는 공8m 안의 킥 가능한 최인접 필드 선수가 실제 킥 플레이트 슈팅·클리어를 먼저 시도한다. 팀 공격축 내적0.25 미만 또는 공격축 전진1m 이하 목표는 자책 위험으로 거부한다. 안전한 슈터가 없거나 첫 시도로 공이 움직이지 않으면 기존 다중 선수 재탐지·서로 다른 접근/지원 위치로 전환한다.
- 세 기능은 `MatchController→TeamPlanner→PlayerSkillExecutor` 공통 계층에 있어 R0 규칙형과 PPO 학습형에 동일하게 적용된다. 감독의6명령·133-float 관측은 바꾸지 않았고 M1 학습은 시작하지 않았다. 시작/킥오프 스폰은 `±3.00m`이며 경기 중 인위적 재배치는 없다.
- 검증은 컴파일 성공, MNG EditMode `73/73`, 프로젝트 전체 EditMode `220/220`, 실제 정체 Rigidbody `1/1`, 전체 MNG PlayMode `25/25`, 독립 R0 3×300초, Builder `MNG M0 VALIDATION PASS`다. 독립 R0는 점수 `5:3`,6명령 전부, 소유 전환841, 밀집 `182/90006=0.2022%`, 후방 운반 `607/10097=6.01%`, 패스 킥13/완료2, 슛56/유효48이다. 전체 PlayMode 재표본은 점수 `6:4`, 소유 전환720, 밀집 `305/90006=0.3389%`, 후방 운반 `592/9194=6.44%`, 패스 킥14/완료1, 슛56/유효51이다.
- 증거는 `Logs/MNG-R0-CoordinatedDefense-Compile.log`, `MNG-R0-CoordinatedDefense-EditMode.xml`, `MNG-R0-CoordinatedDefense-AllEditMode.xml`, `MNG-R0-CoordinatedDefense-StallPhysical.xml`, `MNG-R0-CoordinatedDefense-R0Gate.xml`, `MNG-R0-CoordinatedDefense-AllPlayMode.xml`, `MNG-R0-CoordinatedDefense-BuilderValidation.log`다. R0는 자동 회귀를 통과했지만 사용자 육안 합격 전까지 미완성이며 M1은 계속 중단한다.

## 2026-09-19 MNG R0 사용자 육안 승인 대기 — 이전 자동 근거

- 중단 당시 남아 있던 같은 팀 밀집률 실패를 기준 완화 없이 해결했다. 목표점 간격만 키우는 방식 대신 `MNG_PlayerSkillExecutor`에 결정론적 긴급 분리를 추가했다. 4.25m 안에서 충돌 위험이 생기면 운반자, 킥 실행자, 패스 수신자, Human, 경계/경합 탈출 담당자의 경로를 보존하고 우선순위가 낮은 AI 한 명만 최대 속도의 85%로 짧게 양보한다. 같은 우선순위에서는 슬롯 순서로 한 명만 양보해 서로 반대로 흔들리는 현상을 막는다.
- 독립 R0 3×300초 승인 게이트는 `Logs/MNG-R0-EmergencySpacing-R0Gate.xml`에서 통과했다. 합계 점수 `20:14`, 명령 `3,080회`와 6종 전부, 소유 전환559회, 패스 킥10·완료1, 유효 슛34, 전진 보상119이며, 3m 미만 밀집은 `9/90006=0.01%`, 소유 중 후방 공 속도는 `1306/13303=9.82%`다.
- 최종 전체 회귀는 EditMode `212/212`, MNG PlayMode `24/24`다. 전체 PlayMode 안의 별도 R0 재표본도 점수 `9:11`, 6명령 전부, 밀집 `60/90006=0.067%`, 후방 공 속도 `842/9840=8.56%`, 패스 킥3, 유효 슛23으로 통과했다. 물리 계약은 드리블 직선/좌/우 각 `20/20`, 오픈 골 슈팅10m/20m 각 `20/20`, 무수비 패스 수신5m/10m/20m 각 `20/20`을 유지한다.
- Builder 자산 연결은 `Logs/MNG-R0-EmergencySpacing-BuilderValidation.log`의 `MNG M0 VALIDATION PASS`로 통과했다. 컴파일, 경계 탈출, 골키퍼 배급, 스폰 `±1.50m`, 킥오프 외 재배치 금지 계약도 회귀에 포함된다.
- 최종 증거는 `Logs/MNG-R0-EmergencySpacing-Compile.log`, `MNG-R0-EmergencySpacing-Contract.xml`, `MNG-R0-EmergencySpacing-Physical.xml`, `MNG-R0-EmergencySpacing-R0Gate.xml`, `MNG-R0-EmergencySpacing-AllEditMode.xml`, `MNG-R0-EmergencySpacing-AllPlayMode.xml`, `MNG-R0-EmergencySpacing-BuilderValidation.log`다.
- R0는 개발자 자동 기준을 통과한 육안 승인 후보다. 사용자가 R0를 먼저 점검하기로 했으므로 최종 체감 승인 전까지 R0 코드·Scene·Prefab을 기준 후보로 고정하고 M1 코드 변경·학습·평가를 시작하지 않는다. 검토는 `docs/archive/manager/m-stage/MNG_R0_Visual_Approval_Guide.md`를 따른다. 호출 제한 스킬 회피와 직접 C#/Unity batchmode 검증 지침은 루트 `AGENTS.md`에 계속 적용한다.

## 2026-09-19 MNG 공통 경기 보조 개선·사용량 중단 — 아래 R0 승인 기록보다 우선

- 사용자 요청에 따라 시작/킥오프 때만 적용되는 포메이션 축별 랜덤 오프셋을 `±0.75m`에서 `±1.50m`로 정확히 2배 확대했다. 경기 진행 중에는 재배치하지 않는 기존 계약을 유지한다.
- `MNG_TeamPlanner`는 v6이다. 골키퍼가 공을 소유하면 감독이 규칙형인지 PPO인지와 무관하게 전진 거리, 수신자 압박, 패스 선 차단을 검사해 안전하면 동료에게 실제 킥 플레이트 패스를 하고, 어렵다면 상대 골대 방향으로 강하게 걷어낸다. 비소유 골키퍼의 Claim/Block과 공 주시 이동은 유지한다.
- `MNG_BallControl`과 `MNG_PlayerSkillExecutor` 공통 계층에 벽/코너 정체 탈출을 추가했다. 경계 3m 안에서 공 속도 0.45m/s 이하가 0.75초 지속되면 가장 가까운 AI 선수가 먼저 공 뒤로 물러나 공간을 만들고, 경기장 안쪽 각도로 접근해 킥 플레이트로 걷어낸다. 공 순간이동, 직접 탈출 impulse, 강제 소유 이전은 사용하지 않는다.
- 같은 팀 밀집을 줄이기 위해 필드 선수 개인 공간을 7m, 최대 목표 오프셋을 10m, 보정 강도를 2.5로 높였다. 첫 전체 R0 표본의 3m 미만 밀집률 `2389/90006=2.65%`를 재시험에서 `1273/90006=1.41%`로 낮췄지만 승인 게이트 `<1%`에는 아직 미달했다. 게이트를 완화하지 않는다.
- 자동 근거: Unity 컴파일 성공, 경계/전면밀기 EditMode `12/12`, 런타임 계약 `51/51`, 전체 EditMode `210/210`, 실제 벽 탈출 PlayMode `1/1` 통과. 전체 MNG PlayMode는 간격 최종 강화 전 코드에서 `22/23`이며 유일 실패는 R0 밀집률이었다. 실패 경기 자체는 합계 `18:11`, 6개 명령 전부, 소유 전환 560회, 패스 킥 11/완료1, 유효 슛31로 정상 진행됐다. 간격 7m/10m/2.5 강화 뒤 단일 R0 재시험은 재컴파일에 성공했고 합계 `11:13`, 6개 명령, 소유 전환512회, 패스 킥13/완료5, 유효 슛26이지만 밀집률 게이트에 실패했다. 최종 상수 변경 뒤 전체 EditMode/PlayMode는 중단선 때문에 다시 실행하지 않았다.
- 증거는 `Logs/MNG-R0-Boundary-Compile.log`, `MNG-R0-Boundary-FrontPush.xml`, `MNG-R0-Boundary-RuntimeContract.xml`, `MNG-R0-Boundary-PhysicalEscape.xml`, `MNG-R0-Boundary-AllEditMode.xml`, `MNG-R0-Boundary-AllPlayMode.xml`, `MNG-R0-Boundary-R0Gate-Retry.xml`에 보존했다.
- 범용 개발 중 호출 횟수 하드 스톱이 있는 선택적 진단 스킬을 사용하지 않고, 직접 C# 감사/`apply_patch`/설치된 Unity batchmode로 검증하는 재발 방지 지침을 루트 `AGENTS.md`에 저장했다. 스킬·플러그인·MCP 실패는 직접 수정 경로가 남아 있으면 전체 중단 사유로 취급하지 않는다.
- 마지막 사용량은 5시간 91% 사용(잔여9%), 주간 14% 사용(잔여86%)이다. 중단선에 따라 추가 코드 변경, 재시험, Builder, 학습을 시작하지 않는다. Unity Editor/Trainer/Player는 실행 중이지 않고 Unity CLI helper만 남아 있다.
- 재개 시 사용량을 먼저 확인하고, 3m 이내에서 목표점만 바꾸는 방식보다 즉시 분리 속도/역할 우선권을 주는 공통 간격 보조를 검토한다. 관련 단위 테스트와 R0 3×300초를 먼저 통과시킨 뒤 전체 PlayMode `23/23`, Builder 검증 순서로 마무리한다. R0/M1 학습은 이 회귀가 끝나기 전 시작하지 않는다.

## 2026-09-19 MNG R0 규칙형 감독 개발자 승인 완료 — 아래 과거 R0 기록보다 우선

- R0 코드·자산·자동 승인 게이트를 완료했고 개발자 기준으로 승인했다. 사람의 최종 육안 평가는 경기 체감 확인으로 별도 유지하지만, R0 기능 완료나 후속 M1 개발을 막는 조건은 아니다.
- 공 운반 목표는 양 팀 모두 상대 골대 방향으로 최소 6m 전진한다. 회피 후보의 후방 경로를 제거하고 공격 전진·팀 간격 가중치를 높였다.
- 전면 밀기 경로는 `Carry`, 단독 `Press`, 공을 받은 `ReceivePass`에만 적용한다. `Cover`, `Mark`, `SupportRun`, 골키퍼는 공 주위 궤도에 합류하지 않는다.
- 패서와 수신자는 동일한 전방 합류점을 사용한다. 킥 준비 중에는 킥 플레이트 정면 전진을 유지하면서 목표를 바라보고, 슈팅은 골키퍼 반대쪽 골문 안쪽을 겨냥한다.
- 정지·후진 소유 해제는 0.12초이며, 상대 킥 플레이트 탈취와 공 순간이동 금지 계약은 유지한다.
- 정면 경합 탈출은 현재 소유 팀이 상대 골대 방향 4m와 측면 2.25m를 결합한 목표로 실제 킥 플레이트 앞밀기를 수행한다. 강제 소유 이전이나 공 순간이동은 없고, 상대 킥 플레이트 접촉 시에는 쉽게 탈취된다.
- 전면 밀기 수행자에게도 동료 간격 보정을 적용했다. 필드 선수 개인 공간 반경은 6m, 최대 회피 오프셋은 8m이며, 보정 뒤에도 목표를 다시 공격 방향으로 제한한다.
- 슈팅 가능 창은 골문까지 24m, 골문 측면 여유 12m다. 40m 실험은 실제 슈팅 성공률 0/20이어서 채택하지 않았다. R0 규칙은 위험 지역 패스, 압박 탈출 패스, 공간이 확보된 전방 패스, 전진 운반, 슈팅을 구분한다.

### 최종 자동 근거

| 검증 | 결과 | 증거 |
| --- | --- | --- |
| 전체 EditMode | `207/207` 통과 | `Logs/MNG-R0-Final-EditMode.xml` |
| MNG 전체 PlayMode | `22/22` 통과 | `Logs/MNG-R0-Final-PlayMode.xml` |
| 전진 드리블·정지/후진 해제 | 직선·좌·우 `20/20`, 테스트 통과 | `Logs/MNG-Core-Dribble-Retry-PlayMode.xml` |
| 상대 킥 플레이트 탈취 | 테스트 통과 | `Logs/MNG-Core-Steal-PlayMode.xml` |
| 오픈 골 슈팅 | 10m `20/20`, 20m `20/20` | `Logs/MNG-R0-OpenGoal-PlayMode.xml` |
| 무수비 패스 수신 | 5m·10m·20m 각각 `20/20` | `Logs/MNG-R0-PassReceive-PlayMode.xml` |
| R0 3×300초 독립 승인 게이트 | 합계 `2:1`, 6개 명령, 패스 킥 5·완료 패스 1, 슈팅/유효 슈팅 4, 전진 35 | `Logs/MNG-R0-Spacing-3Match-PlayMode.xml` |
| R0 독립 게이트 간격·방향 | 뭉침 `210/90006`(0.23%), 소유 중 후방 속도 `205/2489`(8.24%) | `Logs/MNG-R0-Spacing-3Match-PlayMode.xml` |
| 전체 PlayMode 내 R0 재표본 | 합계 `3:1`, 뭉침 `558/90006`(0.62%), 후방 속도 `155/2582`(6.00%), 패스 킥 1, 유효 슈팅 4, 전진 37 | `Logs/MNG-R0-Final-PlayMode.xml` |
| MNG Builder 자산 검증 | `MNG M0 VALIDATION PASS` | `Logs/MNG-R0-Final-BuilderValidation-r3.log` |

독립 3×300초 승인 게이트에서는 실제 완료 패스가 1회 발생했다. 전체 PlayMode 재표본에서는 패스 킥 1회가 수신까지 연결되지 않았지만, 별도 경쟁 표본의 완료 패스와 독립 60회 수신 검증이 모두 있으므로 기능 고장으로 보지 않는다. R0는 개발자 승인 완료이며, 사람의 경기 체감 확인은 `MNG_R0_Visual_Approval_Guide.md`를 따른다.


## 2026-09-18 전면 밀기 접근 계약과 골키퍼 활동 반경 조정

- R0은 계속 개발 중이며 미승인이다.
- 드리블 보정은 공이 선수 전면에 있고 킥 플레이트가 전진 구동 중일 때만 적용한다. 측면·후방 자석 끌기는 허용하지 않는다.
- 목표가 옆이나 뒤로 크게 바뀌면 기존 소유 보정을 해제하고 공 둘레의 안전 반경을 따라 목표 반대편 준비점으로 이동한 뒤 전면으로 민다.
- 느슨한 공 회수도 공으로 직행하지 않고 목표 반대편 준비점과 전면 밀기 단계를 사용한다.
- 골키퍼 Claim/Block 활동 반경의 전방 깊이, 측면 허용폭, 최대 전진 거리를 명명 상수로 분리하고 기존 값의 정확히 2배로 적용했다. 골문 기준 Home 위치는 유지한다.
- 기존 M1 모델은 변경 전 실행 계약의 산출물이므로 이 검증과 R0 재평가 전에는 승인 모델로 취급하지 않는다.

- 대상: 모든 담당자
- 마지막 검토: 2026-09-07
- 상태: L1 r017, L2 승인 계열 r018 200,184, L3 r001-r005 200k와 모든 L2-Find Run을 보존한다. L2-Find r017은500,248에서 종료했으나99% gate 미달로 미승인이다. 사용자 지시에 따라 추가 개발·학습과 L2-Score 전환을 중단한 상태다.

이 문서는 현재 사실과 다음 작업만 유지한다. 완료 과정의 상세 기록은 [Archive](../README.md)로 옮긴다.

## 2026-09-10 MNG Manager 현재 상태

- **2026-09-18 R0 합격 철회·드리블 개선 — 최우선:** 사용자가 R0는 아직 합격이 아니라고 명시했다. 기존 전진 전용 보정과0.08초 해제가 좌우·후진·회전 드리블의 공 손실 원인이어서, 규칙형/PPO 공통 `MNG_BallControl`을 실제 평면 이동·회전 기준으로 바꿨다. 킥 플레이트 앞 흔들리는 목표점, Rigidbody 구름 torque, 상대 진영 전진 중4.75m 전방 수비 회피, 상대 plate 접촉 시 즉시 소유 해제와0.22초 재획득 잠금을 추가했다. 위치 순간이동·부모 결합은 없다. 새 자동·육안 회귀 전 R0는 개발 중이며, 물리 계약이 바뀌었으므로 기존 M1 r003은 resume하지 않는다.
- **2026-09-17 M1 r003 중단 — 최우선:** R0 v0는 자동 기준 임시 합격했고 사람 육안 최종 승인은 후속이다. 새 Player 오류0/manifest SHA 일치 뒤 fresh-policy `MNG_M1Attack-20260917-r003`을 seed13003으로 시작했다.20k frozen100은95득점/자책0이지만 PassBuild2·pass plate0·완료0,40k는97득점/자책0이지만 PassBuild0·pass plate0·완료0이라 v6 gate에 모두 실패했다. 사용량 중단선에서48,454 PT/ONNX/configuration을 정상 저장했고 최종 ONNX SHA는 `0245cdcbce0a0f5ee6f4966a29140b7089ecaa038293a265488665d62c78e39c`다. 잔여량은5시간9%/주간31%, 실행 중 Trainer/Player/평가/Unity0, reset credit 미사용이다. r003은 미승격 실패 근거로 보존하고 단순 resume하지 않는다. 다음은 Pass 상황에서도 직접 득점이+1을 독점하는 명령-성과 인과성 결함을 고친 뒤 fresh-policy r004를 시작하는 것이다. 상세 수치와 재개 순서는 `docs/archive/manager/m-stage/MNG_05_Development_Handoff.md` 최상단을 따른다.
- **2026-09-17 R0 임시 합격·M1 r003 진입 — 과거 이력:** 같은 명령 반복 수락 때 `CommandAgeSeconds`가0으로 초기화되던 공통 결함을 수정하고 회귀 테스트를 추가했다. R0 Builder·EditMode195/195·전체 MNG PlayMode22/22 통과. 수정 뒤 실제300초는15,001tick/명령1,154회/6종 전부/Carry5·Pass14·Shot1/소유전환37회/공 최대61.06m/0:0으로 완주했다. 기능이 모두 작동하는 비학습 약한 기준선이라는 목적을 충족해 개발자 임시 합격으로 고정하며 사람 육안 최종 승인은 후속이다. 당시 사용량은5시간 잔여44%/주간 잔여36%였다. r001/r002를 보존하고 `MNG_M1Attack-20260917-r003`을 `-InitializeFrom` 없이 fresh policy로 시작했다. 현재 중단선은5시간 잔여10% 이하 하나뿐이며 reset credit은 사용하지 않는다.
- **2026-09-16 R0 사용량 중단 — 최우선:** 강화학습을 전혀 쓰지 않는 `MNG_RuleBasedManager` v0와 Red 대 Navy 전용 `MNG_R0_RuleVsRule.unity`를 구현했다. Fallback/Human/PPO endpoint는 비활성이고 두 규칙 감독은 PPO와 동일한6명령·Planner·기술층을 사용한다. Unity 재컴파일·R0 Builder, EditMode47/47, 전체 MNG PlayMode22/22 통과. 300초 자동 경기는15,001tick/0:0/명령1,156회/4종/소유전환14회/공 최대47.22m로 완주했다. 기능 안정성만 자동 통과했으며 사용자 육안 승인이 남아 있다. README와 구현 계획은 갱신했지만 커리큘럼·검증 문서와 짧은 관전 안내는 미완료다. 최신 사용량은5시간 잔여1%/주간 잔여44%여서 지정 중단선을 적용했고, reset credit·새 학습·Player build·R1·M1 r003은 시작하지 않았다. 정확한 재개 순서는 `docs/archive/manager/m-stage/MNG_05_Development_Handoff.md` 최상단을 따른다.
- **2026-09-16 최신:** 사용량 중단선을 주간 잔여3% 이하 또는5시간 잔여9% 이하로 변경했다. M1 r002는19,996 step에서82득점/패스명령1/완료0,59,994 step에서83득점/패스명령0/완료0으로 기존 r001의87득점을 갱신하지 못하고 패스 다양성도 회복하지 못해74,000 step에서 조기 중단했다. 구조적 원인은 개선 전 패스0회 정책을 `-InitializeFrom`으로 이어받은 것과 득점만으로 통과하던 v5 gate다. v6는82득점·자책0·실제 패스 플레이트12회·완료 패스8회를 모두 요구하고, 완료 패스 보상0.04/PPO beta0.01/fresh policy 강제를 적용했다. 컴파일·MNG Builder와 최종 EditMode192/192(`Logs/MNG-v6-EditMode-r4.xml`)를 통과했고 설정 내용 SHA도 기준과 일치한다. 이번 작은 단계는 여기서 저장 종료하며 r003 학습은 아직 시작하지 않는다.
- **2026-09-11 최신:** 사용자 지시로 MNG 공 질량을4.5에서 Core 원래값3.0으로 복구했다. 공 전용 동/정지 마찰0.6→0.05, 반발0.05→0.15(Maximum combine), 드리블 가속30→24, 속도동조8→5.5로 흡착을 줄였다. Planner v3는 선수별 열린 지원경로·상대 마크·서로 다른 예측압박과 Keeper loose-ball Claim/상대소유 Block을 추가하며 관측133/행동[6]은 불변이다. Builder 통과, EditMode43/43, 전체 PlayMode20/20. 실제 plate 패스5/10/20m·슛10/20m·드리블3종·탈취는 전부20/20이고 Fallback300초는5:3/공 최대73.28m로 완주했다. M1/M1B/M2 YAML은20k→100k로 올렸고 중요 모델은 최소100k 새 Run만 성능 판정한다. 기존20k Run/모델/로그는 이력으로 보존한다.
- `MNG_M1Attack-20260911-r001`은 새 실행 의미로100,000 step을 자연 완료했다. 최종5요약 평균보상0.630, ONNX/PT 검사 통과, 보상누출0. frozen100은65득점/자책0/timeout35로 random28보다+37%p지만 사전 요구76 미달이다. 100k checkpoint와 `Logs/MNG_M1Attack-20260911-r001-eval-r001`을 보존하고 동일 optimizer로 총300k까지 연장하며 200k/300k를 각각 재평가한다. M1은 아직 미승격이다.
- 200k 불변 후보 `MNG_Manager-199964`의 별도 frozen100은64득점/자책0/timeout36으로 요구76 미달이다(`Logs/MNG_M1Attack-20260911-r001-eval-r003`). 기준을 완화하지 않고 동일 optimizer를201,444에서300k까지 재개했다. 이 항목의 실행 상태는 과거 이력이며 현재 사용량 중단선은 위 2026-09-16 항목을 따른다.
- M1 r001은300,015에서 자연 종료했고 최종 ONNX/PT SHA는 `219DD807...B6391ED` / `8E804E76...D1CE8F`다. 고정100 `eval-r004`는68득점/자책0/timeout32로76골 gate 미달이며 미승격이다. protocol v4 진단 `eval-r005`도68/0/32를 재현했고 Carry24/34, Pass23/34, Shot21/32다. 총2,652 decisions에서 PassBuild는0회였고 마지막2,640-decision telemetry에서 mask 가용도도131회뿐이다. 계획된 ValidShot/CompletedPass/AdvancedFiveMeters 보상 사건도 Runtime 지급 경로에 아직 연결되지 않았다. 다음은 실제 plate strike/수신 기반 shaping 배선과 물리적으로 가능한 pass mask 확장 후 새 r002 최소100k이며, 나머지 작업은 사용자 요청에 따라 중단했다.
- 13:57 KST 최신: Keeper는 이동 방향과 무관하게 공을 바라보며 전후진한다. 양 팀 정면 경합이0.60초 정체되면 한 팀이 킥 플레이트로 측면 탈출을 시도하고 상대는 물러나며, 탈출 주체는1.40초마다 교대한다. 300초 Fallback은 기존 공 이동0.72m/0:0에서61.71m/1:1/소유전환81회로 개선됐다. MNG 프리팹에는 활성 중계/Human 추적 Main Camera1개만 남긴다.
- Builder와 EditMode188/188, 카메라 단일1/1, 경합 단일1/1, 최종300초1/1은 통과했다. 전체 PlayMode 최종 제품 실행은19/20이며 유일 실패는 Trainer 없는 정책 Scene의 Academy 첫 틱이 테스트 비활성화보다 앞선 순서 경합이다. 중단 직전 모든 정책 Scene 테스트에서 Academy 자동 스텝을 선차단하도록 수정했지만 이 테스트 전용 마지막 수정은 아직 재실행 전이다. 재개 시 전체20/20을 먼저 확인하고 기존 frozen M1/M2/M1B를 새 실행 의미로 재평가한다.
- 12:20 KST 최신: 포메이션 오차는 최초 배치와 득점 뒤 중앙 킥오프에만 적용하고 경기 중에는 적용하지 않는다. Planner v2는 역할별 anchor/공 추종, 한 명의 primary presser,1.25초 유지·3m 교체 우위,3m 목표 분리를 사용하며 관측133/행동[6]은 불변이다.
- 실행 계약 v3의 random-valid 기준선은 M1 66득점·M2 빠른회수52/실점45다. 기존 frozen M1은99득점/자책0/timeout1, M2는 빠른회수62/전체64/실점35/timeout65로 각각 새 gate를 통과했다. 근거는 `Logs/MNG_M1Attack-20260910-r001-eval-r005`, `Logs/MNG_M2Defense-20260910-r001-eval-r003`이다.
- M1A 정지 수비 근거는 보존했다. 별도 M1B는 정상 Navy Fallback을 활성화하며 random-valid20득점 대비 요구30을 사용한다. `MNG_M1Moving-20260910-r001`은 승인 M1에서 초기화해20,006 step을 완료했고 frozen ONNX SHA `FAF9EAE392C604042C14787C0548E300594271BF8BF5BC85CE44E87FC3A6C8D5`로 움직이는 Navy100상황에서33득점/자책0/timeout67을 기록해 통과했다. 근거는 `Logs/MNG_M1Moving-20260910-r001-eval-r001`이다.
- 최신 회귀는 EditMode40/40, PlayMode17/17이다. PlayMode는 Navy의 실제 이동, 킥오프 외 스폰 보정0회, 킥 플레이트 패스·슛·드리블·탈취, M1/M2/M3 기준선을 함께 확인한다. 승인 M1B 눈검사용 Scene은 `Assets/_Soccer/Manager/Curriculum/M1_AttackMoving/Scenes/MNG_M1_AttackMoving_Evaluation.unity`다.
- 11:05 KST 감사에서 M1 Navy가 멈추는 것은 사용자 실행 오류가 아니라 M1 코드가 Navy Fallback과 네 `MNG_PlayerSkillExecutor`를 비활성화한 결과임을 확인했다. 기존 M1 승인은 정지 수비 상대 공격이며 움직이는 수비 상대 성능은 별도 M1B가 필요하다.
- 일반 경기와 M1/M2/M3 생성기에 seed 재현·좌우 대칭을 유지하는 축별 최대±0.75m 스폰 오프셋을 추가했다. 확정 carrier의 킥 플레이트 앵커는 고정한다. v2 무작위 기준선은 M1 58득점, M2 빠른회수66·실점50이다.
- 기존 frozen ONNX v2 재평가는 M1 87득점/자책0/timeout13, M2 빠른회수71/전체회수73/실점40/Red득점2/timeout58로 모두 새 gate를 통과했다. M1 근거는 계속 정지 Navy 범위다. EditMode37/37, PlayMode14/14 통과.
- 여러 선수가 같은 경로로 움직이는 원인은 팀 명령 하나를 `MNG_TeamPlanner`가 공 기준 고정 depth/lane 목표로 변환하고 동일 motor가 추종하기 때문이다. 역할별 formation anchor, carrier/presser/cover/outlet 동적 배정, 임무 hysteresis, 동료 분리를 저수준 플래너에 추가하되 Manager 관측133/행동[6]은 유지하는 것이 권장안이다.
- 기존 Core Stadium을 보존한 별도 MNG 런타임을 개발 중이다. 관측133 floats, 행동 discrete `[6]`, 선수8명 비-Agent 실행층, H키 Red Striker 소유권, 경기300초/GoalPause3초, 공 scale×1.10·mass3.0·friction0.05·bounce0.15가 최신 계약이다. 사용자가 M0 실제 화면·조작·HUD/H키 검토를 합격으로 확인했다.
- 사용자 결정에 따라 기존 center+wing 킥 플레이트 형상을 MNG 전용 `MNG_KickPlate`로 유지했다. 패스·슛은 plate가 실제 공에 닿을 때 한 번만 impulse를 주며, 공은 plate 전방 드리블 위치에서 소유된다. 전진 때만 제한 보정하고 정지·후진은0.08초 뒤 소유 해제, 상대 plate zone은0.02초에 탈취할 수 있다.
- 최신 자동 근거는 EditMode `34/34`, PlayMode `12/12`, Builder와 M1/M2 Windows Player 오류0이다. 실제 plate 패스5/10/20m와 슛10/20m는 각20/20, 직선/좌/우8m 드리블 각20/20, 양 팀 탈취20/20이다. Fallback 300초는15,001tick 동안 NaN·공 소실·중복득점 없이 완주했지만0:0이라 안정성만 통과했다.
- `MNG_M1Attack-20260910-r001`은20,007 step PPO 후 frozen ONNX 고정100상황에서92득점/자책0/timeout8로 요구75득점을 통과했다. `MNG_M2Defense-20260910-r001`은 승인 M1 PT에서 초기화해20,004 step PPO 후10초 내 회수63/100, 전체회수68, 실점29로 최소회수60·최대실점54를 통과했다.
- M1/M2는 실제 정책 성능 승격 근거다. M1/M2 평가 JSON, Player log, build manifest와 실패 진단 r001/r002도 보존했다.
- M3 `MNG_M3Fallback-20260910-r001`은 승인 M2 PT에서 initialize-from하여100,080 step을 정상 완료했다. 최종 ONNX SHA-256은 `FE69ECD1893994D57C8D008C5AEF7B087A0FC6531682660F099504729C3A4B1E`이다. 학습 보상이 대부분0이라 학습 효과는 아직 입증되지 않았다.
- M3 정식300초×40경기 frozen 평가 경로와 scoreRate>=0.50·무득점<=20%·양팀득점 표본 gate를 구현했고 EditMode36/36을 통과했다. 사용량 중단선 때문에 평가 Player 빌드/실행은 아직 하지 않았으며 M3는 미승격 상태다. 상세 명령·SHA·재개 순서는 `docs/archive/manager/m-stage/MNG_05_Development_Handoff.md`가 단일 인계다.

## 2026-09-06 L2-Find/L2-Score 구현 인계

- **r017 종료·미승인·개발 중단:** r017은500,248에서 exit0 자연 종료했고 최종 PT/ONNX/root ONNX/config/training-status/log를 저장했다. 마지막250k는 기존 균일360도 분포였으며 최종5요약 성공·소유98.61/98.36/97.14/98.59/98.72%, 평균3.26~3.91초라99% 연속 gate 미달이다. 방향별 최신5요약은 Front100/100/97.78/100/100%, SideBlind100/100/100/94.74/94.12%, Rear66.67/75/83.33/100/100%였다. 60도 단계는 연속100%였고140도 확장 직후 SideBlind81.82%로 떨어져 초기 방향이 인과적 병목임은 재현했지만, 최종 전방위 희귀 실패를 제거하지는 못했다. 보상 누출0, 코드 행동개입0이며 고정300 승급평가는 열지 않았다. PT SHA`F7B6F591C5F87E1DD5E842F8432CAF1C04B342EA053887FB74020B74920EE60B`, ONNX SHA`35A5B11F16264B11F7D5D62BB45D57E2C8F6F21027F9207B1D9E5B4AA0DC3C4B`; checker/PT load 통과. 전체90파일 manifest와 결과는 `Logs/CurriculumL2Find-20260907-r017-result`에 보존했다. 사용자 지시에 따라 r018·L2-Score·추가 학습을 시작하지 않는다.
- **r016 중단·원인확정·r017 방향 커리큘럼:** r016은252,928 PT/ONNX 저장 후 인과감사를 위해 중단했다. 최근5요약95.83/100/96.77/96.61/98.59%이며 전체25요약 평균98.17%라 분할보상도 희귀 실패를 제거하지 못했다. r014 고정300에서 성공291건의 최초 주 추격자 방향오차 평균은45.65도, 실패9건은116.72도였고 실패는 최선 접근 시점에도108.29도였다. 실패 초기거리17.17m/팀 최선거리3.40m와 Far·MiddleThird100%를 함께 보면 환경 도달불가나 학습량 부족이 아니라 측면에서 공을 향해 회전한 뒤 접촉하는 제어가 병목이다. 전방 Ray(+/-60도)와 후방 Ray(180도+/-45도) 사이 좌우60~135도에 측면 사각 구간이 있고, vector 공 offset은 월드축인데 이동 action은 몸축이며 몸 방향 vector가 없다. 일반 보상은 L2-Find allowlist로 차단돼 누출 간섭도 없다. r017은 보상·관측크기·action을 바꾸지 않고 최초 최근접 선수의 최대 방향오차를60/100/140/180도로 확대한다. 진행50% 이후 마지막250k는 기존과 같은 균일360도 분포다. r014300,060에서 optimizer 없이16workers/500k로 한정하고99% gate와 독립300은 완화하지 않는다. r016 원본·최종 artifact·감사자료는 `results/CurriculumL2Find-20260907-r016`과 `Logs/CurriculumL2Find-20260907-r016-interrupted-causal-audit`에 보존한다. L2-Score는 시작하지 않는다.
- **r015 종료·r016 분할보상:** r015는300,112에서 자연 종료했고 최종5요약 성공95.16/92.00/95.77/90.74/100%로99% gate 미달이다. 동일 새seed31415934 균일300은 r014와 r015가 모두291/300=97%였으며, 실패 episode의 두 번째 추격자 최선거리 평균은 r0145.37m에서 r01510.54m로 악화됐다. 두 번째 추격자가 첫 번째 실패를 보완해22.41m에서4.02m까지 접근한 사례는 있어 두 명 개념은 유지하지만, 개인 거리·방향 보상을 두 명에게 전액 지급해 shaping 예산을 두 배로 만든 구조는 폐기한다. r016은 두 추격자 각각에게 기존 개인 거리·방향 potential의0.5배만 지급해 합산 예산을 r014와 같게 한다. team/terminal 보상, timeout`-0.5`, 실제접촉 성공,15초, 균일배치, lr0.00003/beta0.001과 코드 행동개입0은 불변이다. r015가 아니라 r014300,060 PT에서 optimizer 없이16workers/300k로 한정하며 L2-Score를 시작하지 않는다. r015 원본과 고정평가는 `Logs/CurriculumL2Find-20260907-r015-result`에 보존했다.
- **r015 두 추격자 원인수정:** r014 최종 새seed 균일100의 유일한 실패는 최초 최근접 Midfielder였고 DefenderKeeper가 최근접인8경기는 모두 성공해 골키퍼 경계가 단독 원인이라는 증거는 없다. L2-Find는 실제 첫 접촉 즉시 종료하므로 L1 support 거리두기와 L2/L3 waiting/receiver 보조도 적용되지 않는다. 그러나 기존 보상은 team 최단거리 potential 외에 최초 최근접1명만 개인 거리·방향 potential을 받아 다른 선수의 적극 추격 credit이 부족했다. r015는 최초 최근접2명에게 각각 기존 signed 거리·방향 potential을 주며 행동·회전·속도·위치를 코드로 바꾸지 않는다. timeout은 r014의`-0.5`로 유지하고 실제접촉 성공·15초·균일배치·나머지 보상·lr/beta는 불변이다. 미실행 timeout강화 가설과 첫 검증 실패는 `Logs/L2-Find-r015-timeout-hypothesis-superseded`에 보존한다. 수정 후 Python/Unity/Builder/Player/ProjectSettings를 다시 검증하고 r014300,060에서 optimizer 없이16workers/300k 한정학습한다. L2-Score는 시작하지 않는다.

- **r014 종료·미승인·r015 timeout 실패 신호 분리:** r014는656에서 init_path 없는 설정으로 올바르게 재개돼 lesson0 Near20%가110k에서 lesson1 균일로 전환됐고300,060 PT·ONNX를 자연 저장했다. 종료 래퍼 exit1은 PyTorch stderr deprecation warning을 PowerShell이 오류로 승격한 것이며 Trainer/Player0, training-status final300,060, 전체 artifact로 정상 종료를 확인했다. 최종 균일5요약 성공·소유100/96.55/100/96.67/98.59%,3.01~4.33초라 gate 미달. 새seed 균일100 진단은249,980=98%, 최종=99%이며 승인300을 대신하지 않는다. `Logs/CurriculumL2Find-20260907-r014-result/sha256-manifest.json`에 전체44파일 보존. r015는 timeout group penalty만`-0.5→-1.0`으로 바꾸고 실제접촉·15초·균일배치·나머지 보상·lr/beta·코드 행동개입0을 유지한다. r014300,060에서 optimizer 없이16workers/최대300k 한정이다. 임시200k 설정은 Catalog300k 계약 테스트1건이 정확히 거부해 `Logs/L2-Find-r015-validation-attempt1`에 보존했고 학습 전300k로 교정했다. 전체 검증·빌드 후 시작하며 L2-Score는 시작하지 않는다.

- **2026-09-07 사용자 요청 중단·r014 재개 지점:** 새 annealed Near YAML은 실제 POCA parser를 통과했고 Python46/46, EditMode146/146, PlayMode6/6, 공통 Builder12 profiles/11 trainable, Windows Player build, ProjectSettings 3개 SHA 불변을 확인했다. `CurriculumL2Find-20260907-r014`를 r008300,128에서16workers/seed27182819로 시작했으나 사용자 요청 직후 정상 Ctrl+C로 **656 step** 번호 PT·ONNX/root ONNX/checkpoint/config/log/training-status를 저장했다. 최근 summary는 아직0개라 성능 판단이나 승인이 아니다. Trainer/Player/Unity0개를 확인했다. 재개 전용 `curriculum_l2_find_annealed_near_15s_resume_poca.yaml`은 `init_path`가 없으며 `Logs/Resume-CurriculumL2Find-20260907-r014.ps1`로 같은 Run을 `--resume`해야 한다. 초기화 YAML·`-InitializeFrom` 재사용 금지. 보존 자료는 `Logs/CurriculumL2Find-20260907-r014-paused/README.md`와 `sha256-manifest.json`. L2-Score는 시작하지 않는다.

- **r013 미승인·r014 제한 Near→균일 통합:** r013은16workers 재개 후127,272에서 정상 저장했고 최근5요약98.31/98.28/97.22/98.31/98.48%, 마지막 Near91.67%라 낮은 entropy 단독 변경을 폐기했다. PT·ONNX/config/log/inspect/SHA는 `Logs/CurriculumL2Find-20260907-r013-result`에 보존했다. r014는 r008 보상·lr/beta로 복귀해 진행35%까지만20% Near를 rehearsal하고 이후65%를 균일분포로 학습한다. r009 상시50% Near가 Mid/Far를 훼손한 원인을 피하며 최종5요약·고정300은 균일구간만 사용한다. 자원 실측에 따라16workers/aggregate300k, 코드 행동개입0회다. L2-Score는 시작하지 않는다.

- **r013 RAM 안전 저장·동일 optimizer 16workers 재개:** entropy 단독 실험 r013은32workers에서 약451~470step/s였지만 RAM89%·여유3.42~3.45GiB가2회 반복되어 자원 기준에 따라71,876 PT·ONNX를 정상 저장했다. Trainer/Player 종료 후 래퍼만 정확한 PID로 정리했다. `init_path` 없는 `curriculum_l2_find_low_entropy_15s_resume_poca.yaml`로 Catalog/Player manifest를 갱신하고 같은 optimizer·seed·보상·환경·aggregate300k를16workers로 재개한다. 코드 행동개입0회이며 L2-Score는 시작하지 않는다.

- **2026-09-07 r012 복구·미승인·r013 entropy 분리:** r012는 사용량 경계에서 정상 저장한130,448 번호 PT·ONNX가 보존됐지만, 활성 YAML의 `init_path`를 남긴 채 `--resume`한 명령이 r008300,128을 읽고 즉시 종료하며 공용 checkpoint/training-status를 덮었다. 유효130,448 SHA와 이후 오염 artifact를 `Logs/CurriculumL2Find-20260906-r012-interrupted/sha256-manifest.json`에 함께 보존했으며 r012는 다시 resume하지 않는다. 유효 최근5요약100/98.46/96.77/96.83/98.31%로 terminal group0.4→1.0도 악화되어 폐기한다. r013은 r008 보상·균일배치·lr0.00003으로 복귀하고 entropy beta만0.001→0.0001로 낮춘다. r010의 lr/beta 결합 변경을 분리해 드문 확률 행동 원인을 시험하며 r008300,128에서 optimizer 없이32workers/300k로 한정한다. L2-Score는 사용자 지시로 시작하지 않는다.

- **2026-09-07 r012 사용량 재개:** 2026-09-06 23:43 KST 실제5시간 잔여5% 경계에서 r012 전용 콘솔에 정상 Ctrl+C를 보내130,448 PT·ONNX를 저장했고 Trainer/Player/Unity0개를 확인했다. 중단 checkpoint PT SHA`387D91D7BD494BFDB42FBE70E5310B14A186574E5A92AADC411E387907EAA0EC`, ONNX SHA`28DBE77FCBBF5C19329E6D06004725D30286B289C36B2EE2B331A8AEBB594071`이며 ONNX checker를 통과했다. 2026-09-07 재확인 사용량은5시간 잔여100%/주간 잔여69%다. 동일 r012 optimizer와32workers를 `--resume`하고300k 자연 종료 후 최근5요약을 판정한다. L2-Score는 사용자 지시로 시작하지 않는다.


- **L2-Find r011 종료·미승인·r012 실제접촉 강화:** r011은32workers/300,172에서 PT·ONNX를 정상 export했고 최근5요약 성공96.97/100/100/98.61/100%로 연속99% gate에 미달했다. 중간190k의100% 두 구간 뒤에도 근접실패가 재발했고, 실제접촉 전4m 진입 후2m 이탈 group-0.2는 최종 구간에서 발생률0~1.52%에 그쳐 실패를 안정적으로 없애지 못했다. 추가 고정300은 실행하지 않으며 결과는 `Logs/CurriculumL2Find-20260906-r011-result/sha256-manifest.json`에 보존한다. r012는 폐기한 이탈 벌점을 제거하고 실제 Red 접촉에서만 기존 group성공0.4에 group+0.6을 더해 실제 terminal 접촉의 group가치를1.0으로 높인다. 거리·방향 shaping, timeout-0.5,15초, 균일배치, 코드 행동개입0회는 그대로이며 r008300,128에서 optimizer 없이 lr0.00003/beta0.001/32workers/300k로 한정한다.

- **L2-Find r010 종료·미승인·r011 원인수정:** r010은32workers/300,272에서 PT·ONNX를 export했지만 최근5요약100/95.31/98.57/94.92/98.51%로 r008보다 악화했다. 낮은lr/beta 안정화 가설을 폐기하고 추가 고정300은 실행하지 않는다. r010 결과는 `Logs/CurriculumL2Find-20260906-r010-result/sha256-manifest.json`에 보존했다. r011은 실제 r008 실패의2.83~5.34m 접근 뒤 이탈을 다룬다. team 최단거리4m 진입 후 실제접촉 없이 최선보다2m 이상 이탈하면 group-0.2를1회만 지급한다. 실제접촉만 성공, timeout-0.5, 균일배치,15초, 코드 행동개입0회는 불변이다. r008300,128에서 optimizer 없이 lr0.00003/beta0.001/32workers/300k로 한정한다.

- **L2-Find r009 종료·미승인·r010 안정화:** r009은32workers/300,068에서 PT·ONNX를 export했다. 최근5요약97.56/100/98.78/100/100%로 연속99% gate 미달이다. 99,988 checkpoint의 동일seed 고정100은100%였으나 새seed31415928 균일300은291/300=97%(Near100/Mid94.68/Far96.92%)라 근거리50% oversampling이 Mid/Far 일반화를 약화했다. r009는 폐기·미연장하고 결과를 `Logs/CurriculumL2Find-20260906-r009-result/sha256-manifest.json`에 보존한다. r010은 균일298/300의 r008300,128에서 optimizer 없이 시작하며 균일분포·보상·실제접촉을 유지하고 lr0.00001/beta0.0003으로 업데이트 진동과 불필요 행동을 줄인다. 코드 행동개입0회,32workers/300k 한정이다.

- **20:49 KST 사용량 기준 중단:** 실제 사용량이5시간97%(잔여3%)/주간31%(잔여69%)여서 사용자 지정5시간 잔여5% 중단선을 적용했다. Trainer/Player/Unity0개이며 검증된 r009 학습은 아직 시작하지 않았다. 정확한 모델·SHA·검증·명령·다음 판정은 `Logs/L2-Find-usage-handoff-20260906-2049/README.md`에 저장했다. 5시간 reset은2026-09-07 00:37:49 KST이고+10분인00:47:49 KST에 단발 재개한 뒤 최신 사용량부터 재확인한다. 크레딧은 사용하지 않는다.

- **L2-Find r008 종료·진단300·r009 설계:** r008은32workers/300,128에서 PT·ONNX를 export했고 최근5요약98.59/98.63/100/98.53/98.46%로 연속99% gate 미달이다. 50k checkpoint 동일seed 고정100은91/91/95/95/95/98%였다. 새seed31415927 진단300은298/300=99.33%였으나 학습요약 미달을 대체하지 않는다. 실패2건은 선택actor가 초기21.44m에서5.34m까지4.50m/s로 접근한 뒤 과주행했고 다른 Red도2.83m에서 접촉하지 못했다. r009는 실제접촉/15초/보상구조를 유지하고 학습의50%만 Red 주변3~12m 유효 위치를 oversample하며 최종 평가는 parameter0의 균일 무작위다. 코드 행동·회전·속도 개입0회. 읽기전용 행동/거리 telemetry는 Python46/46, EditMode146/146, PlayMode6/6, Builder/Windows build, ProjectSettings 불변을 통과했다. r008 결과 SHA는 `Logs/CurriculumL2Find-20260906-r008-result/sha256-manifest.json`에 보존했다.

- **L2-Find r007 종료·미승인·r008 종료신호 수정:** r007은32workers/300,444에서 PT·ONNX를 export했으나 최근5요약92.73~96.88%였다. 49,972/99,992/149,963/199,988/249,971/300,444 동일seed42424243 고정100은 각각92/96/97/94/95/94%로99% 미달이며 추가 독립300 가치가 없어 미승인이다. 근접 adaptive actor 보상이 접촉 없는 접근의 가치를 키운 것으로 판단해 폐기한다. r008은 r006 고정0.01/m team/actor signed potential로 복귀하고15초 동안 실제 Red 접촉이 없을 때만 group`-0.5`를 EndGroupEpisode 직전1회 지급한다. 성공·빠른접촉·방향·공배치·Navy비활성·킥마스크·코드 행동개입0회는 불변이다. r006 최종 PT에서 optimizer 없이 lr0.00003/beta0.001/32workers/300k로 한정한다. 전체 hash는 `Logs/CurriculumL2Find-20260906-r007-result/sha256-manifest.json`에 보존한다.
- **L2-Find r006 종료·고정평가·r007 원인수정:** r006은32workers/500,216에서 PT·ONNX를 정상 export했다. 최근5요약 성공은100/98.59/96.30/94.83/97.10%로 연속99% gate에 미달했고, 최종 PT SHA`E889FAAB64CB27A66DE469C0384A48BF272E6EA9E5CF103DDDD9B8A21389CB67`의 새seed31415926 정확300은294/300=`98%`였다. 실패6건은 전부 Near이며 초기거리10.74m·최선거리4.39m, Mid/Far100%였다. r006이 먼 공 포화를 해소했지만 고정0.01/m actor potential은 가까운 공의 완전 접근 총보상이 작았다. r007은 team potential 불변, 시작 최근접 actor의 접촉기준2m 완전 접근 credit을 최소0.3으로 정규화하고 최대0.05/m·signed 누적-0.5..+0.5로 제한한다. 실제 접촉만 성공이며 코드 이동·회전·속도 개입0회다. r006 최종에서 optimizer 없이 lr0.00003/beta0.001/32workers/300k로 한정하고 검증 뒤 실행한다. r006은 미승인이므로 L2-Score 전환하지 않는다.
- **L2-Find r005 종료·고정평가·r006 원인수정:** r005는32workers/1,000,244에서 PT·ONNX를 정상 export했고 최근5요약 성공94.74~98.44%로99% 연속 gate에 미달했다. 주요 checkpoint 동일seed4871201 고정100 선별에서 최종이98%로 최고였고, 최종 PT SHA`14C773B6463F9082A228CA2A23A7375D4E919DCB06C77794A1F865AD34ADC82A`의 새seed27182818 정확300은289/300=`96.33%`였다. r004 새15초 baseline93% 대비+3.33%p, 평균 첫 접촉4.127→3.760초, peak planar7.08→7.26m/s 개선됐지만99% 미달이므로 승인·L2-Score 전환하지 않는다. 실패11경기의 초기거리43.86m/최선거리13.48m이고 MiddleThird100%와 달리 양쪽 third·wide·최초 최근접 midfielder/striker에만 실패가 있었다. 기존 거리 potential cap0.2가20m 개선 뒤 포화되어 먼 공의 마지막 접근 신호가 끊긴 원인에 따라 r006은 signed team/선택actor cap을0.5로 늘리고 entropy beta0.003→0.001로 낮춘다. 다른 보상·15초·성공·공배치·코드 개입0회는 유지하며 r005 최종에서 optimizer 없이32workers/500k로 한정한다.
- **L2-Find 강화 r005 및 골키퍼 범위 확대:** 사용자 실기 관찰에 따라 기존 승인 r004500,656과96%/95% 독립300 증거는 보존하되 새 최종 기준으로 쓰지 않는다. L2-Find만 제한시간`30→15초`, 성공 기준`95→99%`로 강화한다. 기존 상대 공 위치 관측을 유지하고 코드 이동·회전·가속 개입은0회다. 시작 최근접 actor에 방향오차 signed potential`0.001/degree`, 누적`-0.1..+0.1`을 추가하고, 실제 첫 접촉에 시작 최단거리/최고속도9m/s로 정규화한 빠른 도달 보상 최대`+0.4`를 지급한다. 거리 team/actor potential, 실제접촉 성공, Navy 비활성, 킥 마스크, 공 배치는 유지한다. 새 YAML은 r004에서 optimizer 없이 초기화하는32workers/최대1m/50k 저장이며 동일 lr0.00005/beta0.003을 유지한다. 골키퍼 공통 측면 범위는 Home`±14→±28m`, Engage`±18→±36m`로2배 확대하며 하프라인 경계는 불변이다. Python46/46, Unity 전체 EditMode146/146, Soccer PlayMode6/6, Base/Attack/Defense/Press/Rule 및 L0~L3 parity,12 profiles/11 trainable, Windows Player build를 통과했고 ProjectSettings/EditorBuildSettings/Standalone define은 불변이다. 새 Player에서 동결 r004를 seed31427182 고정300으로 평가한 결과 성공279/300=`93%`, 평균 첫 접촉4.127초, 평균 peak planar7.08m/s·closing6.923m/s·속도사용률78.67%로 새99% gate에 미달해 강화 학습 필요를 확인했다. PT SHA`21585B106653D7928729823B4C8B1C5887F38864853D68DD56237F0A05C2708B`는 평가 전후 불변이다. 중단된 L2-Score r002는 보존하고 새 L2-Find99% 승인 전 재개하지 않는다.
- **15:33 KST L2-Score r002 사용량 정상 중단:** r002는 step16,760에서 해당 세션에만 정상 중단 신호를 보내 PT/ONNX/config/trainer log/inspect를 보존했고 Trainer/Player/Unity 잔류0을 확인했다. 정확한 파일·SHA와 재개 금지 인계는 `Logs/L2-Score-usage-handoff-20260906-1533/README.md`다. 새 강화 L2-Find 승인 뒤에는 이 optimizer를 resume하지 않고 승인 checkpoint에서 새 L2-Score Run을 초기화한다.
- **L2-Score r001 정체·r002 원인수정:** r001은32workers/500,088/정상 종료, 오류·누출0이며 최종 PT SHA`EF115E28236D174EB3586FF0A823E657ED684CAEC42BB0E4E5C2BBA520AD1022`, ONNX SHA`E862C4F8FE846FA9ADDECC2C42EE2715C31126E5A17385ADA8DEBFED944D0679`이다. 최근5요약 성공0~41.67%/자책골0~20%/긴패스0으로 미달했다. 로그상 최선349,927의 동일seed97531 고정100도 baseline28%→30%뿐이고 소유88→80%, 자책골2→5, 최선골거리35.20→40.24m로 악화해 r001을 resume하지 않는다. r002는 승인 L2-Find r004500,656에서 다시 초기화하고 lr0.0003→0.00005, beta0.005→0.003, 첫 전방슛 보상0.08→0.02, 실제득점 보상0.4→1.0으로 바꾼 결합 실험이다. 자책골-1.0, 성공/스폰/30초/긴패스 조건은 불변이며32workers300k로 한정한다. 코드·보상표·README·테스트·Catalog/YAML을 함께 검증하고 개선 없으면 단순연장하지 않는다.
- **L2-Score r001 시작 결정:** 승인 L2-Find r004500,656의 동일seed97531 고정100 baseline은 성공28%/소유88%/자책골2/슛시도80%/유효슛21%/평균25.04초/유효긴패스1%/긴패스득점bonus0이다. 승인 r018 baseline의8%/46%/자책골2보다 탐색·득점은 개선됐으나 최종95%·자책골0에는 미달한다. 검증된 `curriculum_l2_score_poca.yaml`로 r004 PT에서 optimizer 없이 `CurriculumL2Score-20260906-r001`, seed314159,32workers,500k를 정확히 한 번 실행하고50k checkpoint와10k inspect를 확인한다. Navy 비활성,30초 단일결과, 자책골 즉시실패, 엄격 긴패스 조건은 불변이다.
- **L2-Find r004 승인·L2-Score 전환:** r004는32workers/500,656/정상 종료했고 PT SHA`21585B106653D7928729823B4C8B1C5887F38864853D68DD56237F0A05C2708B`, ONNX SHA`587AA7B3794FCBFC380FA72C291A578AF8BD173DB686C35F2B7221724C59418F`, config/log/50k checkpoint/inspect를 보존했다. 299,939 checkpoint의 동일seed 고정100은97%였지만 새seed24681357 정확300은278/300=92.67%라 미승인이다. 최종500,656은 같은seed 고정100=96%, 새seed 정확300 두 실행이288/300=96%와285/300=95%로 완화 없는 gate를 모두 통과했다. 두 action trace가 달라 bitwise 재현은 아니지만 두 stochastic repeat가 각각 기준 이상이다. 코드 이동 보조0회, 보상 누출0이다. 평가기의 마지막 Stats batch가 요청300을301로 넘길 수 있던 문제는 정확히 앞300개 core episode metric만 집계하고 raw/폐기 수를 기록하도록 고쳤으며 Python46/46 통과했다. L2-Find 승인 모델은 최종500,656으로 고정하고 L2-Score YAML을 이 PT에서 optimizer 없이 초기화하도록 변경했다. 다음은 YAML parser/Unity profile·Builder/Windows Player/ProjectSettings 검증 후 L2-Score 고정 baseline과32workers 한정학습이다.
- **14:22 KST 사용량 초기화·r004 재개:** 실제 사용량이5시간0%/주간0%로 초기화됐고 사용자 지정 중단선은 주간 잔여2% 이하 또는5시간 잔여5% 이하로 확정했다. 크레딧은 사용하지 않는다. 인계의 SHA·Player·r004 config/max500k를 유지하고 Run/로그 미존재, Trainer/Player/Unity0개, 포트5740~5771 비어 있음을 재확인했다. 검증된 r004를 seed424242/32workers/500k로 정확히 한 번 시작하고10k step 증가와50k checkpoint를 inspect로 확인한다.
- **12:33 KST r004 학습 직전 사용량 중단:** r003 동결300경기82% 원인에 따라 초기 최근접 actor 한 명에게만 signed bounded potential(`0.01/m`, 에피소드 누적 `-0.2..+0.2`)을 주는 r004를 구현했다. 기존 team potential과 실제 접촉 성공 판정은 유지하며 코드가 선수의 action·방향·속도·위치를 바꾸는 개입은0회다. 음수도 허용하는 별도 reward API로 분리해 다른 curriculum 개인 보상의 양수 계약은 유지했다. Python41/41, 최종 EditMode143/143, 전체 Soccer PlayMode18/18, Builder Base/Attack/Defense/Press/Rule 및 L0/L1/L2/L2-Find/L2-Score/L3 parity, 12 profiles/11 trainable, Windows Player145,245,921 bytes·오류0을 통과했고 Standalone define은 `APP_UI_EDITOR_ONLY;SENTIS_ANALYTICS_ENABLED`다. private helper 서명을 바꾼 첫 EditMode는 reflection 계약 1건 실패하여 원래3인자 서명을 복구하고 signed 전용 helper를 추가했으며 실패·최종 로그를 모두 보존한다. r004 YAML은 r0031,000,064 PT에서 optimizer 없이 초기화하는32workers/500k/lr0.00005/beta0.003 구성이고 Run·trainer log 경로는 아직 없으므로 학습은 시작되지 않았다. 실제 사용량5시간39%/주간98%(잔여2%)에서 사용자 지정 중단선을 지켰고 크레딧은 사용하지 않았다. 정확한 인계는 `Logs/L2-Find-usage-handoff-20260906-1233/README.md`; 주간 reset `2026-09-12 02:35:44 KST`보다10분 늦은 `2026-09-12 02:45:44 KST` 단발 재개 후 최신 한도를 먼저 확인한다.
- **L2-Find r003 고정300 원인확정·r004 설계:** 새seed20260906 고정300은 성공82%(247/300), 동결 PT SHA 불변이다. 거리별 Near86/Mid83/Far80%보다 구역·역할 차이가 컸다. 중앙90% 대비 측면76%, MiddleThird88/NavyThird83/RedThird77%, 최초 최근접 역할 Striker85/Keeper84/Midfielder75%였고 실패53경기의 초기 최근접 거리는42.70m, 최선 접근도19.04m에 그쳤다. r002의 네 선수 개별 high-water는 재사용하지 않는다. r004는 초기 최근접 actor 한 명에게만 signed bounded potential`0.01/m`, 누적`-0.2..+0.2`를 추가하고 team potential은 유지한다. 왕복 회복은 이전 음수를 상쇄할 뿐 반복 보상되지 않으며 코드가 행동·방향·속도·위치를 바꾸는 개입은0회다. r0031,000,064에서 lr`0.00005`, beta`0.003`,32workers500k로 한정한다.
- **사용자 사용량 기준 정정·진단 재개:** 중단 기준은 주간 사용량의 남은 비율이2% 이하일 때로 정정됐다. 현재 남은 약8%에서는 작업한다. r003을 더 연장하지 않고 초기 공 거리(20/40m band), 종방향 구역, 측면 구역, 최초 최근접 선수 역할별 성공률과 성공/실패의 초기·최선 거리를 읽기 전용으로 기록한다. 이 telemetry는 보상·행동·성공 판정·공 배치를 바꾸지 않으며 코드 이동 개입은0회다. 검증·빌드 뒤 동결 r0031,000,064를 고정300으로 진단한다.
- **08:50 KST 사용량 중단·정확한 재개점:** r003은 같은 optimizer로 총1,000,064에서 정상 종료했고 Trainer/Player/Unity0개다. 최근5요약 성공77.27/90.48/84.85/76.19/89.47%(평균83.65%), 누출0, 약574.89step/s. 최종 PT SHA`F44A8D3E249D21976C252A34B700BCB0E0E485ACDF4722C9E9078DA652EECB66`, ONNX SHA`7D513C2CE1CE437235B99C870431C9DE15FF2A9387DE5DC053C7E8018E470C64`; config/log/inspect/checkpoint를 보존했다. 같은seed86421 고정100은84%로500k의82%보다2%p만 개선됐고95% 미달이므로 추가 연장·독립300·L2-Score를 시작하지 않는다. 다음은 실패가 초기 공 위치/거리/선수역할에 집중되는지 조건부 telemetry를 추가해 같은 동결 모델로 진단한 뒤 보상 구조를 결정한다. 코드 추격/이동 개입은 계속0회다. 실제 사용량은5시간92%/주간91%로 중단하며 크레딧을 사용하지 않는다. 가장 늦은 weekly reset은`2026-09-12 02:35:44 KST`; 이 자동화는+10분인`2026-09-12 02:45:44 KST` 단발 재개로 이동한다. 재개 시 먼저 최신 한도를 재확인하고 초기화 미반영이면 충분히 늦춰 다시 단발 예약한다.
- **08:25 KST L2-Find r003 500k 종료·한정 연장:** bounded team potential r003은32workers/500,272/exit0, 최근5요약 성공76.92~84.62%(평균80.49%), 누출0이었다. PT SHA`B5886E3E5E0598CA8F3FFD589DA365A7525E8181700A9253BE175C8E8C044055`, ONNX SHA`857F1B1BBB645483C7C3DFC87CA23B14161076EBE90FF7E0BD9017EBDFBEA396`. 같은seed86421 고정100은82%로 r00175.25%보다 약7%p 개선됐지만95% 미달이라 승인하지 않는다. 같은 보상의 실제 개선 근거가 있어 optimizer를 유지한 같은 r003을 총1,000,000까지만 한 번 resume한다. init_path 없는`curriculum_l2_find_potential_1m_resume_poca.yaml`을 사용하며 성공 기준·환경·코드 이동 개입0회는 불변이다. 연장 고정평가가 계속 개선되지 않으면 버전을 반복하지 않고 학습 신호를 다시 진단한다.
- **07:55 KST L2-Find r002 종료·고정평가·r003 원인수정:** r002는32workers/300,284/exit0, PT·ONNX·config·log·inspect를 보존했다. 최근5요약 성공 평균69.15%였고 같은seed86421 고정100은50k45%/100k51%/250k57%/최종63%로, r001 최종75.25%보다 악화됐다. 모든 선수의 개별 high-water 보상이 가장 가까운 선수가 먼저 닿는 terminal 목표와 경쟁한 것으로 판단해 r002를 연장하지 않는다. r003은 보존 r001 최종에서 초기화하며 가장 가까운 Red의 초기-현재 거리 potential을 team에 `0.01/m`, 누적 `-0.2..+0.2`로 지급한다. 멀어지면 같은 credit을 회수해 왕복 farming을 막고 코드가 방향·이동·속도를 바꾸는 개입은0회다. 검증·빌드 뒤32workers500k 한정 실행하고95%는 완화하지 않는다.
- **01:50 KST L2-Find r001 종료·원인수정:** 승인 r018의 새 L2-Score 고정100은 득점8%, 확정소유46%, 자책골2건으로 미달했고, 소유 성공 경기의 첫 소유는 약11.3초라 사전 기준에 따라 L2-Find를 실행했다. r001은32workers/300,500/exit0, PT·ONNX·config·log·10k/50k/100k/200k/final inspect를 보존했다. 최근5요약 성공은15.38~61.54%(평균41.46%)로95% 미달이지만 같은seed86421 고정100 비교에서 승인r018 43%→r001 최종75.25%로 실제 개선됐다. 접근 team 보상이 가장 가까운 한 선수의 거리만 사용해 나머지 선수의 추격 학습 신호가 약한 원인을 확인했다. r002는 각 선수 자신의 거리 high-water 개선에 개인`0.005/m`, 선수당 cap`0.1`을 추가하고 거리 회귀·복구 반복에는 지급하지 않는다. team`0.01/m`, cap`0.2`와 성공 판정은 유지한다. r001 최종에서 learning rate`0.0001`로 새32workers300k를 한정 실행하며 추가 코드 이동 개입은0회다.
- 기준 모델은 승인된 L2 r018 checkpoint `results/CurriculumL1-20260905-r018/Soccer4v4_Base/Soccer4v4_Base-200184.pt`다. PT SHA-256은 `832EF690250A9A98136112FC1919353CD60C7F39821E4EBB92968408D5986E8F`, ONNX SHA-256은 `6D82A3DE105C2DD2FAB9D9542212A1B925E891AB176697DB41B86155564149D8`이며 원본 Run과 L3 r001-r005를 수정하지 않는다.
- L2-Score는 Navy를 등록 후 비활성화하고 Red 네 명만 Neural로 둔다. 공은 벽에서1m, Red 골 중심에서5m 이상 떨어진 경기장 내부에 무작위 배치한다. Red 득점, 자책골,30초 timeout 중 먼저 발생한 하나로 에피소드가 끝난다. Red 득점만 성공이며 자책골은 즉시 실패와 group `-1.0`이다. 최종 승인 기준은 독립 고정300경기 성공95% 이상과 자책골0건이며 완화하지 않는다.
- 득점 뒤 긴 패스 추가 보상은 확정 carrier의 명시적 킥, 지정 동료10~24m, 시작 시 Navy 골까지30m 이상, receiver가8m 이상 더 유리함, 실제 공 이동8m 이상, 중간 다른 접촉 없음, 지정 수신자의0.35초 안정 제어를 모두 요구한다. 이 조건을 먼저 완료한 뒤 같은 에피소드에서 득점하면 group `+0.2`를 한 번 지급한다. 근거리 우연접촉과 득점거리30m 이내의 불필요한 패스는 보상하지 않는다.
- L2-Find는 L2-Score의 동결 r018 진단에서 확정 소유가85% 미만이거나 평균 첫 소유가10초를 넘을 때만 실행한다. 킥을 막고 실제 Red 공 접촉을 성공으로 하며, 코드가 선수 방향·이동을 공으로 강제하지 않는다. 현재 접촉 뒤 동료 간격 코드 개입은0회다. 실행 시 독립 고정300경기95%를 통과해야 L2-Score 초기 checkpoint로 승격한다.
- 신규 학습은32 executable workers다. 구현 뒤 Python, EditMode, Soccer PlayMode, Builder parity, Windows Player, ProjectSettings를 검증하고 승인 r018의 L2-Score 고정 진단부터 수행한다.

## 2026-09-05 13:15 KST L3-A 구현·검증·학습 직전 인계

- **20:20 KST 안정 수신 보상 검증·사용량 인계:** 같은 지정 수신자가 마지막 접촉자이고 공 2.4m 이내에서 실제 0.35초를 처음 유지했을 때만 개인 `0.1`을 라운드당 최대 1회 주는 `CurriculumStableReceiver`를 L3 전용 allowlist에 추가했다. null carrier의 일시 회복은 기존 계약 안에서만 허용되며 다른 접촉자/carrier, 거리 밖, 우연접촉은 보상·성공이 아니다. 코드 기술 보조 빈도는 조건 충족 라운드당 1회이고 이동/행동 강제 개입은 0회다. Python 41/41, 최종 Unity EditMode 137/137, Soccer PlayMode 18/18, Builder L0/L1/L2/L3 parity 및 10 profiles/9 trainable, Windows Player 144,877,875 bytes 빌드 성공, ProjectSettings 변경 없음과 Standalone define `APP_UI_EDITOR_ONLY;SENTIS_ANALYTICS_ENABLED`를 확인했다. 첫 Unity 호출은 define reload 중 XML 없이 종료됐고, 실제 첫 테스트는 새 enum을 일반 보상으로 분류한 테스트 배열 누락으로 136/137 실패했으며 배열을 고쳐 최종 통과했다. 실패·최종 로그를 모두 보존한다. 실제 Codex 사용량이 5시간 91%/주간 59%가 되어 r006은 시작하지 않았다. 크레딧은 사용하지 않았다. 정확한 다음 단계는 새 r006 YAML을 r005의 고정 평가 최상 checkpoint `results/CurriculumL3-20260905-r005/Soccer4v4_Base/Soccer4v4_Base-149876.pt`에서 optimizer 없이 100k로 만들고 Catalog를 그 YAML/100k로 바꾼 뒤 Validate/Build, source snapshot, 32workers/seed12345 새 Run이다. 5시간 창 reset `2026-09-06 00:12:24 KST`보다 10분 늦은 `00:22:24 KST` 단발 재개로 자동화를 이동한다. 상세 복구 자료는 `Logs/L3-usage-handoff-20260905-2020`다.
- **20:08 KST r005200k 종료·안정 수신 보상 설계:** r005는32workers200,064/exit0. 최근5요약 성공32.26/35.48/32.86/25.40/22.37%, 안정35.48/45.16/40/44.44/34.21%로 연속50/85 기준 미달이며 후반 하락해 추가 단순연장을 중지한다. 같은seed13579 고정100은149,876 checkpoint33%, 최종29%; 각 안정41%/42%, 엄격패스95%, 소유97%/99%,4초 이내다. 안정 뒤 완료 전환은 약70~80%라 주 병목은 안정 자체다. 다음 단일 보상 변경은 같은 지정 수신자가 마지막 접촉자·공2.4m 이내로 실제0.35초를 최초 충족할 때 개인0.1을 라운드1회 지급한다. null carrier/거리 밖 회복/우연접촉은 제외한다. `CurriculumStableReceiver`를 enum 끝과 L3 전용 allowlist에 추가하고 기존 전진cap0.15·완료group0.4+receiver0.1·3m/6초 판정은 유지한다.
- **19:58 KST r005100k 개선·한정 연장:** r005는32workers100,132/exit0, 최근5요약 성공15.28/14.71/8.96/22.39/19.40%, 안정18.06/19.12/14.93/31.34/23.88%, 소유97.22~100%, 엄격패스91.04~97.01%,4.43초 이하, 누출0이다. 같은seed13579 고정100은23%로 r00414%보다+9%p 개선됐다. PT `0B817F5E7000002F15808D84973E66B2098F235E0C1CDC76D9E12AB3E603A872`, ONNX `C3B9DD3B1E5337CFA156FD40088683B9A70536A9E6CF4843BECDA45A16A93203`; config/log/inspect/checkpoints를 보존했다. 승급 미달이지만 동일 보상·optimizer에서 재현된 상승이므로 init_path 없는 `curriculum_l3_progressive_stable_reward_200k_resume_poca.yaml`로 같은 r005를 총200k까지만 resume한다. 200k에서도50%/85% 연속 기준이 열리지 않으면 단순연장을 중지한다.
- **19:48 KST r004 종료·보상 순서 수정:** r004는32workers100,412/exit0, 최근5요약 성공6.67~8.99%·소유97.73~100%·엄격패스95.95~98.88%·안정수신11.24~15.00%·2.56~3.43초로 정체했다. 최종 PT `A11E606D2D34031A1760467CBAC1BE2C50B566F220CB97758010710CE37D7B33`, ONNX `5E7E8EFA26D2427517E25EA9937FD5BC43CE8262DC0FC07D37B3CF7EA7BAE313`; config/log/50k·100k/PT/ONNX/inspect를 보존했고 오류·잔류프로세스는 없다. 같은seed13579 고정100은 성공14%/안정18%/전진0.815m/접촉실패44%/거리실패30%였다. 수신자 외 Red를0.35초 멈춘 격리 비교는97회·90.88초 개입에도 성공9%/안정16%로 악화되어 제거했다. 보상 감사 결과 안정 전 전진에도 shaping이 지급되던 문제를 찾아, 안정0.35초 확정 뒤에만 전진 high-water와 cap0.15 shaping을 갱신하도록 수정했다. 보상액·3m/6초·접촉실패는 불변이다. 새 YAML `curriculum_l3_progressive_from_r004_stable_reward_poca.yaml`로 r004 최종 PT에서 optimizer 없이 r00532workers100k를 초기화하고, 정체하면 단순연장하지 않는다.
- **19:27 KST 사용량 재개·r004 시작 결정:** 초기화 반영 뒤 실제 사용량은5시간1%/주간45%, 검증·평가 경계에서는10%/46%였고 크레딧은 사용하지 않았다. r001-r003과 r018은 불변 보존하고 r004 경로/포트가 비어 있음을 확인했다. 안정 전 최대2초 수신 회복 수정은 Python41/41, 최종 Unity EditMode136/136, Soccer PlayMode18/18, Builder L0/L1/L2/L3 parity와10 profiles/9 trainable, WindowsBuild 오류0/기존warning485를 통과했다. 테스트가 비공개 거리 상수에 접근한 첫 실행은 컴파일 실패로 보존했고 공용 API를 넓히지 않고 문서 계약값2.4m로 고쳐 통과했다. 승인r018/동일seed13579 고정100은 성공2→8%, 안정수신7→11%, 평균전진0.19→0.51m, 거리실패89→27%로 개선됐다. 회복 중 다른 선수 실제접촉62%는 성공이 아니라 즉시 실패로 기록됐고 우연접촉 배제 계약은 유지됐다. 성공 신호가 열렸으므로 새 r004를32workers/100k/seed12345로 한정 실행하며50k checkpoint와10k inspect를 확인한다.
- **13:42 KST 사용량 인계/정확한 재개점:** 실제 사용량5시간88%/주간43%, 제한 표시는 없으나 사용자 지시의 거의 소진 경계로 중단한다. 크레딧은 사용하지 않았다. Trainer/Player/Unity 없음. r00120k, r00260k(50k PT/ONNX), r00320k와 모든 inspect/log는 원본 보존했다. 최신 원인 계측 고정100경기는 엄격패스98%, 안정0.1/0.2/0.35초11%/9%/7%, 성공2%, 실패사유 거리89%/킥7%/다른접촉0/다른carrier0/시간0이다. 이 근거로 **안정 전** 공이2.4m 밖이면 수신 후 최대2초까지 추격·재접근을 허용하고 그 시간은 안정/전진 보상에서 제외하며 안정 clock을0으로 되돌리는 코드를 반영했다. 안정0.35초 뒤 거리초과, 다른 접촉/carrier, 추가킥은 즉시 실패이고 성공 순간 현재 지정 확정carrier/2.4m/순수3m 조건은 불변이다. 이 마지막 수정은 아직 Unity검증/빌드/평가를 하지 않았다. 재개 즉시 Python41/41 확인 → Unity EditMode135/135 → Soccer PlayMode18/18 → Builder Validate/WindowsBuild/ProjectSettings SHA → 승인r018 고정100 진단을 먼저 하며, 개선 확인 때만 새 `CurriculumL3-20260905-r004` 32workers100k를 시작한다. source/results/SHA는 `Logs/L3-usage-handoff-20260905-1342`에 저장했다. 실제5시간 reset은 `2026-09-05 18:00:30 KST`, 단발재개 목표는+10분 `18:10:30 KST`다.
- **13:32 KST r003 중단·수신 기술 보조 보정:** 두 번째 판정 Player도32workers20k에서 첫 요약 엄격패스100%/안정수신3.18%/성공0%였고20k까지 성공은 한 에피소드 수준이라 r002와 같은 정체였다. 지정 수신자가 엄격 인계를 만든 직후 기존 flight 수신추격이 종료되어 진행 중인 공을2.4m 안에서 잡지 못하는 것이 남은 원인이다. r003 세션만 Ctrl+C로 종료하고 event/log/inspect를 보존했다. `soccer_l2_pass_advice=1`의 기존 공추격을 엄격수신 뒤 안정0.35초까지만 연장하고 즉시 해제한다. 기존 `Pass Receiver Chase Seconds`에 개입시간이 합산된다. 공·선수 이동/소유를 강제하지 않고 이후3m 전진은 Neural이며, 검증·재빌드 후 새 r004로 효과를 확인한다.
- **13:24 KST r002 중단·두 번째 원인수정:** 첫 수정 Player로 32workers60k까지 실행했고 50k PT/ONNX를 보존했다. 50k까지5요약 L3성공은0/0/0/0.83/0%, 엄격패스 약97%, 안정수신 평균 약3%, 전진 평균0.17m였다. 한 번의 성공 신호는 열렸지만 carrier null 공백이0.2초보다 길어 대부분 중단되어 정체했다. r002 Player에서 더 연장하지 않고 특정 세션만 Ctrl+C 종료했다. 다음 Player는 엄격 L2 확정인계 후 지정 수신자가 마지막 접촉자이며 공2.4m 안에 머문 실제 시간을0.35초 안정소유로 세고, 다른 접촉/다른 확정carrier/추가킥은 즉시 실패, 성공 순간에는 지정 수신자가 현재 확정carrier여야 한다. null identity 자체를 소유 증거나 성공으로 세지 않는다. 수정·검증·재빌드 뒤 새 r003으로 시작하며 r001/r002는 resume하지 않는다.
- **13:18 KST r001 중단·원인수정:** 32workers로 실제20k까지 실행했고 약413.50step/s, 엄격패스100%였지만 L3성공0%, 안정수신0%→3.73%, 평균 전진0.12→0.15m였다. 수신 직후 평균 약0.27초 만에 전부 중단되어, 현재 carrier가 한 FixedUpdate라도 비는 전환 공백을 즉시 실패로 처리한 것이 0.35초 안정소유를 거의 불가능하게 만들었다. 해당 Trainer 세션에만 Ctrl+C를 보내 종료했고 관련 프로세스0, event/log/10k·20k inspect를 원본 보존했다. checkpoint_interval50k 전이라 PT/ONNX는 생성되지 않았다. 수정은 다른 접촉·다른 확정 carrier·추가킥 즉시실패를 유지하면서 최대0.2초의 identity 전환 공백만 허용하고, 공2.4m 이내에서 실제로 receiver가 확정 carrier였던 FixedUpdate 시간만0.35초에 누적한다. r001은 다른 Player로 resume하지 않고 검증·재빌드 후 새 r002100k를 시작한다.

- L3-A는 기존 L2의 엄격한 성공 판정을 선행 조건으로 그대로 사용한다. 확정 carrier의 명시적 킥, 킥 당시 유리한 고정 수신자, 실제 공 순변위2m, 현재 확정 carrier와 마지막 접촉자가 해당 수신자라는 조건을 모두 통과해야 L3 전진 단계가 시작된다. 우연한 접촉은 시작이나 성공이 아니다.
- 지정 수신자는 엄격 인계 뒤 마지막 접촉자로 공2.4m 안에0.35초 머물고 Navy 골 방향으로 공을 순수3m 이상6초 안에 전진한 순간 현재 확정 carrier여야 한다. 다른 선수 접촉/확정 carrier와 수신 뒤 모든 킥은 즉시 실패다. 일시적인 null carrier는 성공 증거가 아니지만 연속 물리 제어를 지우지도 않는다. 전진 보상은 수신 지점 기준 최고 전진량에만 비례하고 라운드 상한0.15라 왕복·반복 접촉으로 파밍할 수 없다. 완료 보상은 기존 group0.4+receiver0.1이다.
- 기존 코드 기술은 `soccer_l2_pass_advice=1`로 유지된다. 지원 위치, 실제 명시적 킥의 유리한 동료 방향 조준, 적합한 flight의 지정 수신자 추격이 개입하며 기존 Advice Stats로 빈도를 기록한다. L3 전진 이동은 코드가 대신하지 않는다. 따라서 결과는 Neural 단독이 아니며 고정 평가에서 개입 시간·횟수를 공개한다.
- 새 profile/scene/config: `curriculum-l3`, `Stadium4v4_CurriculumL3`, `curriculum_l3_progressive_from_l2_r018_poca.yaml`; aggregate max_steps100k, checkpoint50k, 32 executable workers, init은 승인 r018 `Soccer4v4_Base-200184.pt`, 난이도1/대기0/pass gate0/advice1이다. 관측379, action branches `[3,3,3,3]`, 공 물리와 keeper 보호는 불변이다.
- 검증: Python41/41, Unity EditMode135/135, Soccer PlayMode18/18, Builder `L0/L1/L2/L3 preserve the Base policy contract`, Windows Player10 profiles/9 trainable, build exit0. L3 build runtime DLL SHA `0A778F139B4E1F07A13CCD9BD7E0F5E4C48F826847A9C07F9D454B1CA5895319`, YAML SHA `2B4C8A0BE796282E5F95E70194FDFBBBB9884CE91293C19F5ACB2F4185E1373D`.
- 승급 기준: 최근5개 정렬 요약 각각 성공50%/초기소유85%/엄격패스85%/안정수신85% 이상, 평균15초 이하, 난이도1/대기0/gate0/보상누출0. 통과 후 새 seed의 독립 고정300경기와 L0/L1/L2 회귀를 수행한다.
- 학습 직전 관련 Trainer/Player/Unity 없음, Run 경로 미존재, host CPU1.63%/RAM36%(20.12GiB available)/GPU0%(0/8188MiB). 사용량은 5시간30%/주간34%, 제한 없음, 크레딧 미사용이다. 정확한 다음 명령은 `Tools/Train-Soccer.ps1 -Profile curriculum-l3 -RunId CurriculumL3-20260905-r001 -NumEnvs 32 -Seed 12345 -TorchDevice cuda`이며 새 Run이므로 `--force`/`--resume`을 쓰지 않는다.

## 2026-09-05 02:53 KST 재개 — 현재 작업과 최신 범위

- **03:10 KST r008 종료/판단:** 32workers,100,236,exit0, 약639step/s. 최근5요약 성공평균31%/소유98%/9.76초/누출0, 기회상실 발생22%. 고정seed17423/29471 각100경기30%/22%(r007기준36%/22%)로 개선없음. PT/ONNX/config/log 보존, 단순연장 금지.
- r009 32workers/100,204/exit0, 후반5요약 성공46%(16.7~76.9% 변동)로 연속50% 미달. 고정두seed41%/34%. 방향조준83/85회에도 발신자재접촉30/27회가 주 실패이며 첫수신은44/35, 그중2m 충족41/34로 수신 뒤 판정은 정상이다. 6~16m 패스에 Controlled가 약해 plate 재접촉하는 원인을 검증하기 위해 기술층 권장힘을 Strong으로 한정변경하고 동일r009 고정평가부터 수행한다. 킥물리수치/판정/목표/타이밍은 불변.
- Strong 기술 동일r009 고정두seed56%/38%(합산47%). 발신자재접촉4/6%로 원인은 개선됐지만 패스시도70%/54%가 다음 병목이다. 기회가 plate 준비 전 사라지지 않도록 확정carrier+유리한대상 동안 평면속도0을 유지하는 코드 감속을 한정 비교한다. `Pass Carrier Hold Seconds`로 시간 기록. 성공판정/수신/공/대상위치는 불변이며 고정평가 개선 전 새 학습 없음.
- 전체 carrier hold는 동일r010 고정두seed34%/26%로 악화되어 폐기. 공을 운반해야 하는 준비완료 프레임까지 멈춰 소유가 깨지는 구조다. 감속은 plate cooldown 중에만 적용하고 `CanKick` 프레임에는 Neural 이동+Strong explicit strike를 그대로 허용한다. 동일r010 고정비교 전 학습없음.
- cooldown 전용hold도 seed17423 47%로 Strong-only 56%보다 낮아 hold 코드를 제거했다. 다음 기술은 적합한 실제 flight 동안 지정수신자만 공을 향해 기존 이동힘으로 달리게 한다. 순간이동/강제소유/판정변경 없음. `Pass Receiver Chase Seconds` 기록. 동일r010 고정비교 전 학습없음.
- receiver chase 고정두seed62%/52%로50% 통과. r01132workers100,688 종료, 후반5요약 평균59%이나 한 구간46.43%로 각50% 기준 미달. 나머지53.57~73.81%라 동일설정의 init_path 없는 `curriculum_l2_pass_advised_200k_resume_poca.yaml`로 같은r011을 총200k까지만 resume한다. 기술층 포함 준비단계이며 최종난이도1/대기0/고정300/회귀는 남음.
- r011200,396 종료, 후반5요약 평균64%이나 한10k 요약45.71%로 여전히 엄격검사 미달. 작은 구간표본 변동을 줄이기 위해 환경/보상/optimizer 불변, summary_freq만10k→20k로 바꾼 init없는300k resume을 실행해 더 큰표본5개를 확인한다. 기준완화가 아니며 각50% 유지.
- r011300,364 준비단계 승인: 20k 최근5요약 성공56.25~74.51%(평균66%), 소유100%,6.84초,누출0. 최종난이도1/대기0 고정100은15%로 급락. 최종에서 반복하지 않고 중간난도0.5/대기0/pass-advice1의 r012를 r011에서 초기화해32workers100k로 단계화한다.
- r012100,456 종료: 성공19%,소유91%,14.85초. 첫소유 유리한대상29%,패스시도24%가 원인이라 연장금지. 지정수신자가 carrier보다 상대골문 방향8m 앞을 기존 이동력으로 유지하는 support positioning 기술을 추가하고 개입시간 기록. r011300,364 고정모델/난도0.5에서 먼저 비교한다.
- 첫 support 고정비교 난도0.5는25%, 첫소유 유리한대상15%로 실패. `BallCarrier` 확정 전 지정수신자가 Neural 행동으로 위치를 잃고, 확정 뒤에는 같은 속도의 carrier를 따라잡기 어렵다. 라운드 시작부터 focus passer를 임시 anchor로 8m 지원위치를 유지하고 확정carrier가 생기면 anchor를 교체하도록 수정한다. 다른 조건 불변, 재검증·동일평가 전 학습없음.
- pre-carrier anchor 수정 고정평가: 동일r011/seed41729에서 난도0.5·대기0 95%, 최종난도1·대기0 94%(각100경기,complete). 지원 위치/Strong 조준/수신chase 코드 기술 포함이며 Neural 단독 성과 아님. 순간이동·강제소유 없이 원래 명시적킥/유리대상/실제2m/확정수신 판정. 전체EditMode133/133 및 Build통과. 다음은 r011300,364에서 난도1/대기0/advice1 r013을32workers100k로 초기화하고, 5요약 통과시 새seed 고정300+L0/L1회귀를 수행한다.
- **08:58 KST r013 종료·회귀 판단:** 32workers, 최종100,016, exit0, 약436step/s. 최근5요약 성공93.75%/95.77%/98.23%/96.99%/99.23%(평균96.80%), 소유99.79%, 평균2.10초, 누출0으로 학습 gate를 통과했다. 새seed64213 고정300은 L2 298/300=99.33%, 소유100%,1.63초이며 명시적패스300회·확정수신298회다. 코드 기술은 지원위치407.24초, 수신추격80.58초, 패스추천246회, 실제방향조준301회 기록되어 이 결과는 Neural 단독 성과가 아니다. L0 seed75319 고정300은98%로 통과했다. L1 seed86420 phase2 고정300은 성공58.67%/소유92.67%/유효슛86.33%/방향슛63.33%/13.42초로 성공65%와 방향75% 기준을 미달했다. 따라서 L2는 아직 승인하지 않고 L3도 시작하지 않는다. r013 PT SHA `E58195224DF82C1313532DC5BCDF7DBE4AC32D930DA4F47BFCD76BBF397845B2`; checkpoint/ONNX/config/log/eval은 `Logs/L2-r013-closeout`에 보존한다. 다음은 동일 r013 체크포인트에서 L1 phase2 복습을 새 Run으로 32workers 한정 수행한 뒤 L2/L1/L0 고정평가를 모두 다시 한다.
- **09:58 KST L2 최종 승인·L3 전환:** r013100,016에서 L1 phase2 복습 r018을32workers로100k 실행했고 고정L1이62.33%/방향69.33%로 개선됐으나 미달이었다. 후반 학습 지표가 상승해 같은 optimizer를 총200k까지만 연장했고 최종200,184를 승인 후보로 고정했다. 새 고정평가는 L1 seed86420 300경기 성공71%/소유98%/유효슛92.33%/방향슛78%/9.74초, L2 seed64213 300경기97%/소유100%/2.03초, 추가seed29471 100경기98%, L0 seed75319 300경기97.33%로 모두 통과했다. L2 두seed400경기 합산97.25%이며 명시적패스297/300·확정수신291/300이다. 지원위치522.90초, 수신추격81.86초, 패스추천254회, 실제방향조준299회의 코드 기술이 포함되어 Neural 단독 성과가 아니다. Python38/38, Soccer EditMode133/133, Soccer PlayMode18/18, Base/Attack/Defense/Press/Rule 및 L0/L1/L2 Validate를 통과했고 ProjectSettings SHA/diff는 전후 동일하다. 전체 PlayMode에서 별도 Escape 3개는 실패했으나 Soccer18개는 같은 실행에서도 모두 통과했고 전용 재실행도 정상 종료했다. PT SHA `832EF690250A9A98136112FC1919353CD60C7F39821E4EBB92968408D5986E8F`, ONNX SHA `6D82A3DE105C2DD2FAB9D9542212A1B925E891AB176697DB41B86155564149D8`. r017/r013/r018 및 모든 평가를 보존하고 L3-A 구현으로 전환한다.
- **10:01 KST 정확한 재개점/사용량:** 승인 요약과 99개 파일 SHA manifest는 `Logs/L2-r018-approved-closeout`에 보존했다. 다음은 [L3 계획](../player-curriculum/l3-development-plan.md)의 L3-A 계약, 보상 수치·상한, profile/scene/config를 코드·문서·테스트에 함께 구현하는 단계이며 아직 L3 코드는 변경하지 않았다. 마지막 사용량 조회는 5시간87%/주간29%로 제한 전이지만 사용자 지시의 거의 소진 상태로 판단했다. 자동화를 실제 5시간 초기화 `2026-09-05 12:40:28 KST`보다10분 뒤인 `12:50:28 KST` 단발 재개로 변경했으며 크레딧은 사용하지 않았다.
- 다음 단일 기술변경 r009: `soccer_l2_pass_advice=1`, gate0. 확정 Neural carrier가 킥 가능하고 유리한 동료가 있을 때 Controlled Kick을 권장하고 실제 명시적 plate 접촉 방향을 가장 가까운 유리한 동료로 조준한다. 성공판정/2m/확정수신/힘/보상은 불변. Human/Rule/fallback 제외. 코드로 가르친 기술이며 세 개 Advice 태그로 모든 개입을 기록한다. 고정r008에서 기술층 효과평가 후 근거가 있을 때만32workers100k r009.
- 첫 기술층 고정평가 seed17423/100은 성공32%, 추천66/요청조준16회였으나 `Pass Direction Aimed` 0회였다. 원인은 plate 접촉에서 strike를 소비한 뒤 `CanKick` false라 실제방향 보정이 비활성화된 수명주기 오류다. 학습을 시작하지 않고 실제 명시적 strike 경로에서는 carrier/Neural/유리한대상만 다시 검사하도록 수정한 뒤 재검증·동일평가한다. 실패결과는 보존.

- 사용자 승인: L2 정식50%/고정평가/회귀 통과 후 L3까지 개발·학습·검증한다. 오전5시 조건은 해제. 신규 학습32workers, 원본 이력 보존. 시작 시 실제 Trainer/Player/평가/Unity 없음. 아래16workers 및 L3금지는 과거 단계 기록이다.
- r008 준비: 확정carrier의 첫 유효 고정수신자를 대상으로 자기전진 때문에 자격을 잃은 첫 사건에만 -0.15/라운드 불이익. 첫킥/소유공백/대상비활성/무효수치 후에는 영구 비활성. 정지 보상/자동감속/조준/킥 추가 없음. 실제 성공판정·기존 보상액·379관측/[3,3,3,3] 유지.
- 수정 전 소스·설정·문서와 승인모델SHA는 `Logs/L2-opportunity-loss-before-source`, `Logs/L2-opportunity-loss-before-sha.json`에 보존했다. Python38/38 통과. 새 YAML `curriculum_l2_opportunity_loss_poca.yaml`: r007100,436 초기화, 32workers, 최초100k 종료/50k 저장, 나머지조건 동일. 별도 새Player에서 한정진단 후 복수seed L2와 L1 회귀를 확인하며 단순연장하지 않는다.
- 새 heartbeat `soccer-l2-l3-32`를30분 간격 ACTIVE로 만들었다. 매 실행/주요경계에서 사용량을 확인하고90% 이상이면 실제 resetsAt+10분 단발재개로 변경한다. 시작 조회5시간1%/주간0%, 크레딧 미사용. L3 승인·회귀·보존까지 끝나거나 사용자중단시 PAUSED.
- 정확한 다음 단계: 전체 Soccer EditMode/PlayMode → 설정SHA복원·비파괴Validate → WindowsBuild·Player보존 → 신규r00832workers100k → inspect/모든ONNX·SHA/고정L2복수seed 및 L1회귀.

## 2026-09-05 최신 사용자 정정 — 이전 인계 이력

- **이번 후속 개발 최종 인계(아래 진행 중 표기는 이력):** 확정소유 진단 및 v2 L0/L1회귀 모두 정상 종료. r007 L2 gate0 31%/gate1 28%(각100경기), L0 299/300=99.67%/1.385초. 동일Player/protocol2의 L1 승인r017 80%/방향슛88.33%, 후보r007 64.33%/방향슛75.67%(각300경기). 후보는 L2 50%와 L1 65%에 미달하므로 미승인, L3 없음. 진단구현/113EditMode/18PlayMode/38Python/Validate/Build/설정검증을 완료했고 새학습/r008/보상변경은 하지 않았다.
- **다음 구체적 작업:** [확정소유 감사](../player-curriculum/l2-opportunity-audit-20260905.md)를 바탕으로 소유 후 자기전진으로 유효한 고정수신자의 기회를 잃는 사건과 L1기술유지를 함께 다루는 보상을 설계한다. 소유소실·대상움직임과 자기전진의 원인을 분리하고 라운드1회상한/정지파밍방지/레슨격리/회귀테스트를 먼저 확정한다. 관련코드/보상표/테스트/전체Unity검증·빌드를 통과한 뒤에만16workers 한정학습을 시작한다. 기존r007 맹목적연장 금지.
- **보존/예약:** 새 결과5개와 Player로그·검증XML·소스·build-info·host기록은 `Logs/L2-opportunity-audit-closeout` 및 SHA목록으로 보존한다. 승인r017/r007 가중치는 원본 유지. 사용량 마지막조회5시간98%/주간96%, 실제제한 표시는 없고 크레딧미사용. 현재 구체적 감사단계를 마무리하고 남은사용량을 고려해 기존예약을 초기화예정(05:33:56 KST) 이후 **9월5일05:35경 후속개발1회**로 갱신했다(API ACTIVE, 만료05:36:08 KST). 이는 사용량제한으로 실패했다는 주장이 아니며 무기한대기예약이 아니다. 실제재개 시 최신사용량을 확인하고 주간까지 제한됐으면 가장늦은해제 이후로 조정한다. 예약은 위 L2 보상개발을 이어가며 정식50%/회귀 통과 전 L3 진입을 금지한다.

- **02:25 KST 감사 결과/회귀 중:** 확정소유 진단 두평가 complete/각100경기/exit0, gate0성공31%/gate1성공28%로 미통과. gate1에서75경기에는 유리한대상이 있었지만38경기는 첫킥 전 기회를 잃었다. 최초상실까지0.459초/최초carrier전진4.301m, 사유거리부족19/38·골문이득부족20/38·거리초과0/38(중복가능). 후반거리초과 표본과 최초원인을 구분한다. [감사 기록](../player-curriculum/l2-opportunity-audit-20260905.md)에 근거와 보상재설계 방향을 저장했다. 자동감속/조준/킥/새보상은 아직 없음. 세션7182는 동일진단Player/protocol2/seed86420의 승인r017 L1phase2 300경기 → r007 L1phase2 300경기 순차 회귀다. 출력 `Logs/L1-r017-v2-opportunity-player-seed86420-300.json`, `Logs/L2-r007-v2-l1-opportunity-player-seed86420-300.json`. Trainer/r008/L3 없음. 진단 Player는 `Builds/SoccerTraining-L2-opportunity-audit`에 별도 보존. 사용량5시간91%/주간94%이지만 실제제한은 없으며 크레딧미사용.

- **02:21 KST 검증 완료/실제 평가:** 확정소유 진단은 EditMode113/113, PlayMode18/18, Python38/38, 비파괴Validate 및 WindowsBuild(exit0,오류0/기존warning485) 통과. ProjectSettings 원본 SHA 일치/diff 없음. 소스·평가기·build-info는 `Logs/L2-opportunity-audit-source`와 SHA목록에 보존했다. Runtime DLL SHA `95BF3796267E7668010BCFDF7778618AD4D96DB681B56A7B486511D646A35EF9`, Curriculum DLL `6A4D40F949F595CC1E21FD4A75BCB5141786CA4F5F00FB67C6F2D3472B90CB76`. 세션96453은 r007100,436/seed17423/gate0·1/각100경기 순차 고정평가이며 Trainer가 아니다. 출력 `Logs/L2-r007-confirmed-opportunity-gate{0,1}-seed17423-100.json`. 종료 후 first-loss 원인과 확정소유 표본을 검토한다. `inspect_soccer_training.py`의 r007 재점검/현재host는 `Logs/L2-opportunity-audit-host-and-r007.json`에 저장했다.

- **02:20 KST 후속 개발:** 실제 Trainer/Player/Unity 없음, 사용량5시간77%/주간92% 사용(실제 제한 아님) 확인 후 확정소유자 진단을 구현했다. 첫 소유자 identity가 유지되는 첫 킥 전 FixedUpdate만 표본으로 삼아 지정수신자 너무가까움/너무멂/골문이득부족/유효대상/예상통로와 최초 기회상실 시각·전진거리를 기록한다. 성공 판정·보상·행동·물리·관측은 변경하지 않았다. 소유소실은 별도분모다. `Logs/L2-opportunity-audit-before-source` 원본 및 기존 `Builds/SoccerTraining-L2-pass-gate` Player를 보존했다. 지금은 `L2-opportunity-audit-EditMode` Unity 검증 중이며 새 Trainer/r008 없음. 다음은 PlayMode/설정복원/Validate/Build 후 r007 고정모델 gate0·1 각100경기로 원인을 검토한다. L3 금지·50% 필수 유지.

- 사용자는 “l2를 완성하지 마라. 성공률을50% 이상 기록해야 통과”라고 정정했다. 현재 결과를 완료로 승인하지 않으며 **30% 진입/28% 근접예외를 폐기**한다. L2 승인 전 L3 개발·빌드·학습을 시작하지 않는다.
- r007 protocol2/방향gate0/대기1/쉬운배치의 seed17423·29471 각100경기는36%·22%, 합산58/200=29%다. 두 번째 평가는 정상 완료됐으며 새 Trainer는 시작하지 않았다. 이 수치는 미통과이며 보조 없는 최종 평가도 아니다.
- `Logs/L2-r007-provisional-L3-entry-20260905.json`의 조건부진입은 **철회된 과거 판단**이다. 원본은 삭제·덮어쓰지 않는다. 새 판정은 `Logs/L2-r007-50-percent-required-20260905.json`에 별도로 저장한다. `Tools/assess_soccer_l3_entry.py`는50% 사전검사만 하며 단독 L3 승인이나 근접예외를 허용하지 않는다.
- 정식 `inspect_soccer_training.py`의 최근5요약 각각 성공50%/소유85%/15초, 최종난이도1/대기0/방향gate0, 독립 고정평가300경기 이상 및 L0/L1 회귀를 유지한다. 우연한접촉 성공처리, 실제 보상·물리·관측·Action 변경은 없다.
- 정확한 다음 작업: 실제프로세스 확인 → L2 최초확정소유 이후 패스 기회 상실/방향 준비/보상구조 및 L1기술유지 감사 → 관련검증 후16workers 한정학습. 근거 없는 버전 증가/자동연장/L3우회 없음. L3 계획은 보류자료다. 기존 유한 후속예약도 이 L2 전용 지시를 따른다.
- 정정 검증 완료: Python38/38 통과(29%/49% 예외 차단,50% 사전검사 경계 포함), 새 판정JSON에 성공0.29/미통과/예외false 기록. 관련문서 링크 및 diff검사 정상, ProjectSettings diff 없음. 실제 Soccer Trainer/Player/평가/Unity 실행 없음. L1r017/r007 PT SHA는 각각 `82842DAC039282288D72C64D290021C4ED0D8EDE6D6EB839B405141395637069` / `773F55CCD11C7941D38572283AB11528A02C82130645992444FA22942091F4D8`로 불변. 이번 수정은 Python판정/문서뿐이며 Unity 재빌드·새 학습은 하지 않았다. 기존 예약id `soccer-l1-l2`를 L2 50% 전용지침으로 갱신했고 기존 만료시각9월5일02:13:40 KST를 유지했다. 무기한 예약 추가 없음.

## 2026-09-05 실제 재개 — 이전 인계 이력

- **01:44 KST 인계 / 현재 최종 상태:** r007100,436 학습 및 모든 평가 종료(exit0), 01:42 실제 Trainer/Player/Unity 없음. v2의 seed17423/각100경기는 r006 gate0=27%,gate1=26%; r007 gate0=36%,gate1 반복a=28%/b=35%. 반복 Action trace SHA가 달라 전체 실행의 bitwise 재현은 실패했다. 세 난수원 seed 고정은 수정·검증했으나 Unity/실행 순서 등을 포함한 나머지 변동의 정확한 원인은 미확정이다. **개선 가능성은 있으나 작은 차이·한 번의평가만으로 확정하지 않으며 L2 50% 미달/회귀미달로 미승인.** 구평가도 삭제하지 않고 sampling seed 미고정 이력으로 보존한다. 새 평가기/protocol2 소스·검사는 `Logs/L2-evaluation-v2-source`, 결과/전체Run/모델/config/log/SHA는 r007 snapshot 및 closeout에 보존했다. Python32/32, EditMode105/105, PlayMode18/18, 비파괴Validate/WindowsBuild통과, 설정diff없음. L1r017 원본 SHA 불변. r008/자동 추가학습 없음.
- **정확한 다음 작업:** 프로세스 확인 → v2 반복 변동과 L1 기준선 검토 → 첫 **확정 carrier**의 패스 기회가 사라지는 시점(대상이 너무 가까움/뒤로 밀림/너무 멂, 이동·회전)을 분리 진단한다. 현재 near-ball proxy는 actual carrier가 아니므로 보상 결정에 직접 쓰지 않는다. 그 근거로 소유 후 기회 유지·방향 준비 보상을 재설계하고, L1 기술 유지(복습/혼합 등의 공통계약 유지 방안)를 검토한다. 수신 성공 기준 추가 완화·단순 Run 증가·맹목적 r007 연장은 하지 않는다. 모든 최종 승급에는 대기/통로보조 해제·원래배치·독립300경기·L0/L1 회귀가 남아 있다.
- **예약/사용량:** 01:42 조회5시간62%/주간90% 사용, 실제 제한 없음(중단원인을 사용량 제한이라고 주장하지 않는다). 초기화 KST9월5일05:33:56/9월7일13:52:31, 크레딧미사용. 기존 예약은 무기한 반복이 아니라 **30분 간격에 만료시각9월5일02:13:40 KST를 둔 최대1회 미래 후속 개발**로 갱신했다(01:43:40 KST 갱신/API ACTIVE확인). 이 실행은 대기 반복이 아니라 위 구체적 원인 감사·개발을 이어간다. 실제 제한이면 최신 resetsAt을 다시 조회해 제한된창의 가장늦은해제 이후 단발 재개만 조정한다. 앱/PC가 실행되어야 하며 이미 도구까지 제한된 후의 예약변경을 보장하지 않는다. 아래 이전 PAUSED/진행중 문구는 이력이다.

- **v2 유한 검증 실행:** 세션14136은 r007100,436 gate1/seed17423/100경기 반복a·b, r006100,060 gate1·0, r007gate0의 총5회 순차 평가다. 첫a는 complete/100경기/성공28%/protocol2/seed적용true. 같은출력 재사용 CLI는 Player 실행 전에 오류로 막고 기존JSON SHA 불변을 확인했다. 구평가 동시Player 없음. 구방식 동일Player L1기준선은 r00668.67%, 승인r01776%로 완료·보존했으며 구평가임을 유지한다. r007의 준비proxy에서는 킥mask 근접3562결정 중 유리한대상4.24%/전진행동87.76%였다. 실제carrier 확정/에피소드 비율이 아니며 긴 실패구간이 표본을 지배할 수 있으므로 이 수치만으로 보상을 바꾸지 않는다. 수정평가 반복과 비교 완료 후 다음 설계를 결정한다.

- **평가기 v2 구현:** `soccer_evaluation_seeding.py`로 Python/NumPy/Torch seed를 모델 로딩 뒤 초기화하고 평가JSON에 protocol2/샘플링seed/적용여부/Action trace SHA를 기록한다. 기존 출력 파일이 있으면 덮어쓰지 않고 오류로 막는다. Python32/32 통과. 이미 실행된 구평가 프로세스는 구방식 그대로 마무리하며 원본을 유지한다. 종료 후 같은r007/Player/seed17423/100경기를 두 번 실행해 trace와 지표 재현성을 확인하고, r006/r007 gate0/1 비교를 새 파일로 다시 수행한다. 학습/보상/Unity 코드 추가 변경은 없다.

- **평가 재현성 감사 — 아래 수치의 해석을 제한:** 설치된 `TorchPolicy`/상위`Policy`는 전달seed를 저장할 뿐 Torch 샘플링 난수를 초기화하지 않는다. 기존 `evaluate_soccer_policy.py`에도 Python/NumPy/Torch seed 초기화가 없었다. 따라서 기존 평가는 **가중치 고정/frozen 및 실제경기 증거는 맞지만 환경seed만 고정된 확률적 평가**이며 동일seed 정밀 paired 비교나 작은 수치 차이의 원인 단정은 불가하다. 기존 원본/승인 L1r017/모든평가를 보존한다. 현재 이미 시작한 순차 평가3회가 끝나면 평가기 sampling seed 및 action trace SHA를 추가하고 재현성 반복 검사와 핵심 비교를 다시 한다. 이 전에는 추가 학습/보상 변경/r008 없음. L1 약화도 동일Player+수정된 평가기로 재확인한다.

- **r007 회귀 종료/원인 감사:** L0 seed75319/300경기 성공298/300=99.33%, L1 phase2/seed86420/300경기 성공188/300=62.67%, 소유99%,운반93%,유효슛90.67%,방향슛70.67%,11.15초. L1 성공65%/방향슛75% 기준 미달로 후보 승인 불가. L1r017 원본 SHA 불변, r007/평가 모두 보존하며 r008 없음. 빌드 효과와 모델 약화를 구분하려고 동일 새Player에서 r006 및 승인r017의 L1 각300경기 기준선을 확인한다. 그 전에 r007/seed17423/gate1/100경기 읽기 전용 `--preparation-diagnostics`를 실행한다. 이는 팀소유+plate준비+공1.8m+대기선수제외 관측의 **근접 proxy**이지 실제 carrier 확정이 아니다. 킥mask된 근접 상황에서 대상 존재/이동·회전 선택을 기록하며 행동·보상·난수·물리는 바꾸지 않는다. Python28/28 통과, 기본진단off. 프로세스3회 순차 유한 평가이며 Trainer 없음. 출력 `Logs/L2-r007-preparation-proxy-seed17423-100.json`, `Logs/L2-r006-l1-current-player-seed86420-300.json`, `Logs/L1-r017-pass-gate-player-regression-seed86420-300.json`.

- **r007 네 조건 고정 평가 완료:** 각100경기/complete/exit0/SHA 불변. gate1은 seed17423 **27→25%**, seed29471 **16→27%**(합산21.5→26%); gate0은 **32→29%**, **20→23%**로 합산26→26%다. 따라서 일관된 개선이나 보조 제거 전이의 증거가 없고 r007 자동 연장은 하지 않는다. gate1의 적합시도는각36%, 수신27/28%, 성공25/27%로 여전히 대부분의 라운드가 발신 기회를 못 만든다. 첫 실제 킥 통로72/76=94.74%이나 이는 코드 gate와 결합한 결과다. 다음으로 같은 r007의 L0 seed75319/300경기, L1 phase2/seed86420/300경기 회귀를 순차 실행한다. 출력 `Logs/L2-r007-l0-regression-seed75319-300.json`, `Logs/L2-r007-l1-regression-seed86420-300.json`. 후보 보존과 진단 목적이며 아직 승급 없음. 그 뒤 버전 증가보다 소유 안정화 중 이동/회전 및 패스 준비 신호의 구조를 감사한다.

- **r007 100k 정상 종료 / 평가 중:** 세션70055 exit0, 최종100,436 PT SHA `773F55CCD11C7941D38572283AB11528A02C82130645992444FA22942091F4D8`, ONNX4개 checker통과, Trainer/Player 종료 확인. `Logs/L2-r007-100k-diagnostic.json` 성공 기준 미달/누출0/약641step/s, 학습 RAM58%/여유13GiB/GPU53°C. 49,939/99,924/100,436 checkpoint 및 전체Run/configuration/source-config/build-info/provenance/run_logs/ONNX를 `Logs/L2-r007-100k-snapshot`, SHA목록을 `Logs/L2-r007-100k-manifest.csv`에 보존했다. 현재 **평가 세션72346**은 같은100,436/seed17423·29471/gate1·0 각각100경기 총4회 순차 평가한다. 출력 `Logs/L2-r007-pass-gate-{1,0}-seed{17423,29471}-100.json`. 이 평가는 추가 학습이 아니다. 종료 후 초기r006과 비교하고 개선 근거 없는 연장/다음Run을 만들지 않는다. 아직 L2 승인·무작위300경기·L0/L1 후보 회귀 미완료. 예약PAUSED·단발.

- **r007 실제 실행:** `CurriculumL2-20260905-r007`, 세션70055, Trainer1+Python workers16/Player16 실측. step0→10,000, r006100,060 초기화/난이도0/대기1/pass-gate1/max100k/50k 저장/CUDA 확인. YAML parser 및 `L2-r007-Validate.log`9프로필/8학습형 정상 exit0, 소스 SHA는 검증된 pass-gate snapshot과 일치하며 설정diff 없음. source-config/build-info를 Run에 보존했다. 현재는 이 Trainer만 모니터링하고100k 종료 뒤 같은 두seed 고정 gate1/0 비교한다. 예약은 PAUSED·단발 그대로이며 중복 학습 없음.

- **r007 한정 진단 준비:** 두seed 각각100경기 결과 gate0 32%/20%, gate1 27%/16%. 코드만으로 성공 개선은 없지만 첫 실제 킥은60/60 통로 일치, 차단은8141/8202=99.26%였다. 이 제약 아래 신경망이 이동·회전으로 기회를 만들 수 있는지 별도100k 한 번만 검증한다. `curriculum_l2_pass_gate_poca.yaml` r006100,060 초기화/16workers/100k 종료/50k 저장/다른 보상·판정 불변. 종료 뒤 동일seed gate1 초기27%/16%와 비교 및 gate0 전이 평가, 개선 없으면 자동 연장하지 않는다. 아직 Trainer 시작 전이며 YAML parser·비파괴 Validate 후 실행한다.

- **새 Player 고정 비교:** pass-gate Windows Build 정상(UTC2026-09-04T16:09:12.9716666Z, 오류0/기존warning485,144685242bytes), 설정diff 없음. Runtime SHA `120C1B036788097CB58FAF30543A9CE9AF86D1734CCBB109B190F93BA4D8CE82`, Curriculum SHA `92AFA42A07226A36199EA7F4864A1E1B437E1DCDD9C366AFB843B1DDD20F6DD8`, `Builds/SoccerTraining-L2-pass-gate` 보존. 동일r006/seed17423/100경기 gate0 성공32%, gate1 성공27%. gate1 첫 실제 킥35회 모두 통로에 있지만 킥기회3804 중3768(99.05%) 차단, 적합시도35%/수신29%/10.20초. 즉 코드만으로 인계 성공 향상 없음. 이전 preparation24%와 새 gate0의32% 차이가 있어 구빌드 대비 향상으로 주장하지 않는다. 새seed29471의 gate0/1 각각100경기를 순차 실행해 효과를 추가 확인한다. 아직 r007 없음. 각 평가 frozen/optimizer없음/PT SHA 불변.

- **검증 진행:** pass-gate 변경은 Python26/26, 전체 Soccer EditMode105/105, 대상 PlayMode6/6 및 전체 Soccer PlayMode18/18 통과. `L2-pass-gate-Validate.log` 정상 exit0. 테스트가 제거한 Standalone `SENTIS_ANALYTICS_ENABLED`를 복구했다. `L2-pass-gate-Build.log` Windows Build 진행 중이며 기존 preparation Player는 별도 보존됐다. 새 소스는 `Logs/L2-pass-gate-source`에 복사했다. 빌드 완료 후 동일 r006100,060/seed17423/100경기 gate0/1 비교를 실행한다. Trainer/r007 없음. 제한 환경 Unity 시작은 캐시 접근 오류로 실패해, 종료 확인 후 승인된 일반 사용자 실행으로 전체 테스트를 통과했다. 기존 fallback Scene의 trailing whitespace는 사용자/기존 변경이라 건드리지 않았다.

- **현재 구현/다음 검증:** r006 고정100경기 complete/exit0/SHA 불변, 인계24%로 초기24%와 동일. 시도36%/수신26%, 유리한 동료가 있는 결정98.65%에서 킥, 첫소유→킥0.373초. `Logs/L2-r006-100k-snapshot`에 전체Run/설정/로그/평가 복사, `Logs/L2-r006-100k-manifest.csv` SHA 목록 보존. r006 자동 연장 없음. 다음 실험의 기본0/최종난이도1 강제0인 `soccer_l2_pass_aim_gate`를 구현했다. 예상 통로가 없는 킥만 mask하며 자동 조준/킥/물리/성공 기준/보상액 변경 없음. Agent/Env/Controller/평가/승급검사/문서·테스트를 갱신했다. 다음은 전체 Unity EditMode/PlayMode/define복원/Validate/Build, 동일r006 gate0/1 고정 비교다. 아직 r007 없음. 예약은 단발·PAUSED이며 직접 진행한다.

- **r006 100k 종료/평가 중:** 세션85357 exit0, 최종100,060 PT SHA `A38D173F20A01EA0DABF55B22BA432EA1207FA43BC23DD688C70AC2E3A0D554F`. 모든 ONNX checker 통과, Trainer/Player 종료 확인 후 평가만 실행했다. `Logs/L2-r006-100k-diagnostic.json`: 최근5요약 평균 성공28.42%/소유99.29%/시도38.48%/수신28.42%/10.80초, 누출0. 50k 저장 및 약634step/s, 학습 중 RAM61%/GPU57°C. 현재 평가 세션39252, `Logs/L2-r006-preparation-seed17423-100.json`, 동일seed17423/난이도0/대기1/100경기. 완료 전 자동 연장 없음. 아래 학습 중 문장은 종료된 이력이다.

- `CurriculumL2-20260905-r006` 학습을 실제 시작했다. 세션85357, r005200,256 초기화/16workers/CUDA/난이도0/대기1/10만 자동 종료/5만 저장. 16환경 연결 및 step0→10k→20k 증가 확인. preparation 소스 SHA가 검증 스냅샷과 일치하며 기존 EditMode104/104, PlayMode18/18, Validate/Windows Build 성공, ProjectSettings diff 없음. 새 보상/물리 수정 없이 검증된 preparation YAML을 사용했다.
- 현재 작업은 이 Trainer 감시→100k 종료/모델·ONNX·config·로그·SHA 보존→동일seed17423/100경기 고정 평가다. 기존 preparation 기준선24%와 비교 전에는 연장/새 버전을 만들지 않는다. L2 미승인, L0/L1 후보 회귀는 후속 단계다.
- 예약 정책 변경: 기존 `soccer-l1-l2` 10분 무기한 반복을 단발(COUNT1)·PAUSED로 변경했다. 활성 작업 중에는 유한 Trainer를 직접 감시하고 중복 wakeup을 예약하지 않는다. 필요 시 예상 종료 또는 제한 해제 시각 뒤 단발 재개만 예약한다. 계정 조회 당시5시간8%/주간81% 사용으로 제한 없음. 조회된 초기화는 KST 9월5일05:33:56 / 9월7일13:52:31. 실제 제한 시 다시 조회하여 제한된 창 중 가장 늦은 resetsAt 이후를 사용한다. 이미 제한되어 도구 실행도 막히면 그때 예약을 바꿀 수 있다는 보장은 없으므로 사전 인계/유한 종료를 유지한다. 크레딧 미사용. 자동화 중지는 사용자 학습 중단 요청이 아니다.

## 2026-09-04 작업 재개 — 최우선

- **r006 시작 전 고정 비교 — 최신:** 새 preparation Player에서 동일 r005200,256/seed17423/100경기/난이도0/대기1 평가 `Logs/L2-r005-preparation-seed17423-100.json` complete/exit0/frozen/optimizer없음/PT SHA 불변. 인계20→24%, 시도28→33%, 접촉23→25%, 에피소드13.29→10.77초. 첫 소유 대상70/98=71.43%→78/98=79.59%, 첫 킥 준비 대상67/88=76.14%→71/79=89.87%, 통로30/88=34.09%→32/79=40.51%, 첫 실제 킥 대상57/93=61.29%→70/93=75.27%, 통로28/93=30.11%→33/93=35.48%. 준비 대상78경기, 개선 보상 사건32회/합계0.03349로 신호가 존재하고 공유 cap을 넘지 않았다. 이는 환경 변경 효과이며 학습 향상으로 세지 않는다. 이 근거로 `curriculum_l2_preparation_poca.yaml`을 추가해 r005200,256에서 r006 최초100k/16workers/50k 저장 한정 진단을 준비한다. 기존 r005/L1r017/Player는 보존 완료.

- **현재 구현/정확한 재개 지점 — 최우선:** r005 첫 기회 진단에 따라 준비 단계 지정 수신자 시작을14→15.5m로 변경했다. 최초 확정 소유 순간의 지정 수신자를 고정하고 예상 킥 방향 품질을 무보상 기준선으로 잡은 뒤, 첫 킥 전 동일 최초 carrier가 실제 킥 준비 상태에서 만든 최고 품질 증가분만 기존 `CurriculumPassDirection` 팀/라운드cap0.1 안에서 지급한다. 실제 킥 방향 보상과 cap을 공유하고 대상 교체·기준선 재설정은 없다. `Pass Preparation Target Available/Best Direction Quality/Reward Total/Events`를 추가했다. 자동 조준·킥·소유·성공 판정은 불변. 코드/보상표/L2 README/계약/테스트를 갱신했으며 다음은 Python·전체 Soccer EditMode/PlayMode·Standalone define 복원·Validate·Windows Build·설정 diff 확인이다. 그 뒤 동일 r005200,256/seed17423/100경기로 환경 효과와 새 보상 계측을 고정 비교한다. 아직 r006/Trainer 없음.

- **첫 기회 진단 결과 — 최우선:** `Logs/L2-r005-first-opportunity-seed17423-100.json` complete/100경기/exit0/동일PT SHA 불변, 인계20% 재현. 첫 확정 소유98경기/평균1.210초, 대상70/98=71.43%, 예상 통로41/98=41.84%. 첫 킥 준비88경기/1.323초, 대상67/88=76.14%, 예상 통로30/88=34.09%. 첫 실제 킥93경기/1.919초, 대상57/93=61.29%, 실제 통로28/93=30.11%, 첫 소유 후0.694초, 동일 최초 carrier79/93=84.95%. 첫 준비 샘플이 실제 킥 전에 있었던 경기는88%. 전체 적합 패스28회/인계20회. **기회는 대체로 존재하지만 소유 후 첫 킥까지 대상 존재와 정렬이 감소**하며, 수신 기준 완화나 단순 킥 빈도 증가는 해결책이 아니다.
- **다음 설계:** 대기 보조의 지정 수신자 시작 거리를14→15.5m로 늘려 소유 안정화 중3m 최소 거리 아래로 사라지는 기회를 줄인다. 최초 소유 때 지정한 유리한 수신자와의 현재 예상 킥 방향 품질을 기준선으로 잡고, 첫 킥 전 그 품질의 **최고 기록 증가분**만 기존 `CurriculumPassDirection` cap0.1 안에서 보상한다. 처음부터 정렬된 자세/정지/악화/원상복귀/대상 교체로 보상을 반복하지 않으며 실제 킥 방향 보상과 cap을 공유한다. 자동 조준·킥·소유 강제·성공 기준 변경은 없다. 기존 진단 Player는 `Builds/SoccerTraining-L2-r005-first-opportunity`, 소스/평가는 `Logs/L2-first-opportunity-source`에 보존했다. 구현 후 문서·테스트·Build·기존모델 고정 비교부터 진행하며 아직 r006 없음.

- **현재 고정 평가 — 최신:** 첫 기회 진단 Player Build 성공(UTC `2026-09-04T12:17:33.7985598Z`, 오류0/기존warning485,144682170bytes). Runtime SHA `D79BA4AE66D7F9C5EDB26237144F9E4B5F7B1F02029556706A1D5F4F6ABA5442` 불변, 진단 포함 Curriculum SHA `188C5ED5EAF77F5C7EE2B4713290CD3CCDB940A421A0ACC5FF11AF920533A553`. EditMode102/102,PlayMode18/18,Python24/24,Build 정상·설정 diff 없음. 동일 r005200,256 PT SHA `A2FDAA...58806`, seed17423/100경기/대기1/난이도0 고정 평가 시작, 출력 `Logs/L2-r005-first-opportunity-seed17423-100.json`. 종료 후 성능20% 재현 여부와 First Possession/Kick Ready/Strike 통계를 분석한다. Trainer/r006 없음, L1r017 SHA 불변.

- **검증 진행 — 최신:** 첫 기회 진단의 전체 Soccer EditMode102/102,Python24/24 통과. `Logs/L2-first-opportunity-PlayMode.*` 실행 중이다. 이후 Standalone define 복원 및 `Logs/L2-first-opportunity-Build.log` Windows Build/프로필 검증, 동일 모델 고정100경기 평가로 이어간다. Trainer/r006은 없으며 이전 Player·모델은 보존. 진단은 보상/Action/성공 판정을 바꾸지 않았다.

- **최신 구현/다음 작업:** `First Possession`, `First Kick Ready`(FixedUpdate), `First Strike`의 대상/방향 통로/최선 각도/거리/라운드 시간, 첫 소유→첫 킥 지연과 동일 선수 여부를 기록하는 읽기 전용 진단을 추가했다. 보상·성공 판정·행동·난수는 불변. 이전 Player는 `Builds/SoccerTraining-L2-r005-direction`, 변경 전 Controller는 `Logs/L2-first-opportunity-source/SoccerCurriculumController.L2.before.cs`에 보존. 현재 Trainer/Player/Unity 없음 확인, r006 없음. 다음은 전체 Soccer EditMode/PlayMode와 Player Build 검증 후 동일 r005200,256/seed17423/100경기/대기1/난이도0으로 `Logs/L2-r005-first-opportunity-seed17423-100.json` 고정 평가한다. 첫 순간과 전체 킥 통계의 차이를 확인하기 전 새 보상/기술 보조를 결정하지 않는다.

- **정확한 다음 재개 지점 — 최우선:** r005200,256 고정 평가 `Logs/L2-r005-200k-direction-seed17423-100.json` complete/100경기/exit0/SHA 불변. 성공 **20%**, 시도22%, 접촉21%, 소유99%,11.602초. 동일 Player 초기12% →100k21% →200k20%로 초기 대비 신호는 있으나 추가100k 향상은 확인되지 않았다. **더 연장하거나 r006을 만들지 않았다.** 다음은 모델/보상/성공 기준을 고정한 채 **첫 확정 소유·첫 킥의 유리한 대상 존재와 예상/실제 방향**을 분리하는 읽기 전용 진단이다. 전체 킥 통계는 실패 후 재추격 킥까지 섞이므로 첫 기회가 언제 사라지는지 판단할 수 없다. 단순 기준 완화·작은 수치 조정 뒤 버전 증가를 하지 않는다.
- **병목 근거:**200k 평가의 적합한 실제 패스22회 중 인계20회. 공 이동2m 미달은1경기뿐이므로 수신 성공 기준을 더 낮출 근거가 없다. 실제 킥237회 중 유리한 대상55회/통로22회/평균 최선각도41.95도, 적합한 패스 목표거리4.252m. 킥 가능한 결정268회 중 유리한 대상60회이며 이 중57회(95%) 킥을 택했다. 무조건 더 자주 킥하도록 장려하기보다 **대상 기회 유지·킥 전 방향 맞추기**를 조사한다. 방향 보상은 실제 킥에서만 신호를 주므로 킥 전에 기회를 잃는 문제를 직접 해결하지 못할 수 있다(가설). 수신 기술·보조 추가나 보상 재설계는 이 진단을 근거로 결정한다.
- **보존/검증:** r005100k/200k 각각 checkpoint/ONNX/configuration/source·재개 YAML/manifest/진단/고정 평가/전체run_logs snapshot 완료.200k PT SHA `A2FDAA6333A3A40A5ABDAA11F1FD8C9694B968BFC56F0D67E160D146C5258806` 불변. 이전r004/L1r017과 이전 Player 보존. 이번 변경의 검증은 EditMode102/102,PlayMode18/18,Python24/24,비파괴 Validate/Windows Build 정상,설정 diff 없음. 아직 L2 승인·무보조·원래 배치 최종300경기·L0/L1 회귀는 미완료. 자동 점검을 유지한다. 아래 실행 중 문구는 종료된 이력이다.

- **최신 종료/현재 평가:** r005는 **200,256에서 정상 종료(exit0)**, 실제 Trainer/Player 종료 확인. 최종PT SHA `A2FDAA6333A3A40A5ABDAA11F1FD8C9694B968BFC56F0D67E160D146C5258806`, root ONNX checker 통과. `Logs/L2-r005-200k-diagnostic.json` 최근5요약 성공21.429%/소유98.416%/시도33.293%/접촉25.298%/10.893초/방향품질0.4259, 보상 누출0. 모델·configuration·source/재개 YAML·SHA manifest·진단·build-info·**run_logs 전체**를 `Logs/L2-r005-200k-snapshot`에 보존했다. 현재 고정200,256/seed17423/100경기/대기1/난이도0 평가 세션 **85422**, 출력 `Logs/L2-r005-200k-direction-seed17423-100.json`. 완료 후100k의21%/시도34% 및 초기12%/시도21%와 비교해 다음 작업을 결정한다. 추가 학습 자동 시작 없음, r006 없음, L2 미승인. 아래 재개 중 기록은 완료된 이력이다.

- **현재 실행 — 최우선:** r005 고정100경기 `Logs/L2-r005-direction-seed17423-100.json` complete/exit0/SHA 불변: 인계 **21%**, 시도34%, 접촉23%, 소유97%,11.113초. 동일 Player 초기12%/시도21%보다 양의 변화지만 표본100경기이며 아직50% 승급 미달이다. 보상과 buffer를 함께 바꾼 실험이라 각 요소 효과를 분리해 주장하지 않는다. **같은 r005를 총200k까지** 세션 **70058**로 재개했다(init_path 없는 방향 resume YAML/`--resume`/16workers/대기1/난이도0). 100k root checkpoint·ONNX·설정·진단·평가·**run_logs 전체** snapshot 완료. 다음은 이 세션의100,040 재개/스텝 증가/150k checkpoint/200k 자동 종료를 확인하고, `Logs/L2-r005-200k-diagnostic.json` 및 새 최종 PT의 `Logs/L2-r005-200k-direction-seed17423-100.json` 고정 평가를 저장한다. 중복 실행 금지, 추가 보상 변경·r006 생성 없음. L2 미승인, L1 원본 유지.

- **종료/평가 — 최신:** r005는 **100,040 정상 종료(exit0)**, Trainer/Player 종료 확인. 최종PT SHA `CC35D24C1704CE0470B40BCF41DC0985F54DE5FE48F40F1C6ECFC4D6BEC603B8`, root ONNX checker 통과. `Logs/L2-r005-first-diagnostic.json` 최근5요약 성공17.053%/소유97.114%/시도35.044%/접촉24.605%/11.544초/최고방향품질0.4416, 누출0. 데이터 갱신 `_update_policy`11회(기존100k 재개3회와 구별), CPU/RAM/GPU 정상 범위. 모델/설정/manifest/진단/build-info와 **run_logs 전체**를 `Logs/L2-r005-100k-snapshot`에 보존했다. 현재 같은seed17423/100경기/난이도0/대기1 고정 평가 `Logs/L2-r005-direction-seed17423-100.json` 실행 중. 이 결과를 새 Player 기준선12%/시도21%와 비교한 뒤 동일 r005총200k 한정 재개 여부를 판단한다. 보상/환경 추가 변경 또는 r006 생성은 아직 없다. 아래 학습 중 표기는 이전 이력이다.

- **현재 Trainer — 최우선:** `CurriculumL2-20260904-r005`, 세션 **15911**, 방향 YAML/r004200,964 초기화/16workers/최초100k 자동 종료. 원본 YAML과 모델·Player SHA manifest를 Run에 보존했다. 다음은 스텝 증가/50k checkpoint/CPU·RAM·GPU/보상 누출 및100k 정상 종료 확인이다. 종료 후 `Logs/L2-r005-first-diagnostic.json`과 최종 번호 PT 고정100경기 평가 `Logs/L2-r005-direction-seed17423-100.json`을 보존한다. 새 Player 기준선은 아래12%이며 이전 Player13%와 혼동하지 않는다. 중복 Trainer/평가/Unity를 띄우지 않는다. 아직 L2 미승인, 보조 제거와 L0/L1 회귀는 후속 게이트다.
- **새 Player 기준선:** `Logs/L2-direction-r004-baseline-seed17423-100.json` complete/100경기/exit0/원본 SHA 불변, 인계12%/시도21%, 적용 순변위2m. 방향 보상50사건 총3.5720(평균 최고품질0.3572×100×cap0.1), 시도21사건 각0.15를 실제 확인했다. 코드가 Action/성공 판정을 바꾸지 않는 보상 실험이며 물리 표본 변동을 인정한다. 기준선 및 소스/빌드/검증/원본 모델 SHA는 별도 보존했다.

- **현재 실행 — 최우선:** 방향 실험 Player Build 성공(UTC `2026-09-04T11:13:18.0298499Z`, 오류0/기존warning485,144679098bytes). Runtime SHA `D79BA4AE66D7F9C5EDB26237144F9E4B5F7B1F02029556706A1D5F4F6ABA5442`, Curriculum SHA `254A5984EE65378A63BAF2C26CACF60A0AE15ACE433569C6AE95C69306CDE46F`, 설정 diff 없음. 비파괴 Validate도 전체 Stadium/L0·L1·L2 정상 통과. 현재 원본r004200,964/seed17423/100경기 고정 기준선 평가 세션 **8609**, 출력 `Logs/L2-direction-r004-baseline-seed17423-100.json`. 종료/SHA/성공 및 새 방향 보상 기록을 확인한 뒤 `CurriculumL2-20260904-r005`를 방향 YAML/16workers/100k로 시작한다. 아직 새 Trainer는 없다. 소스/build-info/원본 모델·설정·DLL SHA는 `Logs/L2-direction-source`에 보존했다.

- **검증 완료/빌드 전 — 최신:** 방향 실험 전체 Soccer EditMode **102/102**, 전체 Soccer PlayMode **18/18**, Python **24/24** 정상 통과. 테스트가 제거한 Standalone define을 복원했다. 현재 `Logs/L2-direction-Validate.log` 비파괴 검증 중이며 정상 종료 후 `Logs/L2-direction-Build.log`로 Windows Player를 빌드한다. 이전 Player와 r004 최종 Run은 보존되어 있다. 새 Trainer는 아직 없다.

- **검증 진행 — 최신:** 방향 실험 최종 소스의 전체 Soccer EditMode **102/102**, Python **24/24**, 두 YAML의 실제 Trainer parser 통과. 새 보상이 일반 Profile cap 대신 명시적 라운드 cap을 사용하도록 경로를 보완하고 테스트했다. `Logs/L2-direction-final-EditMode.*` 정상 종료, 현재 `Logs/L2-direction-PlayMode.*` 실행 중이다. 이후 테스트가 변경하는 Standalone define을 `APP_UI_EDITOR_ONLY;SENTIS_ANALYTICS_ENABLED`로 복원하고 비파괴 Validate 및 Windows Build한다. 현재 새 Trainer/r005는 없으며 중복 실행하지 않는다.

- **최신 변경/재개 지점:** 실제 확정 소유자의 명시적 킥 방향 품질 개선 보상(`CurriculumPassDirection`, 팀/라운드cap0.1) 및 적합한 패스 시도0.05→0.15 구현. 단순 자세/접촉은 보상하지 않으며 성공 판정·물리·Action·L1은 그대로다. 자동 패스/조준 없음. 공통 Engine/enum/Controller/순수 helper/테스트/모니터 허용 목록/문서를 갱신했다. 이전 Player는 `Builds/SoccerTraining-L2-r004-short-distance`, 이전 소스는 `Logs/L2-direction-before-source`에 보존했다. 아직 새 Player·r005 학습은 없다. 다음은 Python/전체 Soccer EditMode·PlayMode/비파괴 Validate/Windows Build, 설정 복원·SHA 확인 후 새 `curriculum_l2_direction_poca.yaml`의100k 진단을16workers로 시작한다. 초기화r004200,964, buffer10240/batch1024/3epochs로 데이터 갱신 빈도도 시험한다. 결합 실험임을 명시한다. 종료 후 같은seed17423/100경기 고정 비교, 추가 연장 전 원인 검토. L2 미승인, L1 원본 보존.

- **다음 점검의 정확한 재개 지점 — 최우선:** r004 최종 고정 평가 `Logs/L2-r004-200k-short-distance-seed17423-100.json` complete/100경기/exit0/원본 SHA 불변. 인계 **13%**, 시도24%, 수신 접촉·확정 소유19%, 소유99%,11.282초. 동일2m 기준 초기13% → r004100k16% → r004200k13%로 확정적 개선이 없다. **r004를 더 연장하지 않고 r005도 아직 생성하지 않았다. 현재 Trainer/Player/평가/Unity 0개 확인.** 100k 및200k의 모델·설정·진단·평가 snapshot, 번호별 PT/ONNX, 승인 L1 r017 SHA를 보존/확인했다. 200k snapshot은 `Logs/L2-r004-200k-snapshot`이다. L2 승인/보조 제거/무작위 최종 평가/L0·L1 회귀는 미완료이며 자동 점검을 유지한다.
- **재설계 전 조사:** 실제 킥246회 모두 확정 carrier였으나 유리한 대상 존재75회(30.49%), 방향 통로 충족24회(9.76%), 대상 존재 시 최선 방향 오차57.20도였다. 24개 비행 중19개 수신 제어 확인,6경기는 순변위2m 미달이다. 따라서 소유 인식 오류로 단정하거나 순변위 기준을 더 낮추지 않고, **발신자가 기회가 있을 때 방향·타이밍을 선택하도록 주는 보상과 초기 슈팅 정책의 습관**을 먼저 점검한다. 현 보상은 최초 소유0.1/적합한 실제 패스0.05/성공발신0.1·수신0.2/완료그룹0.4·개별0.1/지원cap0.02이며 패스 준비 방향 학습 신호는 없다. 코드 패스 보조는 아직 추가하지 않았다.
- **과도한 정체 해석 방지:** 재개 구간 `run_logs/timers.json`의 정책 데이터 갱신 `_update_policy`는3회, 내부 minibatch optimizer update는180회다. 100k 스텝을 충분한 정책 반복 학습으로 간주하지 말고 buffer40960/batch2048/3epochs와 유효 패스 표본 수를 함께 검토한다. 이 사실만으로 보상 오류나 optimizer 고장을 확정하지 않는다. 다음 실행은 보상·기회·업데이트 수의 원인 감사 후 결정하며 무의미한 버전 증가를 하지 않는다.
- **보존 범위 주의:** 100k snapshot은 PT/ONNX/configuration/source YAML/training_status/진단/고정평가/build-info를 보존했지만 당시 `run_logs` 전체를 재개 전에 독립 복사하지는 않았다. 재개가 갱신한 Player 로그·timers의100k 원본은 별도 보존됐다고 주장하지 않는다. 200k `run_logs` 전체는 snapshot으로 복사하며 이후 모든 재개 전 이 폴더도 반드시 별도 보존한다.

- **최신 종료/다음 평가:** r004는 **200,964에서 정상 종료(exit0)**, 실제 Trainer/Player 종료 확인. PT SHA `F927EB0C12F1F7E0D2A4C734B852D80DE2891FA1A3F0D8489BACAF45CFDB448F`, root ONNX checker 통과. `Logs/L2-r004-200k-diagnostic.json` 마지막5요약 성공16.813%/소유98.259%/시도24.443%/접촉21.168%/12.186초. 150k checkpoint 저장 및 RAM63%/GPU54°C를 실행 중 확인했고 오류·보상 누출은 발견되지 않았다. 현재 같은 seed17423/100경기/2m/대기1/난이도0으로 `Logs/L2-r004-200k-short-distance-seed17423-100.json` 고정 평가 중이다. 이 결과로100k의16%와 비교하고 이후 결정한다. 추가 학습 자동 연장 없음. 아래 재개 중 표기는 종료된 이력이다.

- **현재 실행 — 최우선:** r004 재개 Trainer 세션 **98363**, max200k/init_path None/16 workers. 비파괴 Validate는 Base·Attack·Defense·Press·Rule 및 L0/L1/L2 계약 통과, Unity 정상 종료·설정 diff 없음. 아래 100k 보존 뒤 같은 Run을 `--resume`했다. 다음은 이 세션의 스텝 증가·150k 저장·200k 자동 종료 확인, `Logs/L2-r004-200k-diagnostic.json` 저장 및 새 최종 번호 PT를 `Logs/L2-r004-200k-short-distance-seed17423-100.json`으로 고정 평가한다. 중복 실행하지 않는다. 성공 기준/보상/Player는 불변이며 L2는 아직 미승인이다.

- **최신 결과/재개 지점:** r004는 **100,032에서 정상 종료(exit0)**했고 실제 Trainer/Player/평가 종료를 확인했다. 최종 PT SHA `AEAD5EF897E6B7DC17D0B17DD6E2883F5AA93F248C88276116869114CC705B14`, root ONNX checker 통과. `Logs/L2-r004-first-diagnostic.json` 마지막5요약 성공10.889%/소유96.866%/시도17.857%/접촉13.056%/11.827초, 허용 외 보상 누출0. checkpoint/ONNX/configuration/source YAML/training_status/build-info/진단/고정평가를 `Logs/L2-r004-100k-snapshot`에 보존했다. 모든 번호별 checkpoint와 원본 Run도 유지한다.
- `Logs/L2-r004-short-distance-seed17423-100.json` complete/100경기/exit0/고정 정책/SHA 불변: 인계 **16%**(동일2m 초기 모델13%). 3%p 차이는 확정적 향상이 아니며 승급 근거도 아니다. 동일 조건의 한정 추세 확인으로 **같은 r004를 총200k까지만 재개**한다. init_path 없는 `curriculum_l2_short_distance_resume_poca.yaml`, 16 workers, `--resume`, 대기1/난이도0, Player·보상·행동 계약 불변. 재개 YAML을 Run의 `resume-200k-config.yaml`에 보존했다. Python23/23 통과, Runtime/Curriculum DLL SHA는 검증된2m Player와 동일. `Logs/L2-r004-resume-Validate.log` 비파괴 Unity 검증 완료 후에만 Trainer를 시작한다. 200k 후 같은 seed17423/100경기로 비교하고 정체하면 추가 연장/버전 증가 전에 발신 방향·선택의 보상 구조를 재설계한다. L2 미승인, L1 원본 승인 증거 보존. 아래 실행 중 기록은 이전 이력이다.

- **현재 실행 — 최우선:** `CurriculumL2-20260904-r004`, Trainer 세션 **92567**, Trainer1/Player16 확인. r003201,224에서 초기화(step0), max100k/대기1/난이도0을 실제 로그로 확인했고 **10k 진행**했다. `source-config.yaml`을 Run에 보존했고 현재 Player는2m 거리 기준이다. 다음은 이 Trainer의 스텝/50k checkpoint/100k 자동 종료를 모니터링한다. 종료 후 `Logs/L2-r004-first-diagnostic.json`과 불변 최종 PT 고정 평가를 수행해 기준선13% 대비 학습 향상을 확인한다. 중복 Trainer/평가/Build를 시작하지 않는다. 근거 없는 기준 완화/새 버전 증가는 금지하며 정체하면 발신 방향·선택의 보상 구조를 점검한다. 재개가 정당할 때는 `curriculum_l2_short_distance_resume_poca.yaml`/같은 r004/`--resume`으로 총200k까지만. L1 승인 모델·모든 r003 결과와 이전 Player는 보존됐고 L2는 아직 미승인이다.

- **최신 고정 결과/다음 실행:** `Logs/L2-r003-short-distance-seed17423-100.json` complete/100경기/exit0/SHA 불변, 실제2m 조건, 인계13%/시도22%/접촉15%/소유100%/평균10.823초. 이전3m 기준6~7% 대비 확인된 인계의 성공 신호가 생겼으므로 r003을 더 연장하지 않고 **2m 기준 r004 100k 진단**을 준비했다. 초기화는r003201,224, YAML `curriculum_l2_short_distance_poca.yaml`; 후속검토용resume는init_path 없는 `curriculum_l2_short_distance_resume_poca.yaml`/총200k다. 두 실제 Trainer parser 통과. 기준 변경효과를 신경망 학습 향상으로 표현하지 않는다. 현재 평가 종료/새 Trainer는 아직 미시작. r003에는 현재2m Player로 재개하지 않는다.

- **현재 실행:** 2m 거리 기준 Player Build 성공(UTC `2026-09-04T10:24:06.2375589Z`, 오류0/기존warning485/9 profiles). Runtime SHA `B0A709FEC6BB921A328BBA4E07B376CE450E02775C962C4BAB4124109D3EE67B`, Curriculum SHA `7D163CE569A330691023382803595B51ECCA2DBB5AA38855A3106185557A4207`, 설정 diff 없음. 고정 r003201,224/seed17423/100경기/대기1/난이도0 비교를 세션 **13309**, `Logs/L2-r003-short-distance-seed17423-100.json`으로 실행했다. 다음 작업은 이 평가의 완료/원본 SHA/2m 기준과 성공·접촉 통계 확인이다. 아직 새 Trainer/r004는 없으며 중복 실행하지 않는다.

- 2m 거리 기준 검증 완료: EditMode **98/98**, 전체 Soccer PlayMode **18/18**, Python **23/23** 통과(`Logs/L2-short-distance-*`). 테스트가 바꾼 Standalone define 복원 후 `Logs/L2-short-distance-Build.log` Windows Build 시작. 완료 후 같은 r003201,224/seed17423/100경기/난이도0/대기1을 `Logs/L2-r003-short-distance-seed17423-100.json`으로 고정 평가한다. 아직 새 기준 평가/학습은 시작하지 않았다. 기존 모델·Player·소스는 모두 보존.

- **최신 원인/구현:** `Logs/L2-r003-flight-seed17423-100.json` complete/100경기/exit0/SHA 불변. 적합한 패스19회/첫 수신16회, 성공6%(직전7%와 별도 실행 변동). 첫 수신 공 순변위 평균2.9347m, 수신자-공1.5719m, 발신자-공2.9719m, 비행0.1525초. 첫 접촉에서3m 충족은6/16=37.5%, **10경기는 제어된 수신자이나 순변위 부족**이었다. 수신 전 간섭3회/후 간섭9회(만료 제외). 실패를 전부 재접촉 기술 부족으로 보지 않고 **공 이동과 수신자 중심 거리의 구분**을 먼저 바로잡는다.
- 공 순변위 요구만 **3→2m**로 완화했고 대상 거리3~16m/유리함/명시적 킥/현재 지정 carrier·최종 접촉·2.4m/8초/수신 전 간섭 무효 및 보상액은 그대로다. `Required Ball Displacement` 통계와 2m 경계 테스트, 보상 변경표/계약/README/계획을 갱신했다. 현재 `Logs/L2-short-distance-EditMode.*` 검증 중이며 이후 PlayMode/Build/같은201,224 고정100경기 비교를 한다. **아직 새 기준 학습/평가와 r004는 시작하지 않았다.** 기준 변경 효과를 학습 향상으로 표현하지 않는다. 이전 진단 Player는 `Builds/SoccerTraining-L2-r003-flight-diagnostic`, 수정 소스·문서는 `Logs/L2-short-distance-source`에 보존했다.

- **현재 실행:** 첫 수신 계측 Player Build 성공(UTC `2026-09-04T10:17:17.8345221Z`, 9 profiles/8 trainable, 오류0/기존 warning485). Curriculum DLL SHA `00ECFA7CD9D57C49258039FBD9416CD8D2C48D0926B6A8A16EA7E860A76A1C33`, Runtime/정책 계약 불변, 설정 diff 없음. 같은 r003201,224 PT/seed17423/100경기/대기1/난이도0 고정 평가 세션 **51901**, 출력 `Logs/L2-r003-flight-seed17423-100.json`. 다음 작업은 이 평가 완료·원본 SHA·First Reception 통계와 수신 전후 무효화 확인이다. Trainer는 없으며 r004도 아직 없다. 기존 소스/모델/Player는 별도 보존했다.

- 첫 수신 진단 검증: EditMode **97/97**, 전체 Soccer PlayMode **18/18**, Python **23/23** 통과(`Logs/L2-r003-flight-*`). Standalone define 복원 뒤 `Logs/L2-r003-flight-Build.log` Windows Build 실행 중이다. 완료 후 같은201,224 PT/seed17423/100경기로 `Logs/L2-r003-flight-seed17423-100.json` 평가를 시작한다. 현재 Trainer는 없고 보상/행동 변경도 없다. 기존 Player는 `Builds/SoccerTraining-L2-r003-200k-handoff`, 계측 소스는 `Logs/L2-r003-flight-source`에 보존했다.

- **최신 결과/결정:** `Logs/L2-r003-200k-handoff-seed17423-100.json` complete/100경기/exit0/SHA 불변. 인계7%/시도22%/접촉13%/소유100%로 100k의 인계7%/시도22%에서 진전이 확인되지 않았고 접촉20→13%로 감소했다. 표본 변동 가능성을 인정하되 맹목 연장 근거로 삼지 않는다. **r003 추가 연장 및 r004 생성은 하지 않았다.** 현재 Trainer/평가 종료. 동일201,224 모델에서 첫 수신 당시 공 순변위·거리·시간, 수신 전후 재접촉/재킥을 계측하는 읽기 전용 통계를 추가했다. 보상·행동·성공 기준은 유지. 다음은 EditMode/PlayMode/진단 Player Build 후 같은 seed17423/100경기 재평가다. 결과에 따라 발신 조준·킥 타이밍·수신 접근·3m 조건을 분리해 설계를 판단한다.

- **2026-09-04 19:10 KST 최신:** r003은 총201,224에서 정상 종료(PTY10944 exit0), Trainer/Player 종료 확인. `Logs/L2-r003-200k-diagnostic.json` 마지막 5요약 성공10.065%/시도29.076%/접촉20.841%, 허용 외 보상 누출0. 성공이 0~33.33%로 변동하므로 평균 상승만으로 승급하지 않는다. 최종 PT SHA `87F6B0FA7C50312E5D3C39E6510E0D128EB1D36E813D5C120033E7FD1EF0E9DA`, root ONNX checker 통과. root checkpoint/ONNX/configuration/재개 YAML/training_status/진단/build-info를 `Logs/L2-r003-200k-snapshot`에 보존했다.
- **현재 실행:** 고정201,224 PT/seed17423/100경기/대기1/난이도0 평가 세션 **15487**, `Logs/L2-r003-200k-handoff-seed17423-100.json`. 완료 후 동일 조건 100k 모델(성공7%/시도22%/접촉20%)과 비교한다. 신규 학습·빌드·평가를 중복 시작하지 않는다. 기존500k 설정으로 자동 연장하지 않고 이번 고정 결과까지 확인한다. L1 승인 모델과 원본100k/200k 모델은 보존, L2 미승인. 아래 재개 중 문구는 종료된 이력이다.

- **현재 실행 — 최우선:** r003 재개 Trainer 세션 **10944**, Trainer1/Player16개. 실제 로그 `Resuming training from step 100336`, init_path None, max_steps200000 확인했고 **110k까지 진행**했다. 대기1/난이도0, 동일 Player·보상·정책 구조다. `resume-200k-config.yaml`을 Run에 보존. `Logs/L2-r003-latest.json`: RAM62%/여유11.80GiB/GPU53°C, 허용 외 보상 누출0. 다음 점검은 이 실행의 스텝/150k 저장/200k 자동 종료이며 중복 Trainer·평가·Build를 시작하지 않는다. 종료 후 `Logs/L2-r003-200k-diagnostic.json` 분석 및 새 불변 최종 PT를 같은 seed17423/100경기로 평가한다. 과거100k 결과·원본 checkpoint는 보존돼 있다. 최근 Unity 비파괴 Validate 정상 종료, Python23/23, 설정 diff 없음. 코드/보상 변경은 이번 재개에서 없다. L2는 아직 미승인이다.

- **2026-09-04 18:53 KST 재개 확인 — 최신:** r003은 100,336에서 정상 종료(exit0), Trainer/Player/평가 0개 확인. 번호별 PT SHA `8A0601A517D2B4E9875552BA26C3121A60BC2FDD97B0D561544E7007F9DAD85C`, 최종 ONNX checker 통과, configuration/최종 모델/이벤트 보존. `Logs/L2-r003-first-diagnostic.json` 마지막 5요약 성공2.922%/소유98.75%/시도13.628%/접촉9.094%/평균12.621초, 보상 누출0. **성공 기준 미달**이다.
- 고정 평가 `Logs/L2-r003-handoff-seed17423-100.json` complete/100경기/exit0/SHA 불변: 인계7%, 시도22%, 접촉20%, 소유91%, 평균12.102초. 같은 seed/조건의 초기 r002 모델은5%/16%/12%였다. 작은 표본의 2%p 차이는 확정적 개선으로 해석하지 않는다. 성공 신호와 시도·접촉 증가를 근거로 환경·보상·버전은 유지하고 **같은 r003을 총200k까지만** 한정 연장한다. 새 `curriculum_l2_handoff_200k_resume_poca.yaml`은 init_path 없음/max200k/실제 Trainer parser 통과. `--resume`으로 optimizer와 학습 상태를 유지한다. 기존500k 재개 설정은 보존하되 이번에는 사용하지 않는다. 200k 정상 종료 후 같은 고정 조건 평가와 신호 추세를 확인하고, 정체하면 다음 버전 전에 발신 방향·수신 접근·공 이동 거리를 분해 진단한다.
- 100k의 root checkpoint/최종 ONNX/configuration/원본 실행 YAML/training_status/진단/평가/build-info를 `Logs/L2-r003-100k-snapshot`에 별도 복사했다. 현재 Player Runtime/Curriculum SHA는 이전 검증 빌드와 동일하다. `Logs/L2-r003-resume-Validate.log` 비파괴 Unity 검증 실행 중이며, 완료 후에만 학습을 재개한다. 아직 재개 Trainer는 시작하지 않았다. 아래 30k 진행 문구들은 이전 실행 이력이다.

- r003 후속 확인: 30k 진행, 인계 성공 보상이 실제 기록되기 시작했다(30k 요약 4.76%; 3개 요약 평균 1.59%로 아직 승급 근거 아님). `Logs/L2-r003-latest.json`, 허용 외 보상 누출 없음, RAM63%/여유11.62GiB/GPU54°C. 현재 Trainer 세션92658을 유지하며 50k 저장과 100k 자동 종료를 점검한다.

- **현재 실행 — 가장 최신:** `CurriculumL2-20260904-r003`, PTY 세션 **92658**, Trainer 1개/Player **16개** 확인. r002 최종 500,380에서 초기화, step0 시작, 대기1/난이도0, max_steps **100,000**을 실제 로그로 확인했고 첫 10k 스텝 진행. `source-config.yaml`을 Run에 보존했다. 현재 Trainer를 모니터링하며 신규 Trainer/평가/Build를 중복 시작하지 않는다. 50k 저장, 100k 자동 정상 종료를 확인한 뒤 `Logs/L2-r003-first-diagnostic.json` 분석 및 불변 checkpoint 고정 평가를 한다. 지금은 기존 PTY Ctrl+C 전달 문제를 피하려고 max_steps 자체를 진단 지점으로 설정했다. 개선 근거가 있을 때만 init_path 없는 `curriculum_l2_handoff_resume_poca.yaml`/같은 run-id/`--resume`으로 총 500k 이내 연장한다. L2는 미승인이며 L3는 하지 않는다.

- r003 시작 근거: 새 인계 기준 고정 평가 `Logs/L2-r002-handoff-seed17423-100.json` **100경기 complete/exit0/원본 SHA 불변**, 성공5%/시도16%/접촉12%. 동일 모델·seed의 이전 유지 기준 성공0%와 비교하되 이것은 **기준 변경으로 생긴 성공 신호이지 학습 향상 증거가 아니다**. 자동 조준/강제 패스는 없고 기존 대기 보조만 유지했다. 인계 후 추가 유지 요구는 0.75→0초, 나머지 유리함/명시적 킥/3m 순변위/현재 지정 carrier·최종 접촉/2.4m/8초 조건과 보상 수치는 유지. 코드/문서/테스트·Build 증거와 기존 Player/모델 전부 보존. 새로운 두 YAML은 실제 Trainer parser 통과했다. 아래 진행 중/예정 항목은 이 최신 실행 이전 이력이다.

- **현재 실행:** 새 인계 기준 Player Build 성공(UTC `2026-09-03T22:34:15.7577220Z`, 9 profiles/8 trainable, 오류0/기존 warning485). Runtime SHA `A823CEB7FB1B246ABED910E7826FC03FA65B42D2A350615B4782CA621B01975B`, Curriculum SHA `9970B12869B02329EA87C9D760F079D4BD11EF04DE88241347CD57A3161E67FF`. 설정 diff 없음. 같은 r002 500,380/seed17423 고정 100경기 평가를 세션 **86477**, `Logs/L2-r002-handoff-seed17423-100.json`으로 실행 중이다. 첫 25경기는 인계 성공4%/시도20%/접촉20%, 요구 제어시간0이 기록됐다. 완료 후 확정 결과로 새 한정 학습의 타당성을 판단한다. 아직 r003 없음. 이 평가는 신경망 추가 학습이 아니라 기준 변경 효과다.

- 인계 기준 검증 저장: EditMode **97/97**, 전체 Soccer PlayMode **18/18**, Python **23/23** 통과(`Logs/L2-handoff-*`). Standalone 설정 복원 후 `Logs/L2-handoff-Build.log` Windows Build를 시작했다. 완료 후 기존 r002 최종 checkpoint와 같은 seed17423/100경기/난이도0/대기1로 `Logs/L2-r002-handoff-seed17423-100.json` 고정 평가 예정. 아직 새 기준 평가 및 r003 학습은 시작하지 않았다. 수정 소스·문서 8개는 `Logs/L2-handoff-source`에, 이전 진단 Player는 `Builds/SoccerTraining-L2-r002-reception-diagnostic`에 보존했다.

- **최신 진단/구현:** `Logs/L2-r002-reception-seed17423-100.json` 100경기 complete/exit0/원본 SHA 불변. 유효 패스 16, 접촉·현재 수신 carrier 13, 성공 0. 비행 무효 원인은 발신자 재접촉 12/수신자 재킥 2/다른 선수 접촉 1/만료 1이었다. 최대 수신 연속 시간은 에피소드 합 0.3801초다(전체 100경기 평균 0.0038초; 수신한 경기 평균과 구분). 사용자에게 결과와 기준 분리를 알렸다. 추가 유지 시간을 0.75→0초로 바꾸고, 명시적 유리한 패스·3m 순변위·지정 수신자의 현재 carrier/최종 접촉/거리·8초 제한은 유지한다. 현재 FixedUpdate 확인 뒤 성공이라 터치 콜백만으로 보상하지 않는다. 보상 수치/물리/자동 조준·킥은 변경하지 않았다. 변경표/계약/README/계획/테스트 갱신. 다음은 전체 테스트·Player Build·같은 r002 500,380/seed17423/100경기 새 기준 고정 평가이며 새 Run은 그 결과 후 판단한다. 아래 평가 실행 중 문구는 완료된 이력이다.

- **현재 실행:** 수신 계측 Player Build 성공(9 profiles/8 trainable, 오류 0/기존 warning 485, `Logs/L2-reception-diagnostic-Build.log`, UTC `2026-09-03T22:26:48.7907037Z`). Curriculum DLL SHA `A396540D2D7F4921D9A8959A630F58587D56BE19A0AADB5A0FA94C9035C5C20A`. ProjectSettings diff 없음. `Logs/L2-r002-reception-seed17423-100.json` 고정 평가를 세션 **25385**로 실행했다. r002 최종 500,380/seed17423/100경기/대기1/난이도0, optimizer 없음. 다음 작업은 이 평가 결과와 프로세스 확인이다. 신규 Trainer/평가를 중복 시작하지 않는다. 진단 결과로 재킥·재접촉·소유 유지 병목을 구분하고, 다음 학습 전에 완화/보조 필요성을 판단한다. 아직 r003은 없다.

- **최신 확정/다음 작업:** r002는 500,380에서 자연 종료했고 PTY 22713 exit 0, Player 0개 확인. `configuration.yaml`/최종 root ONNX/번호별 PT·ONNX/이벤트를 보존했고 ONNX checker 통과. 최종 PT SHA `B31B36C6FEF0A51D8BDF7F8936B81A5F677B0E90DDDECFE30F56A8C652534BC9`. `Logs/L2-r002-final.json` 마지막 5요약은 소유 약 96%·패스 시도 29%·수신 접촉 23%·성공 0%, 평균 13.53초. L2 미승인. 수신 실패 계측은 EditMode 95/95·PlayMode 18/18·Python 23/23 통과했고 설정 복원 완료. 현재 `Logs/L2-reception-diagnostic-Build.log`로 Windows Build 실행 중(명령 반환만으로 완료 판정 금지). 완료/프로세스 확인 후 이 최종 불변 PT를 seed 17423, 100경기, 난이도 0/대기 보조 1로 `Logs/L2-r002-reception-seed17423-100.json`에 고정 평가한다. 아직 평가는 시작하지 않았다. 이전 학습 Player는 `Builds/SoccerTraining-L2-r002-waiting`에 보존했고 계측 소스는 `Logs/L2-reception-diagnostic-source`에 저장했다. r003 미생성. 이전 중단 요청은 반영되지 않았으나 이번 정상 종료로 해소됐다.

- **최신 진행:** r002 PTY 22713의 Ctrl+C가 종료로 반영되지 않았고 320k까지 학습/번호별 checkpoint 저장이 계속됨을 확인했다. 강제 종료하지 않고 기존 500k 한정 실행의 자연 종료를 기다린다. 320k 최근 5요약 패스 시도 20.71%·수신 접촉 13.57%·성공 0%, 약 627 step/s, 보상 누출 없음. 새 Trainer나 평가를 중복 시작하지 않는다. 보상·행동을 바꾸지 않는 수신 실패 계측(재킥 주체/재접촉 주체/만료/확정 수신/최대 제어 시간)을 소스에 추가했다. 실행 Player는 이전 코드라 이 통계가 아직 없다. 종료/보존 확인 → 테스트/진단 Player Build → 같은 불변 r002 checkpoint 고정 평가 순서로 진행한다. 신경망은 아직 L2 미승인, r003 미생성. 아래 실행 중/다음 예정 문구는 이 최신 항목 이전 이력이다.

- r002 후속 확인: 70k까지 약 628 step/s, 최근 요약의 적합한 패스 시도 평균 약 16%·수신자 접촉 약 11%, 성공은 아직 0%, 일반 보상 누출 없음. 49,960 checkpoint/ONNX가 저장됐고 ONNX checker 통과. Player 16개 유지, 새 치명 오류 검색 없음, ProjectSettings/EditorBuildSettings diff 없음. 첫 100k 진단 계획을 유지한다. 단순 시도 보상만 최적화하는지 반드시 함께 확인한다.

- **현재 실행 — 가장 최신:** `CurriculumL2-20260904-r002`, PTY 세션 22713/Trainer PID 10256, Player 16개. L1 r017 99,916에서 초기화했고 쉬운 배치 0/대기 보조 1을 실제 시작 로그로 확인했다. 20k 스텝 진행 확인, RAM 61%/여유 12.12GiB/GPU 53°C로 과부하 징후 없다. `source-config.yaml`을 Run 안에 별도 복사해 실행 설정을 보존했다(Trainer의 정상 종료 configuration.yaml과 구분). 다음 작업은 **이 Trainer 모니터링**이며 새 Trainer/평가/빌드를 중복 시작하지 않는다. 50k 저장을 확인하고 100k에는 `Tools/inspect_soccer_training.py --run-id CurriculumL2-20260904-r002 --at-step 100000 --output Logs/L2-r002-first-diagnostic.json`으로 진단한다. 시도→접촉→0.75초 안정 수신을 구분하고, 성공이 계속 0이면 수신 직후 재킥/비행 무효화 원인을 점검한다. 보조 포함 성과는 L2 최종 승급 불가다. 아래 평가·구현 기록은 이 실행 이전 이력이다.

- **대기 보조 비교 완료/다음 실행:** 같은 L1 r017 checkpoint·seed 96321·각 100경기 고정 평가 모두 complete/exit 0/SHA 불변. 보조 0은 패스 시도·수신 접촉·성공 0%, 보조 1은 시도 16%·수신 접촉 14%·성공 0%였다. `Logs/L2-waiting-off-L1-seed96321-100.json`, `Logs/L2-waiting-on-L1-seed96321-100.json`. 따라서 준비 환경이 패스 경험을 제공하는 효과는 확인됐지만 안정 수신은 미완성이다. 승인 L1 모델에서 `CurriculumL2-20260904-r002`/`curriculum_l2_waiting_poca.yaml`로 새 Run 하나를 시작한다. 16 workers/50k 저장/100k 진단/최대 500k. 첫 진단에서 시도 증가와 접촉→안정 수신 전환을 따로 확인하며 단순 시도 증가만으로 L2를 승인하지 않는다. 재개에는 init_path 없는 `curriculum_l2_waiting_resume_poca.yaml`만 사용한다.

- **대기 보조 고정 평가 진행:** 새 Player Build 완료(`Logs/L2-waiting-Build.log`, build-info `2026-09-03T22:06:18.8884426Z`, 오류 0/기존 warning 485). Runtime DLL SHA `9FF295EAEABCE3489DE278FDDA1617DC67F7CE6814028B3EF860EF54C119C2A1`, Curriculum DLL SHA `8476013542C430B4CC4E155E6E60EFF8454A3AB2134FEEBF1BE0A8A7D36D7256`. L1 r017 고정 모델/seed 96321/100경기에서 보조 1은 적합한 패스 시도 16%, 수신자 접촉 14%, 안정 수신 완료 성공 0%였다(`Logs/L2-waiting-on-L1-seed96321-100.json`, complete/exit 0/SHA 불변). 학습 기회는 생겼지만 L2 성공은 아니다. 동일 조건 보조 0 비교를 `Logs/L2-waiting-off-L1-seed96321-100.json`으로 실행 중이며 새 Trainer는 아직 없다.

- 대기 보조 검증 저장: EditMode 95/95, 전체 Soccer PlayMode 18/18, Python 23/23 및 두 준비 YAML의 실제 Trainer parser 통과. 로그 `Logs/L2-waiting-*`. 테스트가 제거한 Standalone `SENTIS_ANALYTICS_ENABLED`를 복구하고 Windows Build를 실행했다. 비교 모델은 L1 승인 r017 99,916, 새 동일 seed 96321/쉬운 배치/각 100경기로 보조 0과 1을 비교한다. 출발 위치와 대기가 함께 다른 준비 환경 비교이므로 모델의 학습 개선으로 표현하지 않는다.

- **현재 구현/검증:** `soccer_l2_waiting_assist`와 Env 대기 registry, Agent의 대기 action mask/평면 속도 제한을 구현했다. 준비 과제에서 발신자 외 필드 선수 2명이 대기하고 유리한 실제 패스가 나가면 해제한다. Keeper/Human/Rule/fallback은 대기 대상에서 제외한다. 수신자 14m 시작, 대기 중 지원 보상 0, 순간이동·자동 조준/킥 없음. 최종 난이도 1에서는 설정과 무관하게 보조를 끈다. `Waiting Assistance`/대기 선수·초 통계 및 모니터의 무보조 최종 게이트를 추가했다. EditMode 95/95, Python 23/23 통과. `Logs/L2-waiting-PlayMode.*` 실행 후 Build/고정 비교를 진행한다. 준비 YAML `curriculum_l2_waiting_poca.yaml`/init_path 없는 resume 변형을 생성했지만 r002 Trainer는 아직 시작하지 않았다.

- **현재 확정 결과/재개 지점:** 실제 킥 계측 평가 `Logs/L2-r001-eval-strikes-seed85234-100.json` 완료(exit 0/100경기/원본 SHA 불변). Red의 비보정 명시적 킥 346회 모두 확정 carrier, 평균 공 거리 1.403m로 소유 판정 오류가 아니다. 킥 당시 유리한 동료 17/346=4.91%, 실제 킥 통로 일치 0%, 유리한 대상이 있을 때 최선 대상 방향 오차 평균 99.56°, 최근접 동료 거리 평균 33.54m였다. 이것은 모든 킥 사건 평균이지 첫 킥/에피소드 평균이 아니다. 단순 동시 돌진만 원인이라고 단정하지 않으며, 유리한 거리 유지와 방향 연결에 학습 기회가 거의 없는 것이 확인된 병목이다. 현재 Trainer/평가/Player는 모두 종료됐다.
- **다음 구현 계획(아직 미구현):** L2-A 준비 단계에만 `soccer_l2_waiting_assist`를 추가한다. 기본값 0, 준비 YAML에서만 1. 지정 발신자 외 필드 선수는 대기하고 유리한 실제 패스가 나가면 이동·수신을 Neural에 다시 맡긴다. 수신자 시작 거리는 준비 단계에서 14m로 두어 발신자가 소유를 확보하는 동안 통로를 유지한다. 위치를 매 프레임 순간이동시키거나 킥 방향/힘을 자동 선택하지 않는다. 공통 Env/Agent의 임시 action mask와 평면 속도 제한으로 대기하며 Human/Rule/fallback·일반 경기·L0/L1에는 적용하지 않는다. Keeper는 기존 공통 보호를 유지한다. 대기 보조 중의 지원 위치는 학습으로 획득한 성과로 보상/보고하지 않는다.
- 대기 보조는 사용자가 허용한 **코드로 가르치는 준비 기술**이며 이미 사용자에게 이 점을 알렸다. `Waiting Assistance` 및 `Soccer/Skill Advice/L2 Waiting Agent Seconds`를 기록하고, 최종 난이도 1에서는 요청값과 무관하게 끈다. 모니터·고정 평가에서도 보조가 남은 결과는 L2 최종 승급하지 않도록 검사한다. 구현 후 전체 테스트·빌드·동일 고정 정책 비교로 실제 패스 기회가 생기는지 확인한 뒤에만 새 학습 Run을 만든다. r002는 아직 생성하지 않았다. 초기화는 보존된 L1 r017 승인 모델을 우선하며 모든 기존 L2 r001 모델과 Player는 보존한다.

- **진단 Player 검증 완료/평가 실행:** 실제 킥 계측 추가 후 EditMode 89/89, PlayMode 18/18, 9 profiles/8 trainable 검증 및 Windows Build 오류 0/기존 warning 485 통과. `Logs/L2-r001-diagnostic-*`, build-info `2026-09-03T21:52:51.8352769Z`. 초기 L2 Player는 `Builds/SoccerTraining-L2-r001-initial`에 보존했다. 같은 r001 99,912/seed 85234/쉬운 배치/100경기를 `Logs/L2-r001-eval-strikes-seed85234-100.json`으로 평가 중이다. 새 학습은 시작하지 않았다.

- **진단 계측 작업 중:** r001 번호/모델을 유지한 채 `RecordL2StrikeDiagnostics`를 추가했다. 실제 명시적 킥 접촉마다 확정 carrier 여부, 공 거리, 유리한 동료 존재, 킥 통로 일치, 가장 가까운 동료 거리와 최선 대상 각도 오차를 기록한다. 성공 판정·보상·행동·배치는 변경하지 않았다. `Logs/L2-r001-diagnostic-EditMode.*` 검증부터 수행 중이며 완료 후 진단 Player에서 같은 r001 99,912 checkpoint/seed 85234를 100경기 재평가한다. 아직 새 Trainer는 없다.

- **최신 재개 지점:** L2 r001은 첫 100k에서 적합한 패스 시도가 계속 0이라 저장 후 중단했고 고정 관측 진단도 완료했다. 현재 Trainer/평가/Player는 실행 중이 아니다. `Logs/L2-r001-eval-opportunities-seed85234-100.json`은 seed 85234/100경기/complete/exit 0, checkpoint SHA `49F6A6206D45824A27A6E38B1567EDACD6B0234070ED230C78CB5961FE1022A8` 불변이다. 성공 0%, 킥 가능 결정 311개 중 더 유리한 동료가 있는 결정은 22개(7.07%)뿐이었고 그 22개에서는 모두 킥을 선택했다. 따라서 단순히 킥 선택을 못 하는 것보다 **동료의 유리한 위치 유지/실제 접촉 시점 방향**을 먼저 조사한다. 이 수치는 결정 순간이며 실제 킥 순간의 타깃·방향 조건을 직접 증명하지 않는다.
- 다음 작업: Run 번호를 올리거나 기존 r001을 맹목 재개하지 않는다. 동일 고정 checkpoint로 실제 킥 순간의 확정 carrier/유리한 동료 존재/킥 통로·각도 진단을 추가해 재평가한다. 수신자의 공 동시 돌진으로 패스 기회가 사라지는지 확인한 뒤, 필요하면 단계화된 지원 위치 연습 또는 명시적인 수신 대기 보조를 설계한다. 후자를 쓰면 코드가 가르친 내용과 해제 단계·개입 빈도를 반드시 명시한다. 원래 L1 승인 모델과 r001 49,960/99,912 모델은 그대로 보존하고 L2 기준을 임의로 통과 처리하지 않는다.
- 중단 산출물 확인: 두 번호별 `.pt/.onnx` 및 `checkpoint.pt`, TensorBoard event, Player run_logs가 존재하고 99,912 ONNX checker 통과. 이번 인터럽트는 exit 1이며 Run 루트의 정상 종료용 `configuration.yaml`/최종 root ONNX는 생성되지 않았다. 이를 정상 최대 스텝 완료로 기록하지 않는다. 실행한 초기화/재개 YAML과 Player 메타데이터는 원본 및 소스 스냅샷에 보존돼 있다. 정확한 이어받기 원본은 위 불변 번호별 checkpoint다. ProjectSettings/EditorBuildSettings diff 없음.

- **현재 실행 갱신:** r001의 실제 100k 진단에서 소유 93.3%, 킥 92.1%이나 적합한 패스 시도/수신/성공은 모두 0%였다. `Logs/L2-r001-first-diagnostic.json`, 일반 보상 누출 0, 약 636 step/s, RAM 62%/GPU 52°C로 하드웨어 병목이 아니다. 계획대로 99,912 checkpoint/ONNX를 확인한 뒤 Trainer 47564를 Ctrl+C로 중단했다(exit 1, 최대 스텝 완료가 아님). 49,960/99,912 모델은 보존한다. 새 Run을 만들지 않고 `Logs/L2-r001-eval-opportunities-seed85234-100.json`으로 고정 정책 100경기 관측 진단을 실행한다. 다음 점검은 이 평가부터 확인한다.
- 관측 진단 추가: 평가 도구가 킥 가능한 결정에서 관측 Vector 43의 자기·동료 위치(공통 스케일 80, Stadium 62/42.32727)를 복원해 더 유리한 3~16m 동료가 있는 비율과 그때 킥 선택 비율을 기록한다. 이는 **결정 시점의 읽기 전용 진단**이고 실제 킥 순간의 성공/보상을 대신하지 않는다. 평가 정책이나 Player는 바꾸지 않았다. Python 테스트 22/22 통과.

- **현재 실행(가장 최신):** `CurriculumL2-20260904-r001` 단일 Trainer 세션 47564/PID 15792, Player 16개. 승인 L1 `Soccer4v4_Base-99916.pt`에서 초기화하고 새 Run의 step 0부터 학습을 시작했으며 10,000 스텝 정상 진행을 확인했다. 쉬운 배치 0, 최대 500k, 50k 저장, 첫 100k 진단. 현재 RAM 61%/여유 12.23GiB, GPU 53°C로 과부하 징후 없음. 기존 PyTorch x.T 경고는 중단 오류가 아니다. **다음 점검은 중복 실행 없이 이 Trainer를 모니터링**한다. `Logs/L2-r001-latest.json`을 갱신하고 100k에는 `--at-step 100000`으로 별도 첫 진단을 저장한다. 적합한 패스 시도가 계속 0이면 원인 조사 후 수정하며 맹목적으로 최대 스텝까지 유지하지 않는다. 아래 구현/미시작 기록은 이 실행 이전 이력이다.

- L2 런타임 연결 저장(이전 준비 상태보다 우선): `SoccerCurriculumController.L2.cs`로 킥 당시 유리한 수신자 고정, 다른 선수 재접촉/새 킥/8초 만료 무효화, 확정 수신자의 공 2.4m 안 추가 0.75초 연속 제어를 구현했다. L1 슈팅 보조·8m 킥 잠금은 L2에서 끈다. 신경망의 이동/조준/킥 선택을 자동 패스로 대체하지 않았다.
- L2 전용 보상: 소유 0.1, 적합한 실제 킥 첫 시도 0.05, 성공 발신 0.1·수신 0.2, 완료 그룹 0.4·수신자 0.1, 지원 0.005/샘플 및 선수별 cap 0.02. 각 사건/완료는 에피소드당 1회 제한이다. Engine 허용 목록으로 다른 단계 보상을 차단한다. 코드/보상표/README/격리 테스트를 함께 갱신했다.
- L2 자산 생성 완료: `SoccerProjectBuilder.BuildCurriculumL2Batch`, `Logs/L2-r001-Generate.log`, compile·L0/L1/L2 계약 검사 정상 종료. 기존 L0/L1·팀 씬은 재생성하지 않았다. `curriculum-l2` Profile/port 5705, 20초 과제, 쉬운 배치 0→확대 배치 1. 초기화 `curriculum_l2_poca.yaml`, 재개 `curriculum_l2_resume_poca.yaml`, 500k/50k 저장/16 workers 계획이다. 두 YAML 실제 Trainer parser와 Python 20/20 통과. 아직 Trainer는 시작하지 않았으며 전체 Unity 테스트→새 Player→고정 smoke 평가를 먼저 수행한다.
- L2 런타임 검증: 전체 Soccer EditMode 89/89 및 전체 Soccer/Stadium PlayMode 18/18 정상 종료(`Logs/L2-r001-EditMode.xml`, `Logs/L2-r001-PlayMode.xml`). 테스트가 제거한 Standalone `SENTIS_ANALYTICS_ENABLED`를 복구하고 Windows Player 빌드를 시작했다(`Logs/L2-r001-Build.log`). 이전 승인 r017 Player는 `Builds/SoccerTraining-r017-approved`에 222파일/145,093,792 bytes로 복사 보존했으며 exe SHA는 기존 `D3DB57B9B88A2D8FEDD1621461DDD7B29ED18001EFD29A93EF9F31D4E8A483CD`와 동일하다.
- L2 Build 완료: `Logs/L2-r001-Build.log`, 9 profiles/8 trainable 검증, Windows 빌드 오류 0/기존 warning 485, build-info `2026-09-03T21:37:55.4741746Z`. 실행파일은 Unity 공용 launcher라 SHA가 이전과 같으며 실제 코드 식별에는 Runtime DLL SHA `330A938146B595910AD42E844B7505B8CFBAB94FEDB17B652EDB6B4AA6C59B9A`, Curriculum DLL SHA `87286D164838671EF0C0095F974875D7FE74C0196331CBCB2575C8A64325DFF3`도 보존한다. 소스/씬/YAML/문서/build-info 사본은 `Logs/CurriculumL2-20260904-r001-source`에 저장했다.
- L2 출발 평가: 승인 L1 정책을 그대로 새 L2 Player/쉬운 배치/seed 74123에서 100경기 고정 평가했다. `Logs/L2-r001-L1-baseline-seed74123-100.json`, complete/exit 0, 원본 SHA 불변. 소유 89%, 실제 킥 87%지만 적합한 패스 시도·수신·성공은 0%, 평균 15.60초였다. L1 기술이 패스로 자동 전이되지 않음을 확인했으며 이것을 L2 학습 성공으로 해석하지 않는다. r001에서 실제 적합한 시도 발생 여부를 100k에 우선 진단한다. 계속 0이면 500k까지 맹목 유지하지 않고 동료 배치/선택 기회/방향을 분리 조사한다.

- 최신 확인(06:16 KST): 실행 중인 Trainer/평가/학습 Player 없음. L2는 아직 완료되지 않았으며 05:00 이전 완료 조건을 충족하지 못했으므로 이번 작업에서 L3는 시작하지 않는다. L2까지의 개발·학습 승인은 계속 유효하다.
- L2 구현 첫 저장: `SoccerCurriculumPassRules.cs`의 유리한 수신자·실제 킥 통로·순변위/안정 수신 판정과 14개 EditMode 사례를 구현했다. 아직 Controller/Engine 런타임 미연결, L2 Scene/Prefab/YAML/Player 미생성, Trainer 미실행이다. 다음 작업은 명시적 킥 때 대상 snapshot을 만들고 수신 연속 제어 0.75초를 추적하는 런타임 연결, 레슨별 보상 종류/상한 추가, Builder/Catalog/모니터 확장이다. L1의 킥 잠금·슈팅 보조를 L2에 실수로 적용하지 않도록 분리해야 한다.
- 평가/모니터 보호: `evaluate_soccer_policy.py`가 L0/L1/L2 문자열과 일치하는 profile, 양수 경기 수·시간 제한을 실행 전에 검증한다. 과거 숫자 `--lesson 1`은 즉시 실패하며 Player를 띄우지 않는다. L2 모니터는 성공 50%/소유 85%/15초의 5개 동기화 요약, 안정된 난이도와 L1 보상 누출까지 검사하며 쉬운 배치는 최종 승급하지 않는다. Python 테스트 전체 20/20 통과. 이번 Soccer.EditModeTests는 새 14사례를 포함해 85/85, 기존 경기 PlayMode 6/6 및 전체 Soccer/Stadium PlayMode 18/18 통과. 로그는 `Logs/L2-r001-contract-*`에 저장했다. 테스트가 제거한 `SENTIS_ANALYTICS_ENABLED`를 복구했다. `git diff --check`의 Base fallback Scene 435행 공백은 이번 수정 이전부터 있던 사용자/기존 변경이라 건드리지 않았다.
- 현재 빌드는 여전히 승인된 r017 Player다. 새 L2 순수 판정은 Unity에서 컴파일/테스트했지만 Player에 포함하지 않았으며, 사용하지 않는 준비 코드라 기존 학습 동작은 바뀌지 않았다. L2 런타임/씬 연결 후 새 빌드를 만들고 학습 전에 동작 평가를 해야 한다. 작업 스냅샷 `Logs/L2-r001-contract-source`를 보존한다. 활성 자동 점검은 L2까지 이어서 작업하고 L2 승인 후 중지하도록 갱신했다.
- 준비 코드 Builder Validate 정상 종료(`Logs/L2-r001-contract-Validate.log`), ProjectSettings/EditorBuildSettings diff 없음. 새로 수정한 대상의 whitespace 검사는 통과했다. 검증 종료 후 Unity/Trainer/평가/Player 프로세스 없음. 다음 자동 점검은 이 준비 코드 검사를 반복하는 대신 L2 런타임·보상·씬 구현부터 진행한다.
- L1 최종 승인: `CurriculumL1-20260904-r017`의 `Soccer4v4_Base-99916.pt`를 선택했다. 100k 당시 최근 5요약이 최종 게이트를 통과했으며 평균 성공 82.82%, 소유 100%, 운반 충족 98.52%, 방향 슛 87.57%였다. 약 140k에서 평가를 위해 정상 인터럽트했으므로 max_steps 완료 Run으로 기록하지 않는다. 모든 중간 모델과 로그를 보존한다.
- 독립 고정 평가: `Logs/L1-r017-eval-final-seed86420-300.json`, seed 86420/phase 2/300경기/complete. 성공 233/300=77.67%, 소유 100%, 8m 운반 충족 95.67%, 평균 운반 13.45m, 유효 슛 98%, 방향 슛 85.33%, 득점 82.33%, 평균 8.38초. optimizer 없이 원본 정책을 고정하여 평가했다.
- L0 회귀: 동일 모델 `Logs/L1-r017-eval-l0-regression-seed75319-300.json`, seed 75319/300경기/complete. 성공·소유 297/300=99%, 평균 1.52초. 두 평가의 원본 SHA-256은 `82842DAC039282288D72C64D290021C4ED0D8EDE6D6EB839B405141395637069`로 동일하다. 승인 ONNX는 같은 디렉터리의 `Soccer4v4_Base-99916.onnx`다.
- 성과 범위: 무수비 L1 과제의 코드 보조 포함 성과다. 순수 신경망의 독립 슈팅/자유 경기 실력으로 해석하지 않는다. 최종 300경기에서 8m 킥 잠금 해제 305회, 슛 권장 187회, 골문 밖 킥 보류 5,860회가 기록됐다. 이것은 사용자 승인에 따라 코드로 가르친 부분이다.
- r017 검증 보존: Compile/Builder Validate 통과, Soccer EditMode 73/73·PlayMode 18/18, Windows Build 오류 0(기존 warning 485). `Logs/L1-r017-*`, `Logs/CurriculumL1-20260904-r017-source`, `Builds/SoccerTraining/build-info.json`. ProjectSettings/EditorBuildSettings 추가 변경 없음. L2는 별도 보상 허용 목록과 유리한 동료의 안정 재소유 판정을 구현한 뒤 검증·빌드·학습한다.

- 사용자가 L1→L2 지속 개발·학습을 다시 승인했고, L2가 한국시간 오전 5시 이전에 완료되면 L3도 허용했다. 이전의 추가 실행 금지는 해제됐다. 자동 모니터링 `soccer-l1-l2`를 ACTIVE로 재개했다.
- r014 고정 평가 병목: 소유 99.67%, 유효 슛 98.33%에 비해 성공 69%, 첫 슛 방향 성공 67.46%, 모든 킥 평균 방향 오차 93.84°. 같은 보상/환경 반복 학습보다 킥 방향과 타이밍의 물리 난도가 우선 문제다.
- 구현 중인 직접 기술: `SoccerNeuralKickAdvisor`. 확정 소유·공 1.8m·상대 골문 24m 안에서 실제 접촉 방향이 골대 안쪽 0.5m 여유 궤적일 때만 Neural에 킥을 권장하고, 같은 범위의 골문 밖 킥은 보류한다. 12m 안은 Controlled, 그 밖은 Strong을 권장하며 Neural이 이미 올바르게 선택한 힘은 유지한다. Human·Rule·fallback에는 적용하지 않는다. 최종 phase에서는 8m 운반 전 기술층을 끈다. 이것은 신경망이 독자 학습한 기술이 아니라 코드로 가르친 공통 기술이며 `Soccer/Skill Advice/*` 빈도를 결과에 명시한다.
- 현재 검증 순서: Python 모니터 10/10 통과 → Unity compile/EditMode/PlayMode/공통 Builder 검증 → 새 Windows Player → 변경 전 r014 checkpoint 300경기 고정 재평가. 효과가 확인되면 r014에서 초기화한 의미 있는 후속 Run 하나로 학습한다. 아직 새 Run은 시작하지 않았다.
- 첫 기술층 평가: 새 Player에서 r014 동일 checkpoint/seed 13579를 300회 평가해 67.67%로 기존 69%보다 개선되지 않았다. 첫 슛 방향 65.33%, 추천 184회, 골문 밖 킥 보류 3,517회였다. 원인은 기술층의 넓은 골대 여유 판정과 L1의 골대 안쪽 0.5m 판정 불일치, 18m 밖 첫 킥 누락으로 확인했다. 새 학습 Run은 시작하지 않고 같은 구현을 골대 안쪽/24m로 수정하며, 최종 phase는 8m 운반 전 기술층을 비활성화한다. 실패 평가 `Logs/L1-r015-advisor-r014-full-300.json`은 보존한다.
- 수정 기술층 평가: `Logs/L1-r015-advisor-v2-r014-full-300.json`, 동일 r014 checkpoint/seed 13579, 300회 complete/exit 0/SHA 불변. 성공 72.33%(기존 69% 대비 +3.33%p), 득점 74.33%, 소유 100%, 유효 슛 99.33%, 방향 슛 78.33%, 첫 슛 방향 75.17%, 평균 9.61초. 추천 180회, 골문 밖 킥 보류 6,789회. 방향 슛 기준은 넘었지만 최종 성공 75%는 미달이며 재접촉 9%가 일부 차이를 만든다. 효과가 확인돼 r014 최종 모델에서 r015 적응 학습을 시작한다.
- r015 계획: `CurriculumL1-20260904-r015`, 초기화 `Soccer4v4_Base-500136.pt`, `curriculum_l1_advised_poca.yaml`, 16 workers, 최대 500k/50k 저장/100k 진단. 보상·물리·배치·승급 기준은 r014와 동일하고 공통 슈팅 기술층만 의미 있는 변경이다. 재개에는 init_path 없는 `curriculum_l1_advised_resume_poca.yaml`만 사용한다. 종료 뒤 새 seed 고정 평가 전에는 최종 운반 phase로 승급하지 않는다.
- r015 실행: 16 workers가 정상 연결된 단일 Trainer로 시작했다. 50k checkpoint/ONNX 저장을 확인했고 처리량은 약 665 step/s, Player 16개, 메모리 약 61%/여유 약 12.15GiB, GPU 약 30%·633MiB·54°C로 과부하 징후가 없다. 50k까지 소유 100%, 유효 슛 평균 99.62%, 방향 슛 평균 86.63%, 성공 평균 79.24%지만 구간 변동으로 5회 연속 성공 기준은 아직 미달이다. 100k 첫 진단까지 설정을 유지한다. `Logs/L1-r015-latest.json`.
- r015 승인: Trainer는 500,004 스텝에서 exit 0으로 종료했다. 마지막 5요약은 성공 최저 77.8%/평균 84.9%, 소유 최저 98%, 유효 슛 최저 96%, 방향 슛 최저 88.9%, 최대 8.1초로 전 조건을 통과했고 일반 보상 누출은 0이다. 학습에 쓰지 않은 seed 97531 고정 300경기 평가도 성공 84%, 소유·유효 슛 99.67%, 방향 슛 90%, 득점 87.67%, 평균 5.81초로 통과했다. `Logs/L1-r015-final.json`, `Logs/L1-r015-eval-final-full-seed97531-300.json`. checkpoint `Soccer4v4_Base-500004.pt`, SHA-256 `8F71BF84F6E9C85461D6412FD924CDA33D27DEBA60FBE55780123F841FFBFDEE` 평가 전후 불변. 실패한 레슨 문자열 `1` 명령은 0경기에서 중단하고 `Logs/L1-r015-eval-wrong-lesson-tag-seed97531.json`으로 보존했다.
- r016 계획: 승인된 r015 최종 checkpoint에서 `curriculum_l1_carry_advised_poca.yaml`로 초기화한다. phase 2, 원래 배치, 8m 제어 운반 전 슈팅 기술층 비활성, 16 workers, 최대 1m/50k 저장/100k 첫 진단이다. 최근 5요약 성공 65%, 소유·유효 슛 85%, 방향 슛 75%, 평균 20초 이하와 새 seed 고정 300경기, L0 회귀를 모두 통과해야 L1 최종 승인이다. 재개는 init_path 없는 `curriculum_l1_carry_advised_resume_poca.yaml`만 사용한다.
- r016 진단/중단: 99,960 checkpoint를 저장하고 약 120k에서 단일 Trainer를 인터럽트했다. 100k 지표는 소유 100%지만 슛 시도 99.31%, 운반 충족 2.35%, 성공·유효 슛 1.11%, 평균 운반 1.84m였다. 일반 보상 누출은 0. 기존 근거리 정책이 운반 전에 즉시 차는 명확한 원인이라 같은 설정을 1m까지 반복하지 않는다. `Logs/L1-r016-first-diagnostic.json`, checkpoint SHA `4331C68E4F1CE9FBF3941DA1C506AEAC337842C79626729F5FC992196A8D21EE`를 보존한다.
- 직접 기술 수정: 최종 L1에서만 8m 운반 전 킥 branch 1/2를 action mask로 잠그고, 달성 후 킥과 Neural 슈팅 기술층을 함께 연다. 같은 선수가 잠깐 소유 판정에서 빠졌다가 다시 잡으면 운반 진행을 유지하고, 다른 Red가 소유하면 0m부터 다시 잠근다. 보상 수치·8m 거리·성공 기준·일반 경기/Human/Rule/fallback은 바꾸지 않는다. 해제 빈도는 `Soccer/Skill Advice/Carry Gate Unlocked`로 기록한다.
- r017은 위 직접 기술 변경을 포함한 새 Player에서 `curriculum_l1_carry_gated_poca.yaml`로 r015 승인 checkpoint를 다시 초기화하는 단일 비교 Run이다. r016의 학습된 조기 킥 습관은 이어받지 않는다. 16 workers, 최대 1m/50k 저장/100k 진단이며 재개는 `curriculum_l1_carry_gated_resume_poca.yaml`만 쓴다.

## 2026-09-03 컴퓨터 종료 전 인계 — 최우선

- 사용자 요청: 정상 실행 중인 작업까지만 마치고 추가 학습을 중단한다. 재부팅만으로 자동 재개하지 않으며, 사용자가 다시 진행을 요청할 때까지 새 Trainer/평가/빌드/자동화 재활성화를 하지 않는다. 아래 이전 실험 계획과 실행 명령은 이 중단 지시를 덮어쓰지 않는다.
- 마지막 Run `CurriculumL1-20260903-r014`: 학습 500,136 스텝 정상 종료(34301 exit 0), 이어진 고정 평가 300회 정상 종료(81726 exit 0). 강제 종료나 학습 프로세스 kill은 하지 않았다. 점검 시 `SoccerTraining`, `mlagents-learn`, `python` 실행 프로세스 없음. 자동화 `soccer-l1-l2`는 저장된 설정에서 `PAUSED` 확인.
- 고정 평가 `Logs/L1-r014-eval-final-full-300.json`: complete, seed 13579, 성공 207/300=69%, 소유 99.67%, 유효 슛 98.33%, 방향 슛 70.33%, 평균 10.26초. 첫 슛 295회 중 방향 슛 67.46%, 평균 오차 48.33°. 최근 5학습 요약/고정 평가 모두 근거리 승인 기준 미달이므로 L1 최종 운반과 L2는 아직 미완료다. seed가 다른 이전 결과와의 차이를 확정적인 개선으로 주장하지 않는다.
- 핵심 모델: `results/CurriculumL1-20260903-r014/Soccer4v4_Base/Soccer4v4_Base-500136.pt`. SHA-256 `5FFB8CF83F661842994421320098E986F6DE5666709B3E9B3B4B97639471F6BA` 평가 전후 동일. root `Soccer4v4_Base.onnx`와 모든 중간 `.pt/.onnx`, TensorBoard event, configuration.yaml, Player 로그 보존. ONNX checker 통과, ProjectSettings/EditorBuildSettings 추가 변경 없음.
- 종료 백업 완료: `Logs/Shutdown-20260903-r014`, 774개 파일/317,065,775 bytes(약 302MiB). 현재 Soccer Assets(.meta/모델 포함), Tools, docs, ProjectSettings, 패키지 manifest/lock, 현재 Windows Player, r014 결과 전체, 관련 검증/평가 로그를 추가 복사했다. `manifest.json`에 전체 파일 SHA-256을 기록했고 원본과 774개 모두 일치했다(저장 스크립트 79605 exit 0). 이 완료 확인 문장만 백업 후 현재 문서에 추가했다. 기존 원본/이전 Run은 이동·삭제·덮어쓰기하지 않았다. 같은 디스크의 추가 사본이므로 별도 장치 재해 복구 백업을 뜻하지 않는다.
- 재부팅 후 재개 지점: 이 절과 `training/staged-reward-plan.md`부터 읽고 사용자 재개 요청을 확인한다. r014는 이미 max_steps를 마쳤으므로 기존 초기화 YAML을 재실행하거나 무조건 `--resume`하지 않는다. 현재 모델과 평가 자료를 보존한 채 슛 방향/킥 선택 타이밍을 진단하고 다음 실험의 단일 변경 가설을 정한 뒤 새 Run으로 진행한다. 필요하면 저장된 `.pt`에서 가중치를 이어받으며 처음부터 무작위 학습할 필요는 없다. r015는 생성/시작하지 않았다. 자동 모니터링 재활성화도 재개 요청 이후에만 한다.

## 2026-09-03 L1/L2 자율 학습 작업

- r014 종료: Trainer 34301 exit 0, 최종 500,136 checkpoint/ONNX/root ONNX 및 configuration.yaml 저장, ONNX checker 통과. 최종 checkpoint SHA `5FFB8CF83F661842994421320098E986F6DE5666709B3E9B3B4B97639471F6BA`. 종료 후 학습 Player 0개 확인. 기존 중간 checkpoint를 모두 유지한다.
- r014 종료 판단: 최근 5요약 성공 70.58%, 소유 100%, 유효 슛 98.06%, 방향 슛 72.72%, 평균 9.47초, 일반 보상 누출 0. 전체 학습 요약을 검사했으나 연속 기준을 통과한 구간은 없었다. 최고 5구간 평균은 470k의 75.93%이지만 그 안의 최저 구간은 71.88%로 미달이다. 평균만으로 승급하지 않는다. 첫 슛 평균 오차 46.59°이며 공 소유보다 슛 방향/타이밍을 후속 분석한다.
- 현재 단계: 고정 정책 300회 평가 완료, 사용자 요청으로 저장 후 일시중지. `Logs/L1-r014-eval-final-full-300.json`(81726 exit 0, seed 13579, 난이도 1/alignment 1/phase 1), 원본 500,136 strict 로드/optimizer 없음/SHA 불변 확인. 성공 69%. 추가 Run을 시작하지 않는다.
- r014 첫 진단 저장: `Logs/L1-r014-first-diagnostic.json`은 `--at-step 100000`으로 이후 자료를 배제한 실제 100k 요약이다. 최근 5구간 성공 평균 67.85%, 소유 100%, 유효 슛 99.31%, 평균 10.88초, 일반 보상 누출 0. 원래 배치 75% 연속 기준 미달이므로 승급하지 않는다. 첫 슛 평균 오차 52.29°/방향 슛 66.19%로 방향 대응을 계속 관찰한다. 과거 요약의 host 항목은 검사 시점 현재값이며 과거 부하가 아니다.
- r014 후속 점검(230k): Trainer 34301 및 Player 16개 정상 진행, 49,984/99,983/149,880/199,944 checkpoint 보존. 최근 5구간 성공 평균 64.64%, 소유·유효 슛·시간 조건 통과, 일반 보상 누출 및 Player 로그 예외 검색 결과 0. RAM 62%/여유 11.88GiB, GPU 59°C로 과부하 징후 없음. 500k 계획을 유지하고 종료 후 새 seed 13579 고정 300회 평가로 판단한다. `Logs/L1-r014-latest.json`은 후속 점검마다 갱신한다. 1분 heartbeat ACTIVE 확인, 현재 사용자 조치 불필요.
- r014 실행 이력: `CurriculumL1-20260903-r014` 원래 배치(난이도 1), 16개 Player 및 r013의 승인 149,876 checkpoint 초기화 경로 확인. 최대 500k/50k 저장/100k 진단, 보상·물리·Player 변경 없음. 초기화/재개 YAML parser와 모니터 테스트 10/10 통과. 소스/설정/build-info는 `Logs/CurriculumL1-20260903-r014-source` 보존. 완료 Run을 재실행하지 않는다.
- r013 평가 완료(seed 24680, 각 300회, 네 실행 모두 exit 0/원본 SHA 불변): 149,876 모델 중간 81.0%/원래 65.0%, 최종 200,148 모델 중간 79.67%/원래 62.0%. 차이가 작아 통계적으로 확실한 성능 우위를 단정하지 않는다. 149,876은 실제 연속 요약도 통과했으므로 이 후보를 선택한다. 중간 평가 소유 100%/유효 슛 98%/6.34초, 원래 배치 소유 100%/유효 슛 99%/11.33초. 원래 배치의 75% 성공 기준은 아직 미달이다. 병목은 여전히 슛 방향 대응이며 공 발견/소유 실패가 아니다.
- 선택 원본 SHA: `96C237314C170240ACB00194BB1D9614F448875AD1F7E0201F683F32FED21C6C`. r013 종료 모델 및 모든 중간 파일도 보존한다. r014 초기화 YAML은 `curriculum_l1_full_poca.yaml`, 재개는 init_path 없는 `curriculum_l1_full_resume_poca.yaml`. 최대 500k이고 연속 기준 미달이면 종료 모델만으로 승급하지 않는다. 최종 승인 평가 seed 13579(새 300회)를 사용하고 이전 seed 24680 비교는 별도 검증 자료로 취급한다.
- r013 종료 진단: 마지막 5요약 성공 평균 77.37%로 개별 구간 미달 때문에 중간 단계 연속 기준 불충족. 반면 150k 당시 5요약은 평균 85.16%로 모두 통과했다. 최신 모델만 고르지 않고 149,876 후보를 함께 평가한다. `Logs/L1-r013-at150k.json`, `Logs/L1-r013-latest.json`. 모니터에 `--at-step`을 추가해 이후 지표를 섞지 않고 과거 요약을 검증한다. host 정보는 현재값임을 별도 표시하며 테스트 10/10 통과.
- r013 고정 평가 증거: `Logs/L1-r013-eval-150k-medium-300.json`, `Logs/L1-r013-eval-150k-full-300.json`, `Logs/L1-r013-eval-final-medium-300.json`, `Logs/L1-r013-eval-final-full-300.json` 모두 완료. 중간 단계 통과는 L1 최종 운반/득점 통과가 아니다.
- r012 종료/분석 완료: 500,008 스텝 정상 종료(87816 exit 0), 최종 checkpoint/ONNX 및 root ONNX 저장. 마지막 5요약 정면 성공 평균 89.69%, 소유 100%, 유효 슛 95.02%, 방향 슛 90.15%, 평균 3.29초, 보상 누출 0. 최종 고정 정책(seed 24680, 각 300회)은 정면 89.33%, 중간 83.33%, 원래 63.0%. 세 평가 모두 완료/원본 SHA 불변. 99,988 정책의 같은 평가 seed 대비 정면 +9%p, 원래 +3.33%p다. 원래 배치 75% 기준에는 못 미쳐 최종 운반으로 승급하지 않는다. 준비 과제의 반복보다 각도 대응 전이가 필요한 것으로 판단한다.
- r013 계획: 공 좌우 범위 0 → ±0.75m만 확대. `curriculum_l1_angle_poca.yaml` 초기화 / `curriculum_l1_angle_resume_poca.yaml` 재개, 최대 200k·100k 진단·50k 저장·16 workers. 이미 중간 배치 83.33%이므로 우선 짧게 보강하고 종료 후 중간/원래 배치를 각각 300회 평가한다. 원래 배치의 실제 5요약/독립 기준 충족 전에는 난이도 1 준비를 생략하지 않는다. YAML Trainer parser 및 모니터 검사 9/9 통과, 런타임 소스 hash가 r012 빌드 스냅샷과 같음을 확인했다. YAML-only 변경이라 Player를 다시 빌드하지 않는다.
- r013 100k 진단: 49,890 및 99,900 checkpoint/ONNX 저장. 최근 5요약 성공 평균 79.70%, 80k 구간 70.59% 때문에 연속 승급 조건 미달. 소유/유효 슛/시간 조건 충족, 일반 보상 누출/Player 예외 0. 약 600 step/s, RAM 64%/여유 11.2GB, GPU 60°C. `Logs/L1-r013-first-diagnostic.json`(실제 summary 100k). 현재 설정을 유지하여 예정한 200k 종료 후 평가한다.
- r012 최종 평가 증거: `Logs/L1-r012-eval-final-easy-300.json`, `Logs/L1-r012-eval-final-medium-300.json`, `Logs/L1-r012-eval-final-full-300.json`(각 300회 완료). 원본 SHA `6506FD7AAE97D6EEB9EFCFC9B9AFCFBCD6D3E4B5EB813389026D7CC55F95A68E`, 최종 ONNX checker 통과. r012 Run을 재실행/덮어쓰기하지 않는다.
- r011 최종 고정 평가(seed 98765, 300회): 성공 58.67%(176/300), 소유 100%, 슛 시도/유효 슛 96.33%, 방향 슛 60.33%, 평균 12.31초, 조준 보상 평균 0.00124. `Logs/L1-r011-eval-500k-300.json`; SHA `676527c1ea3808ec97e8f778ccc8655fc6253140c0013a6ce36a3c030db9bb60` 불변. r010 750k의 55.67% 대비 +3%p로 확실한 개선/승급 근거는 부족하다. 최근 5학습 요약 성공 평균 52.91%, 일반 보상 누출 0. 쉬운 준비 단계를 설계하되 성공 기준을 추가 완화하지 않는다.
- r012 설계: r011 최종 모델 초기화, 보상 수치·물리·거리·성공 판정 그대로, 근거리 공 좌우 시작 범위만 정면(0)부터 단계 확대. 16 workers, 100k 진단/최대 500k/50k 저장. `soccer_l1_spawn_difficulty` 기본 1, 최종 운반은 항상 1. 첫 슛 통계를 별도로 수집하고 승급 검사에서 준비 단계/본과제를 구분한다. 사전 평가에서 쉬운 배치를 이미 해결하면 불필요한 반복을 건너뛰고 중간 난이도를 선택한다.
- r012 검증: Soccer EditMode 71/71, PlayMode 18/18 및 정상 exit 0; Python 승급 검사 9/9. `Logs/L1-r012-EditMode.xml`, `Logs/L1-r012-PlayMode.xml`. 다섯 Stadium/L0/L1 계약 검사와 Windows 빌드 성공(2026-09-03T11:26:02Z, 오류 0, 기존 shader warning 485). `Logs/L1-r012-Build.log`. ProjectSettings define/EditorBuildSettings 원상 유지 확인. 소스/설정/검사/build-info는 `Logs/CurriculumL1-20260903-r012-source`, 이전 r011 전체 Player는 `Builds/SoccerTraining-r011` 보존.
- r012 사전 평가(모두 r011 500,388 고정 정책, seed 98765, 각 300회, alignment=1): 정면 78.33%/소유 99.67%/유효 슛 93.67%/방향 슛 81%/5.98초; 중간 ±0.75m 70%; 원래 ±1.5m 53.67%. `Logs/L1-r012-before-easy-300.json`, `Logs/L1-r012-before-medium-300.json`, `Logs/L1-r012-before-full-300.json`, 모두 exit 0/원본 SHA 불변. 추가 학습 성적이 아니라 과제 난이도 기준선이다. 원래 배치의 r011 이전 평가 58.67%와 이번 53.67%는 별도 실행의 변동으로 보존하며 평균으로 덮어쓰지 않는다. 같은 seed도 실제 Player의 물리 실행을 bitwise 동일하게 보장하지 않는다.
- 선택: 정면 과제가 78.33%로 아직 안정적 완성은 아니므로 계획대로 난이도 0에서 먼저 보강한다. 중간 단계도 70%로 미달이다. 첫 슛 평균 각도 오차는 정면 약 38°, 중간 약 42°로, 정면 스폰만으로 모든 실제 슛이 정렬되는 것은 아니다. 시도 순간의 방향 개선 여부를 후속 결과와 비교한다.
- r012 첫 진단: 49,936 및 99,988 checkpoint/ONNX 저장. 120k 최근 5요약 성공 평균 82.40%, 소유/유효 슛/시간 조건 충족, 일반 보상 누출 0. 100k 구간 성공 68.75% 때문에 현재 준비 단계 연속 조건은 미달이며 승급하지 않는다. `Logs/L1-r012-first-diagnostic.json`(실제 summary 120k). 약 548 step/s, RAM 64%/여유 11.1GB, GPU 59°C(학습 16개 + 고정 평가 2개 동시 실행 시점).
- r012 중간 평가 이력: 99,988 모델, seed 24680. 정면 300회 성공 80.33%, 소유 99.67%, 유효 슛 95.33%, 평균 5.99초. 본과제 300회 성공 59.67%. `Logs/L1-r012-eval-100k-easy-300.json`, `Logs/L1-r012-eval-100k-full-300.json` 모두 완료/원본 SHA 불변. 사전 평가 seed 98765와 다르므로 동일 경기의 paired 개선으로 해석하지 않는다.
- r012 150k 시점 이력: 최근 5요약 성공 평균 85.70%로 준비 연속 조건을 다시 통과했고 149,980 checkpoint/ONNX를 저장했다. 난이도 0의 `training_gate_passed`는 false였다. 이후 500,008에서 정상 종료하고 위 최종 평가를 수행했다.
- r012 중단 시도 이력: PTY Ctrl+C 두 요청에 Trainer가 종료되지 않았지만 강제 kill하지 않고 설정된 500k까지 정상 종료시켰다. 세션 87816은 종료됐다. 이후 Run도 종료가 필요하면 먼저 체크포인트를 보존하며 broad kill을 사용하지 않는다.
- r010 최종 독립 평가(seed 98765, 각 300회): 749,904 checkpoint 성공 55.67%/소유 99.33%/방향 슛 56.67%; 1,500,436 checkpoint 성공 55.0%/소유 100%/방향 슛 57.67%. `Logs/L1-r010-eval-750k-300.json`, `Logs/L1-r010-eval-1500k-300.json`. 학습 마지막 5요약 성공 평균 45.25%, 일반 보상 누출 0. 승급하지 않는다.
- r011 가설: 근거리 첫 슛 이전 조준 오차 개선에만 0.002/도, 에피소드 cap 0.08을 지급. 첫 기준 상태·정지·악화·기존 최소 오차로 되돌아오기에는 보상 없음. 안정 소유/킥 가능 상태만, 재소유 때 cap 초기화 안 함. 기본 feature parameter 0이며 r011만 1. 기존 슛 물리·스폰·75% 승급 기준 불변. [보상 기준표](../../soccer/rewards.md)와 README/테스트 갱신.
- r011 초기화: r010의 불변 `Soccer4v4_Base-749904.pt`를 YAML `init_path`로 지정. 16 workers, 100k 진단, 최대 500k 한정 학습/50k 저장. 초기화 YAML을 그대로 --resume에 쓰면 안 되며 재개 때는 init_path 없는 YAML이 필요하다.
- r010 Player 보존: `Builds/SoccerTraining-r010` 전체 복사. 새 공유 Player를 만들기 전 완료한 두 평가의 원본 checkpoint SHA는 유지됨.
- r009는 65,626 스텝으로 보존. 최근 유효 득점 0%, 승급 불가.
- 보상 세 지급 경로에 단계별 허용 목록 추가. 일반 운반·패스·공격·패널티 누출 차단, 기존 일반 경기 Profile/안전 규칙 유지.
- 개인 reward 기반 자동 승급을 고정 phase + 성공률 평가로 대체. 근거리 75%, 최종 L1 65%로 각 5%p 완화.
- 32 worker 우선 시험. 초기 host: i7-14700HX 20코어/28스레드, RAM 약 32GB/여유 20GB, RTX 4060 Laptop 8GB, 외부 전원 연결.
- [단계별 보상 계획](../player-curriculum/staged-reward-plan.md). 검증·실행·고정 모델 평가 결과는 완료 시 이 절에 갱신한다.
- 검증: 최종 Soccer EditMode 68/68 (`Logs/L1-r010-Final-EditMode.xml`); 최종 코드의 Soccer 전체 PlayMode 18/18 및 정상 종료 (`Logs/L1-r010-Final-AllSoccer-PlayMode.xml`). 실제 Player에서 r010 학습과 별도 고정 모델 평가를 실행했다. 두 단계/연장 YAML은 설치 Trainer의 `parse_command_line`에서 로드 성공.
- r010 Windows 빌드: 성공/오류 0 (`Logs/L1-r010-Build.log`, build-info 2026-09-03T03:03:36Z). 소스 스냅샷 `Logs/CurriculumL1-20260903-r010-source`.
- 32 worker 진단: 최근 약 641 step/s, RAM 81%/여유 5.7GB, GPU 약 54°C/633MiB. 일반 전술 보상 누출 0. 110k 최신 성공 44.4%, 최근 5개 평균 17.3%로 승급 불가 (`Logs/L1-r010-32env.json`).
- 16 worker 비교(190k): 약 629 step/s, RAM 61%/여유 12.2GB, GPU 53°C. 32의 처리량 이점 약 2%에 비해 메모리 부담이 커 16 유지. 이는 순차 실행 구간 비교이며 동일 고정 정책의 엄밀한 벤치마크는 아니다. 최근 5개 성공 평균 16.1%, 보상 누출 0 (`Logs/L1-r010-16env.json`).
- 평가 도구: `Tools/evaluate_soccer_policy.py`는 optimizer 없이 고정 checkpoint의 Policy를 strict 로드하여 실제 Player를 제어하며, 다른 seed와 LSTM 종료 초기화를 사용한다. 원본 SHA-256을 전후 비교하고 25에피소드마다 부분 결과를 저장한다. 25회 smoke 정상 완료.
- r010 299,936 고정 checkpoint 평가(100회, seed 54321): 성공 26%, 소유 100%, 슛 시도 73%, 방향 슛 28%, 전체 골 56%, 재접촉 21%, 평균 15.70초. 킥 가능 1,350 decision 중 선택 387회. 파일 `Logs/L1-r010-eval-300k.json`; Policy를 수정하지 않았으며 ONNX checker 통과. 아직 승급 불가, 학습 지속.
- r010 499,896 고정 checkpoint 평가(동일 seed, 별도 100회): 성공 45%, 소유 100%, 슛 시도 94%, 방향 슛 46%, 전체 골 55%, 재접촉 17%, 평균 15.55초. `Logs/L1-r010-eval-500k.json`. 유효 골과 방향 슛 비율이 거의 같아 현 시점의 주요 병목은 공 발견보다 슛 방향/마무리다. 730k 최근 5개 요약 성공 평균 46.7%, 최신 53.3%. 아직 근거리 승급 기준 미달.
- 749,904 / 1,000,484 고정 checkpoint 각 100회 평가는 성공 56% / 45%. `Logs/L1-r010-eval-750k.json`, `Logs/L1-r010-eval-1m.json`. 1m를 더 좋은 모델로 승인하지 않는다. 1m 시점 Run 전체를 `Logs/CurriculumL1-20260903-r010-at1m`에 별도 복사 보존했다. 초기 keep_checkpoints=21 정책으로 가장 오래된 50k 파일은 Trainer가 순환 정리했으며, 남은 중간 모델은 이 스냅샷과 results에 보존한다. 연장은 keep_checkpoints=65다.
- 다음 판단(사용자 재개 요청 이후): 완료된 r014 최종 평가와 첫 슛 방향/선택 타이밍을 검토해 동일 목표의 다음 단일 가설 실험을 설계한다. 근거리 연속/고정 평가 기준 미달이므로 즉시 운반 phase로 승급하지 않는다. 성공 기준을 추가 완화하거나 같은 설정을 무기한 연장하지 않는다. 지금은 추가 실행 금지.
- 추가 검증: 최종 공통 환경 PlayMode 필터 6/6 (`Logs/L1-r010-Final-PlayMode.xml`), 모니터 승급 검사 6/6 (`Tools/test_soccer_training_monitor.py`). 초기 검증용 Editor는 시작 전 대기해 재실행했고 재실행은 정상 종료. 학습 Player 오류가 아니다.
- 자동 후속 점검: 2026-09-03 사용자 요청에 따라 `soccer-l1-l2` heartbeat를 1분 간격 ACTIVE로 등록했다. 기존 항목은 업데이트 시 앱에 없다고 반환되어 재생성했으며 저장된 automation.toml에서도 일정/ACTIVE를 확인했다. 직접 실행 중 약 30초 간격으로 점검하고, 응답 종료 후 1분 간격으로 이어간다. 주기적 로그/프로세스 점검이지 매 순간 연속 관측 보장은 아니다. 단순 진행은 조용히 감시하고 종료·오류·개선 조치·단계 전환만 짧게 알린다. PC와 앱이 실행 중이어야 하며 사용량/권한 제한으로 후속 실행이 지연될 수 있다. L1 최종 승인/L0 회귀 전 L2 학습 금지, 두 단계 검증 완료 또는 사용자 중단 요청 시 중지.
- r010 연장 종료: 세션 `42891` exit 0, 최종 1,500,436. configuration.yaml은 종료 시 1.5m/65개로 갱신됐다. `Logs/L1-r010-latest.json`은 1.5m 결과이며 승급 미달/누출 0이다. r011 새 실행 전에 중복 Trainer가 없음을 확인했다.
- 마지막 Unity 검증 종료 뒤 `SENTIS_ANALYTICS_ENABLED`를 포함한 기존 Standalone define 및 EditorBuildSettings가 유지됨을 diff로 확인했다. 변경 문서 링크와 대상 diff whitespace 검사 통과.
- 설치 Trainer는 `learn.py`의 학습 종료 `finally`에서 `configuration.yaml`을 기록한다. 따라서 연장 실행 중 results의 configuration은 이전 1m 값일 수 있으며, 현재 실행 인수/시작 로그와 `curriculum_l1_extended_poca.yaml`(1.5m/65개)을 기준으로 확인한다. 종료 시 갱신된다. 소스 YAML 사본은 `Logs/L1-r010-extension-1500k.yaml`에도 보존한다.
- r011 검증 완료: Soccer EditMode 70/70, 전체 Soccer PlayMode 18/18 정상 종료 (`Logs/L1-r011-EditMode.xml`, `Logs/L1-r011-PlayMode.xml`). 다섯 Stadium parity/Profile 검증 및 Windows Build 성공, 오류 0 (`Logs/L1-r011-Build.log`, build-info 2026-09-03T04:10:06Z). 기존 shader warnings 485개. 테스트 종료 후 Standalone `SENTIS_ANALYTICS_ENABLED` 복구. 초기화/재개 YAML은 init_path만 다른 것을 실제 Trainer parser로 확인했다.
- r011 Player smoke: 아직 학습하지 않은 750k 후보를 새 빌드/조준 parameter 1로 50회 평가, 성공 58%, 실제 조준 보상 에피소드 평균 0.00312/합계 0.15594 (`Logs/L1-r011-alignment-smoke.json`). 이는 기능 동작 확인이지 새 학습 개선 증거가 아니다. 소스/YAML/build-info는 `Logs/CurriculumL1-20260903-r011-source`에 저장했다.
- r011 첫 진단(실제 120k): 최근 5개 요약 성공 평균 42.59%, 소유 98.82%, 유효 슛 93.56%, 방향 슛 43.84%, 에피소드 16.24초, 조준 보상 0.00204/에피소드. 아직 성능 개선 증거는 없고 승급 미달이다. 일반 보상 누출/치명 오류 0, 약 635 step/s, RAM 62%/여유 12GB, GPU 52°C. 99,936 checkpoint 저장 확인, 원본 749,904 checkpoint SHA 유지. `Logs/L1-r011-first-diagnostic.json`. 예정한 500k 한정 학습은 유지하며 이후 고정 300경기로 판단한다.
- 진단 해석: `Shot Error Degrees`와 `Strong Shot Fraction`은 모든 실제 킥 사건 평균이고, `Shot On Target`은 에피소드 중 한 번이라도 방향 슛이 있었는지의 비율이다. 두 통계의 모집단이 다르므로 직접 역수/상보 비율로 해석하지 않는다.

### r014 원래 배치 실행과 안전한 재개

실행 중인 Trainer가 없을 때만 초기 실행한다.

```powershell
Set-Location C:\GitHub\Machine-Learning
& 'C:\Users\USER\miniconda3\envs\mlagents\Scripts\mlagents-learn.exe' 'Assets\_Soccer\Curriculum\L1_CarryAndShoot\Training\curriculum_l1_full_poca.yaml' --run-id CurriculumL1-20260903-r014 --results-dir results --env Builds\SoccerTraining\SoccerTraining.exe --num-envs 16 --base-port 5605 --seed 12345 --torch-device cuda --timeout-wait 120 --no-graphics --env-args --training-profile curriculum-l1 -job-worker-count 1
```

재개는 YAML을 `curriculum_l1_full_resume_poca.yaml`로 바꾸고 `--resume`을 추가한다. 진단은 `Tools/inspect_soccer_training.py --run-id CurriculumL1-20260903-r014 --output Logs/L1-r014-latest.json`. 과거 체크포인트 근처 요약은 `--at-step 150000`처럼 지정하되 그 시점 host 자원은 복원되지 않는다. 고정 승인 평가는 `--alignment 1 --spawn-difficulty 1 --seed 13579 --episodes 300`으로 수행한다.

### r013 완료 명령(이력)

r013은 완료됐으므로 아래 초기 명령을 다시 실행하지 않는다.

초기 실행(이미 실행 중이면 중복 시작 금지):

```powershell
Set-Location C:\GitHub\Machine-Learning
& 'C:\Users\USER\miniconda3\envs\mlagents\Scripts\mlagents-learn.exe' 'Assets\_Soccer\Curriculum\L1_CarryAndShoot\Training\curriculum_l1_angle_poca.yaml' --run-id CurriculumL1-20260903-r013 --results-dir results --env Builds\SoccerTraining\SoccerTraining.exe --num-envs 16 --base-port 5605 --seed 12345 --torch-device cuda --timeout-wait 120 --no-graphics --env-args --training-profile curriculum-l1 -job-worker-count 1
```

중단 후 재개는 YAML만 `curriculum_l1_angle_resume_poca.yaml`로 바꾸고 `--resume`을 추가한다. 진단은 `Tools/inspect_soccer_training.py --run-id CurriculumL1-20260903-r013 --output Logs/L1-r013-latest.json`. 고정 평가에는 `--alignment 1 --spawn-difficulty 0.5`(중간) 또는 `--spawn-difficulty 1`(본과제)을 명시한다.

### r012 완료 명령(이력)

r012는 완료됐다. 아래 명령은 이력이며 재실행하지 않는다.

초기 실행은 `curriculum_l1_straight_poca.yaml`(r011 500,388 init_path 포함), 재개는 `curriculum_l1_straight_resume_poca.yaml`(init_path 없음)이다. 아래 초기 명령은 이미 실행했다. 실행 중인 Trainer가 있으면 중복 시작하지 않는다.

```powershell
Set-Location C:\GitHub\Machine-Learning
& 'C:\Users\USER\miniconda3\envs\mlagents\Scripts\mlagents-learn.exe' 'Assets\_Soccer\Curriculum\L1_CarryAndShoot\Training\curriculum_l1_straight_poca.yaml' --run-id CurriculumL1-20260903-r012 --results-dir results --env Builds\SoccerTraining\SoccerTraining.exe --num-envs 16 --base-port 5605 --seed 12345 --torch-device cuda --timeout-wait 120 --no-graphics --env-args --training-profile curriculum-l1 -job-worker-count 1
```

중단 재개는 위에서 YAML만 `curriculum_l1_straight_resume_poca.yaml`로 바꾸고 `--resume`을 추가한다. 기존 Run에 초기화 YAML을 다시 넣지 않는다. 모니터는 `Tools/inspect_soccer_training.py --run-id CurriculumL1-20260903-r012 --output Logs/L1-r012-latest.json`. 평가는 불변 번호 체크포인트와 `--alignment 1 --spawn-difficulty 0`(준비) 또는 `--spawn-difficulty 1`(본과제)을 명시한다.

### r011 종료 명령(이력)

r011 500k는 완료됐다. 아래는 이력이며 재실행하지 않는다. 초기 실행은 `curriculum_l1_alignment_poca.yaml`(불변 원본 init_path 포함)을 사용했고 재개 YAML은 init_path를 제외한다.

```powershell
Set-Location C:\GitHub\Machine-Learning
& 'C:\Users\USER\miniconda3\envs\mlagents\Scripts\mlagents-learn.exe' 'Assets\_Soccer\Curriculum\L1_CarryAndShoot\Training\curriculum_l1_alignment_resume_poca.yaml' --run-id CurriculumL1-20260903-r011 --results-dir results --env Builds\SoccerTraining\SoccerTraining.exe --num-envs 16 --base-port 5605 --seed 12345 --torch-device cuda --timeout-wait 120 --no-graphics --resume --env-args --training-profile curriculum-l1 -job-worker-count 1
```

100k 진단은 `Tools/inspect_soccer_training.py --run-id CurriculumL1-20260903-r011 --output Logs/L1-r011-latest.json`. 평가에는 `Tools/evaluate_soccer_policy.py`의 `--alignment 1 --seed 98765 --episodes 300`과 불변 `.pt` 경로를 지정한다. 학습 중인 `checkpoint.pt` 자체를 평가 대상으로 사용하지 않는다.

### r010 종료 시 사용한 학습 명령(이력)

이 명령의 1.5m 실행은 완료되었다. 현재 후속 실험은 r011이며 이 명령을 다시 시작하지 않는다. 이 실험용 YAML은 기본 Profile manifest YAML을 바꾸지 않으므로 직접 Trainer 명령을 사용했다.

```powershell
Set-Location C:\GitHub\Machine-Learning
& 'C:\Users\USER\miniconda3\envs\mlagents\Scripts\mlagents-learn.exe' 'Assets\_Soccer\Curriculum\L1_CarryAndShoot\Training\curriculum_l1_extended_poca.yaml' --run-id CurriculumL1-20260903-r010 --results-dir results --env Builds\SoccerTraining\SoccerTraining.exe --num-envs 16 --base-port 5605 --seed 12345 --torch-device cuda --timeout-wait 120 --no-graphics --resume --env-args --training-profile curriculum-l1 -job-worker-count 1
```

## 한눈에 보기

| 영역 | 현재 상태 |
| --- | --- |
| 활성 경기장 | 다섯 팀 모두 `Stadium4v4_*` Scene과 `StadiumEnvironment_*` Prefab 사용 |
| Build Settings | Base, Attack, Defense, Press, Rule Stadium Scene만 포함 |
| 프로젝트 기본 Scene | `Assets/_Soccer/Core/Scenes/Stadium4v4_Base.unity` |
| 공통 경기 규칙 | 5분 경기, Goal Pause·Reset, HUD, Human/AI 전환 유지 |
| Stadium 크기 | 긴 축 124m, Half Length 62m, Half Width 약 42.327m |
| Stadium 물리 | 공 scale `0.012705`, 굴림 각속도, 반지름 포함 경계 제한, 모서리 완화 벽, Goal Floor 유지 |
| Kick | Controlled `2000`, Strong `5000` |
| 정책 계약 | 전체 입력 379, Action Branch `[3,3,3,3]`, 기존 Behavior 이름 유지 |
| 팀 표시 | TeamId 0은 Red, TeamId 1은 Navy. 선수·골대·킥 플레이트·HUD에 공통 적용 |
| 팀 전술 | Attack·Defense·Press·Rule의 TeamDefinition·RewardProfile·Rule FSM 연결 유지 |
| 공유 훈련 Build | `Builds/SoccerTraining/SoccerTraining.exe`, L0/L1 포함 학습 7종 + Rule 평가 1종 |
| 병렬 학습 | `Tools/Train-Soccer.ps1`에서 최대 32 worker; r010 처리량·메모리 비교 후 후속 실험은 16 사용 |
| 전술 상대 안전장치 | Attack·Defense·Press는 현재 Build에 Base v2 Navy Model이 없어 기본 시작 차단 |
| 기존 경기장 | 자산은 이력 보존용으로 남아 있지만 Build Settings와 기본 실행 경로에서는 사용하지 않음 |
| v2 ONNX | L0 승인 및 L1 실험 체크포인트 보존. 최종 L1/L2와 완전 경기용 Base 모델은 아직 미승인 |

## 활성 Stadium 자산

| 유형 | Scene | Environment Prefab | 학습 제어 |
| --- | --- | --- | --- |
| Base fallback | `Assets/_Soccer/Core/Scenes/Stadium4v4_Base_FallbackTraining.unity` | Base Prefab instance | Red 학습, Navy 규칙 fallback 강제 |
| Base | `Assets/_Soccer/Core/Scenes/Stadium4v4_Base.unity` | `Assets/_Soccer/Core/Prefabs/StadiumEnvironment_Base.prefab` | Red·Navy |
| Attack | `Assets/_Soccer/Teams/Attack_KMW/Scenes/Stadium4v4_Attack.unity` | `Assets/_Soccer/Teams/Attack_KMW/Prefabs/StadiumEnvironment_Attack.prefab` | Red |
| Defense | `Assets/_Soccer/Teams/Defense_PJH/Scenes/Stadium4v4_Defense.unity` | `Assets/_Soccer/Teams/Defense_PJH/Prefabs/StadiumEnvironment_Defense.prefab` | Red |
| Press | `Assets/_Soccer/Teams/Press_KMG/Scenes/Stadium4v4_Press.unity` | `Assets/_Soccer/Teams/Press_KMG/Prefabs/StadiumEnvironment_Press.prefab` | Red |
| Rule | `Assets/_Soccer/Teams/Rule_PHC/Scenes/Stadium4v4_Rule.unity` | `Assets/_Soccer/Teams/Rule_PHC/Prefabs/StadiumEnvironment_Rule.prefab` | 학습 없음, Red 4명 Rule FSM |

각 팀 Scene과 Prefab은 고유 GUID를 사용한다. 공통 Stadium 지형·골대·벽·센서·HUD·공·카메라 계약은 Core Base Stadium에서 생성하며 팀별 전술, RewardProfile, TeamDefinition, 학습 플래그만 각 작업공간에 연결한다. Scene 임시 Model Override는 기존과 같이 비어 있고 영구 Model 참조는 TeamDefinition이 소유한다.

## Stadium 기본 계약

- 관찰 위치 정규화, 목표 위치 제한, Rule 목표, 수비 걷어내기 판단은 활성 `SoccerArenaGeometry`를 사용한다.
- Geometry가 없을 때도 기존 직사각형 경기장 수치가 아니라 Stadium 상수로 fallback한다.
- Demo 골대는 Red `#D33F4E`, Navy `#2F5C9C`로 표시한다. 선수가 해당 골문 안에 있고 실제 골대 Collider가 카메라와 선수를 가로막을 때만 RGB를 유지한 채 반투명화한다. 연회색 Goal Floor도 다섯 환경에서 공통 사용한다.
- 선수 몸통은 추가 HSV 조정으로 Red 채도를 약 10% 높인 `#A00218`, Navy 채도·밝기를 각각 약 15% 높인 `#002770`이다. Navy 채도는 계산 결과 100%에서 제한되며 킥 플레이트와 HUD는 기존 팀 색 계열을 유지한다.
- 하늘색 반투명 벽은 직선부와 모서리부 두께가 같고 Ray Sensor의 `wall` 대상으로 인식된다.
- 네 모서리는 높이가 외곽 벽과 같은 1.5m 판 3개씩, 총 12개로 안쪽을 완화한다.
- 공은 Stadium 경계 안에서만 움직이며 평면 이동량에 맞춰 굴러가는 각속도를 적용한다.
- 보상 패널은 Stadium에서 우측 하단에 배치된다.
- 다섯 Stadium은 도로·주차장 27개, 관목·꽃 104개, 외부 도시 소품 42개를 모두 제외한다. 공급자 원본 파일과 Terrain은 보존한다.

세부 수치는 [경기 계약](../../soccer/gameplay-contract.md), 생성·검증 절차는 [설정과 검증](../../project/setup-and-validation.md)을 따른다.

## 2026-08-31 검증 결과

| 단계 | 결과 | 증거 |
| --- | --- | --- |
| 선명한 Red·Navy 선수와 외부 장식 제거를 포함한 Stadium 전체 재생성 | 통과 | `Logs/Stadium-Clutter-Color-Generate.log`, `STADIUM EXTERIOR OPTIMIZATION REMOVAL objects=173`, `ACTIVE STADIUM VALIDATION PASS` |
| 전체 EditMode | `56/56` 통과 | `Logs/Stadium-Clutter-Color-EditMode-All.xml` |
| Soccer PlayMode | `18/18` 통과 | `Logs/Stadium-Clutter-Color-Soccer-PlayMode.xml` |
| 기준선 보존 감사 | 기준선 806개 중 동일 776, 의도된 변경 30, 누락 0, 새 팀 Stadium 파일 16 | `Logs/Stadium4v4-AllTeams-Final-Audit.json`, `Logs/Stadium4v4-AllTeams-BaselineChanges.csv` |
| Red·Navy 자산 회귀 검사 | 다섯 Prefab의 선수·킥 플레이트·골대 Material 경로·정확한 RGB, HUD Label·Class, Tag, 센서 순서 통과 | `RedAndNavyVisualIdentityIsConsistentAcrossEveryActiveStadiumAndHud` |
| 훈련 Profile Validate | 6개 Profile, 학습 가능 5개 통과. Base fallback Navy의 Trainer·ONNX 강제 차단 확인 | `Logs/Soccer-Training-Validate.log` |
| 훈련 변경 포함 전체 EditMode | `64/64` 통과 | `Logs/Soccer-Training-EditMode.xml` |
| 훈련 변경 후 Soccer PlayMode | `18/18` 통과 | `Logs/Soccer-Training-PlayMode.xml` |
| 공유 Windows 훈련 Build | 성공, 7개 Scene, 143,098,493 bytes, 오류 0 | `Logs/Soccer-Training-Build.log`, `Builds/SoccerTraining/build-info.json` |
| Build Profile headless routing | `base-fallback`, `base-selfplay`, `attack`, `defense`, `press`, `rule-eval` 모두 통과 | `Logs/Soccer-Profile-Smoke/*.log` |
| Trainer communicator smoke | 최종 Build에서 Unity package `4.0.3`, communication `1.5.0`, `Soccer4v4_Base?team=0` 연결 후 7,680 step에서 수동 중단 | `Logs/Soccer-Training-Smoke-Results/BuildSmokeFinal-20260831-1646` |

공유 Build의 실행 파일 SHA-256은 `d3db57b9b88a2d8fedd1621461ddd7b29ed18001efd29a93ef9f31d4e8a483cd`이며 `build-info.json`과 실제 파일이 일치한다. Build warning 485개는 Unity AI Inference(Sentis) package의 D3D11 Shader compile warning이고 C# 또는 Player Build 오류는 0개다. Communicator smoke가 만든 ONNX는 연결·Step 증가 확인을 위한 중단 산출물이므로 성능 평가나 TeamDefinition 등록에 사용하지 않는다.

EditMode는 다섯 Stadium Prefab 모두에서 승인된 도로·조경·도시 소품이 남지 않고 새 Red·Navy 몸통 재질이 공통 연결되는지 검사한다. PlayMode는 다섯 Scene을 차례로 열어 팀별 TeamDefinition, 학습 플래그, 선수 8명, 전후방 센서, HUD, Goal Occlusion Fader, Stadium Geometry와 Kick 값을 검증한다. 골대 가림 검사는 골문 밖 선수와 열린 입구를 통해 보이는 골문 안 선수에게는 불투명을 유지하고, 골문 안 선수가 실제 골대 Collider에 가려질 때만 Red·Navy RGB를 유지하며 Alpha를 `0.25`로 바꾼 뒤 원래 Material로 복구하는 것까지 확인한다.

프로젝트 전체 PlayMode를 필터 없이 실행한 결과는 Soccer 18개가 모두 통과하고 별도 `MachineLearning.Escape` 테스트 3개가 실패했다. Stadium 검증 결과와 혼동하지 않도록 Soccer 어셈블리 범위를 다시 실행해 위의 `18/18`을 확정했다. Escape 실패는 `Logs/Stadium-AllTeams-PlayMode-AfterRebuild.xml`에 남겨 두었으며 이번 Soccer 범위에서는 수정하지 않았다.

기준선 감사에서 공급자 원본 `Assets/Hayq Art/GrantStadium` 파일 누락이나 변경은 없었다. Unity가 검증 중 `Scripting Define` 순서를 바꾼 것은 기준선 순서로 복구했으며, `SENTIS_ANALYTICS_ENABLED`와 `APP_UI_EDITOR_ONLY`를 모두 유지했다. `ProjectSettings.asset`의 의도된 차이는 Stadium 기본 Scene 지정이다. `EditorBuildSettings.asset`의 의도된 차이는 다섯 Stadium Scene으로의 교체다.

## 현재 알려진 제약

- 입력 shape는 379로 유지했지만 경기장 크기, 골대 폭, 공 크기, Kick과 경계 물리가 달라졌다. 기존 ONNX의 텐서 호환 가능성만으로 플레이 품질을 보장할 수 없으므로 새 Stadium에서 재학습·재평가해야 한다.
- 기존 2026-08-31 결과는 초기 인프라 검증 이력이다. 현재 L0 승인 모델과 L1 실험 Run이 존재하며, 32/16 worker 비교 및 실제 장기 학습 결과는 이 문서 상단을 따른다. L1 최종 및 L2는 아직 승인되지 않았다.
- Attack·Defense·Press는 승인 Base v2 Model을 `BaseTeamDefinition`에 등록하고 Windows Build를 다시 만들기 전까지 Launcher가 시작을 차단한다. 의도적인 fallback 상대 실험만 `-AllowFallbackOpponent`를 사용한다.
- 자동 테스트는 장시간 Human 조작감, 프레임 성능, 실제 Controller, 학습 수렴과 경기 재미를 대신하지 않는다.
- 기존 `Soccer4v4_*` Scene과 `SoccerEnvironment_*` Prefab은 삭제하지 않았다. 활성 경로에서 제외된 이력 자산이며 별도 정리 승인이 있기 전까지 보존한다.
- `SoccerFieldTwos.prefab`의 과거 시연 기록 Component는 활성 Stadium 경로와 무관하며 삭제하지 않았다.

## 다음 작업 순서

1. r012 정면 공 준비 단계를 검증/학습한 뒤 0 → 0.5 → 1로 좌우 배치를 넓힌다. 각 단계 저장/평가하고 난이도 1의 실제 성공률로 근거리 승급 여부를 결정한다.
2. 통과 정책에서 L1의 8m 제어 운반→슛→득점 단계를 학습한다. 독립 평가와 L0 회귀 검사로 승인한다.
3. L1 승인 후에만 L2 패스·수신 환경, 전용 보상, Profile/Build를 구현·검증하고 학습한다.
4. 이후 완전 경기와 팀별 전술은 별도 단계다. 중간 정책을 완성 Base 모델로 등록하지 않는다.

학습 절차는 [학습 운용](../../soccer/training/overview.md), 현재 커리큘럼은 [단계별 보상 계획](../player-curriculum/staged-reward-plan.md), 담당 경계는 [학습형 팀 가이드](../../soccer/training/learning-teams.md)를 사용한다. commit·push는 수행하지 않는다.

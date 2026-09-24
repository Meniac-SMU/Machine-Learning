> 보관일: 2026-09-24. 작성 당시의 계획·판정·승인 이력이며 현재 실행 지시가 아닙니다. 현행 기준: [현재 상태](../../../soccer/current-status.md).

# MNG Manager 개발 인계 — 2026-09-10 13:57 KST

> **2026-09-23 최신 D3~D5 인계:** [MS3-v2 안정화 결과](../ms-v2/ms3-stability-d3-d5-report-20260923.md)와 current-status가 우선이다. D1~D5 안정화·한정 학습 준비 완료, R1~R6 실행 이력 완료/R7~R8 미진입, 실제 새 학습0이다. 최종 runtime `MS3-stability-contact-reset-20260923`, 준비물 `Logs/MNG-Rebuild/MS3-D5-prepared-20260923`. 기존400k의 Pass0/history200k열세는 남아 있으므로 원본resume나승격을 하지 않는다. 준비 스크립트는 새MS2actor사본·새optimizer·초기/history400k고정pool을 사용하며 첫100k모니터링/최대200k로 제한한다. 아래 이전 보류 문구는 역사 기록이다.

> **2026-09-23 최신 인계:** [Post-R6 수정·검증 보고서](../ms-v2/ms-post-r6-implementation-report-20260923.md)와 [현재 상태](../../../soccer/current-status.md)를 먼저 읽는다. D1~D3 회귀 통과, 최종 r4 동결80경기 진단 완료, D4 동적 공정성 승인/D5 대규모 학습 준비는 보류다. 새 학습·optimizer 갱신은 시작하지 않았다. 아래 이전 인계의 자동 재개·사용량 중단 지시는 현재 실행 조건이 아니다.

2026-09-23 사용자 지시: 5시간 및 주간 잔여량에 따른 사전 중단·재개 제한은 모두 폐지했다. 과거 사용량/중단 기록은 당시 사실이며 현재 실행 조건이 아니다. 서비스 자체 제한과 별개로 RAM/NaN/crash/데이터 무결성 및 정상 저장 규칙은 유지한다.

> **2026-09-19 실행 경로 변경:** R0 사용자 완료 판정 이후 새 감독 RL은 [MS 개발 인계](../ms-v1/ms-agent-handoff.md)를 따른다. [최신 상태](../../../soccer/current-status.md)에 승인·미구현 상태를 기록했다. 아래 R0 승인 대기·M1 재개·기존 M 순서 안내는 과거 기록이며 현재 실행 지시가 아니다. 이번에는 MS 계획만 작성했고 실제 개발·빌드·학습은 하지 않았다.

## 2026-09-19 R0 Planner v9 2인 공격·킥 세기 증가 구현 완료 — 이전 구현 기록

- 사용량 비율에 따른 실행 제한은 폐지했다(2026-09-23). 오류·중단 시 증거와 재개점을 보존한다.
- 자기 팀 필드 운반자가 상대 골대30m 안에 들어오면 운반자를 제외한 가장 가까운 제어 가능 필드 선수1명을 공격 지원자로 선발한다. 지원 목표는 공의 공격 방향2.75m·안쪽 측면3.75m이며, 운반자와 같은 점에 몰리지 않고 패스·리바운드에 참가한다. `PassBuild` 지정 수신자는 기존 수신 임무를 우선한다.
- 구현은 공통 `MNG_TeamPlanner` v9에 있어 R0 규칙형과 PPO 학습형에 동일하다. 감독6명령과133-float 크기는 유지한다. 패스·슛 힘1000 증가분은 질량3kg·0.02초 물리 틱 기준 출구속도 `+6.6667m/s`로 환산했다. 패스는20.6667~34.6667m/s, 슛은34.6667m/s, 공 최고속도와 관측 정규화는36.6667m/s다.
- 기존 dirty Scene/Prefab을 넓게 재생성하지 않았다. `MNG_BallControl.Awake`가 Rigidbody 최고속도를 공통 런타임 계약으로 설정하므로 규칙형·학습형·Human이 모두 같은 값을 사용하고 오래된 직렬화 상한이 새 킥을 자르지 않는다.
- 검증: 최종 컴파일 성공, MNG EditMode `75/75`, 실제 물리 `2/2`(슛10/20m와 패스5/10/20m 각20/20), MNG PlayMode 전체 `25/25`, Builder `MNG M0 VALIDATION PASS`다. 독립 R0 3×300초는 `8:2`, 소유전환693, 밀집0.2833%, 후방 운반5.95%, 패스9/완료2, 슛52/유효48이다. 최종 관측 정규화까지 포함한 전체 PlayMode R0 표본은 `6:8`, 소유전환758, 밀집0.0933%, 후방 운반5.85%, 패스11/완료3, 슛69/유효58이다.
- 증거는 `Logs/MNG-R0-AttackSupport-KickPower-*`에 보존했다. R0는 자동 회귀 통과·사용자 육안 미합격 상태이며 M1 학습은 시작하지 않았다. 새 물리와 관측 정규화 전의 r001~r003은 승격하거나 단순 resume하지 않는다.

## 2026-09-19 R0 Planner v8 2인 압박·골문 복귀·정체 안전 슈팅 구현 완료 — 이전 상태

- 사용자 예정 목록을 실제 구현했다. 상대 소유 시 `ProtectBack`을 포함한 모든 감독 명령에서 비골키퍼2명을 1차 직접 압박자와 2차 골대 쪽 통로 차단자로 나눈다. 공이 자기 골대30m 안이면 MidLeft/MidRight가 공격 진영에서 골문 압박·전방12m 차단선으로 의무 복귀한다.
- 전역 정체 활성 직후2.5초는 자기 운반자 또는 공8m 안의 킥 가능한 필드 선수 한 명이 실제 킥 플레이트로 상대 골문 방향 슈팅·클리어를 우선한다. 공격축 내적0.25 미만 또는 전진1m 이하 목표는 거부하고, 안전 슈터가 없거나 첫 시도가 정체를 풀지 못하면 기존 다중 선수 재탐지·접근/지원 경로로 넘어간다.
- 구현은 공통 `MNG_TeamPlanner` v8에 있으므로 R0 규칙형과 PPO 학습형 모두 동일하게 사용한다.6명령과133-float 관측은 불변이고 M1 코드·학습·평가는 시작하지 않았다.
- 검증: 컴파일 성공, MNG EditMode `73/73`, 프로젝트 전체 EditMode `220/220`, 실제 정체 Rigidbody `1/1`, MNG PlayMode `25/25`, 독립 R0 3×300초 `1/1`, Builder `MNG M0 VALIDATION PASS`다. 독립 경기 수치는 점수 `5:3`, 명령3,370회/6종, 소유 전환841, 밀집 `0.2022%`, 후방 운반 `6.01%`, 패스 킥13/완료2, 슛56/유효48이다. 전체 PlayMode 재표본은 점수 `6:4`, 밀집 `0.3389%`, 후방 운반 `6.44%`, 패스 킥14/완료1, 슛56/유효51이다.
- 증거: `Logs/MNG-R0-CoordinatedDefense-Compile.log`, `MNG-R0-CoordinatedDefense-EditMode.xml`, `MNG-R0-CoordinatedDefense-AllEditMode.xml`, `MNG-R0-CoordinatedDefense-StallPhysical.xml`, `MNG-R0-CoordinatedDefense-R0Gate.xml`, `MNG-R0-CoordinatedDefense-AllPlayMode.xml`, `MNG-R0-CoordinatedDefense-BuilderValidation.log`.
- R0는 자동 회귀를 통과했지만 사용자 육안 합격 전까지 미완성이다. 다음 작업은 R0 Scene을1배속으로 관전해 2인 압박 역할 분리, 미드필더 복귀, 자책 방향 없는 정체 슈팅 우선, `±3.00m` 시작/킥오프 변화만 확인하는 것이다. M1은 계속 중단한다.

## 2026-09-19 R0 전역 공 정체 복구·적극적 골키퍼 구현 완료 / 사용량 중단 — 이전 상태

- 사용자 지시에 따라 R0만 수정했으며 M1 코드 변경·학습·평가는 시작하지 않았다. R0는 여전히 **사용자 육안 미합격 상태**다.
- 경기 시작과 매 킥오프에만 적용되는 스폰 오프셋을 기존 `±1.50m`에서 `±3.00m`로 정확히 2배 확대했다. 경기 진행 중 인위적인 선수 위치 변경은 없다.
- `MNG_TeamPlanner.PlannerVersion=7`로 올렸다. 골키퍼가 공을 소유하면 4.5~38m 안의 압박이 낮고 패스 통로가 확보된 동료에게 패스하고, 조건을 만족하는 동료가 없으면 상대 골대 방향으로 강하게 클리어한다. 중립 공은 더 넓게 선점하고, 상대 운반자가 8m 안이면 수동 블록 대신 적극적인 `KeeperClaim`을 선택한다.
- 소유 여부나 대치 선수 수와 무관하게 공이 반경 0.55m 안에서 1.20초 정체되면 전역 정체 복구를 켠다. 가장 가까운 필드 선수만 움직이는 것이 아니라 운반자·압박자·나머지 필드 선수 전부가 서로 다른 접근/지원 목표를 받고, 정체가 계속되면 1초마다 방향과 위치를 다시 탐지한다. 공이 기준점에서 2.25m 벗어나거나 속도가 3m/s에 도달하면 해제한다.
- 위 세 변경은 `MNG_TeamPlanner`, `MNG_PlayerSkillExecutor`, `MNG_MatchController` 공통 계층에 있으므로 규칙형 R0와 학습형 PPO 양쪽에 동일하게 적용된다. 정체 복구 중에는 감독 명령보다 공 회수·경기 재개를 우선하지만, 공 이동은 실제 선수 이동과 킥 플레이트 접촉으로 수행한다.

### 이번 구현의 검증 근거

| 검증 | 결과 | 파일 |
| --- | ---: | --- |
| 컴파일 | 성공, C# 오류 0 | `Logs/MNG-R0-StallRecovery-Compile.log` |
| 런타임 계약 | `54/54` | `Logs/MNG-R0-StallRecovery-Contract-r2.xml` |
| 전면 밀기/Planner 계약 | `14/14` | `Logs/MNG-R0-StallRecovery-FrontPush.xml` |
| 실제 Rigidbody 전역 정체 복구 | `1/1` | `Logs/MNG-R0-StallRecovery-Physical.xml` |
| 전체 EditMode | `215/215` | `Logs/MNG-R0-StallRecovery-AllEditMode.xml` |
| MNG PlayMode 묶음 | `25/25` | `Logs/MNG-R0-StallRecovery-AllPlayMode.xml` |
| R0 독립 3×300초 게이트 | `1/1` | `Logs/MNG-R0-StallRecovery-R0Gate.xml` |
| Builder 자산 검증 | `MNG M0 VALIDATION PASS` | `Logs/MNG-R0-StallRecovery-BuilderValidation.log` |

- 독립 R0 게이트 결과는 점수 `11:7`, 명령 `3,254`회/6종 전부, 소유 전환 `741`, 공 최대 이동 `73.34m`, 같은 팀 3m 미만 밀집 `103/90006=0.1144%`, 소유 중 후방 운반 `482/11790=4.088%`, 패스 킥 `17`/완료 `3`, 슛 킥 `46`/유효 슛 `40`, 전진 보상 `137`이다.
- 전체 MNG PlayMode 재표본도 점수 `4:6`, 명령 `3,348`회/6종, 소유 전환 `618`, 밀집 `0.08555%`, 후방 운반 `5.035%`, 패스 킥 `29`/완료 `8`, 슛 킥·유효 슛 `28`, 전진 보상 `126`이었다.
- 프로젝트 전체 PlayMode 집계는 `42/46`이다. MNG 묶음은 `25/25`로 전부 통과했으며, 나머지 네 실패는 MNG 밖의 삭제된 Legacy `EscapePrototype` Scene 참조 3건과 기존 Core 킥 플레이트 시험 1건이다. 이 R0 변경의 합격 근거로 전체 프로젝트 통과를 주장하지 않는다.

### 사용량 중단 지점과 다음 재개 순서

- 마지막 확인 시 5시간 창은 `91% 사용/9% 잔여`, 주간 창은 `86% 사용/14% 잔여`였다. 사용자 중단 기준인 5시간 잔여 `10% 이하`에 도달했으므로 추가 코드·문서 수정·Unity 실행을 즉시 중단했다. 리셋 크레딧은 사용하지 않았고 커밋·푸시도 하지 않았다.
- 코드 구현과 자동 검증은 끝났다. 아직 하지 않은 일은 최신 결과를 `MNG_README.md`, `MNG_01_Runtime_Contract.md`, `MNG_04_Validation_and_Handoff.md`, `MNG_R0_Visual_Approval_Guide.md`, `docs/soccer/current-status.md`에 동기화하는 문서 정리다. 현재 이 인계 섹션이 가장 최신이고 아래 `±1.50m`/Planner v6 기록은 과거 이력이다.
- 다음 작업은 사용량 비율의 재개 조건 없이 위 다섯 문서를 `±3.00m`, Planner v7, 1.20초 전역 정체 복구, 적극적 골키퍼, 최신 로그에 맞춰 갱신하고 제한된 diff 검사를 하는 것이다. 새 구현이나 장시간 회귀는 필요 없다.
- 그 다음 Unity에서 R0를 육안 검토한다. 시작/킥오프마다 더 넓지만 포메이션을 훼손하지 않는 위치 차이, 골키퍼의 안전 패스/즉시 장거리 클리어/8m 근접 도전, 중앙·측면 어느 곳에서든 공 정체 시 양 팀 여러 필드 선수가 서로 다른 방향으로 재탐지·접근하는지를 확인한다.
- 사용자 육안 합격 전까지 R0는 미완성이고 M1은 계속 중단한다.

### 사용자 추가 요청 — R0 구현 완료 목록

1. **2인 이상 수비 압박**: 수비가 필요한 상황에는 최인접 선수 한 명만 공을 상대하게 두지 않는다. 최소 두 명을 `1차 압박자`와 `2차 압박·차단자`로 나누어 공과 상대 운반자를 압박한다. 두 선수가 완전히 같은 목표점에 겹치지 않도록 한 명은 직접 탈취를 시도하고 다른 한 명은 패스·돌파 경로를 차단한다.
2. **미드필더의 의무 수비 복귀**: 공이 자기 골대30m 안에 있고 자기 팀 소유가 아니면, 기존 공격·지원 임무보다 수비 복귀를 우선하여 압박 또는 골대 전방12m 차단 위치로 이동한다. 골문 위협이 해소되면 포메이션 역할로 복귀한다.
3. **공 정체 시 안전한 슈팅 우선**: 전역 공 정체를 감지하면 곧바로 재탐지·재배치 절차로 넘어가기 전에, 공을 찰 수 있는 선수가 실제 킥 플레이트로 먼저 강한 슈팅 또는 클리어를 시도한다. 단, 계산된 킥 방향이 자기 골대 방향 또는 자책골 위험 각도이면 이 단계는 금지하고 즉시 기존 재탐지·위치 변경 절차를 수행한다. 안전한 슈팅 대상이 없거나 첫 시도가 공의 정체를 해소하지 못한 경우에도 기존 다중 선수 정체 복구로 전환한다.
4. **규칙형·학습형 공통 계약**: 위 기능은 특정 감독의 규칙에만 넣지 않고 `MatchController → TeamPlanner → PlayerSkillExecutor` 공통 계층에 구현한다. 따라서 R0 규칙형과 PPO 학습형 모두 동일한 2인 압박, 미드필더 복귀, 안전 슈팅 우선 절차를 사용한다. 감독은 여전히 6개 팀 명령을 선택하고, 선수별 위치·탈취·킥 실행은 공통 코드가 담당한다.
5. **완료 검증**: 단위 계약에서 최소 두 명의 서로 다른 수비 작업, 먼 미드필더의 골문 복귀, 자기 골대 방향 슈팅 거부, 안전한 정체 슈팅 후 기존 복구로의 fallback을 확인했다. 실제 물리 정체 시험, R0 3×300초, MNG 전체 회귀와 Builder도 통과했다. 사용자 육안 검토만 남아 있다.

이 목록은 Planner v8과 계약·물리 테스트로 구현을 완료했다. Scene·Prefab YAML은 직접 수정하지 않았고 기존 연결은 Builder로 검증했다.

## 2026-09-19 R0 사용자 육안 승인 후보 고정 — 이전 자동 근거

- 중단 지점의 유일한 실패였던 R0 같은 팀 3m 미만 밀집률을 승인 기준 `<1%` 완화 없이 해결했다. `MNG_PlayerSkillExecutor`는 4.25m 안의 동료 충돌 위험을 감지하고, 운반자·킥 실행자·패스 수신자·Human·강제 탈출 담당자에게 우선권을 준다. 우선순위가 낮은 AI 한 명만 최대 속도의85%로 양보하며, 같은 임무는 슬롯 순서로 정확히 한 명만 양보한다. 5.25m까지 짧은 이탈 hold를 사용해 경계에서 반복 진입하지 않는다.
- 새 단위 계약은 `Logs/MNG-R0-EmergencySpacing-Contract.xml` `14/14`, 실제 Rigidbody 분리는 `Logs/MNG-R0-EmergencySpacing-Physical.xml` `1/1`이다.
- 독립 3×300초 게이트 `Logs/MNG-R0-EmergencySpacing-R0Gate.xml`은 점수 `20:14`, 명령3,080회/6종, 소유 전환559, 공 최대거리73.36m, 밀집 `9/90006=0.01%`, 후방 운반 `1306/13303=9.82%`, 패스 킥10/완료1, 유효 슛34, 전진119로 통과했다.
- 최종 전체 EditMode는 `Logs/MNG-R0-EmergencySpacing-AllEditMode.xml` `212/212`, 전체 MNG PlayMode는 `Logs/MNG-R0-EmergencySpacing-AllPlayMode.xml` `24/24`다. 전체 재표본의 R0도 밀집 `60/90006=0.067%`, 후방 운반 `8.56%`로 통과했다. 드리블3종, 슈팅10/20m, 패스 수신5/10/20m는 각각 `20/20`이다.
- 최종 Builder는 `Logs/MNG-R0-EmergencySpacing-BuilderValidation.log`에서 `MNG M0 VALIDATION PASS`다. 스폰 `±1.50m`, 골키퍼 안전 패스/강한 클리어, 벽·코너 후퇴 후 킥 플레이트 탈출은 규칙형/PPO 공통 실행 계약으로 확정한다.
- R0는 개발자 자동 기준을 통과했다. 사용자가 R0를 먼저 직접 점검하기로 했으므로 이 버전을 육안 승인 후보로 고정하며, 최종 체감 승인 또는 수정 지시가 오기 전에는 M1 코드 변경·학습·평가를 시작하지 않는다. 기존 Run, ONNX/PT, 로그는 덮어쓰지 않았고 커밋·푸시는 하지 않았다.

### 사용자 검토와 다음 재개 경계

1. Unity에서 `MNG_R0_Visual_Approval_Guide.md`의 R0 전용 Scene을 1배속으로 관전하고 경기 흐름·간격·공격 방향·패스·슛·골키퍼·경계 탈출·킥오프를 확인한다.
2. 사용자가 `R0 육안 합격`이라고 선언하면 이 승인 후보를 R0 최종본으로 기록한다. 문제가 있으면 장면과 증상을 기준으로 R0만 수정하고 전체 회귀를 다시 실행한다.
3. R0 최종 승인 뒤 M1을 재개하더라도 변경 전 물리/플래너 계약으로 학습된 r001~r003을 승격하거나 단순 resume하지 않는다.
4. M1에서는 v6의 득점82·자책0·패스 킥12·완료 패스8 gate를 유지하고, PassBuild 선택이 실제 킥 플레이트 패스와 동료 수신을 일으킨 뒤에만 성공으로 이어지도록 명령-성과 인과성을 먼저 재검토한다.
5. 코드·고정 random baseline·Player manifest를 다시 검증한 뒤 새 이름의 fresh-policy Run으로 학습한다. 체크포인트마다 득점과 패스 사건을 함께 평가하며 두 번 연속 진전이 없으면 무의미하게 step을 채우지 않는다.

## 2026-09-19 골키퍼 배급·경계 탈출·스폰 확대 사용량 중단 — 아래 기록보다 우선

- 현재 변경은 규칙형/PPO가 공유하는 저수준 경기 보조다. 시작/킥오프 스폰 오차는 `±1.50m`이며 경기 중 인위적 위치 변화는 없다. `MNG_TeamPlanner.PlannerVersion=6`은 골키퍼 소유 시 안전한 전진 패스가 있으면 `AimPass/ReceivePass`, 없으면 상대 골대 방향 `AimShot`을 생성한다.
- 경계 3m 안의 저속 공이 0.75초 정체되면 선택된 AI 선수가 0.55초 물러난 뒤 경기장 안쪽 목표로 접근해 실제 킥 플레이트로 공을 빼낸다. 물리 시험 `Logs/MNG-R0-Boundary-PhysicalEscape.xml`은 `1/1`이다.
- 루트 `AGENTS.md`에 재발 방지 지침을 추가했다. 광범위한 MNG 구현에서는 호출 횟수 하드 스톱 진단 스킬을 쓰지 않고 직접 소스 감사, `apply_patch`, Unity Editor batchmode 검증을 사용한다. 선택적 스킬/플러그인/MCP 실패만으로 전체 작업을 중단하지 않되 사용량·권한·안전 경계는 우회하지 않는다.
- 통과 근거: 컴파일 `Logs/MNG-R0-Boundary-Compile.log`, 전면밀기/경계 계약 `12/12`, 런타임 계약 `51/51`, 전체 EditMode `210/210`, 물리 경계 탈출 `1/1`. 단, 전체 EditMode와 아래 전체 PlayMode는 최종 간격 상수 7m/10m/2.5 적용 전 실행이다. 상수 적용 뒤 단일 R0 재시험 과정의 재컴파일은 성공했지만 전체 회귀는 중단선 때문에 다시 실행하지 않았다.
- 전체 PlayMode `Logs/MNG-R0-Boundary-AllPlayMode.xml`은 `22/23`이다. 유일 실패는 R0 3×300초의 같은 팀 3m 미만 밀집률 `2389/90006=2.65%`가 `<1%` 게이트를 넘은 것이다. 경기 기능은 합계 `18:11`, 6명령 전부, 소유 전환560, 패스 킥11/완료1, 유효 슛31로 작동했다.
- 필드 개인 공간을 7m, 최대 회피 오프셋을 10m, 강도를 2.5로 강화한 재시험 `Logs/MNG-R0-Boundary-R0Gate-Retry.xml`은 밀집률을 `1273/90006=1.41%`로 줄였지만 아직 실패다. 재시험 경기 결과는 `11:13`, 소유 전환512, 패스 킥13/완료5, 유효 슛26이다. 승인 기준을 완화하지 않는다.
- 마지막 사용량은 5시간 잔여9%, 주간 잔여86%로 중단선에 도달했다. 추가 코드 수정·Unity 재실행·Builder·학습은 시작하지 않았다. Unity Editor/Trainer/Player 프로세스는 없고 Unity CLI helper만 실행 중이다.

### 정확한 재개 순서

1. 사용량 비율에 따른 실행 제한은 폐지했다(2026-09-23). 오류·중단 시 증거와 재개점을 보존한다.
2. 현재 diff를 보존하고 `MNG_PlayerSkillExecutor`의 간격 보조를 상수만 더 키우지 말고, 3m 근접 시 즉시 분리 속도 또는 역할/운반자 우선권으로 개선한다. 공 운반자와 패스 수신자의 기술 수행은 방해하지 않아야 한다.
3. `MNG_FrontPushContractTests`와 관련 단일 물리 시험을 먼저 실행하고, R0 `R0RuleManagersCompleteThreeFullThreeHundredSecondMatches`에서 밀집률 `<1%`, 후방 운반 `<25%`, 6명령·득점·패스·슛 사건을 모두 확인한다.
4. 통과하면 전체 EditMode와 MNG PlayMode `23/23`, `MNG_ProjectBuilder.ValidateM0AssetsBatch`를 실행한다. 실패하면 수치 완화 없이 원인을 고친다.
5. 최종 결과를 이 문서와 `docs/soccer/current-status.md`에 기록한 뒤에만 R0 완료 상태를 복구하고 M1 후속 작업을 검토한다.

## 2026-09-19 R0 개발자 승인 완료 — 아래 과거 중단 기록보다 우선

- 코드·Scene·Prefab 연결과 자동 승인 게이트까지 완료했고 R0를 개발자 기준으로 승인했다. 사용자의 실제 화면 검토는 경기 체감의 최종 확인이며 후속 M1의 선행 조건은 아니다.
- `MNG_TeamPlanner` v5는 자기 골대 방향 운반을 금지하고, 운반 목표의 최소 공격 전진 6m와 필드 선수 목표 간격 6m를 보장한다.
- `MNG_PlayerSkillExecutor`는 공 소유 전환 직후의 `Press`, 패스 수신 직후의 `ReceivePass`, 일반 `Carry`만 킥 플레이트 전면 밀기로 이어간다. 비운반 커버·마크·지원 선수의 공 주변 궤도 진입은 차단했다.
- 전면 밀기 수행자도 6m 개인 공간과 최대 8m 동료 회피 보정을 사용하며, 보정 결과를 다시 상대 골대 방향으로 제한한다. 공 주변 같은 팀 뭉침은 최종 독립 게이트에서 0.23%다.
- R0 규칙형 감독은 소유 직후 과거 `ProtectBack`/`ActiveRecover` 유지 시간을 즉시 해제하며, 위험 패스·압박 탈출·안전한 전방 패스·운반·슈팅을 비신경망 규칙으로 선택한다.
- 패스는 패서와 수신자가 같은 전방 합류점을 사용하고, 슈팅은 골키퍼 반대쪽 골문 안쪽을 겨냥한다. 모든 킥은 `MNG_KickPlate`의 실제 접촉과 Rigidbody impulse를 사용한다.
- 정지·후진 시 소유 해제는 0.12초, 드리블 자석 보정은 전진 중에만 적용한다. 상대 킥 플레이트의 빠른 탈취 계약은 유지한다.
- 정면 경합에서는 현재 소유 팀이 상대 골대 방향 4m와 측면 2.25m를 결합한 탈출 목표로 전면 밀기한다. 직접 공 impulse, 순간이동, 강제 소유 이전은 사용하지 않는다.

### 최종 검증 근거

| 검증 | 결과 | 파일 |
| --- | ---: | --- |
| 전체 EditMode | `207/207` | `Logs/MNG-R0-Final-EditMode.xml` |
| MNG 전체 PlayMode | `22/22` | `Logs/MNG-R0-Final-PlayMode.xml` |
| 전진 드리블 | 직선·좌·우 `20/20` | `Logs/MNG-Core-Dribble-Retry-PlayMode.xml` |
| 상대 탈취 | 통과 | `Logs/MNG-Core-Steal-PlayMode.xml` |
| 오픈 골 슈팅 | 10m·20m 각각 `20/20` | `Logs/MNG-R0-OpenGoal-PlayMode.xml` |
| 패스 수신 | 5m·10m·20m 각각 `20/20` | `Logs/MNG-R0-PassReceive-PlayMode.xml` |
| R0 3×300초 독립 승인 게이트 | 합계 `2:1`, 6명령, 패스 킥5·완료1, 슈팅/유효 슈팅4, 전진35 | `Logs/MNG-R0-Spacing-3Match-PlayMode.xml` |
| 독립 게이트 간격·공격 방향 | 뭉침 0.23%, 소유 중 후방 속도 8.24% | `Logs/MNG-R0-Spacing-3Match-PlayMode.xml` |
| 전체 PlayMode 내 R0 재표본 | 합계 `3:1`, 뭉침 0.62%, 후방 속도 6.00%, 패스 킥1, 유효 슈팅4, 전진37 | `Logs/MNG-R0-Final-PlayMode.xml` |
| Builder 자산 검증 | `MNG M0 VALIDATION PASS` | `Logs/MNG-R0-Final-BuilderValidation-r3.log` |

독립 3경기에서 실제 완료 패스 1회를 확인했다. 전체 PlayMode 재표본의 패스 킥은 수신까지 연결되지 않았지만 별도 경쟁 표본과 독립 수신 60회의 성공으로 물리 기능은 승인한다. 실제 경기에서 패스 빈도와 연결 품질이 재미 기준에 맞는지는 육안 체감 항목으로 유지한다.


## 2026-09-18 전면 밀기 재배치 작업

- 공을 옆이나 뒤에서 자석처럼 끌지 않고, 항상 목표 방향 반대편으로 돌아간 뒤 킥 플레이트 전면으로 미는 공통 실행 계층을 추가했다.
- 소유 중 큰 방향 전환은 소유 보정을 잠시 해제하고, 공과의 안전 반경을 유지하는 궤도 웨이포인트를 거쳐 준비점으로 이동한다.
- R0은 미승인 상태를 유지하며 컴파일, EditMode, PlayMode 및 실제 경기 시각 검토가 필요하다.
- 골키퍼 Claim은 깊이 18→36m, 측면 여유 10→20m, 최대 전진 16→32m로 확대했다. Block은 깊이 28→56m, 측면 여유 14→28m, 최대 전진 12→24m로 확대했다. Home 기준점은 유지한다.

## 2026-09-18 R0 합격 철회·공통 드리블 개선 — 아래 모든 과거 기록보다 우선

- 사용자는 R0가 아직 합격이 아니라고 명시했다. 아래의 임시 합격 기록은 역사적 자동 근거일 뿐 현재 승인 상태가 아니다.
- 기존 공 보정은 `IsForwardDriveActive()`일 때만 작동하고0.08초간 전진하지 않으면 소유권을 해제해 좌우·후진·회전 드리블에서 공을 놓쳤다. 공통 `MNG_BallControl`을 실제 평면 이동/회전 기준으로 변경해 규칙형과 PPO가 같은 개선 기술을 사용한다.
- 킥 플레이트 앞 목표점에 작은 앞뒤·좌우 시간 기반 흔들림을 주고, 동적 Rigidbody에는 이동속도/반지름에 맞는 각속도 torque를 적용한다. 위치 순간이동이나 부모 결합은 사용하지 않아 충돌과 자연스러운 구름을 유지한다.
- 상대 진영으로 전진할 때4.75m 안 전방 통로의 가장 위급한 수비수를 찾아 반대 측면 가속도를 선수와 공 목표에 함께 적용한다. 후진·횡이동에는 인위적 공격 회피를 넣지 않는다.
- 상대 킥 플레이트가 드리블 위치에서 접촉하면 기존 carrier를 즉시 놓고, 빼앗긴 선수는0.22초 동안 재획득할 수 없다. 이 탈취 우선순위가 자석 보정보다 높다.
- 이 변경 후 R0 300초/드리블 방향별/탈취/회피 회귀와 실제 화면 검토가 필요하다. 기존 M1 r003은 물리 계약이 바뀌었으므로 재사용·resume하지 않고, M1 인과성 개편 뒤 새 fresh-policy Run만 허용한다.

## 2026-09-17 M1 r003 사용량 중단 — 아래 모든 과거 기록보다 우선

- R0 v0는 아래 자동 근거로 개발자 임시 합격했다. 사람 육안 최종 승인은 후속이며 M1 진행의 선행 조건은 아니다.
- 공통 명령-age 수정이 들어간 M1 Player를 새로 빌드했다. `build-info.json`은 오류0/경고486, YAML SHA `a16d8405630ae108625b80df59dc057a12932029c4d740f184f27c70c528f21d`, level0 SHA `7e44c4b974742bd36d1d8d360c49ff528db8dc4ae64d632dcc9ae6d81abb25ea`이고 manifest와 실제 파일이 일치한다. 샌드박스 안 Unity는 라이선스 named-pipe에 연결하지 못해 멈췄으며, 외부 권한으로 같은 검증된 스크립트를 실행하자 정상 빌드됐다.
- `MNG_M1Attack-20260917-r003`은 seed13003, `init_path: None`, PPO beta0.01, checkpoint20k, hard cap300k의 fresh policy다. 시작·20k·40k·중단 시점 진단 JSON을 `Logs/MNG_M1Attack-20260917-r003-*.json`에 저장했고 legacy reward 누출은0이다.
- 20k 후보 `MNG_Manager-19998.onnx` frozen100은95득점/자책0/timeout5지만 명령 `333,2,45,241,573,88`, pass 가능588, PassBuild2, pass plate0, 완료 pass0이라 실패했다. 근거는 `Logs/MNG_M1Attack-20260917-r003-eval-r001`이다.
- 40k 후보 `MNG_Manager-39999.onnx` frozen100은97득점/자책0/timeout3이지만 명령 `235,0,11,198,656,77`, pass 가능581, PassBuild0, pass plate0, 완료 pass0이라 다시 실패했다. 근거는 `Logs/MNG_M1Attack-20260917-r003-eval-r002`다. 득점은 좋아졌지만 pass 다양성은 악화됐으므로 M1은 미승격이다.
- 사용량 비율에 따른 실행 제한은 폐지했다(2026-09-23). 오류·중단 시 증거와 재개점을 보존한다.
- 마지막 사용량은5시간91% 사용(잔여9%), 주간69% 사용(잔여31%)이다. 당시에는 중단선에 따라 새 작업을 시작하지 않았다. 이 조건은 현재 폐지됐다. reset credit은 사용하지 않았다.
- 다음 재개 때 r003을 단순 `--resume`하지 않는다. 20k와40k 모두 높은 득점인데 pass0인 것은 학습량 부족보다 **감독 명령과 실제 성공의 인과성이 약한 구조**다. Pass 상황에서도 저수준 운반/공 이동이 goal+1을 쉽게 얻어 완료 pass+0.04보다 훨씬 유리하다. 먼저 Pass fixture에서 직접 운반 득점을 성공으로 인정하지 않고 실제 plate pass와 동료 수신 뒤 공격 전개를 요구하며, 명령별 실제 사건과 보상을 연결한다. 테스트·random baseline을 다시 고정한 뒤 기존 r003 optimizer를 버리고 새 fresh-policy r004를 만든다. v6 gate는 완화하지 않는다.

## 2026-09-17 R0/M1 개발 재개 — 아래 모든 과거 기록보다 우선

- 당시 확인 사용량은5시간56% 사용(잔여44%), 주간64% 사용(잔여36%)이었다. 당시 사용량 중단 기준은 2026-09-23에 폐지했다. reset credit은 사용하지 않는다.
- 사용자는 R0의 사람 최종 합격을 당장 필수로 두지 않고 개발자가 자동 근거로 임시 합격시킨 뒤 M1을 이어도 된다고 승인했다. 같은 명령 재수락 때마다 `CommandAgeSeconds`가0으로 초기화되던 공통 결함을 수정하고 회귀 테스트를 추가했다.
- R0 Builder, EditMode195/195, 전체 MNG PlayMode22/22를 통과했다. 수정 뒤 실제300초는15,001tick/명령1,154회/6종 전부/소유전환37회/공 최대61.0646553m/0:0으로 완주했고, 합산 Carry5/Pass14/Shot1/Recover607/Balanced4/Protect523을 기록했다. 강화학습 없이 기능이 모두 작동하는 약한 기준선이라는 R0 목적을 충족하므로 **개발자 임시 합격**으로 고정하며 사람 육안 최종 승인은 후속으로 남긴다.
- M1은 기존 r001/r002와 실패 근거를 보존하고 `MNG_M1Attack-20260917-r003` fresh policy를 `-InitializeFrom` 없이 사용한다. v6의 고정100상황 득점82·자책0·실제 패스 플레이트12·완료 패스8 gate는 완화하지 않는다.

## 2026-09-16 R0 규칙형 감독 구현·사용량 중단 — 아래 모든 과거 기록보다 우선

- 사용자 결정에 따라 M0와 M1 사이에 `R0_RuleBaseline`을 신설했다. 새 `MNG_RuleBasedManager` v0는 강화학습·Trainer·ONNX·reward를 전혀 사용하지 않고, MNG snapshot에서0.5초마다 기존6명령 중 하나를 선택한다. 대상 계산과 선수 실행은 PPO와 동일한 `MNG_TacticalTargetResolver → MNG_MatchController → MNG_TeamPlanner → MNG_PlayerSkillExecutor` 계층을 사용하며 선수를 직접 이동시키거나 공에 힘을 주지 않는다.
- `MNG_ProjectBuilder.BuildR0RuleBaseline()`과 전용 Scene `Assets/_Soccer/Manager/Curriculum/R0_RuleBaseline/Scenes/MNG_R0_RuleVsRule.unity`를 추가했다. Scene에는 Red/Navy 규칙 감독만 활성화하고 기존 Fallback, Human 입력, PPO policy endpoint는 비활성화했다. `MNG_R0RuleMatchMonitor`는 결정을 바꾸지 않고 명령 수와 경기 종료만 기록한다.
- 연결된 Unity Editor에서 재컴파일과 R0 Builder가 성공했다. MNG EditMode `47/47`, 전체 MNG PlayMode `22/22`를 통과했다. R0 300초 자동 경기는15,001tick, 점수0:0, 명령1,156회, 명령4종, 소유전환14회, 공 최대이동47.2158165m로 완주했다. 이는 기능·안정성 근거이며 공격 품질 또는 R0 최종 승인이 아니다.
- `MNG_README.md`와 `MNG_02_Implementation_Plan.md`에는 R0 책임·단계·현재 결과를 반영했다. 사용량 중단 때문에 `MNG_03_Curriculum_and_Rewards.md`, `MNG_04_Validation_and_Handoff.md`, R0 전용 짧은 관전 안내의 정식 문서화는 아직 남아 있다.
- R0 최종 완료는 사용자가 위 Scene을1배속 Play Mode로 직접 보고 양 팀 이동, 패스·슛 시도, 압박·복귀, 골키퍼 반응, 킥오프 재배치를 승인한 뒤에만 선언한다. 자동 경기0:0도 눈검토에서 공격 흐름 개선 필요 여부를 판단할 항목이다.
- 마지막 사용량 확인은5시간99% 사용(잔여1%), 주간56% 사용(잔여44%)이다. 당시 사용자 중단선에 도달해 저장·중단했다. 이 조건은 현재 폐지됐다. reset credit은 사용하지 않았고 새 학습·Player build·R1·M1 r003은 시작하지 않았다.

### 정확한 재개 순서

1. 사용량 비율에 따른 실행 제한은 폐지했다(2026-09-23). 오류·중단 시 증거와 재개점을 보존한다.
2. 현재 diff와 두 R0 Runtime 파일, Builder, EditMode/PlayMode 테스트, 생성된 Scene을 확인한다. 기존 MNG 변경과 Run은 원복·덮어쓰기하지 않는다.
3. 남은 R0 문서 세 곳을 마무리하고 Unity 재컴파일/Builder 후 R0 관련 EditMode와 PlayMode만 짧게 재확인한다. 전체22개 PlayMode는 이번 실행에서 이미 통과했으므로 코드가 바뀌지 않았다면 불필요하게 반복하지 않는다.
4. Unity Editor에서 R0 Scene을 열어 사용자 육안 승인 안내를 제공한다. 승인 전 R0 완료 또는 R1 시작으로 표시하지 않는다.
5. 사용자 승인 후 R0 결과를 규칙형 기준선으로 동결하고, 다음 지시에 따라 R1 개선 또는 기존 M1 fresh-policy r003 재개 순서를 결정한다.

## 2026-09-16 M1 v6 검증 재개 — 아래 모든 과거 기록보다 우선

- 사용량 비율에 따른 실행 제한은 폐지했다(2026-09-23). 오류·중단 시 증거와 재개점을 보존한다.
- M1 r002는 r001의 패스0회 정책에서 초기화한 것이 구조적 오류였다. 19,996 step 고정100은82득점·패스 명령1·실제 패스 플레이트1·완료0, 59,994 step은83득점·패스 명령0·실제 패스0·완료0으로 득점만 유지하고 전술 다양성이 붕괴했다. 기존 r001 최종87득점보다도 낮아 두 연속 checkpoint 정체 기준으로74,000 step에서 중단했으며, 보존된 마지막 정식 checkpoint는59,994다.
- protocol v6는 득점82 이상·자책골0과 함께 실제 패스 플레이트12회 이상·완료 패스8회 이상을 모두 요구한다. 완료 패스 보상은0.04, M1 PPO beta는0.01이며, `Tools/MNG_Train.ps1`은 M1-AttackChoice에서 구형 `-InitializeFrom`을 거부한다. 다음 M1은 fresh policy로만 시작한다.
- v6 코드 Unity 컴파일과 MNG Builder는 각각 `Logs/MNG-v6-Compile.log`, `Logs/MNG-v6-Generate.log`에서 통과했다. 첫 전체 EditMode `MNG-v6-EditMode-r3.xml`은 새 요구득점 예상값을76으로 남긴 테스트 한 줄 때문에191/192였고, 예상값을 실제 규칙82로 고친 최종 `MNG-v6-EditMode-r4.xml`은192/192다. ProjectSettings/EditorBuildSettings 내용 SHA는 기준 `54FE0BA0...D49D85` / `AE8E2777...68A104`와 일치하고 Core 내용 변경은0이다. Player 재빌드와 r003 학습은 이번 작은 단계에서 시작하지 않는다.

### 이번 작은 단계의 재개 순서

1. v6 Player를 새 build-info/YAML SHA로 재빌드한다.
2. `MNG_M1Attack-YYYYMMDD-r003`을 `-InitializeFrom` 없이 fresh policy로 시작하고20k마다 고정100을 평가한다.
3. 득점82·자책0·실제 패스 플레이트12·완료 패스8을 함께 확인한다. 두 연속 평가에서 득점과 행동 다양성이 정체하면 정상 저장 후 중단하고 관측/보상/상대 구조를 다시 감사한다.

## 2026-09-11 공 물리 복구·Planner v3·장기학습 전환 — 아래 모든 과거 기록보다 우선

- 사용자 지시로 MNG 공 mass를4.5에서 Core 원래값3.0으로 복구했다. MNG 전용 PhysicsMaterial은 동/정지 마찰0.6→0.05(Minimum), 반발0.05→0.15(Maximum)로 바꾸고 드리블 보정은 acceleration30→24, damping8→5.5로 낮췄다. 공 크기1.10과 킥 플레이트 실접촉 계약은 유지한다.
- PlannerVersion은2→3이다. 감독 관측133/행동[6]을 바꾸지 않고, 각 필드 선수가 자기 위치·역할·상대 압박·동료 간격으로 열린 지원경로를 선택한다. 비소유 때 한 명만 예측압박하고 나머지는 각자 마크/커버한다. Keeper는 항상 공을 보며 위험 loose ball을 최대16m 범위에서 Claim하고 상대 소유 시 골-공 선상으로 Block한다.
- `MNG-Physics-PlannerV3-Generate.log` Builder 통과, `MNG-Physics-PlannerV3-EditMode-r2.xml` 43/43, `MNG-Physics-PlannerV3-PlayMode.xml` 20/20이다. 실제 패스5/10/20m, 슛10/20m, 드리블 직선/좌/우, 탈취는 각20/20. Fallback300초는5:3, possession transition356, escape3, 공 최대73.27846m다.
- M1/M1B/M2 YAML을20k에서100k로 변경했다. 연결 smoke만 짧게 허용하고 중요 모델은 최소100k aggregate decision 뒤 평가한다. 기존20k M1/M2/M1B와100k M3 r001은 변경 전 실행 의미의 이력으로 보존하며 덮어쓰거나 승인 근거를 자동 승계하지 않는다.
- 다음 순서: 새 Player를 단계별 빌드하고 YAML/parser/build hash를 확인한다. 새 `MNG_M1Attack-20260911-r001`을100k 학습·inspect 저장·frozen100 평가한 뒤 통과 시 그 PT에서 M1B와 M2를 각각 새100k Run으로 진행한다. 이후 새 M2에서 M3를100k 학습하고 정식300초×40 평가한다. 이 문장의 중단선은 당시 이력이며 현재 기준은 맨 위 2026-09-16 항목을 따른다.
- M1 r001은100,000에서 자연 저장했고 final ONNX SHA는 `143B0477C9EEC8DF21B334F0CF14E3F33D6D79FFC3BDD791A886826361BF7248`이다. 고정100평가는65득점/자책0/timeout35로 random28보다+37%p지만 요구76에 미달했다. gate를 완화하지 않고 기존100k PT/ONNX와 평가 evidence를 보존한 채 동일 optimizer `--resume`, 총300k 상한으로 연장한다. 200k와300k를 각각 새 evidence revision으로 평가한다.
- 200k 예정 체크포인트 `MNG_Manager-199964`도 별도 후보 ID와 build로 동결 평가했다. `Logs/MNG_M1Attack-20260911-r001-eval-r003`에서64득점/자책0/timeout36으로 요구76에 미달하여 미승격이다. 10만·20만 모델/빌드/평가를 덮어쓰지 않고 동일 optimizer를201,444에서 총300k까지 재개했다. 재개 시작 증거는 `Logs/MNG_M1Attack-20260911-r001-resume300k-r2-startup.json`이며, 다음 판단은300k 불변 번호 ONNX의 새100경기 평가다.
- M1 r001은 총300,015 step에서 자연 종료했다. 최종 numbered/root ONNX SHA는 `219DD8070346ED18439AC394D72A482B7CE865757CA50B0117D3B04C7B6391ED`, PT SHA는 `8E804E7655AB8B2E7EA38CC8F7D0579C2B787654AAC9492BDD8BA2348ED1CE8F`이며 ONNX checker/PT load를 통과했다. 최근5 summary 평균 보상은0.692, 누수0이다. `eval-r004` 고정100은68득점/자책0/timeout32로76골 gate에 미달하므로 더 단순 연장하거나 M1B/M2로 승격하지 않는다.
- r001의 `keep_checkpoints: 5` 때문에 재개 과정에서100k/200k numbered PT가 자동 정리된 사실을 종료 점검에서 확인했다. 해당 시점 ONNX는 평가 전 Unity ModelAsset으로 각각 SHA `143B0477...BF7248` / `ABF6125C...D8D05F`가 별도 보존되어 평가 재현이 가능하지만, 두 중간 optimizer PT 자체는 남아 있지 않다. 최종 및239,967/259,992/279,999/299,975 PT·ONNX는 남아 있다. 새 r002부터 `keep_checkpoints`를 최소16으로 올리고100k/200k 번호 PT를 평가 전에 별도 manifest로 고정해 같은 손실을 막는다.
- 정책·물리를 바꾸지 않는 M1 protocol v4 진단을 추가하고 EditMode43/43을 통과했다. v4는 Planner v3의 실제 random-valid 기준선28을 정정해 기록하되 절대 통과선76은 유지한다. 같은300,015 ONNX의 `eval-r005`는 점수68/0/32를 재현했다. 유형별은 Carry24/34, Pass23/34, Shot21/32이고 좌우는33/50 대35/50이라 특정 종류나 mirror 단독 병목은 아니다.
- 핵심 진단은 총2,652 decisions 중 `PassBuild` 선택0회다. 마지막 누적 telemetry 시점인2,640 decisions에서 Pass mask도131회(약5.0%)만 열렸고, 전체 명령 합계는 Advance22/Pass0/Shot500/Recover193/Balanced1570/Protect367이다. 또한 보상 profile/ledger에는 ValidShot·CompletedPass·AdvancedFiveMeters가 정의돼 있지만 제품 Runtime의 실제 `Award` 호출은 Goal/Match와 M2 Recovery 계열뿐이라 M1 shaping이 연결되지 않았다. 즉30만 부족이 아니라 좁은 패스 가용성과 미배선 사건 보상 때문에 Balanced/Shot 중심 정책으로 수렴한 것이 우선 원인이다.

### 다음 명령에서의 정확한 재개 순서

1. 사용량 비율에 따른 실행 제한은 폐지했다(2026-09-23). 오류·중단 시 증거와 재개점을 보존한다.
2. 킥 플레이트의 실제 strike를 intent(Pass/Shot)와 kickId로 발행하고, 다른 아군의0.20초 실제 소유·순변위3m를 만족한 패스만 `CompletedPass`, 실제 shot plate strike만 `ValidShot`, 소유 중 최고 전진5m만 `AdvancedFiveMeters`로 원장에 연결한다. 명령 선택 자체에는 보상하지 않는다.
3. `MNG_TacticalTargetResolver`가 전술적으로 위험하다는 이유만으로 물리적으로 가능한 패스를 mask하지 않도록 한다. 거리 내 활성 동료는 후보로 유지하고 lane clearance/전진성은 receiver 점수에만 사용한다. 관측133/행동[6] 계약은 바꾸지 않는다.
4. 실제 킥/수신/상대 개입/우연 접촉 회귀, EditMode, 전체 MNG PlayMode, Builder2회와 ProjectSettings SHA를 검증하고 변경된 실행 의미의 random-valid100 기준선을 새 protocol로 고정한다.
5. 기존 r001을 보존하고 새 `MNG_M1Attack-YYYYMMDD-r002`를 최소100k 학습한다. 100k 고정100을 평가하고 미달이면 같은 optimizer를 문서화된 최대300k까지만 이어 200k/300k를 평가한다. 76골·자책0 통과 전에는 M1B/M2/M3를 진행하지 않는다.
6. r002 YAML은 `keep_checkpoints: 16` 이상으로 두고100k/200k/300k numbered PT·ONNX의 SHA manifest를 매 평가 전에 저장한다.

## 13:57 골키퍼 시선·경합 정체 해소·관전 카메라 복구 — 아래 모든 과거 기록보다 우선

- 골키퍼 이동은 목표 위치와 바라보는 위치를 분리했다. `MNG_PlayerMotor`의 새 facing-target 이동 경로를 사용해 Keeper가 전진·후진하는 동안 항상 현재 공을 향한다. 실제 PlayMode에서 공 반대 방향으로 후퇴하면서 공 방향 dot>0.85를 확인했다.
- 정면 경합 정체는 양 팀의 가장 가까운 경합자1명씩을 찾고, 공이0.55m 안에0.60초 머물면 `MNG_ContestedBallEscape`를 발동한다. 첫 대칭 측면 이동안은300초 동안168회 발동했는데도 공 최대 이동0.72m라 폐기했다. 최종안은1.40초 주기마다 Red/Navy 중 한 팀을 교대로 공을 빼는 주체로 정하고 상대는 공에서 물러나게 한다. 직접 순간이동·강제소유·직접 공 impulse는 없으며 실제 킥 플레이트와 motor만 사용한다.
- 최종 경합안의300초 Fallback 재현 결과는 `1:1`, 소유 전환81회, 탈출3회, 공 최대 이동61.71m다(`Logs/MNG-Contest-WinnerYield-PlayMode.xml`). 기존 중앙 교착 재발을 막기 위해 전체 경기 PlayMode에 공 최대 이동5m 초과 gate를 추가했다. 정면 배치에서 서로 반대 측면 명령이 나오는 단일 검사는 `Logs/MNG-Contest-Target-Retest-PlayMode.xml` 1/1 통과다.
- 기존 비활성 Main Camera/AgentCamera9개를 생성기에서 제거하고, 활성 `MainCamera` 하나와 `MNG_SpectatorCamera`를 MNG 프리팹에 생성한다. 기본은 `(0,100,-100)` 중계 시점, H로 Red Striker를 Human 전환하면 선수 뒤를 추적하고 AI 복귀 시 중계 시점으로 즉시 돌아간다. 단일 전환 검사는 `Logs/MNG-Camera-Retest-PlayMode.xml` 1/1 통과다.
- Builder 생성/검증은 `Logs/MNG-Camera-Contest-Generate.log`에서 통과했고, 전체 EditMode는188/188 통과했다(`Logs/MNG-Camera-Contest-EditMode.xml`). 최종 제품 코드가 포함된 전체 PlayMode r3/r4는 각각19/20이며 실패1건은 제품 assertion이 아니라 Trainer 없는 학습 Scene의 첫 FixedUpdate에서 의도된 `MNG diagnostic heuristic is disabled`가 테스트보다 먼저 실행된 순서 경합이다. 제품 대상 골키퍼·카메라·경합·300초 경기 검사는 모두 통과했다.
- 중단 직전 모든 정책 Scene 검사에서 ML-Agents Academy 자동 스텝을 장면 로드 전에 끄고 Manager 비활성화 뒤 복구하도록 테스트 격리를 공통 수정했다. 이 마지막 테스트 전용 수정은 사용자의 중단 요청에 따라 아직 Unity 재컴파일/전체20개 재실행을 하지 않았다. 다음 재개 첫 작업은 새 log/XML로 MNG 전체 PlayMode20/20을 확인하는 것이다.
- 테스트가 제거한 Standalone define은 `APP_UI_EDITOR_ONLY;SENTIS_ANALYTICS_ENABLED`로 복구됐다. 새 학습, 기존 Run 재개/덮어쓰기, commit, push, reset credit 사용은 하지 않았다. 기존 M1/M2/M1B 승인 모델은 보존되며, 실행 의미가 바뀌었으므로 다음 단계 전에 frozen 재평가가 필요하다.

### 정확한 다음 재개 순서

1. 사용량 비율에 따른 실행 제한은 폐지했다(2026-09-23). 오류·중단 시 증거와 재개점을 보존한다.
2. 컴파일 후 MNG 전체 PlayMode를 새 r5 XML/log로 실행해20/20과300초 공 최대 이동5m 초과를 확인한다. Academy 순서 경합 예외가0인지 확인한다.
3. ProjectSettings/EditorBuildSettings/Core Prefab SHA와 Standalone define을 다시 확인한다.
4. 승인 frozen M1, M2, M1B를 변경된 저수준 실행 계약으로 새 evidence revision에 재평가한다. 기존 승인 근거와 모델은 덮어쓰지 않는다.
5. 세 frozen gate가 유지될 때만 아직 미승격인 M3 r001의300초×40경기 정식 평가를 실행한다. 실패 시 gate를 완화하지 않고 새 r002 원인을 분리한다.

## 12:20 역할별 이동·킥오프 스폰·M1B 이동 수비 승인 — 아래 모든 과거 기록보다 우선

- 포메이션 스폰 오차는 최초 Scene 배치와 득점 뒤 중앙 킥오프 `ResetRound`에서만 적용한다. 경기 중 `FixedUpdate`에는 Transform 보정이 없으며 `SpawnPlacementRevision` 회귀가 일반 플레이30 tick 동안 불변, 득점 뒤 정확히1회 증가, 이후 다시 불변임을 확인한다. 상황형 M1/M2/M3는 각 episode 시작 배치에만 오차를 적용한다.
- Manager 관측133/행동[6] 계약은 유지하고 `MNG_TeamPlanner`를 v2로 올렸다. Keeper/MidLeft/MidRight/Striker의 formation anchor·공 추종 비율·lane/depth를 분리했고, 수비 시 가장 가까운 비골키퍼 한 명만 primary presser로 정한다. 담당은1.25초 유지하고 다른 선수가3m 이상 유리할 때만 교체한다. Move/Cover/Receive 목표는 최소3m 분리한다.
- 이 실행 의미 변경에 따라 M1/M2/M3 protocol v3를 새로 만들고 v1/v2는 보존했다. 새 random-valid 기준선은 M1 `66득점/자책0/timeout34`, M2 `빠른회수52/전체57/실점45/timeout55`다. v3 gate는 M1 요구76·자책0, M2 빠른회수60·최대실점44다.
- 기존 frozen M1은 v3에서 `99득점/자책0/timeout1`로 통과했다(`Logs/MNG_M1Attack-20260910-r001-eval-r005`). 기존 frozen M2는 `빠른회수62/전체64/실점35/timeout65`로 통과했다(`Logs/MNG_M2Defense-20260910-r001-eval-r003`). 각 ONNX SHA는 기존 승인 모델과 동일하다. M1 결과는 여전히 정지 Navy 범위다.
- 기존 M1A를 덮어쓰지 않고 `M1_AttackMoving` Scene/Player/YAML/평가 protocol을 추가했다. Navy `MNG_FallbackManager`와 네 실행기를 활성화하며 PlayMode에서 Navy 선수의 실제 위치 이동을 확인한다. moving-defense random-valid 기준선은 `20득점/자책0/timeout80`, 승격 gate는 `30득점/자책0`이다.
- `MNG_M1Moving-20260910-r001`은 승인 M1 r001에서 initialize-from해 `20,006` step을 정상 완료했다. 최종 ONNX SHA-256은 `FAF9EAE392C604042C14787C0548E300594271BF8BF5BC85CE44E87FC3A6C8D5`; checker 통과, 최종 최근5 summary 평균 보상은 약0.294이며 학습 완료 자체와 품질 판단을 분리했다.
- M1B frozen validation seed21001의100개 미러 상황은 `33득점/자책0/timeout67`로 gate를 통과했다(`Logs/MNG_M1Moving-20260910-r001-eval-r001`). build manifest는 model/protocol/exe/level0 SHA와 `movingNavyDefense=true`를 고정한다. Unity 눈검사용 승인 Scene은 `Curriculum/M1_AttackMoving/Scenes/MNG_M1_AttackMoving_Evaluation.unity`, 모델은 `Models/MNG_M1Moving-20260910-r001.onnx`다.
- 최종 회귀는 EditMode `40/40`(`Logs/MNG-M1Moving-EditMode.xml`)와 PlayMode `17/17`(`Logs/MNG-M1Moving-PlayMode-r2.xml`)이다. 첫 전체 PlayMode는 새 배선 검사에서 Red Agent를 정리하지 않아 다음 테스트에 Heuristic 예외가 누적되어 `7/17`이었고 실패 로그를 보존했다. 테스트 격리를 수정한 r2만 최종 근거다.
- MNG 학습 진행도 검사를 위해 `Tools/inspect_soccer_training.py`가 단일 TensorBoard behavior 폴더를 자동 인식하도록 확장했다. MNG에는 Soccer L1/L2 gate를 적용하지 않고 `unconfigured-behavior/promotion_decision=false`로 표시한다. 시작·종료 증거는 `Logs/MNG_M1Moving-20260910-r001-startup.json`, `...-final.json`이다.
- 사용량 비율에 따른 실행 제한은 폐지했다(2026-09-23). 오류·중단 시 증거와 재개점을 보존한다.

## 11:05 M1 Navy·스폰·개별 이동 감사와 v2 재평가 — 아래 모든 과거 기록보다 우선

- 사용자 검토를 위해 임시 변경됐던 Catalog의 `m1TimeScale`과 `m2TimeScale`을 모두 `20`으로 복구했다.
- M1 Navy 정지는 로컬 실행 오류가 아니다. `MNG_CurriculumController.Start()`는 M1에서 Navy Fallback을 끄고 Navy 4명의 `MNG_PlayerSkillExecutor`를 모두 비활성화한다. 따라서 승인 M1 r001은 정지 수비수를 상대로 학습·평가됐다. 계획의 “정지/느린 수비” 중 느린 수비 변형은 구현되지 않았고, M1 결과는 움직이는 상대에 대한 공격력을 증명하지 않는다. M2에서는 Navy Fallback이 활성화된다.
- 포메이션 기준 위치에 축별 최대 `±0.75m`의 결정적 스폰 오프셋을 추가했다. 일반 MNG 새 경기와 M1/M2/M3 상황 생성에 적용되며, 같은 seed/상황은 재현되고 좌우 평가쌍은 X가 같고 Y가 반전된다. 확정 소유자는 킥 플레이트 드리블 앵커를 유지한다. 생성기 버전은 모두 v2이며 v1 protocol/evidence는 삭제하지 않았다.
- v2 독립 무작위 기준선은 M1 `58득점/자책0/timeout42`, M2 `빠른회수66/전체회수78/실점50/timeout50`이다. v2 gate는 M1 요구득점70, M2 빠른회수60·최대실점49로 고정했다.
- 기존 frozen M1 ONNX를 v2에서 재평가한 결과 `87득점/자책0/timeout13`, 통과했다(`Logs/MNG_M1Attack-20260910-r001-eval-r004`). 이 결과도 정지 Navy 조건이다. 기존 frozen M2 ONNX는 `빠른회수71/전체회수73/실점40/Red득점2/timeout58`, 통과했다(`Logs/MNG_M2Defense-20260910-r001-eval-r002`). 모델 SHA는 v1 승인 때와 동일하다.
- 회귀는 EditMode `37/37`(`Logs/MNG-SpawnJitter-V2-EditMode.xml`)와 PlayMode `14/14`(`Logs/MNG-SpawnJitter-PlayMode.xml`)가 통과했다. Player build 오류0, 기존 Sentis warning485다.
- 개별 움직임의 실제 주체는 신경망이 아니라 `MNG_TeamPlanner`다. `MNG_ManagerAgent`는6개 팀 명령 중 하나만 고르고, `MNG_MatchController.DispatchTeamPlan()`이 네 슬롯 task를 만든 뒤 `MNG_PlayerSkillExecutor`가 목표점으로 이동한다. 비소유 선수 목표가 대부분 `ball + 동일 depth + slot별 고정 lane`이라 공이 움직이면 여러 목표가 같은 벡터로 이동하고, 동일 motor가 같은 방식으로 추종해 평행·동기 이동처럼 보인다. 현재는 역할별 home zone, 동적 역할 배정 비용, 임무 유지 hysteresis, 동료 분리, 경로 회피가 없다.

### 권장 개별 이동 개선 순서

1. Manager 관측133/행동[6]은 유지하고 저수준 플래너만 개선한다. 신경망이 선수별 조이스틱8개를 직접 출력하도록 계약을 확대하지 않는다.
2. 각 팀에 역할별 formation anchor를 두고 공 위치만 따르는 비율을 역할·소유 상태별로 다르게 한다. Keeper는 골문, 두 Midfielder는 서로 다른 half-space, Striker는 전방/후방 연계 영역을 기준으로 한다.
3. 매 결정마다 carrier, primary presser, cover, outlet/receiver를 별도 배정한다. 현재 위치에서 후보 목표까지의 거리·역할 적합도·상대 위협을 비용으로 쓰고, 작은 차이로 담당자가 계속 바뀌지 않도록 hysteresis를 둔다.
4. 비담당 선수는 공으로 직접 평행 이동하지 않고 자신의 anchor와 공 기반 support target을 혼합한다. 동료가 가까우면 target을 분리하고, 도착 반경·속도 계수를 역할별로 달리한다.
5. task/target/slot trace와 최소 동료 거리, 동일방향 동시이동 비율을 추가한 뒤 정적 단위검사→MNG PlayMode→frozen M1/M2/M3 재평가 순서로 검증한다. 실행 의미가 바뀌므로 기존 ONNX 호환만으로 성능 유지라고 간주하지 않는다.
6. M1의 움직이는 공격 상대는 기존 M1A를 덮어쓰지 않고 별도 M1B curriculum/Run으로 추가한다. 정지→감속 Fallback→정상 Fallback 순으로 난도를 올린 뒤 새 moving-defense 평가를 통과해야 “움직이는 수비 상대 공격”으로 승인한다.

## 05:15 M3 r001 학습 완료·정식 평가 준비 — 아래 모든 과거 기록보다 우선

- 사용자 확인으로 M0 사람 검토를 합격 처리했고, 이미 승격된 M1/M2 뒤의 M3 개발을 진행했다.
- M3는 동일 관측133/행동[6]/결정0.5초 계약으로 Red PPO 1개와 Navy `MNG_Fallback` 1개만 활성화한다. 고정 seed의 kickoff/Red 소유/Navy 소유/중립 경합을 좌우 대칭으로 섞는다. 60초 수집은 `EpisodeInterrupted`, 300초 경기는 승패 terminal이며, 득점은 경기 종료가 아니라3초 GoalPause 후 계속된다.
- `MNG_M3Fallback-20260910-r001`은 승인 M2 PT에서 initialize-from하여 `100,080` step을 정상 완료했다. 최종 frozen ONNX SHA-256은 `FE69ECD1893994D57C8D008C5AEF7B087A0FC6531682660F099504729C3A4B1E`, 최종 PT SHA-256은 `962DC8BF8BC29E8307DA579A481589351B86D5E8716EA40B049C7FAD5C2B298E`이다. 중간 PT/ONNX 39,960/59,880/79,920/99,960 및 최종100,080을 기존 Run 안에 보존했다.
- 학습 로그의 최근 구간 평균 보상은 대부분0이고 간헐적으로 `-0.125`였다. 정상 완료와 정책 품질은 별개이므로 M3는 아직 승격하지 않는다. 이 r001은 삭제하거나 덮어쓰지 않고 frozen 평가 대상으로만 사용한다.
- 정식 M3 평가 코드를 추가했다. validation seed23001, 300초×40경기, frozen Red `InferenceOnly` 대 Navy Fallback이며, gate는 scoreRate>=0.50, 무득점경기<=8/40(20%), 양 팀 실제 득점>=1이다. 모델/protocol/exe/level0 SHA를 manifest에 고정하고 새 evidence revision만 허용한다.
- 평가 계약 EditMode는 `36/36` 통과했다(`Logs/MNG-M3-Eval-EditMode.xml`). 정식 평가 Player 빌드와40경기 실행은 아직 시작하지 않았다.
- 05:15 KST에 5시간 사용량 `94% 사용 / 6% 잔여`를 확인해 사용자 중단선(잔여8% 이하)에 도달했다. 학습은 이미 완료·저장됐고 새 장기 평가를 시작하지 않았다. 마무리 후 최종 확인은 `99% 사용 / 1% 잔여`다. reset credit 1개는 사용하지 않았다.

### 정확한 다음 재개 순서

1. 사용량 비율에 따른 실행 제한은 폐지했다(2026-09-23). 오류·중단 시 증거와 재개점을 보존한다.
2. 설정 기준 SHA와 잔류 프로세스를 재확인한다. ProjectSettings/EditorBuildSettings/Core Prefab 기준은 각각 `54FE0BA0...D49D85` / `AE8E2777...68A104` / `A88CA3D0...322B46`, Standalone define은 `APP_UI_EDITOR_ONLY;SENTIS_ANALYTICS_ENABLED`다.
3. 다음 명령으로 새 평가 증거를 만든다: `& '.\Tools\MNG_EvaluateM3.ps1' -RunId 'MNG_M3Fallback-20260910-r001' -EvidenceId 'MNG_M3Fallback-20260910-r001-eval-r001'`.
4. 정확히40경기 결과와 Player 로그 오류0, frozen ONNX/build manifest SHA 일치를 확인한다. scoreRate·무득점·양팀득점 gate를 모두 통과할 때만 M3를 승격한다.
5. 실패하면 r001을 보존하고 보상 원장을 먼저 감사한다. 특히 계획서의 유효슛/실제패스수신/전진5m/실제회수 shaping이 M3 60초 수집에 실제 지급되었는지 확인한 뒤, 단일 원인 수정으로 `MNG_M3Fallback-20260910-r002`를 새로 학습한다. gate를 사후 완화하지 않는다.
6. 정식40경기와 별도로 D2 최소 시연(양 팀 실제 감독 ONNX 10 seed쌍×60초, 각 팀 득점>=1, 득점 경기>=8/10, 사건 trace) 및 최소1개300초 시연은 M3 최종 승격 근거에 추가한다.

## 04:35 M1/M2 승격 완료 — 아래 모든 과거 기록보다 우선

- 사용자가 M0의 실제 화면·킥 플레이트·조작감·HUD/H키 검토를 모두 합격으로 확인했으므로 M0를 마감했다.
- M1 `MNG_M1Attack-20260910-r001`은 단일 Red PPO로20,007 step을 완료했다. frozen ONNX SHA는 `3B1D69BBE79E6E458D4F2117B0D51E20D6D38FFCC2C519C0314E70580685DCCB`이며, validation seed21001의 고정100상황에서92득점·자책0·timeout8로 요구75득점을 통과했다. 근거는 `Logs/MNG_M1Attack-20260910-r001-eval-r003/`이다.
- M2 무작위 유효명령 사전 기준선은 seed22001에서10초 내 회수66·전체회수77·실점55다. `MNG_M2Defense-20260910-r001`은 승인 M1 PT에서 initialize-from하여20,004 step을 완료했고, frozen ONNX SHA `B60306FE77C060BD770B1D2512CC5782A169571B62C9D2015093D7310CB73000`으로10초 내 회수63·전체회수68·실점29·timeout71을 기록했다. 최소회수60과 무작위보다 적은 실점(최대54)을 모두 통과했다. 근거는 `Logs/MNG_M2Defense-20260910-r001-eval-r001/`이다.
- 평가 Player는 Trainer 없이 `InferenceOnly`로 실행했고 모델·protocol·exe·level0 SHA를 build manifest에 고정했다. M1 평가 r001은 sandbox 초기화 중단, r002는 잘못된 MonoScript 직렬화로 level0 역직렬화 실패였으며 둘 다 보존했다. MonoBehaviour를 동일 이름 파일로 분리한 r003만 M1 합격 근거다.
- 최신 전체 회귀는 EditMode `34/34`(`Logs/MNG-M2-Final-EditMode.xml`)와 PlayMode `12/12`(`Logs/MNG-M2-Final-PlayMode.xml`)다. PlayMode에서 드리블 직선/좌/우 각20/20, 패스5/10/20m 각20/20, 슛10/20m 각20/20, 양 팀 탈취20/20을 재확인했다.
- 전체 묶음 회귀의 물리 기준선은 독립 실행과 소폭 달랐다(M1 득점64 대65, M2 빠른회수67 대66·실점52 대55). 사전 고정한 독립 기준선을 사후 변경하지 않았고 두 모델의 승격 여유는 이 변동보다 크다. 이는 Unity 물리의 실행 간 완전 비트 결정성을 증명하지 못한다는 제한으로 남긴다.
- ProjectSettings/EditorBuildSettings/Core Prefab은 테스트 후 Standalone define을 복구하면 기준 SHA와 일치한다. commit·push·force·기존 Run 덮어쓰기는 하지 않았다.

### 정확한 다음 순서

1. 사용량 비율에 따른 실행 제한은 폐지했다(2026-09-23). 오류·중단 시 증거와 재개점을 보존한다.
2. M3_FallbackMatch를60초 혼합 시작 상태와 Navy Fallback 상대, 동일133/[6]/0.5초 계약으로 구현한다. 득점은 episode 종료가 아니라3초 GoalPause이고60초 수집 제한만 interrupted로 끝낸다.
3. 승인 M2 PT에서 새 M3 Run을 initialize-from하고100k 또는3시간 상한을 적용한다.
4. 고정10 seed쌍×60초 D2 평가와 최소1개300초 경기를 frozen ONNX로 실행한다. 정식40경기 scoreRate>=0.50은 별도 승격 gate다.

## 00:12 MNG 킥 플레이트 완료 상태 — 아래 모든 과거 중단 기록보다 우선

- 사용자 결정에 따라 기존 킥 플레이트 기믹을 제거하지 않고 MNG 전용 `MNG_KickPlate`로 분리했다. Builder는 기존 center+wing 3개 Collider 형상을 보존하고 legacy `SoccerKickPlate` 컴포넌트만 제거한다. 최종 MNG Prefab에는 `MNG_KickPlate` 8개, legacy plate controller 0개가 있다.
- 패스·슛 요청은 더 이상 공 Rigidbody에 직접 impulse를 주지 않는다. `TryKick`은 소유자 plate를 무장하고, plate가0.08초 전진해 자기 Collider로 공에 실제 접촉한 한 번만 token을 소비해 목표 출구속도 impulse를 준다. plate는0.50초에 걸쳐 수축하며 OnCollisionEnter/Stay 반복으로 중복 힘이 발생하지 않는다.
- plate 수축 위치 전방1.32m를 드리블 위치로 정의했다. 공이 반경0.72m에 놓이면 실제 충돌이 없어도1 fixed tick(0.02초) 후 소유가 된다. 전진 명령속도0.40m/s 이상·전방 정렬내적0.70 이상일 때만 제한된 가속/감쇠로 공이 plate 앞을 따르며, parenting·teleport는 없다.
- 정지·후진·옆걸음에서는 보정을 즉시 끄고0.08초 뒤 소유를 해제한다. 정지한 선수가 제자리에서 즉시 재획득하지 못하게 다시 전진하거나 공이1.02m release 범위를 벗어날 때까지 차단한다. 상대 plate의 드리블 zone은 유효 탈취 후보이며0.02초 확인, 이전 소유자 잠금0.12초로 쉽게 탈취되도록 했다.
- `MNG_PlayerAvatar`는 전용 plate와 cooldown reset을 소유하고, `MNG_PlayerMotor`는 마지막 명령속도를 기록한다. `MNG_BallControl`은 plate 접촉만 세고, 실제 strike·드리블 보정·정지 해제·탈취·킥 후 재흡착 차단을 단일 writer로 처리한다. 경기/round reset은 plate, drive, possession, cooldown을 함께 초기화한다.

### 최신 자동 검증

| 계층 | 최신 결과 | 근거 |
|---|---:|---|
| EditMode | `29/29` | `Logs/MNG-KickPlate-EditMode-r3.xml` |
| PlayMode 전체 | `8/8` | `Logs/MNG-KickPlate-PlayMode-r2.xml` |
| 실제 plate 패스 | 5m·10m·20m 각각 `20/20` | 전체 PlayMode의 `UnopposedPassesAtFiveTenAndTwentyMetersAreActuallyReceived` |
| 실제 plate 슛 | 10m·20m 각각 `20/20` | 전체 PlayMode의 `OpenGoalShotsFromTenAndTwentyMetersReachNinetyPercent` |
| plate 드리블 | 직선·좌회전·우회전 각각 `20/20`, 정지·후진 소유 해제 | `Logs/MNG-KickPlate-Dribble-PlayMode-r5.xml`, 전체 PlayMode 재확인 |
| 상대 탈취 | 전체 `20/20`, Red/Navy 각각 `10/10`, 모두0.2초 이내 | `Logs/MNG-KickPlate-Steal-PlayMode-r3.xml`, 전체 PlayMode 재확인 |
| Fallback 300초 | `15,001` fixed tick 완주, NaN·공 소실·중복득점0 | `Logs/MNG-Fallback300-PlayMode-r1.xml`, 전체 PlayMode 재확인 |
| Builder | 연속2회 validation/build/batch 통과 | `Logs/MNG-KickPlate-Generate-r3.log`, `Logs/MNG-KickPlate-Generate-r4.log` |
| Windows Player | Succeeded, 오류0, 기존 Sentis warning485, exe667,648 bytes | `Logs/MNG-KickPlate-Windows-Build-r1.log`, `Builds/MNG_Training/build-info.json` |

- 300초 Fallback 경기는 `0:0`, possession transition593회였다. 장기 물리/상태 안정성은 통과했지만 득점 능력·전술 품질·RL 성능은 증명하지 않는다.
- 최종 exe SHA는 `D3DB57B9B88A2D8FEDD1621461DDD7B29ED18001EFD29A93EF9F31D4E8A483CD`, level0 SHA는 `8692F853D61E65C77F204DEB76C736CC09E0A95D52FC3ABE4C9A868BEBF4656D`, trainer config SHA는 `8D8F7AD80D83AAB1B87F22E9F868B01D48193C656E34C9B6231CEEBDF6765C68`이다.
- 테스트가 바꾼 Standalone define은 `APP_UI_EDITOR_ONLY;SENTIS_ANALYTICS_ENABLED`로 복구했다. ProjectSettings/EditorBuildSettings/Core Prefab SHA는 각각 기준 `54FE0B...D49D85` / `AE8E27...68A104` / `A88CA3...22B46`과 일치한다.
- 진단 실패 XML/log를 삭제하거나 덮어쓰지 않았고 commit·push·force·기존 training Run 변경을 하지 않았다. r006/r007은 계속 PPO 연결 smoke일 뿐 성능 모델이 아니다.
- 사용량 비율에 따른 실행 제한은 폐지했다(2026-09-23). 오류·중단 시 증거와 재개점을 보존한다.

### 남은 M0 사람 검토와 다음 순서

1. Unity Editor 또는 최신 Windows Player에서 plate가 패스/슛 때 눈에 보이게 전진·수축하고, 공이 실제 plate에서 출발하며 중복 발사가 없는지 확인한다.
2. Red Striker를 H키로 AI→HUMAN→AI 각20회 전환한다. 전환 중 위치·점수·시간·공 상태가 보존되고, HUMAN 동안 나머지3명과 Navy는 계속 동작하는지 확인한다.
3. W/A/S/D 전진·좌우 회전으로 공이 plate 앞에서 자연스럽게 따라오는지, 손을 놓거나 S로 후진하면 공이 즉시 보정에서 풀리고 쉽게 빼앗기는지 확인한다. 정지 때 튀김·진동·흡착처럼 보이는 현상이 없어야 한다.
4. 소유 중·패스 비행 중·GoalPause 중 각각 H 전환을 확인하고, HUD의 `AI/HUMAN`, 점수, 시간, 양 팀 명령이 실제 상태와 일치하는지 확인한다. 키보드를 우선 확인하고 게임패드는 입력 경로가 마련된 뒤 별도 확인한다.
5. 위 사람 검토에 결함이 없으면 M0를 마감한다. M1 장기 성능 훈련/M2 self-play는 별도 gate이며 자동 plate 성공률이나 r007 연결 smoke를 RL 성능으로 승격하지 않는다.

재생성 명령의 실제 진입점은 `MachineLearning.Soccer.Manager.Editor.MNG_ProjectBuilder.BuildM0StadiumBatch`다. 과거 기록의 `BuildAllBatch` 표기는 잘못됐으므로 사용하지 않는다.

## 21:49 재개 후 최신 상태 — 아래 모든 과거 중단 기록보다 우선

- M0 열린 골문 gate의 원인을 분리했다. 선수 자식 Collider를 모두 비활성화하고 공 초기화 시간을 늘렸으며, 실제 damping에서20m에 못 미치던 강킥을24→28m/s로 확정했다. `Logs/MNG-speed28-PlayMode.xml`은10m `20/20`, 20m `20/20`, 전체 PlayMode `4/4` 통과다.
- 24방향×5/20/40m의72개 motor 접근 fixture를 추가했다. 도착 오차0.5m 이하·정지속도0.5m/s 이하·NaN0·필드이탈0 조건으로 최신 전체 EditMode `26/26`을 통과했다(`Logs/MNG-pass-fix-EditMode.xml`). 이는 순수 motor 수치 gate이며 실제 Rigidbody 충돌/화면 품질 증거는 아니다.
- 실제 Rigidbody 패스 수신 fixture를5/10/20m×20방향으로 추가했다. 다른 선수 Collider를 끄고 지정 수신자와 실제 충돌한 뒤 `MNG_BallControl.Carrier`가 그 선수가 된 경우만 성공으로 센다.
- 복합 선수 Collider는 실제 접촉해도 rigidbody 중심거리가 기본 bounds 획득 반경보다 컸다. 수신 가능 속도18m/s 이하의 **실제 충돌**이 먼저 있었을 때만0.24초 동안 그 충돌의 실측 중심거리를 인정하고 공 상대속도를 감쇠하도록 수정했다. 비접촉 원거리 소유권은 허용하지 않는다.
- 자동 패스는 약킥14m/s를 고정 사용하지 않고 거리별로10m 이하14m/s, 10~20m 14→28m/s 선형 보간, 20m 이상28m/s를 사용한다. 최종 단일 gate `Logs/MNG-pass-distance-speed-PlayMode.xml`은5m·10m·20m 모두 `20/20`, `1/1` 통과다.
- `ResetMatch()`가 점수/위치뿐 아니라 실제 possession ledger와 접촉 진단도 초기화하도록 연결했다. 반복 fixture 사이의 소유 상태 누수를 막는다.
- 최신 패스 코드까지 포함한 전체 EditMode는 `26/26` 통과했다. 다만 최신 전체 PlayMode(예상5개), Builder 재생성, Windows Player 재빌드는 아직 실행하지 않았다. 직전 전체 PlayMode `4/4`와 패스 단일 `1/1`은 서로 분리된 증거다.
- 현재 남은 M0 자동 gate는 실제8m 운반(직선/좌/우), 접촉 탈취/비접촉 강탈0/양팀 대칭, Fallback 대 Fallback300초 완주다. HUD/H 양방향 전환과 조작감은 실제 화면 검증으로 별도 남아 있다.
- 이번 재개 중 진단 실패 로그/XML을 삭제하거나 덮어쓰지 않았고 commit·push·force·기존 Run 변경을 하지 않았다. r006/r007은 계속 연결 smoke일 뿐 정책 성능 모델이 아니다.
- 21:50 KST 기준 5시간 사용량은 `93% 사용 / 7% 잔여`로 사용자 지정 중단선(잔여8% 이하)에 도달했다. 그래서 아래 재개 순서를 저장하고 추가 Unity 실행·빌드·훈련을 중단했다. reset credit 2개는 사용하지 않았다.
- 종료 점검에서 Unity/Trainer/MNG Player 잔류 프로세스는0이다. Standalone define은 `APP_UI_EDITOR_ONLY;SENTIS_ANALYTICS_ENABLED`로 복구했고, ProjectSettings/EditorBuildSettings/Core Prefab SHA는 각각 기존 기준 `54FE0B...D49D85` / `AE8E27...68A104` / `A88CA3...22B46`과 일치한다.

### 정확한 다음 재개 순서

1. 사용량 비율에 따른 실행 제한은 폐지했다(2026-09-23). 오류·중단 시 증거와 재개점을 보존한다.
2. 최신 전체 PlayMode를 새 XML/log로 실행해 `5/5`와 열린 골문10/20m·패스5/10/20m 각각 `20/20`을 확인한다.
3. `MNG_ProjectBuilder.BuildAllBatch`를 실행하고 M0/M1 Scene 계약 및 Builder 재실행 배율 누적0을 확인한다.
4. Windows training Player를 재빌드하고 오류0, exe/level0 SHA, ProjectSettings/EditorBuildSettings/Core Prefab 불변성을 다시 기록한다. Unity 테스트가 제거한 Standalone define은 `APP_UI_EDITOR_ONLY;SENTIS_ANALYTICS_ENABLED`로 복구한다.
5. 8m 운반 fixture와 접촉 탈취 fixture를 구현·통과시킨 뒤 Fallback 대 Fallback300초 자동 완주를 실행한다.
6. 실제 화면에서 HUD, H 양방향 전환, 소유중/패스비행/GoalPause, AI→Human writer0을 확인한다. 자동 수치와 실제 영상 판정을 섞지 않는다.
7. 위 M0 gate가 모두 통과하기 전에는 M1 성능 훈련이나 M2/self-play를 시작하지 않는다. 필요 시 r008은 최신 Player의 연결 회귀용 새 Run으로만 실행하고 기존 Run을 재개·덮어쓰지 않는다.

## 18:27 재개 후 최신 상태 — 아래 과거 중단 기록보다 우선

- 현재 티켓은 **M0 런타임 완성 및 M1-A PPO 연결**이다. M1-A 연결 gate는 `MNG_M1Smoke-20260909-r006`과 최신 `r007`로 통과했지만, 이는 정책 성능 승인이 아니다.
- 전술 목표 누락을 고쳐 `MNG_TacticalTargetResolver`가 신경망/규칙형에 동일한 예측 수신자와 실제 goal-width 기반 슛 대상을 제공한다. 정책 명령 자체는 선택하지 않는다.
- M0-B 누락이던 `MNG_HudPresenter`를 구현했다. 공통 Soccer UXML/PanelSettings를 재사용하고 점수·시간·HUMAN 상태·양 팀 현재 명령·누적 보상을 표시하며 legacy `SoccerHudController`는 남기지 않는다.
- 안정 통과 근거는 Builder 통과(`Logs/MNG-hud-Generate-r2.log`), EditMode `25/25`, 기본 PlayMode `3/3`, Windows Player 오류0/기존 Sentis warning485/exe667,648 bytes다. 기본 PlayMode는 양쪽 골로 실제 공을 발사해 득점1회·GoalPause·중앙 reset도 확인한다(`Logs/MNG-physical-goal-PlayMode-r2.xml`). 이후 추가한 열린 골문 성공률 gate는 아래와 같이 실패했으므로 최신 전체 PlayMode는 `3/4`이며 M0 기술 gate는 미통과다.
- r007은 단일 `MNG_Manager?team=0`, Bootstrap activeManagers1, 1,112 environment step, policy update2, optimizer update33, checkpoint2로 정상 종료했다. 관찰 누락·예외·reward profile 누락·missing script·kinematic·masked command 오류는 모두0이다.
- r007의 1,100 decision 집계에서 pass mask가22회 열리고 `PassBuild`가5회 선택됐다. shot mask는0회였다. 짧은 무학습 smoke가 슛 지역 도달이나 전술 품질을 증명하지 않으며, 실제 goal width 판정은 EditMode 회귀로만 확인했다.
- r007 root ONNX SHA `A6779CDA3D495839A1551CFF8FF0DEE361BF47B523BABA9EE01E3FD31A21760B`, checkpoint SHA `A232445432703F345FDC7F95C1ECAB116939F9D23F5CDF76A2C139782C1D2533`이다. 연결 smoke 모델이므로 성능 모델로 등록하지 않는다.
- 최종 Player exe SHA `D3DB57B9B88A2D8FEDD1621461DDD7B29ED18001EFD29A93EF9F31D4E8A483CD`, level0 SHA `15C144539EAA6D980B896ECA6CD20ACCB30824E84DBA75128B8C6816261D33EA`이다.
- `ProjectSettings.asset` SHA `54FE0BA053A6860458DBF2BDEBE716F5F7989E0DF55556519FB9237444D49D85`, `EditorBuildSettings.asset` SHA `AE8E277743DB8A6270B39BCDB61A7369BDD54D342E56FC9AD02D0DFA1368A104`, Core Prefab SHA `A88CA3D038348B0BE9B84E01FA8FFC007D2C9325366CB14212DA52C787322B46`로 기준과 같다.
- 현재 Trainer/MNG Player/Unity 잔류 프로세스는0이다. commit·push·force·기존 Run 덮어쓰기는 수행하지 않았다. reset credit도 사용하지 않았다.
- 마지막 사용량은 5시간96% 사용/4% 잔여였다. 사용자 지정 잔여8% 중단선에 도달해 추가 실행을 중단했다. reset credit은 사용하지 않았다.

### 마지막 M0 열린 골문 실패

- 신규 PlayMode `OpenGoalShotsFromTenAndTwentyMetersReachNinetyPercent`는 실제 Rigidbody 공을 좌우 골 방향과5개 횡 위치로 10m/20m 각각20회 발사한다.
- 첫 실행 `Logs/MNG-open-goal-PlayMode.xml`은 10m `16/20`으로90% gate 미달이었다.
- 선수 방해를 제거하려 모든 선수 Collider를 비활성화했지만, `Logs/MNG-open-goal-PlayMode-r2.xml`도 10m `16/20`, 전체 `3/4` 실패였다. 따라서 수비 방해가 원인은 아니다.
- 재개 시 gate를 완화하지 말고 실패 sign/lane/최종 위치·속도를 attempt별 trace로 기록해 goal surface/frame geometry 또는 ResetMatch/ball state 원인을 고친다. 그 전에는 20m 결과와 열린 골문 gate를 통과로 기록하지 않는다.

### 다음 작업 순서

1. 열린 골문 실패 attempt의 sign/lane/최종 위치·속도 trace를 추가하고 동일40샷을 새 XML/log로 재실행해 16/20 원인을 고친다.
2. 그 뒤 24방향×3거리 접근, 8m 운반, 접촉 탈취, 5/10/20m 패스 fixture를 구현한다. 자동 수치와 실제 영상 판정을 분리한다.
2. Fallback 대 Fallback 300초 완주, 양 팀 득점/회수 사례, 공소실·NaN·중복득점0을 확인한다.
3. 실제 화면에서 HUD, H 양방향 전환, 소유중/패스비행/GoalPause, AI→Human writer0을 확인한다.
4. 위 M0 gate 뒤에만 `MNG_CurriculumController`/Catalog와 `MNG_M1_AttackChoice` 20초 Scene/YAML/고정100 평가 도구를 구현한다.
5. M1은 새 Run·20k decision 또는1시간 상한이며 r006/r007을 초기 성능 모델로 채택하지 않는다. M2/self-play는 M1 승급 전 시작하지 않는다.

아래 내용은 94% 사용 시점의 과거 중단 기록으로 보존한다. 당시 r003 예정은 r003–r007 진단을 거치며 이미 대체되었다.

## 중단 이유와 현재 판정

- 2026-09-09 KST에 5시간 사용량 `94% 사용 / 6% 잔여`를 확인했다.
- 사용자 지정 중단선은 잔여 `8% 이하`이므로 새 구현·Unity 실행·Player Build·훈련을 중단했다.
- 무료 사용량 reset credit은 사용하지 않았다.
- 마지막 변경은 `MNG_TrainingBootstrap`의 활성 Manager 판정을 `manager.enabled`에서 `manager.isActiveAndEnabled`로 고친 것이다. 이 변경 이후의 컴파일·테스트·Player Build·Trainer 스모크는 아직 실행하지 않았다.
- 현재 Trainer, `MNG_Training` Player, Unity 프로세스는 남아 있지 않다.
- commit·push는 수행하지 않았다. 작업 전부터 존재한 다른 Soccer 변경과 과거 Run을 정리하거나 원복하지 않았다.

## 구현된 범위

MNG는 기존 379/[3,3,3,3] 선수 정책과 분리된 감독 정책이다.

- 관측: 팀 기준으로 정규화·미러링한 정확히 `133` floats.
- 행동: `Balanced`, `ProtectLead`, `HighPress`, `WideAttack`, `CentralAttack`, `CounterAttack`의 discrete `[6]`.
- 제어권: 팀당 Manager 1개, 선수 4명은 `MNG_PlayerMotor`/`MNG_PlayerSkillExecutor`; H키는 Red Striker만 Human으로 전환하고 Manager는 나머지 3명을 계속 지휘한다.
- 물리: Core 복사본에서 공 scale `×1.10`, mass `4.5`, bounce `0.05`; 공 parenting/teleport 없이 force/impulse를 사용한다.
- 경기: 300초, 득점 1회 집계, 3초 Goal Pause, reset, possession ledger, command/task revision, 보상 dedupe와 60초 cap.
- 상대: M0에서는 양 팀 explicit `MNG_FallbackManager`; M1 연결 씬에서는 Red PPO endpoint 1개와 Navy fallback.
- 생성: `MNG_ProjectBuilder`가 Core Stadium으로부터 MNG Prefab/M0 Scene/M1 Scene/Profile을 다시 만들고 legacy Agent/DecisionRequester/환경 writer가 남지 않는지 검사한다.
- 도구: `Tools/MNG_Build.ps1`, `Tools/MNG_Train.ps1`; Run 중복 방지와 config/exe/level0 SHA 기록을 포함한다.

주요 시작점:

- `Runtime/MNG_ManagerAgent.cs`
- `Runtime/MNG_MatchController.cs`
- `Runtime/MNG_ObservationWriter.cs`
- `Runtime/MNG_TeamPlanner.cs`
- `Runtime/MNG_PlayerMotor.cs`
- `Runtime/MNG_BallControl.cs`
- `Runtime/MNG_HumanInput.cs`
- `Runtime/MNG_RewardEngine.cs`
- `Editor/MNG_ProjectBuilder.cs`
- `Editor/MNG_TrainingBuildBuilder.cs`
- `Tests/EditMode/MNG_RuntimeContractTests.cs`
- `Tests/PlayMode/MNG_RuntimePlayModeTests.cs`
- `Training/MNG_M1_ConnectionSmoke.yaml`

## 검증 근거와 경계

마지막 Bootstrap 수정 전에 다음이 통과했다.

| 근거 | 결과 | 파일 |
|---|---:|---|
| MNG Builder validation | 통과 | `Logs/MNG-M1-SingleTeam-Scene-Build.log` |
| MNG EditMode | `23/23` | `Logs/MNG-M1-PreBuild-EditMode.xml` |
| MNG PlayMode | `2/2` | `Logs/MNG-M0-PlayMode.xml` |
| Windows training Player | 오류0, 기존 Sentis shader warning487, exe 667,648 bytes | `Logs/MNG-M1-SingleTeam-Windows-Build-Manifest.log` |
| YAML parser | `MNG_Manager`, max_steps1000 확인 | 설치된 ML-Agents `1.2.0.dev0` parser |

자동 통과는 수동 H키 조작감, 실제 Controller, 물리 체감, 장기 경기 안정성, 학습 수렴, 전술 품질을 증명하지 않는다. 특히 아래 r001/r002는 승인 모델이 아니다.

## 보존 Run 판정

### `MNG_M1Smoke-20260909-r001`

- 종료와 artifact는 보존한다.
- Red/Navy 두 Behavior가 모두 Trainer에 연결됐다.
- 의도한 단일 Red 학습 계약을 위반했으므로 실패 진단이며 resume·승급 금지다.

### `MNG_M1Smoke-20260909-r002`

- Trainer에서는 `MNG_Manager?team=0` 하나만 연결됐고 1,024 environment steps를 처리했다.
- `_update_policy` 2회, `TorchPPOOptimizer.update` 36회, 최종 1,024-step ONNX/PT export까지 수행됐다.
- 그러나 Player 로그에는 다음 오류가 있다.
  - `MNG connection smoke requires exactly one active manager, found 2` 1회.
  - `Fewer observations (0) made than vector observation size (133)` 1,025회.
  - `MNG reward profile for Navy is missing` 3회.
- 따라서 r002는 communicator/export 경로 진단일 뿐, 유효한 관측·행동·보상 스모크나 학습 성능 근거가 아니다. resume·force·모델 등록·승급 금지다.
- Bootstrap 예외의 직접 원인은 inactive Navy GameObject에 붙은 enabled Component까지 센 것이며 마지막 코드에서 `isActiveAndEnabled`로 수정했다. 0-observation과 Navy reward 예외가 이 초기화 예외의 연쇄 결과인지 여부는 r003 전 로그 검증으로 확인해야 한다.

## 불변 SHA-256 근거

| 대상 | SHA-256 |
|---|---|
| `Training/MNG_M1_ConnectionSmoke.yaml` | `8D8F7AD80D83AAB1B87F22E9F868B01D48193C656E34C9B6231CEEBDF6765C68` |
| `Builds/MNG_Training/MNG_Training.exe` | `D3DB57B9B88A2D8FEDD1621461DDD7B29ED18001EFD29ED02D0DFA1368A104` |
| `Builds/MNG_Training/MNG_Training_Data/level0` | `02B4BC4E35118AC3194D75536CA73742627C83885A36CB82D2739D173EA8F5B0` |
| r002 root ONNX | `6E6F9287FE01E495571B44EA85F460486941BEA1BE21B540C413603B6B5EDCA5` |
| r002 1,024-step PT | `1DFF12A85421F5EB9F8589F91BD2BDD5CF84FDC0A5009771E9F9F17A4230942E` |

주의: exe SHA는 이전 Soccer build와 같을 수 있어 단독으로 씬 변경을 식별하지 못한다. 실제 serialized scene을 포함한 `level0` SHA와 `Builds/MNG_Training/build-info.json`을 함께 사용한다.

## 정확한 재개 순서

1. 사용량 비율에 따른 실행 제한은 폐지했다(2026-09-23). 오류·중단 시 증거와 재개점을 보존한다.
2. `MNG_TrainingBootstrap.cs`의 `isActiveAndEnabled` 변경을 포함해 MNG EditMode와 PlayMode를 다시 실행하고 XML의 total/passed/failed/result를 직접 확인한다.
3. `MNG_ProjectBuilder.BuildAllBatch`를 다시 실행하고 M0/M1 Scene 계약을 검증한다.
4. `Tools/MNG_Build.ps1`로 Windows Player를 새로 만들고 `ProjectSettings.asset`, `EditorBuildSettings.asset`, Standalone define을 기준값과 비교한다.
5. 새 Player를 Trainer 없이 한 번 실행해 Player 로그에 아래가 모두 맞는지 확인한다.
   - active Manager 정확히 1개.
   - `MNG_Manager?team=0`만 등록.
   - 0/133 observation padding 0회.
   - reward profile 누락 예외 0회.
   - missing script 경고 0회.
6. 위 네 조건이 모두 통과할 때만 새 Run ID `MNG_M1Smoke-20260909-r003`으로 max_steps1000 연결 스모크를 실행한다. r001/r002를 재개하거나 덮어쓰지 않는다.
7. r003에서 단일 brain, 133 관측, 유효 action mask, PPO update, 체크포인트/ONNX, Player/Trainer 오류0을 별개로 확인한다. 이것은 연결 검증이며 정책 품질 승인이 아니다.
8. r003 통과 뒤에만 M0 실제 경기/HUMAN 조작 검증과 문서의 다음 M1 gate를 진행한다. 장기 훈련과 M2 self-play는 그보다 앞서 시작하지 않는다.

## 설정 보존

- 마지막 확인 시 `ProjectSettings/ProjectSettings.asset`과 `ProjectSettings/EditorBuildSettings.asset`은 이번 작업 diff가 없다.
- MNG 테스트 중 Standalone define이 바뀌면 기록된 기준 `APP_UI_EDITOR_ONLY;SENTIS_ANALYTICS_ENABLED`로 복구하고 파일 diff/hash를 다시 확인한다.
- 기존 L0/L1/L2/L3 모델, 결과, 로그, 승인/미승인 판정은 MNG 결과와 섞지 않는다.

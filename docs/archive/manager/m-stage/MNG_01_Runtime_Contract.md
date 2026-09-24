> 보관일: 2026-09-24. 작성 당시의 계획·판정·승인 이력이며 현재 실행 지시가 아닙니다. 현행 기준: [현재 상태](../../../soccer/current-status.md).

﻿# MNG 런타임 계약 v1

상태(2026-09-19): R0 사용자 완료 판정, 이후 감독 RL은 [MS 계획](../ms-v1/ms-plan.md)을 따른다. 아래 현행 공통 물리·기술·관측/행동 계약은 재사용하고, 기존 M 단계의 일정·승급 순서는 MS가 대체한다. MS는 계획만 작성되었으며 변경 시 schema/Profile/테스트/문서를 동기화한다.

## 1. 환경·공

- 기존 Stadium Mesh/Terrain/골문/벽/카메라/선수 외형·몸 Collider 크기 유지. 필드 약124×84.655m지만 실제 Geometry 값을 읽는다.
- Red/Navy 각4명. 고정 슬롯 [Keeper, MidLeft, MidRight, Striker]. 경기300초, GoalPause3초, 득점 후 재개.
- MNG 복사본에서만 공 크기/물리 변경. Builder 최초 기준 snapshot을 저장해 절대 목표값으로 적용한다. 재실행 배율 누적 금지.

| 항목 | 기존 | MNG | 차이 |
|---|---:|---:|---:|
| 공 uniform scale | 기준 실측 약0.012705 | 기준×1.10, 약0.0139755 | +10% |
| 공 월드 지름 | 약1.0573473m | 약1.163082m | +10% |
| 공 중심Y | 0.52867365 | 0.581541015 | +0.052867365m |
| 공 mass | 3 | 3 | 원래값 복구, ×1.00 |
| 공 동/정지 마찰 | 공유 재질 의존 | 0.05/0.05, Minimum | 선수·바닥 흡착 감소 |
| 공 반발 | 0.8 | 0.15, Maximum | 과도한 튐 없이 저반발0.05 정체 완화 |

Collider radius와 Transform scale을 동시에 확대하지 않는다. 시각/충돌 반지름·ResetY·Goal 판정·소유 거리를 함께 검증. MNG 전용 물리 재질을 만들고 양쪽 Collider Combine을 점검한다. 기존 공유 재질 변경 금지.

## 2. 단일 실행 주체

- 팀당 MNG_ManagerAgent 하나. 선수는 Agent가 아닌 Motor/SkillExecutor. MNG 복사본에서는 legacy AgentSoccer/DecisionRequester/정책 Ray/보상/MatchSetup 실행 경로를 제거한다.
- 동일 Rigidbody를 legacy와 MNG가 동시에 제어하지 않는다. 새 공과 경기 Controller도 writer 하나.
- FixedUpdate 순서: 제어권 전환→직전 접촉/소유/득점 확정→같은 tick snapshot→감독 decision 수락→작업 배정→Human 또는 AI 입력→물리 적용.
- 감독은0.5 simulated seconds마다 결정(0.02 fixedDeltaTime이면25tick), 사이에는 명령 유지. Motor는 매 물리 tick. 커리큘럼 전체 동일 주기.
- GoalPause 중 결정·행동 정지. 킥 token은1회만 소비. Human 전환/소유 상실/득점/종료는 즉시 작업 중단.
- snapshot에 tick/episode ID, 모든 사건에 eventId. 양 팀과 보상은 동일 시점 데이터를 사용한다.

## 3. 숫자 관측 v1 — 133 float, stack1

감독 카메라/Ray 입력 없음. 양 팀 관측 범위 동일. 상대의 명령·미래 정답·훈련 seed·정답 행동은 미포함.
팀좌표는180도 회전: s=Red+1/Navy-1, (x',z')=(s*x,s*z). 속도/heading/target도 같은 변환. Human 키 조작은 기존 로컬 의미 유지.

| 순서 | 데이터 | 차원 |
|---|---|---:|
|0~95|아군4→상대4, 각 active1,posXZ2,velocityXZ2,forwardXZ2,roleOneHot3,humanFlag1,kickCooldown1|96|
|96~99|공 posXZ2,velocityXZ2|4|
|100~102|소유 neutral/own/opponent|3|
|103~111|carrier none/own0~3/opponent0~3|9|
|112~115|episode elapsed fraction, match remaining fraction, scoreDiff, ownHumanMode|4|
|116~121|이전 수락 명령 one-hot|6|
|122|명령 age|1|
|123~127|pending own pass receiver none/own0~3|5|
|128|자기팀 마지막 상실 후 시간|1|
|129~132|아군 control mask4|4|

- 위치는 Geometry 반길이/반폭으로 나누고 골문 내부를 위해 [-1.2,1.2] clamp. 선수속도/9, 공속도/36.6667. heading=평면 단위벡터.
- cooldown은 잔여/최댓값, age/2, 상실시간/10, scoreDiff/5를 각각 clamp. 상실 없음=1. 시간 분모는 현재 episode 제한과300초를 구분.
- inactive 슬롯 전체0. Human은 inactive가 아니다. 상대 Human 정보도 대칭으로 제공.
- 슬롯 거리순 재정렬 금지. episode reset에서 previous command=Balanced, receiver=none, age=0.
- 구현 시 schema 총합/순서/finite 값/좌우 대칭 자동 검사. 변경하면 version 증가 후 모든 모델 재검증.

## 4. Discrete action [6]

|ID|명령|실행 의미|
|---|---|---|
|0|AdvanceCarry|AI carrier 전진,2명 전진 지원,1명 후방|
|1|PassBuild|AI carrier가 실행 가능한 동료에게1회 패스, 수신/후속 지원|
|2|AttemptShot|AI carrier가 유효 슛 위치에서 조준 후1회 발사|
|3|ActiveRecover|비소유 시 비keeper 최대2명 회수/압박, 소유 시 높은 지원/재회수 준비|
|4|Balanced|비소유 최근접1명 추격, 소유 시 carrier 운반·1명 지원·나머지 균형|
|5|ProtectBack|최대1명 회수, 나머지 후방; 소유자는 안전방향 운반|

- 0은AI carrier 있을 때,1은AI carrier+킥 준비+pass 후보,2는AI carrier+킥 준비+슛 해 존재 시만 가능.
- 3은 통제 가능한 active선수>=1,4/5 항상 가능. 최소2개 유효 선택 유지. 전술 정답을 mask로 강요하지 않는다.
- Human carrier일 때0/1/2 mask.3/4/5는 다른3명의 높은 지원/균형/후방 차이로 의미 있게 유지.
- Advance/Balanced에 자동 패스/슛을 숨기지 않는다. 감독 명령1/2 뒤 조준/발사 타이밍만 executor가 결정.
- 자기 골문 위험구역 clearance는 모든팀 공통 예외로만 허용, 개입 로그 별도. 이 예외가 경기 대부분을 결정하면 설계 실패.
- 동일 snapshot에서3/4/5가 최소1명의 역할/목표를 다르게 만드는 테스트. 명령 선택과 실행 명령 둘 다 기록.

## 5. 새 실행 기술

### 이동·탐색·추격

공 찾기는 snapshot 조회. 학습시키지 않는다. 최대 평면속도9m/s, 회전125°/s부터 시작하는 target velocity Motor. 가감속·도착감속·경계 제한·짧은 선형 예측추격·동료 회피를 코드화한다. AI/Human/Fallback 모두 같은 물리 상한.
Planner v9은 감독의6명령과133-float 관측을 유지하면서 각 선수가 자기 위치·역할·상대 압박·동료 간격으로 열린 로컬 경로를 고른다. 모든 선수는 카메라/Ray와 무관하게 snapshot의 정확한 공 위치·방향·속도를 사용한다. 전진 운반은 공격 깊이10~45m에서 점진적으로 최대75%까지 중앙 슈팅 통로를 향해, 측면 골문 밖으로 직진하는 단순 기술 실수를 막는다. 상대가 소유하면 감독 명령이 `ProtectBack`이어도 비골키퍼 최소2명을 선발한다. 1차 압박자는 예측 공 위치를 직접 압박하고, 2차 압박자는 예측 공의 자기 골대 쪽2.75m·측면2.25m 위치에서 패스와 돌파를 차단한다. 공이 자기 골대 전방30m 안에 있고 자기 팀 소유가 아니면, 압박자가 아닌 MidLeft/MidRight도 공격 임무보다 골대 전방12m 차단선 복귀를 우선한다. 반대로 자기 팀 필드 선수가 상대 골대30m 구역에서 소유하면 운반자 외 가장 가까운 제어 가능 필드 선수1명을 추가 공격 지원자로 지정한다. 지원 목표는 공보다 공격 방향2.75m·안쪽 측면3.75m라 운반자와 겹치지 않으면서 패스·리바운드에 참가하며, `PassBuild`의 지정 수신자가 있으면 그 수신 임무를 우선한다. Human은 강제 이동에서 제외한다.

전역 정체 탐지기가 공이0.55m 반경에1.20초 머문 것을 확인하면, 첫2.5초 동안 킥 쿨다운이 끝난 자기 운반자 또는 공8m 안의 최인접 필드 선수가 상대 골문 방향의 실제 킥 플레이트 슈팅·클리어를 먼저 시도한다. 목표 방향과 팀 공격축의 내적이0.25 미만이거나 목표가 공격축으로1m 이상 전진하지 않으면 자책 위험으로 거부한다. 안전한 슈터가 없거나 시도 창이 끝나도 공이 움직이지 않으면 기존 다중 선수 재탐지·서로 다른 접근/지원 경로로 전환한다. 공이 기준점에서2.25m 이동하거나3m/s에 도달하면 override를 해제한다. 이 수비·정체 보조는 규칙형과 PPO가 공유하는 `MatchController→TeamPlanner→PlayerSkillExecutor` 계약이다. Keeper는 이동 중 항상 공을 보고, 기존 두 배 활동 반경에서 Home/Claim/Block을 선택하며, 소유 시4.5~38m 안전 패스가 없으면 상대 골대 방향으로 강하게 클리어한다.

### 소유·드리블·회수

- MNG_BallControl이 유일한 carrier 원장. legacy RewardEngine에서 소유권을 가져오지 않는다.
- 각 선수의 MNG 전용 킥 플레이트가 소유 기준이다. 수축 위치에서 전방1.32m를 `DribblePosition`으로 하고, 공이 그 위치의 수평반경0.72m 안에 있거나 실제 플레이트 접촉이 있으면 소유 후보가 된다. 드리블 위치 배치는 접촉이 없어도 유효하며1 fixed tick(0.02초) 확인 후 확정한다.
- 동시 후보는 플레이트 드리블 위치까지의 거리→기존소유자→고정slot 순으로 판정한다. 상대 플레이트가 더 가까운 유효 후보가 되면 동일한0.02초 확인 뒤 탈취할 수 있다. 이전소유자 재획득 잠금은0.12초다.
- 소유자가 직진 명령 중일 때만 공을 플레이트 드리블 위치로 제한된 가속/감쇠 보정한다. 직진은 명령속도0.40m/s 이상이며 선수 전방과의 정렬 내적0.70 이상이다. 공 Rigidbody 충돌을 유지하고 parenting/원거리 흡착/순간 위치 대입은 하지 않는다.
- 공이 드리블 위치에서1.02m보다 멀어지면 해제한다. 정지·후진·옆걸음처럼 직진 조건을 잃으면 보정을 즉시 끄고0.08초 뒤 소유를 해제한다. 그 선수는 다시 직진하거나 공이 release 범위를 벗어날 때까지 제자리 재획득하지 못한다.
- 상대가 공을 빼앗기 쉽게 플레이트 드리블 zone 자체를 유효 탈취 접점으로 인정한다. 다만 이 zone과 실제 플레이트 접촉이 모두 없는 원거리 강탈은 허용하지 않는다.
- control owner 변경과 ball carrier 변경은 별개. H키로 공을 떨어뜨리거나 재배치하지 않는다.
- 힘/가속/감쇠와 위 거리·시간 값은 MNG_Physics Profile/코드/테스트에 고정한다. 장시간 학습 중 물리 변경 금지.

### 패스·슛

- legacy2000/5000 Force를 그대로 사용하지 않는다. `TryKick`은 공에 직접 힘을 주지 않고 소유자의 MNG 킥 플레이트를 무장한다. 플레이트가0.08초 동안 전진해 자기 center/wing collider로 공에 실제 접촉한 한 번만 token을 소비하고 `impulse=mass*(vTarget-vCurrent)`를 적용한다. 이후0.50초 동안 수축한다.
- pass범위5~28m. 2026-09-19 요청한 패스·슛 힘 `+1000`은 MNG의 직접 Force가 아닌 실제 plate 접촉 임펄스 계약에 맞춰 `1000×0.02초÷3kg=+6.6667m/s`로 환산한다. 약킥은 `14→20.6667m/s`, 강킥/슛은 `28→34.6667m/s`, 공 최고속도는 `30→36.6667m/s`다. 자동 패스는10m 이하20.6667m/s, 10~20m는20.6667→34.6667m/s 선형 보간, 20m 이상34.6667m/s를 사용한다. 관측의 공속도 정규화도36.6667을 사용하며, 이 물리 변경 전 M1 모델은 새 승격 근거로 재사용하지 않는다.
- pass 후보: 미래 수신위치 선형예측+경로차단+전진성+동료 압박 평가, 동률slot.5~28m 밖이거나 inactive인 물리적 불가 후보만 mask하고, 후방 또는 좁은 경로는 점수상 불리하게 두어 감독이 위험을 학습할 선택지는 유지한다. Human도 수신대상 가능하나 이동 강제 금지.
- 슛: Goal plane까지24m 이내, 골문 개구에서 공반경+0.5m 여유, 후보3점 중 차단위험 최소. 유효하지 않으면 슛 mask.
- 초기 조준오차10° 이내/접촉·쿨다운 충족 후1회 발사. 대기1초 초과하면 실패 보고. 숨은 다른 명령 금지.
- 기존 center+wing 3개 Collider 형상은 MNG 전용 `MNG_KickPlate`로 유지한다. legacy `SoccerKickPlate` 제어 컴포넌트는 MNG Prefab에서0개이며, OnCollisionEnter/Stay가 반복되어도 무장된 token당 힘은 정확히1회만 적용한다.
- Human E/Space는 기존 약/강킥 입력 의미. 골문 자동조준은 하지 않고 방향은 사람조작에서 얻는다. 공통 위험지역 안전제약은 동일 적용·기록.

## 6. H키·전술 교체

- 시작 AI. H edge 한 번에 Red Striker만 Manager↔Human. 나머지3명/Navy 불변.
- 전환 tick에 pending 이동/조준/킥 token 취소. 이후 writer는 새 owner 하나. Human키 입력은 Human만 읽는다.
- 점수·시간·위치·방향·속도·소유권 보존. Human→AI는 현재 상태에서 다음 배정, 남은 키입력/이전킥 폐기.
- Human을 배정 대상에서 제외하되 공간점유·패스대상·전체133관측에 계속 포함한다.
- GoalPause 중 owner 전환 가능하지만 재개 전 이동 불가. 득점 후 모드는 유지, 새 경기 기본AI.
- 전술 ONNX는 감독만 교체. 공통 executor/Profile물리/상대/점수/시간/Human 불변. contract불일치는 거절.
- model없음/추론실패를 몰래 Fallback으로 바꾸지 않는다. 명시적 오류/안전정지. 사용자가 선택한 Fallback은 HUD로 구분.

## 7. Core Fallback 포팅

MNG_FallbackManager를 기본안으로 한다. 기존 Core 규칙을 snapshot→명령/계획으로 옮기고 양팀 동일 executor로 실행한다. 기존 AgentSoccer를 살려 서로 다른 물리로 싸우게 하지 않는다.

- 현재 우선순위: 위험지역 중앙clearance→유효24m근거리슛→6m 근접압박 때 열린5~28m패스→전진10m/lane3m운반. 실제 패스 실행이 연결된 뒤9m는 연속 패스 순환과0:0을 만들었으므로, 가까운 압박만 탈압박하고 나머지는 전진·슛으로 이어가게 한다. 이는 고정 상대의 경기 흐름을 빠르게 하는 규칙이며 PPO의 선택을 대신하지 않는다.
- 비소유 최근접 추격, 나머지 역할별 공기준 지원/수비, Keeper제한.
- 기준 함수 GetAutonomousTarget/TryGetAutonomousKickTarget 전체를 source snapshot/hash로 보존하고 fixture 비교.
- 별도 Rule FSM의1.5초 후 패스를 Core Fallback 규칙이라고 섞지 않는다.
-6명령으로 표현하며 생기는 차이는 명시적으로 기록. 완전동일하다고 주장하지 않는다.
- 약한 상대는 반응주기/선택noise만 조절. 속도·킥·드리블은 같게 유지. 최종 평가는 비약화 Fallback.

# Rule 팀 담당자 가이드

- 대상: `Rule_PHC` FSM 담당자
- 상태: Rule 수정 경계와 검증의 단일 기준
- 마지막 검토: 2026-08-24

Rule은 강화학습하지 않는다. 행동을 바꾸려면 `RuleBasedSoccerController.cs`의 상태·전이·목표·조향을 수정한다. `RuleRewardProfile.asset`은 HUD·통계·공통 비교 척도이며 행동을 강화하지 않는다.

## 실행 경로

```text
HeuristicOnly
  → AgentSoccer.ReadAutonomousInput
  → ISoccerRuleController.Decide
  → SoccerRuleCommand.WriteTo
  → AgentSoccer 공통 이동·Kick
  → DefenderKeeper 공통 Engage/Recovery shield
  → 현재 공 속도를 포함한 공통 자책골 Kick safety
```

- Red 네 선수만 Rule FSM Component를 가진다.
- Navy는 공통 Base 비학습 상대다.
- Human 모드에서는 선택한 Red 한 명만 직접 조작한다.
- 나머지 선수는 계속 FSM을 실행하고 AI 복귀 시 Human 선수도 다시 합류한다.
- Trainer YAML, Models 폴더와 ONNX를 사용하지 않는다.

## 담당 파일

| 용도 | 경로 |
| --- | --- |
| FSM | `Assets/_Soccer/Teams/Rule_PHC/Runtime/RuleBasedSoccerController.cs` |
| 평가 Policy | `Assets/_Soccer/Teams/Rule_PHC/Runtime/RuleRewardPolicy.cs` |
| 평가 Profile | `Assets/_Soccer/Teams/Rule_PHC/Profiles/RuleRewardProfile.asset` |
| Team 정의 | `Assets/_Soccer/Teams/Rule_PHC/Profiles/RuleTeamDefinition.asset` |
| Prefab | `Assets/_Soccer/Teams/Rule_PHC/Prefabs/StadiumEnvironment_Rule.prefab` |
| Scene | `Assets/_Soccer/Teams/Rule_PHC/Scenes/Stadium4v4_Rule.unity` |

`SoccerRuleControllerContract.cs`는 읽기 기준이지 개인 수정 파일이 아니다. Action Branch를 바꾸면 모든 Neural ONNX에도 영향을 준다.

## 수정 가능

| 계층 | 수정 예 | 지켜야 할 것 |
| --- | --- | --- |
| 상태 | 상태 추가·통합 | 전이, 목표, Kick, Reset을 함께 구현 |
| 전이 | 우선순위, 거리, hysteresis | 공통 `EngageBall/RecoverGoal`과 충돌 금지 |
| 목표 | Carry·Support·Cover·Recover 위치 | 최종 Keeper target 제한 통과 |
| Pass·Shoot | 후보 점수, 거리, 조준, 경로 | 공통 Kick Branch·힘 유지 |
| 조향 | 목표 회전, 장애물 회피, separation | Rigidbody 직접 조작 금지 |
| Rule 상수 | 상태 유지·압박·슛·패스 거리 | 한 번에 한 가설 |
| Raycast 판단 | Mask와 경로 해석 | 네 Red 선수 설정 일치 |
| 순수 Helper·Test | 계산 분리와 회귀 | 매 Decision allocation 금지 |

## 수정 금지

- Trainer YAML, ONNX와 Model Slot 추가
- `RuleTeamDefinition`의 `RuleBased` Controller Type 또는 null Model 변경
- `SoccerRuleCommand`의 필드 순서와 `0~2` 의미 변경
- `AgentSoccer`, `SoccerEnvController`, `SoccerRewardEngine`에 Rule 분기 추가
- `SoccerDefenderKeeperRules` 또는 공통 crowding 판정 우회
- 경기 시간, Reset, UI, Camera, 경기장·Physics·Sensor를 Rule Prefab만 변경
- Navy 배선과 Training flag 변경
- 다른 팀 폴더 수정

공통 값은 [경기 계약](../gameplay-contract.md)을 사용하고 이 문서에 복제하지 않는다.

## FSM 상태

| 상태 | 목적 | 주요 수정 지점 |
| --- | --- | --- |
| `ChaseLooseBall` | 소유권 없는 공 추격 | 추격자 선택, target |
| `CarryBall` | 공 운반 | 운반 target, Pass·Shoot 전이 |
| `PassBall` | 동료에게 Controlled Kick | 후보 점수와 경로 |
| `ShootBall` | 상대 Goal Strong Kick | 거리·조준·경로 |
| `ClearBall` | 위험 지역 중앙 Controlled 걷어내기 | 자기 수비 지역과 공통 Kick safety |
| `SupportAttack` | 공격 지원 | 지원 위치와 간격 |
| `PressBall` | 상대 소유자 압박 | 대상과 접근 거리 |
| `CoverDefense` | 수비 공간 Cover | 공·Goal 기준 target |
| `RecoverGoal` | Keeper 긴급 복귀 | 최우선 전이와 Home target |

새 상태를 추가할 때는 Enum, 진입·이탈, target, Kick, Reset, Keeper constraint와 Test를 한 작업에서 추가한다. 같은 선수에 두 번째 `ISoccerRuleController`를 붙이지 않는다.

## Keeper와 teammate separation

Rule Keeper는 다음 다섯 층을 모두 유지한다.

1. 공통 Keeper mode의 `EngageBall`과 `RecoverGoal`을 일반 상태보다 우선한다.
2. `RecoverGoal`은 최소 상태 유지 시간을 기다리지 않는다.
3. 먼 공의 최근접 추격자 후보에서 Keeper를 제외한다.
4. 최종 목표를 `ConstrainTarget`에 통과시킨다.
5. `AgentSoccer`의 실제 이동·속도 shield를 다시 통과한다.

동료가 가까우면 target 방향에 Reynolds separation을 합성한다. 이 계산은 직접 행동을 바꾸므로 Reward와 무관하게 동작한다. 매 Decision마다 LINQ, 새 Collection, 문자열 비교나 `FindObjects*`를 추가하지 않는다.

## Rule Reward의 의미

처리 흐름은 `공통 사건 → RuleRewardPolicy → RuleRewardProfile → cap → HUD·Stats`다. Trainer가 없으므로 Profile 값을 바꿔도 FSM은 달라지지 않는다.

| 문제 | 잘못된 접근 | 먼저 수정할 곳 |
| --- | --- | --- |
| 전원이 공에 몰림 | crowding penalty만 증가 | 추격자 선택·Support target·separation |
| Pass를 안 함 | Pass reward 증가 | `PassBall` 전이와 후보·경로 |
| Keeper가 위협 공에 소극적 | Defense reward 변경 | 공통 `EngageBall/RecoverGoal`과 Rule 상태 우선순위 |
| 먼 거리 Shoot | Attack reward 변경 | `ShootDistance`·조준 |
| 골문 앞 자책골 Kick | Rule penalty만 변경 | `ClearBall` 중앙 target과 공통 `SoccerDefensiveClearanceRules` |
| 상태가 떨림 | Formation reward 변경 | 상태 유지 시간과 hysteresis |

공정한 Neural 대 Rule 비교에는 Rule Profile을 Base 평가 척도와 같게 유지한다. 실제 값은 [보상 기준표](../rewards.md)를 따른다.

Rule Profile·Policy 값을 바꾸면 학습형과 같은 변경 의무가 적용된다.

| Profile | 항목 | 기존 값 | 변경 값 | 변경량 | 비교 영향 | 이유 |
| --- | --- | ---: | ---: | ---: | --- | --- |
| 예: Rule | `passSuccess` | `0.005` | `0` | `-0.005` | Base와 직접 비교 불가 | Pass 통계 제외 |

FSM만 바꿨다면 Reward 문서를 수정하지 않고 행동 지표의 전후 차이를 기록한다.

## 수정 절차

1. 증상과 성공 기준을 한 문장으로 정의한다.
2. 같은 Seed·상대·경기 수의 기준 지표와 영상을 저장한다.
3. 상태 선택, target, 조향, Pass/Shoot 중 한 계층을 고른다.
4. 한두 상수 또는 한 전이만 먼저 바꾼다.
5. 순수 계산 Test와 Rule Scene 회귀를 실행한다.
6. 승률뿐 아니라 상태 체류, 전환, Pass, Shot, crowding, Keeper 복귀와 CPU·GC를 비교한다.

증상별 시작점:

| 증상 | 먼저 볼 곳 |
| --- | --- |
| 잘못된 상태 선택 | `SelectState` 우선순위 |
| 상태는 맞지만 위치가 나쁨 | 상태별 target |
| 목표는 맞지만 막힘 | movement command와 avoidance |
| Pass 상대가 나쁨 | Pass 후보 평가 |
| Kick 시점이 나쁨 | 거리·조준·경로 조건 |
| 동료가 뭉침 | 추격자·지원 목표와 separation |

## 성능과 완료 Checklist

- [ ] 모든 Command 값이 `0~2`다.
- [ ] 새 상태 Data를 Reset한다.
- [ ] FixedUpdate·Decision에 반복 allocation을 추가하지 않았다.
- [ ] `RecoverGoal`, target 제한과 최종 shield를 유지했다.
- [ ] 공 추격자는 한 명이고 나머지는 지원·Cover한다.
- [ ] Human 전환과 AI 복귀를 확인했다.
- [ ] 같은 조건에서 행동 지표, 영상, CPU와 GC를 비교했다.
- [ ] Reward 변경 시 기준표·README·Test·변경량 표를 갱신했다.
- [ ] 공통 변경은 Rule Prefab에서 제거하고 공통 담당자에게 넘겼다.

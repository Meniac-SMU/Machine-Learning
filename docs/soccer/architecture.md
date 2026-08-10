# Soccer 아키텍처와 변경 경계

- 대상: Core·Editor·팀 Runtime 담당자
- 상태: 코드 책임과 공통/개별 경계의 단일 기준
- 마지막 검토: 2026-08-10

## 전체 흐름

```text
TeamDefinition + RewardProfile + Scene 설정
                    │
                    ▼
           SoccerMatchSetup
                    │
        ┌───────────┴───────────┐
        ▼                       ▼
SoccerEnvController       SoccerRewardEngine
경기·팀·Reset·소유권       사건 판정·상한·장부
        │                       │
        ▼                       ▼
   AgentSoccer ◀──── RewardPolicy ─── RewardProfile
        │
        ├─ Neural Action
        ├─ model 없는 fallback
        ├─ Rule FSM Command
        └─ Human input
                    │
                    ▼
         공통 이동·Kick·Keeper shield
```

## Runtime 책임

| 파일 | 책임 | 팀별 분기 가능 여부 |
| --- | --- | --- |
| `AgentSoccer.cs` | 관측, Action 해석, Human 입력, 공통 이동·Kick | 금지 |
| `SoccerEnvController.cs` | 경기 시간·상태, Team Group, 소유권 연결, Reset | 금지 |
| `SoccerRewardEngine.cs` | 공통 사건 감지, 상한, HUD 장부 | 금지 |
| `SoccerFormationEvaluator.cs` | Formation·crowding 순수 계산 | 금지 |
| `SoccerDefenderKeeperRules.cs` | 양 팀 공통 Keeper 제한·Recovery | 금지 |
| `SoccerMatchSetup.cs` | TeamDefinition, Model, 학습·추론 배선 | 공통 로직만 |
| `SoccerTeamDefinition.cs` | 논리 Team ID, Behavior, Controller Type, Model | Asset 값만 팀별 |
| `SoccerTeamRewardPolicyBase.cs` | 공통 사건을 전술 Policy로 전달 | 기반 계약은 금지 |
| `*RewardPolicy.cs` | 이미 판정된 사건의 전술별 가중 | 제한 허용 |
| `RuleBasedSoccerController.cs` | Rule 상태·전이·목표·조향 | Rule 담당 허용 |

모든 공통 Runtime은 `Assets/_Soccer/Core/Scripts`에 있다. 팀 전용 코드는 자기 `Teams/<담당 폴더>/Runtime` 밖으로 확장하지 않는다.

## 작업공간

| 논리 Type | 물리 폴더 | Environment Prefab | Controller |
| --- | --- | --- | --- |
| Base | `Core` | `SoccerEnvironment_Base.prefab` | Neural |
| Attack | `Teams/Attack_KMW` | `SoccerEnvironment_Attack.prefab` | Neural |
| Defense | `Teams/Defense_PJH` | `SoccerEnvironment_Defense.prefab` | Neural |
| Press | `Teams/Press_KMG` | `SoccerEnvironment_Press.prefab` | Neural |
| Rule | `Teams/Rule_PHC` | `SoccerEnvironment_Rule.prefab` | Rule FSM |

각 환경은 고유 GUID를 가진 Regular Prefab이다. 독립성은 Model·Reward·Trainer 연결을 격리하기 위한 것이며 서로 다른 경기장을 만들기 위한 것이 아니다.

## 공통 항목과 허용 차이

| 반드시 같은 항목 | 전술별로 달라도 되는 항목 |
| --- | --- |
| 계층·Transform·활성 상태 | Prefab 경로와 GUID |
| Tag·Layer·Mesh·Renderer·Material | Blue TeamDefinition·BehaviorName |
| Rigidbody·Collider·Physics Material | Blue Model 또는 Model Override |
| 공·Goal·벽·선수 배치 | RewardProfile·제한된 RewardPolicy |
| Ray Sensor와 Observation/Action | Training flag와 그에 따른 BehaviorType |
| 이동·Kick·경기 시간·Reset | Rule Blue의 FSM Component |
| HUD·Camera 공통 설정 | 논리 Type에 따른 표시 문자열 |

공통 차이가 필요하면 한 Prefab을 직접 저장하지 않는다. `SoccerProjectBuilder`의 공통 적용 단계에 반영한 뒤 Template과 다섯 Prefab을 모두 갱신한다.

## Builder

`Assets/_Soccer/Editor/SoccerProjectBuilder.cs`가 다음을 담당한다.

- 공용 `SoccerField4v4.prefab` 생성·정리
- Base와 네 작업공간 Prefab 생성 또는 공통 설정 동기화
- Scene, TeamDefinition, RewardProfile, UI 연결
- 정책 계약, 물리, 센서, Material, 역할, 학습 flag 검증
- Template과 다섯 Prefab의 공통 snapshot parity 검증

Model·Profile만 교체한 작업에서 `BuildAll`을 습관적으로 실행하지 않는다. 비파괴적인 `Validate 4v4 Prototype`을 먼저 사용한다. Builder 생성이 필요한 공통 변경에서는 기존 전술별 Model·Profile 차이가 보존되는지 확인한다.

## Controller별 Action 경로

### Neural

`DecisionRequester → OnActionReceived → AgentSoccer.MoveAgent → Keeper shield → Rigidbody/KickPlate`

Training은 `BehaviorType.Default`, Model 추론은 `InferenceOnly`다. Model이 없으면 `HeuristicOnly` fallback을 사용하므로 “AI로 움직인다”는 사실만으로 ONNX 추론을 증명할 수 없다.

### Rule

`HeuristicOnly → ReadAutonomousInput → ISoccerRuleController.Decide → SoccerRuleCommand.WriteTo → AgentSoccer`

Rule은 Trainer와 ONNX를 사용하지 않는다. 행동 문제는 FSM에서 수정하며 `RuleRewardProfile` 변경으로 행동을 바꾸려 하지 않는다.

### Human

`HeuristicOnly → raw keyboard/gamepad → Human target velocity → Rigidbody`

Human 한 명만 이 경로를 사용하고 다른 선수는 기존 Controller를 유지한다. AI 복귀 시 최신 TeamDefinition과 Controller를 다시 적용한다.

## 보상 흐름

1. 공 접촉과 시간 흐름을 `SoccerEnvController`가 `SoccerRewardEngine`에 전달한다.
2. Engine이 소유권, Pass, 회수, Formation 등 공통 사건을 확정한다.
3. Team의 `RewardPolicy`가 공통 사건에 전술별 multiplier를 적용한다.
4. `RewardProfile`의 명목값과 사건별·소유권별·경기별 상한을 적용한다.
5. Group 또는 개인에게 지급하고 별도 경기 장부를 HUD에 기록한다.

판정 규칙을 Team Policy에서 다시 구현하지 않는다. 전체 값은 [보상 기준표](rewards.md)를 사용한다.

## 변경 위치 선택

| 원하는 변화 | 먼저 수정할 곳 |
| --- | --- |
| 전술의 학습 성향 | 해당 `RewardProfile.asset` |
| 최적화 속도·Network | 해당 Trainer YAML |
| 전술별 조건부 shaping | 해당 `*RewardPolicy.cs` |
| Rule의 선택·위치·Kick 시점 | `RuleBasedSoccerController.cs` |
| 새 보상 사건·판정 | 공통 Engine 작업으로 승격 |
| 관측·행동·물리 | 정책 계약 변경으로 승격 |
| 경기장·UI·Camera | Builder 공통 변경으로 승격 |
| Model 등록 | TeamDefinition 또는 임시 Blue Override |

개별 절차는 [학습형 팀 가이드](training/learning-teams.md)와 [Rule 팀 가이드](training/rule-team.md)를 따른다.

## 성능 원칙

- FixedUpdate·Decision 경로에서 LINQ, `FindObjects*`, 새 List·배열과 문자열 Log를 반복 생성하지 않는다.
- 거리 비교는 가능한 제곱 거리를 사용한다.
- Raycast 반복은 고정 Buffer와 NonAlloc API를 우선한다.
- 새 공통 계산은 Neural 8명과 반복 학습 비용을 기준으로 검토한다.
- Rule 전용 판단은 공통 Agent 계약을 우회하지 않는다.

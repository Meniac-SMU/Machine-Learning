# Soccer 아키텍처와 변경 경계

- 대상: Core·Editor·팀 Runtime 담당자
- 상태: 코드 책임과 공통/개별 경계의 단일 기준
- 마지막 검토: 2026-08-31

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
    공통 이동·Kick·Keeper shield·Kick safety
```

## Runtime 책임

| 파일 | 책임 | 팀별 분기 가능 여부 |
| --- | --- | --- |
| `AgentSoccer.cs` | 관측, Action 해석, Human 입력, 공통 이동·Kick | 금지 |
| `SoccerEnvController.cs` | 경기 시간·상태, Team Group, 소유권 연결, Reset, model 없는 공통 선택과 Kick 사건 전달 | 금지 |
| `SoccerRewardEngine.cs` | 공통 사건 감지, 상한, HUD 장부 | 금지 |
| `SoccerFormationEvaluator.cs` | Formation·crowding과 Keeper Engage 목표 정합성의 순수 계산 | 금지 |
| `SoccerDefenderKeeperRules.cs` | 양 팀 공통 Keeper 제한·`EngageBall`·`RecoverGoal` | 금지 |
| `SoccerDefensiveClearanceRules.cs` | 양 팀 공통 자책골 궤적 예측·중앙 걷어내기·Strong Kick 분류 | 금지 |
| `SoccerMatchSetup.cs` | TeamDefinition, Model, 학습·추론 배선 | 공통 로직만 |
| `SoccerTeamDefinition.cs` | 논리 Team ID, Behavior, Controller Type, Model | Asset 값만 팀별 |
| `SoccerTeamRewardPolicyBase.cs` | 공통 사건을 전술 Policy로 전달 | 기반 계약은 금지 |
| `*RewardPolicy.cs` | 이미 판정된 사건의 전술별 가중 | 제한 허용 |
| `RuleBasedSoccerController.cs` | Rule 상태·전이·목표·조향 | Rule 담당 허용 |

모든 공통 Runtime은 `Assets/_Soccer/Core/Scripts`에 있다. 팀 전용 코드는 자기 `Teams/<담당 폴더>/Runtime` 밖으로 확장하지 않는다.

## 작업공간

| 논리 Type | 물리 폴더 | Environment Prefab | Controller |
| --- | --- | --- | --- |
| Base | `Core` | `StadiumEnvironment_Base.prefab` | Neural |
| Attack | `Teams/Attack_KMW` | `StadiumEnvironment_Attack.prefab` | Neural |
| Defense | `Teams/Defense_PJH` | `StadiumEnvironment_Defense.prefab` | Neural |
| Press | `Teams/Press_KMG` | `StadiumEnvironment_Press.prefab` | Neural |
| Rule | `Teams/Rule_PHC` | `StadiumEnvironment_Rule.prefab` | Rule FSM |

각 환경은 고유 GUID를 가진 Regular Prefab이다. 독립성은 Model·Reward·Trainer 연결을 격리하기 위한 것이며 서로 다른 경기장을 만들기 위한 것이 아니다.

## 공통 항목과 허용 차이

| 반드시 같은 항목 | 전술별로 달라도 되는 항목 |
| --- | --- |
| 계층·Transform·활성 상태 | Prefab 경로와 GUID |
| Tag·Layer·Mesh·Renderer·Material | Red TeamDefinition·BehaviorName |
| Rigidbody·Collider·Physics Material | Red Model 또는 Model Override |
| 공·Goal·벽·선수 배치 | RewardProfile·제한된 RewardPolicy |
| Ray Sensor와 Observation/Action | Training flag와 그에 따른 BehaviorType |
| 이동·Kick·경기 시간·Reset | Rule Red의 FSM Component |
| 공통 fallback·Keeper·자책골 safety·Reward 사건 스키마 | 전술별 기존 사건의 제한된 가중 |
| HUD·Camera 공통 설정 | 논리 Type에 따른 표시 문자열 |

공통 차이가 필요하면 한 Prefab을 직접 저장하지 않는다. `SoccerProjectBuilder`의 공통 적용 단계에 반영한 뒤 Template과 다섯 Prefab을 모두 갱신한다.

## Builder

`Assets/_Soccer/Editor/SoccerProjectBuilder.cs`가 다음을 담당한다.

- Core의 `StadiumEnvironment_Base.prefab`을 유일한 활성 경기장 기준본으로 사용
- Base와 네 작업공간의 독립 Stadium Prefab·Scene 생성 및 공통 설정 동기화
- TeamDefinition, RewardProfile, 모델 오버라이드, 학습 flag, Rule FSM, UI·Camera 연결
- 데모 Goal 2개, 공 scale·Reset 높이·굴림, 벽·모서리·Rigidbody 보존 검증
- 정책 계약, 정확히 전방/후방 각 1개인 센서, 공통 Reward 필드·cap, Material, 역할, 학습 flag 검증
- 다섯 Stadium Prefab의 공통 snapshot parity와 Scene/Build Settings 검증

Model·Profile만 교체한 작업에서는 비파괴적인 `Validate active 4v4 Stadiums`를 먼저 사용한다. Builder 생성이 필요한 공통 변경에서는 기존 전술별 Model·Profile·Model Override 차이가 보존되는지 확인한다. 예전 `SoccerEnvironment_*`와 `Soccer4v4_*`는 이력 보존 자산이며 Build Settings·새 Scene 기본값·학습 Scene에서 사용하지 않는다.

### 활성 Stadium 공통 계약

`Core/Editor/SoccerStadiumBuilder`는 `Stadium4v4_Base`와 `StadiumEnvironment_Base`를 만든 뒤 `SoccerProjectBuilder.BuildStadiumWorkspacesBatch`를 호출해 Attack·Defense·Press·Rule에 같은 경기장을 전파한다. 물리·시각 계약은 Base Stadium 한 벌에서 오고, 팀별 Prefab에는 전술 자산과 학습 배선만 다르게 연결한다.

- Demo를 새 Scene GUID로 복제하고 원래 다섯 Root에 같은 원점 이동·균일 확대를 적용한다. Terrain은 독립 TerrainData와 TerrainLayer 복사본의 size·타일·식생 크기에 확대율을 반영하고 Transform은 1로 유지한다.
- 확대하지 않는 Gameplay Root는 현재 `StadiumEnvironment_Base`의 선수·공·MatchSetup·규칙을 유지하고, 이전 `DemoDesign`과 `StadiumPhysics`만 새 결과로 교체한다. 전체 재생성은 이력용 사각 Base Prefab을 읽지 않는다. 원본 데모 골대 Mesh·Renderer를 사용하며 바닥·벽·골문 안쪽 충돌면은 Core 안에서 구성한다.
- `SoccerArenaGeometry`가 필드·골문 치수와 세 판으로 나눈 모서리 경계를 제공한다. EnvController, Rule 목표, Keeper 목표, 관측 위치 정규화, 자책골 예측과 Strong Shot 판정은 이 값을 사용한다. 예외 fallback 상수도 Stadium 치수다.
- Controlled/Strong Kick의 공통 기본값과 모든 활성 Prefab 직렬화 값은 `2000 / 5000`이다. `AgentSoccer.EffectiveControlledKickPower`와 `EffectiveStrongKickPower`가 Human·Neural·fallback·Rule의 실제 접촉 힘을 공급하며 Team 또는 Controller 종류로 분기하지 않는다.
- `SoccerBallController`는 `FreezePositionY`를 유지하면서 중심 Y를 공 반지름에 고정하고 평면 속도에서 실제 굴림 각속도를 만든다. Geometry 제한은 Collider·Continuous Collision Detection을 통과한 비정상 위치만 복구한다.
- `SoccerGoalSurface`는 골대 프레임과 득점 가능한 안쪽 면·Trigger 영역을 구분한다. 공의 기존 Y 고정을 유지하므로 바닥 접촉에 의존하지 않고 별도 영역과 골라인 안쪽 중심 위치로 득점한다. 득점 Trigger는 Ignore Raycast Layer다. 기존 태그 기반 득점 경로는 기존 Scene에 유지한다. 이미 있던 두 Goal Floor Collider만 `Materials/StadiumGoalFloor.mat`로 보이게 만들며 물리 바닥을 중복 생성하지 않는다.
- 공유 PanelSettings·UXML과 공통 이동·카메라 기본값에서 HUD·Settings·Camera를 직접 구성하고 새 Environment를 연결한다. Stadium Scene의 `SoccerHudController.rewardPanelBottomRight`만 켜서 공유 UXML/USS를 바꾸지 않고 전술·누적 보상 패널의 `top/translate`를 해제하고 우측 하단 `20px`에 둔다. `SoccerGoalOcclusionFader`의 명시적 Renderer 배열에는 데모 골대만 등록하므로 공유 스타디움 Material 자산을 바꾸지 않는다.
- 골대는 수비 팀 Tag에 따라 Core의 독립 Red/Navy 재질을 사용한다. 노란색 텍스처 아틀라스와 분리하고 fader가 해당 재질을 런타임 복사하여 RGB는 유지하고 Alpha만 조정한다. `SoccerArenaGeometry.ContainsGoalInterior`로 선수가 해당 골문 안에 있는지 먼저 제한하고, 카메라-선수 선분이 등록된 골대 Collider에 선수보다 먼저 닿을 때만 반투명화한다. Collider가 전혀 없는 이력용 골대만 Renderer bounds fallback을 사용한다.
- 환경 정리는 Stadium 복사본에 배치된 인스턴스에만 적용한다. `SM_Tree_*`·`SM_Fir_*`와 전용 `StadiumTerrain.asset`의 나무 인스턴스를 제거하고, 승인된 공급자 Prefab 이름 목록을 기준으로 도로·주차장 27개, 관목·꽃 104개, 도시 소품 42개를 추가 제거한다. `BuildBatch`와 반복 가능한 `ApplyTuningToEnvironment`가 같은 제거 함수를 사용하므로 재생성 시 되살아나지 않으며, 정리된 Core Prefab을 네 팀 Stadium에 복제한다. 공급자 원본 에셋은 수정하거나 삭제하지 않는다.
- EditMode·PlayMode는 다섯 Stadium Scene/Prefab의 parity와 팀 배선을 검증한다. Build Settings에는 Base·Attack·Defense·Press·Rule Stadium 다섯 Scene만 등록한다.

후속 조정에는 `SoccerStadiumBuilder.ApplyTuningBatch`를 사용한다. 기존 Stadium Scene/Prefab을 먼저 `Logs`에 백업한 뒤, 저장된 Prefab에서 공·골대 폭·환경별 Kick 힘과 생성된 벽·골문 안쪽 충돌면·표시 바닥을 갱신하고 Scene의 Stadium 전용 HUD 배치 플래그를 저장한다. 승인된 벽 재질, 팀 색 골대 재질, 연회색 Goal Floor 재질과 나무 제거도 같은 경로에 포함되어 전체 재생성 때 되살아나지 않는다. 공유 UI 문서, 그 외 Demo 외형, 지형 표면, 선수·MatchSetup은 재생성하지 않는다. 저장된 `goalModelWidthMultiplier`를 기준으로 목표 배율과의 비율만 적용하므로 반복 실행해도 골대가 계속 커지지 않는다. 전체 `BuildBatch`도 같은 최신 수치를 사용하지만 수동 디자인 편집을 유지해야 할 때는 실행하지 않는다.

Builder의 수치는 [경기 계약](gameplay-contract.md#활성-stadium-공통-scene), 실제 통과·실패와 미검증 범위는 [현재 상태](current-status.md)에 기록한다.

## Controller별 Action 경로

### Neural

`DecisionRequester → OnActionReceived → AgentSoccer.MoveAgent → Keeper shield → Kick safety → Rigidbody/KickPlate`

Training은 `BehaviorType.Default`, Model 추론은 `InferenceOnly`다. Model이 없으면 `HeuristicOnly` fallback을 사용하므로 “AI로 움직인다”는 사실만으로 ONNX 추론을 증명할 수 없다. `SoccerMatchSetup.Force Red/Navy Fallback`은 TeamDefinition에 Model이 등록되어 있어도 해당 팀의 Model을 의도적으로 비우고 Trainer 등록도 차단한다. Base fallback 전용 Scene만 Navy에 이 값을 사용하며 일반 Base·전술 Scene의 기본값은 꺼져 있다. 공통 fallback은 확정 carrier일 때 기본적으로 운반하고, 압박 시 Controlled Pass, 실제 Goal 궤적이 `24m` 이내일 때만 Strong Shot, 자기 Goal 위험 지역에서는 중앙 Controlled Clearance를 선택한다.

### Rule

`HeuristicOnly → ReadAutonomousInput → ISoccerRuleController.Decide → SoccerRuleCommand.WriteTo → AgentSoccer`

Rule은 Trainer와 ONNX를 사용하지 않는다. 행동 문제는 FSM에서 수정하며 `RuleRewardProfile` 변경으로 행동을 바꾸려 하지 않는다. 확정 `BallCarrier` 한 명만 Carry/Pass/Shoot를 선택하고, 최대 `1.5초` 운반 뒤 열린 Pass를 검토하며 Goal 위험 지역에서는 중앙 Controlled Clearance를 사용한다.

### Human

`HeuristicOnly → raw keyboard/gamepad → Human target velocity → Rigidbody`

Human 한 명만 이 경로를 사용하고 다른 선수는 기존 Controller를 유지한다. AI 복귀 시 최신 TeamDefinition과 Controller를 다시 적용한다.

## 보상 흐름

1. 공 접촉, 명시적 Kick과 시간 흐름을 `SoccerEnvController`가 `SoccerRewardEngine`에 전달한다.
2. Engine이 소유권, Pass, 안정 운반, 회수, Formation, 무의미한 Strong Kick과 안전 보정 사건을 확정한다.
3. Team의 `RewardPolicy`가 공통 사건에 전술별 multiplier를 적용한다.
4. `RewardProfile`의 명목값과 사건별·소유권별·경기별 상한을 적용한다.
5. Group 또는 개인에게 지급하고 별도 경기 장부를 HUD에 기록한다.

판정 규칙을 Team Policy에서 다시 구현하지 않는다. 전체 값은 [보상 기준표](rewards.md)를 사용한다.

명시적 Kick은 힘을 적용하기 전에 현재 공 속도와 `Force / mass × fixedDeltaTime`을 합친 예상 속도로 자기 Goal plane 교차를 검사한다. 위험하면 중앙 Controlled 방향으로 바꾸고, 그 힘만으로도 기존 골문 방향 속도를 상쇄하지 못하면 골문 방향 X 속도 성분을 제거한 뒤 걷어낸다. 이 최종 안전 계층은 Neural Action, fallback과 Rule Command 모두에 적용되며 Team Policy가 우회할 수 없다.

## 변경 위치 선택

| 원하는 변화 | 먼저 수정할 곳 |
| --- | --- |
| 전술의 학습 성향 | 해당 `RewardProfile.asset` |
| 최적화 속도·Network | 해당 Trainer YAML |
| 전술별 조건부 shaping | 해당 `*RewardPolicy.cs` |
| Rule의 선택·위치·Kick 시점 | `RuleBasedSoccerController.cs` |
| 새 보상 사건·판정 | 공통 Engine 작업으로 승격 |
| 자책골 예측·공통 걷어내기 | `SoccerDefensiveClearanceRules`와 `AgentSoccer` 최종 Kick 경로 |
| model 없는 공통 carry/pass/shot 선택 | `SoccerEnvController.cs` |
| Keeper 위협 대응 | `SoccerDefenderKeeperRules.cs` |
| 관측·행동·물리 | 정책 계약 변경으로 승격 |
| 경기장·UI·Camera | Builder 공통 변경으로 승격 |
| Model 등록 | TeamDefinition 또는 임시 Red Override |

개별 절차는 [학습형 팀 가이드](training/learning-teams.md)와 [Rule 팀 가이드](training/rule-team.md)를 따른다.

## 성능 원칙

- FixedUpdate·Decision 경로에서 LINQ, `FindObjects*`, 새 List·배열과 문자열 Log를 반복 생성하지 않는다.
- 거리 비교는 가능한 제곱 거리를 사용한다.
- Raycast 반복은 고정 Buffer와 NonAlloc API를 우선한다.
- 새 공통 계산은 Neural 8명과 반복 학습 비용을 기준으로 검토한다.
- Rule 전용 판단은 공통 Agent 계약을 우회하지 않는다.

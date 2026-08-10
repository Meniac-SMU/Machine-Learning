# Escape 프로토타입 상세 명세

> 보관일: 2026-08-09  
> 보관 이유: 사용자 결정에 따른 Escape 개발 종료  
> 현행 개발 문서: [Soccer 개요](../../soccer/overview.md), [학습 운용](../../soccer/training/overview.md)  
> 최종 상태: 구현 자산은 역사 자료로 보존하며 더 이상 개발·학습·빌드 대상에 포함하지 않음
> 현재 위치: 아래 `Assets/Escape` 경로는 작성 당시 기록이며, 2026-08-10 이후 구현 자산은 `Assets/_Legacy/Escape`에 있음

마지막 갱신: 2026-08-09  
상태: `보관 — 개발 종료`

이 문서는 `Assets/Escape` 탈출 게임 프로토타입의 게임 규칙, 구현 범위, ML-Agents 학습 명세, 에셋 구조와 검증 기준을 정의한다. 씬·프리팹·코드·UI·Trainer 설정과 Windows 학습 Build까지 구현했으며 실제 장기 학습은 아직 시작하지 않았다.

## 1. 목표와 핵심 게임 루프

플레이어 1명은 120초 안에 도시형 격자 맵을 탐색해 건물 벽의 버튼 5개 중 3개를 누르고, 활성화된 출구로 탈출해야 한다. 적 3명은 협력해서 플레이어를 추적하고 공격한다.

1. 에피소드 시작 시 캐릭터, 버튼, Gate 위치와 캐릭터 방향을 무작위화한다.
2. 플레이어는 버튼을 탐색한다.
3. 서로 다른 버튼을 처음 접촉할 때마다 버튼이 빨간색에서 연두색으로 바뀐다.
4. 세 번째 버튼이 눌리면 Gate가 활성화된다.
5. 플레이어가 Gate에 닿으면 플레이어 승리다.
6. 플레이어 체력이 0이 되거나 제한 시간 120초가 끝나면 적 승리다.
7. 바닥으로 승패 색을 보여준 뒤 새 에피소드로 완전히 초기화한다.

## 2. 확정 내용과 설계 가정

### 사용자 요구로 확정된 내용

- 3D 정사각형 맵, 5×5 동일 간격의 대형 큐브 건물 25개, 건물 사이 도로
- 플레이어 1명, 적 3명
- 플레이어의 AI/사람 조작 전환, 3인칭 마우스 시점
- 전진·후진과 좌·우 회전을 동시에 수행할 수 있는 이동 방식
- 플레이어 체력 3, 피격 후 3초간 빨간색 점멸 및 무적
- 모든 캐릭터에 Ray Perception Sensor를 부착하고 `Player`, `Enemy`, `Wall`, `Building`, `Gate`, `Button`을 구분
- 서로 다른 건물 5개에 버튼을 하나씩 붙이고, 각 건물의 네 면 중 한 면을 무작위 선택
- 버튼 3개 이상을 누르면 Gate 활성화
- 120초 제한, 탈출 시 플레이어 승리, 사망 또는 시간 초과 시 적 승리
- 매 에피소드마다 모든 무작위 요소를 다시 추첨하며 이전 에피소드 결과는 고려하지 않음
- UI Toolkit 기반 HUD와 지정된 다섯 위치
- 플레이어도 학습 가능한 Agent이며, 적 3명은 협력 정책을 사용

### 프로토타입 기본값으로 제안한 내용

아래 값은 구현을 시작할 수 있도록 정한 조정 가능한 기본값이다. 사용자 피드백이나 첫 Heuristic 검증 결과에 따라 Inspector 값만 바꿀 수 있게 만든다.

| 항목 | 기본값 |
| --- | --- |
| 맵 크기 | 96×96m |
| 건물 크기 | 11×6×11m |
| 건물 중심 간격 | 18m |
| 도로 폭 | 7m |
| 외곽 벽 | 높이 4m, 두께 1m |
| 플레이어 속도 | 5.0m/s |
| 적 속도 | 4.05m/s |
| 공통 회전 속도 | 160°/s |
| 적 공격 거리 | 1.8m |
| 적 공격 재시도 간격 | 1.0초 |
| 플레이어 무적 시간 | 3.0초 |
| 점멸 간격 | 0.2초 |
| 제한 시간 | 시뮬레이션 시간 120초 |
| 결과 색 표시 | 사람/관전 모드 0.75초, 고속 학습 모드 지연 없음 |

## 3. 월드와 맵 규격

### 고정 배치

- Floor 중심은 환경 로컬 좌표 `(0, 0, 0)`이다.
- 건물 중심 X/Z 좌표는 각각 `-36, -18, 0, 18, 36`을 사용한다.
- 따라서 25개 건물은 완전한 5×5 격자를 이루고, 인접 건물 사이에는 7m 도로가 생긴다.
- 외곽 벽의 안쪽 경계는 X/Z `±48`이다.
- 도로 교차로의 중심 X/Z 후보는 각각 `-45, -27, -9, 9, 27, 45`다.
- 두 축 후보의 조합으로 36개 SpawnPoint를 만들고, 캐릭터 네 명은 서로 다른 점을 사용한다.

### 프리팹 앵커

- 각 Building 프리팹은 북·동·남·서 네 개의 `ButtonSocket`을 가진다.
- Environment 프리팹은 36개 `SpawnPoint`와 4개 `GateAnchor`를 가진다.
- 다섯 개의 서로 다른 Building을 먼저 뽑은 뒤, 각 Building마다 네 면 중 하나를 독립적으로 뽑는다.
- Gate는 북·동·남·서 외곽 벽 중 하나를 무작위로 선택하고 선택된 벽의 중앙 앵커에 생성한다.
- Gate 활성화 전에는 외곽을 통과할 수 없다. 활성화 시 막는 문 Collider를 끄고 탈출 Trigger를 켠다.

### 색과 외형

| 대상 | 기본 표현 |
| --- | --- |
| Floor | 흙과 비슷한 갈색 |
| Building | 중간 회색의 큰 큐브 |
| Player | 파란색 캡슐 또는 단순 캐릭터 |
| Player 피격 | 3초 동안 파란색/빨간색 교대 점멸 |
| Enemy | 플레이어와 구분되는 주황색 계열 |
| Player 정면 표시 | 몸체보다 밝은 청록색 돌출 큐브 |
| Enemy 정면 표시 | 몸체보다 밝은 노란색 돌출 큐브 |
| 누르지 않은 Button | 빨간색 |
| 누른 Button | 연두색, 재접촉해도 상태 변화 없음 |
| 활성 Gate | 밝은 청록 또는 연두색 Emission |
| Player 승리 Floor | 연두색 Emission |
| Enemy 승리 Floor | 빨간색 Emission |

최종 모델, 애니메이션, 사운드는 이 프로토타입 범위에 넣지 않는다. Primitive와 단색 Material로 기능을 먼저 검증한다.

## 4. 캐릭터와 조작

### 공통 이동 모델

- Rigidbody 기반 지상 이동이며 점프, 횡이동, 경사 이동은 없다.
- 전진/후진 축과 좌/우 회전 축을 별도로 적용한다.
- 이동과 회전은 같은 FixedUpdate에서 함께 적용할 수 있다.
- 충돌로 속도가 계속 쌓이지 않도록 목표 속도 또는 `MovePosition`/`MoveRotation` 방식으로 제한한다.
- 모든 Agent의 `MaxStep`은 0으로 두고 에피소드 종료는 Environment Controller만 담당한다.

### 플레이어 사람 조작

| 입력 | 기능 |
| --- | --- |
| W | 전진 |
| S | 후진 |
| A | 왼쪽 회전 |
| D | 오른쪽 회전 |
| 마우스 X/Y | 3인칭 카메라 Orbit yaw/pitch |
| I | Human/AI 제어 전환 |

- `W+A`, `W+D`, `S+A`, `S+D` 조합이 가능하다.
- 마우스는 카메라 시점만 바꾸며 캐릭터 방향은 A/D가 바꾼다.
- 카메라는 플레이어를 중심으로 약 6m 뒤, 3m 높이에서 추적하고 벽 관통 방지 SphereCast를 사용한다.
- 학습용 Headless 실행에서는 Camera와 오디오를 비활성화한다.

### 적 공격과 플레이어 체력

- 적은 플레이어와의 거리가 1.8m 이하이고 Building/Wall에 가로막히지 않았을 때 자동 근접 공격을 시도한다.
- 공격은 별도 Action Branch를 쓰지 않는다. 이동 정책이 공격 가능 위치로 접근하는 문제를 학습한다.
- 유효한 한 번의 공격은 체력 1을 감소시킨다.
- 피격 즉시 3초 무적과 빨간색 점멸을 시작한다.
- 무적 중 들어온 공격은 체력, 보상, 점멸 시간을 다시 바꾸지 않는다.
- 세 번째 유효 피격으로 체력이 0이 되는 즉시 EnemyWin 상태로 전환한다.
- 적은 이번 프로토타입에서 체력이나 사망 상태가 없다.

## 5. 에피소드 상태와 무작위화

### 상태 흐름

`Resetting → Running → PlayerWin 또는 EnemyWin → ResultFeedback → Resetting`

- `Running` 상태에서만 시간, 이동, 공격, 버튼, Gate Trigger를 처리한다.
- 승패 판정은 한 번만 허용한다. 같은 물리 프레임의 중복 Trigger가 결과를 덮어쓰지 못한다.
- 시간 초과는 게임 규칙상 적의 정상 승리이므로 학습에서도 종료 보상을 주고 Episode를 완료한다.
- 결과 피드백 뒤 Controller가 모든 Agent와 적 그룹의 Episode를 함께 끝내고 한 번만 Reset한다.

### Reset 시 반드시 초기화할 항목

- 제한 시간을 120초로 복구
- 플레이어 체력을 3, 무적과 점멸을 꺼진 상태로 복구
- 버튼 5개를 새 위치에 배치하고 모두 빨간색/미사용 상태로 복구
- 버튼 카운트를 0으로 복구
- Gate 위치를 새로 선택하고 비활성/폐쇄 상태로 복구
- 네 캐릭터의 SpawnPoint를 중복 없이 새로 선택
- 네 캐릭터의 Y 회전값을 0~360°에서 독립적으로 선택
- Rigidbody 선속도와 각속도를 0으로 복구
- Floor Material과 Emission을 갈색 기본 상태로 복구
- 보상 원장과 HUD를 초기화
- 비활성화된 Agent가 있다면 다시 활성화하고 MultiAgentGroup에 재등록

런타임 기본은 Unity `Random`을 사용한다. 자동 테스트와 재현 실험을 위해서만 선택적 Seed 주입 경로를 제공한다. 이전 에피소드와 다른 값을 강제로 뽑는 로직은 만들지 않는다.

## 6. ML-Agents 설계

### Agent, Behavior, Team, Group

| 대상 | 수 | Behavior Name | Team ID | 그룹 |
| --- | ---: | --- | ---: | --- |
| PlayerAgent | 1 | `EscapePlayer` | 0 | 독립 Agent, 공동 Self-Play 단계에서는 1인 그룹 검토 |
| EnemyAgent | 3 | `EscapeEnemy` | 1 | 한 개의 `SimpleMultiAgentGroup` |

- 적 세 명은 같은 Behavior Parameters와 Shared Policy를 사용한다.
- 적 그룹 보상은 `AddGroupReward`, 정상 승패 종료는 `EndGroupEpisode`를 사용한다.
- 비대칭 경쟁이므로 플레이어와 적은 서로 다른 Behavior Name과 Team ID를 사용한다.
- Environment Controller는 한 환경마다 고유 Enemy Group을 소유한다. 추후 학습 속도를 위해 환경을 복제해도 그룹을 공유하지 않는다.
- DungeonEscape 예제의 환경 단위 Reset과 그룹 등록 방식을 참고하되, 전역 검색 대신 직렬화된 참조를 사용해 여러 환경 복제에 안전하게 만든다.

### 행동 공간(Actions)

두 Behavior 모두 동일한 두 개의 Discrete Branch를 사용한다.

| Branch | 크기 | 값 |
| --- | ---: | --- |
| 이동 | 3 | 0 정지, 1 전진, 2 후진 |
| 회전 | 3 | 0 유지, 1 왼쪽, 2 오른쪽 |

두 Branch가 독립적이므로 전진하면서 회전할 수 있다. `Heuristic()`도 같은 ActionBuffer에 W/S와 A/D를 각각 기록하여 사람 조작과 학습 행동 사이의 의미 차이를 없앤다.

`DecisionRequester` 초기값은 Decision Period 5, Take Actions Between Decisions 활성화다. 기본 Fixed Timestep 0.02초에서 약 10Hz로 새 결정을 받고 사이 스텝에는 같은 행동을 반복한다. 체감 조작과 학습 안정성을 Heuristic으로 확인한 뒤 1~5 범위에서 조정한다.

### Ray 관측

모든 Player/Enemy 프리팹에 `RayPerceptionSensorComponent3D`를 붙인다.

| 설정 | 초기값 |
| --- | --- |
| Detectable Tags | `Player`, `Enemy`, `Wall`, `Building`, `Gate`, `Button` |
| Rays Per Direction | 12 |
| Max Ray Degrees | 180° |
| 총 Ray 수 | 25 |
| Ray Length | 24m |
| Sphere Cast Radius | 0, 실제 Raycast 사용 |
| Start/End Vertical Offset | 캐릭터 중심 높이 약 1m |
| Observation Stacks | 1 |
| Use Batched Raycasts | 활성화 |

Ray 관측 크기는 `(1 + 2×12) × (6 tags + 2) = 200`이다. 건물과 외곽 벽을 서로 다른 태그로 구분하고, Collider가 붙은 실제 GameObject에 정확한 태그를 지정한다.

### 벡터 관측(초안)

값은 모두 `[0, 1]` 또는 `[-1, 1]`로 정규화한다. 절대 월드 좌표나 벽을 투과하는 목표 방향은 제공하지 않는다.

Player 7개:

1. 현재 체력 / 3
2. 남은 시간 / 120
3. Gate 조건에 반영된 버튼 수 / 3
4. Gate 활성 여부
5. 로컬 전진 속도 / 최대 속도
6. Y축 각속도 / 최대 각속도
7. 무적 여부

Enemy 7개:

1. 자신의 로컬 전진 속도 / 최대 속도
2. 자신의 Y축 각속도 / 최대 각속도
3. 남은 시간 / 120
4. 플레이어 체력 / 3
5. 플레이어 무적 여부
6. Gate 활성 여부
7. Gate 조건에 반영된 버튼 수 / 3

Ray만으로는 이미 눌렀던 개별 Button을 구분하거나 보이지 않는 상대 위치를 알 수 없다. 첫 학습 결과에서 기억 부족이 확인되면 Discrete Action과 궁합이 맞는 LSTM을 Trainer 설정에 추가한다. 처음부터 절대 위치 같은 특권 관측을 넣지는 않는다.

### 보상(초기 기준안)

최종 승패 보상이 다른 shaping보다 충분히 크도록 유지한다.

| 사건 | Player | Enemy Group |
| --- | ---: | ---: |
| 처음 세 개의 서로 다른 Button 접촉 | 각 `+0.10` | 0 |
| 무적이 아닌 Player에게 유효 타격 | `-0.05` | `+0.05` |
| Gate 탈출 | `+1.00` | `-1.00` |
| 체력 0 | `-1.00` | `+1.00` |
| 120초 시간 초과 | `-1.00` | `+1.00` |
| 시간 비용 | 전체 120초에 최대 `-0.05` | 0 |

- 같은 Button 반복 접촉, 무적 중 공격, 벽 충돌에는 보상을 주지 않는다.
- 세 번째 이후의 추가 Button은 시각 상태는 바뀔 수 있지만 추가 shaping 보상은 없다.
- 플레이어가 일부러 맞아서 적을 유인하거나 적이 무적 중 계속 공격하는 행동으로 보상을 만들 수 없게 한다.
- Self-Play 단계에서는 문서 권고대로 shaping을 더 줄이고 최종 `+1/-1` 중심 실험도 비교한다.
- HUD의 보상은 별도 임의 점수가 아니라 Player에게 실제 적용된 reward delta의 에피소드 누계를 표시한다.

### 사람, 학습, 추론 모드

Player는 내부적으로 세 모드를 가진다.

| 모드 | Behavior Type | 동작 |
| --- | --- | --- |
| Human | `HeuristicOnly` | W/S/A/D 입력 사용 |
| Training | `Default` | Python Trainer가 연결되면 Remote Policy, 없으면 AI 입력 0 |
| Inference | `InferenceOnly` | 지정된 ONNX 모델 사용 |

- HUD의 AI Toggle은 상태 표시 전용이며 마우스로 조작하지 않는다. `I` 키로 Human/AI를 전환한다.
- ON은 Trainer 연결 중이면 Training, 학습 모델이 연결되어 있으면 Inference를 뜻한다.
- Trainer와 모델이 모두 없으면 ON 상태에서 에이전트를 움직이지 않고 `AI 모델/Trainer 대기` 상태를 표시해, AI라고 표시하면서 키보드가 대신 움직이는 혼동을 막는다.
- 적은 개발 초기에는 검증용 Rule-based Heuristic 추격을 사용하고, 학습 후에는 세 Agent가 같은 Enemy ONNX 모델을 사용한다.

### 학습 순서

1. **환경 검증:** Player Human + Enemy Rule-based Heuristic으로 모든 규칙과 Reset을 검증한다.
2. **Player 단독 학습:** `EscapePlayer`를 PPO로 학습하고 Enemy는 고정 Heuristic을 사용한다.
3. **Enemy 협력 학습:** `EscapeEnemy`를 MA-POCA(`trainer_type: poca`)로 학습하고 Player는 고정 Heuristic 또는 Player ONNX를 사용한다.
4. **상대 다양화:** 여러 난이도의 고정 상대 모델을 바꾸며 각 정책의 과적합을 줄인다.
5. **선택적 비대칭 Self-Play:** 두 Behavior에 서로 다른 Team ID와 Self-Play 설정을 주고 공동 학습을 실험한다. 환경·보상 기준선이 안정되기 전에는 시작하지 않는다.

Player와 Enemy 학습 설정은 분리된 YAML로 시작한다. 준비된 `C:\Users\USER\miniconda3\envs\mlagents` 환경의 Python 3.10.12, `mlagents 1.2.0.dev0`, PyTorch CUDA 구성을 사용한다.

16개 병렬 Arena의 계산과 Benchmark 계획은 [Escape 성능 계획 보관본](escape-performance.md)에 남겨 두었다. 해당 Benchmark는 개발 종료로 취소됐다.
학습 Build는 ML-Agents 4.0.3의 `TrainingAreaReplicator`로 Environment 프리팹을 복제하며 `mlagents-learn --num-areas`로 Arena 수를 전달한다.

## 7. UI Toolkit 명세

씬에는 Runtime `UIDocument` 한 개와 Escape 전용 `PanelSettings`, UXML, USS를 사용한다. Legacy Canvas/uGUI는 사용하지 않는다.

| 위치 | 표시 | 예시 |
| --- | --- | --- |
| 중앙 상단 | 남은 시간 | `남은 시간 119.8` |
| 좌측 하단 | Gate 조건에 반영된 버튼 수 | `버튼 0/3` |
| 우측 하단 | 플레이어 체력 | `체력 3/3` |
| 좌측 상단 | Player의 현재 에피소드 누적 보상 | `보상 +0.000` |
| 우측 상단 | AI 상태와 단축키 | `AI (I 키)`, `Human/Training/Inference` |

- 버튼 표시는 최대 `3/3`으로 고정한다. 맵에는 여전히 총 5개가 존재한다.
- 결과 상태에서는 중앙에 `탈출 성공` 또는 `적 승리`를 짧게 표시한다.
- UI는 Controller 이벤트를 구독하고 매 프레임 Hierarchy 검색을 하지 않는다.
- 학습용 Headless 실행에서는 UIDocument를 끌 수 있어야 한다.

## 8. 예정 에셋과 코드 구조

당시 모든 신규 Unity 에셋은 `Assets/Escape` 아래에 두었다. 현재 보관 루트는 `Assets/_Legacy/Escape`다.

```text
Assets/Escape/
  Scenes/
    EscapePrototype.unity
  Prefabs/
    Environment/EscapeEnvironment.prefab
    Environment/EscapeBuilding.prefab
    Environment/EscapeButton.prefab
    Environment/EscapeGate.prefab
    Agents/EscapePlayer.prefab
    Agents/EscapeEnemy.prefab
  Scripts/
    Agents/EscapeAgentMotor.cs
    Agents/EscapePlayerAgent.cs
    Agents/EscapeEnemyAgent.cs
    Agents/EscapePlayerHealth.cs
    Agents/EscapeEnemyAttack.cs
    Camera/EscapeThirdPersonCamera.cs
    Core/EscapeEnvironmentController.cs
    Core/EscapeEpisodeState.cs
    Core/EscapeRandomizer.cs
    Core/EscapeRewardLedger.cs
    Input/EscapePlayerControlRouter.cs
    World/EscapeButton.cs
    World/EscapeGate.cs
    UI/EscapeHudController.cs
  Materials/
  UI/
    EscapeHud.uxml
    EscapeHud.uss
    EscapePanelSettings.asset
  Training/
    escape_player_ppo.yaml
    escape_enemy_poca.yaml
  Tests/
    EditMode/
    PlayMode/
```

실제 파일 수는 책임 분리에 따라 조금 달라질 수 있다. 첫 구현 예상 규모는 씬 1개, 프리팹 6~8개, 런타임 스크립트 약 14개, Material 7개 내외, UI 에셋 3개, Trainer 설정 2개다.

전역 프로젝트 설정 변경은 최소화한다. 현재 `Wall` 태그는 있고 Unity 기본 `Player` 태그를 사용할 수 있다. 구현 시 `Enemy`, `Building`, `Gate`, `Button` 태그를 `TagManager`에 추가해야 하며, 기존 `Wall`, `Goal`은 보존한다.

## 9. 구현 단계와 완료 기준

### M1 — 정적 맵과 데이터 구조 (완료)

- EscapePrototype 씬과 Environment 프리팹 생성
- 25개 건물, 36개 SpawnPoint, 100개 ButtonSocket, 4개 GateAnchor 검증
- 갈색 Floor, 외곽 벽, 기본 조명과 단색 Material 구성

완료 기준: 위치 공식과 실제 Transform 수가 자동 테스트에서 일치하고 도로가 막히지 않는다.

### M2 — Heuristic 게임 완성 (완료)

- 공통 이동, 사람 조작, 카메라, 체력/무적/공격 구현
- 버튼, Gate, 120초 Timer, 승패, Floor 피드백 구현
- 매 Episode 전체 Randomization과 Reset 구현
- UI Toolkit HUD와 AI Toggle 구현

완료 기준: 학습기 없이 한 판을 처음부터 끝까지 플레이하고 즉시 다음 판을 시작할 수 있다.

### M3 — Agent와 협력 구조 연결 (완료)

- Player/Enemy Agent, Behavior Parameters, DecisionRequester, Ray Sensor 구성
- Player와 Enemy의 Observations/Actions/Rewards 구현
- Enemy 3명을 SimpleMultiAgentGroup에 등록
- Training/Inference/Human 모드 전환과 Trainer YAML 초안 작성

완료 기준: Heuristic과 Agent 행동 의미가 같고, Python Trainer를 연결하기 직전 상태에서 Console 오류 없이 Episode가 반복된다.

### M4 — 사전 학습 검증 (완료)

- EditMode/PlayMode 테스트 실행
- Behavior Name, Team ID, Branch Size, Vector Observation 수, Ray 태그 검증
- 100회 이상 자동 Reset에서 Spawn 중복, 버튼 수, Gate 수, 상태 누수 검사
- Unity 컴파일 및 짧은 Heuristic Smoke Test
- 준비된 Python/ML-Agents 환경으로 Trainer 연결 Smoke Test 준비
- `TrainingAreaReplicator`와 `--num-areas`로 Arena 수를 1/4/8/16으로 바꿀 수 있는 Headless 성능 측정 경로 준비

당시 완료 기준은 실제 장기 학습만 남긴 상태였으며, 환경 로직·보상 이벤트·Reset·센서 구성까지 검증했다. 장기 학습은 이후 개발 종료로 진행하지 않았다.

## 10. 테스트 체크리스트

- [x] 건물이 정확히 25개이고 5×5 동일 간격이다.
- [x] 캐릭터 네 명의 SpawnPoint가 한 Episode 안에서 중복되지 않는다.
- [x] 버튼은 정확히 5개이고 서로 다른 Building에 붙는다.
- [x] 각 버튼은 선택된 Building의 네 면 중 정확히 한 면에 정렬된다.
- [x] 같은 버튼을 여러 번 밟아도 카운트와 보상이 한 번만 증가한다.
- [x] 세 번째 버튼에서 Gate가 한 번만 활성화된다.
- [x] Gate는 한 Episode에 정확히 하나이며 새 Episode마다 다시 추첨된다.
- [x] W/S와 A/D 동시 입력이 이동과 회전에 동시에 반영된다.
- [x] 적 속도가 플레이어 속도보다 낮다.
- [x] 첫 유효 피격 뒤 3초 동안 추가 피해가 무시된다.
- [x] 세 번째 유효 피격에서 즉시 EnemyWin이 된다.
- [x] 120초 시간 초과와 Gate 탈출이 각각 올바른 승패를 한 번만 만든다.
- [x] Floor가 결과 색을 보인 뒤 갈색으로 복구된다.
- [x] 모든 Ray Sensor의 여섯 태그와 Collider 태그가 일치한다.
- [x] Player/Enemy Action Branch가 `[3, 3]`이고 Vector Observation이 각각 7이다.
- [x] 세 Enemy가 같은 Behavior Name과 같은 MultiAgentGroup을 사용한다.
- [x] UI 다섯 영역이 지정 위치와 값을 표시하고 Reset 직후 초기값으로 돌아간다.
- [x] Human, Training, Inference 모드가 한 Agent의 입력을 동시에 덮어쓰지 않는다.

## 11. 프로토타입에서 제외하는 범위

- NavMesh 기반 이동, A* 경로 탐색, 미니맵
- 점프, 달리기, 구르기, 플레이어 공격, 적 체력
- 건물 내부, 문 열기 애니메이션, 복잡한 3D 모델
- 사운드, 파티클, 완성형 애니메이션, 설정 메뉴
- 온라인 멀티플레이, 저장/불러오기, 리더보드
- 절차 생성 도로, 건물 크기 변화, 맵 크기 Curriculum
- 실제 긴 학습 실행과 ONNX 품질 선정

이 항목들은 당시 범위에서 제외됐고 Escape 개발 종료에 따라 추가 결정하지 않았다.

## 12. 확정된 해석과 기본 동작

1. Gate는 북·동·남·서 네 외곽 벽 중 무작위로 선택된 한 벽의 중앙에 설치한다.
2. 적 공격은 “가까이 있으면 자동 근접 공격”으로 해석했고 공격 버튼이나 공격 Action은 두지 않았다.
3. 마우스는 카메라만 회전하고 캐릭터 몸 방향은 A/D로만 회전하도록 해석했다.

2번과 3번은 당시 기본 동작으로 구현됐다.

# Soccer 경기·정책 계약

- 대상: Core, 학습, Rule, UI 담당자
- 상태: 공통 런타임 계약의 단일 기준
- 마지막 검토: 2026-09-04

이 문서의 값은 모든 활성 Soccer 환경에 공통이다. 한 팀의 실험을 위해 바꾸지 않는다. 구현 기준은 `AgentSoccer`, `SoccerEnvController`, `SoccerSettings`, `SoccerDefenderKeeperRules`, `SoccerDefensiveClearanceRules`와 `SoccerProjectBuilder`다.

## 경기 흐름

| 항목 | 계약 |
| --- | --- |
| 경기 시간 | `300초` |
| 팀 구성 | Red 4명, Navy 4명 |
| 상태 | `Playing → GoalPause → Playing` 또는 `Finished` |
| Goal Pause | 득점 후 `3초`, 공과 선수 정지 뒤 Kickoff 위치로 Reset |
| 득점 | 득점 팀 `+1`, 실점 팀 `-1` |
| 종료 | 승리 팀 `+0.5`, 패배 팀 `-0.5`, 무승부 추가 보상 없음 |
| Episode | 팀별 `SimpleMultiAgentGroup`, 경기 Controller가 Reset 권한 보유 |

세부 shaping은 [보상 기준표](rewards.md)를 사용한다.

## 선수 역할

| 역할 | 인원 | 책임 |
| --- | ---: | --- |
| `DefenderKeeper` | 1 | 후방 보호, 위협 공 적극 차단과 Goal 복귀 |
| `Midfielder` | 2 | 자유로운 지원·연계·공수 전환 |
| `Striker` | 1 | 전방 침투와 득점 시도 |

미드필더와 스트라이커는 시작 위치에 고정하지 않는다. 역할 차이는 관측과 속도 계수에 반영되지만 관측 순서와 Action 의미는 같다.

## 관측 계약 v2

기본 Vector Observation은 `43`개다.

| 순서 | 내용 |
| --- | --- |
| 1 | 역할 One-hot 3개 |
| 2 | 자기 위치·속도 4개 |
| 3 | 공 상대 위치·속도 4개 |
| 4 | 중립·아군·상대 소유권 3개와 Kick 가능 여부 1개 |
| 5 | 동료 3명의 상대 위치·속도, 각 4개 |
| 6 | 상대 4명의 상대 위치·속도, 각 4개 |

Red와 Navy는 공격 방향을 기준으로 X축을 대칭 정규화한다. Agent 등록 순서, 역할 순서, 정규화 분모를 바꾸면 정책 계약을 올리고 모든 ONNX를 다시 검증해야 한다.

전·후방 `RayPerceptionSensorComponent3D`를 함께 사용한다. 각 선수는 전방 센서 `1개`와 로컬 Yaw `180°`인 후방 센서 `1개`를 정확히 가져야 한다. 2026-08-24 변경 전에도 후방 센서가 이미 존재했으므로 중복 센서를 추가하지 않았고, Builder와 EditMode 검증으로 정확한 개수를 강제한다.

- 전방 Ray: 방향당 `5`, 최대 각도 `60°`, Stack `3`, Observation `264`
- 후방 Ray: 방향당 `1`, 최대 각도 `45°`, Sphere radius `0.5`, Stack `3`, Observation `72`
- 전체 정책 입력: `43 + 264 + 72 = 379`
- Ray 거리: `80`
- 공, 양쪽 Goal, 벽, 양 팀 선수를 구분한다.

Ray 이름, Tag 순서, 각도, Layer Mask와 Stack은 ONNX 입력 계약이다.

## Action 계약 v2

Discrete Branch는 `[3,3,3,3]`이다.

| Branch | `0` | `1` | `2` |
| --- | --- | --- | --- |
| 전후 이동 | 정지 | 전진 | 후진 |
| 좌우 이동 | 없음 | 오른쪽 | 왼쪽 |
| 회전 | 없음 | 왼쪽 | 오른쪽 |
| Kick | 없음 | Controlled Kick | Strong Kick |

Neural, model 없는 fallback, Rule FSM은 같은 Branch를 `AgentSoccer`에 전달한다. Branch 순서나 의미를 팀 전용으로 바꾸지 않는다.

### Neural 근거리 슈팅 기술층

사람도 공 접촉과 슛 타이밍을 맞추기 어려운 현재 물리를 고려해 `SoccerNeuralKickAdvisor`를 모든 Neural 팀의 공통 기술층으로 사용한다. 이 코드는 신경망이 이동·회전·소유 판단을 담당한다는 전제에서 다음의 제한된 Kick 선택만 보정한다.

- 확정된 공 소유자이고 공이 선수 `1.8m` 안에 있으며 KickPlate가 준비된 경우만 적용한다.
- 상대 골문 중심까지 `24m` 이내에서 실제 접촉 혼합 방향이 골대 안쪽 `0.5m` 여유 범위를 통과할 때, Neural이 킥하지 않았다면 `12m` 안은 Controlled, 그 밖은 Strong Kick을 권장한다.
- 같은 근거리에서 Neural이 골문 밖으로 향한 킥을 요청하면 그 결정 주기에는 킥을 보류한다. 골문 궤적으로 요청한 기존 킥의 힘은 바꾸지 않는다.
- Human, Rule, 모델 없는 fallback, 공 미소유자, 골문 `24m` 밖에는 개입하지 않는다. L1 최종에서는 Controller가 기술층을 끄고 실제 8m 운반이 확인된 뒤에만 다시 켠다.
- 개입 횟수는 `Soccer/Skill Advice/Shot Recommended`, `Off Target Shot Deferred`와 Red/Navy 분리 태그에 합계로 기록한다.

이 기술층은 Observation `379`, Action `[3,3,3,3]`, 킥 힘과 접촉 방향을 바꾸지 않지만 실행 Action 분포에는 개입한다. 따라서 기존 ONNX도 새 Player에서 다시 평가해야 하고, 최종 모델의 성과는 신경망 단독 기술과 코드 보조가 합쳐진 결과로 명시한다.

최종 L1 `CarryAndScore`에서는 8m 운반 전 킥 branch 1/2도 action mask로 잠근다. r016 첫 100k에서 기존 정책이 99.3%의 에피소드에서 운반 전에 킥해 운반 충족률이 2.35%에 머문 원인에 대한 직접 기술 제약이다. 같은 선수가 잠깐 공을 놓쳤다가 다시 잡으면 운반 진행을 보존하고, 다른 Red가 소유하면 진행과 잠금을 다시 시작한다. 8m 달성 시 킥과 슈팅 기술층을 함께 열고 `Soccer/Skill Advice/Carry Gate Unlocked`로 기록한다. 이 제약 역시 L1 커리큘럼 Neural에만 적용하며 Human·Rule·fallback과 일반 Stadium 경기는 바꾸지 않는다.

## 사람 조작

L2 준비용 `soccer_l2_pass_aim_gate`(기본0)는 난이도<1에서만 켤 수 있는 임시 킥 action mask다. 확정 Neural carrier의 실제 접촉 혼합식 예상 방향이 유리한 동료 통로와 맞지 않으면 킥1/2를 차단하되 이동/회전/킥 힘/관측379/Action[3,3,3,3]/성공 판정은 변경하지 않는다. Human/Rule/fallback 제외, 라운드 초기화/Controller 해제 시 정리한다. 난이도1에서는 강제 해제하며 개입 횟수를 별도 기록한다. L1 슈팅 기술층과 구분한다.

L2 방향 학습은 최초 소유 때 고정한 지정 수신자의 킥 전 예상 방향 개선과 실제 명시적 킥의 동료 방향 품질을 보상 신호로만 사용한다. 첫 자세는 무보상 기준선이며 두 구간은 팀/라운드 cap0.1을 공유한다. 킥 방향·힘·Action을 대신 선택하지 않고, 느슨한 방향 품질을 기존 패스 통로/수신 성공으로 인정하지 않는다. 상세 cap과 지급 조건은 보상 기준표를 따른다. 관측379/Action[3,3,3,3]/L0·L1 기술층은 유지한다.

L2 최신 거리 기준(2026-09-04 r003 진단 후)은 **공 순변위2m**, 킥 시점 대상 거리3~16m다. 수신자 중심 도착 전 실제 접촉하는 거리를 구분한 것이며, 아래 이전3m 공 순변위 서술보다 우선한다. 일반 경기 패스 거리3m는 바꾸지 않는다. 확정된 지정 수신자 제어 확인과 나머지 L2 조건은 유지한다.

L2-A의 명시적 `soccer_l2_waiting_assist=1`은 준비 단계에서만 지정 발신자 외 필드 선수에게 대기를 제공한다. 지정 수신자는 현재 발신자 전방15.5m에서 시작한다. Env 등록 + Agent action mask/평면 속도 제한이며 순간이동·자동 패스·킥 힘 변경은 없다. 유리한 실제 패스가 나가면 대기를 해제한다. Human·Rule·Heuristic fallback은 제외하고 Keeper는 기존 보호를 유지한다. 기본값 0 및 최종 난이도 1에서는 비활성이다. 학습 결과에는 코드 보조 포함 여부를 함께 기록한다.

L2 ShortPass 커리큘럼은 별도 레슨으로, L1 슈팅 기술층과 8m 운반 전 킥 잠금을 사용하지 않는다. 확정 소유자의 공 1.8m 이내 킥 기회 조건은 유지한다. 패스 조준·실행과 수신자 이동은 Neural이 결정하며 코드가 패스 방향/힘을 자동 선택하지 않는다. 킥 당시 더 유리한 수신자와 실제 킥 통로를 고정하고, 순변위2m 이후 해당 선수가 현재 확정 carrier·최종 접촉자·공2.4m 이내인지 FixedUpdate에서 확인한다. r002 진단 후 인계와 인계 후 유지를 분리하여 추가0.75초 유지 조건은 제거했다. 단순 터치 콜백만으로 완료하지 않는다. 일반 경기 패스 판정과 보상 수치는 그대로다.

| 동작 | 키보드 | Xbox Controller |
| --- | --- | --- |
| 전진 | `W` | Right Trigger |
| 후진 | `S` | Left Trigger |
| 회전 | `A/D` | Left Stick X |
| 패스 | `E` | `A` |
| 슈팅 | `Space` | `B` |
| Human/AI 전환 | `H` | `Y` |

Human 모드는 Red의 human-controllable 선수 한 명에만 적용된다. 나머지는 현재 Neural 또는 Rule Controller를 계속 실행한다.

사람 이동은 흔히 쓰이는 target planar velocity 방식이다.

- Gamepad Trigger와 Stick은 dead zone `0.15` 뒤 analog 값을 유지한다.
- 최대 평면 속도는 `9m/s`다.
- 가속은 `137.5m/s²`, 정지·역방향 감속은 `48m/s²`다.
- `Vector3.MoveTowards`로 FixedUpdate마다 목표 속도에 접근한다.
- Human일 때만 Rigidbody `Interpolate`, Training·AI 복귀 시 `None`을 사용한다.
- Human은 Decision Period `1`, Neural·Rule은 `5`이며 결정 사이 행동을 반복한다.

AI·Rule의 추론 비용과 기존 학습 의미를 유지하기 위해 analog target-velocity 경로는 Human에만 적용한다. Human 대상 Striker의 한 FixedUpdate 전진 속도 변화량은 `137.5×0.02 = 2.75m/s`, AI Striker는 `2.2×1.25 = 2.75m/s`로 같다. 변경 전 Human은 `32×0.02 = 0.64m/s`여서 최고속도는 같아도 가속 체감이 약 `4.3배` 느렸다.

## 이동과 Kick 물리

| 항목 | 값 |
| --- | ---: |
| AI 이동 기본 배율 | `agentRunSpeed = 2.2` |
| 역할별 전진 계수 | DefenderKeeper `1.05`, Midfielder `1.1`, Striker `1.25` |
| 최대 평면 속도 | `9m/s` |
| 회전 속도 | `125°/s` |
| Controlled Kick | `2000` |
| Strong Kick | `5000` |
| 일반 접촉 Dribble Push | `120` |
| KickPlate 전진 시간 | `0.08초` |
| KickPlate 복귀 시간 | `0.5초` |

KickPlate가 복귀하기 전에는 새 Kick Branch를 mask한다. Rigidbody, Collider, Physics Material, 속도 clamp와 Kick 힘은 전술별 튜닝 대상이 아니다.

## 활성 Stadium 공통 Scene

Base·Attack·Defense·Press·Rule의 기본 Scene과 Prefab은 모두 Stadium이다. 다섯 Prefab은 고유 GUID를 가진 독립 Regular Prefab이지만 물리·시각 계약은 `Core/Prefabs/StadiumEnvironment_Base.prefab`에서 동일하게 복제한다. 예전 `SoccerEnvironment_*`와 `Soccer4v4_*`는 이력 보존용이며 Build Settings와 학습 Scene으로 사용하지 않는다.

| 항목 | 현재 공통값 |
| --- | ---: |
| 필드 길이 × 폭 | 약 `124 × 84.655m` |
| 골문 개구 폭 / 높이 / 깊이 | 데모 Goal, 약 `20.393 / 6.291 / 5.635m` |
| 공 uniform local scale | `0.012705` |
| 공 월드 지름 | 약 `1.057m` |
| 공 Reset 중심 Y | 반지름과 동일, 약 `0.529m` |
| 선수 Reset Y | `0.52m` |
| AI Overview Camera / FOV | `(0,100,-100)` / `55°` |
| Controlled Kick / Strong Kick | `2000 / 5000` |
| 선수 표시 색 | Red 고채도 빨강 `#A00218`, Navy 선명한 남색 `#002770` |
| 킥 플레이트 색 | Red `#4F0D18`, Navy `#081A38` |
| 골대 표시 색 | Red `#D33F4E`, Navy `#2F5C9C` |

Demo의 `SM_Football_Field` Mesh 경계를 기준으로 길이를 맞춘다. 모든 데모 Root에 추가 균일 배율 약 `1.010856`과 같은 원점 이동을 적용하며 GrantStadium의 최종 local scale은 세 축 모두 약 `2.021713`이다. Terrain은 Transform 확대를 지원하지 않으므로 독립 데이터 크기에 같은 배율을 적용한다. 물리 필드 중심은 `(0,0,0)`이고 공격축은 기존과 같은 X다. 선수·공·센서는 데모 확대 Hierarchy 밖에서 독립 크기를 사용한다. 후속 승인에 따라 데모 골대 두 개의 좌우 축만 직전 Stadium의 `2배`로 늘렸으며, 높이·앞뒤 local scale과 나머지 데모 Root·Terrain은 유지한다.

필드 테두리에는 높이 `2.835m` (직전 `3.15m` 대비 `-10%`, 기존 `4.5m` 대비 `-37%`), 전 구간 두께 `0.12m`, Alpha `0.224`인 하늘색 벽을 놓는다. Alpha는 이전 `0.28`에서 불투명도를 상대적으로 `20%` 낮춘 값이고, 직선 벽은 이전 `0.5m`에서 모서리 판과 같은 두께로 얇아졌다. 양 끝은 넓어진 골문을 비우고 분할한다. 네 모서리에는 각각 3개씩 총 12개의 얇은 Cube 판을 둔다. 후속 승인으로 기준 현 길이를 `1 → 1.5m`로 늘렸으며, 접합 여유를 포함한 실제 판 크기는 약 `1.54 × 2.835 × 0.12m`다. 반경 약 `2.898m`의 90도 구간을 세 직선으로 나누고 접합부에 총 `0.04m` 여유를 더해 틈을 막는다. 직선 벽은 이 모서리의 접점까지 줄인다. 네 모서리가 제외하는 평면 면적은 합계 약 `8.397m²`, 전체 필드의 약 `0.080%`다. `3개 × 4모서리`와 네 직선 구간을 유지하므로 골문 입구를 제외한 외곽은 `16개 변`이다.

직선 벽 6개와 모서리 판 12개 모두 같은 Material과 `wall` Tag, 충돌·센서 인식을 사용한다. 공·양 팀 골문은 `ball`, `redGoal`, `navyGoal` Tag와 기존 Sensor LayerMask 계약을 사용한다. 선수와 킥 플레이트는 `redAgent`, `navyAgent` Tag를 사용한다. 평평한 바닥 충돌면과 Terrain은 Ignore Raycast Layer로 두며 데모의 중복 바닥·장식 충돌면은 복사본에서 비활성화한다.

Red가 수비하는 음수 X 골대는 붉은색, Navy가 수비하는 양수 X 골대는 푸른색 계열이다. `Core/Materials/StadiumRedGoal.mat`와 `StadiumNavyGoal.mat`를 별도로 사용하고 원본 노란색 텍스처 아틀라스는 적용하지 않는다. 선수 몸 중심이 해당 골문 폭·높이·깊이 안에 있고 카메라에서 선수로 향하는 선분이 실제 골대 Collider를 먼저 통과할 때만 같은 RGB를 유지한 채 Alpha가 `0.25`로 바뀐다. 골문 밖 선수, 골문 안 선수를 열린 입구에서 보는 경우, 화면 밖 선수는 골대를 반투명하게 만들지 않는다. 가림이 끝나면 원래 불투명 재질로 돌아간다. 공급 에셋의 공유 재질·골대 Mesh·Collider는 수정하지 않고 Core 복사본의 팀 Tag와 머티리얼만 공용 Builder가 관리한다.

골문 안쪽에 이미 있던 Floor Collider를 중복 생성하지 않고 같은 Cube의 Renderer를 활성화해 연한 회색 `#D2D6DB` 바닥으로 표시한다. 전용 `Core/Materials/StadiumGoalFloor.mat`를 사용하고, 크기는 실제 골문 깊이·개구 폭과 같으며 윗면은 필드 높이 `Y=0`에 맞춘다. Back·Side·Roof Collider는 계속 보이지 않는다.

Stadium 복사본의 `SM_Tree_*`·`SM_Fir_*` 나무 배치와 Terrain 나무 인스턴스를 모두 제거한다. 추가 최적화 대상으로 승인된 도로·주차장 27개, 관목·꽃 104개, 외부 도시 소품 42개도 Core 기준본에서 제거하고 Base·Attack·Defense·Press·Rule에 동일하게 전파한다. 제거 대상에는 `SM_Road_*`, `SM_R_P8`, `Parking`, `SM_R_Gr*`, `SM_Bush_*`, `SM_Flower_A`, `SM_Flowers_01`, `SM_City_Light`, 도시 벤치·펜스·배리어·교통 신호·자전거가 포함된다. 공급자 원본 Prefab·Mesh·Texture와 Demo Scene은 보존하며 Terrain 표면·크기·레이어도 변경하지 않는다.

공의 `FreezePositionY`를 보존하되 중심과 바닥 사이의 `0.02m` 틈을 없애 중심 Y를 월드 반지름에 맞춘다. Stadium 공 컨트롤러는 매 물리 프레임의 평면 속도와 반지름으로 `ω = up × v / r` 굴림 각속도를 계산하므로 좌우 Yaw만 보이는 대신 진행 방향에 맞춰 구른다. Y 위치·속도는 고정하며, `SoccerArenaGeometry`의 반지름 포함 경계 제한이 Side·End·세 판 모서리·골문 깊이 밖의 최종 위치를 복구한다. 물리 벽과 Continuous Collision Detection이 주 경계이고 이 제한은 고속 충돌 누락이나 직접 위치 변경을 막는 최종 안전장치다.

바닥 충돌에 의존하지 않는 골문 안쪽 Trigger를 별도로 두고, 공 중심이 골라인을 넘어 실제 골문 폭·높이·깊이 안에 있을 때 한 번 득점한다. 골대 프레임 충돌은 득점하지 않으며 Trigger도 센서 LayerMask에서 제외한다. 득점 후에는 기존 Goal Pause와 전체 Reset 경로를 사용한다.

경기 시간·Goal Pause·4v4 역할·팀 설정·이동·공 질량/damping·보상 수치·입력 `379`·Action은 모든 Stadium에서 같다. 공통 코드 기본값과 다섯 Prefab은 Controlled Kick `2000`, Strong Kick `5000`을 사용한다. Human·Neural·fallback·Rule이 같은 힘을 읽으며, 위험 Strong Kick을 중앙으로 돌리는 안전 보정에는 Controlled 값 `2000`이 적용된다.

폭·골문 치수·모서리 경계를 `SoccerArenaGeometry`에서 읽어 자기 위치 관측 정규화, 목표 위치, 자기/상대 골문 궤적, Keeper 접근과 득점 판정을 맞춘다. Geometry 참조가 임시로 없을 때의 fallback도 Stadium 치수를 쓴다. AI 목표는 선수 반경 여유 `0.55m`를 두고 모서리 판 안쪽으로 제한한다. 보상·전술 거리 상수는 비례 확대하지 않는다. 데모 골대의 시야 가림은 Alpha `0.25` 표시 로직을 사용하며 Collider와 센서 인식은 유지한다.

입력 shape가 같아도 경기 폭·골문·공 크기에 따른 기존 ONNX 정책 품질은 재평가가 필요하다. Build Settings와 팀별 학습 Scene은 모두 Stadium 경로를 사용한다.

## DefenderKeeper 보호

공격 방향을 양수로 정규화한 깊이를 사용한다.

- `-4m`까지는 일반 이동을 허용한다.
- `-4m → 0m`에서 공격 방향 이동·속도를 선형으로 줄인다.
- 하프라인 `0m`에서는 공격 방향 힘과 기존 속도를 제거한다.
- 이미 하프라인을 넘었으면 즉시 `RecoverGoal` 대상이다.
- 상대가 먼 지역에서 소유하거나 공보다 지나치게 앞선 경우 `RecoverGoal`로 시작 Goal 깊이에 복귀한다.
- 상대 또는 중립 공이 자기 공격 기준 깊이 `-8m` 이하에 있거나 골문으로 빠르게 향하면 `EngageBall`로 전환해 공과 골문 사이를 적극 차단한다.
- `RecoverGoal`의 Z 추적 폭은 `±28m`(기존`±14m`의2배), `EngageBall`의 Z 추적 폭은 `±36m`(기존`±18m`의2배)다. Engage 목표는 시작 위치와 공 위치를 `72%` 비율로 보간하되 공격 깊이 `-6m`를 넘지 않는다. 하프라인의 전진 안전 경계는 확대하지 않는다.
- 공이 골문 위험 깊이 `-48m` 이하에 있으면 공 위치까지 직접 들어간다. 목표 위치는 Stadium의 실제 반폭·Goal 깊이·둥근 모서리를 반영해 Clamp하므로 골라인 안의 공에도 접근할 수 있다.

Neural, fallback, Rule 모두 최종 `ConstrainMovement`와 `ConstrainVelocity`를 통과한다. Rule은 별도로 긴급 `RecoverGoal` 상태를 사용하지만 공통 shield를 우회할 수 없다.

## 공통 자책골 방지와 기본 선택

모든 Controller의 명시적 Controlled/Strong Kick은 실제 공 접촉 직전에 `SoccerDefensiveClearanceRules`를 통과한다.

- 자기 Goal plane `±62m`, 실제 반폭 약 `10.196m`와 공 여유 폭 `0.75m`를 향해 최대 `36m` 안에서 교차할 것으로 예상되는 Kick은 자책골 위험으로 본다.
- 요청 방향만 보지 않고 현재 공 속도와 `Kick 힘 / 질량 × fixedDeltaTime`을 합친 예상 속도를 사용한다. 중앙 Controlled 보정 후에도 기존 골문 방향 속도가 더 크면 그 X 성분을 제거한 뒤 걷어낸다.
- 골문 위험 지역은 공격 기준 깊이 `-48m` 이하, 좌우 약 `±14.196m`다. 이 지역에서 공격 성분이 `0.15` 이하인 옆·후방 Kick도 위험 행동으로 본다.
- 위험 Kick은 공에서 필드 중앙 `(0, 0)`을 향하는 Controlled Kick으로 바꾼다. 골라인 안쪽 공도 같은 방향으로 중앙 패스·걷어내기가 가능하다.
- 골라인 뒤라도 좁아진 골대 바깥쪽에 있는 공은 자책골 궤적으로 오판하지 않는다.
- Strong Kick은 수비 진영의 전진 걷어내기, 동료가 있는 긴 패스 lane, 또는 Goal까지 `24m` 이내의 실제 슛 궤적일 때만 의미 있는 선택으로 분류한다.

v2 ONNX가 없는 model fallback은 공에 닿자마자 Strong Kick을 반복하지 않는다. 기본은 공을 공격 방향 `10m`와 자기 lane `3m` 앞으로 운반하고, `7m` 안에서 압박받을 때 `5~28m` 동료에게 Controlled Pass를 선택한다. 골문 위험 지역에서는 중앙 Controlled Clearance, Goal까지 `24m` 안의 유효 궤적에서만 Strong Shot을 사용한다. Rule FSM도 골문 위험 지역에서는 중앙 Controlled Clearance를 사용하고, 슛 거리를 `22m`로 제한하며, 최대 `1.5초` 운반 뒤 열린 동료가 있으면 Pass를 선택한다.

## 공간과 포메이션

- 미드필더·스트라이커 고정 Anchor는 없다.
- Formation은 간격, 지원 선택지, 공수 균형과 DefenderKeeper 역할을 평가한다.
- 같은 팀 최단 거리 `3m` 미만은 소유권과 무관하게 별도 crowding 판정을 받는다.
- Rule은 보상으로 학습하지 않으므로 FSM에 allocation 없는 teammate separation 조향을 사용한다.

정확한 임계값, 배율과 상한은 [보상 기준표](rewards.md)에만 기록한다.

## HUD와 Camera

- 경기 시작은 AI 모드다.
- 점수, 남은 시간, AI/Human 상태, 양 팀 전술과 경기 누적 보상을 표시한다.
- Core Stadium만 전술·누적 보상 패널을 우측 중앙에서 우측 하단 `20px` 여백으로 옮긴다. 공유 UXML/USS와 기존 Scene의 우측 중앙 배치는 유지한다.
- 좌측 하단에는 테두리 없는 중앙 정렬 조작 표를 표시한다.
- 우측 상단의 중복 조작 안내는 사용하지 않는다.
- AI Overview Camera는 `(0,82,-78)`, FOV `50`이다.
- Goal이 화면 안 선수를 가릴 때만 전용 Renderer를 Alpha `0.25`로 전환한다. Collider와 Goal Tag는 유지한다.

## 계약 변경 조건

다음 변경은 Tensor 계약을 깨므로 정책 계약 버전을 올리고 모든 Model을 교체해야 한다.

- Observation 개수·순서·정규화
- Ray 구성
- Action Branch·값 의미
- 역할·Agent 등록 순서

다음 변경은 Tensor shape가 같아도 기존 정책의 행동 분포와 학습 목표를 바꾼다.

- Decision Period·행동 반복
- 이동·회전·Kick 물리
- Goal·공 Geometry
- Keeper shield, fallback과 Kick 안전 보정
- Reward 값·판정·cap
- 소유권 판정처럼 관측과 행동 문맥을 바꾸는 공통 규칙

두 범주 모두 한 팀에서만 수정하지 말고 Builder, 다섯 환경 Prefab, 테스트와 모든 문서를 함께 갱신한다. 두 번째 범주는 shape가 같으면 v2를 유지할 수 있지만 기존 Model을 재검증하고 보통 다시 학습해야 한다.

2026-08-24 변경은 Ray 개수·순서와 Vector/Action shape를 바꾸지 않아 정책 계약 버전은 v2, 전체 입력은 `379`로 유지한다. 그러나 Goal·공 물리 크기, Human 가속, Keeper shield, Kick 안전 보정과 Reward 판정이 달라졌으므로 이전 v2 ONNX가 생기더라도 새 동작 품질을 보장하지 않는다. Base와 전술별 정책은 현재 공통 환경에서 다시 학습·평가해야 한다.

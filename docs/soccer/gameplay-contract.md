# Soccer 경기·정책 계약

- 대상: Core, 학습, Rule, UI 담당자
- 상태: 공통 런타임 계약의 단일 기준
- 마지막 검토: 2026-08-10

이 문서의 값은 모든 활성 Soccer 환경에 공통이다. 한 팀의 실험을 위해 바꾸지 않는다. 구현 기준은 `AgentSoccer`, `SoccerEnvController`, `SoccerSettings`, `SoccerDefenderKeeperRules`와 `SoccerProjectBuilder`다.

## 경기 흐름

| 항목 | 계약 |
| --- | --- |
| 경기 시간 | `300초` |
| 팀 구성 | Blue 4명, Purple 4명 |
| 상태 | `Playing → GoalPause → Playing` 또는 `Finished` |
| Goal Pause | 득점 후 `3초`, 공과 선수 정지 뒤 Kickoff 위치로 Reset |
| 득점 | 득점 팀 `+1`, 실점 팀 `-1` |
| 종료 | 승리 팀 `+0.5`, 패배 팀 `-0.5`, 무승부 추가 보상 없음 |
| Episode | 팀별 `SimpleMultiAgentGroup`, 경기 Controller가 Reset 권한 보유 |

세부 shaping은 [보상 기준표](rewards.md)를 사용한다.

## 선수 역할

| 역할 | 인원 | 책임 |
| --- | ---: | --- |
| `DefenderKeeper` | 1 | 후방 보호와 Goal 복귀 |
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

Blue와 Purple은 공격 방향을 기준으로 X축을 대칭 정규화한다. Agent 등록 순서, 역할 순서, 정규화 분모를 바꾸면 정책 계약을 올리고 모든 ONNX를 다시 검증해야 한다.

전·후방 `RayPerceptionSensorComponent3D`를 함께 사용한다.

- 전방 Ray: `264`
- 후방 Ray: `72`
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

## 사람 조작

| 동작 | 키보드 | Xbox Controller |
| --- | --- | --- |
| 전진 | `W` | Right Trigger |
| 후진 | `S` | Left Trigger |
| 회전 | `A/D` | Left Stick X |
| 패스 | `E` | `A` |
| 슈팅 | `Space` | `B` |
| Human/AI 전환 | `H` | `Y` |

Human 모드는 Blue의 human-controllable 선수 한 명에만 적용된다. 나머지는 현재 Neural 또는 Rule Controller를 계속 실행한다.

사람 이동은 흔히 쓰이는 target planar velocity 방식이다.

- Gamepad Trigger와 Stick은 dead zone `0.15` 뒤 analog 값을 유지한다.
- 최대 평면 속도는 `9m/s`다.
- 가속은 `32m/s²`, 정지·역방향 감속은 `48m/s²`다.
- `Vector3.MoveTowards`로 FixedUpdate마다 목표 속도에 접근한다.
- Human일 때만 Rigidbody `Interpolate`, Training·AI 복귀 시 `None`을 사용한다.
- Human은 Decision Period `1`, Neural·Rule은 `5`이며 결정 사이 행동을 반복한다.

AI·Rule의 추론 비용과 기존 학습 의미를 유지하기 위해 analog target-velocity 경로는 Human에만 적용한다.

## 이동과 Kick 물리

| 항목 | 값 |
| --- | ---: |
| AI 이동 기본 배율 | `agentRunSpeed = 2` |
| 최대 평면 속도 | `9m/s` |
| 회전 속도 | `120°/s` |
| Controlled Kick | `1500` |
| Strong Kick | `4000` |
| 일반 접촉 Dribble Push | `120` |
| KickPlate 전진 시간 | `0.08초` |
| KickPlate 복귀 시간 | `0.5초` |

KickPlate가 복귀하기 전에는 새 Kick Branch를 mask한다. Rigidbody, Collider, Physics Material, 속도 clamp와 Kick 힘은 전술별 튜닝 대상이 아니다.

## DefenderKeeper 보호

공격 방향을 양수로 정규화한 깊이를 사용한다.

- `-4m`까지는 일반 이동을 허용한다.
- `-4m → 0m`에서 공격 방향 이동·속도를 선형으로 줄인다.
- 하프라인 `0m`에서는 공격 방향 힘과 기존 속도를 제거한다.
- 이미 하프라인을 넘었으면 즉시 Recovery 대상이다.
- 상대 소유 또는 마지막 상대 터치, 자기 진영으로 향하는 위협 공, 공보다 지나치게 앞선 상황에서도 Recovery한다.
- Recovery 목표는 시작 Goal 깊이이며 공의 Z 위치를 `±10m` 안에서 일부 추적한다.

Neural, fallback, Rule 모두 최종 `ConstrainMovement`와 `ConstrainVelocity`를 통과한다. Rule은 별도로 긴급 `RecoverGoal` 상태를 사용하지만 공통 shield를 우회할 수 없다.

## 공간과 포메이션

- 미드필더·스트라이커 고정 Anchor는 없다.
- Formation은 간격, 지원 선택지, 공수 균형과 DefenderKeeper 역할을 평가한다.
- 같은 팀 최단 거리 `3m` 미만은 소유권과 무관하게 별도 crowding 판정을 받는다.
- Rule은 보상으로 학습하지 않으므로 FSM에 allocation 없는 teammate separation 조향을 사용한다.

정확한 임계값, 배율과 상한은 [보상 기준표](rewards.md)에만 기록한다.

## HUD와 Camera

- 경기 시작은 AI 모드다.
- 점수, 남은 시간, AI/Human 상태, 양 팀 전술과 경기 누적 보상을 표시한다.
- 좌측 하단에는 테두리 없는 중앙 정렬 조작 표를 표시한다.
- 우측 상단의 중복 조작 안내는 사용하지 않는다.
- AI Overview Camera는 `(0,82,-78)`, FOV `50`이다.
- Goal이 화면 안 선수를 가릴 때만 전용 Renderer를 Alpha `0.25`로 전환한다. Collider와 Goal Tag는 유지한다.

## 계약 변경 조건

다음 변경은 기존 v2 ONNX 호환성을 깨뜨릴 수 있다.

- Observation 개수·순서·정규화
- Ray 구성
- Action Branch·값 의미
- 역할·Agent 등록 순서
- Decision Period·행동 반복
- 이동·회전·Kick 물리
- 소유권 판정처럼 관측과 행동 문맥을 바꾸는 공통 규칙

이 경우 한 팀에서만 수정하지 말고 계약 버전, Builder, 다섯 환경 Prefab, Trainer, 테스트, 모든 문서를 함께 갱신한다.

# Turtle 목표 찾기 실험 기술 메모

> 상태: 보관 — 신규 개발 대상 아님  
> 보관 위치: `Assets/_Legacy/Turtle`  
> 원문에서 현행에 필요한 Turtle 부분만 추려 보존함

이 문서는 기존 Turtle 강화학습 실험을 실행·해석할 때 필요한 최소 정보만 남긴다. Soccer 설계의 현재 기준으로 사용하지 않는다.

## Asset

- Scene: `Scenes/Scene1 Basic.unity`, `Scenes/Scene2 Ray.unity`
- Prefab: `Prefabs/Environment - 1 Basic.prefab`, `Environment - 2 Ray.prefab`
- Agent: `Scripts/TurtleAgent.cs`, `Scripts/TurtleAgentRay.cs`
- UI: `Scripts/GUI_TurtleAgent.cs`
- Model: `Models` 아래 ONNX 3개
- Trainer: `Training~/Config/Turtle.yaml`
- 과거 결과: `Training~/Results`

위 상대 경로의 Root는 `Assets/_Legacy/Turtle`이다. `Training~`은 Unity import에서 제외되지만 `mlagents-learn`에는 직접 경로를 전달할 수 있다.

## 정책

Basic Vector Observation은 5개다.

1. Goal local X/Z
2. Turtle local X/Z
3. Y 회전을 `-1~1`로 정규화한 값

Ray 환경의 직접 Vector는 위치 X/Z와 회전 3개이며 `RayPerceptionSensorComponent3D`가 `Wall`, `Goal`을 감지한다.

두 환경 모두 크기 4의 단일 Discrete Branch를 사용한다.

| 값 | 행동 |
| ---: | --- |
| `0` | 정지 |
| `1` | 전진 |
| `2` | 왼쪽 회전 |
| `3` | 오른쪽 회전 |

Decision Period는 `5`이며 결정 사이 행동을 반복한다. 키보드 Heuristic은 방향키를 사용한다.

## Reward와 Episode

- Step마다 `-2 / MaxStep`
- Goal 도달 `+1` 후 Episode 종료
- Wall 충돌 시작 `-0.05`
- Wall 접촉 유지 `-0.01 × fixedDeltaTime`
- Reset 시 Turtle은 중앙, Goal은 임의 각도와 `1~2.5` 거리
- Prefab `MaxStep = 5000`

두 Prefab의 기존 Model과 BehaviorType은 보존한다. 다시 학습할 경우 원본을 직접 바꾸지 말고 별도 실험 사본에서 학습용 Behavior 설정을 확인한다.

## 과거 PPO 기준

| 설정 | 값 |
| --- | --- |
| Behavior | `Turtle` |
| Trainer | PPO |
| Batch / Buffer | `512 / 10240` |
| Learning Rate | `0.0003`, linear |
| Hidden Units / Layers | `128 / 2` |
| Extrinsic Gamma | `0.99` |
| Max Steps | `1,000,000` |
| Time Horizon | `64` |
| Summary Frequency | `50,000` |

이는 역사적 실험값이며 Soccer Trainer에 복사하지 않는다.

## 보존 원칙

- 관련 `.meta`, Material, Prefab과 Model을 함께 보존한다.
- 신규 Soccer 코드에서 Turtle Asset을 참조하지 않는다.
- 구조 이동으로 깨진 GUID·Scene 참조만 최소 수정한다.
- 재학습이나 삭제는 별도 사용자 결정이 필요하다.

# Escape 학습 성능 예산과 병렬 환경 계획

> 보관일: 2026-08-09  
> 보관 이유: 사용자 결정에 따른 Escape 개발 종료  
> 현행 개발 문서: [Soccer 개요](../../soccer/overview.md), [학습 운용](../../soccer/training/overview.md)  
> 최종 상태: 성능 계획은 역사 자료로만 보존하며 Benchmark와 학습을 진행하지 않음
> 현재 위치: Escape 구현과 Trainer 설정은 2026-08-10 이후 `Assets/_Legacy/Escape`에 보관

마지막 갱신: 2026-08-09  
상태: `보관 — 개발 종료`

이 문서는 Escape의 Ray 관측과 병렬 Arena 수에 대한 당시 성능 계획이다. `Builds/Escape/EscapeTraining.exe`와 Trainer YAML까지 준비했지만, 개발 종료 결정으로 실제 처리량 Benchmark와 장기 학습은 취소됐다. 아래 권장값은 현행 작업 지시가 아니다.

## 1. 확인된 학습 환경

### 프로젝트와 Python

| 항목 | 확인 값 |
| --- | --- |
| 주요 Unity 프로젝트 | `C:\GitHub\Machine-Learning` |
| Unity | `6000.3.16f1` |
| Unity ML-Agents Package | `4.0.3` |
| Conda 환경 | `C:\Users\USER\miniconda3\envs\mlagents` |
| Python | `3.10.12` |
| `mlagents` / `mlagents-envs` | `1.2.0.dev0` / `1.2.0.dev0` |
| PyTorch | `2.2.2+cu121` |
| CUDA 확인 | `torch.cuda.is_available() == true` |

당시 Root `results`에 있던 Turtle 학습 기록은 현재 `Assets/_Legacy/Turtle/Training~/Results`에 보관한다. 당시 Conda 환경의 준비 상태는 현재 환경을 보장하지 않으므로 다시 실행할 때 별도로 확인한다.

### 현재 학습 노트북

| 항목 | 확인 값 |
| --- | --- |
| 모델 | HP Victus Gaming Laptop 16-r1xxx |
| CPU | Intel Core i7-14700HX, 28 logical processors |
| RAM | 32,472MB, 약 32GB |
| GPU | NVIDIA GeForce RTX 4060 Laptop GPU |
| VRAM | 8GB |
| Driver / CUDA Runtime | NVIDIA Driver 592.82 / PyTorch CUDA 12.1 |

## 2. 16개 Arena의 관측량 계산

초기 설계는 Arena당 Player 1명과 Enemy 3명, Agent당 Ray 25개와 Vector Observation 7개를 사용한다.

| 계산 항목 | 값 |
| --- | ---: |
| Arena 수 | 16 |
| 총 Agent 수 | 64 |
| Agent당 Ray 수 | `1 + 2×12 = 25` |
| Agent당 Ray 관측 | `25 × (6 tags + 2) = 200 floats` |
| Agent당 전체 관측 | `200 + 7 = 207 floats` |
| Fixed Update | 50Hz |
| Decision Period | 5 |
| Agent당 Decision | 시뮬레이션 1초당 10회 |
| 전체 Raycast | 시뮬레이션 1초당 `64×25×10 = 16,000회` |
| 원시 Float 관측량 | 시뮬레이션 1초당 약 0.505MiB |

원시 관측 데이터량과 207차원 MLP 입력은 매우 작다. CNN Camera Observation을 사용하지 않으므로 RTX 4060의 VRAM 8GB에는 부담이 낮다. Enemy MA-POCA가 PPO보다 더 많은 계산을 사용하더라도 Agent 64개와 이 관측 크기는 GPU 메모리 측면에서 무리한 규모가 아니다.

## 3. 실제 병목 위치

Ray Perception Sensor의 Raycast는 NVIDIA GPU가 아니라 Unity Physics와 CPU에서 처리한다. RTX 4060은 PyTorch 정책 학습을 가속하지만 Raycast 자체를 빠르게 하지 않는다.

실시간 시뮬레이션에서는 초당 16,000회지만 학습 가속 배율에 따라 벽시계 기준 작업량이 증가한다.

| Time Scale | 목표 Raycast/벽시계 초 |
| ---: | ---: |
| 1 | 16,000 |
| 5 | 80,000 |
| 10 | 160,000 |
| 20 | 320,000 |

건물은 단순 BoxCollider 25개, 캐릭터는 Capsule/Rigidbody 4개이므로 Arena 하나의 Physics 복잡도는 낮다. i7-14700HX에서는 16 Arena가 타당한 첫 목표지만, 노트북 CPU의 전력 제한과 발열 때문에 Time Scale 20이 항상 Time Scale 10보다 높은 처리량을 보장하지는 않는다.

## 4. 16개 병렬 환경에 대한 결론

### 권장 구성

- ML-Agents 4.0.3의 `TrainingAreaReplicator`에 Escape Environment 프리팹 하나를 Base Area로 연결한다.
- **한 Unity Build 프로세스 안에 Arena 16개**를 `--num-areas=16`으로 복제한다.
- 최초 Benchmark에서는 `mlagents-learn --num-areas=16 --num-envs=1`을 사용한다.
- `--num-envs=16`은 Unity 프로세스 16개를 뜻하므로 Arena 16개와 동시에 사용하면 총 256 Arena가 되어 노트북에 지나치게 무겁다.
- Training Mode에서는 Camera, UIDocument, Audio, 그림자와 불필요한 Renderer를 비활성화하고 `--no-graphics`를 사용한다.
- Ray Sensor의 `Use Batched Raycasts`를 켠다.
- Decision Period는 5, Rays Per Direction은 12로 먼저 구현한다.
- Time Scale은 5에서 시작해 10, 20 순서로 올리며 총 Environment Steps/second가 실제로 증가하는지 측정한다.

현재 설치된 `mlagents-learn --help`에서 `--num-areas`와 `--num-envs`를 모두 확인했다. `--num-areas`는 Unity 프로세스 하나 안의 Training Area 수이고, `--num-envs`는 동시에 실행할 Unity 프로세스 수다.

현재 노트북의 CPU 28 logical processors, RAM 32GB, RTX 4060 8GB 조합에서는 위 방식의 Arena 16개가 합리적인 초기값이다. 다만 “16개가 최적”인지는 구현 전 계산만으로 확정할 수 없고, 8개와 16개의 실제 처리량 비교로 결정해야 한다.

### 피해야 할 구성

- Unity Editor에서 화면을 렌더링한 채 장시간 고속 학습
- Arena 16개가 들어 있는 Build를 다시 `--num-envs=16`으로 실행
- Arena마다 Camera, UI, 실시간 그림자와 개별 광원을 켜 두기
- 동일한 정적 Building에 여러 중복 Collider를 붙이기
- 처리량을 확인하지 않고 Time Scale만 크게 올리기

## 5. 낮은 사양 PC의 시작값

아래 값은 절대 최소 사양이 아니라 Benchmark 시작점이다.

| 대략적인 PC | Arena 시작값 | 권장 사항 |
| --- | ---: | --- |
| 8개 이상 빠른 CPU Core, RAM 16GB 이상, CUDA GPU | 8 | 8→16 순서로 측정 |
| 4~6 CPU Core, RAM 16GB | 4 | Time Scale 5부터 측정 |
| 4 CPU Core 또는 RAM 8GB | 1~2 | Ray 수 축소와 낮은 Time Scale 우선 |
| CUDA GPU 없음 | 1~4 | Unity 환경보다 PyTorch CPU 학습이 먼저 병목일 수 있음 |

낮은 사양에서도 게임을 직접 플레이하거나 ONNX 추론만 하는 것은 학습보다 훨씬 가볍다. Runtime 게임 씬은 Arena 한 개와 Agent 네 개만 실행하므로 16 Arena 학습 부하를 그대로 요구하지 않는다.

## 6. 부하가 높을 때 조정 순서

학습 의미를 덜 훼손하는 순서로 조정한다.

1. Arena 수를 16→8→4로 낮춘다.
2. Time Scale을 처리량이 가장 높은 지점으로 낮춘다.
3. Rays Per Direction을 12→8로 낮춘다.
   - 총 Ray 수: 25→17
   - Ray 관측: 200→136 floats
   - Raycast 비용: 약 32% 감소
4. 필요하면 Decision Period를 5→10으로 늘린다.
   - Raycast와 Decision 빈도가 절반이 되지만 조작 반응은 10Hz→5Hz로 낮아지므로 마지막 수단으로 비교한다.
5. Arena를 한 프로세스에 더 넣는 방식과 소수의 Unity 프로세스로 나누는 방식을 비교한다.

요구된 여섯 Detectable Tag는 줄이지 않는다. 먼저 Ray 해상도, Arena 수와 Time Scale을 조정한다.

## 7. 구현 시 성능 요구사항

- `TrainingAreaReplicator`로 Base Area를 복제하고 CLI `--num-areas`로 Arena 수를 `1, 4, 8, 16`에서 선택한다.
- 각 Arena는 고유 Environment Controller와 Enemy MultiAgentGroup을 가진다.
- Replicator Separation은 최소 120m로 두어 서로 다른 Arena의 Collider와 Ray가 상호작용하지 않게 한다.
- Training Mode에서 렌더링/UI/Camera를 일괄 비활성화한다.
- 모든 Building은 가능한 한 하나의 BoxCollider를 사용한다.
- Ray Sensor는 Batched Raycast를 사용한다.
- 성능 비교 때 Behavior, Seed, Time Scale 외 설정을 동일하게 유지한다.

## 8. 구현 후 Benchmark 절차

1. Headless 학습 Build를 만든다.
2. `--num-areas=1`, `4`, `8`, `16`을 같은 Trainer 설정과 Seed로 각각 3~5분 실행한다.
3. 각 Arena 수에서 Time Scale `5, 10, 20`을 비교한다.
4. 다음 값을 기록한다.
   - Environment Steps/second와 Agent Decisions/second
   - Unity 프로세스 CPU 사용률과 RAM
   - GPU 사용률과 VRAM
   - `timers.json`의 Environment Step, Policy와 Trainer Update 시간
   - Episode 완료 수와 통신 대기 여부
5. 8→16으로 늘렸을 때 총 처리량 증가가 작거나 RAM/발열이 크게 증가하면 8개를 기본값으로 선택한다.
6. 최적 Arena 수를 정한 뒤 Player PPO와 Enemy MA-POCA를 각각 다시 측정한다.

프레임률보다 단위 벽시계 시간당 수집되는 유효 Experience 수를 선택 기준으로 사용한다.

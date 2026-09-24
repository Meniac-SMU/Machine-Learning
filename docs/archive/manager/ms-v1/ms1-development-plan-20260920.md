> 보관일: 2026-09-24. 작성 당시의 계획·판정·승인 이력이며 현재 실행 지시가 아닙니다. 현행 기준: [현재 상태](../../../soccer/current-status.md).

# MS1 혼합 판단 개발·학습 계획 — 2026-09-20

2026-09-23 사용자 지시: 5시간 및 주간 잔여량에 따른 사전 중단·재개 제한은 모두 폐지했다. 과거 사용량/중단 기록은 당시 사실이며 현재 실행 조건이 아니다. 서비스 자체 제한과 별개로 RAM/NaN/crash/데이터 무결성 및 정상 저장 규칙은 유지한다.

MS0 사용자 확인 뒤 시작하는 MS1의 구현과 실행 단일 기준이다. 상위 범위는 [MS 계획](ms-plan.md), 합격선은 [MS 평가 계약](ms-validation.md), MS0 기반은 [MS0 완료 보고서](ms0-completion-20260919.md)를 따른다. 이 문서는 계획을 고정하며 아직 MS1 코드·Player·Run을 완료했다는 뜻이 아니다.

## 목표와 완료 정의

MS1은 선수의 이동이나 축구 기술을 다시 학습하지 않는다. 기존 코드가 이동·회전·공 추격·드리블·패스 대상과 수신·슛 조준·킥·골키퍼·간격을 실행하고, PPO 감독 하나가 133개 관측으로 여섯 팀 명령의 선택과 전환 시점을 학습한다.

하나의 정책을 공격·수비·공 전환 상황에 함께 노출한다. 공격학교와 수비학교를 별도 학습 단계로 나누지 않는다. R0-Easy를 상대로 30초 상황 episode를 학습하고, 고정 평가에서 공격과 수비 양쪽 최소 기준을 통과하면 즉시 MS2로 넘어간다.

## 고정 계약

- BehaviorName `MNG_Manager`, 관측 133개, 이산 branch 하나와 명령 6개를 유지한다.
- 첫 MS1 정책은 fresh PPO다. MS0 smoke, 과거 M1 r001~r003, 과거 M1 v6 PT/ONNX를 initialize/resume하지 않는다.
- Base RewardProfile의 실제 사건 보상을 사용한다. 2026-09-20 공통 패스 개선과 최종 polish에서 실제 완료 패스만 `+0.04`에서 `+0.06`으로 소폭 높이며, 명령 선택 자체, 시간 생존, 특정 명령 사용 횟수에는 보상하지 않는다.
- 기본 상대는 R0-Easy `이동 0.35× / 판단 1.5초`다. R0-Rescue `0.20× / 2.0초`는 첫 20k 안의 임시 구조 구간으로만 허용한다. 최종 평가는 항상 Easy다.
- episode는 30초 또는 첫 득점에서 끝난다. 30초 절단은 interruption이며 승리·패배 reward를 추가하지 않는다.
- 기본 학습 목표는 aggregate manager step 100k 또는 학습 실행 60분 중 먼저 도달한 값이다. 20k마다 진단하고 gate를 일찍 통과하면 즉시 종료한다.
- 100k에서 최근 두 진단의 보상, 고정 평가, 실제 사건 지표가 함께 계속 상승하고 보상 악용이 없으면 한 checkpoint 구간인 20k만 추가할 수 있다. 절대 상한은 120k다. reward만 상승하거나 gate를 이미 통과했거나 악용 징후가 있으면 연장하지 않는다.
- 사용량 비율에 따른 실행 제한은 폐지했다(2026-09-23). 오류·중단 시 증거와 재개점을 보존한다.

## 최소 구현 구조

| 단위 | 구현 방향 |
| --- | --- |
| 혼합 시나리오 | `MNG_MS1Scenario`와 결정적 scheduler를 추가한다. 기존 `MNG_AttackScenarioGenerator`와 `MNG_DefenseScenarioGenerator`의 배치·좌우 mirror·spawn jitter를 재사용한다. |
| episode 제어 | `MNG_MS1Controller`가 50:50 상황 선택, 30초 interruption, 첫 득점 종료, worker seed, R0 Profile, worker별 결과 기록을 소유한다. |
| 평가 | `MNG_MS1EvaluationController`가 공격/수비 성공, 회수 유지, 패스 subset, 명령/mask/실제 사건을 기록한다. 기존 M1의 ONNX import·InferenceOnly build·SHA 로직만 재사용한다. |
| 자산 | `MNG_MSBuilder`를 확장해 MS1 Train, 평가 Red, 평가 Navy Scene과 MS1 Windows Player를 생성·검증한다. 과거 M Scene은 수정하지 않는다. |
| 학습 설정 | `Training/MNG_MS1.yaml` 하나를 새로 만든다. 공격·수비별 YAML과 별도 정책은 만들지 않는다. |
| 프로토콜 | `Evaluation/MNG_MS1_Protocol_v1.json`에 seed, episode 수, 성공 판정, gate와 실제 사건 필드를 고정한다. |
| 실행 도구 | `MNG_MS_Train.ps1`에 MS1을 추가하고 `NumEnvs=16`만 허용한다. 별도 평가 스크립트는 기존 해시 검증 패턴을 재사용한다. |

신규 파일은 `MNG_MS1*` 또는 `MNG_MS*` 이름을 사용한다. 기존 `MNG_CurriculumController`의 코드 일부를 재사용할 수 있지만 기존 M1 stage를 그대로 실행하거나 옛 gate를 MS1에 연결하지 않는다.

## 상황 분포

각 worker는 같은 비율을 장기적으로 보장하는 결정적 순서를 사용하되, worker와 episode마다 위치·좌우 mirror·세부 배치 seed가 달라야 한다.

| 상황군 | 전체 비율 | 내부 구성 | 시작 상태 |
| --- | ---: | --- | --- |
| 공격 | 50% | Carry / Pass / Shot을 균형 순환 | 정책 팀이 실제 킥 플레이트 소유로 시작 |
| 수비·전환 | 50% | 중앙 Direct와 측면 Wide를 균형 순환 | 절반은 R0 소유, 절반은 중립 공 |

- 공격에서는 전진 공간, 패스 통로, 슛 가능 위치가 모두 나타나야 한다. Pass fixture는 동료 수신자와 실제 통로가 있어야 한다.
- 전진 통로가 막힌 상황은 패스를 먼저 검토하도록 구성한다. R0는 공통 차단 판정으로 패스를 우선하고, PPO는 같은 상황에서 실제 완료 패스 보상을 통해 판단을 학습한다.
- 공통 패스 목표점은 수신 선수의 예측 위치에서 상대 골문 방향으로 lead하고, 좌우 중 경기장 중앙 쪽으로 1.5m 치우친 지점이다. 규칙형과 PPO 모두 같은 Planner/Executor를 사용한다.
- 수비에서는 상대 소유를 먼저 확인한 뒤 정책 팀이 회수하여 0.5초 이상 유지해야 회수 성공이다. 중립 시작을 처음부터 자기 소유로 잘못 판정하지 않는다.
- 좌우 mirror는 같은 seed쌍으로 만들고, 최종 평가는 정책 팀 Red/Navy를 교환한다.
- 시작 위치는 경기장·골·벽과 겹치지 않고 즉시 접촉 폭발이 생기지 않는 범위로 고정한다.
- worker 로그에는 situation group, kind, possession mode, seed, policy team, 시작 좌표 hash를 남긴다.

## 보상과 종료

| 사건 | MS1 처리 |
| --- | --- |
| 득점 / 실점 | 기존 `+1 / -1`, 첫 실제 goal에서 episode 종료 |
| 유효 슛 | 기존 `+0.02` |
| 완료 패스 | `+0.06` (기존 `+0.04`, 변경 `+0.02`) |
| 5m 전진 | 기존 `+0.01` |
| 회수 / 빠른 회수 | 기존 `+0.03 / +0.02` |
| 밀집 | 기존 `-0.002` |
| 30초 timeout | reward 추가 없이 interruption |
| 명령 선택 | reward 없음 |

보상 이벤트 ID와 cap은 기존 `MNG_EventLedger`와 `MNG_RewardEngine`을 사용한다. 평가 성공 판정은 학습 보상과 별도로 기록한다. 수비 회수 성공을 측정하더라도 episode를 즉시 끝내지 않고 첫 득점 또는 30초까지 전환 판단을 계속 경험하게 한다.

## PPO 초기값

```yaml
trainer_type: ppo
batch_size: 256
buffer_size: 4096
learning_rate: 0.0003
beta: 0.005
epsilon: 0.2
lambd: 0.95
num_epoch: 3
hidden_units: 128
num_layers: 2
normalize: false
gamma: 0.995
time_horizon: 128
summary_freq: 1000
checkpoint_interval: 20000
max_steps: 120000 # normal target 100k; conditional extension ceiling
```

`max_steps`는 16개 환경이 함께 만드는 aggregate manager step이다. worker 수를 곱하지 않는다. 100k는 기본 목표이며 120k는 조건부 절대 상한이다. checkpoint마다 PT·ONNX·optimizer·trainer log·worker log·config hash를 보존한다.

## 16개 병렬 환경

MS1 학습은 `--num-envs 16`으로 고정한다. Windows Player 프로세스 16개가 각각 경기장 하나를 실행하므로 실제 독립 환경은 16개다. 한 Scene 안에 경기장 16개를 복제하지 않는다.

1. 실행 전에 연속 16개 port가 비어 있는지 검사한다. 선택한 base port와 전체 범위를 Run manifest에 남긴다.
2. trainer seed는 191001에서 시작하고 worker identity와 episode index를 섞어 reset seed를 파생한다. 16개 worker가 같은 시작 배치를 반복하면 학습을 시작하지 않는다.
3. 각 worker는 자기 JSONL만 쓴다. 결과 병합은 모든 프로세스가 종료된 뒤 수행한다.
4. time scale 10, `fixedDeltaTime=0.02`, `--no-graphics`를 사용한다. 물리·센서·공 접촉·판정은 끄지 않는다.
5. 시작 후 16개 process, 16개 worker-start, 서로 다른 port/process/seed, episode 완료, 첫 PPO update를 확인한다.
6. RAM 사용 85% 이상, 여유 RAM 4GiB 미만, worker crash, port 충돌, NaN, 장시간 timeout이 발생하면 정상 checkpoint 후 중단한다. 사용자가 16개를 고정했으므로 8개나 2개로 자동 축소하지 않는다.
7. trainer/Player를 종료할 때 해당 Run의 프로세스만 정상 종료한다. 광범위한 process kill을 사용하지 않는다.

## 개발 순서

### 1. 계약과 기준 동결

- MS0 사용자 합격, 현재 source/Scene/Profile/config SHA, dirty status를 새 MS1 source snapshot에 보존한다.
- MS1 protocol v1과 학습 YAML을 먼저 작성하고 설치 trainer로 파싱한다.
- 과거 M1 v6의 골·패스 횟수 gate가 MS1에 연결되지 않는지 정적 검사한다.

### 2. 혼합 reset과 episode 구현

- 50:50 scheduler, 공격 3종, 수비 소유/중립 반반, 좌우 mirror와 worker seed를 구현한다.
- R0-Easy를 기본 상대에 연결하고 Rescue는 명시적 실행 인자로만 선택하게 한다.
- 첫 goal 종료와 30초 interruption, 한 팀 한 명령 writer를 구현한다.

### 3. telemetry와 고정 평가 구현

- raw/accepted command, mask availability, 실제 pass/shot/advance/recovery/goal, override, episode 결과를 worker별로 기록한다.
- 정책 팀 Red/Navy 평가 Scene을 생성하고 같은 ONNX SHA로 2진영을 평가한다.
- 무작위 유효명령 정책과 ONNX 정책이 같은 build·seed·상대 조건을 사용하게 한다.

### 4. 테스트·Builder·Player

- EditMode에서 분포, 결정성, 서로 다른 worker seed, timeout interruption, 성공 판정을 검사한다.
- PlayMode에서 공격 소유, R0 소유, 중립 공, Red/Navy 정책 Scene, 단일 writer, goal 1회 종료를 검사한다.
- Unity 컴파일과 MS Builder Validate 뒤 MS1 Windows Player를 빌드한다.
- source/exe/level/config/protocol SHA가 모두 일치해야 학습을 허용한다.

### 5. 학습 전 baseline

- 진단 seed에서 uniform-valid-command를 공격·수비·양 진영으로 평가한다. fresh PPO의 첫 정상 export는 별도의 초기 참고값으로 같은 진단 seed에서 평가하되, 이를 진정한 zero-step 모델이라고 표시하지 않는다.
- 실제 event가 거의 발생하지 않아 Easy가 학습 경험을 제공하지 못하는 경우에만 첫 최대 20k를 Rescue로 시작한다.
- Rescue를 사용하더라도 20k 이후 같은 정책을 Easy로 전환하고, 최종 gate에는 Rescue 결과를 사용하지 않는다.

### 6. 16환경 연결 smoke와 본학습

- 새 Run `MNG_MS1-YYYYMMDD-r001`을 fresh로 시작한다.
- 16개 worker 연결과 첫 update를 실행 중 확인한 뒤 같은 공식 Run을 계속 20k까지 진행한다. 연결 확인만을 위한 별도 학습 Run을 만들지 않으며 모든 step을 100k 예산에 포함한다.
- 20k, 40k, 60k, 80k, 100k checkpoint에서 진단한다. gate를 통과하면 남은 step을 채우지 않고 후보를 동결한다. 80k와 100k에서 보상·고정 평가·실제 사건이 모두 개선 중이고 악용이 없을 때만 120k까지 한 번 연장한다.

### 7. 후보 평가와 완료

- checkpoint 진단은 공격·수비 각각 10 seed × 양 진영으로 실행한다.
- 선정 후보 하나만 최종 holdout 120 episode로 평가한다. 첫 공격 40·수비 40은 주 gate에 쓰고, 전체 60개 공격 중 정확한 Pass 유형 20개는 패스 보조 gate에 쓴다.
- 같은 최종 seed로 random baseline을 한 번 평가하고 학습 정책과 비교한다.
- gate 통과 시 PT·ONNX·optimizer·build/config/source/protocol/result hash를 보존하고 MS1 완료 보고서를 작성한다.

## 합격 기준

- 공격 40 episode 중 실제 득점 또는 유효 슛 성공 24개 이상.
- 수비 40 episode 중 실점 없이 실제 회수 후 0.5초 유지 성공 20개 이상.
- 공격과 수비 각각 같은 seed의 random-valid-command baseline보다 10%p 이상 개선.
- 미리 고정한 Pass 유형 20 episode에서 실제 완료 패스 3개 이상. 패스는 MS1의 보조 기준이며 공격·수비 주 gate보다 우선하지 않는다.
- 양 진영 결과가 존재하고 명령이 mask를 통과해 Planner/Executor에 전달된 증거가 있어야 한다.
- crash, NaN, 이중 writer, Rule/PPO 제어 충돌, 가짜/중복 보상 사건, worker 로그 덮어쓰기가 없어야 한다.

여섯 명령의 균등 사용, 높은 득점 총량, 과거 M1 v6의 82골·패스 플레이트 12회·완료 패스 8회는 MS1 합격 조건이 아니다.

## 정체와 중단 처리

- 두 연속 20k 진단에서 공격·수비 중 낮은 성공률이 개선되지 않으면 checkpoint를 저장하고 원인을 분석한다.
- 같은 실패가 두 번 반복되면 새 r번호의 보상 미세조정을 중단하고 관측·mask·명령 영향·R0 강도·기술 실행·보상 사건을 점검한다.
- 100k 또는 60분에서 gate 미달이면 원칙적으로 MS1 미완료다. 다만 프로젝트 공통 연장 규칙을 만족하면 120k까지 한 번 연장할 수 있다. 합격선을 낮추거나 120k를 넘기지 않는다.
- 사용량 비율에 따른 실행 제한은 폐지했다(2026-09-23). 오류·중단 시 증거와 재개점을 보존한다.

## MS1에서 하지 않는 작업

선수별 RL, 관측 133/명령 6 재설계, 새로운 축구 기술, R0 전술 개선, 별도 공격·수비 정책, self-play, Full R0 최종 대전, Human 혼합 학습, UI 변경은 MS1 범위에 포함하지 않는다. Self-play는 MS3, Full R0 성능 gate는 MS2에서 수행한다.

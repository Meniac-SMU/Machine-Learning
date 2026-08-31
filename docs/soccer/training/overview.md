# Soccer 학습·Model 운용

- 대상: Base·Attack·Defense·Press 학습 및 Model 통합 담당자
- 상태: 학습 Pipeline의 단일 기준
- 마지막 검토: 2026-08-31

Attack, Defense, Press는 같은 정책 계약을 서로 다른 shaping으로 학습하는 작업공간이다. 최종 게임에서는 세 결과를 한 팀의 감독 전술로 사용한다. Rule은 학습 Pipeline에 포함하지 않는다.

## 작업공간과 Behavior

모든 경로의 기준은 `Assets/_Soccer`다.

| Type | Scene | Trainer YAML | BehaviorName | 학습 구성 |
| --- | --- | --- | --- | --- |
| Base smoke | `Core/Scenes/Stadium4v4_Base.unity` | `Core/Training/base_4v4_v2_temp.yaml` | `Soccer4v4_Base` | 짧은 연결 확인 |
| Base fallback | `Core/Scenes/Stadium4v4_Base_FallbackTraining.unity` | `Training/soccer_4v4_poca_base_fallback.yaml` | `Soccer4v4_Base` | Red 학습, Navy 규칙 fallback 강제 |
| Base Self-Play | `Core/Scenes/Stadium4v4_Base.unity` | `Training/soccer_4v4_poca.yaml` | `Soccer4v4_Base` | Red·Navy 양 팀 학습 |
| Attack | `Teams/Attack_KMW/Scenes/Stadium4v4_Attack.unity` | `Teams/Attack_KMW/Training/attack_poca.yaml` | `Soccer4v4_Attack` | Red 학습, Navy Base |
| Defense | `Teams/Defense_PJH/Scenes/Stadium4v4_Defense.unity` | `Teams/Defense_PJH/Training/defense_poca.yaml` | `Soccer4v4_Defense` | Red 학습, Navy Base |
| Press | `Teams/Press_KMG/Scenes/Stadium4v4_Press.unity` | `Teams/Press_KMG/Training/press_poca.yaml` | `Soccer4v4_Press` | Red 학습, Navy Base |

공유 Windows Build는 위 다섯 학습 Profile과 `rule-eval`을 한 실행 파일에 포함한다. Rule은 Scene 선택과 평가만 가능하며 Trainer에는 연결하지 않는다. Base fallback Scene의 Navy는 `Force Navy Fallback`이 켜져 있으므로 나중에 Base ONNX가 등록되어도 규칙 fallback을 계속 사용한다.

담당자 접미사는 경로에만 사용한다. YAML의 `behaviors` Key, TeamDefinition과 Model Type에는 `_KMW`, `_PJH`, `_KMG`를 붙이지 않는다.

## 필수 선행 조건

1. [경기 계약](../gameplay-contract.md)의 Observation·Action·물리가 모든 환경에서 같다.
2. `Tools/Soccer/Validate active 4v4 Stadiums`가 통과한다.
3. 수정하려는 Reward 값이 [보상 기준표](../rewards.md)와 Profile에서 일치한다.
4. 새 경로의 Unity compile과 관련 Test가 통과한다.
5. Base 상대 Model과 Seed를 기록한다.

현재 활성 `Models` 폴더에는 v2 ONNX가 없다. `Assets/_Soccer/SourceModels/SoccerTwos.onnx`는 이전 계약이므로 사용하지 않는다. Attack·Defense·Press 장기 학습 전에 승인된 Base v2 Model을 `BaseTeamDefinition.asset`에 등록해야 한다. 그렇지 않으면 Navy가 heuristic fallback으로 움직일 수 있다.

`Train-Soccer.ps1`은 이 상태에서 Attack·Defense·Press 시작을 기본 차단한다. Base Model을 등록한 뒤 공유 Windows Build를 다시 만들어 manifest의 `navyModelConfiguredInBuild`가 `true`가 되어야 한다. fallback 상대가 목적임을 별도 실험으로 기록한 경우에만 `-AllowFallbackOpponent`를 명시한다.

현재 Stadium의 약 `124 × 84.655m` 필드, 데모 Goal, 공 scale `0.012705`, Controlled/Strong `2000 / 5000`, Keeper·Kick safety는 입력 `379`와 Action `[3,3,3,3]` shape를 유지하지만 환경 동역학과 학습 목표를 바꾼다. 따라서 새 Base smoke부터 Stadium 환경에서 수행하고, 이전 경기장 동역학으로 만든 Model을 현재 기준 성능으로 간주하지 않는다.

## 권장 학습 순서

```text
Base smoke
  → Base Self-Play
  → Base v2 Model 승인·등록
  → Attack / Defense / Press 개별 학습
  → 고정 조건 평가
  → Tensor·런타임 교체 검증
  → 감독 전술 목록 등록
```

### Base smoke

짧은 설정으로 다음만 확인한다.

- Trainer가 `Soccer4v4_Base`를 발견한다.
- Red·Navy Agent 수와 Team ID가 맞다.
- Step이 증가하고 NaN·communicator 오류가 없다.
- Episode 종료와 새 경기 Reset이 정상이다.

Smoke 결과를 성능 Model로 사용하지 않는다.

### Base Self-Play

공통 Base Profile을 사용해 대표 상대를 만든다. 학습 길이보다 먼저 Model이 기본 경기 루프를 수행하는지, 보상 상한에 즉시 포화되지 않는지 확인한다. 승인 Model은 기존 파일을 덮어쓰지 않고 Version을 올린다.

### 전술 학습

Attack·Defense·Press는 같은 Base Model, Seed 집합, 경기 시간과 평가 절차를 사용한다. 한 Run에서 Reward와 Network를 동시에 크게 바꾸지 않는다. 전술별 허용 범위는 [학습형 팀 가이드](learning-teams.md)를 따른다.

## 공유 Windows 학습 Build

Unity Editor와 Hub를 닫은 상태에서 프로젝트 Root의 PowerShell에서 실행한다.

```powershell
cd C:\GitHub\Machine-Learning
.\Tools\Build-SoccerTraining.ps1
```

결과는 `Builds/SoccerTraining/SoccerTraining.exe`다. 함께 생성되는 `training-profiles.json`에는 Scene, BehaviorName, YAML hash, 기본 Port, Self-Play와 fallback 설정이 기록된다. 팀원은 별도 Scene Build를 만들지 않고 이 실행 파일과 프로젝트의 YAML을 공유한다.

실행 파일을 직접 열면 Profile 선택창이 표시된다. 자동 학습에서는 선택창을 사용하지 않고 `--training-profile`을 전달한다. 지원 Profile은 다음과 같다.

| Profile | 목적 | 기본 Port |
| --- | --- | --- |
| `base-fallback` | Red Base를 Navy 규칙 fallback과 학습 | `5055` |
| `base-selfplay` | Base 양 팀 Self-Play | `5005` |
| `attack` | Attack Red 학습 | `5105` |
| `defense` | Defense Red 학습 | `5205` |
| `press` | Press Red 학습 | `5305` |
| `rule-eval` | Rule 수동·자동 평가만 수행 | `5405` |

## 8개 또는 16개 병렬 학습

기존 `mlagents` Conda 환경을 활성화한 뒤 아래 형식으로 실행한다. 설치 명령은 필요 없다.

```powershell
cd C:\GitHub\Machine-Learning
conda activate mlagents
.\Tools\Train-Soccer.ps1 -Profile base-fallback -RunId Base-20260831-r003 -NumEnvs 8
```

16개로 바꾸려면 `-NumEnvs 16`만 사용한다. 실제 Trainer를 시작하지 않고 Build·YAML hash·Run ID·Port 범위만 검사하려면 끝에 `-ValidateOnly`를 붙인다.

```powershell
.\Tools\Train-Soccer.ps1 -Profile base-selfplay -RunId Base-20260831-r004 -NumEnvs 16
.\Tools\Train-Soccer.ps1 -Profile attack -RunId Attack-20260831-r001 -NumEnvs 8
.\Tools\Train-Soccer.ps1 -Profile defense -RunId Defense-20260831-r001 -NumEnvs 8
.\Tools\Train-Soccer.ps1 -Profile press -RunId Press-20260831-r001 -NumEnvs 8
```

### Step과 Episode Length

Trainer의 `Step`은 시간이 아니라 학습 대상 Agent 한 명이 만든 의사결정 경험 한 개다. 같은 BehaviorName을 쓰는 모든 학습 대상 Agent와 모든 Worker의 Step이 합산된다. Base fallback은 Worker마다 Red 4명만 학습하므로 8개 Worker에서는 Red 32명의 경험이 하나의 `Soccer4v4_Base` Step에 함께 누적된다.

현재 선수의 `DecisionPeriod`는 물리 Frame 5회, 프로젝트의 Fixed Timestep은 `0.02초`다. 따라서 Agent 한 명의 의사결정 1회는 시뮬레이션 시간 약 `0.1초`, `Environment/Episode Length = 3000`은 약 `300초 = 5분`이다. 이 값은 YAML 목표가 아니라 실제 종료된 Episode에서 관측한 Agent별 길이다. `time_scale: 10`은 Wall-clock 실행만 가속하며 시뮬레이션 경기 5분을 줄이지 않는다.

`-NumEnvs`는 한 Unity 안의 선수 수가 아니라 동시에 실행할 Windows 프로그램 수다. YAML의 `max_steps`는 Worker별 값이 아니라 모든 Worker와 학습 대상 Agent가 합쳐서 도달하는 총 Step이다. Base fallback은 총 `5,000,000`, 나머지 학습 Profile은 총 `500,000`을 현재 기준으로 사용한다. 8개를 16개로 늘려도 CPU·GPU·메모리 병목 때문에 반드시 두 배 빨라지는 것은 아니므로 처음에는 8개로 Step/s와 메모리를 확인한다.

학습 횟수를 바꾸려면 선택 Profile의 YAML에서 `max_steps`를 수정한 뒤 Build를 다시 실행한다. 실행 스크립트는 Build 당시 YAML hash와 현재 YAML이 다르면 시작을 차단하여 다른 설정이 같은 Run ID에 섞이지 않게 한다.

Base fallback의 `Base-20260831-r003`은 500,704 Step 체크포인트부터 총 5,000,000 Step까지 이어간다. `checkpoint_interval: 500000`, `keep_checkpoints: 12`로 종료 직전·종료 시점의 중복 가능성을 포함해 50만 기준 Model과 이후 50만 단위 이정표를 보존한다. 이 연장은 정책·Reward·Network를 바꾸지 않고 종료 목표와 체크포인트 보존 수만 늘리는 경우이므로 같은 Run에 `-Resume`을 사용한다.

```powershell
.\Tools\Train-Soccer.ps1 -Profile base-fallback -RunId Base-20260831-r003 -NumEnvs 8 -Resume
```

중단된 동일 Run을 이어갈 때만 다음을 사용한다.

```powershell
.\Tools\Train-Soccer.ps1 -Profile attack -RunId Attack-20260831-r001 -NumEnvs 8 -Resume
```

호환되는 기존 신경망을 초기값으로 가져와 새 Run으로 발전시키려면 다음을 사용한다.

```powershell
.\Tools\Train-Soccer.ps1 -Profile attack -RunId Attack-20260831-r002 -NumEnvs 8 -InitializeFrom Attack-20260831-r001
```

- 정책·Reward·Network 등 학습 의미가 바뀌면 새 `--run-id`를 쓴다. 같은 학습의 `max_steps` 연장만은 기존 Run을 재개할 수 있다.
- 중단된 같은 설정을 이어갈 때만 `--resume`한다.
- `--force`로 기존 결과를 덮어쓰지 않는다.
- `--initialize-from`은 원본 Model과 사용 이유를 기록한 fine-tuning에만 사용한다.
- YAML의 `extrinsic.strength`는 Unity Reward 전체 배율이다. 사건별 값은 Profile에서 조정한다.
- Base 두 방식은 모두 `Base-YYYYMMDD-rNNN`을 사용하므로 서로 다른 Revision을 지정한다.
- 서로 다른 Profile을 동시에 돌릴 경우 기본 Port 대역은 겹치지 않지만, GPU 메모리와 전체 Step/s를 별도로 확인한다.

실행 전에 실제 YAML을 읽는다. Hyperparameter의 단일 기준은 문서가 아니라 해당 YAML이다.

## Model 이름과 등록

파일명은 `Type-YYYYMMDD-vNNN.onnx` 형식이다.

```text
Base-20260810-v001.onnx
Attack-20260810-v001.onnx
Defense-20260810-v001.onnx
Press-20260810-v001.onnx
```

등록 순서:

1. 자기 `Models` 폴더에 새 파일로 추가한다.
2. 자기 TeamDefinition의 `Inference Model`에 연결한다.
3. 한 Prefab에만 시험할 때는 `SoccerMatchSetup.Red Model Override`를 사용하고 시험 후 비운다.
4. 선수별 `BehaviorParameters.Model`은 수정하지 않는다.
5. 네 선수의 Model 일치와 실제 Input/Output Tensor를 검사한다.
6. 기준 Model과 같은 Seed·상대·경기 수로 비교한다.

이름과 `PolicyContractVersion = 2`는 분류 정보일 뿐 Tensor 호환성의 증명이 아니다.

## 평가

TensorBoard에서 팀 Reward 추세는 기존 합산 `Soccer/Reward/*`가 아니라 다음 두 그래프를 먼저 비교한다.

- `Soccer/Red/Match Reward`
- `Soccer/Navy/Match Reward`

종료된 경기가 없는 요약 구간에는 경기 평균이 생기지 않는다. 그 사이의 진행은 `Soccer/Red/Reward/Summary Total`, `Soccer/Navy/Reward/Summary Total`과 각 팀의 `Reward/{RewardKind}`를 확인한다. 기존 합산 태그는 양 팀의 반대 부호가 상쇄되므로 승패나 발전 추세 판단에 사용하지 않는다.

각 Run에 최소한 다음을 기록한다.

| 범주 | 기록 |
| --- | --- |
| 재현 | YAML hash 또는 commit, Run ID, Seed, 상대 Model |
| 경기 | 승률, 득실점, 슛과 유효 슛 |
| 연계 | Pass, progressive pass, 3-player combination |
| 수비 | 일반 회수, 빠른 회수, coordinated press |
| 공간 | 평균·최소 동료 거리, crowding, Keeper 복귀 시간 |
| Reward 건강성 | 사건 횟수, possession/match cap 도달률, Goal 대비 shaping 비율 |
| 학습 안정성 | Entropy, Value loss, NaN, 행동 고착 |
| 비용 | Step/s, CPU, memory, GC |

수치만 보지 말고 같은 Seed의 경기 영상도 확인한다. Reward가 높아도 Goal 성과가 나빠지거나 동일 행동을 반복하면 exploit 가능성을 먼저 조사한다.

## 최종 감독 전술

최종 일반 경기에서는 ONNX만 교체하고 Base RewardProfile을 공통 평가 척도로 유지한다. Model 변경 시 점수, 남은 시간, 공·선수 상태와 Human 조작을 유지하고 네 AI 선수를 같은 Frame에 바꾼다.

Attack·Defense·Press 학습 Scene의 HUD 누적 Reward는 Profile 척도가 다르므로 서로 직접 비교하지 않는다. Rule은 Model 목록과 전술 교체 요청에서 항상 제외한다.

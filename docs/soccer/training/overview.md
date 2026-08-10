# Soccer 학습·Model 운용

- 대상: Base·Attack·Defense·Press 학습 및 Model 통합 담당자
- 상태: 학습 Pipeline의 단일 기준
- 마지막 검토: 2026-08-10

Attack, Defense, Press는 같은 정책 계약을 서로 다른 shaping으로 학습하는 작업공간이다. 최종 게임에서는 세 결과를 한 팀의 감독 전술로 사용한다. Rule은 학습 Pipeline에 포함하지 않는다.

## 작업공간과 Behavior

모든 경로의 기준은 `Assets/_Soccer`다.

| Type | Scene | Trainer YAML | BehaviorName | 학습 구성 |
| --- | --- | --- | --- | --- |
| Base smoke | `Core/Scenes/Soccer4v4_Base.unity` | `Core/Training/base_4v4_v2_temp.yaml` | `Soccer4v4_Base` | 짧은 연결 확인 |
| Base long | 같은 Base Scene | `Training/soccer_4v4_poca.yaml` | `Soccer4v4_Base` | 양 팀 Self-Play |
| Attack | `Teams/Attack_KMW/Scenes/Soccer4v4_Attack.unity` | `Teams/Attack_KMW/Training/attack_poca.yaml` | `Soccer4v4_Attack` | Blue 학습, Purple Base |
| Defense | `Teams/Defense_PJH/Scenes/Soccer4v4_Defense.unity` | `Teams/Defense_PJH/Training/defense_poca.yaml` | `Soccer4v4_Defense` | Blue 학습, Purple Base |
| Press | `Teams/Press_KMG/Scenes/Soccer4v4_Press.unity` | `Teams/Press_KMG/Training/press_poca.yaml` | `Soccer4v4_Press` | Blue 학습, Purple Base |

담당자 접미사는 경로에만 사용한다. YAML의 `behaviors` Key, TeamDefinition과 Model Type에는 `_KMW`, `_PJH`, `_KMG`를 붙이지 않는다.

## 필수 선행 조건

1. [경기 계약](../gameplay-contract.md)의 Observation·Action·물리가 모든 환경에서 같다.
2. `Tools/Soccer/Validate 4v4 Prototype`이 통과한다.
3. 수정하려는 Reward 값이 [보상 기준표](../rewards.md)와 Profile에서 일치한다.
4. 새 경로의 Unity compile과 관련 Test가 통과한다.
5. Base 상대 Model과 Seed를 기록한다.

현재 활성 `Models` 폴더에는 v2 ONNX가 없다. `Assets/_Soccer/SourceModels/SoccerTwos.onnx`는 이전 계약이므로 사용하지 않는다. Attack·Defense·Press 장기 학습 전에 승인된 Base v2 Model을 `BaseTeamDefinition.asset`에 등록해야 한다. 그렇지 않으면 Purple이 heuristic fallback으로 움직일 수 있다.

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
- Blue·Purple Agent 수와 Team ID가 맞다.
- Step이 증가하고 NaN·communicator 오류가 없다.
- Episode 종료와 새 경기 Reset이 정상이다.

Smoke 결과를 성능 Model로 사용하지 않는다.

### Base Self-Play

공통 Base Profile을 사용해 대표 상대를 만든다. 학습 길이보다 먼저 Model이 기본 경기 루프를 수행하는지, 보상 상한에 즉시 포화되지 않는지 확인한다. 승인 Model은 기존 파일을 덮어쓰지 않고 Version을 올린다.

### 전술 학습

Attack·Defense·Press는 같은 Base Model, Seed 집합, 경기 시간과 평가 절차를 사용한다. 한 Run에서 Reward와 Network를 동시에 크게 바꾸지 않는다. 전술별 허용 범위는 [학습형 팀 가이드](learning-teams.md)를 따른다.

## 실행 예시

프로젝트에 맞는 Python 환경을 활성화한 뒤 Trainer를 먼저 실행하고 해당 Scene을 Play한다.

```powershell
mlagents-learn Assets/_Soccer/Teams/Attack_KMW/Training/attack_poca.yaml `
  --run-id Attack-20260810-r001 `
  --seed 12345
```

- 설정 또는 Reward 가설이 바뀌면 새 `--run-id`를 쓴다.
- 중단된 같은 설정을 이어갈 때만 `--resume`한다.
- `--force`로 기존 결과를 덮어쓰지 않는다.
- `--initialize-from`은 원본 Model과 사용 이유를 기록한 fine-tuning에만 사용한다.
- YAML의 `extrinsic.strength`는 Unity Reward 전체 배율이다. 사건별 값은 Profile에서 조정한다.

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
3. 한 Prefab에만 시험할 때는 `SoccerMatchSetup.Blue Model Override`를 사용하고 시험 후 비운다.
4. 선수별 `BehaviorParameters.Model`은 수정하지 않는다.
5. 네 선수의 Model 일치와 실제 Input/Output Tensor를 검사한다.
6. 기준 Model과 같은 Seed·상대·경기 수로 비교한다.

이름과 `PolicyContractVersion = 2`는 분류 정보일 뿐 Tensor 호환성의 증명이 아니다.

## 평가

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

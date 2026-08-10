# 학습형 팀 담당자 가이드

- 대상: Base·Attack·Defense·Press 담당자
- 상태: 개인 수정 경계와 인수인계의 단일 기준
- 마지막 검토: 2026-08-10

## 먼저 기억할 것

1. 자기 `RewardProfile`, Trainer YAML, Versioned ONNX와 TeamDefinition의 Model 참조만 기본 소유한다.
2. Observation, Action, Physics, Sensor, 경기장과 Reward 사건 판정은 공통 계약이다.
3. 한 번에 한 가설을 바꾸고 새 Run ID로 비교한다.
4. Reward를 바꾸면 Profile·기준표·README·Test·변경량 표를 함께 갱신한다.
5. 공통 변경이 필요하면 자기 Prefab에서 우회하지 말고 공통 작업으로 승격한다.

전체 계약은 [경기 계약](../gameplay-contract.md), 학습 순서는 [학습 운용](overview.md), 실제 Reward 값은 [보상 기준표](../rewards.md)를 따른다.

## 담당 파일

기준 Root는 `Assets/_Soccer`다.

| Type | Profile | YAML | TeamDefinition·Models |
| --- | --- | --- | --- |
| Base | `Core/Profiles/BaseRewardProfile.asset` | `Core/Training/base_4v4_v2_temp.yaml`, `Training/soccer_4v4_poca.yaml` | `Core/Profiles/BaseTeamDefinition.asset`, `Core/Models` |
| Attack | `Teams/Attack_KMW/Profiles/AttackRewardProfile.asset` | `Teams/Attack_KMW/Training/attack_poca.yaml` | `Teams/Attack_KMW/Profiles/AttackTeamDefinition.asset`, `Teams/Attack_KMW/Models` |
| Defense | `Teams/Defense_PJH/Profiles/DefenseRewardProfile.asset` | `Teams/Defense_PJH/Training/defense_poca.yaml` | `Teams/Defense_PJH/Profiles/DefenseTeamDefinition.asset`, `Teams/Defense_PJH/Models` |
| Press | `Teams/Press_KMG/Profiles/PressRewardProfile.asset` | `Teams/Press_KMG/Training/press_poca.yaml` | `Teams/Press_KMG/Profiles/PressTeamDefinition.asset`, `Teams/Press_KMG/Models` |

Base 담당자도 `Core/Scripts`를 개인 영역으로 보지 않는다. Base의 개인 실험 범위는 Profile, Trainer, Model과 TeamDefinition의 Model 참조다.

## 수정 가능

| 대상 | 가능한 변경 | 조건 |
| --- | --- | --- |
| 자기 `*RewardProfile.asset` | 기존 shaping의 크기와 상한, `0`으로 비활성화 | 양수 저장 규칙과 변경표 유지 |
| 자기 Trainer YAML | learning rate, batch/buffer, network, memory, 학습 길이, engine 설정 | Behavior Key와 정책 계약 유지 |
| 자기 `Models` | 새 ONNX 추가 | Version 증가, 덮어쓰기 금지 |
| 자기 TeamDefinition | 승인 Model 등록 | ID·Behavior·Controller·Contract Version 유지 |
| 자기 Prefab의 Blue Override | 한 환경 추론 시험 | 시험 후 비우고 Purple은 유지 |
| 자기 `*RewardPolicy.cs` | 이미 확정된 사건의 문맥별 multiplier | 새 사건 판정·직접 지급·매 Frame Reward 금지 |
| 자기 README·Test·실험 기록 | 가설과 결과 | 실제 Asset과 동기화 |

## 공통 작업으로 넘겨야 하는 변경

- 새 `SoccerRewardKind` 또는 새 사건
- 판정 거리·시간·소유권·지급 대상·부호 변경
- Observation 순서·정규화, Ray Sensor
- Action Branch·값 의미
- 이동·회전·Kick·Rigidbody·Collider·Physics Material
- Keeper shield, crowding 판정
- 경기 시간·Goal·Reset·UI·Camera·경기장
- Builder와 공통 Prefab 생성 방식

공통 작업은 Template과 Base·Attack·Defense·Press·Rule Prefab 5개, 정책 계약, Test와 문서를 함께 검토한다.

## 수정 금지

| 하지 말 것 | 이유 |
| --- | --- |
| 다른 팀 폴더 수정 | 실험과 Model 참조가 섞임 |
| Core Runtime에 Team 이름 분기 추가 | 공통 계약이 갈라짐 |
| 선수별 `BehaviorParameters.Model` 수정 | 시작 시 덮어써지고 선수 간 불일치 발생 |
| `trainBlue`, `trainPurple`, 상대 TeamDefinition 변경 | Trainer 대상과 기준 상대가 바뀜 |
| 자기 Prefab만 경기장·물리·Sensor 변경 | parity와 ONNX 호환이 깨짐 |
| BehaviorName 또는 YAML Key 변경 | Trainer 연결 실패 |
| 기존 Run·ONNX 덮어쓰기 | 재현과 회귀 비교 불가 |
| Rule에 Trainer·ONNX 연결 | 비학습 비교군 훼손 |

## Reward 실험 절차

### Profile 저장 규칙

- Profile에는 magnitude를 양수로 저장한다.
- 실점·패배와 teammate crowding의 음수 부호는 Engine이 적용한다.
- `formationChangeScale`은 고정액이 아니라 개선량 multiplier다.
- 사건 값을 키울 때 개별 cap과 전체 possession/match cap을 함께 확인한다.
- `extrinsic.strength`를 바꾸면 Unity Reward 전체가 변하므로 사건별 비교와 섞지 않는다.

### 변경 전

1. 문제를 한 문장으로 쓴다. 예: “Attack이 전진하지만 Goal 기여 없이 공을 오래 운반한다.”
2. 기준 Run의 Model, YAML, Seed, 상대와 경기 수를 기록한다.
3. 사건 빈도, cap 도달률과 영상을 확인한다.
4. Profile 한 항목 또는 YAML 한 가설만 선택한다.

### 변경할 때

1. 자기 Profile 또는 YAML만 수정한다.
2. [보상 기준표](../rewards.md)의 값·cap·판정을 확인한다.
3. 해당 Team README와 관련 Test를 갱신한다.
4. 새 Run ID를 사용한다.
5. 결과에 다음 표를 포함한다.

| Profile | 항목 | 기존 값 | 변경 값 | 변경량 | Cap 변경 | 가설 |
| --- | --- | ---: | ---: | ---: | --- | --- |
| 예: Attack | `attackSuccess` | `0.05` | `0.04` | `-0.01` | 없음 | 무의미한 장기 운반 억제 |

판정 거리나 주기를 바꿨다면 값이 그대로여도 변경표에 적고 공통 작업으로 처리한다.

## 학습 전 Checklist

- [ ] 자기 Scene과 Environment Prefab을 사용한다.
- [ ] TeamDefinition, Profile, BehaviorName과 YAML Key가 일치한다.
- [ ] Blue만 Trainer 대상이고 Purple은 의도한 Base 상대다.
- [ ] 승인 Base v2 Model이 실제로 등록되어 있다.
- [ ] `SourceModels/SoccerTwos.onnx`를 사용하지 않는다.
- [ ] Model Override와 선수별 Model Slot에 이전 시험값이 남지 않았다.
- [ ] `Validate 4v4 Prototype`과 관련 Test가 통과한다.
- [ ] 기준 Run, Seed, 상대 Model과 성공 기준을 기록했다.

승인 Base Model이 없으면 먼저 Base smoke와 Self-Play를 수행한다. fallback 상대를 Base Neural이라고 기록하지 않는다.

## 학습 중 확인

| 증상 | 먼저 확인 |
| --- | --- |
| Reward가 급격히 포화 | 사건 빈도와 possession/match cap |
| Reward는 오르지만 Goal은 감소 | shaping exploit과 영상 |
| 사건이 거의 없음 | 값을 키우기 전 판정 발생 여부 |
| 행동이 고착 | Entropy, Mask, Action 분포와 Reset |
| 학습 속도가 느림 | Step/s, Raycast, CPU·GC, time scale |
| Purple이 이상하게 움직임 | Base Model 등록과 BehaviorType |

같은 설정을 이어갈 때만 `--resume`하고 변경된 설정은 새 Run으로 시작한다.

## ONNX 인수인계

1. `Type-YYYYMMDD-vNNN.onnx`로 새 파일을 추가한다.
2. Run ID, YAML, Seed, 기준 상대와 평가 결과를 함께 기록한다.
3. 자기 TeamDefinition에 등록한다.
4. Input/Output Tensor와 네 선수 Model 일치를 확인한다.
5. 같은 Seed와 경기 수로 이전 승인 Model과 비교한다.
6. 최종 시연에서는 Model만 교체하고 Base RewardProfile을 유지한다.

## 완료 Checklist

- [ ] 자기 영역 밖을 전술 실험으로 수정하지 않았다.
- [ ] 한 가설과 새 Run ID를 사용했다.
- [ ] Reward 변경을 기준표·README·Test·변경량 표에 반영했다.
- [ ] BehaviorName, TeamDefinition과 YAML Key를 유지했다.
- [ ] 새 ONNX Version과 재현 정보를 남겼다.
- [ ] 승률뿐 아니라 행동 지표, cap, 영상과 비용을 확인했다.
- [ ] 공통 변경 요구는 한 팀 Prefab에서 제거하고 공통 담당자에게 전달했다.

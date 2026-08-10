# 중요 결정 기록

> 보관 상태: D-001~D-026의 원문과 배경 기록  
> 현행 요약: [현재 결정](../../project/decisions.md)  
> 이 문서의 대체된 경로·공개 정책·작업 경계는 현재 지침으로 사용하지 않는다.

마지막 갱신: 2026-08-10

이 문서는 나중에 구조를 보고 “왜 이렇게 했는가?”를 이해할 수 있도록 중요한 결정을 보존한다. 결정을 바꿀 때는 이전 항목을 삭제하지 말고 상태를 `대체됨`으로 변경한 뒤 새 결정 번호를 연결한다.

## 결정 템플릿

- 번호 / 제목:
- 날짜:
- 상태: 제안, 확정, 대체됨
- 배경:
- 결정:
- 이유:
- 영향:
- 대체한 결정 또는 관련 문서:

---

## D-001 — 작업 폴더 경계

- 날짜: 2026-07-18
- 상태: `확정`
- 결정: 수정 가능한 폴더를 `C:\GitHub\Machine-Learning`으로 제한한다. `C:\GitHub\ml-agents`는 조회 및 비변경 실행만 가능한 읽기 전용 참고 폴더로 사용한다. 그 밖의 `C:\GitHub` 하위 폴더는 조회하지 않는다.
- 이유: 사용자 자료를 보호하고 작업 범위를 명확히 유지하기 위해서다.
- 영향: 모든 명령과 파일 경로는 실행 전에 이 경계를 만족하는지 확인한다.

## D-002 — 기존 목표 찾기 에셋 보존

- 날짜: 2026-07-18
- 상태: `확정`
- 결정: 기존 Turtle 목표 찾기 장면, 프리팹, 코드, 모델 및 관련 에셋을 읽기 전용으로 보존한다.
- 이유: 사용자가 개발한 임시 에셋이며 향후 참고 기준으로 사용해야 하기 때문이다.
- 영향: 새 구현은 기존 파일을 고치지 않는다. D-026에 따라 현재 보관 위치는 `Assets/_Legacy/Turtle`이며, 구조 이동으로 깨진 참조를 복구하는 최소 수정만 허용한다.

## D-003 — 두 게임의 에셋 분리

- 날짜: 2026-07-18
- 상태: `대체됨(D-014, D-026)`
- 결정: 탈출 게임은 `Assets/Escape`, 축구 게임은 `Assets/Soccer` 아래에서 개발한다.
- 이유: 두 학습 환경과 게임 콘텐츠의 의존성과 변경 범위를 분명히 하기 위해서다.
- 영향: 새로운 공용 에셋 위치는 자동으로 만들지 않고 필요가 확인되면 별도 결정한다.

## D-004 — 학습 환경 우선 개발

- 날짜: 2026-07-18
- 상태: `확정`
- 결정: 게임 콘텐츠를 본격적으로 개발하기 전에 최소 학습 환경을 만들고 학습 가능성을 검증한다.
- 이유: 학습 설계 오류와 게임 로직 오류를 분리하고 반복 비용을 줄이기 위해서다.
- 영향: 관측, 행동, 보상, 종료 조건, 평가 지표를 먼저 정의한다.

## D-005 — 개인 문서의 Git 업로드 방지

- 날짜: 2026-07-18
- 상태: `대체됨(D-026)`
- 결정: 모든 내부 계획과 기록은 `docs`에서 관리하고 루트 `.gitignore`의 `/docs/` 규칙으로 원격 업로드를 막는다.
- 이유: 문서는 사용자 개인 조회용이기 때문이다.
- 영향: 협업을 위해 공개할 문서가 필요해지면 `docs`의 ignore를 해제하지 말고 공개 위치를 별도로 결정한다.

## D-006 — 기획 미정 항목을 임의로 확정하지 않음

- 날짜: 2026-07-18
- 상태: `대체됨(D-014, D-015, D-016)`
- 결정: Escape와 Soccer의 세부 규칙은 다음 사용자 기획을 받은 뒤 확정한다.
- 이유: 현재 정보만으로 역할, 보상, 경기 구성 등을 정하면 사용자의 게임 의도와 달라질 수 있기 때문이다.
- 영향: 현재 문서에는 필요한 질문과 제안만 기록한다.

## D-007 — Escape의 비대칭 Agent 구조

- 날짜: 2026-07-18
- 상태: `대체됨(D-014)`
- 결정: Player 1명은 `EscapePlayer`, Enemy 3명은 `EscapeEnemy` Behavior를 사용하고 Team ID를 각각 0과 1로 분리한다. Enemy 3명은 같은 Shared Policy와 한 개의 `SimpleMultiAgentGroup`을 사용한다.
- 이유: Player와 Enemy의 역할과 인원수가 다른 비대칭 경쟁이며, Enemy는 공동 승리 조건을 협력해서 달성해야 하기 때문이다.
- 영향: Player 단독 PPO, Enemy MA-POCA, 이후 선택적 비대칭 Self-Play를 단계적으로 검증한다.
- 관련 문서: `archive/2026-08-09-escape-prototype-spec.md` 6장

## D-008 — 동시 이동을 위한 두 개의 Discrete Branch

- 날짜: 2026-07-18
- 상태: `대체됨(D-014)`
- 결정: Player와 Enemy 모두 이동 크기 3과 회전 크기 3의 두 Discrete Branch를 사용한다.
- 이유: W/S와 A/D의 동시 입력을 그대로 표현하면서 사람 Heuristic과 학습 Action의 의미를 일치시키기 위해서다.
- 영향: 이동과 회전은 한 물리 스텝에서 동시에 적용하며 공격은 자동 근접 판정으로 분리한다.
- 관련 문서: `archive/2026-08-09-escape-prototype-spec.md` 4장, 6장

## D-009 — 환경 Controller 단일 Reset 권한

- 날짜: 2026-07-18
- 상태: `대체됨(D-014)`
- 결정: Agent `MaxStep`을 0으로 두고 승패, 시간, Group 종료, Randomization과 Reset을 Environment Controller 한 곳에서 처리한다.
- 이유: Player와 Enemy Episode가 따로 Reset되어 상태가 어긋나는 것을 막고 DungeonEscape의 환경 단위 관리 방식을 적용하기 위해서다.
- 영향: 시간 초과도 적의 정상 승리로 처리하고 모든 참여자의 Episode를 같은 결과로 종료한다.
- 관련 문서: `archive/2026-08-09-escape-prototype-spec.md` 5장, 6장

## D-010 — Escape 프로토타입 치수 기준

- 날짜: 2026-07-18
- 상태: `대체됨(D-014)`
- 결정: 96×96m 맵, 11×12×11m 건물, 18m 중심 간격, 7m 도로, 36개 교차로 SpawnPoint를 사용한다.
- 이유: 5×5 격자와 동일 간격을 단순한 정수 좌표로 만들고 캐릭터 이동과 Ray 거리의 기준을 명확히 하기 위해서다.
- 영향: 수치는 Inspector 설정과 Anchor 기반으로 만들어 초기 플레이 검증 후 에셋 재작성 없이 조정한다.
- 관련 문서: `archive/2026-08-09-escape-prototype-spec.md` 2장, 3장

## D-011 — UI와 Agent 제어 모드의 분리

- 날짜: 2026-07-18
- 상태: `대체됨(D-014)`
- 결정: Player 제어를 Human, Training, Inference 세 내부 모드로 분리하고 `I` 키로 전환한다. HUD AI Toggle은 상태 표시 전용이다.
- 이유: `BehaviorType.Default`의 자동 fallback 때문에 AI ON 표시 중 키보드 Heuristic이 실행되는 혼동을 막기 위해서다.
- 영향: Trainer와 Model이 모두 없을 때 AI ON의 fallback Action은 0이며 HUD에 대기 상태를 표시한다.
- 관련 문서: `archive/2026-08-09-escape-prototype-spec.md` 6장, 7장

## D-012 — Gate 위치

- 날짜: 2026-07-18
- 상태: `대체됨(D-014)`
- 결정: 매 Episode마다 북·동·남·서 외곽 벽 중 한 면을 무작위로 선택하고 Gate를 선택된 벽의 중앙에 설치한다.
- 이유: 사용자가 Gate의 벽 중앙 배치를 명시적으로 확인했다.
- 영향: Environment 프리팹에 외곽 벽 중앙 GateAnchor 네 개를 두며 모서리 GateAnchor는 만들지 않는다.
- 관련 문서: `archive/2026-08-09-escape-prototype-spec.md` 3장

## D-013 — 학습 병렬화의 초기 기준

- 날짜: 2026-07-18
- 상태: `대체됨(D-014)`
- 결정: ML-Agents 4.0.3의 `TrainingAreaReplicator`와 `--num-areas=16`으로 한 Unity Build 프로세스에 Arena 16개를 두고 `--num-envs=1`로 시작한다. 실제 기본 Arena 수와 Time Scale은 1/4/8/16 Benchmark로 확정한다.
- 이유: 관측 데이터와 GPU 메모리 부담은 낮지만 Raycast는 CPU Physics 작업이며 고속 Time Scale에서 노트북 CPU와 발열이 병목이 될 수 있기 때문이다.
- 영향: Training Mode는 렌더링을 끄고 Batched Raycast를 사용하며 Arena 수를 설정값으로 바꿀 수 있게 한다.
- 관련 문서: `archive/2026-08-09-escape-training-performance.md`

## D-014 — Escape 개발 종료와 역사 보존

- 날짜: 2026-08-09
- 상태: `확정`
- 결정: Escape 게임의 추가 개발, 학습, Benchmark와 빌드를 모두 종료한다. 기존 `Assets/Escape` 구현 자산과 작업 기록은 역사 자료로 보존하고 현행 개발·빌드 대상에서 제외한다.
- 이유: 최종 제품 범위를 Soccer 4v4 감독 전술 게임에 집중하기 위해서다.
- 영향: Escape 상세 문서 두 개를 날짜가 붙은 Archive로 이동한다. 과거 결정과 작업 이력은 삭제하지 않고 이 결정으로 대체 상태를 표시한다. D-026 이후 구현 자산의 현재 위치는 `Assets/_Legacy/Escape`다.
- 관련 문서: `01-goals.md`, `02-plan.md`, `archive/2026-08-09-escape-prototype-spec.md`, `archive/2026-08-09-escape-training-performance.md`

## D-015 — Pass 폐기와 Rule FSM 비교 팀

- 날짜: 2026-08-09
- 상태: `확정`
- 결정: Pass를 독립 학습 전술로 유지하지 않고 `Rule`로 이름을 바꾼다. Rule은 기존 Raycast를 사용하는 완전한 FSM 팀이며 Trainer YAML, ONNX와 모델 자동 할당에서 제외한다.
- 이유: 학습 정책과 동일한 경기 조건에서 규칙형 판단의 품질과 비용을 비교하기 위해서다.
- 영향: Rule은 `HeuristicOnly`로 동작하고 사람 한 명의 개입과 완전 자율 동작을 모두 지원한다. 경기 중 공격형·수비형·압박형으로 변경하지 않는다. 과거 Pass 사본은 `Assets/_Legacy/ArchivedSoccer~/Pass`에 Unity import 제외 상태로 보관한다.
- 관련 문서: `04-design-details.md`, `09-soccer-prototype-spec.md`, `12-soccer-4v4-v2-team-training.md`

## D-016 — 경기 중 감독 ONNX 전술 교체

- 날짜: 2026-08-09
- 상태: `확정`
- 결정: Attack, Defense, Press 폴더는 초기 전문 학습 공간으로 사용한다. 최종 게임에서는 플레이어가 감독이 되어 한 팀의 공격형·수비형·압박형 ONNX를 경기 도중 교체한다.
- 이유: 전술별 팀 대전보다 경기 상황을 읽고 전술을 선택하는 감독 플레이가 최종 게임의 핵심이기 때문이다.
- 영향: 세 ONNX는 관측 43개와 행동 계약 v2 `[3,3,3,3]`을 공유해야 한다. 전술 변경 중에도 점수, 시간, 공, 선수 위치와 Human 조작 상태를 유지하며 Rule은 교체 대상에서 제외한다.
- 관련 문서: `01-goals.md`, `02-plan.md`, `09-soccer-prototype-spec.md`, `12-soccer-4v4-v2-team-training.md`

## D-017 — Soccer 4v4 역할과 킥 계약 v2

- 날짜: 2026-08-09
- 상태: `확정`
- 결정: 팀당 수비·골키퍼 겸임 1명, 자유 미드필더 2명, 스트라이커 1명을 사용한다. 행동 계약은 `[3,3,3,3]`이며 제어 킥은 `1500`, 강한 킥은 `4000`이다. 사람 입력은 키보드 `E`/`Space`, Xbox `A`/`B`를 사용한다.
- 이유: 사람과 신경망, Rule FSM이 같은 물리 행동 의미를 공유하면서 패스와 슛을 명시적으로 구분하기 위해서다.
- 영향: 계약 v1 ONNX는 사용할 수 없다. 세 전술 ONNX의 런타임 교체 전 계약 검증이 필요하다.
- 관련 문서: `09-soccer-prototype-spec.md`

## D-018 — 연계 보상과 자유 포메이션

- 날짜: 2026-08-09
- 상태: `부분 대체됨(D-024)`
- 결정: 득점과 경기 결과를 필수 보상으로 유지하고, 기존 보상에 전진 패스, 서로 다른 세 선수의 연계, 협력 압박을 추가한다. 포메이션은 고정 위치 대신 넓은 간격·지원·공수 균형의 개선만 낮은 비중으로 보상한다.
- 이유: 감독이 지시하기 좋은 팀 연계를 유도하면서 선수들이 자기 시작 위치에 과도하게 묶이는 현상을 줄이기 위해서다.
- 영향: 안정적 소유권 확인과 사건별·소유권별·경기별 shaping 상한을 사용한다. 포메이션은 미드필더 위치를 고정하지 않고 수비·골키퍼 겸임 선수의 상황별 전진을 허용한다.
- 관련 문서: `04-design-details.md`, `12-soccer-4v4-v2-team-training.md`

## D-019 — 전술별 독립 환경 프리팹과 단일 모델 슬롯

- 날짜: 2026-08-09
- 상태: `부분 대체됨(D-022)`
- 결정: Base, Attack, Defense, Press, Rule은 각 작업공간 폴더 안의 서로 다른 Regular Prefab을 사용한다. 각 프리팹은 Blue 전술 TeamDefinition, Purple Base TeamDefinition, 보상 정책, 훈련 플래그와 Blue/Purple 단일 모델 오버라이드 슬롯을 소유한다.
- 이유: 한 개발자가 자기 전술 프리팹이나 모델 참조를 바꿔도 다른 개발자의 환경과 모델 연결이 바뀌지 않게 하기 위해서다.
- 영향: 독립 GUID와 전술별 모델·보상 참조는 보존한다. 기존 프리팹의 공통 기하를 무조건 보존하던 원칙은 D-022로 대체하며, 선수별 `BehaviorParameters.Model` 직접 지정은 금지한다.
- 관련 문서: `09-soccer-prototype-spec.md`, `12-soccer-4v4-v2-team-training.md`, `13-soccer-current-status.md`

## D-020 — 전술 모델명과 최종 공통 평가 보상

- 날짜: 2026-08-09
- 상태: `확정`
- 결정: 신경망 파일명은 `Type-YYYYMMDD-vNNN.onnx`를 사용한다. 최종 시연에서 Attack, Defense, Press 전환은 ONNX만 바꾸며 RewardProfile은 공통 Base 평가 기준을 유지한다.
- 이유: HUD가 실제 적용 모델에서 전술을 판별하고, 한 경기 안의 누적 보상을 동일한 척도로 비교하기 위해서다.
- 영향: 잘못된 이름은 `UNKNOWN`, 네 선수 모델 불일치는 `MIXED`로 표시한다. 이름은 표시 규칙일 뿐 계약 증명이 아니므로 실제 ONNX 입력·출력 검증이 별도로 필요하다.
- 관련 문서: `12-soccer-4v4-v2-team-training.md`, `13-soccer-current-status.md`

## D-021 — AI 기본 시작과 경기 HUD 기준

- 날짜: 2026-08-09
- 상태: `확정`
- 결정: 모든 Soccer 씬은 AI 모드로 시작한다. HUD는 점수·시간·제어 모드에 현재 Blue/Purple 전술과 경기 누적 보상을 추가하며, AI 관전 카메라는 `(0,82,-78)`, FOV `50`을 사용한다.
- 이유: 첫 화면부터 자율 경기를 관전하고 현재 정책과 평가 결과를 확인하기 위해서다.
- 영향: `H` 또는 Xbox `Y` Human 전환은 유지한다. 누적 보상은 모델 교체로 초기화되는 Agent 내부 값이 아니라 RewardEngine의 독립 경기 장부를 사용한다.
- 관련 문서: `09-soccer-prototype-spec.md`, `10-soccer-camera-update.md`, `13-soccer-current-status.md`

## D-022 — 독립 모델 슬롯과 공통 경기장 동기화

- 날짜: 2026-08-09
- 상태: `확정`
- 결정: Base, Attack, Defense, Press, Rule의 환경 프리팹과 모델 슬롯은 독립 상태를 유지하되 경기장 구조와 공통 경기 설정은 한 작업에서 다섯 프리팹에 동일하게 적용한다.
- 이유: 개발자별 ONNX와 보상 실험은 격리하면서도 골대, 벽, 공, 물리, 센서와 공통 표시 기능의 누락으로 학습·시연 환경이 달라지는 문제를 방지하기 위해서다.
- 영향: 공통 변경은 Builder의 공통 적용 지점을 사용한다. 공용 템플릿과 다섯 프리팹의 계층·Transform·Renderer·Material·물리·센서 계약을 비교하며, 하나라도 다르면 생성 검증을 실패시킨다. 전술별 차이는 TeamDefinition, 모델 슬롯, RewardPolicy·RewardProfile, 학습 플래그와 Rule FSM으로 제한한다.
- 관련 문서: `09-soccer-prototype-spec.md`, `12-soccer-4v4-v2-team-training.md`, `13-soccer-current-status.md`

## D-023 — 골대 전용 머티리얼과 조건부 반투명

- 날짜: 2026-08-09
- 상태: `확정`
- 결정: 골대와 선수는 머티리얼을 공유하지 않는다. 골대는 `GoalBlue`, `GoalPurple`, `GoalNetBlack`, `GoalNetWhite` 전용 머티리얼을 사용하며, 카메라와 화면 안 선수 사이를 가릴 때만 런타임 복제 머티리얼의 Alpha를 `0.25`로 낮춘다.
- 이유: 선수 외형에 영향을 주지 않고 골대의 존재감과 선수 시야를 모두 유지하기 위해서다.
- 영향: 반투명 전환은 Renderer와 Shadow 표현에만 적용한다. 골대 GameObject, Collider, Layer, `blueGoal`·`purpleGoal` Tag와 득점 판정은 변경하지 않는다. 모든 활성 4v4 씬은 같은 가림 제어 설정을 사용한다.
- 관련 문서: `09-soccer-prototype-spec.md`, `10-soccer-camera-update.md`, `13-soccer-current-status.md`

## D-024 — 수비·골키퍼 공통 하프라인 제약과 위기 복귀

- 날짜: 2026-08-09
- 상태: `확정`
- 결정: 수비·골키퍼 겸임 선수는 Neural 정책, 모델 없는 fallback과 Rule FSM이 공유하는 action shield를 사용한다. 공격 깊이 `-4m`부터 공격 방향 이동과 속도를 완만하게 줄이고 하프라인 `0m`에서 공격 방향 힘과 속도를 차단한다. 상대 소유·마지막 상대 터치 또는 자기 골문 위협 시 시작 골문 깊이로 최대 속도 복귀하며, Rule은 긴급 `RecoverGoal` 상태를 사용하고 먼 공의 추격자 후보에서 해당 선수를 제외한다.
- 이유: 수비·골키퍼 겸임 선수가 공을 따라 상대 진영까지 이탈하는 현상을 막고, 공수 전환 때 골문 보호를 모든 제어 방식에서 동일하게 보장하기 위해서다.
- 영향: D-018의 자유 포메이션 결정 중 수비·골키퍼 겸임 역할의 넓은 전진 허용을 대체한다. 포메이션 전체의 역할 비중 `10%`는 유지하되 팀 소유 시 공격 깊이 `-4m` 이내를 만점, `+4m`를 0점으로 평가하고, 비소유 시 시작 골문 깊이 `6m` 이내를 만점, `30m`를 0점으로 평가한다. 보상 배율과 상한, 행동 Branch와 관측 크기는 바꾸지 않는다.
- 관련 문서: `04-design-details.md`, `09-soccer-prototype-spec.md`, `12-soccer-4v4-v2-team-training.md`, `14-soccer-reward-reference.md`

## D-025 — 학습형·규칙형 담당자의 수정 경계 분리

- 날짜: 2026-08-10
- 상태: `확정`
- 결정: 학습형 담당자는 자기 RewardProfile, Trainer YAML, 버전 모델과 제한된 RewardPolicy를 수정하고, 규칙형 담당자는 Rule FSM의 상태·전이·목표·조향을 수정한다. 관측·행동·물리·센서·경기장·소유권과 보상 사건 판정·골키퍼 보호는 공통 계약으로 두며 개인 작업공간에서 분기하거나 우회하지 않는다.
- 이유: 여러 담당자가 보상과 전술을 반복 변경해도 다른 팀의 실험, 최종 ONNX 호환성과 공통 경기 환경을 손상시키지 않기 위해서다.
- 영향: 보상 변경은 기준표·팀 README·작업 로그·테스트와 이전값/새값/변경량 표를 같은 작업에서 갱신한다. Rule의 평가 보상은 행동을 바꾸지 않으며 행동 변경은 FSM에서 수행한다. 공통 변경은 Builder를 통해 템플릿과 다섯 환경에 함께 적용한다.
- 관련 문서: `12-soccer-4v4-v2-team-training.md`, `14-soccer-reward-reference.md`, `15-soccer-learning-team-maintainer-manual.md`, `16-soccer-rule-team-maintainer-manual.md`

## D-026 — 활성 Soccer와 Legacy 에셋의 물리 분리

- 날짜: 2026-08-10
- 상태: `확정`
- 결정: 활성 Soccer 에셋을 `Assets/_Soccer`에 모으고 Turtle, Escape, Unity 템플릿, 과거 진단과 폐기 Soccer 실험을 `Assets/_Legacy`에 분류한다. 팀 물리 폴더는 `Attack_KMW`, `Defense_PJH`, `Press_KMG`, `Rule_PHC`로 구분하되 논리 전술명·BehaviorName·모델 접두사·클래스·asmdef 이름은 Attack, Defense, Press, Rule을 유지한다. 실제 공통 참조인 `Assets/Settings`와 `Assets/InputSystem_Actions.inputactions`는 Assets 루트에 둔다. `docs`와 Markdown은 Git 공유 대상으로 전환한다.
- 이유: 서로 다른 학습 실험과 기본 콘텐츠의 혼재를 없애고, 팀원이 저장소 하나만 받아도 현재 게임과 보관 실험의 경계 및 담당 영역을 바로 알 수 있게 하기 위해서다.
- 영향: 에셋과 `.meta`를 함께 이동해 GUID를 보존하고 Builder, Build Settings, 테스트와 현행 문서 경로를 새 구조로 갱신한다. Legacy는 삭제 대상이 아니며 Turtle·Escape 실행 가능성을 유지한다. `ArchivedSoccer~/Pass`와 현행 Rule의 중복 GUID 사본은 `~` 폴더 밖에 동시에 두지 않는다. D-003의 물리 경로와 D-005의 문서 비공개 결정을 대체한다.
- 보상 영향: 보상 수치·판정·상한 변경 없음.
- 관련 문서: `README.md`, `AGENTS.md`, `17-unity-asset-layout.md`, `15-soccer-learning-team-maintainer-manual.md`, `16-soccer-rule-team-maintainer-manual.md`

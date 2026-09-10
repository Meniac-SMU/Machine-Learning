# MNG 감독 강화학습 계획 — 개발 실행 진입점

- 작성일: 2026-09-09
- 실행 담당: 사용자 1명 + GPT-5.6 Sol. 자동으로 별도 작업/하위 에이전트를 만들지 않는다.
- 현재 상태(2026-09-10 13:57): M0는 사용자 확인으로 합격했다. Keeper 공 고정 시선, 킥 플레이트 기반 교대 경합 탈출, 중계/Human 추적 Main Camera를 추가했다. 최종300초 Fallback은 공 최대 이동61.71m와1:1을 재현했다. 마지막 테스트 격리 수정 뒤 전체 PlayMode 재실행, 기존 frozen M1/M2/M1B 새 evidence 재평가, M3 r001 정식300초×40경기가 순서대로 남았다. 기존 승인 모델과 Run은 보존하며 최신 상세 근거와 재개 순서는 `MNG_05_Development_Handoff.md`를 따른다.
- 목표: 개발 시작 후 2일 내 학습된 감독의 4대4 시연, 15일 내 self-play·전술 교체·AI/HUMAN·검증 자료 완성.
- 사용자가 MNG 구현과 킥 플레이트 활용 개발을 승인했다. 기존 Core/과거 Run을 보존하며 MNG 전용 파일과 산출물만 개발한다.

## 문서 순서와 단일 기준

1. 이 문서: 범위·승인·인계 원칙.
2. [런타임 계약](MNG_01_Runtime_Contract.md): 물리·관측·행동·HUMAN·상대.
3. [구현 순서](MNG_02_Implementation_Plan.md): 파일 책임·작업 의존성·완료 기준.
4. [커리큘럼과 보상](MNG_03_Curriculum_and_Rewards.md): 단계·승급·학습 예산·PPO·self-play.
5. [검증과 인계](MNG_04_Validation_and_Handoff.md): 평가·명령 도구·15일 일정·중단 조건.

수치 변경은 책임 문서와 Profile/코드/테스트를 동시에 갱신한다. 계획상의 기준은 아직 측정 결과가 아니다. 미구현 경로와 API는 아래 문서에 ‘예정’으로 명시한다.

## 확정된 사용자 결정

| 항목 | 결정 |
|---|---|
| 감독 | 팀당 Agent 1개, PPO, 숫자 관측, 카메라는 관람용 |
| 경기 | 기존 Stadium·경기장 규격·선수 외형/크기·UI 유지, 4대4 |
| 폴더 | Assets/_Soccer/Manager |
| 명명 | 신규/대폭 재작성 코드·에셋·신경망 MNG_ 접두사 |
| 공 | 기준본 대비 균일 크기×1.10, 질량×1.50 |
| HUMAN | H키로 Red Striker 직접 조작, 감독은 나머지3명 지휘·4명 전체 관측 |
| 기술 | 이동·추격·드리블·패스·슛·수비는 새 공통 코드. 기술 자체는 학습하지 않음 |
| 초기 상대 | Core Fallback 규칙을 활용. 필요하면 같은 규칙의 새 감독으로 포팅 |
| 최종 상대 | 같은 감독 구조로 PPO self-play |
| Q-MNG-01 | 6개 팀 명령으로 시작. 대상/좌표는 코드가 결정 — 사용자 답변 확정 |
| Q-MNG-02 | 스크립트 Human 대역으로 준비 학습 + 실제 H키/사람 조작 검증 — 사용자 답변 확정 |

공 배율·환경·Human 제어권은 임의 튜닝 금지. 그 외 기술/PPO/보상 초기값은 이 계획의 설계 선택이며 검증을 통해 조정할 수 있다. 실패 후 평가 기준을 사후 완화하지 않는다.

## 기존 프로젝트와 경계

- 작성 당시 git status에 기존 수정/미추적 파일 다수. 시작 시 재확인하고 사용자 변경을 원복/정리/덮어쓰지 않는다.
- 과거 L0/L1/L2/L3·r017 PT/ONNX/log/승인/미승인 판정 보존. 기존99% gate를 MNG 기준으로 바꾸지 않는다.
- MNG 감독은 기존 관측379/행동[3,3,3,3]과 별도 정책이다. 기존 선수 모델이나 외부 SoccerTwos PT를 감독 초기값으로 이식하지 않는다.
- Core Stadium을 읽어 MNG 전용 복사본을 만든다. 공 변경도 이 복사본에 적용한다. 기존 다섯 Stadium을 새 물리로 일괄 변경하지 않는다.
- 최신 사용자 MNG 요구는 기존 팀 폴더/논리명/MA-POCA/선수 Ray 계약과 구분되는 신규 범위다. 기존 프로젝트 AGENTS.md를 읽되 과거 선수 계약을 새 감독에 그대로 강제하지 않는다.
- 공통 코드 변경은 최소 opt-in 연결만. 대폭 변경은 MNG_ 파일로 분리. 공통 변경에는 기존 회귀 필요.
- 기존 Builder로 MNG 씬을 재생성하지 않는다. MNG Builder는 MNG 자산만 쓴다.
- 실제 구현 단계에서 docs/soccer/current-status.md에 MNG 진행 섹션을 추가한다. 완료된 과거 기록은 유지한다. 이 계획 작성에서는 기존 상태 문서를 바꾸지 않았다.
- 구현 승인과 학습 승인을 구분한다. 후속 지시가 전체 구현·한정 훈련을 포함하면 매 단계 재승인을 요구하지 않고 gate/예산에 따라 진행한다.

## 확인한 기반

- Editor6000.3.16f1, 프로젝트 ML-Agents4.0.3: ProjectVersion.txt/Packages/manifest.json. Python trainer는 개발 시작 시 별도 확인.
- Core 공 질량3, 중심Y0.52867365, Bouncy 반발0.8: Prefab/재질 확인.
- Core Fallback: SoccerEnvController.GetAutonomousTarget/TryGetAutonomousKickTarget와 AgentSoccer.ReadAutonomousInput.
- 별도 RuleBasedSoccerController는 Core Fallback이 아니다. 서로의 규칙을 섞지 않는다.
- HUD는 SoccerEnvController 구체 타입 의존. 화면 레이아웃은 재사용하고 MNG_HudPresenter로 데이터 연결을 작성한다.
- 이 계획은 현재 소스에 대한 정적 조사로 작성했다. 실제 동작과 학습 성능은 미검증.

## 실행 시작 체크리스트

- [ ] 최신 사용자 개발 지시와 이 문서의 결정 확인
- [ ] AGENTS.md, 문서5개, git status 확인
- [ ] 기존 변경·원본 Geometry·UI·공 값을 source snapshot으로 보존
- [x] M0→M1→M2 순으로 작업, 각 산출물과 실제 검증 기록
- [ ] 2일 시연에 Trainer/ONNX 제어 증거 포함. 규칙형 결과를 RL이라고 표시 금지
- [ ] 한 번에 한 주요 변경, 새 Run ID, 실패·성공 산출물 보존

## 공식 참고

- https://github.com/Unity-Technologies/ml-agents/blob/develop/com.unity.ml-agents/Documentation~/Learning-Environment-Design-Agents.md
- https://github.com/Unity-Technologies/ml-agents/blob/develop/com.unity.ml-agents/Documentation~/Training-Configuration-File.md

공식 문서는 설계 참고다. 실제 API/YAML은 설치된 Unity 패키지와 Python trainer로 검증한다.

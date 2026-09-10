# MNG 구현 순서와 파일 책임

모든 코드·에셋·도구 이름은 생성 예정이다. 현재 존재하는 것은 계획 Markdown뿐이다. 사용자의 개발 지시 후 시작한다.

## 1. 예정 폴더와 코드

| 위치 | 예정 파일과 책임 |
|---|---|
| Runtime | MNG_ManagerAgent.cs: 감독 학습 진입점 |
| Runtime | MNG_MatchController.cs / MNG_MatchSnapshot.cs: 경기 상태와 동시점 관측 |
| Runtime | MNG_ObservationWriter.cs / MNG_Command.cs: 관측133·행동6 schema |
| Runtime | MNG_TeamPlanner.cs: 명령을 선수별 작업으로 배정 |
| Runtime | MNG_PlayerMotor.cs / MNG_PlayerSkillExecutor.cs: 이동·기술 실행 |
| Runtime | MNG_BallControl.cs / MNG_PossessionLedger.cs / MNG_KickSolver.cs: 공 제어·소유·킥 |
| Runtime | MNG_FallbackManager.cs: 기존 Core Fallback 판단을 포팅 |
| Runtime | MNG_HumanInput.cs / MNG_ControlOwnership.cs / MNG_HumanSurrogate.cs: 사람·대역·제어권 |
| Runtime | MNG_RewardEngine.cs / MNG_EventLedger.cs: 감독 보상·사건 중복 방지 |
| Runtime | MNG_ModelRegistry.cs / MNG_HudPresenter.cs: 전술 모델·기존 화면 연결 |
| Runtime | MNG_CurriculumController.cs / MNG_TrainingBootstrap.cs: 단계 설정·훈련 배선 |
| Runtime | MNG_Runtime.asmdef |
| Editor | MNG_ProjectBuilder.cs / MNG_TrainingBuildBuilder.cs / MNG_Validation.cs / MNG_Editor.asmdef |
| Profiles | MNG_Physics.asset / MNG_BaseReward.asset / MNG_AttackReward.asset / MNG_DefenseReward.asset / MNG_PressReward.asset / MNG_CurriculumCatalog.asset |
| Prefabs | MNG_StadiumEnvironment.prefab |
| Scenes | MNG_Stadium4v4.unity |
| Training | MNG_M1_AttackChoice.yaml 등 단계별 PPO YAML |
| Models | MNG_Base-YYYYMMDD-rNNN.onnx 등 최종 모델 |
| Evaluation | MNG_Protocol_v1.md 및 MNG_ 접두사 고정 seed·상황 데이터 |
| Tests/EditMode, Tests/PlayMode | MNG_ 접두사 테스트와 테스트 asmdef |

단계별 Scene은 Curriculum/M0_Technical/Scenes/MNG_M0_Technical.unity처럼 배치한다. M1_AttackChoice, M2_DefenseChoice, M3_FallbackMatch, M4_HumanSupport, M5_SelfPlay, M6_Tactics도 같은 형태다. 모든 Scene은 동일한 환경 Prefab과 단계 Catalog 값을 사용한다.

Manager는 사용자 지정 폴더명이며 분류 폴더는 접두사 예외다. 코드 class/filename, 새 asset/model/BehaviorName에는 MNG_를 붙인다. 기존 Mesh/UI/Texture를 참조만 하면 개명하지 않는다. 기존 serialized script를 이름만 바꿔 GUID를 깨뜨리지 않는다. 새 복사본은 새 GUID, 기존 자산은 기존 GUID를 유지한다.

대용량 생성물은 Assets에 넣지 않는다. 예정 경로는 results/MNG_<run>/, Logs/MNG_<run>/, Builds/MNG_Training/이다. Manager에는 설정·채택 ONNX·평가 프로토콜만 둔다. PT/로그/영상 전량은 Run 폴더에 보존하고 링크한다.

## 2. 인터페이스와 책임

아래는 실재 API가 아닌 구현 목표다. C#, namespace MachineLearning.Soccer.Manager를 사용하고 불필요한 프레임워크를 추가하지 않는다.

| 책임 | 입력/출력과 금지 사항 |
|---|---|
| MatchController | 시간·점수·phase·reset·팀 등록. 공 소유 원장을 중복 소유하지 않음 |
| MatchSnapshot | 같은 tick의 불변 데이터, 고정 slot. 매 프레임 FindObjects 금지 |
| ObservationWriter | snapshot+team→133float. 독립 테스트 가능 |
| ManagerAgent | CollectObservations/WriteDiscreteActionMask/OnActionReceived/AddReward의 유일한 학습 입구 |
| TeamPlanner | snapshot+command+controlMask→PlayerTask[4]. Human에게 작업 전송 금지 |
| SkillExecutor | 작업→move/aim/kick, 취소·만료·발사 token 관리 |
| PlayerMotor | 위치 제어의 유일한 writer, InputOwner 검증 |
| BallControl/PossessionLedger | 실제 접촉→carrier/freeball/kick 사건. 직접 학습 보상 지급 금지 |
| KickSolver | 질량/목표/현재속도→가능 여부·impulse |
| FallbackManager | 기존 Core 판단을 새 snapshot에 포팅. 별도 Rule FSM과 구분 |
| RewardEngine | 사건당 한 번 감독 reward 지급, 선수 reward 없음 |
| HudPresenter | 기존 UI 요소에 MNG 값을 표시. 기존 HUD 동시 활성화 금지 |
| ModelRegistry | hash/contract/라벨/교체. 라벨로 경기 행동을 분기하지 않음 |

PlayerTask 최소 필드는 Skill/Target/ReceiverSlot/Revision/Expiry다. 재사용 buffer를 쓰더라도 snapshot 참조가 소비 중 바뀌지 않도록 한다. 오래된 revision 명령은 executor가 거부한다.

## 3. 순차 실행 티켓

### M0-A 기준 보존·격리 — 첫날 시작

- git status와 기존 변경 목록, Editor/패키지/lock, 원본 Stadium/UI/공/Fallback 함수를 기록한다.
- MNG 전용 asmdef와 Builder 작성. Stadium 표시 부분을 복사/참조하고 새 실행 컴포넌트로 교체한다.
- 비MNG 파일 쓰기 allowlist는 초기에는 비워 둔다. 불가피한 작은 연결만 기록 후 변경한다.
- 완료: 기존 Scene 불변, MNG Scene import/compile 성공, 필드·골문·선수 Geometry 비교 일치.

### M0-B 경기·UI·제어권

- MatchController/HudPresenter/ControlOwnership,300초 경기·3초 pause·reset 구현.
- H 전환과 Human 입력을 먼저 만들고 기존 EnvController/Agent 실행 의존을 제거한다.
- 완료: 입력 writer 하나, H 전환 상태 보존, goalId당 점수1회, reset 참조 누락0.

### M0-C 공과 기술

- 공1.1/1.5 배율, 접촉·드리블·회수·패스·슛을 신규 구현한다.
- legacy Force 값과 접촉 혼합식을 그대로 이어 붙이지 않고 안정적인 목표 속도/방향 제어를 만든다.
- 고정 스크립트로 M0 기술 시험. 제어 결함을 훈련으로 해결하려 하지 않는다.
- 완료: 런타임 물리 조건과 M0 gate, 실제 화면에서 운반·탈취·골 확인.

### M0-D 팀 계획·Fallback

- 6명령/mask/배정/지원/커버, Core Fallback 우선순위를 구현한다.
- 모든 선수가 상시 공에 몰리는지,3/4/5 명령의 실제 차이를 테스트한다.
- 완료: Fallback 대 Fallback 정상 경기 종료와 득점/회수 예시. 아직 RL 성과가 아님.

### M1-A PPO 연결 — 첫날 후반 목표

- ManagerAgent/관측/행동/보상/Bootstrap/YAML 구현, BehaviorName=MNG_Manager.
- 기존 Soccer4v4_Base나 선수 Agent가 trainer에 등록되지 않는지 검사한다.
- smoke에서 decision 증가·optimizer update·checkpoint·ONNX 제어를 확인한다.
- smoke 전용 buffer256/batch64/summary100 설정으로1000decision 안에 update1회 이상을 확인한 후 본 설정으로 새Run 시작한다. smoke 결과를 성능 모델로 쓰지 않는다.

### M1/M2/M3 — 두 번째 날

- 짧은 공격 선택→수비 선택→Fallback 경기 순서. 관측/행동/물리를 유지한다.
- 준비학습에 완벽한 성공률을 요구하며 시연을 무한 연기하지 않는다. gate 실패 시 수정·한정 재시도하고 미달을 알린다.
- 완료: D2 기준, 실제 ONNX 양팀 감독 영상, 초기/최종/무작위 비교표.

### M4 — 3~5일

- 대역은 Human 입력 통로에서만 움직이고 감독 명령을 읽지 않는다.
- mode/control mask 불변, 실제 사람 H키/입력 검증과 구분한다.
- 완료: Human에 대한 AI 명령0, mode 혼합 경기 정상 진행.

### M5 — 6~8일

- 동일 BehaviorName, 감독 TeamId0/1, PPO self_play. 선수 그룹·MA-POCA 사용하지 않음.
- 과거 상대 pool과 고정 Fallback 평가를 보존한다.
- 완료: 상대 snapshot 교체의 실제 증거, 고정 상대 퇴행 점검.

### M6 — 9~11일

- 같은 Base PT에서 Attack/Defense/Press 새Run 각각 생성. reward profile만 다르게 학습한다.
- 최종 경기에서 모델만 교체. executor/물리/관측 불변.
- 완료: 전술별 측정 차이와 기본 경기 품질. 차이가 없으면 미완료.

### M7 — 12~15일

- 보류 seed/상대/좌우/Human/모델 교체 시험, 최종 영상/build/manifest.
- 새로운 기술·관측 추가를 멈추고 결함 수정·평가·인계에 집중한다.

## 4. 작업 의존 규칙

- 변경 범위 확인→구현→관련 테스트→Builder 검증→필요한 Player build→결과 기록 순서.
- 과거 테스트와 새 사양이 충돌하면 과거 테스트를 완화하지 말고 MNG 격리를 확인한다.
- wrapper만 작성하고 기존 자율 선택이 살아 있지 않은지 Scene 컴포넌트 목록으로 검사한다.
- 씬별 코드 복사 금지. 공통 Prefab+Catalog로만 단계 차이를 만든다.
- Player에 Editor 의존이 새지 않는지 첫날 build로 검사한다.
- YAML 주석은 ASCII, 한국어 설명은 Markdown. 설치 버전·CP949 파서 문제를 피한다.
- 2일을 외부 패키지 이식이나 기존 전체 refactor에 사용하지 않는다.

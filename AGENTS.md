# Machine-Learning 에이전트 지침

이 지침은 `C:\GitHub\Machine-Learning`과 그 하위 경로에만 적용한다. 현재 개발 대상은 Unity ML-Agents 기반 Soccer 4v4다.

## 작업 경계

- 프로젝트 파일의 탐색·생성·수정·삭제 대상은 `C:\GitHub\Machine-Learning` 안으로 제한한다. 다른 `C:\GitHub` 프로젝트는 조회하지 않는다.
- 이 프로젝트를 검증하기 위해 설치된 Unity·Python 실행 파일을 호출할 수는 있지만, 설치 폴더나 외부 프로젝트 파일을 수정하지 않는다.
- 사용자 변경을 덮어쓰지 않는다. 삭제가 필요하면 독립된 생성물인지 확인하고 먼저 승인받는다.
- Unity Asset을 이동하거나 이름을 바꿀 때 `.meta`를 함께 이동해 GUID를 보존한다.
- `.gitignore`와 공개 범위는 사용자가 명시적으로 승인한 작업에서만 변경한다.
- 상세 문서를 무조건 전부 읽지 않는다. [문서 색인](docs/README.md)에서 작업 유형에 필요한 문서만 선택한다.

## Asset 경계

- 활성 Soccer: `Assets/_Soccer`
- 보관 자료: `Assets/_Legacy`
- 공통 프로젝트 설정: `Assets/Settings`, `Assets/InputSystem_Actions.inputactions`
- 학습형 담당 폴더: `Attack_KMW`, `Defense_PJH`, `Press_KMG`
- 규칙형 담당 폴더: `Rule_PHC`

담당자 접미사는 물리 폴더명에만 쓴다. `BehaviorName`, 모델 접두사, 클래스·namespace·asmdef의 논리명은 `Attack`, `Defense`, `Press`, `Rule`을 유지한다. Legacy에는 신규 Soccer 기능을 추가하지 않는다.

## Soccer 계약과 담당 경계

경기 시간, 관측, Action Branch, 이동·킥 물리, 소유권·보상 사건 판정, 센서, 골키퍼 보호, Reset과 경기장은 공통 계약이다. 한 팀 전용으로 분기하거나 자기 Prefab만 바꾸지 않는다.

- 런타임 계약: [경기 계약](docs/soccer/gameplay-contract.md)
- 코드와 Prefab 흐름: [아키텍처](docs/soccer/architecture.md)
- 학습형 수정 범위: [학습형 팀 가이드](docs/soccer/training/learning-teams.md)
- Rule 수정 범위: [Rule 팀 가이드](docs/soccer/training/rule-team.md)
- Asset 이동 규칙: [Asset 구조](docs/project/asset-layout.md)

공통 경기장 변경은 `SoccerProjectBuilder`를 통해 공용 Template과 Base·Attack·Defense·Press·Rule 환경 Prefab 5개에 함께 적용하고 parity 검증을 통과시킨다.

## 보상 변경 규칙

보상 종류·수치·판정·주기·거리·상한을 바꾸면 같은 작업에서 다음을 모두 갱신한다.

1. 코드 또는 `*RewardProfile.asset`
2. [보상 기준표](docs/soccer/rewards.md)
3. 해당 팀 README와 관련 테스트
4. 현재 상태 또는 실험 기록

결과에는 `기존 값 / 변경 값 / 변경량` 표를 반드시 포함한다. 새 보상은 기존 값을 `없음(0)`으로 적는다. Rule의 보상은 학습 신호가 아니라 HUD·통계용이며, Rule 행동은 FSM에서 수정한다.

## 검증과 기록

- 변경 전후 `git status`를 확인하고 관련 정적 검사, Unity compile, Builder Validate, EditMode·PlayMode 중 가능한 검증을 수행한다.
- Unity 실행 뒤 자동 생성된 `.meta`, Scene, Prefab과 ProjectSettings의 예상 밖 변경을 확인한다.
- 최신 결과와 미검증 항목은 [현재 상태](docs/soccer/current-status.md)에만 기록한다.
- 완료된 작업과 과거 기획은 `docs/archive`에 두며, 일반 작업에서는 기본 Context로 읽지 않는다.
- 검증 명령과 라이선스 우회는 [설정 및 검증](docs/project/setup-and-validation.md)을 따른다.

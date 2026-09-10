# Machine-Learning 에이전트 지침

이 지침은 `C:\GitHub\Machine-Learning`과 그 하위 경로에만 적용한다. 현재 개발 대상은 Unity ML-Agents 기반 Soccer 4v4다.

## 작업 경계

- 2026-09-07 사용자 중단·r017 종료: r017은500,248에서 exit0 자연 종료했고 PT/ONNX/config/log/inspect/SHA를 `Logs/CurriculumL2Find-20260907-r017-result`에 보존했다. 최종 균일360도 최근5요약 성공·소유98.61/98.36/97.14/98.59/98.72%,3.26~3.91초로99% gate 미달이며 고정300 승급평가는 열지 않았다. 60도 단계는 연속100%였고140도 확장 시 SideBlind81.82%로 하락해 초기 방향 병목은 재현됐지만 최종 희귀 실패는 제거하지 못했다. r017은 미승인이다. 사용자 지시에 따라 추가 버전·학습·L2-Score를 시작하지 않고 개발을 중단한다.
- 2026-09-07 r016 중단·r017 방향 커리큘럼 원인수정: r016은 무의미한 연장을 피하려고252,928 checkpoint 저장 후 정상 중단했다. 최근5요약95.83/100/96.77/96.61/98.59%로 분할보상도99% gate를 만들지 못했다. 고정 r014300의 성공 episode 최초 주 추격자 방향오차 평균45.65도와 실패116.72도, 실패의 최선 접근 시점108.29도를 비교하면 병목은 학습량이나 골키퍼 경계가 아니라 초기 측면 방향의 회전·접근이다. 전방 Ray는 +/-60도, 후방 Ray는180도 기준 +/-45도라 좌우60~135도에 측면 사각 구간이 있고, vector 공 offset은 월드축인데 이동은 몸축이며 몸 방향 vector가 없다. r017은 관측/action 계약과 보상을 바꾸지 않고 최대 최초 방향오차를60->100->140->180도로 넓혀 마지막250k를 기존과 동일한 균일360도 분포로 학습한다. r014300,060에서 optimizer 없이16workers/500k 한정하며 실제접촉/15초/추격자2명 각0.5/코드 행동개입0은 유지한다. 검증·빌드 전 학습 금지, L2-Score 금지.

- 2026-09-07 r015 종료·r016 분할보상 단일가설: r015는300,112에서 자연 종료했으나 최종5요약 성공95.16/92.00/95.77/90.74/100%로99% gate 미달이다. 새seed31415934 균일300은 r014와 r015 모두291/300=97%여서 두 추격자 전액 보상은 성공률을 개선하지 못했고, 실패 시 두 번째 추격자 최선거리도 r0145.37m에서 r01510.54m로 악화됐다. 두 번째 Neural 추격자 개념은 유지하되 r016은 두 선수의 개인 거리·방향 potential을 각각0.5배로 나눠 합산 개인 shaping 예산을 기존 한 명 수준으로 고정한다. team·terminal 보상, timeout`-0.5`, 실제접촉/15초/균일배치, lr/beta, 코드 행동개입0은 불변이다. r015가 아니라 r014300,060에서 optimizer 없이16workers/300k로 한정한다. 검증과 빌드 전 학습 금지, L2-Score 금지.
- 2026-09-07 r014 종료·r015 두 추격자 단일가설: r014는300,060에서 자연 종료했으나 최종 균일5요약 성공100/96.55/100/96.67/98.59%로99% gate 미달이다. 최종 checkpoint 새seed 균일100은99%였지만 학습 gate를 대체하지 않는다. 유일한 실패의 최초 최근접 역할은 Midfielder였고 DefenderKeeper 최근접8경기는 전부 성공해 골키퍼 경계만의 실패 증거는 없었다. 다만 기존 코드가 최초 최근접1명에게만 개인 거리·방향 신호를 주어 나머지 선수가 기다릴 수 있으므로 r015는 최초 최근접2명 각각에게 동일한 signed 거리·방향 potential을 준다. timeout은`-0.5`, 실제접촉/15초/균일배치/다른 보상·관측·행동은 유지하며 코드 이동·회전·속도 개입0이다. r014300,060에서 optimizer 없이16workers/300k로 한정한다. 미실행 timeout강화 가설과 실패 검증은 `Logs/L2-Find-r015-timeout-hypothesis-superseded`에 보존한다. 검증과 빌드 전 학습 금지, L2-Score 금지.
- 2026-09-07 사용자 중단 인계: `CurriculumL2Find-20260907-r014`는 검증 후16workers로 시작했으나 사용자 요청 즉시 정상 Ctrl+C로 **656 step** PT·ONNX를 저장하고 Trainer/Player/Unity가 모두 종료됐다. 재개할 때는 초기화 YAML이나 `Train-Soccer.ps1 -InitializeFrom`을 다시 쓰지 말고, `Logs/Resume-CurriculumL2Find-20260907-r014.ps1`의 init_path 없는 resume YAML과 `--resume`으로 동일 optimizer·seed·총300k를 이어간다. 최근5요약은 아직 생성되지 않아 승인 판단 전이다. L2-Score를 시작하지 않는다.
- 2026-09-07 최신 사용자 승인: L2-Find만 계속해 강화 모델이 최근5요약과 독립 고정300 성공99%를 통과하면 짧은 Editor 실행 가이드를 제공하고 작업을 마친다. L2-Score로 넘어가지 않는다. r012 terminal 보강과 r013 entropy 단독 변경은 악화되어 폐기했다. r014는 r008 보상·lr/beta를 유지하고 진행35%까지만20% Near rehearsal 후 균일분포로 돌아간다. 32workers는 RAM89%·여유3.42~3.45GiB가 반복되어 문서화된 안전 기준에 따라16workers로 축소하며 aggregate step과 계약은 유지한다. 코드 이동·회전·속도 개입은0회다. 골키퍼 공통 범위 Home`±28m`, Engage`±36m`는 유지한다.
- 2026-09-06 최신 사용자 승인: 승인된 L2 r018 뒤에 `L2-Score`를 필수 중간 단계로 추가하고, 고정 진단에서 무작위 공 탐색이 부족하면 `L2-Find`를 먼저 학습한다. L2-Score는 Navy를 비활성화한 빈 경기장, 30초 단일 득점 에피소드, Red 득점 성공/자책골 즉시 실패, 고정300경기 성공95% 이상과 자책골0건을 완화 없이 요구한다. 이를 통과하고 L0/L1/L2 회귀를 확인한 뒤 L3를 다시 진행한다.
- 2026-09-05 최신 사용자 승인: L2 정식 승인과 결과 보존 후 L3 개발·학습·평가까지 자율 진행한다. 이전 오전5시 진입 제한은 해제됐으며 L2 성공50% 및 최종 고정평가/회귀는 유지한다. 신규 학습은 32개 환경 병렬(`--num-envs 32`, 기본 NumEnvs=32)이다. 과거 Run의16workers 기록은 당시 이력으로 보존한다.
- 야간 작업은 원인 진단→의미 있는 수정→관련 문서/테스트/Unity검증/빌드→한정학습→고정평가를 반복한다. 실제 Codex 사용량은 실행과 주요 경계에서 확인한다. **주간 잔여량이2% 이하 또는5시간 잔여량이5% 이하**가 되면 정확한 인계를 저장하고 중단한다. 사용자가 별도로 지시하지 않으면 재개 자동화를 만들지 않는다. 크레딧은 사용자 지시 없이 사용하지 않는다.

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

# 작업 기록

> 보관 상태: 2026년 구현 과정의 상세 Log  
> 현행 상태: [Soccer 현재 상태](../../soccer/current-status.md)  
> 일반 작업에서는 이 전체 파일을 Context로 읽지 않는다.

> 경로 기록 원칙: 아래 `Assets/Soccer`, `Assets/Escape`, `config/Turtle.yaml`과 Root `results`는 당시 위치다. 현재 위치는 `Assets/_Soccer`, `Assets/_Legacy/Escape`, `Assets/_Legacy/Turtle/Training~/Config/Turtle.yaml`, `Assets/_Legacy/Turtle/Training~/Results`다. Escape 문서는 [prototype](../legacy/escape-prototype.md)과 [performance](../legacy/escape-performance.md)에 보관한다.

## 2026-07-18 — Escape 건물 높이 및 적 속도 조정

### 요청

- 건물 높이를 기존의 절반으로 낮추고, 적 캐릭터의 이동 속도를 약 10% 낮춘다.

### 변경

- 건물 높이를 `12m`에서 `6m`로 변경했다.
- 적 이동 속도를 `4.5m/s`에서 `4.05m/s`로 변경했다(정확히 10% 감소).
- 버튼 소켓의 세로 위치를 건물 높이에 비례하도록 변경해, 이후 건물 높이 조정에도 벽면의 같은 상대 위치를 유지하게 했다.

### 검증 결과

- Escape 생성 검증 통과: 건물 25개, SpawnPoint 36개, Button 5개, Gate Anchor 4개, Player 1명, Enemy 3명.
- Windows Development Build 성공: `Builds/Escape/EscapeTraining.exe` (207,819,197 bytes).
- Unity 명령행 EditMode 테스트는 초기화 뒤 테스트 실행 없이 종료되어 결과 XML을 생성하지 못했다. 빌드 생성기의 검증과 생성 프리팹 값 확인으로 이번 조정값을 확인했다.

## 2026-07-18 — 프로젝트 작업 기반 구성

### 요청

- 작업 및 조회 경계를 프로젝트 지침으로 만든다.
- 개인용 문서 체계를 만들고 GitHub 업로드를 막는다.
- 기존 목표 찾기 임시 에셋은 조회만 하고 보호한다.
- `Assets/Escape`, `Assets/Soccer` 폴더를 만든다.
- 현재 프로젝트의 기술 상태를 문서화한다.

### 확인한 내용

- 프로젝트 Unity 버전: `6000.3.16f1`
- Unity ML-Agents 패키지: `4.0.3`
- 기존 목표 찾기 구현: Basic 벡터 관측 방식과 Ray Sensor 방식
- 기존 학습 설정: `config/Turtle.yaml`, PPO, 최대 1,000,000 스텝
- 읽기 전용 참고 폴더에 공식 학습 문서와 Soccer 예제가 존재함
- 작업 전부터 `Assets/ML-Agents/Timers/Scene2 Ray_timers.json.meta`가 Git 미추적 상태였음

### 수행한 작업

- 루트에 `AGENTS.md` 작성
- `docs` 및 `docs/archive` 생성
- 목표, 계획, 기술 현황, 세부 설계, 결정, 작업 기록 문서 작성
- `.gitignore`에 `/docs/` 추가
- `Assets/Escape`, `Assets/Soccer` 생성

### 보호 조치

- 기존 `Assets` 파일은 조회만 했고 수정하지 않음
- `C:\GitHub\ml-agents`는 조회만 했고 수정하지 않음
- 허용된 두 폴더 외의 `C:\GitHub` 하위 폴더는 조회하지 않음
- 기존 Git 미추적 파일을 변경하거나 삭제하지 않음

### 남은 확인

- Unity를 다음에 열었을 때 신규 폴더용 `.meta` 파일이 정상 생성되는지 확인
- Python trainer 설치 버전과 Unity 패키지 `4.0.3`의 호환성 확인 — 이후 2026-07-18 재확인 작업에서 완료
- 다음 사용자 메시지에서 Escape와 Soccer의 상세 기획 확정

### 다음 작업

사용자의 게임 기획을 `04-design-details.md`에 반영하고, 결정된 내용에 따라 게임별 최소 학습 환경의 관측, 행동, 보상, 종료 조건을 명세한다.

---

## 2026-08-10 — 학습형·규칙형 담당자 유지보수 매뉴얼

### 요청

- 서로 다른 담당자가 학습형과 규칙형을 계속 개선할 때 수정 가능·금지 영역을 분리한다.
- 보상, 공통 규칙, 팀별 규칙과 학습 전에 손댈 위치를 상세하고 쉽게 안내한다.
- 별도 매뉴얼을 `docs`에 만들고 같은 경계를 코드에도 짧게 남긴다.

### 구현

- `15-soccer-learning-team-maintainer-manual.md`에 Base·Attack·Defense·Press 작업공간, Profile·YAML·ONNX 수정 범위, 보상 필드·상한·부호, 변경 기록 양식, 학습 전·중·후 절차와 완료 체크리스트를 작성했다.
- `16-soccer-rule-team-maintainer-manual.md`에 FSM 상태·전이·목표·조향의 수정 범위, 공통 행동 계약과 골키퍼 보호, Rule 보상이 행동이 아닌 HUD·통계용이라는 구분, 성능·회귀 체크리스트를 작성했다.
- 보상 변경 시 `docs/14`, 팀 README, 작업 로그, 테스트와 이전값·새 값·변경량 표를 같은 작업에서 갱신하도록 두 매뉴얼에 명시했다.
- 공통 Core, TeamDefinition, MatchSetup, Builder, 팀별 RewardPolicy, Rule FSM과 다섯 Trainer YAML에 수정 경계를 설명하는 짧은 주석을 추가했다.
- 중요 결정 D-025와 문서 인덱스, 현재 현황을 갱신했다.

### 검증

- 신규 문서 내부 링크 해석, 제목 구조, placeholder 검사 통과
- 다섯 Trainer YAML의 기존 Behavior 키 보존 확인
- `git diff --check` 통과. 기존 줄끝 변환 경고 외 오류 없음
- Unity 전체 C#·테스트 어셈블리 컴파일 성공: `Logs/Soccer-Compile.log`
- Builder `ValidateBatch`는 Unity Licensing Client가 `com.unity.editor.headless` 채널을 반복해서 찾지 못해 메서드 진입 전 중단: `Logs/Soccer-Maintainer-Validate.log`

### 보상 변경

- 없음. 보상 종류, 수치, 부호, 판정 조건, 지급 대상과 상한을 변경하지 않았으므로 `docs/14-soccer-reward-reference.md`의 실제 보상 표는 그대로 유지한다.

---

## 2026-08-09 — 5분 경기·Human 이동·골키퍼 복귀·동료 과밀·HUD 개편

### 현행 감사

- Human은 `DecisionPeriod=1`로 물리 고정 스텝 `0.02초`, 즉 50Hz마다 입력과 행동이 적용되고 있었다. 따라서 5스텝 행동 반복은 Human 끊김의 원인이 아니었다.
- 직접 원인은 선수 Rigidbody 보간이 꺼진 상태에서 LateUpdate 카메라가 20ms 단위 위치를 추적한 것과, 매 스텝 `VelocityChange`를 누적해 약 4틱 만에 최대 평면 속도에 도달하는 이동 방식이었다.
- 수비·골키퍼 겸임 역할은 Neural 행동, 모델 없는 fallback과 Rule FSM 어디에도 하프라인 최종 제한이나 불리한 전환 복귀 보장이 없었다.
- 기존 포메이션 보상은 밀집 자체에 음수 보상을 주지 않았고, 한 쌍의 간격 개선만으로는 포메이션 개선 문턱 `0.08`을 넘기 어려웠다.

### 구현

- 경기 시간을 코드 기본값, Builder 생성·검증, 공용 템플릿과 Base·Attack·Defense·Press·Rule 프리팹, HUD 초기 시계와 테스트에서 `300초 / 05:00`로 통일했다.
- Human만 목표 평면 속도 방식으로 변경했다. 가속은 `32m/s²`, 입력 해제·역방향 감속은 `48m/s²`이며 게임패드 트리거와 스틱의 아날로그 값을 보존한다.
- Human 전환 시 해당 선수 Rigidbody만 `Interpolate`, AI 복귀 시 `None`으로 되돌린다. AI·Rule·학습의 기존 행동 반복과 이동 물리는 유지해 반복 학습 비용을 늘리지 않았다.
- 수비·골키퍼 겸임 선수의 공통 action shield를 추가했다. 공격 깊이 `-4m`부터 공격 방향 성분을 완만하게 줄이고 하프라인 `0m`에서 공격 힘과 속도를 차단한다.
- 상대 소유·마지막 상대 터치 또는 자기 골문 위협 시 시작 골문 깊이로 최대 속도 복귀한다. Rule은 최소 상태 유지 시간을 건너뛰는 `RecoverGoal`을 사용하며 먼 공의 추격자 후보에서 제외된다.
- 같은 팀 최단 거리 `3m` 미만을 `0.75초`마다 평가해 팀 과밀 패널티를 지급한다. `3m→1m`에서 심각도 `0→1`, 한 샘플에는 가장 심한 한 쌍만 반영하고 팀별 경기 상한을 별도로 둔다.
- Rule 팀은 `3m` 안 동료에게 allocation 없는 Reynolds separation 조향을 적용한다.
- 우측 상단 중복 조작 힌트를 제거하고 좌측 하단을 중앙 정렬 무테 3열 표로 변경했다.

### 보상 변경량

| 보상 항목 | 기존 | 변경 | 변경량 |
| --- | ---: | ---: | ---: |
| 동료 과밀 명목 패널티 | 없음 `0` | `-0.0025×심각도` | 샘플당 최대 `-0.0025` |
| 평가 주기 | 없음 | `0.75초` | 신규 |
| 거리 기준 | 없음 | `3m→1m`, 심각도 `0→1` | 신규 |
| 다중 쌍 처리 | 없음 | 가장 심한 한 쌍만 | 샘플 최대 1회 |
| 팀별 경기 절대값 상한 | 없음 `0` | `0.25` | 경기당 최대 `-0.25` |
| 포메이션 보상 배율·상한 | 기존 전술별 값 | 동일 | `0` |
| 골키퍼 역할 위치 평가 | 상황별 넓은 전진 허용 | 소유 시 `-4m` 만점·`+4m` 0점, 비소유 시 홈 깊이 `6m` 만점·`30m` 0점 | 금액 `0`, 판정 기준 변경 |
| 그 외 보상 | 기존 값 | 동일 | `0` |

전체 행동별 보상 표와 실제 판정·상한은 `14-soccer-reward-reference.md`에 동기화했다. Base·Attack·Defense·Press·Rule 5개 프로필은 모두 과밀 명목값 `0.0025`, 경기별 절대값 상한 `0.25`를 사용한다.

### 검증 결과

- Unity 런타임·Editor·EditMode·PlayMode 테스트 어셈블리 컴파일 성공: `Logs/Soccer-Compile.log`.
- `SoccerProjectBuilder.BuildAllBatch` 생성과 공용 템플릿+5개 독립 프리팹 parity 검증 성공: `Logs/Soccer-Generate.log`.
- 공용 템플릿과 5개 프리팹의 `matchDurationSeconds: 300`, 5개 RewardProfile의 `0.0025 / 0.25`, 여섯 씬의 Human `32 / 48` 직렬화 값을 확인했다.
- HUD UXML XML 파싱, Soccer asmdef JSON 파싱과 `git diff --check`를 통과했다.
- 신규 테스트에는 5분 시계·프리팹 시간, Human Interpolate 전환, 과밀 무소유권 샘플, 최악 한 쌍, 팀별 cap·`ResetMatch`, Rule separation, 골키퍼 팀 대칭·하프라인 shield 단언을 추가했다.
- Unity Test Runner 실행은 테스트 수집 전에 Licensing Client가 `com.unity.editor.headless` 채널을 찾지 못해 중단됐다. 이는 컴파일이나 테스트 단언 실패가 아니며 결과 XML은 생성되지 않았다: `Logs/Soccer-Gamefeel-EditMode.log`.

---

## 2026-08-03 — Soccer 4v4 v2 팀 학습 작업공간 구현

### 변경

- 행동 계약을 `[3,3,3,3]`으로 올리고 킥 없음/제어 킥/강한 킥을 명시적인 네 번째 브랜치로 분리했다.
- 역할을 수비/골키퍼 겸임 1명, 자유 미드필더 2명, 스트라이커 1명으로 변경했다.
- 공통 보상을 GoalResult, MatchResult와 보조 보상 6종으로 정리하고 사건 추적, 상한, 항목별 통계를 추가했다.
- 포메이션 평가는 고정 좌표 대신 간격, 패스 지원, 상대 소유 시 후방 커버를 사용하며 미드필더에는 고정 앵커를 두지 않았다.
- 공통 Core와 Attack, Defense, Press, Pass 작업공간을 만들고 팀별 코드, 프로필, 씬, YAML을 생성했다.
- 네 팀의 초기 RewardProfile 값은 Base와 같고, 각 팀 씬의 Purple은 Base, Blue는 해당 팀 정의를 사용한다.

### 검증

- Unity 스크립트 컴파일 성공.
- `SoccerProjectBuilder.BuildAllBatch` 생성 검증 성공: 8명, 역할 분포, v2 Action Spec, Vector Observation 43, 팀 작업공간과 씬 확인.
- Conda `mlagents` 환경과 `mlagents-learn --help` 실행을 확인했다.
  - 팀별 RewardProfile 11개 값의 Base 동일성, Blue/Purple 정의 연결 및 YAML 6개 문법을 정적 검증했다.
  - 후속 Test Runner와 Windows Build는 Unity Licensing Client에서 `com.unity.editor.headless` 권한을 얻지 못해 보류했다.
  - 일반 Editor 자동 Play 방식도 라이선스 채널 연결 전에 멈췄고, 트레이너는 62.8초 뒤 0스텝으로 종료했다. 새 ONNX는 생성되지 않았다.

### 다음 작업

- Unity Hub에서 라이선스 연결을 복구한 뒤 EditMode/PlayMode 테스트와 Base Windows Build를 실행한다.
  - 3만 스텝 임시 Base Self-Play를 20분 미만으로 수행하고 `Base4v4V2.onnx`를 설치한다.

---

## 2026-07-18 — Escape 프로토타입 상세 기획

### 요청

- `Assets/Escape`에서 만들 탈출 게임의 Unity 제작 전 규모와 기능 범위를 상세히 계획한다.
- 5×5 건물 맵, Player/Enemy, 전투, Button/Gate, Randomization, UI Toolkit 요구를 학습 가능한 명세로 바꾼다.
- 로컬 ML-Agents 문서와 DungeonEscape 예제를 우선 참고한다.
- 이번 작업에서는 계획만 완성하고 씬, 프리팹, 코드는 다음 명령부터 만든다.

### 확인한 내용

- 실제 대상 폴더는 `C:\GitHub\Machine-Learning\Assets\Escape`이며 현재 비어 있다.
- 프로젝트는 Unity `6000.3.16f1`, ML-Agents Unity Package `4.0.3`을 사용한다.
- 현재 Package의 Behavior Type, DecisionRequester, Ray Sensor, SimpleMultiAgentGroup API를 확인했다.
- 로컬 문서에서 비대칭 Team, 협력 Group, MA-POCA, Self-Play의 조합 규칙을 확인했다.
- DungeonEscape의 Controller Reset, Group 등록/종료, Ray 관측, Discrete Action, `MaxStep: 0`, `DecisionPeriod: 5` 패턴을 확인했다.
- 당시 비활성 PowerShell PATH에서는 Python Launcher와 `mlagents-learn`이 바로 검색되지 않았다. 이후 준비된 Conda 환경의 정확한 경로와 기존 학습 기록을 확인했다.

### 수행한 작업

- `07-escape-prototype-spec.md`에 게임 루프, 맵 치수, 이동/공격, Episode, Randomization, ML 관측/행동/보상, UI, 에셋 구조, 구현 단계와 테스트 기준을 작성했다.
- `01-goals.md`, `02-plan.md`, `03-technical-baseline.md`, `04-design-details.md`, `05-decisions.md`를 새 Escape 기획과 연결했다.
- 씬, 프리팹, 코드, Material, ProjectSettings는 변경하지 않았다.
- `C:\GitHub\ml-agents`는 읽기만 했고 변경하지 않았다.

### 남은 결정

- 제안 상태인 맵 치수, 공격 거리와 보상값은 첫 Heuristic 검증 후 조정 가능
- 준비된 Python 환경을 다음 구현의 Trainer 연결 Smoke Test에 사용

### 다음 작업

사용자 승인 후 `07-escape-prototype-spec.md`의 M1~M4를 따라 `Assets/Escape` 안에 새 Scene, Prefab, Script, UI Toolkit 에셋, Trainer 설정과 Test를 만들고 학습 직전 상태까지 검증한다.

---

## 2026-07-18 — 학습 환경 재확인과 Ray 병렬 성능 검토

### 요청

- 주요 프로젝트가 `C:\GitHub\Machine-Learning`이고 Python 학습 환경이 이미 준비됐다는 사실을 다시 확인한다.
- RTX 4060 Gaming Laptop에서 Ray Sensor를 사용하는 Arena 16개가 적절한지 계산한다.
- 더 낮은 사양 PC를 위한 부하 조정 기준을 정한다.
- Gate가 선택된 외곽 벽의 중앙에 설치된다는 결정을 반영한다.

### 확인한 내용

- Conda 환경: `C:\Users\USER\miniconda3\envs\mlagents`
- Python `3.10.12`, `mlagents`와 `mlagents-envs 1.2.0.dev0`
- PyTorch `2.2.2+cu121`, CUDA 사용 가능, RTX 4060 Laptop GPU 8GB 인식
- CPU i7-14700HX 28 logical processors, RAM 약 32GB
- 기존 Turtle 학습 결과가 동일 Conda 환경의 `mlagents-learn`으로 생성됨
- Arena 16개, Agent 64개, Ray 25개, Decision 10Hz 기준 시뮬레이션 초당 Raycast 약 16,000회
- 원시 관측량은 시뮬레이션 초당 약 0.505MiB로 작고, 주된 위험은 GPU가 아니라 Time Scale에 따라 증가하는 CPU Physics 부하임

### 결정과 문서 변경

- Python 준비 미완료라는 이전 결론을 정정했다.
- Gate를 네 외곽 벽 중 선택된 벽의 중앙에 두는 것으로 확정했다.
- `08-escape-training-performance.md`에 현재 PC, 16 Arena 계산, 낮은 사양 시작값, 축소 순서와 Benchmark 절차를 기록했다.
- Arena 16개는 타당한 초기 목표지만 구현 후 8개와 처리량을 비교해 최종 기본값을 결정한다.
- 현재 ML-Agents 4.0.3의 `TrainingAreaReplicator`와 `mlagents-learn --num-areas` 지원을 확인해 Arena 복제 방식으로 채택했다.

### 다음 작업

Escape 구현 시 `TrainingAreaReplicator`와 `--num-areas`로 Arena 수를 설정하고, Headless/Batched Raycast 경로를 포함해 학습 Build에서 1/4/8/16 Arena 처리량을 측정한다.

---

## 2026-07-18 — Escape M1~M4 구현 및 사전 학습 검증

### 수행한 작업

- `Assets/Escape`에 `EscapePrototype` 씬, Environment/Building/Button/Gate/Player/Enemy 프리팹과 전용 Material을 생성했다.
- 96×96m 맵, 5×5 건물, 36 SpawnPoint, 외곽 네 벽 중앙 Gate Anchor와 매 Episode Randomization을 구현했다.
- Rigidbody 이동, W/S 전후진, A/D 회전, 마우스 3인칭 카메라, 체력 3, 3초 무적과 적 근접 공격을 구현했다.
- 버튼 5개, 3개 조건 Gate, 120초 제한, 승패·Floor 색·Reset과 UI Toolkit HUD를 구현했다.
- Player/Enemy Agent, `[3,3]` Discrete Action, Vector Observation 7, Ray 25개와 여섯 Tag를 구성했다.
- Enemy 3명을 동일 Behavior/Team과 `SimpleMultiAgentGroup`으로 연결하고 Player PPO, Enemy MA-POCA 설정을 작성했다.
- `TrainingAreaReplicator`와 CLI `--num-areas`를 사용하는 Headless 병렬 Arena 경로를 구성했다.
- `EscapeProjectBuilder`에 에셋 생성·구조 검증·Windows Build 자동화 메뉴와 Batch 진입점을 만들었다.

### 검증 결과

- 생성 검증: Building 25, SpawnPoint 36, Button 5, Gate Anchor 4, Agent 4 통과
- EditMode: 3/3 통과
- PlayMode: 3/3 통과. 100개 Seed에서 캐릭터 Spawn 중복, Button Building 중복, Gate 범위와 상태 누수 없음
- Windows Development Build: `Builds/Escape/EscapeTraining.exe`, 약 207.8MB
- Headless Smoke: `-escapeMode human -escapeSmokeSeconds 2`, 경고·예외 없이 `Running` 상태에서 자동 종료
- 준비된 Python 환경에서 두 YAML 파싱 통과: Player `ppo`, Enemy `poca`, `num_areas=16`, `num_envs=1`

### 발견하고 수정한 문제

- Gate Trigger 컴포넌트를 독립 스크립트로 분리해 Prefab 직렬화 오류를 해결했다.
- `BehaviorParameters` 추가 순서가 기본 `Agent`를 자동 생성해 캐릭터마다 Agent가 중복되던 문제를 수정했다. 최종 Environment는 Agent가 정확히 네 개다.
- Python 설정 키를 현재 `mlagents 1.2.0.dev0`이 요구하는 `env_settings`로 수정했다.
- UI Toolkit 기본 Theme를 `Assets/Escape/UI/EscapeRuntimeTheme.tss`로 격리했다.

### 다음 작업

실제 학습을 시작한다. Player PPO부터 짧은 Trainer 연결 Smoke를 수행한 뒤 1/4/8/16 Arena 처리량을 비교하고, 안정화되면 긴 Player 학습과 Enemy MA-POCA 학습으로 진행한다.

---

## 2026-07-18 — 방향 표시, I 키 전환과 도로 폭 조정

- Player 정면에 청록색, Enemy 정면에 노란색 `FacingMarker` 큐브를 추가했다. 충돌에는 관여하지 않고 캐릭터의 로컬 +Z 방향을 표시한다.
- UI Toggle의 마우스 입력을 비활성화하고 `I` 키 입력을 Player Agent에서 받아 Human/AI 모드를 전환하도록 변경했다.
- 건물 크기를 12×12×12m에서 11×12×11m로 줄이고 중심 간격 18m를 유지해 도로 폭을 6m에서 7m로 늘렸다.
- Button Socket도 새 건물 면 위치에 맞춰 자동 계산하도록 변경했다.
- EditMode 3/3, PlayMode 3/3, 생성 검증, Windows Build와 Headless Smoke를 다시 통과했다.
## 2026-07-18 — Soccer 4v4 프로토타입 구현 및 검증

- ML-Agents `SoccerFieldTwos`, 공, 메시, 재질, Ray Sensor, `SoccerTwos.onnx`를 `Assets/Soccer` 아래에 복제했다.
- 원본 Field X/Z를 4배로 확대하고 Ray 길이를 80으로 조정했으며, Blue/Purple 각 4명과 고정 킥오프 좌표를 생성했다.
- 팀별 `SimpleMultiAgentGroup`, Group Reward, 180초 경기, 득점 후 3초 Goal Pause와 결정론적 전체 복귀를 구현했다.
- Blue 1번 선수에 WASD 및 Xbox Trigger/Left Stick 입력을 연결하고 H/Y Human-AI 전환을 구현했다.
- Escape HUD와 같은 UI Toolkit 방식으로 점수, 시계, AI/Human ON/OFF, 득점·종료 메시지를 구성했다.
- MA-POCA + Self-Play 훈련 YAML, 반복 생성/검증 Editor Builder, EditMode/PlayMode 테스트를 추가했다.
- Unity 생성 검증 통과, EditMode 5/5 통과, PlayMode 2/2 통과.
- 상세 명세는 `09-soccer-prototype-spec.md`에 기록했다.

---

## 2026-08-03 — Soccer 선수 받침대와 킥 구현

### 수행한 작업

- 모든 Blue/Purple 선수 정면에 중앙 바와 첨부 도면처럼 좌우 바깥쪽으로 펼쳐진 날개를 갖춘 `KickPlate`를 추가했다.
- 받침대 높이를 선수 높이의 30%로 만들고, 세 Collider를 선수 Rigidbody의 복합 Collider와 같은 팀 Tag로 구성했다.
- `Space`와 Xbox `A` 입력으로 0.08초 동안 빠르게 전진하고 0.5초 동안 줄어든 뒤 즉시 다시 사용할 수 있는 킥을 구현했다.
- 킥 힘을 `2000`에서 `5000`으로 높여 공을 더 빠르고 멀리 밀며, 평상시 접촉에는 약한 드리블 힘을 적용했다.
- AI가 공을 정면 가까이 두고 전진할 때 자동 킥하도록 기존 `[3,3,3]` Action Spec 안에서 연동했다.
- `SoccerPlate1`~`SoccerPlate8` Layer를 배정하고 각 Ray Sensor에서 자기 받침대 Layer만 제외했다. 자기 받침대는 관통 관측하고 다른 선수 받침대는 팀 Tag로 감지한다.
- `KickPlateBlue.mat`, `KickPlatePurple.mat`을 `Assets/Soccer/Materials`에 생성하고 프리팹과 씬을 재생성했다.
- 득점 후 라운드 복귀 및 Episode 시작 시 받침대 상태도 접힌 준비 상태로 초기화한다.

### 검증 결과

- Unity 스크립트 컴파일 및 `SoccerProjectBuilder.BuildAllBatch` 생성 검증 통과.
- 생성기 검증에서 받침대 8개, 고유 Layer 8개, 부품 3개, 높이 30%, 소유자/팀 Tag, 자기 제외·타인 포함 Ray Mask를 확인했다.
- EditMode 8/8 통과: `Logs/Soccer-KickPlate-EditMode.xml`.
- PlayMode 6/6 통과: `Logs/Soccer-KickPlate-PlayMode.xml`. 바깥쪽 날개 형상, 시간 기반 전진·0.5초 복귀 및 센서 Layer 규칙을 확인했다.

---

## 2026-08-09 — Soccer 전술·보상·Rule 기획 전환

### 확정한 방향

- 최종 게임을 Soccer 4v4 한 종류로 확정하고 Escape 개발, 학습, Benchmark와 빌드를 종료했다.
- 기존 Escape 구현 자산과 작업 이력은 삭제하지 않고 역사 자료로 보존하기로 했다.
- Attack, Defense, Press를 최종 별도 팀이 아니라 초기 전문 학습 공간과 감독용 ONNX 전술로 정의했다.
- 최종 게임에서 플레이어가 감독이 되어 경기 중 공격형·수비형·압박형 모델을 교체하는 방향을 확정했다.
- Pass 학습 계획을 폐기하고 FSM과 Raycast만 사용하는 `Rule` 비교 팀으로 교체했다.
- Rule은 Trainer YAML, ONNX와 모델 자동 할당에서 제외하고 경기 중 다른 전술로 변경하지 않기로 했다.

### 행동과 역할

- 팀당 수비·골키퍼 겸임 1명, 자유 미드필더 2명, 스트라이커 1명을 유지했다.
- 행동 계약 v2 `[3,3,3,3]`을 유지했다.
- 제어 킥을 `1500`, 강한 킥을 `4000`으로 변경했다.
- 사람 입력을 키보드 `E`/`Space`, Xbox `A`/`B`로 확정했다.

### 보상 재검토

- 안정적 소유권을 `0.75초` 제어와 공-선수 거리 `4m` 이하로 확인하도록 강화했다.
- 기존 사건에 전진 패스, 서로 다른 세 선수의 3인 연계, 간격을 둔 협력 압박을 추가했다.
- Base 일반 패스를 `0.005`, 드리블 개인 보상을 `0.01`, 포메이션 배율을 `0.01`로 낮췄다.
- 일반 패스, 전진 패스, 3인 연계, 포메이션과 전체 shaping에 소유권당 상한을 적용했다.
- 전체 보조 보상 상한은 소유권당 `0.12`, 3분 경기당 `0.5`로 정했다.
- Attack, Defense, Press의 추천값은 각 팀 README와 `12-soccer-4v4-v2-team-training.md`에 기록했다.

### 포메이션 완화

- 선수 간 이상 거리를 `5~34m`, 외곽 허용 범위를 `1.5~52m`로 확대했다.
- 지원 거리를 `3~32m`로 넓히고 미드필더 고정 앵커를 사용하지 않았다.
- 수비·골키퍼 겸임 선수는 수비 중에도 공보다 `8m` 앞까지 허용하고 `25m` 초과 전진부터 감점하도록 했다.
- 포메이션 점수에서 역할 위치 비중을 `10%`로 낮추고 개선량만 제한적으로 보상했다.

### 문서 정리

- `01-goals.md`, `02-plan.md`, `03-technical-baseline.md`, `04-design-details.md`를 Soccer 감독 전술 중심으로 갱신했다.
- 중요 결정 `D-014`~`D-018`을 추가하고 종료된 Escape 결정을 대체 상태로 표시했다.
- `09-soccer-prototype-spec.md`와 `12-soccer-4v4-v2-team-training.md`를 v2 현행 계약으로 전면 갱신했다.
- Escape 상세 문서 두 개를 `docs/archive`로 이동하고 개발 종료 배너를 추가했다.
- 과거 작업 기록은 당시 사실을 보존하기 위해 수정하지 않았다.

### 구현·검증 결과

- Pass 폴더를 Rule로 교체하고 Rule의 `Training`, `Models`, Trainer YAML을 제거했다.
- Rule 씬의 Blue 4명에 FSM 컨트롤러를 연결하고 양 팀 학습 플래그를 모두 껐다.
- Escape 씬과 이전 Pass 씬을 Build Settings에서 제거했다.
- Unity 스크립트 컴파일 성공.
- `SoccerProjectBuilder.BuildAllBatch` 생성 검증 성공: 8명, 역할 분포, v2 행동, 킥 힘, 세 신경망 전술, Rule 학습 배제 확인.
- EditMode Test Runner와 Windows 학습 Build는 반복 실행과 라이선스 클라이언트 재시작 후에도 `com.unity.editor.headless` 라이선스 채널에서 정지했다.
- Windows 학습 Build가 생성되지 않아 20분 미만 Base 학습과 새 `Base4v4V2.onnx` 설치는 수행하지 못했다.
- 구형 v1 ONNX는 계약 불일치로 사용하지 않았으며 현재 v2 TeamDefinition 모델 슬롯은 비어 있다.

### 다음 검증

- Unity Hub 라이선스 채널 정상화 후 EditMode·PlayMode 회귀 테스트
- Base Windows 학습 Build 생성과 3만 스텝 이하 임시 학습
- Rule 전체 경기와 Human 개입 플레이 검증
- 경기 중 감독 모델 교체 기능 구현과 상태 보존 검증

---

## 2026-08-09 — 팀별 독립 환경·HUD·정책 호환성 보강

### 요청과 실행 계획

- Base·Attack·Defense·Press·Rule 씬이 각 작업공간 안의 독립 환경 프리팹을 사용하도록 참조 구조를 분리한다.
- 전술별 보상으로 학습한 ONNX를 공통 최종 환경에 교체 적용할 때의 정책 계약과 Domain Shift 위험을 검수한다.
- 모든 Soccer 씬에서 사라진 UI Toolkit HUD를 복구하고 현재 전술명과 양 팀 누적 보상을 추가한다.
- 모델명은 `Type-YYYYMMDD-vNNN.onnx` 규칙으로 통일하고 HUD 전술명은 실제 모델 이름에서 판별한다.
- 게임 시작 모드를 AI로 바꾸고 AI 관전 카메라를 경기장에 더 가깝게 조정한다.
- 실행 중 상태는 `13-soccer-current-status.md`에서 단계별로 최신화한다.

### 감사 중 확인한 사실

- 기존 Base 이외 전술 씬의 `UIDocument.m_PanelSettings`가 비어 있어 HUD가 렌더링되지 않는다.
- 현재 다섯 작업공간 씬이 모두 공용 `SoccerField4v4.prefab` 하나를 참조하므로 프리팹 독립성이 없다.
- RewardProfile은 학습 보상 계산에만 사용되며 ONNX 관측에는 포함되지 않는다. 관측·행동·물리 계약이 동일하면 보상값 차이 자체는 추론 오류 원인이 아니다.

### 구현 결과

- `Core`, `Attack`, `Defense`, `Press`, `Rule`에 서로 다른 GUID의 Regular Environment Prefab을 생성했다.
- 각 프리팹 안에 Blue 전술 TeamDefinition, Purple Base TeamDefinition, 보상 정책, 훈련 플래그와 단일 모델 오버라이드 슬롯을 배치했다.
- 공통 프리팹은 최초 복제 템플릿으로만 사용하고 기존 로컬 프리팹의 개발자 변경은 덮어쓰지 않도록 생성기를 변경했다.
- 여섯 Soccer 씬이 Base 또는 자기 작업공간의 프리팹만 사용하도록 재생성했다.
- 모든 씬의 `UIDocument`에 `SoccerPanelSettings.asset`과 `SoccerHud.uxml`을 다시 연결했다.
- HUD 중앙 우측에 실제 모델명 기반 Blue/Purple 전술과 경기 누적 보상을 추가했다.
- 누적 보상은 `Agent.SetModel()`로 초기화되지 않는 RewardEngine 독립 장부로 구현했다.
- 모든 씬을 AI 모드로 시작하고 AI Overview 카메라를 `(0,82,-78)`, FOV `50`으로 조정했다.
- 모델명 `Type-YYYYMMDD-vNNN.onnx` 파서와 잘못된 이름 `UNKNOWN`, 선수별 불일치 `MIXED` 표시를 추가했다.
- Base 보상 정책을 독립 파일로 분리해 환경 프리팹의 누락 스크립트 직렬화 경고를 제거했다.

### 호환성 결론

- RewardProfile 수치가 달라도 ONNX 추론 입력은 변하지 않으므로 공격형 모델을 Base 최종 환경에서 실행할 수 있다.
- 최종 시연은 모델만 바꾸고 Base RewardProfile을 유지해야 경기 누적 보상을 같은 척도로 비교할 수 있다.
- 관측 43, Ray 포함 전체 입력 379, 행동 `[3,3,3,3]`, Agent 등록 순서, Ray 태그 순서, Decision Period, 물리, 킥 힘과 소유권 판정은 작업공간 간 동일해야 한다.
- 팀 개발자가 변경할 범위는 자기 RewardProfile 수치, TeamDefinition 모델과 로컬 프리팹의 모델 오버라이드다.
- 현재 v2 ONNX가 없어 실제 ONNX 입력·출력과 경기 중 런타임 교체는 아직 검증하지 못했다.

### 검증 결과

- Unity 전체 C#·테스트 어셈블리 컴파일 성공: `Logs/Soccer-Compile.log`
- `SoccerProjectBuilder.BuildAllBatch` 생성·계약 검증 성공: `Logs/Soccer-Generate.log`
- 다섯 프리팹 고유 GUID, Regular 타입, 누락 스크립트 0개 확인
- 여섯 씬의 프리팹 원본, PanelSettings, UXML, HUD 환경 참조 확인
- EditMode `23/23` 통과: `Logs/Soccer-Workspace-EditMode.xml`
- Soccer 전용 PlayMode `3/3` 통과: `Logs/Soccer-Workspace-PlayMode.xml`
- 전체 PlayMode 첫 실행은 종료된 Escape 씬을 Build Settings에서 의도적으로 제외한 상태라 Escape 테스트 3개만 실패했다. Soccer 3개는 같은 실행에서도 통과했으며 이후 Soccer 필터 결과로 확정했다.

### 후속 안전성 보강과 최신 검증

- Attack·Defense·Press의 비학습 Purple을 프리팹에서 `HeuristicOnly`로 직렬화하고 Rule의 8명도 같은 방식으로 Trainer 등록을 차단했다.
- `SoccerMatchSetup` 실행 순서를 `-1000`으로 올려 ML-Agents Agent 초기화 전에 모델과 비학습 제어 모드를 확정했다.
- Base는 8명 `Default`, Attack·Defense·Press는 Blue 4명 `Default`와 Purple 4명 `HeuristicOnly`, Rule은 8명 `HeuristicOnly`인 생성 결과를 확인했다.
- `AgentSoccer.Team`과 `BehaviorParameters.TeamId` 일치를 생성 계약에 추가했다.
- 계약 스냅샷을 Field 위치·회전·크기, 공 시작점, 모든 Rigidbody·Collider·Physics Material과 여섯 씬의 이동 설정까지 확대했다.
- 최신 `SoccerProjectBuilder.BuildAllBatch`와 전체 C#·테스트 어셈블리 컴파일은 성공했다.
- 최신 EditMode 실행은 Unity Licensing Client가 `com.unity.editor.headless` 채널을 열지 못해 테스트 진입 전에 반복 대기했다. 라이선스 클라이언트 재시작과 `-quit` 재시도 후에도 같아 해당 Unity 프로세스를 종료했다.
- 기존 EditMode `23/23`, Soccer PlayMode `3/3` 성공 XML은 보존하되 최신 TeamId·초기 Trainer 차단 단언 이전 결과로 구분한다.

### 다음 작업

- v2 Base ONNX 생성과 이름 규칙에 맞춘 등록
- 실제 ONNX tensor 계약 검사
- 경기 중 감독 모델 교체 관리자와 `Agent.SetModel()` 원자 교체 구현
- 전술 교체 직후 recurrent memory 초기화 영향 평가
- Windows Build에서 HUD 시각 배치 확인
- Unity Hub 라이선스 채널 정상화 후 최신 EditMode·Soccer PlayMode 재실행

---

## 2026-08-09 — 공통 경기장 동기화·골대 가림·보상 수치 감사 완료

### 요청

- 독립 프리팹 구조를 유지하면서 경기장 전반의 공통 변경은 모든 환경 프리팹에 같은 내용으로 적용되도록 규칙화한다.
- 골대가 카메라와 선수 사이에서 선수를 가릴 때만 골대를 반투명하게 표시한다.
- 현재 실제 적용 중인 보상을 행동별로 다시 감사하고 이해하기 쉬운 표로 정리한다.

### 공통 경기장 동기화 규칙

- 공통 경기장 변경 대상은 `Assets/Soccer/Prefabs/SoccerField4v4.prefab` 템플릿과 Base·Attack·Defense·Press·Rule의 다섯 독립 환경 프리팹이다.
- 경기장 구조·공통 렌더링·물리·카메라 변경은 여섯 프리팹 자산을 하나의 변경 단위로 취급한다.
- `AGENTS.md`에 동기화 의무를 추가하고 중요 결정 D-022로 확정했다.
- Builder가 `ApplyCommonArenaConfiguration`을 공통 템플릿과 기존 다섯 프리팹 모두에 적용하도록 구성했다.
- 전술 전용 컴포넌트를 제외한 전체 계층, 활성 상태, 태그, Layer, Transform, Mesh, Renderer, Material, Rigidbody, Collider와 정책 관측 계약의 parity를 비교한다.
- 공통 템플릿과 다섯 프리팹의 parity 검증을 통과하기 전에는 경기장 공통 변경을 완료로 처리하지 않는다.

### 골대 가림 처리

- 골대가 선수와 같은 머티리얼을 공유하지 않도록 `GoalBlue`, `GoalPurple`, `GoalNetBlack`, `GoalNetWhite` 전용 머티리얼을 생성해 여섯 골대 Renderer에 배치했다.
- `SoccerGoalOcclusionFader`가 화면 안 선수의 중심과 카메라 사이에서 Blue 또는 Purple 골대 Renderer Bounds가 먼저 교차하는지를 검사한다.
- 실제 가림 중인 골대 그룹만 alpha `0.25`, fade speed `8`로 부드럽게 반투명 처리한다.
- 가림이 끝나면 원본 Opaque 머티리얼과 Shadow 설정을 그대로 복구한다.
- 일반·Base·Attack·Defense·Press·Rule 여섯 활성 씬의 Main Camera에 같은 가림 제어기와 해당 씬 환경 참조를 연결했다.
- 선수 Renderer와 머티리얼은 변경하지 않았다. 골대 Collider 수·활성 상태, `blueGoal`·`purpleGoal` 태그와 `SoccerBallController` 득점 판정도 그대로 유지했다.

### 보상 감사

- Base·Attack·Defense·Press RewardProfile asset의 실제 직렬화 값과 `SoccerRewardEngine`의 지급 조건을 다시 대조했다.
- 사건별 행동, 지급 대상, 실제 수치, 반복 방지 조건과 소유권·경기 상한을 구분해 사용자용 표로 정리했다.
- Rule은 강화학습에서 제외되지만 경기 표시를 위해 Base Profile 수치를 보유한다는 점을 별도로 구분했다.

### 검증 결과

- Unity 전체 C# 및 테스트 어셈블리 컴파일 성공: `Logs/Soccer-Compile.log`
- `SoccerProjectBuilder.BuildAllBatch` 생성·계약 검증 성공: `Logs/Soccer-Generate.log`
- 공통 템플릿+다섯 독립 프리팹의 경기장 parity, 골대 전용 머티리얼, 선수 머티리얼 비공유, 골대 Collider·태그 계약 검증 성공
- 여섯 활성 씬의 가림 제어기 환경 참조와 alpha `0.25`, fade speed `8` 설정 검증 성공
- 가림 PlayMode 단언 보강 후 최종 C#·테스트 어셈블리 재컴파일과 `git diff --check` 통과
- 최신 EditMode 시도는 Unity Licensing Client가 `com.unity.editor.headless` 채널을 열지 못해 테스트 진입 전에 중단했다: `Logs/Soccer-EditMode.log`
- 이번 EditMode 시도에서는 새 결과 XML이 생성되지 않았으며 테스트 단언 실패도 발생하지 않았다. 이전 회귀 기준 EditMode `23/23`과 Soccer PlayMode `3/3` 성공 XML은 보존했다.

### 남은 확인

- Unity Hub 라이선스 채널 정상화 후 최신 공통 경기장·골대 가림 EditMode 및 Soccer PlayMode 단언 재실행
- Windows Build에서 AI 전체 시점과 인간 추적 시점의 골대 전환 속도·가독성 시각 확인

---

## 2026-08-10 — Unity 에셋 구조와 팀 소유 폴더 정리

### 요청

- 현재 Soccer 파일을 `_Soccer`에 모으고 Turtle·Escape·불필요한 과거 실험을 `_Legacy`로 보관한다.
- 삭제 없이 `.meta`와 관련 에셋을 함께 이동해 기존 실험도 실행 가능하게 유지한다.
- 팀 물리 폴더를 `Attack_KMW`, `Defense_PJH`, `Press_KMG`, `Rule_PHC`로 바꾸고 모든 문서를 최신화한다.

### 변경

- 활성 Soccer 전체를 `Assets/_Soccer`로 이동했다.
- Turtle을 `Assets/_Legacy/Turtle`, Escape를 `Assets/_Legacy/Escape`, 템플릿과 진단 자료를 각각 `UnityTemplate`, `Diagnostics`로 분류했다.
- Turtle Trainer 설정과 결과를 Unity import 제외용 `Training~/Config`, `Training~/Results`에 보존했다. YAML은 `mlagents-learn`에서 직접 경로로 사용할 수 있다.
- 폐기된 Pass와 초기 ML-Agents Soccer 샘플을 `Assets/_Legacy/ArchivedSoccer~`에 보관했다. Pass와 현행 Rule의 중복 GUID 사본은 `~` 폴더 때문에 동시에 import되지 않는다.
- 팀 폴더만 담당자 접미사로 변경하고 논리 전술명, BehaviorName, 모델·클래스·asmdef 이름은 유지했다.
- Builder, Build Settings, 테스트와 현행 문서의 물리 경로를 갱신하고, 과거 로그의 옛 경로에는 현재 위치 주석을 추가했다.
- `docs`와 Markdown을 Git 공유 대상으로 전환하고 구조 안내 `docs/17-unity-asset-layout.md`를 추가했다.
- 실제 공통 참조인 `Assets/Settings`와 `Assets/InputSystem_Actions.inputactions`는 루트에 유지했다.

### 보상 영향

| 항목 | 기존 값 | 변경 값 | 변경량 |
| --- | ---: | ---: | ---: |
| Soccer 보상 수치·판정·상한 | 기존 기준 유지 | 기존 기준 유지 | `0` |

이번 작업은 에셋 위치와 참조만 변경했으며 보상 코드·Profile 값은 변경하지 않았다.

### 문서·정적 검증

- 활성 폴더와 Legacy 폴더의 README, 팀 README, `AGENTS.md`, 루트 README와 `docs` 전체를 새 구조 기준으로 검토했다.
- 현행 경로는 `_Soccer`와 담당자별 물리 폴더를 사용하고, 역사 기록은 당시 경로와 현재 위치를 함께 보존한다.
- 이동으로 삭제 표시된 추적 파일 480개가 모두 새 목적지에 존재하며 이동 전 `.meta` GUID 불일치는 `0`이다.
- Unity import 대상에는 누락·고아 `.meta`와 중복 GUID가 없고, Build Settings 여섯 씬은 새 경로와 기존 GUID를 유지한다.
- Soccer·Escape·Turtle을 포함한 전체 솔루션을 Unity 내장 .NET 참조로 컴파일해 오류 `0`을 확인했다.
- Markdown 30개의 로컬 링크, asmdef·JSON, UXML 파싱과 `git diff --check`를 통과했다.
- Unity 배치 import 두 차례는 Licensing Client의 `com.unity.editor.headless` 채널 오류로 AssetDatabase 진입 전에 중단됐다. 따라서 `SoccerProjectBuilder.ValidateBatch`와 최신 EditMode·PlayMode는 라이선스 복구 후 다시 실행한다.
- Builder 원본 `SoccerFieldTwos.prefab`의 구 시연 기록 Missing Script 4개는 기존 상태이며 출력 프리팹 생성 시 자동 제거된다. 사용자 승인 없이 삭제하지 않고 검토 후보로 남겼다.

---

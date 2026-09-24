# 프로젝트 파일 구조와 보존 기준

마지막 검토: 2026-09-24. 물리 경로와 탐색 범위의 기준이다.

| 경로 | 역할 | 기본 탐색 |
|---|---|---|
| `docs/soccer/current-status.md` | 최신 진행 상태만 | 상태 확인 시 |
| `docs/soccer/training/ms-v3-current.md` | 현행 MNG 운영 계약 | MNG 작업 시 |
| `docs/soccer`, `docs/project` | 현재 주제별 문서 | 필요한 주제만 |
| `docs/archive` | 과거 계획·완료 보고서·누적 이력 | 제외, 색인에서 선택 |
| `Assets/_Soccer/Manager` | 현재 감독 RL와 재사용하는 M/MS 기반 | Runtime/Editor/Tests부터 |
| `Assets/_Soccer/Core`, `Teams` | 공통 선수·팀별 구현 | Core 작업 시 |
| `Assets/_Soccer/Curriculum` | 기존 선수 L0~L4·L2-Find/Score 보존 | 해당 단계 작업 시 |
| `Assets/_Soccer/Materials`, `Meshes`, `SourceModels`, `UI` | 공용 외형·UI | 해당 자산 작업 시 |
| `Assets/Hayq Art` | 프로젝트에 존재하는 외부 아트 자산 | 사용 여부를 이름만으로 판단하지 않음 |
| `Assets/Settings`, `InputSystem_Actions.inputactions` | 공통 렌더·입력 | 설정 작업 시 |
| `Assets/_Legacy` | Turtle·Escape·과거 실험·진단 원본 | 제외 |
| `Tools` | 실행·평가·검증 도구 | README에서 진입점 선택 |
| `Builds`, `results`, `Logs` | 빌드·정책 계보·검증 원본 | 전체 읽기 제외, 정확한 경로 선택 |
| `Library`, `obj`, `.vs`, `UserSettings`, `__pycache__` | 생성물·개인 설정 | 제외 |

## Unity 에셋

에셋은 대응 `.meta`와 함께 이동한다. GUID 참조와 문자열 경로를 따로 확인한다. 씬·프리팹·모델의 사용 여부는 Builder·프로필·Build Settings·실행 스크립트까지 검사해야 판단할 수 있다.

MNG의 `Curriculum/MS_V2`, `MNG_ManagerV2`, MS3V2 빌드·YAML 이름은 현행 v3에서도 쓰는 기술 ABI다. Models/EvaluationModels 및 이전 커리큘럼은 Builder와 과거 평가의 참조가 있어 원위치 보존한다. 기존 선수 정책과 감독 정책을 합치지 않는다.

2026-09-24에 참조 없는 타이머 산출물을 다음 위치로 보관했다. 내용과 `.meta`는 원본 hash 그대로다.

- `Assets/_Soccer/Diagnostics` → `Assets/_Legacy/Diagnostics/SoccerRuntimeTimers~`
- `Assets/ML-Agents` → `Assets/_Legacy/Diagnostics/MLAgentsTimers-20260924~`

`~` 폴더는 Unity import에서 제외된다. `Assets/ML-Agents`는 ML-Agents 종료 때 타이머가 다시 생길 수 있는 생성 경로다. `.gitignore`의 기존 제외를 유지하며 재생성을 소스 복구로 오인하지 않는다.

기존 `Training~`, `ArchivedSoccer~`도 import 제외 보관소다. 과거 Pass 사본과 현행 Rule의 동일 GUID 사본을 동시에 import하지 않는다. Legacy에는 신규 Soccer 기능을 추가하지 않는다.

## 문서·실행 증거

MNG의 과거 `MNG_01`~`MNG_05`, R0 안내는 `docs/archive/manager/m-stage`에 모았다. 원래 Markdown `.meta`도 같은 위치에 보존했으며 runtime 에셋으로 사용하지 않는다. 활성 Manager 폴더에는 짧은 `MNG_README.md`를 둔다.

Builds와 results는 큰 파일이라는 이유로 제거하지 않는다. 현행 actor·평가 비교·정상 중단·실패 원인 재현의 원본이다. `Logs`의 source snapshot·hash manifest·당시 명령은 역사적 기록이므로 현재 경로로 일괄 치환하지 않는다. 이동된 현행 문서는 [Archive](../archive/README.md)와 [정리 보고서](cleanup-20260924.md)의 이동 명세로 찾는다.

## 이동 검증

양쪽 절대 경로가 프로젝트 안인지 확인 → 기존 변경·hash 기록 → Editor 종료 확인 → 에셋/meta 함께 이동 → 현재 문서·코드 참조 갱신 → 링크·GUID·내용 보존 검사 순서로 수행한다. 실행 자산을 이동하면 Unity import/compile와 영향 회귀까지 확인한다. 이번 이동은 Markdown과 미참조 진단 JSON이며 런타임 C#·씬·프리팹·모델·설정을 유지했다.

담당 접미사 `Attack_KMW`, `Defense_PJH`, `Press_KMG`, `Rule_PHC`는 물리 폴더에만 쓴다. 논리 BehaviorName·모델 접두사·타입·asmdef는 유지한다. `.gitignore`, commit/push 및 공개 범위를 파일 정리와 함께 임의 변경하지 않는다.

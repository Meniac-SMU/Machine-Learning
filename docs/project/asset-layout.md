# Unity 에셋 구조와 소유권

마지막 검토: 2026-08-10  
상태: `Asset 위치와 소유권의 단일 기준`

## 목적

현재 개발 중인 Soccer와 과거 Turtle·Escape·Soccer 실험을 물리적으로 분리한다. 모든 관련 에셋과 `.meta`를 저장소 안에 유지하여 Git으로 받은 팀원이 GUID 참조를 잃지 않고 프로젝트를 열 수 있게 한다.

## 최상위 구조

```text
Assets/
├─ _Soccer/                    # 활성 Soccer만
├─ _Legacy/                    # 보관 실험과 진단 자료
├─ Settings/                   # 현재 프로젝트 공통 URP 설정
└─ InputSystem_Actions.inputactions  # 현재 프로젝트 공통 입력 참조
```

`Assets/AI Models`, `Escape`, `Materials`, `ML-Agents`, `Prefabs`, `Scenes`, `Scripts`, `TutorialInfo`처럼 용도가 섞였던 옛 최상위 폴더는 다시 만들지 않는다. `Assets/Settings`와 `Assets/InputSystem_Actions.inputactions`는 실제 공통 참조이므로 Assets 루트에 유지한다.

## 활성 Soccer

```text
Assets/_Soccer/
├─ Core/
├─ Teams/
│  ├─ Attack_KMW/
│  ├─ Defense_PJH/
│  ├─ Press_KMG/
│  └─ Rule_PHC/
├─ Editor, Prefabs, Scenes, Tests, Training, UI/
└─ Materials, Meshes, SourceModels, Diagnostics/
```

담당 접미사는 물리 폴더만 구분한다. 다음 값은 정책 호환성과 직렬화 계약이므로 폴더명에 맞춰 바꾸면 안 된다.

| 물리 폴더 | 논리 전술명 | 유지할 대표 식별자 |
| --- | --- | --- |
| `Attack_KMW` | `Attack` | `Soccer4v4_Attack`, `AttackRewardProfile`, Attack BehaviorName·모델 접두사 |
| `Defense_PJH` | `Defense` | `Soccer4v4_Defense`, `DefenseRewardProfile`, Defense BehaviorName·모델 접두사 |
| `Press_KMG` | `Press` | `Soccer4v4_Press`, `PressRewardProfile`, Press BehaviorName·모델 접두사 |
| `Rule_PHC` | `Rule` | `Soccer4v4_Rule`, `RuleRewardProfile`, Rule BehaviorName·FSM 클래스 |

## Legacy

```text
Assets/_Legacy/
├─ Turtle/
│  ├─ Models, Materials, Prefabs, Scenes, Scripts/
│  └─ Training~/Config, Results/
├─ Escape/
├─ UnityTemplate/
├─ Diagnostics/ML-Agents, UnityUpgrade~, GeneratedProjects~/
└─ ArchivedSoccer~/Pass, MLAgentsSamples/
```

- Turtle와 Escape는 삭제하지 않으며 관련 에셋과 `.meta`를 함께 보존한다.
- `Training~`와 `ArchivedSoccer~`는 Unity import 대상이 아니다. Turtle YAML은 `mlagents-learn`에서 명시적 경로로 직접 사용할 수 있다.
- `GeneratedProjects~`의 `.csproj`와 `.sln`, Root의 Build·Cache·학습 결과 일부는 ignore된 로컬 산출물일 수 있다. Source 보존과 Git 전달을 같은 의미로 보지 않는다.
- Escape Builder가 필요하면 현재 루트 `Assets/_Legacy/Escape`를 사용한다.
- `ArchivedSoccer~/Pass`와 현행 Rule에는 같은 GUID의 역사 사본이 있으므로 둘을 Unity import 대상에 동시에 두지 않는다.
- Legacy를 활성 개발로 되돌릴 때는 필요한 범위, 대상 GUID와 Build Settings 영향을 별도 결정으로 기록한다.

## 이동·이름 변경 규칙

1. 원본과 목적지가 모두 `C:\GitHub\Machine-Learning` 안인지 절대 경로로 확인한다.
2. Unity가 닫힌 상태에서 에셋과 대응 `.meta`를 함께 이동한다.
3. 폴더 이동 후 Builder 상수, Build Settings, 테스트 경로, 문서와 문자열 기반 로드를 검색한다.
4. GUID 기반 씬·프리팹 참조가 유지되는지 확인한다.
5. Unity import와 컴파일, Builder 검증, 가능한 EditMode·PlayMode 테스트를 실행한다.
6. 비어 있는 옛 폴더를 남기거나 같은 이름으로 다시 만들지 않는다.
7. 삭제가 필요한 것으로 보이는 산출물은 지우지 말고 후보 목록과 근거를 사용자에게 먼저 보고한다.

## 문서와 전달

- `docs`, Root README, Soccer·Legacy README와 팀 README는 향후 팀 공유 대상이다.
- 프로젝트가 관리하는 모든 Markdown은 `.gitignore`에서 Git 추적 대상으로 허용한다. 생성 cache처럼 폴더 전체가 제외된 위치는 다시 포함하지 않는다.
- 실제 GitHub 공개는 Markdown 검토 후 `git add`·commit·push 단계에서 진행한다.
- 과거 로그에는 당시 경로를 유지하고 `현재 위치:` 주석을 추가한다.
- 현행 명세·명령·체크리스트는 반드시 현재 물리 경로를 사용한다.

## 관련 문서

- 코드와 Prefab 책임: [Soccer 아키텍처](../soccer/architecture.md)
- 공통 계약 변경 검증: [설정 및 검증](setup-and-validation.md)
- 과거 Asset과 기획 기록: [Archive 안내](../archive/README.md)

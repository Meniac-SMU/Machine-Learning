# Machine-Learning 에이전트 지침

이 지침은 `C:\GitHub\Machine-Learning`과 하위 경로에만 적용한다. 마지막 정리: 2026-09-24.

## 먼저 읽을 범위

1. [문서 색인](docs/README.md)에서 작업 유형을 선택한다.
2. 진행 상태가 필요할 때만 [현재 상태](docs/soccer/current-status.md)를 읽는다.
3. MNG 현행 계약은 [MS v3 운영 기준](docs/soccer/training/ms-v3-current.md)이다. 상세 보고서·과거 문서를 처음부터 모두 읽지 않는다.
4. 검색은 `Tools/Find-ProjectContext.ps1`로 범위를 고르거나 `rg`에 특정 폴더를 지정한다. `docs/archive`, `Assets/_Legacy`, `Logs`, `results`, `Builds`, `Library`, `obj`, `.vs`, `__pycache__`를 기본 재귀 검색에 포함하지 않는다. 증거가 필요하면 색인에서 특정 Run/보고서 하나를 선택한다.

## 현재 작업 경계

- MS2-v3 출발 기준 채택 및 MS3-v3 학습 전 D1~D5 준비 완료. 새 v3 R6 학습 미시작, R7~R8 미진입. 상태·증거의 단일 기준은 current-status다.
- 활성 모델 자격은 `docs/soccer/training/ms-v3-model-registry.json`, ABI는 `ms-v3-schema.json`이다. v2 명칭의 코드·YAML·빌드는 v3에서 재사용할 수 있으므로 이름만으로 제거하지 않는다.
- 기존 MS3-v2 r005 400k와 구 MS3 정책은 현행 초기값·상대 풀·평가 기준·champion·기본 시연에 사용하지 않는다. PT/ONNX/log/build와 registry 원본은 보존한다.
- L2-Find r017은 미승인 종료 상태다. 후속 학습·L2-Score·L3를 자동 재개하지 않는다.
- 이번 정리는 파일·문서 구조 작업이며 게임 규칙 변경·학습·평가 실행·모델 승격·commit/push 승인이 아니다.

## 보존·이동 규칙

- 조회·수정·이동 대상은 프로젝트 안으로 한정하고 다른 프로젝트는 조회하지 않는다. 설치된 Unity/Python 실행 파일 호출은 가능하지만 외부 파일은 수정하지 않는다.
- 기존 미커밋 변경을 보존한다. 원본·실패 기록·모델 계보를 잃지 않는다.
- Unity 에셋 이동은 Editor가 닫힌 상태에서 `.meta`를 함께 이동해 GUID를 유지한다. 경로 문자열, Builder, Build Settings, 테스트와 문서 링크도 갱신한다.
- 이번 사용자 지시에서 문서는 통합 삭제 가능하나, 그 외 파일은 기본적으로 보관 이동한다. 구 도구·빌드 삭제는 더 이상 사용하지 않음이 확정된 경우만 가능하다.
- `.gitignore`와 공개 범위는 명시적 승인 없이 변경하지 않는다. 준비 manifest·hash·증거 원본을 정리 편의를 위해 덮어쓰지 않는다.
- 완료 보고서와 대체된 기획은 `docs/archive`, Unity 과거 에셋은 `Assets/_Legacy`에 둔다. 현행 경로와 보관 이유는 [Asset 구조](docs/project/asset-layout.md)에 기록한다.

## 개발 계약

- MNG 감독의 관측244/명령6과 기존 선수 정책의 관측379/행동[3,3,3,3]은 별도 계약이다. [MNG 운영 기준](docs/soccer/training/ms-v3-current.md), [Core 경기 계약](docs/soccer/gameplay-contract.md)을 구분한다.
- MNG 학습형·규칙형·fallback은 같은 선수 기술 경로를 쓴다. 감독의 전략 선택을 코드가 대신하지 않으며 사람의 입력 소유권을 유지한다.
- Core 공통 경기장·소유권·보상 사건·물리·센서·Reset은 한 팀 전용으로 분기하지 않는다. 공통 변경은 SoccerProjectBuilder로 Template과 Base·Attack·Defense·Press·Rule 5환경에 함께 적용하고 parity를 검증한다. MNG는 전용 Builder를 사용한다.
- 담당 폴더 접미사 `Attack_KMW`, `Defense_PJH`, `Press_KMG`, `Rule_PHC`는 물리 경로만 구분한다. BehaviorName·클래스·namespace·asmdef의 논리명은 유지한다.
- 보상 변경은 코드/Profile, [보상 기준표](docs/soccer/rewards.md), 팀 README·테스트·현재 상태를 함께 갱신하고 `기존 값 / 변경 값 / 변경량`을 명시한다. Rule 보상은 HUD·통계용이다.
- MNG 광범위한 구현·회귀에는 자체 호출 제한이 있는 선택적 진단 스킬을 적용하지 않는다. 별도 작업·하위 에이전트는 자동 생성하지 않는다.

## 검증·기록

변경 전후 git status를 확인한다. 범위에 맞게 정적 참조 검사, compile, Builder, EditMode/PlayMode를 선택하고 [검증 절차](docs/project/setup-and-validation.md)를 따른다. Unity 실행 뒤 Scene/Prefab/meta/ProjectSettings의 예상 밖 변경을 점검한다. 정적 검사·빌드·자동 경기·사용자 시각 확인·학습 품질을 별개로 보고한다. 최신 상태만 current-status에 두고 상세 이력은 Archive에 쓴다.

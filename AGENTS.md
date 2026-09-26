# Machine-Learning 에이전트 지침

이 지침은 `C:\GitHub\Machine-Learning`과 하위 경로에만 적용한다. 마지막 정리: 2026-09-26.

## 먼저 읽을 범위

1. [문서 색인](docs/README.md)에서 작업 유형을 선택한다.
2. 진행 상태가 필요할 때만 [현재 상태](docs/soccer/current-status.md)를 읽는다.
3. MNG 현행 계약은 [MS v3 운영 기준](docs/soccer/training/ms-v3-current.md)이다. 상세 보고서·과거 문서를 처음부터 모두 읽지 않는다.
4. 검색은 `Tools/Find-ProjectContext.ps1`로 범위를 고르거나 `rg`에 특정 폴더를 지정한다. `docs/archive`, `Assets/_Legacy`, `Logs`, `results`, `Builds`, `Library`, `obj`, `.vs`, `__pycache__`를 기본 재귀 검색에 포함하지 않는다. 증거가 필요하면 색인에서 특정 Run/보고서 하나를 선택한다.

## 현재 작업 경계

- MS3-v3 r002는 최신 승인 범위2M을 완료했다. 최종2,001,834 step 저장,100k별 점검,400k 간격1.2M·1.6M·2M 총960경기 평가를 마치고 학습·평가·자원 모니터를 종료했다. 무작위 상대는 제외했다. R6 완료를 유지한다. 이후 사용자가 승인한0k~2M 순차 토너먼트220경기로2M(실제2,001,834 step)를 champion으로 확정하고 registry에 등록했다. 기본40경기, 승률 차이5%p 이하20경기 추가, 이후 완전 동률만40경기 추가라는 사용자 규칙을 적용했다. 증거는 `Logs/MNG-Rebuild/MS3-v3-r002-tournament-20260926/completion.json`이다.2M 이후 학습은 별도 승인 대상이다. 주간 잔여4% 이하 또는 치명적 문제 시 저장·중단 조건은 유지한다. gate·원본·실패 복구·완료 증거는 `Logs/MNG-Rebuild/MS3-v3-r002-to2m-20260925`에 보존한다. 설정 변화는 명목 최대 step과 모델 보존 수21→64뿐이며 PPO·runtime·보상·상대 풀 규칙은 유지했다.1.7M 시작 포트 충돌은 원래 포트 해제 후 같은 난수 조건으로 복구했다. r001·r900은 재개/승격하지 않는다. 상태·증거의 단일 기준은 current-status다.
- 활성 모델 자격은 `docs/soccer/training/ms-v3-model-registry.json`, ABI는 `ms-v3-schema.json`이다. v2 명칭의 코드·YAML·빌드는 v3에서 재사용할 수 있으므로 이름만으로 제거하지 않는다.
- 향후 챔피언은 새 모델이 현 챔피언과 직접 대결에서 이기면 교체한다. 기본40경기·300초·진영 교대, 승률 차이5%p 이하20경기 추가, 이후 완전 동률만40경기 추가 후 누적 승수로 판정한다. 다양한 상대의 평가는400k 간격으로 유지하고100k에는 상태 점검만 한다. MS2·Full·Recover·carry-shot·Balanced·동일 Run 과거 정책의 성적과 패스/수비/진영 지표를 별도로 보고하며 이를 추가 승격 문턱으로 삼지 않는다. 무작위 상대는 제외한다. 상세 기준은 `docs/soccer/training/ms-v3-current.md`를 따른다.
- 기존 MS3-v2 r005 400k와 구 MS3 정책은 현행 초기값·상대 풀·평가 기준·champion·기본 시연에 사용하지 않는다. PT/ONNX/log와 registry 원본은 보존한다. 빌드는 2026-09-25 사용자 지시에 따라 현행 의존성을 확인해 폐기할 수 있으며 [빌드 보존 목록](docs/project/build-retention.json)을 우선한다.
- L2-Find r017은 미승인 종료 상태다. 후속 학습·L2-Score·L3를 자동 재개하지 않는다.
- 이번 정리는 파일·문서 구조 작업이며 게임 규칙 변경·학습·평가 실행·모델 승격·commit/push 승인이 아니다.

## 보존·이동 규칙

- 조회·수정·이동 대상은 프로젝트 안으로 한정하고 다른 프로젝트는 조회하지 않는다. 설치된 Unity/Python 실행 파일 호출은 가능하지만 외부 파일은 수정하지 않는다.
- 기존 미커밋 변경을 보존한다. 원본·실패 기록·모델 계보를 잃지 않는다.
- 2026-09-26 사용자가 과거 선수 실험(A)과 과거 MNG 실험(B)을 모두 보존하기로 결정했다. [Run별 결정](docs/archive/history/storage-candidates-20260926.json)의 대상은 삭제하거나 Git 추적에서 제외하지 않는다. 새 용량 정리는 [점검 보고서](docs/project/storage-review-20260926.md)에서 시작하고 전체 원본을 반복해서 읽지 않는다. 완료 로그의 NTFS 압축은 내용 해시를 검증하며 개별 파일에만 적용한다. 생성 캐시는 재생성되므로 오래된 날짜만으로 실행 자산·모델을 삭제하지 않는다.
- Unity 에셋 이동은 Editor가 닫힌 상태에서 `.meta`를 함께 이동해 GUID를 유지한다. 경로 문자열, Builder, Build Settings, 테스트와 문서 링크도 갱신한다.
- 문서는 통합 삭제 가능하다. 에셋·도구는 미사용이 확인된 것만 정리하며, 구 빌드는 현행 경로·정책 변경·대체 검증을 확인하고 사용 종료가 확정된 경우 삭제한다. 모델 계보·원본 증거는 함께 지우지 않는다.
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

새 Manager 빌드는 `Tools/Build-MNGCurrent.ps1`에 단계·목적을 명시한다. 다른 Builder/Unity 메뉴 빌드도 완료 직후 `Tools/Build-Lifecycle.ps1 -Action Register`로 생성 시점·단계·목적·근거를 기록하고 실제 사용 후 `-Action Use`로 사용 이력을 남긴다. 새 빌드·단계 전환·정리 작업 때 `-Action Review`로 구 빌드 폐기를 검토한다. 나이만으로 자동 삭제하지 않는다. 상세는 [빌드 관리 기준](docs/project/build-lifecycle.md)이다.

변경 전후 git status를 확인한다. 범위에 맞게 정적 참조 검사, compile, Builder, EditMode/PlayMode를 선택하고 [검증 절차](docs/project/setup-and-validation.md)를 따른다. Unity 실행 뒤 Scene/Prefab/meta/ProjectSettings의 예상 밖 변경을 점검한다. 정적 검사·빌드·자동 경기·사용자 시각 확인·학습 품질을 별개로 보고한다. 최신 상태만 current-status에 두고 상세 이력은 Archive에 쓴다.

# 프로젝트 전체 용량 점검 — 2026-09-26

범위는 `C:\GitHub\Machine-Learning` 내부다. 사용자가 요청한 로컬·Git 전달 용량 절감에 따라 실제 파일 목록, Git 추적 여부, Unity GUID·경로·Builder 참조와 현행 학습 문서를 검사했다. 사용자는 과거 학습 원본 A+B를 **모두 그대로 보존**하기로 결정했다. 이번 작업은 commit/push·학습·평가·모델 승격을 수행하지 않았다.

## 결과

| 작업 | 절감량 | 적용과 보존 |
|---|---:|---|
| 완료된 텍스트 로그 17,032개 NTFS 압축 | 약26.52GiB | 내용·경로 유지, 모든 파일 압축 전후 SHA-256 일치 |
| 재생성 캐시와 미사용 Unity 기본 템플릿 삭제 | 약993.32MiB | 14,865파일. 모델·학습 원본·현재 씬/프리팹 보존 |
| Git 객체 repack | 약42.94MiB | 전체4,564객체·모든 refs·HEAD 보존, 이력 재작성 없음 |
| 합계 | 약27.53GiB | 새 점검 증거 약34.39MiB를 제외한 순절감 추정은 약27.50GiB |

압축량은 Windows `GetCompressedFileSizeW`가 보고한 저장 크기 차이다. 캐시·Git은 파일 크기 합계 차이를 사용했다. 클러스터 할당·다른 프로세스의 기록까지 반영한 정확한 볼륨 여유 공간 변화는 아니다. NTFS 압축은 논리적 파일 크기를 줄이지 않으므로 Explorer의 “크기”와 “디스크 할당 크기”를 구분한다. 압축 대상의 논리 크기는40,853,516,483바이트, 압축 후 보고 저장 크기는12,378,361,856바이트다. 폴더 및 신규 파일의 자동 압축 속성은 변경하지 않았다.

Git 작업 트리에서 실제 삭제한 추적 파일은 Python 캐시16개와 템플릿15개, 합계31개·약0.15MiB다. 새 보고서·보존 명세도 추가되므로 순변화는 이 삭제량보다 작다. 학습 원본을 보존하기로 했으므로 **Git 전달 대상의 큰 용량 감소는 없다**. 이번 변경을 commit/push하기 전에는 팀원의 pull 내용도 바뀌지 않는다. 기존 커밋에 저장된 파일은 현재 파일을 삭제해도 과거 이력에 남는다. Git 이력 재작성·강제 push·원격 삭제는 하지 않았다.

## 조사 범위와 판단

시작 시 논리적 전체 파일 크기는 약54.62GiB였다. Logs40.67GiB, results5.60GiB, Git4.60GiB, Library3.02GiB, Builds0.67GiB가 대부분이다. Assets는 전체 약65.54MiB이고 Git 추적 파일 전체 약5.30GiB의 대부분은 PT/ONNX였다. 모든 파일의 경로·크기·추적 여부를 목록화한 뒤 Unity 씬63개·프리팹126개와 에셋 GUID 및 문자열 참조를 점검했다. 사용하지 않는 씬 이름만으로 대용량 절감이 가능한 구조는 아니었다.

삭제한 경로는 다음으로 한정했다. 삭제 전에 프로젝트 내부 절대 경로와 reparse 여부, Unity Editor·학습 Player 실행 여부를 확인했다.

| 경로 | 약 MiB | 근거 |
|---|---:|---|
| `Library/Bee` | 658.42 | 재생성 가능한 컴파일·빌드 캐시 |
| `Library/BurstCache` | 273.63 | 재생성 가능한 Burst 캐시 |
| `Library/ShaderCache` | 26.40 | 재생성 가능한 셰이더 캐시 |
| `Library/PlayerDataCache` | 27.07 | 재생성 가능한 Player 데이터 캐시 |
| `Library/BuildPlayerData` | 2.94 | 재생성 가능한 빌드 캐시 |
| `obj` | 4.71 | 소스가 보존된 생성 중간 파일 |
| `Tools/__pycache__` | 0.10 | Python 소스로 재생성 가능, `.pyc`/`.pyo` 재추적 방지 규칙 추가 |
| `Assets/_Legacy/UnityTemplate`와 대응 `.meta` | 0.05 | Unity 환영/Tutorial 템플릿. 외부 GUID·타입·경로 소비자 없음 |

Library 전체, PackageCache, Artifacts, 개인 설정은 유지했다. 캐시는 이후 실행·빌드에서 다시 생길 수 있고 첫 빌드 시간이 늘 수 있다. 현재 게임 코드·씬·프리팹·모델을 최적화하거나 재생성하지 않았다.

## 사용자가 보존을 확정한 원본

| 그룹 | 범위 | Run 수 | 약 GiB | 결정 |
|---|---|---:|---:|---|
| A | 과거 `results/Base-*`, `results/Curriculum*` | 65 | 4.78 | 로컬·기존 Git 추적 모두 그대로 보존 |
| B | 과거 `results/MNG_*` | 41 | 0.35 | 로컬·기존 Git 추적 모두 그대로 보존 |

정확한 경로는 [Run별 보존 결정](../archive/history/storage-candidates-20260926.json)에 있다. L2-Find r017, 승인 MS2 초기 정책 Run, 현재 v3 r001/r002/r900은 원래부터 위 삭제 후보에서 제외했다. A+B를 다시 자동 삭제 후보로 취급하거나 Git에서 제외하지 않는다.

## 별도 결정 전 보존한 소규모 후보

| 경로 | 약 MiB | 판단이 필요한 이유 |
|---|---:|---|
| `Assets/_Legacy/Turtle` | 10.70 | 현행 Soccer의 외부 GUID 참조는 없으나 과거 구현·모델·학습 원본 |
| `Assets/_Legacy/Escape` | 0.39 | 종료된 구현이지만 과거 테스트·참고 소스가 남음 |
| `Assets/_Legacy/ArchivedSoccer~` | 4.28 | 과거 Pass·sample 원본. Unity import 제외 보관소 |

이 세 그룹은 합계 약15.37MiB로 **삭제하지 않았다**. A+B 결정과는 별개이며 향후 참고 필요성을 사용자가 판단할 수 있다. ArchivedSoccer와 Rule의 중복 GUID는 import 제외된 역사적 사본과 현행 에셋의 관계이므로 단순 GUID 검색 결과만으로 런타임 의존이라고 단정하지 않는다.

`Assets/_Soccer/Core/Terrain/StadiumTerrain.asset`는 약9.87MiB이고 Manager 포함12개 프리팹에서 사용한다. 외부 경기장 아트 약12.22MiB는 Builder·씬 참조가 있다. Manager의 구 Models/EvaluationModels에도 Builder 소비자가 있으므로 버전명만으로 삭제하지 않는다. Core의 과거 ONNX 사본도 모델 계보 판단 없이 제거하지 않았다.

## 최신화와 검증

- Manager 진입 문서의 오래된 D5 준비 상태를 현재 r002 2M 학습·토너먼트 완료 및2M champion 상태로 갱신했다. 현재 상태·registry의 기존 변경은 보존했다.
- 빌드 보존 목록을 실제5개에 맞췄다. WorkerIOFix 현행 학습/평가2개, 이전 D4/D5 증거용2개, 공용 SoccerTraining1개다. 이번에는 Player를 추가 삭제하지 않았다. 향후 폐기는 [빌드 관리 기준](build-lifecycle.md)을 따른다.
- 남은 에셋1,831개의 시작 시 SHA-256과 종료 시 SHA-256이 같다. 변경한 에셋 폴더 내 텍스트는 Legacy/Manager README2개뿐이며 삭제는 기본 템플릿15파일이다.
- results4,066개, 기존 Logs81,738개, Builds1,057개에서 누락·길이 변화가 없다. 압축한 로그17,032개는 별도로 전체 내용 해시도 검증했다. 압축하지 않은 모든 results/logs 파일의 전체 해시를 새로 검증한 것은 아니다.
- 현재2M champion, 승인 초기 ONNX·PT 해시가 기존 증거와 일치한다. 현 champion SHA-256: `31b042143e5a01be5e5c2717126e6a967b1c16400d8a4167e72580acde4dc2a1`.
- Git 전체 객체 ID4,564개와 refs·HEAD가 동일하며 `git fsck --connectivity-only --no-dangling`, `git diff --check`가 통과했다. 기존 미커밋 변경은 유지했다.
- 정적 참조·내용 보존 검증이다. Unity compile/PlayMode·학습·성능 검증을 새로 실행하지 않았다.

기계 판독 요약은 [검증·절감 명세](../archive/history/storage-audit-20260926.json), 대용량 원본 목록·삭제 목록·압축 전후 파일별 해시는 `Logs/StorageAudit-20260926`에 있다. 기본 작업에서는 이 보고서와 요약만 읽고 전체 목록은 필요한 항목을 지정해서 조회한다. 로그 경로가 바뀌지 않아 기존 실행 도구·기획 문서의 경로 치환은 필요 없다.

후속 정리는 현재 상태·모델 registry·빌드 보존 목록을 먼저 읽고, 완료된 파일과 재생성 캐시를 구분한다. 새 빌드는 단계·목적·생성 시점·실사용을 기록하며 오래됐다는 이유만으로 자동 삭제하지 않는다. 학습 중인 로그는 압축 대상에서 제외하고, 완료 로그의 개별 파일 압축은 전후 해시 검증을 조건으로 한다.

# 빌드 용량 정리 — 2026-09-25

작업 범위는 `C:\GitHub\Machine-Learning` 내부다. 사용 종료된 빌드를 제거하라는 사용자 지시에 따라 기존 전량 보존 방침을 재검토했다. Git 시작 상태는 `84242324a55e2ff82c011cd4e40f0b0377b3d1e4`, 작업 트리는 깨끗했다. commit/push는 수행하지 않았다.

## 결과

**과거 Player129개, 26,820파일을 삭제했다.** 검토된 삭제 루트는47개다. 삭제한 파일의 논리 크기는18,550,564,799bytes(17.277GiB)이며, `Builds`는18,982,133,047bytes(17.679GiB)에서431,568,248bytes(0.402GiB)로 약97.7% 감소했다. 이후 남긴 세 빌드에 작은 생성 이력 JSON을 추가했다. 기록 파일 증가분과 파일시스템 할당 단위 때문에 OS의 여유 공간 변화는 이 수치와 조금 다를 수 있다.

| 삭제 그룹 | 크기(GiB) | 판단 |
|---|---:|---|
| `MNG_MS` | 7.983 | 구 MS0~MS3·평가·Checkpoint Duel·최종 리뷰, 현행 v3의 학습/평가 입력 아님 |
| 구 MNG M 단계 | 3.061 | Training/Attack/Defense/Fallback 및 M1/M2 평가 복제본, 현행 MS v3로 대체 |
| 과거 SoccerTraining 스냅샷 | 3.111 | 이전 r010/r011/r014/r016/r017 및 L2 진단 복제본, 마지막 공용 빌드 별도 보존 |
| 구 `MNG_V2` | 2.927 | R5R6·PostR6·stability·common-possession 중간 runtime, 현행 forward-pass-center로 대체 |
| Escape Player | 0.194 | Legacy 실험 실행 생성물, 소스·에셋은 보존 |

전체 경로129개, 주요 파일 SHA와 원본 메타데이터 내용은 [삭제 명세](../archive/history/build-cleanup-20260925.json)에 있다. 개별 파일26,820개의 경로·크기·시각은 `Logs/BuildCleanup-20260925/files-before.json`에 보관했다. 상세 명세는 기본 문서 Context에서 제외한다.

## 보존 판단

현재 상태, MS v3 운영 기준, D4·D5 보고서, v3 registry, 원래 D5 manifest, 도구 색인, 기존 선수 학습 운용 문서를 교차 확인했다. 최신이라는 이름만으로 판단하지 않았다.

- `Builds/MNG_V2/MS2-forward-pass-center-20260923/MS3V2`: D5 학습 준비의 현행 Player.
- `Builds/MNG_V2/MS2-forward-pass-center-20260923/EvaluationV2`: 현행 평가·시연 Player.
- `Builds/SoccerTraining`: 기존 선수 학습·평가 도구가 참조하는 마지막 공용 빌드. r017은 미승인 종료 상태이며 학습을 재개하지 않는다.

현행 MNG runtime SHA는 `30aa9b0c9ce224de2d9e9b66457f258fc1bdaef2f3a8ef6069bb6f2194ac775a`다. 폴더의 V2 명칭은 v3에서도 쓰는 ABI라서 보존했다. 초기 MS2 actor·ONNX, 제외된 구400k를 포함한 모델 계보, results·Logs의 원본 검증 자료, source snapshots, registry/manifest를 유지했다.

전체 용량 조사에서 `Logs` 약8.75GiB, `results` 약5.26GiB, `.git` 약4.60GiB, `Library` 약3.01GiB, `Assets` 약65.53MiB였다. Logs/results는 원본 증거·모델, .git은 커밋 이력, Library는 현행 Unity 생성 캐시이므로 이번에 제거하지 않았다. Assets의 Legacy/외부 아트도 사용 종료가 확인되지 않아 유지했다. 대부분의 여유 공간은 원본 게임 에셋을 제거하지 않고 과거 실행 파일에서 확보했다.

## 삭제와 재현의 경계

모든 삭제 경로의 절대 위치가 프로젝트 Builds 내부임을 확인하고 보존 경로·상위 경로·재분석 지점을 차단했다. 실행 중 Player가 없음을 확인했으며 검토 이후 파일 크기/시각/개수 변화가 없을 때만 삭제했다. 준비 단계의 PowerShell 자료형·시각 비교 오류는 삭제 루프 진입 전에 중단됐고 수정 후 전체 검사를 다시 통과했다.

삭제 전 각 Player의 실행 파일·level0·게임 runtime SHA, 루트 build-info/설명/실행 안내를 보관했다. 삭제 빌드 내부의 구 `MS3-Final-Review/SmokeLogs` 임시 출력2개(총3,348bytes)도 함께 삭제됐으며 경로·크기는 파일 명세에 남아 있다. 프로젝트 `Logs` 및 `results`의 원본은 삭제하지 않았다.

과거 바이너리 그대로의 재실행은 이제 불가능하다. 보관한 해시·메타데이터는 바이너리 백업이 아니다. 구 M/MS·v2 smoke·PostR6 진단 스크립트의 역사적 경로를 현행 빌드로 바꾸지 않았고, 해당 명령의 실행 제한을 Tools 색인에 명시했다. 새 빌드로 과거 실험을 실행해 동일 조건 재현으로 주장하지 않는다.

## 이후 관리

[관리 기준](build-lifecycle.md)과 [보존 목록](build-retention.json)을 추가했다. AGENTS·문서/Tools 색인·파일 구조·현재 상태·Builder 안내에도 연결했다.

- `Tools/Build-MNGCurrent.ps1`: 단계·목적 필수, UTC 시각을 포함한 새 디렉터리 생성, 기존 Builder 호출 후 두 Player의 생성 기록 자동 등록. `-PlanOnly`로 경로만 확인 가능.
- `Tools/Build-Lifecycle.ps1`: Register로 생성 기록, Use로 실제 사용 근거, Review로 보존/미등록/미사용 검토. 원래 build-info와 manifest는 수정하지 않는다.
- 새 빌드·단계 전환·용량 정리 시 검토한다. 수명이 지났다는 이유만으로 자동 삭제하지 않으며, 경과일 기준이 필요하면 호출자가 `-UnusedDays`로 지정한다.

## 검증

- 남긴 세 빌드의 기존641파일 SHA가 삭제 전후 모두 일치했다. 소급 기록 sidecar3개만 추가했다.
- v3 registry의 초기 actor·ONNX SHA가 일치했다. 게임 C#·씬·프리팹·모델·Packages·ProjectSettings 및 기존 실행 도구의 내용은 이번 작업에서 변경하지 않았다.
- `Tools/Test-BuildLifecycle.ps1`의12검사 통과: 프로젝트 밖/경로 탈출 거부, 미등록 사용 거부, 등록 덮어쓰기 거부, 사용 기록 분리, 장기 미사용 표시, 보존 대상 유지, Review 무삭제, PlanOnly 무실행·무생성.
- 새 PowerShell 도구3개 구문 검사와 `git diff --check`가 통과했다. 변경 문서13개의 로컬 링크140개가 모두 존재했다. 최종 결과는 `Logs/BuildCleanup-20260925/validation.json`에 기록했다.
- Unity 실행·새 Player 빌드·경기·학습은 수행하지 않았다. 새 빌드 래퍼의 실제 Unity 빌드 성공을 이번 결과로 주장하지 않는다.

### 기존 D5 준비 검증에서 발견한 별도 문제

기존 `launch-first-100k.ps1 -ValidateOnly`는 `Prepared source changed; revalidate`로 중단됐다. 기존97소스 중57개는 원래 SHA와 일치했고40개는 불일치했다.35개는 CRLF/LF 차이만으로 원래 SHA와 일치한다. 나머지5개는 단순 줄바꿈 통일만으로 판별되지 않았다: `MNG_MS3V2.yaml`, `prepare_mng_ms3_experiment.py`, `mng_v2_evaluate.py`, `MNG_ManagerAgent.cs`, `MNG_V2EvaluationController.cs`.

불일치 파일은 이번 작업에서 수정하지 않았으며 Git 기준 소스 변경도 없다. 원래 manifest와 소스는 그대로 두었다. 학습을 시작하기 전에 차이 확인·준비 재검증이 필요하다. 날짜가 지난 준비 완료 보고서를 현재 실행 가능 판정으로 재사용하지 않도록 current-status에 반영했다. 기존 검증을 무효로 덮어쓰거나 hash만 바꿔 우회하지 않았다.

| 증거 | 위치 |
|---|---|
| 보존/폐기 근거·Player별 SHA·원래 메타데이터 | [삭제 명세](../archive/history/build-cleanup-20260925.json) |
| 사전 목록·삭제 루트/근거 | `Logs/BuildCleanup-20260925/{files-before,plan}.json` |
| 보존 빌드641파일 SHA | `Logs/BuildCleanup-20260925/protected-before.json` |
| 원래 빌드 설명/manifest 사본 | `Logs/BuildCleanup-20260925/metadata/` |
| 삭제 결과·후속 검증 | `Logs/BuildCleanup-20260925/{result,validation}.json` |
| 기존 D5 source SHA 차이 | `Logs/BuildCleanup-20260925/prepared-source-audit.json` |

`cleanup.ps1`은 완료된 삭제의 일회성 기록이며 재실행하지 않는다. 앞으로의 검토는 읽기 전용 `Build-Lifecycle.ps1 -Action Review`를 사용한다.

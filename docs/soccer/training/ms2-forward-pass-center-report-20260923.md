# MS2-v2 / 경기 규칙 재정비 — 전방 패스·중앙 드리블 (2026-09-23)

## 목적과 단계 위치

MS2-v2 준비 완료 정책(200,120 step)을 동결한 채 사용자가 지정한 네 가지 경기 규칙을 수정한다. MS3-v2 초기 자기대전 이후 안정화 작업에서 파생된 **MS2 공통 실행 규칙 재정비**이며, 별도 알파벳 단계는 신설하지 않는다.

- MS 전체: MS2-v2 준비 완료 정책 기준 실행 규칙 수정. MS3-v2 전체 완료나 대규모 학습 완료를 의미하지 않는다.
- 개편 R1~R6: 이전 실행·검증 이력 보존. R7~R8은 미진입.
- MS3-v2 안정화 D1~D5: 이전 runtime에서 완료한 이력 보존. 새 규칙에는 새 D3 기술 회귀와 경기 smoke가 필요하며, 이전 D4 통계와 D5 manifest를 그대로 현행 승인으로 사용할 수 없다.
- 이번 작업의 신규 학습, optimizer update, 모델 승격: 0.

## 적용 규칙

1. 자기 골문 앞 적극 걷어내기는 유지한다. MNG와 Core의 자동 선수는 골키퍼/필드, Neural/규칙형/fallback 구분 없이 기존 위험 범위에서 골문 반대 방향 패스 또는 강한 중앙 걷어내기를 시도한다. 수동 사람 입력 소유권은 유지한다.
2. 2초 정체에 따른 강제 패스는 제거한다. 0.1m 최고 전진 위치 타이머 클래스와 테스트도 제거했다. 그 규칙을 기다리기 위해 추가했던 소유 유지 연장도 제거하고, 기존 비전진 제어 상실 0.12초 경로를 복구했다. 킥 준비/수신의 2초는 별개 기술 예산으로 유지한다.
3. 감독이 **PassBuild를 선택했을 때** 전방의 유효 동료에게 패스를 강제 준비한다. 다른 감독 선택을 자동으로 PassBuild로 바꾸지는 않는다. 기존 기술 범위5~28m, 상대 경로 여유1.25m를 적용한다. 상대 골문 중심에서24m 이내는 기존 슈팅 범위이므로 PassBuild 대상에서 제외한다. 예측 수신 위치의 기존 중앙 쪽1.5m 편향 및 전진 lead를 유지한다.
4. 전진 드리블의 목표를 현재 공 위치에서 중앙 쪽으로 최소 기존 횡이동 폭3m만큼 유도한다. 이미 더 중앙인 목표는 유지하고 중앙선을 넘어 반대 벽 쪽으로 이동시키지 않는다. MNG v1/v2의 정책형·규칙형·fallback이 공유하는 계획 경로에 적용한다. ProtectBack의 횡방향 공 보호와 골키퍼 기술은 해당 전진 명령과 구분한다.

패스 준비는 후속 감독 명령으로 즉시 덮어쓰지 않고 최초 임무 ID와 원래2초 만료를 유지한다. 준비 도중 경로 차단/전방 조건 상실/골문 근처 진입/소유 상실/기한 만료가 발생하면 취소한다. 실제 킥판 타격 직전에도 살아 있는 선수 위치로 재검증한다. 취소는 해당 패스 임무만 끝내며 대체 감독 명령을 만들어내지 않는다. 실제 타격 후에는 수신자에게2초 수신 예산을 부여한다. 성공이나 소유권을 조작하지 않는다.

자기 골문 앞 걷어내기는 이 공격 패스 조건과 분리하여 우선 적용한다. 따라서 위험 지역의 걷어내기를 공격 PassBuild의 차단 조건으로 없애지 않는다. 보상액, 관측244/행동6, Core 관측379/행동[3,3,3,3]은 유지한다. `commonPassAttempts`와 `commonNoTargetAttempts`는 기존 결과 reader 호환용으로 남지만 폐지된 정체 규칙이므로 항상0이다.

## 구현 파일

- `MNG_TacticalTargetResolver`: 앞선 동료·실제 예측 타격점의 경로·골문 거리 조건.
- `MNG_TeamPlanner` / `MNG_TeamPlannerV2`: 유효 패스 임무와 중앙 편향 전진 목표.
- `MNG_CommonPossessionRules`: 걷어내기 유지, 선택한 PassBuild의 기한·수신자·취소 관리. 정체 타이머 제거.
- `MNG_PlayerSkillExecutor` / `MNG_KickPlate` / `MNG_BallControl`: 명령 출처와 임무 ID 보존, 타격 직전 재검증, 정체용 소유 연장 제거.
- Core `AgentSoccer` / `SoccerEnvController`: 자기 골문 앞 공통 개입만 유지.

## 검증과 실패 처리

검증 증거 디렉터리: `Logs/MNG-Rebuild/MS2-forward-pass-validation-20260923`.

- 첫 EditMode: 361개 중354통과/7실패. 이전 규칙의 뒤쪽 동료 허용, 막힌 패스 허용, 골문 근처 패스 허용 및 오래된 배치 기대값을 확인했다. 새 계약에 맞는 전방·열린 경로 배치와 금지 조건 assertions로 정리했다.
- 두 번째 EditMode: 361/361 통과. 이후 폐지된 정체 타이머의4개 테스트를 제거했으므로 최종 회귀 총수는 별도 기록한다.
- 집중 PlayMode: 3/3 통과. 12개 양 진영/골키퍼·필드의 새 패스·걷어내기 물리 상황, 정체 미개입/선택한 패스 지속/차단 시 취소, 기존22개 킥 기술 상황 포함.
- 기존22개 기술 검사는 `IssueTechnicalPass`로 실제 executor/킥판/수신 파이프라인을 직접 호출한다. 뒤쪽 패스의 물리적 가능성과 감독에게 허용되는 전술 선택을 혼동하지 않기 위한 분리다. 전략 조건 우회는 테스트 fixture 내부에만 있으며 런타임 설정으로 추가하지 않았다.
- Python 평가 도구: 31/31 통과.
- 기존 동결 입력121개: SHA-256 모두 불변.

최종 결과:

| 검증 | 결과 |
| --- | --- |
| 최종 전체 Soccer EditMode | **357/357 통과** (`edit-final.xml`) |
| MNG + Core + Stadium PlayMode | **54 통과, 실패0, 기존 수동 진단2 제외** (`play.xml`) |
| Python 평가 도구 | **31/31 통과** |
| Core Builder | Base/Attack/Defense/Press/Rule 공통 경기장 계약 및 L0~L3 커리큘럼 검증 통과 |
| 새 Player | MS3V2, EvaluationV2 빌드 성공·프로세스 exit0 |
| 동결 MS2 자체 경기 | **4 paired seed, 8경기, 각300초 정상 완료**, 무결성 true |
| 모델·설정 보존 | 동결 입력121개 SHA 불변. ProjectSettings/EditorBuildSettings 시작 전 SHA 복원·일치 |

PlayMode 제외2개는 기존 `FirstContactWithoutGameplayCallbacks`와 `FixedRecoverFirstContactReplaySeparatesMirrorAndRegistration`이다. 각각 명시적 native 진단/replay 출력 인자가 필요한 수동 진단이며 이번 실패를 제외한 것이 아니다.

새 runtime은 `MNG-MS2-forward-pass-center-20260923`, MNG.Runtime.dll SHA-256은 `30aa9b0c9ce224de2d9e9b66457f258fc1bdaef2f3a8ef6069bb6f2194ac775a`다. 빌드는 `Builds/MNG_V2/MS2-forward-pass-center-20260923/{MS3V2,EvaluationV2}`에 있다. 빌드 입력 소스39개는 `built-source`/`built-source-manifest.json`으로 보존했다. 이후 `AgentSoccer.cs`의 공백만 있는 줄1개를 정리했으며 동작 코드는 바뀌지 않았다. Unity가 자동 추가한 analytics define만 시작 전 상태로 복원했다.

## 동결 경기에서 확인한 범위

`Logs/MNG-Rebuild/MS2-forward-pass-smoke-20260923/report.json`과 검증 디렉터리의 `smoke-audit.json`을 근거로 한다. 두 팀은 같은 MS2 준비 완료 ONNX(SHA `3e8cd87b21ce50d1dd45384ce9f28145442123dbfb41aed6ee0ae6ce12ec5fd7`)와 새 규칙을 사용했다. seed594001~594004를 후보 진영 교대하여8경기 실행했다. 각 실제 종료 시간은300.014초이며 학습0이다.

- 폐지한 정체 강제 패스 시도: **양 팀 합산0회**.
- 양 팀 전체 실제 패스 타격: **18회** = 자기 골문 앞 걷어내기17 + 감독 선택 PassBuild1.
- 양 팀 공통 걷어내기: **135개 준비 요청, 실제 타격18회**(패스17/강한 킥1). 준비와 타격을 같은 성공으로 집계하지 않는다.
- 양 팀 감독 PassBuild 선택: **3회**, 실제 타격1회, 준비 중 취소2회. 집중 물리 fixture에서는 선택된 유효 패스가 실제 타격·수신까지 이어지는 것을 확인했다. 경기 로그의 `CancelledMatchState`는 조건 변경의 세부 사유를 분리하지 않으므로, 이2개 모두가 상대 경로 차단 때문이라고 단정하지 않는다.
- 기존 평가기 후보 진영 집계: 실제 패스12회, 지정 수신5회. 이는 양 팀 합산이 아닌8경기의 지정 후보 쪽만 센 수치다.
- 경기 승패: Red5승 / Navy2승 / 무승부1. 동일 정책 자체 경기8개는 성능 향상이나 진영 공정성의 승인 표본으로 충분하지 않다.
- 기존 정책의 자발적 PassBuild 선택 빈도는 여전히 낮다. 새 규칙이 정책의 선택 확률을 학습시킨 것은 아니다. 총 패스18회를 정책 자발 선택18회로 보고하지 않는다.

## 현재 판단과 다음 단계

요청한 네 가지 실행 규칙 수정과 자동·물리 회귀, 새 Player 빌드, 동결 경기 smoke는 **완료**했다. 현재 확인된 회귀 실패는0이며 이를 이유로 추가 기능 수정을 할 필요는 없다. 다만 대규모 학습 준비 승인까지 완료했다고 보지는 않는다. 기존 정책의 낮은 패스 선택 빈도와 실제 경기 품질은 새 runtime의 충분한 paired D4 평가로 확인하고, 그 결과로 D5의 한정 학습 manifest를 갱신해야 한다. 기존400k resume나 수백만 step 학습을 이번 결과만으로 시작하지 않는다.

단계 이름은 **MS2-v2 / 경기 규칙 재정비 / 전방 패스·중앙 드리블 완료**로 통일한다. R1~R6 이력 완료, R7~R8 미진입. MS3-v2 안정화 D1~D5의 이전 완료 이력은 보존하되, 이번 runtime의 D3 기술 회귀는 재통과했고 D4는8경기 smoke까지만 실행했으며 D5 현행 manifest 갱신은 미실행이다.

새 규칙의 MS2 자체 경기를 1배속·5분으로 볼 때는 프로젝트 루트에서 다음 명령을 사용한다. 이번 구현 작업에서는 가시 창을 별도로 열지 않았다.

```powershell
& C:/Users/USER/miniconda3/envs/mlagents/python.exe -B Tools/watch_mng_v2.py --ms2-self-play --build Builds/MNG_V2/MS2-forward-pass-center-20260923/EvaluationV2
```

기존 관전 도구의 기본 경로는 이전 비교 runtime을 보존하며, 새 `--build` 옵션으로 위 runtime을 명시한다. 옵션 파싱과 Python 구문을 확인했다.

## 보존과 다음 단계

시작 전 dirty/untracked 소스3741개 inventory와 사본은 `Logs/MNG-Rebuild/MS2-forward-pass-start-20260923`에 보존했다. 이전 Player, ONNX/PT, 이전80경기 성적과 보고서는 역사 증거로 유지한다. 이번 전방 패스·중앙 드리블의 경기 성능 향상은 기존 성적만으로 주장할 수 없다. 대규모 학습 전에는 이 runtime으로 D4의 충분한 paired 평가와 D5의 새 runtime/source/model manifest를 갱신해야 한다.

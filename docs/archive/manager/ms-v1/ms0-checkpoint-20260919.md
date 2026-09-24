> 보관일: 2026-09-24. 작성 당시의 계획·판정·승인 이력이며 현재 실행 지시가 아닙니다. 현행 기준: [현재 상태](../../../soccer/current-status.md).

# MS0 중단 체크포인트 — 2026-09-19

2026-09-23 사용자 지시: 5시간 및 주간 잔여량에 따른 사전 중단·재개 제한은 모두 폐지했다. 과거 사용량/중단 기록은 당시 사실이며 현재 실행 조건이 아니다. 서비스 자체 제한과 별개로 RAM/NaN/crash/데이터 무결성 및 정상 저장 규칙은 유지한다.

> **해소됨:** 사용자가 MS0 완료 작업을 재개했고 2026-09-19에 MS0가 완료되었다. 현재 결과는 [MS0 완료 보고서](ms0-completion-20260919.md)와 [사용자 확인 체크리스트](ms0-user-checklist.md)를 따른다. 사용량 비율 중단 기준은 2026-09-23에 폐지했다. 아래 내용과 `2%` 기준은 당시 중단 원인을 보존한 과거 기록이다.

## 중단 이유와 실행 상태

사용자가 작업 중단과 다음 작업용 상태 저장을 지시했다. 마지막 사용량 확인은 5시간 75% 사용(잔여 25%), 주간 99% 사용(잔여 1%)이다. 주간 잔여량 `2% 이하` 중단 규칙도 충족했다. reset credit은 사용하지 않았고 자동 재개·새 작업·학습 프로세스는 만들지 않았다.

MS0 학습 누적 step은 `0`이다. Trainer와 Player는 시작하지 않았고 Windows build도 만들지 않았다. 기존에 열려 있던 Unity Editor는 종료하지 않았으며 마지막 스크립트 컴파일은 성공했다. 후속 작업에는 사용량 비율의 재개 조건을 적용하지 않는다.

## 구현되어 작업 트리에 남은 것

| 범위 | 파일 / 현재 기능 |
| --- | --- |
| MS 모드 | `Runtime/MNG_MSController.cs`: R0 상대/셀프플레이 모드, 20초 smoke, time scale 10, worker 포트 기반 seed, 단일 제어권 검사, worker별 JSONL |
| R0 강도 | `Runtime/MNG_MSOpponentProfile.cs`: Rescue `0.20×/2.0초`, Easy `0.35×/1.5초`, Medium `0.60×/1.0초`, Full `1.00×/0.5초` 고정 |
| 공통 연결 | `MNG_PlayerMotor` 속도 배율, `MNG_RuleBasedManager` 판단 주기, `MNG_MatchController` worker seed와 self-play 종료 모드, `MNG_RewardEngine` 최종 승무패 보상 |
| 자산 Builder | `Editor/MNG_MSBuilder.cs`: 네 Profile, Train/Evaluation/SelfPlay Scene, Validate, Windows build와 SHA256 `build-info.json` |
| 생성 자산 | `Curriculum/MS_ManagerSimple/Scenes/`의 Scene 3개, `Profiles/MS/`의 Profile 4개와 Unity `.meta` |
| 학습 계약 | `Training/MNG_MS0_R0Smoke.yaml` 1,280 step, `ParallelSmoke` 256 step, `SelfPlaySmoke` 2,048 step. 총 예정 smoke는 4,096 step으로 5,000 상한 이내 |
| 도구 | `Tools/MNG_MS_Snapshot.ps1`, `MNG_MS_Build.ps1`, `MNG_MS_Train.ps1`. 열린 Editor가 있으면 Pipeline eval을 쓰고 없으면 batch Editor를 사용 |
| 테스트 | `Tests/EditMode/MNG_MSContractTests.cs`와 `MNG_RuntimePlayModeTests.cs`의 MS0 Scene 2개 검사 |

기존 dirty R0/M 코드·Scene·모델·Run은 원복하거나 덮어쓰지 않았다. 새 commit/push도 하지 않았다.

## 확인된 증거

- Unity 6000.3.16f1 스크립트 컴파일 성공. 마지막 결과: `Logs/MNG-MS/MS0/final-recompile-status.json`.
- Builder가 MS Profile 네 개와 Scene 세 개를 생성하고 `MNG MS0 VALIDATION PASS`를 반환했다.
- MS EditMode 계약 `8/8` 통과: 네 강도 고정값, Full=현재 R0, self-play terminal 부호, worker별 seed, 이동 배율 유효성.
- PlayMode 첫 실행 `0/2`: trainer가 없는 검사에서 Academy 자동 stepping이 정책 Heuristic을 호출한 테스트 설정 문제.
- PlayMode 두 번째 실행: R0 Scene 검사는 통과했다. 테스트 사이에 첫 Scene Agent가 한 틱 남아 self-play 검사 시작 시 동일 Heuristic 로그가 발생하여 합계 `1/2`였다.
- 각 테스트 끝에서 Manager Agent GameObject를 비활성화하도록 수정했고 최종 컴파일은 통과했다. 이 마지막 수정 뒤 PlayMode 재실행은 하지 않았다.
- 최초 dirty 기준은 `Logs/MNG-MS/MS0/source-snapshot`, `source-sha256.json`, `git-status.txt`, `tracked-working-tree.patch`, `untracked-files.txt`에 289개 파일 기준으로 보존했다.

## 아직 완료되지 않은 MS0 gate

1. 마지막 PlayMode MS0 2개를 재실행하여 `2/2`를 확인한다.
2. 초기 snapshot 뒤 소스가 수정되었으므로 **최종 구현 source manifest를 새 경로로 생성**하고 Builder/build script가 그 경로의 SHA256을 기록하도록 맞춘다. 현재 `Logs/MNG-MS/MS0/source-sha256.json`은 초기 기준 보존용이며 최종 build source 증거가 아니다.
3. R0Smoke와 SelfPlaySmoke Windows Player를 각각 빌드하고 executable/Data/config/protocol/source hash가 일치하는지 확인한다.
4. `ParallelSmoke`를 1→2→8 workers로 각각 256 aggregate step 실행한다. worker JSONL의 서로 다른 port/process/seed와 episode 완료, 포트 범위, RAM을 확인한다.
5. R0Smoke를 최소 첫 checkpoint 뒤 정상 중단하고 같은 Run에 `-Resume`하여 1,280까지 증가시킨다. PPO update, `.pt`, `.onnx`, resume 전후 step을 보존한다.
6. SelfPlaySmoke 2,048 step에서 양 TeamId, ghost snapshot 생성, team switch, 최종 reward 부호, `.pt`와 `.onnx`를 확인한다.
7. 위 실행 합계가 5,000 aggregate manager step 이내인지 보고서로 확정한 뒤에만 MS0 완료로 변경한다.

## 다음 세션의 정확한 시작 순서

1. 사용량 비율에 따른 실행 제한은 폐지했다(2026-09-23). 오류·중단 시 증거와 재개점을 보존한다.
2. `git status --short`와 이 문서의 파일을 확인한다. 기존 결과나 초기 snapshot을 삭제하지 않는다.
3. 최종 source manifest 경로 문제를 먼저 해결한다. `MNG_MSBuilder.cs`와 `MNG_MS_Build.ps1`이 새 최종 snapshot 경로를 같은 값으로 사용하도록 수정한 뒤 `MNG_MS_Snapshot.ps1 -OutputDirectory <새 경로>`를 한 번 실행한다.
4. 열린 Editor가 연결되면 다음 두 명령으로 최종 Scene 검사를 재실행한다.

```powershell
& 'C:\Users\USER\AppData\Local\Unity\bin\unity.exe' command run_tests --project-path 'C:\GitHub\Machine-Learning' --timeout 60 --mode PlayMode --filter 'MS0' --filter_type testName --async_tests true --format json
& 'C:\Users\USER\AppData\Local\Unity\bin\unity.exe' command test_status --project-path 'C:\GitHub\Machine-Learning' --timeout 60 --format json
```

5. `2/2` 뒤 `Tools\MNG_MS_Build.ps1 -Profile Both`를 실행한다. 스크립트와 build manifest의 최종 source 경로가 맞지 않으면 빌드하지 않는다.
6. 빌드가 통과한 뒤 `Tools\MNG_MS_Train.ps1`의 `-ValidateOnly`를 세 stage에서 먼저 실행하고, 1→2→8 parallel, R0 중단·resume, self-play 순으로만 진행한다. 기존 Run에 `--force`를 쓰지 않는다.

MS0 완료 선언에 필요한 PT/ONNX/update/resume/self-play 증거가 전혀 생성되지 않았으므로, 현재 상태를 `완료`나 `학습 연결 통과`로 표현하지 않는다.

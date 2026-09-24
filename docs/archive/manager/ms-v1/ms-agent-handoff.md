> 보관일: 2026-09-24. 작성 당시의 계획·판정·승인 이력이며 현재 실행 지시가 아닙니다. 현행 기준: [현재 상태](../../../soccer/current-status.md).

# MS 후속 개발 에이전트 지침

> **2026-09-23 현행 인계:** [MS2 완료 기준 공통 경기 규칙 보강](../ms-v2/ms2-common-possession-report-20260923.md)을 먼저 읽는다. 2초 정체 강제 패스·공통 골문 앞 걷어내기와 정지 소유 충돌을 수정하고 동결80경기를 비교했다. `MNG-MS2-common-possession-stop-20260923`이 현행이다. R1~R6 및 이전 D1~D5 완료 이력은 보존하되, 새 규칙의 D5 manifest·32환경 연결·상대 규칙 배정 확인 전 이전 학습 스크립트를 실행하지 않는다. 학습/optimizer/승격은0이며 이번 승점률 차이의 CI는0을 포함한다.

2026-09-23 사용자 지시: 5시간 및 주간 잔여량에 따른 사전 중단·재개 제한은 모두 폐지했다. 과거 사용량/중단 기록은 당시 사실이며 현재 실행 조건이 아니다. 서비스 자체 제한과 별개로 RAM/NaN/crash/데이터 무결성 및 정상 저장 규칙은 유지한다.

> 2026-09-22 우선 적용: 구조 개편 작업은 [새 주 계획](../ms-v2/ms-rebuild-plan-20260922.md), [런타임 계약](../ms-v2/ms-rebuild-runtime-contract-20260922.md), [학습·승격 계약](../ms-v2/ms-rebuild-training-validation-20260922.md)을 먼저 따른다. 이 문서의 기존 모델/133관측/일정/예산은 v1 이력이며 충돌 시 새 계획이 우선한다. 기존 MS3 모델은 선택 자격 폐기·증거 보존 상태다. 현재는 계획만 완료했고 개발은 미착수다.

작성: 2026-09-19. MS0는 구현·빌드·병렬·resume·self-play 연결을 완료했다. 먼저 [MS0 완료 보고서](ms0-completion-20260919.md)를 읽고, [MS 계획](ms-plan.md) → 이 문서 → [평가 계약](ms-validation.md) 순서로 확인한다. [MS0 중단 체크포인트](ms0-checkpoint-20260919.md)는 완료 전 과거 이력이다. 최신 완료/차단 상태는 [현재 상태](../../../soccer/current-status.md)를 따른다.

## 시작 시 읽을 것과 금지할 것

1. 프로젝트 루트 `AGENTS.md`, 위 세 문서, 현재 `git status --short`를 읽는다. 실행 승인은 후속 사용자 지시의 범위를 따른다. 이 문서가 이번 계획 세션에 개발 권한을 부여하지 않는다.
2. R0 사용자 완료 판정을 존중한다. 새로운 R0 개선 목록이나 기존 M1 재학습을 시작하지 않는다. 기존 dirty 코드·Scene·Prefab·미추적 R0 파일·결과를 보존한다. 자동 commit/push/일괄 원복/강제 삭제 금지.
3. Git HEAD만으로 R0 기준을 보존할 수 없다. 학습 시작 전에 **실제 작업 트리**의 런타임/Builder/프로필/씬/패키지·설정과 관련 `.meta`를 MS source snapshot으로 보존하고 SHA256 manifest를 만든다.
4. 준비부터 self-play 연결까지 한 구현 담당자가 순서대로 처리한다. 별도 작업/하위 에이전트 자동 생성은 하지 않는다. 새 기능은 기존 공통 구현을 먼저 찾아 재사용한다.

## 현재 소스로 확인한 사실

| 경로(프로젝트 기준) | 확인 사실 / MS 작업 |
| --- | --- |
| `Assets/_Soccer/Manager/Runtime/MNG_ManagerAgent.cs` | BehaviorName `MNG_Manager`, DecisionPeriod 25, 명령 branch 1개. Initialize가 이름을 덮어쓰므로 Inspector만 바꿔 MS 이름 분리하지 말 것 |
| `Runtime/MNG_ObservationWriter.cs` (이하 Runtime은 Manager 하위) | 133-float, 팀 기준 관측, 공 속도 정규화는 현재 KickSolver 상수. 좌우 대칭과 슬롯 대응을 시험 |
| `Runtime/MNG_TeamPlanner.cs` | PlannerVersion 9. 공통 압박·공격 지원·정체 슈팅이 명령보다 우선할 수 있음 |
| `Runtime/MNG_RuleBasedManager.cs` | RuleVersion 0, 판단 0.5초, 공통 snapshot/target resolver/명령 경로. MS 강도 프로필은 없음 |
| `Runtime/MNG_MatchController.cs` | Policy와 Rule 명령 수신 경로, EndEpisode/Interrupted 존재. 모드별 단일 명령 writer와 종료 순서 확인 필요 |
| `Runtime/MNG_RewardEngine.cs`, `MNG_RewardProfile.cs`, `MNG_TacticalRewardTracker.cs` | 실제 사건 보상과 중복 방지 재사용. self-play 종료 reward 부호는 별도 검증 필요 |
| `Editor/MNG_ProjectBuilder.cs` | 팀별 TeamId 설정과 R0 생성 경로 존재. 기존 자산 전체 재생성 없이 MS만 생성 |
| `Editor/MNG_TrainingBuildBuilder.cs`, `Tools/MNG_Build.ps1` | 기존 M 단계 전용으로 유지한다. MS0는 별도 `MNG_MSBuilder`와 `MNG_MS_Build.ps1`을 사용한다. |
| `Tools/MNG_Train.ps1` | 기존 M stage 전용으로 유지한다. MS0는 1개 이상 환경과 port 범위를 지원하는 `MNG_MS_Train.ps1`을 사용한다. |
| `Runtime/MNG_CurriculumController.cs` | `Time.timeScale` 설정 경로 존재. MS 엔진 설정과 중복 적용되지 않도록 하나의 소유자만 사용 |
| `ProjectSettings/ProjectVersion.txt`, `Packages/manifest.json` | Unity 6000.3.16f1, Unity ML-Agents 4.0.3. Python 패키지 버전은 별도 확인 |

## 파일 책임과 최소 산출물

| 상태 | 담당 단위 | 경로 / 책임 |
| --- | --- | --- |
| MS0 완료 | MS 모드 | `Runtime/MNG_MSController.cs`: R0 상대·self-play 모드, episode seed·종료, worker별 JSONL. 공통 물리/판정 복제 금지 |
| MS0 완료 | 상대 강도 | `Runtime/MNG_MSOpponentProfile.cs`: MS 전용 강도·명령 주기·이동 제한. Full에서 원래 R0와 동일 |
| MS0 완료 | MS 빌드 | `Editor/MNG_MSBuilder.cs`: Scene·Profile 생성, Validate, 전용 Windows build·manifest |
| MS0 완료 | Scene | `Curriculum/MS_ManagerSimple/Scenes/MNG_MS_Train.unity`, `MNG_MS_Evaluation.unity`, `MNG_MS_SelfPlay.unity` |
| MS0 완료 | smoke 설정 | `Training/MNG_MS0_*.yaml`, `Evaluation/MNG_MS0_Protocol_v1.json` |
| MS0 완료 | 실행 도구 | `Tools/MNG_MS_Snapshot.ps1`, `MNG_MS_Build.ps1`, `MNG_MS_Train.ps1` |
| 후속 | MS1~MS3 설정·평가 | 단계별 YAML, 혼합 reset, 고정 evaluator와 protocol. 기존 MS0 파일을 확장하거나 작은 전용 파일을 추가한다. |
| 공통 | 산출물 | `Builds/MNG_MS/<build-id>/`, `results/MNG_MS<stage>-YYYYMMDD-rNNN/`, `Logs/MNG-MS/<run-id>/` |

별도 프레임워크를 만들지 않는다. 기존 클래스에 작은 opt-in 연결이 더 간단하면 전용 클래스 수를 줄여도 되며, 책임·R0 기본 동작·MS 프로토콜은 유지한다. 완료로 표시한 MS0 파일은 실제 실행·검증된 도구이며 후속 단계가 임의로 대체하지 않는다.

## 실행 티켓과 의존성

| 순서 | 작업 | 완료 증거 |
| --- | --- | --- |
| T0 | 실제 dirty 기준 보존, 계약·trainer/python/torch/GPU·여유 메모리 확인, 평가 seed 고정 | source/config/build manifest, 기존 변경 목록 |
| T1 | 공유 기술층에 MS 모드/상대 강도/단일 제어권/보상 연결, 명령 영향 확인 | 필수 EditMode·PlayMode, R0 Full 동등성, 행동 추적 |
| T2 | MS 전용 Scene·build·병렬 launcher·고정 evaluator | compile/Validate/build, 1→2→8 환경 smoke, worker별 독립 episode |
| T3 | PPO 연결·저장·resume, self-play 양 팀 연결/종료 부호/상대 snapshot smoke | update·PT·ONNX·resume·양 팀과 상대 ID 기록, MS0 통과 |
| T4 | MS1 혼합 학습과 고정 상황 평가 | gate 보고, 선택 PT와 학습 증거 |
| T5 | MS2 정상 경기 학습과 R0-Full 평가 | 정상 R0 대전·무작위/초기 대비 결과, 동결 MS2 PT |
| T6 | MS3 self-play 본학습, 회귀와 실제 ONNX 시연 | 상대 교체·팀 교대·최종 평가·사용 가이드 |

T0→T1→T2→T3가 MS0다. T4/T5/T6마다 gate 충족 시 즉시 이동한다. 단순한 metadata 저장 같은 저위험 변경에 테스트를 늘리지 말고, 실제 제어·보상·종료·병렬 격리·물리 영향에 집중한다.

T0→T3은 2026-09-19 완료되었다. 다음 작업은 T4이며 MS0 smoke 모델을 강한 정책으로 승격하거나 MS1 initialize source로 사용하지 않는다. MS1은 계획대로 fresh PPO에서 시작한다.

T4의 상세 구현·학습 순서는 [MS1 개발·학습 계획](ms1-development-plan-20260920.md)을 따른다. 2026-09-20 사용자 결정으로 MS1은 16개 환경 고정이며 아래 MS0 당시의 1→2→8 비교 기록은 새 MS1 worker 수를 바꾸는 근거가 아니다.

## MS0에서 반드시 해결할 감독 영향 문제

- 같은 snapshot에서 사용 가능한 명령을 각각 적용해 `policy output → accepted command → plan → executor → 실제 킥/전진/회수`를 기록한다. 안전 개입 시 원래 명령·대체 사유·영향 받은 선수를 별도 기록한다.
- 공격/수비에서 각각 두 개 이상의 선택지가 실제 목표/실행에 차이를 만드는 대표 fixture를 둔다. 특히 PassBuild와 AttemptShot을 선택했을 때 자동 전진/정체 슈팅만 반복되는지 확인한다.
- 마스크는 불가능한 명령만 제거하고 정답 하나를 강제하지 않는다. 전술적 선택이 가능한 fixture에서 선택지 2개 이상을 확인한다. 모든 명령 균등 사용을 요구하지 않는다.
- 기존 R0 안전 보조는 제거하지 않는다. PPO 제어권 연결 버그면 수정하지만 공통 명령 의미 자체가 붕괴했다면 학습을 돌려 숨기지 말고 차단 근거를 남긴다. R0 동작을 바꾸는 대규모 전술 재설계는 MS 밖이다.

## 병렬 실행과 환경 준비

1. 기존 conda `mlagents`의 `mlagents-learn.exe`를 탐색한다. 현재 도구의 후보는 `C:\Users\USER\miniconda3\envs\mlagents\Scripts\mlagents-learn.exe`다. 버전·torch CUDA 가용성과 YAML 파싱을 확인하고 불필요한 재설치/업그레이드를 하지 않는다.
2. 하나의 Windows executable 안에 경기장 하나를 두고 하나의 trainer가 여러 프로세스를 실행한다. 처음부터 한 Scene에 여러 경기장 복제로 확장하지 않는다.
3. MS0에서는 `1 → 2 → 8` workers를 검사했다. MS1 본학습은 사용자 지시에 따라 **16개 독립 환경 고정**이다. 시작 전 16개 port·process·seed·로그를 검증하고, 불안정하면 8개로 낮추지 않고 정상 저장 후 원인을 보고한다.
4. engine time scale은 10을 첫 후보로 하고 1배속 물리 smoke와 실제 킥/종료가 같은 계약인지 확인한다. `fixedDeltaTime=0.02`를 유지한다. 10에서 문제가 생기면 5→1로 낮춘다. 그래픽은 끄되 물리/센서/판정은 끄지 않는다.
5. CPU/RAM/여유 RAM, aggregate decisions/sec, worker별 episode 수·seed, PPO update, crash/timeout을 기록한다. RAM 사용 85% 이상 또는 여유 4GiB 미만이면 worker를 낮춘다. 낮은 수에서도 불안정하면 본학습 차단.
6. 포트는 base와 **모든 worker가 사용할 전체 범위**를 검사한다. seed는 trainer seed가 custom reset RNG까지 도달하는지 확인하며 worker/episode별로 파생한다. 같은 시작 장면을 모든 worker가 반복하지 않아야 한다.
7. 로그/평가 파일은 run/worker/episode별 경로로 분리하고 하나의 CSV에 여러 프로세스가 동시에 쓰지 않게 한다. R0 프로필은 학습 모드에만 적용, 평가 모드는 Full 강제를 검증한다.
8. 재현 manifest에 실제 사용 executable·Data/level·소스·프로필·YAML·패키지·PT·ONNX SHA256과 dirty snapshot 경로를 남긴다. 소스만 최신이고 build가 옛 버전이면 학습 금지.

## PPO와 정책 인계 초기값

- BehaviorName `MNG_Manager` 유지. `batch_size=256`, `buffer_size=4096`, `learning_rate=3e-4`, `beta=0.005`, `epsilon=0.2`, `lambd=0.95`, `num_epoch=3`, hidden 128×2, normalize false, gamma 0.995, horizon 128. 현재 M3 설정을 출발점으로 쓰되 MS YAML은 별도 생성한다.
- summary 1000, checkpoint 20000. 각 단계 상한은 계획 문서 기준. 병렬 수를 늘려도 aggregate step 상한을 worker 수만큼 곱하지 않는다.
- MS1은 fresh. MS2는 합격 MS1 PT, MS3는 합격 MS2 PT에서 새 Run으로 weights를 가져오고 새 단계 optimizer로 시작한다. ONNX는 inference 산출물이며 optimizer resume 자료가 아니다.
- 동일 단계 중단 재개는 계약/config/hash가 같은 Run에서 `--resume`, 단계 이동은 새 Run의 `--initialize-from`을 사용한다. 설치 trainer의 실제 step/optimizer 초기화 동작을 smoke로 확인하고 단계별 소비 step을 별도 누적한다. 두 옵션 동시 사용·`--force` 금지.

## Self-play 연결을 미리 준비한다

- 양 팀에 같은 `MNG_Manager` BehaviorName, 서로 다른 TeamId(실제 enum 값 확인), 동일 관측/행동 계약을 둔다. 학습 중에는 trainer가 한쪽 학습·반대쪽 snapshot inference를 관리한다. R0와 PPO가 같은 팀 명령을 동시에 쓰지 않게 한다.
- 초기 후보: `save_steps=10000`, `team_change=20000`, `swap_steps=2000`, `window=5`, `play_against_latest_model_ratio=0.5`, `initial_elo=1200`. 이 키들은 설치된 trainer `settings.py`에 존재함을 정적으로 확인했다. 실행 호환성은 T3에서 검증한다.
- seed 정책이 실제 MS2 checkpoint인지, 양 Team ID에 관측/보상/종료가 전달되는지, snapshot이 생성·교체되는지 확인한다. 양 팀에 같은 ONNX를 수동 장착한 시연만으로 self-play 학습이라 하지 않는다.
- 설치된 `mlagents/trainers/ghost/trainer.py`는 **마지막 transition reward 부호로 승/무/패를 추정**한다. 최종 틱 shaping/득점이 무승부·패배 부호를 바꾸지 않게, MS3 전용 terminal 처리에서 마지막 보상을 승 +0.5 / 무 0 / 패 -0.5로 확정하고 즉시 양 팀 EndEpisode를 한 번 호출한다. 마지막 틱의 goal은 통계에 남기고 terminal 우선 계약으로 문서화한다.
- 위 terminal 처리 차이는 MS3에만 적용하고 MS1/2/R0를 조용히 바꾸지 않는다. 이전 보상/변경 보상/변경량과 마지막 틱 goal 동시 발생 처리를 테스트한다. 단순 `AddReward(0)`는 무승부 부호 보장이 아니다.
- R0를 ghost snapshot pool에 섞는 별도 시스템은 만들지 않는다. R0는 독립 평가 상대, snapshot pool은 PPO 과거 정책으로만 구성한다. Elo는 내부 진단 지표이며 최종 강도 증거는 고정 대전 결과다.

## 중단과 최종 인계

단계·gate·차단 원인, run ID/step/누적 예산, 정상 저장 여부, PT/ONNX/hash, build/config, worker/port/seed, 마지막 평가와 다음 정확한 명령을 현재 상태와 Run 보고서에 남긴다. 중단 시 자기 Run의 trainer/worker만 정상 종료하며 광범위한 프로세스 kill을 하지 않는다. 필요한 개발이 미완료이면 예정 명령을 실행 가능 명령이라고 제시하지 않는다.

사용량 비율에 따른 실행 제한은 폐지했다(2026-09-23). 오류·중단 시 증거와 재개점을 보존한다.

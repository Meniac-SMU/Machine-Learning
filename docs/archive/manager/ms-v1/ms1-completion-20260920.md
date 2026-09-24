> 보관일: 2026-09-24. 작성 당시의 계획·판정·승인 이력이며 현재 실행 지시가 아닙니다. 현행 기준: [현재 상태](../../../soccer/current-status.md).

# MS1 완료 보고서 — 2026-09-20

2026-09-23 사용자 지시: 5시간 및 주간 잔여량에 따른 사전 중단·재개 제한은 모두 폐지했다. 과거 사용량/중단 기록은 당시 사실이며 현재 실행 조건이 아니다. 서비스 자체 제한과 별개로 RAM/NaN/crash/데이터 무결성 및 정상 저장 규칙은 유지한다.

## 완료 판정

MS1을 완료했다. 선택한 정책은 `MNG_MS1-20260920-r007-step7443`이며, 약화 규칙형 상대 R0-Easy(`이동 0.35×`, 감독 판단 간격 `1.5초`)를 대상으로 공격·수비/전환 판단을 한 PPO에 학습한 결과다. 독립 Windows Player **16개**를 `--num-envs 16`으로 연결한 학습에서 얻었고, 최종 판정은 훈련 중 보상이 아니라 별도 ONNX 고정 평가로 수행했다. 최종 훈련 Player, 1배속 사람 검수 Player, headless runtime smoke와 완료 manifest도 생성했다.

MS1 완료는 정상 강도의 강한 R0를 이겼다는 뜻이 아니다. 원래 강도 R0-Full과의 정상 킥오프 120/300초 대전은 MS2 범위이며 아직 시작하지 않았다.

## 선택 모델과 다음 단계 입력

| 항목 | 값 |
| --- | --- |
| Run | `MNG_MS1-20260920-r007` |
| 후보 | `MNG_MS1-20260920-r007-step7443` |
| ONNX | `results/MNG_MS1-20260920-r007/MNG_Manager/MNG_Manager-7443.onnx` |
| ONNX SHA-256 | `311f72b3d625d76156ba581ae060610b701d68bd8e69ff2a7b3afee8cc9b4e81` |
| PT | `results/MNG_MS1-20260920-r007/MNG_Manager/MNG_Manager-7443.pt` |
| PT SHA-256 | `ac8eb65dd7c633a6cb5e4dfa267daa92d63c8df5bfecb4031a4fe28c5cb4d3c3` |
| protocol | `MNG-MS1-v8` |
| protocol SHA-256 | `b2bdb219b5a5ed9cc3be92dc0478ec7c7a9593c147a779feb9b417c0afb34158` |

MS2는 반드시 위 `MNG_Manager-7443.pt`를 초기 가중치로 사용한다. r007 Run의 `checkpoint.pt`는 15k 종료 시점의 optimizer를 담은 다른 checkpoint이고 SHA-256도 `6de90cfb6ed808e8b8f436bd4f2d2d80a78aa6c2746678b5eef56052eb5db609`이므로, 선택 모델 대신 사용하면 안 된다. MS2가 optimizer까지 이어받을 수 없다면 번호가 붙은 PT의 정책 가중치로 새 optimizer를 시작하고 그 사실을 매니페스트에 기록한다.

## 최종 고정 평가

v7에서 공격·수비는 통과하고 패스 비교만 남았다. 사용자는 패스 성공 기준을 소폭 낮추고 패스만 문제라면 통과시킬 의향을 명시했다. 이에 v8은 기존 결과를 재분류하지 않고 새 holdout seed `405001/406001`로 다시 평가했다.

| 지표 | ONNX | 무작위 유효명령 | v8 요구 | 결과 |
| --- | ---: | ---: | ---: | --- |
| 공격 성공 | 38/40 | 31/40 | 24/40 이상, baseline 대비 -2 이내 | 통과, +7 |
| 수비/전환 성공 | 38/40 | 36/40 | 20/40 이상, baseline 대비 -2 이내 | 통과, +2 |
| Pass 하위집합 완료 | 4/20 | 4/20 | 2/20 이상, baseline 비열등 | 통과, 동률 |

완료 패스의 의미는 낮추지 않았다. 공이 2.5m 이상 실제로 이동하고 의도한 동료가 0.15초 이상 소유해야 한 건으로 센다. 단순 `PassBuild` 명령, 킥 횟수, 우연한 접촉은 완료가 아니다.

정책의 120 episode 전체 telemetry는 Pass 킥 29회, 완료 패스 10회, 유효 슛 95회, 5m 전진 507회다. 여섯 명령 선택 횟수는 순서대로 `265, 152, 195, 1263, 1817, 442`이며, 특정 한 명령만 내는 정책은 아니다. 이 표본은 빠른 개발 gate이며 모든 상대와 초기조건에서의 통계적 우월성을 증명하지 않는다.

평가 원본은 `Logs/MNG-MS/MNG_MS1-20260920-r007-step7443-eval-v8-final-r001/`에 있다. `comparison.json`, `onnx.json`, `uniform-valid-command.json`, 두 Player log를 모두 보존했다. 최종 평가 빌드는 `Builds/MNG_MS/MS1-Evaluation/MNG_MS1-20260920-r007-step7443/`이며 모델·protocol hash가 평가 결과와 일치한다.

## 학습과 실패 실험 보존

MS1의 공식/보정 Run `r001`부터 `r009`까지 결과·PT·ONNX·trainer log를 삭제하거나 덮어쓰지 않았다. 선택한 r007은 약 7.4k 중간 checkpoint다. 이후 r008·r009에서는 전진 통로 차단 시 PPO가 PassBuild를 직접 고르게 하는 학습 전용 `PassDecisionPolish` 실험을 수행했으나, 공격·수비·패스 gate를 함께 개선하지 못해 선택하지 않았다.

코드에는 다음 작업에서 재사용할 수 있는 제한된 학습 보조가 남아 있다.

- `BlockedForwardPassDecision`은 전진 통로가 막힌 상황에서 패스를 먼저 판단하면 `+0.01`, 해당 구간 cap `+0.01`만 부여한다.
- `-mngMS1LearnPassChoice`는 `PassDecisionPolish` 훈련에서만 코드의 런타임 패스 override를 끈다.
- 일반 추론, 규칙형 R0, 선택된 r007 평가에는 기존 공통 패스 보조가 계속 적용된다. 전진 불가 시 패스를 먼저 검토하고, 수신자 직선 대신 좌우 중 경기장 중앙 쪽으로 약간 비켜 찬다.

따라서 r008·r009는 실패 증거이지 최종 정책의 출처가 아니다. 새 학습을 재개할 때 이 실험을 자동으로 이어서도 안 된다.

## 빌드·소스·검증 증거

- 최종 빌드 입력 소스 스냅샷: `Logs/MNG-MS/MS1-final-source-v19/source-sha256.json`, 385개 파일, manifest SHA-256 `5a1856fe83e5d4d7193213264b50e0cdbeda33b34ebbaf8fb41cd0928179f206`
- 최종 학습 Player: `Builds/MNG_MS/MS1/MNG_MS1.exe`
- 학습 Player 매니페스트: `Builds/MNG_MS/MS1/build-info.json`
- 최종 평가 Player 매니페스트: `Builds/MNG_MS/MS1-Evaluation/MNG_MS1-20260920-r007-step7443/evaluation-build-info.json`
- 1배속 사람 검수 Player: `Builds/MNG_MS/MS1-Review/MNG_MS1_Review.exe`
- 사람 검수 Player 매니페스트: `Builds/MNG_MS/MS1-Review/review-build-info.json`
- 최종 자동 비교: `Logs/MNG-MS/MNG_MS1-20260920-r007-step7443-eval-v8-final-r001/comparison.json`
- 전체 EditMode: `Logs/MNG-MS/MS1/final-v18-editmode.xml`, `242/242` 통과
- 검수 Player smoke: `Logs/MNG-MS/MS1/final-review-smoke-v19.log`, bootstrap 성공·런타임 예외 없음
- smoke episode 증거: `Logs/MNG-MS/MS1/review-smoke-v19-evidence/worker-0-process-17656.jsonl`
- 완료 매니페스트: `Logs/MNG-MS/MS1-completion-20260920/ms1-completion.json`

최종 평가·훈련·검수 Player는 모두 Unity `Succeeded`, error 0이다. 훈련·검수 빌드는 각각 warning 486개를 기록했으며 주로 기존 프로젝트 자산/빌드 경고다. error 0과 테스트 통과를 경고 0으로 잘못 표현하지 않는다. 훈련·검수 매니페스트의 protocol, source, executable, level, `MNG.Runtime.dll` hash를 실제 파일과 다시 대조해 모두 일치함을 확인했다.

초기 v17 빌드에서는 `BuildMS1Assets()`가 생성형 Scene fileID를 다시 써 source snapshot과 달라졌고, v18 빌드에서는 Unity가 `SENTIS_ANALYTICS_ENABLED` define을 처음 추가했다. 두 빌드는 각각 `Builds/MNG_MS/MS1-v17-pre-final`, `Builds/MNG_MS/MS1-v18-pre-final`에 보존했다. 최종 Builder는 Scene 생성과 빌드를 분리해 동결 Scene을 검증만 하도록 수정했고, 안정된 ProjectSettings까지 포함한 v19에서 빌드 뒤 385개 manifest 파일을 전부 재검사해 동일성을 확인했다. 완료 문서는 빌드 입력 동결 뒤 작성되므로 source manifest는 컴파일·빌드 입력의 기준이며 이 보고서의 사후 문구는 완료 manifest에서 별도로 추적한다.

## 완료 범위와 제한

- 통과한 범위: 16환경 MS1 학습 연결, 약화 R0 상대 공격·수비/전환 판단, 실제 ONNX 추론, 실제 사건 기반 평가, 최소 패스 근거, 재현용 hash와 로그 보존.
- 사람 확인 범위: 플레이의 자연스러움, 차단 시 패스 우선 검토, 중앙 쪽 오프셋 패스, 물리 이상 유무. 절차는 [MS1 사용자 확인 체크리스트](ms1-user-checklist.md)를 따른다.
- 남은 범위: R0-Full 대등성, 120/300초 정상 경기, 진영 교환, MS2 승격, MS3 self-play 성능 향상. 이 보고서로 이 항목들을 완료 처리하지 않는다.
- repository에는 MS 이전부터의 관련 변경과 실험 증거가 함께 있는 dirty working tree가 유지된다. 이번 완료 과정에서 commit, push, reset, clean을 실행하지 않았다.

사용량 비율에 따른 실행 제한은 폐지했다(2026-09-23). 오류·중단 시 증거와 재개점을 보존한다.

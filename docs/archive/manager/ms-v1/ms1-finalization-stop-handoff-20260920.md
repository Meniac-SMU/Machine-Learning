> 보관일: 2026-09-24. 작성 당시의 계획·판정·승인 이력이며 현재 실행 지시가 아닙니다. 현행 기준: [현재 상태](../../../soccer/current-status.md).

# MS1 최종화 사용량 중단 인계 — 2026-09-20

2026-09-23 사용자 지시: 5시간 및 주간 잔여량에 따른 사전 중단·재개 제한은 모두 폐지했다. 과거 사용량/중단 기록은 당시 사실이며 현재 실행 조건이 아니다. 서비스 자체 제한과 별개로 RAM/NaN/crash/데이터 무결성 및 정상 저장 규칙은 유지한다.

## 2026-09-20 재개 결과 — 해소

사용량 비율에 따른 실행 제한은 폐지했다(2026-09-23). 오류·중단 시 증거와 재개점을 보존한다.

## 중단 이유

최종 문서와 source snapshot v16을 저장한 직후 Codex 사용량을 다시 확인했다.

- 5시간 창: 사용 96%, **잔여 4%**
- 주간 창: 사용 30%, 잔여 70%
- 사용량 비율에 따른 실행 제한은 폐지했다(2026-09-23). 오류·중단 시 증거와 재개점을 보존한다.
- reset credit: 1개 사용 가능, **사용하지 않음**

5시간 잔여량이 중단선에 도달했으므로 새 Unity 빌드나 smoke를 시작하지 않았다. trainer와 MS1 Player worker는 실행 중이 아니며, 새 학습 Run도 시작하지 않았다.

## 완료된 상태

- 16환경 학습과 모든 r001~r009 증거 보존
- 최종 후보 `MNG_MS1-20260920-r007-step7443` 선택
- v8 새 holdout 자동 평가 통과: 공격 `38/40` 대 random `31/40`, 수비 `38/40` 대 random `36/40`, Pass `4/20` 대 random `4/20`
- 선택 ONNX SHA-256 `311f72b3d625d76156ba581ae060610b701d68bd8e69ff2a7b3afee8cc9b4e81`
- 선택 PT SHA-256 `ac8eb65dd7c633a6cb5e4dfa267daa92d63c8df5bfecb4031a4fe28c5cb4d3c3`
- 최종 평가 Player build `Succeeded`, error 0
- 전체 EditMode `242/242` 통과
- 검수 Scene `Assets/_Soccer/Manager/Curriculum/MS_ManagerSimple/Scenes/MNG_MS1_Review.unity` 생성
- source snapshot v16 저장: `Logs/MNG-MS/MS1-final-source-v16/source-sha256.json`, 384개 파일
- 완료 보고서 초안과 사람 체크리스트 작성

## 남은 작업

아래 작업만 남았다. 추가 학습이나 새 후보 평가는 필요하지 않다.

1. 사용량 비율에 따른 실행 제한은 폐지했다(2026-09-23). 오류·중단 시 증거와 재개점을 보존한다.
2. 이 인계와 중단 상태 문서가 v16 뒤에 추가되었으므로 Builder와 `Tools/MNG_MS1_Build.ps1`, `Tools/MNG_MS1_Train.ps1`의 source manifest 경로를 `MS1-final-source-v17`로 올린다.
3. `Tools/MNG_MS_Snapshot.ps1 -OutputDirectory Logs\MNG-MS\MS1-final-source-v17`로 새 snapshot을 만들고 manifest를 검증한다.
4. 현재 `Builds/MNG_MS/MS1`을 삭제하지 말고 `Builds/MNG_MS/MS1-v15-r009`처럼 안전한 보존 경로로 옮긴다. 대상 절대 경로가 프로젝트 안인지 먼저 확인한다.
5. `MachineLearning.Soccer.Manager.Editor.MNG_MSBuilder.BuildMS1WindowsBatch`로 훈련용 Player를 다시 빌드한다. `build-info.json`의 protocol SHA가 v8, source manifest가 v17, result가 `Succeeded`, errors가 0인지 확인한다.
6. `MachineLearning.Soccer.Manager.Editor.MNG_MSBuilder.BuildMS1ReviewBatch`를 다음 인자로 실행한다.
   - run: `MNG_MS1-20260920-r007`
   - candidate: `MNG_MS1-20260920-r007-step7443`
   - model: `C:\GitHub\Machine-Learning\results\MNG_MS1-20260920-r007\MNG_Manager\MNG_Manager-7443.onnx`
7. `review-build-info.json`에서 모델 SHA, v8 protocol SHA, v17 source manifest SHA, `timeScale=1`, `opponent=R0-Easy`, seed `405001/406001`, result `Succeeded`, errors 0을 확인한다.
8. 검수 Player를 headless로 짧게 실행하고 정확한 PID만 종료해 bootstrap/예외 없는지 smoke한다. broad process kill은 하지 않는다.
9. `Logs/MNG-MS/MS1-completion-20260920/ms1-completion.json`에 선택 모델/PT, protocol, source v17, 세 build manifest, comparison, EditMode 결과의 SHA-256을 기록한다.
10. `ms1-completion-20260920.md`, `ms1-user-checklist.md`, `current-status.md`의 대기 문구를 실제 결과로 갱신하고 `git diff --check`를 실행한다.

## 변경 금지와 다음 단계

- r007의 15k `checkpoint.pt`를 MS2 초기값으로 쓰지 않는다. 선택된 `MNG_Manager-7443.pt`를 사용한다.
- r008·r009를 자동 승격하거나 새 Run으로 재학습하지 않는다.
- v8 결과를 다시 같은 seed로 튜닝하지 않는다.
- 기존 Run·PT·ONNX·평가·빌드·snapshot을 덮어쓰거나 삭제하지 않는다.
- 최종화가 끝나기 전 MS2를 시작하지 않는다.
- commit, push, reset, clean은 사용자 지시 없이 실행하지 않는다.

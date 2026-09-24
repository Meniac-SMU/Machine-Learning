> 보관일: 2026-09-24. 작성 당시의 계획·판정·승인 이력이며 현재 실행 지시가 아닙니다. 현행 기준: [현재 상태](../../../soccer/current-status.md).

# MS2 사용량 중단 체크포인트 — 2026-09-20

2026-09-23 사용자 지시: 5시간 및 주간 잔여량에 따른 사전 중단·재개 제한은 모두 폐지했다. 과거 사용량/중단 기록은 당시 사실이며 현재 실행 조건이 아니다. 서비스 자체 제한과 별개로 RAM/NaN/crash/데이터 무결성 및 정상 저장 규칙은 유지한다.

> 해소됨: 재개 후 step 19,912의 300초 최종평가가 모든 gate를 통과해 MS2를 완료했다. 최신 판정은 [MS2 완료 보고서](ms2-completion-20260920.md)를 따른다. 아래 내용은 중단 당시의 정확한 상태와 재개 절차 기록이다.

## 중단 이유와 현재 판정

사용자가 5시간 사용량이 활성 중단 기준에 도달했다고 알려 중단했다. 새 학습·평가·빌드는 더 시작하지 않았다. 현재 MS2는 **구현·빌드·16환경 학습 연결·첫 120초 진단 통과**, **300초 최종 gate 미실행** 상태다. 따라서 MS2 완료나 MS3 진입으로 표시하지 않는다.

중단 확인 시 다음 프로세스와 자원은 모두 0개였다.

- `MNG_MS2.exe` 및 `MNG_MS2_Preflight.exe`
- 이 Run의 `mlagents-learn` launcher와 trainer Python/subprocess
- 포트 `5900..5915` listener

## MS1 정책 사전평가

동결 후보는 `MNG_MS1-20260920-r007-step7443`이다. 모델은 기존 선택 ONNX를 그대로 썼으며 SHA-256은 `311f72b3d625d76156ba581ae060610b701d68bd8e69ff2a7b3afee8cc9b4e81`이다. 조건당 120초 10경기, seed offset `491001`로 Medium/Full, Red/Navy, ONNX/무작위 유효명령을 모두 실행했다.

| R0 | 정책 | Red score rate | Navy score rate | 합산 score rate | 득실 |
| --- | --- | ---: | ---: | ---: | ---: |
| Medium | MS1 ONNX | 1.00 | 1.00 | 1.000 | 44:0 |
| Medium | 무작위 | 0.90 | 0.85 | 0.875 | 27:1 |
| Full | MS1 ONNX | 0.45 | 0.50 | 0.475 | 4:7 |
| Full | 무작위 | 0.15 | 0.15 | 0.150 | 0:19 |

Full 격차는 `+0.325`이고 진영별 격차는 Red `+0.30`, Navy `+0.35`다. 평가 전에 고정한 합격선은 Full 합산 `+0.10` 이상, 어느 진영도 무작위보다 `-0.10`보다 크게 낮지 않을 것이었다. 이를 통과했으므로 계획대로 MS2를 시작했다. 원본 8개 JSON·Player 로그·빌드 로그·집계는 `Logs/MNG-MS/MS2-preflight-20260920-r001`에 있다.

## 구현·빌드 상태

- 훈련 장면: `Assets/_Soccer/Manager/Curriculum/MS_ManagerSimple/Scenes/MNG_MS2_Train.unity`
- 설정: `Assets/_Soccer/Manager/Training/MNG_MS2.yaml`
- 계약: `Assets/_Soccer/Manager/Evaluation/MNG_MS2_Protocol_v1.json`
- 평가 실행기: `Tools/MNG_MS2_Evaluate.ps1`
- 학습 실행기: `Tools/MNG_MS2_Train.ps1`
- source snapshot: `Logs/MNG-MS/MS2-source-v1/source-sha256.json`, 387개 파일
- Player: `Builds/MNG_MS/MS2/MNG_MS2.exe`
- build manifest: `Builds/MNG_MS/MS2/build-info.json`

Player build는 Unity `6000.3.16f1`, `Succeeded`, error 0이다. executable SHA-256은 `d3db57b9b88a2d8fedd1621461ddd7b29ed18001efd29a93ef9f31d4e8a483cd`, level은 `2bedb05eca2fb0956340fd3dfe82138bce405bdbdb4f1b7119df529b82fe56c4`, 런타임 DLL은 `ca1f25b2c8cc4ca9429dbf14419a91730f1cf8f43ed3b71ab76f76022116675a`다.

훈련은 새 보상이나 새 선수 기술 없이 정상 킥오프 120초, R0-Full `1.0×/0.5초`, Base 실제 사건 보상을 쓴다. 사전평가에서 Medium ONNX가 양 진영 100%였으므로 계획의 선택적 Medium 최대 20k는 생략했다. MS1의 번호가 붙은 PT `MNG_Manager-7443.pt`를 `init_path`로 읽고 새 optimizer를 만들었다.

## Run과 저장된 checkpoint

`MNG_MS2-20260920-r001`은 detached launcher의 출력 연결 문제로 trainer·Player 연결 전에 종료됐다. 결과 모델과 학습 step은 없고 빈 실패 흔적만 보존한다.

유효 Run은 `MNG_MS2-20260920-r002`다. 정확히 16개 Player, seed `192001`, port `5900..5915`, CUDA로 시작했고 worker JSONL 16개가 생성됐다. 시작 및 20k·40k·60k inspect 자료는 Run 로그 디렉터리에 있다.

| 저장 step | ONNX SHA-256 | PT SHA-256 |
| ---: | --- | --- |
| 19,912 | `7fce2603698df4f3b586f19b27f41f3c7dc143eca8c966324a2013c476c1e1e2` | `04fd43f2e3383d923b60a60483b386e78862cd78013539d087ad9a43f81b9e3f` |
| 39,976 | `fe556a7b794d68d4fb707221df291d56e67a266e03de148d82a807213a2ad4f2` | `b5e06c5490765afac4f7916f49b34e7a387f5344f3d72b093a2a38decafdaf56` |
| 59,920 | `bdd5b8306d54f0a79cd23e4f458eafd565329c1bc2c9797e0ddd0ac49965d290` | `43805b3847485b0e8fa51ed24ea781a529042b24274f0c67ed8331d6e243fdf8` |

`checkpoint.pt` SHA-256은 `9f06af83d75c2406bdecc17b91b8c806c82e63337624fa407493dd6c2bb7fa23`이다. 종료 요청 시 trainer 화면은 약 75k였지만 Ctrl+C가 중첩 PowerShell을 통과하지 않았다. 마지막 완전 export가 59,920임을 확인한 다음 이 Run의 trainer/worker PID만 종료했다. 이 때문에 trainer 로그 말미의 worker `BrokenPipe/EOFError`는 범용 환경 장애가 아니라 지정 종료 결과다. 59,920 이후는 저장·선택·예산 증거로 쓰지 않는다.

## 통과한 20k 진단

후보 `MNG_MS2-20260920-r002-step19912`를 Full R0에서 120초, 진영당 ONNX 10경기와 무작위 10경기로 평가했다.

- ONNX 합산 score rate `0.50`
- 무작위 합산 score rate `0.15`
- 격차 `+0.35`
- ONNX 득실 `7:6`
- Red `0.50`, Navy `0.50`
- 진단 판정 `passed=true`

원본은 `Logs/MNG-MS/MNG_MS2-20260920-r002-step19912-diagnostic-r001`에 있다. 이 값은 120초 저비용 진단이며 300초 최종 합격을 대신하지 않는다.

## 정확한 재개 순서

1. 사용량 비율에 따른 실행 제한은 폐지했다(2026-09-23). 오류·중단 시 증거와 재개점을 보존한다.
2. 새 학습을 재개하기 전에, 이미 진단을 통과한 step 19,912에 300초 최종 평가를 한 번 실행한다.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\MNG_MS2_Evaluate.ps1 `
  -RunId MNG_MS2-20260920-r002 `
  -CandidateId MNG_MS2-20260920-r002-step19912 `
  -ModelPath .\results\MNG_MS2-20260920-r002\MNG_Manager\MNG_Manager-19912.onnx `
  -Mode Final `
  -EvidenceId MNG_MS2-20260920-r002-step19912-final-r001
```

3. 최종 gate는 300초 ONNX 20 seed×양 진영 40경기에서 합산 score rate `>=0.50`, 총 득점 `>=` 총 실점, Red/Navy 각각 `>=0.40`, 같은 build·seed 무작위보다 `+0.10` 이상이다. 통과하면 step 19,912를 MS2 동결 후보로 선택하고 더 학습하지 않는다.
4. 최종 gate가 실패하면 합격선을 바꾸지 않는다. step 39,976와 59,920을 차례로 120초 진단하고 가장 먼저 전체 진단 조건을 통과한 후보 하나만 300초 최종 평가한다.
5. 저장 후보가 모두 최종 실패한 경우에만 같은 Run을 재개한다. 실행 명령은 아래와 같다.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\MNG_MS2_Train.ps1 `
  -RunId MNG_MS2-20260920-r002 -NumEnvs 16 -Seed 192001 `
  -BasePort 5900 -TorchDevice cuda -Resume
```

재개는 저장된 `checkpoint.pt`에서 aggregate 100k 상한까지만 진행한다. 보상합이 흔들렸으므로 reward 숫자만 보고 연장하지 않는다. 고정 경기 결과, 실제 득실·명령·패스·슛 telemetry가 함께 개선되고 보상 악용이 없을 때만 프로젝트 공통 추가 학습 규칙을 검토한다. MS2 최종 통과 전 MS3를 시작하지 않는다.

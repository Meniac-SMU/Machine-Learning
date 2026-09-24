> 보관일: 2026-09-24. 작성 당시의 계획·판정·승인 이력이며 현재 실행 지시가 아닙니다. 현행 기준: [현재 상태](../../../soccer/current-status.md).

# MS1 구현 체크포인트 — 2026-09-20 사용량 중단

2026-09-23 사용자 지시: 5시간 및 주간 잔여량에 따른 사전 중단·재개 제한은 모두 폐지했다. 과거 사용량/중단 기록은 당시 사실이며 현재 실행 조건이 아니다. 서비스 자체 제한과 별개로 RAM/NaN/crash/데이터 무결성 및 정상 저장 규칙은 유지한다.

## 중단 이유와 재개 금지선

- 마지막 확인값은 5시간 사용량 `92%`(잔여 `8%`), 주간 사용량 `45%`(잔여 `55%`)다.
- 사용량 비율에 따른 실행 제한은 폐지했다(2026-09-23). 오류·중단 시 증거와 재개점을 보존한다.
- 5시간 기준에 도달했으므로 16환경 Trainer Run을 시작하지 않았다. reset credit은 사용하지 않았다.
- 재개할 때 가장 먼저 사용량을 다시 읽는다. 어느 한 기준이라도 계속 충족하면 아래 명령을 실행하지 않는다.

## 이번 세션에서 완료한 구현

1. `MNG_MS1Controller`
   - Red의 단일 `MNG_ManagerAgent`와 Navy의 단일 `MNG_RuleBasedManager`를 강제한다.
   - R0-Easy `movementSpeedMultiplier=0.35`, `decisionIntervalSeconds=1.5`를 기본 상대 계약으로 사용한다.
   - episode는 30초이며 `InterruptedCollection` 종료를 사용한다.
   - 공격과 수비/전환을 정확히 50:50으로 교대한다.
   - 수비/전환 100회 안에서 Neutral 50회, Direct 25회, Wide 25회를 배분한다.
   - 공격 시작은 Red 소유, 수비 시작은 Navy 또는 Neutral 소유로 배치한다.
   - Red가 공을 0.5초 연속 소유하면 회복 성공 telemetry로 기록한다.
   - `-mngEvidenceDir`을 받아 worker별 JSONL을 남긴다.
2. 생성 자산과 Builder
   - 학습 Scene: `Assets/_Soccer/Manager/Curriculum/MS_ManagerSimple/Scenes/MNG_MS1_Train.unity`
   - Red PPO만 활성화하고 Navy R0만 활성화한다. Human/Fallback과 반대편 정책 감독은 비활성화한다.
   - `BuildMS1Assets`, `ValidateMS1Assets`, `BuildMS1WindowsBatch`를 `MNG_MSBuilder`에 추가했다.
3. 학습·평가 계약
   - YAML: `Assets/_Soccer/Manager/Training/MNG_MS1.yaml`
   - protocol: `Assets/_Soccer/Manager/Evaluation/MNG_MS1_Protocol_v1.json`
   - PPO 기본 목표는 aggregate `100000` step, 조건부 절대 상한은 `120000` step이며 checkpoint 간격은 `20000`이다.
   - 100k 이후 연장은 최근 두 진단의 보상·고정 평가·실제 사건이 모두 상승하고 보상 악용이 없을 때만 허용한다.
   - 최종 판정 계약은 공격 `24/40`, 수비 `20/40`, 각 random 대비 `+10%p`, 정확한 Pass 유형 완료 패스 `3/20`이다.
4. 실행기
   - `Tools/MNG_MS1_Build.ps1`
   - `Tools/MNG_MS1_Train.ps1`
   - Trainer 실행기는 `NumEnvs=16`만 허용하고 port `5800..5815`, seed `191001`을 기본값으로 사용한다. 8개나 2개로 자동 축소하지 않는다.

## 완료된 검증과 증거

| 검증 | 결과 | 증거 |
| --- | --- | --- |
| Unity compile + MS1 Scene 생성 | 통과 | `Logs/MNG-MS/MS1/assets-batch-2.log` |
| Builder MS1 Validate | `MNG MS1 VALIDATION PASS` | `Logs/MNG-MS/MS1/assets-batch-2.log` |
| MS1 EditMode 계약 | `3/3` 통과 | `Logs/MNG-MS/MS1/editmode.xml` |
| 최종 source snapshot | 330개 파일 | `Logs/MNG-MS/MS1-final-source/source-sha256.json` |
| Windows Player | 성공, errors `0` | `Logs/MNG-MS/MS1/player-build.log` |
| Player manifest | 성공 | `Builds/MNG_MS/MS1/build-info.json` |

빌드 해시는 다음과 같다.

| 항목 | SHA-256 |
| --- | --- |
| YAML | `6b1a4e13dc31790d031c929ba3d07c99eed9daa1de1a97bd5a47c4daf3283ced` |
| protocol | `09536c0344d92a72cfb6f01c638284389fb4b6905ba4b339745cc48e9242ae07` |
| source manifest | `161950966253500c59b1132faa45856a8bf56ce275169987b94efeff15afe11f` |
| executable | `d3db57b9b88a2d8fedd1621461ddd7b29ed18001efd29a93ef9f31d4e8a483cd` |
| level data | `52027d2dab46bf56912407802b7f8ad3a1f465e70977d886de4865df8c797071` |

Player 빌드에는 경고 486개가 기록되었지만 빌드 오류는 0이며 결과는 `Succeeded`다. 이 수치는 강한 정책이나 학습 성공의 증거가 아니다.

## 아직 실행하지 않은 범위

- 16환경 실행기의 `-ValidateOnly`
- 실제 16개 Player 연결과 서로 다른 worker/port 확인
- fresh PPO Run과 첫 정상 update
- 20k 단위 checkpoint 공격·수비 진단
- Easy가 경험을 거의 주지 못할 때만 허용되는 최초 최대 20k Rescue 전환 판단
- PT/ONNX export 및 optimizer 상태 증거
- 최종 120 episode 고정 평가와 MS1 승격 판정
- MS1 Scene 대상 PlayMode wiring 검사

`results/MNG_MS1-*`와 `Logs/MNG-MS/MNG_MS1-*` 공식 Run은 이번 중단 시점에 만들지 않았다. 기존 Run을 덮어쓰거나 `--force`를 사용하지 않는다.

## 정확한 재개 순서

1. 사용량 비율에 따른 실행 제한은 폐지했다(2026-09-23). 오류·중단 시 증거와 재개점을 보존한다.
2. Player manifest와 16환경 실행기만 검증한다.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Tools\MNG_MS1_Train.ps1 `
  -RunId MNG_MS1-20260920-r001 -NumEnvs 16 -Seed 191001 -TorchDevice cuda -ValidateOnly
```

3. PlayMode wiring 검사를 추가·실행해 Scene의 단일 Red PPO, 단일 Navy R0, 30초 종료를 확인한다.
4. 포트 `5800..5815`와 GPU/메모리 여유를 확인한 뒤 같은 Run ID에서 `-ValidateOnly`만 제거해 **16환경** fresh PPO를 시작한다. 이때 `Tools/inspect_soccer_training.py`의 startup JSON과 trainer log를 즉시 저장한다.
5. 첫 update, worker 16개 JSONL, checkpoint `.pt`, ONNX가 확인되지 않으면 학습 성공으로 보고하지 않는다.
6. 20k마다 공격·수비 진단을 수행하고 조기 통과 또는 정체 중단을 적용한다. 기본 목표는 aggregate 100k다. 공통 연장 조건을 모두 만족할 때만 120k까지 진행한다.
7. 통과 후보에만 protocol의 120 episode 최종 평가를 실행하고, 네 gate가 모두 통과할 때만 MS1 완료로 판정한다.

재개 시 새 날짜를 사용한다면 Run ID 날짜도 실제 시작일로 바꾸고, 기존 디렉터리가 있으면 `r002`처럼 새 revision을 쓴다.

## 종료 상태

- Unity Editor, ML-Agents Trainer, `MNG_MS1.exe` Player는 실행 중이지 않다.
- Unity CLI helper 하나만 남아 있으며 학습이나 Player 환경이 아니다.
- commit, push, reset, force, 자동 재개, heartbeat를 만들지 않았다.

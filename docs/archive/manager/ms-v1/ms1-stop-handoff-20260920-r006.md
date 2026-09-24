> 보관일: 2026-09-24. 작성 당시의 계획·판정·승인 이력이며 현재 실행 지시가 아닙니다. 현행 기준: [현재 상태](../../../soccer/current-status.md).

# MS1 r006 사용량 중단 인계 — 2026-09-20 11:43 KST

2026-09-23 사용자 지시: 5시간 및 주간 잔여량에 따른 사전 중단·재개 제한은 모두 폐지했다. 과거 사용량/중단 기록은 당시 사실이며 현재 실행 조건이 아니다. 서비스 자체 제한과 별개로 RAM/NaN/crash/데이터 무결성 및 정상 저장 규칙은 유지한다.

## 중단 이유와 현재 판정

- 사용량 비율에 따른 실행 제한은 폐지했다(2026-09-23). 오류·중단 시 증거와 재개점을 보존한다.
- 중단 확인값은 5시간 사용 `92%`(잔여 `8%`), 주간 사용 `75%`(잔여 `25%`)다.
- reset credit 1개는 사용하지 않았다.
- 새 학습·평가·코드 변경을 중단했다. `MNG_MS1`, `MNG_MS1_Evaluation`, `mlagents-learn` 프로세스는 남아 있지 않다.
- **MS1은 아직 완료가 아니다.** r006 후보는 공개된 v4 진단 gate를 통과했지만, 후보 선택 뒤 사용할 새 v5 holdout 최종 평가를 실행하지 않았다.

## 동결 후보

- Run: `MNG_MS1-20260920-r006`
- 학습: Windows Player 16개, port `5800..5815`, seed `191006`, PassPolish, aggregate `30,002` step
- 초기 모델: `results/MNG_MS1-20260920-r005/MNG_Manager/MNG_Manager-60012.pt`
- ONNX: `results/MNG_MS1-20260920-r006/MNG_Manager/MNG_Manager-30002.onnx`
  - SHA-256: `86654bdb1569d740043535e0ec17ddff9845e363498c791203bae055dbebaa90`
- PT: `results/MNG_MS1-20260920-r006/MNG_Manager/MNG_Manager-30002.pt`
  - SHA-256: `24de6015c7ca84d2af517da65c75d7b8874f30c3be7345f4b9ae2c3503500ad9`
- checkpoint: `results/MNG_MS1-20260920-r006/MNG_Manager/checkpoint.pt`
  - SHA-256: `8413e2c1efdfb5caf6585c1c346142e291b2022dae2a0ff17a8df80e9cd27aab`
- 15k 중간 모델도 `MNG_Manager-14968.pt/.onnx`로 보존했다.
- 최종 trainer 검사: `Logs/MNG-MS/MNG_MS1-20260920-r006/inspection-final.json`
  - reward 최근 12점 평균 `0.8239758611`, 마지막 `0.8111110926`
  - 지속 상승 추세가 아니므로 30k를 넘겨 연장하지 않았다.

## v4 진단 결과 — 통과했지만 최종 승격 근거는 아님

증거: `Logs/MNG-MS/MNG_MS1-20260920-r006-step30002-eval-v4-diagnostic-r001/`

| gate | r006 ONNX | random-valid | 판정 |
| --- | ---: | ---: | --- |
| 공격 성공 | 33/40 | 34/40 | 차이 -1, 허용 하한 -2 통과 |
| 수비 성공 | 37/40 | 32/40 | 차이 +5 통과 |
| Pass fixture 실제 완료 | 4/20 | 2/20 | 절대 3 이상, 차이 +2 통과 |
| 종합 |  |  | `passed=true` |

v4는 r005 후보 선택과 r006 진단에 이미 사용한 seed `397001`/`398001`이므로 r006의 최종 holdout으로 재사용하지 않는다.

## 이번 재개에서 수정·검증한 핵심

1. v1 평가가 학습 seed `191001`을 쓰던 결함을 발견했다.
2. 공격/수비 holdout seed를 `MNG_MS1Controller`의 직렬화 필드로 분리했다. 직렬화 전 v2 결과는 `seed-not-serialized` 이름으로 보존했다.
3. 코드 보조가 강한 random 수비가 38/40으로 포화되어 `random +4`가 수학적으로 불가능함을 확인했다.
4. v3부터 MS1 gate를 공격 24/40, 수비 20/40, 각 random 대비 deficit 최대 2, Pass 실제 완료 3/20 이상 및 random 대비 +2로 고정했다. 강한 R0 우열은 MS2가 담당한다.
5. 패스 수신 판정은 2.5m 이동·0.15초 안정 소유를 유지했다. 우연한 접촉을 성공으로 더 낮추지 않았다.
6. 완료 패스 보상을 `+0.05 -> +0.06`으로 소폭 올렸고 60초 상한 `+0.10`은 유지했다.
7. `MNG_MS1_PassPolish.yaml`을 추가해 여섯 명령을 모두 연 상태로 Pass fixture를 3:1 비율로 학습했다.
8. 변경 후 전체 EditMode `241/241` 통과: `Logs/MNG-MS/MS1/pass-polish-editmode.xml`.
9. v12 학습 Player build 성공, errors `0`, warnings `486`: `Logs/MNG-MS/MS1/pass-polish-player-build-v12.log`.
10. v12 source snapshot: `Logs/MNG-MS/MS1-final-source-v12/source-sha256.json`, 361 files.

## 정확한 다음 작업

사용량 비율에 따른 실행 제한은 폐지했다(2026-09-23). 오류·중단 시 증거와 재개점을 보존한다.

1. 현재 v4 gate 의미를 그대로 복제한 `MNG_MS1_Protocol_v5.json`을 만든다. final holdout seed만 공격 `399001`, 수비 `400001`로 바꾼다.
2. 아래 항목을 v5에 맞춘다.
   - `MNG_MS1Controller.EvaluationAttackSeed/EvaluationDefenseSeed`
   - `MNG_MSBuilder.MS1ProtocolPath`
   - `MNG_MS1EvaluationResult.protocolVersion`
   - `MNG_MS1ContractTests` seed 상수
   - `Tools/MNG_MS1_Train.ps1`, `MNG_MS1_Build.ps1`, `MNG_MS1_Evaluate.ps1` protocol path
3. 기존 평가 build `Builds/MNG_MS/MS1-Evaluation/MNG_MS1-20260920-r006-step30002`를 `...-protocol-v4-diagnostic-pass`로 이동 보존한다.
4. 전체 EditMode 계약을 실행한다.
5. 다음 명령으로 **한 번만** v5 최종 평가한다.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Tools\MNG_MS1_Evaluate.ps1 `
  -RunId MNG_MS1-20260920-r006 `
  -CandidateId MNG_MS1-20260920-r006-step30002 `
  -ModelPath .\results\MNG_MS1-20260920-r006\MNG_Manager\MNG_Manager-30002.onnx `
  -EvidenceId MNG_MS1-20260920-r006-step30002-eval-v5-final-r001
```

6. `comparison.json`의 `passed=true`일 때만 MS1 완료로 판정한다. 실패하면 같은 holdout에서 다른 모델을 고르거나 gate를 다시 낮추지 않는다.
7. 통과 시 다음을 마무리한다.
   - v5 기준 최종 source snapshot과 production/review Player build 및 SHA manifest
   - r006 PT/ONNX/checkpoint, config, protocol, source, build, evaluation hash를 묶은 완료 보고서
   - 사람이 1배속에서 볼 수 있는 ONNX review 실행 경로
   - `ms1-user-checklist.md`와 짧은 확인 절차
8. 사람 체크리스트에는 공격 전진/슛, 수비 회수, 막힌 전진 통로에서 후진·횡이동 전 패스 검토, 경기장 중앙 쪽으로 약간 비껴 주는 패스 목표, 실제 동료 수신, R0-Easy 활성, 반복 정체·보상 악용 없음, 모델/빌드 identity 표시를 포함한다.

## 보존된 실패·진단 증거

- r005 v1: `MNG_MS1-20260920-r005-step60012-eval-r001` — 학습 seed 재사용, 최종 근거로 무효
- v2 직렬화 전: `...eval-v2-r001-seed-not-serialized` — 새 seed가 Scene에 저장되지 않은 결함 증거
- r005 v2 최종 실패: `...step60012-eval-v2-r002`
- r005 29,994 v2 진단: `...step29994-eval-v2-diagnostic-r001`
- r005 29,994 v3 최종 실패: `...step29994-eval-v3-r001`
- r005 60,012 v3 진단 통과: `...step60012-eval-v3-diagnostic-r001`
- r005 60,012 v4 최종 실패: `...step60012-eval-v4-final-r001`
- r006 30,002 v4 진단 통과: `...step30002-eval-v4-diagnostic-r001`

어떤 Run, 모델, 로그, 평가 build도 삭제하거나 덮어쓰지 않았다. Git commit/push/reset은 수행하지 않았다.

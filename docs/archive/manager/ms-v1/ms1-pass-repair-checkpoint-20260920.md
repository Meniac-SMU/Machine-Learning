> 보관일: 2026-09-24. 작성 당시의 계획·판정·승인 이력이며 현재 실행 지시가 아닙니다. 현행 기준: [현재 상태](../../../soccer/current-status.md).

# MS1 패스 공통 개선·r002 시작 전 체크포인트 — 2026-09-20

2026-09-23 사용자 지시: 5시간 및 주간 잔여량에 따른 사전 중단·재개 제한은 모두 폐지했다. 과거 사용량/중단 기록은 당시 사실이며 현재 실행 조건이 아니다. 서비스 자체 제한과 별개로 RAM/NaN/crash/데이터 무결성 및 정상 저장 규칙은 유지한다.

## 중단 사유와 현재 실행 상태

- 마지막 Codex 사용량 확인값은 5시간 사용량 `95%`(잔여 `5%`), 주간 사용량 `15%`(잔여 `85%`)다.
- 사용량 비율에 따른 실행 제한은 폐지했다(2026-09-23). 오류·중단 시 증거와 재개점을 보존한다.
- reset credit 1개는 사용하지 않았다. 2026-09-23부터 사용량 비율은 재개 조건이 아니다.
- 이 체크포인트 작성 시 새 r002 결과·worker 로그 디렉터리는 없다. 기존 r001과 평가 증거는 보존했다.

## 완료된 첫 공식 학습 r001

- Run: `MNG_MS1-20260920-r001`
- 16개 독립 Windows Player, port `5800..5815`, seed `191001`, CUDA, R0-Easy 상대다.
- fresh PPO로 시작해 aggregate `99,980` step checkpoint까지 저장했다.
- 최근 10개 episode 평균 reward는 20k `0.7506`, 40k `0.7742`, 60k `0.8224`, 80k `0.8301`, 100k `0.8785`로 상승했다.
- 100k 누적 실제 사건은 episode 종료 3,163, 공격 득점 1,499/1,588, 수비 회수 1,414/1,575, 수비 실점 103이다.
- 명령 수는 Advance `8,388`, Pass `1,304`, Shot `2,698`, Recover `31,427`, Balanced `46,126`, Protect `11,054`다. 여섯 명령은 모두 사용했지만 Pass 비중은 낮았다.

동결 후보 해시는 다음과 같다.

| 항목 | SHA-256 |
| --- | --- |
| r001 99,980 PT | `2f16c829484154f237661c3178b9f50578f63db065430dff4b7e10504e345a83` |
| r001 99,980 ONNX | `4cb2e7c0d898addbf1d0406b570e0338d9b7019bd0c9ccb0b9d5f77abf730c37` |
| r001 checkpoint.pt | `21d8b8da7e3243c812abec1ee0cfdcaa3a5e6dc42ea9d3b9ee0130371c1e8098` |

## r001 고정 평가와 진단 결론

기존 fixture 평가 `Logs/MNG-MS/MNG_MS1-20260920-r001-step99980-eval-r001`은 ONNX 공격 `39/40`, 수비 `37/40`, random 공격 `36/40`, 수비 `31/40`, Pass 하위집합 완료 `0`이었다. 공격 random 대비 향상이 3경기뿐이고 패스가 없어 gate에 실패했다.

Pass fixture의 직접 운반 통로를 막고 대각선 outlet을 연 뒤 실행한 진단 `Logs/MNG-MS/MNG_MS1-20260920-r001-step99980-passfixture-v2-eval-r001`은 다음과 같다.

| 정책 | 공격 성공 | 수비 성공 | 전체 패스 타격 | 전체 완료 패스 | Pass 유형 패스 명령/타격/완료 |
| --- | ---: | ---: | ---: | ---: | ---: |
| r001 ONNX | 39/40 | 35/40 | 4 | 1 | 0 / 0 / 0 |
| uniform valid command | 35/40 | 36/40 | 8 | 2 | 17 / 2 / 0 |

수정 fixture에서도 r001 정책은 Pass 유형에서 Pass 명령을 선택하지 않았다. random은 Pass 명령과 실제 타격을 만들었지만 수신 완료는 없었다. 따라서 단순 학습 연장이 아니라 공통 패스 실행점과 판단 규칙을 먼저 수정했다.

## 사용자 지시에 따라 완료한 공통 패스 변경

1. `MNG_TacticalTargetResolver.IsForwardDribbleBlocked`
   - 운반자 전방 `8m`, 좌우 반폭 `3m`, 최소 앞 깊이 `0.5m` 안에 상대가 있으면 전진 통로 차단으로 판정한다.
2. 규칙형 R0와 fallback
   - 전진 통로가 막히고 안전한 패스 대상이 있으면 후진·횡운반 전에 `PassBuild`를 우선한다.
   - R0 이유 telemetry는 `blocked-forward-pass`다. `RuleVersion`은 `1`이다.
3. 공통 Planner 패스 목표
   - 수신 선수 속도를 `0.35초` 예측하고 상대 골문 방향 `3.5m`를 lead한다.
   - 좌우 중 경기장 중앙에 가까운 쪽으로 `1.5m` 치우친 목표를 사용한다.
   - PPO와 R0가 같은 `MNG_TeamPlanner.SelectPassTarget`을 사용하므로 양쪽에 동일하게 적용된다.
4. 강화학습 신호
   - 실제 완료 패스 보상을 `+0.04 -> +0.05`로 높였다.
   - CompletedPass 60초 상한 `+0.10`, 실제 3m 이동, 다른 동료의 0.20초 안정 소유, event ID 중복 방지는 유지한다.
   - 명령 선택 자체에는 보상하지 않는다.
5. MS1 패스 보조 gate
   - 완료 기준을 `4/20 -> 3/20`으로 낮췄다.
   - 기존 구현이 첫 공격 20개를 잘못 패스 subset으로 세던 문제를 수정했다.
   - 정책당 120 episode를 실행하고 첫 공격 40·수비 40을 주 gate에 쓰며, 전체 공격 60개에서 정확한 Pass 유형 20개만 패스 gate에 쓴다.

## 검증과 빌드 증거

| 항목 | 결과 | 증거 |
| --- | --- | --- |
| Unity compile + MS1 Builder Validate | 통과 | `Logs/MNG-MS/MS1/pass-repair-assets-v3.log` |
| 공통 MNG EditMode 계약 | `63/63` 통과 | `Logs/MNG-MS/MS1/pass-policy-editmode-v3.xml` |
| MS1 분포·보상·Pass subset 계약 | `6/6` 통과 | `Logs/MNG-MS/MS1/pass-policy-ms1-editmode-v3.xml` |
| 최종 source snapshot | 344개 파일 | `Logs/MNG-MS/MS1-final-source-v4/source-sha256.json` |
| Windows Player | 성공, errors `0`, warnings `486` | `Logs/MNG-MS/MS1/pass-repair-player-build-v4.log` |
| 16환경 r002 실행기 ValidateOnly | 통과 | 콘솔 검증, 아래 재개 명령과 동일 인자 |

최종 실행 입력 해시는 다음과 같다.

| 항목 | SHA-256 |
| --- | --- |
| source manifest v4 | `d2d42c175d4c23e8567e648c8df2f4205532f7d88104fc4fa3799fe1b4bf05d2` |
| PassRepair YAML | `5fd7cb18ef0ef453e76e481e86609f7e12b34f08cfa9b02e6b56567606c77483` |
| protocol v1 | `9fbfb9381ce7b7d1cca4430090d704d3923a7a7951e7f7bcbd15eb892c31f43a` |
| Player executable | `d3db57b9b88a2d8fedd1621461ddd7b29ed18001efd29a93ef9f31d4e8a483cd` |
| Player level0 | `e79b160c9418dd09d72fc5f81d25a185a373d224b05a6cfecc5cba6e7d9020ea` |

변경 전 Player는 `Builds/MNG_MS/MS1-pre-passrepair-20260920`에 복사 보존했다. v3 소스 snapshot도 과도기 증거로 보존하되 공식 r002 입력은 v4다.

## 준비된 r002 보정 학습

- Run ID: `MNG_MS1-20260920-r002`
- profile: `PassRepair`
- initialization: r001 `MNG_Manager-99980.pt` 가중치, 새 optimizer
- 16환경 고정, seed `191002`, port `5800..5815`, CUDA
- learning rate `0.0001`, max step `40,000`, checkpoint `20,000`
- 목적은 새 공통 패스 목표와 막힌 전진 통로 판단을 학습하면서 r001 공격·수비 능력을 보존하는 것이다.

정확한 시작 명령은 다음과 같다.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Tools\MNG_MS1_Train.ps1 `
  -RunId MNG_MS1-20260920-r002 -NumEnvs 16 -Seed 191002 `
  -BasePort 5800 -TorchDevice cuda -TrainingProfile PassRepair
```

재개 절차:

1. 사용량 비율에 따른 실행 제한은 폐지했다(2026-09-23). 오류·중단 시 증거와 재개점을 보존한다.
2. 위 명령에 `-ValidateOnly`를 붙여 build/config/protocol/source/exe/level 해시와 port 범위를 다시 확인한다.
3. r002 디렉터리가 없는 것을 확인하고 `-ValidateOnly`를 제거해 한 번만 시작한다. `--force`와 `-Resume`을 사용하지 않는다.
4. 시작 즉시 16 Player, worker-start 16개, 서로 다른 process/worker/port, 첫 PPO update와 유한 loss를 확인하고 `Tools/inspect_soccer_training.py` startup 증거를 저장한다.
5. 20k checkpoint에서 reward, 공격/수비 실제 사건, Pass 유형의 Pass 명령·타격·완료를 진단한다. 패스 완료만 좋아지고 공격·수비가 크게 악화되면 계속하지 않는다.
6. 40k에서 후보를 동결하고 새 120-episode ONNX/random 평가를 실행한다. 공격 `24/40`, 수비 `20/40`, 양쪽 random 대비 `+4경기`, 정확한 Pass 유형 완료 `3/20`을 모두 요구한다.
7. 40k 이후 추가 연장은 이 보정 Run에 허용하지 않는다. 전체 공통 연장 규칙은 다른 RL Run에 계속 적용하지만, r002는 환경·보상 변경을 확인하는 한정 보정이므로 새 평가 없이 범위를 넓히지 않는다.

## 남은 MS1 완료 작업

- r002 16환경 실제 학습과 20k/40k 진단
- 선정 checkpoint의 PT/ONNX/optimizer/update 증거와 SHA 보존
- 새 protocol의 정책당 120 episode ONNX/random 고정 평가
- 패스 악용, 시간 끌기, Balanced/Recover 고정, 공격·수비 회귀 점검
- gate 통과 시 MS1 완료 보고서와 사용자 확인 체크리스트 작성

commit, push, reset, force, 자동 재개, heartbeat는 수행하지 않았다.

> 보관일: 2026-09-24. 작성 당시의 계획·판정·승인 이력이며 현재 실행 지시가 아닙니다. 현행 기준: [현재 상태](../../../soccer/current-status.md).

# MS3 PPO self-play 완료 보고서 — 2026-09-21

## 완료 판정

MS3를 완료했다. 공식 Run은 `MNG_MS3-20260921-r002`이며, MS2 P0의 정책 가중치를 초기값으로 사용하되 optimizer는 새로 시작했다. Red와 Navy 감독은 같은 PPO behavior를 사용해 32개 독립 Windows Player 환경에서 self-play했고 aggregate `300,028` step에서 자연 종료했다. P1 Planner 변경은 포함하지 않았다.

300k 최소 완료 gate를 모두 통과했으므로 이 checkpoint를 동결했다. 최대 1M은 실패 시 무조건 채우는 목표가 아니라 진전이 계속되는 경우에만 쓰는 상한이다. 이미 score, 양 진영, 득실, R0 회귀, 전술 사건, self-play 교체, 행동 무결성 조건을 통과했으므로 400k 연장은 수행하지 않았다.

## 출발점과 학습 계약

| 항목 | 값 |
|---|---|
| 초기 정책 | `MNG_MS2-20260921-r005-step99968` |
| 초기 PT SHA-256 | `9ab8536762c0838960ec807fc943cc6e1cb4d166c4324db2d0406bb88ee3c0ca` |
| optimizer | 새로 시작 |
| 환경 | Windows Player 32개 |
| 상대 | Red PPO 대 Navy PPO self-play |
| 경기 길이 | 300초 |
| 정책 보조 | `MNG_PolicyAssistMode.None` |
| 학습 seed | `193001` |
| 필수 구간 | aggregate 300k |
| 절대 상한 | aggregate 1M |

감독 PPO는 여섯 팀 명령의 선택과 전환 시점을 결정한다. 선수의 이동, 공 접근, 패스와 슛의 물리 실행, 골키퍼 동작은 공통 코드가 처리한다. MS3의 핵심 무결성 조건은 PPO가 고른 명령을 사후 코드가 다른 명령으로 바꾸지 않는 것이다.

## 공식 학습 증거

공식 Run은 `Starting training from step 0`으로 시작했고 다음 번호가 붙은 checkpoint를 남겼다.

| 구간 | 실제 checkpoint step |
|---:|---:|
| 50k | 49,980 |
| 100k | 99,976 |
| 150k | 149,908 |
| 200k | 199,940 |
| 250k | 249,936 |
| 300k | 300,028 |

`training_status.json`에는 final checkpoint `300,028`이 기록되어 있다. 32 worker가 모두 연결됐고 완료 episode는 484개다. snapshot 상대 교체는 11회, learning team change는 2회였다. 양 팀 모두 여섯 명령을 학습 중 실제로 사용했다.

행동 무결성 집계 결과는 다음과 같다.

| 항목 | 결과 |
|---|---:|
| raw/effective action 불일치 | 0 |
| 강제 명령 override | 0 |
| 직접 blocked-pass 판단 보상 | 0 |
| assist mode 위반 | 0 |
| worker/episode 무결성 위반 | 0 |

첫 시도 `MNG_MS3-20260921-r001`은 49,941 pilot checkpoint까지는 유효했지만, 강제 종료 뒤 `training_status.json`이 없어 resume이 MS3 checkpoint가 아닌 P0 초기 정책을 다시 불러왔다. 이 Run은 `pilot-preserved-not-official-MS3-candidate`로 보존했고 공식 후보에서 제외했다. 기존 증거를 삭제하거나 덮어쓰지 않았다.

## R0-Full 고정 평가

100k와 200k는 양 진영 ONNX/무작위 각 10경기, 경기당 120초로 진단했다. 300k는 양 진영 ONNX/무작위 각 20경기, 경기당 300초인 총 80경기로 최종 평가했다.

| checkpoint | 합산 score rate | Red | Navy | 득점:실점 | 유효 슛/분 | 5m 전진/분 |
|---:|---:|---:|---:|---:|---:|---:|
| 100k | 0.725 | 0.800 | 0.650 | 23:7 | 2.100 | 5.700 |
| 200k | 0.625 | 0.550 | 0.700 | 20:10 | 1.875 | 5.375 |
| 300k | 0.875 | 0.825 | 0.925 | 137:29 | 1.825 | 4.965 |

최종 300k와 같은 seed의 MS2 P0 기준은 합산 `0.75`, Red `0.70`, Navy `0.80`, 득실 `98:35`다. 300k 정책은 P0보다 합산 `+0.125` 높고, 같은 최종 평가의 무작위 유효명령 정책 `0.1625`보다 `+0.7125` 높다.

유효 슛은 P0의 분당 `1.72`에서 `1.825`로 약 `6.1%` 증가해 전술 사건 5% 개선 조건을 통과했다. 5m 전진은 `4.975`에서 `4.965`로 유지 수준이다. 최종 평가에서 유효 `PassBuild` 명령은 0회였다. 완료 패스 telemetry가 소량 기록됐더라도 이를 PPO가 패스 명령을 배운 증거로 해석하지 않는다. 이번 완료 판정은 강한 경기 결과, 양 진영 재현, 유효 슛 증가와 행동 무결성에 근거한다.

## 완료 gate

| 조건 | 요구 | 결과 | 판정 |
|---|---:|---:|---|
| R0-Full 합산 score rate | 0.65 이상 | 0.875 | 통과 |
| Red score rate | 0.55 이상 | 0.825 | 통과 |
| Navy score rate | 0.55 이상 | 0.925 | 통과 |
| 득점 | 실점 이상 | 137 대 29 | 통과 |
| P0 회귀 | 하락 0.10 이하 | 오히려 +0.125 | 통과 |
| 전술 사건 | 하나 이상 5% 개선 | 유효 슛 +6.1% | 통과 |
| self-play 상대 교체 | 존재 | 11회 | 통과 |
| 학습 진영 교대 | 존재 | 2회 | 통과 |
| 행동 무결성 | 위반 0 | 위반 0 | 통과 |

## 완료 후 checkpoint 직접 대전

사용자 요청에 따라 초기 P0, 100k, 200k 정책을 각각 300k와 직접 대전시켰다. 대진마다 이전 정책이 Red인 20경기와 Navy인 20경기를 수행해 총 40경기로 진영 효과를 상쇄했다. 경기당 300초, 동일 seed, policy assist 없음 조건이다.

| 대진 | 이전 정책 score rate | 300k score rate | 이전 정책 관점 승·무·패 | 이전 정책 관점 득실 |
|---|---:|---:|---:|---:|
| 초기 대 300k | 0.4500 | **0.5500** | 14승 8무 18패 | 88:115 |
| 100k 대 300k | **0.6000** | 0.4000 | 22승 4무 14패 | 98:93 |
| 200k 대 300k | **0.6125** | 0.3875 | 22승 5무 13패 | 104:86 |

이 추가 결과는 300k가 R0-Full에는 가장 강하지만 self-play checkpoint 전체를 직접 지배하지는 않는다는 뜻이다. MS3 파이프라인과 사전 고정한 R0-Full 완료 gate는 유지한다. 다만 300k를 모든 상대에 가장 강한 리그 챔피언이라고 표현하지 않는다. 100k와 200k 사이의 직접 대전이 없으므로 이번 결과만으로 선택 모델을 교체하지 않는다. 세부 진영·전술 사건과 증거는 [MS3 checkpoint 직접 대전 결과](ms3-checkpoint-duel-results-20260921.md)를 따른다.

## 선택 모델과 빌드

| 산출물 | SHA-256 |
|---|---|
| `MNG_Manager-300028.onnx` | `1d0916383887939430401de9058c68ab1d3b841b654569d2aaf6c3db81df2899` |
| `MNG_Manager-300028.pt` | `ee905613f218c3b863b7111b5076adf845ec43fc6c2721e9c7466bb59b68d8ce` |
| 최종 검수 실행 파일 | `d3db57b9b88a2d8fedd1621461ddd7b29ed18001efd29a93ef9f31d4e8a483cd` |

사람 확인 빌드는 `Builds/MNG_MS/MS3-Final-Review/MNG_MS3_Final_Review.exe`다. Red와 Navy 모두 같은 동결 ONNX를 사용하며, 규칙형·fallback·Human 감독은 비활성화되어 있다. 경기는 300초, 1배속, 자동 종료 없음이다.

25초 headless smoke에서 `policies=2`, `rules=0`, 양 팀 각각 60판단, `policyAssistMode=None`, override 0, 직접 blocked-pass 보상 0, 런타임 예외 0을 확인했다. 전체 경기 성능 판정은 smoke가 아니라 484개 학습 episode와 R0-Full 80경기 최종 평가를 근거로 한다.

## 검증과 증거 위치

- 전체 Unity EditMode: `244/244` 통과, 실패·skip 0
- 학습 상태: `results/MNG_MS3-20260921-r002/run_logs/training_status.json`
- 50k 점검과 행동 무결성: `Logs/MNG-MS/MNG_MS3-20260921-r002`
- 100k/200k/300k 평가: `Logs/MNG-MS/MNG_MS3-20260921-r002-step*`
- 최종 평가: `Logs/MNG-MS/MNG_MS3-20260921-r002-step300000-ms3-r0-final-seed491001`
- 검수 빌드 매니페스트: `Builds/MNG_MS/MS3-Final-Review/review-build-info.json`
- 최신 source snapshot: `Logs/MNG-MS/MS3-final-source-20260921-v4/source-sha256.json`
- 기계 판독 완료 매니페스트: `Logs/MNG-MS/MS3-completion-20260921/ms3-completion.json`
- 사람 확인 절차: [MS3 사용자 확인 체크리스트](ms3-user-checklist.md)

MS2 P0의 상태는 사용자의 결정대로 최종 완료가 아닌 임시 합격으로 남는다. MS3는 그 임시 합격 정책을 출발점으로 한 별도의 완료 단계다. P1 변경은 이후 별도 승인 작업이며 이번 모델과 평가에는 포함되지 않았다.

> 보관일: 2026-09-24. 작성 당시의 계획·판정·승인 이력이며 현재 실행 지시가 아닙니다. 현행 기준: [현재 상태](../../../soccer/current-status.md).

# MS2 P0 완료 및 사용량 중단 인계 — 2026-09-21

2026-09-23 사용자 지시: 5시간 및 주간 잔여량에 따른 사전 중단·재개 제한은 모두 폐지했다. 과거 사용량/중단 기록은 당시 사실이며 현재 실행 조건이 아니다. 서비스 자체 제한과 별개로 RAM/NaN/crash/데이터 무결성 및 정상 저장 규칙은 유지한다.

> **2026-09-21 재개 후 해소:** 통과 ONNX로 최종 검수 Player를 다시 빌드했고 build success/error 0을 확인했다. 22초 headless smoke에서 R0-Full·Red PPO·Navy R0·1배속·assist None으로 부팅했으며 60판단까지 override와 직접 blocked-pass 보상 0, 런타임 예외 0이었다. 실행 파일은 `Builds/MNG_MS/MS2-Final-Review/MNG_MS2_Final_Review.exe`다. 사용자는 사람 확인을 MS3 진입 gate로 쓰지 않고 P0를 임시 합격으로 처리하도록 지시했으며 MS3 작업을 시작했다. 아래 내용은 중단 당시 인계 기록으로 보존한다.

## 중단 이유와 현재 결론

P0 학습과 두 번의 대규모 no-assist 평가는 완료되었다. 마지막 확인에서 Codex 5시간 사용량은 94% 사용, 잔여 6%였고 활성 중단선인 잔여 9% 이하에 도달했다. 주간 사용량은 46% 사용, 잔여 54%다. 추가 학습, 최종 검수 빌드, MS3, P1은 시작하지 않고 저장했다.

`MNG_MS2-20260921-r005-step99968`은 P0 통과 모델이다. 첫 100k 평가 지점에서 기본 seed와 독립 seed가 모두 모든 gate를 통과했으므로 같은 Run을 200k로 재개하지 않는다.

## 동결 모델

| 종류 | 경로 | SHA-256 |
|---|---|---|
| ONNX | `results/MNG_MS2-20260921-r005/MNG_Manager/MNG_Manager-99968.onnx` | `9d4f49c31e696a52691d230a74abe25e5e04699a4e4293bbf351c2e7d51e21dd` |
| PT | `results/MNG_MS2-20260921-r005/MNG_Manager/MNG_Manager-99968.pt` | `9ab8536762c0838960ec807fc943cc6e1cb4d166c4324db2d0406bb88ee3c0ca` |
| optimizer checkpoint | `results/MNG_MS2-20260921-r005/MNG_Manager/checkpoint.pt` | `f378a40525e0bf708f87060d7e9ba158a3bb9f99c34c12c9386fc46f1e7c9c68` |

중단 신호 처리 중 생성된 `MNG_Manager-102912.pt`는 크기 0바이트인 불완전 파일이다. 후보, 재개점, MS3 초기화에 사용하지 않는다. MS3 초기 정책은 위 `MNG_Manager-99968.pt`만 사용한다.

## 학습 무결성

- MS1 `MNG_MS1-20260920-r007-step7443` 정책 가중치에서 새 optimizer로 시작했다.
- R0-Full, Windows Player 32개, seed `192001`, policy assist `None`이다.
- worker 파일 32개, worker-start 32개, 고유 프로세스 32개, 완료 episode 148개다.
- raw/effective 명령 불일치 0, blocked-pass override 0, 직접 blocked-pass 판단 보상 0이다.
- 실패 integrity flag와 assist-mode 위반도 0이다.
- 증거: `Logs/MNG-MS/MNG_MS2-20260921-r005/p0-action-integrity.json`
- 100k inspector: `Logs/MNG-MS/MNG_MS2-20260921-r005/inspect-step100000.json`
- inspector 최근 5개 비가중 평균 reward는 `3.825236177444458`, 마지막은 `2.68999981880188`이다. `legacy_reward_leaks`는 비어 있다.

## 전체 고정 평가

각 평가는 R0-Full, 경기당 300초, Red/Navy 각각 ONNX 20경기와 균등 무작위 유효명령 20경기로 총 80경기다.

| seed | ONNX 합산 | Red | Navy | 득점:실점 | 무작위 | 격차 | 행동 무결성 | 판정 |
|---:|---:|---:|---:|---:|---:|---:|---|---|
| 491001 | 0.75 | 0.70 | 0.80 | 98:35 | 0.1625 | +0.5875 | mismatch/override/direct reward 모두 0 | 통과 |
| 592001 | 0.925 | 0.95 | 0.90 | 123:31 | 0.0875 | +0.8375 | mismatch/override/direct reward 모두 0 | 통과 |

원본 비교 파일:

- `Logs/MNG-MS/MNG_MS2-20260921-r005-step99968-p0-final-seed491001/comparison.json`
- `Logs/MNG-MS/MNG_MS2-20260921-r005-step99968-p0-final-seed592001/comparison.json`

두 평가 모두 합산 0.50, 진영별 0.40, 득점>=실점, 무작위 대비 +0.20을 완화 없이 통과했다. 각 평가의 네 원본 결과 JSON과 Player 로그, build log를 같은 디렉터리에 보존했다.

## 검증과 보존 상태

- P0 사전 전체 EditMode: `244/244` 통과
- 결과: `Logs/MNG-MS/MS2-p0-preflight-20260921/editmode.xml`
- source snapshot: `Logs/MNG-MS/MS2-p0-source-20260921`
- source manifest SHA-256: `2a00ecf532978e488afa7bba6490a9da3a8fc4f7601c24b21e140b8a03cf1c5e`
- 학습 Player: `Builds/MNG_MS/MS2/MNG_MS2.exe`
- r004 실패 시동과 기존 r003 모델·로그는 삭제하거나 덮어쓰지 않았다.
- P1 변경은 적용하지 않았다.
- trainer, `MNG_MS2`, `MNG_MS2_Preflight`, 해당 Run Python 하위 프로세스는 모두 종료 상태다.
- commit과 push는 수행하지 않았다.

## 다음 세션의 정확한 작업

1. 사용량 비율에 따른 실행 제한은 폐지했다(2026-09-23). 오류·중단 시 증거와 재개점을 보존한다.
2. 위 ONNX 경로와 SHA-256을 다시 확인한다.
3. `MNG_MSBuilder.BuildMS2FinalReviewBatch`에 Run ID `MNG_MS2-20260921-r005`, candidate ID `MNG_MS2-20260921-r005-step99968`, 위 ONNX 절대 경로를 전달해 `Builds/MNG_MS/MS2-Final-Review/MNG_MS2_Final_Review.exe`를 다시 빌드한다.
4. 기존 검수 폴더의 `NOT_APPROVED_P0_REFERENCE_ONLY.txt`는 새 통과 모델 빌드와 build-info가 확인된 뒤 제거한다.
5. 단일 headless smoke로 R0-Full, Red PPO, Navy R0, `policyAssistMode=None`, time scale 1, runtime exception 0을 확인한다. 대규모 평가는 이미 두 번 완료했으므로 반복하지 않는다.
6. 사람 확인용 `START_MS2_FINAL_REVIEW.cmd`, README, `review-build-info.json`에서 candidate ID와 ONNX SHA가 위 값과 일치하는지 확인한다.
7. 사용자에게 실행 절차와 눈으로 볼 항목을 안내한다. 이 단계에서 MS3나 P1을 시작하지 않는다.

## 사용자 확인 전까지 금지되는 작업

- r005를 200k 이상으로 재개하지 않는다.
- 0바이트 `MNG_Manager-102912.pt`를 사용하지 않는다.
- P1 Planner 변경을 P0 결과에 섞지 않는다.
- MS3 self-play를 시작하지 않는다.
- 기존 Run, 로그, 모델, 평가 원본을 삭제하거나 덮어쓰지 않는다.

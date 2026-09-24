> 보관일: 2026-09-24. 작성 당시의 계획·판정·승인 이력이며 현재 실행 지시가 아닙니다. 현행 기준: [현재 상태](../../../soccer/current-status.md).

# MS2 P0 no-assist proof 실행 계약

2026-09-23 사용자 지시: 5시간 및 주간 잔여량에 따른 사전 중단·재개 제한은 모두 폐지했다. 과거 사용량/중단 기록은 당시 사실이며 현재 실행 조건이 아니다. 서비스 자체 제한과 별개로 RAM/NaN/crash/데이터 무결성 및 정상 저장 규칙은 유지한다.

작성: 2026-09-21  
상태: 구현·검증 후 공식 Run 시작 예정

## 목적

MS2 P0 proof는 감독 PPO가 선택한 6개 명령과 실제 실행 명령을 완전히 일치시킨 상태에서 R0-Full보다 강한 정책을 다시 학습하고 검증한다. 선수 이동, 공 접근, 패스·슛 물리, 경로 회피 같은 저수준 기술은 기존 Manager-Simple 실행기를 유지한다. P1의 압박 인원, 공격 지원, 자동 정체 처리, 골키퍼 배급 변경은 이번 Run에 포함하지 않는다.

기존 `MNG_MS2-20260920-r003` 100,056-step 모델과 그 확인 빌드는 비교 자료로 보존하되 P0 확정 모델로 사용하지 않는다. `MNG_MS2-20260921-r004`는 sandbox의 worker 15 socket bind 거부로 31/32 연결 뒤 PPO update 전에 종료됐으며 실패 시동 증거로만 보존한다. 공식 proof Run은 sandbox 밖에서 동일 동결 입력으로 시작하는 `MNG_MS2-20260921-r005`다.

## 고정 입력

| 항목 | 값 |
| --- | --- |
| 초기 정책 | `MNG_MS1-20260920-r007/MNG_Manager-7443.pt` |
| 초기 정책 SHA-256 | `ac8eb65dd7c633a6cb5e4dfa267daa92d63c8df5bfecb4031a4fe28c5cb4d3c3` |
| optimizer | 새로 생성, 과거 MS2 optimizer 재사용 금지 |
| 학습 상대 | R0-Full, 이동 `1.0`, 판단 간격 `0.5초` |
| 병렬 환경 | 독립 Windows Player 32개 |
| 학습 seed | `192001` |
| 최대 aggregate step | `500,000` |
| checkpoint | 매 `100,000` step |
| 정책 보조 | `-mngPolicyAssistMode none` |
| P1 변경 | 적용하지 않음 |

`max_steps`는 32를 곱하지 않은 manager aggregate step이다. 정책 가중치만 MS1 step7443에서 초기화하므로 학습은 step 0과 새 optimizer 상태에서 시작한다.

## P0 행동·보상 불변식

PPO 학습과 평가는 다음 세 조건을 모두 만족해야 한다.

1. episode별 `policyRawCommands`와 `policyEffectiveCommands`가 모든 명령에서 일치한다.
2. `blockedPassOverrides == 0`이다.
3. `explicitBlockedPassRewards == 0`이다.

학습 worker가 한 번이라도 위 조건을 어기면 런타임 예외로 중단하고 해당 Run을 proof 실패로 처리한다. 평가는 네 결과 파일, 즉 Red/Navy의 ONNX와 동일 조건 무작위 유효명령에서 같은 불변식을 다시 검사한다.

완료 패스는 기존 물리 사건 기준을 유지한다. 공이 2.5m 이상 이동하고 다른 동료가 0.15초 이상 안정 소유해야 하며, 보상은 `+0.06`, 60초당 상한은 `+0.10`이다. 단순 `PassBuild` 선택에는 P0 보상을 주지 않는다.

## 100k 단위 실행

각 `100k`, `200k`, `300k`, `400k`, `500k` checkpoint에 대해 다음 순서를 지킨다.

1. PT·ONNX가 완전히 저장된 checkpoint를 동결하고 SHA-256을 기록한다.
2. 32 worker evidence를 검사해 행동 불변식 위반, crash, NaN, 누락 worker가 없는지 확인한다.
3. no-assist 고정 평가를 실행한다. Red/Navy 각각 ONNX 20경기와 무작위 유효명령 20경기, 경기당 300초다.
4. 평가 결과의 행동 불변식을 먼저 검사한다. 위반하면 경기 성능과 무관하게 proof 실패다.
5. 아래 성능 gate를 모두 검사한다.
6. 기본 seed에서 통과한 첫 checkpoint는 새 독립 평가 seed `592001`로 같은 전체 평가를 한 번 더 실행한다.
7. 두 평가가 모두 통과한 첫 checkpoint를 승격하고 학습을 종료한다. 500k까지 통과 후보가 없으면 P0 미통과로 보존하고 원인 분석 전 MS3를 시작하지 않는다.

## 성능 gate

- ONNX 양 진영 합산 score rate `>= 0.50`
- Red score rate `>= 0.40`
- Navy score rate `>= 0.40`
- ONNX 총 득점 `>=` 총 실점
- 같은 build·seed·상대의 무작위 유효명령보다 score rate `+0.20` 이상
- raw/effective mismatch `0`
- 강제 pass override `0`
- 직접 blocked-pass 명령 보상 `0`

평가 하나는 총 80경기다. 각 진영에서 ONNX 20경기와 무작위 20경기를 실행하므로, 작은 표본 진단으로 대체하지 않는다.

## 중단과 재개

사용량 비율에 따른 실행 제한은 폐지했다(2026-09-23). 오류·중단 시 증거와 재개점을 보존한다.

## MS3 진입 조건

MS3는 기본 seed와 독립 seed를 모두 통과한 P0 PT만 초기 정책으로 사용한다. P1은 P0 proof와 섞지 않는다. P1을 수행한다면 MS3보다 먼저 별도 A/B 변경으로 다루며, 사용자의 추가 지시 없이 이번 proof에 포함하지 않는다.

## 2026-09-21 실행 결과

- 공식 Run: `MNG_MS2-20260921-r005`
- 통과 checkpoint: `step99968`
- 학습 무결성: 32 worker, 완료 episode 148개, raw/effective mismatch 0, override 0, 직접 blocked-pass 보상 0, assist-mode 위반 0
- 기본 seed `491001`: ONNX `0.75`, Red `0.70`, Navy `0.80`, 득실 `98:35`, 무작위 `0.1625`, 격차 `+0.5875`, 행동 무결성 통과
- 독립 seed `592001`: ONNX `0.925`, Red `0.95`, Navy `0.90`, 득실 `123:31`, 무작위 `0.0875`, 격차 `+0.8375`, 행동 무결성 통과
- 두 평가는 각각 80경기를 모두 완료했으며 작은 진단으로 대체하지 않았다.
- 첫 checkpoint가 두 seed 모두 통과했으므로 200k 이후 추가 학습은 실행하지 않는다.
- 승격 PT: `results/MNG_MS2-20260921-r005/MNG_Manager/MNG_Manager-99968.pt`
- 승격 PT SHA-256: `9ab8536762c0838960ec807fc943cc6e1cb4d166c4324db2d0406bb88ee3c0ca`
- P1과 MS3는 시작하지 않았다. 사용량 중단선 때문에 최종 사람 확인 빌드만 다음 세션에 남긴다.

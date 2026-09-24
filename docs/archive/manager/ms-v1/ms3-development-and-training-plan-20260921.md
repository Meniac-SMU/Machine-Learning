> 보관일: 2026-09-24. 작성 당시의 계획·판정·승인 이력이며 현재 실행 지시가 아닙니다. 현행 기준: [현재 상태](../../../soccer/current-status.md).

# MS3 PPO self-play 개발·학습·평가 계획 — 2026-09-21

## 실행 결과와 완료 판정

이 계획은 공식 Run `MNG_MS3-20260921-r002`로 실행을 마쳤다. 정책은 MS2 P0 `step99968` 가중치에서 새 optimizer로 시작했고, P1 변경 없이 32환경 self-play를 aggregate `300,028` step까지 자연 종료했다. 50k 간격 산출물, 100k·200k 진단, 300k 최종 R0-Full 평가와 최종 사람 확인 빌드를 모두 보존했다.

- 32 worker, 완료 episode 484개, snapshot 상대 교체 11회, 학습 진영 교대 2회
- 양 팀 raw/effective action 불일치 0, override 0, 직접 blocked-pass 판단 보상 0, 무결성 위반 0
- R0-Full score rate: 100k `0.725`, 200k `0.625`, 300k `0.875`
- 300k 양 진영: Red `0.825`, Navy `0.925`; 득실 `137:29`; 무작위 대비 `+0.7125`
- P0 대비 유효 슛/분: `1.72 -> 1.825`로 `6.1%` 증가
- 최종 PassBuild 적용 횟수 0: 완료 패스 수치는 패스 판단 학습 증거로 사용하지 않음
- 선택 ONNX: `MNG_Manager-300028.onnx`, SHA-256 `1d0916383887939430401de9058c68ab1d3b841b654569d2aaf6c3db81df2899`

300k 최소 완료 gate를 통과했으므로 계획의 동결 규칙에 따라 400k~1M 연장을 하지 않았다. ML-Agents의 자연 종료 뒤 `training_status.json`을 보존하기 위해 100k와 200k에서는 번호가 붙은 checkpoint를 동결해 평가하고 공식 trainer를 강제 중단·재개하지 않았다. 이 절차 차이는 평가 입력과 간격을 바꾸지 않았고, 중단 시 초기 정책을 다시 불러오는 위험을 피했다. 최종 판정과 산출물은 [MS3 완료 보고서](ms3-completion-20260921.md)를 따른다.

## 결정과 출발점

사용자는 MS2 P0를 최종 완료가 아닌 **임시 합격**으로 판단했다. P1은 적용하지 않고, P0 사람 확인을 승인 gate로 사용하지 않으며, 검증된 P0 정책을 MS3의 초기 정책으로 사용한다.

- 초기 PT: `results/MNG_MS2-20260921-r005/MNG_Manager/MNG_Manager-99968.pt`
- SHA-256: `9ab8536762c0838960ec807fc943cc6e1cb4d166c4324db2d0406bb88ee3c0ca`
- 초기화 방식: 정책 가중치만 로드하고 optimizer는 새로 시작
- P1 Planner 변경: 없음
- 정책 보조: `MNG_PolicyAssistMode.None`
- 선수 이동·패스·슛·골키퍼·물리: P0와 동일

## 학습 환경

Red와 Navy에 각각 활성 `MNG_ManagerAgent` 하나를 둔다. 두 Agent는 같은 `MNG_Manager` BehaviorName을 사용하고 TeamId는 Red 0, Navy 1이다. 규칙형 감독, fallback 감독, 사람 입력은 모두 비활성화한다.

- 독립 Windows Player: 32개
- 정상 경기 시작과 킥오프 사용
- 경기 길이: 300초
- time scale: 10
- 학습 seed: `193001`
- 첫 필수 학습량: 300k aggregate manager steps
- 절대 상한: 1M aggregate manager steps
- checkpoint: 50k마다 PT·ONNX 저장
- 상세 평가: 100k마다

`max_steps`는 32를 곱하지 않는다. 32개 환경이 합쳐 만든 behavior step의 총량이다.

## Self-play 상대 구성

ML-Agents 기본 self-play만 사용한다. 별도 리그나 인구 기반 탐색은 만들지 않는다.

| 설정 | 값 | 목적 |
|---|---:|---|
| `save_steps` | 25k | 50k 점검 사이에 과거 정책 2개 확보 |
| `swap_steps` | 25k | 현재 정책 한 종류에 과적응하지 않도록 상대 교체 |
| `team_change` | 100k | 100k 평가 구간마다 학습 진영 교대 |
| `window` | 10 | 최근 과거 정책의 다양성 유지 |
| 최신 정책 상대 비율 | 0.5 | 현재와 과거 정책을 절반씩 상대 |
| 초기 ELO | 1200 | 절대 실력 승급값으로 쓰지 않고 추이만 기록 |

## 50k 단순 모니터링

50k, 150k, 250k 및 이후 홀수 50k 지점에서는 학습을 멈추지 않고 다음을 확인한다.

1. 번호가 붙은 PT와 ONNX가 0바이트가 아니며 해시를 계산할 수 있다.
2. reward, policy/value loss, ELO가 유한하다.
3. 32개 worker와 TeamId 0/1 연결이 유지된다.
4. Red/Navy 모두 raw action과 effective action이 일치한다.
5. 양 팀 override와 직접 blocked-pass 보상이 0이다.
6. snapshot swap이 계속 발생하며 100k 경계에서는 learning team change가 발생한다.
7. 여섯 명령 중 하나만 계속 선택하는 붕괴가 없는지 command count를 확인한다.

이 점검은 승급 평가가 아니다. 오류, NaN, 행동 불일치, worker 손실이 있으면 다음 checkpoint를 기다리지 않고 proof 실패로 중단한다.

## 100k 상세 평가

각 100k checkpoint에서 학습을 정상 중단하고 번호가 붙은 ONNX를 동결한다. `checkpoint.pt`나 중단 중 생성된 불완전 파일을 평가하지 않는다.

### 100k·200k

- R0-Full 상대
- Red/Navy 각각 ONNX 10경기
- 같은 조건 균등 무작위 10경기
- 경기당 120초
- 합계 40경기

### 300k와 이후

- R0-Full 상대
- Red/Navy 각각 ONNX 20경기
- 같은 조건 균등 무작위 20경기
- 경기당 300초
- 합계 80경기

평가 seed는 `491001`로 고정한다. P0 평가의 동일 seed·동일 진영 결과에서 같은 수의 앞쪽 match를 사용해 비교한다. 이를 통해 상대, 시작 분포, 진영 차이를 바꾸지 않고 학습량에 따른 차이를 본다.

## 전술 발전 판정

각 checkpoint에서 다음 수치를 경기당 비율로 비교한다.

- score rate와 Red/Navy 진영별 score rate
- 득점과 실점
- valid shot
- 5m advance
- completed pass
- 여섯 명령의 적용 횟수와 분포
- action integrity

P0 대비 전술 발전은 valid shot, 5m advance, completed pass 중 하나 이상이 경기당 5% 이상 증가하면서 R0 score rate가 P0 동일 표본보다 0.10 넘게 하락하지 않을 때 인정한다. 한 지표 증가가 다른 핵심 지표의 큰 붕괴와 함께 나타나면 발전으로 보지 않는다.

100k 이후에는 바로 앞 checkpoint도 함께 비교한다. score rate가 0.025 이상 증가하거나, score rate가 0.05 넘게 떨어지지 않은 상태에서 전술 사건 하나가 5% 이상 증가하면 진전으로 기록한다.

## 300k 최소 완료 gate

300k에서는 다음을 모두 요구한다.

- R0-Full 합산 score rate `>= 0.65`
- Red와 Navy 각각 score rate `>= 0.55`
- 총 득점 `>=` 총 실점
- P0 동일 seed 합산 score rate 대비 하락 `<= 0.10`
- valid shot, 5m advance, completed pass 중 하나 이상 경기당 5% 개선
- Red/Navy 학습 팀 교대 증거
- 과거 snapshot 상대 교체 증거
- 양 팀 raw/effective action 일치
- 양 팀 override 0, 직접 blocked-pass 보상 0

이 gate는 self-play 정책이 R0 전용으로 과적합되어야 한다는 의미가 아니다. 기본 축구 능력을 잃지 않은 상태에서 과거·현재 PPO를 상대하며 전술 사건이 발전했음을 확인하는 최소 회귀 기준이다.

## 300k 이후 연장

300k gate를 통과하면 MS3 완료 후보를 동결한다. 사용자의 공통 연장 지침에 따라 보상과 고정 평가가 계속 오르고 악용 징후가 없을 때만 다음 100k 한 구간을 추가할 수 있다. 이미 충분한 후보가 있고 개선 근거가 약하면 연장하지 않는다.

300k gate를 통과하지 못했지만 100k→200k→300k의 고정 평가가 계속 개선되면 400k부터 같은 절차로 연장한다. 최대 1M을 넘지 않는다. 두 번 연속 100k 평가에서 진전이 없으면 다음 checkpoint에서 중단하고 관측·마스크·보상·선수 기술 실행·self-play 상대 추출·평가 구조를 점검한다. 실패 뒤 보상 숫자부터 임의로 조절하지 않는다.

## 완료 산출물

- 최종 번호가 붙은 PT와 ONNX, SHA-256
- 32 worker 학습 evidence와 action-integrity 집계
- trainer log의 snapshot swap·learning team change 근거
- 50k inspector JSON
- 100k R0 원본 결과와 checkpoint assessment
- P0 대비 전술 지표 표
- 최종 사람 확인용 PPO 대 PPO 1배속 빌드
- MS3 완료 manifest와 사용자 확인 절차

## 중단선

사용량 비율 중단 기준은 2026-09-23에 폐지했다. 실제 오류나 사용자 중단 시 checkpoint와 재개 절차를 보존한다. reset credit은 사용자 지시 없이 사용하지 않는다.

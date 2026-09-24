> 보관일: 2026-09-24. 작성 당시의 계획·판정·승인 이력이며 현재 실행 지시가 아닙니다. 현행 기준: [현재 상태](../../../soccer/current-status.md).

# MS 평가·보상·완료 계약

> 2026-09-22 우선 적용: 새 실험의 기준은 [MS v2 학습·평가·승격 계약](../ms-v2/ms-rebuild-training-validation-20260922.md)이다. 32환경·100k 모니터·200k 상세·최소400k·1M 엄격 검증과 과거 모델 상대 우위를 적용한다. 아래 v1 gate와 보상 종속 전술 카운터는 과거 평가 재현용이며 새 모델 승격의 기준이 아니다. 실제 사건/보상 사건 분리는 [런타임 계약](../ms-v2/ms-rebuild-runtime-contract-20260922.md)을 따른다.

작성: 2026-09-19, MS2 P0 proof 계약 갱신: 2026-09-21. MS0는 [완료 보고서](ms0-completion-20260919.md)의 증거로 통과했다. MS1 v8의 패스 보조 gate는 사용자의 명시적 완화 결정 뒤 새 holdout seed로 판정했다. [MS 계획](ms-plan.md), [개발 인계](ms-agent-handoff.md), [MS2 P0 proof 실행 계약](ms2-p0-proof-plan-20260921.md)을 함께 따른다.

## 독립 평가와 최소 표본

- Training seed와 평가 seed를 분리한다. 학습 seed 초기 후보 191001, checkpoint 진단 291001~291020, MS1 최종 공격 391001~391020·수비 392001~392020, MS2/3 최종 경기 491001~491020을 사용한다. reset 내부 RNG까지 seed를 전달한다.
- 경기 seed 하나당 같은 초기조건의 Red/Navy 교환 2경기를 쌍으로 실행한다. 최종 대전 상대마다 **20 seed × 2진영 = 40경기, 경기당 300초**다. 현재 MS1 빠른 구현은 Red 정책 고정으로 결정론적 120개 30초 episode를 실행한다. 첫 공격 40·수비 40을 주 gate로 쓰고, 전체 공격 60개에서 정확한 Pass 유형 20개를 패스 보조 gate로 쓴다. 진영 교환 평가는 MS2 정상 경기 gate부터 필수 적용한다.
- checkpoint 진단은 10 seed × 양 진영 = 20개를 쓰며 MS1은 두 상황군을 각각 평가한다. MS2/3의 저비용 진단은 120초, 최종 gate는 300초다. 서로 다른 경기 길이의 점수를 직접 비교하지 않는다.
- 최종 seed는 후보 선택용으로 반복 최적화하지 않는다. 한 단계의 선정 후보에 1회 사용하고, 실패 뒤 계약이나 구현을 수정했다면 새 protocol revision과 새로운 holdout seed를 평가 전에 고정한다. 과거 실패 결과는 보존한다. MS1 v8은 공격 `405001`, 수비 `406001`을 사용했다.
- 추론은 탐색 설정을 고정하고 trainer update를 하지 않는다. 모델·build·프로필·seed·팀·실행모드를 기록한다. crash/timeout/결과 누락은 패배로 묻지 말고 평가 무효로 처리한다.
- 득점 평균, 실점 평균, 승/무/패, score rate `(승+0.5×무)/경기수`, 진영별 결과를 함께 보고한다. 40경기는 신속 개발의 최소 판정이며 보편적 우월성의 통계적 증명은 아니다. seed쌍 단위 불확실성 구간도 보고하되 실패 후 합격선을 바꾸는 근거로 쓰지 않는다.

## MS0 준비 gate

1. 현재 R0 source snapshot과 hash, Full 프로필 동등성, 물리/관측/마스크/제어권/보상 이벤트 중복/episode 종료 검사 통과.
2. MS 전용 Builder Validate·컴파일·실행 build 성공. 1개·2개·8개 환경에서 독립 경기/seed/로그/포트를 확인하고 다음 단계의 첫 병렬 후보를 정한다. 16개는 MS0 필수 검사가 아니며 실제 본학습 자원이 필요할 때만 비교한다.
3. PPO update 1회 이상, 유한 loss/reward/관측, PT·ONNX 생성, 동일 Run resume 후 step 증가 확인. 임의 heuristic이 학습을 대체하지 않는지 trace 확인.
4. 실제 Player episode에서 policy 명령이 mask를 통과해 공통 Planner/Executor로 수용되고 한 팀에 writer가 하나뿐인지 기록한다. 명령별 공격·수비 성공률과 무작위 baseline은 MS1 고정 평가에서 확인한다.
5. self-play 양 Team ID, snapshot 생성·상대 변경·학습 팀 교대, 승/무/패 terminal reward 부호를 작은 smoke로 확인한다. 합격 Run 합계는 5k 이내로 관리하고, 추가 진단을 실행했다면 모델 선택에 쓰지 않은 별도 Run·실제 초과량·사유를 완료 보고서에 공개한다.

## MS1 세부 학습 gate

- MS1 학습 Run은 `--num-envs 16`의 독립 Windows Player 16개를 사용한다. worker 수를 낮춘 Run은 사용자 재승인 전 MS1 합격 후보가 될 수 없으며, `max_steps=100000`은 worker 합산 aggregate step이다.
- 공격 성공: episode 내 실제 득점 **또는 유효 슛 사건** 1회 이상. `AttemptShot` 선택이나 단순 공 접촉은 성공이 아니다.
- 수비/전환 성공: 상대 득점 없이 자기 팀이 공을 회수하여 0.5초 이상 유지. 처음부터 자기 소유로 reset하지 않는다. 시간만 버틴 episode는 회수 성공이 아니다.
- R0-Easy 0.35 고정, 공격 성공 **24/40 이상**, 수비/전환 성공 **20/40 이상**이다. 각 지표는 동일 seed의 무작위 유효명령 baseline보다 최대 **2 episode까지만 낮을 수 있다**. 최종 후보는 baseline보다 공격 `+7`, 수비 `+2`라서 이 허용치를 사용해 손실을 가리지 않았다.
- 공격 fixture 중 10 seed쌍(20개)은 패스 통로와 동료가 있는 압박 상황으로 미리 고정한다. 이 하위집합에서 **실제 완료 패스 2회 이상**이며 동일 seed 무작위 유효명령 baseline보다 **비열등(차이 0 이상)**이어야 한다. 패스는 MS1 보조 gate이며 명령 횟수/우연한 동료 접촉은 제외한다. 공이 2.5m 이상 이동하고 의도한 동료가 0.15초 이상 소유해야 완료로 센다.
- 과거 M1의 높은 득점 수·패스 플레이트 횟수 조건은 상속하지 않는다. 위 최소 패스 근거는 PassBuild 선택이 실제 실행으로 이어지는지 확인하기 위한 것이다.
- 각 진영 결과·mask availability·명령 선택·실행 실패·override를 함께 보고한다. 성공률만 넘고 학습 명령이 무시되는 정책은 gate 실패다.

## MS2 강한 PPO vs 완성 R0 gate

- P0 proof는 MS1 step7443 정책 가중치에서 새 optimizer로 시작하고 R0-Full, 32환경, 최대 500k를 사용한다. 장면은 정상 킥오프 경기이며 강제 소유·상황별 재배치는 없다. P1 변경은 섞지 않는다.
- 100k마다 전체 최종 형식으로 평가한다. 양 진영 ONNX와 동일 조건 무작위 유효명령을 각각 20경기, 경기당 300초 실행한다.
- 최종 정상 R0 40경기에서 **score rate ≥ 0.50**, **총 득점 ≥ 총 실점**, **양 진영 score rate 각각 ≥ 0.40**를 만족한다.
- 같은 R0·seed·build에서 무작위 유효명령 baseline보다 score rate **20%p 이상** 높아야 한다. 첫 통과 후보는 독립 seed `592001`에서 같은 전체 gate를 재현해야 한다.
- 모든 학습 episode와 평가에서 raw/effective 명령이 전 명령에 걸쳐 같고, 강제 pass override와 직접 `BlockedForwardPassDecision` 보상이 모두 0이어야 한다. 한 번이라도 위반하면 성능과 무관하게 P0 proof 실패다.
- 여기서 ‘강한 PPO’는 **승인된 R0에 대등 이상이고 무작위 감독보다 개선된 MS 정책**이라는 운영상 정의다. 80~90% 압승을 기다리며 self-play 진입을 지연시키지 않는다. 무승부만으로도 baseline 개선과 득실점 조건을 따로 충족해야 한다.
- 최소 1회 실제 ONNX가 감독 명령을 내리는 inference 경기를 확인한다. 규칙형 결과나 학습 중 heuristic을 최종 PPO 성과로 대신하지 않는다.

## MS3 self-play 완료 gate

- 2026-09-21 확정 계약은 [MS3 개발·학습·평가 계획](ms3-development-and-training-plan-20260921.md)을 따른다. MS2 P0 PT로 정책 가중치만 초기화하고 새 optimizer로 실제 PPO update를 수행하며, 과거 snapshot 교체와 Red/Navy 학습 진영 교대를 로그로 입증한다.
- 300k 최종 R0-Full 평가는 양 진영 ONNX/무작위 각 20경기, 경기당 300초인 총 80경기로 시행한다. 합산 score rate **0.65 이상**, Red/Navy 각각 **0.55 이상**, 총 득점이 총 실점 이상이어야 한다.
- 같은 seed의 MS2 P0 score rate보다 **10%p 초과 하락하지 않아야** 하며, valid shot·5m advance·completed pass 중 실제 PPO 명령과 연결되는 한 지표가 분당 5% 이상 개선되어야 한다. 직접 명령이 0회인 사건은 개선 증거로 세지 않는다.
- 학습과 평가 전체에서 양 팀 raw/effective action이 같고, 강제 override와 직접 `BlockedForwardPassDecision` 보상이 0이어야 한다. 하나라도 위반하면 성능과 무관하게 실패다.
- PT/ONNX/config/source/build hash, 50k 점검, 100k 상세평가, 행동 무결성, 진영/상대별 결과, 실행 가이드와 1배속 PPO 대 PPO 시연을 남긴다. 학습되지 않은 복제 정책이나 random 상대를 self-play 증거로 쓰지 않는다.
- 정책 간 직접 승부는 보조 진단으로 사용할 수 있지만 완료 필수 gate는 아니다. R0 고정 상대와 동일 seed P0 기준을 사용해 checkpoint 사이의 절대 경기력과 회귀를 비교한다.

## 보상 — 실제 사건을 사용

MS1/2는 현재 `MNG_RewardProfile`의 Base 값을 재사용한다. 단계용 timeout 보상을 새로 추가하지 않는다. Rule 보상은 통계용이며 policy endpoint가 활성인 팀에만 학습 신호를 전달한다.

| 사건 | 현재 Base 값 | MS1/2 계획 | 변경량 |
| --- | ---: | ---: | ---: |
| 득점 / 실점 | +1 / -1 | 동일 | 0 |
| 경기 승 / 패 | +0.5 / -0.5 | 실제 경기 종료에서 동일 | 0 |
| 유효 슛 | +0.02 | 동일 | 0 |
| 완료 패스 | +0.06 | 동일 | 0 |
| 전진 차단 후 패스 판단 | +0.01 | P0 PPO 학습·평가 `0`; 명시적 legacy 재현에서만 +0.01 | `-0.01` |
| 5m 전진 | +0.01 | 동일 | 0 |
| 회수 / 빠른 회수 | +0.03 / +0.02 | 동일 | 0 |
| 밀집 | -0.002 | 동일 | 0 |
| shaping 총 상한 | 분당 0.25 | 동일 | 0 |

- 기존 사건별 cap·event ID 중복 방지·소유/패스 판정은 재사용한다. Attack/Defense/Press 배율을 섞지 않고 Base 하나만 쓴다. P0의 `MNG_PolicyAssistMode.None`에서는 `BlockedForwardPassDecision`을 호출하지 않아 명령 선택 직접 보상이 0이다. `+0.01` 명목값은 과거 결과 재현용 `LegacyBlockedForwardPass` 모드에만 남는다.
- 30초 MS1 구간 절단에 MatchWin/MatchLoss를 추가하지 않는다. MS2 120초와 최종 300초 경기는 실제 점수에 따라 결과를 처리한다. 동점은 결과 reward 0.
- MS3 terminal만 마지막 transition 값을 `승 +0.5 / 무 0 / 패 -0.5`로 확정한다. 기존에는 마지막 틱 사건 reward와 결과 AddReward가 합산될 수 있으므로 변경량은 고정 상수가 아니며 **확정 terminal 값 - 기존 마지막 틱 누적 reward**다. 마지막 틱 goal/shaping이 있었는지 기록한다.
- MS3의 마지막 틱 외 득실점/보조 사건은 유지한다. 이 예외는 설치 trainer의 승패 부호 해석과 맞추기 위한 것으로 R0 점수·경기 결과 판정을 바꾸지 않는다.
- 보상 변경 규칙에 따라 `docs/soccer/rewards.md`, 코드/Profile, 테스트, README와 현재 상태를 함께 갱신한다. 2026-09-20 완료 패스 보상은 실제 사건에 한해 `+0.04 -> +0.06`으로 변경했다.
- r008·r009는 역사적 `-mngMS1LearnPassChoice true`로 런타임 pass override를 끈 실험이었다. P0부터는 역사적 스위치에 의존하지 않고 `MNG_PolicyAssistMode.None`이 PPO 학습·평가·제품 추론의 기본값이다.

## 증거와 진전 판정

- 주지표: MS1은 공격/수비 성공률 중 낮은 값, MS2는 Full R0 진단 score rate, MS3는 동결 MS2 대비 score rate(동시에 R0 회귀 확인). 진단 protocol은 단계 중 고정한다.
- 두 연속 평가에서 주지표 개선이 없으면 정상 저장하고 명령/보상/마스크/실행 telemetry로 원인을 조사한다. 단순 보상합·entropy 증가만으로 연장하지 않는다. 상대 강도가 바뀐 전후 점수를 동일 곡선으로 비교하지 않는다.
- 필수 telemetry: raw policy/accepted command, mask availability, 실제 plan·override, 패스 시도/완료, 슛/유효 슛, 전진·회수·득실점, 자책골, 정체·밀집, 이벤트별 reward/cap, worker episode/seed, PPO update/loss, model/hash.
- 실제 정책의 개선과 코드의 효과를 구분하기 위해 같은 build에서 무작위 유효명령·초기 PPO·최종 PPO·R0를 비교한다. 고정 Balanced는 MS0 명령 영향 진단용으로만 사용한다.
- 보상 exploit, 한쪽 진영 붕괴, 모델 미연결, Rule과 PPO 이중 제어, 가짜/중복 사건, worker 데이터 덮어쓰기, NaN·timeout은 진전 수치와 무관하게 차단한다.
- 최종 보고는 `R0 사용자 완료`, `MS0 준비 완료`, `MS1 학습 통과`, `MS2 R0 대전 통과`, `MS3 파이프라인 완료`, `self-play 성능 향상 입증 여부`를 각각 표시한다. 계획 작성이나 build 성공을 학습 완료로 표시하지 않는다.

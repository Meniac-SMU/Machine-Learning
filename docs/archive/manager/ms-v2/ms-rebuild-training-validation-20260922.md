> 보관일: 2026-09-24. 작성 당시의 계획·판정·승인 이력이며 현재 실행 지시가 아닙니다. 현행 기준: [현재 상태](../../../soccer/current-status.md).

# MS v2 학습·평가·승격 계약

2026-09-23 사용자 지시: 5시간 및 주간 잔여량에 따른 사전 중단·재개 제한은 모두 폐지했다. 과거 사용량/중단 기록은 당시 사실이며 현재 실행 조건이 아니다. 서비스 자체 제한과 별개로 RAM/NaN/crash/데이터 무결성 및 정상 저장 규칙은 유지한다.

상태: 2026-09-22 계획. [주 계획](ms-rebuild-plan-20260922.md), [런타임 계약](ms-rebuild-runtime-contract-20260922.md)과 함께 적용. 수치는 새 실험의 시작 계약이며 구현/학습 결과를 의미하지 않는다.

## 1. 공통 실행 계약

- 32개 독립 Windows Player, 한 경기 Red/Navy 감독 각 1명, PPO self-play. 두 팀에 동일한 기술·물리·관측 권한 적용.
- aggregate step은 학습팀 감독 transition 합계. 환경 수 32를 다시 곱하지 않는다. 반대팀은 현재 또는 과거 snapshot을 사용하는 고정 상대이며 두 독립 optimizer의 동시 학습이라고 설명하지 않는다.
- 모든 상세 경기/회귀/승격 평가는 **300초**. 120초와 300초 결과를 연결해 진척 곡선을 만들지 않는다.
- 정책·실행기·물리·보상·관측 schema·상대 pool·trainer 버전과 초기값/optimizer 출처를 manifest에 저장. scorer는 이 값이 다른 결과를 자동 합산하지 않음.
- 새로운 history는 MS2-v2 준비 통과 policy를 self-play step0으로 시작한다. 구 MS3 계열은 초기값·후보·기본 학습 상대에서 제외한다.
- 사용량 비율에 따른 실행 제한은 폐지했다(2026-09-23). 오류·중단 시 증거와 재개점을 보존한다.

## 2. PPO 시작 설정

구조 수정의 효과를 해석할 수 있도록 보상 숫자·gamma·network 크기는 처음에 유지한다. 1M까지 연장이 예정된 self-play에서는 종료 직전 갱신 폭이 사라지지 않도록 schedule을 명시한다.

| 항목 | 구 MS3 | v2 시작 계획 | 이유 |
|---|---:|---:|---|
| batch/buffer/epochs | 256/4096/3 | 유지 | 알고리즘 규모 통제 |
| hidden layers/units | 2/128 | 유지 | 관측/실행 구조 우선 |
| learning rate | 5e-5 linear | 5e-5 constant | 종료 예산에 종속된 조기 소진 방지 |
| beta | 0.003, implicit linear | 0.003 constant | 상대 변화에 대한 탐색 유지 |
| epsilon | 0.15, implicit linear | 0.15 constant | schedule 명시, 실제 KL/clip fraction 검사 |
| gamma/lambda/horizon | .995/.95/128 | 유지 | 장기성 조정은 별도 가설 |
| max_steps | 300k | 1M 최초 선언 | 400k checkpoint의 판단으로 계속 여부 결정 |
| checkpoint interval | 50k | 100k | 사용자 점검 주기와 정렬, graceful-stop 저장 별도 |
| snapshot save | 25k | 10k | opponent history 확보, 모니터링 횟수와 구별 |
| team_change | 100k | 40k | 더 많은 양 진영 순환, save의 4배 |
| opponent swap | 25k | 10k ghost steps | 상대 장기 고정 완화 |
| recent window | 10 | 30 + 별도 pinned history | recent 순환과 영구 상대 구분 |

이 설정은 최적값 주장이 아니다. 구현 이후 훈련 smoke에서 update/entropy/KL/finite 확인. 과도한 KL·불안정이 실제 발생하면 한 항목만 수정하고 새 실험 manifest에 기록한다. 보상 변화가 없더라도 구조 및 schedule이 달라져 구 MS3와 순수 학습량 비교 실험은 아니다.

## 3. 과거 강자를 잊지 않는 상대 풀

- 기본 확률: 최신 snapshot 30%, recent 50%, pinned 20%. 각 선택 시 실제 상대 ID/SHA/선택 확률/학습 진영 기록.
- pinned: 새 step0 정책 + 승격된 역사 champion. champion이 아직 없으면 step0만 사용한다. 이미 동일 SHA가 있는 경우 중복 등록하지 않는다.
- recent는 30개 순환, pinned는 별도 영구 보존. 400k/1M에서 과거 champion을 recent overflow로 삭제하지 않음. 학습용 상대 pool만 old 모델 파일 삭제와 혼동하지 않는다.
- 1M 후 champion이 많아지면 중요 약점 상대를 우선 선택하되 선정 규칙·가중치 변경을 새 실험으로 기록. 초기에는 성능 적응형 확률을 넣지 않음.
- stock ML-Agents YAML만으로 pinned 혼합과 완전 resume가 된다고 가정하지 않는다. 설치된 GhostTrainer의 snapshot 저장/선택/복원 지점을 먼저 확인하고 **프로젝트 내부 trainer extension/adapter**로 최소 변경을 구현한다. 외부 conda 설치 파일을 직접 수정하지 않는다.
- 첫 구현은 같은 상대 정책을 32환경에 배포하는 방식을 허용한다. 서로 다른 32종 상대를 동시에 사용한다고 주장하지 않으며, per-worker league 엔진 제작을 필수 범위로 확대하지 않는다.
- 학습 중 상대 교체는 step/tick과 상대 구간을 기록한다. episode 도중 교체된 경기는 별도 표시하고 내부 ELO를 공식 승격 근거로 쓰지 않는다. frozen 평가에서는 경기 도중 교체 금지.
- resume에는 학습 모델/optimizer 외 pool 가중치·SHA·RNG·team/swap/save counter를 저장한다. stock 저장이 불충분하면 sidecar를 구현하고 uninterrupted/resume smoke에서 상태 연속성 검증. 과거 pool 유실 재시작을 같은 self-play 경험으로 위장하지 않음.
- 별도 rule opponent를 PPO 학습에 혼합하는 것은 첫 self-play의 필수가 아니다. R0는 준비 학습과 외부 평가에 사용한다. 1M 이후 필요성이 증명되면 혼합 학습을 새 단일 가설로 추가한다.

## 4. 일정과 연장 판단

| 시점 | 실행 | 판정 |
|---|---|---|
| 시작 | 초기 정책/SHA, 32환경, inspector, 고정 리그 상대 명세 저장 | 준비 gate 통과 확인 |
| 매 100k | reward/entropy/KL/value loss/조건부 action/정체/사건·task 무결성, 자원, pool 점검 | 이상 시 저장·원인 확인 |
| 매 200k | 아래 동일 조건 리그 + R0 고정평가 | 독립 경기 성능과 실제 행동 분석 |
| 200k | 진단 평가 | 일반 성능 정체만으로 최소400k 생략하지 않음 |
| 400k | 최소 self-play 구간 완료, 첫 champion 후보 검증 | 무조건 완료 선언 금지 |
| 600k/800k | 이전 두 상세 평가의 공통 상대 성능 및 champion 비교 | 개선 시 다음 200k 연장 |
| 1M | 엄격 holdout·독립 seed 검증 | 통과/정체/퇴보·상성 판정 후 다음 실험 결정 |
| 1.2M~2M, 이후 2.2M~3M | 각 200k 단위 상세평가, 매100k 점검 | 2M/3M도 엄격 평가, 예산 자동 무제한 확대 금지 |

사용량 비율에 따른 실행 제한은 폐지했다(2026-09-23). 오류·중단 시 증거와 재개점을 보존한다.

400k 이후에는 같은 anchor 상대 묶음에서 점수율/득실이 개선되고 실제 임무/전술 지표가 악화되지 않을 때 연장한다. reward는 보조 해석이며 상대 변경 때문에 흔들릴 수 있으므로 reward 단독 상승/하락을 연장·종료 기준으로 삼지 않는다. 두 연속 상세 평가에서 개선이 없으면 다음 checkpoint에서 멈춰 구조/상대/보상 가설을 검토한다. 400k 이전 평가는 진단 자료로 사용하고 단순 정체 조기종료와 충돌시키지 않음.

400k champion 통과 자체로 전체 실험을 자동 종료하지 않는다. 승격은 별도 동결하고, 사용자 목표에 맞게 좋은 추이가 지속되면 1M까지 진행한다. 이는 구 'gate 통과 즉시 종료' 규칙을 대체한다.

## 5. 상세 평가 상대와 예산

평가 기준 모델은 모두 같은 runtime/schema 버전에서 동작한다. 선정 정책 SHA를 평가 시작 전에 동결한다.

- 학습량 순서 비교: 현재 후보 대 step0, 가장 최근 200k checkpoint, 현 champion, history 중 이전 상세평가에서 가장 어려웠던 상대. 중복 SHA 제거, 최대 4개 neural 상대.
- 고정 진척 곡선용 anchor는 step0, step200k가 생성된 뒤 이 둘을 계속 유지. 상대 풀이 늘어났다는 이유로 전체 평균을 과거 평균과 직접 비교하지 않음.
- Rule 비교: R0-Full-v2. 새 Planner가 적용된 상대라 기존 R0-Full-v1과 구별한다. 동작 속도·판단 주기를 임의로 높이지 않는다.
- 단순 기준 정책은 step0/400k/1M에 비교: uniform-valid, 항상 회수, 균형 유지, 소유 시 전진·슛 가능 시 슛. masked command는 Balanced fallback으로 고정·기록.
- 각 일반 상세 대진은 **20 초기조건 × 양 진영 = 40경기**, 300초. neural 4개 + R0 1개라면 최대 200경기. baseline 및 승격 확인은 별도 예산으로 명시.
- 경기 seed와 inference RNG seed를 모두 기록한다. scorer가 한 결과를 여러 seed로 중복 처리하지 않게 identity 검사. ONNX 추론 sampling/argmax 모드는 manifest로 고정하고 평가 중 변경하지 않음.
- 같은 seed에서 진영 교환을 묶어 계산하고, 진영별 성능도 별도 보고한다. 20쌍이 경계 결과면 '확정 우위'로 주장하지 않음.
- 고정 개발 seed는 모든 후보 진단에 재사용할 수 있으나 최종 승격에는 **새 holdout seed** 사용. 반복 조회한 holdout은 이후 개발 세트로 전환한다.

## 6. 승격: 과거 모델 상대 우위가 주 기준

점수율은 `(승 + 0.5×무)/경기 수`. 최고 보상·가장 최신 step·R0 최고점만으로 champion을 교체하지 않는다.

| 기준 | 새 계약의 권장 수치 |
|---|---|
| 최소 경험 | 해당 self-play Run 400k 이상 |
| 직전 champion 대전 | holdout 점수율 ≥0.60, paired 95% 구간 하한 >0.50 |
| 첫 champion | champion 대신 새 step0 reference를 위 조건으로 평가 |
| 역사 리그 | 고정 가중 평균 ≥0.55, 주요 개별 neural 상대 ≥0.45 |
| 진영 | champion 대전 각 진영 점수율 ≥0.50, 편향 발견 시 진영별 원인 기록 |
| 득실 | champion 상대 합산 득점 ≥실점, 단순 무승부 누적 승격 방지 |
| R0-v2 | 진단용 보조 회귀. 이전 champion 대비 0.05 초과 점수율 하락이면 동일 조건 추가 확인 후 원인 보고 |
| 무결성 | raw/effective 불일치0, 출처 없는 자동 전술0, 중복 사건0, schema 동일 |

R0 회귀만으로 신경망 상대 우위를 무시해 기존 모델을 영구 고집하지 않는다. 신경망 우위는 확실하지만 R0 회귀가 재현되면 '범용 champion' 승격은 보류하고 '리그 개선 후보'로 보존, 회귀 원인을 다음 실험에 반영한다. 단순 무작위보다 약한 모델은 초기 승격 후보로 쓰지 않는다.

일반 승격 확인은 별도 신규 40 seed쌍=80경기로 시작한다. 경계 판정은 신규 seed를 추가해 최대 80쌍=160경기까지 확인. 다중 중간조회로 우위를 부풀리지 않도록 확인 총 표본 수와 검사 시점은 시작 전에 고정한다. 40경기 진단에서 유리한 순간만 골라 조기 승격하지 않음.

통계 구현: seed쌍을 단위로 20,000회 paired bootstrap, RNG seed와 데이터 hash 저장. 다수 상대를 동시에 '모두 우세'라고 주장할 때는 사전 정의한 다중비교 보정 또는 별도 확증 시험 필요. 위 0.45 바닥은 강한 상대 하나에게 크게 약해지는 것을 막는 실무 gate이지 모든 과거 모델에 대한 통계적 우위를 뜻하지 않는다.

## 7. 1M의 엄격 평가와 이후 수정

1. 해당 runtime에서 0/200k/400k/600k/800k 및 champion 중 중복 제거, 최신 1M 후보와 대전. 과거 1M 미만 모델은 이번 새 실험의 모델이다.
2. **최소 2개 독립 holdout seed 가족, 가족마다 25쌍**, 합계 대진당 100경기. 직전 champion 및 가장 어려운 상대는 가족마다 50쌍=합계200경기. 전부300초, 같은 inference mode.
3. 위 승격 gate를 적용하고 가장 어려운 상대·진영별 결과를 함께 공개. R0-v2 및 단순 baseline은 각각 최소40경기로 보조 검사.
4. 주요 개선이 첫 학습 seed의 우연인지 확인하기 위해 같은 고정 설정으로 독립 학습 seed 재현. 최소400k, 필요 시1M까지 비교. 이 비용을 첫 Run의 step에 합산하지 않음. 시간 부족이면 '1M 1seed 후보'로 표시하고 엄격 재현 완료라고 주장하지 않음.
5. 개선이 지속되고 검증을 통과하면 runtime/보상 고정 상태로 200k씩2M까지 확장. 1M에서 정체면 무조건2M 진행하지 않고 한 가설만 변경.
6. 상대 구성/schedule 조정은 새 Run/실험 metadata로 분리. 관측·action·물리·reward 판정 변경 시 새 환경 버전으로 분리하고 이전 모델도 동일 새 조건에서 재검증. 입력 schema가 달라지면 직접 resume 금지.
7. 값 변경을 동반한 새 Run은 경험 계보를 기록하여 '추가200k', '부모1M', '누적경험1.2M'을 구분. optimizer를 초기화했는지도 명시.
8. 2M에서 다시 엄격 평가 후에만3M까지200k 단위 확장. 3M은 첫 장기 계획의 상한이며 이후에는 별도 계획 갱신.

## 8. 필수 결과 보고 형식

- 대진표: 후보/상대 SHA, 학습step, 환경버전, seed수, 시간, 진영, 승무패·점수율·득실·신뢰구간.
- 행동표: 소유/비소유/위험지역/mask별 조건부 명령 비율, actual task 인원, raw/rewarded 사건, 자동 안전 개입률, 임무 지연/취소 이유.
- 발전 설명: '회수 명령 증가'보다 '해당 상태의 압박 인원 변경 → 탈취/실점/득점 변화'를 확인. 인과 단정은 같은 상태 개입 실험이 있을 때만 수행.
- 상태 구분: 준비 통과 / 후보 / champion / 은퇴 / 재현 미완료. 학습 중 잘못된 모델을 champion처럼 리뷰 빌드에 연결하지 않음.
- 원본은 읽기 전용 보존. 전체 audit/export/평가 gate 실패도 지우지 않음.

## 9. 설정 근거

설치된 trainer 코드를 기준으로 실제 지원 여부를 확인하고 구현한다. 공식 ML-Agents 문서는 상대 다양성·안정성 trade-off, save/team/swap/window의 의미와 보수적인 shaping을 설명한다. 이 문서의 숫자는 공식 최적값이 아니라 이번 프로젝트에 대한 시작 제안이다.

- [Unity ML-Agents Training Configuration — Self-Play](https://unity-technologies.github.io/ml-agents/Training-Configuration-File/#self-play)
- [기존 소스·학습 로그 감사](rl-architecture-audit-20260922.md)

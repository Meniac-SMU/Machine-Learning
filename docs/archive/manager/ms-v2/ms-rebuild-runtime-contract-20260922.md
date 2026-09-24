> 보관일: 2026-09-24. 작성 당시의 계획·판정·승인 이력이며 현재 실행 지시가 아닙니다. 현행 기준: [현재 상태](../../../soccer/current-status.md).

# MS 개편 런타임·P1 구현 계약

2026-09-23 MS3-v2 안정화(D3~D5): 현행 revision은 `MNG-P1-MS3-contact-reset-20260923`이다. MNG 공·8선수의 접촉 solver 반복을 position48/velocity16으로 통일했다(기존 엔진 기본6/1). 질량·속도·킥·보상·관측·명령 수는 유지하며 전역 Physics나 별도 Core 환경은 변경하지 않는다. `ResetRound`는 Transform와 Rigidbody의 위치·회전을 함께 복원하여 jitter/SyncTransforms 후에도 초기 관측 방향이 남은 회전에 오염되지 않게 한다. PPO·규칙형·사람 선수에 같은 물리 경로를 적용한다. 수치적 접촉 민감성을 줄인 보정이며 bitwise 미러 궤적이나 완전한 승률 동등성을 보장하지 않는다. 원인 분리·회귀·최종 Player 증거는 [D3~D5 보고서](ms3-stability-d3-d5-report-20260923.md)를 따른다. 아래 물리 프로필 유지 문구는 그 작성 시점의 이력이다.

2026-09-23 Post-R6 수정: 관측 244·행동 6·task/event version 2를 유지하며 환경 전이 revision을 `MNG-P1-postR6-20260923`, runtime protocol을 3, 동결 평가 protocol을 `MNG-V2-FROZEN-300-v2`로 구분한다. 팀 상대 좌우 목표, 중립 동률 우선권, 전 경기 정체 집계를 수정한다. 패스 후보의 경로 평가는 실제 킥의 lead target을 사용한다. 수신 선수는 공을 전면판으로 마주 보며 수신하고, 패서는 기존 회전 속도와 예상 비행 시간으로 수신 준비를 기다린다. 소유자의 킥 조준 회전은 실제 공 방향과 기존 capture radius로 선행량을 제한하고 전진 드리블 조건을 유지한다. 수신 성공을 소유 상실로 취소하지 않는다. 킥 준비는 기존 2초 만료이며, 실제 Pass strike가 발생하면 같은 부모 명령의 유효한 ReceivePass에 출발 시점부터 기존 2초 수신 예산을 준다. 수신 중에도 새 감독 명령·상대 소유·경기 정지는 즉시 적용하고, 이미 교체되거나 만료된 임무는 복원하지 않는다. 새 패스 강제 선택·전략 mask·보상은 추가하지 않는다. 공통 실행기를 사용하는 규칙형 감독에도 같은 기술 수정이 적용된다. 결과와 증거는 [현재 상태](../../../soccer/current-status.md)를 따른다.

상태: 2026-09-22 계획. [주 계획](ms-rebuild-plan-20260922.md)의 기술 명세. 아래 값은 후속 구현의 시작 설계이며 코드 적용값이 아니다. 기술상 조정이 필요하면 근거와 영향, 검증 결과를 같은 문서에 남긴다.

2026-09-23 최종 보완: 드리블 흔들림의 시간 입력은 전역 `Time.fixedTime`에서 `EpisodeElapsedSeconds`로 변경했다. 물리 계수와 흔들림 진폭·주파수는 유지하며 다른 경기의 경과 시간이 현재 경기의 제어 위상에 섞이지 않게 한다. 최종 r4의 잔여 진영 격차를 이 시간 기준만으로 설명하지 않는다. 계측·성능 승인 경계는 [수정·검증 보고서](ms-post-r6-implementation-report-20260923.md)에 기록한다.

2026-09-22 구현 명세 동결: [기계 판독 schema](../../../soccer/training/ms-v2-schema.json)의 선수별 27개 순서로 244개 관측을 구현한다. 기존 writer/씬은 명시적인 v1 경로로 보존하고 `UseRuntimeV2` 씬에서만 새 Planner/수명/관측을 사용한다. 물리 프로필과 보상 수치는 유지한다. 전진 운반의 목표 깊이는 기존 10m, 균형 운반은 기존 최소 전진 6m를 재사용하며 보호는 중앙 쪽 3m 횡이동으로 시작한다. 요청 이동 속도는 전진 1.0/균형 0.6/보호 0.3의 최대속도 비율로 구분한다. 이는 새 물리 최대속도가 아니라 명령별 목표 이동 속도이며, 보호가 전진 강제 보정을 통과하지 않도록 한다. 별도 전술 품질 평가는 아직 아니다. 검증 결과는 현재 상태에 기록한다.

## 1. 유지할 기술과 학습할 판단

유지: 선수 이동·회전·공 접근·물리 킥·공 수신 실행·충돌 회피·골키퍼 세이브 기술. 학습: 6개 팀 명령의 선택 시점, 공격 참여 인원, 압박 강도, 후방 인원, 전진/패스/슛/안전 보유 의도. 패스 수신 후보와 슛 조준점은 이번 버전에서는 코드가 계산한다. 따라서 패스 대상과 슈팅 코스까지 신경망이 자유 선택한다고 주장하지 않는다.

동일한 명령이 모든 상태에서 무조건 달라야 한다는 요구는 하지 않는다. 선수 부족·물리 진행 중 같은 명령 결과가 우연히 같을 수 있다. 중요한 것은 **해당 전술이 유효한 동일 상황에서 명령이 전술 차이를 보존하는 것**이다. 명령 다양성 자체에 보상하지 않는다.

## 2. 4명 구조의 명령 매트릭스

팀 구성은 keeper 1 + field 3. 아래 인원은 field 3명에 대한 배정이다. carrier가 keeper인 상황은 별도 규칙을 적용한다. 중복 압박/지원 인원 계산 금지. 부족한 활성 선수는 우선순위대로 배치하고 실제 인원을 trace한다.

| 명령 | 필드 선수가 공 소유 | 비소유 시 | 위험/의도 |
|---|---|---|---|
| 전진 운반 AdvanceCarry | 운반 1, 전방 침투 1, 후방 연결 1 | 소유 없으면 mask | 직선/빈 공간 전진, 침투 지원 |
| 패스 전개 PassBuild | 패서 1, 지정 수신 1, 후방 연결 1 | 소유 없으면 mask | 코드가 계산한 동료에게 1회 의도 패스 |
| 슈팅 시도 AttemptShot | 슈터 1, 리바운드 지원 1, 후방 1 | 소유 없으면 mask | 유효 범위에서 1회 의도 슛 |
| 적극 회수 ActiveRecover | 운반 1, 전방/측면 공격 지원 2 | 압박 2, 후방 차단 1 | 높은 공격 참여와 재회수, 후방 위험 허용 |
| 균형 유지 Balanced | 운반 1, 측면 연결 1, 후방 1 | 압박 1, 통로 차단/커버 2 | 중간 전진 속도·깊이, 즉시 침투보다 연결 |
| 후방 보호 ProtectBack | 안전 보유/짧은 공간 이동 1, 후방 2 | 추격 압박 0, 후방 지역 방어 3 | 전진·강제 슛 금지, 접근 상대와의 물리 경합은 유지 |

운반의 기술 안정화는 공통으로 유지한다. `EnforceAttackingCarryTarget`로 ProtectBack까지 항상 전진시키지 않는다. 보호/균형/적극회수의 목표 깊이·속도·지원 위치 차이는 fixture에서 검증해 버전화한다. 안전 보유는 공을 순간 고정하는 기능이 아니며 정지로 소유를 잃는 기존 물리에서는 짧은 안전 운반으로 구현한다.

- 압박자 선정은 command 인원 요청을 입력으로 받아야 한다. `RequiresDoublePressure`가 이를 무조건 2명으로 덮어쓰는 경로 제거.
- 공격 지원도 command에 따른 역할 배정 뒤 적용. ProtectBack의 후방 선수를 자동 리바운드 지원자로 올리지 않음.
- 자기 골문 위험은 위치 계산에 사용할 수 있지만 선택된 전술의 인원 의미를 조용히 교체하지 않는다.
- 현재 snapshot에서 패스/슛 불가능하면 mask로 표현. 명령 수신 사이 상태 변화로 실행 불가가 되면 `StateChanged`를 기록하고 해당 임무만 중립 중단. 다른 좋은 전략으로 대체하지 않음.
- 마스크는 물리적 실행 가능성을 제한한다. '막혔으니 패스만 허용'처럼 정답을 강제하는 전략 마스크는 사용하지 않음.
- R0와 PPO는 같은 Planner/Executor를 사용한다. 규칙형도 동일한 선택 비용을 부담한다.

## 3. 골키퍼·정체·경계 예외

- keeper가 공을 소유하면 PassBuild는 안전 배급, AttemptShot은 긴 클리어. Advance/Recover/Balanced/Protect는 각 전술의 지원 배치와 짧은 안전 보유 이동. keeper의 뒤 공간/활동 범위 등 물리 제한은 유지.
- keeper 무소유 시 Home/Claim/Block 기술은 유지하며 `KeeperTechnique`로 출처 구분. 자동 공격 패스/슛과 수비 반사 동작을 혼동하지 않음.
- 정체 탐지는 유지하되 자동 `AimShot` 생성 제거. 최소 충돌 해소/재접근만 허용하고 감독의 패스·슛 의도는 변경하지 않음.
- 벽 끼임 해소 킥이 꼭 필요하면 `SafetyEscape` 출처와 고정 제한 속도/방향/시간을 설정. 기본적으로 안전 움직임으로 해결하고, 긴 공격 클리어를 안전 기능으로 숨기지 않음.
- 정체로 무제한 대기하지 않는다. scene geometry/접근 로직을 먼저 수정하고, 물리 불능 episode 종료는 실패율로 보고한다. 위험 상황을 유리하게 초기화하는 reset 보상 악용 방지. 무승부 승격 지표에서 실패 episode를 임의 제외하지 않음.
- 사건의 실제 골/실점은 출처와 무관하게 점수에 반영한다. 보조 전술 사건은 별도로 분리해 PPO 전략 성공으로 보고하지 않음.

## 4. 임무 수명과 명령 수락

0.5초 감독 판단 주기는 유지. 선수 전체를 몇 초 동안 잠그는 방식은 사용하지 않는다. task 상태는 `Idle, Preparing, Committed, Recovering, Receiving` 5개로 정의한다.

| 현재 실행 상태 | 새 명령 처리 | 기간/종료 기준 |
|---|---|---|
| 이동·운반·압박·지원·커버 | 최신 유효 임무로 즉시 대체 | 다음 물리 tick 반영 |
| 패스/슛 접근·조준 Preparing | 안전하게 취소/대체 가능 | 2초 전체를 잠금으로 사용하지 않음 |
| 플레이트 발사 Committed | 발사 중인 짧은 물리 구간만 보호 | 기존 실제 플레이트 전진 약 0.08초, watchdog 최대 0.15초를 시작값으로 검증 |
| 발사 후 Recovering | 이동 임무 변경 허용, 재킥만 cooldown 제한 | 기존 물리 수축 약 0.50초 유지 |
| 수신 이동 Receiving | 새 전술로 취소 가능 | 자기 수신 확인·상대 탈취·timeout 즉시 종료 |
| 득점/종료/비활성/Human 전환 | 즉시 취소, 물리 토큰 안전 정리 | 팀 전체 안전 중단 |

같은 킥 임무를 매 0.5초 재요청하더라도 목표/수신자/의도가 동일하면 준비를 재시작하지 않는다. `RetainedEquivalent`로 기록하여 명령 반복 때문에 영원히 조준하지 못하는 문제 방지. 반복 command가 새로운 킥을 발생시키려면 이전 kick token 소비/종료가 확인되어야 한다.

Committed 중에는 선수별 최신 요청 하나만 `Deferred`로 보관한다. 물리 구간 종료 시 현재 상태에서 재검증 후 적용 또는 만료 기록. 모든 거절을 조용히 버리지 않는다. queue는 길이 1, 새 요청이 이전 대기를 대체하며 task expiry를 무한 연장하지 않음. 큐의 내용은 최근 수락 명령에서 재계산하고 별도의 보이지 않는 오래된 목표를 유지하지 않음.

반환 계약: `Accepted, RetainedEquivalent, DeferredCommit, RejectedStale, RejectedOwner, RejectedInvalidState, CancelledPossessionLost, CancelledMatchState, Expired`와 적용 tick. `SetTask` bool을 의미 있는 결과 구조로 바꾸고 MatchController가 전부 집계한다. watchdog 초과는 오류로 처리하고 자동 슛으로 해결하지 않음.

## 5. 추적 계약

- 식별자: run/build/schema/worker/episode/tick/team, `commandId`, `taskId`, `parentCommandId`, `kickId`, `eventId`.
- 단계: 관측 tick → raw command/mask → 수락 command → proposed task → accepted/deferred/rejected task → 실행 phase/물리 kick → 접촉/수신/골/종료 사건.
- 출처: `PolicyCommand, RuleCommand, KeeperTechnique, SafetyEscape`. 기존 raw/effective 동일성 0위반 조건 유지, 출처 없는 숨은 전략 동작 금지.
- 지연 수치: 판단→임무 적용, 적용→실제 킥, phase 지속시간, 거절/취소/재시도 이유. 단순 거절 수 0을 목표로 하지 말고 설명되지 않는 거절 0을 요구.
- 실물 사건은 발생시점에 저장. observation에 미래 event나 상대 신경망의 내부 명령을 유출하지 않음.
- full trace는 bounded ring buffer와 진단 경기에서 저장하고 학습에서는 aggregate + 오류 구간만 보존. 32 worker 로그 I/O 때문에 학습 속도가 무너지는 것을 방지.

## 6. 관측 v2 시작 설계: 244 float

기존 133개 순서를 유지하고 아래 실행 관측을 append한다. BehaviorName은 `MNG_ManagerV2`, schema는 `MNG-OBS-v2-244`. 구 ONNX를 강제로 연결하지 않는다.

| 추가 항목 | 개수 |
|---|---:|
| 아군 각 선수 실제 skill one-hot(None 포함 기존 13개) | 13×4 |
| 실제 phase one-hot(5개) | 5×4 |
| task 잔여시간 / 3초, commitment 잔여시간 / 0.15초, 각 0~1 clamp | 2×4 |
| 실제 확정 수신 슬롯 one-hot(None+4), 이전 계획 후보와 구별 | 5×4 |
| 현재 실행 임무 target 좌표, 팀 기준 정규화 | 2×4 |
| 전역 정체 활성, 정체 시간 / 5초 clamp, GoalPause 활성 | 3 |
| 총합 | 133 + 108 + 3 = 244 |

inactive의 추가 block은 0, active Idle은 None skill/Idle phase/수신 None과 시간0을 사용. target은 현재 실행 target만 포함하며 새 거절 임무 target을 노출하지 않음. 180도 팀좌표 변환 유지. 오래된 command age와 실제 임무 시간이 다른 의미임을 명세에 표시한다.

위 시간 분모와 차원은 명시적 계획값이다. 구현 검증에서 더 긴 유효 상태가 필요하면 silent clamp로 숨기지 말고 schema 변경을 기록하고 첫 정식 학습 전에 동결한다. 현재 snapshot에 있는 정보를 늘리는 작업이지 RNN/대형 network 도입은 아니다.

## 7. 실제 사건 지표

| 지표 | 정의/기록 |
|---|---|
| raw shot strike | 실제 Shot intent 플레이트 접촉, kickId 중복 제거 |
| goal-directed strike | 실제 킥의 초기 목표선이 골문 범위, 기존 ValidShot와 대응하는 명칭 |
| observed on-target/blocked/save | 실제 후속 궤적·골라인/차단 사건으로 별도 측정, 측정 전에는 0이 아닌 unavailable |
| raw pass strike | 실제 Pass intent 접촉, 예정 수신자·명령 출처 저장 |
| teammate reception | 2.5m 이동 뒤 다른 동료가 0.15초 안정 소유 |
| intended reception | 위 수신자가 kick 시작 시 저장한 예정 수신자와 일치 |
| raw advance | 동일 소유 cycle의 새 5m 경계 통과, 후퇴 왕복 재집계 금지 |
| 회수 | `observedRecoveries`: 기존 M2 기준 상대 안정 소유 0.30초 → 자기 안정 소유 0.20초. 일반 MS는 회수 reward 호출이 없으므로 ledger 지급 횟수와 구분. 골 정지/새 episode에서 자격 초기화, 동일 자기 소유 중복 0 |
| 실점/승패 | 실제 ledger 사건, 보상 cap과 독립 |
| rewarded event / capped / amount | raw 사건과 별도로 reward 반환값 기록 |

보상 숫자는 첫 P1 비교에서 유지. 기존 reward의 실제 동료 수신 정의도 첫 실행에서 임의로 더 엄격하게 바꾸지 않고 intended 지표를 추가한다. 보상 정의 변경이 필요하면 별도 실험과 기존/변경/차이 표로 처리한다. 이미 지급 중인 자동 사건 shaping은 우선 출처를 기록하고, P1에서 자동 전술을 제거해 기여를 분리한다.

## 8. 필수 검증 fixture

1. 동일 상대 소유 snapshot에서 Recover 2명/Balance 1명/Protect 0명 추격 압박 및 field 합계 3명.
2. 동일 자기 소유 snapshot에서 Advance/Pass/Shot의 carrier skill, 수신·리바운드·후방 역할 차이.
3. 골문 위험/공격 골문 근처에서도 자동 보조가 command 인원을 덮어쓰지 않음.
4. Preparing 중 새 수비 command 즉시 대체, Committed 동안 1회만 킥, 이후 deferred command 적용.
5. 수신 뒤 영구 ReceivePass 잔류 없음, keeper 자동 공격 배급 없음, 정체 자동 슛 없음.
6. reward cap을 0으로 만든 검사에서도 raw 사건 수 동일. intended 실패와 teammate 성공 구별.
7. 양 진영 mirror, observation count 244/finite, phase/target 실제 상태 일치.
8. 짧은 동적 rollout에서 단순 task 표뿐 아니라 실제 이동·배치 차이 확인, 물리 안정 회귀 검사.
9. raw/effective 불일치 0, 중복 token 0, 사건 출처/parent id 누락 0. schema 불일치 모델은 부팅 실패.
10. 위 검증 후에만 32환경 훈련 빌드와 정식 학습 허용. 이번 계획 단계에서는 미실행.

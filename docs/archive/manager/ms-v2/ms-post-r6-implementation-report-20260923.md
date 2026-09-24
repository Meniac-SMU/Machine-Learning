> 보관일: 2026-09-24. 작성 당시의 계획·판정·승인 이력이며 현재 실행 지시가 아닙니다. 현행 기준: [현재 상태](../../../soccer/current-status.md).

# Post-R6 수정 및 학습 전 검증 보고서 — 2026-09-23

대상 계획: [후속 진단·수정 계획](ms-post-r6-diagnosis-plan-20260923.md). 사용자는 D1~D4 수정, 실패 원인 수정과 반복 검사, 규칙형 공통 오류 반영을 승인했다. 학습·optimizer update·모델 승격은 이 작업의 범위가 아니다. 최신 완료 판정은 [현재 상태](../../../soccer/current-status.md)에 기록한다.

**최종 판단:** 재현된 좌표·소유 우선권·패스 기술·벽 회피·계측 오류를 수정하고 자동 회귀를 통과했다. D4의 동결 진단도 수행했으나 **동적 진영 공정성 승인과 대규모 학습 준비 진입은 보류**한다. 최종40경기의 진영 격차가 남고, 기존400k 정책은 패스를 거의 선택하지 않으며 Recover 단순 상대 및200k 정책에 대한 우세도 입증하지 못했다. 모든 문제를 완전히 해결했다고 선언하거나 검사 기준을 낮추지 않는다. 실제 학습은 시작하지 않았다.

## 수정 내용과 적용 범위

| 영역 | 확인한 문제 | 수정 | 적용 대상 |
|---|---|---|---|
| 팀 상대 좌표 | MidLeft/MidRight lane, 중앙 지원·슛, 겹침 분리 fallback 일부가 world y에 고정 | 팀 부호와 상대 좌표로 반전 | MNG v2 PPO·규칙형, 공통 legacy Planner |
| 옛 규칙형 선수 | 중앙 lane fallback과 지원 깊이 분기가 world 좌우를 사용 | 팀 상대 lane/깊이, 몸 기준 장애물 회피 분기 수정 | RuleBasedSoccerController, Core autonomous fallback |
| 중립 소유 동률 | Red 먼저 순회한 후보가 동률 우선 | episode seed에 따른 첫 팀, 무소유 동률 획득마다 교대; 골 이후 우선권 보존 | 공통 MNG PossessionLedger |
| 근사 동률 | 두 후보씩 epsilon 비교하면 세 후보 순열에 따라 결과 변화 가능 | 전체 최단거리 확정 후 그 거리의 tie band에서 우선권 적용 | 공통 MNG PossessionLedger |
| 경합 탈출 | 골마다 최초 무소유 탈출팀이 Red로 복원 | episode 초기 우선권과 라운드 상태 분리; 실제 무소유 grant만 교대·집계 | 공통 MNG BallControl |
| 정체 통계 | 골/reset에서 이전 구간 activation 소실 | legacy 필드 의미 보존 + episode 누적 횟수/시간 추가 | Match, Trace, 동결 평가 |
| 드리블 시간 기준 | 흔들림 위상이 Unity 전체 실행 시간에 의존 | 경기 elapsed time 사용; spawn 로그에 경기/전역 시간 병기 | 공통 MNG BallControl, PPO·규칙형 |
| 패스 후보 | 점수 계산과 실제 킥이 서로 다른 목표점을 사용 | 실제 SelectPassTarget으로 후보 거리·통로 평가 | PPO·규칙형 공통 resolver |
| 패스 수신 | 이동 목표를 향해 등을 보임, 운반용 orbit 개입, 수신 성공을 소유 상실로 취소 | 공을 보는 전면판 수신, 고정 접근 방향, 수신 중 짧은 저속 제어 | PPO·규칙형 공통 executor |
| 패스 준비 | 125도/초 회전으로 돌아서기 전에 공 도착 | 기존 회전 속도/킥 속도로 예상 회전 시간과 비행 시간을 비교해 준비 | 공통 executor; 사람 수신 선수는 자동 준비 조건에서 제외 |
| 킥 전 회전 | 실제 소유자가 새 조준 접근점으로 움직이거나 공보다 빨리 돌면서 소유 상실 | 공 방향과 capture radius로 회전 선행량 제한, 전진 제어를 유지하며 전면판 위치 오차 보정 | 공통 AimAndKick: 패스·슛 및 규칙형 공유 |
| 늦은 패스 수신 | 준비에 시간이 걸리면 공 도착 전에 수신 임무 만료 | 실제 strike에서 같은 부모 명령의 살아 있는 ReceivePass에 기존 2초 수신 예산 부여 | v2 PPO·규칙형; 다른 명령 교체/상대 소유/경기 정지는 즉시 취소 |
| 벽 밀집 | 동료에게서 멀어지는 방향이 벽 밖이면 실제 속도 0으로 밀집 지속 | 벽 밖 회피를 벽의 접선 방향으로 변경 | 공통 executor; 별도 양 진영 물리 fixture |

명령 6개, 관측 244개, 물리 프로필, 보상 수치, Recover 인원 배정은 유지한다. Pass 선택 횟수 강제, 전략 mask, 정책 출력 override, 다양성 보상, 패스 전용 학습 구조를 추가하지 않았다. 킥 준비는 기존 2초 만료를 따른다. 실제 출발한 패스의 수신 임무에만 출발 시점부터 기존 2초 예산을 주며, 새 감독 명령은 언제든 교체할 수 있다. 실제 전면판의 짧은 물리 commitment 외에 새 전략 잠금을 만들지 않았다.

MNG 규칙형 감독은 PPO 감독과 같은 Planner/Executor/BallControl을 사용하므로 공통 수정이 자동 적용된다. 별도 Rule 팀 선수의 FSM과 Core fallback에서는 같은 좌표 오류만 수정했다. 그 경로에 존재하지 않는 MNG 소유 ledger나 수신 임무를 복제하지 않았다. Rule의 명령 선택 우선순위나 보상은 변경하지 않았다.

## 버전과 원본 보존

- 관측 `MNG-OBS-v2-244`, Behavior `MNG_ManagerV2`, task/event version 2 유지.
- 환경 `MNG-P1-postR6-20260923`, runtime protocol 3, 동결 평가 `MNG-V2-FROZEN-300-v2`.
- legacy Planner 9→10, v2 Planner 10→11.
- 원본 dirty/untracked source: `Logs/MNG-Rebuild/PostR6-start-20260923/source-sha256.json`과 `source/`.
- 기존 R5/R6 Run 및 구 평가 DLL 121개 입력 파일: `Logs/MNG-Rebuild/PostR6-validation-20260923/frozen-inputs-before.json`.
- 새 평가 빌드는 기존 빌드 경로와 분리한다. 기존400k 정책은 변경된 환경의 동결 진단 기준이며, 새 환경에서 자동 승격하거나 기존 Run에 resume하지 않는다.

## 실패를 통한 수정 기록

1. 최초 MNG EditMode 165개 중 1개 실패: 중앙 공격 지원 위치의 좌우 반전 누락. 수정 후 전체 Soccer 303/303 통과.
2. 최초 16개 패스 물리 상황: 킥은 발생했으나 안전 상황의 실제 수신 실패. 타임라인에서 수신 방향·회전 지연·성공 직후 잘못된 취소를 확인하고 수정했다. 카운터는 run 누적이므로 fixture 전후 차이로 검증한다.
3. legacy Planner까지 대칭 검사 확장: 344개 중 23개 실패. 로컬 우회 후보의 lane bias가 world 좌표에 고정되어 있었다. 수정 후 남은 1개는 수신을 운반용 front-push에 넣는 옛 계약 assertion이었으며 새 수신 물리 계약에 맞춰 변경했다. 이후 345/345 통과.
4. 전체 MNG·공통 환경 PlayMode 36/36 통과. 추가 규칙형 3경기 회귀에서 밀집1.26%가 기존 `<1%` 기준을 넘었다. 문턱을 완화하지 않고 위치·실제/요청 속도 로그를 추가해 벽 바깥으로 계속 회피하는 상황을 확인했다. 벽 접선 회피 수정 후 재검증한다.
5. 평가 빌더에 공통 Builder를 직접 호출한 첫 시도는 asmdef 참조 오류였다. 불필요한 assembly 의존을 추가하지 않고 공통 Builder 검증을 별도 batch 작업으로 분리했다. 오류 로그도 보존한다.
6. r1 동결 무작위 정책 8경기의 Pass 69건 중 54건이 Playing 중 소유 상실로 끊겼다. 압박과 기술 오류를 분리하려고 상대 없는 90도 회전 fixture를 추가해 양 진영 모두 실패를 재현했다. 저속 전진만 적용하면 plate가 공보다 먼저 회전해 제어 반경을 벗어났다. 회전 선행량 제한만으로는 준비가 늦어졌고, 위치 보정만 추가하면 전진 제어 조건을 벗어났다. 실제 공 방향을 따르는 회전과 전진 정렬을 보장하는 위치 보정을 함께 적용했다. 킥 성공 이후 수신 임무가 먼저 만료되는 후속 실패까지 확인하고 실제 strike에 연결된 수신 예산으로 수정했다. `pass-turn-r1`~`r7` 로그를 보존한다.
7. lifecycle에서 GoalPause/Finished 취소 일부가 소유 상실로 표시됨을 발견했다. `CancelledMatchState`로 분류를 수정하고 전용 회귀를 추가했다. 종료 결과가 기록된 뒤 Player 종료 과정에서 추가로 들어온 Finished 명령은 경기 중 행동 통계에서 제외한다.
8. r3 미러40경기의 Red/Navy 점수율은30%/67.5%, seed cluster 차이의95% 구간은[-55,-17.5]%p였다. 이를 대칭 합격으로 처리하지 않았다. 초기 좌표/관측은 일치했고 첫 `>0.001` 관측 차이는 대부분 결정6, 약3초의 선수·공 접촉 속도에서 시작했으며 그 이전 정책 명령은 같았다. 공 제어가 `Time.fixedTime`을 사용해 시작 이전 시간에 의존함을 추가 발견하여 경기 elapsed time으로 바꿨다. 이 수정은 시간 기준의 재현성 문제를 제거하지만 r3 진영 격차의 인과 원인이라고 단정하지 않는다. r3 결과와 최초 차이·실점 소유 전환 분석을 보존하며 최종 runtime을 별도로 재평가한다.

## 집계 의미

- `stallActivations`: 과거와 동일하게 마지막 round 구간만 의미한다. 새 비교는 `episodeStallActivations`와 `episodeStallActiveSeconds`를 사용한다.
- 정책 확률/행동 가능 횟수: own/opponent/neutral 소유 상태로 분리하고 goal pause를 제외한다. Pass가 열려 있다는 사실을 유리한 패스 기회나 패스 성공으로 부르지 않는다.
- 선수 실행 인원/후방 인원: 다음 결정 직전, 직전 명령 아래에서 관측한 값이다. 명령의 인과 효과를 단독으로 증명하지 않는다.
- 원시 `rearPlayerCounts`는 공보다 뒤에 있는 필드 선수 전체이며 carrier도 포함할 수 있다. 읽기 전용 후처리의 `meanNonCarrierFieldPlayersBehindBall`은 carrier를 제외한다. 둘 다 기하학적 위치이고 전술상 커버 역할 수와 같지 않다. 최종80경기에서 두 집계의 표본 분모 일치를 확인했다.
- task/source/strike/reception/delay: run 누적과 episode 누적의 범위를 명시한다. kick delay는 수락 task ID와 실제 strike가 연결되는 경우만 집계한다.
- 일반 trace ring 512건의 샘플과 별도로 진단 시 전체 lifecycle stream 및 정책 결정 확률 로그를 선택적으로 저장한다.
- mirror 평가는 좌표·heading·팀·스폰 offset·중립 초기 우선권을 반전하고 후보/상대 추론 RNG도 교차한다. 골 시점이 달라진 이후 두 궤적을 완전히 동일한 경기라고 가정하지 않는다.

## 보상과 사용량 규칙

| 항목 | 이전 | 이후 | 변경량 |
|---|---:|---:|---:|
| 실제 완료 패스 Base 보상 | +0.06 | +0.06 | 0 |
| Pass 명령 선택 보상 | 0 | 0 | 0 |
| 일반 MS 회수 보상 지급 | 0 | 0 | 0 |
| 명령 다양성 보상 | 0 | 0 | 0 |

5시간·주간 사용량 비율에 따른 사전 중단, 진입, 재개 조건을 프로젝트 지침과 관련 계획/인계 문서에서 폐지했다. 과거 실제 중단 시점과 측정 수치는 이력으로 보존한다. RAM·NaN·crash·데이터 무결성 검사와 정상 저장은 유지한다. 서비스 자체 한도 변경이나 reset credit 사용을 뜻하지 않는다.

## 최종 검증과 평가

검증 디렉터리: `Logs/MNG-Rebuild/PostR6-validation-20260923/`.

| 검증 층위 | 최종 결과 | 증거 |
|---|---|---|
| 전체 Soccer EditMode | 최종 clock 수정 후 346/346 통과 | `edit-clock.xml` (앞선 `edit-release.xml`도 346/346) |
| 전체 MNG + 공통 SoccerEnvironment PlayMode | 패스·벽·취소 수정 후 38/38 통과 | `play-release.xml` |
| 마지막 clock 변경 영향 회귀 | 공통 환경6개, PostR6 3개, 패스·슛·드리블·탈취·R0 경기 포함 14/14 통과 | `play-clock.xml`; 앞선 38개 전체를 마지막 clock 변경 후 모두 다시 실행한 것은 아님 |
| 양 진영 패스 물리 상황 | 11상황×2팀=22개; 안전 14개 모두 실제 strike+의도 수신, 위험/취소 8개 기대대로 | 위 PlayMode 내 PostR6 fixture 로그 |
| 벽 밀집 물리 fixture | 양 진영 모두 2초 내 분리 기준 통과 | 위 PlayMode 내 PostR6 wall test |
| 최종 규칙형 3×300초 경기 | 8:3, pass strike24/완료9, shot49/유효42, 밀집233/90006=0.2589% (`<1%` 유지) | `play-clock.log`의 R0 RULE 3X300S RESULT; 이전 r3 회귀는6:3, pass32/15, 밀집0.1644% |
| 기존 기술 회귀 | 빈 골문 슛·5/10/20m 패스·전진/정지/역행 드리블·탈취 및 기존 커리큘럼 100상황씩 | 위 38개 검사에 포함; 정책 학습 아님 |
| 공통 Builder | Base/Attack/Defense/Press/Rule stadium parity, L0/L1/L2/L2-Find/L2-Score/L3 contract PASS | `builder-validate.log` |
| Python 동결 평가기 | 5/5 통과 | `python -m unittest discover -s Tools -p test_mng_v2_evaluate.py -v` |
| 최종 Windows 동결 평가 빌드 | 성공, 별도 r4 디렉터리 | `build-evaluation-r4.log`, `Builds/MNG_V2/PostR6-20260923-r4/EvaluationV2/build-info.json` |

최종 runtime DLL SHA-256: `3ab38c2c3ca952246e4dce7888a3fc657ecfcc770439a652fd0bd6c2dc2a4df6`.
EXE SHA: `d3db57b9b88a2d8fedd1621461ddd7b29ed18001efd29a93ef9f31d4e8a483cd`.
씬 level SHA: `c5cac17c4e7c79bc3925db043ad8e757d5ebe5580e760b7813f1e1dc783375ba`.
빌드 manifest의 `candidateStatus=smoke-only-untrained`는 모델을 내장하지 않은 공통 평가 Player의 빌더 기본 표기다. 실제 평가 정책은 각 report의 candidate/opponent ONNX SHA와 Python 동결 추론 경로로 식별한다.

Unity가 자동으로 추가한 Standalone `SENTIS_ANALYTICS_ENABLED`만 확인 후 작업 시작 당시 설정으로 복원했다. 빌드 당시 설정은 `ProjectSettings-build-r4.asset`에 보존한다. 시작 당시 원본은 `APP_UI_EDITOR_ONLY`이며 과거 보고서의 다른 시점 define 값으로 덮어쓰지 않았다. `EditorBuildSettings.asset`도 원본과 동일하다.

## 최종 r4 동결 평가 80경기

경기당300초, 병렬8 Player, ONNX 추론만 수행했다. 후보는 r005의402,389 정책(SHA `653d1f98126bd111ce4c15c26c0382d2170a2be312c8631aded44cff38d1614f`)이다. 점수율은 `(승+0.5×무)/경기수`다. 맨 마지막 무작위 행만 후보가 uniform-valid다.

| 후보 대 상대 | 경기 | 승/무/패 | 점수율 | 득실 | Red/Navy 점수율 | 결과 디렉터리 접미사 |
|---|---:|---:|---:|---:|---:|---|
| 400k 대 동일400k, mirror·RNG 교차 | 40 | 15/7/18 | 46.25% | 100:100 | 32.5%/60% | `mirror40` |
| 400k 대 규칙형 Full | 8 | 8/0/0 | 100% | 39:5 | 100%/100% | `full` |
| 400k 대 Recover 전용 | 8 | 3/2/3 | 50% | 13:12 | 62.5%/37.5% | `recover` |
| 400k 대 같은 Run의202,112 | 8 | 2/2/4 | 37.5% | 12:18 | 0%/75% | `history200` |
| 400k 대 MS2 준비 완료200,120 | 8 | 3/3/2 | 56.25% | 15:21 | 37.5%/75% | `initial` |
| 무작위 유효 명령 대 규칙형 Full | 8 | 0/0/8 | 0% | 3:47 | 0%/0% | `uniform-full` |

각 결과는 `Logs/MNG-Rebuild/PostR6-r4-{접미사}-20260923/report.json` 및 경기별 `verified.json`, `decisions.jsonl`, `spawns.jsonl`, `v2-*.jsonl`에 있다. 무작위 비교는 전체 `lifecycle-*.jsonl`도 보존했다. 종합 후처리는 `r4-final-diagnostics.json`이다. 최종80경기 모두 raw/effective 불일치·override·직접 명령 선택 보상0, 300초 종료, runtime SHA 일치 및 계측 무결성 검사를 통과했다.

동일 정책40경기는10개 독립 spawn seed(592311~592320)를 팀 교대와 추론 RNG 교차로4회씩 사용했다. 나머지는 각4 seed×2진영이다. Full592401, Recover592411, uniform592421, history200592431, initial592441부터 시작한다. 작은8경기 비교는 순위 확정이나 승격 근거가 아니다. 전승 표본의 bootstrap 구간이[1,1]이더라도 모집단 승률100%를 뜻하지 않는다.

개발 중 r1 72경기, r3 56경기, 최종 r4 80경기로 총208개의 **학습 없는** 경기 결과를 보존했다(`evaluation-inventory.json`). 런타임이 다른208경기를 한 승률로 합산하지 않는다. r4의 처음4대진은 원인 수정 비교를 위해 앞선 평가 seed를 재사용했으므로 신규 독립 holdout이라고 부르지 않는다.

### 핵심 1: 패스 미사용

- 최종 동정책40경기에서 소유 중 Pass 합법4072회, 허용된 상태의 평균 선택 확률은 `0.00003595256`, 즉 **0.003595%**다. 실제 Pass 선택·strike·수신은0이었다. 정책의 학습된 선택 확률이 첫 병목이며, 실행 코드만 고쳤다고 정책의 선택이 바뀌지는 않는다.
- 무작위8경기는 **71명령→3 strike→0 의도 수신**이었다. 실제 소유 상실 취소21, 다른 명령으로 전환33, 반복 Pass14, 실제 strike3으로 연결 사유를 나눴다. 반복 Pass를 자동 실패로 세지 않는다. strike3건 뒤 다음 명령이0.24~0.46초 내 Recover/Protect로 바뀌었다. 이것만으로 각 수신 실패의 단일 원인을 확정하지는 않는다.
- 같은 무작위 대진의 규칙형 상대는 **53 strike→17 의도 수신**을 기록했다. 400k와 대전한 규칙형 상대도31→5였다. 안전 물리 fixture의14/14 실제 수신과 함께 공통 패스 기술이 작동함을 확인했다. 규칙형도 모든 패스에 성공한다는 뜻은 아니다.
- `r4-pass-transition-audit.json`과 `Tools/inspect_mng_pass_transitions.py`로 명령/strike/취소 연결을 재현한다. 앞선 r1의54건 소유 상실/1 strike도 동일 도구로 재현했다. 독립 분모가 다른 명령 수와 킥 수를 단순한 기술 성공률로 해석하지 않는다.
- **추가 강제 정책 없음:** 패스 보상+0.06 유지, 선택 보상0, 사용 횟수·사용률 강제0, 출력 override0. 수정된 기술을 실제로 배우는지는 향후 한정 학습의 과제다.

### 핵심 2: Recover 집중과 정체

- 최종 동정책40경기에서 소유 시 Recover 선택3005/4377=**68.65%**, 비소유19663결정 중18638=**94.79%**다. 비소유 선택 집계에는 기존 conditionalCommands의 pause 표본이 포함될 수 있으므로, 전술 분석에는 별도로 pause를 제외한 확률/실행 표본을 사용한다.
- 공보다 뒤에 있는 **carrier 제외 필드 선수**는 직전 Recover 아래 소유 표본3419개의 평균1.532명, 비소유 표본17087개의 평균1.824명이다. 원시 carrier 포함 소유 평균2.487명과 구분한다. 이는 역할 배정 또는 전략 효과의 인과 증거가 아니다.
- 소유→비소유 이후 실점의 읽기 전용 분석은100실점 중99개를0.5초 관측에서 식별했고64개에 앞선 소유 이탈을 연결했다. 그중 Recover 이후32개, 관측 이탈→실점 중앙값12.75초다. 슛처럼 의도적으로 공을 놓는 경우도 이탈에 포함되고, 선택 상태가 다르므로 명령별 수치를 인과 성공률로 비교하지 않는다(`r4-possession-loss-goals.json`).
- 경기 전체 정체는571회,1804.885초: **경기당14.275회·45.122초**다. 옛 마지막 round 카운터131회와 같은 분모가 아니다. 전체 실행 source tick 중 candidate 안전 회피는3.288%다. 정체 시간과 안전 회피 시간은 다른 정의이며 더하지 않는다.
- Recover는 소유 시 공격 가담1+지원2, 비소유 시 압박2+커버1인 의도된 명령이다. 높은 사용률을 오류로 단정해 배정·보상·명령 의미를 바꾸지 않았다. 다만 Recover 전용 상대50%,200k 상대37.5%라는 작은 비교에서 기존400k의 충분한 우세는 보이지 않는다. 규칙형 전승만으로 장기 학습을 승인하지 않는다.

### 핵심 3: 진영 차이

- D1의 결정론 로직·mask·목표 미러 검사는 통과했고, 최종 동적20개 미러 쌍의 공통 kickoff 위치 최대 오차는 `9.54e-7m`, 초기244관측 최대차이는 `2.38e-7`이었다. 중립 탈출 grant는Red627/Navy622로 전체 합계는 다르지만 각 경기의 교대 수 차이는1이하였다. episode 수와 첫 팀 parity가 다른 총합을 동률 조건으로 요구하지 않는다.
- 그러나 Red-Navy 점수율 차이는 **-27.5%p**, seed cluster bootstrap95% 구간은 **[-42.5,-7.5]%p**였다. 이는 최종 표본의 진영 격차 경고다. 적은10 seed와 개발 중 반복 분석이라는 한계가 있으며, 우연 가능성을 없애거나 과거 Red 우세의 원인으로 연결하지 않는다. 동시에 ‘색상만 다르니 모두 난수’라고 정리하지도 않는다.
- 두 역할을 모두 추적한40개 궤적 비교에서 최초 `>0.001` 관측 차이는 결정6~7(약3~3.5초)이었고, **그 이전 명령은40/40 같았다**. 첫 차이는 선수/공 접촉 속도에서 시작했다. 모든 최초 spawn의 경기 시간·전역 시간이0인 것도 확인했다. 전역 clock 의존 제거가 이번40경기의 잔여 차이를 설명한다는 증거는 없다(`r4-mirror-first-divergence.json`).
- 하위 자산 추가 검사: 각 슬롯의 물리 노드4개/컴포넌트5개와 조상 로컬 pose를 비교해 물리 설정 차이0. plate layer 번호는 선수별로 다르지만32개 collision matrix row가 모두 동일하고, Soccer 런타임에 IgnoreLayerCollision/IgnoreCollision 분기가 없었다. Red striker의 추가 HumanControlMarker는 물리 컴포넌트가 없다. 평가 씬의 직접 물리 override0. raw 차이와 검토 결과는 `physical-asset-audit.json`, `physical-asset-audit-reviewed.json`에 보존했다.
- **미해결 경계:** PhysX 접촉 해석·native actor 생성/처리 순서와 아주 작은 부동소수점 차이 중 어느 것이 잔여 격차를 일으키는지는 분리하지 못했다. 자산 대조나 최초 divergence만으로 특정 engine 원인을 확정하지 않는다. 승률을 맞추기 위한 팀별 능력 보정은 추가하지 않았다.

## 단계별 종료 판정

| 단계 | 판정 | 남은 경계 |
|---|---|---|
| D1 좌표·중립 우선권 | 구현 및 결정론 회귀 통과 | 전체 물리 궤적 동일성을 의미하지 않음 |
| D2 계측·평가기 | 구현 및 실제80경기 무결성 검사 통과 | 원시/후처리 분모와 기하학/전술 역할 차이 명시 |
| D3 패스 기술·공통 규칙형 | 물리 fixture·실행 회귀 통과 | 학습 정책의 실제 패스 선택/전술 유지 품질은 별도 |
| D4 동적 진단 | 진단 수행 완료, **동적 공정성 승인은 보류** | 최종 진영 격차와 접촉 이후 차이의 원인 미분리 |
| D5 대규모 학습 준비·학습 | **미진입** | 사용자 범위와 위 잔여 증거에 따라 Run/config/optimizer/승격 없음 |

## 최종 보존 및 실행 종료 확인

- 시작 source snapshot3715개 대비 기존45개 파일 수정, 새9개 source/문서 파일 추가, 원본 파일 누락0. 새9개는 회귀 C#3개와 meta3개, 읽기 전용 진단 Python2개, 이 보고서1개다. 사용량 규칙 제거에 따른 문서 변경도45개에 포함된다. 시작 이전의 사용자 dirty/untracked 상태를 git HEAD로 덮어쓰지 않았다.
- R5/R6 Run 및 구 평가 runtime 입력121개 SHA 변화0. Scene/Prefab/PT/ONNX/model registry/ProjectSettings 변화0. Unity가 재생성한 기존 Python bytecode2개도 시작 snapshot의 바이트로 복원했다.
- 실패한 중간 XML·로그, r1/r2/r3/r4 평가 빌드, 모든14개 동결 평가 report를 보존했다. r2는 취소 사유 계측 수정 빌드이며 별도 경기 평가를 하지 않았다. 최종 판단은 r4의80경기이고, 이전 빌드 성적을 최종으로 대체하지 않는다.
- 동결 평가 프로세스와 이 작업에서 실행한 Unity Editor batch는 종료했다. 기존 Unity CLI helper는 종료 대상에 포함하지 않았다. 새 Trainer/optimizer/학습 Run, commit/push, 모델 승격은 수행하지 않았다.
- 최종 파일별 hash와 보존 판정은 `Logs/MNG-Rebuild/PostR6-validation-20260923/final-preservation.json`에 기록한다. 실행 방법은 기존 평가기와 두 read-only 진단 도구를 재사용한다. `mng_v2_evaluate.py`는 명시적인 `--build`와 새 `--output`을 요구하며 기존 결과를 덮어쓰지 않는다.

## 다음 단계의 판단 기준

기술 회귀 통과와 기존 정책의 품질 승인은 구분한다. D1~D4는 환경 결함을 수정하고 측정 가능한 상태로 만드는 단계다. 기존400k 정책이 새 환경에서 패스를 배워 사용하거나 Recover 단순 상대보다 강해졌다는 보장은 없으며, 이 결과만으로 수백만 step 학습을 시작하지 않는다.

다음 필수 작업은 **기록된 초기 명령을 그대로 재생하는 첫 접촉 구간의 작은 실험**이다. 동일 seed/pose/action을 고정한 상태에서 원래 순서·팀을 바꾼 순서·물리 등록 순서만 바꾼 조건을 분리해 최초 차이를 기록한다. native 등록 순서 교차가 실제로 구현·계측됐는지 확인하기 전에는 ‘생성 순서 검증 완료’라고 쓰지 않는다. 이 실험을 통과하거나 잔여 차이의 허용 근거를 확보한 뒤 새 독립 seed로 확인한다. 원인이 미분리된 현재 상태에서 평가 경기 수나 학습 step만 늘리는 것은 권하지 않는다.

후속 준비 단계에서는 수정된 환경을 고정하고 제한된 단일 가설 검증의 예산·상대·평가 seed·종료 기준을 확정한다. 이전 계획의 최대200k 진단 예산을 상한 후보로 유지하되 이 작업에서 설정 파일/optimizer/새 학습 Run을 만들지 않는다. 기존 r005의 자동 resume나 새 champion 지정도 하지 않는다. 패스 명령 빈도를 강제하거나 최소 빈도로 모델을 승격하는 대신, 합법적 선택 확률→수락→실제 strike→의도한 동료 수신을 별도 분모로 본다. Recover 단순 상대와 규칙형 상대 양쪽에서 실제 득실/역습/정체 시간의 개선을 확인한다.

양 진영의 성적 차이는 seed를 묶은 신뢰구간과 직접 미러 fixture를 함께 본다. 신뢰구간에 0이 포함된다는 이유로 완전한 동등성이 증명됐다고 하지 않는다. PhysX 접촉 이후 비트 단위 궤적 대칭이나 개체 생성 순서의 인과 효과는 이 자동 검사만으로 확정하지 않는다. 사람의 Windows Player 경기 화면 검수도 별도의 후속 확인이다.

> 보관일: 2026-09-24. 작성 당시의 계획·판정·승인 이력이며 현재 실행 지시가 아닙니다. 현행 기준: [현재 상태](../../../soccer/current-status.md).

# MS3-v2 자기대전 안정화 — 진영 로직 보정(D1)·진단 계측 정비(D2)

2026-09-23. 사용자 지정 D1·D2 후속 점검. 진입 문서는 [R6 이후 진단·수정 계획](ms-post-r6-diagnosis-plan-20260923.md)이며, 최신 진행 상태는 [현재 상태](../../../soccer/current-status.md)를 따른다. 이전 D1~D4 구현·실험은 [이전 보고서](ms-post-r6-implementation-report-20260923.md)에 보존한다.

## 이번 수정

### MS3-v2 안정화 / 진영 로직 보정(D1)

기존 팀 상대 lane·중립 우선권·round reset 수정은 유지했다. 추가로 `MNG_BallControl.TrySelectBoundaryEscapePlayer`의 근접 거리 동률 처리가 순회 순서에 의존함을 재현했다. 거리가 `0.1 / 0.100009 / 0.100018m`인 후보를 놓으면 epsilon의 연쇄 비교 때문에 실제 최단거리 기준 동률 범위 밖 후보가 중간 기준이 되고, 팀을 뒤집을 때 선택 결과가 달라졌다.

테스트 fixture의 최초 실행은 공 질량을 기본1로 둔 오류로2개 모두 실패했다. 프로젝트의 실제 프로필 질량3으로 맞춘 뒤 수정 전 재현 검사에서 **Red 우선권은 실패, Navy 우선권은 통과**했다. 이를 실제 로직 오류의 재현 증거로 사용한다. 최초 fixture 설정 오류를 제품 결함으로 집계하지 않는다.

선택은 이제 모든 유효 후보의 실제 최단거리를 먼저 구하고, 그 최단거리에서 기존 epsilon 이내인 후보 중 명시된 중립 우선권과 슬롯 순서를 적용한다. 유효한 기존 소유자 우선, 비활성·사람 선수 제외, 기존 epsilon, 실제 탈출 적용 시 우선권 소비 규칙은 유지한다. 임의의 색상별 능력 보정이나 난수 소비는 추가하지 않았다.

이 경로는 MNG PPO 감독과 규칙형 감독이 공유하므로 양쪽에 적용된다. 별도 Rule 팀 FSM/Core는 이 함수를 호출하지 않아 같은 패치를 복제하지 않았다. 이전 공통 lane 수정은 그대로 두고 전체 Soccer 회귀로 확인한다. 이 수정이 과거 승률 격차의 원인 또는 해소라는 주장은 하지 않는다.

### MS3-v2 안정화 / 진단 계측 정비(D2)

- `decisionSamplesByState`: goal pause를 제외한 자기 소유/상대 소유/중립 판단 수를 명시한다. 기존 `availableByState`, `probabilitySumsByState`와 같은 순서다. 일반 평균 확률의 분모는 판단 수, 허용 시 평균은 허용 횟수와 허용 시 확률 합계를 쓴다.
- `episodeStallActivations`, `episodeStallActiveSeconds`: 입력 경기 중 해당 계측이 없는 경우 summary는 `null`이다. 미측정을0회/0초로 바꾸지 않는다. 옛 `stallActivations`의 마지막 round 의미는 유지한다.
- `passContext` v1: 최종 마스크 계산 시점의 tick, 소유·goal pause, 전방 막힘, 후보 슬롯, 실제 `SelectPassTarget` 기준 거리, 상대와 패스 선분의 최소 거리를 기록한다. 목표 없음의 거리 `-1`, 활성 상대 없음의 clearance `-1`은 각각 미적용/무한 여유 표기이며 거리0이 아니다.
- `--decision-trace --lifecycle-trace`를 함께 켜면 `pass-opportunities.jsonl`에 위 문맥과 Pass 확률·허용·실제 선택·parent command ID를 연결한다. 관측 tick, 명령, 마스크, 소유, goal pause가 맞지 않으면 연결을 성공으로 보고하지 않는다. 종료 후 추가 요청만 제외한다. 옛 Player에 문맥이 없으면 미측정으로 남긴다.
- `passOpportunityGroups`는 소유 상태·전방 막힘별 판단/허용/선택 수와 확률 합계다. 실제 거리와 통로 여유는 각 결정에 연속값으로 보존해 임의의 거리 구간이나 안전 합격선을 도입하지 않았다. 허용은 기술적 선택 가능성을 뜻하며 패스 성공 보장이 아니다.
- 계측 helper는 정책의 observation, mask, 확률, 선택 명령을 수정하지 않는다. 보상,244관측,6행동,Recover 인원,패스 명령 강제/override는 변경하지 않았다.

## 검증 증거

증거 폴더: `Logs/MNG-Rebuild/MS3-D1D2-validation-20260923`.

| 검사 | 결과 | 증명 범위 |
| --- | --- | --- |
| 수정 전 동률 재현 | 2개 중1개 실패 | 팀 회전에 따라 선택이 달라지는 코드 오류 |
| 최종 전체 Soccer EditMode | 349/349 통과 | 기존 계약·대칭·정체 reset 및 신규 동률·패스 문맥 회귀 |
| D1 수정 직후 PlayMode | 9/9 통과 | 패스22상황, 벽 분리, 취소 사유, 공통 환경6검사 |
| Python | 8/8 통과 | 분모·누락·trace·tick 연결·pairing·계측 비간섭 |
| 계측 비간섭 합성 검사 | 1,000회 선택 모두 동일 | 마스크/goal pause 변화 중 입력과 명령 불변 |
| 실제400k ONNX 오프라인 재추론 | 128/128 명령 일치, 입력 변경0 | 기록된 관측에서 계측 전후 frozen 추론 동일; 새 경기 아님 |

최종 계측까지 포함한 PlayMode도 **9/9**로 다시 통과했다(`play-final.xml`). MNG Builder Validate와 Windows Player 빌드도 성공했다(`build.log`). 두 PlayMode 실행을18개의 독립 시나리오로 합산하지 않는다.

새 Player의 **무작위 유효명령 대 규칙형 감독, 300초×2경기**는 D2 연결 smoke다. `Logs/MNG-Rebuild/MS3-D1D2-smoke-20260923/report.json` 및 각 match의 `pass-opportunities.jsonl`에 저장했다. seed592501, 양 진영, mirror를 사용했지만 한 seed뿐이므로 D4 공정성 검증이나 승률 추정으로 쓰지 않는다.

- Python 결정과 Unity lifecycle **1,202건** 연결: 자기 소유95, 상대 소유280, 중립761, goal pause66. active 분모 합은1,136이다.
- Pass 허용85, 선택14, 실제 후보 목표 기록85; 전방 막힘 상태의 허용46. 명령 불일치/override/직접 명령 보상은 모두0이다.
- 무작위 후보의 실제 Pass strike는0이며 경기 결과는0:6,0:5다. 계측 연결 통과를 패스 성공이나 정책 품질 향상으로 해석하지 않는다.
- 경기 전체 정체는31회/64.2811초, 옛 마지막 round 카운터 합은6이다. 서로 다른 범위임을 구별해 보존했다.
- 신규 문맥의 관측 tick·명령·mask·소유·pause 일치 검사와 분모 대조를 통과했다. 최소 통로 거리와 목표 거리는 원본 결정마다 보존했다.

## 버전과 보존

변경 전 source3724개를 `Logs/MNG-Rebuild/MS3-D1D2-start-20260923`에 SHA와 함께 보존했다. 새 환경 revision은 `MNG-P1-MS3-stability-D1D2-20260923`이다. protocol3, frozen 평가 protocol v2, observation244, task/event2는 유지한다. 이전 r4 Player와 성적은 해당 옛 runtime의 증거이며 이번 소스로 실행한 결과로 재표시하지 않는다.

새 평가 Player: `Builds/MNG_V2/MS3-stability-D1D2-20260923/EvaluationV2/MNG_EvaluationV2.exe`. Runtime SHA-256: `a03fc8a568cc52bff698227a542df2714805868dc4a6ab8c8d42de49021e5f26`. 이 Player는 communicator 기반 평가용이며 사람 검수용 단독 모델 빌드가 아니다.

기존 Run/PT/ONNX/registry·Player·실패 로그를 보존하고, 학습/optimizer 갱신/모델 승격을 하지 않는다. 5시간·주간 사용량 비율의 사전 중단 규칙은 앞선 사용자 지시대로 폐지 상태를 유지한다.

최종 보존 감사에서 기존 source12파일 변경과 새 보고서1개를 확인했고 누락 파일은0개였다. 보호한 R5/R6 입력121파일은 모두 SHA 동일이며, 두 ProjectSettings 파일·기존 Scene/Prefab/모델/registry는 변경하지 않았다. 결과는 `final-preservation.json`이다. 이번에 commit/push는 하지 않았다.

재현 진입점: Unity EditMode filter `MachineLearning.Soccer`; PlayMode filter `MachineLearning.Soccer.Manager.Tests.MNG_PostR6PlayModeTests;MachineLearning.Soccer.Tests.SoccerEnvironmentPlayModeTests`; Python `python -m unittest discover -s Tools -p test_mng_v2_evaluate.py -v`. Player는 `MNG_V2Builder.BuildPostR6EvaluationBatch`에 새 `-mngBuildRoot`를 명시해 만들었다. 연결 smoke는 `Tools/mng_v2_evaluate.py --candidate uniform-valid --opponent R0-Full-v2 --pairs 1 --seed 592501 --parallel 2 --mirror-pairs --decision-trace --lifecycle-trace`에 위 새 `--build`와 독립 `--output`을 지정했다. 기존 출력 폴더를 재사용하지 않는다.

## 전체 단계 위치

| MS 기준 | R/D 대응 | 상태 해석 |
| --- | --- | --- |
| MS0 준비·MS1 혼합 세부 판단 | 과거 MS0/MS1 | 완료 이력 보존 |
| MS2-v2·MS3-v2 공통 기반 개편 | R1~R4 | 구현·검증 완료 이력 |
| MS2-v2 준비 학습 | R5 | fresh200,120 step 완료 이력 |
| MS3-v2 초기 자기대전 | R6 | self-play402,389 step와 평가 완료; 확장 승인과는 별개 |
| MS3-v2 안정화 / 진영 로직 보정 | D1 | 이번 지정 범위 완료: 추가 오류 수정·회귀 통과 |
| MS3-v2 안정화 / 진단 계측 정비 | D2 | 이번 지정 범위 완료: 누락 계측·무결성 및 실제 Player 연결 통과 |
| MS3-v2 안정화 / 패스 기술 검증 | D3 | 이전 기술 회귀 통과, 이번에 전략/규칙 추가 수정 없음 |
| MS3-v2 안정화 / 동적 대칭·전술 평가 | D4 | 이전 진단 수행, 동적 진영 격차 미해결·승인 보류 |
| MS3-v2 안정화 / 한정 학습 진입 판단 | D5 | 미진입 |
| MS3-v2 검증 후 학습 확장 | R7~R8 | 미진입 |

다음은 MS3-v2 안정화의 동적 대칭·전술 평가(D4)에서 첫 접촉 구간의 명령 replay와 생성/물리 등록 순서를 분리하는 조사다. D1 순수 로직 회귀나 D2 기록 연결 통과만으로 대규모 학습을 재개하지 않는다. 기존400k의 Pass0과 낮은 선택 확률도 학습 전략 문제로 남아 있다. 사람의 화면 검수, 수정 후 정책 품질 개선 및 진영 공정성 승인은 이번 자동 검사의 증명 범위 밖이다.

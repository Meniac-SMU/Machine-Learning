> 보관일: 2026-09-24. 작성 당시의 계획·판정·승인 이력이며 현재 실행 지시가 아닙니다. 현행 기준: [현재 상태](../../../soccer/current-status.md).

# MS3-v2 자기대전 안정화 — 패스 검증·동적 대칭·학습 진입 준비(D3~D5)

2026-09-23. [진단·수정 계획](ms-post-r6-diagnosis-plan-20260923.md)의 D3~D5 후속 작업. D1·D2의 선행 결과는 [별도 보고서](ms3-stability-d1-d2-report-20260923.md)에 보존한다. 이번 작업은 학습 직전 준비까지이며 프로젝트 학습 step·optimizer update·모델 승격은 모두 0이다.

## 진행 위치와 승인 경계

MS0/MS1 완료 이력 → MS2-v2 준비 학습(R5, 200,120) → MS3-v2 초기 자기대전(R6, 402,389) → **MS3-v2 R6 이후 안정화** 순서다. 공통 개편 R1~R4와 R5~R6 실행 이력은 유지하고 R7~R8 확장에는 진입하지 않는다. D1~D5는 MS3-v2 안정화의 세부 작업 ID이며 별도의 최상위 단계가 아니다. 규칙형 감독 R0와 개편 순서 R1~R8도 구분한다.

**최종 판정: D3 기술 검증, D4 원인 분리·수정·동결 재평가, D5 한정 학습 진입 준비를 완료했다.** 새 실험의 첫100k 점검·최대200k까지 준비 gate는 통과했다. 수백만 step 확대, 기존400k 재개, 새 모델 승격은 승인하지 않는다. MS3-v2 전체 완료가 아니라 R6 이후 안정화와 학습 직전 준비 완료다.

## MS3-v2 안정화 / 패스 기술 검증(D3)

최종 코드에서 양 진영 22개 패스 상황을 다시 검증했다. 안전한 14개 상황은 실제 strike 1회와 지정 동료의 안정 수신 1회씩 성공했다. 나머지 8개는 위험 경로·소유 상실·명령 교체의 기대 결과를 검사한다. 출발 후 감독 명령 교체로 수신이 취소되는 경우도 포함하며, 모든 상황을 수신 성공으로 세지 않는다. 직선·측면·후방·이동 수신과 좌우 회전, 출발 후 취소를 포함한다.

패스를 강제로 선택시키는 출력 교체, 추가 전략 mask, 최소 횟수, 다양성 보상은 추가하지 않았다. 실제 완료 패스 +0.06, 명령 선택 보상 0, 일반 MS 회수 보상 0을 유지한다. 기존 준비된 MS2-v2 actor의 가중치를 새 optimizer로 시작하는 한정 실험을 준비하되, 400k 정책의 Pass 미사용을 기술 성공과 혼동하지 않는다.

## MS3-v2 안정화 / 동적 대칭·전술 평가(D4)

### 최초 접촉 원인 분리

동일 Recover 명령을 양 팀에 고정하고 2개 seed에서 250 물리 tick(5초)을 기록했다. 정상 반복, 180도 미러, 활성화/등록 역순, 실제 선수 생성 역순, 접촉 스크립트 응답 제외를 비교했다. 원본 기록은 `Logs/MNG-Rebuild/MS3-D3D5-validation-20260923/replay*`에 보존한다.

- seed592311은 첫 접촉 미러 오차가 작았다. seed592312는 tick144(2.88초)의 공 속도 차이가 약 5.9125m/s로 커졌다. 이동 명령 차이는 tick146, 목표 차이는 tick150부터 나타나므로 이 구간은 정책 명령 차이가 먼저 발생한 사례가 아니다.
- 같은 방향의 반복 및 실제 생성 순서를 뒤집은 실행은 이 차이를 제거하지 못했다. BallControl 접촉 응답을 제외해도 차이가 남았다.
- 직전 tick143의 실제 상태를 복원하고 감독·계획·공 제어 콜백을 제거한 채 동일 모터 입력만 적용한 native physics 재생에서도 약 5.9131m/s 차이를 확인했다. 0.00001m 초기 위치 perturbation과 solver 설정에 민감했다. 색상별 설계 능력 차이의 증거는 아니다.
- solver 반복 수 조사는 단조롭게 개선되지 않았다. 따라서 작은 재생 하나를 완전 대칭의 증명으로 사용하지 않는다. 실제 5초 고정 명령 재생에서 48/16 설정은 seed592312의 최초 큰 속도 차이를 약 0.007814m/s로 줄였지만 이후 최대 약 0.6285m/s까지 차이가 남았다. PhysX 궤적의 bitwise 대칭을 보장하지 않는다.

### 공통 물리 보정과 리셋 오류 수정

`MNG_PhysicsProfile.ConfigureContactSolver`를 공과 8명 선수의 Awake/Configure에 적용했다. position solver iterations는 기본 6에서 48, velocity solver iterations는 1에서 16으로 변경했다. 팀별 분기가 없으며 전역 ProjectSettings Physics 값이나 별도 Core 환경의 solver를 바꾸지 않았다. 질량·최대 이동/회전 속도·킥 값·보상·244관측·6행동은 유지한다. 실제 계산 비용은 최종 32환경 preflight로 확인한다.

별도로 reset 직전 Transform/Rigidbody를 37도로 회전시킨 뒤 즉시 관측을 검사했다. 수정 전 검사에서 초기 방향과 관측 방향의 벡터 거리가 0.892395854로 실패했다(`reset-before.xml`). 기존 reset은 Rigidbody 회전만 복원하고, 뒤의 위치 jitter/SyncTransforms 경로에서 남아 있던 Transform 방향이 다시 반영될 수 있었다. 이제 공과 선수의 **Transform 및 Rigidbody 위치·회전을 함께 복원**한다. 새 회귀 검사는 전체 PlayMode 39개 일반 검사에 포함해 통과했다.

물리와 reset은 MNG PPO 감독·규칙형 감독·선수가 공유하는 경로다. 별도 Core Rule FSM에 동일한 MNG 함수가 없으므로 수치를 무조건 복제하지 않았다. Core 환경 PlayMode와 공통 5환경 Builder parity를 함께 확인한다.

### 실행 파일 구분

최종 build root는 `Builds/MNG_V2/MS3-stability-contact-reset-20260923`이다. MS3V2와 EvaluationV2의 runtime SHA는 둘 다 `53de9775c503106bc11472394ffdf612367a3935771a0b1b5992acb3bb0ebab4`이며 환경 revision은 `MNG-P1-MS3-contact-reset-20260923`이다. build-info에 EXE·level·runtime 해시를 각각 보존한다. 빌드 자체는 모델 승인 상태를 부여하지 않는다.

이전 D1D2 Player(a03fc8…)의 이번 추가 40경기는 Red30%/Navy65%, seed cluster95% 차이 [-62.5,-5]%p로 여전히 문제가 있었다. 이 결과 때문에 원인 분리와 수정을 진행했다. 중간 준비 Player(6001dc…)와 해당 32환경 smoke도 최종 결과로 대체 집계하지 않는다. 서로 다른 런타임의 경기 수·승률을 합치지 않는다.

## MS3-v2 안정화 / 한정 학습 진입 준비(D5)

`check_mng_ms3_preflight.py`는 Trainer 없이 32개 UnityEnvironment를 동시에 열고 barrier에서 모두 살아 있음을 확인한 후 양 팀의 244관측·6행동·유한 수치와 512개 명령 전달을 검사한다. 환경 step은 동작 검사이며 학습 step/optimizer update는 0이다.

`prepare_mng_ms3_experiment.py`는 D1D2/D3/D4/EditMode/PlayMode/Python 결과, 동일 runtime, 32환경 preflight, actor-only 초기화의 형태와 해시를 검사한다. 하나라도 미통과면 readyToStart=false인 준비물을 만들며 학습 시작 스크립트가 거부한다. 잘못된 runtime/worker 수/누락 검사 및 실패한 D4를 통과시키지 않는 회귀를 추가했다.

준비한 실행 경계는 32환경, trainer seed20260923, 최대 aggregate200k, 첫100k 정상 저장·모니터링, 후속200k 상세 평가다. 실제 마지막 batch 때문에 경계 step을 조금 넘길 수 있음을 명시한다. 1M/2M 자동 확대나 R7 자동 진입은 없다. 새 Run은 기존 r005의 optimizer/pool을 resume하지 않는다. 기존 MS2-v2 actor와 새 optimizer·새 pool을 사용하고 초기 actor 및 역사400k actor를 고정 상대에 보존한다. 따라서 구 Run과의 엄격한 단일 요인 인과 실험이라고 부르지 않는다.

학습 wrapper는 명시적 TrainingBuild·MaximumSteps·Seed를 요구하고 runtime/source/actor/history 해시를 확인한다. 준비 스크립트는 최대200k로 잠근다. 200k 재개에는 별도 monitor-100k.json의 Run/runtime/통과 확인이 필요하다. 임의의 기존 Run이나 새 build를 슬쩍 이어 붙이지 못하게 했다.

GhostTrainer adapter에 실제 소비한 learning-team transitions와 ghost transitions 기록을 추가했다. 진영 전환 전의 팀에 해당 advance의 학습량을 귀속하고 resume/저장 경계를 추적한다. 40k 교대이므로 200k 중단에서 한 구간의 차이가 남을 수 있으며 교대 횟수를 50:50 노출의 증거로 삼지 않는다. history checkpoint에서는 Policy만 읽고 optimizer를 가져오지 않는다. 기존 pool/RNG 복원 회귀도 유지한다.

최종 holdout seed996001부터40쌍은 미실행 예약으로 분리한다. 개발 진단 seed를 미사용 holdout이라고 부르지 않는다. 실제 학습의 NaN/crash/무결성·RAM·정상 저장 경계는 유지하고, 5시간/주간 사용량 비율 기반 중단 규칙은 폐지된 상태다.

## 검증과 실패 이력

- 최종 Soccer EditMode **349/349 통과**.
- 최종 MNG+Core 환경 PlayMode **39/39 일반 검사 통과**, 명시적 진단 인수가 필요한 2개는 Ignore. XML total41/pass39/fail0/skip2다.
- 중간 PlayMode의 일반38개는 통과했으나 진단2개가 namespace 필터에서도 선택되어 이전 기록 덮어쓰기를 거부했다. 진단 인수 없이 실행할 때 Ignore하도록 수정했다. 이 2개를 게임 결함으로 집계하지 않는다.
- 리셋 새 검사의 수정 전 실패는 실제 제품 결함 재현이며 위 fixture 실행 조건 문제와 별개다.
- 최종 Python **29/29** 회귀는 pool복원, history actor, 실제 학습 진영 귀속, PPO 계측 무간섭, frozen evaluator, 준비 gate와 actor 원본 보존을 포함한다.
- 새 self-play/evaluation Player 빌드, 공통5환경 비변경 Builder 검증을 수행했다. 실제 학습, 새 모델 성능 검증, 수동 화면 검수는 수행하지 않았다.

## 최종 동적 대칭 결과와 해석

새 runtime의 동일400k정책 40경기는 seed592701~592710, 각 seed의 양 진영·추론 RNG교차 4경기, 고정300초다. Red 점수율55%, Navy57.5%, 차이−2.5%p, seed cluster bootstrap95% 구간은 **[−30,+25]%p**다. 점수율은 승1/무0.5/패0이다. 기존처럼 한쪽으로 큰 차이가 난 패턴은 이번 표본에 재현되지 않았다. 그러나 구간이 넓고 10개 독립 seed뿐이므로 동등성 입증이나 장기 진영 편향 부재를 주장하지 않는다. 새 seed 표본과 복합 환경 수정이므로 이전 결과와의 차이를 한 수정의 인과 기여량으로 계산하지 않는다.

20개 미러 쌍에서 최초 관측 최대 오차2.385e−7, 대응 kickoff 위치·방향 최대 오차2.385e−7였다. 이후 최초 관측 차이>0.001은 결정6~34에 나타나며 물리 궤적은 여전히 완전히 동일하지 않다. 반복 가능한 초기화와 최초 접촉 문제를 분리했고, 승률을 인위적으로 맞추는 색상 보정은 하지 않았다.

동일정책40경기에서 자기 소유 판단4,546회 중 Pass허용4,313회, Pass선택0회다. 전방 막힘2,455회 중 허용2,313회이며 이때 평균 Pass확률은 약0.00404%다. 허용 패스의 목표 거리 중앙값10.464m, 상대와 통로의 최소 거리 중앙값1.749m다. 개별 연속값을 남겼으며 이것을 전부 안전한 패스라고 판정하지 않는다. 경로가 존재해도 기존400k정책의 선택 확률이 매우 낮다는 사실과 D3 기술 실행 성공은 동시에 성립한다.

Recover 선택은 자기 소유3,105/4,546=68.30%, pause제외 비소유 평균 선택 확률95.02%다. 직전 Recover 아래 관측한 carrier제외 후방 필드 선수는 자기 소유3,558표본 평균1.594명, 비소유16,580표본 평균1.821명이다. 감독 역할 배정과 실제 기하학적 후방 위치를 구분한다. 112실점을 관측에서 식별했고65개에 마지막 소유 이탈을 연결했다. Recover 뒤의 연결32개는 이탈→실점 중앙값13.003초다. 의도한 슛도 소유 이탈에 포함되므로 명령별 실패율·인과 효과로 해석하지 않는다.

episode전체 정체557회/1,759.108초, 경기당43.978초다. 안전 회피 source는 candidate74,109/2,254,800 tick=3.287%이며 정체 시간과 같은 지표가 아니다. 높은 Recover 빈도를 이유로 인원 배정·명령 의미·보상을 강제로 바꾸지 않았다.

## 같은 최종 runtime의 기준 대진

| 대진 | 경기 | 승/무/패 | 점수율 | 득:실 | Red/Navy 점수율 |
|---|---:|---:|---:|---:|---:|
| 400k 대 동일400k | 40 | 18/9/13 | 56.25% | 130:112 | 55%/57.5% |
| 400k 대 규칙형 Full | 8 | 8/0/0 | 100% | 36:4 | 100%/100% |
| 400k 대 Recover 전용 | 8 | 6/1/1 | 81.25% | 24:11 | 75%/87.5% |
| 400k 대 같은Run200k | 8 | 1/1/6 | 18.75% | 22:33 | 12.5%/25% |
| 400k 대 MS2 준비200k | 8 | 5/2/1 | 75% | 24:14 | 87.5%/62.5% |
| 무작위 유효명령 대 규칙형 Full | 8 | 0/0/8 | 0% | 5:48 | 0%/0% |

총80경기 모두300초·초기화/명령/계측 무결성 검사를 통과했다. 명령 불일치·override·직접 명령 보상은0이다. 동일정책 외 대진은 각4개 독립 seed×양 진영이다(Full592801~, Recover592811~, history200592821~, initial592831~, uniform592841~). 8경기 소표본으로 정책 순위나 승격을 확정하지 않는다. 모든 bootstrap 구간이 [1,1] 또는 [0,0]인 표본도 모집단 확률을 확정하지 않는다.

무작위 후보의 Pass는90선택→6실제strike→1완료 수신이었다. 선택과 strike를 연결하면 소유 상실23, 다른 명령51, 반복Pass10, 실제strike6이다. 반복Pass는 자동 실패로 세지 않는다. 정상 경기에서도 실제 완료 패스가 나왔지만 이를 기존400k정책의 패스 개선으로 해석하지 않는다.

기존400k는 새 환경에서 같은Run200k에 열세였고 Pass확률도 매우 낮다. 따라서 규칙형 상대 전승만으로 기존 모델을 계속 학습시키거나 챔피언으로 재승인하지 않는다. 원본 registry는 역사 기록으로 보존하며 새 환경의 후보 선택은 향후 동일 runtime 평가에서 다시 판단한다. 정책 학습 성과의 문제를 색상별 능력 보정·패스 강제·Recover 약화로 숨기지 않는다.

## 준비물과 다음 실행 경계

- 준비 폴더: `Logs/MNG-Rebuild/MS3-D5-prepared-20260923`.
- `manifest.json`: `readyToStart=true`, `trainingStarted=false`, `optimizerUpdates=0`. 예약 Run은 `MNG_MS3V2-20260923-r001`이며 results/Run 폴더는 생성하지 않았다.
- `initial-policy.pt`: MS2 준비 actor의 Policy를 그대로 복제하고 사본의 global_step만0으로 초기화했다. 원본은 step200120이었다. 첫 준비 검사에서 이 차이를 검출하고 원본을 바꾸지 않는 사본 초기화와 회귀2개를 추가했다. 기존 optimizer 상태는 없으며 초기 actor source/새 사본/history SHA를 기록했다.
- `config-preview.yaml`: 최대200k/32환경/기존PPO설정. 새 학습에서 배우는지 검증하는 자료이며 설정 생성 자체가 학습은 아니다.
- `launch-first-100k.ps1`: source/build/actor/history gate를 확인하고100k에서 정상 저장하도록 준비했다. **이번 작업에서 실행하지 않았다.**
- `resume-to-200k.ps1`: 같은 Run/runtime의100k 모니터링 통과 파일이 없으면 거부한다. 이번 작업에서 실행하지 않았다.
- `preparation-gate.json`은 `readiness-receipt-final.json`과 최종32환경 보고서의 해시에 연결된다. D4 통과의 범위는 원인 분리·코드 보정·한정 학습 준비이며 완전한 진영 동등성이나 모델 품질 승인이 아니다.

최종32환경 점검은14.922초, 양 팀512개 결정, 동시32개 barrier 통과, 학습0/optimizer0이었다. RAM38%→동시 연결55%, 해당 시점 가용15,138,344,960byte(약14.10GiB)였다. 이는 짧은 환경 연결 점검이며 GPU optimizer를 포함한 장시간 학습의 메모리·처리량 보장은 아니다.

권장 다음 작업은 준비한 별도 실험의100k까지 실행한 뒤 실제 learning-team 노출, loss/KL/clip/entropy, Pass 기회별 확률→strike→수신, Recover/후방 위험, 정체·안전 개입, 규칙형·Recover·초기·history200/400k 동결 대진을 함께 보는 것이다. reward상승만으로200k 이후 연장하지 않는다. 200k에서 진전이 없거나 퇴행하면 정상 저장 후 원인을 재진단한다. 숨겨둔 holdout은 개발 조정이 끝난 최종 후보에만 사용한다.

## 보존과 증거 위치

이번 시작 snapshot은 `Logs/MNG-Rebuild/MS3-D3D5-start-20260923`이다. 이전부터 존재한 dirty working tree를 초기화하지 않았다. 원본121개 모델·기록·registry·빌드 입력 해시가 모두 동일함을 `frozen-inputs-verified.json`에서 확인했다. Unity가 추가한 Standalone `SENTIS_ANALYTICS_ENABLED`만 이번 시작 baseline과 대조해 되돌렸고 ProjectSettings/EditorBuildSettings를 모두 보존했다. 기존 모델/씬/프리팹을 새 학습용으로 덮어쓰지 않았다. commit/push는 수행하지 않았다.

시작 snapshot 대비 기존17개 파일을 수정하고 준비 도구·회귀3개 및 이 보고서를 추가했다. 저장소 전체 `git diff --check`는 기존 MNG_StadiumEnvironment.prefab의 trailing whitespace를 보고했지만 해당 prefab은 시작 snapshot과 동일하다. 이번 추가·수정 줄의 별도 공백 검사는 통과했다. 관계없는 prefab 포맷 변경은 하지 않았다. 학습·평가 Player는 모두 종료했고 기존 Unity 연결 helper는 보존했다.

최종 검사 XML, 실패 재현 XML, 빌드/Builder log, 소스 보존 검사, `mirror-final-analysis.json`, `mirror-final-summary.json`, `baselines-final-analysis.json`, `uniform-pass-final.json`, `readiness-receipt-final.json`은 `Logs/MNG-Rebuild/MS3-D3D5-validation-20260923`에 있다. 각 대진 원본은 `Logs/MNG-Rebuild/MS3-D4-final-*`, 최종32환경은 `MS3-D5-preflight32-final-20260923`에 보존한다. runtime·정책 해시를 확인한 뒤 같은 버전 안에서만 비교한다.

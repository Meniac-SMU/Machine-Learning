# MNG 검증·운영·Sol 인계

## 1. 단일 실행 환경

작업 루트 C:/GitHub/Machine-Learning. M0 런타임, M1 공격 선택, M2 수비 선택의 Player·훈련·frozen ONNX 독립 평가 경로를 구현하고 실제 실행했다. M3 학습과 frozen 300초×40경기 평가 경로도 구현했으나 정식 평가는 아직 실행 전이다. 범용 inspect/manifest 통합 도구는 아직 미구현이다.

| 도구 | 상태와 요구 기능 |
|---|---|
| Tools/MNG_Build.ps1 | 구현·실행 확인. M1/M2/M3 단계 Scene별 BuildPlayer와 exe/level0/config SHA 기록 |
| Tools/MNG_Train.ps1 | 구현·M1/M2/M3 실행 확인. 단계·RunId·seed·단일 worker·중복Run 거절, 다음 단계는 승인 이전 PT에서 initialize-from |
| Tools/MNG_Evaluate.ps1 / MNG_EvaluateM2.ps1 | 구현·실행 확인. frozen ONNX를 InferenceOnly Player에 고정하고 별도100상황 JSON/log/build manifest 생성 |
| Tools/MNG_EvaluateM3.ps1 | 구현·계약 테스트 확인, 실행 전. frozen M3 ONNX 대 Navy Fallback의300초×40경기 JSON/log/build manifest 생성 |
| Tools/MNG_Inspect.py | 미구현. 감독decision·update·checkpoint·자원·상대모델·legacy reward 누출 수집 |
| Tools/MNG_Manifest.py | 미구현. source/config/build/model/eval SHA-256 기록 |

현재/예정 호출 인터페이스:

```powershell
# 실행 확인된 단계별 진입점.
.\Tools\MNG_Build.ps1 -Profile M2-DefenseChoice
.\Tools\MNG_Train.ps1 -Stage M2-DefenseChoice -RunId MNG_M2Defense-YYYYMMDD-r001 -Seed 12001 -InitializeFrom MNG_M1Attack-YYYYMMDD-r001
.\Tools\MNG_Evaluate.ps1 -RunId MNG_M1Attack-YYYYMMDD-r001
.\Tools\MNG_EvaluateM2.ps1 -RunId MNG_M2Defense-YYYYMMDD-r001
.\Tools\MNG_EvaluateM3.ps1 -RunId MNG_M3Fallback-YYYYMMDD-r001
```

기존 Build/Train 스크립트는 참고 자료이며 새Behavior·Scene·PPO 배선에 맞는지 확인 없이 사용하지 않는다. 실제 실행 전 설치 trainer --help/YAML parse, Python/Torch/Unity ML-Agents 버전과 통신 호환성을 기록한다.

## 2. 테스트 계층

| 계층 | 필수 확인 |
|---|---|
| 정적 | prefix/path/schema/파일 참조/변경 범위/충돌 marker |
| EditMode | 관측133/좌우대칭/mask/명령차이/킥 계산/소유 사건/보상중복·cap/owner |
| Builder | 모든MNG 단계의 geometry·공·제어·관측·행동 parity, legacy컴포넌트0 |
| PlayMode | 골·reset·공제어·회수·패스·모델교체·H전환·중복writer0 |
| Build | 실제Windows Player 생성, 에디터전용API누출0, source/config/build해시 |
| 학습 smoke | 감독만등록, PPO update, finite값, 저장/재개, 상대고정 확인 |
| 독립 평가 | frozen ONNX로 미사용seed/좌우/동일상대 비교 |
| 시각·입력 | 실제Stadium 화면/기존UI/카메라/H키/키보드·게임패드 확인 |

Unity XML이 없으면 exit0이어도 테스트 통과가 아니다. -runTests 실행에서는 과거 -quit 조합의 XML 누락 이력이 있어 먼저 -quit 없이 Test Runner 자체 종료를 사용한다. 최신 실제 환경으로 확인한다. Editor가 열려 있으면 같은 프로젝트 배치 실행을 중복하지 않는다. 기존 모든 프로그램을 일괄 종료하지 않는다.

소스 변경 없는 문서 작업에는 Unity를 실행하지 않는다. Core를 건드리지 않은 MNG 변경은 MNG 테스트+기존 환경 불변 검사, 공통 Core 변경은 기존 Soccer 관련 전체 회귀가 필요하다.

## 3. 실험 분리와 공정성

- 정책 집합: 무작위 유효명령, 초기 미학습, MNG_Fallback, M3 Base, 최종Base, Attack/Defense/Press, self-play 과거snapshot.
- 모든 정책은 같은6명령·executor·공4.5·크기1.1·속도·safety를 사용한다.
- Random은 mask된 행동을 제외한 균등선택. 모든 기술이 실패하는 무의미한 baseline을 만들지 않는다.
- 훈련 seed는11000대, 개발검증21000대, 최종보류31000대. 구체 목록은 구현초기에 fixture로 저장하고 hash를 고정한다.
- 좌우같은 초기배치를180도 회전한 쌍으로 경기한다. 쌍은 통계상 독립2개로 과장하지 않는다.
- validation으로 선택한 checkpoint만 final holdout에 사용. 결과가 나쁘다고 최종seed를 바꾸거나 반복 탐색하지 않는다.
- final 기준 상대별50 seed쌍=100경기, 동일예산에서 Base/전술/규칙형 비교. 인간모드는 별도 최소20 seed쌍+실제사용자 테스트.
- 300초가 최종 경기 단위. 짧은60초지표와 섞지 않는다.

## 4. 최종 완료 gate

### 시스템

- 고정100경기에서 공소실/NaN/중복득점/소유원장불일치/몰래Fallback/명령Human침범0.
- Field/Goal/선수원본치수일치, 공배율/질량정확, Builder재실행불변.
- 공격·수비·압박모델 모두입출력호환,교체시점수/시간/공/모드보존.
- H전환 양방향20회 이상, 소유중/패스비행/GoalPause 포함. 관측Human포함과AI쓰기0 모두통과.

### 경기·학습

- frozen Base 대 MNG_Fallback scoreRate>=0.50, 무득점경기비율<=20%,양팀회수/슛/득점샘플존재.
- 같은상대에서 Base가 random선택보다 scoreRate +0.10 이상이고, seed쌍bootstrap95% 차이구간하한>0을 목표 gate로 둔다. 미달이면 학습효과 미입증.
- 필요시 표본확대는 불확실성 감소를 위해 사전고정된 추가seed로1회만. 유리한seed고르기금지.
- 시간초과로완료기준을완화하지않는다. 기능완료/성능미달을분리해보고한다.

### 전술

같은상대·seed에서 Base 대비 사전지정지표를 비교한다.

| 모델 | 우선 지표 | 초기 목표 |
|---|---|---|
| Attack | 경기당 유효슛 | Base 대비+10% 이상 |
| Defense | 경기당 허용 유효슛 | Base 대비-10% 이하 |
| Press | 상실후5초내회수 비율 | Base 대비+10%p 이상 |

분모0인 경우 상대변경을몰래하지않고 측정불가로표시한다. 전술별scoreRate가Base보다0.10넘게하락하면전술품질실패. 95%구간과실제영상같이제시하며차이가불분명하면더학습/미달기록. 학습Reward 총합으로모델끼리비교하지않는다.

## 5. 기록과 로그

각Run은 config/source/build/version/seed/worker/model/opponent hash를 저장한다. 최소메트릭:

- manager decision/sec, physics tick/sec, episode/sec, PPO update수,loss/entropy/NaN.
- 요청/실행명령,mask비율,명령체류시간,실행실패이유,각선수task revision.
- 득실점/유효슛/pass수신/회수/빠른회수/과밀/무득점/공정지시간.
- Human모드시간,제어권전환,AI→Human금지쓰기,Human/AI기여사건.
- legacyreward/group지급0,신경망없는자동Fallback0,안전clearance개입횟수.
- self-play 상대hash/step/교체시각/TeamId,trainer학습측과추론측 구분.

training status에는 명령을 실행한 것과 파일 생성/성공을 확인한 것을 구분한다. 전체trace는 평가/진단에만, 훈련 hot path는 집계로 성능 유지.

## 6. 성능·중단·실패 대응

- 1→4→8환경으로 측정 후 증설. RAM80% 초과/여유8GiB 미만이 지속되면 checkpoint 후worker축소. GPU0%만으로문제단정금지.
- wall timeout wrapper는 Ctrl+C로정상저장요청,해당Run process만관리. 종료안되면출력/상태보존후정확한PID범위만판단.
- training 시작·checkpoint·phase전환·완료시inspect 저장. 기존 Tools/inspect_soccer_training.py는선수계약가정을검토한뒤재사용하거나MNG_로포팅.
- 같은gate에2회새Run실패하면무한연장금지. 관측/실행/보상/상대원인분리후사용자에게미달과대안보고.
- 사용량중단은프로젝트AGENTS의기준에따라정확한재개명령/마지막checkpoint/미완료테스트저장. reset credit/재개automation을자동생성하지않음.
- code 수정후훈련Player를다시빌드하지않고예전exe로학습하지않는다. manifest불일치면실행거절.

## 7. 15일 운영 일정

| 기간 | 목표 |
|---|---|
| 1일 | M0기술/진행/UI/제어권, PPO smoke·첫훈련 |
| 2일 | M1/M2/M3, actual ONNX4대4시연과1차비교 |
| 3~5일 | 기본경기안정화/HUMAN대역학습/실제입력검증 |
| 6~8일 | self-play/pool/고정상대퇴행방지 |
| 9~11일 | Base에서3전술학습·교체 |
| 12~13일 | holdout/좌우/전술/입력평가 |
| 14~15일 | 결함수정·최종빌드/영상/설명/증거 |

2일은실제48시간기한으로관리하되개발자의노동시간과자동훈련시간을구분한다. 계획상시간배분은실행보증이아니다. 첫날늦게도기술이안되면새외부모델탐색으로갈아타지말고기술범위를정리하고지연을알린다.

## 8. 각 작업 후 Sol 인계 형식

아래 내용을 docs/soccer/current-status.md의 MNG섹션과해당Run README에남긴다. 계획문서를성공로그로덮어쓰지않는다.

```text
현재 티켓/단계:
최신 사용자 승인 범위:
완료 변경과 파일:
기존 변경 보존 확인:
검증 명령/결과/XML/로그 경로:
현재 Run/seed/worker/max_steps/wall budget:
최신 PT/ONNX/source/config/build SHA:
승급 gate별 수치와 통과/미달/미실행:
실패 원인과 다음 단일 변경:
정확한 재개 명령(실재 도구에 한함):
Human/시각 수동 검증 남은 것:
학습 진행 여부와 관련 process:
```

최종 패키지는 Windows 실행물,4개감독ONNX,PT/설정/소스정보,고정평가CSV/요약,대표·실패영상,AI/HUMAN조작설명,재현절차를포함한다. 공통기술은코드이고감독선택은RL이라는범위를명시한다.

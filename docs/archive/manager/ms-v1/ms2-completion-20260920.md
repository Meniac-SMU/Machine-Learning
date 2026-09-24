> 보관일: 2026-09-24. 작성 당시의 계획·판정·승인 이력이며 현재 실행 지시가 아닙니다. 현행 기준: [현재 상태](../../../soccer/current-status.md).

# MS2 완료 보고서 — 2026-09-20

> **후속 상태:** 이 문서는 최초 완료 Run `r002`의 동결 기록이다. 사용자의 100k 확장 지시로 `r003` 학습과 코드 보조 통제 평가를 추가 완료했다. 최신 모델·판정·MS3 전 권장 작업은 [MS2 100k 확장 및 코드 보조 감사](ms2-100k-and-code-assist-audit-20260921.md)를 우선한다. `r002` 증거와 아래 당시 판정은 이력으로 보존한다.

## 완료 판정

MS2를 완료했다. 선택 정책은 `MNG_MS2-20260920-r002-step19912`다. MS1에서 익힌 공격·수비/전환 판단을 정상 킥오프 120초 경기와 완성된 규칙형 R0-Full에 노출한 뒤, 훈련과 분리된 300초 ONNX 고정평가에서 R0와 대등 이상이고 무작위 유효명령 감독보다 명확히 강한지 판정했다.

최종 정책은 MS2의 네 필수 조건을 모두 통과했다.

- 합산 score rate `0.525 >= 0.50`
- 총 득점 `45 >= 44` 총 실점
- Red와 Navy score rate 각각 `0.525 >= 0.40`
- 같은 R0·seed·build의 무작위 유효명령 `0.125`보다 `+0.400 >= +0.10`

이 판정은 정상 강도 R0와의 운영상 대등성, 양 진영 안정성, 무작위 감독 대비 고수준 판단 개선을 뜻한다. 모든 축구 상대에 대한 보편적 우월성이나 선수 기술까지 신경망이 학습했다는 뜻은 아니다.

## 선택 모델과 MS3 입력

| 항목 | 값 |
| --- | --- |
| Run | `MNG_MS2-20260920-r002` |
| 후보 | `MNG_MS2-20260920-r002-step19912` |
| ONNX | `results/MNG_MS2-20260920-r002/MNG_Manager/MNG_Manager-19912.onnx` |
| ONNX SHA-256 | `7fce2603698df4f3b586f19b27f41f3c7dc143eca8c966324a2013c476c1e1e2` |
| PT | `results/MNG_MS2-20260920-r002/MNG_Manager/MNG_Manager-19912.pt` |
| PT SHA-256 | `04fd43f2e3383d923b60a60483b386e78862cd78013539d087ad9a43f81b9e3f` |
| protocol | `MNG-MS2-v1` |
| protocol SHA-256 | `58aeec18a463d38f7e9941d9ac4bea184d63831d7a81a81c0777cf02b6363a96` |

MS3는 반드시 위 번호가 붙은 `MNG_Manager-19912.pt`의 정책 가중치에서 새 optimizer로 시작한다. `checkpoint.pt`, step 39,976 또는 59,920을 자동으로 대신 쓰지 않는다. 후기 checkpoint는 보존된 학습 이력이며, 가장 이른 gate 통과 후보를 동결한다는 조기 종료 계약에 따라 승격하지 않았다.

## 학습 구성과 예산

- 시작 정책: MS1 선택 PT `MNG_Manager-7443.pt`, SHA-256 `ac8eb65dd7c633a6cb5e4dfa267daa92d63c8df5bfecb4031a4fe28c5cb4d3c3`
- 인계 방식: 정책 가중치 초기화, 새 MS2 optimizer
- 환경: 독립 Windows Player 16개
- trainer seed: `192001`
- port: `5900..5915`
- 경기: 정상 킥오프, 120초, R0-Full `1.0×/0.5초`
- 보상: 기존 Base 실제 사건 보상, MS2용 새 reward 없음
- 저장 checkpoint: 19,912 / 39,976 / 59,920

사전평가에서 MS1 정책은 R0-Medium 양 진영 score rate `1.00`을 기록했고 Full에서도 무작위보다 `+0.325` 높았다. 따라서 선택 사항이던 Medium 최대 20k를 생략하고 Full에서 바로 학습했다. 이는 상대 난이도를 숨긴 것이 아니라 이미 통과한 쉬운 구간을 반복하지 않고 self-play 진입을 빠르게 하는 계획상의 조기 전환이다.

사용량 중단 전 trainer 화면은 약 75k까지 진행됐지만 마지막 완전 저장은 59,920이다. 저장되지 않은 구간은 모델 선택이나 학습량 근거에 포함하지 않는다. 재개 후 새 학습을 하지 않고 이미 진단을 통과한 19,912의 최종평가를 먼저 수행했고, 통과 즉시 학습을 종료했다.

## 사전평가와 120초 진단

MS1 동결 정책 사전평가는 조건당 120초 10경기로 Medium/Full, Red/Navy, ONNX/무작위를 모두 비교했다.

| R0 | MS1 ONNX | 무작위 | 격차 | ONNX 득실 |
| --- | ---: | ---: | ---: | ---: |
| Medium | 1.000 | 0.875 | +0.125 | 44:0 |
| Full | 0.475 | 0.150 | +0.325 | 4:7 |

Full에서 정책과 무작위의 차이가 크게 벌어졌으므로 Easy 상대에서 보이지 않던 정책 효과가 강한 상대에서 나타난다는 가설을 지지했다. 동시에 score rate `0.475`, 득실 `4:7`로 최종 gate에는 약간 부족해 MS2 학습을 진행했다.

step 19,912의 120초 진단은 ONNX `0.50`, 무작위 `0.15`, 득실 `7:6`, Red/Navy 각각 `0.50`이었다. 이 진단 통과를 근거로 한 후보만 아래 300초 최종평가에 올렸다.

## 300초 최종 고정평가

평가 seed offset은 `491001`이고, 각 진영에서 ONNX 20경기와 무작위 20경기를 같은 build·R0-Full 조건으로 실행했다. ONNX 최종 판정 표본은 20 seed×양 진영 40경기이며, 무작위 비교도 별도의 40경기다.

| 정책·진영 | 승 | 무 | 패 | 득실 | score rate |
| --- | ---: | ---: | ---: | ---: | ---: |
| ONNX Red | 7 | 7 | 6 | 17:16 | 0.525 |
| ONNX Navy | 8 | 5 | 7 | 28:28 | 0.525 |
| **ONNX 합산** | **15** | **12** | **13** | **45:44** | **0.525** |
| 무작위 Red | 2 | 3 | 15 | 8:38 | 0.175 |
| 무작위 Navy | 0 | 3 | 17 | 6:47 | 0.075 |
| **무작위 합산** | **2** | **6** | **32** | **14:85** | **0.125** |

ONNX와 무작위의 격차는 `+0.400`이다. 결과 파일 누락, crash, timeout, NaN은 없었고 네 Player는 모두 exit 0으로 끝났다. 비교 결과는 `Logs/MNG-MS/MNG_MS2-20260920-r002-step19912-final-r001/comparison.json`, 개별 경기·명령 telemetry는 같은 디렉터리의 네 `Full-*.json`에 있다.

## 강화학습과 코드 보조의 경계

이 정책은 저수준 이동과 킥을 신경망이 직접 출력하는 end-to-end 선수 정책이 아니다. 팀당 PPO 감독 하나가 133개 관측으로 여섯 팀 명령 중 하나를 선택하고, 선수 이동·공 접근·패스 대상·슛 조준·골키퍼·안전 보조는 공통 코드가 실행한다. 따라서 정확한 표현은 **코드 기반 선수 기술 위에서 고수준 명령을 학습한 강화학습 감독**이다.

최종 ONNX 40경기의 원시 명령은 `AdvanceCarry 448, PassBuild 13, AttemptShot 175, ActiveRecover 6715, Balanced 14019, ProtectBack 2634`다. 실제 적용 명령은 `289, 843, 175, 6507, 13640, 2550`이며 전진 차단 시 공통 패스 보조가 830회 개입했다. 완료 패스는 31회, 유효 슛 241회, 5m 전진 578회다.

이 수치는 직접 PassBuild 선택이 강하게 학습됐다고 주장할 수 없음을 보여 준다. 패스 판단에는 코드 보조가 상당하다. 그러나 같은 코드 보조·같은 물리·같은 마스크·같은 R0를 공유한 무작위 감독은 score rate `0.125`, 득실 `14:85`에 그쳤다. ONNX는 양 진영 `0.525`, 득실 `45:44`를 기록했다. 차이를 만든 것은 모든 선수 기술을 새로 배운 것이 아니라 회복·균형·후방 보호·전진·슛 등 고수준 명령의 상태별 선택과 전환이다. 따라서 단순 규칙형 껍데기라고 볼 근거보다 실제 정책 판단이 성능에 기여했다는 비교 근거가 강하다.

## 검증과 보존 증거

- MS2 protocol: `Assets/_Soccer/Manager/Evaluation/MNG_MS2_Protocol_v1.json`
- 학습 설정: `Assets/_Soccer/Manager/Training/MNG_MS2.yaml`
- 학습 장면: `Assets/_Soccer/Manager/Curriculum/MS_ManagerSimple/Scenes/MNG_MS2_Train.unity`
- 학습 Player: `Builds/MNG_MS/MS2/MNG_MS2.exe`
- 학습 build manifest: `Builds/MNG_MS/MS2/build-info.json`, Unity `Succeeded`, error 0
- 최종 평가 build manifest: `Builds/MNG_MS/MS2-Preflight/MNG_MS2-20260920-r002-step19912/evaluation-build-info.json`, error 0
- 최종 비교: `Logs/MNG-MS/MNG_MS2-20260920-r002-step19912-final-r001/comparison.json`
- 전체 EditMode: `Logs/MNG-MS/MS2/final-editmode.xml`, `242/242` 통과
- 모델 hash 목록: `Logs/MNG-MS/MNG_MS2-20260920-r002/model-sha256-stop.json`
- 중단·재개 이력: [MS2 사용량 중단 체크포인트](ms2-stop-handoff-20260920.md)

기존 R0·MS0·MS1 Run과 MS2의 r001 기동 실패 흔적, r002 후기 checkpoint, 사전평가·진단·최종평가 원본을 삭제하거나 덮어쓰지 않았다. commit, push, reset, clean을 실행하지 않았다.

## 완료 범위와 다음 단계

완료 범위는 정상 강도 R0 상대 학습 연결, 16환경 실행, 양 진영 정책 평가, R0 대등성, 무작위 감독 대비 우월성, 실제 ONNX 추론과 PT/ONNX/hash 보존이다. 40경기는 빠른 개발을 위한 최소 고정 표본이며 모든 배치에서 항상 52.5%를 재현한다는 통계적 보장은 아니다.

다음 단계는 MS3 PPO self-play다. MS3는 동결 MS2 PT에서 시작하고, 적어도 두 과거 snapshot과 양 진영 학습 구간을 증명한 뒤 동결 MS2 및 R0-Full 회귀를 평가한다. MS3를 시작했다는 기록이나 self-play 성능 향상은 이 보고서에 포함하지 않는다.

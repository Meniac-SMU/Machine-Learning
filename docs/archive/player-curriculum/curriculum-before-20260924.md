> 2026-09-24 보관: 과거 누적 기록. 현재 실행 지시는 [현재 상태](../../soccer/current-status.md)를 따른다.

# Soccer 4v4 커리큘럼 학습 지침

## 목표

Base 정책을 처음부터 5분 Self-Play에 투입하지 않고, 공 인식부터 4v4 경기까지 난이도를 단계적으로 올린다. 모든 단계는 동일한 `Soccer4v4_Base` BehaviorName, 43개 벡터 관측, `[3,3,3,3]`의 네 이산 행동 브랜치, 4개 Red 역할과 동일한 Stadium 물리를 유지한다. 따라서 이전 단계 체크포인트를 `--initialize-from`으로 이어 받을 수 있다.

## 단계

| 단계 | 환경 | 상대 | 핵심 목표 | 상태 |
|---|---|---|---|---|
| L0 | `Curriculum/L0_BallApproach` | Navy 비활성 | 공 접근·0.75초 안정 소유 | 구현됨 |
| L1 | `Curriculum/L1_CarryAndShoot` | Navy 비활성 | 근거리 마무리 후 8m 운반·유효 슛·득점 | r017 승인 모델 보존 |
| L2 | `Curriculum/L2_ShortPass` | Navy 비활성/수동 장애 | 짧은 패스와 재소유 | 미통과: 성공률50% 이상 및 정식 검증 필수, 조기 진입 취소 |
| L3 | `Curriculum/L3_ProgressivePlay` | 무수비 준비 후 제한 수비 | 전진 패스와 3인 연계 | L2 승인 전 보류, 실행환경 미구현 |
| L4 | `Curriculum/L4_Weak4v4` | 약한 고정 4v4 | 압박이 있는 경기 전이 | 계획됨 |
| L5 | `Core/Scenes/Stadium4v4_Base_FallbackTraining.unity` | Navy Rule fallback | 완전한 4v4 경기 | 기존 Core 사용 |
| L6 | `Core/Scenes/Stadium4v4_Base.unity` | 학습 중인 Navy | Base Self-Play | 기존 Core 사용 |

## L0 학습 계획

L0는 20초짜리 짧은 에피소드다. 골키퍼를 제외한 세 Red 필드 선수 중 공이 배치될 기준 선수를 매 에피소드 순환하고, 공은 해당 선수의 공격 방향 1~2m와 측면 ±1m 범위에 무작위 배치한다. Navy GameObject는 첫 라운드 리셋 뒤 비활성화되며 Red 관측에는 고정된 0 패딩이 들어가므로 관측 크기는 변하지 않는다. 접근 거리의 새 최솟값과 최초 0.75초 안정 소유에 자체 상한이 있는 shaping 보상을 주며, 안정 소유가 확인되면 즉시 성공으로 종료한다. 이 보상들은 일반 경기용 shaping 상한과 분리되지만 에피소드별 자체 상한은 유지한다. 우연한 킥을 소유로 오인하지 않도록 L0의 킥 행동은 마스킹한다. L0에서는 일반 경기용 포메이션 보상과 군집 패널티를 끄고 공 접근과 소유 신호만 사용한다.

- 장기 목표량: 500,000 aggregate steps. 먼저 100,000-step 진단 Run으로 지표를 확인한 뒤 통과 가능한 추세일 때 이어서 학습한다.
- 기본 병렬 수: 16
- 성공 조건: Red가 공에 접근해 0.75초 안정 소유를 확정
- TensorBoard: `Soccer/Curriculum/L0/Success`, `Possession`, `Approach Progress`, `Episode Seconds`
- 승급 권장선: 별도 평가 구간의 성공률 85% 이상, 평균 완료 10초 이하
- 100k 진단 통과 기준: Success와 Possession이 후반으로 갈수록 명확히 상승하고 Success가 약 40~50% 이상에 도달할 것
- 실패 시: 500k로 바로 늘리지 않고 행동 분포, 관측 정규화, 충돌과 접촉 로그를 먼저 점검

## L1 학습 계획

L1은 완료된 L0와 짧은 드리블 모델에서 초기화한다. 에피소드는 30초이며 Navy는 비활성화한다. r010부터 [단계별 보상 계획](staged-reward-plan.md)을 적용한다. 일반 전술 보상을 지급 단계에서 차단하며 고정 `soccer_l1_phase` YAML을 사용한다. `ShortRangeFinish` 성공률 검증 뒤 별도 `CarryAndScore` Run으로 초기화한다. 근거리 단계는 추가 운반 거리 없이 안정 소유→명시적 킥→득점, 최종 단계는 8m 이상 제어 운반→명시적 킥→실제 득점이다. 슛 없이 미는 득점은 성공이 아니다.

- 목표량: 최대 1,000,000 aggregate steps
- 진단 간격: 50,000 steps마다 체크포인트, 100,000 steps 단위 성능 판정
- 기본 병렬 수: 32 시험 후 처리 속도·자원에 따라 16으로 축소
- 최종 성공 조건: 안정 소유 + 8m 제어 운반 + 골문 안쪽을 향한 명시적 유효 슛 + 실제 득점
- 지원 조건: 비소유자 3명이 소유자와 최소 6m, 서로 5m 이상 간격을 유지한다. 골키퍼는 자기 진영 밖으로 전진하지 않는다.
- TensorBoard: `Soccer/Curriculum/L1/Phase`, `Success`, `Possession`, `Dribble Progress`, `Carry Requirement Met`, `Shot Attempt`, `Shot On Target`, `Valid Shot`, `Goal`, `Episode Support Shape`, `Episode Seconds`
- 승급 권장선: 최근 5개 요약에서 소유율 85%, 유효 슛 시도율 85%, 유효 슈팅률 75%, 득점 및 전체 성공률 65%, 평균 완료 20초 이하
- 퇴행 확인: 최종 L1 체크포인트를 L0 환경에서도 평가해 공 접근 능력이 85% 이상 유지되는지 확인

## 순차 학습 원칙

1. 각 단계는 새 Run ID로 시작한다.
2. L1 이상은 `-InitializeFrom`으로 직전 단계 Run을 지정한다. `-Resume`은 같은 Run이 중단됐을 때만 사용한다.
3. 총 step만 채웠다는 이유로 승급하지 않는다. 성공률, 완료 시간, 리워드 분해 태그를 함께 확인한다.
4. 8/16개 실행 인스턴스는 하나의 정책과 aggregate step 목표를 공유한다. 인스턴스 수를 늘려도 YAML의 `max_steps`가 인스턴스마다 따로 적용되지는 않는다.
5. L0/L1에는 Self-Play를 켜지 않는다. Self-Play는 L6에서 사용한다.

## 2026-09-02 완료 기록

### L0 승급 모델

- Run: `CurriculumL0-20260902-r007`
- 초기화: `CurriculumL0-20260902-r006`
- 완료: 500,040 aggregate steps / 16개 실행 인스턴스
- 마지막 10개 구간 평균: 성공률 99.95%, 소유율 99.95%, 완료 시간 1.17초
- 승급 판정: 통과. 요구치 성공률 85%, 완료 시간 10초를 안정적으로 초과 달성했다.
- ONNX: `results/CurriculumL0-20260902-r007/Soccer4v4_Base.onnx`
- SHA-256: `F3795E86AD25DACA3BA71B37FA69FF6B0DD9D6A057CA60BD310A26CA1323D7DB`

### L1 짧은 드리블 기준 모델

- Run: `CurriculumL1-20260902-r001`
- 초기화: `CurriculumL0-20260902-r007`
- 저장 체크포인트: 99,992 aggregate steps / 16개 실행 인스턴스
- 100k 구간: 성공률 97.4%, 소유율 100%, 완료 시간 2.23초, 평균 제어 드리블 2.04m, 지원 간격 품질 59.0%
- 120k 구간 확인: 성공률 99.0%, 소유율 100%, 완료 시간 2.16초로 퇴행 없이 유지됐다.
- 판정: L1-A 짧은 드리블만 통과. 슛과 득점이 빠진 결과이므로 최종 L1 승급 모델로 사용하지 않는다.
- ONNX: `results/CurriculumL1-20260902-r001/Soccer4v4_Base/Soccer4v4_Base-99992.onnx`
- SHA-256: `724A17AC0C45D546109656624AE82B95C5344D6770E3C91A534720C0E6A33C8C`
- 최종 L1 학습 초기화: `-InitializeFrom "CurriculumL1-20260902-r001"`을 사용한다. `checkpoint.pt`가 99,992 스텝 상태를 보존한다.

## 실행 예시

빌드를 최신화한 다음 L0를 시작한다.

```powershell
conda activate mlagents
cd "C:\GitHub\Machine-Learning"
powershell -ExecutionPolicy Bypass -File ".\Tools\Train-Soccer.ps1" -Profile curriculum-l0 -RunId "CurriculumL0-YYYYMMDD-r001" -NumEnvs 16
```

L0가 완료되고 승급 조건을 만족하면 L1을 새 Run으로 시작한다.

```powershell
powershell -ExecutionPolicy Bypass -File ".\Tools\Train-Soccer.ps1" -Profile curriculum-l1 -RunId "CurriculumL1-YYYYMMDD-r001" -NumEnvs 8 -InitializeFrom "CurriculumL0-YYYYMMDD-r001"
```

날짜와 revision은 실제 실행일 및 중복되지 않는 번호로 바꾼다.

## 자동 실행 가능 범위

Unity Editor 조작 없이 Windows Training Player, YAML, Run ID와 포트 검증, 8/16개 인스턴스 실행, 중간 로그·TensorBoard 지표 확인, 중단된 동일 Run 재개까지 자동화할 수 있다. 다음 요청에서 명시적으로 학습 시작을 지시하면 L0 Trainer를 시작하고 상태를 추적할 수 있다. 컴퓨터 절전·재부팅, GPU 드라이버 오류, Unity 라이선스나 Conda 환경 손상처럼 운영체제 수준의 중단은 별도 복구가 필요하다.

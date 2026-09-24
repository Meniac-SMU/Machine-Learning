# L1 - Carry And Shoot

## 승인 모델 (2026-09-04)

`results/CurriculumL1-20260904-r017/Soccer4v4_Base/Soccer4v4_Base-99916.pt`와 동일 번호 ONNX를 보존한다. 최종 8m 운반→명시적 슛→실제 득점의 독립 300경기 성공률은 77.67%, L0 회귀 300경기 성공률은 99%다. 학습 최근 5요약과 고정 평가 기준을 모두 통과했다. 이 성과에는 8m 전 킥 잠금과 Neural 슈팅 기술층이 포함되어 있으며 신경망 단독 슈팅 능력을 뜻하지 않는다. 자세한 근거와 SHA는 [현재 상태](../../../../docs/soccer/current-status.md)에 기록한다. 아래 Run별 설명은 실험 이력이다.

r016 첫 100k는 소유 100%지만 기존 정책이 99.3%의 에피소드에서 즉시 킥해 운반 충족 2.35%/성공 1.11%에 그쳤으므로 진단 checkpoint를 보존하고 종료했다. 수정 후 최종 단계는 승인된 r015 `Soccer4v4_Base-500004.pt`에서 새 Run으로 초기화한다. `soccer_l1_phase=2`에서 8m 제어 운반 전 kick branch를 action mask로 잠그고 코드 기반 Neural 슈팅 기술층도 비활성화한다. 같은 선수가 잠깐 공을 놓쳤다가 재소유하면 진행을 유지하지만 다른 선수가 소유하면 다시 0m부터 시작한다. 8m 뒤의 명시적 슛과 실제 득점만 성공이다. 16 workers/최대 1m/50k 저장이며, 중단 재개에는 init_path 없는 전용 resume YAML만 사용한다.

2026-09-04부터 모든 Neural 팀에 공통 `SoccerNeuralKickAdvisor` 기술층을 적용한다. r014의 소유/킥 시도는 충분했지만 첫 슛 방향 성공이 67.46%에 머문 원인을 겨냥한다. 확정 소유·공 1.8m·골문 24m 안에서 실제 골대 안쪽 궤적이 만들어진 순간에만 킥을 권장하고, 같은 범위의 골문 밖 킥은 보류한다. Human·Rule·fallback과 골문 24m 밖에는 개입하지 않는다. 최종 phase에서는 8m 운반을 확인하기 전 기술층을 끈다. 보상·물리·Tensor 계약은 유지하며 `Soccer/Skill Advice/*`로 코드 개입 빈도를 반드시 보고한다. 새 Player에서 r014를 먼저 고정 평가한 뒤, 효과가 확인될 때만 r014에서 초기화한 하나의 후속 Run으로 학습한다.

r014는 `curriculum_l1_full_poca.yaml`로 원래 좌우 ±1.5m 배치(`soccer_l1_spawn_difficulty=1`)를 훈련한다. r013 중간 과제 승인 후보에서 초기화, 16 workers/최대 500k/50k 저장이다. 재개는 `curriculum_l1_full_resume_poca.yaml`과 `--resume`. 보상·물리는 동일하며 원래 배치 최근 5요약 + 새 seed 13579의 300회 평가 통과 후에만 최종 8m 운반 phase로 넘어간다.

r013은 `curriculum_l1_angle_poca.yaml`의 `soccer_l1_spawn_difficulty=0.5`로 중간 각도(공 좌우 ±0.75m)를 학습한다. r012 승인된 정면 준비 모델에서 초기화하고, 16 workers/최대 200k/50k 저장으로 보강한다. 보상과 물리는 동일하며 기존 r012 Player를 사용한다. 재개는 `curriculum_l1_angle_resume_poca.yaml`과 `--resume`이다. 중간 배치 성공 역시 L1 전체 완료가 아니다.

r012는 `curriculum_l1_straight_poca.yaml`로 정면 공 준비 단계를 학습한다. 보상은 r011 그대로이고 `soccer_l1_spawn_difficulty=0`만 설정한다. 0 → 0.5 → 1 순으로 공 좌우 범위를 0 → ±0.75 → ±1.5m로 확대한다. 기본값은 1, 최종 8m 운반 단계도 항상 1이다. 준비 단계 통과는 L1 통과가 아니다. 재개는 init_path 없는 `curriculum_l1_straight_resume_poca.yaml`과 `--resume`을 쓴다. 고정 모델 평가는 `--spawn-difficulty 0`과 `--spawn-difficulty 1` 결과를 구분한다.

r011 실험은 `curriculum_l1_alignment_poca.yaml`을 사용한다. r010의 불변 750k 체크포인트에서 초기화하며, 첫 슛 전 골문 방향 조준이 개선될 때만 작은 보상을 지급한다. 환경 매개변수 `soccer_l1_alignment=1`로 명시적으로 켜고 기본값은 0이다. 최종 운반 단계에는 지급하지 않는다. 물리·스폰·입력·행동·득점 성공 판정은 r010과 같다. 수치는 [보상 기준표](../../../../docs/soccer/rewards.md)를 따른다. r010은 별도 보존한다.

현재 실행 설계는 [단계별 보상 계획](../../../../docs/archive/player-curriculum/staged-reward-plan.md), 수치 변경표는 [보상 기준표](../../../../docs/soccer/rewards.md)를 따른다. r010부터 일반 전술 보상을 전부 차단하고 소유·슛·득점 위주로 학습한다. 근거리 phase는 골문 20~24m 앞이며 4m 조기 실패선은 사용하지 않는다. 소유된 공 1.8m 안에서만 킥을 선택한다. `curriculum_l1_poca.yaml`은 고정 phase 1, `curriculum_l1_carry_poca.yaml`은 고정 phase 2다. 32 worker를 시험하고 과부하/처리량에 따라 16으로 내린다. 50k마다 체크포인트를 저장한다. 승급은 개인 reward가 아닌 실제 성공률로 판단하며, 근거리 75%, 최종 65%로 완화한다.

완료된 L0 및 짧은 드리블 체크포인트를 보존한다. Navy는 비활성 상태이고 Red 네 명의 관측·행동 계약은 Base와 같다. phase별로 새 Run을 사용하며 실제 성공 지표로 승급한다.

- 제한 시간: 30초
- `ShortRangeFinish`: 골문 20~24m 앞에서 안정 소유 후 명시적으로 킥한 공으로 득점
- `CarryAndScore`: 안정 소유 후 8m 이상 운반하고 골문 안쪽을 향한 유효 슛으로 득점
- 성공 보상: 커리큘럼 팀 0.4 + 실제 슈터 개인 득점 0.4 + 완료 0.1. 일반 경기 보상은 중복 지급하지 않음
- 보조 보상: 최초 안정 소유, 제어 운반 거리(최종 단계만), 슛 시도, 골문 방향 슛, 득점, 비소유자 3명의 간격 유지. 방향 슛 보상과 실제 성공 판정은 구분한다.
- 실패 처리: 최종 단계의 운반 조건 미충족 킥과 슛 없는 득점은 성공 아님. 4m 조기 실패선은 사용하지 않음
- 권장 목표: 최대 1,000,000 aggregate steps, 50,000-step 체크포인트
- 최종 승급 기준: 최근 5개 요약에서 소유 85%, 유효 슛 시도 85%, 유효 슈팅 75%, 득점/전체 성공 65%, 평균 완료 20초 이하

생성 자산은 `Prefabs/StadiumEnvironment_CurriculumL1.prefab`과 `Scenes/Stadium4v4_CurriculumL1.unity`이다.

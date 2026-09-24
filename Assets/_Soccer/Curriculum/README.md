# Soccer 4v4 Curriculum

이 폴더는 Base 정책을 단계별로 학습하기 위한 L0~L4 전용 작업공간이다. L0·L1·L2·L2-Find·L2-Score·L3의 구현과 실험 이력을 보존한다. 현재는 L2-Find r017 미승인 종료 후 중단 상태이며 L4는 계획 단계다.

- `L0_BallApproach`: 공 접근과 0.75초 안정 소유
- `L1_CarryAndShoot`: 소유·간격 지원·근거리 슛을 거쳐 8m 운반 후 유효 슛과 득점
- `L2_ShortPass`: 짧은 패스와 재소유 구현·승인 이력 보존
- `L3_ProgressivePlay`: 전진 패스·연계 구현·실험 이력 보존
- `L4_Weak4v4`: 약한 4v4 상대를 이용한 전이 예정
- `L2_Find`: r017 step500248, 강화99% gate 미달·미승인, 후속 학습 중단
- `L2_Score`: 빈 경기장 득점 구현·중단 이력 보존
- L5: 기존 `Core/Scenes/Stadium4v4_Base_FallbackTraining.unity`
- L6: 기존 `Core/Scenes/Stadium4v4_Base.unity` Self-Play

전체 학습 순서, 승급 조건, 실행 명령과 중단 기준은 [`docs/soccer/training/curriculum.md`](../../../docs/soccer/training/curriculum.md)를 따른다.

프리팹과 씬은 직접 복제 편집하지 않고 `SoccerProjectBuilder.BuildCurriculumWorkspacesBatch`로 Core Stadium 기준본에서 재생성한다.

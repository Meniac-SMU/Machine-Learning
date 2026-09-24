# Rule FSM 공간

> 2026-09-23 공통 실행 지침: 자동 선수는 2초간 상대 골문 방향 최고 위치를 0.1m 이상 갱신하지 못하면 동료 패스를 강제로 시도한다. 자기 골문 앞에서는 골키퍼/필드 역할에 관계없이 전방 패스 또는 강한 걷어내기를 우선한다. 공통 Core의 AgentSoccer 경로에서 적용하며 팀별 보상은 변경하지 않는다. [현행 경기 계약](../../../../docs/soccer/gameplay-contract.md)을 따른다.

- 담당 폴더: `Rule_PHC`
- 논리 Type·Behavior·C# 이름: `Rule`
- 목적: 강화학습 없이 동작하는 Neural 비교 기준

## 주요 파일

| 용도 | 경로 |
| --- | --- |
| FSM | `Runtime/RuleBasedSoccerController.cs` |
| 통계 연결 | `Runtime/RuleRewardPolicy.cs` |
| 통계 Profile | `Profiles/RuleRewardProfile.asset` |
| Team | `Profiles/RuleTeamDefinition.asset` |
| 환경 | `Prefabs/StadiumEnvironment_Rule.prefab` |
| Scene | `Scenes/Stadium4v4_Rule.unity` |

행동 변화는 `ChaseLooseBall`, `CarryBall`, `PassBall`, `ShootBall`, `ClearBall`, `SupportAttack`, `PressBall`, `CoverDefense`, `RecoverGoal`의 전이·target·조향에서 만든다. 확정 carrier 한 명만 최대 `1.5초` 운반 뒤 Pass를 검토하고, Goal 위험 지역에서는 중앙 Controlled Clearance를 사용한다. Trainer, Models와 ONNX를 추가하지 않는다.

`RuleRewardProfile`은 HUD·통계용이며 FSM을 학습시키지 않는다. 공 몰림과 Rule 전용 Pass·Shoot·Clear 시점은 FSM, Keeper 공통 문제는 `SoccerDefenderKeeperRules`의 `EngageBall/RecoverGoal`, 실제 Kick 자책골 방지는 `SoccerDefensiveClearanceRules`에서 수정한다. 공통 Action·Physics·Keeper shield는 우회하지 않는다.

Goal·공 70% 크기, 후방 Ray, 적극 Keeper, Human 이동 parity와 최종 자책골 안전 계층은 Rule도 공유한다. Pass·운반 보상과 Strong Kick 패널티는 통계에 기록되지만 Rule 행동 자체는 위 FSM에서 수정한다.

절차: [Rule 팀 가이드](../../../../docs/soccer/training/rule-team.md). 통계 값: [Reward 기준표](../../../../docs/soccer/rewards.md).

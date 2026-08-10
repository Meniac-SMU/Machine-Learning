# Rule FSM 공간

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
| 환경 | `Prefabs/SoccerEnvironment_Rule.prefab` |
| Scene | `Scenes/Soccer4v4_Rule.unity` |

행동 변화는 `ChaseLooseBall`, `CarryBall`, `PassBall`, `ShootBall`, `ClearBall`, `SupportAttack`, `PressBall`, `CoverDefense`, `RecoverGoal`의 전이·target·조향에서 만든다. Trainer, Models와 ONNX를 추가하지 않는다.

`RuleRewardProfile`은 HUD·통계용이며 FSM을 학습시키지 않는다. 공 몰림은 추격자 선택·지원 target·separation, Keeper 문제는 `RecoverGoal`에서 수정한다. 공통 Action·Physics·Keeper shield는 우회하지 않는다.

절차: [Rule 팀 가이드](../../../../docs/soccer/training/rule-team.md). 통계 값: [Reward 기준표](../../../../docs/soccer/rewards.md).

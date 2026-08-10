# Attack 학습 공간

- 담당 폴더: `Attack_KMW`
- 논리 Type·Behavior·Model 접두사: `Attack`
- 목적: 감독의 공격형 ONNX 학습

## 주요 파일

| 용도 | 경로 |
| --- | --- |
| Team | `Profiles/AttackTeamDefinition.asset` |
| Reward | `Profiles/AttackRewardProfile.asset` |
| 조건부 가중 | `Runtime/AttackRewardPolicy.cs` |
| 환경 | `Prefabs/SoccerEnvironment_Attack.prefab` |
| Scene | `Scenes/Soccer4v4_Attack.unity` |
| Trainer | `Training/attack_poca.yaml` |
| 출력 Model | `Models/Attack-YYYYMMDD-vNNN.onnx` |

Blue만 Attack으로 학습하고 Purple은 승인된 Base 상대를 사용한다. Base v2 ONNX가 없으면 heuristic fallback일 수 있으므로 장기 학습 전에 확인한다.

허용 범위는 이 Profile, YAML, Versioned ONNX, TeamDefinition Model 참조와 제한된 RewardPolicy다. 선수별 Model, Purple 배선, 다른 팀, 공통 Runtime·Prefab 계약은 수정하지 않는다.

절차: [학습형 팀 가이드](../../../../docs/soccer/training/learning-teams.md). 실제 값: [Reward 기준표](../../../../docs/soccer/rewards.md).

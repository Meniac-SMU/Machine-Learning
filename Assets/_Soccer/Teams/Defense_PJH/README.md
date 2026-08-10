# Defense 학습 공간

- 담당 폴더: `Defense_PJH`
- 논리 Type·Behavior·Model 접두사: `Defense`
- 목적: 감독의 수비형 ONNX 학습

## 주요 파일

| 용도 | 경로 |
| --- | --- |
| Team | `Profiles/DefenseTeamDefinition.asset` |
| Reward | `Profiles/DefenseRewardProfile.asset` |
| 조건부 가중 | `Runtime/DefenseRewardPolicy.cs` |
| 환경 | `Prefabs/SoccerEnvironment_Defense.prefab` |
| Scene | `Scenes/Soccer4v4_Defense.unity` |
| Trainer | `Training/defense_poca.yaml` |
| 출력 Model | `Models/Defense-YYYYMMDD-vNNN.onnx` |

Blue만 Defense로 학습하고 Purple은 승인된 Base 상대를 사용한다. Keeper의 half-line 제한과 Recovery는 공통 shield이므로 Defense 전용으로 우회하지 않는다.

허용 범위는 이 Profile, YAML, Versioned ONNX, TeamDefinition Model 참조와 제한된 RewardPolicy다. 선수별 Model, Purple 배선, 다른 팀, 공통 Runtime·Prefab 계약은 수정하지 않는다.

절차: [학습형 팀 가이드](../../../../docs/soccer/training/learning-teams.md). 실제 값: [Reward 기준표](../../../../docs/soccer/rewards.md).

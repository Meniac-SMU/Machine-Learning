# Press 학습 공간

- 담당 폴더: `Press_KMG`
- 논리 Type·Behavior·Model 접두사: `Press`
- 목적: 감독의 전방 압박형 ONNX 학습

## 주요 파일

| 용도 | 경로 |
| --- | --- |
| Team | `Profiles/PressTeamDefinition.asset` |
| Reward | `Profiles/PressRewardProfile.asset` |
| 조건부 가중 | `Runtime/PressRewardPolicy.cs` |
| 환경 | `Prefabs/SoccerEnvironment_Press.prefab` |
| Scene | `Scenes/Soccer4v4_Press.unity` |
| Trainer | `Training/press_poca.yaml` |
| 출력 Model | `Models/Press-YYYYMMDD-vNNN.onnx` |

Blue만 Press로 학습하고 Purple은 승인된 Base 상대를 사용한다. 협력 압박 shaping과 teammate crowding penalty는 별도 사건이며 값을 바꾸기 전에 빈도와 cap을 확인한다.

허용 범위는 이 Profile, YAML, Versioned ONNX, TeamDefinition Model 참조와 제한된 RewardPolicy다. 선수별 Model, Purple 배선, 다른 팀, 공통 Runtime·Prefab 계약은 수정하지 않는다.

절차: [학습형 팀 가이드](../../../../docs/soccer/training/learning-teams.md). 실제 값: [Reward 기준표](../../../../docs/soccer/rewards.md).

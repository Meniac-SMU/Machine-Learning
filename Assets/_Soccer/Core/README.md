# Soccer 공통 Core

`Assets/_Soccer/Core`는 모든 Controller가 공유하는 정책·경기 계약과 Base 학습 공간이다. 팀 전용 실험을 위해 `Core/Scripts`에 분기를 추가하지 않는다.

## 진입점

| 폴더·파일 | 책임 |
| --- | --- |
| `Scripts/AgentSoccer.cs` | Observation, Action, Human 입력, 이동·Kick |
| `Scripts/SoccerEnvController.cs` | 경기 상태, Team, Reset |
| `Scripts/Core/SoccerRewardEngine.cs` | 공통 Reward 사건과 cap |
| `Scripts/Core/SoccerDefenderKeeperRules.cs` | 공통 Keeper shield |
| `Profiles` | Base RewardProfile과 TeamDefinition |
| `Prefabs/SoccerEnvironment_Base.prefab` | Base 독립 환경 |
| `Scenes/Soccer4v4_Base.unity` | Base 학습·검증 Scene |
| `Training` | 짧은 Base Trainer 연결 설정 |
| `Models` | 승인된 Base v2 ONNX |

## 수정 경계

- Base 개인 실험은 Base Profile, Trainer, Model과 TeamDefinition의 Model 참조에서 한다.
- Observation·Action·Physics·사건 판정 변경은 전체 정책 계약 변경이다.
- 공통 경기장 변경은 `SoccerProjectBuilder`를 통해 다섯 환경과 함께 검증한다.
- Base Model 변경은 Attack·Defense·Press의 Purple 기준 상대를 바꾸므로 담당자에게 공유한다.

수치는 이 README에 복제하지 않는다. [경기 계약](../../../docs/soccer/gameplay-contract.md), [Reward 기준표](../../../docs/soccer/rewards.md), [학습 운용](../../../docs/soccer/training/overview.md)을 사용한다.

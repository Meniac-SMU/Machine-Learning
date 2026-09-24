# Defense 학습 공간

> 2026-09-23 공통 실행 지침: 자동 선수는 2초간 상대 골문 방향 최고 위치를 0.1m 이상 갱신하지 못하면 동료 패스를 강제로 시도한다. 자기 골문 앞에서는 골키퍼/필드 역할에 관계없이 전방 패스 또는 강한 걷어내기를 우선한다. 공통 Core의 AgentSoccer 경로에서 적용하며 팀별 보상은 변경하지 않는다. [현행 경기 계약](../../../../docs/soccer/gameplay-contract.md)을 따른다.

- 담당 폴더: `Defense_PJH`
- 논리 Type·Behavior·Model 접두사: `Defense`
- 목적: 감독의 수비형 ONNX 학습

## 주요 파일

| 용도 | 경로 |
| --- | --- |
| Team | `Profiles/DefenseTeamDefinition.asset` |
| Reward | `Profiles/DefenseRewardProfile.asset` |
| 조건부 가중 | `Runtime/DefenseRewardPolicy.cs` |
| 환경 | `Prefabs/StadiumEnvironment_Defense.prefab` |
| Scene | `Scenes/Stadium4v4_Defense.unity` |
| Trainer | `Training/defense_poca.yaml` |
| 출력 Model | `Models/Defense-YYYYMMDD-vNNN.onnx` |

Red만 Defense로 학습하고 Navy는 승인된 Base 상대를 사용한다. Keeper의 half-line 제한, `EngageBall`과 `RecoverGoal`, Goal 안쪽 중앙 걷어내기는 공통 shield이므로 Defense 전용으로 우회하지 않는다.

Goal·공 70% 크기, 후방 Ray, Human 이동 parity, 자책골 방지, Pass·운반 보상과 무의미한 Strong Kick 패널티는 Defense 전용 보상이 아니라 모든 팀의 공통 기본기다. 새 환경에서 기존 Model의 동작을 가정하지 말고 재학습·평가한다.

허용 범위는 이 Profile, YAML, Versioned ONNX, TeamDefinition Model 참조와 제한된 RewardPolicy다. 선수별 Model, Navy 배선, 다른 팀, 공통 Runtime·Prefab 계약은 수정하지 않는다.

절차: [학습형 팀 가이드](../../../../docs/soccer/training/learning-teams.md). 실제 값: [Reward 기준표](../../../../docs/soccer/rewards.md).

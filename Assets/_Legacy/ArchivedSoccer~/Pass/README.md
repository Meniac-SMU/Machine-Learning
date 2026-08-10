# Pass 실험 보관 기록

> 상태: 2026-08-09 Rule FSM으로 대체되어 개발·학습 중단  
> 현행 비교군: `Assets/_Soccer/Teams/Rule_PHC`

- Team: `Profiles/PassTeamDefinition.asset`
- Reward: `Profiles/PassRewardProfile.asset`
- Policy: `Runtime/PassRewardPolicy.cs`
- Scene: `Scenes/Soccer4v4_Pass.unity`
- Trainer: `Training/pass_poca.yaml`

Purple은 당시 Base v2 상대를 사용할 계획이었고 초기 Reward는 Base와 같았다. 이 폴더는 Unity import 제외 역사 사본이므로 학습이나 Model 등록을 재개하지 않는다.

일부 GUID가 현행 Rule과 같을 수 있다. [Asset 구조](../../../../docs/project/asset-layout.md)에 따라 한 사본만 검토하며 `Pass`와 `Rule_PHC`를 동시에 import하지 않는다.

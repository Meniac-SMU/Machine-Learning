# Attack

- Blue definition: `Profiles/AttackTeamDefinition.asset`
- Blue reward values: `Profiles/AttackRewardProfile.asset`
- Blue training extension: `Runtime/AttackRewardPolicy.cs`
- Training scene: `Scenes/Soccer4v4_Attack.unity`
- Trainer config: `Training/attack_poca.yaml`

Purple uses the shared Base definition and, once installed, the Base v2 model. Until that model exists its slot remains empty and the opponent falls back to the heuristic controller. Initial reward values are intentionally identical to Base. Put the selected trained model in `Models` and change only the model reference in `AttackTeamDefinition` to apply it at runtime.

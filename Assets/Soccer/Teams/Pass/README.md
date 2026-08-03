# Pass

- Blue definition: `Profiles/PassTeamDefinition.asset`
- Blue reward values: `Profiles/PassRewardProfile.asset`
- Blue training extension: `Runtime/PassRewardPolicy.cs`
- Training scene: `Scenes/Soccer4v4_Pass.unity`
- Trainer config: `Training/pass_poca.yaml`

Purple uses the shared Base definition and, once installed, the Base v2 model. Until that model exists its slot remains empty and the opponent falls back to the heuristic controller. Initial reward values are intentionally identical to Base. Put the selected trained model in `Models` and change only the model reference in `PassTeamDefinition` to apply it at runtime.

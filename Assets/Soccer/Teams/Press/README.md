# Press

- Blue definition: `Profiles/PressTeamDefinition.asset`
- Blue reward values: `Profiles/PressRewardProfile.asset`
- Blue training extension: `Runtime/PressRewardPolicy.cs`
- Training scene: `Scenes/Soccer4v4_Press.unity`
- Trainer config: `Training/press_poca.yaml`

Purple uses the shared Base definition and, once installed, the Base v2 model. Until that model exists its slot remains empty and the opponent falls back to the heuristic controller. Initial reward values are intentionally identical to Base. Put the selected trained model in `Models` and change only the model reference in `PressTeamDefinition` to apply it at runtime.

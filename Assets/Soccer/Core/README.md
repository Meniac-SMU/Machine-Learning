# Soccer 4v4 Core

This folder owns the shared and competition-neutral Soccer implementation.

- `Scripts`: v2 observations and `[3,3,3,3]` actions, match rules, event tracking and reward engine.
- `Profiles`: the Base reward and team definitions.
- `Models`: the shared `Base4v4V2.onnx` slot after Base training.
- `Scenes`: the symmetric Base-versus-Base training scene.
- `Training`: the short Base MA-POCA/Self-Play configuration.

Team owners must not change movement physics, observations, action meanings, event definitions, or field rules here without a shared policy-contract review.

# L1 Completion Progress

Historical r001-r009 log. Current r010+ work is tracked only in [current status](../current-status.md); the [staged reward plan](staged-reward-plan.md) supersedes the old reward-based promotion assumptions below.

Last updated: 2026-09-02 (Asia/Seoul)

## Objective

Complete Curriculum L1 as an end-to-end attacking lesson:

1. Find and possess the ball.
2. Carry the ball under control.
3. Take a valid shot toward the Navy goal.
4. Score.

The L1 lesson is not complete when the agent only reaches or briefly dribbles the ball.

## Saved baseline

- Source run: `CurriculumL1-20260902-r001`
- Persisted checkpoint step: `99,992`
- Checkpoint: `results/CurriculumL1-20260902-r001/Soccer4v4_Base/checkpoint.pt`
- ONNX: `results/CurriculumL1-20260902-r001/Soccer4v4_Base/Soccer4v4_Base-99992.onnx`
- ONNX SHA-256: `724A17AC0C45D546109656624AE82B95C5344D6770E3C91A534720C0E6A33C8C`
- Baseline result: short controlled carry is learned; shooting and scoring are not yet part of the success gate.

## Development checkpoints

- [x] P0: Preserve and record the r001 baseline.
- [x] P1: Implement L1 short-range shooting and final carry-to-score phases.
- [x] P2: Add per-team shot attempt, shot-on-target, goal, and final-success metrics.
- [x] P3: Regenerate scenes/configuration and pass Unity compile/EditMode/PlayMode checks.
- [x] P4: Build the Windows headless training player.
- [ ] P5: Train with 16 parallel environments, saving every 100,000 steps.
- [ ] P6: Iterate environment/rewards if the promotion gate fails.
- [ ] P7: Verify the final model and document its exact checkpoint and SHA-256.

## Final promotion gate

The recent evaluation window must satisfy all of the following, rather than relying on combined reward alone:

- Possession success: at least 85%.
- Valid shot attempt: at least 85%.
- Shot on target: at least 75%.
- Goal and full L1 success: at least 70%.
- Mean completion time: at most 20 seconds.
- Stability: the gate holds across five consecutive summaries.
- Regression: L0 possession remains at least 85% in a separate check.

## Save policy

- Source, documentation, generated Unity assets, and configuration changes are written to disk after each development checkpoint.
- Training uses `checkpoint_interval: 100000`; a saved checkpoint is required before stopping or changing a run.
- No Git commit or push is created without separate approval.

## Verification checkpoint P3

- Trainer YAML strict parse: passed; both `ShortRangeFinish` and `CarryAndScore` lessons loaded.
- Unity batch compile: passed.
- Curriculum prefab/scene regeneration and validation: passed.
- EditMode: 73/73 passed.
- Soccer PlayMode: 18/18 passed.
- Full-project PlayMode note: three unrelated legacy Escape tests fail because `EscapePrototype` is absent from the active build scene list; no Soccer test failed.

## Build checkpoint P4

- Executable: `Builds/SoccerTraining/SoccerTraining.exe`
- Executable SHA-256: `D3DB57B9B88A2D8FEDD1621461DDD7B29ED18001EFD29A93EF9F31D4E8A483CD`
- Profile manifest SHA-256: `5ED7CB1D1B0104F916B7948E2E02E626C9EC545938661606C201FEDE1E7D814F`
- L1 profile: `Curriculum L1 - Carry And Shoot`
- Preflight: 16 workers, ports 5605-5620, 1,000,000 aggregate steps, and r001 initialization all validated.

## Diagnostic run r002

- Run: `CurriculumL1-20260902-r002`
- Stopped at 43,948 aggregate steps after the first diagnostic summaries.
- Saved ONNX: `results/CurriculumL1-20260902-r002/Soccer4v4_Base/Soccer4v4_Base-43948.onnx`
- Finding: invalid goals could reset the ball and accumulate ordinary match goal rewards inside one 30-second episode, producing mean group reward 4.060 and risking a false curriculum promotion.
- Correction: every goal now ends the drill immediately; ordinary match goal rewards are disabled in curriculum reward mode, and only a valid carry-shot-goal receives the 0.4 curriculum group success reward. The short-finish transition threshold is therefore 0.32, corresponding to about 80% valid success.

## Diagnostic run r003

- Run: `CurriculumL1-20260902-r003`
- Persisted interruption checkpoint and export: 219,548 aggregate steps.
- Checkpoint SHA-256: `E48D6F77ED551C66C5901D6BF1EA46E8FD68B554ED39C9E05670B8859CA009AB`
- ONNX SHA-256: `CF86C5A74F2792019DAC52C4381A186598F8B171C45BD265C92CC73864CEE1C6`
- Recent five-summary means near 210k: possession 25.5%, shot attempt 19.4%, shot on target 5.9%, valid shot and success 0.65%, any Red goal 77.0%, episode 7.60 seconds.
- Finding: the r001 forward-carry policy could push the ball across the goal line from the 10-16m short-finish spawn before stable possession and an explicit kick were learned.
- Correction for r004: move the short-finish focus spawn to 20-26m from goal while preserving the 1m carry bridge and the final 30-36m spawn.

## Ready-to-resume checkpoint r004

- The 20-26m short-finish correction is saved in source.
- Corrected Windows headless build: succeeded.
- r004 preflight: passed for 16 workers, ports 5605-5620, and initialization from r003.
- Training process start: blocked by the Codex execution-approval usage gate before any r004 process or result directory was created.
- Resume source: `results/CurriculumL1-20260902-r003/Soccer4v4_Base/checkpoint.pt` at 219,548 steps.
- Intended next Run: `CurriculumL1-20260902-r004` initialized from `CurriculumL1-20260902-r003`.
- No training process is currently running.

## Diagnostic run r004

- Run: `CurriculumL1-20260902-r004`
- Persisted interruption checkpoint and export: 217,944 aggregate steps.
- Recent five-summary means near 210k: possession 62.6%, shot attempt 47.1%, shot on target 14.3%, valid shot and success 9.9%, episode 14.44 seconds.
- Finding: the 20-26m spawn removed much of the push-in shortcut and improved possession and attempts, but aiming remained the bottleneck.
- Correction for r005: award the capped individual shot-on-target reward after stable possession even before the 1m carry requirement is complete. The valid-shot and success gates still require the full carry distance, and automatic curriculum progression still depends only on group success reward.

## Diagnostic run r005

- Run: `CurriculumL1-20260902-r005`
- Persisted interruption checkpoint and export: 120,852 aggregate steps.
- Early 30k success reached 22.2%, but the recent five-summary mean near 100k fell to 5.7%; possession remained 73.6% and shot attempts 48.5%.
- Finding: instantaneous projected kick direction did not reliably match the actual ball trajectory because existing velocity and physical deflection also affect the result.
- Correction for r006: make `ShortRangeFinish` a pure shooting bridge with no new carry-distance requirement; a post-possession explicit kick followed by an actual goal is a valid shot and an on-target result. `CarryAndScore` still requires the full 8m controlled carry before the explicit scoring kick.

## Diagnostic run r006

- Run: `CurriculumL1-20260902-r006`
- Persisted interruption checkpoint and export: 330,880 aggregate steps.
- Near 300k, recent five-summary means were possession 96.0%, shot attempt 56.6%, actual valid scoring shot 16.6%, and episode 15.17 seconds.
- Finding: short-finish curriculum dribble rewards accumulated far more frequently than shot rewards, reinforcing the old push-carry behavior even though short-finish no longer required new carry distance.
- Correction for r007: continue measuring dribble progress in the short-finish phase but award no curriculum dribble-distance reward there. Restore the capped dribble reward automatically in the final `CarryAndScore` phase, where the 8m carry is required.

## Diagnostic run r007

- Run: `CurriculumL1-20260902-r007`
- Persisted interruption checkpoint and export: 74,980 aggregate steps.
- Recent five-summary mean near 60k: possession 97.2%, shot attempt 47.6%, valid scoring shot 12.3%, episode 14.17 seconds. The 70k group reward briefly reached 0.212 but had not yet shown stable promotion performance.
- Finding: removing short-phase dribble reward preserved possession but did not make the first shooting problem sufficiently easy.
- Correction for r008: place the short-finish agent 8-12m from goal and terminate the episode as a failure when the ball crosses within 4m of goal without a valid explicit shot. Apply the same anti-push finish line to the final phase.

## Diagnostic run r008

- Run: `CurriculumL1-20260902-r008`
- Persisted interruption checkpoint and export: 25,212 aggregate steps.
- At 20k, possession was 3.5%, shot attempt 0.6%, success 0%, and mean episode time 0.75 seconds.
- Finding: the focus agent's 8-12m placement plus the ball's additional 1-3m forward offset left too little space before the 4m anti-push line, terminating episodes before stable possession.
- Correction for r009: move the focus agent to 12-16m from goal and apply the anti-push line only after stable possession has been established.

## Diagnostic run r009

- Run: `CurriculumL1-20260902-r009`, initialized from the preserved r007 checkpoint rather than the regressed r008 policy.
- Persisted interruption checkpoint and export: 65,626 aggregate steps.
- Saved checkpoint: `results/CurriculumL1-20260902-r009/Soccer4v4_Base/checkpoint.pt` (`SHA-256 3FF16050D3A36D11E365033BD9583F4EEB6758287F75B3347EBB3D78D8C093F5`).
- Saved ONNX: `results/CurriculumL1-20260902-r009/Soccer4v4_Base.onnx` and `Soccer4v4_Base/Soccer4v4_Base-65626.onnx` (`SHA-256 6DFB63A613B5CABFCE95C71179D297BF8DD29A7803967C006D631F3188E4A2B2`).
- Six-summary mean from 10k through 60k: possession 64.47%, shot attempt 3.90%, shot on target 1.01%, valid shot 3.76%, actual Red goal 36.57%, valid scoring success 0.87%, episode 2.25 seconds, and group reward 0.00365.
- Final 60k summary: possession 69.57%, shot attempt/valid shot 4.35%, shot on target 0%, actual Red goal 31.52%, valid scoring success 0%, episode 2.85 seconds, and group reward 0.00021.
- Result: r009 did not meet the `ShortRangeFinish` promotion gate. The high ordinary-goal rate alongside near-zero explicit scoring success shows that the policy still primarily pushed the ball across the line instead of learning a reliable kick-and-score action. It never advanced to `CarryAndScore` (`lesson_num: 0`).
- Integrity: all 16 Player logs were retained and contained no error, exception, crash, disconnect, or timeout match. The interruption exported both `.pt` and `.onnx` artifacts normally.
- Work stopped by user request after r009 analysis. No r010 run or result directory was created. The briefly prepared r010 source changes were reverted, leaving the saved r009 source state in place.

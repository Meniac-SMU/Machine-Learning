using System.Collections;
using System.Linq;
using NUnit.Framework;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Unity.MLAgents;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MachineLearning.Soccer.Manager.Tests
{
    public sealed class MNG_RuntimePlayModeTests : IPrebuildSetup, IPostBuildCleanup
    {
        const string ScenePath = "Assets/_Soccer/Manager/Scenes/MNG_Stadium4v4.unity";
        const string AttackChoiceScenePath =
            "Assets/_Soccer/Manager/Curriculum/M1_AttackChoice/Scenes/MNG_M1_AttackChoice.unity";
        const string DefenseChoiceScenePath =
            "Assets/_Soccer/Manager/Curriculum/M2_DefenseChoice/Scenes/MNG_M2_DefenseChoice.unity";
        const string AttackMovingScenePath =
            "Assets/_Soccer/Manager/Curriculum/M1_AttackMoving/Scenes/MNG_M1_AttackMoving.unity";
        const string FallbackMatch60ScenePath =
            "Assets/_Soccer/Manager/Curriculum/M3_FallbackMatch/Scenes/MNG_M3_FallbackMatch60.unity";
        const string FallbackMatch300ScenePath =
            "Assets/_Soccer/Manager/Curriculum/M3_FallbackMatch/Scenes/MNG_M3_FallbackMatch300.unity";
#if UNITY_EDITOR
#endif

        public void Setup()
        {
#if UNITY_EDITOR
            var originalScenes = EditorBuildSettings.scenes;
            var requiredScenes = new[]
            {
                ScenePath,
                AttackChoiceScenePath,
                AttackMovingScenePath,
                DefenseChoiceScenePath,
                FallbackMatch60ScenePath,
                FallbackMatch300ScenePath
            }
                .Where(path => originalScenes.All(scene => scene.path != path))
                .Select(path => new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = originalScenes
                .Concat(requiredScenes)
                .ToArray();
#endif
        }

        public void Cleanup()
        {
#if UNITY_EDITOR
            EditorBuildSettings.scenes = EditorBuildSettings.scenes
                .Where(scene => scene.path != ScenePath
                    && scene.path != AttackChoiceScenePath
                    && scene.path != AttackMovingScenePath
                    && scene.path != DefenseChoiceScenePath
                    && scene.path != FallbackMatch60ScenePath
                    && scene.path != FallbackMatch300ScenePath)
                .ToArray();
#endif
        }

        [TearDown]
        public void RestoreAcademyAutomaticStepping()
        {
            if (Academy.IsInitialized)
                Academy.Instance.AutomaticSteppingEnabled = true;
        }

        [UnityTest]
        public IEnumerator M1AttackChoiceSceneStartsOwnedScenarioAndEndsOnGoal()
        {
            Academy.Instance.AutomaticSteppingEnabled = false;
            yield return SceneManager.LoadSceneAsync(AttackChoiceScenePath, LoadSceneMode.Single);
            var configuredTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            yield return null;

            var match = Object.FindFirstObjectByType<MNG_MatchController>();
            var curriculum = Object.FindFirstObjectByType<MNG_CurriculumController>();
            var ballControl = Object.FindFirstObjectByType<MNG_BallControl>();
            var manager = Object.FindObjectsByType<MNG_ManagerAgent>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Single(candidate => candidate.Team == Team.Red);
            manager.GetComponent<DecisionRequester>().enabled = false;
            manager.enabled = false;
            Academy.Instance.AutomaticSteppingEnabled = true;
            Time.timeScale = configuredTimeScale;

            Assert.That(curriculum, Is.Not.Null);
            Assert.That(curriculum.Stage, Is.EqualTo(MNG_CurriculumStage.M1AttackChoice));
            Assert.That(match.ConfiguredMatchDurationSeconds, Is.EqualTo(20f));
            Assert.That(Object.FindObjectsByType<MNG_FallbackManager>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                .All(fallback => !fallback.enabled), Is.True);
            var goalDistance = match.Snapshot.FieldHalfLength - curriculum.CurrentScenario.BallPosition.x;
            Assert.That(goalDistance, Is.InRange(20f, 35f));

            for (var step = 0; step < 8 && !ballControl.Carrier.IsValid; step++)
                yield return new WaitForFixedUpdate();
            Assert.That(ballControl.Carrier.IsValid, Is.True);
            Assert.That(ballControl.Carrier.Team, Is.EqualTo(Team.Red));
            Assert.That(ballControl.Carrier.Slot, Is.EqualTo(curriculum.CurrentScenario.CarrierSlot));

            var completedBefore = curriculum.CompletedEpisodes;
            Assert.That(match.GoalTouched(Team.Red), Is.True);
            yield return new WaitForFixedUpdate();
            Assert.That(curriculum.CompletedEpisodes, Is.EqualTo(completedBefore + 1));
            Assert.That(curriculum.LastOutcome, Is.EqualTo(MNG_CurriculumOutcome.Goal));
            Assert.That(curriculum.EpisodeIndex, Is.EqualTo(1));
            Assert.That(match.State, Is.EqualTo(MNG_MatchState.Playing));
            Assert.That(match.RedScore, Is.Zero);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator M1RandomValidCommandBaselineRunsFixedOneHundredScenarios()
        {
            Academy.Instance.AutomaticSteppingEnabled = false;
            yield return SceneManager.LoadSceneAsync(AttackChoiceScenePath, LoadSceneMode.Single);
            var configuredTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            yield return null;

            var match = Object.FindFirstObjectByType<MNG_MatchController>();
            var curriculum = Object.FindFirstObjectByType<MNG_CurriculumController>();
            var manager = Object.FindObjectsByType<MNG_ManagerAgent>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Single(candidate => candidate.Team == Team.Red);
            manager.GetComponent<DecisionRequester>().enabled = false;
            manager.enabled = false;
            Academy.Instance.AutomaticSteppingEnabled = true;
            Time.timeScale = configuredTimeScale;
            curriculum.SetValidationScenarios(true);

            var outcomes = new int[4];
            curriculum.EpisodeCompleted += (_, outcome) => outcomes[(int)outcome]++;
            curriculum.BeginScenario(0);
            var random = new System.Random(MNG_CurriculumCatalog.M1ValidationSeed);
            var mask = new bool[MNG_CommandMask.CommandCount];
            var valid = new int[MNG_CommandMask.CommandCount];
            var decisionTicks = 0;
            var safetyTicks = 0;
            while (curriculum.CompletedEpisodes < MNG_CurriculumCatalog.M1EvaluationScenarioCount
                && safetyTicks < 110000)
            {
                if (decisionTicks <= 0 && match.IsPlayActive
                    && match.TryGetSnapshot(out var snapshot))
                {
                    MNG_CommandMask.Write(snapshot, Team.Red, match.GetDecisionState(Team.Red), mask);
                    var validCount = 0;
                    for (var command = 0; command < mask.Length; command++)
                    {
                        if (mask[command]) valid[validCount++] = command;
                    }
                    Assert.That(validCount, Is.GreaterThanOrEqualTo(2));
                    var selected = (MNG_Command)valid[random.Next(validCount)];
                    Assert.That(match.AcceptCommand(Team.Red, selected), Is.True);
                    decisionTicks = MNG_ManagerAgent.DecisionPeriod;
                }

                decisionTicks--;
                safetyTicks++;
                yield return new WaitForFixedUpdate();
            }

            Debug.Log($"MNG M1 RANDOM BASELINE scenarios={curriculum.CompletedEpisodes} "
                + $"goals={outcomes[(int)MNG_CurriculumOutcome.Goal]} "
                + $"ownGoals={outcomes[(int)MNG_CurriculumOutcome.OwnGoal]} "
                + $"timeouts={outcomes[(int)MNG_CurriculumOutcome.Timeout]} ticks={safetyTicks}");
            Assert.That(curriculum.CompletedEpisodes,
                Is.EqualTo(MNG_CurriculumCatalog.M1EvaluationScenarioCount));
            Assert.That(outcomes[(int)MNG_CurriculumOutcome.OwnGoal], Is.Zero);
        }

        [UnityTest]
        public IEnumerator M1MovingDefenseSceneEnablesOnlyNavyFallbackAndExecutors()
        {
            Academy.Instance.AutomaticSteppingEnabled = false;
            yield return SceneManager.LoadSceneAsync(AttackMovingScenePath, LoadSceneMode.Single);
            var configuredTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            yield return null;

            var curriculum = Object.FindFirstObjectByType<MNG_CurriculumController>();
            var fallbacks = Object.FindObjectsByType<MNG_FallbackManager>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            var redManager = Object.FindObjectsByType<MNG_ManagerAgent>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Single(manager => manager.Team == Team.Red);
            redManager.GetComponent<DecisionRequester>().enabled = false;
            redManager.enabled = false;
            Academy.Instance.AutomaticSteppingEnabled = true;
            Time.timeScale = configuredTimeScale;
            Assert.That(curriculum.Stage, Is.EqualTo(MNG_CurriculumStage.M1AttackChoice));
            Assert.That(curriculum.UsesMovingNavyDefense, Is.True);
            Assert.That(fallbacks.Single(fallback => fallback.Team == Team.Red).enabled, Is.False);
            Assert.That(fallbacks.Single(fallback => fallback.Team == Team.Navy).enabled, Is.True);
            Assert.That(Object.FindObjectsByType<MNG_PlayerAvatar>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(avatar => avatar.Team == Team.Navy)
                .All(avatar => avatar.GetComponent<MNG_PlayerSkillExecutor>().enabled), Is.True);

            var navyPlayers = Object.FindObjectsByType<MNG_PlayerAvatar>(
                    FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(avatar => avatar.Team == Team.Navy)
                .ToArray();
            var startingPositions = navyPlayers.Select(avatar => avatar.Body.position).ToArray();
            for (var step = 0; step < 30; step++) yield return new WaitForFixedUpdate();
            Assert.That(navyPlayers.Where((avatar, index) =>
                    Vector3.Distance(avatar.Body.position, startingPositions[index]) > 0.25f)
                .Any(), Is.True, "At least one Navy fallback player must actually move.");
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator M1MovingDefenseRandomBaselineRunsFixedOneHundredScenarios()
        {
            Academy.Instance.AutomaticSteppingEnabled = false;
            yield return SceneManager.LoadSceneAsync(AttackMovingScenePath, LoadSceneMode.Single);
            var configuredTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            yield return null;

            var match = Object.FindFirstObjectByType<MNG_MatchController>();
            var curriculum = Object.FindFirstObjectByType<MNG_CurriculumController>();
            var manager = Object.FindObjectsByType<MNG_ManagerAgent>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Single(candidate => candidate.Team == Team.Red);
            manager.GetComponent<DecisionRequester>().enabled = false;
            manager.enabled = false;
            Academy.Instance.AutomaticSteppingEnabled = true;
            Time.timeScale = configuredTimeScale;
            curriculum.SetValidationScenarios(true);
            curriculum.BeginScenario(0);

            var outcomes = new int[4];
            curriculum.EpisodeCompleted += (_, outcome) => outcomes[(int)outcome]++;
            var random = new System.Random(MNG_CurriculumCatalog.M1ValidationSeed);
            var mask = new bool[MNG_CommandMask.CommandCount];
            var valid = new int[MNG_CommandMask.CommandCount];
            var decisionTicks = 0;
            var safetyTicks = 0;
            while (curriculum.CompletedEpisodes < MNG_CurriculumCatalog.M1EvaluationScenarioCount
                && safetyTicks < 110000)
            {
                if (decisionTicks <= 0 && match.IsPlayActive && match.TryGetSnapshot(out var snapshot))
                {
                    MNG_CommandMask.Write(snapshot, Team.Red, match.GetDecisionState(Team.Red), mask);
                    var validCount = 0;
                    for (var command = 0; command < mask.Length; command++)
                        if (mask[command]) valid[validCount++] = command;
                    Assert.That(validCount, Is.GreaterThanOrEqualTo(2));
                    Assert.That(match.AcceptCommand(
                        Team.Red, (MNG_Command)valid[random.Next(validCount)]), Is.True);
                    decisionTicks = MNG_ManagerAgent.DecisionPeriod;
                }
                decisionTicks--;
                safetyTicks++;
                yield return new WaitForFixedUpdate();
            }

            Debug.Log($"MNG M1 MOVING RANDOM BASELINE scenarios={curriculum.CompletedEpisodes} "
                + $"goals={outcomes[(int)MNG_CurriculumOutcome.Goal]} "
                + $"ownGoals={outcomes[(int)MNG_CurriculumOutcome.OwnGoal]} "
                + $"timeouts={outcomes[(int)MNG_CurriculumOutcome.Timeout]} ticks={safetyTicks}");
            Assert.That(curriculum.CompletedEpisodes,
                Is.EqualTo(MNG_CurriculumCatalog.M1EvaluationScenarioCount));
        }

        [UnityTest]
        public IEnumerator M2DefenseSceneStartsWithRedPolicyAndNavyFallback()
        {
            Academy.Instance.AutomaticSteppingEnabled = false;
            yield return SceneManager.LoadSceneAsync(DefenseChoiceScenePath, LoadSceneMode.Single);
            var configuredTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            yield return null;

            var match = Object.FindFirstObjectByType<MNG_MatchController>();
            var curriculum = Object.FindFirstObjectByType<MNG_CurriculumController>();
            var managers = Object.FindObjectsByType<MNG_ManagerAgent>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            var fallbacks = Object.FindObjectsByType<MNG_FallbackManager>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            Assert.That(curriculum.Stage, Is.EqualTo(MNG_CurriculumStage.M2DefenseChoice));
            Assert.That(match.ConfiguredMatchDurationSeconds, Is.EqualTo(20f));
            Assert.That(managers.Count(manager => manager.isActiveAndEnabled), Is.EqualTo(1));
            var activeManager = managers.Single(manager => manager.isActiveAndEnabled);
            Assert.That(activeManager.Team, Is.EqualTo(Team.Red));
            Assert.That(fallbacks.Single(fallback => fallback.Team == Team.Red).enabled, Is.False);
            Assert.That(fallbacks.Single(fallback => fallback.Team == Team.Navy).enabled, Is.True);
            Assert.That(curriculum.CurrentDefenseScenario.NavyCarrierSlot, Is.EqualTo(3));

            activeManager.GetComponent<DecisionRequester>().enabled = false;
            activeManager.enabled = false;
            Academy.Instance.AutomaticSteppingEnabled = true;
            Time.timeScale = configuredTimeScale;

            for (var step = 0; step < 30 && match.Snapshot.Possession == MNG_Possession.Neutral; step++)
                yield return new WaitForFixedUpdate();
            Assert.That(match.Snapshot.Possession, Is.EqualTo(MNG_Possession.Navy));
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator M2RandomValidCommandBaselineRunsFixedOneHundredScenarios()
        {
            Academy.Instance.AutomaticSteppingEnabled = false;
            yield return SceneManager.LoadSceneAsync(DefenseChoiceScenePath, LoadSceneMode.Single);
            var configuredTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            yield return null;

            var match = Object.FindFirstObjectByType<MNG_MatchController>();
            var curriculum = Object.FindFirstObjectByType<MNG_CurriculumController>();
            var manager = Object.FindObjectsByType<MNG_ManagerAgent>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Single(candidate => candidate.Team == Team.Red);
            manager.GetComponent<DecisionRequester>().enabled = false;
            manager.enabled = false;
            Academy.Instance.AutomaticSteppingEnabled = true;
            Time.timeScale = configuredTimeScale;
            curriculum.SetValidationScenarios(true);
            curriculum.BeginScenario(0);

            var outcomes = new int[4];
            curriculum.EpisodeCompleted += (_, outcome) => outcomes[(int)outcome]++;
            var random = new System.Random(MNG_CurriculumCatalog.M2ValidationSeed);
            var mask = new bool[MNG_CommandMask.CommandCount];
            var valid = new int[MNG_CommandMask.CommandCount];
            var decisionTicks = 0;
            var safetyTicks = 0;
            while (curriculum.CompletedEpisodes < MNG_CurriculumCatalog.M2EvaluationScenarioCount
                && safetyTicks < 110000)
            {
                if (decisionTicks <= 0 && match.IsPlayActive
                    && match.TryGetSnapshot(out var snapshot))
                {
                    MNG_CommandMask.Write(snapshot, Team.Red, match.GetDecisionState(Team.Red), mask);
                    var validCount = 0;
                    for (var command = 0; command < mask.Length; command++)
                    {
                        if (mask[command]) valid[validCount++] = command;
                    }
                    Assert.That(validCount, Is.GreaterThanOrEqualTo(2));
                    Assert.That(match.AcceptCommand(
                        Team.Red, (MNG_Command)valid[random.Next(validCount)]), Is.True);
                    decisionTicks = MNG_ManagerAgent.DecisionPeriod;
                }
                decisionTicks--;
                safetyTicks++;
                yield return new WaitForFixedUpdate();
            }

            Debug.Log($"MNG M2 RANDOM BASELINE scenarios={curriculum.CompletedEpisodes} "
                + $"fastRecoveries={curriculum.RecoveriesWithinDeadline} "
                + $"allRecoveries={curriculum.Recoveries} "
                + $"conceded={outcomes[(int)MNG_CurriculumOutcome.OwnGoal]} "
                + $"redGoals={outcomes[(int)MNG_CurriculumOutcome.Goal]} "
                + $"timeouts={outcomes[(int)MNG_CurriculumOutcome.Timeout]} ticks={safetyTicks}");
            Assert.That(curriculum.CompletedEpisodes,
                Is.EqualTo(MNG_CurriculumCatalog.M2EvaluationScenarioCount));
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator M3SixtySecondCollectionUsesInterruptedMatchesAndMixedStarts()
        {
            Academy.Instance.AutomaticSteppingEnabled = false;
            yield return SceneManager.LoadSceneAsync(FallbackMatch60ScenePath, LoadSceneMode.Single);
            var configuredTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            yield return null;

            var match = Object.FindFirstObjectByType<MNG_MatchController>();
            var controller = Object.FindFirstObjectByType<MNG_FallbackMatchController>();
            var manager = Object.FindObjectsByType<MNG_ManagerAgent>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Single(candidate => candidate.Team == Team.Red);
            manager.GetComponent<DecisionRequester>().enabled = false;
            manager.enabled = false;
            Academy.Instance.AutomaticSteppingEnabled = true;
            Time.timeScale = configuredTimeScale;

            Assert.That(controller.Stage, Is.EqualTo(MNG_CurriculumStage.M3FallbackMatch));
            Assert.That(controller.IsFullMatch, Is.False);
            Assert.That(match.ConfiguredMatchDurationSeconds, Is.EqualTo(60f));
            Assert.That(match.FinishMode, Is.EqualTo(MNG_MatchFinishMode.InterruptedCollection));
            var firstKind = controller.CurrentScenario.Kind;
            while (controller.CompletedMatches < 1)
                yield return new WaitForFixedUpdate();
            Assert.That(controller.MatchIndex, Is.EqualTo(1));
            Assert.That(controller.CurrentScenario.Kind, Is.EqualTo(firstKind));
        }

        [UnityTest]
        public IEnumerator M3FullMatchUsesThreeHundredSecondTerminalResult()
        {
            Academy.Instance.AutomaticSteppingEnabled = false;
            yield return SceneManager.LoadSceneAsync(FallbackMatch300ScenePath, LoadSceneMode.Single);
            var configuredTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            yield return null;

            var match = Object.FindFirstObjectByType<MNG_MatchController>();
            var controller = Object.FindFirstObjectByType<MNG_FallbackMatchController>();
            var manager = Object.FindObjectsByType<MNG_ManagerAgent>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Single(candidate => candidate.Team == Team.Red);
            manager.GetComponent<DecisionRequester>().enabled = false;
            manager.enabled = false;
            Academy.Instance.AutomaticSteppingEnabled = true;
            Time.timeScale = configuredTimeScale;

            Assert.That(controller.IsFullMatch, Is.True);
            Assert.That(match.ConfiguredMatchDurationSeconds, Is.EqualTo(300f));
            Assert.That(match.FinishMode, Is.EqualTo(MNG_MatchFinishMode.TerminalResult));
        }

        [UnityTest]
        public IEnumerator M0SceneRunsFallbackThroughManagerRuntimeWithoutLegacyWriters()
        {
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            yield return null;

            var match = Object.FindFirstObjectByType<MNG_MatchController>();
            Assert.That(match, Is.Not.Null);
            Assert.That(Object.FindObjectsByType<AgentSoccer>(
                FindObjectsInactive.Include, FindObjectsSortMode.None), Is.Empty);
            Assert.That(Object.FindObjectsByType<MNG_PlayerAvatar>(
                FindObjectsInactive.Include, FindObjectsSortMode.None), Has.Length.EqualTo(8));
            Assert.That(Object.FindObjectsByType<MNG_PlayerMotor>(
                FindObjectsInactive.Include, FindObjectsSortMode.None), Has.Length.EqualTo(8));
            Assert.That(Object.FindObjectsByType<MNG_FallbackManager>(
                FindObjectsInactive.Include, FindObjectsSortMode.None), Has.Length.EqualTo(2));
            var hud = Object.FindFirstObjectByType<MNG_HudPresenter>(FindObjectsInactive.Include);
            Assert.That(hud, Is.Not.Null);
            Assert.That(hud.MatchController, Is.SameAs(match));
            Assert.That(Object.FindFirstObjectByType<SoccerHudController>(FindObjectsInactive.Include), Is.Null);
            Assert.That(Object.FindObjectsByType<MNG_ManagerAgent>(
                FindObjectsInactive.Include, FindObjectsSortMode.None)
                .All(manager => !manager.gameObject.activeInHierarchy), Is.True);
            var policyAgents = Object.FindObjectsByType<Agent>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.That(policyAgents, Has.Length.EqualTo(2));
            Assert.That(policyAgents.All(agent => agent is MNG_ManagerAgent), Is.True);

            var initialTick = match.Snapshot.TickId;
            for (var i = 0; i < 30; i++) yield return new WaitForFixedUpdate();

            Assert.That(match.State, Is.EqualTo(MNG_MatchState.Playing));
            Assert.That(match.Snapshot.TickId, Is.GreaterThan(initialTick));
            Assert.That(match.MatchRemainingSeconds, Is.LessThan(300f));
            foreach (var fallback in Object.FindObjectsByType<MNG_FallbackManager>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                Assert.That(fallback.LastDecision.Reason, Is.Not.Null.And.Not.Empty);
            Assert.That(Object.FindFirstObjectByType<MNG_BallControl>().GetComponent<Rigidbody>().mass,
                Is.EqualTo(4.5f).Within(0.0001f));
            Assert.DoesNotThrow(() => match.GoalTouched(Team.Red));
            Assert.That(match.RedScore, Is.EqualTo(1));
            Assert.That(match.State, Is.EqualTo(MNG_MatchState.GoalPause));
        }

        [UnityTest]
        public IEnumerator KeeperBackpedalsWhileContinuingToFaceTheBall()
        {
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            yield return null;

            var match = Object.FindFirstObjectByType<MNG_MatchController>();
            foreach (var fallback in Object.FindObjectsByType<MNG_FallbackManager>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                fallback.enabled = false;
            var keeper = Object.FindObjectsByType<MNG_PlayerAvatar>(
                    FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Single(avatar => avatar.Team == Team.Red && avatar.Role == MNG_PlayerRole.Keeper);
            var executor = keeper.GetComponent<MNG_PlayerSkillExecutor>();
            var ball = match.Snapshot.BallPosition;
            var position = new Vector2(keeper.Body.position.x, keeper.Body.position.z);
            var towardBall = (ball - position).normalized;
            executor.SetTask(new MNG_PlayerTask
            {
                Skill = MNG_PlayerSkill.KeeperHome,
                Target = position - towardBall * 5f,
                Revision = 10000,
                ExpirySeconds = match.EpisodeElapsedSeconds + 5f
            });

            for (var step = 0; step < 20; step++) yield return new WaitForFixedUpdate();

            var planarVelocity = new Vector2(keeper.Body.linearVelocity.x, keeper.Body.linearVelocity.z);
            var facing = new Vector2(keeper.transform.forward.x, keeper.transform.forward.z).normalized;
            Assert.That(Vector2.Dot(planarVelocity.normalized, towardBall), Is.LessThan(-0.75f),
                "The keeper should be able to retreat away from the ball.");
            Assert.That(Vector2.Dot(facing, towardBall), Is.GreaterThan(0.85f),
                "Retreating must not turn the keeper's back to the ball.");
        }

        [UnityTest]
        public IEnumerator ContestedStationaryBallAssignsOppositeLateralEscapeRoutes()
        {
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            yield return null;

            var match = Object.FindFirstObjectByType<MNG_MatchController>();
            var ballControl = Object.FindFirstObjectByType<MNG_BallControl>();
            var ball = ballControl.GetComponent<Rigidbody>();
            var avatars = Object.FindObjectsByType<MNG_PlayerAvatar>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var fallback in Object.FindObjectsByType<MNG_FallbackManager>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                fallback.enabled = false;

            var red = avatars.Single(avatar => avatar.Team == Team.Red && avatar.Slot == 3);
            var navy = avatars.Single(avatar => avatar.Team == Team.Navy && avatar.Slot == 3);
            foreach (var avatar in avatars.Where(avatar => avatar != red && avatar != navy))
                SetPlayerPose(avatar, new Vector3(
                    avatar.Team == Team.Red ? -30f : 30f,
                    0.52f,
                    avatar.Slot * 5f - 8f), avatar.Team == Team.Red ? Vector3.right : Vector3.left);
            SetPlayerPose(red, new Vector3(-2.4f, 0.52f, 0f), Vector3.right);
            SetPlayerPose(navy, new Vector3(2.4f, 0.52f, 0f), Vector3.left);
            ball.position = new Vector3(0f, ball.position.y, 0f);
            ball.linearVelocity = Vector3.zero;
            ball.angularVelocity = Vector3.zero;
            Physics.SyncTransforms();

            foreach (var avatar in new[] { red, navy })
            {
                var current = new Vector2(avatar.Body.position.x, avatar.Body.position.z);
                avatar.GetComponent<MNG_PlayerSkillExecutor>().SetTask(new MNG_PlayerTask
                {
                    Skill = MNG_PlayerSkill.MoveTo,
                    Target = current,
                    Revision = 10000,
                    ExpirySeconds = match.EpisodeElapsedSeconds + 5f
                });
            }

            for (var step = 0; step < 40 && !ballControl.IsContestedEscapeActive; step++)
                yield return new WaitForFixedUpdate();
            Assert.That(ballControl.IsContestedEscapeActive, Is.True);
            Assert.That(ballControl.ContestedEscapeActivationCount, Is.EqualTo(1));
            Assert.That(ballControl.TryGetContestedEscapeTarget(Team.Red, red.Slot, out var redTarget), Is.True);
            Assert.That(ballControl.TryGetContestedEscapeTarget(Team.Navy, navy.Slot, out var navyTarget), Is.True);
            Assert.That(Mathf.Sign(redTarget.y), Is.EqualTo(-Mathf.Sign(navyTarget.y)));

            yield return new WaitForFixedUpdate();
            var redDesired = red.GetComponent<MNG_PlayerMotor>().LastDesiredVelocity;
            var navyDesired = navy.GetComponent<MNG_PlayerMotor>().LastDesiredVelocity;
            Assert.That(Mathf.Sign(redDesired.y), Is.EqualTo(-Mathf.Sign(navyDesired.y)),
                "The two front-on contestants must peel away to opposite flanks.");
            Assert.That(Mathf.Abs(redDesired.y), Is.GreaterThan(0.1f));
            Assert.That(Mathf.Abs(navyDesired.y), Is.GreaterThan(0.1f));
        }

        [UnityTest]
        public IEnumerator SpectatorCameraRestoresBroadcastAndHumanFollowViews()
        {
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            yield return null;

            var cameras = Object.FindObjectsByType<Camera>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.That(cameras, Has.Length.EqualTo(1));
            Assert.That(cameras[0].enabled, Is.True);
            Assert.That(cameras[0].CompareTag("MainCamera"), Is.True);
            var spectator = cameras[0].GetComponent<MNG_SpectatorCamera>();
            var human = Object.FindFirstObjectByType<MNG_HumanInput>();
            Assert.That(spectator, Is.Not.Null);
            Assert.That(spectator.IsFollowingHuman, Is.False);
            var overviewPosition = cameras[0].transform.position;

            human.ToggleOwner();
            for (var frame = 0; frame < 12; frame++) yield return null;
            Assert.That(spectator.IsFollowingHuman, Is.True);
            Assert.That(Vector3.Distance(cameras[0].transform.position, overviewPosition), Is.GreaterThan(1f));

            human.ToggleOwner();
            for (var frame = 0; frame < 45; frame++) yield return null;
            Assert.That(spectator.IsFollowingHuman, Is.False);
            Assert.That(Vector3.Distance(cameras[0].transform.position, overviewPosition), Is.LessThan(0.5f));
        }

        [UnityTest]
        public IEnumerator SpawnVariationRunsAtInitialPlacementAndKickoffButNeverDuringPlay()
        {
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            yield return null;

            var match = Object.FindFirstObjectByType<MNG_MatchController>();
            foreach (var fallback in Object.FindObjectsByType<MNG_FallbackManager>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                fallback.enabled = false;
            foreach (var executor in Object.FindObjectsByType<MNG_PlayerSkillExecutor>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                executor.enabled = false;

            var initialPlacement = match.SpawnPlacementRevision;
            Assert.That(initialPlacement, Is.GreaterThan(0));
            for (var step = 0; step < 30; step++) yield return new WaitForFixedUpdate();
            Assert.That(match.State, Is.EqualTo(MNG_MatchState.Playing));
            Assert.That(match.SpawnPlacementRevision, Is.EqualTo(initialPlacement),
                "Ordinary play must never apply artificial spawn movement.");

            Assert.That(match.GoalTouched(Team.Red), Is.True);
            for (var step = 0; step < 220 && match.State == MNG_MatchState.GoalPause; step++)
                yield return new WaitForFixedUpdate();
            Assert.That(match.State, Is.EqualTo(MNG_MatchState.Playing));
            Assert.That(match.SpawnPlacementRevision, Is.EqualTo(initialPlacement + 1),
                "The central kickoff after a goal applies exactly one new placement variation.");

            var kickoffPlacement = match.SpawnPlacementRevision;
            for (var step = 0; step < 30; step++) yield return new WaitForFixedUpdate();
            Assert.That(match.SpawnPlacementRevision, Is.EqualTo(kickoffPlacement),
                "Play after the kickoff must not receive another artificial position change.");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator FallbackManagersCompleteFullThreeHundredSecondMatch()
        {
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            yield return null;

            var match = Object.FindFirstObjectByType<MNG_MatchController>();
            var ballControl = Object.FindFirstObjectByType<MNG_BallControl>();
            var ball = ballControl.GetComponent<Rigidbody>();
            var fallbacks = Object.FindObjectsByType<MNG_FallbackManager>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            Assert.That(fallbacks, Has.Length.EqualTo(2));

#if UNITY_EDITOR
            var serializedMatch = new SerializedObject(match);
            var restartOnFinish = serializedMatch.FindProperty("restartOnFinish");
            Assert.That(restartOnFinish, Is.Not.Null);
            restartOnFinish.boolValue = false;
            serializedMatch.ApplyModifiedPropertiesWithoutUndo();
#endif

            match.ResetMatch();
            var originalTimeScale = Time.timeScale;
            var steps = 0;
            var previousScore = 0;
            var scoreEvents = 0;
            var possessionTransitions = 0;
            var previousPossession = match.Snapshot.Possession;
            var maximumBallDistance = 0f;
            try
            {
                Time.timeScale = 20f;
                while (match.State != MNG_MatchState.Finished && steps < 15100)
                {
                    yield return new WaitForFixedUpdate();
                    steps++;

                    var snapshot = match.Snapshot;
                    if (snapshot.Possession != previousPossession)
                    {
                        possessionTransitions++;
                        previousPossession = snapshot.Possession;
                    }

                    var score = match.RedScore + match.NavyScore;
                    if (score != previousScore)
                    {
                        Assert.That(score - previousScore, Is.EqualTo(1),
                            "A physical goal must be counted exactly once.");
                        Assert.That(match.State, Is.EqualTo(MNG_MatchState.GoalPause));
                        scoreEvents++;
                        previousScore = score;
                    }

                    if (steps % 25 != 0 && match.State != MNG_MatchState.Finished) continue;
                    AssertFinite(ball.position, "ball position");
                    AssertFinite(ball.linearVelocity, "ball velocity");
                    maximumBallDistance = Mathf.Max(maximumBallDistance,
                        new Vector2(ball.position.x, ball.position.z).magnitude);
                    Assert.That(Mathf.Abs(ball.position.x),
                        Is.LessThanOrEqualTo(snapshot.FieldHalfLength + 5f),
                        "The ball was lost beyond the playable field/goal depth.");
                    Assert.That(Mathf.Abs(ball.position.z),
                        Is.LessThanOrEqualTo(snapshot.FieldHalfWidth + 5f),
                        "The ball was lost beyond the playable field width.");
                }
            }
            finally
            {
                Time.timeScale = originalTimeScale;
            }

            Debug.Log($"MNG 300S FALLBACK RESULT steps={steps} score={match.RedScore}:{match.NavyScore} "
                + $"scoreEvents={scoreEvents} possessionTransitions={possessionTransitions} "
                + $"escapeActivations={ballControl.ContestedEscapeActivationCount} "
                + $"maxBallDistance={maximumBallDistance:R}");
            Assert.That(match.State, Is.EqualTo(MNG_MatchState.Finished));
            Assert.That(match.MatchRemainingSeconds, Is.Zero.Within(0.0001f));
            Assert.That(steps, Is.InRange(14990, 15010));
            Assert.That(maximumBallDistance, Is.GreaterThan(5f),
                "Fallback play must break a central deadlock and move the ball into open field.");
            foreach (var fallback in fallbacks)
                Assert.That(fallback.LastDecision.Reason, Is.Not.Null.And.Not.Empty);
        }

        [UnityTest]
        public IEnumerator HumanTogglePreservesMatchStateAndRevokesManagerWrite()
        {
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            yield return null;

            var match = Object.FindFirstObjectByType<MNG_MatchController>();
            var human = Object.FindFirstObjectByType<MNG_HumanInput>();
            var striker = human.GetComponent<MNG_PlayerAvatar>();
            var position = striker.transform.position;
            var remaining = match.MatchRemainingSeconds;

            Assert.That(human.ToggleOwner(), Is.EqualTo(MNG_InputOwner.Human));
            Assert.That(striker.IsHuman, Is.True);
            Assert.That(match.GetDecisionState(Team.Red).ControlMask[striker.Slot], Is.False);
            Assert.That(match.RedScore, Is.Zero);
            Assert.That(match.NavyScore, Is.Zero);
            Assert.That(Vector3.Distance(striker.transform.position, position), Is.LessThan(0.001f));
            Assert.That(match.MatchRemainingSeconds, Is.EqualTo(remaining).Within(0.1f));

            Assert.That(human.ToggleOwner(), Is.EqualTo(MNG_InputOwner.Manager));
            Assert.That(striker.IsHuman, Is.False);
            Assert.That(match.GetDecisionState(Team.Red).ControlMask[striker.Slot], Is.True);
        }

        [UnityTest]
        public IEnumerator PhysicalBallScoresBothGoalsOnceAndResetsAfterPause()
        {
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            yield return null;

            var match = Object.FindFirstObjectByType<MNG_MatchController>();
            var ball = Object.FindFirstObjectByType<MNG_BallControl>().GetComponent<Rigidbody>();
            foreach (var fallback in Object.FindObjectsByType<MNG_FallbackManager>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                fallback.enabled = false;

            foreach (var sign in new[] { -1f, 1f })
            {
                match.ResetMatch();
                ball.isKinematic = false;
                ball.position = new Vector3(sign * 58f, 0.52f, 0f);
                ball.rotation = Quaternion.identity;
                ball.linearVelocity = Vector3.right * (sign * 18f);
                ball.angularVelocity = Vector3.zero;
                Physics.SyncTransforms();

                for (var step = 0; step < 120 && match.State == MNG_MatchState.Playing; step++)
                    yield return new WaitForFixedUpdate();

                Assert.That(match.State, Is.EqualTo(MNG_MatchState.GoalPause),
                    $"Physical ball did not score at goal sign {sign}.");
                Assert.That(match.RedScore, Is.EqualTo(sign > 0f ? 1 : 0));
                Assert.That(match.NavyScore, Is.EqualTo(sign < 0f ? 1 : 0));
                Assert.That(ball.isKinematic, Is.True);
                yield return new WaitForSeconds(0.3f);
                Assert.That(match.RedScore + match.NavyScore, Is.EqualTo(1),
                    "Goal was counted more than once during GoalPause.");

                for (var step = 0; step < 190 && match.State == MNG_MatchState.GoalPause; step++)
                    yield return new WaitForFixedUpdate();
                Assert.That(match.State, Is.EqualTo(MNG_MatchState.Playing));
                Assert.That(ball.isKinematic, Is.False);
                Assert.That(new Vector2(ball.position.x, ball.position.z).magnitude, Is.LessThan(0.02f));
            }
        }

        [UnityTest]
        public IEnumerator OpenGoalShotsFromTenAndTwentyMetersReachNinetyPercent()
        {
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            yield return null;

            var match = Object.FindFirstObjectByType<MNG_MatchController>();
            var ballControl = Object.FindFirstObjectByType<MNG_BallControl>();
            var ball = ballControl.GetComponent<Rigidbody>();
            var avatars = Object.FindObjectsByType<MNG_PlayerAvatar>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var redShooter = avatars.Single(avatar => avatar.Team == Team.Red && avatar.Slot == 3);
            var navyShooter = avatars.Single(avatar => avatar.Team == Team.Navy && avatar.Slot == 3);
            foreach (var fallback in Object.FindObjectsByType<MNG_FallbackManager>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                fallback.enabled = false;
            foreach (var avatar in avatars)
                avatar.GetComponent<MNG_PlayerSkillExecutor>().enabled = false;

            var distances = new[] { 10f, 20f };
            var successCounts = new int[distances.Length];
            var failureTrace = string.Empty;
            for (var distanceIndex = 0; distanceIndex < distances.Length; distanceIndex++)
            {
                var distance = distances[distanceIndex];
                for (var attempt = 0; attempt < 20; attempt++)
                {
                    match.ResetMatch();
                    var sign = attempt % 2 == 0 ? 1f : -1f;
                    var shooter = sign > 0f ? redShooter : navyShooter;
                    SetOnlyPlayerCollidersEnabled(avatars, shooter);
                    shooter.Body.constraints = RigidbodyConstraints.FreezeAll;
                    var lane = (attempt % 5 - 2) * (match.Snapshot.GoalHalfWidth - 1f) * 0.4f;
                    var direction = Vector3.right * sign;
                    SetPlayerPose(shooter,
                        new Vector3(sign * (match.Snapshot.FieldHalfLength - distance
                            - MNG_KickPlate.DribbleAnchorForward), 0.52f, lane),
                        direction);
                    ball.isKinematic = false;
                    var start = shooter.KickPlate.DribblePosition;
                    ball.position = new Vector3(start.x, ball.position.y, start.z);
                    ball.rotation = Quaternion.identity;
                    ball.linearVelocity = Vector3.zero;
                    ball.angularVelocity = Vector3.zero;
                    Physics.SyncTransforms();

                    for (var step = 0; step < 8 && !IsCarrier(ballControl, shooter); step++)
                    {
                        CommandForward(shooter, direction, 1f);
                        yield return new WaitForFixedUpdate();
                    }
                    var kickBefore = ballControl.LatestKickId;
                    var target = new Vector3(
                        sign * (match.Snapshot.FieldHalfLength + 2f),
                        ball.position.y,
                        lane);
                    var armed = IsCarrier(ballControl, shooter)
                        && ballControl.TryKick(shooter.Team, shooter.Slot, target,
                            MNG_KickSolver.StrongExitSpeed, out _);

                    for (var step = 0; step < 120 && match.State == MNG_MatchState.Playing; step++)
                    {
                        CommandForward(shooter, direction, 1f);
                        yield return new WaitForFixedUpdate();
                    }
                    if (armed && ballControl.LatestKickId > kickBefore
                        && match.State == MNG_MatchState.GoalPause)
                    {
                        successCounts[distanceIndex]++;
                    }
                    else
                    {
                        failureTrace += $"\ndistance={distance:R} attempt={attempt} sign={sign:R} "
                            + $"lane={lane:R} finalPosition={ball.position:F3} "
                            + $"finalVelocity={ball.linearVelocity:F3} state={match.State} "
                            + $"armed={armed} strikeDelta={ballControl.LatestKickId - kickBefore} "
                            + $"plateContacts={ballControl.GetPhysicalContactCount(shooter.Team, shooter.Slot)}";
                    }
                }
                Debug.Log($"MNG OPEN GOAL RESULT distance={distance:R}m "
                    + $"success={successCounts[distanceIndex]}/20{failureTrace}");
            }
            Assert.That(successCounts[0] >= 18 && successCounts[1] >= 18, Is.True,
                $"Open-goal shot success was 10m={successCounts[0]}/20, "
                + $"20m={successCounts[1]}/20.{failureTrace}");
        }

        [UnityTest]
        public IEnumerator UnopposedPassesAtFiveTenAndTwentyMetersAreActuallyReceived()
        {
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            yield return null;

            var match = Object.FindFirstObjectByType<MNG_MatchController>();
            var ballControl = Object.FindFirstObjectByType<MNG_BallControl>();
            var ball = ballControl.GetComponent<Rigidbody>();
            var avatars = Object.FindObjectsByType<MNG_PlayerAvatar>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var sender = avatars.Single(avatar => avatar.Team == Team.Red && avatar.Slot == 3);
            var receiver = avatars.Single(avatar => avatar.Team == Team.Red && avatar.Slot == 1);
            foreach (var fallback in Object.FindObjectsByType<MNG_FallbackManager>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                fallback.enabled = false;
            foreach (var avatar in avatars)
                avatar.GetComponent<MNG_PlayerSkillExecutor>().enabled = false;
            SetOnlyPlayerCollidersEnabled(avatars, sender, receiver);
            sender.Body.constraints = RigidbodyConstraints.FreezeAll;
            receiver.GetComponent<MNG_PlayerSkillExecutor>().enabled = false;
            receiver.Body.constraints = RigidbodyConstraints.FreezeAll;

            var distances = new[] { 5f, 10f, 20f };
            var successCounts = new int[distances.Length];
            var failureTrace = string.Empty;
            for (var distanceIndex = 0; distanceIndex < distances.Length; distanceIndex++)
            {
                var distance = distances[distanceIndex];
                for (var attempt = 0; attempt < 20; attempt++)
                {
                    match.ResetMatch();
                    var angle = attempt * Mathf.PI * 2f / 20f;
                    var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    var forward = new Vector3(direction.x, 0f, direction.y);
                    SetPlayerPose(sender, -forward * MNG_KickPlate.DribbleAnchorForward, forward);
                    SetPlayerPose(receiver,
                        forward * (distance + MNG_KickPlate.DribbleAnchorForward),
                        -forward);
                    ball.isKinematic = false;
                    var start = sender.KickPlate.DribblePosition;
                    ball.position = new Vector3(start.x, ball.position.y, start.z);
                    ball.rotation = Quaternion.identity;
                    ball.linearVelocity = Vector3.zero;
                    ball.angularVelocity = Vector3.zero;
                    Physics.SyncTransforms();

                    for (var step = 0; step < 8 && !IsCarrier(ballControl, sender); step++)
                    {
                        CommandForward(sender, forward, 1f);
                        yield return new WaitForFixedUpdate();
                    }
                    var kickBefore = ballControl.LatestKickId;
                    var target = receiver.KickPlate.DribblePosition;
                    var armed = IsCarrier(ballControl, sender)
                        && ballControl.TryKick(sender.Team, sender.Slot, target,
                            MNG_KickSolver.PassExitSpeedForDistance(distance), out _);

                    var closestDistance = float.PositiveInfinity;
                    for (var step = 0; step < 160; step++)
                    {
                        CommandForward(sender, forward, 1f);
                        yield return new WaitForFixedUpdate();
                        closestDistance = Mathf.Min(closestDistance,
                            receiver.KickPlate.DistanceToDribblePosition(ball.position));
                        if (IsCarrier(ballControl, receiver))
                            break;
                    }
                    if (armed && ballControl.LatestKickId > kickBefore
                        && IsCarrier(ballControl, receiver))
                    {
                        successCounts[distanceIndex]++;
                    }
                    else
                    {
                        failureTrace += $"\ndistance={distance:R} attempt={attempt} "
                            + $"angle={angle * Mathf.Rad2Deg:R} closest={closestDistance:R} "
                            + $"finalPosition={ball.position:F3} finalVelocity={ball.linearVelocity:F3} "
                            + $"acquisition={ballControl.GetAcquisitionDistance(receiver.Team, receiver.Slot):R} "
                            + $"armed={armed} strikeDelta={ballControl.LatestKickId - kickBefore} "
                            + $"senderContacts={ballControl.GetPhysicalContactCount(sender.Team, sender.Slot)} "
                            + $"receiverContacts={ballControl.GetPhysicalContactCount(receiver.Team, receiver.Slot)} "
                            + $"carrier={ballControl.Carrier}";
                    }
                }
                Debug.Log($"MNG PASS RECEIVE RESULT distance={distance:R}m "
                    + $"success={successCounts[distanceIndex]}/20{failureTrace}");
            }

            Assert.That(successCounts[0] >= 18 && successCounts[1] >= 18 && successCounts[2] >= 18,
                Is.True,
                $"Unopposed pass reception was 5m={successCounts[0]}/20, "
                + $"10m={successCounts[1]}/20, 20m={successCounts[2]}/20.{failureTrace}");
        }

        [UnityTest]
        public IEnumerator KickPlateDribbleFollowsForwardAndDropsOnStopAndReverse()
        {
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            yield return null;

            var match = Object.FindFirstObjectByType<MNG_MatchController>();
            var ballControl = Object.FindFirstObjectByType<MNG_BallControl>();
            var ball = ballControl.GetComponent<Rigidbody>();
            var avatars = Object.FindObjectsByType<MNG_PlayerAvatar>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var carrier = avatars.Single(avatar => avatar.Team == Team.Red && avatar.Slot == 1);
            foreach (var fallback in Object.FindObjectsByType<MNG_FallbackManager>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                fallback.enabled = false;
            foreach (var avatar in avatars)
                avatar.GetComponent<MNG_PlayerSkillExecutor>().enabled = false;
            SetOnlyPlayerCollidersEnabled(avatars, carrier);

            var routeSuccesses = new int[3];
            var failureTrace = string.Empty;
            var originalTimeScale = Time.timeScale;
            try
            {
                Time.timeScale = 20f;
                for (var route = 0; route < 3; route++)
                for (var attempt = 0; attempt < 20; attempt++)
                {
                    match.ResetMatch();
                    SetPlayerPose(carrier, Vector3.zero, Vector3.right);
                    PlaceBallAtPlate(ball, carrier.KickPlate);

                    var correctionObserved = false;
                    var carrierObserved = false;
                    var pathTravel = 0f;
                    var previousPosition = carrier.Body.position;
                    var minimumPlateDistance = float.PositiveInfinity;
                    var maximumPlateDistance = 0f;
                    for (var step = 0; step < 110 && pathTravel < 8f; step++)
                    {
                        var turnSign = route == 1 ? -1f : route == 2 ? 1f : 0f;
                        var forward = Quaternion.AngleAxis(turnSign * pathTravel * 6f, Vector3.up)
                            * Vector3.right;
                        CommandForward(carrier, forward, 6f);
                        yield return new WaitForFixedUpdate();

                        pathTravel += Vector3.Distance(carrier.Body.position, previousPosition);
                        previousPosition = carrier.Body.position;
                        correctionObserved |= ballControl.DribbleCorrectionAppliedLastStep;
                        carrierObserved |= IsCarrier(ballControl, carrier);
                        var plateDistance = carrier.KickPlate.DistanceToDribblePosition(ball.position);
                        minimumPlateDistance = Mathf.Min(minimumPlateDistance, plateDistance);
                        maximumPlateDistance = Mathf.Max(maximumPlateDistance, plateDistance);
                    }

                    var finalPlateDistance = carrier.KickPlate.DistanceToDribblePosition(ball.position);
                    var success = pathTravel >= 8f
                        && correctionObserved
                        && carrierObserved
                        && IsCarrier(ballControl, carrier)
                        && finalPlateDistance <= MNG_KickPlate.DribbleReleaseRadius;
                    if (success) routeSuccesses[route]++;
                    else if (failureTrace.Length < 4000)
                    {
                        failureTrace += $"\nroute={route} attempt={attempt} travel={pathTravel:R} "
                            + $"correction={correctionObserved} carrierObserved={carrierObserved} "
                            + $"carrier={ballControl.Carrier} minPlate={minimumPlateDistance:R} "
                            + $"maxPlate={maximumPlateDistance:R} finalPlate={finalPlateDistance:R}";
                    }
                }
            }
            finally
            {
                Time.timeScale = originalTimeScale;
            }

            Debug.Log($"MNG DRIBBLE RESULT straight={routeSuccesses[0]}/20 "
                + $"left={routeSuccesses[1]}/20 right={routeSuccesses[2]}/20{failureTrace}");
            Assert.That(routeSuccesses.All(count => count >= 18), Is.True, failureTrace);

            match.ResetMatch();
            SetPlayerPose(carrier, Vector3.zero, Vector3.right);
            PlaceBallAtPlate(ball, carrier.KickPlate);
            for (var step = 0; step < 8 && !IsCarrier(ballControl, carrier); step++)
            {
                CommandForward(carrier, Vector3.right, 2f);
                yield return new WaitForFixedUpdate();
            }
            Assert.That(IsCarrier(ballControl, carrier), Is.True);

            for (var step = 0; step < 8; step++)
            {
                carrier.GetComponent<MNG_PlayerMotor>().Stop(
                    MNG_InputOwner.Manager, carrier.Ownership.Revision, Time.fixedDeltaTime);
                yield return new WaitForFixedUpdate();
            }
            Assert.That(ballControl.Carrier.IsValid, Is.False);
            Assert.That(ballControl.DribbleCorrectionAppliedLastStep, Is.False);

            PlaceBallAtPlate(ball, carrier.KickPlate);
            for (var step = 0; step < 8 && !IsCarrier(ballControl, carrier); step++)
            {
                CommandForward(carrier, Vector3.right, 2f);
                yield return new WaitForFixedUpdate();
            }
            Assert.That(IsCarrier(ballControl, carrier), Is.True);
            for (var step = 0; step < 8; step++)
            {
                carrier.GetComponent<MNG_PlayerMotor>().ApplyDesiredVelocity(
                    Vector2.left * 3f,
                    Vector2.right,
                    MNG_InputOwner.Manager,
                    carrier.Ownership.Revision,
                    Time.fixedDeltaTime);
                yield return new WaitForFixedUpdate();
            }
            Assert.That(ballControl.Carrier.IsValid, Is.False);
            Assert.That(ballControl.DribbleCorrectionAppliedLastStep, Is.False);
        }

        [UnityTest]
        public IEnumerator OpponentKickPlatesStealAtLeastNinetyPercentWithinPointTwoSeconds()
        {
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            yield return null;

            var match = Object.FindFirstObjectByType<MNG_MatchController>();
            var ballControl = Object.FindFirstObjectByType<MNG_BallControl>();
            var ball = ballControl.GetComponent<Rigidbody>();
            var avatars = Object.FindObjectsByType<MNG_PlayerAvatar>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var red = avatars.Single(avatar => avatar.Team == Team.Red && avatar.Slot == 3);
            var navy = avatars.Single(avatar => avatar.Team == Team.Navy && avatar.Slot == 3);
            foreach (var fallback in Object.FindObjectsByType<MNG_FallbackManager>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                fallback.enabled = false;
            foreach (var avatar in avatars)
                avatar.GetComponent<MNG_PlayerSkillExecutor>().enabled = false;
            SetOnlyPlayerCollidersEnabled(avatars, red, navy);
            red.Body.constraints = RigidbodyConstraints.FreezeAll;
            navy.Body.constraints = RigidbodyConstraints.FreezeAll;

            var totalSuccess = 0;
            var redSteals = 0;
            var navySteals = 0;
            var trace = string.Empty;
            for (var attempt = 0; attempt < 20; attempt++)
            {
                match.ResetMatch();
                var angle = attempt * Mathf.PI * 2f / 20f;
                var forward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                var owner = attempt % 2 == 0 ? red : navy;
                var stealer = attempt % 2 == 0 ? navy : red;
                SetPlayerPose(owner, -forward * MNG_KickPlate.DribbleAnchorForward, forward);
                SetPlayerPose(stealer,
                    forward * (MNG_KickPlate.DribbleAnchorForward + 0.20f),
                    -forward);
                PlaceBallAtPlate(ball, owner.KickPlate);
                for (var step = 0; step < 8 && !IsCarrier(ballControl, owner); step++)
                {
                    CommandForward(owner, forward, 1f);
                    yield return new WaitForFixedUpdate();
                }

                if (!IsCarrier(ballControl, owner))
                {
                    trace += $"\nattempt={attempt} initial-owner-failed team={owner.Team}";
                    continue;
                }

                SetPlayerPose(owner,
                    -forward * (MNG_KickPlate.DribbleAnchorForward + 0.20f),
                    forward);
                var elapsed = 0f;
                for (var step = 0; step < 10 && !IsCarrier(ballControl, stealer); step++)
                {
                    CommandForward(owner, forward, 1f);
                    CommandForward(stealer, -forward, 1f);
                    yield return new WaitForFixedUpdate();
                    elapsed += Time.fixedDeltaTime;
                }

                if (IsCarrier(ballControl, stealer) && elapsed <= 0.20f)
                {
                    totalSuccess++;
                    if (stealer.Team == Team.Red) redSteals++;
                    else navySteals++;
                }
                else
                {
                    trace += $"\nattempt={attempt} owner={owner.Team} stealer={stealer.Team} "
                        + $"elapsed={elapsed:R} carrier={ballControl.Carrier} "
                        + $"ownerDistance={owner.KickPlate.DistanceToDribblePosition(ball.position):R} "
                        + $"stealerDistance={stealer.KickPlate.DistanceToDribblePosition(ball.position):R}";
                }
            }

            Debug.Log($"MNG STEAL RESULT total={totalSuccess}/20 red={redSteals}/10 "
                + $"navy={navySteals}/10{trace}");
            Assert.That(totalSuccess, Is.GreaterThanOrEqualTo(18), trace);
            Assert.That(redSteals, Is.GreaterThanOrEqualTo(9), trace);
            Assert.That(navySteals, Is.GreaterThanOrEqualTo(9), trace);
        }

        static bool IsCarrier(MNG_BallControl ballControl, MNG_PlayerAvatar avatar)
            => ballControl.Carrier.IsValid
                && ballControl.Carrier.Team == avatar.Team
                && ballControl.Carrier.Slot == avatar.Slot;

        static void AssertFinite(Vector3 value, string label)
        {
            Assert.That(float.IsNaN(value.x) || float.IsInfinity(value.x)
                || float.IsNaN(value.y) || float.IsInfinity(value.y)
                || float.IsNaN(value.z) || float.IsInfinity(value.z),
                Is.False,
                $"{label} contains a non-finite component: {value}");
        }

        static void SetOnlyPlayerCollidersEnabled(
            MNG_PlayerAvatar[] avatars,
            params MNG_PlayerAvatar[] enabledAvatars)
        {
            foreach (var avatar in avatars)
            foreach (var collider in avatar.GetComponentsInChildren<Collider>(true))
                collider.enabled = enabledAvatars.Contains(avatar);
        }

        static void SetPlayerPose(MNG_PlayerAvatar avatar, Vector3 position, Vector3 forward)
        {
            position.y = 0.52f;
            var rotation = Quaternion.LookRotation(forward, Vector3.up);
            avatar.transform.SetPositionAndRotation(position, rotation);
            avatar.Body.position = position;
            avatar.Body.rotation = rotation;
            avatar.Body.linearVelocity = Vector3.zero;
            avatar.Body.angularVelocity = Vector3.zero;
            Physics.SyncTransforms();
        }

        static void PlaceBallAtPlate(Rigidbody ball, MNG_KickPlate plate)
        {
            var target = plate.DribblePosition;
            ball.isKinematic = false;
            ball.position = new Vector3(target.x, ball.position.y, target.z);
            ball.rotation = Quaternion.identity;
            ball.linearVelocity = Vector3.zero;
            ball.angularVelocity = Vector3.zero;
            Physics.SyncTransforms();
        }

        static void CommandForward(MNG_PlayerAvatar avatar, Vector3 forward, float speed)
        {
            var direction = new Vector2(forward.x, forward.z).normalized;
            avatar.GetComponent<MNG_PlayerMotor>().ApplyDesiredVelocity(
                direction * speed,
                direction,
                MNG_InputOwner.Manager,
                avatar.Ownership.Revision,
                Time.fixedDeltaTime);
        }
    }
}

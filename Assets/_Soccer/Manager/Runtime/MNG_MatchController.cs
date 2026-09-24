using System;
using MachineLearning.Soccer;
using UnityEngine;

namespace MachineLearning.Soccer.Manager
{
    public enum MNG_MatchState
    {
        Playing = 0,
        GoalPause = 1,
        Finished = 2
    }

    public enum MNG_MatchFinishMode
    {
        TerminalResult = 0,
        InterruptedCollection = 1,
        SelfPlayTerminalResult = 2
    }

    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    public sealed partial class MNG_MatchController : MonoBehaviour
    {
        public const float MatchDurationSeconds = 300f;

        [SerializeField] SoccerArenaGeometry arenaGeometry;
        [SerializeField] Rigidbody ballBody;
        [SerializeField] MNG_PlayerAvatar[] players = Array.Empty<MNG_PlayerAvatar>();
        [SerializeField] MNG_RewardEngine rewardEngine;
        [SerializeField] bool restartOnFinish = true;
        [SerializeField] bool useRuntimeV2;
        public bool UseRuntimeV2 => useRuntimeV2;
        public void ConfigureRuntimeV2(bool value) => useRuntimeV2 = value;
        public event Action<Team, int, MNG_TaskResult> TaskProcessed;
        public long GetCommandId(Team team) => m_TaskRevisions[(int)team] * 2 + (int)team;
        readonly long[] m_TaskResultCounts = new long[9];
        public long GetTaskResultCount(MNG_TaskResultKind kind) => m_TaskResultCounts[(int)kind];
        public void RecordTaskResult(Team team, int slot, MNG_TaskResult result)
        {
            m_TaskResultCounts[(int)result.Kind]++;
            TaskProcessed?.Invoke(team, slot, result);
        }
        [SerializeField, Min(1f)] float matchDurationSeconds = MatchDurationSeconds;
        [SerializeField] MNG_MatchFinishMode finishMode = MNG_MatchFinishMode.TerminalResult;

        readonly MNG_MatchSnapshot m_Snapshot = new MNG_MatchSnapshot();
        readonly MNG_TeamDecisionState[] m_Decisions =
        {
            new MNG_TeamDecisionState(),
            new MNG_TeamDecisionState()
        };
        readonly bool[] m_CommandMaskBuffer = new bool[MNG_CommandMask.CommandCount];
        readonly MNG_PlayerAvatar[] m_AvatarsByIndex =
            new MNG_PlayerAvatar[MNG_MatchSnapshot.PlayersPerTeam * MNG_MatchSnapshot.TeamCount];
        readonly MNG_PlayerSkillExecutor[] m_ExecutorsByIndex =
            new MNG_PlayerSkillExecutor[MNG_MatchSnapshot.PlayersPerTeam * MNG_MatchSnapshot.TeamCount];
        readonly MNG_PlayerTask[] m_RedTaskBuffer = new MNG_PlayerTask[MNG_MatchSnapshot.PlayersPerTeam];
        readonly MNG_PlayerTask[] m_NavyTaskBuffer = new MNG_PlayerTask[MNG_MatchSnapshot.PlayersPerTeam];
        readonly long[] m_TaskRevisions = new long[2];
        readonly MNG_ManagerAgent[] m_Managers = new MNG_ManagerAgent[2];
        readonly Vector3[] m_PlayerStartingPositions =
            new Vector3[MNG_MatchSnapshot.PlayersPerTeam * MNG_MatchSnapshot.TeamCount];
        readonly Quaternion[] m_PlayerStartingRotations =
            new Quaternion[MNG_MatchSnapshot.PlayersPerTeam * MNG_MatchSnapshot.TeamCount];
        readonly MNG_GlobalBallStallTracker m_GlobalBallStall = new();

        long m_TickId;
        long m_EpisodeId;
        long m_GoalId;
        int m_KickoffIndex;
        long m_SpawnPlacementRevision;
        bool m_HasSnapshot;
        bool m_RestartPending;
        int m_SpawnSeedOffset;
        Vector3 m_BallStartingPosition;
        Quaternion m_BallStartingRotation;

        public MNG_MatchSnapshot Snapshot => m_Snapshot;
        public float MatchRemainingSeconds { get; private set; } = MatchDurationSeconds;
        public float ConfiguredMatchDurationSeconds => matchDurationSeconds;
        public MNG_MatchFinishMode FinishMode => finishMode;
        public float EpisodeElapsedSeconds { get; private set; }
        public float GoalPauseRemainingSeconds { get; private set; }
        public int RedScore { get; private set; }
        public int NavyScore { get; private set; }
        public MNG_MatchState State { get; private set; } = MNG_MatchState.Playing;
        public bool IsPlayActive => State == MNG_MatchState.Playing && MatchRemainingSeconds > 0f;
        public long SpawnPlacementRevision => m_SpawnPlacementRevision;
        public int SpawnSeedOffset => m_SpawnSeedOffset;
        public bool IsGlobalBallStallRecoveryActive => m_GlobalBallStall.IsActive;
        public int GlobalBallStallRecoveryActivations => m_GlobalBallStall.ActivationCount;
        public int EpisodeStallActivations => m_GlobalBallStall.EpisodeActivationCount;
        public float EpisodeStallActiveSeconds => m_GlobalBallStall.EpisodeActiveSeconds;
        public bool EvaluationMirror { get; private set; }
        public void ConfigureEvaluationMirror(bool mirror) => EvaluationMirror = mirror;
        public Team EpisodeNeutralFirstTeam => ((CalculateSpawnSeed(m_EpisodeId, 0, m_SpawnSeedOffset) & 1) ^ (EvaluationMirror ? 1 : 0)) == 0 ? Team.Red : Team.Navy;
        public event Action<Team, MNG_Command, long> CommandAccepted;
        public event Action<Team, long> GoalScored;

        void Awake()
        {
            ValidateBindingsOrThrow();
            CaptureStartingState();
            m_GlobalBallStall.Reset();
            ApplyRegularMatchSpawnJitter(0, 0);
            CaptureSnapshot();
        }

        void FixedUpdate()
        {
            var deltaTime = Time.fixedDeltaTime;
            m_TickId++;
            if (m_RestartPending)
            {
                m_RestartPending = false;
                ResetMatch();
                return;
            }
            if (State == MNG_MatchState.Finished) return;

            EpisodeElapsedSeconds += deltaTime;
            MatchRemainingSeconds = Mathf.Max(0f, MatchRemainingSeconds - deltaTime);
            if (MatchRemainingSeconds <= 0f)
            {
                FinishMatch();
                return;
            }

            if (State == MNG_MatchState.GoalPause)
            {
                GoalPauseRemainingSeconds = Mathf.Max(0f, GoalPauseRemainingSeconds - deltaTime);
                FreezeBodies();
                if (GoalPauseRemainingSeconds <= 0f)
                {
                    m_KickoffIndex++;
                    ResetRound();
                }
                CaptureSnapshot();
                return;
            }

            m_Decisions[0].CommandAgeSeconds += deltaTime;
            m_Decisions[1].CommandAgeSeconds += deltaTime;
            if (m_Decisions[0].SecondsSincePossessionLoss >= 0f)
                m_Decisions[0].SecondsSincePossessionLoss += deltaTime;
            if (m_Decisions[1].SecondsSincePossessionLoss >= 0f)
                m_Decisions[1].SecondsSincePossessionLoss += deltaTime;
            for (var i = 0; i < players.Length; i++) players[i]?.AdvanceCooldown(deltaTime);
            UpdateGlobalBallStall(deltaTime);
            CaptureSnapshot();
            UpdatePassBuildRules();
            UpdateCommonRules();
        }

        public void Configure(
            SoccerArenaGeometry configuredGeometry,
            Rigidbody configuredBall,
            MNG_PlayerAvatar[] configuredPlayers)
        {
            arenaGeometry = configuredGeometry;
            ballBody = configuredBall;
            players = configuredPlayers ?? Array.Empty<MNG_PlayerAvatar>();
            ValidateBindingsOrThrow();
            CaptureStartingState();
            m_GlobalBallStall.Reset();
            CaptureSnapshot();
        }

        public bool TryGetSnapshot(out MNG_MatchSnapshot snapshot)
        {
            snapshot = m_Snapshot;
            return m_HasSnapshot;
        }

        public MNG_TeamDecisionState GetDecisionState(Team team)
            => m_Decisions[team == Team.Red ? 0 : 1];

        public MNG_PlayerAvatar GetPlayerAvatar(Team team, int slot)
        {
            if (slot < 0 || slot >= MNG_MatchSnapshot.PlayersPerTeam)
                throw new ArgumentOutOfRangeException(nameof(slot));
            var avatar = m_AvatarsByIndex[GetIndex(team, slot)];
            return avatar != null
                ? avatar
                : throw new InvalidOperationException($"MNG avatar {team}/{slot} is not bound.");
        }

        public void RegisterManager(MNG_ManagerAgent manager)
        {
            if (manager == null) throw new ArgumentNullException(nameof(manager));
            var index = manager.Team == Team.Red ? 0 : 1;
            if (m_Managers[index] != null && m_Managers[index] != manager)
                throw new InvalidOperationException($"MNG match already has a manager for {manager.Team}.");
            m_Managers[index] = manager;
            if (rewardEngine == null) rewardEngine = GetComponent<MNG_RewardEngine>();
            rewardEngine?.RegisterManager(manager);
        }

        public void SetRewardEngine(MNG_RewardEngine configuredRewardEngine)
        {
            rewardEngine = configuredRewardEngine
                ? configuredRewardEngine
                : throw new ArgumentNullException(nameof(configuredRewardEngine));
        }

        public float AwardBlockedForwardPassDecision(Team team, long decisionId)
        {
            return rewardEngine != null
                ? rewardEngine.Award(
                    team,
                    MNG_RewardEventKind.BlockedForwardPassDecision,
                    decisionId)
                : 0f;
        }

        public void ConfigureMatchDuration(
            float seconds,
            bool restartWhenFinished,
            MNG_MatchFinishMode configuredFinishMode = MNG_MatchFinishMode.TerminalResult)
        {
            if (!MNG_MatchSnapshot.IsFinite(seconds) || seconds < 1f)
                throw new ArgumentOutOfRangeException(nameof(seconds));
            matchDurationSeconds = seconds;
            restartOnFinish = restartWhenFinished;
            finishMode = configuredFinishMode;
        }

        public void ConfigureSpawnSeedOffset(int offset)
        {
            if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
            m_SpawnSeedOffset = offset;
        }

        public void EndActivePolicyEpisodes()
        {
            for (var i = 0; i < m_Managers.Length; i++)
            {
                var manager = m_Managers[i];
                if (manager != null && manager.isActiveAndEnabled) manager.EndEpisode();
            }
        }

        public void InterruptActivePolicyEpisodes()
        {
            for (var i = 0; i < m_Managers.Length; i++)
            {
                var manager = m_Managers[i];
                if (manager != null && manager.isActiveAndEnabled) manager.EpisodeInterrupted();
            }
        }

        public bool AcceptCommand(Team team, MNG_Command command)
        {
            var decision = GetDecisionState(team);
            MNG_CommandMask.Write(m_Snapshot, team, decision, m_CommandMaskBuffer);
            var commandIndex = (int)command;
            if (commandIndex < 0 || commandIndex >= m_CommandMaskBuffer.Length
                || !m_CommandMaskBuffer[commandIndex])
            {
                return false;
            }

            ApplyAcceptedCommand(team, command, decision);
            return true;
        }

        public void AcceptPolicyCommand(Team team, MNG_Command command)
        {
            var commandIndex = (int)command;
            if (commandIndex < 0 || commandIndex >= MNG_CommandMask.CommandCount)
                throw new ArgumentOutOfRangeException(nameof(command));

            // ML-Agents applies an action after the environment has advanced from the
            // snapshot whose mask accompanied that action. The Agent validates against
            // that sent mask; rechecking the newer world state here would reject a valid
            // decision when possession changes during the communicator round trip.
            ApplyAcceptedCommand(team, command, GetDecisionState(team));
        }

        void ApplyAcceptedCommand(Team team, MNG_Command command, MNG_TeamDecisionState decision)
        {
            var changed = decision.PreviousCommand != command;
            decision.PreviousCommand = command;
            if (changed) decision.CommandAgeSeconds = 0f;
            DispatchTeamPlan(team, command, decision);
            CommandAccepted?.Invoke(team, command, m_TickId);
        }

        public void SetDecisionTargets(Team team, bool hasPassTarget, bool hasShotTarget, int receiverSlot = -1)
        {
            var decision = GetDecisionState(team);
            decision.HasPassTarget = hasPassTarget;
            decision.HasShotTarget = hasShotTarget;
            decision.PendingPassReceiverSlot = receiverSlot;
            decision.ValidateOrThrow();
        }

        public void SetControlMask(Team team, int slot, bool managerControls)
        {
            if (slot < 0 || slot >= MNG_MatchSnapshot.PlayersPerTeam)
                throw new ArgumentOutOfRangeException(nameof(slot));
            GetDecisionState(team).ControlMask[slot] = managerControls;
            if (!managerControls) m_ExecutorsByIndex[GetIndex(team, slot)]?.Cancel();
        }

        public void SetPossession(MNG_Possession possession, MNG_CarrierRef carrier)
        {
            if (carrier.IsValid)
            {
                var expected = carrier.Team == Team.Red ? MNG_Possession.Red : MNG_Possession.Navy;
                if (possession != expected)
                    throw new ArgumentException("Carrier team must agree with confirmed possession.", nameof(carrier));
            }
            else if (possession != MNG_Possession.Neutral)
            {
                throw new ArgumentException("Confirmed team possession requires a carrier.", nameof(carrier));
            }
            m_Snapshot.Possession = possession;
            m_Snapshot.Carrier = carrier;
        }

        public void RecordPossessionLoss(Team team)
        {
            GetDecisionState(team).SecondsSincePossessionLoss = 0f;
        }

        public void ResetMatch()
        {
            ResetCommonRules(true);
            m_EpisodeId++;
            MatchRemainingSeconds = matchDurationSeconds;
            EpisodeElapsedSeconds = 0f;
            RedScore = 0;
            NavyScore = 0;
            State = MNG_MatchState.Playing;
            GoalPauseRemainingSeconds = 0f;
            m_KickoffIndex = 0;
            ResetDecisionState(m_Decisions[0]);
            ResetDecisionState(m_Decisions[1]);
            m_Snapshot.Possession = MNG_Possession.Neutral;
            m_Snapshot.Carrier = MNG_CarrierRef.None;
            m_GlobalBallStall.ResetEpisode();
            GetComponentInChildren<MNG_BallControl>()?.ConfigureNeutralPriority(EpisodeNeutralFirstTeam);
            rewardEngine?.ResetEpisode();
            ResetRound();
            CaptureSnapshot();
        }

        void ApplyRegularMatchSpawnJitter(long episodeId, int kickoffIndex)
        {
            var offsets = CreateSpawnOffsets(episodeId, kickoffIndex, m_SpawnSeedOffset, EvaluationMirror);
            for (var index = 0; index < m_AvatarsByIndex.Length; index++)
            {
                var avatar = m_AvatarsByIndex[index];
                if (avatar == null) continue;
                var offset = offsets[index];
                var position = avatar.Body.position;
                position.x = Mathf.Clamp(
                    position.x + offset.x,
                    -arenaGeometry.HalfLength + 0.55f,
                    arenaGeometry.HalfLength - 0.55f);
                position.z = Mathf.Clamp(
                    position.z + offset.y,
                    -arenaGeometry.HalfWidth + 0.55f,
                    arenaGeometry.HalfWidth - 0.55f);
                avatar.transform.position = position;
                avatar.Body.position = position;
            }
            Physics.SyncTransforms();
            m_SpawnPlacementRevision++;
        }

        public bool GoalTouched(Team scoringTeam)
        {
            if (!IsPlayActive) return false;
            ResetCommonRules(false);
            if (scoringTeam == Team.Red) RedScore++;
            else NavyScore++;
            State = MNG_MatchState.GoalPause;
            GoalPauseRemainingSeconds = 3f;
            m_GoalId++;
            m_GlobalBallStall.Reset();
            rewardEngine?.AwardGoal(scoringTeam, m_GoalId);
            GoalScored?.Invoke(scoringTeam, m_GoalId);
            GetComponentInChildren<MNG_BallControl>()?.ResetLedger();
            FreezeBodies();
            CaptureSnapshot();
            return true;
        }

        void CaptureSnapshot()
        {
            if (ballBody == null || arenaGeometry == null) return;
            m_Snapshot.TickId = m_TickId;
            m_Snapshot.EpisodeId = m_EpisodeId;
            m_Snapshot.BallPosition = new Vector2(ballBody.position.x, ballBody.position.z);
            m_Snapshot.BallVelocity = new Vector2(ballBody.linearVelocity.x, ballBody.linearVelocity.z);
            m_Snapshot.BallStallRecoveryActive = m_GlobalBallStall.IsActive;
            m_Snapshot.BallStallRecoverySequence = m_GlobalBallStall.Sequence;
            m_Snapshot.BallStationarySeconds = m_GlobalBallStall.StationarySeconds;
            m_Snapshot.GoalPauseActive = State == MNG_MatchState.GoalPause;
            m_Snapshot.EpisodeElapsedSeconds = EpisodeElapsedSeconds;
            m_Snapshot.EpisodeDurationSeconds = matchDurationSeconds;
            m_Snapshot.MatchRemainingSeconds = MatchRemainingSeconds;
            m_Snapshot.RedScore = RedScore;
            m_Snapshot.NavyScore = NavyScore;
            m_Snapshot.FieldHalfLength = arenaGeometry.HalfLength;
            m_Snapshot.FieldHalfWidth = arenaGeometry.HalfWidth;
            m_Snapshot.GoalHalfWidth = arenaGeometry.GoalHalfWidth;
            for (var i = 0; i < players.Length; i++)
            {
                var player = players[i];
                if (player != null)
                {
                    var state = player.CaptureState();
                    if (useRuntimeV2)
                        m_ExecutorsByIndex[GetIndex(player.Team, player.Slot)]?.CaptureExecution(ref state);
                    m_Snapshot.SetPlayer(player.Team, player.Slot, state);
                }
            }
            m_Snapshot.ValidateOrThrow();
            RefreshDecisionTargets(Team.Red);
            RefreshDecisionTargets(Team.Navy);
            m_HasSnapshot = true;
        }

        void RefreshDecisionTargets(Team team)
        {
            var decision = GetDecisionState(team);
            var targets = MNG_TacticalTargetResolver.Resolve(m_Snapshot, team, decision);
            decision.HasPassTarget = targets.HasPassTarget;
            decision.HasShotTarget = targets.HasShotTarget;
            decision.PendingPassReceiverSlot = targets.PassReceiverSlot;
            decision.ValidateOrThrow();
        }

        void CaptureStartingState()
        {
            m_BallStartingPosition = ballBody.position;
            m_BallStartingRotation = ballBody.rotation;
            for (var i = 0; i < m_AvatarsByIndex.Length; i++)
            {
                var avatar = m_AvatarsByIndex[i];
                if (avatar == null) continue;
                m_PlayerStartingPositions[i] = avatar.transform.position;
                m_PlayerStartingRotations[i] = avatar.transform.rotation;
            }
        }

        void ResetRound()
        {
            GetComponentInChildren<MNG_BallControl>()?.ResetLedger();
            m_GlobalBallStall.Reset();
            ballBody.isKinematic = false;
            ballBody.transform.SetPositionAndRotation(m_BallStartingPosition, m_BallStartingRotation);
            ballBody.position = m_BallStartingPosition;
            ballBody.rotation = m_BallStartingRotation;
            ballBody.linearVelocity = Vector3.zero;
            ballBody.angularVelocity = Vector3.zero;
            for (var i = 0; i < m_AvatarsByIndex.Length; i++)
            {
                var avatar = m_AvatarsByIndex[i];
                if (avatar == null) continue;
                // Spawn jitter writes Transform.position and SyncTransforms below.
                // Restore rotation on both sides first, or the old Transform yaw
                // can overwrite the Rigidbody reset and leak into observations.
                avatar.transform.SetPositionAndRotation(m_PlayerStartingPositions[i], m_PlayerStartingRotations[i]);
                avatar.Body.position = m_PlayerStartingPositions[i];
                avatar.Body.rotation = m_PlayerStartingRotations[i];
                avatar.Body.linearVelocity = Vector3.zero;
                avatar.Body.angularVelocity = Vector3.zero;
                avatar.ResetKickCooldown();
                m_ExecutorsByIndex[i]?.ResetForRound();
            }
            // Position variation is applied only at an initial spawn or a central
            // kickoff reset. No in-play transform correction is performed.
            ApplyRegularMatchSpawnJitter(m_EpisodeId, m_KickoffIndex);
            GetComponent<MNG_V2Trace>()?.RecordSpawn(m_EpisodeId, m_KickoffIndex);
            m_Snapshot.Possession = MNG_Possession.Neutral;
            m_Snapshot.Carrier = MNG_CarrierRef.None;
            State = MNG_MatchState.Playing;
            GoalPauseRemainingSeconds = 0f;
        }

        void UpdateGlobalBallStall(float deltaTime)
        {
            if (ballBody == null) return;
            m_GlobalBallStall.Update(
                new Vector2(ballBody.position.x, ballBody.position.z),
                new Vector2(ballBody.linearVelocity.x, ballBody.linearVelocity.z),
                deltaTime);
        }

        void FreezeBodies()
        {
            if (!ballBody.isKinematic)
            {
                ballBody.linearVelocity = Vector3.zero;
                ballBody.angularVelocity = Vector3.zero;
                ballBody.isKinematic = true;
            }
            for (var i = 0; i < m_AvatarsByIndex.Length; i++)
            {
                var avatar = m_AvatarsByIndex[i];
                if (avatar == null) continue;
                avatar.Body.linearVelocity = Vector3.zero;
                avatar.Body.angularVelocity = Vector3.zero;
            }
        }

        void FinishMatch()
        {
            State = MNG_MatchState.Finished;
            MatchRemainingSeconds = 0f;
            FreezeBodies();
            if (finishMode == MNG_MatchFinishMode.InterruptedCollection)
            {
                InterruptActivePolicyEpisodes();
            }
            else if (finishMode == MNG_MatchFinishMode.SelfPlayTerminalResult)
            {
                rewardEngine?.AwardSelfPlayTerminalResult(RedScore, NavyScore, m_EpisodeId);
                EndActivePolicyEpisodes();
            }
            else
            {
                if (RedScore > NavyScore) rewardEngine?.AwardMatchResult(Team.Red, m_EpisodeId);
                else if (NavyScore > RedScore) rewardEngine?.AwardMatchResult(Team.Navy, m_EpisodeId);
                EndActivePolicyEpisodes();
            }
            m_RestartPending = restartOnFinish;
            CaptureSnapshot();
        }

        public static Vector2[] CreateSpawnOffsets(long episode, int kickoff, int seed, bool mirror)
        {
            var random = new System.Random(CalculateSpawnSeed(episode, kickoff, seed));
            var original = new Vector2[8];
            for (var i = 0; i < 8; i++) original[i] = MNG_SpawnJitter.Sample(random);
            if (!mirror) return original;
            var result = new Vector2[8];
            for (var i = 0; i < 8; i++) result[i] = -original[(i + 4) % 8];
            return result;
        }

        public static int CalculateSpawnSeed(long episodeId, int kickoffIndex, int workerIdentity)
        {
            if (episodeId < 0) throw new ArgumentOutOfRangeException(nameof(episodeId));
            if (kickoffIndex < 0) throw new ArgumentOutOfRangeException(nameof(kickoffIndex));
            if (workerIdentity < 0) throw new ArgumentOutOfRangeException(nameof(workerIdentity));
            return unchecked(
                91073
                + (int)episodeId * 486187739
                + kickoffIndex * 16777619
                + workerIdentity * 104729);
        }

        void ValidateBindingsOrThrow()
        {
            if (arenaGeometry == null) throw new InvalidOperationException("MNG match requires SoccerArenaGeometry.");
            if (ballBody == null) throw new InvalidOperationException("MNG match requires one ball Rigidbody.");
            if (players == null || players.Length != MNG_MatchSnapshot.PlayersPerTeam * MNG_MatchSnapshot.TeamCount)
                throw new InvalidOperationException("MNG match requires exactly eight player avatars.");

            var occupied = new bool[MNG_MatchSnapshot.PlayersPerTeam * MNG_MatchSnapshot.TeamCount];
            Array.Clear(m_AvatarsByIndex, 0, m_AvatarsByIndex.Length);
            Array.Clear(m_ExecutorsByIndex, 0, m_ExecutorsByIndex.Length);
            for (var i = 0; i < players.Length; i++)
            {
                var player = players[i];
                if (player == null) throw new InvalidOperationException($"MNG player binding {i} is missing.");
                var index = (player.Team == Team.Red ? 0 : MNG_MatchSnapshot.PlayersPerTeam) + player.Slot;
                if (occupied[index]) throw new InvalidOperationException($"Duplicate MNG player slot {player.Team}/{player.Slot}.");
                occupied[index] = true;
                m_AvatarsByIndex[index] = player;
                m_ExecutorsByIndex[index] = player.GetComponent<MNG_PlayerSkillExecutor>();
            }
        }

        void DispatchTeamPlan(Team team, MNG_Command command, MNG_TeamDecisionState decision)
        {
            var teamIndex = team == Team.Red ? 0 : 1;
            var tasks = team == Team.Red ? m_RedTaskBuffer : m_NavyTaskBuffer;
            var revision = ++m_TaskRevisions[teamIndex];
            if (useRuntimeV2) MNG_TeamPlanner.PlanV2(m_Snapshot, team, command, decision,
                revision, EpisodeElapsedSeconds + 0.75f, tasks);
            else MNG_TeamPlanner.Plan(
                m_Snapshot,
                team,
                command,
                decision,
                revision,
                EpisodeElapsedSeconds + 0.75f,
                tasks);
            OverlayPassBuildRules(team, command, tasks, revision);
            OverlayCommonRules(team, tasks, revision);
            for (var slot = 0; slot < MNG_MatchSnapshot.PlayersPerTeam; slot++)
            {
                if (useRuntimeV2)
                {
                    if (!tasks[slot].CommonRule && !tasks[slot].PassBuildRule)
                    {
                        tasks[slot].ParentCommandId = revision * 2 + teamIndex;
                        tasks[slot].TaskId = tasks[slot].ParentCommandId * 4 + slot;
                    }
                    if (!tasks[slot].CommonRule && !tasks[slot].PassBuildRule && tasks[slot].Source != MNG_ActionSource.KeeperTechnique)
                        tasks[slot].Source = m_Managers[teamIndex] != null && m_Managers[teamIndex].isActiveAndEnabled
                            ? MNG_ActionSource.PolicyCommand : MNG_ActionSource.RuleCommand;
                    m_ExecutorsByIndex[GetIndex(team, slot)]?.RequestTaskV2(tasks[slot]);
                }
                else m_ExecutorsByIndex[GetIndex(team, slot)]?.SetTask(tasks[slot]);
            }
        }

        static int GetIndex(Team team, int slot)
            => (team == Team.Red ? 0 : MNG_MatchSnapshot.PlayersPerTeam) + slot;

        static void ResetDecisionState(MNG_TeamDecisionState decision)
        {
            decision.PreviousCommand = MNG_Command.Balanced;
            decision.CommandAgeSeconds = 0f;
            decision.PendingPassReceiverSlot = -1;
            decision.SecondsSincePossessionLoss = -1f;
            decision.HasPassTarget = false;
            decision.HasShotTarget = false;
            decision.PrimaryPresserSlot = -1;
            decision.PresserHoldUntilSeconds = 0f;
            for (var i = 0; i < decision.ControlMask.Length; i++) decision.ControlMask[i] = true;
        }
    }
}

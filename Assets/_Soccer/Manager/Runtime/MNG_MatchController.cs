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
        InterruptedCollection = 1
    }

    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    public sealed class MNG_MatchController : MonoBehaviour
    {
        public const float MatchDurationSeconds = 300f;

        [SerializeField] SoccerArenaGeometry arenaGeometry;
        [SerializeField] Rigidbody ballBody;
        [SerializeField] MNG_PlayerAvatar[] players = Array.Empty<MNG_PlayerAvatar>();
        [SerializeField] MNG_RewardEngine rewardEngine;
        [SerializeField] bool restartOnFinish = true;
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

        long m_TickId;
        long m_EpisodeId;
        long m_GoalId;
        int m_KickoffIndex;
        long m_SpawnPlacementRevision;
        bool m_HasSnapshot;
        bool m_RestartPending;
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
        public event Action<Team, MNG_Command, long> CommandAccepted;
        public event Action<Team, long> GoalScored;

        void Awake()
        {
            ValidateBindingsOrThrow();
            CaptureStartingState();
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
            CaptureSnapshot();
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

            decision.PreviousCommand = command;
            decision.CommandAgeSeconds = 0f;
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
            rewardEngine?.ResetEpisode();
            ResetRound();
            CaptureSnapshot();
        }

        void ApplyRegularMatchSpawnJitter(long episodeId, int kickoffIndex)
        {
            var random = new System.Random(unchecked(
                91073 + (int)episodeId * 486187739 + kickoffIndex * 16777619));
            for (var index = 0; index < m_AvatarsByIndex.Length; index++)
            {
                var avatar = m_AvatarsByIndex[index];
                if (avatar == null) continue;
                var offset = MNG_SpawnJitter.Sample(random);
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
            if (scoringTeam == Team.Red) RedScore++;
            else NavyScore++;
            State = MNG_MatchState.GoalPause;
            GoalPauseRemainingSeconds = 3f;
            m_GoalId++;
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
                if (player != null) m_Snapshot.SetPlayer(player.Team, player.Slot, player.CaptureState());
            }
            m_Snapshot.ValidateOrThrow();
            RefreshDecisionTargets(Team.Red);
            RefreshDecisionTargets(Team.Navy);
            m_HasSnapshot = true;
        }

        void RefreshDecisionTargets(Team team)
        {
            var targets = MNG_TacticalTargetResolver.Resolve(m_Snapshot, team);
            var decision = GetDecisionState(team);
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
            ballBody.isKinematic = false;
            ballBody.position = m_BallStartingPosition;
            ballBody.rotation = m_BallStartingRotation;
            ballBody.linearVelocity = Vector3.zero;
            ballBody.angularVelocity = Vector3.zero;
            for (var i = 0; i < m_AvatarsByIndex.Length; i++)
            {
                var avatar = m_AvatarsByIndex[i];
                if (avatar == null) continue;
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
            m_Snapshot.Possession = MNG_Possession.Neutral;
            m_Snapshot.Carrier = MNG_CarrierRef.None;
            State = MNG_MatchState.Playing;
            GoalPauseRemainingSeconds = 0f;
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
            else
            {
                if (RedScore > NavyScore) rewardEngine?.AwardMatchResult(Team.Red, m_EpisodeId);
                else if (NavyScore > RedScore) rewardEngine?.AwardMatchResult(Team.Navy, m_EpisodeId);
                EndActivePolicyEpisodes();
            }
            m_RestartPending = restartOnFinish;
            CaptureSnapshot();
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
            MNG_TeamPlanner.Plan(
                m_Snapshot,
                team,
                command,
                decision,
                revision,
                EpisodeElapsedSeconds + 0.75f,
                tasks);
            for (var slot = 0; slot < MNG_MatchSnapshot.PlayersPerTeam; slot++)
                m_ExecutorsByIndex[GetIndex(team, slot)]?.SetTask(tasks[slot]);
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

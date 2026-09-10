using System.Collections.Generic;
using System.Linq;
using Unity.MLAgents;
using UnityEngine;

namespace MachineLearning.Soccer.Curriculum
{
    public enum SoccerCurriculumLesson
    {
        L0BallApproach = 0,
        L1CarryAndShoot = 1,
        L2ShortPass = 2,
        L3ProgressivePlay = 3,
        // Append new lessons so existing serialized L3 assets retain value 3.
        L2Find = 4,
        L2Score = 5
    }

    public enum SoccerL1Phase
    {
        ShortRangeFinish = 1,
        CarryAndScore = 2
    }

    /// <summary>
    /// Base Stadium의 정책 계약은 유지하면서 짧은 L0/L1 과제만 구성한다.
    /// Navy는 SoccerEnvController가 그룹을 등록한 첫 리셋 이후 비활성화되어 Trainer에 참여하지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed partial class SoccerCurriculumController : MonoBehaviour
    {
        public const string L1PhaseParameter = "soccer_l1_phase";
        public const string L1AlignmentParameter = "soccer_l1_alignment";
        public const string L1SpawnDifficultyParameter = "soccer_l1_spawn_difficulty";
        public const string L2FindNearSpawnProbabilityParameter = "soccer_l2_find_near_spawn_probability";
        public const string L2FindMaximumInitialHeadingErrorParameter =
            "soccer_l2_find_maximum_initial_heading_error";
        public const float L1RequiredDribbleProgress = 2f;
        public const float L1ShortFinishRequiredCarryProgress = 0f;
        public const float L1FinalRequiredCarryProgress = 8f;
        public const float SupportMinimumDistance = 6f;
        public const float FieldSupportMaximumDistance = 24f;
        public const float KeeperSupportMaximumDistance = 70f;
        public const float SupportMinimumSeparation = 5f;

        const float L0MinimumBallDistance = 1f;
        const float L0MaximumBallDistance = 2f;
        const float L0MaximumLateralOffset = 1f;
        const float L1MinimumBallDistance = 1f;
        const float L1MaximumBallDistance = 3f;
        const float L1MaximumLateralOffset = 1.5f;
        const float ControlledBallDistance = 2.4f;
        const float MinimumRewardedProgress = 0.05f;
        const float ApproachRewardPerMeter = 0.1f;
        const float ApproachRewardLimit = 0.15f;
        const float PossessionReward = 0.2f;
        const float L1PossessionReward = 0.1f;
        const float DribbleRewardPerMeter = 0.025f;
        const float DribbleRewardLimit = 0.2f;
        const float ShotAttemptReward = 0.08f;
        const float ShotOnTargetReward = 0.2f;
        const float GoalReward = 0.4f;
        const float AlignmentRewardPerDegree = 0.002f;
        const float AlignmentRewardLimit = 0.08f;
        const float NoShotFinishLineDistance = 4f;
        const float SupportSampleSeconds = 0.5f;
        const float SupportRewardPerSample = 0.005f;
        const float SupportRewardLimitPerAgent = 0.02f;
        const float MaximumSupportDistanceRegressionPerSample = 0.35f;

        [SerializeField] SoccerCurriculumLesson lesson;
        [SerializeField, Min(0f)] float groupSuccessReward = 0.4f;
        [SerializeField, Min(0f)] float individualSuccessReward = 0.1f;

        readonly Dictionary<AgentSoccer, float> m_LastSupportDistances = new();
        readonly Dictionary<AgentSoccer, float> m_SupportRewardTotals = new();
        SoccerEnvController m_Environment;
        SoccerRewardEngine m_RewardEngine;
        AgentSoccer[] m_RedAgents = System.Array.Empty<AgentSoccer>();
        AgentSoccer m_FocusAgent;
        AgentSoccer m_CurrentCarrier;
        AgentSoccer m_LastShooter;
        float m_LastShotTime;
        bool m_ShotWasRetouched;
        bool m_HasConfiguredRound;
        bool m_RoundSucceeded;
        bool m_PossessionEstablished;
        bool m_ShotAttempted;
        bool m_ShotOnTarget;
        bool m_ValidShotTaken;
        bool m_GoalScored;
        bool m_CarryKickUnlocked;
        float m_ControlStartBallX;
        float m_BestFocusBallDistance;
        float m_BestDribbleProgress;
        float m_ApproachRewardTotal;
        float m_DribbleRewardTotal;
        float m_AlignmentRewardTotal;
        float m_BestShotAlignmentError;
        bool m_AlignmentEnabled;
        float m_RoundSpawnDifficulty = 1f;
        float m_L2FindNearSpawnProbability;
        float m_L2FindMaximumInitialHeadingError = 180f;
        float m_NextSupportSampleTime;
        float m_RoundStartTime;
        float m_SupportShapeTotal;
        int m_SupportShapeSamples;
        int m_RoundIndex;
        int m_RedScoreAtRoundStart;
        int m_NavyScoreAtRoundStart;
        SoccerL1Phase m_L1Phase = SoccerL1Phase.CarryAndScore;
        SoccerL1Phase m_RoundL1Phase = SoccerL1Phase.CarryAndScore;

        public SoccerCurriculumLesson Lesson => lesson;
        public float GroupSuccessReward => groupSuccessReward;
        public float IndividualSuccessReward => individualSuccessReward;
        public AgentSoccer FocusAgent => m_FocusAgent;
        public SoccerL1Phase L1Phase => m_L1Phase;

        public void Configure(SoccerCurriculumLesson configuredLesson)
        {
            lesson = configuredLesson;
            groupSuccessReward = 0.4f;
            individualSuccessReward = 0.1f;
        }

        void Awake()
        {
            m_Environment = GetComponent<SoccerEnvController>();
            m_RewardEngine = GetComponent<SoccerRewardEngine>();
            if (m_Environment == null || m_RewardEngine == null)
            {
                Debug.LogError("SoccerCurriculumController requires SoccerEnvController and SoccerRewardEngine.", this);
                enabled = false;
                return;
            }

            m_RewardEngine.ConfigureCurriculumRewardMode(true);
            if (lesson == SoccerCurriculumLesson.L0BallApproach)
            {
                m_Environment.ConfigureCurriculumConstraints(false);
            }
            else if (lesson == SoccerCurriculumLesson.L1CarryAndShoot)
            {
                m_Environment.ConfigureCurriculumConstraints(true);
                if (Academy.IsInitialized)
                {
                    ApplyL1Phase(Academy.Instance.EnvironmentParameters.GetWithDefault(
                        L1PhaseParameter,
                        (float)SoccerL1Phase.CarryAndScore));
                    Academy.Instance.EnvironmentParameters.RegisterCallback(L1PhaseParameter, ApplyL1Phase);
                }

                m_Environment.BallStrikeCompleted += HandleBallStrike;
                m_Environment.BallTouchCompleted += HandleBallTouch;
            }
            else if (lesson is SoccerCurriculumLesson.L2ShortPass or SoccerCurriculumLesson.L3ProgressivePlay)
            {
                m_RewardEngine.ConfigureCurriculumRewardStage(lesson == SoccerCurriculumLesson.L3ProgressivePlay
                    ? SoccerCurriculumRewardStage.L3ProgressivePlay : SoccerCurriculumRewardStage.L2Pass);
                m_Environment.ConfigureCurriculumConstraints(true, true);
                m_Environment.ConfigureNeuralShotAdvice(false);
                m_Environment.BallStrikeCompleted += HandleL2Strike;
                m_Environment.BallTouchCompleted += HandleL2Touch;
            }
            else if (lesson is SoccerCurriculumLesson.L2Find or SoccerCurriculumLesson.L2Score)
            {
                m_RewardEngine.ConfigureCurriculumRewardStage(lesson == SoccerCurriculumLesson.L2Find
                    ? SoccerCurriculumRewardStage.L2Find : SoccerCurriculumRewardStage.L2Score);
                m_Environment.ConfigureCurriculumConstraints(lesson == SoccerCurriculumLesson.L2Score, true);
                m_Environment.ConfigureNeuralShotAdvice(false);
                m_Environment.BallStrikeCompleted += HandleFindScoreStrike;
                m_Environment.BallTouchCompleted += HandleFindScoreTouch;
                m_Environment.MatchTimedOut += HandleFindScoreTimeout;
            }
            m_Environment.RoundResetCompleted += ConfigureRound;
        }

        void OnDestroy()
        {
            if (m_Environment != null)
            {
                m_Environment.ClearCurriculumWaitingAgents();
                m_Environment.ConfigureCurriculumPassAimGate(false);
                m_Environment.RoundResetCompleted -= ConfigureRound;
                m_Environment.BallStrikeCompleted -= HandleBallStrike;
                m_Environment.BallTouchCompleted -= HandleBallTouch;
                m_Environment.BallStrikeCompleted -= HandleL2Strike;
                m_Environment.BallTouchCompleted -= HandleL2Touch;
                m_Environment.BallStrikeCompleted -= HandleFindScoreStrike;
                m_Environment.BallTouchCompleted -= HandleFindScoreTouch;
                m_Environment.MatchTimedOut -= HandleFindScoreTimeout;
            }
        }

        void FixedUpdate()
        {
            if (!m_HasConfiguredRound || m_RoundSucceeded)
            {
                return;
            }

            if (lesson == SoccerCurriculumLesson.L1CarryAndShoot
                && m_Environment.RedScore > m_RedScoreAtRoundStart)
            {
                HandleRedGoal();
                return;
            }

            if (lesson == SoccerCurriculumLesson.L1CarryAndShoot
                && m_Environment.NavyScore > m_NavyScoreAtRoundStart)
            {
                m_Environment.CompleteTrainingDrill();
                return;
            }

            if (lesson == SoccerCurriculumLesson.L2Score
                && (m_Environment.RedScore > m_RedScoreAtRoundStart
                    || m_Environment.NavyScore > m_NavyScoreAtRoundStart))
            {
                HandleL2ScoreGoal(m_Environment.RedScore > m_RedScoreAtRoundStart);
                return;
            }

            if (!m_Environment.IsPlayActive)
            {
                return;
            }

            if (lesson == SoccerCurriculumLesson.L0BallApproach)
            {
                UpdateL0Lesson();
                return;
            }

            if (lesson == SoccerCurriculumLesson.L2Find)
            {
                UpdateL2FindLesson();
                return;
            }

            if (lesson == SoccerCurriculumLesson.L2Score)
            {
                UpdateL2ScoreLesson();
                return;
            }

            if (lesson is SoccerCurriculumLesson.L2ShortPass or SoccerCurriculumLesson.L3ProgressivePlay)
                UpdateL2Lesson();
            else
                UpdateL1Lesson();
        }

        void UpdateL0Lesson()
        {
            var carrier = GetControlledRedCarrier();
            if (carrier == null)
            {
                m_CurrentCarrier = null;
                RewardFocusApproachProgress();
                return;
            }

            if (!m_PossessionEstablished)
            {
                m_PossessionEstablished = true;
                m_RewardEngine.AwardCurriculumIndividualReward(
                    carrier,
                    SoccerRewardKind.CurriculumPossessionEstablished,
                    PossessionReward);
            }

            CompleteLesson(carrier);
        }

        void UpdateL1Lesson()
        {
            var carrier = GetControlledRedCarrier();
            if (carrier == null)
            {
                // A brief loss of confirmed control does not erase this player's
                // carry progress. A different Red carrier still resets the task.
                return;
            }

            if (m_CurrentCarrier != carrier)
            {
                m_CurrentCarrier = carrier;
                m_ControlStartBallX = m_Environment.Ball.transform.position.x;
                m_BestDribbleProgress = 0f;
                m_LastSupportDistances.Clear();
                m_NextSupportSampleTime = Time.time;
                if (m_RoundL1Phase == SoccerL1Phase.CarryAndScore)
                {
                    m_CarryKickUnlocked = false;
                    m_Environment.ConfigureCurriculumConstraints(false, true);
                    m_Environment.ConfigureNeuralShotAdvice(false);
                }
            }

            if (!m_PossessionEstablished)
            {
                m_PossessionEstablished = true;
                m_RewardEngine.AwardCurriculumIndividualReward(
                    carrier,
                    SoccerRewardKind.CurriculumPossessionEstablished,
                    L1PossessionReward);
            }

            RewardControlledDribbleProgress(carrier);
            if (!m_CarryKickUnlocked
                && IsL1KickUnlocked(m_RoundL1Phase, m_BestDribbleProgress))
            {
                m_CarryKickUnlocked = true;
                m_Environment.ConfigureCurriculumConstraints(true, true);
                m_Environment.ConfigureNeuralShotAdvice(true);
                if (Academy.IsInitialized)
                {
                    var recorder = Academy.Instance.StatsRecorder;
                    recorder.Add("Soccer/Skill Advice/Carry Gate Unlocked", 1f, StatAggregationMethod.Sum);
                    recorder.Add("Soccer/Red/Skill Advice/Carry Gate Unlocked", 1f, StatAggregationMethod.Sum);
                }
            }
            RewardShotAlignment(carrier);
            RewardSupportShape(carrier);
        }

        void RewardShotAlignment(AgentSoccer carrier)
        {
            if (!m_AlignmentEnabled || m_RoundL1Phase != SoccerL1Phase.ShortRangeFinish
                || m_ShotAttempted || !m_Environment.CanRequestKick(carrier)
                || carrier.KickPlate == null || !carrier.KickPlate.CanKick)
                return;

            var ballPosition = m_Environment.Ball.transform.position;
            var direction = AgentSoccer.CalculateBallPushDirection(
                carrier.transform.position, carrier.transform.forward, ballPosition, true);
            var error = GetShotAlignmentError(ballPosition, direction, carrier.Team, m_Environment.ArenaGeometry);
            if (float.IsNaN(m_BestShotAlignmentError))
            {
                // Establish a baseline, not a reward for standing in an already aligned spawn.
                m_BestShotAlignmentError = error;
                return;
            }
            var requested = CalculateShotAlignmentReward(m_BestShotAlignmentError, error, m_AlignmentRewardTotal);
            if (requested <= 0f) return;
            m_BestShotAlignmentError = error;
            m_AlignmentRewardTotal += m_RewardEngine.AwardCurriculumIndividualReward(
                carrier, SoccerRewardKind.CurriculumShotAlignment, requested);
        }

        public static float CalculateShotAlignmentReward(float bestError, float currentError, float awarded)
        {
            if (float.IsNaN(bestError) || float.IsInfinity(bestError)
                || float.IsNaN(currentError) || float.IsInfinity(currentError)
                || float.IsNaN(awarded) || float.IsInfinity(awarded)) return 0f;
            var improvement = Mathf.Clamp(bestError, 0f, 180f) - Mathf.Clamp(currentError, 0f, 180f);
            return improvement < 0.5f ? 0f : Mathf.Min(
                improvement * AlignmentRewardPerDegree, Mathf.Max(0f, AlignmentRewardLimit - Mathf.Max(0f, awarded)));
        }

        public static float GetShotAlignmentError(
            Vector3 ballPosition, Vector3 direction, Team team, SoccerArenaGeometry arena)
        {
            var halfLength = arena != null ? arena.HalfLength : SoccerArenaGeometry.StadiumHalfLength;
            var goalDirection = new Vector3((team == Team.Red ? halfLength : -halfLength) - ballPosition.x, 0f, -ballPosition.z);
            direction.y = 0f;
            return direction.sqrMagnitude < 0.0001f || goalDirection.sqrMagnitude < 0.0001f
                ? 180f : Vector3.Angle(direction, goalDirection);
        }

        void ApplyL1Phase(float value)
        {
            m_L1Phase = value < 1.5f
                ? SoccerL1Phase.ShortRangeFinish
                : SoccerL1Phase.CarryAndScore;
            m_Environment?.ConfigureCurriculumConstraints(true, true);
            m_RewardEngine?.ConfigureCurriculumRewardStage(m_L1Phase == SoccerL1Phase.ShortRangeFinish
                ? SoccerCurriculumRewardStage.L1Finish : SoccerCurriculumRewardStage.L1Carry);
        }

        void HandleBallStrike(
            AgentSoccer actor,
            int kickAction,
            Vector3 kickDirection,
            bool safetyRedirected)
        {
            if (lesson != SoccerCurriculumLesson.L1CarryAndShoot
                || m_RoundSucceeded
                || actor == null
                || actor.Team != Team.Red
                || kickAction == 0
                || safetyRedirected
                || !m_PossessionEstablished)
            {
                return;
            }

            var isFirstShot = !m_ShotAttempted;
            if (isFirstShot)
            {
                m_ShotAttempted = true;
                m_RewardEngine.AwardCurriculumIndividualReward(
                    actor,
                    SoccerRewardKind.CurriculumShotAttempt,
                    ShotAttemptReward);
            }

            var projectedOnTarget = IsShotOnTarget(
                m_Environment.Ball.transform.position,
                kickDirection,
                actor.Team,
                m_Environment.ArenaGeometry);
            if (Academy.IsInitialized)
            {
                var recorder = Academy.Instance.StatsRecorder;
                recorder.Add("Soccer/Curriculum/L1/Shot Error Degrees",
                    GetShotAlignmentError(m_Environment.Ball.transform.position, kickDirection,
                        actor.Team, m_Environment.ArenaGeometry), StatAggregationMethod.Average);
                recorder.Add("Soccer/Curriculum/L1/Strong Shot Fraction",
                    kickAction == 2 ? 1f : 0f, StatAggregationMethod.Average);
                if (isFirstShot)
                {
                    // One sample per episode that contains a shot; not all retry kicks.
                    recorder.Add("Soccer/Curriculum/L1/First Shot Error Degrees",
                        GetShotAlignmentError(m_Environment.Ball.transform.position, kickDirection,
                            actor.Team, m_Environment.ArenaGeometry), StatAggregationMethod.Average);
                    recorder.Add("Soccer/Curriculum/L1/First Shot On Target",
                        projectedOnTarget ? 1f : 0f, StatAggregationMethod.Average);
                    recorder.Add("Soccer/Curriculum/L1/First Shot Strong Fraction",
                        kickAction == 2 ? 1f : 0f, StatAggregationMethod.Average);
                    var halfLength = m_Environment.ArenaGeometry != null
                        ? m_Environment.ArenaGeometry.HalfLength : SoccerArenaGeometry.StadiumHalfLength;
                    recorder.Add("Soccer/Curriculum/L1/First Shot Goal Distance",
                        PlanarDistance(m_Environment.Ball.transform.position, new Vector3(halfLength, 0f, 0f)),
                        StatAggregationMethod.Average);
                }
            }
            if (projectedOnTarget && !m_ShotOnTarget)
            {
                m_ShotOnTarget = true;
                m_RewardEngine.AwardCurriculumIndividualReward(
                    actor,
                    SoccerRewardKind.CurriculumShotOnTarget,
                    ShotOnTargetReward);
            }

            var carryRequirementMet = m_CurrentCarrier == actor
                && m_BestDribbleProgress + 0.001f >= GetRequiredCarryProgress(m_RoundL1Phase);
            m_ValidShotTaken = carryRequirementMet;
            m_LastShooter = actor;
            m_LastShotTime = Time.time;
            m_ShotWasRetouched = false;
        }

        void HandleBallTouch(AgentSoccer actor)
        {
            // Ignore the same extending plate's collision-stay callbacks, but not
            // a later dribble push. A fresh explicit kick will open a new flight.
            if (m_LastShooter != null && Time.time > m_LastShotTime + 0.1f
                && (actor != m_LastShooter || actor.KickPlate == null || !actor.KickPlate.IsStrikeActive))
                m_ShotWasRetouched = true;
        }

        public static bool IsEligibleScoringStrike(bool validShot, bool retouched, float flightSeconds)
        {
            return validShot && !retouched && flightSeconds >= 0f && flightSeconds <= 8f;
        }

        void HandleRedGoal()
        {
            if (m_GoalScored)
            {
                return;
            }

            m_GoalScored = true;
            if (!IsEligibleScoringStrike(m_ValidShotTaken, m_ShotWasRetouched, Time.time - m_LastShotTime))
            {
                m_Environment.CompleteTrainingDrill();
                return;
            }

            var actor = m_LastShooter ?? m_FocusAgent;
            if (!m_ShotOnTarget)
            {
                m_ShotOnTarget = true;
                m_RewardEngine.AwardCurriculumIndividualReward(
                    actor,
                    SoccerRewardKind.CurriculumShotOnTarget,
                    ShotOnTargetReward);
            }

            m_RewardEngine.AwardCurriculumIndividualReward(
                actor,
                SoccerRewardKind.CurriculumGoal,
                GoalReward);
            CompleteLesson(actor);
        }

        AgentSoccer GetControlledRedCarrier()
        {
            var carrier = m_Environment.BallCarrier;
            if (carrier == null
                || carrier.Team != Team.Red
                || !carrier.isActiveAndEnabled
                || Vector3.Distance(carrier.transform.position, m_Environment.Ball.transform.position)
                    > ControlledBallDistance)
            {
                return null;
            }

            return carrier;
        }

        void RewardFocusApproachProgress()
        {
            if (m_FocusAgent == null || !m_FocusAgent.isActiveAndEnabled)
            {
                return;
            }

            var currentDistance = PlanarDistance(
                m_FocusAgent.transform.position,
                m_Environment.Ball.transform.position);
            var improvement = m_BestFocusBallDistance - currentDistance;
            if (improvement < MinimumRewardedProgress)
            {
                return;
            }

            m_BestFocusBallDistance = currentDistance;
            var remaining = ApproachRewardLimit - m_ApproachRewardTotal;
            var requested = Mathf.Min(improvement * ApproachRewardPerMeter, remaining);
            if (requested <= 0f)
            {
                return;
            }

            m_ApproachRewardTotal += m_RewardEngine.AwardCurriculumIndividualReward(
                m_FocusAgent,
                SoccerRewardKind.CurriculumApproachProgress,
                requested);
        }

        void RewardControlledDribbleProgress(AgentSoccer carrier)
        {
            var progress = m_Environment.Ball.transform.position.x - m_ControlStartBallX;
            var improvement = progress - m_BestDribbleProgress;
            if (improvement < MinimumRewardedProgress)
            {
                return;
            }

            m_BestDribbleProgress = progress;
            if (m_RoundL1Phase == SoccerL1Phase.ShortRangeFinish)
            {
                return;
            }

            var remaining = DribbleRewardLimit - m_DribbleRewardTotal;
            var requested = Mathf.Min(improvement * DribbleRewardPerMeter, remaining);
            if (requested <= 0f)
            {
                return;
            }

            m_DribbleRewardTotal += m_RewardEngine.AwardCurriculumIndividualReward(
                carrier,
                SoccerRewardKind.CurriculumDribbleProgress,
                requested);
        }

        void RewardSupportShape(AgentSoccer carrier)
        {
            if (Time.time < m_NextSupportSampleTime)
            {
                return;
            }

            m_NextSupportSampleTime = Time.time + SupportSampleSeconds;
            var supporters = m_RedAgents
                .Where(agent => agent != null && agent != carrier && agent.isActiveAndEnabled)
                .ToArray();
            if (supporters.Length != 3)
            {
                return;
            }

            var validCount = 0;
            foreach (var supporter in supporters)
            {
                // A position held by curriculum code is not a learned support action.
                if (m_Environment.IsCurriculumWaitingAgent(supporter)) continue;
                var otherSupportPositions = supporters
                    .Where(other => other != supporter)
                    .Select(other => other.transform.position)
                    .ToArray();
                var currentDistance = PlanarDistance(supporter.transform.position, carrier.transform.position);
                var maintainedDistance = !m_LastSupportDistances.TryGetValue(supporter, out var previousDistance)
                    || currentDistance <= previousDistance + MaximumSupportDistanceRegressionPerSample;
                m_LastSupportDistances[supporter] = currentDistance;

                if (!maintainedDistance
                    || !IsValidSupportShape(
                        supporter.transform.position,
                        supporter.PositionRole == AgentSoccer.Position.DefenderKeeper,
                        carrier.transform.position,
                        otherSupportPositions))
                {
                    continue;
                }

                validCount++;
                m_SupportRewardTotals.TryGetValue(supporter, out var awarded);
                var requested = Mathf.Min(SupportRewardPerSample, SupportRewardLimitPerAgent - awarded);
                if (requested <= 0f)
                {
                    continue;
                }

                var applied = m_RewardEngine.AwardCurriculumIndividualReward(
                    supporter,
                    SoccerRewardKind.CurriculumSupportShape,
                    requested);
                m_SupportRewardTotals[supporter] = awarded + applied;
            }

            var quality = validCount / 3f;
            m_SupportShapeTotal += quality;
            m_SupportShapeSamples++;
            if (Academy.IsInitialized)
            {
                Academy.Instance.StatsRecorder.Add(
                    $"Soccer/Curriculum/{GetLessonName()}/Support Shape",
                    quality,
                    StatAggregationMethod.Average);
            }
        }

        void ConfigureRound()
        {
            RecordPreviousRound();
            m_Environment.ClearCurriculumWaitingAgents();
            m_Environment.ConfigureCurriculumPassAimGate(false);
            m_Environment.ConfigureCurriculumPassAdvice(false);
            m_Environment.ConfigureCurriculumPassSupport(null, null);

            if (lesson == SoccerCurriculumLesson.L1CarryAndShoot && Academy.IsInitialized)
            {
                ApplyL1Phase(Academy.Instance.EnvironmentParameters.GetWithDefault(
                    L1PhaseParameter,
                    (float)m_L1Phase));
            }

            foreach (var item in m_Environment.AgentsList)
            {
                var agent = item?.Agent;
                if (agent == null || agent.Team != Team.Navy)
                {
                    continue;
                }

                if (item.Rb != null)
                {
                    item.Rb.linearVelocity = Vector3.zero;
                    item.Rb.angularVelocity = Vector3.zero;
                }

                agent.gameObject.SetActive(false);
            }

            m_RedAgents = m_Environment.AgentsList
                .Select(item => item?.Agent)
                .Where(agent => agent != null && agent.Team == Team.Red && agent.isActiveAndEnabled)
                .OrderBy(agent => (int)agent.PositionRole)
                .ThenBy(agent => agent.name)
                .ToArray();
            if (m_RedAgents.Length != 4)
            {
                Debug.LogError($"Curriculum requires four active Red agents, but found {m_RedAgents.Length}.", this);
                enabled = false;
                return;
            }

            var eligibleFocusAgents = m_RedAgents
                .Where(agent => agent.PositionRole != AgentSoccer.Position.DefenderKeeper)
                .ToArray();
            m_FocusAgent = eligibleFocusAgents[m_RoundIndex % eligibleFocusAgents.Length];
            m_RoundIndex++;
            m_RoundSucceeded = false;
            m_PossessionEstablished = false;
            m_ShotAttempted = false;
            m_ShotOnTarget = false;
            m_ValidShotTaken = false;
            m_GoalScored = false;
            m_CarryKickUnlocked = false;
            m_CurrentCarrier = null;
            m_LastShooter = null;
            m_LastShotTime = float.NegativeInfinity;
            m_ShotWasRetouched = false;
            m_ApproachRewardTotal = 0f;
            m_DribbleRewardTotal = 0f;
            m_AlignmentRewardTotal = 0f;
            m_BestShotAlignmentError = float.NaN;
            m_AlignmentEnabled = lesson == SoccerCurriculumLesson.L1CarryAndShoot && Academy.IsInitialized
                && Academy.Instance.EnvironmentParameters.GetWithDefault(L1AlignmentParameter, 0f) > 0.5f;
            m_BestDribbleProgress = 0f;
            m_SupportShapeTotal = 0f;
            m_SupportShapeSamples = 0;
            m_LastSupportDistances.Clear();
            m_SupportRewardTotals.Clear();
            ResetL2FindScoreRound();
            m_RoundStartTime = Time.time;
            m_RedScoreAtRoundStart = m_Environment.RedScore;
            m_NavyScoreAtRoundStart = m_Environment.NavyScore;
            m_RoundL1Phase = m_L1Phase;
            m_CarryKickUnlocked = m_RoundL1Phase == SoccerL1Phase.ShortRangeFinish;
            m_Environment.ConfigureCurriculumConstraints(
                lesson is SoccerCurriculumLesson.L2ShortPass or SoccerCurriculumLesson.L3ProgressivePlay
                    || lesson == SoccerCurriculumLesson.L2Score
                    || m_CarryKickUnlocked, true);
            m_Environment.ConfigureNeuralShotAdvice(
                lesson == SoccerCurriculumLesson.L1CarryAndShoot
                && m_RoundL1Phase == SoccerL1Phase.ShortRangeFinish);
            var requestedSpawnDifficulty = Academy.IsInitialized
                ? Academy.Instance.EnvironmentParameters.GetWithDefault(L1SpawnDifficultyParameter, 1f) : 1f;
            m_RoundSpawnDifficulty = GetL1SpawnDifficulty(m_RoundL1Phase, requestedSpawnDifficulty);
            m_L2FindNearSpawnProbability = lesson == SoccerCurriculumLesson.L2Find && Academy.IsInitialized
                ? Mathf.Clamp01(Academy.Instance.EnvironmentParameters.GetWithDefault(
                    L2FindNearSpawnProbabilityParameter, 0f))
                : 0f;
            m_L2FindMaximumInitialHeadingError = lesson == SoccerCurriculumLesson.L2Find
                && Academy.IsInitialized
                ? Mathf.Clamp(Academy.Instance.EnvironmentParameters.GetWithDefault(
                    L2FindMaximumInitialHeadingErrorParameter, 180f), 0f, 180f)
                : 180f;

            if (lesson == SoccerCurriculumLesson.L1CarryAndShoot)
            {
                PositionL1Agents();
            }
            else if (lesson is SoccerCurriculumLesson.L2ShortPass or SoccerCurriculumLesson.L3ProgressivePlay)
            {
                ResetL2Round();
                ResetL3Round();
                PositionL2Agents();
            }

            var minimumDistance = lesson == SoccerCurriculumLesson.L0BallApproach
                ? L0MinimumBallDistance
                : L1MinimumBallDistance;
            var maximumDistance = lesson == SoccerCurriculumLesson.L0BallApproach
                ? L0MaximumBallDistance
                : L1MaximumBallDistance;
            var maximumLateralOffset = lesson == SoccerCurriculumLesson.L0BallApproach
                ? L0MaximumLateralOffset
                : L1MaximumLateralOffset * m_RoundSpawnDifficulty;
            if (lesson is SoccerCurriculumLesson.L2ShortPass or SoccerCurriculumLesson.L3ProgressivePlay)
            {
                minimumDistance = 1f;
                maximumDistance = 2f;
                maximumLateralOffset = 0.5f * m_L2SpawnDifficulty;
            }
            var arena = m_Environment.ArenaGeometry;
            var ballPosition = lesson is SoccerCurriculumLesson.L2Find or SoccerCurriculumLesson.L2Score
                ? SampleL2FindScoreBallPosition(arena, m_Environment.Ball.transform.position.y)
                : m_FocusAgent.transform.position
                    + Vector3.right * Random.Range(minimumDistance, maximumDistance)
                    + Vector3.forward * Random.Range(-maximumLateralOffset, maximumLateralOffset);
            if (arena != null && lesson is not SoccerCurriculumLesson.L2Find and not SoccerCurriculumLesson.L2Score)
            {
                ballPosition.x = Mathf.Clamp(ballPosition.x, -arena.HalfLength + 2f, arena.HalfLength - 2f);
                ballPosition.z = Mathf.Clamp(ballPosition.z, -arena.HalfWidth + 2f, arena.HalfWidth - 2f);
            }

            ballPosition.y = m_Environment.Ball.transform.position.y;
            m_Environment.Ball.transform.position = ballPosition;
            if (m_Environment.ballRb != null)
            {
                m_Environment.ballRb.isKinematic = false;
                m_Environment.ballRb.linearVelocity = Vector3.zero;
                m_Environment.ballRb.angularVelocity = Vector3.zero;
                m_Environment.ballRb.Sleep();
            }

            if (lesson is not SoccerCurriculumLesson.L2Find and not SoccerCurriculumLesson.L2Score)
            {
                var lookDirection = ballPosition - m_FocusAgent.transform.position;
                lookDirection.y = 0f;
                if (lookDirection.sqrMagnitude > 0.0001f)
                    m_FocusAgent.transform.rotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
            }

            m_BestFocusBallDistance = PlanarDistance(m_FocusAgent.transform.position, ballPosition);
            InitializeL2FindScoreDistances(ballPosition);
            m_HasConfiguredRound = true;
        }

        void PositionL1Agents()
        {
            var arena = m_Environment.ArenaGeometry;
            var halfLength = arena != null ? arena.HalfLength : SoccerArenaGeometry.StadiumHalfLength;
            var focusX = m_RoundL1Phase == SoccerL1Phase.ShortRangeFinish
                ? halfLength - Random.Range(20f, 24f)
                : halfLength - Random.Range(30f, 36f);
            var focusZ = Random.Range(-4f, 4f);
            MoveAgent(m_FocusAgent, new Vector3(focusX, m_FocusAgent.transform.position.y, focusZ));

            var fieldSupporters = m_RedAgents
                .Where(agent => agent != m_FocusAgent
                    && agent.PositionRole != AgentSoccer.Position.DefenderKeeper)
                .ToArray();
            for (var index = 0; index < fieldSupporters.Length; index++)
            {
                var side = index == 0 ? -1f : 1f;
                MoveAgent(fieldSupporters[index], new Vector3(
                    focusX - 10f - index * 2f,
                    fieldSupporters[index].transform.position.y,
                    Mathf.Clamp(focusZ + side * 10f, -18f, 18f)));
            }

            var keeper = m_RedAgents.FirstOrDefault(agent =>
                agent.PositionRole == AgentSoccer.Position.DefenderKeeper);
            if (keeper != null && keeper != m_FocusAgent)
            {
                MoveAgent(keeper, new Vector3(
                    Mathf.Min(-4f, focusX - 28f),
                    keeper.transform.position.y,
                    0f));
            }
        }

        public static float GetL1SpawnDifficulty(SoccerL1Phase phase, float requested)
        {
            // Only the preparatory finishing drill may narrow the ball's lateral spawn.
            // Invalid values and final carry evaluation must retain the full task.
            if (phase != SoccerL1Phase.ShortRangeFinish || float.IsNaN(requested) || float.IsInfinity(requested))
                return 1f;
            return Mathf.Clamp01(requested);
        }

        void MoveAgent(AgentSoccer agent, Vector3 position)
        {
            if (agent == null)
            {
                return;
            }

            agent.transform.position = position;
            var item = m_Environment.AgentsList.FirstOrDefault(candidate => candidate?.Agent == agent);
            if (item?.Rb != null)
            {
                item.Rb.linearVelocity = Vector3.zero;
                item.Rb.angularVelocity = Vector3.zero;
                item.Rb.Sleep();
            }
        }

        void CompleteLesson(AgentSoccer actor)
        {
            if (m_RoundSucceeded)
            {
                return;
            }

            m_RoundSucceeded = true;
            m_RewardEngine.AwardCurriculumLessonSuccess(
                Team.Red,
                actor,
                groupSuccessReward,
                individualSuccessReward);
            m_Environment.CompleteTrainingDrill();
        }

        void RecordPreviousRound()
        {
            if (!m_HasConfiguredRound || !Academy.IsInitialized)
            {
                return;
            }

            var lessonName = GetLessonName();
            var recorder = Academy.Instance.StatsRecorder;
            recorder.Add(
                $"Soccer/Curriculum/{lessonName}/Success",
                m_RoundSucceeded ? 1f : 0f,
                StatAggregationMethod.Average);
            recorder.Add(
                $"Soccer/Curriculum/{lessonName}/Episode Seconds",
                Mathf.Max(0f, Time.time - m_RoundStartTime),
                StatAggregationMethod.Average);
            recorder.Add(
                $"Soccer/Curriculum/{lessonName}/Possession",
                m_PossessionEstablished ? 1f : 0f,
                StatAggregationMethod.Average);
            if (lesson == SoccerCurriculumLesson.L0BallApproach)
            {
                recorder.Add(
                    "Soccer/Curriculum/L0/Approach Progress",
                    m_ApproachRewardTotal / ApproachRewardPerMeter,
                    StatAggregationMethod.Average);
                return;
            }

            if (lesson is SoccerCurriculumLesson.L2ShortPass or SoccerCurriculumLesson.L3ProgressivePlay)
            {
                RecordL2Round();
                if (lesson == SoccerCurriculumLesson.L3ProgressivePlay)
                    RecordL3Round();
                return;
            }

            if (lesson is SoccerCurriculumLesson.L2Find or SoccerCurriculumLesson.L2Score)
            {
                RecordL2FindScoreRound();
                return;
            }

            recorder.Add(
                "Soccer/Curriculum/L1/Dribble Progress",
                m_BestDribbleProgress,
                StatAggregationMethod.Average);
            recorder.Add(
                "Soccer/Curriculum/L1/Phase",
                (float)m_RoundL1Phase,
                StatAggregationMethod.Average);
            recorder.Add("Soccer/Curriculum/L1/Spawn Difficulty", m_RoundSpawnDifficulty,
                StatAggregationMethod.Average);
            recorder.Add(
                "Soccer/Curriculum/L1/Shot Attempt",
                m_ShotAttempted ? 1f : 0f,
                StatAggregationMethod.Average);
            recorder.Add(
                "Soccer/Curriculum/L1/Shot On Target",
                m_ShotOnTarget ? 1f : 0f,
                StatAggregationMethod.Average);
            recorder.Add(
                "Soccer/Curriculum/L1/Valid Shot",
                m_ValidShotTaken ? 1f : 0f,
                StatAggregationMethod.Average);
            recorder.Add(
                "Soccer/Curriculum/L1/Carry Requirement Met",
                m_BestDribbleProgress + 0.001f >= GetRequiredCarryProgress(m_RoundL1Phase) ? 1f : 0f,
                StatAggregationMethod.Average);
            recorder.Add(
                "Soccer/Curriculum/L1/Goal",
                m_GoalScored ? 1f : 0f,
                StatAggregationMethod.Average);
            recorder.Add("Soccer/Curriculum/L1/Shot Retouched",
                m_ShotWasRetouched ? 1f : 0f, StatAggregationMethod.Average);
            recorder.Add("Soccer/Curriculum/L1/Alignment Reward", m_AlignmentRewardTotal,
                StatAggregationMethod.Average);
            recorder.Add(
                $"Soccer/Curriculum/L1/{GetL1PhaseName(m_RoundL1Phase)}/Success",
                m_RoundSucceeded ? 1f : 0f,
                StatAggregationMethod.Average);
            recorder.Add(
                "Soccer/Curriculum/L1/Episode Support Shape",
                m_SupportShapeSamples > 0 ? m_SupportShapeTotal / m_SupportShapeSamples : 0f,
                StatAggregationMethod.Average);
        }

        public static bool IsValidSupportShape(
            Vector3 supporterPosition,
            bool isKeeper,
            Vector3 carrierPosition,
            IReadOnlyList<Vector3> otherSupportPositions)
        {
            var distance = PlanarDistance(supporterPosition, carrierPosition);
            var maximumDistance = isKeeper
                ? KeeperSupportMaximumDistance
                : FieldSupportMaximumDistance;
            if (distance < SupportMinimumDistance || distance > maximumDistance)
            {
                return false;
            }

            var attackOffset = supporterPosition.x - carrierPosition.x;
            if (isKeeper)
            {
                if (attackOffset > -4f
                    || SoccerDefenderKeeperRules.GetAttackingDepth(Team.Red, supporterPosition.x)
                        > SoccerDefenderKeeperRules.HardHalfLineDepth)
                {
                    return false;
                }
            }
            else if (attackOffset < -24f || attackOffset > 16f)
            {
                return false;
            }

            return otherSupportPositions == null || otherSupportPositions.All(other =>
                PlanarDistance(supporterPosition, other) >= SupportMinimumSeparation);
        }

        public static bool IsShotOnTarget(
            Vector3 ballPosition,
            Vector3 kickDirection,
            Team shootingTeam,
            SoccerArenaGeometry arena)
        {
            var halfLength = arena != null ? arena.HalfLength : SoccerArenaGeometry.StadiumHalfLength;
            var goalHalfWidth = arena != null ? arena.GoalHalfWidth : SoccerArenaGeometry.StadiumGoalHalfWidth;
            var attackSign = shootingTeam == Team.Red ? 1f : -1f;
            if (kickDirection.x * attackSign <= 0.05f)
            {
                return false;
            }

            var goalX = halfLength * attackSign;
            var travel = (goalX - ballPosition.x) / kickDirection.x;
            if (travel <= 0f)
            {
                return false;
            }

            var projectedZ = ballPosition.z + kickDirection.z * travel;
            return Mathf.Abs(projectedZ) <= Mathf.Max(0f, goalHalfWidth - 0.5f);
        }

        public static bool CrossedNoShotFinishLine(
            Vector3 ballPosition,
            Team attackingTeam,
            SoccerArenaGeometry arena)
        {
            var halfLength = arena != null ? arena.HalfLength : SoccerArenaGeometry.StadiumHalfLength;
            var attackSign = attackingTeam == Team.Red ? 1f : -1f;
            return ballPosition.x * attackSign >= halfLength - NoShotFinishLineDistance;
        }

        static float GetRequiredCarryProgress(SoccerL1Phase phase)
        {
            return phase == SoccerL1Phase.ShortRangeFinish
                ? L1ShortFinishRequiredCarryProgress
                : L1FinalRequiredCarryProgress;
        }

        public static bool IsL1KickUnlocked(SoccerL1Phase phase, float carryProgress)
        {
            if (phase == SoccerL1Phase.ShortRangeFinish)
            {
                return true;
            }

            return !float.IsNaN(carryProgress)
                && !float.IsInfinity(carryProgress)
                && carryProgress + 0.001f >= L1FinalRequiredCarryProgress;
        }

        static string GetL1PhaseName(SoccerL1Phase phase)
        {
            return phase == SoccerL1Phase.ShortRangeFinish ? "ShortFinish" : "CarryAndScore";
        }

        string GetLessonName()
        {
            return lesson == SoccerCurriculumLesson.L0BallApproach ? "L0"
                : lesson == SoccerCurriculumLesson.L2ShortPass ? "L2"
                : lesson == SoccerCurriculumLesson.L2Find ? "L2Find"
                : lesson == SoccerCurriculumLesson.L2Score ? "L2Score"
                : lesson == SoccerCurriculumLesson.L3ProgressivePlay ? "L3" : "L1";
        }

        static float PlanarDistance(Vector3 first, Vector3 second)
        {
            var delta = first - second;
            delta.y = 0f;
            return delta.magnitude;
        }
    }
}

using System.Linq;
using Unity.MLAgents;
using UnityEngine;

namespace MachineLearning.Soccer.Curriculum
{
    public sealed partial class SoccerCurriculumController
    {
        public const float L2FindScoreWallClearance = 1f;
        public const float L2FindScoreRedGoalClearance = 5f;
        public const float L2FindProgressRewardPerMeter = 0.01f;
        public const float L2FindProgressRewardLimit = 0.5f;
        public const float L2FindTimeoutPenalty = 0.5f;
        public const int L2FindPursuerCount = 2;
        public const float L2FindPursuerIndividualRewardShare = 0.5f;
        public const float L2FindMissPenalty = 0.2f;
        public const float L2FindMissTriggerDistance = 4f;
        public const float L2FindMissRegressionDistance = 2f;
        public const float L2FindEpisodeSeconds = 15f;
        public const float L2FindHeadingRewardPerDegree = 0.001f;
        public const float L2FindHeadingRewardLimit = 0.1f;
        public const float L2FindFastTouchRewardLimit = 0.4f;
        public const float L2FindReferenceMaximumSpeed = SoccerSettings.DefaultMaximumPlanarSpeed;
        public const float L2FindNearSpawnMinimumDistance = 3f;
        public const float L2FindNearSpawnMaximumDistance = 12f;
        public const float L2ScoreProgressRewardPerMeter = 0.005f;
        public const float L2ScoreProgressRewardLimit = 0.2f;
        public const float L2ScorePossessionReward = 0.1f;
        public const float L2ScoreShotAttemptReward = 0.02f;
        public const float L2ScoreShotOnTargetReward = 0.2f;
        public const float L2ScoreGoalReward = 1f;
        public const float L2ScoreOwnGoalPenalty = 1f;
        public const float L2ScoreLongPassGoalBonus = 0.2f;
        public const float L2ScoreLongPassMinimumTargetDistance = 10f;
        public const float L2ScoreLongPassMaximumTargetDistance = 24f;
        public const float L2ScoreLongPassMinimumBallTravel = 8f;
        public const float L2ScoreLongPassMinimumGoalAdvantage = 8f;
        public const float L2ScoreLongPassMinimumGoalDistance = 30f;
        public const float L2ScoreLongPassControlSeconds = 0.35f;
        public const float L2ScoreLongPassMaximumFlightSeconds = 8f;
        const float L2ScoreLongPassLaneRadius = 2.5f;

        float m_L2FindInitialMinimumDistance;
        float m_L2FindBestMinimumDistance;
        float m_L2FindPreviousMinimumDistance;
        float m_L2FindProgressRewardTotal;
        float m_L2FindFocusInitialDistance;
        float m_L2FindFocusProgressRewardTotal;
        float m_L2FindFocusInitialHeadingError;
        float m_L2FindHeadingRewardTotal;
        float m_L2FindSecondaryFocusInitialDistance;
        float m_L2FindSecondaryFocusProgressRewardTotal;
        float m_L2FindSecondaryFocusInitialHeadingError;
        float m_L2FindSecondaryHeadingRewardTotal;
        float m_L2FindSecondaryFocusBestDistance;
        float m_L2FindFastTouchReward;
        float m_L2FindFocusPeakPlanarSpeed;
        float m_L2FindFocusPeakClosingSpeed;
        float m_L2FindFocusBestDistance;
        float m_L2FindFocusMinimumHeadingError;
        float m_L2FindFocusHeadingErrorAtBestDistance;
        float m_L2FindFocusPlanarSpeedAtBestDistance;
        float m_L2FindFocusClosingSpeedAtBestDistance;
        Vector3 m_L2FindFocusStartPosition;
        int m_L2FindFocusActionSamples;
        int m_L2FindFocusIdleTranslationSamples;
        int m_L2FindFocusForwardSamples;
        int m_L2FindFocusBackwardSamples;
        int m_L2FindFocusLateralSamples;
        int m_L2FindFocusTurnSamples;
        Vector3 m_L2FindInitialBallPosition;
        AgentSoccer.Position m_L2FindInitialNearestRole;
        AgentSoccer m_L2FindFocusAgent;
        AgentSoccer.Position m_L2FindInitialSecondNearestRole;
        AgentSoccer m_L2FindSecondaryFocusAgent;
        float m_L2ScoreInitialGoalDistance;
        float m_L2ScoreBestGoalDistance;
        float m_L2ScoreProgressRewardTotal;
        float m_L2FindFirstTouchSeconds;
        float m_L2ScoreFirstPossessionSeconds;
        bool m_L2FindTouched;
        bool m_L2FindNearSpawnSelected;
        bool m_L2ScoreGoal;
        bool m_L2ScoreOwnGoal;
        bool m_L2ScoreShotAttempted;
        bool m_L2ScoreShotOnTarget;
        bool m_L2ScoreValidLongPass;
        bool m_L2ScoreLongPassBonusAwarded;
        bool m_L2ScoreEasyShotPassRejected;
        AgentSoccer m_L2ScoreLastShooter;
        AgentSoccer m_L2ScoreLastTouch;
        AgentSoccer m_L2ScorePasser;
        AgentSoccer m_L2ScorePassTarget;
        Vector3 m_L2ScorePassBallPosition;
        float m_L2ScorePassTime;
        float m_L2ScoreReceiverControlSince;
        bool m_L2ScorePassPending;

        void ResetL2FindScoreRound()
        {
            m_L2FindInitialMinimumDistance = 0f;
            m_L2FindBestMinimumDistance = 0f;
            m_L2FindPreviousMinimumDistance = 0f;
            m_L2FindProgressRewardTotal = 0f;
            m_L2FindFocusInitialDistance = 0f;
            m_L2FindFocusProgressRewardTotal = 0f;
            m_L2FindFocusInitialHeadingError = 0f;
            m_L2FindHeadingRewardTotal = 0f;
            m_L2FindSecondaryFocusInitialDistance = 0f;
            m_L2FindSecondaryFocusProgressRewardTotal = 0f;
            m_L2FindSecondaryFocusInitialHeadingError = 0f;
            m_L2FindSecondaryHeadingRewardTotal = 0f;
            m_L2FindSecondaryFocusBestDistance = 0f;
            m_L2FindFastTouchReward = 0f;
            m_L2FindFocusPeakPlanarSpeed = 0f;
            m_L2FindFocusPeakClosingSpeed = 0f;
            m_L2FindFocusBestDistance = 0f;
            m_L2FindFocusMinimumHeadingError = 0f;
            m_L2FindFocusHeadingErrorAtBestDistance = 0f;
            m_L2FindFocusPlanarSpeedAtBestDistance = 0f;
            m_L2FindFocusClosingSpeedAtBestDistance = 0f;
            m_L2FindFocusStartPosition = Vector3.zero;
            m_L2FindFocusActionSamples = 0;
            m_L2FindFocusIdleTranslationSamples = 0;
            m_L2FindFocusForwardSamples = 0;
            m_L2FindFocusBackwardSamples = 0;
            m_L2FindFocusLateralSamples = 0;
            m_L2FindFocusTurnSamples = 0;
            m_L2FindInitialBallPosition = Vector3.zero;
            m_L2FindInitialNearestRole = AgentSoccer.Position.Striker;
            m_L2FindFocusAgent = null;
            m_L2FindInitialSecondNearestRole = AgentSoccer.Position.Striker;
            m_L2FindSecondaryFocusAgent = null;
            m_L2ScoreInitialGoalDistance = 0f;
            m_L2ScoreBestGoalDistance = 0f;
            m_L2ScoreProgressRewardTotal = 0f;
            m_L2FindFirstTouchSeconds = 0f;
            m_L2ScoreFirstPossessionSeconds = 0f;
            m_L2FindTouched = false;
            m_L2FindNearSpawnSelected = false;
            m_L2ScoreGoal = false;
            m_L2ScoreOwnGoal = false;
            m_L2ScoreShotAttempted = false;
            m_L2ScoreShotOnTarget = false;
            m_L2ScoreValidLongPass = false;
            m_L2ScoreLongPassBonusAwarded = false;
            m_L2ScoreEasyShotPassRejected = false;
            m_L2ScoreLastShooter = null;
            m_L2ScoreLastTouch = null;
            m_L2ScorePasser = null;
            m_L2ScorePassTarget = null;
            m_L2ScorePassBallPosition = Vector3.zero;
            m_L2ScorePassTime = 0f;
            m_L2ScoreReceiverControlSince = float.NaN;
            m_L2ScorePassPending = false;
        }

        void InitializeL2FindScoreDistances(Vector3 ballPosition)
        {
            if (lesson is not SoccerCurriculumLesson.L2Find and not SoccerCurriculumLesson.L2Score)
                return;
            m_L2FindInitialMinimumDistance = MinimumRedDistanceTo(ballPosition);
            m_L2FindBestMinimumDistance = m_L2FindInitialMinimumDistance;
            m_L2FindPreviousMinimumDistance = m_L2FindInitialMinimumDistance;
            m_L2FindInitialBallPosition = ballPosition;
            var pursuers = SelectL2FindPursuers(m_RedAgents, ballPosition);
            var nearest = pursuers.FirstOrDefault();
            if (nearest != null)
            {
                m_L2FindInitialNearestRole = nearest.PositionRole;
                m_L2FindFocusAgent = nearest;
                m_L2FindFocusInitialDistance = PlanarDistance(nearest.transform.position, ballPosition);
                m_L2FindFocusInitialHeadingError = CalculateL2FindHeadingError(
                    nearest.transform.forward, ballPosition - nearest.transform.position);
                m_L2FindFocusBestDistance = m_L2FindFocusInitialDistance;
                m_L2FindFocusMinimumHeadingError = m_L2FindFocusInitialHeadingError;
                m_L2FindFocusHeadingErrorAtBestDistance = m_L2FindFocusInitialHeadingError;
                m_L2FindFocusStartPosition = nearest.transform.position;
            }
            if (pursuers.Length > 1)
            {
                var secondary = pursuers[1];
                m_L2FindInitialSecondNearestRole = secondary.PositionRole;
                m_L2FindSecondaryFocusAgent = secondary;
                m_L2FindSecondaryFocusInitialDistance = PlanarDistance(
                    secondary.transform.position, ballPosition);
                m_L2FindSecondaryFocusInitialHeadingError = CalculateL2FindHeadingError(
                    secondary.transform.forward, ballPosition - secondary.transform.position);
                m_L2FindSecondaryFocusBestDistance = m_L2FindSecondaryFocusInitialDistance;
            }
            m_L2ScoreInitialGoalDistance = DistanceToNavyGoal(ballPosition);
            m_L2ScoreBestGoalDistance = m_L2ScoreInitialGoalDistance;
        }

        public static AgentSoccer[] SelectL2FindPursuers(AgentSoccer[] agents, Vector3 ballPosition)
        {
            if (agents == null || !IsFinite(ballPosition.x) || !IsFinite(ballPosition.z))
                return System.Array.Empty<AgentSoccer>();
            return agents.Where(agent => agent != null && agent.isActiveAndEnabled)
                .OrderBy(agent => PlanarDistance(agent.transform.position, ballPosition))
                .Take(L2FindPursuerCount)
                .ToArray();
        }

        Vector3 SampleL2FindScoreBallPosition(SoccerArenaGeometry arena, float y)
        {
            if (lesson == SoccerCurriculumLesson.L2Find
                && m_L2FindNearSpawnProbability > 0f
                && Random.value < m_L2FindNearSpawnProbability
                && TrySampleL2FindNearBallPosition(arena, y, out var nearPosition))
            {
                m_L2FindNearSpawnSelected = true;
                return nearPosition;
            }

            var halfLength = arena != null ? arena.HalfLength : SoccerArenaGeometry.StadiumHalfLength;
            var halfWidth = arena != null ? arena.HalfWidth : SoccerArenaGeometry.StadiumHalfWidth;
            for (var attempt = 0; attempt < 256; attempt++)
            {
                var position = new Vector3(
                    Random.Range(-halfLength + L2FindScoreWallClearance,
                        halfLength - L2FindScoreWallClearance),
                    y,
                    Random.Range(-halfWidth + L2FindScoreWallClearance,
                        halfWidth - L2FindScoreWallClearance));
                if (IsValidL2FindScoreBallSpawn(position, arena)
                    && IsWithinL2FindHeadingCurriculum(position))
                    return position;
            }

            return new Vector3(0f, y, 0f);
        }

        bool TrySampleL2FindNearBallPosition(SoccerArenaGeometry arena, float y, out Vector3 position)
        {
            position = default;
            if (m_RedAgents.Length == 0) return false;
            for (var attempt = 0; attempt < 128; attempt++)
            {
                var anchor = m_RedAgents[Random.Range(0, m_RedAgents.Length)];
                if (anchor == null || !anchor.isActiveAndEnabled) continue;
                var angle = Random.Range(0f, Mathf.PI * 2f);
                var distance = Random.Range(L2FindNearSpawnMinimumDistance, L2FindNearSpawnMaximumDistance);
                var candidate = anchor.transform.position
                    + new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);
                candidate.y = y;
                var nearestDistance = MinimumRedDistanceTo(candidate);
                if (nearestDistance + 0.001f < L2FindNearSpawnMinimumDistance
                    || nearestDistance > L2FindNearSpawnMaximumDistance
                    || !IsValidL2FindScoreBallSpawn(candidate, arena)
                    || !IsWithinL2FindHeadingCurriculum(candidate))
                    continue;
                position = candidate;
                return true;
            }
            return false;
        }

        bool IsWithinL2FindHeadingCurriculum(Vector3 ballPosition)
        {
            if (lesson != SoccerCurriculumLesson.L2Find
                || m_L2FindMaximumInitialHeadingError >= 180f - 0.001f)
                return true;
            var nearest = SelectL2FindPursuers(m_RedAgents, ballPosition).FirstOrDefault();
            return nearest != null && IsL2FindHeadingWithinLimit(
                nearest.transform.forward,
                ballPosition - nearest.transform.position,
                m_L2FindMaximumInitialHeadingError);
        }

        public static bool IsL2FindHeadingWithinLimit(
            Vector3 forward,
            Vector3 toBall,
            float maximumHeadingError)
        {
            if (!IsFinite(maximumHeadingError)) return false;
            return CalculateL2FindHeadingError(forward, toBall)
                <= Mathf.Clamp(maximumHeadingError, 0f, 180f) + 0.001f;
        }

        public static bool IsValidL2FindScoreBallSpawn(Vector3 position, SoccerArenaGeometry arena)
        {
            if (!IsFinite(position.x) || !IsFinite(position.y) || !IsFinite(position.z)) return false;
            var halfLength = arena != null ? arena.HalfLength : SoccerArenaGeometry.StadiumHalfLength;
            var halfWidth = arena != null ? arena.HalfWidth : SoccerArenaGeometry.StadiumHalfWidth;
            if (Mathf.Abs(position.x) > halfLength - L2FindScoreWallClearance
                || Mathf.Abs(position.z) > halfWidth - L2FindScoreWallClearance)
                return false;
            if (arena != null && !arena.IsInsideRoundedCorners(position, L2FindScoreWallClearance))
                return false;
            var redGoal = arena != null
                ? arena.GetGoalCenter(Team.Red, position.y)
                : new Vector3(-halfLength, position.y, 0f);
            return PlanarDistance(position, redGoal) + 0.001f >= L2FindScoreRedGoalClearance;
        }

        void UpdateL2FindLesson()
        {
            var minimumDistance = MinimumRedDistanceTo(m_Environment.Ball.transform.position);
            m_L2FindBestMinimumDistance = Mathf.Min(m_L2FindBestMinimumDistance, minimumDistance);
            // Dense potential shaping: approaching the stationary ball pays and moving
            // away removes the same credit. The bounded total prevents oscillation farming.
            var desiredTotal = CalculateL2FindPotentialReward(
                m_L2FindInitialMinimumDistance, minimumDistance);
            var requested = desiredTotal - m_L2FindProgressRewardTotal;
            if (Mathf.Abs(requested) > 0.000001f)
                m_L2FindProgressRewardTotal += m_RewardEngine.AwardCurriculumGroupReward(
                    Team.Red, SoccerRewardKind.CurriculumFindProgress, requested);
            m_L2FindPreviousMinimumDistance = minimumDistance;

            // Split the former single-actor individual reward budget equally between
            // the two initially nearest actors. This preserves a learned backup
            // pursuer without doubling the individual shaping budget. No action,
            // heading, velocity, or position is changed by this reward signal.
            if (m_L2FindFocusAgent != null && m_L2FindFocusAgent.isActiveAndEnabled)
            {
                var focusDistance = PlanarDistance(
                    m_L2FindFocusAgent.transform.position, m_Environment.Ball.transform.position);
                var focusDesiredTotal = CalculateL2FindPotentialReward(
                    m_L2FindFocusInitialDistance, focusDistance)
                    * L2FindPursuerIndividualRewardShare;
                var focusRequested = focusDesiredTotal - m_L2FindFocusProgressRewardTotal;
                if (Mathf.Abs(focusRequested) > 0.000001f)
                    m_L2FindFocusProgressRewardTotal += m_RewardEngine.AwardCurriculumSignedIndividualReward(
                        m_L2FindFocusAgent, SoccerRewardKind.CurriculumFindProgress, focusRequested);

                var toBall = m_Environment.Ball.transform.position
                    - m_L2FindFocusAgent.transform.position;
                var headingError = CalculateL2FindHeadingError(
                    m_L2FindFocusAgent.transform.forward, toBall);
                m_L2FindFocusMinimumHeadingError = Mathf.Min(
                    m_L2FindFocusMinimumHeadingError, headingError);
                var headingDesiredTotal = CalculateL2FindHeadingPotentialReward(
                    m_L2FindFocusInitialHeadingError, headingError)
                    * L2FindPursuerIndividualRewardShare;
                var headingRequested = headingDesiredTotal - m_L2FindHeadingRewardTotal;
                if (Mathf.Abs(headingRequested) > 0.000001f)
                    m_L2FindHeadingRewardTotal += m_RewardEngine.AwardCurriculumSignedIndividualReward(
                        m_L2FindFocusAgent, SoccerRewardKind.CurriculumFindHeading, headingRequested);

                var planarSpeed = 0f;
                var closingSpeed = 0f;
                if (m_L2FindFocusAgent.agentRb != null)
                {
                    var velocity = m_L2FindFocusAgent.agentRb.linearVelocity;
                    velocity.y = 0f;
                    planarSpeed = velocity.magnitude;
                    m_L2FindFocusPeakPlanarSpeed = Mathf.Max(
                        m_L2FindFocusPeakPlanarSpeed, planarSpeed);
                    toBall.y = 0f;
                    if (toBall.sqrMagnitude > 0.0001f)
                    {
                        closingSpeed = Vector3.Dot(velocity, toBall.normalized);
                        m_L2FindFocusPeakClosingSpeed = Mathf.Max(
                            m_L2FindFocusPeakClosingSpeed,
                            closingSpeed);
                    }
                }

                if (focusDistance < m_L2FindFocusBestDistance)
                {
                    m_L2FindFocusBestDistance = focusDistance;
                    m_L2FindFocusHeadingErrorAtBestDistance = headingError;
                    m_L2FindFocusPlanarSpeedAtBestDistance = planarSpeed;
                    m_L2FindFocusClosingSpeedAtBestDistance = closingSpeed;
                }

                m_L2FindFocusActionSamples++;
                var forwardAction = m_L2FindFocusAgent.LastForwardAction;
                var lateralAction = m_L2FindFocusAgent.LastLateralAction;
                if (forwardAction == 0 && lateralAction == 0)
                    m_L2FindFocusIdleTranslationSamples++;
                if (forwardAction == 1) m_L2FindFocusForwardSamples++;
                if (forwardAction == 2) m_L2FindFocusBackwardSamples++;
                if (lateralAction != 0) m_L2FindFocusLateralSamples++;
                if (m_L2FindFocusAgent.LastRotationAction != 0) m_L2FindFocusTurnSamples++;
            }

            if (m_L2FindSecondaryFocusAgent != null && m_L2FindSecondaryFocusAgent.isActiveAndEnabled)
            {
                var secondaryDistance = PlanarDistance(
                    m_L2FindSecondaryFocusAgent.transform.position, m_Environment.Ball.transform.position);
                m_L2FindSecondaryFocusBestDistance = Mathf.Min(
                    m_L2FindSecondaryFocusBestDistance, secondaryDistance);
                var secondaryDesiredTotal = CalculateL2FindPotentialReward(
                    m_L2FindSecondaryFocusInitialDistance, secondaryDistance)
                    * L2FindPursuerIndividualRewardShare;
                var secondaryRequested = secondaryDesiredTotal - m_L2FindSecondaryFocusProgressRewardTotal;
                if (Mathf.Abs(secondaryRequested) > 0.000001f)
                    m_L2FindSecondaryFocusProgressRewardTotal +=
                        m_RewardEngine.AwardCurriculumSignedIndividualReward(
                            m_L2FindSecondaryFocusAgent,
                            SoccerRewardKind.CurriculumFindProgress,
                            secondaryRequested);

                var secondaryHeadingError = CalculateL2FindHeadingError(
                    m_L2FindSecondaryFocusAgent.transform.forward,
                    m_Environment.Ball.transform.position - m_L2FindSecondaryFocusAgent.transform.position);
                var secondaryHeadingDesiredTotal = CalculateL2FindHeadingPotentialReward(
                    m_L2FindSecondaryFocusInitialHeadingError, secondaryHeadingError)
                    * L2FindPursuerIndividualRewardShare;
                var secondaryHeadingRequested = secondaryHeadingDesiredTotal - m_L2FindSecondaryHeadingRewardTotal;
                if (Mathf.Abs(secondaryHeadingRequested) > 0.000001f)
                    m_L2FindSecondaryHeadingRewardTotal +=
                        m_RewardEngine.AwardCurriculumSignedIndividualReward(
                            m_L2FindSecondaryFocusAgent,
                            SoccerRewardKind.CurriculumFindHeading,
                            secondaryHeadingRequested);
            }
        }

        public static float CalculateL2FindPotentialReward(float initialDistance, float currentDistance)
        {
            if (!IsFinite(initialDistance) || !IsFinite(currentDistance)) return 0f;
            return Mathf.Clamp(
                (Mathf.Max(0f, initialDistance) - Mathf.Max(0f, currentDistance))
                    * L2FindProgressRewardPerMeter,
                -L2FindProgressRewardLimit,
                L2FindProgressRewardLimit);
        }

        public static bool HasL2FindNearMiss(float bestDistance, float currentDistance)
        {
            return IsFinite(bestDistance) && IsFinite(currentDistance)
                && bestDistance <= L2FindMissTriggerDistance
                && currentDistance >= bestDistance + L2FindMissRegressionDistance;
        }

        public static float CalculateL2FindHeadingError(Vector3 forward, Vector3 toBall)
        {
            forward.y = 0f;
            toBall.y = 0f;
            if (forward.sqrMagnitude < 0.0001f || toBall.sqrMagnitude < 0.0001f) return 0f;
            return Vector3.Angle(forward, toBall);
        }

        public static string GetL2FindHeadingBand(float headingError)
        {
            if (!IsFinite(headingError)) return "Invalid";
            if (headingError <= 60f) return "Front";
            return headingError <= 135f ? "SideBlind" : "Rear";
        }

        public static float CalculateL2FindHeadingPotentialReward(
            float initialHeadingError,
            float currentHeadingError)
        {
            if (!IsFinite(initialHeadingError) || !IsFinite(currentHeadingError)) return 0f;
            return Mathf.Clamp(
                (Mathf.Clamp(initialHeadingError, 0f, 180f)
                    - Mathf.Clamp(currentHeadingError, 0f, 180f))
                    * L2FindHeadingRewardPerDegree,
                -L2FindHeadingRewardLimit,
                L2FindHeadingRewardLimit);
        }

        public static float CalculateL2FindFastTouchReward(float initialDistance, float elapsedSeconds)
        {
            if (!IsFinite(initialDistance) || !IsFinite(elapsedSeconds)
                || initialDistance < 0f || elapsedSeconds < 0f
                || elapsedSeconds >= L2FindEpisodeSeconds)
                return 0f;
            var theoreticalTravelSeconds = Mathf.Min(
                initialDistance / L2FindReferenceMaximumSpeed,
                L2FindEpisodeSeconds);
            var availableSlack = Mathf.Max(
                0.001f,
                L2FindEpisodeSeconds - theoreticalTravelSeconds);
            var efficiency = Mathf.Clamp01(
                (L2FindEpisodeSeconds - elapsedSeconds) / availableSlack);
            return efficiency * L2FindFastTouchRewardLimit;
        }

        public static string GetL2FindDistanceBand(float distance)
        {
            if (!IsFinite(distance) || distance < 0f) return "Invalid";
            return distance <= 20f ? "Near" : distance <= 40f ? "Mid" : "Far";
        }

        public static string GetL2FindLongitudinalBand(float ballX)
        {
            if (!IsFinite(ballX)) return "Invalid";
            return ballX < -20f ? "RedThird" : ballX > 20f ? "NavyThird" : "MiddleThird";
        }

        public static string GetL2FindLateralBand(float ballZ)
        {
            if (!IsFinite(ballZ)) return "Invalid";
            return Mathf.Abs(ballZ) > 20f ? "Wide" : "Central";
        }

        void UpdateL2ScoreLesson()
        {
            var carrier = GetControlledRedCarrier();
            if (carrier != null && !m_PossessionEstablished)
            {
                m_PossessionEstablished = true;
                m_L2ScoreFirstPossessionSeconds = Time.time - m_RoundStartTime;
                m_RewardEngine.AwardCurriculumIndividualReward(
                    carrier, SoccerRewardKind.CurriculumPossessionEstablished, L2ScorePossessionReward);
            }

            var goalDistance = DistanceToNavyGoal(m_Environment.Ball.transform.position);
            var improvement = m_L2ScoreBestGoalDistance - goalDistance;
            if (improvement >= MinimumRewardedProgress)
            {
                m_L2ScoreBestGoalDistance = goalDistance;
                var requested = Mathf.Min(
                    improvement * L2ScoreProgressRewardPerMeter,
                    L2ScoreProgressRewardLimit - m_L2ScoreProgressRewardTotal);
                if (requested > 0f)
                    m_L2ScoreProgressRewardTotal += m_RewardEngine.AwardCurriculumGroupReward(
                        Team.Red, SoccerRewardKind.CurriculumScoreProgress, requested);
            }

            UpdateL2ScoreLongPass(carrier);
        }

        void HandleFindScoreTouch(AgentSoccer actor)
        {
            if (!m_HasConfiguredRound || m_RoundSucceeded || actor == null || actor.Team != Team.Red) return;
            if (lesson == SoccerCurriculumLesson.L2Find)
            {
                m_L2FindTouched = true;
                m_PossessionEstablished = true;
                m_L2FindFirstTouchSeconds = Time.time - m_RoundStartTime;
                m_RewardEngine.AwardCurriculumIndividualReward(
                    actor, SoccerRewardKind.CurriculumPossessionEstablished, L2ScorePossessionReward);
                m_L2FindFastTouchReward = CalculateL2FindFastTouchReward(
                    m_L2FindInitialMinimumDistance, m_L2FindFirstTouchSeconds);
                if (m_L2FindFastTouchReward > 0f)
                    m_RewardEngine.AwardCurriculumIndividualReward(
                        actor, SoccerRewardKind.CurriculumFastFind, m_L2FindFastTouchReward);
                CompleteLesson(actor);
                return;
            }
            if (lesson != SoccerCurriculumLesson.L2Score) return;
            m_L2ScoreLastTouch = actor;
            if (!m_L2ScorePassPending) return;
            if (actor == m_L2ScorePasser && Time.time <= m_L2ScorePassTime + 0.1f) return;
            if (actor != m_L2ScorePassTarget)
            {
                m_L2ScorePassPending = false;
                m_L2ScoreReceiverControlSince = float.NaN;
            }
        }

        void HandleFindScoreTimeout()
        {
            if (lesson != SoccerCurriculumLesson.L2Find || !m_HasConfiguredRound
                || m_RoundSucceeded || m_L2FindTouched) return;
            m_RewardEngine.AwardCurriculumGroupReward(
                Team.Red,
                SoccerRewardKind.CurriculumFindTimeoutPenalty,
                -L2FindTimeoutPenalty);
        }

        void HandleFindScoreStrike(AgentSoccer actor, int kickAction, Vector3 direction, bool safetyRedirected)
        {
            if (lesson != SoccerCurriculumLesson.L2Score || !m_HasConfiguredRound || m_RoundSucceeded
                || actor == null || actor.Team != Team.Red || kickAction == 0 || safetyRedirected) return;
            if (GetControlledRedCarrier() != actor) return;

            m_L2ScoreLastShooter = actor;
            var onTarget = IsShotOnTarget(
                m_Environment.Ball.transform.position, direction, Team.Red, m_Environment.ArenaGeometry);
            // A pass or sideways clearance is not a shot attempt. This keeps the
            // intermediate reward aligned with the terminal scoring objective.
            if (!m_L2ScoreShotAttempted && direction.x > 0.05f)
            {
                m_L2ScoreShotAttempted = true;
                m_RewardEngine.AwardCurriculumIndividualReward(
                    actor, SoccerRewardKind.CurriculumShotAttempt, L2ScoreShotAttemptReward);
            }
            if (!m_L2ScoreShotOnTarget && onTarget)
            {
                m_L2ScoreShotOnTarget = true;
                m_RewardEngine.AwardCurriculumIndividualReward(
                    actor, SoccerRewardKind.CurriculumShotOnTarget, L2ScoreShotOnTargetReward);
            }

            if (m_L2ScoreValidLongPass) return;
            m_L2ScorePassPending = false;
            m_L2ScoreReceiverControlSince = float.NaN;
            var ball = m_Environment.Ball.transform.position;
            var navyGoal = NavyGoal(ball.y);
            var target = m_RedAgents.Where(candidate => candidate != null && candidate != actor
                    && candidate.isActiveAndEnabled
                    && IsEligibleL2ScoreLongPassAtStrike(
                        actor.transform.position, candidate.transform.position, ball, direction, navyGoal))
                .OrderBy(candidate => PlanarDistance(ball, candidate.transform.position))
                .FirstOrDefault();
            if (target == null)
            {
                if (DistanceToNavyGoal(ball) < L2ScoreLongPassMinimumGoalDistance)
                    m_L2ScoreEasyShotPassRejected = true;
                return;
            }
            m_L2ScorePasser = actor;
            m_L2ScorePassTarget = target;
            m_L2ScorePassBallPosition = ball;
            m_L2ScorePassTime = Time.time;
            m_L2ScorePassPending = true;
        }

        void UpdateL2ScoreLongPass(AgentSoccer carrier)
        {
            if (!m_L2ScorePassPending) return;
            var elapsed = Time.time - m_L2ScorePassTime;
            if (elapsed > L2ScoreLongPassMaximumFlightSeconds)
            {
                m_L2ScorePassPending = false;
                return;
            }
            if (m_L2ScoreLastTouch != m_L2ScorePassTarget || carrier != m_L2ScorePassTarget)
            {
                m_L2ScoreReceiverControlSince = float.NaN;
                return;
            }
            if (PlanarDistance(m_L2ScorePassBallPosition, m_Environment.Ball.transform.position)
                + 0.001f < L2ScoreLongPassMinimumBallTravel)
                return;
            if (float.IsNaN(m_L2ScoreReceiverControlSince))
                m_L2ScoreReceiverControlSince = Time.time;
            if (Time.time - m_L2ScoreReceiverControlSince + 0.001f < L2ScoreLongPassControlSeconds) return;
            m_L2ScoreValidLongPass = true;
            m_L2ScorePassPending = false;
        }

        public static bool IsEligibleL2ScoreLongPassAtStrike(
            Vector3 passer, Vector3 receiver, Vector3 ball, Vector3 direction, Vector3 navyGoal)
        {
            if (!IsFinite(passer.x) || !IsFinite(receiver.x) || !IsFinite(ball.x)
                || !IsFinite(direction.x) || !IsFinite(navyGoal.x)) return false;
            var separation = PlanarDistance(passer, receiver);
            if (separation < L2ScoreLongPassMinimumTargetDistance
                || separation > L2ScoreLongPassMaximumTargetDistance
                || PlanarDistance(ball, navyGoal) < L2ScoreLongPassMinimumGoalDistance
                || PlanarDistance(passer, navyGoal) - PlanarDistance(receiver, navyGoal)
                    < L2ScoreLongPassMinimumGoalAdvantage)
                return false;
            direction.y = 0f;
            var offset = receiver - ball;
            offset.y = 0f;
            if (direction.sqrMagnitude < 0.0001f || offset.sqrMagnitude < 0.0001f) return false;
            var forward = Vector3.Dot(offset, direction.normalized);
            return forward >= L2ScoreLongPassMinimumTargetDistance
                && forward <= L2ScoreLongPassMaximumTargetDistance
                && (offset - direction.normalized * forward).magnitude <= L2ScoreLongPassLaneRadius;
        }

        void HandleL2ScoreGoal(bool redScored)
        {
            if (redScored)
            {
                m_L2ScoreGoal = true;
                var actor = m_L2ScoreLastShooter ?? GetControlledRedCarrier() ?? m_FocusAgent;
                m_RewardEngine.AwardCurriculumIndividualReward(
                    actor, SoccerRewardKind.CurriculumGoal, L2ScoreGoalReward);
                if (m_L2ScoreValidLongPass)
                {
                    m_L2ScoreLongPassBonusAwarded = true;
                    m_RewardEngine.AwardCurriculumGroupReward(
                        Team.Red, SoccerRewardKind.CurriculumLongPassGoalBonus, L2ScoreLongPassGoalBonus);
                }
                CompleteLesson(actor);
                return;
            }

            m_L2ScoreOwnGoal = true;
            m_RewardEngine.AwardCurriculumGroupReward(
                Team.Red, SoccerRewardKind.CurriculumOwnGoalPenalty, -L2ScoreOwnGoalPenalty);
            m_Environment.CompleteTrainingDrill();
        }

        float MinimumRedDistanceTo(Vector3 position)
        {
            return m_RedAgents.Where(agent => agent != null && agent.isActiveAndEnabled)
                .Select(agent => PlanarDistance(agent.transform.position, position))
                .DefaultIfEmpty(0f).Min();
        }

        Vector3 NavyGoal(float y)
        {
            return m_Environment.ArenaGeometry != null
                ? m_Environment.ArenaGeometry.GetGoalCenter(Team.Navy, y)
                : new Vector3(SoccerArenaGeometry.StadiumHalfLength, y, 0f);
        }

        float DistanceToNavyGoal(Vector3 position) => PlanarDistance(position, NavyGoal(position.y));

        void RecordL2FindScoreRound()
        {
            var recorder = Academy.Instance.StatsRecorder;
            var prefix = lesson == SoccerCurriculumLesson.L2Find
                ? "Soccer/Curriculum/L2Find/" : "Soccer/Curriculum/L2Score/";
            recorder.Add(prefix + "Initial Nearest Ball Distance", m_L2FindInitialMinimumDistance,
                StatAggregationMethod.Average);
            recorder.Add(prefix + "Best Nearest Ball Distance", m_L2FindBestMinimumDistance,
                StatAggregationMethod.Average);
            recorder.Add(prefix + "Find Progress Reward", m_L2FindProgressRewardTotal,
                StatAggregationMethod.Average);
            recorder.Add(prefix + "Focus Progress Reward", m_L2FindFocusProgressRewardTotal,
                StatAggregationMethod.Average);
            recorder.Add(prefix + "Secondary Focus Progress Reward", m_L2FindSecondaryFocusProgressRewardTotal,
                StatAggregationMethod.Average);
            recorder.Add(prefix + "Previous Nearest Ball Distance", m_L2FindPreviousMinimumDistance,
                StatAggregationMethod.Average);
            if (lesson == SoccerCurriculumLesson.L2Find)
            {
                var success = m_L2FindTouched ? 1f : 0f;
                recorder.Add(prefix + "Ball Touched", success, StatAggregationMethod.Average);
                recorder.Add(prefix + "First Touch Seconds", m_L2FindFirstTouchSeconds,
                    StatAggregationMethod.Average);
                recorder.Add(prefix + "Fast Touch Reward", m_L2FindFastTouchReward,
                    StatAggregationMethod.Average);
                recorder.Add(prefix + "Heading Reward", m_L2FindHeadingRewardTotal,
                    StatAggregationMethod.Average);
                recorder.Add(prefix + "Secondary Heading Reward", m_L2FindSecondaryHeadingRewardTotal,
                    StatAggregationMethod.Average);
                recorder.Add(prefix + "Pursuer Count",
                    m_L2FindSecondaryFocusAgent != null ? 2f : m_L2FindFocusAgent != null ? 1f : 0f,
                    StatAggregationMethod.Average);
                recorder.Add(prefix + "Focus Peak Planar Speed", m_L2FindFocusPeakPlanarSpeed,
                    StatAggregationMethod.Average);
                recorder.Add(prefix + "Focus Peak Closing Speed", m_L2FindFocusPeakClosingSpeed,
                    StatAggregationMethod.Average);
                recorder.Add(prefix + "Focus Peak Speed Utilization",
                    Mathf.Clamp01(m_L2FindFocusPeakPlanarSpeed / L2FindReferenceMaximumSpeed),
                    StatAggregationMethod.Average);
                recorder.Add(prefix + "Training Near Spawn Selected",
                    m_L2FindNearSpawnSelected ? 1f : 0f, StatAggregationMethod.Average);
                recorder.Add(prefix + "Training Maximum Initial Heading Error",
                    m_L2FindMaximumInitialHeadingError, StatAggregationMethod.Average);
                recorder.Add(prefix + "Diagnostic Initial Heading "
                    + GetL2FindHeadingBand(m_L2FindFocusInitialHeadingError) + " Success",
                    success, StatAggregationMethod.Average);
                recorder.Add(prefix + "Diagnostic Distance "
                    + GetL2FindDistanceBand(m_L2FindInitialMinimumDistance) + " Success",
                    success, StatAggregationMethod.Average);
                recorder.Add(prefix + "Diagnostic Longitudinal "
                    + GetL2FindLongitudinalBand(m_L2FindInitialBallPosition.x) + " Success",
                    success, StatAggregationMethod.Average);
                recorder.Add(prefix + "Diagnostic Lateral "
                    + GetL2FindLateralBand(m_L2FindInitialBallPosition.z) + " Success",
                    success, StatAggregationMethod.Average);
                recorder.Add(prefix + "Diagnostic Nearest Role "
                    + m_L2FindInitialNearestRole + " Success",
                    success, StatAggregationMethod.Average);
                if (m_L2FindSecondaryFocusAgent != null)
                    recorder.Add(prefix + "Diagnostic Second Nearest Role "
                        + m_L2FindInitialSecondNearestRole + " Success",
                        success, StatAggregationMethod.Average);
                var outcome = m_L2FindTouched ? "Success" : "Failure";
                recorder.Add(prefix + "Diagnostic " + outcome + " Initial Distance",
                    m_L2FindInitialMinimumDistance, StatAggregationMethod.Average);
                recorder.Add(prefix + "Diagnostic " + outcome + " Best Distance",
                    m_L2FindBestMinimumDistance, StatAggregationMethod.Average);
                RecordL2FindFocusDiagnostics(recorder, prefix, outcome);
                RecordL2FindSecondaryFocusDiagnostics(recorder, prefix, outcome);
                return;
            }
            recorder.Add(prefix + "Initial Goal Distance", m_L2ScoreInitialGoalDistance,
                StatAggregationMethod.Average);
            recorder.Add(prefix + "Best Goal Distance", m_L2ScoreBestGoalDistance,
                StatAggregationMethod.Average);
            recorder.Add(prefix + "Goal Progress Reward", m_L2ScoreProgressRewardTotal,
                StatAggregationMethod.Average);
            recorder.Add(prefix + "First Possession Seconds", m_L2ScoreFirstPossessionSeconds,
                StatAggregationMethod.Average);
            recorder.Add(prefix + "Shot Attempt", m_L2ScoreShotAttempted ? 1f : 0f,
                StatAggregationMethod.Average);
            recorder.Add(prefix + "Shot On Target", m_L2ScoreShotOnTarget ? 1f : 0f,
                StatAggregationMethod.Average);
            recorder.Add(prefix + "Goal", m_L2ScoreGoal ? 1f : 0f, StatAggregationMethod.Average);
            recorder.Add(prefix + "Own Goal", m_L2ScoreOwnGoal ? 1f : 0f, StatAggregationMethod.Average);
            recorder.Add(prefix + "Valid Long Pass", m_L2ScoreValidLongPass ? 1f : 0f,
                StatAggregationMethod.Average);
            recorder.Add(prefix + "Long Pass Goal Bonus", m_L2ScoreLongPassBonusAwarded ? 1f : 0f,
                StatAggregationMethod.Average);
            recorder.Add(prefix + "Easy Shot Pass Rejected", m_L2ScoreEasyShotPassRejected ? 1f : 0f,
                StatAggregationMethod.Average);
        }

        void RecordL2FindFocusDiagnostics(StatsRecorder recorder, string prefix, string outcome)
        {
            if (m_L2FindFocusAgent == null) return;
            var ballOffset = m_Environment.Ball.transform.position - m_L2FindFocusAgent.transform.position;
            var endDistance = PlanarDistance(
                m_L2FindFocusAgent.transform.position, m_Environment.Ball.transform.position);
            var endHeadingError = CalculateL2FindHeadingError(
                m_L2FindFocusAgent.transform.forward, ballOffset);
            var endPlanarSpeed = 0f;
            var endClosingSpeed = 0f;
            if (m_L2FindFocusAgent.agentRb != null)
            {
                var velocity = m_L2FindFocusAgent.agentRb.linearVelocity;
                velocity.y = 0f;
                ballOffset.y = 0f;
                endPlanarSpeed = velocity.magnitude;
                if (ballOffset.sqrMagnitude > 0.0001f)
                    endClosingSpeed = Vector3.Dot(velocity, ballOffset.normalized);
            }

            var diagnostic = prefix + "Diagnostic " + outcome + " Focus ";
            recorder.Add(diagnostic + "Initial Distance", m_L2FindFocusInitialDistance,
                StatAggregationMethod.Average);
            recorder.Add(diagnostic + "Best Distance", m_L2FindFocusBestDistance,
                StatAggregationMethod.Average);
            recorder.Add(diagnostic + "End Distance", endDistance, StatAggregationMethod.Average);
            recorder.Add(diagnostic + "Initial Heading Error", m_L2FindFocusInitialHeadingError,
                StatAggregationMethod.Average);
            recorder.Add(diagnostic + "Minimum Heading Error", m_L2FindFocusMinimumHeadingError,
                StatAggregationMethod.Average);
            recorder.Add(diagnostic + "Heading Error At Best Distance", m_L2FindFocusHeadingErrorAtBestDistance,
                StatAggregationMethod.Average);
            recorder.Add(diagnostic + "End Heading Error", endHeadingError, StatAggregationMethod.Average);
            recorder.Add(diagnostic + "Planar Speed At Best Distance", m_L2FindFocusPlanarSpeedAtBestDistance,
                StatAggregationMethod.Average);
            recorder.Add(diagnostic + "Closing Speed At Best Distance", m_L2FindFocusClosingSpeedAtBestDistance,
                StatAggregationMethod.Average);
            recorder.Add(diagnostic + "End Planar Speed", endPlanarSpeed, StatAggregationMethod.Average);
            recorder.Add(diagnostic + "End Closing Speed", endClosingSpeed, StatAggregationMethod.Average);
            recorder.Add(diagnostic + "Displacement",
                PlanarDistance(m_L2FindFocusStartPosition, m_L2FindFocusAgent.transform.position),
                StatAggregationMethod.Average);
            var sampleCount = Mathf.Max(1, m_L2FindFocusActionSamples);
            recorder.Add(diagnostic + "Idle Translation Fraction",
                (float)m_L2FindFocusIdleTranslationSamples / sampleCount, StatAggregationMethod.Average);
            recorder.Add(diagnostic + "Forward Fraction",
                (float)m_L2FindFocusForwardSamples / sampleCount, StatAggregationMethod.Average);
            recorder.Add(diagnostic + "Backward Fraction",
                (float)m_L2FindFocusBackwardSamples / sampleCount, StatAggregationMethod.Average);
            recorder.Add(diagnostic + "Lateral Fraction",
                (float)m_L2FindFocusLateralSamples / sampleCount, StatAggregationMethod.Average);
            recorder.Add(diagnostic + "Turn Fraction",
                (float)m_L2FindFocusTurnSamples / sampleCount, StatAggregationMethod.Average);
        }

        void RecordL2FindSecondaryFocusDiagnostics(StatsRecorder recorder, string prefix, string outcome)
        {
            if (m_L2FindSecondaryFocusAgent == null) return;
            var endDistance = PlanarDistance(
                m_L2FindSecondaryFocusAgent.transform.position, m_Environment.Ball.transform.position);
            var diagnostic = prefix + "Diagnostic " + outcome + " Secondary Focus ";
            recorder.Add(diagnostic + "Initial Distance", m_L2FindSecondaryFocusInitialDistance,
                StatAggregationMethod.Average);
            recorder.Add(diagnostic + "Best Distance", m_L2FindSecondaryFocusBestDistance,
                StatAggregationMethod.Average);
            recorder.Add(diagnostic + "End Distance", endDistance, StatAggregationMethod.Average);
        }
    }
}

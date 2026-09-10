using Unity.MLAgents;
using UnityEngine;

namespace MachineLearning.Soccer.Curriculum
{
    public sealed partial class SoccerCurriculumController
    {
        enum L3FailureReason { None, Kick, Touch, Carrier, Distance, Timeout }

        public const float L3RequiredForwardProgress = 3f;
        public const float L3RequiredStableControlSeconds = 0.35f;
        public const float L3MaximumReceptionRecoverySeconds = 2f;
        public const float L3MaximumContinuationSeconds = 6f;
        public const float L3ProgressRewardLimit = 0.15f;
        public const float L3StableReceiverReward = 0.1f;

        AgentSoccer m_L3Receiver;
        Vector3 m_L3StartBallPosition;
        float m_L3StartTime;
        float m_L3StableControlSeconds;
        float m_L3BestForwardProgress;
        float m_L3ProgressRewardTotal;
        float m_L3StableReceiverRewardTotal;
        bool m_L3Active;
        bool m_L3StrictPassCompleted;
        bool m_L3StableReceiverConfirmed;
        bool m_L3Interrupted;
        bool m_L3KickViolation;
        L3FailureReason m_L3FailureReason;

        void ResetL3Round()
        {
            m_L3Receiver = null;
            m_L3StartBallPosition = Vector3.zero;
            m_L3StartTime = 0f;
            m_L3StableControlSeconds = 0f;
            m_L3BestForwardProgress = 0f;
            m_L3ProgressRewardTotal = 0f;
            m_L3StableReceiverRewardTotal = 0f;
            m_L3Active = false;
            m_L3StrictPassCompleted = false;
            m_L3StableReceiverConfirmed = false;
            m_L3Interrupted = false;
            m_L3KickViolation = false;
            m_L3FailureReason = L3FailureReason.None;
        }

        void BeginL3Continuation(AgentSoccer receiver)
        {
            m_L3Receiver = receiver;
            m_L3StartBallPosition = m_Environment.Ball.transform.position;
            m_L3StartTime = Time.time;
            m_L3StableControlSeconds = 0f;
            m_L3StrictPassCompleted = true;
            m_L3Active = true;
            m_Environment.ConfigureCurriculumConstraints(false, true);
            m_Environment.ConfigureCurriculumPassSupport(null, null);
            // Reuse the recorded physical receiver-chase aid only until stable
            // retention is established. It does not move the ball or carrier.
            m_Environment.SetCurriculumReceivingAgent(receiver, true);
        }

        void UpdateL3Continuation(AgentSoccer carrier)
        {
            var elapsed = Time.time - m_L3StartTime;
            if (elapsed > L3MaximumContinuationSeconds)
            {
                FailL3Continuation(L3FailureReason.Timeout);
                return;
            }

            if (m_L2LastTouch != m_L3Receiver || (carrier != null && carrier != m_L3Receiver))
            {
                FailL3Continuation(L3FailureReason.Carrier);
                return;
            }

            var ball = m_Environment.Ball.transform.position;
            var receiverDistance = PlanarDistance(m_L3Receiver.transform.position, ball);
            if (receiverDistance > ControlledBallDistance)
            {
                m_L3StableControlSeconds = 0f;
                if (CanRecoverL3Reception(m_L3StableReceiverConfirmed, elapsed, receiverDistance))
                    return;
                FailL3Continuation(L3FailureReason.Distance);
                return;
            }

            // The shared carrier identity can be temporarily null between contact
            // confirmations. After the strict L2 handoff, same last toucher plus
            // continuous 2.4 m proximity measures physical retention. A different
            // confirmed carrier/toucher still fails, and completion again requires
            // this designated receiver to be the current confirmed carrier.
            m_L3StableControlSeconds += Time.fixedDeltaTime;
            if (!m_L3StableReceiverConfirmed
                && m_L3StableControlSeconds + 0.001f >= L3RequiredStableControlSeconds)
            {
                m_L3StableReceiverConfirmed = true;
                m_Environment.SetCurriculumReceivingAgent(m_L3Receiver, false);
                m_L3StableReceiverRewardTotal += m_RewardEngine.AwardCurriculumIndividualReward(
                    m_L3Receiver, SoccerRewardKind.CurriculumStableReceiver, L3StableReceiverReward);
            }

            if (m_L3StableReceiverConfirmed)
            {
                var forwardProgress = Mathf.Max(0f, ball.x - m_L3StartBallPosition.x);
                var previousBest = m_L3BestForwardProgress;
                var increment = CalculateL3ProgressRewardIncrement(true, previousBest, forwardProgress);
                m_L3BestForwardProgress = Mathf.Max(previousBest, forwardProgress);
                if (increment > 0f)
                {
                    m_L3ProgressRewardTotal += m_RewardEngine.AwardCurriculumIndividualReward(
                        m_L3Receiver, SoccerRewardKind.CurriculumReceiverProgress, increment);
                }
            }

            if (IsSuccessfulL3Continuation(
                carrier == m_L3Receiver,
                m_L2LastTouch == m_L3Receiver,
                false,
                m_L3StartBallPosition,
                ball,
                m_L3StableControlSeconds,
                elapsed,
                receiverDistance))
            {
                m_L3Active = false;
                m_Environment.ClearCurriculumReceivingAgents();
                CompleteLesson(m_L3Receiver);
            }
        }

        void FailL3Continuation(L3FailureReason reason)
        {
            if (!m_L3Active) return;
            m_L3Active = false;
            m_Environment.ClearCurriculumReceivingAgents();
            m_L3Interrupted = true;
            m_L3FailureReason = reason;
            m_L3KickViolation |= reason == L3FailureReason.Kick;
            m_Environment.CompleteTrainingDrill();
        }

        void FailL3Kick() => FailL3Continuation(L3FailureReason.Kick);
        void FailL3Touch() => FailL3Continuation(L3FailureReason.Touch);

        public static float CalculateL3ProgressRewardIncrement(
            bool stableReceiverConfirmed,
            float previousBestForwardProgress,
            float currentForwardProgress)
        {
            if (!stableReceiverConfirmed || !IsFinite(previousBestForwardProgress)
                || !IsFinite(currentForwardProgress)) return 0f;
            var previous = Mathf.Max(0f, previousBestForwardProgress);
            var current = Mathf.Max(previous, currentForwardProgress);
            var rewardedBefore = Mathf.Min(previous / L3RequiredForwardProgress, 1f) * L3ProgressRewardLimit;
            var rewardedNow = Mathf.Min(current / L3RequiredForwardProgress, 1f) * L3ProgressRewardLimit;
            return Mathf.Max(0f, rewardedNow - rewardedBefore);
        }

        public static bool CanRecoverL3Reception(
            bool stableReceiverConfirmed,
            float elapsedSeconds,
            float receiverBallDistance)
        {
            return !stableReceiverConfirmed
                && IsFinite(elapsedSeconds)
                && IsFinite(receiverBallDistance)
                && elapsedSeconds >= 0f
                && elapsedSeconds <= L3MaximumReceptionRecoverySeconds
                && receiverBallDistance > ControlledBallDistance;
        }

        public static bool IsSuccessfulL3Continuation(
            bool sameConfirmedReceiver,
            bool receiverLastTouch,
            bool anyKickAfterReception,
            Vector3 receptionBallPosition,
            Vector3 currentBallPosition,
            float stableControlSeconds,
            float elapsedSeconds,
            float receiverBallDistance)
        {
            if (!sameConfirmedReceiver || !receiverLastTouch || anyKickAfterReception
                || !IsFinite(stableControlSeconds) || !IsFinite(elapsedSeconds)
                || !IsFinite(receiverBallDistance) || elapsedSeconds < 0f
                || elapsedSeconds > L3MaximumContinuationSeconds
                || stableControlSeconds + 0.001f < L3RequiredStableControlSeconds
                || receiverBallDistance > ControlledBallDistance)
                return false;
            var forward = currentBallPosition.x - receptionBallPosition.x;
            return forward + 0.001f >= L3RequiredForwardProgress
                && PlanarDistance(receptionBallPosition, currentBallPosition) + 0.001f
                    >= L3RequiredForwardProgress;
        }

        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        void RecordL3Round()
        {
            var recorder = Academy.Instance.StatsRecorder;
            recorder.Add("Soccer/Curriculum/L3/Strict Pass Completed",
                m_L3StrictPassCompleted ? 1f : 0f, StatAggregationMethod.Average);
            recorder.Add("Soccer/Curriculum/L3/Stable Receiver Possession",
                m_L3StableReceiverConfirmed ? 1f : 0f, StatAggregationMethod.Average);
            recorder.Add("Soccer/Curriculum/L3/Stable Control Seconds",
                m_L3StableControlSeconds, StatAggregationMethod.Average);
            recorder.Add("Soccer/Curriculum/L3/Stable Control 0.10",
                m_L3StableControlSeconds + 0.001f >= 0.1f ? 1f : 0f, StatAggregationMethod.Average);
            recorder.Add("Soccer/Curriculum/L3/Stable Control 0.20",
                m_L3StableControlSeconds + 0.001f >= 0.2f ? 1f : 0f, StatAggregationMethod.Average);
            recorder.Add("Soccer/Curriculum/L3/Best Forward Progress",
                m_L3BestForwardProgress, StatAggregationMethod.Average);
            recorder.Add("Soccer/Curriculum/L3/Progress Reward",
                m_L3ProgressRewardTotal, StatAggregationMethod.Average);
            recorder.Add("Soccer/Curriculum/L3/Stable Receiver Reward",
                m_L3StableReceiverRewardTotal, StatAggregationMethod.Average);
            recorder.Add("Soccer/Curriculum/L3/Interrupted",
                m_L3Interrupted ? 1f : 0f, StatAggregationMethod.Average);
            recorder.Add("Soccer/Curriculum/L3/Kick Violation",
                m_L3KickViolation ? 1f : 0f, StatAggregationMethod.Average);
            foreach (L3FailureReason reason in System.Enum.GetValues(typeof(L3FailureReason)))
            {
                if (reason == L3FailureReason.None) continue;
                recorder.Add($"Soccer/Curriculum/L3/Failure {reason}",
                    m_L3FailureReason == reason ? 1f : 0f, StatAggregationMethod.Average);
            }
            recorder.Add("Soccer/Curriculum/L3/Required Forward Progress",
                L3RequiredForwardProgress, StatAggregationMethod.Average);
        }
    }
}

using System.Linq;
using Unity.MLAgents;
using UnityEngine;

namespace MachineLearning.Soccer.Curriculum
{
    public sealed partial class SoccerCurriculumController
    {
        public const string L2SpawnDifficultyParameter = "soccer_l2_spawn_difficulty";
        public const string L2WaitingAssistParameter = "soccer_l2_waiting_assist";
        public const string L2PassAimGateParameter = "soccer_l2_pass_aim_gate";
        public const string L2PassAdviceParameter = "soccer_l2_pass_advice";
        public const float L2PossessionReward = 0.1f;
        public const float L2PassAttemptReward = 0.15f;
        public const float L2PassDirectionRewardCap = 0.1f;
        public const float L2PassReceptionReward = 0.2f;
        public const float L2PassDeliveredReward = 0.1f;
        public const float L2WaitingReceiverDistance = 15.5f;

        AgentSoccer m_L2Passer;
        AgentSoccer m_L2Target;
        AgentSoccer m_L2LastTouch;
        Vector3 m_L2StrikePosition;
        float m_L2StrikeTime;
        float m_L2ReceiverControlSince;
        float m_L2SpawnDifficulty;
        bool m_L2Attempted;
        bool m_L2AnyKick;
        bool m_L2Pending;
        bool m_L2ReceiverTouched;
        int m_L2Invalidated;
        bool m_L2WaitingAssist;
        bool m_L2PassAimGate;
        int m_L2FlightsStarted;
        int m_L2ReceiverRekicks;
        int m_L2PasserRekicks;
        int m_L2OtherRekicks;
        int m_L2PasserRetouches;
        int m_L2OtherRetouches;
        int m_L2ExpiredFlights;
        bool m_L2ReceiverConfirmed;
        float m_L2MaximumReceiverControl;
        bool m_L2FlightReceiverTouched;
        int m_L2InterruptedBeforeReception;
        int m_L2InterruptedAfterReception;
        bool m_L2ConfirmedShortDisplacement;
        float m_L2BestStrikeQuality;
        float m_L2BestRewardedDirectionQuality;
        AgentSoccer m_L2DesignatedReceiver;
        AgentSoccer m_L2PreparationTarget;
        float m_L2PreparationBestQuality;
        float m_L2PreparationRewardTotal;
        int m_L2PreparationRewardEvents;
        bool m_L2PreparationTargetAvailable;
        // Read-only first-opportunity diagnostics; never used to choose actions or rewards.
        AgentSoccer m_L2FirstCarrier;
        float m_L2FirstPossessionTime;
        bool m_L2FirstKickReadyRecorded;
        Vector3 m_L2FirstCarrierPosition;
        int m_L2ConfirmedPreparationSamples, m_L2LostControlPreparationSamples;
        int m_L2DesignatedTooCloseSamples, m_L2DesignatedTooFarSamples, m_L2DesignatedNoAdvantageSamples;
        int m_L2DesignatedEligibleSamples, m_L2AnyTargetSamples, m_L2AnyLaneSamples;
        bool m_L2OpportunitySeen, m_L2OpportunityLossRecorded;
        SoccerCurriculumPassRules.OpportunityLossTracker m_L2OpportunityLoss;
        float m_L2OpportunityLossReward;

        public static bool ShouldUseL2WaitingAssist(float difficulty, float requested)
        {
            return !float.IsNaN(difficulty) && !float.IsInfinity(difficulty)
                && difficulty >= 0f && difficulty < 1f
                && !float.IsNaN(requested) && !float.IsInfinity(requested) && requested > 0.5f;
        }

        void ResetL2Round()
        {
            m_L2Passer = m_L2Target = m_L2LastTouch = null;
            m_L2Pending = m_L2Attempted = m_L2AnyKick = m_L2ReceiverTouched = false;
            m_L2Invalidated = 0;
            m_L2BestStrikeQuality = 0f;
            m_L2BestRewardedDirectionQuality = 0f;
            m_L2DesignatedReceiver = null;
            m_L2PreparationTarget = null;
            m_L2PreparationBestQuality = 0f;
            m_L2PreparationRewardTotal = 0f;
            m_L2PreparationRewardEvents = 0;
            m_L2PreparationTargetAvailable = false;
            m_L2FirstCarrier = null;
            m_L2FirstPossessionTime = float.NaN;
            m_L2FirstKickReadyRecorded = false;
            m_L2FirstCarrierPosition = Vector3.zero;
            m_L2ConfirmedPreparationSamples = m_L2LostControlPreparationSamples = 0;
            m_L2DesignatedTooCloseSamples = m_L2DesignatedTooFarSamples = m_L2DesignatedNoAdvantageSamples = 0;
            m_L2DesignatedEligibleSamples = m_L2AnyTargetSamples = m_L2AnyLaneSamples = 0;
            m_L2OpportunitySeen = m_L2OpportunityLossRecorded = false;
            m_L2OpportunityLoss = new SoccerCurriculumPassRules.OpportunityLossTracker();
            m_L2OpportunityLossReward = 0f;
            m_L2FlightsStarted = m_L2ReceiverRekicks = m_L2PasserRekicks = m_L2OtherRekicks = 0;
            m_L2PasserRetouches = m_L2OtherRetouches = m_L2ExpiredFlights = 0;
            m_L2ReceiverConfirmed = false;
            m_L2MaximumReceiverControl = 0f;
            m_L2FlightReceiverTouched = m_L2ConfirmedShortDisplacement = false;
            m_L2InterruptedBeforeReception = m_L2InterruptedAfterReception = 0;
            m_L2ReceiverControlSince = float.NaN;
            var requested = Academy.IsInitialized
                ? Academy.Instance.EnvironmentParameters.GetWithDefault(L2SpawnDifficultyParameter, 1f) : 1f;
            m_L2SpawnDifficulty = float.IsNaN(requested) || float.IsInfinity(requested)
                ? 1f : Mathf.Clamp01(requested);
            m_L2WaitingAssist = ShouldUseL2WaitingAssist(m_L2SpawnDifficulty, Academy.IsInitialized
                ? Academy.Instance.EnvironmentParameters.GetWithDefault(L2WaitingAssistParameter, 0f) : 0f);
            m_L2PassAimGate = ShouldUseL2WaitingAssist(m_L2SpawnDifficulty, Academy.IsInitialized
                ? Academy.Instance.EnvironmentParameters.GetWithDefault(L2PassAimGateParameter, 0f) : 0f);
            m_Environment.ConfigureCurriculumPassAimGate(m_L2PassAimGate);
            m_Environment.ClearCurriculumReceivingAgents();
            var passAdvice = Academy.IsInitialized
                ? Academy.Instance.EnvironmentParameters.GetWithDefault(L2PassAdviceParameter, 0f) : 0f;
            m_Environment.ConfigureCurriculumPassAdvice(passAdvice > .5f);
            m_Environment.ConfigureCurriculumPassSupport(passAdvice > .5f ? m_L2DesignatedReceiver : null, m_FocusAgent);
            m_RewardEngine.ConfigureCurriculumRewardStage(lesson == SoccerCurriculumLesson.L3ProgressivePlay
                ? SoccerCurriculumRewardStage.L3ProgressivePlay : SoccerCurriculumRewardStage.L2Pass);
        }

        void PositionL2Agents()
        {
            var focusX = Mathf.Lerp(0f, Random.Range(-8f, 8f), m_L2SpawnDifficulty);
            var focusZ = Mathf.Lerp(0f, Random.Range(-8f, 8f), m_L2SpawnDifficulty);
            MoveAgent(m_FocusAgent, new Vector3(focusX, m_FocusAgent.transform.position.y, focusZ));
            var partners = m_RedAgents.Where(a => a != m_FocusAgent
                && a.PositionRole != AgentSoccer.Position.DefenderKeeper).ToArray();
            // Rotate which of the other field players is the forward receiver.
            var receiver = partners[(m_RoundIndex / 3) % partners.Length];
            m_L2DesignatedReceiver = receiver;
            m_Environment.ConfigureCurriculumPassSupport(receiver, m_FocusAgent);
            foreach (var partner in partners)
            {
                var receiving = partner == receiver;
                MoveAgent(partner, new Vector3(
                    focusX + (receiving
                        ? (m_L2WaitingAssist ? L2WaitingReceiverDistance
                            : Random.Range(6f, 8f + 4f * m_L2SpawnDifficulty)) : -10f),
                    partner.transform.position.y,
                    focusZ + (receiving ? Random.Range(-4f, 4f) * m_L2SpawnDifficulty : 10f)));
                partner.transform.rotation = Quaternion.LookRotation(
                    m_FocusAgent.transform.position - partner.transform.position, Vector3.up);
                m_Environment.SetCurriculumWaitingAgent(partner, m_L2WaitingAssist);
            }
            var keeper = m_RedAgents.First(a => a.PositionRole == AgentSoccer.Position.DefenderKeeper);
            MoveAgent(keeper, new Vector3(-40f, keeper.transform.position.y, 0f));
        }

        void HandleL2Strike(AgentSoccer actor, int kickAction, Vector3 direction, bool safetyRedirected)
        {
            if (!m_HasConfiguredRound || m_RoundSucceeded || actor == null) return;
            if (m_L3Active)
            {
                FailL3Kick();
                return;
            }
            // Diagnostics only: retain the original newer-kick invalidation semantics.
            if (m_L2Pending)
            {
                m_Environment.ClearCurriculumReceivingAgents();
                RecordL2InterruptionPhase();
                if (actor == m_L2Target) m_L2ReceiverRekicks++;
                else if (actor == m_L2Passer) m_L2PasserRekicks++;
                else m_L2OtherRekicks++;
            }
            // Any newer kick supersedes the previous flight, including a wrong kick.
            m_L2Pending = false;
            m_L2ReceiverControlSince = float.NaN;
            if (actor.Team != Team.Red || kickAction == 0 || safetyRedirected) return;
            if (!m_L2AnyKick)
            {
                RecordL2OpportunitySnapshot("First Strike", actor, direction);
                if (Academy.IsInitialized)
                {
                    var recorder = Academy.Instance.StatsRecorder;
                    recorder.Add("Soccer/Curriculum/L2/First Strike Initial Carrier Sampled",
                        m_L2FirstCarrier != null ? 1f : 0f, StatAggregationMethod.Average);
                    if (m_L2FirstCarrier != null)
                    {
                        recorder.Add("Soccer/Curriculum/L2/First Strike Same Initial Carrier",
                            actor == m_L2FirstCarrier ? 1f : 0f, StatAggregationMethod.Average);
                        recorder.Add("Soccer/Curriculum/L2/First Strike Seconds Since Possession",
                            Time.time - m_L2FirstPossessionTime, StatAggregationMethod.Average);
                    }
                }
            }
            m_L2AnyKick = true;
            RecordL2StrikeDiagnostics(actor, direction);
            if (GetControlledRedCarrier() != actor) return;
            var ball = m_Environment.Ball.transform.position;
            var goal = new Vector3(m_Environment.ArenaGeometry != null
                ? m_Environment.ArenaGeometry.HalfLength : SoccerArenaGeometry.StadiumHalfLength, 0f, 0f);
            var quality = m_RedAgents.Where(a => a != actor && a.isActiveAndEnabled)
                .Select(a => SoccerCurriculumPassRules.StrikeDirectionQuality(
                    actor.transform.position, ball, direction, a.transform.position, goal))
                .DefaultIfEmpty(0f).Max();
            var qualityIncrement = SoccerCurriculumPassRules.DirectionQualityIncrement(
                m_L2BestRewardedDirectionQuality, quality);
            m_L2BestStrikeQuality = Mathf.Max(m_L2BestStrikeQuality, quality);
            m_L2BestRewardedDirectionQuality = Mathf.Max(m_L2BestRewardedDirectionQuality, quality);
            if (qualityIncrement > 0f)
                m_RewardEngine.AwardCurriculumIndividualReward(actor,
                    SoccerRewardKind.CurriculumPassDirection, qualityIncrement * L2PassDirectionRewardCap);
            var target = m_RedAgents.Where(a => a != actor && a.isActiveAndEnabled
                && SoccerCurriculumPassRules.IsAdvantageousTarget(actor.transform.position, a.transform.position, goal)
                && SoccerCurriculumPassRules.IsDirectedAtTarget(ball, direction, a.transform.position))
                .OrderBy(a => PlanarDistance(ball, a.transform.position)).FirstOrDefault();
            if (target == null) return;
            // This preparation scaffold ends when an eligible real pass leaves the plate.
            m_Environment.ClearCurriculumWaitingAgents();
            m_L2Passer = actor;
            m_L2Target = target;
            m_L2StrikePosition = ball;
            m_L2StrikeTime = Time.time;
            m_L2LastTouch = actor;
            m_L2Pending = true;
            m_Environment.SetCurriculumReceivingAgent(target, true);
            m_L2FlightReceiverTouched = false;
            m_L2FlightsStarted++;
            if (Academy.IsInitialized)
                Academy.Instance.StatsRecorder.Add("Soccer/Curriculum/L2/Eligible Strike Target Distance",
                    PlanarDistance(ball, target.transform.position), StatAggregationMethod.Average);
            if (!m_L2Attempted)
            {
                m_L2Attempted = true;
                m_RewardEngine.AwardCurriculumIndividualReward(actor,
                    SoccerRewardKind.CurriculumPassAttempt, L2PassAttemptReward);
            }
        }

        void HandleL2Touch(AgentSoccer actor)
        {
            m_L2LastTouch = actor;
            if (m_L3Active && actor != m_L3Receiver)
            {
                FailL3Touch();
                return;
            }
            if (!m_L2Pending) return;
            if (actor == m_L2Target)
            {
                if (!m_L2FlightReceiverTouched) RecordL2FirstReceptionDiagnostics(actor);
                m_L2FlightReceiverTouched = true;
                m_L2ReceiverTouched = true;
                return;
            }
            // Only ignore callbacks from the initial extending plate, not a later dribble.
            if (actor == m_L2Passer && Time.time <= m_L2StrikeTime + 0.1f
                && actor.KickPlate != null && actor.KickPlate.IsStrikeActive) return;
            RecordL2InterruptionPhase();
            m_Environment.ClearCurriculumReceivingAgents();
            m_L2Pending = false;
            m_L2Invalidated++;
            if (actor == m_L2Passer) m_L2PasserRetouches++;
            else m_L2OtherRetouches++;
            m_L2ReceiverControlSince = float.NaN;
        }

        void RecordL2InterruptionPhase()
        {
            if (m_L2FlightReceiverTouched) m_L2InterruptedAfterReception++;
            else m_L2InterruptedBeforeReception++;
        }

        void RecordL2FirstReceptionDiagnostics(AgentSoccer actor)
        {
            if (!Academy.IsInitialized) return;
            var recorder = Academy.Instance.StatsRecorder;
            var ball = m_Environment.Ball.transform.position;
            var displacement = PlanarDistance(m_L2StrikePosition, ball);
            // One sample per eligible flight's FIRST designated-receiver contact.
            // This callback observation does not approve a reception or alter behavior.
            recorder.Add("Soccer/Curriculum/L2/First Reception Ball Displacement", displacement, StatAggregationMethod.Average);
            recorder.Add("Soccer/Curriculum/L2/First Reception Displacement Met",
                displacement >= SoccerCurriculumPassRules.MinimumBallDisplacement ? 1f : 0f, StatAggregationMethod.Average);
            recorder.Add("Soccer/Curriculum/L2/First Reception Receiver Distance",
                PlanarDistance(actor.transform.position, ball), StatAggregationMethod.Average);
            recorder.Add("Soccer/Curriculum/L2/First Reception Passer Distance",
                PlanarDistance(m_L2Passer.transform.position, ball), StatAggregationMethod.Average);
            recorder.Add("Soccer/Curriculum/L2/First Reception Flight Seconds",
                Time.time - m_L2StrikeTime, StatAggregationMethod.Average);
        }

        void RecordL2StrikeDiagnostics(AgentSoccer actor, Vector3 direction)
        {
            if (!Academy.IsInitialized) return;
            var recorder = Academy.Instance.StatsRecorder;
            var ball = m_Environment.Ball.transform.position;
            var goal = new Vector3(m_Environment.ArenaGeometry != null
                ? m_Environment.ArenaGeometry.HalfLength : SoccerArenaGeometry.StadiumHalfLength, 0f, 0f);
            var teammates = m_RedAgents.Where(a => a != actor && a.isActiveAndEnabled).ToArray();
            var targets = teammates.Where(a => SoccerCurriculumPassRules.IsAdvantageousTarget(
                actor.transform.position, a.transform.position, goal)).ToArray();
            // Conditional on actual explicit contact, not on a decision or an episode.
            recorder.Add("Soccer/Curriculum/L2/Strike Confirmed Carrier",
                GetControlledRedCarrier() == actor ? 1f : 0f, StatAggregationMethod.Average);
            recorder.Add("Soccer/Curriculum/L2/Strike Ball Distance",
                PlanarDistance(actor.transform.position, ball), StatAggregationMethod.Average);
            recorder.Add("Soccer/Curriculum/L2/Strike Target Present",
                targets.Length > 0 ? 1f : 0f, StatAggregationMethod.Average);
            recorder.Add("Soccer/Curriculum/L2/Strike Target Lane Present",
                targets.Any(a => SoccerCurriculumPassRules.IsDirectedAtTarget(ball, direction, a.transform.position))
                    ? 1f : 0f, StatAggregationMethod.Average);
            if (teammates.Length > 0)
                recorder.Add("Soccer/Curriculum/L2/Strike Nearest Teammate Distance",
                    teammates.Min(a => PlanarDistance(actor.transform.position, a.transform.position)),
                    StatAggregationMethod.Average);
            if (targets.Length > 0)
                recorder.Add("Soccer/Curriculum/L2/Strike Best Target Error Degrees",
                    targets.Min(a => {
                        var offset = a.transform.position - ball;
                        offset.y = 0f;
                        return Vector3.Angle(direction, offset);
                    }), StatAggregationMethod.Average);
        }

        void RecordL2OpportunitySnapshot(string phase, AgentSoccer actor, Vector3 direction)
        {
            if (!Academy.IsInitialized || actor == null) return;
            var recorder = Academy.Instance.StatsRecorder;
            var prefix = "Soccer/Curriculum/L2/" + phase + " ";
            var ball = m_Environment.Ball.transform.position;
            var goal = new Vector3(m_Environment.ArenaGeometry != null
                ? m_Environment.ArenaGeometry.HalfLength : SoccerArenaGeometry.StadiumHalfLength, 0f, 0f);
            var teammates = m_RedAgents.Where(a => a != actor && a.isActiveAndEnabled).ToArray();
            var targets = teammates.Where(a => SoccerCurriculumPassRules.IsAdvantageousTarget(
                actor.transform.position, a.transform.position, goal)).ToArray();
            recorder.Add(prefix + "Round Seconds", Time.time - m_RoundStartTime, StatAggregationMethod.Average);
            recorder.Add(prefix + "Target Present", targets.Length > 0 ? 1f : 0f, StatAggregationMethod.Average);
            recorder.Add(prefix + "Target Lane Present", targets.Any(a =>
                SoccerCurriculumPassRules.IsDirectedAtTarget(ball, direction, a.transform.position)) ? 1f : 0f,
                StatAggregationMethod.Average);
            recorder.Add(prefix + "Ball Distance", PlanarDistance(actor.transform.position, ball), StatAggregationMethod.Average);
            if (m_L2DesignatedReceiver != null && m_L2DesignatedReceiver != actor)
            {
                var receiver = m_L2DesignatedReceiver.transform.position;
                var reasons = SoccerCurriculumPassRules.DiagnoseTarget(actor.transform.position, receiver, goal);
                recorder.Add(prefix + "Designated Distance", PlanarDistance(actor.transform.position, receiver), StatAggregationMethod.Average);
                recorder.Add(prefix + "Designated Goal Advantage", PlanarDistance(actor.transform.position, goal)
                    - PlanarDistance(receiver, goal), StatAggregationMethod.Average);
                recorder.Add(prefix + "Designated Too Close", (reasons & SoccerCurriculumPassRules.TargetRejection.TooClose) != 0 ? 1f : 0f, StatAggregationMethod.Average);
                recorder.Add(prefix + "Designated Too Far", (reasons & SoccerCurriculumPassRules.TargetRejection.TooFar) != 0 ? 1f : 0f, StatAggregationMethod.Average);
                recorder.Add(prefix + "Designated No Advantage", (reasons & SoccerCurriculumPassRules.TargetRejection.InsufficientAdvantage) != 0 ? 1f : 0f, StatAggregationMethod.Average);
            }
            recorder.Add(prefix + "Kick Allowed", m_Environment.CanRequestKick(actor) ? 1f : 0f, StatAggregationMethod.Average);
            recorder.Add(prefix + "Plate Ready", actor.KickPlate != null && actor.KickPlate.CanKick ? 1f : 0f,
                StatAggregationMethod.Average);
            if (teammates.Length > 0)
                recorder.Add(prefix + "Nearest Teammate Distance", teammates.Min(a =>
                    PlanarDistance(actor.transform.position, a.transform.position)), StatAggregationMethod.Average);
            if (targets.Length > 0)
            {
                recorder.Add(prefix + "Nearest Target Distance", targets.Min(a =>
                    PlanarDistance(actor.transform.position, a.transform.position)), StatAggregationMethod.Average);
                recorder.Add(prefix + "Best Target Error Degrees", targets.Min(a => {
                    var offset = a.transform.position - ball;
                    offset.y = 0f;
                    return Vector3.Angle(direction, offset);
                }), StatAggregationMethod.Average);
            }
        }

        void UpdateL2Lesson()
        {
            if (m_L2WaitingAssist && Academy.IsInitialized)
            {
                var waiting = m_RedAgents.Count(a => m_Environment.IsCurriculumWaitingAgent(a));
                if (waiting > 0)
                {
                    var amount = waiting * Time.fixedDeltaTime;
                    Academy.Instance.StatsRecorder.Add("Soccer/Skill Advice/L2 Waiting Agent Seconds", amount, StatAggregationMethod.Sum);
                    Academy.Instance.StatsRecorder.Add("Soccer/Red/Skill Advice/L2 Waiting Agent Seconds", amount, StatAggregationMethod.Sum);
                }
            }
            var carrier = GetControlledRedCarrier();
            if (m_L3Active)
            {
                UpdateL3Continuation(carrier);
                return;
            }
            if (carrier != null && !m_PossessionEstablished)
            {
                m_L2FirstCarrier = carrier;
                m_L2FirstCarrierPosition = carrier.transform.position;
                m_L2FirstPossessionTime = Time.time;
                RecordL2OpportunitySnapshot("First Possession", carrier,
                    AgentSoccer.CalculateBallPushDirection(carrier.transform.position, carrier.transform.forward,
                        m_Environment.Ball.transform.position, true));
                ConfigureL2PassPreparation(carrier);
                m_PossessionEstablished = true;
                m_RewardEngine.AwardCurriculumIndividualReward(carrier,
                    SoccerRewardKind.CurriculumPossessionEstablished, L2PossessionReward);
            }
            RecordL2ConfirmedPreparation(carrier);
            UpdateL2OpportunityLoss(carrier);
            UpdateL2PassPreparation(carrier);
            // FixedUpdate readiness sample, not an ML action-decision callback.
            if (!m_L2AnyKick && !m_L2FirstKickReadyRecorded && carrier != null && m_Environment.CanRequestKick(carrier)
                && carrier.KickPlate != null && carrier.KickPlate.CanKick)
            {
                m_L2FirstKickReadyRecorded = true;
                RecordL2OpportunitySnapshot("First Kick Ready", carrier,
                    AgentSoccer.CalculateBallPushDirection(carrier.transform.position, carrier.transform.forward,
                        m_Environment.Ball.transform.position, true));
            }
            if (carrier != null) RewardSupportShape(carrier);
            if (!m_L2Pending) return;
            var elapsed = Time.time - m_L2StrikeTime;
            if (elapsed > SoccerCurriculumPassRules.MaximumFlightSeconds)
            {
                m_Environment.ClearCurriculumReceivingAgents();
                m_L2Pending = false;
                m_L2Invalidated++;
                m_L2ExpiredFlights++;
                return;
            }
            if (carrier != m_L2Target || m_L2LastTouch != m_L2Target)
            {
                m_L2ReceiverControlSince = float.NaN;
                return;
            }
            if (float.IsNaN(m_L2ReceiverControlSince)) m_L2ReceiverControlSince = Time.time;
            m_L2ReceiverConfirmed = true;
            m_L2MaximumReceiverControl = Mathf.Max(m_L2MaximumReceiverControl,
                Time.time - m_L2ReceiverControlSince);
            var ball = m_Environment.Ball.transform.position;
            if (PlanarDistance(m_L2StrikePosition, ball) < SoccerCurriculumPassRules.MinimumBallDisplacement)
                m_L2ConfirmedShortDisplacement = true;
            if (!SoccerCurriculumPassRules.IsSuccessfulReception(true,
                carrier != m_L2Passer && carrier.Team == m_L2Passer.Team, carrier == m_L2Target, false,
                m_L2StrikePosition, ball, elapsed, Time.time - m_L2ReceiverControlSince,
                PlanarDistance(carrier.transform.position, ball))) return;
            m_L2Pending = false;
            m_Environment.ClearCurriculumReceivingAgents();
            m_RewardEngine.AwardCurriculumIndividualReward(m_L2Passer,
                SoccerRewardKind.CurriculumPassDelivered, L2PassDeliveredReward);
            m_RewardEngine.AwardCurriculumIndividualReward(carrier,
                SoccerRewardKind.CurriculumPassReception, L2PassReceptionReward);
            if (lesson == SoccerCurriculumLesson.L3ProgressivePlay)
                BeginL3Continuation(carrier);
            else
                CompleteLesson(carrier);
        }

        // Observation only, before the first explicit strike. Confirmed carrier is
        // mandatory: team possession / proximity never substitutes for identity.
        void RecordL2ConfirmedPreparation(AgentSoccer carrier)
        {
            if (m_L2AnyKick || m_L2FirstCarrier == null) return;
            if (carrier != m_L2FirstCarrier)
            {
                m_L2LostControlPreparationSamples++;
                return;
            }
            m_L2ConfirmedPreparationSamples++;
            var goal = new Vector3(m_Environment.ArenaGeometry != null
                ? m_Environment.ArenaGeometry.HalfLength : SoccerArenaGeometry.StadiumHalfLength, 0f, 0f);
            var ball = m_Environment.Ball.transform.position;
            var direction = AgentSoccer.CalculateBallPushDirection(carrier.transform.position,
                carrier.transform.forward, ball, true);
            var anyTarget = false;
            var anyLane = false;
            foreach (var teammate in m_RedAgents)
            {
                if (teammate == carrier || !teammate.isActiveAndEnabled) continue;
                var reasons = SoccerCurriculumPassRules.DiagnoseTarget(carrier.transform.position,
                    teammate.transform.position, goal);
                if (teammate == m_L2DesignatedReceiver)
                {
                    if ((reasons & SoccerCurriculumPassRules.TargetRejection.TooClose) != 0) m_L2DesignatedTooCloseSamples++;
                    if ((reasons & SoccerCurriculumPassRules.TargetRejection.TooFar) != 0) m_L2DesignatedTooFarSamples++;
                    if ((reasons & SoccerCurriculumPassRules.TargetRejection.InsufficientAdvantage) != 0) m_L2DesignatedNoAdvantageSamples++;
                    if (reasons == SoccerCurriculumPassRules.TargetRejection.None) m_L2DesignatedEligibleSamples++;
                }
                if (reasons != SoccerCurriculumPassRules.TargetRejection.None) continue;
                anyTarget = true;
                anyLane |= SoccerCurriculumPassRules.IsDirectedAtTarget(ball, direction, teammate.transform.position);
            }
            if (anyTarget) m_L2AnyTargetSamples++;
            if (anyLane) m_L2AnyLaneSamples++;
            // First disappearance of ALL eligible targets, once per round.
            if (m_L2OpportunitySeen && !anyTarget && !m_L2OpportunityLossRecorded)
            {
                m_L2OpportunityLossRecorded = true;
                RecordL2OpportunitySnapshot("First Lost Opportunity", carrier, direction);
                if (Academy.IsInitialized)
                {
                    Academy.Instance.StatsRecorder.Add("Soccer/Curriculum/L2/First Lost Opportunity Seconds Since Possession",
                        Time.time - m_L2FirstPossessionTime, StatAggregationMethod.Average);
                    Academy.Instance.StatsRecorder.Add("Soccer/Curriculum/L2/First Lost Opportunity Carrier Forward Travel",
                        carrier.transform.position.x - m_L2FirstCarrierPosition.x, StatAggregationMethod.Average);
                }
            }
            m_L2OpportunitySeen |= anyTarget;
        }

        void UpdateL2OpportunityLoss(AgentSoccer carrier)
        {
            if (m_L2FirstCarrier == null) return;
            var target = m_L2PreparationTarget;
            var goal = new Vector3(m_Environment.ArenaGeometry != null
                ? m_Environment.ArenaGeometry.HalfLength : SoccerArenaGeometry.StadiumHalfLength, 0f, 0f);
            if (m_L2OpportunityLoss.Observe(carrier == m_L2FirstCarrier
                    && target != null && target.isActiveAndEnabled, m_L2AnyKick,
                m_L2FirstCarrier.transform.position,
                target != null ? target.transform.position : Vector3.zero, goal))
                m_L2OpportunityLossReward = m_RewardEngine.AwardCurriculumPassOpportunityLoss(m_L2FirstCarrier);
        }

        void ConfigureL2PassPreparation(AgentSoccer carrier)
        {
            if (carrier == null || m_L2DesignatedReceiver == null
                || m_L2DesignatedReceiver == carrier || !m_L2DesignatedReceiver.isActiveAndEnabled)
                return;
            var goal = new Vector3(m_Environment.ArenaGeometry != null
                ? m_Environment.ArenaGeometry.HalfLength : SoccerArenaGeometry.StadiumHalfLength, 0f, 0f);
            if (!SoccerCurriculumPassRules.IsAdvantageousTarget(
                carrier.transform.position, m_L2DesignatedReceiver.transform.position, goal)) return;
            m_L2PreparationTarget = m_L2DesignatedReceiver;
            m_L2PreparationTargetAvailable = true;
            var ball = m_Environment.Ball.transform.position;
            var direction = AgentSoccer.CalculateBallPushDirection(
                carrier.transform.position, carrier.transform.forward, ball, true);
            var quality = SoccerCurriculumPassRules.StrikeDirectionQuality(
                carrier.transform.position, ball, direction, m_L2PreparationTarget.transform.position, goal);
            // The first controlled pose is the unpaid baseline. Preparation and
            // actual-strike direction rewards share one round-wide high-water mark.
            m_L2PreparationBestQuality = quality;
            m_L2BestRewardedDirectionQuality = quality;
        }

        void UpdateL2PassPreparation(AgentSoccer carrier)
        {
            if (m_L2AnyKick || carrier == null || carrier != m_L2FirstCarrier
                || m_L2PreparationTarget == null || !m_L2PreparationTarget.isActiveAndEnabled
                || !m_Environment.CanRequestKick(carrier)
                || carrier.KickPlate == null || !carrier.KickPlate.CanKick) return;
            var goal = new Vector3(m_Environment.ArenaGeometry != null
                ? m_Environment.ArenaGeometry.HalfLength : SoccerArenaGeometry.StadiumHalfLength, 0f, 0f);
            // Keep the first designated target fixed. Losing eligibility never
            // retargets the reward to an easier teammate or resets the baseline.
            if (!SoccerCurriculumPassRules.IsAdvantageousTarget(
                carrier.transform.position, m_L2PreparationTarget.transform.position, goal)) return;
            var ball = m_Environment.Ball.transform.position;
            var direction = AgentSoccer.CalculateBallPushDirection(
                carrier.transform.position, carrier.transform.forward, ball, true);
            var quality = SoccerCurriculumPassRules.StrikeDirectionQuality(
                carrier.transform.position, ball, direction, m_L2PreparationTarget.transform.position, goal);
            m_L2PreparationBestQuality = Mathf.Max(m_L2PreparationBestQuality, quality);
            var increment = SoccerCurriculumPassRules.DirectionQualityIncrement(
                m_L2BestRewardedDirectionQuality, quality);
            m_L2BestRewardedDirectionQuality = Mathf.Max(m_L2BestRewardedDirectionQuality, quality);
            if (increment <= 0f) return;
            var applied = m_RewardEngine.AwardCurriculumIndividualReward(carrier,
                SoccerRewardKind.CurriculumPassDirection, increment * L2PassDirectionRewardCap);
            m_L2PreparationRewardTotal += applied;
            if (applied > 0f) m_L2PreparationRewardEvents++;
        }

        void RecordL2Round()
        {
            var recorder = Academy.Instance.StatsRecorder;
            recorder.Add("Soccer/Curriculum/L2/Self Caused Opportunity Loss",
                m_L2OpportunityLossReward < 0f ? 1f : 0f, StatAggregationMethod.Average);
            recorder.Add("Soccer/Curriculum/L2/Opportunity Loss Reward",
                m_L2OpportunityLossReward, StatAggregationMethod.Average);
            // Per-round raw FixedUpdate sample counts (not decision or success counts).
            recorder.Add("Soccer/Curriculum/L2/Confirmed Preparation Samples", m_L2ConfirmedPreparationSamples, StatAggregationMethod.Average);
            recorder.Add("Soccer/Curriculum/L2/Preparation Lost Control Samples", m_L2LostControlPreparationSamples, StatAggregationMethod.Average);
            recorder.Add("Soccer/Curriculum/L2/Preparation Designated Too Close Samples", m_L2DesignatedTooCloseSamples, StatAggregationMethod.Average);
            recorder.Add("Soccer/Curriculum/L2/Preparation Designated Too Far Samples", m_L2DesignatedTooFarSamples, StatAggregationMethod.Average);
            recorder.Add("Soccer/Curriculum/L2/Preparation Designated No Advantage Samples", m_L2DesignatedNoAdvantageSamples, StatAggregationMethod.Average);
            recorder.Add("Soccer/Curriculum/L2/Preparation Designated Eligible Samples", m_L2DesignatedEligibleSamples, StatAggregationMethod.Average);
            recorder.Add("Soccer/Curriculum/L2/Preparation Any Target Samples", m_L2AnyTargetSamples, StatAggregationMethod.Average);
            recorder.Add("Soccer/Curriculum/L2/Preparation Any Lane Samples", m_L2AnyLaneSamples, StatAggregationMethod.Average);
            recorder.Add("Soccer/Curriculum/L2/Preparation Opportunity Seen", m_L2OpportunitySeen ? 1f : 0f, StatAggregationMethod.Average);
            recorder.Add("Soccer/Curriculum/L2/Preparation Opportunity Lost", m_L2OpportunityLossRecorded ? 1f : 0f, StatAggregationMethod.Average);
            recorder.Add("Soccer/Curriculum/L2/First Kick Ready Sampled Before Strike",
                m_L2FirstKickReadyRecorded ? 1f : 0f, StatAggregationMethod.Average);
            recorder.Add("Soccer/Curriculum/L2/Best Strike Direction Quality", m_L2BestStrikeQuality, StatAggregationMethod.Average);
            recorder.Add("Soccer/Curriculum/L2/Best Rewarded Direction Quality",
                m_L2BestRewardedDirectionQuality, StatAggregationMethod.Average);
            recorder.Add("Soccer/Curriculum/L2/Pass Preparation Target Available",
                m_L2PreparationTargetAvailable ? 1f : 0f, StatAggregationMethod.Average);
            recorder.Add("Soccer/Curriculum/L2/Pass Preparation Best Direction Quality",
                m_L2PreparationBestQuality, StatAggregationMethod.Average);
            recorder.Add("Soccer/Curriculum/L2/Pass Preparation Reward Total",
                m_L2PreparationRewardTotal, StatAggregationMethod.Average);
            recorder.Add("Soccer/Curriculum/L2/Pass Preparation Reward Events",
                m_L2PreparationRewardEvents, StatAggregationMethod.Average);
            recorder.Add("Soccer/Curriculum/L2/Spawn Difficulty", m_L2SpawnDifficulty, StatAggregationMethod.Average);
            recorder.Add("Soccer/Curriculum/L2/Waiting Assistance", m_L2WaitingAssist ? 1f : 0f, StatAggregationMethod.Average);
            recorder.Add("Soccer/Curriculum/L2/Pass Aim Gate", m_L2PassAimGate ? 1f : 0f, StatAggregationMethod.Average);
            recorder.Add("Soccer/Curriculum/L2/Required Receiver Control Seconds",
                SoccerCurriculumPassRules.ReceiverControlSeconds, StatAggregationMethod.Average);
            recorder.Add("Soccer/Curriculum/L2/Required Ball Displacement",
                SoccerCurriculumPassRules.MinimumBallDisplacement, StatAggregationMethod.Average);
            recorder.Add("Soccer/Curriculum/L2/Any Kick", m_L2AnyKick ? 1f : 0f, StatAggregationMethod.Average);
            recorder.Add("Soccer/Curriculum/L2/Pass Attempt", m_L2Attempted ? 1f : 0f, StatAggregationMethod.Average);
            recorder.Add("Soccer/Curriculum/L2/Receiver Touch", m_L2ReceiverTouched ? 1f : 0f, StatAggregationMethod.Average);
            recorder.Add("Soccer/Curriculum/L2/Invalidated Flights", m_L2Invalidated, StatAggregationMethod.Average);
            // Counts per episode. The old Invalidated Flights metric excludes newer kicks;
            // keep it unchanged for historical comparisons and expose all reasons separately.
            recorder.Add("Soccer/Curriculum/L2/Flights Started", m_L2FlightsStarted, StatAggregationMethod.Average);
            recorder.Add("Soccer/Curriculum/L2/Receiver Rekicks", m_L2ReceiverRekicks, StatAggregationMethod.Average);
            recorder.Add("Soccer/Curriculum/L2/Passer Rekicks", m_L2PasserRekicks, StatAggregationMethod.Average);
            recorder.Add("Soccer/Curriculum/L2/Other Rekicks", m_L2OtherRekicks, StatAggregationMethod.Average);
            recorder.Add("Soccer/Curriculum/L2/Passer Retouches", m_L2PasserRetouches, StatAggregationMethod.Average);
            recorder.Add("Soccer/Curriculum/L2/Other Retouches", m_L2OtherRetouches, StatAggregationMethod.Average);
            recorder.Add("Soccer/Curriculum/L2/Expired Flights", m_L2ExpiredFlights, StatAggregationMethod.Average);
            recorder.Add("Soccer/Curriculum/L2/Receiver Confirmed", m_L2ReceiverConfirmed ? 1f : 0f, StatAggregationMethod.Average);
            recorder.Add("Soccer/Curriculum/L2/Maximum Receiver Control Seconds", m_L2MaximumReceiverControl, StatAggregationMethod.Average);
            recorder.Add("Soccer/Curriculum/L2/Pending At End", m_L2Pending ? 1f : 0f, StatAggregationMethod.Average);
            recorder.Add("Soccer/Curriculum/L2/Interrupted Before Reception", m_L2InterruptedBeforeReception, StatAggregationMethod.Average);
            recorder.Add("Soccer/Curriculum/L2/Interrupted After Reception", m_L2InterruptedAfterReception, StatAggregationMethod.Average);
            recorder.Add("Soccer/Curriculum/L2/Confirmed Receiver Short Displacement", m_L2ConfirmedShortDisplacement ? 1f : 0f, StatAggregationMethod.Average);
        }
    }
}

using System;
using MachineLearning.Soccer;
using UnityEngine;

namespace MachineLearning.Soccer.Manager
{
    public enum MNG_PlayerSkill
    {
        None = 0,
        MoveTo = 1,
        Carry = 2,
        ReceivePass = 3,
        AimPass = 4,
        AimShot = 5,
        Press = 6,
        Cover = 7,
        KeeperHome = 8,
        Mark = 9,
        SupportRun = 10,
        KeeperClaim = 11,
        KeeperBlock = 12
    }

    public struct MNG_PlayerTask
    {
        public MNG_PlayerSkill Skill;
        public Vector2 Target;
        public int ReceiverSlot;
        public long Revision;
        public float ExpirySeconds;
        public long TaskId;
        public long ParentCommandId;
        public MNG_ActionSource Source;
        public bool StateChanged;
        public bool CommonRule;
        public bool PassBuildRule;
    }

    public static partial class MNG_TeamPlanner
    {
        public const float KeeperActivityRadiusMultiplier = 2f;
        public const float OriginalKeeperClaimDepth = 18f;
        public const float KeeperClaimDepth =
            OriginalKeeperClaimDepth * KeeperActivityRadiusMultiplier;
        public const float OriginalKeeperClaimLateralMargin = 10f;
        public const float KeeperClaimLateralMargin =
            OriginalKeeperClaimLateralMargin * KeeperActivityRadiusMultiplier;
        public const float OriginalKeeperClaimMaximumAdvance = 16f;
        public const float KeeperClaimMaximumAdvance =
            OriginalKeeperClaimMaximumAdvance * KeeperActivityRadiusMultiplier;
        public const float OriginalKeeperBlockDepth = 28f;
        public const float KeeperBlockDepth =
            OriginalKeeperBlockDepth * KeeperActivityRadiusMultiplier;
        public const float OriginalKeeperBlockLateralMargin = 14f;
        public const float KeeperBlockLateralMargin =
            OriginalKeeperBlockLateralMargin * KeeperActivityRadiusMultiplier;
        public const float OriginalKeeperBlockMaximumAdvance = 12f;
        public const float KeeperBlockMaximumAdvance =
            OriginalKeeperBlockMaximumAdvance * KeeperActivityRadiusMultiplier;
        public const int PlannerVersion = 10;
        public const float PrimaryPresserHoldSeconds = 1.25f;
        public const float PresserSwitchAdvantageMeters = 3f;
        public const float LocalBallAwarenessRadius = 16f;
        public const float LocalAwarenessSwitchAdvantageMeters = 1f;
        public const float MinimumFormationTargetSeparation = 6f;
        public const float MinimumCarrierAttackProgress = 6f;
        public const float NormalCarrierAttackProgress = 10f;
        public const float ShotTargetPostMargin = 1.25f;
        public const float PassTargetForwardLead = 3.5f;
        public const float PassTargetVelocityLeadSeconds = 0.35f;
        public const float PassTargetCenterBias = 1.5f;
        public const float KickExecutionSeconds = 2f;
        public const float KeeperDistributionExecutionSeconds = 3f;
        public const float KeeperPassMinimumDistance = 4.5f;
        public const float KeeperPassMaximumDistance = 38f;
        public const float KeeperPassLaneClearance = 3f;
        public const float KeeperReceiverPressureClearance = 3.5f;
        public const float KeeperNeutralClaimAdvantage = 5f;
        public const float KeeperChallengeDistance = 8f;
        public const float StallRecoveryApproachDepth = 4.5f;
        public const float StallRecoveryApproachWidth = 5f;
        public const float StallRecoveryOutletDepth = 6f;
        public const float StallRecoveryOutletWidth = 7f;
        public const int MinimumDefensivePressers = 2;
        public const float SecondaryPressureGoalSideDistance = 2.75f;
        public const float SecondaryPressureLateralDistance = 2.25f;
        public const float OwnGoalThreatDepth = 30f;
        public const float MidfielderRecoveryDepth = 12f;
        public const float MidfielderRecoveryLateralDistance = 5f;
        public const float StallShotAttemptSeconds = 2.5f;
        public const float StallShotMaximumStartDistance = 8f;
        public const float StallShotMinimumForwardDot = 0.25f;
        public const int MinimumAttackingParticipants = 2;
        public const float OpponentGoalAttackDepth = 30f;
        public const float AttackingSupportForwardDistance = 2.75f;
        public const float AttackingSupportLateralDistance = 3.75f;

        public static void Plan(
            MNG_MatchSnapshot snapshot,
            Team team,
            MNG_Command command,
            MNG_TeamDecisionState decision,
            long revision,
            float expirySeconds,
            MNG_PlayerTask[] destination)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (decision == null) throw new ArgumentNullException(nameof(decision));
            if (destination == null || destination.Length != MNG_MatchSnapshot.PlayersPerTeam)
                throw new ArgumentException("Planner output must contain exactly four slots.", nameof(destination));
            snapshot.ValidateOrThrow();
            decision.ValidateOrThrow();

            var sign = team == Team.Red ? 1f : -1f;
            var ball = snapshot.BallPosition;
            var carrier = snapshot.Carrier.IsValid && snapshot.Carrier.Team == team
                ? snapshot.Carrier.Slot
                : -1;
            var defending = carrier < 0;
            var requiresDoublePressure = RequiresDoublePressure(snapshot, team);
            var chaser = defending && (requiresDoublePressure || command != MNG_Command.ProtectBack)
                ? ResolvePrimaryPresser(snapshot, team, decision, ball)
                : ClearPrimaryPresser(decision);
            var secondaryPresser = requiresDoublePressure
                ? ResolveSecondaryPresser(snapshot, team, decision, ball, chaser)
                : -1;
            var attackingSupport = RequiresAttackingSupport(snapshot, team)
                ? SelectAttackingSupport(snapshot, team, decision, carrier)
                : -1;
            var stallShooter = SelectStallRecoveryShooter(snapshot, team, decision);
            var stallShotActive = stallShooter >= 0;
            var keeperReceiver = !stallShotActive && carrier >= 0
                && snapshot.GetPlayer(team, carrier).Role == MNG_PlayerRole.Keeper
                ? SelectKeeperDistributionReceiver(snapshot, team, carrier, decision)
                : -1;
            if (command == MNG_Command.PassBuild) keeperReceiver = MNG_TacticalTargetResolver.IsPassBuildReceiverValid(snapshot, team, carrier, decision.PendingPassReceiverSlot) ? decision.PendingPassReceiverSlot : -1;

            for (var slot = 0; slot < destination.Length; slot++)
            {
                var player = snapshot.GetPlayer(team, slot);
                if (!player.Active || player.IsHuman || !decision.ControlMask[slot])
                {
                    destination[slot] = CreateTask(MNG_PlayerSkill.None, player.Position, -1, revision, expirySeconds);
                    continue;
                }

                if (slot == stallShooter)
                {
                    destination[slot] = PlanStallRecoveryShot(
                        snapshot, team, slot, revision, expirySeconds);
                    continue;
                }

                if (player.Role == MNG_PlayerRole.Keeper && slot == carrier)
                {
                    destination[slot] = PlanKeeperDistribution(
                        snapshot, team, slot, keeperReceiver, revision, expirySeconds);
                    continue;
                }

                if (slot == keeperReceiver)
                {
                    destination[slot] = CreateTask(
                        MNG_PlayerSkill.ReceivePass,
                        SelectPassTarget(snapshot, team, slot),
                        -1,
                        revision,
                        Mathf.Max(expirySeconds,
                            snapshot.EpisodeElapsedSeconds + KickExecutionSeconds));
                    continue;
                }

                if (player.Role == MNG_PlayerRole.Keeper)
                {
                    destination[slot] = PlanKeeper(
                        snapshot, team, slot, sign, revision, expirySeconds);
                    continue;
                }

                if (snapshot.BallStallRecoveryActive)
                {
                    destination[slot] = stallShotActive
                        ? PlanStallShotSupport(
                            snapshot,
                            team,
                            decision,
                            slot,
                            stallShooter,
                            sign,
                            revision,
                            expirySeconds)
                        : requiresDoublePressure && slot == chaser
                            ? CreateTask(
                                MNG_PlayerSkill.Press,
                                PredictedInterceptTarget(snapshot, slot),
                                -1,
                                revision,
                                expirySeconds)
                        : requiresDoublePressure && slot == secondaryPresser
                            ? CreateTask(
                                MNG_PlayerSkill.Press,
                                SecondaryPressureTarget(snapshot, team, slot),
                                -1,
                                revision,
                                expirySeconds)
                        : PlanStalledFieldPlayer(
                            snapshot,
                            team,
                            decision,
                            slot,
                            carrier,
                            sign,
                            revision,
                            expirySeconds);
                    continue;
                }

                destination[slot] = PlanFieldPlayer(
                    snapshot, team, command, decision, slot, carrier, chaser,
                    secondaryPresser, attackingSupport, sign, revision, expirySeconds);
            }
            ApplyTargetSeparation(snapshot, team, destination);
        }

        static MNG_PlayerTask PlanStallRecoveryShot(
            MNG_MatchSnapshot snapshot,
            Team team,
            int shooterSlot,
            long revision,
            float expirySeconds)
        {
            var remaining = Mathf.Max(
                0.1f,
                MNG_GlobalBallStallTracker.TriggerSeconds
                    + StallShotAttemptSeconds
                    - snapshot.BallStationarySeconds);
            return CreateTask(
                MNG_PlayerSkill.AimShot,
                SelectShotTarget(snapshot, team, shooterSlot),
                -1,
                revision,
                Mathf.Max(expirySeconds, snapshot.EpisodeElapsedSeconds + remaining));
        }

        static MNG_PlayerTask PlanStallShotSupport(
            MNG_MatchSnapshot snapshot,
            Team team,
            MNG_TeamDecisionState decision,
            int slot,
            int shooterSlot,
            float sign,
            long revision,
            float expirySeconds)
        {
            var rank = FindStallRecoveryRank(snapshot, team, decision, slot, shooterSlot);
            if (rank < 0)
                return CreateTask(
                    MNG_PlayerSkill.None,
                    snapshot.GetPlayer(team, slot).Position,
                    -1,
                    revision,
                    expirySeconds);

            var side = RecoverySide(snapshot.BallStallRecoverySequence + rank, team);
            var target = rank == 0
                ? snapshot.BallPosition + new Vector2(
                    -sign * StallRecoveryApproachDepth,
                    side * StallRecoveryApproachWidth)
                : snapshot.BallPosition + new Vector2(
                    sign * StallRecoveryOutletDepth,
                    side * StallRecoveryOutletWidth);
            return CreateTask(
                rank == 0 ? MNG_PlayerSkill.MoveTo : MNG_PlayerSkill.SupportRun,
                Clamp(target, snapshot),
                -1,
                revision,
                expirySeconds);
        }

        public static int SelectStallRecoveryShooter(
            MNG_MatchSnapshot snapshot,
            Team team,
            MNG_TeamDecisionState decision)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (decision == null) throw new ArgumentNullException(nameof(decision));
            if (!IsStallShotAttemptWindow(snapshot)) return -1;

            var ownCarrier = snapshot.Carrier.IsValid && snapshot.Carrier.Team == team
                ? snapshot.Carrier.Slot
                : -1;
            if (ownCarrier >= 0)
            {
                if (!IsEligibleStallShooter(snapshot, team, decision, ownCarrier, true))
                    return -1;
                var target = SelectShotTarget(snapshot, team, ownCarrier);
                return IsSafeStallShotDirection(snapshot, team, target) ? ownCarrier : -1;
            }

            var bestSlot = -1;
            var bestDistance = float.PositiveInfinity;
            for (var slot = 1; slot < MNG_MatchSnapshot.PlayersPerTeam; slot++)
            {
                if (!IsEligibleStallShooter(snapshot, team, decision, slot, false)) continue;
                var distance = Vector2.Distance(
                    snapshot.GetPlayer(team, slot).Position,
                    snapshot.BallPosition);
                if (distance > StallShotMaximumStartDistance || distance >= bestDistance) continue;
                var target = SelectShotTarget(snapshot, team, slot);
                if (!IsSafeStallShotDirection(snapshot, team, target)) continue;
                bestDistance = distance;
                bestSlot = slot;
            }
            return bestSlot;
        }

        public static bool IsStallShotAttemptWindow(MNG_MatchSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            return snapshot.BallStallRecoveryActive
                && snapshot.BallStationarySeconds >= MNG_GlobalBallStallTracker.TriggerSeconds
                && snapshot.BallStationarySeconds
                    <= MNG_GlobalBallStallTracker.TriggerSeconds + StallShotAttemptSeconds;
        }

        public static bool IsSafeStallShotDirection(
            MNG_MatchSnapshot snapshot,
            Team team,
            Vector2 target)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            var direction = target - snapshot.BallPosition;
            if (direction.sqrMagnitude <= 0.0001f) return false;
            var sign = team == Team.Red ? 1f : -1f;
            var forward = new Vector2(sign, 0f);
            return Vector2.Dot(direction.normalized, forward) >= StallShotMinimumForwardDot
                && (target.x - snapshot.BallPosition.x) * sign > 1f;
        }

        static bool IsEligibleStallShooter(
            MNG_MatchSnapshot snapshot,
            Team team,
            MNG_TeamDecisionState decision,
            int slot,
            bool allowKeeper)
        {
            if (slot < 0 || slot >= MNG_MatchSnapshot.PlayersPerTeam
                || !decision.ControlMask[slot])
                return false;
            var player = snapshot.GetPlayer(team, slot);
            return player.Active
                && !player.IsHuman
                && (allowKeeper || player.Role != MNG_PlayerRole.Keeper)
                && player.KickCooldownSeconds <= 0.05f;
        }

        static MNG_PlayerTask PlanKeeperDistribution(
            MNG_MatchSnapshot snapshot,
            Team team,
            int keeperSlot,
            int receiverSlot,
            long revision,
            float expirySeconds)
        {
            var kickExpiry = Mathf.Max(
                expirySeconds,
                snapshot.EpisodeElapsedSeconds + KeeperDistributionExecutionSeconds);
            if (receiverSlot >= 0)
            {
                return CreateTask(
                    MNG_PlayerSkill.AimPass,
                    SelectPassTarget(snapshot, team, receiverSlot),
                    receiverSlot,
                    revision,
                    kickExpiry);
            }

            return CreateTask(
                MNG_PlayerSkill.AimShot,
                SelectShotTarget(snapshot, team, keeperSlot),
                -1,
                revision,
                kickExpiry);
        }

        public static int SelectKeeperDistributionReceiver(
            MNG_MatchSnapshot snapshot,
            Team team,
            int keeperSlot,
            MNG_TeamDecisionState decision)
        {
            var sign = team == Team.Red ? 1f : -1f;
            var opponent = team == Team.Red ? Team.Navy : Team.Red;
            var keeper = snapshot.GetPlayer(team, keeperSlot);
            var bestSlot = -1;
            var bestScore = float.NegativeInfinity;
            for (var slot = 0; slot < MNG_MatchSnapshot.PlayersPerTeam; slot++)
            {
                if (slot == keeperSlot || !decision.ControlMask[slot]) continue;
                var receiver = snapshot.GetPlayer(team, slot);
                if (!receiver.Active || receiver.IsHuman
                    || receiver.Role == MNG_PlayerRole.Keeper)
                    continue;

                var distance = Vector2.Distance(keeper.Position, receiver.Position);
                var progress = (receiver.Position.x - keeper.Position.x) * sign;
                if (distance < KeeperPassMinimumDistance
                    || distance > KeeperPassMaximumDistance
                    || progress < 0.5f)
                    continue;

                var receiverClearance = MinimumPlayerDistance(
                    snapshot, opponent, receiver.Position, -1);
                if (receiverClearance < KeeperReceiverPressureClearance) continue;

                var laneClearance = float.PositiveInfinity;
                for (var opponentSlot = 0;
                     opponentSlot < MNG_MatchSnapshot.PlayersPerTeam;
                     opponentSlot++)
                {
                    var opponentPlayer = snapshot.GetPlayer(opponent, opponentSlot);
                    if (!opponentPlayer.Active) continue;
                    laneClearance = Mathf.Min(
                        laneClearance,
                        DistanceToSegment(
                            opponentPlayer.Position,
                            keeper.Position,
                            receiver.Position));
                }
                if (laneClearance < KeeperPassLaneClearance) continue;

                var score = progress * 0.55f
                    + Mathf.Min(receiverClearance, 12f) * 0.75f
                    + Mathf.Min(laneClearance, 12f) * 0.95f
                    - distance * 0.10f
                    - slot * 0.0001f;
                if (score <= bestScore) continue;
                bestScore = score;
                bestSlot = slot;
            }
            return bestSlot;
        }

        static MNG_PlayerTask PlanStalledFieldPlayer(
            MNG_MatchSnapshot snapshot,
            Team team,
            MNG_TeamDecisionState decision,
            int slot,
            int carrier,
            float sign,
            long revision,
            float expirySeconds)
        {
            var ball = snapshot.BallPosition;
            var side = RecoverySide(snapshot.BallStallRecoverySequence, team);
            if (slot == carrier)
            {
                var carryDirection = new Vector2(sign, side * 0.55f).normalized;
                var carryTarget = Clamp(ball + carryDirection * NormalCarrierAttackProgress, snapshot);
                carryTarget = EnforceAttackingCarryTarget(
                    ball,
                    carryTarget,
                    team,
                    snapshot.FieldHalfLength,
                    snapshot.FieldHalfWidth);
                return CreateTask(
                    MNG_PlayerSkill.Carry,
                    carryTarget,
                    -1,
                    revision,
                    expirySeconds);
            }

            var rank = FindStallRecoveryRank(snapshot, team, decision, slot, carrier);
            if (rank < 0)
                return CreateTask(MNG_PlayerSkill.None,
                    snapshot.GetPlayer(team, slot).Position, -1, revision, expirySeconds);

            if (carrier < 0 && rank == 0)
                return CreateTask(
                    MNG_PlayerSkill.Press,
                    ball,
                    -1,
                    revision,
                    expirySeconds);

            var supportRank = carrier < 0 ? rank - 1 : rank;
            var target = supportRank <= 0
                ? ball + new Vector2(
                    -sign * StallRecoveryApproachDepth,
                    side * StallRecoveryApproachWidth)
                : ball + new Vector2(
                    sign * StallRecoveryOutletDepth,
                    -side * StallRecoveryOutletWidth);
            return CreateTask(
                supportRank <= 0 ? MNG_PlayerSkill.MoveTo : MNG_PlayerSkill.SupportRun,
                Clamp(target, snapshot),
                -1,
                revision,
                expirySeconds);
        }

        static int FindStallRecoveryRank(
            MNG_MatchSnapshot snapshot,
            Team team,
            MNG_TeamDecisionState decision,
            int slot,
            int excludedCarrier)
        {
            var player = snapshot.GetPlayer(team, slot);
            if (!player.Active || player.IsHuman || !decision.ControlMask[slot]
                || player.Role == MNG_PlayerRole.Keeper || slot == excludedCarrier)
                return -1;

            var distance = (player.Position - snapshot.BallPosition).sqrMagnitude;
            var rank = 0;
            for (var otherSlot = 1;
                 otherSlot < MNG_MatchSnapshot.PlayersPerTeam;
                 otherSlot++)
            {
                if (otherSlot == slot || otherSlot == excludedCarrier) continue;
                var other = snapshot.GetPlayer(team, otherSlot);
                if (!other.Active || other.IsHuman || !decision.ControlMask[otherSlot]) continue;
                var otherDistance = (other.Position - snapshot.BallPosition).sqrMagnitude;
                if (otherDistance < distance
                    || (Mathf.Abs(otherDistance - distance) <= 0.0001f && otherSlot < slot))
                    rank++;
            }
            return rank;
        }

        static float RecoverySide(int sequence, Team team)
        {
            var parity = sequence + (team == Team.Red ? 0 : 1);
            return (parity & 1) == 0 ? 1f : -1f;
        }

        static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
        {
            var segment = end - start;
            var lengthSquared = segment.sqrMagnitude;
            if (lengthSquared <= 0.0001f) return Vector2.Distance(point, start);
            var t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / lengthSquared);
            return Vector2.Distance(point, start + segment * t);
        }

        static MNG_PlayerTask PlanFieldPlayer(
            MNG_MatchSnapshot snapshot,
            Team team,
            MNG_Command command,
            MNG_TeamDecisionState decision,
            int slot,
            int carrier,
            int chaser,
            int secondaryPresser,
            int attackingSupport,
            float sign,
            long revision,
            float expirySeconds)
        {
            var ball = snapshot.BallPosition;
            if (slot == carrier)
            {
                switch (command)
                {
                    case MNG_Command.PassBuild:
                        if (!MNG_TacticalTargetResolver.IsPassBuildReceiverValid(snapshot, team, carrier, decision.PendingPassReceiverSlot))
                            return CreateTask(MNG_PlayerSkill.None, ball, -1, revision, expirySeconds);
                        var receiverTarget = decision.PendingPassReceiverSlot >= 0
                            ? SelectPassTarget(
                                snapshot,
                                team,
                                decision.PendingPassReceiverSlot)
                            : ball;
                        return CreateTask(MNG_PlayerSkill.AimPass, receiverTarget,
                            decision.PendingPassReceiverSlot, revision,
                            Mathf.Max(expirySeconds,
                                snapshot.EpisodeElapsedSeconds + KickExecutionSeconds));
                    case MNG_Command.AttemptShot:
                        return CreateTask(MNG_PlayerSkill.AimShot,
                            SelectShotTarget(snapshot, team, slot), -1, revision,
                            Mathf.Max(expirySeconds,
                                snapshot.EpisodeElapsedSeconds + KickExecutionSeconds));
                    default:
                        var advance = command == MNG_Command.ProtectBack
                            ? MinimumCarrierAttackProgress
                            : NormalCarrierAttackProgress;
                        var attackingDepth = ball.x * sign;
                        var shootingLaneBlend = Mathf.InverseLerp(10f, 45f, attackingDepth) * 0.75f;
                        var carryTarget = Clamp(new Vector2(
                            ball.x + sign * advance,
                            Mathf.Lerp(ball.y, 0f, shootingLaneBlend)), snapshot);
                        carryTarget = SelectOpenLocalRoute(
                            snapshot, team, slot, carryTarget, true);
                        carryTarget = EnforceAttackingCarryTarget(
                            ball,
                            carryTarget,
                            team,
                            snapshot.FieldHalfLength,
                            snapshot.FieldHalfWidth);
                        return CreateTask(MNG_PlayerSkill.Carry,
                            carryTarget,
                            -1, revision, expirySeconds);
                }
            }

            if (snapshot.Possession == MNG_Possession.Neutral
                || snapshot.Possession == (team == Team.Red ? MNG_Possession.Navy : MNG_Possession.Red))
            {
                if (slot == chaser)
                    return CreateTask(MNG_PlayerSkill.Press,
                        PredictedInterceptTarget(snapshot, slot), -1, revision, expirySeconds);

                if (slot == secondaryPresser)
                    return CreateTask(
                        MNG_PlayerSkill.Press,
                        SecondaryPressureTarget(snapshot, team, slot),
                        -1,
                        revision,
                        expirySeconds);

                if (IsOwnGoalThreat(snapshot, team)
                    && (snapshot.GetPlayer(team, slot).Role == MNG_PlayerRole.MidLeft
                        || snapshot.GetPlayer(team, slot).Role == MNG_PlayerRole.MidRight))
                    return CreateTask(
                        MNG_PlayerSkill.Cover,
                        MidfielderRecoveryTarget(snapshot, team, slot),
                        -1,
                        revision,
                        expirySeconds);

                var defensiveTarget = DefensiveTarget(snapshot, team, slot, command);
                var canMark = command != MNG_Command.ProtectBack
                    && snapshot.Possession != MNG_Possession.Neutral;
                var skill = canMark ? MNG_PlayerSkill.Mark : MNG_PlayerSkill.Cover;
                if (canMark)
                    defensiveTarget = MarkingTarget(snapshot, team, slot, defensiveTarget);
                defensiveTarget = SelectOpenLocalRoute(
                    snapshot, team, slot, defensiveTarget, false);
                return CreateTask(skill, defensiveTarget, -1, revision, expirySeconds);
            }

            if (command == MNG_Command.PassBuild && slot == decision.PendingPassReceiverSlot)
                return CreateTask(MNG_PlayerSkill.ReceivePass,
                    SelectPassTarget(snapshot, team, slot),
                    -1, revision,
                    Mathf.Max(expirySeconds,
                        snapshot.EpisodeElapsedSeconds + KickExecutionSeconds));

            if (slot == attackingSupport)
                return CreateTask(
                    MNG_PlayerSkill.SupportRun,
                    AttackingSupportTarget(snapshot, team, slot),
                    -1,
                    revision,
                    expirySeconds);

            return CreateTask(MNG_PlayerSkill.SupportRun,
                SelectOpenLocalRoute(snapshot, team, slot,
                    SupportTarget(snapshot, team, slot, command), true),
                -1, revision, expirySeconds);
        }

        static MNG_PlayerTask PlanKeeper(
            MNG_MatchSnapshot snapshot,
            Team team,
            int slot,
            float sign,
            long revision,
            float expirySeconds)
        {
            var ownGoalX = -sign * snapshot.FieldHalfLength;
            var ball = snapshot.BallPosition;
            var depthFromGoal = (ball.x - ownGoalX) * sign;
            var ownPossession = team == Team.Red ? MNG_Possession.Red : MNG_Possession.Navy;
            var opponentPossession = team == Team.Red ? MNG_Possession.Navy : MNG_Possession.Red;
            var home = new Vector2(
                ownGoalX + sign * 4f,
                Mathf.Clamp(ball.y * 0.35f, -snapshot.GoalHalfWidth - 4f, snapshot.GoalHalfWidth + 4f));

            if (snapshot.Possession == MNG_Possession.Neutral
                && depthFromGoal >= -1f && depthFromGoal <= KeeperClaimDepth
                && Mathf.Abs(ball.y)
                <= snapshot.GoalHalfWidth + KeeperClaimLateralMargin)
            {
                var keeper = snapshot.GetPlayer(team, slot);
                var keeperDistance = Vector2.Distance(keeper.Position, ball);
                var closestFieldDistance = FindClosestFieldDistance(snapshot, team, ball);
                var ballHeadingHome = snapshot.BallVelocity.x * sign < -0.75f;
                if (ballHeadingHome
                    || snapshot.BallStallRecoveryActive
                    || keeperDistance <= closestFieldDistance + KeeperNeutralClaimAdvantage)
                {
                    var predicted = ball + snapshot.BallVelocity * 0.30f;
                    predicted.x = ownGoalX + sign * Mathf.Clamp(
                        (predicted.x - ownGoalX) * sign,
                        2.5f,
                        KeeperClaimMaximumAdvance);
                    predicted.y = Mathf.Clamp(
                        predicted.y, -snapshot.GoalHalfWidth - 6f, snapshot.GoalHalfWidth + 6f);
                    return CreateTask(
                        MNG_PlayerSkill.KeeperClaim, Clamp(predicted, snapshot), -1, revision, expirySeconds);
                }
            }


            if (snapshot.Possession == opponentPossession
                && depthFromGoal >= -1f && depthFromGoal <= KeeperClaimDepth
                && Mathf.Abs(ball.y)
                <= snapshot.GoalHalfWidth + KeeperClaimLateralMargin)
            {
                var keeper = snapshot.GetPlayer(team, slot);
                if (Vector2.Distance(keeper.Position, ball) <= KeeperChallengeDistance)
                {
                    var challenge = ball + snapshot.BallVelocity * 0.18f;
                    challenge.x = ownGoalX + sign * Mathf.Clamp(
                        (challenge.x - ownGoalX) * sign,
                        2.5f,
                        KeeperClaimMaximumAdvance);
                    return CreateTask(
                        MNG_PlayerSkill.KeeperClaim,
                        Clamp(challenge, snapshot),
                        -1,
                        revision,
                        expirySeconds);
                }
            }

            if (snapshot.Possession == opponentPossession
                && depthFromGoal >= -1f && depthFromGoal <= KeeperBlockDepth
                && Mathf.Abs(ball.y)
                <= snapshot.GoalHalfWidth + KeeperBlockLateralMargin)
            {
                var blockDepth = Mathf.Clamp(
                    depthFromGoal * 0.42f,
                    4f,
                    KeeperBlockMaximumAdvance);
                var lineFraction = blockDepth / Mathf.Max(depthFromGoal, 1f);
                var block = new Vector2(
                    ownGoalX + sign * blockDepth,
                    Mathf.Clamp(ball.y * lineFraction,
                        -snapshot.GoalHalfWidth - 2f, snapshot.GoalHalfWidth + 2f));
                return CreateTask(
                    MNG_PlayerSkill.KeeperBlock, Clamp(block, snapshot), -1, revision, expirySeconds);
            }

            if (snapshot.Possession == ownPossession) home.y *= 0.75f;
            return CreateTask(MNG_PlayerSkill.KeeperHome, Clamp(home, snapshot), -1, revision, expirySeconds);
        }

        static Vector2 PredictedInterceptTarget(MNG_MatchSnapshot snapshot, int slot)
        {
            var horizon = slot switch
            {
                1 => 0.22f,
                2 => 0.34f,
                3 => 0.46f,
                _ => 0.25f
            };
            return Clamp(snapshot.BallPosition + snapshot.BallVelocity * horizon, snapshot);
        }

        public static bool RequiresDoublePressure(MNG_MatchSnapshot snapshot, Team team)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            var opponentPossession = team == Team.Red
                ? MNG_Possession.Navy
                : MNG_Possession.Red;
            return snapshot.Possession == opponentPossession
                || (snapshot.Possession == MNG_Possession.Neutral
                    && IsOwnGoalThreat(snapshot, team));
        }

        public static bool IsOwnGoalThreat(MNG_MatchSnapshot snapshot, Team team)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            var sign = team == Team.Red ? 1f : -1f;
            var ownGoalX = -sign * snapshot.FieldHalfLength;
            var depthFromGoal = (snapshot.BallPosition.x - ownGoalX) * sign;
            var ownPossession = team == Team.Red
                ? MNG_Possession.Red
                : MNG_Possession.Navy;
            return snapshot.Possession != ownPossession
                && depthFromGoal >= -1f
                && depthFromGoal <= OwnGoalThreatDepth;
        }

        public static bool RequiresAttackingSupport(MNG_MatchSnapshot snapshot, Team team)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (!snapshot.Carrier.IsValid || snapshot.Carrier.Team != team) return false;
            var carrier = snapshot.GetPlayer(team, snapshot.Carrier.Slot);
            if (!carrier.Active || carrier.Role == MNG_PlayerRole.Keeper) return false;
            var ownPossession = team == Team.Red
                ? MNG_Possession.Red
                : MNG_Possession.Navy;
            if (snapshot.Possession != ownPossession) return false;

            var sign = team == Team.Red ? 1f : -1f;
            var opponentGoalX = sign * snapshot.FieldHalfLength;
            var depthFromGoal = (opponentGoalX - snapshot.BallPosition.x) * sign;
            return depthFromGoal >= -1f && depthFromGoal <= OpponentGoalAttackDepth;
        }

        public static int SelectAttackingSupport(
            MNG_MatchSnapshot snapshot,
            Team team,
            MNG_TeamDecisionState decision,
            int carrierSlot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (decision == null) throw new ArgumentNullException(nameof(decision));
            var receiver = decision.PendingPassReceiverSlot;
            if (receiver != carrierSlot
                && IsEligibleAttackingSupport(snapshot, team, decision, receiver))
                return receiver;

            var bestSlot = -1;
            var bestDistance = float.PositiveInfinity;
            for (var slot = 1; slot < MNG_MatchSnapshot.PlayersPerTeam; slot++)
            {
                if (slot == carrierSlot
                    || !IsEligibleAttackingSupport(snapshot, team, decision, slot))
                    continue;
                var distance = (snapshot.GetPlayer(team, slot).Position
                    - snapshot.BallPosition).sqrMagnitude;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                bestSlot = slot;
            }
            return bestSlot;
        }

        public static Vector2 AttackingSupportTarget(
            MNG_MatchSnapshot snapshot,
            Team team,
            int supportSlot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (supportSlot < 0 || supportSlot >= MNG_MatchSnapshot.PlayersPerTeam)
                throw new ArgumentOutOfRangeException(nameof(supportSlot));
            var sign = team == Team.Red ? 1f : -1f;
            var support = snapshot.GetPlayer(team, supportSlot);
            var side = Mathf.Abs(snapshot.BallPosition.y) > 1f
                ? -Mathf.Sign(snapshot.BallPosition.y)
                : support.Role == MNG_PlayerRole.MidLeft ? -sign
                : support.Role == MNG_PlayerRole.MidRight ? sign
                : Mathf.Abs(support.Position.y - snapshot.BallPosition.y) < .0001f ? sign
                : support.Position.y < snapshot.BallPosition.y ? -1f : 1f;
            return Clamp(
                snapshot.BallPosition
                    + new Vector2(
                        sign * AttackingSupportForwardDistance,
                        side * AttackingSupportLateralDistance),
                snapshot);
        }

        static int ResolveSecondaryPresser(
            MNG_MatchSnapshot snapshot,
            Team team,
            MNG_TeamDecisionState decision,
            Vector2 ball,
            int primarySlot)
        {
            var bestSlot = -1;
            var bestDistance = float.PositiveInfinity;
            for (var slot = 1; slot < MNG_MatchSnapshot.PlayersPerTeam; slot++)
            {
                if (slot == primarySlot || !IsEligiblePresser(snapshot, team, decision, slot))
                    continue;
                var distance = (snapshot.GetPlayer(team, slot).Position - ball).sqrMagnitude;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                bestSlot = slot;
            }
            return bestSlot;
        }

        static Vector2 SecondaryPressureTarget(
            MNG_MatchSnapshot snapshot,
            Team team,
            int slot)
        {
            var sign = team == Team.Red ? 1f : -1f;
            var ownGoal = new Vector2(-sign * snapshot.FieldHalfLength, 0f);
            var predicted = snapshot.BallPosition + snapshot.BallVelocity * 0.30f;
            var goalSide = ownGoal - predicted;
            goalSide = goalSide.sqrMagnitude > 0.0001f
                ? goalSide.normalized
                : new Vector2(-sign, 0f);
            var lateral = new Vector2(-goalSide.y, goalSide.x);
            var player = snapshot.GetPlayer(team, slot);
            var desiredSide = player.Role == MNG_PlayerRole.MidLeft ? -1f
                : player.Role == MNG_PlayerRole.MidRight ? 1f
                : (player.Position.y - predicted.y) * sign <= 0f ? -1f : 1f;
            return Clamp(
                predicted
                    + goalSide * SecondaryPressureGoalSideDistance
                    + lateral * desiredSide * SecondaryPressureLateralDistance,
                snapshot);
        }

        static Vector2 MidfielderRecoveryTarget(
            MNG_MatchSnapshot snapshot,
            Team team,
            int slot)
        {
            var sign = team == Team.Red ? 1f : -1f;
            var role = snapshot.GetPlayer(team, slot).Role;
            var side = role == MNG_PlayerRole.MidLeft ? -1f : 1f;
            var y = Mathf.Clamp(
                snapshot.BallPosition.y * 0.45f + sign * side * MidfielderRecoveryLateralDistance,
                -snapshot.GoalHalfWidth - 6f,
                snapshot.GoalHalfWidth + 6f);
            return Clamp(
                new Vector2(
                    -sign * snapshot.FieldHalfLength + sign * MidfielderRecoveryDepth,
                    y),
                snapshot);
        }

        static Vector2 MarkingTarget(
            MNG_MatchSnapshot snapshot,
            Team team,
            int slot,
            Vector2 fallback)
        {
            var sign = team == Team.Red ? 1f : -1f;
            var opponent = team == Team.Red ? Team.Navy : Team.Red;
            var player = snapshot.GetPlayer(team, slot);
            var ownGoal = new Vector2(-sign * snapshot.FieldHalfLength, 0f);
            var opponentCarrier = snapshot.Carrier.IsValid && snapshot.Carrier.Team == opponent
                ? snapshot.Carrier.Slot : -1;
            var selected = -1;
            var bestScore = float.PositiveInfinity;
            for (var opponentSlot = 0; opponentSlot < MNG_MatchSnapshot.PlayersPerTeam; opponentSlot++)
            {
                var candidate = snapshot.GetPlayer(opponent, opponentSlot);
                if (!candidate.Active || opponentSlot == opponentCarrier) continue;
                var playerDistance = Vector2.Distance(player.Position, candidate.Position);
                var laneDistance = Mathf.Abs(candidate.Position.y - (team == Team.Red ? 1f : -1f) * RoleLane(player.Role));
                var goalDistance = Vector2.Distance(candidate.Position, ownGoal);
                var roleMismatch = Mathf.Abs(opponentSlot - slot) * 1.5f;
                var score = playerDistance * 0.60f + laneDistance * 0.18f
                    + goalDistance * 0.10f + roleMismatch;
                if (score >= bestScore) continue;
                bestScore = score;
                selected = opponentSlot;
            }

            if (selected < 0) return fallback;
            var opponentPosition = snapshot.GetPlayer(opponent, selected).Position;
            var goalSide = ownGoal - opponentPosition;
            var markingPoint = opponentPosition
                + (goalSide.sqrMagnitude > 0.001f ? goalSide.normalized * 3f : Vector2.zero);
            return Clamp(Vector2.Lerp(fallback, markingPoint, 0.62f), snapshot);
        }

        static Vector2 SelectOpenLocalRoute(
            MNG_MatchSnapshot snapshot,
            Team team,
            int slot,
            Vector2 baseTarget,
            bool attacking)
        {
            var sign = team == Team.Red ? 1f : -1f;
            var player = snapshot.GetPlayer(team, slot);
            var laneBias = player.Role switch
            {
                MNG_PlayerRole.MidLeft => -sign,
                MNG_PlayerRole.MidRight => sign,
                MNG_PlayerRole.Striker => Mathf.Abs(snapshot.BallPosition.y) < .0001f ? sign : snapshot.BallPosition.y < 0f ? 1f : -1f,
                _ => 0f
            };
            var candidates = new[]
            {
                baseTarget,
                baseTarget + new Vector2(0f, laneBias * 5f),
                baseTarget + new Vector2(0f, -laneBias * 5f),
                baseTarget + new Vector2(sign * 3f, laneBias * 3f),
                baseTarget + new Vector2(sign * 5f, -laneBias * 4f)
            };

            var opponent = team == Team.Red ? Team.Navy : Team.Red;
            var best = Clamp(candidates[0], snapshot);
            var bestScore = float.NegativeInfinity;
            for (var candidateIndex = 0; candidateIndex < candidates.Length; candidateIndex++)
            {
                var candidate = Clamp(candidates[candidateIndex], snapshot);
                var opponentClearance = MinimumPlayerDistance(snapshot, opponent, candidate, -1);
                var teammateClearance = MinimumPlayerDistance(snapshot, team, candidate, slot);
                var travel = Vector2.Distance(player.Position, candidate);
                var progress = (candidate.x - player.Position.x) * sign;
                var score = Mathf.Min(opponentClearance, 12f) * 0.80f
                    + Mathf.Min(teammateClearance, 12f) * 0.90f
                    - travel * 0.13f
                    + progress * (attacking ? 0.40f : -0.03f)
                    - candidateIndex * 0.0001f;
                if (score <= bestScore) continue;
                bestScore = score;
                best = candidate;
            }
            return best;
        }

        public static Vector2 EnforceAttackingCarryTarget(
            Vector2 ball,
            Vector2 proposedTarget,
            Team team,
            float fieldHalfLength,
            float fieldHalfWidth)
        {
            var sign = team == Team.Red ? 1f : -1f;
            var requiredX = ball.x + sign * MinimumCarrierAttackProgress;
            var targetX = sign > 0f
                ? Mathf.Max(proposedTarget.x, requiredX)
                : Mathf.Min(proposedTarget.x, requiredX);
            return new Vector2(
                Mathf.Clamp(targetX, -fieldHalfLength + 1f, fieldHalfLength - 1f),
                Mathf.Clamp(Mathf.Sign(ball.y) * Mathf.Min(Mathf.Abs(proposedTarget.y), Mathf.Abs(Mathf.MoveTowards(ball.y, 0f, BallHoldLateralDistance))), -fieldHalfWidth + 1f, fieldHalfWidth - 1f));
        }

        public static Vector2 SelectShotTarget(
            MNG_MatchSnapshot snapshot,
            Team team,
            int carrierSlot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            var sign = team == Team.Red ? 1f : -1f;
            var opponent = team == Team.Red ? Team.Navy : Team.Red;
            var keeper = snapshot.GetPlayer(opponent, 0);
            float targetSide;
            if (keeper.Active && Mathf.Abs(keeper.Position.y) > 0.25f)
                targetSide = keeper.Position.y > 0f ? -1f : 1f;
            else if (Mathf.Abs(snapshot.BallPosition.y) > 0.25f)
                targetSide = snapshot.BallPosition.y > 0f ? -1f : 1f;
            else
                targetSide = sign * ((carrierSlot & 1) == 0 ? 1f : -1f);

            return new Vector2(
                sign * snapshot.FieldHalfLength,
                targetSide * Mathf.Max(0f, snapshot.GoalHalfWidth - ShotTargetPostMargin));
        }

        public static Vector2 SelectPassTarget(
            MNG_MatchSnapshot snapshot,
            Team team,
            int receiverSlot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (receiverSlot < 0 || receiverSlot >= MNG_MatchSnapshot.PlayersPerTeam)
                throw new ArgumentOutOfRangeException(nameof(receiverSlot));
            var sign = team == Team.Red ? 1f : -1f;
            var receiver = snapshot.GetPlayer(team, receiverSlot);
            var velocityLead = Vector2.ClampMagnitude(receiver.Velocity, 9f)
                * PassTargetVelocityLeadSeconds;
            var predicted = receiver.Position + velocityLead;
            var centerBiasedY = Mathf.MoveTowards(
                predicted.y,
                0f,
                PassTargetCenterBias);
            return Clamp(
                new Vector2(
                    predicted.x + sign * PassTargetForwardLead,
                    centerBiasedY),
                snapshot);
        }

        static float MinimumPlayerDistance(
            MNG_MatchSnapshot snapshot,
            Team team,
            Vector2 point,
            int excludedSlot)
        {
            var best = float.PositiveInfinity;
            for (var slot = 0; slot < MNG_MatchSnapshot.PlayersPerTeam; slot++)
            {
                if (slot == excludedSlot) continue;
                var player = snapshot.GetPlayer(team, slot);
                if (!player.Active) continue;
                best = Mathf.Min(best, Vector2.Distance(point, player.Position));
            }
            return float.IsPositiveInfinity(best) ? 20f : best;
        }

        static float FindClosestFieldDistance(
            MNG_MatchSnapshot snapshot,
            Team team,
            Vector2 point)
        {
            var best = float.PositiveInfinity;
            for (var slot = 0; slot < MNG_MatchSnapshot.PlayersPerTeam; slot++)
            {
                var player = snapshot.GetPlayer(team, slot);
                if (!player.Active || player.Role == MNG_PlayerRole.Keeper) continue;
                best = Mathf.Min(best, Vector2.Distance(player.Position, point));
            }
            return best;
        }

        static Vector2 SupportTarget(
            MNG_MatchSnapshot snapshot,
            Team team,
            int slot,
            MNG_Command command)
        {
            var sign = team == Team.Red ? 1f : -1f;
            var role = snapshot.GetPlayer(team, slot).Role;
            var anchor = FormationAnchor(snapshot, team, role, command);
            var forward = role switch
            {
                MNG_PlayerRole.MidLeft => 3f,
                MNG_PlayerRole.MidRight => 7f,
                MNG_PlayerRole.Striker => 15f,
                _ => -30f
            };
            if (command == MNG_Command.AdvanceCarry) forward += role == MNG_PlayerRole.Striker ? 4f : 2f;
            else if (command == MNG_Command.ProtectBack) forward -= 12f;

            var lane = sign * RoleLane(role);
            var ballTarget = new Vector2(
                snapshot.BallPosition.x + sign * forward,
                snapshot.BallPosition.y + lane * (role == MNG_PlayerRole.Striker ? 0.25f : 0.55f));
            var ballFollow = role switch
            {
                MNG_PlayerRole.MidLeft => 0.42f,
                MNG_PlayerRole.MidRight => 0.34f,
                MNG_PlayerRole.Striker => 0.52f,
                _ => 0f
            };
            return Clamp(Vector2.Lerp(anchor, ballTarget, ballFollow), snapshot);
        }

        static Vector2 DefensiveTarget(
            MNG_MatchSnapshot snapshot,
            Team team,
            int slot,
            MNG_Command command)
        {
            var sign = team == Team.Red ? 1f : -1f;
            var role = snapshot.GetPlayer(team, slot).Role;
            var anchor = FormationAnchor(snapshot, team, role, command);
            var depth = role switch
            {
                MNG_PlayerRole.MidLeft => -10f,
                MNG_PlayerRole.MidRight => -15f,
                MNG_PlayerRole.Striker => -4f,
                _ => -24f
            };
            var laneScale = role == MNG_PlayerRole.Striker ? 0.15f : 0.5f;
            var ballTarget = new Vector2(
                snapshot.BallPosition.x + sign * depth,
                snapshot.BallPosition.y + sign * RoleLane(role) * laneScale);
            var ballFollow = command == MNG_Command.ActiveRecover ? 0.58f
                : command == MNG_Command.ProtectBack ? 0.24f : 0.40f;
            if (role == MNG_PlayerRole.Striker) ballFollow *= 0.7f;
            return Clamp(Vector2.Lerp(anchor, ballTarget, ballFollow), snapshot);
        }

        static Vector2 FormationAnchor(
            MNG_MatchSnapshot snapshot,
            Team team,
            MNG_PlayerRole role,
            MNG_Command command)
        {
            var sign = team == Team.Red ? 1f : -1f;
            var ballDepth = snapshot.BallPosition.x * sign;
            var baseDepth = role switch
            {
                MNG_PlayerRole.MidLeft => -7f,
                MNG_PlayerRole.MidRight => -11f,
                MNG_PlayerRole.Striker => 18f,
                _ => -snapshot.FieldHalfLength + 4f
            };
            var commandShift = command switch
            {
                MNG_Command.AdvanceCarry => 6f,
                MNG_Command.AttemptShot => 8f,
                MNG_Command.ActiveRecover => -2f,
                MNG_Command.ProtectBack => -12f,
                _ => 0f
            };
            var depthFollow = role switch
            {
                MNG_PlayerRole.MidLeft => 0.24f,
                MNG_PlayerRole.MidRight => 0.18f,
                MNG_PlayerRole.Striker => 0.12f,
                _ => 0f
            };
            var x = sign * (baseDepth + commandShift
                + Mathf.Clamp(ballDepth * depthFollow, -9f, 9f));
            var yFollow = role == MNG_PlayerRole.Striker ? 0.10f : 0.16f;
            var y = sign * RoleLane(role) + snapshot.BallPosition.y * yFollow;
            return Clamp(new Vector2(x, y), snapshot);
        }

        static int ResolvePrimaryPresser(
            MNG_MatchSnapshot snapshot,
            Team team,
            MNG_TeamDecisionState decision,
            Vector2 ball)
        {
            var bestSlot = FindClosestControllable(snapshot, team, decision, ball, false);
            if (bestSlot < 0)
            {
                decision.PrimaryPresserSlot = -1;
                decision.PresserHoldUntilSeconds = 0f;
                return -1;
            }

            var currentSlot = decision.PrimaryPresserSlot;
            if (IsEligiblePresser(snapshot, team, decision, currentSlot))
            {
                var currentDistance = Vector2.Distance(
                    snapshot.GetPlayer(team, currentSlot).Position, ball);
                var bestDistance = Vector2.Distance(snapshot.GetPlayer(team, bestSlot).Position, ball);
                var nearbyPlayerMustReact = bestSlot != currentSlot
                    && bestDistance <= LocalBallAwarenessRadius
                    && currentDistance >= bestDistance + LocalAwarenessSwitchAdvantageMeters;
                if (!nearbyPlayerMustReact
                    && (snapshot.EpisodeElapsedSeconds < decision.PresserHoldUntilSeconds
                    || currentDistance <= bestDistance + PresserSwitchAdvantageMeters)
                   )
                    return currentSlot;
            }

            decision.PrimaryPresserSlot = bestSlot;
            decision.PresserHoldUntilSeconds = snapshot.EpisodeElapsedSeconds + PrimaryPresserHoldSeconds;
            return bestSlot;
        }

        static int ClearPrimaryPresser(MNG_TeamDecisionState decision)
        {
            decision.PrimaryPresserSlot = -1;
            decision.PresserHoldUntilSeconds = 0f;
            return -1;
        }

        static bool IsEligiblePresser(
            MNG_MatchSnapshot snapshot,
            Team team,
            MNG_TeamDecisionState decision,
            int slot)
        {
            if (slot < 0 || slot >= MNG_MatchSnapshot.PlayersPerTeam) return false;
            var player = snapshot.GetPlayer(team, slot);
            return player.Active && !player.IsHuman && decision.ControlMask[slot]
                && player.Role != MNG_PlayerRole.Keeper;
        }

        static bool IsEligibleAttackingSupport(
            MNG_MatchSnapshot snapshot,
            Team team,
            MNG_TeamDecisionState decision,
            int slot)
        {
            if (slot < 0 || slot >= MNG_MatchSnapshot.PlayersPerTeam) return false;
            var player = snapshot.GetPlayer(team, slot);
            return player.Active && !player.IsHuman && decision.ControlMask[slot]
                && player.Role != MNG_PlayerRole.Keeper;
        }

        static void ApplyTargetSeparation(
            MNG_MatchSnapshot snapshot, Team team,
            MNG_PlayerTask[] tasks)
        {
            for (var first = 1; first < tasks.Length; first++)
            for (var second = first + 1; second < tasks.Length; second++)
            {
                if (!CanSeparate(tasks[first].Skill) || !CanSeparate(tasks[second].Skill)) continue;
                var delta = tasks[second].Target - tasks[first].Target;
                var distance = delta.magnitude;
                if (distance >= MinimumFormationTargetSeparation) continue;
                var direction = distance > 0.001f
                    ? delta / distance
                    : new Vector2(0f, (team == Team.Red ? 1f : -1f) * (RoleLane((MNG_PlayerRole)second) >= RoleLane((MNG_PlayerRole)first) ? 1f : -1f));
                var correction = direction * ((MinimumFormationTargetSeparation - distance) * 0.5f);
                var firstTask = tasks[first];
                var secondTask = tasks[second];
                if (firstTask.Skill == MNG_PlayerSkill.ReceivePass)
                    secondTask.Target = Clamp(secondTask.Target + correction * 2f, snapshot);
                else if (secondTask.Skill == MNG_PlayerSkill.ReceivePass)
                    firstTask.Target = Clamp(firstTask.Target - correction * 2f, snapshot);
                else
                {
                    firstTask.Target = Clamp(firstTask.Target - correction, snapshot);
                    secondTask.Target = Clamp(secondTask.Target + correction, snapshot);
                }
                tasks[first] = firstTask;
                tasks[second] = secondTask;
            }
        }

        static bool CanSeparate(MNG_PlayerSkill skill)
            => skill == MNG_PlayerSkill.MoveTo
                || skill == MNG_PlayerSkill.Cover
                || skill == MNG_PlayerSkill.ReceivePass
                || skill == MNG_PlayerSkill.Mark
                || skill == MNG_PlayerSkill.SupportRun;

        static int FindClosestControllable(
            MNG_MatchSnapshot snapshot,
            Team team,
            MNG_TeamDecisionState decision,
            Vector2 target,
            bool includeKeeper)
        {
            var bestSlot = -1;
            var bestDistance = float.PositiveInfinity;
            for (var slot = 0; slot < MNG_MatchSnapshot.PlayersPerTeam; slot++)
            {
                var player = snapshot.GetPlayer(team, slot);
                if (!player.Active || player.IsHuman || !decision.ControlMask[slot]
                    || (!includeKeeper && player.Role == MNG_PlayerRole.Keeper))
                    continue;
                var distance = (player.Position - target).sqrMagnitude;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                bestSlot = slot;
            }
            return bestSlot;
        }

        static float Lane(int slot) => slot == 1 ? -1f : slot == 2 ? 1f : slot == 3 ? 0.35f : 0f;

        static float RoleLane(MNG_PlayerRole role) => role switch
        {
            MNG_PlayerRole.MidLeft => -16f,
            MNG_PlayerRole.MidRight => 16f,
            MNG_PlayerRole.Striker => 3f,
            _ => 0f
        };

        static Vector2 Clamp(Vector2 target, MNG_MatchSnapshot snapshot)
        {
            target.x = Mathf.Clamp(target.x, -snapshot.FieldHalfLength + 0.55f, snapshot.FieldHalfLength - 0.55f);
            target.y = Mathf.Clamp(target.y, -snapshot.FieldHalfWidth + 0.55f, snapshot.FieldHalfWidth - 0.55f);
            return target;
        }

        static MNG_PlayerTask CreateTask(
            MNG_PlayerSkill skill,
            Vector2 target,
            int receiver,
            long revision,
            float expiry)
        {
            return new MNG_PlayerTask
            {
                Skill = skill,
                Target = target,
                ReceiverSlot = receiver,
                Revision = revision,
                ExpirySeconds = expiry
            };
        }
    }
}

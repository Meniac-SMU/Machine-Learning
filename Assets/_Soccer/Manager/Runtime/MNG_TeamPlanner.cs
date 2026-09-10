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
        KeeperHome = 8
    }

    public struct MNG_PlayerTask
    {
        public MNG_PlayerSkill Skill;
        public Vector2 Target;
        public int ReceiverSlot;
        public long Revision;
        public float ExpirySeconds;
    }

    public static class MNG_TeamPlanner
    {
        public const int PlannerVersion = 2;
        public const float PrimaryPresserHoldSeconds = 1.25f;
        public const float PresserSwitchAdvantageMeters = 3f;
        public const float MinimumFormationTargetSeparation = 3f;

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
            var chaser = defending && command != MNG_Command.ProtectBack
                ? ResolvePrimaryPresser(snapshot, team, decision, ball)
                : ClearPrimaryPresser(decision);

            for (var slot = 0; slot < destination.Length; slot++)
            {
                var player = snapshot.GetPlayer(team, slot);
                if (!player.Active || player.IsHuman || !decision.ControlMask[slot])
                {
                    destination[slot] = CreateTask(MNG_PlayerSkill.None, player.Position, -1, revision, expirySeconds);
                    continue;
                }

                if (player.Role == MNG_PlayerRole.Keeper && slot != carrier)
                {
                    var keeperTarget = new Vector2(-sign * (snapshot.FieldHalfLength - 4f),
                        Mathf.Clamp(ball.y * 0.35f, -28f, 28f));
                    destination[slot] = CreateTask(MNG_PlayerSkill.KeeperHome, keeperTarget, -1, revision, expirySeconds);
                    continue;
                }

                destination[slot] = PlanFieldPlayer(
                    snapshot, team, command, decision, slot, carrier, chaser, sign, revision, expirySeconds);
            }
            ApplyTargetSeparation(snapshot, destination);
        }

        static MNG_PlayerTask PlanFieldPlayer(
            MNG_MatchSnapshot snapshot,
            Team team,
            MNG_Command command,
            MNG_TeamDecisionState decision,
            int slot,
            int carrier,
            int chaser,
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
                        var receiverTarget = decision.PendingPassReceiverSlot >= 0
                            ? snapshot.GetPlayer(team, decision.PendingPassReceiverSlot).Position
                            : ball;
                        return CreateTask(MNG_PlayerSkill.AimPass, receiverTarget,
                            decision.PendingPassReceiverSlot, revision, expirySeconds);
                    case MNG_Command.AttemptShot:
                        return CreateTask(MNG_PlayerSkill.AimShot,
                            new Vector2(sign * snapshot.FieldHalfLength, 0f), -1, revision, expirySeconds);
                    default:
                        var retreat = command == MNG_Command.ProtectBack ? -6f : 10f;
                        return CreateTask(MNG_PlayerSkill.Carry,
                            Clamp(new Vector2(ball.x + sign * retreat, ball.y), snapshot), -1, revision, expirySeconds);
                }
            }

            if (snapshot.Possession == MNG_Possession.Neutral
                || snapshot.Possession == (team == Team.Red ? MNG_Possession.Navy : MNG_Possession.Red))
            {
                if (slot == chaser && command != MNG_Command.ProtectBack)
                    return CreateTask(MNG_PlayerSkill.Press, ball, -1, revision, expirySeconds);

                var defensiveTarget = DefensiveTarget(snapshot, team, slot, command);
                var skill = snapshot.GetPlayer(team, slot).Role == MNG_PlayerRole.Striker
                    && command != MNG_Command.ProtectBack
                    ? MNG_PlayerSkill.MoveTo
                    : MNG_PlayerSkill.Cover;
                return CreateTask(skill, defensiveTarget, -1, revision, expirySeconds);
            }

            if (command == MNG_Command.PassBuild && slot == decision.PendingPassReceiverSlot)
                return CreateTask(MNG_PlayerSkill.ReceivePass,
                    Clamp(new Vector2(ball.x + sign * 10f, ball.y + Lane(slot) * 5f), snapshot),
                    -1, revision, expirySeconds);

            return CreateTask(MNG_PlayerSkill.MoveTo,
                SupportTarget(snapshot, team, slot, command),
                -1, revision, expirySeconds);
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

            var lane = RoleLane(role);
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
                snapshot.BallPosition.y + RoleLane(role) * laneScale);
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
            var y = RoleLane(role) + snapshot.BallPosition.y * yFollow;
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
                if (snapshot.EpisodeElapsedSeconds < decision.PresserHoldUntilSeconds
                    || currentDistance <= bestDistance + PresserSwitchAdvantageMeters)
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

        static void ApplyTargetSeparation(
            MNG_MatchSnapshot snapshot,
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
                    : new Vector2(0f, RoleLane((MNG_PlayerRole)second) >= RoleLane((MNG_PlayerRole)first) ? 1f : -1f);
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
                || skill == MNG_PlayerSkill.ReceivePass;

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

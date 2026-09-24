using System;
using MachineLearning.Soccer;
using UnityEngine;

namespace MachineLearning.Soccer.Manager
{
    public static partial class MNG_TeamPlanner
    {
        public const int PlannerV2Version = 12;

        public static void PlanV2(MNG_MatchSnapshot snapshot, Team team, MNG_Command command,
            MNG_TeamDecisionState decision, long revision, float expirySeconds, MNG_PlayerTask[] destination)
        {
            if (destination == null || destination.Length != 4) throw new ArgumentException("Four tasks required.");
            snapshot.ValidateOrThrow(); decision.ValidateOrThrow();
            if ((int)command < 0 || (int)command >= 6) throw new ArgumentOutOfRangeException(nameof(command));
            var carrier = snapshot.Carrier.IsValid && snapshot.Carrier.Team == team ? snapshot.Carrier.Slot : -1;
            if (carrier >= 0)
            {
                PlanAttackV2(snapshot, team, command, decision, carrier, revision, expirySeconds, destination);
                return;
            }
            var requested = command == MNG_Command.ActiveRecover ? 2 : command == MNG_Command.Balanced ? 1 : 0;
            var invalidAttack = command == MNG_Command.AdvanceCarry || command == MNG_Command.PassBuild || command == MNG_Command.AttemptShot;
            var primary = requested > 0 ? ResolvePrimaryPresser(snapshot, team, decision, snapshot.BallPosition) : ClearPrimaryPresser(decision);
            var secondary = requested > 1 ? ResolveSecondaryPresser(snapshot, team, decision, snapshot.BallPosition, primary) : -1;
            var sign = team == Team.Red ? 1f : -1f;
            for (var slot = 0; slot < 4; slot++)
            {
                var player = snapshot.GetPlayer(team, slot);
                var skill = MNG_PlayerSkill.None;
                var target = player.Position;
                if (player.Active && !player.IsHuman && decision.ControlMask[slot]
                    && (!invalidAttack || player.Role == MNG_PlayerRole.Keeper))
                {
                    if (player.Role == MNG_PlayerRole.Keeper)
                    {
                        destination[slot] = PlanKeeper(snapshot, team, slot, sign, revision, expirySeconds);
                        destination[slot].Source = MNG_ActionSource.KeeperTechnique;
                        continue;
                    }
                    skill = slot == primary || slot == secondary ? MNG_PlayerSkill.Press : MNG_PlayerSkill.Cover;
                    target = slot == primary ? PredictedInterceptTarget(snapshot, slot)
                        : slot == secondary ? SecondaryPressureTarget(snapshot, team, slot)
                        : DefensiveTarget(snapshot, team, slot, command);
                }
                destination[slot] = CreateTask(skill, Clamp(target, snapshot), -1, revision, expirySeconds);
                destination[slot].StateChanged = invalidAttack;
            }
            ApplyTargetSeparation(snapshot, team, destination);
        }

        static void PlanAttackV2(MNG_MatchSnapshot snapshot, Team team, MNG_Command command,
            MNG_TeamDecisionState decision, int carrier, long revision, float expiry, MNG_PlayerTask[] tasks)
        {
            var sign = team == Team.Red ? 1f : -1f;
            var keeperCarrier = snapshot.GetPlayer(team, carrier).Role == MNG_PlayerRole.Keeper;
            var receiver = command == MNG_Command.PassBuild && decision.HasPassTarget
                ? decision.PendingPassReceiverSlot : -1;
            if (!MNG_TacticalTargetResolver.IsPassBuildReceiverValid(snapshot, team, carrier, receiver)) receiver = -1;
            var support = receiver >= 0 ? receiver : SelectAttackingSupport(snapshot, team, decision, carrier);
            for (var slot = 0; slot < 4; slot++)
            {
                var player = snapshot.GetPlayer(team, slot);
                var skill = MNG_PlayerSkill.None;
                var target = player.Position;
                var taskReceiver = -1;
                var taskExpiry = expiry;
                if (player.Active && !player.IsHuman && decision.ControlMask[slot])
                {
                    if (slot == carrier)
                    {
                        if (command == MNG_Command.PassBuild)
                        {
                            if (receiver >= 0)
                            { skill = MNG_PlayerSkill.AimPass; target = SelectPassTarget(snapshot, team, receiver); taskReceiver = receiver; }
                        }
                        else if (command == MNG_Command.AttemptShot)
                        {
                            if (decision.HasShotTarget || keeperCarrier)
                            { skill = MNG_PlayerSkill.AimShot; target = SelectShotTarget(snapshot, team, slot); }
                        }
                        else
                        {
                            skill = MNG_PlayerSkill.Carry;
                            // Reuse established 10m/6m progress scales. Protection is a short lateral hold.
                            var forward = keeperCarrier || command == MNG_Command.ProtectBack ? 0f
                                : command == MNG_Command.Balanced ? MinimumCarrierAttackProgress : NormalCarrierAttackProgress;
                            target = snapshot.BallPosition + new Vector2(sign * forward,
                                forward == 0 ? (Mathf.Abs(snapshot.BallPosition.y) < 0.0001f ? sign : snapshot.BallPosition.y > 0 ? -1f : 1f) * BallHoldLateralDistance : 0);
                        }
                        if (skill == MNG_PlayerSkill.Carry && command != MNG_Command.ProtectBack && !keeperCarrier)
                            target = EnforceAttackingCarryTarget(snapshot.BallPosition, target, team, snapshot.FieldHalfLength, snapshot.FieldHalfWidth);
                        if (MNG_TaskLifetime.IsKick(skill)) taskExpiry = snapshot.EpisodeElapsedSeconds + KickExecutionSeconds;
                    }
                    else if (player.Role == MNG_PlayerRole.Keeper)
                    {
                        tasks[slot] = PlanKeeper(snapshot, team, slot, sign, revision, expiry);
                        tasks[slot].Source = MNG_ActionSource.KeeperTechnique;
                        continue;
                    }
                    else if (slot == receiver)
                    { skill = MNG_PlayerSkill.ReceivePass; target = SelectPassTarget(snapshot, team, slot); taskExpiry = snapshot.EpisodeElapsedSeconds + KickExecutionSeconds; }
                    else if (command == MNG_Command.ActiveRecover || (slot == support && command != MNG_Command.ProtectBack))
                    {
                        skill = MNG_PlayerSkill.SupportRun;
                        var lateral = slot == 1 ? -MinimumFormationTargetSeparation : MinimumFormationTargetSeparation;
                        target = command == MNG_Command.AttemptShot ? AttackingSupportTarget(snapshot, team, slot)
                            : snapshot.BallPosition + new Vector2(sign * (command == MNG_Command.Balanced ? 0f : NormalCarrierAttackProgress), sign * lateral);
                    }
                    else
                    {
                        skill = MNG_PlayerSkill.Cover;
                        target = snapshot.BallPosition + new Vector2(-sign * MidfielderRecoveryDepth,
                            sign * (slot == 1 ? -MinimumFormationTargetSeparation : MinimumFormationTargetSeparation));
                    }
                }
                tasks[slot] = CreateTask(skill, Clamp(target, snapshot), taskReceiver, revision, taskExpiry);
                tasks[slot].StateChanged = slot == carrier && skill == MNG_PlayerSkill.None && player.Active;
            }
            ApplyTargetSeparation(snapshot, team, tasks);
        }

        public const float BallHoldLateralDistance = 3f;
    }
}

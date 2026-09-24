using System;
using MachineLearning.Soccer;
using UnityEngine;

namespace MachineLearning.Soccer.Manager
{
    /// <summary>
    /// Policy contract v1: exactly 133 scalar observations in the documented order.
    /// The caller owns and reuses the destination buffer.
    /// </summary>
    public static class MNG_ObservationWriter
    {
        public const int ObservationSize = 133;
        const float PlayerSpeedScale = 9f;
        public const float BallSpeedScale = MNG_KickSolver.MaximumBallSpeed;
        const float MatchDurationSeconds = 300f;

        public static int WriteV2(MNG_MatchSnapshot snapshot, Team perspective,
            MNG_TeamDecisionState decision, float[] destination, float[] legacyScratch)
        {
            if (destination == null || destination.Length != MNG_RuntimeV2.ObservationSize)
                throw new ArgumentException("V2 observations require exactly 244 floats.", nameof(destination));
            Write(snapshot, perspective, decision, legacyScratch);
            Array.Copy(legacyScratch, destination, ObservationSize);
            var index = ObservationSize;
            for (var slot = 0; slot < MNG_MatchSnapshot.PlayersPerTeam; slot++)
            {
                var player = snapshot.GetPlayer(perspective, slot);
                if (!player.Active)
                {
                    for (var i = 0; i < 27; i++) destination[index++] = 0f;
                    continue;
                }
                WriteOneHot((int)player.ExecutingSkill, 13, destination, ref index);
                WriteOneHot((int)player.ExecutingPhase, 5, destination, ref index);
                destination[index++] = Mathf.Clamp01(player.TaskRemainingSeconds / MNG_RuntimeV2.TaskTimeScale);
                destination[index++] = Mathf.Clamp01(player.CommitmentRemainingSeconds / MNG_RuntimeV2.CommitmentWatchdogSeconds);
                WriteOneHot(player.ExecutingReceiverIndex, 5, destination, ref index);
                WritePlanar(player.ExecutingTarget, perspective, snapshot.FieldHalfLength,
                    snapshot.FieldHalfWidth, destination, ref index);
            }
            destination[index++] = snapshot.BallStallRecoveryActive ? 1f : 0f;
            destination[index++] = Mathf.Clamp01(snapshot.BallStationarySeconds / MNG_RuntimeV2.StallTimeScale);
            destination[index++] = snapshot.GoalPauseActive ? 1f : 0f;
            if (index != MNG_RuntimeV2.ObservationSize) throw new InvalidOperationException("V2 observation count mismatch.");
            for (var i = 0; i < index; i++)
                if (!MNG_MatchSnapshot.IsFinite(destination[i])) throw new InvalidOperationException("Nonfinite v2 observation.");
            return index;
        }

        public static int Write(
            MNG_MatchSnapshot snapshot,
            Team perspective,
            MNG_TeamDecisionState decision,
            float[] destination)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (decision == null) throw new ArgumentNullException(nameof(decision));
            if (destination == null || destination.Length != ObservationSize)
                throw new ArgumentException($"Observation buffer must contain exactly {ObservationSize} floats.", nameof(destination));

            snapshot.ValidateOrThrow();
            decision.ValidateOrThrow();

            var index = 0;
            var opponent = perspective == Team.Red ? Team.Navy : Team.Red;
            for (var slot = 0; slot < MNG_MatchSnapshot.PlayersPerTeam; slot++)
                WritePlayer(snapshot.GetPlayer(perspective, slot), perspective, snapshot, destination, ref index);
            for (var slot = 0; slot < MNG_MatchSnapshot.PlayersPerTeam; slot++)
                WritePlayer(snapshot.GetPlayer(opponent, slot), perspective, snapshot, destination, ref index);

            WritePlanar(snapshot.BallPosition, perspective, snapshot.FieldHalfLength, snapshot.FieldHalfWidth,
                destination, ref index);
            WriteVelocity(snapshot.BallVelocity, perspective, BallSpeedScale, destination, ref index);

            var ownPossession = perspective == Team.Red ? MNG_Possession.Red : MNG_Possession.Navy;
            var opponentPossession = perspective == Team.Red ? MNG_Possession.Navy : MNG_Possession.Red;
            destination[index++] = snapshot.Possession == MNG_Possession.Neutral ? 1f : 0f;
            destination[index++] = snapshot.Possession == ownPossession ? 1f : 0f;
            destination[index++] = snapshot.Possession == opponentPossession ? 1f : 0f;

            var carrierIndex = 0;
            if (snapshot.Carrier.IsValid)
            {
                carrierIndex = snapshot.Carrier.Team == perspective
                    ? 1 + snapshot.Carrier.Slot
                    : 5 + snapshot.Carrier.Slot;
            }
            WriteOneHot(carrierIndex, 9, destination, ref index);

            destination[index++] = Mathf.Clamp01(snapshot.EpisodeElapsedSeconds / snapshot.EpisodeDurationSeconds);
            destination[index++] = Mathf.Clamp01(snapshot.MatchRemainingSeconds / MatchDurationSeconds);
            var scoreDifference = perspective == Team.Red
                ? snapshot.RedScore - snapshot.NavyScore
                : snapshot.NavyScore - snapshot.RedScore;
            destination[index++] = Mathf.Clamp(scoreDifference / 5f, -1f, 1f);
            destination[index++] = HasHuman(snapshot, perspective) ? 1f : 0f;

            WriteOneHot((int)decision.PreviousCommand, MNG_CommandMask.CommandCount, destination, ref index);
            destination[index++] = Mathf.Clamp01(decision.CommandAgeSeconds / 2f);
            WriteOneHot(decision.PendingPassReceiverSlot + 1, 5, destination, ref index);
            destination[index++] = decision.SecondsSincePossessionLoss < 0f
                ? 1f
                : Mathf.Clamp01(decision.SecondsSincePossessionLoss / 10f);

            for (var slot = 0; slot < MNG_MatchSnapshot.PlayersPerTeam; slot++)
                destination[index++] = decision.ControlMask[slot] ? 1f : 0f;

            if (index != ObservationSize)
                throw new InvalidOperationException($"Observation contract wrote {index} values instead of {ObservationSize}.");
            return index;
        }

        static void WritePlayer(
            MNG_PlayerState player,
            Team perspective,
            MNG_MatchSnapshot snapshot,
            float[] destination,
            ref int index)
        {
            if (!player.Active)
            {
                for (var i = 0; i < 12; i++) destination[index++] = 0f;
                return;
            }

            destination[index++] = 1f;
            WritePlanar(player.Position, perspective, snapshot.FieldHalfLength, snapshot.FieldHalfWidth,
                destination, ref index);
            WriteVelocity(player.Velocity, perspective, PlayerSpeedScale, destination, ref index);
            WriteHeading(player.Forward, perspective, destination, ref index);
            destination[index++] = player.Role == MNG_PlayerRole.Keeper ? 1f : 0f;
            destination[index++] = player.Role == MNG_PlayerRole.MidLeft || player.Role == MNG_PlayerRole.MidRight ? 1f : 0f;
            destination[index++] = player.Role == MNG_PlayerRole.Striker ? 1f : 0f;
            destination[index++] = player.IsHuman ? 1f : 0f;
            destination[index++] = player.MaximumKickCooldownSeconds <= 0f
                ? 0f
                : Mathf.Clamp01(player.KickCooldownSeconds / player.MaximumKickCooldownSeconds);
        }

        static void WritePlanar(
            Vector2 value,
            Team perspective,
            float halfLength,
            float halfWidth,
            float[] destination,
            ref int index)
        {
            var sign = perspective == Team.Red ? 1f : -1f;
            destination[index++] = Mathf.Clamp(sign * value.x / halfLength, -1.2f, 1.2f);
            destination[index++] = Mathf.Clamp(sign * value.y / halfWidth, -1.2f, 1.2f);
        }

        static void WriteVelocity(
            Vector2 value,
            Team perspective,
            float scale,
            float[] destination,
            ref int index)
        {
            var sign = perspective == Team.Red ? 1f : -1f;
            destination[index++] = Mathf.Clamp(sign * value.x / scale, -1f, 1f);
            destination[index++] = Mathf.Clamp(sign * value.y / scale, -1f, 1f);
        }

        static void WriteHeading(Vector2 value, Team perspective, float[] destination, ref int index)
        {
            var sign = perspective == Team.Red ? 1f : -1f;
            var magnitude = value.magnitude;
            if (magnitude <= 0.0001f)
            {
                destination[index++] = 0f;
                destination[index++] = 0f;
                return;
            }

            destination[index++] = sign * value.x / magnitude;
            destination[index++] = sign * value.y / magnitude;
        }

        static bool HasHuman(MNG_MatchSnapshot snapshot, Team perspective)
        {
            for (var slot = 0; slot < MNG_MatchSnapshot.PlayersPerTeam; slot++)
            {
                var player = snapshot.GetPlayer(perspective, slot);
                if (player.Active && player.IsHuman) return true;
            }
            return false;
        }

        static void WriteOneHot(int selected, int count, float[] destination, ref int index)
        {
            for (var i = 0; i < count; i++) destination[index++] = i == selected ? 1f : 0f;
        }
    }
}

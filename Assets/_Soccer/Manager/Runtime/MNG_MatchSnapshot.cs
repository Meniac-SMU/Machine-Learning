using System;
using MachineLearning.Soccer;
using UnityEngine;

namespace MachineLearning.Soccer.Manager
{
    public enum MNG_PlayerRole
    {
        Keeper = 0,
        MidLeft = 1,
        MidRight = 2,
        Striker = 3
    }

    public enum MNG_Possession
    {
        Neutral = 0,
        Red = 1,
        Navy = 2
    }

    [Serializable]
    public struct MNG_PlayerState
    {
        public bool Active;
        public Vector2 Position;
        public Vector2 Velocity;
        public Vector2 Forward;
        public MNG_PlayerRole Role;
        public bool IsHuman;
        public float KickCooldownSeconds;
        public float MaximumKickCooldownSeconds;
        public MNG_PlayerSkill ExecutingSkill;
        public MNG_TaskPhase ExecutingPhase;
        public float TaskRemainingSeconds;
        public float CommitmentRemainingSeconds;
        // Zero is None, 1..4 are own-team slots 0..3.
        public int ExecutingReceiverIndex;
        public Vector2 ExecutingTarget;
    }

    [Serializable]
    public struct MNG_CarrierRef
    {
        public bool IsValid;
        public Team Team;
        public int Slot;

        public static MNG_CarrierRef None => new MNG_CarrierRef { IsValid = false, Slot = -1 };

        public static MNG_CarrierRef For(Team team, int slot)
        {
            if (slot < 0 || slot >= MNG_MatchSnapshot.PlayersPerTeam)
                throw new ArgumentOutOfRangeException(nameof(slot));
            return new MNG_CarrierRef { IsValid = true, Team = team, Slot = slot };
        }
    }

    /// <summary>
    /// Immutable-by-convention state captured once per physics tick. Runtime owners
    /// fill the reusable instance before either team writes observations.
    /// </summary>
    public sealed class MNG_MatchSnapshot
    {
        public const int PlayersPerTeam = 4;
        public const int TeamCount = 2;

        readonly MNG_PlayerState[] m_Players = new MNG_PlayerState[PlayersPerTeam * TeamCount];

        public long TickId { get; set; }
        public long EpisodeId { get; set; }
        public Vector2 BallPosition { get; set; }
        public Vector2 BallVelocity { get; set; }
        public bool BallStallRecoveryActive { get; set; }
        public bool GoalPauseActive { get; set; }
        public int BallStallRecoverySequence { get; set; }
        public float BallStationarySeconds { get; set; }
        public MNG_Possession Possession { get; set; }
        public MNG_CarrierRef Carrier { get; set; } = MNG_CarrierRef.None;
        public float EpisodeElapsedSeconds { get; set; }
        public float EpisodeDurationSeconds { get; set; } = 300f;
        public float MatchRemainingSeconds { get; set; } = 300f;
        public int RedScore { get; set; }
        public int NavyScore { get; set; }
        public float FieldHalfLength { get; set; } = SoccerArenaGeometry.StadiumHalfLength;
        public float FieldHalfWidth { get; set; } = SoccerArenaGeometry.StadiumHalfWidth;
        public float GoalHalfWidth { get; set; } = SoccerArenaGeometry.StadiumGoalHalfWidth;

        public MNG_PlayerState GetPlayer(Team team, int slot)
        {
            ValidateSlot(slot);
            return m_Players[GetIndex(team, slot)];
        }

        public void SetPlayer(Team team, int slot, MNG_PlayerState state)
        {
            ValidateSlot(slot);
            m_Players[GetIndex(team, slot)] = state;
        }

        public void ValidateOrThrow()
        {
            if (TickId < 0) throw new InvalidOperationException("Snapshot tick cannot be negative.");
            if (EpisodeId < 0) throw new InvalidOperationException("Snapshot episode cannot be negative.");
            RequireFinitePositive(FieldHalfLength, nameof(FieldHalfLength));
            RequireFinitePositive(FieldHalfWidth, nameof(FieldHalfWidth));
            RequireFinitePositive(GoalHalfWidth, nameof(GoalHalfWidth));
            RequireFinite(EpisodeElapsedSeconds, nameof(EpisodeElapsedSeconds));
            RequireFinitePositive(EpisodeDurationSeconds, nameof(EpisodeDurationSeconds));
            RequireFinite(MatchRemainingSeconds, nameof(MatchRemainingSeconds));
            RequireFinite(BallPosition, nameof(BallPosition));
            RequireFinite(BallVelocity, nameof(BallVelocity));
            RequireFinite(BallStationarySeconds, nameof(BallStationarySeconds));
            if (BallStationarySeconds < 0f)
                throw new InvalidOperationException("Ball stationary time cannot be negative.");
            if (BallStallRecoverySequence < 0)
                throw new InvalidOperationException("Ball stall recovery sequence cannot be negative.");

            if (Carrier.IsValid && (Carrier.Slot < 0 || Carrier.Slot >= PlayersPerTeam))
                throw new InvalidOperationException("Carrier slot is outside the fixed four-player roster.");

            for (var i = 0; i < m_Players.Length; i++)
            {
                var player = m_Players[i];
                RequireFinite(player.Position, $"Player[{i}].Position");
                RequireFinite(player.Velocity, $"Player[{i}].Velocity");
                RequireFinite(player.Forward, $"Player[{i}].Forward");
                RequireFinite(player.KickCooldownSeconds, $"Player[{i}].KickCooldownSeconds");
                RequireFinite(player.MaximumKickCooldownSeconds, $"Player[{i}].MaximumKickCooldownSeconds");
                RequireFinite(player.TaskRemainingSeconds, $"Player[{i}].TaskRemainingSeconds");
                RequireFinite(player.CommitmentRemainingSeconds, $"Player[{i}].CommitmentRemainingSeconds");
                RequireFinite(player.ExecutingTarget, $"Player[{i}].ExecutingTarget");
                if ((int)player.ExecutingSkill < 0 || (int)player.ExecutingSkill > 12
                    || (int)player.ExecutingPhase < 0 || (int)player.ExecutingPhase > 4
                    || player.ExecutingReceiverIndex < 0 || player.ExecutingReceiverIndex > 4
                    || player.TaskRemainingSeconds < 0 || player.CommitmentRemainingSeconds < 0)
                    throw new InvalidOperationException("Invalid execution observation state.");
                if (player.KickCooldownSeconds < 0f || player.MaximumKickCooldownSeconds < 0f)
                    throw new InvalidOperationException($"Player[{i}] has a negative kick cooldown.");
            }
        }

        static int GetIndex(Team team, int slot) => (team == Team.Red ? 0 : PlayersPerTeam) + slot;

        static void ValidateSlot(int slot)
        {
            if (slot < 0 || slot >= PlayersPerTeam) throw new ArgumentOutOfRangeException(nameof(slot));
        }

        internal static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        static void RequireFinite(float value, string name)
        {
            if (!IsFinite(value)) throw new InvalidOperationException($"{name} must be finite.");
        }

        static void RequireFinitePositive(float value, string name)
        {
            RequireFinite(value, name);
            if (value <= 0f) throw new InvalidOperationException($"{name} must be positive.");
        }

        static void RequireFinite(Vector2 value, string name)
        {
            RequireFinite(value.x, name + ".x");
            RequireFinite(value.y, name + ".y");
        }
    }

    /// <summary>
    /// Per-team state that belongs to the previous accepted decision rather than
    /// the simultaneous world snapshot.
    /// </summary>
    public sealed class MNG_TeamDecisionState
    {
        public MNG_Command PreviousCommand { get; set; } = MNG_Command.Balanced;
        public float CommandAgeSeconds { get; set; }
        public int PendingPassReceiverSlot { get; set; } = -1;
        public float SecondsSincePossessionLoss { get; set; } = -1f;
        public bool HasPassTarget { get; set; }
        public bool HasShotTarget { get; set; }
        public int PrimaryPresserSlot { get; set; } = -1;
        public float PresserHoldUntilSeconds { get; set; }
        public bool[] ControlMask { get; } = { true, true, true, true };

        public void ValidateOrThrow()
        {
            if ((int)PreviousCommand < 0 || (int)PreviousCommand >= MNG_CommandMask.CommandCount)
                throw new InvalidOperationException("Previous command is outside policy contract v1.");
            if (!MNG_MatchSnapshot.IsFinite(CommandAgeSeconds) || CommandAgeSeconds < 0f)
                throw new InvalidOperationException("Command age must be finite and non-negative.");
            if (PendingPassReceiverSlot < -1 || PendingPassReceiverSlot >= MNG_MatchSnapshot.PlayersPerTeam)
                throw new InvalidOperationException("Pending pass receiver must be none or an own-team slot.");
            if (!MNG_MatchSnapshot.IsFinite(SecondsSincePossessionLoss))
                throw new InvalidOperationException("Possession-loss age must be finite.");
            if (SecondsSincePossessionLoss < 0f && SecondsSincePossessionLoss != -1f)
                throw new InvalidOperationException("Possession-loss age uses -1 only for no recorded loss.");
            if (ControlMask == null || ControlMask.Length != MNG_MatchSnapshot.PlayersPerTeam)
                throw new InvalidOperationException("Control mask must contain exactly four slots.");
            if (PrimaryPresserSlot < -1 || PrimaryPresserSlot >= MNG_MatchSnapshot.PlayersPerTeam)
                throw new InvalidOperationException("Primary presser must be none or an own-team slot.");
            if (!MNG_MatchSnapshot.IsFinite(PresserHoldUntilSeconds) || PresserHoldUntilSeconds < 0f)
                throw new InvalidOperationException("Presser hold time must be finite and non-negative.");
        }
    }
}

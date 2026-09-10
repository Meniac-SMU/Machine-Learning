using System;
using MachineLearning.Soccer;

namespace MachineLearning.Soccer.Manager
{
    /// <summary>
    /// The only discrete action branch exposed by an MNG manager policy.
    /// Numeric values are part of policy contract v1.
    /// </summary>
    public enum MNG_Command
    {
        AdvanceCarry = 0,
        PassBuild = 1,
        AttemptShot = 2,
        ActiveRecover = 3,
        Balanced = 4,
        ProtectBack = 5
    }

    public static class MNG_CommandMask
    {
        public const int CommandCount = 6;

        public static void Write(
            MNG_MatchSnapshot snapshot,
            Team perspective,
            MNG_TeamDecisionState decision,
            bool[] destination)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (decision == null) throw new ArgumentNullException(nameof(decision));
            if (destination == null || destination.Length != CommandCount)
                throw new ArgumentException($"Command mask must contain exactly {CommandCount} entries.", nameof(destination));

            snapshot.ValidateOrThrow();
            decision.ValidateOrThrow();

            var hasAiCarrier = false;
            var carrierKickReady = false;
            if (snapshot.Carrier.IsValid && snapshot.Carrier.Team == perspective)
            {
                var slot = snapshot.Carrier.Slot;
                var carrier = snapshot.GetPlayer(perspective, slot);
                hasAiCarrier = carrier.Active && !carrier.IsHuman && decision.ControlMask[slot];
                carrierKickReady = hasAiCarrier && carrier.KickCooldownSeconds <= 0f;
            }

            var hasControllablePlayer = false;
            for (var slot = 0; slot < MNG_MatchSnapshot.PlayersPerTeam; slot++)
            {
                if (decision.ControlMask[slot] && snapshot.GetPlayer(perspective, slot).Active)
                {
                    hasControllablePlayer = true;
                    break;
                }
            }

            destination[(int)MNG_Command.AdvanceCarry] = hasAiCarrier;
            destination[(int)MNG_Command.PassBuild] = carrierKickReady && decision.HasPassTarget;
            destination[(int)MNG_Command.AttemptShot] = carrierKickReady && decision.HasShotTarget;
            destination[(int)MNG_Command.ActiveRecover] = hasControllablePlayer;
            destination[(int)MNG_Command.Balanced] = true;
            destination[(int)MNG_Command.ProtectBack] = true;
        }
    }
}

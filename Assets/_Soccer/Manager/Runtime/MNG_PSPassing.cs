using System;
using MachineLearning.Soccer;
using UnityEngine;

namespace MachineLearning.Soccer.Manager
{
    // Opt-in experiment: branch 0 is the unchanged six commands; branch 1 is
    // legacy target (0) or an explicitly selected teammate slot plus one (1..4).
    // No pressure, lane risk, direction, or goal-distance filter is applied.
    public static class MNG_PSPassing
    {
        public static bool Enabled => Array.IndexOf(Environment.GetCommandLineArgs(), "-mngPSRecipients") >= 0;
        public static bool ContactPassEnabled => Array.IndexOf(Environment.GetCommandLineArgs(), "-mngPSContactPass") >= 0;
        // Controlled ground pass. Strong shots and legacy/common-rule kicks keep
        // their original strength; this selected-pass skill aims to be receivable.
        public static float ContactExitSpeed(float distance, float linearDamping)
            => Mathf.Clamp(Mathf.Max(distance / 1.5f, 8f + Mathf.Max(0f,linearDamping)*distance),
                10f, MNG_KickSolver.MaximumBallSpeed);

        public static Vector2 Target(MNG_MatchSnapshot s, Team team, int slot)
        {
            var p = s.GetPlayer(team, slot);
            var target = p.Position + Vector2.ClampMagnitude(p.Velocity, 9f) * .2f;
            return new Vector2(Mathf.Clamp(target.x, -s.FieldHalfLength + .55f, s.FieldHalfLength - .55f),
                Mathf.Clamp(target.y, -s.FieldHalfWidth + .55f, s.FieldHalfWidth - .55f));
        }

        public static bool Valid(MNG_MatchSnapshot s, Team team, int carrier, int receiver)
        {
            if (carrier < 0 || carrier > 3 || receiver < 0 || receiver > 3 || carrier == receiver) return false;
            var from = s.GetPlayer(team, carrier); var to = s.GetPlayer(team, receiver);
            return from.Active && to.Active && !to.IsHuman
                && (s.BallPosition - Target(s, team, receiver)).sqrMagnitude > .0001f;
        }

        public static bool Valid(MNG_MatchSnapshot s, Team team, int carrier, int receiver, bool directed)
            => directed ? Valid(s, team, carrier, receiver)
                : MNG_TacticalTargetResolver.IsPassBuildReceiverValid(s, team, carrier, receiver);
    }
}

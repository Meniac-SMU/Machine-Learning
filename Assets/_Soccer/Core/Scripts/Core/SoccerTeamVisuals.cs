using UnityEngine;

namespace MachineLearning.Soccer
{
    /// <summary>
    /// Shared visual identity for the two stable ML-Agents team ids.
    /// Team numeric values and policy behavior names remain unchanged.
    /// </summary>
    public static class SoccerTeamVisuals
    {
        public const string RedDisplayName = "RED";
        public const string NavyDisplayName = "NAVY";
        public const string RedAgentTag = "redAgent";
        public const string NavyAgentTag = "navyAgent";
        public const string RedGoalTag = "redGoal";
        public const string NavyGoalTag = "navyGoal";

        // Additional requested HSV tuning: Red saturation +10%; Navy saturation/value +15%.
        public static readonly Color RedPlayerColor = new Color32(160, 2, 24, 255);
        public static readonly Color NavyPlayerColor = new Color32(0, 39, 112, 255);
        public static readonly Color RedKickPlateColor = new Color32(79, 13, 24, 255);
        public static readonly Color NavyKickPlateColor = new Color32(8, 26, 56, 255);
        public static readonly Color RedGoalColor = new Color32(211, 63, 78, 255);
        public static readonly Color NavyGoalColor = new Color32(47, 92, 156, 255);

        public static string DisplayName(Team team)
        {
            return team == Team.Red ? RedDisplayName : NavyDisplayName;
        }

        public static string AgentTag(Team team)
        {
            return team == Team.Red ? RedAgentTag : NavyAgentTag;
        }

        public static string GoalTag(Team team)
        {
            return team == Team.Red ? RedGoalTag : NavyGoalTag;
        }
    }
}

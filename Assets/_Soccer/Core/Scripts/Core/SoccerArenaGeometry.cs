using UnityEngine;

namespace MachineLearning.Soccer
{
    /// <summary>
    /// The origin-centred, X-attacking Stadium dimensions used by every active Soccer workspace.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SoccerArenaGeometry : MonoBehaviour
    {
        public const float StadiumHalfLength = 62f;
        public const float StadiumHalfWidth = 42.32727f;
        public const float StadiumGoalHalfWidth = 10.196409f;
        public const float StadiumGoalHeight = 6.2911057f;
        public const float StadiumGoalDepth = 5.6345015f;
        public const float StadiumCornerRadius = 2.8977776f;

        [SerializeField, Min(1f)] float halfLength = StadiumHalfLength;
        [SerializeField, Min(1f)] float halfWidth = StadiumHalfWidth;
        [SerializeField, Min(0.5f)] float goalHalfWidth = StadiumGoalHalfWidth;
        [SerializeField, Min(1f)] float goalHeight = StadiumGoalHeight;
        [SerializeField, Min(1f)] float goalDepth = StadiumGoalDepth;
        [SerializeField, Min(0f)] float cornerRadius = StadiumCornerRadius;
        [SerializeField, Min(0.1f)] float goalModelWidthMultiplier = 2f;

        public const int CornerSegments = 3;

        public float HalfLength => halfLength;
        public float HalfWidth => halfWidth;
        public float GoalHalfWidth => goalHalfWidth;
        public float GoalHeight => goalHeight;
        public float GoalDepth => goalDepth;
        public float CornerRadius => cornerRadius;
        public float GoalModelWidthMultiplier => goalModelWidthMultiplier;

        public void Configure(float length, float width, float openingWidth, float height, float depth,
            float roundedCornerRadius = 0f, float modelWidthMultiplier = 1f)
        {
            halfLength = length * 0.5f;
            halfWidth = width * 0.5f;
            goalHalfWidth = openingWidth * 0.5f;
            goalHeight = height;
            goalDepth = depth;
            cornerRadius = roundedCornerRadius;
            goalModelWidthMultiplier = modelWidthMultiplier;
        }

        public Vector3 GetGoalCenter(Team defendingTeam, float y)
        {
            return new Vector3(defendingTeam == Team.Red ? -halfLength : halfLength, y, 0f);
        }

        public Vector3 ClampTarget(Vector3 target)
        {
            const float playerMargin = 0.55f;
            target.z = Mathf.Clamp(target.z, -halfWidth + playerMargin, halfWidth - playerMargin);
            var inGoalLane = Mathf.Abs(target.z) <= goalHalfWidth - playerMargin;
            var depth = halfLength + (inGoalLane ? goalDepth : 0f) - playerMargin;
            target.x = Mathf.Clamp(target.x, -depth, depth);
            target = ClampRoundedCorners(target, playerMargin);
            return target;
        }

        /// <summary>
        /// Keeps a spherical ball fully inside the pitch, including the goal
        /// interiors and the three-panel rounded corners. Physical walls remain
        /// the primary boundary; this is the Stadium's continuous-collision
        /// fallback for unusually fast contacts or direct teleports.
        /// </summary>
        public Vector3 ClampBallPosition(Vector3 position, float ballRadius)
        {
            ballRadius = Mathf.Max(0f, ballRadius);
            var lateralLimit = Mathf.Max(0f, halfWidth - ballRadius);
            position.z = Mathf.Clamp(position.z, -lateralLimit, lateralLimit);

            var goalLaneLimit = Mathf.Max(0f, goalHalfWidth - ballRadius);
            var inGoalLane = Mathf.Abs(position.z) <= goalLaneLimit;
            var endLimit = Mathf.Max(0f,
                halfLength + (inGoalLane ? goalDepth : 0f) - ballRadius);
            position.x = Mathf.Clamp(position.x, -endLimit, endLimit);
            return ClampRoundedCorners(position, ballRadius);
        }

        public bool ContainsBallPosition(Vector3 position, float ballRadius, float tolerance = 0.001f)
        {
            var clamped = ClampBallPosition(position, ballRadius);
            return new Vector2(position.x - clamped.x, position.z - clamped.z).sqrMagnitude
                <= tolerance * tolerance;
        }

        // Positive-corner points run from the end-wall tangent to the side-wall
        // tangent. Reflecting X/Z gives the same convex boundary at every corner.
        public Vector3 GetCornerPoint(int xSign, int zSign, int pointIndex, float y = 0f)
        {
            var angle = pointIndex * (Mathf.PI * 0.5f / CornerSegments);
            return new Vector3(xSign * (halfLength - cornerRadius + cornerRadius * Mathf.Cos(angle)),
                y, zSign * (halfWidth - cornerRadius + cornerRadius * Mathf.Sin(angle)));
        }

        public Vector3 GetCornerNormal(int xSign, int zSign, int segment)
        {
            var angle = (segment + 0.5f) * (Mathf.PI * 0.5f / CornerSegments);
            return new Vector3(xSign * Mathf.Cos(angle), 0f, zSign * Mathf.Sin(angle));
        }

        public bool IsInsideRoundedCorners(Vector3 position, float margin = 0f)
        {
            if (cornerRadius <= 0f) return true;
            var xSign = position.x < 0f ? -1 : 1;
            var zSign = position.z < 0f ? -1 : 1;
            for (var segment = 0; segment < CornerSegments; segment++)
            {
                var normal = GetCornerNormal(xSign, zSign, segment);
                if (Vector3.Dot(position - GetCornerPoint(xSign, zSign, segment), normal) > -margin + 0.001f)
                    return false;
            }
            return true;
        }

        Vector3 ClampRoundedCorners(Vector3 target, float margin)
        {
            if (cornerRadius <= 0f) return target;
            var xSign = target.x < 0f ? -1 : 1;
            var zSign = target.z < 0f ? -1 : 1;
            for (var segment = 0; segment < CornerSegments; segment++)
            {
                var normal = GetCornerNormal(xSign, zSign, segment);
                var excess = Vector3.Dot(target - GetCornerPoint(xSign, zSign, segment), normal) + margin;
                if (excess > 0f) target -= normal * excess;
            }
            return target;
        }

        public bool ContainsGoalInterior(Team defendingTeam, Vector3 position)
        {
            var depth = position.x * (defendingTeam == Team.Red ? -1f : 1f);
            return depth >= halfLength && depth <= halfLength + goalDepth
                && Mathf.Abs(position.z) <= goalHalfWidth
                && position.y >= 0f && position.y <= goalHeight;
        }

        public bool ContainsGoalBall(Team defendingTeam, Vector3 position)
        {
            return ContainsGoalInterior(defendingTeam, position);
        }
    }
}

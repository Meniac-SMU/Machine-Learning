using System;
using UnityEngine;

namespace MachineLearning.Soccer.Manager
{
    /// <summary>
    /// Detects a ball that remains in one small area regardless of possession or
    /// nearby contenders. The planner uses the changing sequence to force both
    /// teams to reassess their approach lanes until the ball actually escapes.
    /// </summary>
    public sealed class MNG_GlobalBallStallTracker
    {
        public const float TriggerSeconds = 2f;
        public const float KickoffGraceSeconds = 2f;
        public const float ObservationRadius = 0.55f;
        public const float ReleaseDistance = 2.25f;
        public const float ReleaseSpeed = 3f;
        public const float RetargetSeconds = 1f;

        Vector2 m_ObservationOrigin;
        Vector2 m_RecoveryOrigin;
        float m_StationarySeconds;
        float m_RetargetSeconds;
        float m_KickoffGraceRemaining;
        bool m_HasObservationOrigin;

        public bool IsActive { get; private set; }
        public int Sequence { get; private set; }
        public int ActivationCount { get; private set; }
        public int EpisodeActivationCount { get; private set; }
        public float EpisodeActiveSeconds { get; private set; }
        public float StationarySeconds => m_StationarySeconds;

        public bool Update(
            Vector2 ballPosition,
            Vector2 ballVelocity,
            float deltaTime)
        {
            if (!MNG_MatchSnapshot.IsFinite(ballPosition.x)
                || !MNG_MatchSnapshot.IsFinite(ballPosition.y)
                || !MNG_MatchSnapshot.IsFinite(ballVelocity.x)
                || !MNG_MatchSnapshot.IsFinite(ballVelocity.y)
                || !MNG_MatchSnapshot.IsFinite(deltaTime)
                || deltaTime <= 0f)
                throw new ArgumentOutOfRangeException(nameof(deltaTime));

            // Kickoff placement is intentionally stationary. Count only time
            // after the grace window; ordinary releases must not re-arm it.
            if (m_KickoffGraceRemaining > 0f)
            {
                var ignoredSeconds = Mathf.Min(deltaTime, m_KickoffGraceRemaining);
                m_KickoffGraceRemaining -= ignoredSeconds;
                deltaTime -= ignoredSeconds;
                ClearTracking(ballPosition, true);
                if (deltaTime <= 0f) return false;
            }

            if (IsActive)
            {
                EpisodeActiveSeconds += deltaTime;
                if (Vector2.Distance(ballPosition, m_RecoveryOrigin) >= ReleaseDistance
                    || ballVelocity.magnitude >= ReleaseSpeed)
                {
                    ClearTracking(ballPosition, true);
                    return false;
                }

                m_StationarySeconds += deltaTime;
                m_RetargetSeconds += deltaTime;
                while (m_RetargetSeconds >= RetargetSeconds)
                {
                    m_RetargetSeconds -= RetargetSeconds;
                    Sequence++;
                }
                return true;
            }

            if (!m_HasObservationOrigin)
            {
                m_ObservationOrigin = ballPosition;
                m_HasObservationOrigin = true;
            }

            if (Vector2.Distance(ballPosition, m_ObservationOrigin) > ObservationRadius)
            {
                m_ObservationOrigin = ballPosition;
                m_StationarySeconds = 0f;
                return false;
            }

            m_StationarySeconds += deltaTime;
            if (m_StationarySeconds < TriggerSeconds) return false;

            IsActive = true;
            m_RecoveryOrigin = ballPosition;
            m_RetargetSeconds = 0f;
            Sequence++;
            ActivationCount++;
            EpisodeActivationCount++;
            return true;
        }

        public void ResetEpisode()
        {
            EpisodeActivationCount = 0; EpisodeActiveSeconds = 0f; Reset();
        }

        public void ResetForKickoff()
        {
            Reset();
            m_KickoffGraceRemaining = KickoffGraceSeconds;
        }

        public void Reset()
        {
            IsActive = false;
            Sequence = 0;
            ActivationCount = 0;
            m_ObservationOrigin = default;
            m_RecoveryOrigin = default;
            m_StationarySeconds = 0f;
            m_RetargetSeconds = 0f;
            m_KickoffGraceRemaining = 0f;
            m_HasObservationOrigin = false;
        }

        void ClearTracking(Vector2 nextOrigin, bool keepOrigin)
        {
            IsActive = false;
            m_StationarySeconds = 0f;
            m_RetargetSeconds = 0f;
            m_RecoveryOrigin = default;
            m_ObservationOrigin = nextOrigin;
            m_HasObservationOrigin = keepOrigin;
        }
    }

    public sealed class MNG_ContestedBallEscape
    {
        public const float TriggerSeconds = 0.60f;
        public const float StationaryRadius = 0.55f;
        public const float EscapeDistance = 2.25f;
        public const float EscapeCycleSeconds = 1.40f;

        Vector2 m_ObservationOrigin;
        float m_StationarySeconds;
        float m_EscapeSeconds;
        bool m_HasOrigin;

        public bool IsActive { get; private set; }
        public Vector2 EscapeOrigin { get; private set; }
        public int Sequence { get; private set; }
        public int ActivationCount { get; private set; }

        public bool Update(
            Vector2 ballPosition,
            bool redContenderNearby,
            bool navyContenderNearby,
            float deltaTime)
        {
            if (!MNG_MatchSnapshot.IsFinite(ballPosition.x)
                || !MNG_MatchSnapshot.IsFinite(ballPosition.y)
                || !MNG_MatchSnapshot.IsFinite(deltaTime)
                || deltaTime <= 0f)
                throw new ArgumentOutOfRangeException(nameof(deltaTime));

            if (!redContenderNearby || !navyContenderNearby)
            {
                ClearTracking();
                return false;
            }

            if (IsActive)
            {
                if (Vector2.Distance(ballPosition, EscapeOrigin) >= EscapeDistance)
                {
                    ClearTracking();
                    return false;
                }

                m_EscapeSeconds += deltaTime;
                if (m_EscapeSeconds >= EscapeCycleSeconds)
                {
                    Sequence++;
                    EscapeOrigin = ballPosition;
                    m_EscapeSeconds = 0f;
                }
                return true;
            }

            if (!m_HasOrigin)
            {
                m_ObservationOrigin = ballPosition;
                m_HasOrigin = true;
            }

            if (Vector2.Distance(ballPosition, m_ObservationOrigin) > StationaryRadius)
            {
                m_ObservationOrigin = ballPosition;
                m_StationarySeconds = 0f;
                return false;
            }

            m_StationarySeconds += deltaTime;
            if (m_StationarySeconds < TriggerSeconds) return false;

            IsActive = true;
            EscapeOrigin = ballPosition;
            m_EscapeSeconds = 0f;
            m_StationarySeconds = 0f;
            Sequence++;
            ActivationCount++;
            return true;
        }

        public void Reset()
        {
            ClearTracking();
            Sequence = 0;
            ActivationCount = 0;
            EscapeOrigin = Vector2.zero;
        }

        void ClearTracking()
        {
            IsActive = false;
            m_HasOrigin = false;
            m_StationarySeconds = 0f;
            m_EscapeSeconds = 0f;
        }
    }
}

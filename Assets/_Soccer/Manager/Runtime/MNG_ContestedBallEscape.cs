using System;
using UnityEngine;

namespace MachineLearning.Soccer.Manager
{
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

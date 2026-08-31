using UnityEngine;
using UnityEngine.Serialization;

namespace MachineLearning.Soccer
{
    /// <summary>
    /// 4대4 경기의 공통 이동 수치와 호환용 머티리얼 참조.
    /// </summary>
    public sealed class SoccerSettings : MonoBehaviour
    {
        public const float DefaultAgentRunSpeed = 2.2f;
        public const float DefaultMaximumPlanarSpeed = 9f;
        public const float DefaultRotationSpeed = 125f;
        // Human 대상은 Striker다. 2.2 * 1.25 / 0.02 = 137.5로 AI 전진 가속과 맞춘다.
        public const float DefaultHumanAcceleration = 137.5f;
        public const float DefaultHumanDeceleration = 48f;

        [FormerlySerializedAs("blueMaterial")]
        public Material redMaterial;
        [FormerlySerializedAs("purpleMaterial")]
        public Material navyMaterial;
        public bool randomizePlayersTeamForTraining;
        [Min(0.1f)] public float agentRunSpeed = DefaultAgentRunSpeed;
        [Min(0.1f)] public float maximumPlanarSpeed = DefaultMaximumPlanarSpeed;
        [Min(1f)] public float rotationSpeed = DefaultRotationSpeed;

        [Header("Human movement")]
        [Min(0.1f)] public float humanAcceleration = DefaultHumanAcceleration;
        [Min(0.1f)] public float humanDeceleration = DefaultHumanDeceleration;
    }
}

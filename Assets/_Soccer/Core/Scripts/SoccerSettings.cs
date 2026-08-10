using UnityEngine;

namespace MachineLearning.Soccer
{
    /// <summary>
    /// 4대4 경기의 공통 이동 수치와 호환용 머티리얼 참조.
    /// </summary>
    public sealed class SoccerSettings : MonoBehaviour
    {
        public const float DefaultHumanAcceleration = 32f;
        public const float DefaultHumanDeceleration = 48f;

        public Material purpleMaterial;
        public Material blueMaterial;
        public bool randomizePlayersTeamForTraining;
        [Min(0.1f)] public float agentRunSpeed = 2f;
        [Min(0.1f)] public float maximumPlanarSpeed = 9f;
        [Min(1f)] public float rotationSpeed = 120f;

        [Header("Human movement")]
        [Min(0.1f)] public float humanAcceleration = DefaultHumanAcceleration;
        [Min(0.1f)] public float humanDeceleration = DefaultHumanDeceleration;
    }
}

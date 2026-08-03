using UnityEngine;

namespace MachineLearning.Soccer
{
    /// <summary>
    /// Shared tuning values for the playable 4v4 scene. The material fields are
    /// retained so the copied ML-Agents Soccer scenes and prefabs stay compatible.
    /// </summary>
    public sealed class SoccerSettings : MonoBehaviour
    {
        public Material purpleMaterial;
        public Material blueMaterial;
        public bool randomizePlayersTeamForTraining;
        [Min(0.1f)] public float agentRunSpeed = 2f;
        [Min(0.1f)] public float maximumPlanarSpeed = 9f;
        [Min(1f)] public float rotationSpeed = 120f;
    }
}

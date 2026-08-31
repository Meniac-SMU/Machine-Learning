using UnityEngine;

namespace MachineLearning.Soccer
{
    /// <summary>Separates a ray-visible goal frame from its interior scoring colliders.</summary>
    [DisallowMultipleComponent]
    public sealed class SoccerGoalSurface : MonoBehaviour
    {
        [SerializeField] Team defendingTeam;
        [SerializeField] bool scores;

        public Team DefendingTeam => defendingTeam;
        public bool Scores => scores;

        public void Configure(Team team, bool isScoringSurface)
        {
            defendingTeam = team;
            scores = isScoringSurface;
        }
    }
}

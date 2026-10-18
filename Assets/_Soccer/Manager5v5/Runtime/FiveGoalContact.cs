using UnityEngine;

namespace MachineLearning.Soccer.Manager5v5
{
    public sealed class FiveGoalContact : MonoBehaviour
    {
        public FiveMatch match;
        void OnCollisionEnter(Collision collision) => Contact(collision.collider);
        void OnCollisionStay(Collision collision) => Contact(collision.collider);
        void Contact(Collider collider)
        {
            var player=collider.GetComponentInParent<FivePlayer>();
            if(player!=null) match.RegisterBallContact(player);
        }
        void OnTriggerEnter(Collider other)
        {
            var goal = other.GetComponent<SoccerGoalSurface>();
            if (goal != null && goal.Scores)
                match.Score(goal.DefendingTeam == Team.Red ? Team.Navy : Team.Red);
        }
    }
}

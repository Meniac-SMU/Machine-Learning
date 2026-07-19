using UnityEngine;

namespace MachineLearning.Soccer
{
    public sealed class SoccerBallController : MonoBehaviour
    {
        public GameObject area;
        [HideInInspector] public SoccerEnvController envController;
        public string purpleGoalTag = "purpleGoal";
        public string blueGoalTag = "blueGoal";

        void Start()
        {
            if (area != null)
            {
                envController = area.GetComponent<SoccerEnvController>();
            }
        }

        public void Configure(GameObject configuredArea)
        {
            area = configuredArea;
            envController = configuredArea != null ? configuredArea.GetComponent<SoccerEnvController>() : null;
        }

        void OnCollisionEnter(Collision collision)
        {
            if (envController == null)
            {
                return;
            }

            if (collision.gameObject.CompareTag(purpleGoalTag))
            {
                envController.GoalTouched(Team.Blue);
            }
            else if (collision.gameObject.CompareTag(blueGoalTag))
            {
                envController.GoalTouched(Team.Purple);
            }
        }
    }
}

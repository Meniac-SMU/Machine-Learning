using UnityEngine;
using UnityEngine.Serialization;

namespace MachineLearning.Soccer
{
    public sealed class SoccerBallController : MonoBehaviour
    {
        public GameObject area;
        [HideInInspector] public SoccerEnvController envController;
        [FormerlySerializedAs("blueGoalTag")]
        public string redGoalTag = "redGoal";
        [FormerlySerializedAs("purpleGoalTag")]
        public string navyGoalTag = "navyGoal";
        [SerializeField] bool enforceStadiumPlanarMotion;
        [SerializeField, Min(0f)] float lockedCenterHeight;

        Rigidbody ballRigidbody;
        SphereCollider ballCollider;

        public bool EnforcesStadiumPlanarMotion => enforceStadiumPlanarMotion;
        public float LockedCenterHeight => lockedCenterHeight;

        void Start()
        {
            CacheComponents();
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

        public void ConfigureStadiumMotion(float centerHeight)
        {
            enforceStadiumPlanarMotion = true;
            lockedCenterHeight = Mathf.Max(0f, centerHeight);
            CacheComponents();
        }

        void CacheComponents()
        {
            if (ballRigidbody == null) ballRigidbody = GetComponent<Rigidbody>();
            if (ballCollider == null) ballCollider = GetComponent<SphereCollider>();
        }

        void FixedUpdate()
        {
            if (!enforceStadiumPlanarMotion) return;
            CacheComponents();
            if (ballRigidbody == null || ballRigidbody.isKinematic) return;
            if (envController == null && area != null)
                envController = area.GetComponent<SoccerEnvController>();

            var position = ballRigidbody.position;
            position.y = lockedCenterHeight;
            var geometry = envController != null ? envController.ArenaGeometry : null;
            if (geometry != null && ballCollider != null)
            {
                var scale = transform.lossyScale;
                var radius = ballCollider.radius * Mathf.Max(scale.x, scale.z);
                var clamped = geometry.ClampBallPosition(position, radius);
                var correction = clamped - position;
                if (correction.sqrMagnitude > 0.00000001f)
                {
                    // Cancel only velocity that continues through the violated
                    // boundary. Tangential movement and rebounds remain intact.
                    var inward = correction.normalized;
                    var outwardSpeed = -Vector3.Dot(ballRigidbody.linearVelocity, inward);
                    if (outwardSpeed > 0f)
                        ballRigidbody.linearVelocity += inward * outwardSpeed;
                    position = clamped;
                }
            }
            ballRigidbody.position = position;

            var velocity = ballRigidbody.linearVelocity;
            velocity.y = 0f;
            ballRigidbody.linearVelocity = velocity;
            var angular = ballRigidbody.angularVelocity;
            var radiusForRolling = Mathf.Max(0.0001f, lockedCenterHeight);
            var rolling = Vector3.Cross(Vector3.up, velocity) / radiusForRolling;
            ballRigidbody.angularVelocity = new Vector3(rolling.x, angular.y, rolling.z);
        }

        void OnCollisionEnter(Collision collision)
        {
            HandleGoalCollision(collision);
        }

        void OnCollisionStay(Collision collision)
        {
            // A ball may first touch the goal floor while its centre is still on
            // the pitch. Check again after crossing; GoalTouched guards repeats.
            if (envController != null && envController.ArenaGeometry != null)
            {
                HandleGoalCollision(collision);
            }
        }

        void OnTriggerEnter(Collider other)
        {
            HandleStadiumGoal(other);
        }

        void OnTriggerStay(Collider other)
        {
            // Enter can occur while the ball's leading edge is across the line
            // but its centre is still outside the goal.
            HandleStadiumGoal(other);
        }

        void HandleStadiumGoal(Collider collider)
        {
            if (envController == null || envController.ArenaGeometry == null)
            {
                return;
            }

            var surface = collider.GetComponent<SoccerGoalSurface>();
            if (surface != null && surface.Scores
                && envController.ArenaGeometry.ContainsGoalBall(surface.DefendingTeam, transform.position))
            {
                envController.GoalTouched(surface.DefendingTeam == Team.Red ? Team.Navy : Team.Red);
            }
        }

        void HandleGoalCollision(Collision collision)
        {
            if (envController == null)
            {
                return;
            }

            if (envController.ArenaGeometry != null)
            {
                HandleStadiumGoal(collision.collider);
                return;
            }

            if (collision.gameObject.CompareTag(navyGoalTag))
            {
                envController.GoalTouched(Team.Red);
            }
            else if (collision.gameObject.CompareTag(redGoalTag))
            {
                envController.GoalTouched(Team.Navy);
            }
        }
    }
}

using System;
using System.Linq;
using UnityEngine;

namespace MachineLearning.Soccer.Manager
{
    [DefaultExecutionOrder(-500)]
    [DisallowMultipleComponent]
    public sealed class MNG_TrainingBootstrap : MonoBehaviour
    {
        [SerializeField, Min(1f)] float timeScale = 20f;

        public float TimeScale => timeScale;

        void Awake()
        {
            Time.timeScale = timeScale;
        }

        void Start()
        {
            var managers = GetComponentsInChildren<MNG_ManagerAgent>(true);
            // Start runs after every Agent Awake/OnEnable. Checking during this component's
            // earlier Awake can observe a policy endpoint before Unity finishes activation.
            var activeManagers = managers
                .Where(manager => manager.enabled && manager.gameObject.activeInHierarchy)
                .ToArray();
            if (activeManagers.Length != 1)
                throw new InvalidOperationException(
                    $"MNG training Scene requires exactly one active manager, found {activeManagers.Length}.");
            var trainingTeam = activeManagers[0].Team;
            if (GetComponentsInChildren<MNG_FallbackManager>(true)
                .Any(fallback => fallback.enabled && fallback.Team == trainingTeam))
                throw new InvalidOperationException("MNG training team cannot also be driven by fallback.");
            var curriculum = GetComponent<MNG_CurriculumController>();
            var fallbackMatch = GetComponent<MNG_FallbackMatchController>();
            var mode = curriculum != null ? curriculum.Stage.ToString()
                : fallbackMatch != null ? fallbackMatch.Stage.ToString()
                : "ConnectionSmoke";
            Debug.Log($"MNG TRAINING BOOTSTRAP PASS mode={mode} team={trainingTeam} "
                + $"activeManagers=1 timeScale={timeScale:R}");
        }
    }
}

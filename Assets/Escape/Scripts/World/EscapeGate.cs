using UnityEngine;

namespace MachineLearning.Escape
{
    public sealed class EscapeGate : MonoBehaviour
    {
        [SerializeField] Collider blockingCollider;
        [SerializeField] Collider exitTrigger;
        [SerializeField] Renderer targetRenderer;
        [SerializeField] Material closedMaterial;
        [SerializeField] Material activeMaterial;

        EscapeEnvironmentController m_Environment;

        public bool IsActive { get; private set; }

        void Awake()
        {
            m_Environment = GetComponentInParent<EscapeEnvironmentController>();
        }

        public void ActivateGate()
        {
            IsActive = true;
            if (blockingCollider != null)
            {
                blockingCollider.enabled = false;
            }

            if (exitTrigger != null)
            {
                exitTrigger.enabled = true;
            }

            ApplyMaterial(activeMaterial);
        }

        public void ResetClosed()
        {
            IsActive = false;
            if (blockingCollider != null)
            {
                blockingCollider.enabled = true;
            }

            if (exitTrigger != null)
            {
                exitTrigger.enabled = false;
            }

            ApplyMaterial(closedMaterial);
            gameObject.SetActive(true);
        }

        public void Configure(Collider blocker, Collider trigger, Renderer renderer, Material closed, Material active)
        {
            blockingCollider = blocker;
            exitTrigger = trigger;
            targetRenderer = renderer;
            closedMaterial = closed;
            activeMaterial = active;
        }

        internal void NotifyExit(Collider other)
        {
            if (IsActive && other.GetComponentInParent<EscapePlayerAgent>() != null)
            {
                m_Environment?.PlayerEscaped();
            }
        }

        void ApplyMaterial(Material material)
        {
            if (targetRenderer != null && material != null)
            {
                targetRenderer.sharedMaterial = material;
            }
        }
    }

}

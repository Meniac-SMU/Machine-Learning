using UnityEngine;

namespace MachineLearning.Escape
{
    [RequireComponent(typeof(Collider))]
    public sealed class EscapeButton : MonoBehaviour
    {
        [SerializeField] Renderer targetRenderer;
        [SerializeField] Material inactiveMaterial;
        [SerializeField] Material activeMaterial;

        EscapeEnvironmentController m_Environment;

        public bool IsPressed { get; private set; }

        void Awake()
        {
            m_Environment = GetComponentInParent<EscapeEnvironmentController>();
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<EscapePlayerAgent>() != null)
            {
                m_Environment?.TryPressButton(this);
            }
        }

        public bool Press()
        {
            if (IsPressed)
            {
                return false;
            }

            IsPressed = true;
            ApplyMaterial(activeMaterial);
            return true;
        }

        public void ResetButton()
        {
            IsPressed = false;
            ApplyMaterial(inactiveMaterial);
            gameObject.SetActive(true);
        }

        public void Configure(Renderer renderer, Material inactive, Material active)
        {
            targetRenderer = renderer;
            inactiveMaterial = inactive;
            activeMaterial = active;
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

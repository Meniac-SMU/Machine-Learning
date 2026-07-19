using UnityEngine;

namespace MachineLearning.Escape
{
    public sealed class EscapePlayerHealth : MonoBehaviour
    {
        [SerializeField] int maxHealth = 3;
        [SerializeField] float invulnerabilityDuration = 3f;
        [SerializeField] float blinkInterval = 0.2f;
        [SerializeField] Renderer targetRenderer;
        [SerializeField] Material normalMaterial;
        [SerializeField] Material hitMaterial;

        float m_InvulnerabilityRemaining;

        public int CurrentHealth { get; private set; }
        public int MaxHealth => maxHealth;
        public bool IsInvulnerable => m_InvulnerabilityRemaining > 0f;

        void Awake()
        {
            ResetHealth();
        }

        void Update()
        {
            if (m_InvulnerabilityRemaining <= 0f)
            {
                return;
            }

            m_InvulnerabilityRemaining = Mathf.Max(0f, m_InvulnerabilityRemaining - Time.deltaTime);
            if (targetRenderer != null)
            {
                var showHit = Mathf.FloorToInt(m_InvulnerabilityRemaining / blinkInterval) % 2 == 0;
                targetRenderer.sharedMaterial = showHit ? hitMaterial : normalMaterial;
            }

            if (m_InvulnerabilityRemaining <= 0f && targetRenderer != null)
            {
                targetRenderer.sharedMaterial = normalMaterial;
            }
        }

        public bool TryDamage(out bool died)
        {
            died = false;
            if (CurrentHealth <= 0 || IsInvulnerable)
            {
                return false;
            }

            CurrentHealth--;
            died = CurrentHealth <= 0;
            m_InvulnerabilityRemaining = invulnerabilityDuration;
            if (targetRenderer != null)
            {
                targetRenderer.sharedMaterial = hitMaterial;
            }

            return true;
        }

        public void ResetHealth()
        {
            CurrentHealth = maxHealth;
            m_InvulnerabilityRemaining = 0f;
            if (targetRenderer != null)
            {
                targetRenderer.sharedMaterial = normalMaterial;
            }
        }

        public void Configure(Renderer renderer, Material normal, Material hit)
        {
            targetRenderer = renderer;
            normalMaterial = normal;
            hitMaterial = hit;
        }
    }
}

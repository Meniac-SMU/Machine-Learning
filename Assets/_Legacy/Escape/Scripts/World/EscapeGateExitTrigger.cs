using UnityEngine;

namespace MachineLearning.Escape
{
    public sealed class EscapeGateExitTrigger : MonoBehaviour
    {
        EscapeGate m_Gate;

        void Awake()
        {
            m_Gate = GetComponentInParent<EscapeGate>();
        }

        void OnTriggerEnter(Collider other)
        {
            m_Gate?.NotifyExit(other);
        }
    }
}

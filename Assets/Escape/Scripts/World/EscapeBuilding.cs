using UnityEngine;

namespace MachineLearning.Escape
{
    public sealed class EscapeBuilding : MonoBehaviour
    {
        [SerializeField] Transform[] buttonSockets;

        public int SocketCount => buttonSockets?.Length ?? 0;

        public Transform GetSocket(int index)
        {
            return buttonSockets[index];
        }

        public void Configure(Transform[] sockets)
        {
            buttonSockets = sockets;
        }
    }
}

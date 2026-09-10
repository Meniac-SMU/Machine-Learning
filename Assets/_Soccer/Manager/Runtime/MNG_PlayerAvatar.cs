using System;
using MachineLearning.Soccer;
using UnityEngine;

namespace MachineLearning.Soccer.Manager
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class MNG_PlayerAvatar : MonoBehaviour
    {
        [SerializeField] Team team;
        [SerializeField, Range(0, MNG_MatchSnapshot.PlayersPerTeam - 1)] int slot;
        [SerializeField] MNG_PlayerRole role;
        [SerializeField, Min(0f)] float maximumKickCooldownSeconds = 1f;

        Rigidbody m_Body;
        float m_KickCooldownSeconds;
        bool m_IsHuman;
        MNG_ControlOwnership m_Ownership;
        MNG_KickPlate m_KickPlate;

        public Team Team => team;
        public int Slot => slot;
        public MNG_PlayerRole Role => role;
        public Rigidbody Body => m_Body != null ? m_Body : m_Body = GetComponent<Rigidbody>();
        public bool IsHuman => m_IsHuman;
        public float KickCooldownSeconds => m_KickCooldownSeconds;
        public float MaximumKickCooldownSeconds => maximumKickCooldownSeconds;
        public MNG_KickPlate KickPlate
            => m_KickPlate != null ? m_KickPlate : m_KickPlate = GetComponentInChildren<MNG_KickPlate>(true);
        public MNG_ControlOwnership Ownership
            => m_Ownership ??= new MNG_ControlOwnership(team, slot);

        void Awake()
        {
            m_Body = GetComponent<Rigidbody>();
            m_KickPlate = GetComponentInChildren<MNG_KickPlate>(true);
            m_Ownership = new MNG_ControlOwnership(team, slot);
        }

        public void Configure(
            Team configuredTeam,
            int configuredSlot,
            MNG_PlayerRole configuredRole,
            float configuredMaximumKickCooldownSeconds = 1f)
        {
            if (configuredSlot < 0 || configuredSlot >= MNG_MatchSnapshot.PlayersPerTeam)
                throw new ArgumentOutOfRangeException(nameof(configuredSlot));
            if (configuredMaximumKickCooldownSeconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(configuredMaximumKickCooldownSeconds));
            team = configuredTeam;
            slot = configuredSlot;
            role = configuredRole;
            maximumKickCooldownSeconds = configuredMaximumKickCooldownSeconds;
            m_Ownership = new MNG_ControlOwnership(team, slot);
        }

        public void SetHumanControlled(bool value) => m_IsHuman = value;

        public void StartKickCooldown()
        {
            m_KickCooldownSeconds = maximumKickCooldownSeconds;
        }

        public void ResetKickCooldown() => m_KickCooldownSeconds = 0f;

        public void AdvanceCooldown(float deltaTime)
        {
            if (deltaTime < 0f) throw new ArgumentOutOfRangeException(nameof(deltaTime));
            m_KickCooldownSeconds = Mathf.Max(0f, m_KickCooldownSeconds - deltaTime);
        }

        public MNG_PlayerState CaptureState()
        {
            var body = Body;
            var forward = transform.forward;
            return new MNG_PlayerState
            {
                Active = gameObject.activeInHierarchy,
                Position = new Vector2(transform.position.x, transform.position.z),
                Velocity = new Vector2(body.linearVelocity.x, body.linearVelocity.z),
                Forward = new Vector2(forward.x, forward.z),
                Role = role,
                IsHuman = m_IsHuman,
                KickCooldownSeconds = m_KickCooldownSeconds,
                MaximumKickCooldownSeconds = maximumKickCooldownSeconds
            };
        }
    }
}

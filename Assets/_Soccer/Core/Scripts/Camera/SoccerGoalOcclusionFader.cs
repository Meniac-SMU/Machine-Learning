using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

namespace MachineLearning.Soccer
{
    /// <summary>
    /// 카메라와 선수 사이의 골대만 일시적으로 반투명 처리하는 표시 제어기.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class SoccerGoalOcclusionFader : MonoBehaviour
    {
        static readonly string[] RedGoalNames = { "GoalRed", "GoalNetRed", "GoalNetRedOuter" };
        static readonly string[] NavyGoalNames = { "GoalNavy", "GoalNetNavy", "GoalNetNavyOuter" };

        [SerializeField] SoccerEnvController environment;
        [FormerlySerializedAs("blueGoalRendererOverrides")]
        [SerializeField] Renderer[] redGoalRendererOverrides = Array.Empty<Renderer>();
        [FormerlySerializedAs("purpleGoalRendererOverrides")]
        [SerializeField] Renderer[] navyGoalRendererOverrides = Array.Empty<Renderer>();
        [Range(0.05f, 0.95f)] [SerializeField] float occludedAlpha = 0.25f;
        [Min(0.1f)] [SerializeField] float fadeSpeed = 8f;
        [Min(0f)] [SerializeField] float boundsPadding = 0.4f;
        [Range(0f, 0.25f)] [SerializeField] float viewportMargin = 0.03f;

        readonly List<PlayerTarget> m_PlayerTargets = new();
        Camera m_Camera;
        GoalFadeGroup m_RedGoal;
        GoalFadeGroup m_NavyGoal;
        bool m_Initialized;

        public SoccerEnvController Environment => environment;
        public float OccludedAlpha => occludedAlpha;
        public float FadeSpeed => fadeSpeed;
        public float BoundsPadding => boundsPadding;
        public bool IsRedGoalOccluding => m_RedGoal?.IsOccluding ?? false;
        public bool IsNavyGoalOccluding => m_NavyGoal?.IsOccluding ?? false;
        public int RedGoalRendererCount => m_RedGoal?.RendererCount ?? 0;
        public int NavyGoalRendererCount => m_NavyGoal?.RendererCount ?? 0;

        void Awake()
        {
            m_Camera = GetComponent<Camera>();
            InitializeIfNeeded();
        }

        void OnDisable()
        {
            m_RedGoal?.RestoreOriginals();
            m_NavyGoal?.RestoreOriginals();
        }

        void OnDestroy()
        {
            DisposeGroups();
        }

        public void Configure(SoccerEnvController configuredEnvironment)
        {
            if (environment == configuredEnvironment)
            {
                return;
            }

            environment = configuredEnvironment;
            if (Application.isPlaying)
            {
                DisposeGroups();
                InitializeIfNeeded();
            }
        }

        public void RefreshOcclusion(bool immediate = false)
        {
            if (!isActiveAndEnabled || !InitializeIfNeeded())
            {
                return;
            }

            var redOccluding = false;
            var navyOccluding = false;
            var cameraPosition = m_Camera.transform.position;
            foreach (var playerTarget in m_PlayerTargets)
            {
                if (!playerTarget.TryGetCenter(out var targetPosition) || !IsVisibleOnScreen(targetPosition))
                {
                    continue;
                }

                var toTarget = targetPosition - cameraPosition;
                var targetDistance = toTarget.magnitude;
                if (targetDistance <= 0.2f)
                {
                    continue;
                }

                var ray = new Ray(cameraPosition, toTarget / targetDistance);
                if (!redOccluding && IsInsideGoal(Team.Red, targetPosition))
                {
                    redOccluding = m_RedGoal.IntersectsBeforeTarget(ray, targetDistance, boundsPadding);
                }

                if (!navyOccluding && IsInsideGoal(Team.Navy, targetPosition))
                {
                    navyOccluding = m_NavyGoal.IntersectsBeforeTarget(ray, targetDistance, boundsPadding);
                }

                if (redOccluding && navyOccluding)
                {
                    break;
                }
            }

            var deltaTime = immediate ? float.PositiveInfinity : Time.unscaledDeltaTime;
            m_RedGoal.UpdateFade(redOccluding, occludedAlpha, fadeSpeed, deltaTime);
            m_NavyGoal.UpdateFade(navyOccluding, occludedAlpha, fadeSpeed, deltaTime);
        }

        bool IsInsideGoal(Team defendingTeam, Vector3 targetPosition)
        {
            var arena = environment != null ? environment.ArenaGeometry : null;
            if (arena != null)
            {
                return arena.ContainsGoalInterior(defendingTeam, targetPosition);
            }

            var direction = defendingTeam == Team.Red ? -1f : 1f;
            var depth = targetPosition.x * direction;
            return depth >= SoccerArenaGeometry.StadiumHalfLength
                && depth <= SoccerArenaGeometry.StadiumHalfLength + SoccerArenaGeometry.StadiumGoalDepth
                && Mathf.Abs(targetPosition.z) <= SoccerArenaGeometry.StadiumGoalHalfWidth
                && targetPosition.y >= 0f
                && targetPosition.y <= SoccerArenaGeometry.StadiumGoalHeight;
        }

        public void ConfigureGoalRenderers(Renderer[] red, Renderer[] navy)
        {
            DisposeGroups();
            redGoalRendererOverrides = red ?? Array.Empty<Renderer>();
            navyGoalRendererOverrides = navy ?? Array.Empty<Renderer>();
            if (Application.isPlaying)
            {
                InitializeIfNeeded();
            }
        }

        bool InitializeIfNeeded()
        {
            if (m_Initialized)
            {
                return m_RedGoal != null && m_NavyGoal != null;
            }

            m_Camera ??= GetComponent<Camera>();
            environment ??= FindFirstObjectByType<SoccerEnvController>();
            if (m_Camera == null || environment == null)
            {
                return false;
            }

            var renderers = environment.GetComponentsInChildren<Renderer>(true);
            m_RedGoal = new GoalFadeGroup(redGoalRendererOverrides.Length > 0
                ? redGoalRendererOverrides.Where(renderer => renderer != null).ToArray()
                : FindGoalRenderers(renderers, "redGoal", RedGoalNames));
            m_NavyGoal = new GoalFadeGroup(navyGoalRendererOverrides.Length > 0
                ? navyGoalRendererOverrides.Where(renderer => renderer != null).ToArray()
                : FindGoalRenderers(renderers, "navyGoal", NavyGoalNames));
            m_PlayerTargets.Clear();
            foreach (var item in environment.AgentsList)
            {
                if (item?.Agent != null)
                {
                    m_PlayerTargets.Add(new PlayerTarget(item.Agent));
                }
            }

            m_Initialized = true;
            return m_RedGoal.RendererCount > 0 && m_NavyGoal.RendererCount > 0;
        }

        bool IsVisibleOnScreen(Vector3 worldPosition)
        {
            var viewport = m_Camera.WorldToViewportPoint(worldPosition);
            return viewport.z > m_Camera.nearClipPlane
                && viewport.x >= -viewportMargin
                && viewport.x <= 1f + viewportMargin
                && viewport.y >= -viewportMargin
                && viewport.y <= 1f + viewportMargin;
        }

        static Renderer[] FindGoalRenderers(IEnumerable<Renderer> renderers, string requiredTag, string[] names)
        {
            var requiredNames = new HashSet<string>(names, StringComparer.Ordinal);
            return renderers
                .Where(renderer => requiredNames.Contains(renderer.gameObject.name)
                    && renderer.gameObject.CompareTag(requiredTag))
                .OrderBy(renderer => renderer.gameObject.name, StringComparer.Ordinal)
                .ToArray();
        }

        void DisposeGroups()
        {
            m_RedGoal?.Dispose();
            m_NavyGoal?.Dispose();
            m_RedGoal = null;
            m_NavyGoal = null;
            m_PlayerTargets.Clear();
            m_Initialized = false;
        }

        sealed class PlayerTarget
        {
            readonly AgentSoccer m_Agent;
            readonly Renderer m_BodyRenderer;

            public PlayerTarget(AgentSoccer agent)
            {
                m_Agent = agent;
                m_BodyRenderer = agent.GetComponentsInChildren<Renderer>(true)
                    .FirstOrDefault(renderer => renderer.gameObject.name.StartsWith("AgentCube_", StringComparison.Ordinal));
            }

            public bool TryGetCenter(out Vector3 center)
            {
                if (m_Agent == null || !m_Agent.gameObject.activeInHierarchy)
                {
                    center = default;
                    return false;
                }

                center = m_BodyRenderer != null && m_BodyRenderer.enabled
                    ? m_BodyRenderer.bounds.center
                    : m_Agent.transform.position + Vector3.up * 0.75f;
                return true;
            }
        }

        sealed class GoalFadeGroup : IDisposable
        {
            readonly RendererState[] m_Renderers;
            float m_CurrentAlpha = 1f;

            public GoalFadeGroup(Renderer[] renderers)
            {
                m_Renderers = renderers.Select(renderer => new RendererState(renderer)).ToArray();
            }

            public bool IsOccluding { get; private set; }
            public int RendererCount => m_Renderers.Length;

            public bool IntersectsBeforeTarget(Ray ray, float targetDistance, float padding)
            {
                var maximumHitDistance = Mathf.Max(0f, targetDistance - 0.15f);
                foreach (var state in m_Renderers)
                {
                    if (state.IntersectsBeforeTarget(ray, maximumHitDistance, padding))
                    {
                        return true;
                    }
                }

                return false;
            }

            public void UpdateFade(bool isOccluding, float fadedAlpha, float speed, float deltaTime)
            {
                IsOccluding = isOccluding;
                var targetAlpha = isOccluding ? fadedAlpha : 1f;
                m_CurrentAlpha = float.IsPositiveInfinity(deltaTime)
                    ? targetAlpha
                    : Mathf.MoveTowards(m_CurrentAlpha, targetAlpha, speed * deltaTime);

                if (m_CurrentAlpha >= 0.999f && !isOccluding)
                {
                    RestoreOriginals();
                    return;
                }

                foreach (var renderer in m_Renderers)
                {
                    renderer.ApplyTransparentAlpha(m_CurrentAlpha);
                }
            }

            public void RestoreOriginals()
            {
                m_CurrentAlpha = 1f;
                IsOccluding = false;
                foreach (var renderer in m_Renderers)
                {
                    renderer.RestoreOriginals();
                }
            }

            public void Dispose()
            {
                foreach (var renderer in m_Renderers)
                {
                    renderer.Dispose();
                }
            }
        }

        sealed class RendererState : IDisposable
        {
            readonly Material[] m_OriginalMaterials;
            readonly ShadowCastingMode m_OriginalShadowMode;
            readonly Collider[] m_Colliders;
            Material[] m_TransparentMaterials;
            Color[] m_OriginalColors;
            bool m_UsingTransparentMaterials;

            public RendererState(Renderer renderer)
            {
                Renderer = renderer;
                m_OriginalMaterials = renderer.sharedMaterials;
                m_OriginalShadowMode = renderer.shadowCastingMode;
                m_Colliders = renderer.GetComponentsInChildren<Collider>(true);
            }

            public Renderer Renderer { get; }

            public bool IntersectsBeforeTarget(Ray ray, float maximumHitDistance, float fallbackPadding)
            {
                if (Renderer == null || !Renderer.enabled || !Renderer.gameObject.activeInHierarchy)
                {
                    return false;
                }

                var hasActiveCollider = false;
                foreach (var collider in m_Colliders)
                {
                    if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    hasActiveCollider = true;
                    if (collider.Raycast(ray, out var hit, maximumHitDistance)
                        && hit.distance < maximumHitDistance)
                    {
                        return true;
                    }
                }

                if (hasActiveCollider)
                {
                    return false;
                }

                // Compatibility fallback for an authored goal without colliders.
                var bounds = Renderer.bounds;
                bounds.Expand(fallbackPadding * 2f);
                return bounds.IntersectRay(ray, out var boundsHitDistance)
                    && boundsHitDistance < maximumHitDistance;
            }

            public void ApplyTransparentAlpha(float alpha)
            {
                if (Renderer == null)
                {
                    return;
                }

                EnsureTransparentMaterials();
                for (var index = 0; index < m_TransparentMaterials.Length; index++)
                {
                    var material = m_TransparentMaterials[index];
                    if (material == null)
                    {
                        continue;
                    }

                    var color = m_OriginalColors[index];
                    color.a *= alpha;
                    if (material.HasProperty("_BaseColor"))
                    {
                        material.SetColor("_BaseColor", color);
                    }

                    if (material.HasProperty("_Color"))
                    {
                        material.SetColor("_Color", color);
                    }
                }

                if (!m_UsingTransparentMaterials)
                {
                    Renderer.sharedMaterials = m_TransparentMaterials;
                    m_UsingTransparentMaterials = true;
                }

                Renderer.shadowCastingMode = ShadowCastingMode.Off;
            }

            public void RestoreOriginals()
            {
                if (Renderer == null)
                {
                    return;
                }

                if (m_UsingTransparentMaterials)
                {
                    Renderer.sharedMaterials = m_OriginalMaterials;
                    m_UsingTransparentMaterials = false;
                }

                Renderer.shadowCastingMode = m_OriginalShadowMode;
            }

            void EnsureTransparentMaterials()
            {
                if (m_TransparentMaterials != null)
                {
                    return;
                }

                m_TransparentMaterials = new Material[m_OriginalMaterials.Length];
                m_OriginalColors = new Color[m_OriginalMaterials.Length];
                for (var index = 0; index < m_OriginalMaterials.Length; index++)
                {
                    var source = m_OriginalMaterials[index];
                    if (source == null)
                    {
                        continue;
                    }

                    m_OriginalColors[index] = source.HasProperty("_BaseColor")
                        ? source.GetColor("_BaseColor")
                        : source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white;
                    m_TransparentMaterials[index] = CreateTransparentCopy(source);
                }
            }

            static Material CreateTransparentCopy(Material source)
            {
                var material = new Material(source)
                {
                    name = source.name + " (Goal Fade Runtime)",
                    hideFlags = HideFlags.HideAndDontSave,
                    renderQueue = (int)RenderQueue.Transparent
                };
                material.SetOverrideTag("RenderType", "Transparent");
                if (material.HasProperty("_Surface"))
                {
                    material.SetFloat("_Surface", 1f);
                }

                if (material.HasProperty("_Blend"))
                {
                    material.SetFloat("_Blend", 0f);
                }

                if (material.HasProperty("_SrcBlend"))
                {
                    material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                }

                if (material.HasProperty("_DstBlend"))
                {
                    material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                }

                if (material.HasProperty("_ZWrite"))
                {
                    material.SetFloat("_ZWrite", 0f);
                }

                material.DisableKeyword("_ALPHATEST_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.SetShaderPassEnabled("ShadowCaster", false);
                return material;
            }

            public void Dispose()
            {
                RestoreOriginals();
                if (m_TransparentMaterials == null)
                {
                    return;
                }

                foreach (var material in m_TransparentMaterials)
                {
                    if (material != null)
                    {
                        UnityEngine.Object.Destroy(material);
                    }
                }

                m_TransparentMaterials = null;
                m_OriginalColors = null;
            }
        }
    }
}

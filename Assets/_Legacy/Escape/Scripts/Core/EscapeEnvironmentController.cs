using System;
using System.Collections.Generic;
using Unity.MLAgents;
using UnityEngine;

namespace MachineLearning.Escape
{
    public sealed class EscapeEnvironmentController : MonoBehaviour
    {
        [Header("Episode")]
        [SerializeField] float episodeDuration = 120f;
        [SerializeField] float resultFeedbackDuration = 0.75f;
        [SerializeField] int deterministicSeed;
        [SerializeField] EscapeRunMode defaultRunMode = EscapeRunMode.Human;

        [Header("World")]
        [SerializeField] Renderer floorRenderer;
        [SerializeField] Material floorDefaultMaterial;
        [SerializeField] Material floorPlayerWinMaterial;
        [SerializeField] Material floorEnemyWinMaterial;
        [SerializeField] EscapeBuilding[] buildings;
        [SerializeField] Transform[] spawnPoints;
        [SerializeField] Transform[] gateAnchors;
        [SerializeField] GameObject[] gateWallBlockers;
        [SerializeField] EscapeButton[] buttons;
        [SerializeField] EscapeGate gate;

        [Header("Agents")]
        [SerializeField] EscapePlayerAgent player;
        [SerializeField] EscapeEnemyAgent[] enemies;

        SimpleMultiAgentGroup m_EnemyGroup;
        System.Random m_Random;
        int m_EpisodeIndex;
        float m_ResultTimer;
        float m_SmokeSecondsRemaining = -1f;
        readonly int[] m_LastSpawnIndices = new int[4];
        readonly int[] m_LastButtonBuildingIndices = new int[EscapeMapLayout.ButtonCount];

        public EscapeEpisodeState State { get; private set; } = EscapeEpisodeState.Resetting;
        public EscapeEpisodeState LastResult { get; private set; } = EscapeEpisodeState.Resetting;
        public float RemainingTime { get; private set; }
        public int PressedButtonCount { get; private set; }
        public int SelectedGateIndex { get; private set; } = -1;
        public int BuildingCount => buildings?.Length ?? 0;
        public int SpawnPointCount => spawnPoints?.Length ?? 0;
        public int GateAnchorCount => gateAnchors?.Length ?? 0;
        public int ActiveButtonCount => buttons?.Length ?? 0;
        public IReadOnlyList<int> LastSpawnIndices => m_LastSpawnIndices;
        public IReadOnlyList<int> LastButtonBuildingIndices => m_LastButtonBuildingIndices;
        public IReadOnlyList<EscapeButton> Buttons => buttons;
        public EscapePlayerAgent Player => player;
        public bool IsRunning => State == EscapeEpisodeState.Running;
        public bool IsGateActive => gate != null && gate.IsActive;
        public bool IsPlayerInvulnerable => player != null && player.Health.IsInvulnerable;
        public float RemainingTimeNormalized => episodeDuration <= 0f ? 0f : Mathf.Clamp01(RemainingTime / episodeDuration);
        public float RequiredButtonsNormalized => Mathf.Clamp01(Mathf.Min(PressedButtonCount, EscapeMapLayout.RequiredButtonCount) / (float)EscapeMapLayout.RequiredButtonCount);
        public float PlayerHealthNormalized => player == null ? 0f : player.Health.CurrentHealth / (float)player.Health.MaxHealth;

        void Awake()
        {
            m_EnemyGroup = new SimpleMultiAgentGroup();
            if (enemies != null)
            {
                foreach (var enemy in enemies)
                {
                    if (enemy != null)
                    {
                        m_EnemyGroup.RegisterAgent(enemy);
                    }
                }
            }
        }

        void Start()
        {
            ConfigureRunModeFromCommandLine();
            ResetEnvironment();
            if (Application.isBatchMode)
            {
                foreach (var rendererComponent in GetComponentsInChildren<Renderer>(true))
                {
                    rendererComponent.enabled = false;
                }
            }
        }

        void FixedUpdate()
        {
            if (m_SmokeSecondsRemaining >= 0f)
            {
                m_SmokeSecondsRemaining -= Time.fixedDeltaTime;
                if (m_SmokeSecondsRemaining <= 0f)
                {
                    Debug.Log($"Escape smoke completed: state={State}, time={RemainingTime:0.00}, buttons={PressedButtonCount}, health={player.Health.CurrentHealth}.");
                    Application.Quit(0);
                    m_SmokeSecondsRemaining = -1f;
                }
            }

            if (State == EscapeEpisodeState.Running)
            {
                RemainingTime = Mathf.Max(0f, RemainingTime - Time.fixedDeltaTime);
                player.AddGameReward(-0.05f * Time.fixedDeltaTime / episodeDuration);
                foreach (var enemy in enemies)
                {
                    enemy.TickAttack(Time.fixedDeltaTime);
                }

                if (RemainingTime <= 0f)
                {
                    CompleteEpisode(EscapeEpisodeState.EnemyWin);
                }

                return;
            }

            if (State != EscapeEpisodeState.ResultFeedback)
            {
                return;
            }

            m_ResultTimer -= Time.fixedDeltaTime;
            if (m_ResultTimer <= 0f)
            {
                player.EndEpisode();
                m_EnemyGroup.EndGroupEpisode();
                ResetEnvironment();
            }
        }

        void OnDestroy()
        {
            m_EnemyGroup?.Dispose();
        }

        public void TryPressButton(EscapeButton button)
        {
            if (!IsRunning || button == null || !button.Press())
            {
                return;
            }

            PressedButtonCount++;
            if (PressedButtonCount <= EscapeMapLayout.RequiredButtonCount)
            {
                player.AddGameReward(0.1f);
            }

            if (PressedButtonCount >= EscapeMapLayout.RequiredButtonCount && !gate.IsActive)
            {
                gate.ActivateGate();
            }
        }

        public void ApplyEnemyHit(EscapeEnemyAgent enemy)
        {
            if (!IsRunning || !player.Health.TryDamage(out var died))
            {
                return;
            }

            player.AddGameReward(-0.05f);
            m_EnemyGroup.AddGroupReward(0.05f);
            if (died)
            {
                CompleteEpisode(EscapeEpisodeState.EnemyWin);
            }
        }

        public void PlayerEscaped()
        {
            if (IsRunning && gate.IsActive)
            {
                CompleteEpisode(EscapeEpisodeState.PlayerWin);
            }
        }

        public bool TryGetPlayerObjective(out Vector3 objective)
        {
            if (gate != null && gate.IsActive)
            {
                objective = gate.transform.position;
                return true;
            }

            var closestDistance = float.PositiveInfinity;
            objective = default;
            var found = false;
            foreach (var button in buttons)
            {
                if (button == null || button.IsPressed)
                {
                    continue;
                }

                var distance = (button.transform.position - player.transform.position).sqrMagnitude;
                if (distance >= closestDistance)
                {
                    continue;
                }

                closestDistance = distance;
                objective = button.transform.position;
                found = true;
            }

            return found;
        }

        public void ResetEnvironment(int? seedOverride = null)
        {
            State = EscapeEpisodeState.Resetting;
            PressedButtonCount = 0;
            RemainingTime = episodeDuration;
            m_EpisodeIndex++;
            var seed = seedOverride ?? (deterministicSeed != 0
                ? deterministicSeed + m_EpisodeIndex
                : unchecked(Environment.TickCount ^ GetInstanceID() ^ (m_EpisodeIndex * 397)));
            m_Random = new System.Random(seed);

            ResetFloor();
            RandomizeButtons();
            RandomizeGate();
            RandomizeAgents();
            State = EscapeEpisodeState.Running;
        }

        public void Configure(
            Renderer configuredFloorRenderer,
            Material floorDefault,
            Material floorPlayerWin,
            Material floorEnemyWin,
            EscapeBuilding[] configuredBuildings,
            Transform[] configuredSpawnPoints,
            Transform[] configuredGateAnchors,
            GameObject[] configuredGateWallBlockers,
            EscapeButton[] configuredButtons,
            EscapeGate configuredGate,
            EscapePlayerAgent configuredPlayer,
            EscapeEnemyAgent[] configuredEnemies)
        {
            floorRenderer = configuredFloorRenderer;
            floorDefaultMaterial = floorDefault;
            floorPlayerWinMaterial = floorPlayerWin;
            floorEnemyWinMaterial = floorEnemyWin;
            buildings = configuredBuildings;
            spawnPoints = configuredSpawnPoints;
            gateAnchors = configuredGateAnchors;
            gateWallBlockers = configuredGateWallBlockers;
            buttons = configuredButtons;
            gate = configuredGate;
            player = configuredPlayer;
            enemies = configuredEnemies;
        }

        void CompleteEpisode(EscapeEpisodeState result)
        {
            if (!IsRunning)
            {
                return;
            }

            LastResult = result;
            player.Motor.Stop();
            foreach (var enemy in enemies)
            {
                enemy.Motor.Stop();
            }

            if (result == EscapeEpisodeState.PlayerWin)
            {
                player.AddGameReward(1f);
                m_EnemyGroup.AddGroupReward(-1f);
                floorRenderer.sharedMaterial = floorPlayerWinMaterial;
            }
            else
            {
                player.AddGameReward(-1f);
                m_EnemyGroup.AddGroupReward(1f);
                floorRenderer.sharedMaterial = floorEnemyWinMaterial;
            }

            State = EscapeEpisodeState.ResultFeedback;
            m_ResultTimer = IsFastTraining() ? 0f : resultFeedbackDuration;
        }

        void RandomizeButtons()
        {
            var buildingIndices = CreateShuffledIndices(buildings.Length);
            for (var i = 0; i < buttons.Length; i++)
            {
                m_LastButtonBuildingIndices[i] = buildingIndices[i];
                var building = buildings[m_LastButtonBuildingIndices[i]];
                var socket = building.GetSocket(m_Random.Next(0, building.SocketCount));
                var button = buttons[i];
                button.transform.SetPositionAndRotation(socket.position, socket.rotation);
                button.ResetButton();
            }
        }

        void RandomizeGate()
        {
            SelectedGateIndex = m_Random.Next(0, gateAnchors.Length);
            for (var i = 0; i < gateWallBlockers.Length; i++)
            {
                gateWallBlockers[i].SetActive(i != SelectedGateIndex);
            }

            var anchor = gateAnchors[SelectedGateIndex];
            gate.transform.SetPositionAndRotation(anchor.position, anchor.rotation);
            gate.ResetClosed();
        }

        void RandomizeAgents()
        {
            var spawnIndices = CreateShuffledIndices(spawnPoints.Length);
            for (var i = 0; i < m_LastSpawnIndices.Length; i++)
            {
                m_LastSpawnIndices[i] = spawnIndices[i];
            }

            var playerSpawn = spawnPoints[spawnIndices[0]];
            player.ResetForEpisode(playerSpawn.position, RandomRotation());
            for (var i = 0; i < enemies.Length; i++)
            {
                var enemySpawn = spawnPoints[spawnIndices[i + 1]];
                enemies[i].ResetForEpisode(enemySpawn.position, RandomRotation());
            }
        }

        int[] CreateShuffledIndices(int count)
        {
            var indices = new int[count];
            for (var i = 0; i < count; i++)
            {
                indices[i] = i;
            }

            for (var i = count - 1; i > 0; i--)
            {
                var swapIndex = m_Random.Next(i + 1);
                (indices[i], indices[swapIndex]) = (indices[swapIndex], indices[i]);
            }

            return indices;
        }

        Quaternion RandomRotation()
        {
            return Quaternion.Euler(0f, (float)(m_Random.NextDouble() * 360d), 0f);
        }

        void ResetFloor()
        {
            if (floorRenderer != null)
            {
                floorRenderer.sharedMaterial = floorDefaultMaterial;
            }
        }

        void ConfigureRunModeFromCommandLine()
        {
            var mode = defaultRunMode;
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], "-escapeSmokeSeconds", StringComparison.OrdinalIgnoreCase)
                    && float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var smokeSeconds))
                {
                    m_SmokeSecondsRemaining = Mathf.Max(0.1f, smokeSeconds);
                    continue;
                }

                if (!string.Equals(args[i], "-escapeMode", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                mode = args[i + 1].ToLowerInvariant() switch
                {
                    "player" => EscapeRunMode.PlayerTraining,
                    "enemy" => EscapeRunMode.EnemyTraining,
                    "joint" => EscapeRunMode.JointTraining,
                    _ => EscapeRunMode.Human
                };
            }

            player.SetControlMode(mode switch
            {
                EscapeRunMode.PlayerTraining => EscapePlayerControlMode.Training,
                EscapeRunMode.EnemyTraining => EscapePlayerControlMode.AutonomousHeuristic,
                EscapeRunMode.JointTraining => EscapePlayerControlMode.Training,
                _ => EscapePlayerControlMode.Human
            });

            var trainEnemies = mode is EscapeRunMode.EnemyTraining or EscapeRunMode.JointTraining;
            foreach (var enemy in enemies)
            {
                enemy.SetTraining(trainEnemies);
            }
        }

        static bool IsFastTraining()
        {
            return Application.isBatchMode || (Academy.IsInitialized && Academy.Instance.IsCommunicatorOn);
        }
    }
}

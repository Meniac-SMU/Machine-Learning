using System.Collections.Generic;
using UnityEngine;

namespace MachineLearning.Escape
{
    public static class EscapeMapLayout
    {
        public const float MapSize = 96f;
        public const float MapHalfExtent = 48f;
        public const float BuildingSize = 11f;
        public const float BuildingHeight = 6f;
        public const float BuildingSpacing = 18f;
        public const float RoadWidth = 7f;
        public const int BuildingGridSize = 5;
        public const int BuildingCount = BuildingGridSize * BuildingGridSize;
        public const int SpawnGridSize = 6;
        public const int SpawnPointCount = SpawnGridSize * SpawnGridSize;
        public const int GateAnchorCount = 4;
        public const int ButtonCount = 5;
        public const int RequiredButtonCount = 3;

        static readonly float[] s_BuildingCoordinates = { -36f, -18f, 0f, 18f, 36f };
        static readonly float[] s_SpawnCoordinates = { -45f, -27f, -9f, 9f, 27f, 45f };

        public static IReadOnlyList<float> BuildingCoordinates => s_BuildingCoordinates;
        public static IReadOnlyList<float> SpawnCoordinates => s_SpawnCoordinates;

        public static Vector3 GetBuildingPosition(int index)
        {
            var row = index / BuildingGridSize;
            var column = index % BuildingGridSize;
            return new Vector3(s_BuildingCoordinates[column], BuildingHeight * 0.5f, s_BuildingCoordinates[row]);
        }

        public static Vector3 GetSpawnPosition(int index, float height = 1f)
        {
            var row = index / SpawnGridSize;
            var column = index % SpawnGridSize;
            return new Vector3(s_SpawnCoordinates[column], height, s_SpawnCoordinates[row]);
        }
    }
}

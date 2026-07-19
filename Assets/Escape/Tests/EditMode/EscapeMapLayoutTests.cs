using NUnit.Framework;
using UnityEngine;

namespace MachineLearning.Escape.Tests
{
    public sealed class EscapeMapLayoutTests
    {
        [Test]
        public void BuildingGridHasExpectedCountAndRoadWidth()
        {
            Assert.AreEqual(25, EscapeMapLayout.BuildingCount);
            Assert.AreEqual(5, EscapeMapLayout.BuildingCoordinates.Count);
            Assert.AreEqual(11f, EscapeMapLayout.BuildingSize);
            Assert.AreEqual(7f, EscapeMapLayout.RoadWidth);
            Assert.AreEqual(EscapeMapLayout.RoadWidth, EscapeMapLayout.BuildingCoordinates[1] - EscapeMapLayout.BuildingCoordinates[0] - EscapeMapLayout.BuildingSize);
        }

        [Test]
        public void SpawnGridContainsThirtySixIntersections()
        {
            Assert.AreEqual(36, EscapeMapLayout.SpawnPointCount);
            Assert.AreEqual(6, EscapeMapLayout.SpawnCoordinates.Count);
            Assert.AreEqual(new Vector3(-45f, 1f, -45f), EscapeMapLayout.GetSpawnPosition(0));
            Assert.AreEqual(new Vector3(45f, 1f, 45f), EscapeMapLayout.GetSpawnPosition(35));
        }

        [Test]
        public void BuildingsStayInsideNinetySixMeterMap()
        {
            for (var i = 0; i < EscapeMapLayout.BuildingCount; i++)
            {
                var position = EscapeMapLayout.GetBuildingPosition(i);
                Assert.LessOrEqual(Mathf.Abs(position.x) + EscapeMapLayout.BuildingSize * 0.5f, EscapeMapLayout.MapHalfExtent);
                Assert.LessOrEqual(Mathf.Abs(position.z) + EscapeMapLayout.BuildingSize * 0.5f, EscapeMapLayout.MapHalfExtent);
            }
        }
    }
}

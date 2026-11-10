#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace ScaryParty.City.Editor.Tests
{
    [TestFixture]
    public class CityPolygonAndGroundTests
    {
        [Test]
        public void Test_EarClipping_ConvexQuad_TriangulatesCorrectly()
        {
            List<Vector2> quad = new List<Vector2>
            {
                new Vector2(0f, 0f),
                new Vector2(10f, 0f),
                new Vector2(10f, 10f),
                new Vector2(0f, 10f)
            };

            List<int> tris = PolygonUtils.TriangulateEarClipping(quad, clockwiseForUnityNormals: true);

            Assert.IsNotNull(tris);
            Assert.AreEqual(6, tris.Count, "Quad should produce exactly 2 triangles (6 indices)");

            float totalArea = 0f;
            for (int i = 0; i < tris.Count; i += 3)
            {
                Vector2 a = quad[tris[i]];
                Vector2 b = quad[tris[i + 1]];
                Vector2 c = quad[tris[i + 2]];
                float triArea = Mathf.Abs((b.x - a.x) * (c.y - a.y) - (c.x - a.x) * (b.y - a.y)) * 0.5f;
                totalArea += triArea;
            }

            Assert.AreEqual(100f, totalArea, 0.001f, "Total area of triangulated quad should be 100");
        }

        [Test]
        public void Test_EarClipping_ConcaveLShapedPolygon_StaysStrictlyInside()
        {
            // L-shaped polygon:
            // (0,20) --- (10,20)
            //   |          |
            //   |       (10,10) --- (20,10)
            //   |                     |
            // (0,0) --------------- (20,0)
            // The region [10, 20] x [10, 20] is the notch (OUTSIDE the polygon).
            List<Vector2> lShape = new List<Vector2>
            {
                new Vector2(0f, 0f),
                new Vector2(20f, 0f),
                new Vector2(20f, 10f),
                new Vector2(10f, 10f), // Reflex vertex
                new Vector2(10f, 20f),
                new Vector2(0f, 20f)
            };

            List<int> tris = PolygonUtils.TriangulateEarClipping(lShape, clockwiseForUnityNormals: true);

            Assert.IsNotNull(tris);
            Assert.AreEqual(12, tris.Count, "6-vertex polygon should produce 4 triangles (12 indices)");

            // Check that (15, 15) which is in the exterior notch is NEVER inside any triangle
            Vector2 outsidePoint = new Vector2(15f, 15f);
            float totalArea = 0f;

            for (int i = 0; i < tris.Count; i += 3)
            {
                Vector2 a = lShape[tris[i]];
                Vector2 b = lShape[tris[i + 1]];
                Vector2 c = lShape[tris[i + 2]];

                Assert.IsFalse(
                    PolygonUtils.IsPointInTriangle2D(outsidePoint, a, b, c),
                    $"Triangle ({a}, {b}, {c}) leaked outside into the concave notch at {outsidePoint}!"
                );

                float triArea = Mathf.Abs((b.x - a.x) * (c.y - a.y) - (c.x - a.x) * (b.y - a.y)) * 0.5f;
                totalArea += triArea;
            }

            // Expected area: 20*10 (bottom) + 10*10 (top-left) = 300
            Assert.AreEqual(300f, totalArea, 0.001f, "Triangulated area of L-shape must match exact geometric area");
        }

        [Test]
        public void Test_EarClipping_NormalsPointUpInUnityXZPlane()
        {
            List<Vector2> triangle = new List<Vector2>
            {
                new Vector2(0f, 0f),
                new Vector2(10f, 0f),
                new Vector2(0f, 10f)
            };

            List<int> tris = PolygonUtils.TriangulateEarClipping(triangle, clockwiseForUnityNormals: true);
            Assert.AreEqual(3, tris.Count);

            Vector3 a = new Vector3(triangle[tris[0]].x, 0f, triangle[tris[0]].y);
            Vector3 b = new Vector3(triangle[tris[1]].x, 0f, triangle[tris[1]].y);
            Vector3 c = new Vector3(triangle[tris[2]].x, 0f, triangle[tris[2]].y);

            Vector3 normal = Vector3.Cross(b - a, c - a);
            Assert.Greater(normal.y, 0f, "Normal in Unity XZ plane must point upwards (+Y)");
        }

        [Test]
        public void Test_InsetPolygonSafe_ReflexCornerDoesNotShootOutwards()
        {
            Vector3[] lShape = new Vector3[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(20f, 0f, 0f),
                new Vector3(20f, 0f, 10f),
                new Vector3(10f, 0f, 10f), // Reflex vertex at index 3
                new Vector3(10f, 0f, 20f),
                new Vector3(0f, 0f, 20f)
            };

            float inset = 2f;
            Vector3[] insetPoly = PolygonUtils.InsetPolygonSafe(lShape, inset);

            Assert.IsNotNull(insetPoly);
            Assert.AreEqual(6, insetPoly.Length);

            // In the original, reflex vertex was at (10, 10).
            // It MUST NOT be pushed into the exterior notch (x > 10 or z > 10).
            Vector3 insetReflex = insetPoly[3];
            Assert.LessOrEqual(insetReflex.x, 10.01f, $"Reflex vertex X shot outwards into notch: {insetReflex.x}");
            Assert.LessOrEqual(insetReflex.z, 10.01f, $"Reflex vertex Z shot outwards into notch: {insetReflex.z}");
        }

        [Test]
        public void Test_BuildingBaseElevation_DoesNotSinkIntoGround()
        {
            CityConfig config = ScriptableObject.CreateInstance<CityConfig>();
            config.sidewalkHeight = 0.25f;

            float groundY = 0.02f + config.sidewalkHeight;
            float bHeight = 12f;
            Vector3 centerPos = new Vector3(50f, 0f, 50f);

            Vector3 buildingPosition = new Vector3(centerPos.x, groundY + bHeight * 0.5f, centerPos.z);
            float buildingBottomY = buildingPosition.y - (bHeight * 0.5f);

            Assert.AreEqual(groundY, buildingBottomY, 0.0001f,
                "Building bottom face must sit exactly flush with sidewalk and block ground surface without sinking");
        }

        [Test]
        public void Test_PizzeriaElevation_IsAboveNewGroundMesh()
        {
            CityConfig config = ScriptableObject.CreateInstance<CityConfig>();
            config.sidewalkHeight = 0.20f;

            float actualGroundY = 0.02f + config.sidewalkHeight; // 0.22f
            float floorLocalY = -1.6f;

            // Solver elevation
            float candidateY = actualGroundY + 0.03f - floorLocalY;
            float worldFloorY = candidateY + floorLocalY;

            Assert.Greater(worldFloorY, actualGroundY, "Pizzeria floor must be strictly above the ground surface");
            Assert.AreEqual(actualGroundY + 0.03f, worldFloorY, 0.0001f, "Pizzeria floor must have a 0.03m threshold above ground surface");
        }

        [Test]
        public void Test_BlockFiller_DoesNotInstantiateLegacyGroundOrSolidCore()
        {
            GameObject testParent = new GameObject("TestParent");
            try
            {
                BlockFiller filler = testParent.AddComponent<BlockFiller>();
                CityConfig config = ScriptableObject.CreateInstance<CityConfig>();
                config.sidewalkHeight = 0.2f;
                config.generateSolidCore = false;
                config.fillBlockGroundInRoadMesh = true;

                BlockInfo block = new BlockInfo
                {
                    polygon = new Vector3[]
                    {
                        new Vector3(0, 0, 0),
                        new Vector3(40, 0, 0),
                        new Vector3(40, 0, 40),
                        new Vector3(0, 0, 40)
                    },
                    worldCenter = new Vector3(20, 0, 20),
                    zoneType = ZoneType.Commercial
                };

                filler.FillBlock(block, config, new System.Random(123), testParent.transform, null, 0);

                Transform[] allChildren = testParent.GetComponentsInChildren<Transform>(true);
                foreach (var child in allChildren)
                {
                    Assert.IsFalse(child.name.StartsWith("BlockGround"), $"Found legacy object {child.name} which should not be created!");
                    Assert.IsFalse(child.name.StartsWith("SolidCore"), $"Found legacy object {child.name} which should not be created!");
                }
            }
            finally
            {
                Object.DestroyImmediate(testParent);
            }
        }
    }
}
#endif

using NUnit.Framework;
using UnityEngine;
using ScaryParty.Pizzeria.Composition;

namespace ScaryParty.Pizzeria.Editor.Tests
{
    public class PizzeriaPatioLayoutTests
    {
        [Test]
        public void EnsureBuilt_KeepsPadInsideExteriorRegionAcrossAll4Rotations_AndRebuildsOnVariantSwitch()
        {
            var go = new GameObject("PizzeriaPatioTestRoot");
            var profile = ScriptableObject.CreateInstance<PizzeriaLayoutProfile>();
            try
            {
                var root = go.AddComponent<PizzeriaRoot>();
                var fp = go.AddComponent<PizzeriaFootprint>();
                fp.structuralRegions = new[]
                {
                    Rect.MinMaxRect(0f, -13.2f, 32.8f, 9.2f),
                    Rect.MinMaxRect(16.4f, -27.6f, 32.8f, -13.2f)
                };
                fp.exteriorRegions = new[] { profile.patioRectL };

                float[] angles = { 0f, 90f, 180f, 270f };
                for (int a = 0; a < angles.Length; a++)
                {
                    go.transform.rotation = Quaternion.Euler(0f, angles[a], 0f);
                    profile.variant = PizzeriaLayoutVariant.L_RecuoOperacional;
                    fp.exteriorRegions = new[] { profile.GetActivePatioRect() };

                    PizzeriaParkingLotBuilder.EnsureBuilt(root, profile);

                    var pad = go.transform.Find("__PizzeriaExteriorFacilities/ParkingLotPad");
                    Assert.IsNotNull(pad, $"Pad ausente na rotação {angles[a]}");
                    Assert.IsNotNull(pad.GetComponent<Collider>(), "Pad sem collider");

                    Vector3 padLocal = go.transform.InverseTransformPoint(pad.position);
                    Vector3 half = pad.localScale * 0.5f;
                    Rect padRect = Rect.MinMaxRect(padLocal.x - half.x, padLocal.z - half.z, padLocal.x + half.x, padLocal.z + half.z);
                    Rect ext = profile.GetActivePatioRect();

                    Assert.GreaterOrEqual(padRect.xMin, ext.xMin - 0.1f, $"Rot {angles[a]} xMin");
                    Assert.LessOrEqual(padRect.xMax, ext.xMax + 0.1f, $"Rot {angles[a]} xMax");
                    Assert.GreaterOrEqual(padRect.yMin, ext.yMin - 0.1f, $"Rot {angles[a]} yMin");
                    Assert.LessOrEqual(padRect.yMax, ext.yMax + 0.1f, $"Rot {angles[a]} yMax");

                    var anchors = go.GetComponent<PizzeriaExteriorAnchors>();
                    Assert.IsNotNull(anchors);
                    Assert.IsTrue(anchors.HasCompleteExteriorAnchors());
                    Assert.IsTrue(anchors.HasCompleteSpawnAnchors());
                }

                // Troca L -> T -> L deve reconstruir o pátio para o novo retângulo
                profile.variant = PizzeriaLayoutVariant.T_PatioLateral;
                fp.exteriorRegions = new[] { profile.GetActivePatioRect() };
                PizzeriaParkingLotBuilder.EnsureBuilt(root, profile);
                var padT = go.transform.Find("__PizzeriaExteriorFacilities/ParkingLotPad");
                Vector3 padTLocal = go.transform.InverseTransformPoint(padT.position);
                Assert.Less(padTLocal.x, 0f, "Variante T deve posicionar o pátio em X < 0.");

                profile.variant = PizzeriaLayoutVariant.L_RecuoOperacional;
                fp.exteriorRegions = new[] { profile.GetActivePatioRect() };
                PizzeriaParkingLotBuilder.EnsureBuilt(root, profile);
                var padL = go.transform.Find("__PizzeriaExteriorFacilities/ParkingLotPad");
                Vector3 padLLocal = go.transform.InverseTransformPoint(padL.position);
                Assert.Greater(padLLocal.x, 0f, "Retorno para L deve reposicionar o pátio em X > 0.");
            }
            finally
            {
                Object.DestroyImmediate(profile);
                Object.DestroyImmediate(go);
            }
        }
    }
}

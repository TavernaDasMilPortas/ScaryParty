#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ScaryParty.City.Modular;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScaryParty.VisualStyles.Editor.Tests
{
    public class VisualStylePhase2And3Tests
    {
        [Test]
        public void PreviewRenderer_RendersFourDefaultSamples_NonEmptyNonMagenta_AndZeroDiskWrites()
        {
            VisualStylePresetSeeder.EnsureAllDefaultsExist();
            var sourceProfile = VisualStylePresetSeeder.LoadPreset(VisualStylePresetType.SoftToonVibrant);
            var ruleSet = VisualStylePresetSeeder.EnsureDefaultRuleSet();
            Assert.IsNotNull(sourceProfile);
            Assert.IsNotNull(ruleSet);

            string profileDiskPath = AssetDatabase.GetAssetPath(sourceProfile);
            string preProfileBytes = File.ReadAllText(profileDiskPath);
            string preHash = sourceProfile.ComputeContentHash();
            int generatedFilesBefore = Directory.Exists(VisualStyleMaterialFactory.GeneratedRootFolder)
                ? Directory.GetFiles(VisualStyleMaterialFactory.GeneratedRootFolder, "*", SearchOption.AllDirectories).Length
                : 0;

            var workingClone = sourceProfile.CloneTransient();
            VisualStylePreviewRenderer renderer = null;
            try
            {
                renderer = new VisualStylePreviewRenderer
                {
                    Resolution = 256,
                    ComparisonMode = VisualPreviewComparisonMode.SideBySideAB,
                    LightingMode = VisualLightingPreviewMode.Interior
                };
                renderer.InitializeDefaultSlots(workingClone, ruleSet);

                Assert.AreEqual(4, renderer.Slots.Count, "Preview Lab must initialize 4 canonical sample slots.");
                Assert.AreEqual(4, renderer.ActivePreviewSceneCount);

                renderer.RenderAllSlots(workingClone);

                for (int i = 0; i < renderer.Slots.Count; i++)
                {
                    var slot = renderer.Slots[i];
                    Assert.IsNotNull(slot.toonTexture, $"Slot '{slot.title}' should have Toon RenderTexture.");
                    Assert.IsNotNull(slot.originalTexture, $"Slot '{slot.title}' should have Original RenderTexture.");
                    AssertValidRenderedPixels(slot.toonTexture, $"{slot.title} (Toon)");
                    AssertValidRenderedPixels(slot.originalTexture, $"{slot.title} (Original)");
                }

                // Live parameter tuning + lighting/camera/recipe/pose sweeps without writing disk files
                workingClone.lighting.lightSteps = 2;
                workingClone.outlines.outlineMode = VisualStyleOutlineMode.MaterialOutline;
                workingClone.outlines.outlineWidth = 0.09f;
                workingClone.colorPalette.saturationShift = 0.25f;

                renderer.LightingMode = VisualLightingPreviewMode.Exterior;
                renderer.ComparisonMode = VisualPreviewComparisonMode.SplitAB;
                renderer.GlobalCameraMode = VisualPreviewCameraMode.Isometric;

                // Test Character pose & Modular Building recipe switch
                var charSlot = renderer.Slots[2];
                renderer.ApplyCharacterPoseSample(charSlot.subjectGo, 1);
                charSlot.swatchIndex = 2;

                var bldgSlot = renderer.Slots[3];
                bldgSlot.buildingRecipe = ModularBuildingRecipe.Shop;
                renderer.RebuildSlotSubject(bldgSlot, ruleSet);

                renderer.RebuildAllTransientMaterials(workingClone, ruleSet);
                renderer.RenderAllSlots(workingClone);

                AssertValidRenderedPixels(charSlot.toonTexture, "Character Pose+Swatch Toon");
                AssertValidRenderedPixels(bldgSlot.toonTexture, "ModularBuilding Shop Toon");

                // Assert zero writes to source profile or Generated folder
                Assert.AreEqual(preHash, sourceProfile.ComputeContentHash(), "Previewing must never mutate source ScriptableObject.");
                Assert.AreEqual(preProfileBytes, File.ReadAllText(profileDiskPath), "Previewing must never write source asset on disk.");

                int generatedFilesAfter = Directory.Exists(VisualStyleMaterialFactory.GeneratedRootFolder)
                    ? Directory.GetFiles(VisualStyleMaterialFactory.GeneratedRootFolder, "*", SearchOption.AllDirectories).Length
                    : 0;
                Assert.AreEqual(generatedFilesBefore, generatedFilesAfter, "Previewing must not create files under Generated/.");
            }
            finally
            {
                renderer?.Dispose();
                UnityEngine.Object.DestroyImmediate(workingClone);
            }

            Assert.AreEqual(0, CountLeakedPreviewRenderTextures(), "Dispose must release all preview RenderTextures.");
            Assert.AreEqual(0, CountLeakedPreviewRoots(), "Dispose must close/destroy all preview scene roots.");
        }

        [Test]
        public void PreviewRenderer_ArbitraryAddedSampleMesh_RendersAndRemovesWithoutLeaks()
        {
            VisualStylePresetSeeder.EnsureAllDefaultsExist();
            var profile = VisualStylePresetSeeder.LoadPreset(VisualStylePresetType.AnimeSoft).CloneTransient();
            var ruleSet = VisualStylePresetSeeder.EnsureDefaultRuleSet();

            var sphereGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var tempMesh = UnityEngine.Object.Instantiate(sphereGo.GetComponent<MeshFilter>().sharedMesh);
            tempMesh.name = "ArbitraryRuntimePreviewMesh";
            UnityEngine.Object.DestroyImmediate(sphereGo);

            VisualStylePreviewRenderer renderer = null;
            try
            {
                renderer = new VisualStylePreviewRenderer { Resolution = 192 };
                renderer.InitializeDefaultSlots(profile, ruleSet);

                var customSlot = renderer.AddCustomSampleSlot("DynamicCustomMesh", tempMesh, profile, ruleSet);
                Assert.AreEqual(5, renderer.Slots.Count);
                Assert.GreaterOrEqual(customSlot.targets.Count, 1);

                renderer.RenderSlot(customSlot, profile);
                AssertValidRenderedPixels(customSlot.toonTexture, "CustomSampleMesh (Toon)");

                Assert.IsTrue(renderer.RemoveSlot(customSlot.slotId));
                Assert.AreEqual(4, renderer.Slots.Count);
            }
            finally
            {
                renderer?.Dispose();
                UnityEngine.Object.DestroyImmediate(tempMesh);
                UnityEngine.Object.DestroyImmediate(profile);
            }

            Assert.AreEqual(0, CountLeakedPreviewRenderTextures());
            Assert.AreEqual(0, CountLeakedPreviewRoots());
        }

        [Test]
        public void WizardWindow_OpenBindClose_ZeroLeakedResources()
        {
            VisualStylePresetSeeder.EnsureAllDefaultsExist();
            GlobalCelShadingWizard wnd = null;
            try
            {
                wnd = GlobalCelShadingWizard.OpenForTests();
                Assert.IsNotNull(wnd);
                Assert.IsNotNull(wnd.WorkingProfileForTests);
                Assert.IsNotNull(wnd.PreviewRendererForTests);
                Assert.AreEqual(4, wnd.PreviewRendererForTests.Slots.Count);

                Assert.IsNotNull(wnd.rootVisualElement.Q<VisualElement>("preview-grid-container"));
                Assert.IsNotNull(wnd.rootVisualElement.Q<ScrollView>("settings-scroll"));
                Assert.IsNotNull(wnd.rootVisualElement.Q<Button>("btn-generate"));
                Assert.IsNotNull(wnd.rootVisualElement.Q<Button>("btn-apply"));
                Assert.IsNotNull(wnd.rootVisualElement.Q<Button>("btn-restore-original"));
                Assert.IsNotNull(wnd.rootVisualElement.Q<Button>("btn-export-report"));
            }
            finally
            {
                if (wnd != null) wnd.Close();
            }

            Assert.AreEqual(0, CountLeakedPreviewRenderTextures(), "Closing Wizard must leak zero RenderTextures.");
            Assert.AreEqual(0, CountLeakedPreviewRoots(), "Closing Wizard must leak zero preview root GameObjects.");
        }

        private static void AssertValidRenderedPixels(RenderTexture rt, string label)
        {
            RenderTexture prev = RenderTexture.active;
            Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                tex.Apply();

                Color[] pixels = tex.GetPixels();
                Color bg = new Color(0.14f, 0.16f, 0.20f, 1f);
                int nonBgCount = 0;
                int magentaErrorCount = 0;

                // Sample every pixel cleanly (256x256 = 65k pixels, < 2ms)
                for (int i = 0; i < pixels.Length; i++)
                {
                    Color p = pixels[i];
                    if (Mathf.Abs(p.r - bg.r) + Mathf.Abs(p.g - bg.g) + Mathf.Abs(p.b - bg.b) > 0.04f)
                    {
                        nonBgCount++;
                    }
                    if (p.r > 0.90f && p.g < 0.08f && p.b > 0.90f)
                    {
                        magentaErrorCount++;
                    }
                }

                Assert.Greater(nonBgCount, 20, $"Rendered preview '{label}' appears empty (all background color).");
                Assert.AreEqual(0, magentaErrorCount, $"Rendered preview '{label}' contains magenta broken-shader pixels.");
            }
            finally
            {
                RenderTexture.active = prev;
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        private static int CountLeakedPreviewRenderTextures()
        {
            return Resources.FindObjectsOfTypeAll<RenderTexture>()
                .Count(r => r != null && r.name != null && r.name.StartsWith("RT_Preview_", StringComparison.Ordinal));
        }

        private static int CountLeakedPreviewRoots()
        {
            return Resources.FindObjectsOfTypeAll<GameObject>()
                .Count(go => go != null && go.name != null && go.name.StartsWith("__VisualPreviewRoot_", StringComparison.Ordinal));
        }
    }
}
#endif

#if UNITY_EDITOR
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ScaryParty.VisualStyles.Editor.Tests
{
    public class VisualStylePhase1Tests
    {
        [Test]
        public void VisualStyleProfile_SerializeAndCloneIsolation_PreservesPreset()
        {
            var original = ScriptableObject.CreateInstance<VisualStyleProfile>();
            original.ApplyPresetDefaults(VisualStylePresetType.SoftToonVibrant);
            string expectedHash = original.ComputeContentHash();

            VisualStyleProfile clone = original.CloneTransient();
            try
            {
                Assert.AreEqual(original.styleId, clone.styleId);
                Assert.AreEqual(original.lighting.lightSteps, clone.lighting.lightSteps);

                clone.lighting.lightSteps = 5;
                clone.colorPalette.saturationShift = -0.5f;

                Assert.AreEqual(3, original.lighting.lightSteps);
                Assert.AreEqual(expectedHash, original.ComputeContentHash(), "Original profile hash must not change when clone is edited.");
                Assert.AreNotEqual(expectedHash, clone.ComputeContentHash());
            }
            finally
            {
                Object.DestroyImmediate(original);
                Object.DestroyImmediate(clone);
            }
        }

        [Test]
        public void VisualConversionRuleSet_PriorityOrder_RespectsExclusionOverrideMetadataRuleHeuristicUnclassified()
        {
            var ruleSet = ScriptableObject.CreateInstance<VisualConversionRuleSet>();
            try
            {
                VisualConversionRuleSet.PopulateDefaults(ruleSet);
                ruleSet.SetOrUpdateManualOverride(new VisualSlotOverride
                {
                    sourceAssetGuid = "guid-123",
                    sourceLocalFileId = 42,
                    slotIndex = 1,
                    overrideCategoryId = VisualCategoryIds.Characters,
                    overrideRole = VisualSemanticRole.PlayerApron,
                    overrideShaderFamily = IllustrateShaderFamily.Simple,
                    overrideAction = VisualConversionAction.Convert,
                    notes = "Explicit apron slot override"
                });

                // 1. Exclusion wins even when asset path looks like food/city
                var uiSlot = new VisualAssetSlotDescriptor
                {
                    sourceMaterialGuid = "guid-ui",
                    shaderName = "TextMeshPro/Distance Field",
                    materialName = "PizzaFont"
                };
                var uiDecision = ruleSet.Evaluate(in uiSlot);
                Assert.AreEqual(VisualConversionAction.Preserve, uiDecision.action);
                Assert.AreEqual(1, uiDecision.priorityTier);

                // 2. Manual Override wins over metadata & user rules
                var ovSlot = new VisualAssetSlotDescriptor
                {
                    sourceMaterialGuid = "guid-123",
                    sourceLocalFileId = 42,
                    slotIndex = 1,
                    shaderName = "Universal Render Pipeline/Lit",
                    hasDeclaredMetadata = true,
                    declaredCategoryId = VisualCategoryIds.Props,
                    declaredRole = VisualSemanticRole.PizzeriaProp
                };
                var ovDecision = ruleSet.Evaluate(in ovSlot);
                Assert.AreEqual(VisualSemanticRole.PlayerApron, ovDecision.semanticRole);
                Assert.AreEqual(2, ovDecision.priorityTier);

                // 3. Custom category added purely as asset data without recompiling C#
                ruleSet.categories.Add(new VisualCategoryDefinition
                {
                    categoryId = "CustomVehicles",
                    displayName = "Delivery Vehicles",
                    defaultShaderFamily = IllustrateShaderFamily.Opaque,
                    defaultSemanticRole = VisualSemanticRole.Metal,
                    defaultAction = VisualConversionAction.Convert
                });
                ruleSet.rules.Add(new VisualClassificationRule
                {
                    ruleName = "Delivery Scooters",
                    priority = 5,
                    materialNameRegex = "Scooter",
                    targetCategoryId = "CustomVehicles",
                    targetRole = VisualSemanticRole.Metal,
                    targetShaderFamily = IllustrateShaderFamily.Opaque,
                    confidence = 0.95f
                });

                var customSlot = new VisualAssetSlotDescriptor
                {
                    sourceMaterialGuid = "guid-scooter",
                    materialName = "Mat_Scooter_Paint",
                    shaderName = "Universal Render Pipeline/Lit"
                };
                var customDecision = ruleSet.Evaluate(in customSlot);
                Assert.AreEqual("CustomVehicles", customDecision.categoryId);
                Assert.AreEqual(VisualConversionAction.Convert, customDecision.action);

                // 4. Unknown custom shader without match goes to Unclassified / ManualReview
                var unknownSlot = new VisualAssetSlotDescriptor
                {
                    sourceMaterialGuid = "guid-mystery",
                    materialName = "MysteryMat",
                    shaderName = "Custom/WeirdProprietaryShader"
                };
                var unknownDecision = ruleSet.Evaluate(in unknownSlot);
                Assert.AreEqual(VisualCategoryIds.Unclassified, unknownDecision.categoryId);
                Assert.AreEqual(VisualConversionAction.ManualReview, unknownDecision.action);
            }
            finally
            {
                Object.DestroyImmediate(ruleSet);
            }
        }

        [Test]
        public void VisualStyleShaderUtility_ApplyColorBlockToSlot_PreservesExistingProperties()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var mat = new Material(Shader.Find(VisualStyleShaderUtility.ShaderOpaque) ?? Shader.Find("Universal Render Pipeline/Lit"));
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = mat;

            try
            {
                int customPropId = Shader.PropertyToID("_LightRamp");
                var initialBlock = new MaterialPropertyBlock();
                initialBlock.SetFloat(customPropId, 0.42f);
                renderer.SetPropertyBlock(initialBlock, 0);

                Color expectedColor = new Color(0.9f, 0.25f, 0.2f, 1f);
                VisualStyleShaderUtility.ApplyColorBlockToSlot(renderer, 0, expectedColor);

                var readBack = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(readBack, 0);

                Assert.AreEqual(0.42f, readBack.GetFloat(customPropId), 0.001f, "Pre-existing MPB properties must be preserved.");
                Color mainCol = readBack.GetColor(VisualStyleShaderUtility.MainColorId);
                Color baseCol = readBack.GetColor(VisualStyleShaderUtility.BaseColorId);
                Assert.AreEqual(expectedColor.r, mainCol.r, 0.001f);
                Assert.AreEqual(expectedColor.g, mainCol.g, 0.001f);
                Assert.AreEqual(expectedColor.b, mainCol.b, 0.001f);
                Assert.AreEqual(expectedColor.a, mainCol.a, 0.001f);
                Assert.AreEqual(expectedColor.r, baseCol.r, 0.001f);
                Assert.AreEqual(expectedColor.g, baseCol.g, 0.001f);
                Assert.AreEqual(expectedColor.b, baseCol.b, 0.001f);
                Assert.AreEqual(expectedColor.a, baseCol.a, 0.001f);
            }
            finally
            {
                Object.DestroyImmediate(mat);
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void VisualStylePresetSeeder_CreatesAndLoadsFourPresetsAndRuleSet()
        {
            VisualStylePresetSeeder.EnsureAllDefaultsExist();

            var soft = VisualStylePresetSeeder.LoadPreset(VisualStylePresetType.SoftToonVibrant);
            var anime = VisualStylePresetSeeder.LoadPreset(VisualStylePresetType.AnimeSoft);
            var comic = VisualStylePresetSeeder.LoadPreset(VisualStylePresetType.ComicBold);
            var flat = VisualStylePresetSeeder.LoadPreset(VisualStylePresetType.FlatGraphic);
            var dispatch = VisualStylePresetSeeder.LoadPreset(VisualStylePresetType.DispatchInspired);
            var ruleSet = AssetDatabase.LoadAssetAtPath<VisualConversionRuleSet>(VisualStylePresetSeeder.RuleSetPath);
            var runtimeCfg = AssetDatabase.LoadAssetAtPath<VisualStyleRuntimeConfig>(VisualStylePresetSeeder.RuntimeConfigAssetPath);

            Assert.IsNotNull(soft);
            Assert.IsNotNull(anime);
            Assert.IsNotNull(comic);
            Assert.IsNotNull(flat);
            Assert.IsNotNull(dispatch);
            Assert.IsNotNull(ruleSet);
            Assert.IsNotNull(runtimeCfg);
            Assert.AreEqual("soft_toon_vibrant", soft.styleId);
            Assert.AreEqual("anime_soft", anime.styleId);
            Assert.AreEqual("comic_bold", comic.styleId);
            Assert.AreEqual("flat_graphic", flat.styleId);
            Assert.AreEqual("dispatch_inspired", dispatch.styleId);
            Assert.AreEqual(VisualStyleRuntimeConfig.CanonicalGameplaySceneName, VisualStyleRuntimeConfig.GetGameplaySceneName());
        }
    }
}
#endif

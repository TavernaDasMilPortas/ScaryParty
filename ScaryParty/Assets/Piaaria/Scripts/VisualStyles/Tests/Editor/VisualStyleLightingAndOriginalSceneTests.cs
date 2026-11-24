#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ScaryParty.City.Modular;
using ScaryParty.Lighting;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ScaryParty.VisualStyles.Editor.Tests
{
    /// <summary>
    /// Verificacoes automatizadas EditMode para o plano integrado de iluminacao cartoon (v4.0):
    /// separacao de rota vs estilo (Modos A/B/C), troca ao vivo sem vazamento entre presets,
    /// contratos e fallback B0-B3, ciclo Dia/Noite, janelas deterministicas da cidade,
    /// zonas internas da pizzaria preservando _MainColor, relogio NGO, orcamento URP 2048
    /// e auditorias estaticas de menu/RuntimeSafety.
    /// </summary>
    public class VisualStyleLightingAndOriginalSceneTests
    {
        private VisualStyleProfile _dispatch;
        private VisualStyleProfile _softToon;

        [SetUp]
        public void SetUp()
        {
            VisualStylePresetSeeder.EnsureAllDefaultsExist();
            _dispatch = VisualStylePresetSeeder.LoadPreset(VisualStylePresetType.DispatchInspired);
            _softToon = VisualStylePresetSeeder.LoadPreset(VisualStylePresetType.SoftToonVibrant);
            VisualStyleRuntimeConfig.ClearSessionOverride();
            LightingFeatureRegistry.ResetToDefaultRealtimeOnly();
            LightingShaderGlobals.ResetGlobals();
        }

        [TearDown]
        public void TearDown()
        {
            VisualStyleRuntimeConfig.ClearSessionOverride();
            LightingFeatureRegistry.ResetToDefaultRealtimeOnly();
            LightingShaderGlobals.ResetGlobals();
        }

        private static string ComputeSha256(string path)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "");
        }

        [Test]
        public void RuntimeConfig_RouteAndVisualMode_AreDecoupledAndMigrateLegacyFlags()
        {
            var cfg = ScriptableObject.CreateInstance<VisualStyleRuntimeConfig>();
            try
            {
                cfg.useStyledGameplay = true;
                cfg.activeGameplaySceneName = VisualStyleRuntimeConfig.CanonicalGameplaySceneName;
                cfg.activeProfile = _dispatch;
                cfg.gameplayRoute = GameplaySceneRoute.Original;
                cfg.visualMode = VisualApplicationMode.Disabled;
                cfg.EnsureMigrated();

                Assert.AreEqual(VisualApplicationMode.PersistentDefault, cfg.visualMode);
                Assert.AreEqual(GameplaySceneRoute.Original, cfg.gameplayRoute);

                cfg.visualMode = VisualApplicationMode.RuntimePreview;
                cfg.gameplayRoute = GameplaySceneRoute.Original;

                bool shouldApplyOriginal = cfg.ResolveEffectiveStyleForScene(
                    VisualStyleRuntimeConfig.CanonicalGameplaySceneName,
                    out VisualApplicationMode effectiveMode,
                    out VisualStyleProfile resolvedProfile,
                    out _);

                Assert.IsTrue(shouldApplyOriginal);
                Assert.AreEqual(VisualApplicationMode.RuntimePreview, effectiveMode);
                Assert.AreSame(_dispatch, resolvedProfile);

                VisualStyleRuntimeConfig.SetRuntimePreviewOverride(_softToon, null);
                bool hasSessionOverride = cfg.ResolveEffectiveStyleForScene(
                    VisualStyleRuntimeConfig.CanonicalGameplaySceneName,
                    out _,
                    out VisualStyleProfile overrideProfile,
                    out _);
                Assert.IsTrue(hasSessionOverride);
                Assert.AreSame(_softToon, overrideProfile);

                VisualStyleRuntimeConfig.ClearSessionOverride();
                Assert.IsFalse(VisualStyleRuntimeConfig.HasSessionOverride);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cfg);
            }
        }

        [Test]
        public void VisualStyleManager_LiveSwitch_DispatchToSoftToonToOriginal_RestoresBaseline()
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Shader projectOpaque = Shader.Find(VisualStyleShaderUtility.ShaderOpaque) ?? urpLit;

            var canonicalMat = new Material(urpLit) { name = "CanonicalFloorMat" };
            var dispatchMat = new Material(projectOpaque) { name = "DispatchFloorMat" };
            var softMat = new Material(projectOpaque) { name = "SoftFloorMat" };

            var dispatchLib = ScriptableObject.CreateInstance<VisualMaterialLibrary>();
            dispatchLib.styleId = _dispatch.styleId;
            dispatchLib.mappedMaterials.Add(new MappedMaterialEntry
            {
                sourceMaterial = canonicalMat,
                sourceMaterialName = canonicalMat.name,
                action = VisualConversionAction.Convert,
                targetMaterial = dispatchMat
            });

            var softLib = ScriptableObject.CreateInstance<VisualMaterialLibrary>();
            softLib.styleId = _softToon.styleId;
            softLib.mappedMaterials.Add(new MappedMaterialEntry
            {
                sourceMaterial = canonicalMat,
                sourceMaterialName = canonicalMat.name,
                action = VisualConversionAction.Convert,
                targetMaterial = softMat
            });

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var mgrGo = new GameObject("TestVisualStyleManager");
            bool prevFog = RenderSettings.fog;
            float prevAmbient = RenderSettings.ambientIntensity;

            try
            {
                Renderer rend = go.GetComponent<Renderer>();
                rend.sharedMaterial = canonicalMat;

                VisualStyleManager mgr = mgrGo.AddComponent<VisualStyleManager>();
                mgr.RegisterAsActive();

                // Dispatch -> SoftToon -> Original
                mgr.SetStyle(_dispatch, dispatchLib, applySceneLighting: true, applyToLoadedSceneRenderers: true);
                mgr.ApplyToVisual(go);
                Assert.AreSame(dispatchMat, rend.sharedMaterial, "Dispatch style must bind Dispatch material.");

                mgr.SetStyle(_softToon, softLib, applySceneLighting: true, applyToLoadedSceneRenderers: true);
                mgr.ApplyToVisual(go);
                Assert.AreSame(softMat, rend.sharedMaterial, "Switching Dispatch -> SoftToon must replace with SoftToon material cleanly.");

                mgr.SetStyle(null, null, applySceneLighting: true, applyToLoadedSceneRenderers: false);
                Assert.AreSame(canonicalMat, rend.sharedMaterial, "Restoring original (null profile) must restore canonical material.");
                Assert.AreEqual(prevFog, RenderSettings.fog);
                Assert.AreEqual(prevAmbient, RenderSettings.ambientIntensity, 0.001f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(mgrGo);
                UnityEngine.Object.DestroyImmediate(go);
                UnityEngine.Object.DestroyImmediate(dispatchLib);
                UnityEngine.Object.DestroyImmediate(softLib);
                UnityEngine.Object.DestroyImmediate(canonicalMat);
                UnityEngine.Object.DestroyImmediate(dispatchMat);
                UnityEngine.Object.DestroyImmediate(softMat);
            }
        }

        [Test]
        public void LightingContracts_BackendFallbackToRealtime_AndDebugViewGlobals()
        {
            var stub = new FuturePrecomputedProviderStub(LightingBackendMode.HybridPrepared);
            LightingFeatureRegistry.RegisterProvider(stub);
            try
            {
                var ctx = new LightingContext
                {
                    sceneName = "GameScene",
                    requestedBackend = LightingBackendMode.HybridPrepared,
                    hasPrecomputedCityData = false,
                    hasPrecomputedInteriorData = false
                };

                ILightingContributionProvider resolvedProvider = LightingFeatureRegistry.ResolveProvider(
                    LightingBackendMode.HybridPrepared,
                    ctx,
                    out LightingBackendMode resolvedMode,
                    out string warning);

                Assert.IsNotNull(resolvedProvider);
                Assert.AreEqual(LightingBackendMode.RealtimeStylized, resolvedMode);
                Assert.IsFalse(string.IsNullOrEmpty(warning), "Missing B3 data must emit fallback warning to Solution A.");

                // Now simulate valid B3 data becoming available
                ctx.hasPrecomputedCityData = true;
                LightingFeatureRegistry.ResolveProvider(
                    LightingBackendMode.HybridPrepared,
                    ctx,
                    out LightingBackendMode resolvedWithData,
                    out string warningWithData);
                Assert.AreEqual(LightingBackendMode.HybridPrepared, resolvedWithData);
                Assert.IsTrue(string.IsNullOrEmpty(warningWithData));

                // Debug view global shader switch
                LightingFeatureRegistry.ActiveDebugView = LightingDebugViewMode.AdditionalLightsOnly;
                Assert.AreEqual((float)(int)LightingDebugViewMode.AdditionalLightsOnly, Shader.GetGlobalFloat(LightingShaderGlobals.DebugLightingViewId), 0.01f);

                LightingFeatureRegistry.ActiveDebugView = LightingDebugViewMode.Standard;
                Assert.AreEqual(0f, Shader.GetGlobalFloat(LightingShaderGlobals.DebugLightingViewId), 0.01f);
            }
            finally
            {
                LightingFeatureRegistry.ResetToDefaultRealtimeOnly();
            }
        }

        [Test]
        public void FixedInteriorLightingDataB2_InvalidatesOnLayoutMismatchOrRotation_WithSafeFallback()
        {
            var data = ScriptableObject.CreateInstance<FixedInteriorLightingData>();
            var tempGo = new GameObject("TempPizzeriaTransform");
            try
            {
                data.layoutFingerprint = "L_shape_v1";
                data.expectedRootRotation = Quaternion.identity;
                data.maxRotationDeviationDegrees = 2.5f;
                data.isBakedDataValid = false;

                Assert.IsFalse(data.ValidateAgainstLayout("L_shape_v1", tempGo.transform, out string unbakedReason));
                Assert.IsFalse(string.IsNullOrEmpty(unbakedReason));

                data.isBakedDataValid = true;
                data.bakedZones.Add(new ZonePrecomputedSampleB2 { zoneId = PizzeriaZoneId.Kitchen, bounceStrength = 0.25f });
                Assert.IsTrue(data.ValidateAgainstLayout("L_shape_v1", tempGo.transform, out string validReason));
                Assert.IsTrue(string.IsNullOrEmpty(validReason));

                Assert.IsFalse(data.ValidateAgainstLayout("L_shape_v2_modified", tempGo.transform, out _));

                tempGo.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
                Assert.IsFalse(data.ValidateAgainstLayout("L_shape_v1", tempGo.transform, out _));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tempGo);
                UnityEngine.Object.DestroyImmediate(data);
            }
        }

        [Test]
        public void TimeOfDayProfile_EvaluatesAnchors17h19h22h02h_WithNonZeroFloorAndAmbient()
        {
            var todProfile = ScriptableObject.CreateInstance<TimeOfDayProfile>();
            try
            {
                todProfile.ApplyCartoonDefaults();
                float[] anchors = { 17f, 19f, 22f, 2f };

                foreach (float hour in anchors)
                {
                    LightingEnvironmentState env = todProfile.Evaluate(hour);
                    Assert.GreaterOrEqual(env.minShadowBrightness, 0.18f, $"Shadow floor at {hour}h must remain stylized/readable.");
                    Assert.GreaterOrEqual(env.ambientIntensity, 0.65f, $"Ambient intensity at {hour}h must remain readable.");
                    Assert.Greater(env.sunIntensity, 0.2f, $"Directional intensity at {hour}h must stay > 0.");
                }

                float dayEmission = todProfile.EvaluateCityNightEmission(12f);
                float nightEmission = todProfile.EvaluateCityNightEmission(22f);
                Assert.Greater(nightEmission, dayEmission, "City night window emission weight must exceed midday emission.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(todProfile);
            }
        }

        [Test]
        public void CityLightingDirector_And_PizzeriaLightingController_AreDeterministicAndPreserveMpbMainColor()
        {
            bool litA = CityLightingDirector.ComputeWindowLitState(12345, 3, 987, 1, 2, CityModuleId.B, out float intA, out Color colA);
            bool litB = CityLightingDirector.ComputeWindowLitState(12345, 3, 987, 1, 2, CityModuleId.B, out float intB, out Color colB);
            Assert.AreEqual(litA, litB);
            Assert.AreEqual(intA, intB, 0.0001f);
            Assert.AreEqual(colA, colB);

            // Shopfront ground floor must light up deterministically for many candidates
            bool foundShopfrontLit = false;
            for (int b = 0; b < 10; b++)
            {
                if (CityLightingDirector.ComputeWindowLitState(42, b, b * 17, 0, 0, CityModuleId.D, out float sInt, out _))
                {
                    Assert.Greater(sInt, 1f);
                    foundShopfrontLit = true;
                    break;
                }
            }
            Assert.IsTrue(foundShopfrontLit);

            // Pizzeria controller must neutralize Point shadow casting and preserve _MainColor in MPB
            var pizzeriaGo = new GameObject("TempPizzeriaRoot");
            var pointLightGo = new GameObject("CeilingPoint");
            pointLightGo.transform.SetParent(pizzeriaGo.transform);
            Light point = pointLightGo.AddComponent<Light>();
            point.type = LightType.Point;
            point.shadows = LightShadows.Soft;

            var childBox = GameObject.CreatePrimitive(PrimitiveType.Cube);
            childBox.transform.SetParent(pizzeriaGo.transform);
            childBox.transform.localPosition = Vector3.zero;

            var customMpb = new MaterialPropertyBlock();
            Color preservedTint = new Color(0.85f, 0.22f, 0.14f, 1f);
            customMpb.SetColor("_MainColor", preservedTint);
            Renderer r = childBox.GetComponent<Renderer>();
            r.SetPropertyBlock(customMpb);

            try
            {
                VisualStyleRuntimeConfig.SetRuntimePreviewOverride(_dispatch, null);
                var ctrl = pizzeriaGo.AddComponent<PizzeriaLightingController>();
                ctrl.EnsureDefaultZonePresets();
                ctrl.RefreshInteriorLighting();

                Assert.AreEqual(LightShadows.None, point.shadows, "Interior Point lights must never cast cubemap shadows in Solution A.");

                var verifyMpb = new MaterialPropertyBlock();
                r.GetPropertyBlock(verifyMpb);
                Assert.AreEqual(preservedTint, verifyMpb.GetColor("_MainColor"), "Interior zone fill must preserve item/pizza _MainColor.");
                Assert.Greater(verifyMpb.GetFloat(LightingShaderGlobals.InteriorZoneWeightId), 0f);
            }
            finally
            {
                VisualStyleRuntimeConfig.ClearSessionOverride();
                UnityEngine.Object.DestroyImmediate(pizzeriaGo);
            }
        }

        [Test]
        public void NetworkTimeOfDayState_ReconstructsLateJoinClockDeterministically()
        {
            var netState = new TimeOfDayNetState
            {
                ReferenceClockHours = 19.0f,
                ReferenceServerTime = 100.0,
                HoursPerRealMinute = 1.5f,
                AutoAdvance = true,
                ActiveStyleHash = 42
            };

            // +120 seconds elapsed = +2.0 real minutes -> +3.0 game hours -> 19 + 3 = 22h
            float hourAfter2Min = NetworkTimeOfDayState.EvaluateSynchronizedClockHours(netState, 220.0);
            Assert.AreEqual(22.0f, hourAfter2Min, 0.001f);

            // Wrap past midnight: +240 seconds = +4 min -> +6 hours -> 19 + 6 = 25 -> 1.0h
            float hourAfter4Min = NetworkTimeOfDayState.EvaluateSynchronizedClockHours(netState, 340.0);
            Assert.AreEqual(1.0f, hourAfter4Min, 0.001f);

            // Frozen clock ignores elapsed time
            netState.AutoAdvance = false;
            Assert.AreEqual(19.0f, NetworkTimeOfDayState.EvaluateSynchronizedClockHours(netState, 9999.0), 0.001f);
        }

        [Test]
        public void UrpBudgetAndStaticAudits_ProtectCanonicalAssets_ComplyMenuOrganization_AndKeepRuntimeClean()
        {
            // 1. Canonical RPAsset / Renderer / GameScene hashes must remain intact
            string rpHashBefore = ComputeSha256(VisualStyleUrpOutlineConfigurator.CanonicalRpAssetPath);
            string rdHashBefore = ComputeSha256(VisualStyleUrpOutlineConfigurator.CanonicalRendererDataPath);
            string gsHashBefore = ComputeSha256(VisualStyleAuditService.CanonicalGameScenePath);

            var canonicalRp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(VisualStyleUrpOutlineConfigurator.CanonicalRpAssetPath);
            var canonicalRd = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(VisualStyleUrpOutlineConfigurator.CanonicalRendererDataPath);
            VisualStyleUrpOutlineConfigurator.ConfigureLightingBudgetAndSsao(canonicalRp, canonicalRd);

            Assert.AreEqual(rpHashBefore, ComputeSha256(VisualStyleUrpOutlineConfigurator.CanonicalRpAssetPath), "PC_RPAsset canonico nao pode ser modificado.");
            Assert.AreEqual(rdHashBefore, ComputeSha256(VisualStyleUrpOutlineConfigurator.CanonicalRendererDataPath), "PC_Renderer canonico nao pode ser modificado.");
            Assert.AreEqual(gsHashBefore, ComputeSha256(VisualStyleAuditService.CanonicalGameScenePath), "GameScene.unity canonica nao pode ser modificada.");

            // 2. Rollback manifest serialization sanity (Mode B contract)
            var manifest = new OriginalGameSceneRollbackManifest
            {
                styleId = "dispatch_inspired",
                preApplySceneSha256 = gsHashBefore,
                createdBootstrapObject = true
            };
            string json = JsonUtility.ToJson(manifest);
            var parsed = JsonUtility.FromJson<OriginalGameSceneRollbackManifest>(json);
            Assert.AreEqual("dispatch_inspired", parsed.styleId);
            Assert.AreEqual(gsHashBefore, parsed.preApplySceneSha256);
            Assert.IsTrue(parsed.createdBootstrapObject);

            // 3. Static audit: all MenuItem Tools attributes in Editor scripts use allowed buckets
            string[] editorScripts = Directory.GetFiles(Path.GetFullPath("Assets/Piaaria/Scripts"), "*.cs", SearchOption.AllDirectories)
                .Where(p => p.IndexOf($"{Path.DirectorySeparatorChar}Editor{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) >= 0)
                .ToArray();

            var menuRegex = new Regex("\\[MenuItem\\(\\s*\"(Tools/[^\"]+)\"", RegexOptions.Compiled);
            foreach (string scriptPath in editorScripts)
            {
                string strippedScript = Regex.Replace(File.ReadAllText(scriptPath), @"//.*$|/\*[\s\S]*?\*/", string.Empty, RegexOptions.Multiline);
                foreach (Match m in menuRegex.Matches(strippedScript))
                {
                    string menuPath = m.Groups[1].Value;
                    bool validBucket =
                        menuPath.StartsWith("Tools/Scary Party/Wizards/", StringComparison.Ordinal) ||
                        menuPath.StartsWith("Tools/Scary Party/Uso Comum/", StringComparison.Ordinal) ||
                        menuPath.StartsWith("Tools/Scary Party/Setup e Uso Único/", StringComparison.Ordinal) ||
                        menuPath.StartsWith("Tools/Scary Party/Legado/", StringComparison.Ordinal);
                    Assert.IsTrue(validBucket, $"MenuItem fora da convencao Scary Party em {Path.GetFileName(scriptPath)}: '{menuPath}'");
                }
            }

            // 4. Static audit: zero AssetDatabase/UnityEditor inside VisualStyles/Runtime
            string runtimeDir = Path.GetFullPath("Assets/Piaaria/Scripts/VisualStyles/Runtime");
            foreach (string rtFile in Directory.GetFiles(runtimeDir, "*.cs", SearchOption.AllDirectories))
            {
                string stripped = Regex.Replace(File.ReadAllText(rtFile), @"//.*$|/\*[\s\S]*?\*/", string.Empty, RegexOptions.Multiline);
                Assert.IsFalse(Regex.IsMatch(stripped, @"\bAssetDatabase\b"), $"Runtime script {Path.GetFileName(rtFile)} nao pode usar AssetDatabase.");
                Assert.IsFalse(Regex.IsMatch(stripped, @"\busing\s+UnityEditor\b"), $"Runtime script {Path.GetFileName(rtFile)} nao pode importar UnityEditor.");
            }
        }

        [Test]
        public void OutlineAndLightingCalibration_AllPresetsHavePlaneInvariantThresholdsAndNoInteractionPopOff()
        {
            var presets = new[]
            {
                VisualStylePresetType.DispatchInspired,
                VisualStylePresetType.SoftToonVibrant,
                VisualStylePresetType.AnimeSoft,
                VisualStylePresetType.ComicBold,
                VisualStylePresetType.FlatGraphic
            };

            VisualStyleProfile transient = ScriptableObject.CreateInstance<VisualStyleProfile>();
            Material outlineMat = null;
            try
            {
                foreach (VisualStylePresetType preset in presets)
                {
                    VisualStyleProfile assetProfile = VisualStylePresetSeeder.LoadPreset(preset);
                    Assert.IsNotNull(assetProfile, $"Perfil asset ausente para preset {preset}");
                    Assert.GreaterOrEqual(assetProfile.outlines.depthThreshold, 2.0f, $"Asset {preset} deve usar depthThreshold plane-invariante >= 2.0");
                    Assert.GreaterOrEqual(assetProfile.outlines.normalThreshold, 0.25f, $"Asset {preset} normalThreshold muito baixo");
                    Assert.IsFalse(assetProfile.outlines.suppressOutlineOnInteractionFocus, $"Asset {preset} nao deve desligar outline ao focar interacao");
                    Assert.GreaterOrEqual(assetProfile.lighting.minShadowBrightness, 0.25f, $"Asset {preset} minShadowBrightness muito escuro");

                    transient.ApplyPresetDefaults(preset);
                    Assert.GreaterOrEqual(transient.outlines.depthThreshold, 2.0f, $"Preset default {preset} depthThreshold");
                    Assert.GreaterOrEqual(transient.outlines.normalThreshold, 0.25f, $"Preset default {preset} normalThreshold");
                    Assert.IsFalse(transient.outlines.suppressOutlineOnInteractionFocus, $"Preset default {preset} suppressOutlineOnInteractionFocus");
                    Assert.GreaterOrEqual(transient.lighting.minShadowBrightness, 0.25f, $"Preset default {preset} minShadowBrightness");

                    if (preset == VisualStylePresetType.DispatchInspired ||
                        preset == VisualStylePresetType.SoftToonVibrant ||
                        preset == VisualStylePresetType.ComicBold)
                    {
                        Assert.IsTrue(assetProfile.outlines.useColorTest, $"Asset {preset} deve habilitar color edge test");
                        Assert.IsTrue(transient.outlines.useColorTest, $"Preset default {preset} deve habilitar color edge test");
                    }
                }

                Shader outlineShader = VisualStyleShaderUtility.ResolveScreenSpaceOutlineShader();
                Assert.IsNotNull(outlineShader, "Shader ScaryParty/Illustrate Stylized/ScreenSpaceOutline deve resolver no projeto.");
                Assert.IsTrue(VisualStyleShaderUtility.IsScreenSpaceOutlineShader(outlineShader));

                outlineMat = new Material(outlineShader);
                var legacySettings = new ToonOutlineSettings
                {
                    screenSpaceWidth = 1.2f,
                    useDepthTest = true,
                    depthThreshold = 0.25f,
                    useNormalsTest = true,
                    normalThreshold = 0.2f,
                    useColorTest = false
                };

                VisualStyleShaderUtility.ApplyOutlineSettingsToPassMaterial(outlineMat, legacySettings);
                Assert.AreEqual(5.4f, outlineMat.GetFloat("_Depth_Threshold"), 0.001f);
                Assert.GreaterOrEqual(outlineMat.GetFloat("_NormalThreshold"), 0.25f);
                Assert.AreEqual(0f, outlineMat.GetFloat("_USECOLORTEST"), 0.001f);
                Assert.IsFalse(outlineMat.IsKeywordEnabled("_USEDEPTHTEST"));
                Assert.IsFalse(outlineMat.IsKeywordEnabled("_USENORMALSTEST"));
                Assert.IsFalse(outlineMat.IsKeywordEnabled("_USECOLORTEST"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(transient);
                if (outlineMat != null)
                {
                    UnityEngine.Object.DestroyImmediate(outlineMat);
                }
            }
        }

        [Test]
        public void MaterialFactory_BuildBoundedMaterialAssetName_CapsLongRelativePathsAndPreservesHash()
        {
            string styleId = "custom_style_20261111_225726_very_long_suffix";
            string folder = $"{VisualStyleMaterialFactory.GeneratedRootFolder}/custom_style_20261111_225726/Materials/Architecture";
            string longName = "PizzeriaWall_Mat_Gen_CaixaRegistradora_DetailedProceduralSurfaceName_ExtraLongToken";
            string shortHash = "9aa86e4c";

            string boundedName = VisualStyleMaterialFactory.BuildBoundedMaterialAssetName(
                styleId,
                VisualSemanticRole.PizzeriaWall,
                longName,
                shortHash,
                folder,
                175);

            string relPath = $"{folder}/{boundedName}";
            Assert.LessOrEqual(relPath.Length, 175, $"Caminho relativo gerado excedeu o limite: {relPath}");
            StringAssert.EndsWith($"_{shortHash}.mat", boundedName);
        }
    }
}
#endif

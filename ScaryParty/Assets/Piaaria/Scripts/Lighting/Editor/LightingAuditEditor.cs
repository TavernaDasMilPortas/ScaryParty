#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using ScaryParty.City.Modular;
using ScaryParty.VisualStyles;
using ScaryParty.VisualStyles.Editor;

namespace ScaryParty.Lighting.Editor
{
    [Serializable]
    public class LightSourceAuditEntry
    {
        public string sourceContext;
        public string objectPath;
        public string lightType;
        public string shadowType;
        public float range;
        public float intensity;
        public int estimatedShadowSlices;
        public bool enabled;
    }

    [Serializable]
    public class LightingBaselineAuditReport
    {
        public string timestampUtc;
        public string unityVersion;
        public string activeRenderPipelineName;
        public string activeRenderPipelinePath;
        public int mainLightShadowResolution;
        public int additionalLightsShadowAtlasSize;
        public int tierLow;
        public int tierMedium;
        public int tierHigh;
        public int shadowCascadeCount;
        public float shadowDistance;
        public string renderingMode;
        public bool ssaoPresent;
        public float ssaoIntensity;
        public float ssaoDirectLightingStrength;
        public int totalLightsCount;
        public int shadowedLocalLightsCount;
        public int worstCaseLocalShadowSlices;
        public bool atlasDowngradeRisk;
        public string currentStyleId;
        public string currentGameplayRoute;
        public string currentVisualMode;
        public List<string> findings = new List<string>();
        public List<LightSourceAuditEntry> lights = new List<LightSourceAuditEntry>();
    }

    /// <summary>
    /// Auditoria M0 de iluminação, atlas de sombras URP e rotas visuais.
    /// </summary>
    public static class LightingAuditEditor
    {
        public const string ReportsFolder = "Assets/Piaaria/Art/VisualStyles/Reports";
        public const string BaselineMarkdownPath = ReportsFolder + "/Lighting_M0_Baseline_Report.md";
        public const string BaselineJsonPath = ReportsFolder + "/Lighting_M0_Baseline_Report.json";
        public const string CanonicalRPAssetPath = "Assets/Settings/PC_RPAsset.asset";
        public const string CanonicalRendererPath = "Assets/Settings/PC_Renderer.asset";
        public const string PizzeriaManualPrefabPath = "Assets/Piaaria/Prefabs/Pizzeria/PizzeriaModular_Manual.prefab";

        [MenuItem("Tools/Scary Party/Uso Comum/Visuals/Auditar Iluminação e Atlas URP (M0)", false, 180)]
        public static void RunFullAuditMenu()
        {
            LightingBaselineAuditReport report = RunFullAudit(saveReports: true);
            AssetDatabase.Refresh();
            Debug.Log($"[LightingAuditEditor] Auditoria M0 salva em '{BaselineMarkdownPath}'. " +
                      $"Luzes={report.totalLightsCount}, Locais com sombra={report.shadowedLocalLightsCount}, " +
                      $"Slices estimadas={report.worstCaseLocalShadowSlices}, Risco atlas={report.atlasDowngradeRisk}");
        }

        public static LightingBaselineAuditReport RunFullAudit(bool saveReports = true)
        {
            var report = new LightingBaselineAuditReport
            {
                timestampUtc = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'"),
                unityVersion = Application.unityVersion
            };

            InspectRenderPipeline(report);
            InspectRendererAndSsao(report);
            InspectRuntimeConfig(report);
            InspectSceneNonDestructively(VisualStyleAuditService.CanonicalGameScenePath, "Canonical:GameScene", report);
            InspectPrefab(PizzeriaManualPrefabPath, "Prefab:PizzeriaModular_Manual", report);
            InspectCityCatalog(report);
            AnalyzeFindings(report);

            if (saveReports)
            {
                SaveReports(report);
            }

            return report;
        }

        private static void InspectRenderPipeline(LightingBaselineAuditReport report)
        {
            UniversalRenderPipelineAsset rp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (rp == null)
            {
                rp = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
            }
            if (rp == null)
            {
                rp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(CanonicalRPAssetPath);
            }

            if (rp == null)
            {
                report.findings.Add("Nenhum UniversalRenderPipelineAsset encontrado.");
                return;
            }

            report.activeRenderPipelineName = rp.name;
            report.activeRenderPipelinePath = AssetDatabase.GetAssetPath(rp);
            report.mainLightShadowResolution = rp.mainLightShadowmapResolution;
            report.additionalLightsShadowAtlasSize = rp.additionalLightsShadowmapResolution;
            report.shadowCascadeCount = rp.shadowCascadeCount;
            report.shadowDistance = rp.shadowDistance;

            var so = new SerializedObject(rp);
            report.tierLow = so.FindProperty("m_AdditionalLightsShadowResolutionTierLow")?.intValue ?? 0;
            report.tierMedium = so.FindProperty("m_AdditionalLightsShadowResolutionTierMedium")?.intValue ?? 0;
            report.tierHigh = so.FindProperty("m_AdditionalLightsShadowResolutionTierHigh")?.intValue ?? 0;
        }

        private static void InspectRendererAndSsao(LightingBaselineAuditReport report)
        {
            ScriptableRendererData rendererData = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(CanonicalRendererPath);
            if (rendererData == null)
            {
                report.findings.Add($"Renderer canonical nao encontrado em '{CanonicalRendererPath}'.");
                return;
            }

            var rendererSo = new SerializedObject(rendererData);
            SerializedProperty modeProp = rendererSo.FindProperty("m_RenderingMode");
            int modeInt = modeProp != null ? modeProp.intValue : -1;
            report.renderingMode = modeInt == 2 ? "ForwardPlus" : (modeInt == 1 ? "Deferred" : "Forward");

            if (rendererData.rendererFeatures == null) return;
            foreach (ScriptableRendererFeature feature in rendererData.rendererFeatures)
            {
                if (feature == null) continue;
                if (feature.GetType().Name.IndexOf("ScreenSpaceAmbientOcclusion", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    report.ssaoPresent = true;
                    var fSo = new SerializedObject(feature);
                    SerializedProperty settingsProp = fSo.FindProperty("m_Settings");
                    if (settingsProp != null)
                    {
                        report.ssaoIntensity = settingsProp.FindPropertyRelative("Intensity")?.floatValue ?? 0f;
                        report.ssaoDirectLightingStrength = settingsProp.FindPropertyRelative("DirectLightingStrength")?.floatValue ?? 0f;
                    }
                }
            }
        }

        private static void InspectRuntimeConfig(LightingBaselineAuditReport report)
        {
            VisualStyleRuntimeConfig cfg = Resources.Load<VisualStyleRuntimeConfig>(VisualStyleRuntimeConfig.ResourcesPath);
            if (cfg == null)
            {
                report.currentStyleId = "<Missing>";
                report.currentGameplayRoute = "Original";
                report.currentVisualMode = "Disabled";
                return;
            }

            report.currentStyleId = cfg.activeProfile != null ? cfg.activeProfile.styleId : cfg.activeStyleId;
            report.currentGameplayRoute = cfg.gameplayRoute.ToString();
            report.currentVisualMode = cfg.visualMode.ToString();
        }

        private static void InspectSceneNonDestructively(string scenePath, string context, LightingBaselineAuditReport report)
        {
            if (!File.Exists(scenePath))
            {
                report.findings.Add($"Cena '{scenePath}' nao encontrada no disco.");
                return;
            }

            Scene existing = SceneManager.GetSceneByPath(scenePath);
            bool openedTemp = false;
            Scene scene = existing;
            if (!scene.isLoaded)
            {
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                openedTemp = true;
            }

            try
            {
                GameObject[] roots = scene.GetRootGameObjects();
                for (int i = 0; i < roots.Length; i++)
                {
                    InspectHierarchyLights(roots[i], context, report);
                }
            }
            finally
            {
                if (openedTemp && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static void InspectPrefab(string prefabPath, string context, LightingBaselineAuditReport report)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                report.findings.Add($"Prefab '{prefabPath}' nao encontrado.");
                return;
            }
            InspectHierarchyLights(prefab, context, report);
        }

        private static void InspectHierarchyLights(GameObject root, string context, LightingBaselineAuditReport report)
        {
            if (root == null) return;
            Light[] lights = root.GetComponentsInChildren<Light>(true);
            for (int i = 0; i < lights.Length; i++)
            {
                Light l = lights[i];
                if (l == null) continue;
                int slices = 0;
                if (l.shadows != LightShadows.None && l.enabled && l.gameObject.activeInHierarchy)
                {
                    if (l.type == LightType.Point) slices = 6;
                    else if (l.type == LightType.Spot) slices = 1;
                }

                report.totalLightsCount++;
                if (l.type != LightType.Directional && l.shadows != LightShadows.None)
                {
                    report.shadowedLocalLightsCount++;
                    report.worstCaseLocalShadowSlices += l.type == LightType.Point ? 6 : 1;
                }

                report.lights.Add(new LightSourceAuditEntry
                {
                    sourceContext = context,
                    objectPath = GetPath(root.transform, l.transform),
                    lightType = l.type.ToString(),
                    shadowType = l.shadows.ToString(),
                    range = l.range,
                    intensity = l.intensity,
                    estimatedShadowSlices = slices,
                    enabled = l.enabled
                });
            }
        }

        private static void InspectCityCatalog(LightingBaselineAuditReport report)
        {
            CityModuleCatalog catalog = AssetDatabase.LoadAssetAtPath<CityModuleCatalog>("Assets/Piaaria/ScriptableObjects/City/Modular/CityModuleCatalog.asset");
            if (catalog == null)
            {
                report.findings.Add("CityModuleCatalog nao encontrado.");
                return;
            }
            report.findings.Add($"CityModuleCatalog carregado com {catalog.definitions?.Count ?? 0} modulos.");
        }

        private static void AnalyzeFindings(LightingBaselineAuditReport report)
        {
            if (report.tierHigh >= 1024 && report.worstCaseLocalShadowSlices > 4)
            {
                report.atlasDowngradeRisk = true;
                report.findings.Add($"RISCO CRITICO ATLAS URP: Tier High={report.tierHigh} com {report.shadowedLocalLightsCount} luzes locais com sombra ({report.worstCaseLocalShadowSlices} slices) excede atlas de {report.additionalLightsShadowAtlasSize}.");
            }
            if (report.ssaoPresent && report.ssaoDirectLightingStrength > 0.16f)
            {
                report.findings.Add($"SSAO DirectLightingStrength={report.ssaoDirectLightingStrength:0.00} pode escurecer cavidades iluminadas em estilo cartoon (recomendado <= 0.15).");
            }
        }

        private static void SaveReports(LightingBaselineAuditReport report)
        {
            if (!Directory.Exists(ReportsFolder))
            {
                Directory.CreateDirectory(ReportsFolder);
            }

            File.WriteAllText(BaselineJsonPath, JsonUtility.ToJson(report, true), Encoding.UTF8);

            var sb = new StringBuilder();
            sb.AppendLine("# Relatorio M0 — Auditoria de Iluminacao e Atlas URP");
            sb.AppendLine();
            sb.AppendLine($"- **Timestamp:** {report.timestampUtc}");
            sb.AppendLine($"- **Unity:** {report.unityVersion}");
            sb.AppendLine($"- **RP Asset:** `{report.activeRenderPipelineName}` (`{report.activeRenderPipelinePath}`)");
            sb.AppendLine($"- **Renderer Mode:** `{report.renderingMode}` | **Shadow Distance:** `{report.shadowDistance}m` | **Cascades:** `{report.shadowCascadeCount}`");
            sb.AppendLine($"- **Additional Lights Atlas:** `{report.additionalLightsShadowAtlasSize}` (Tiers Low/Med/High: `{report.tierLow}/{report.tierMedium}/{report.tierHigh}`)");
            sb.AppendLine($"- **SSAO:** Present={report.ssaoPresent} | Intensity=`{report.ssaoIntensity:0.00}` | DirectLightingStrength=`{report.ssaoDirectLightingStrength:0.00}`");
            sb.AppendLine($"- **Lights Found:** `{report.totalLightsCount}` | **Shadowed Local Lights:** `{report.shadowedLocalLightsCount}` | **Estimated Slices:** `{report.worstCaseLocalShadowSlices}` | **Atlas Downgrade Risk:** `{report.atlasDowngradeRisk}`");
            sb.AppendLine($"- **Runtime Config:** Route=`{report.currentGameplayRoute}`, VisualMode=`{report.currentVisualMode}`, Style=`{report.currentStyleId}`");
            sb.AppendLine();
            sb.AppendLine("## Achados e Causas Raiz");
            for (int i = 0; i < report.findings.Count; i++)
            {
                sb.AppendLine($"- {report.findings[i]}");
            }
            File.WriteAllText(BaselineMarkdownPath, sb.ToString(), Encoding.UTF8);
        }

        private static string GetPath(Transform root, Transform target)
        {
            var stack = new Stack<string>();
            Transform cur = target;
            while (cur != null && cur != root)
            {
                stack.Push(cur.name);
                cur = cur.parent;
            }
            return string.Join("/", stack);
        }
    }
}
#endif

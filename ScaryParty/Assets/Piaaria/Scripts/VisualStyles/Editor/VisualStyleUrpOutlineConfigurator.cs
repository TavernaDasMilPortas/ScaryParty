#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ScaryParty.VisualStyles.Editor
{
    public sealed class VariantRenderPipelineResult
    {
        public string rendererDataPath;
        public string rpAssetPath;
        public string outlineMaterialPath;
        public ScriptableRendererData rendererData;
        public UniversalRenderPipelineAsset rpAsset;
        public Material screenSpaceOutlineMaterial;
        public FullScreenPassRendererFeature outlineFeature;
    }

    /// <summary>
    /// Creates and updates per-style duplicated URP RendererData, UniversalRenderPipelineAsset,
    /// and Distant Lands FullScreenPassRendererFeature without ever modifying canonical PC_RPAsset/PC_Renderer.
    /// </summary>
    public static class VisualStyleUrpOutlineConfigurator
    {
        public const string CanonicalRpAssetPath = "Assets/Settings/PC_RPAsset.asset";
        public const string CanonicalRendererDataPath = "Assets/Settings/PC_Renderer.asset";
        public const string CanonicalOutlineTemplateMatPath =
            "Assets/ThirdParty/Distant Lands/Illustrate - Stylized Toon Shader/Outline Render Feature/Outline Render Feature.mat";
        public const string OutlineFeatureAssetName = "Illustrate_ScreenSpaceOutline";

        public static VariantRenderPipelineResult CreateOrUpdateVariantRenderPipeline(
            VisualStyleProfile profile,
            VisualMaterialLibrary library = null)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));

            string safeId = VisualStyleMaterialFactory.SanitizeFileToken(string.IsNullOrEmpty(profile.styleId) ? "default" : profile.styleId);
            string renderFolder = $"{VisualStyleMaterialFactory.GetStyleGeneratedFolder(profile.styleId)}/Render";
            VisualStylePresetSeeder.EnsureFolder(renderFolder);

            string rendererPath = $"{renderFolder}/{safeId}_Renderer.asset";
            string rpPath = $"{renderFolder}/{safeId}_RPAsset.asset";
            string matPath = $"{renderFolder}/{safeId}_ScreenSpaceOutline.mat";

            // 1. Ensure Screen-Space Outline Material
            Material outlineMat = EnsureScreenSpaceOutlineMaterial(matPath, profile);

            // 2. Clone canonical PC_Renderer if missing (never mutate Assets/Settings/PC_Renderer.asset)
            ScriptableRendererData rendererData = EnsureClonedAsset<ScriptableRendererData>(
                CanonicalRendererDataPath, rendererPath, "RendererData");

            // 3. Ensure FullScreenPassRendererFeature on cloned RendererData
            FullScreenPassRendererFeature feature = EnsureFullScreenOutlineFeature(
                rendererData, outlineMat, profile);

            // 4. Clone canonical PC_RPAsset if missing and point its renderer list to cloned RendererData
            UniversalRenderPipelineAsset rpAsset = EnsureClonedAsset<UniversalRenderPipelineAsset>(
                CanonicalRpAssetPath, rpPath, "UniversalRenderPipelineAsset");
            BindRendererDataToPipelineAsset(rpAsset, rendererData);

            // 5. Link onto library if supplied
            if (library != null && library.renderPipelineRendererData != rendererData)
            {
                library.renderPipelineRendererData = rendererData;
                EditorUtility.SetDirty(library);
            }

            AssetDatabase.SaveAssets();

            return new VariantRenderPipelineResult
            {
                rendererDataPath = rendererPath,
                rpAssetPath = rpPath,
                outlineMaterialPath = matPath,
                rendererData = rendererData,
                rpAsset = rpAsset,
                screenSpaceOutlineMaterial = outlineMat,
                outlineFeature = feature
            };
        }

        private static Material EnsureScreenSpaceOutlineMaterial(string matPath, VisualStyleProfile profile)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            bool isNew = mat == null;

            if (isNew)
            {
                if (File.Exists(CanonicalOutlineTemplateMatPath))
                {
                    AssetDatabase.CopyAsset(CanonicalOutlineTemplateMatPath, matPath);
                    AssetDatabase.ImportAsset(matPath);
                    mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                }

                if (mat == null)
                {
                    Shader shader = Shader.Find(VisualStyleShaderUtility.ScreenSpaceOutlineShader);
                    if (shader == null)
                    {
                        throw new InvalidOperationException($"Missing required shader '{VisualStyleShaderUtility.ScreenSpaceOutlineShader}'.");
                    }

                    mat = new Material(shader) { name = Path.GetFileNameWithoutExtension(matPath) };
                    AssetDatabase.CreateAsset(mat, matPath);
                }
            }

            string before = EditorJsonUtility.ToJson(mat);
            Shader outlineShader = Shader.Find(VisualStyleShaderUtility.ScreenSpaceOutlineShader);
            if (outlineShader != null && mat.shader != outlineShader)
            {
                mat.shader = outlineShader;
            }

            ApplyOutlineProfileToMaterial(mat, profile);

            string after = EditorJsonUtility.ToJson(mat);
            if (!isNew && !string.Equals(before, after, StringComparison.Ordinal))
            {
                EditorUtility.SetDirty(mat);
            }

            return mat;
        }

        public static void ApplyOutlineProfileToMaterial(Material mat, VisualStyleProfile profile)
        {
            if (mat == null || profile == null) return;
            var o = profile.outlines;

            VisualStyleShaderUtility.SetFloatSafe(mat, "_Width", Mathf.Max(0f, o.screenSpaceWidth));
            VisualStyleShaderUtility.SetColorSafe(mat, "_OutlineColor", o.outlineColor);
            VisualStyleShaderUtility.SetFloatSafe(mat, "_USEDEPTHTEST", o.useDepthTest ? 1f : 0f);
            VisualStyleShaderUtility.SetFloatSafe(mat, "_Depth_Threshold", Mathf.Max(0.001f, o.depthThreshold));
            VisualStyleShaderUtility.SetFloatSafe(mat, "_USENORMALSTEST", o.useNormalsTest ? 1f : 0f);
            VisualStyleShaderUtility.SetFloatSafe(mat, "_NormalThreshold", Mathf.Max(0.001f, o.normalThreshold));
            VisualStyleShaderUtility.SetFloatSafe(mat, "_USECOLORTEST", o.useColorTest ? 1f : 0f);
            VisualStyleShaderUtility.SetFloatSafe(mat, "_ColorThreshold", Mathf.Max(0.001f, o.colorThreshold));
        }

        private static T EnsureClonedAsset<T>(string sourcePath, string targetPath, string label) where T : ScriptableObject
        {
            T existing = AssetDatabase.LoadAssetAtPath<T>(targetPath);
            if (existing != null) return existing;

            if (!File.Exists(sourcePath))
            {
                throw new FileNotFoundException($"Canonical {label} not found at '{sourcePath}'.", sourcePath);
            }

            if (!AssetDatabase.CopyAsset(sourcePath, targetPath))
            {
                throw new IOException($"Failed to copy canonical {label} from '{sourcePath}' to '{targetPath}'.");
            }

            AssetDatabase.ImportAsset(targetPath);
            existing = AssetDatabase.LoadAssetAtPath<T>(targetPath);
            if (existing == null)
            {
                throw new InvalidOperationException($"Copied {label} could not be loaded at '{targetPath}'.");
            }
            return existing;
        }

        private static FullScreenPassRendererFeature EnsureFullScreenOutlineFeature(
            ScriptableRendererData rendererData,
            Material outlineMat,
            VisualStyleProfile profile)
        {
            if (rendererData == null) return null;

            FullScreenPassRendererFeature feature = null;
            if (rendererData.rendererFeatures != null)
            {
                for (int i = 0; i < rendererData.rendererFeatures.Count; i++)
                {
                    if (rendererData.rendererFeatures[i] is FullScreenPassRendererFeature existing &&
                        (existing.name == OutlineFeatureAssetName ||
                         existing.passMaterial == outlineMat ||
                         (existing.passMaterial != null && existing.passMaterial.shader != null &&
                          existing.passMaterial.shader.name == VisualStyleShaderUtility.ScreenSpaceOutlineShader)))
                    {
                        feature = existing;
                        break;
                    }
                }
            }

            bool added = false;
            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
                feature.name = OutlineFeatureAssetName;
                AssetDatabase.AddObjectToAsset(feature, rendererData);
                added = true;
            }

            string before = EditorJsonUtility.ToJson(feature);
            feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.AfterRenderingPostProcessing;
            feature.fetchColorBuffer = true;
            feature.requirements = ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal | ScriptableRenderPassInput.Color;
            feature.passMaterial = outlineMat;
            feature.passIndex = 0;
            feature.bindDepthStencilAttachment = false;
            feature.SetActive(profile.outlines.outlineMode == VisualStyleOutlineMode.ScreenSpace);
            string after = EditorJsonUtility.ToJson(feature);

            if (added)
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);

                var so = new SerializedObject(rendererData);
                var featuresProp = so.FindProperty("m_RendererFeatures");
                var mapProp = so.FindProperty("m_RendererFeatureMap");
                if (featuresProp != null)
                {
                    int idx = featuresProp.arraySize;
                    featuresProp.InsertArrayElementAtIndex(idx);
                    featuresProp.GetArrayElementAtIndex(idx).objectReferenceValue = feature;
                }
                if (mapProp != null)
                {
                    int idx = mapProp.arraySize;
                    mapProp.InsertArrayElementAtIndex(idx);
                    mapProp.GetArrayElementAtIndex(idx).longValue = localId;
                }
                so.ApplyModifiedPropertiesWithoutUndo();

                EditorUtility.SetDirty(feature);
                EditorUtility.SetDirty(rendererData);
                rendererData.SetDirty();
            }
            else if (!string.Equals(before, after, StringComparison.Ordinal))
            {
                EditorUtility.SetDirty(feature);
                EditorUtility.SetDirty(rendererData);
                rendererData.SetDirty();
            }

            return feature;
        }

        private static void BindRendererDataToPipelineAsset(
            UniversalRenderPipelineAsset rpAsset,
            ScriptableRendererData rendererData)
        {
            if (rpAsset == null || rendererData == null) return;

            var so = new SerializedObject(rpAsset);
            var listProp = so.FindProperty("m_RendererDataList");
            if (listProp == null || listProp.arraySize == 0) return;

            var firstSlot = listProp.GetArrayElementAtIndex(0);
            if (firstSlot.objectReferenceValue == rendererData) return;

            firstSlot.objectReferenceValue = rendererData;
            var defaultIdxProp = so.FindProperty("m_DefaultRendererIndex");
            if (defaultIdxProp != null)
            {
                defaultIdxProp.intValue = 0;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(rpAsset);
        }
    }
}
#endif

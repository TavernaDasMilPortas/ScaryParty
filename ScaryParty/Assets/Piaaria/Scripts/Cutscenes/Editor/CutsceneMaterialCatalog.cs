using System.Collections.Generic;
using System.IO;
using ScaryParty.VisualStyles;
using UnityEditor;
using UnityEngine;

namespace ScaryParty.Cutscenes.Editor
{
    public static class CutsceneMaterialCatalog
    {
        public static List<Material> CollectMaterialsFromPrefab(GameObject prefab)
        {
            var result = new List<Material>();
            if (prefab == null) return result;

            foreach (var r in prefab.GetComponentsInChildren<Renderer>(true))
            {
                foreach (var mat in r.sharedMaterials)
                {
                    if (mat != null && !result.Contains(mat))
                        result.Add(mat);
                }
            }
            return result;
        }

        public static Material SuggestMaterialByName(string sourceMaterialName, List<Material> candidates)
        {
            if (string.IsNullOrEmpty(sourceMaterialName) || candidates == null)
                return null;

            foreach (var mat in candidates)
            {
                if (mat != null && string.Equals(mat.name, sourceMaterialName, System.StringComparison.OrdinalIgnoreCase))
                    return mat;
            }
            return null;
        }

        public static VisualStyleProfile ResolveActiveVisualProfile()
        {
            var cfg = VisualStyleRuntimeConfig.LoadCached();
            if (cfg != null && cfg.activeProfile != null)
                return cfg.activeProfile;

            string[] guids = AssetDatabase.FindAssets("t:VisualStyleProfile");
            foreach (var g in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                if (path.Contains("flat_graphic"))
                    return AssetDatabase.LoadAssetAtPath<VisualStyleProfile>(path);
            }
            return guids.Length > 0
                ? AssetDatabase.LoadAssetAtPath<VisualStyleProfile>(AssetDatabase.GUIDToAssetPath(guids[0]))
                : null;
        }

        public static int ApplyActivePresetToProfile(CutsceneBindingProfile bindingProfile, out string report)
        {
            report = string.Empty;
            if (bindingProfile == null)
            {
                report = "BindingProfile is null.";
                return 0;
            }

            VisualStyleProfile styleProfile = ResolveActiveVisualProfile();
            if (styleProfile == null)
            {
                report = "Nenhum VisualStyleProfile ativo encontrado no projeto.";
                return 0;
            }

            // Collect embedded materials from FBX if available
            var fbxMaterials = new Dictionary<string, Material>();
            if (!string.IsNullOrEmpty(bindingProfile.fbxAssetPath))
            {
                foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(bindingProfile.fbxAssetPath))
                {
                    if (sub is Material m && !fbxMaterials.ContainsKey(m.name))
                        fbxMaterials[m.name] = m;
                }
            }

            Shader stylizedOpaque = Shader.Find(VisualStyleShaderUtility.GetShaderName(IllustrateShaderFamily.Opaque));
            if (stylizedOpaque == null)
            {
                report = "Shader IllustrateOpaqueStylized não encontrado.";
                return 0;
            }

            string matDir = $"Assets/Piaaria/Cutscene/Materials/{bindingProfile.cutsceneId}";
            if (!Directory.Exists(matDir))
            {
                Directory.CreateDirectory(matDir);
                AssetDatabase.Refresh();
            }

            var generatedCache = new Dictionary<string, Material>();
            int appliedCount = 0;

            foreach (var mb in bindingProfile.materialBindings)
            {
                if (string.IsNullOrEmpty(mb.sourceMaterialName)) continue;

                if (!generatedCache.TryGetValue(mb.sourceMaterialName, out var targetMat))
                {
                    string safeName = mb.sourceMaterialName.Replace("/", "_").Replace("\\", "_");
                    string assetPath = $"{matDir}/Mat_CS_{safeName}.mat";
                    targetMat = AssetDatabase.LoadAssetAtPath<Material>(assetPath);

                    fbxMaterials.TryGetValue(mb.sourceMaterialName, out var srcMat);
                    Color baseCol = Color.white;
                    Texture mainTex = null;
                    Vector2 texScale = Vector2.one;
                    Vector2 texOffset = Vector2.zero;
                    if (srcMat != null)
                    {
                        baseCol = VisualStyleShaderUtility.ExtractBaseColor(srcMat, Color.white);
                        mainTex = VisualStyleShaderUtility.ExtractMainTexture(srcMat, out texScale, out texOffset);
                    }
                    Vector2? scale = srcMat != null ? texScale : (Vector2?)null;
                    Vector2? offset = srcMat != null ? texOffset : (Vector2?)null;

                    if (targetMat == null)
                    {
                        targetMat = new Material(stylizedOpaque);
                        AssetDatabase.CreateAsset(targetMat, assetPath);
                    }
                    else if (targetMat.shader != stylizedOpaque)
                    {
                        targetMat.shader = stylizedOpaque;
                    }

                    VisualStyleShaderUtility.ApplyProfileToMaterial(
                        targetMat,
                        styleProfile,
                        "Props",
                        VisualSemanticRole.PizzeriaProp,
                        baseCol,
                        IllustrateShaderFamily.Opaque,
                        mainTex,
                        scale,
                        offset);

                    EditorUtility.SetDirty(targetMat);
                    generatedCache[mb.sourceMaterialName] = targetMat;
                }

                mb.targetMaterial = targetMat;
                mb.isConfirmed = true;
                appliedCount++;
            }

            EditorUtility.SetDirty(bindingProfile);
            AssetDatabase.SaveAssets();
            report = $"Aplicado preset '{styleProfile.name}' em {appliedCount} slots ({generatedCache.Count} materiais em {matDir}).";
            return appliedCount;
        }
    }
}

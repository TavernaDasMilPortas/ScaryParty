using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using ScaryParty.VisualStyles;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ScaryParty.Cutscenes.Editor
{
    public static class CutsceneMaterialCatalog
    {
        private static readonly Regex TrailingIndexRegex = new Regex(@"\.\d+$", RegexOptions.Compiled);

        private static readonly Dictionary<string, string> PizzeriaBaseAliases =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "pilastrarechoncuda", "pilastra" },
                { "porta_maior", "batentefachada" },
                { "coiso", "coiso" }
            };

        private static readonly string[] CharacterSlotLabels =
        {
            "Character:BodyPrimary",
            "Character:ApronOrDetail",
            "Character:EyesWhite",
            "Character:Pupils"
        };

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
                if (mat != null && string.Equals(mat.name, sourceMaterialName, StringComparison.OrdinalIgnoreCase))
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

        /// <summary>
        /// Aplica os materiais do preset visual ativo aos slots do BindingProfile,
        /// resolvendo automaticamente as duas instâncias de pizzaria (exato e nome base)
        /// e as variações de personagens (4 e 7 slots) a partir dos prefabs de referência do preset.
        /// </summary>
        public static int ApplyActivePresetToProfile(CutsceneBindingProfile bindingProfile, out string report)
        {
            report = string.Empty;
            if (bindingProfile == null)
            {
                report = "BindingProfile is null.";
                return 0;
            }

            var cfg = VisualStyleRuntimeConfig.LoadCached();
            VisualStyleProfile styleProfile = cfg != null && cfg.activeProfile != null
                ? cfg.activeProfile
                : ResolveActiveVisualProfile();

            if (styleProfile == null)
            {
                report = "Nenhum VisualStyleProfile ativo encontrado no projeto.";
                return 0;
            }

            VisualMaterialLibrary library = cfg != null ? cfg.activeLibrary : null;
            string styleId = !string.IsNullOrEmpty(cfg?.activeStyleId) ? cfg.activeStyleId : styleProfile.name;

            ResolveReferencePrefabs(library, styleId, out var pizzeriaRef, out var characterRef);
            BuildPizzeriaIndex(pizzeriaRef, out var exactPizzeria, out var basePizzeria);
            Material[] characterRefMats = ExtractPrimaryCharacterMaterials(characterRef);
            HashSet<string> characterObjectIds = CollectCharacterObjectIds(bindingProfile);

            var fbxMaterials = CollectEmbeddedFbxMaterials(bindingProfile.fbxAssetPath);
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

            var generatedCache = new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);
            int pizzeriaMatched = 0;
            int characterMatched = 0;
            int fallbackGenerated = 0;

            foreach (var mb in bindingProfile.materialBindings)
            {
                if (string.IsNullOrEmpty(mb.sourceMaterialName)) continue;

                string leafName = ExtractLeafName(mb.rendererPath);
                bool isCharacter = IsCharacterSlot(mb, leafName, characterObjectIds);

                if (isCharacter)
                {
                    if (TryResolveCharacterMaterial(
                        mb, styleProfile, characterRefMats, fbxMaterials,
                        stylizedOpaque, matDir, generatedCache, out var charMat, out var semantic))
                    {
                        mb.targetMaterial = charMat;
                        mb.slotSemanticName = semantic;
                        mb.isConfirmed = true;
                        characterMatched++;
                        continue;
                    }
                }
                else if (TryResolvePizzeriaMaterial(leafName, mb.slotIndex, exactPizzeria, basePizzeria, out var pizMat, out var matchedKey))
                {
                    mb.targetMaterial = pizMat;
                    mb.slotSemanticName = $"Pizzeria:{matchedKey}";
                    mb.isConfirmed = true;
                    pizzeriaMatched++;
                    continue;
                }

                // Fallback estilizado para meshes exclusivos da cutscene (ex.: Cube, Plane.004)
                mb.targetMaterial = GetOrCreateStylizedFallback(
                    mb.sourceMaterialName,
                    "Props",
                    VisualSemanticRole.PizzeriaProp,
                    styleProfile,
                    fbxMaterials,
                    stylizedOpaque,
                    matDir,
                    generatedCache);
                mb.slotSemanticName = "Cutscene:FallbackProp";
                mb.isConfirmed = true;
                fallbackGenerated++;
            }

            EditorUtility.SetDirty(bindingProfile);
            TryApplyToOpenCutsceneStage(bindingProfile);
            AssetDatabase.SaveAssets();

            int total = pizzeriaMatched + characterMatched + fallbackGenerated;
            report = $"Preset '{styleId}' aplicado em {total} slots " +
                     $"(Pizzaria ref: {pizzeriaMatched}, Personagens ref: {characterMatched}, Fallback estilizado: {fallbackGenerated}).";
            return total;
        }

        private static void ResolveReferencePrefabs(
            VisualMaterialLibrary library,
            string styleId,
            out GameObject pizzeriaRef,
            out GameObject characterRef)
        {
            pizzeriaRef = library != null ? library.styledPizzeriaVariantPrefab : null;
            characterRef = library != null ? library.styledCharacterVariantPrefab : null;

            if (pizzeriaRef == null && !string.IsNullOrEmpty(styleId))
            {
                pizzeriaRef = AssetDatabase.LoadAssetAtPath<GameObject>(
                    $"Assets/Piaaria/Prefabs/VisualStyles/{styleId}/PizzeriaModular_{styleId}.prefab");
            }
            if (pizzeriaRef == null)
            {
                pizzeriaRef = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Piaaria/Prefabs/Pizzeria/PizzeriaModular_CelShaded.prefab")
                    ?? AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Piaaria/Prefabs/Pizzeria/PizzeriaPrefab.prefab");
            }

            if (characterRef == null && !string.IsNullOrEmpty(styleId))
            {
                characterRef = AssetDatabase.LoadAssetAtPath<GameObject>(
                    $"Assets/Piaaria/Prefabs/VisualStyles/{styleId}/CharacterVisual_{styleId}.prefab");
            }
            if (characterRef == null)
            {
                characterRef = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Piaaria/Prefabs/Characters/CharacterVisual.prefab");
            }
        }

        private static void BuildPizzeriaIndex(
            GameObject pizzeriaRef,
            out Dictionary<string, Material[]> exactMap,
            out Dictionary<string, Material[]> baseMap)
        {
            exactMap = new Dictionary<string, Material[]>(StringComparer.OrdinalIgnoreCase);
            baseMap = new Dictionary<string, Material[]>(StringComparer.OrdinalIgnoreCase);
            if (pizzeriaRef == null) return;

            foreach (var r in pizzeriaRef.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                if (mats == null || mats.Length == 0) continue;

                if (!exactMap.ContainsKey(r.name))
                    exactMap[r.name] = mats;

                string baseName = StripNumericSuffix(r.name);
                if (!string.IsNullOrEmpty(baseName) && !baseMap.ContainsKey(baseName))
                    baseMap[baseName] = mats;
            }
        }

        private static Material[] ExtractPrimaryCharacterMaterials(GameObject characterRef)
        {
            if (characterRef == null) return Array.Empty<Material>();

            var smr = characterRef.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (smr != null && smr.sharedMaterials != null && smr.sharedMaterials.Length > 0)
                return smr.sharedMaterials;

            var r = characterRef.GetComponentInChildren<Renderer>(true);
            return r != null && r.sharedMaterials != null ? r.sharedMaterials : Array.Empty<Material>();
        }

        private static HashSet<string> CollectCharacterObjectIds(CutsceneBindingProfile profile)
        {
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(profile.manifestAssetPath)) return ids;

            var textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(profile.manifestAssetPath);
            if (textAsset == null || !CutsceneManifestParser.TryParse(textAsset.text, out var manifest, out _))
                return ids;

            var armatureIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var obj in manifest.objects)
            {
                if (string.Equals(obj.type, "ARMATURE", StringComparison.OrdinalIgnoreCase))
                    armatureIds.Add(obj.id);
            }

            foreach (var obj in manifest.objects)
            {
                if (!string.Equals(obj.type, "MESH", StringComparison.OrdinalIgnoreCase)) continue;
                if (string.IsNullOrEmpty(obj.parentId) || !armatureIds.Contains(obj.parentId)) continue;

                // Ignora props segurados por um personagem (ex.: prato/forma.012 sob Armature.001)
                if (obj.name.StartsWith("Plane", StringComparison.OrdinalIgnoreCase))
                    ids.Add(obj.id);
            }

            return ids;
        }

        private static bool IsCharacterSlot(
            MaterialBindingEntry mb,
            string leafName,
            HashSet<string> characterObjectIds)
        {
            if (characterObjectIds.Count > 0 && !string.IsNullOrEmpty(mb.sourceObjectId))
                return characterObjectIds.Contains(mb.sourceObjectId);

            return leafName.StartsWith("Plane.", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(leafName, "Plane.004", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryResolveCharacterMaterial(
            MaterialBindingEntry mb,
            VisualStyleProfile styleProfile,
            Material[] characterRefMats,
            Dictionary<string, Material> fbxMaterials,
            Shader stylizedOpaque,
            string matDir,
            Dictionary<string, Material> generatedCache,
            out Material resolved,
            out string semanticLabel)
        {
            // Slots 0..3 correspondem diretamente aos 4 slots do CharacterVisual (BodyPrimary, Apron/Detail, EyesWhite, Pupils)
            if (mb.slotIndex >= 0 && mb.slotIndex < characterRefMats.Length && characterRefMats[mb.slotIndex] != null)
            {
                resolved = characterRefMats[mb.slotIndex];
                semanticLabel = mb.slotIndex < CharacterSlotLabels.Length
                    ? CharacterSlotLabels[mb.slotIndex]
                    : $"Character:Slot{mb.slotIndex}";
                return true;
            }

            // Slots extras (>= 4) das variações de 7 slots: estiliza preservando a cor/textura original da roupa/acessório
            resolved = GetOrCreateStylizedFallback(
                mb.sourceMaterialName,
                "Characters",
                VisualSemanticRole.NpcBody,
                styleProfile,
                fbxMaterials,
                stylizedOpaque,
                matDir,
                generatedCache);
            semanticLabel = $"Character:VariationSlot{mb.slotIndex}";
            return resolved != null;
        }

        private static bool TryResolvePizzeriaMaterial(
            string leafName,
            int slotIndex,
            Dictionary<string, Material[]> exactMap,
            Dictionary<string, Material[]> baseMap,
            out Material resolved,
            out string matchedKey)
        {
            resolved = null;
            matchedKey = string.Empty;
            if (string.IsNullOrEmpty(leafName)) return false;

            if (exactMap.TryGetValue(leafName, out var exactMats) && TryPickSlot(exactMats, slotIndex, out resolved))
            {
                matchedKey = leafName;
                return true;
            }

            string baseName = StripNumericSuffix(leafName);
            if (!string.IsNullOrEmpty(baseName) && baseMap.TryGetValue(baseName, out var baseMats) && TryPickSlot(baseMats, slotIndex, out resolved))
            {
                matchedKey = baseName;
                return true;
            }

            if (!string.IsNullOrEmpty(baseName)
                && PizzeriaBaseAliases.TryGetValue(baseName, out var alias)
                && baseMap.TryGetValue(alias, out var aliasMats)
                && TryPickSlot(aliasMats, slotIndex, out resolved))
            {
                matchedKey = alias;
                return true;
            }

            return false;
        }

        private static bool TryPickSlot(Material[] mats, int slotIndex, out Material picked)
        {
            picked = null;
            if (mats == null || mats.Length == 0) return false;

            if (slotIndex >= 0 && slotIndex < mats.Length && mats[slotIndex] != null)
            {
                picked = mats[slotIndex];
                return true;
            }

            foreach (var m in mats)
            {
                if (m != null)
                {
                    picked = m;
                    return true;
                }
            }
            return false;
        }

        private static Dictionary<string, Material> CollectEmbeddedFbxMaterials(string fbxAssetPath)
        {
            var dict = new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(fbxAssetPath)) return dict;

            foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(fbxAssetPath))
            {
                if (sub is Material m && !dict.ContainsKey(m.name))
                    dict[m.name] = m;
            }
            return dict;
        }

        private static Material GetOrCreateStylizedFallback(
            string sourceMaterialName,
            string categoryId,
            VisualSemanticRole role,
            VisualStyleProfile styleProfile,
            Dictionary<string, Material> fbxMaterials,
            Shader stylizedOpaque,
            string matDir,
            Dictionary<string, Material> generatedCache)
        {
            string cacheKey = $"{categoryId}:{sourceMaterialName}";
            if (generatedCache.TryGetValue(cacheKey, out var cached))
                return cached;

            string safeName = sourceMaterialName.Replace("/", "_").Replace("\\", "_");
            string assetPath = $"{matDir}/Mat_CS_{safeName}.mat";
            Material targetMat = AssetDatabase.LoadAssetAtPath<Material>(assetPath);

            fbxMaterials.TryGetValue(sourceMaterialName, out var srcMat);
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
                categoryId,
                role,
                baseCol,
                IllustrateShaderFamily.Opaque,
                mainTex,
                scale,
                offset);

            EditorUtility.SetDirty(targetMat);
            generatedCache[cacheKey] = targetMat;
            return targetMat;
        }

        private static void TryApplyToOpenCutsceneStage(CutsceneBindingProfile bindingProfile)
        {
            var stage = UnityEngine.Object.FindFirstObjectByType<CutsceneStage>();
            if (stage == null || stage.generatedRoot == null) return;

            for (int i = 0; i < stage.generatedRoot.childCount; i++)
            {
                var child = stage.generatedRoot.GetChild(i).gameObject;
                if (child.name.EndsWith("_StageRoot", StringComparison.OrdinalIgnoreCase))
                {
                    CutsceneSceneAssembler.ApplyMaterialBindings(child, bindingProfile);
                    EditorSceneManager.MarkSceneDirty(stage.gameObject.scene);
                }
            }
        }

        private static string ExtractLeafName(string rendererPath)
        {
            if (string.IsNullOrEmpty(rendererPath)) return string.Empty;
            int idx = rendererPath.LastIndexOf('/');
            return idx >= 0 && idx + 1 < rendererPath.Length
                ? rendererPath.Substring(idx + 1)
                : rendererPath;
        }

        private static string StripNumericSuffix(string name)
        {
            return string.IsNullOrEmpty(name) ? string.Empty : TrailingIndexRegex.Replace(name, string.Empty).Trim();
        }
    }
}

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using ScaryParty.Pizzeria.Authoring;

namespace ScaryParty.Pizzeria.Editor
{
    public class PizzeriaMaterialApplicator
    {
        [System.Serializable]
        public class SlotBackupEntry
        {
            public string relativePath;
            public string nodeGuid;
            public int slotIndex;
            public string materialAssetGuid;
        }

        [System.Serializable]
        public class MaterialBackupData
        {
            public string timestamp;
            public List<SlotBackupEntry> entries = new List<SlotBackupEntry>();
        }

        public class AnalysisReport
        {
            public int totalRenderers;
            public int totalSlots;
            public int nullSlots;
            public int classifiedSlots;
            public int preservedSlots;
            public Dictionary<MaterialPaletteRole, int> roleCounts = new Dictionary<MaterialPaletteRole, int>();
            public List<string> warnings = new List<string>();
        }

        public class ApplyReport
        {
            public int updatedRenderers;
            public int updatedSlots;
            public int preservedSlots;
            public List<string> modifiedMaterials = new List<string>();
        }

        private const string BackupPrefsKey = "PizzeriaMaterialApplicator_Backup_";

        /// <summary>
        /// Obtém ou cria os materiais persistentes gerados para cada papel na pasta configurada.
        /// Atualiza os assets existentes sem recriar novos sufixos a cada execução (idempotente).
        /// </summary>
        public static Dictionary<MaterialPaletteRole, Material> GetOrCreateGeneratedMaterials(PizzeriaAppearanceProfile profile)
        {
            var result = new Dictionary<MaterialPaletteRole, Material>();
            if (profile == null) return result;

            profile.EnsureShaders();
            string folder = string.IsNullOrEmpty(profile.generatedMaterialsFolder) 
                ? "Assets/Art/Pizzeria/Materials/Generated" 
                : profile.generatedMaterialsFolder;

            EnsureFolderExists(folder);

            Shader targetShader = profile.opaqueShader != null 
                ? profile.opaqueShader 
                : Shader.Find("Distant Lands/Illustrate/Opaque") ?? Shader.Find("Universal Render Pipeline/Lit");

            foreach (MaterialPaletteRole role in Enum.GetValues(typeof(MaterialPaletteRole)))
            {
                if (role == MaterialPaletteRole.Preserve || role == MaterialPaletteRole.Custom)
                    continue;

                string assetName = $"Mat_Gen_{role}.mat";
                string assetPath = $"{folder}/{assetName}";

                Material mat = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
                bool isNew = false;
                if (mat == null)
                {
                    mat = new Material(targetShader);
                    isNew = true;
                }
                else if (mat.shader != targetShader)
                {
                    mat.shader = targetShader;
                }

                Color roleColor = profile.GetRoleColor(role);

                // Configura propriedades Distant Lands / URP
                ConfigureMaterialProperties(mat, roleColor, profile);

                if (isNew)
                {
                    AssetDatabase.CreateAsset(mat, assetPath);
                }
                else
                {
                    EditorUtility.SetDirty(mat);
                }

                result[role] = mat;
            }

            AssetDatabase.SaveAssets();
            return result;
        }

        private static void ConfigureMaterialProperties(Material mat, Color color, PizzeriaAppearanceProfile profile)
        {
            // Propriedades Distant Lands Illustrate
            if (mat.HasProperty("_MainColor"))
                mat.SetColor("_MainColor", color);
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", color);
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);

            // Shading limpo toon
            if (profile.disableWaves && mat.HasProperty("_UseWave"))
                mat.SetFloat("_UseWave", 0f);

            if (profile.disableHSVVariation && mat.HasProperty("_UseHSVVariation"))
                mat.SetFloat("_UseHSVVariation", 0f);

            if (mat.HasProperty("_LightingMode"))
                mat.SetFloat("_LightingMode", profile.enableToonLighting ? 1f : 0f);

            if (mat.HasProperty("_UseShadows"))
                mat.SetFloat("_UseShadows", profile.enableShadows ? 1f : 0f);

            if (mat.HasProperty("_UseTriplanar"))
                mat.SetFloat("_UseTriplanar", 0f);

            if (mat.HasProperty("_CullMode"))
                mat.SetFloat("_CullMode", 2f); // Back culling
        }

        /// <summary>
        /// Analisa os renderers e slots do prefab e classifica de acordo com o perfil e regras de prioridade.
        /// </summary>
        public static AnalysisReport AnalyzePrefab(GameObject prefab, PizzeriaAppearanceProfile profile)
        {
            var report = new AnalysisReport();
            if (prefab == null) return report;

            MeshRenderer[] renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
            report.totalRenderers = renderers.Length;

            foreach (var r in renderers)
            {
                if (r == null || ShouldIgnoreRenderer(r)) continue;

                Material[] mats = r.sharedMaterials;
                if (mats == null || mats.Length == 0)
                {
                    report.warnings.Add($"{GetHierarchyPath(r.transform, prefab.transform)}: MeshRenderer sem materiais.");
                    continue;
                }

                report.totalSlots += mats.Length;
                PizzeriaNodeIdentity identity = r.GetComponent<PizzeriaNodeIdentity>();

                for (int s = 0; s < mats.Length; s++)
                {
                    Material m = mats[s];
                    if (m == null)
                    {
                        report.nullSlots++;
                        report.warnings.Add($"{GetHierarchyPath(r.transform, prefab.transform)} [Slot {s}]: Material nulo.");
                    }

                    MaterialPaletteRole role = ResolveRole(r, s, profile, identity);
                    if (role == MaterialPaletteRole.Preserve)
                    {
                        report.preservedSlots++;
                    }
                    else
                    {
                        report.classifiedSlots++;
                    }

                    if (!report.roleCounts.ContainsKey(role))
                        report.roleCounts[role] = 0;
                    report.roleCounts[role]++;
                }
            }

            return report;
        }

        /// <summary>
        /// Aplica os materiais da paleta aos slots correspondentes no prefab de forma idempotente,
        /// preservando o tamanho do array de sharedMaterials e posições originais.
        /// </summary>
        public static ApplyReport ApplyMaterialsToPrefab(GameObject prefab, PizzeriaAppearanceProfile profile, bool saveAsset = true)
        {
            var report = new ApplyReport();
            if (prefab == null || profile == null) return report;

            var generatedMats = GetOrCreateGeneratedMaterials(profile);
            MeshRenderer[] renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);

            // 1. Criar backup do estado anterior para permitir restauração
            BackupCurrentMaterials(prefab);

            for (int i = 0; i < renderers.Length; i++)
            {
                MeshRenderer r = renderers[i];
                if (r == null || ShouldIgnoreRenderer(r)) continue;

                Material[] currentMats = r.sharedMaterials;
                int slotCount = currentMats != null && currentMats.Length > 0 ? currentMats.Length : 1;
                Material[] newMats = new Material[slotCount];
                bool rendererModified = false;
                PizzeriaNodeIdentity identity = r.GetComponent<PizzeriaNodeIdentity>();

                for (int s = 0; s < slotCount; s++)
                {
                    Material oldMat = (currentMats != null && s < currentMats.Length) ? currentMats[s] : null;
                    MaterialPaletteRole role = ResolveRole(r, s, profile, identity);

                    if (role == MaterialPaletteRole.Preserve)
                    {
                        newMats[s] = oldMat;
                        report.preservedSlots++;
                        continue;
                    }

                    if (generatedMats.TryGetValue(role, out Material targetMat))
                    {
                        newMats[s] = targetMat;
                        report.updatedSlots++;
                        rendererModified = true;
                    }
                    else
                    {
                        newMats[s] = oldMat;
                        report.preservedSlots++;
                    }
                }

                if (rendererModified)
                {
                    Undo.RecordObject(r, "Apply Pizzeria Materials");
                    r.sharedMaterials = newMats;
                    EditorUtility.SetDirty(r);
                    report.updatedRenderers++;
                }
            }

            if (saveAsset)
            {
                EditorUtility.SetDirty(prefab);
                string assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(prefab);
                if (string.IsNullOrEmpty(assetPath))
                    assetPath = AssetDatabase.GetAssetPath(prefab);

                if (!string.IsNullOrEmpty(assetPath))
                {
                    PrefabUtility.SavePrefabAsset(prefab);
                }
            }

            return report;
        }

        /// <summary>
        /// Restaura os materiais anteriores dos slots modificados a partir do backup armazenado.
        /// </summary>
        public static int RestorePreviousMaterials(GameObject prefab)
        {
            if (prefab == null) return 0;

            string key = BackupPrefsKey + prefab.name;
            string json = EditorPrefs.GetString(key, string.Empty);
            if (string.IsNullOrEmpty(json))
            {
                Debug.LogWarning("[PizzeriaMaterialApplicator] Nenhum backup anterior encontrado para restauração.");
                return 0;
            }

            MaterialBackupData backup = JsonUtility.FromJson<MaterialBackupData>(json);
            if (backup == null || backup.entries == null || backup.entries.Count == 0)
                return 0;

            Dictionary<string, Material> assetCache = new Dictionary<string, Material>();
            MeshRenderer[] renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
            int restoredCount = 0;

            foreach (var entry in backup.entries)
            {
                Transform targetT = prefab.transform.Find(entry.relativePath);
                if (targetT == null && !string.IsNullOrEmpty(entry.nodeGuid))
                {
                    // Tenta achar por PizzeriaNodeIdentity
                    foreach (var r in renderers)
                    {
                        var id = r.GetComponent<PizzeriaNodeIdentity>();
                        if (id != null && id.nodeGuid == entry.nodeGuid)
                        {
                            targetT = r.transform;
                            break;
                        }
                    }
                }

                if (targetT == null) continue;
                MeshRenderer rend = targetT.GetComponent<MeshRenderer>();
                if (rend == null) continue;

                Material[] mats = rend.sharedMaterials;
                if (entry.slotIndex < 0 || entry.slotIndex >= mats.Length) continue;

                Material originalMat = null;
                if (!string.IsNullOrEmpty(entry.materialAssetGuid))
                {
                    if (!assetCache.TryGetValue(entry.materialAssetGuid, out originalMat))
                    {
                        string path = AssetDatabase.GUIDToAssetPath(entry.materialAssetGuid);
                        if (!string.IsNullOrEmpty(path))
                        {
                            originalMat = AssetDatabase.LoadAssetAtPath<Material>(path);
                        }
                        assetCache[entry.materialAssetGuid] = originalMat;
                    }
                }

                mats[entry.slotIndex] = originalMat;
                rend.sharedMaterials = mats;
                EditorUtility.SetDirty(rend);
                restoredCount++;
            }

            EditorUtility.SetDirty(prefab);
            string pPath = AssetDatabase.GetAssetPath(prefab);
            if (!string.IsNullOrEmpty(pPath))
                PrefabUtility.SavePrefabAsset(prefab);

            return restoredCount;
        }

        private static void BackupCurrentMaterials(GameObject prefab)
        {
            MeshRenderer[] renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
            MaterialBackupData data = new MaterialBackupData
            {
                timestamp = DateTime.UtcNow.ToString("o")
            };

            foreach (var r in renderers)
            {
                if (r == null || ShouldIgnoreRenderer(r)) continue;
                string relPath = GetHierarchyPath(r.transform, prefab.transform);
                PizzeriaNodeIdentity id = r.GetComponent<PizzeriaNodeIdentity>();
                string guid = id != null ? id.nodeGuid : string.Empty;

                Material[] mats = r.sharedMaterials;
                for (int s = 0; s < mats.Length; s++)
                {
                    string matGuid = string.Empty;
                    if (mats[s] != null)
                    {
                        string path = AssetDatabase.GetAssetPath(mats[s]);
                        if (!string.IsNullOrEmpty(path))
                            matGuid = AssetDatabase.AssetPathToGUID(path);
                    }

                    data.entries.Add(new SlotBackupEntry
                    {
                        relativePath = relPath,
                        nodeGuid = guid,
                        slotIndex = s,
                        materialAssetGuid = matGuid
                    });
                }
            }

            string json = JsonUtility.ToJson(data);
            EditorPrefs.SetString(BackupPrefsKey + prefab.name, json);
        }

        public static MaterialPaletteRole ResolveRole(MeshRenderer r, int slotIndex, PizzeriaAppearanceProfile profile, PizzeriaNodeIdentity identity)
        {
            if (profile == null) return MaterialPaletteRole.ParedeClara;

            string relPath = GetHierarchyPath(r.transform, r.transform.root);
            string guid = identity != null ? identity.nodeGuid : string.Empty;

            // 1. Override explícito no perfil
            if (profile.explicitOverrides != null)
            {
                for (int i = 0; i < profile.explicitOverrides.Count; i++)
                {
                    var ov = profile.explicitOverrides[i];
                    bool guidMatch = !string.IsNullOrEmpty(guid) && ov.nodeGuid == guid;
                    bool pathMatch = !string.IsNullOrEmpty(ov.nodePath) && (ov.nodePath == relPath || r.name == ov.nodePath);
                    if ((guidMatch || pathMatch) && ov.slotIndex == slotIndex)
                    {
                        return ov.role;
                    }
                }
            }

            // 2. Regra por nome / sugestão do perfil
            string originalMatName = string.Empty;
            Material[] mats = r.sharedMaterials;
            if (mats != null && slotIndex < mats.Length && mats[slotIndex] != null)
                originalMatName = mats[slotIndex].name;

            int cat = identity != null ? (int)identity.category : 0;
            return profile.SuggestRole(r.name, originalMatName, cat);
        }

        private static bool ShouldIgnoreRenderer(MeshRenderer r)
        {
            // Ignora Canvas, UI, Partículas, TextMeshPro, SheetText e RectTransform
            if (r.GetComponent<Canvas>() != null) return true;
            if (r.GetComponent<ParticleSystem>() != null) return true;
            if (r.name.StartsWith("UI_", StringComparison.OrdinalIgnoreCase)) return true;
            if (r.name.Equals("SheetText", StringComparison.OrdinalIgnoreCase)) return true;
            if (r.GetComponent<RectTransform>() != null) return true;
            return false;
        }

        private static string GetHierarchyPath(Transform current, Transform root)
        {
            if (current == root) return "";
            string path = current.name;
            Transform parent = current.parent;
            while (parent != null && parent != root)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }

        private static void EnsureFolderExists(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath)) return;

            string[] parts = folderPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }
    }
}
#endif

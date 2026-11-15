#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using ScaryParty.Pizzeria.Authoring;
using ScaryParty.Pizzeria.Visual;

namespace ScaryParty.Pizzeria.Editor
{
    /// <summary>
    /// Utilitário de auditoria visual e estrutural pré e pós geração da variante Cel Shading.
    /// Valida integridade do prefab original, conformidade de renderers, slots, shaders
    /// e ausência de regressões em componentes críticos de gameplay/rede.
    /// </summary>
    public static class PizzeriaToonAudit
    {
        [System.Serializable]
        public class AuditReport
        {
            public string timestamp;
            public string targetPrefabPath;
            public string targetPrefabGuid;
            public string sourcePrefabHash;
            public int totalGameObjects;
            public int totalMeshRenderers;
            public int totalSkinnedMeshRenderers;
            public int totalSlots;
            public int nullSlots;
            public int uniqueMaterialsCount;
            public int networkObjectsCount;
            public int stationsCount;
            public int collidersCount;
            public List<string> warnings = new List<string>();
            public List<string> errors = new List<string>();
            public Dictionary<string, int> shaderDistribution = new Dictionary<string, int>();
        }

        public static AuditReport AuditPrefab(string prefabPath)
        {
            var report = new AuditReport
            {
                timestamp = DateTime.UtcNow.ToString("o"),
                targetPrefabPath = prefabPath
            };

            if (string.IsNullOrEmpty(prefabPath) || !File.Exists(prefabPath))
            {
                report.errors.Add($"Prefab não encontrado no caminho: {prefabPath}");
                return report;
            }

            report.targetPrefabGuid = AssetDatabase.AssetPathToGUID(prefabPath);
            report.sourcePrefabHash = ComputeFileMD5(prefabPath);

            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                Transform[] allTransforms = root.GetComponentsInChildren<Transform>(true);
                report.totalGameObjects = allTransforms.Length;

                MeshRenderer[] meshRenderers = root.GetComponentsInChildren<MeshRenderer>(true);
                report.totalMeshRenderers = meshRenderers.Length;

                SkinnedMeshRenderer[] skinnedRenderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                report.totalSkinnedMeshRenderers = skinnedRenderers.Length;

                var uniqueMats = new HashSet<Material>();

                void InspectRenderer(Renderer r)
                {
                    Material[] mats = r.sharedMaterials;
                    if (mats == null || mats.Length == 0)
                    {
                        report.warnings.Add($"Renderer sem nenhum slot: {GetHierarchyPath(r.transform, root.transform)}");
                        return;
                    }

                    for (int s = 0; s < mats.Length; s++)
                    {
                        report.totalSlots++;
                        Material m = mats[s];
                        if (m == null)
                        {
                            report.nullSlots++;
                            report.warnings.Add($"Slot nulo [{s}] em {GetHierarchyPath(r.transform, root.transform)}");
                            continue;
                        }

                        uniqueMats.Add(m);
                        string shaderName = m.shader != null ? m.shader.name : "(Missing Shader)";
                        if (!report.shaderDistribution.ContainsKey(shaderName))
                            report.shaderDistribution[shaderName] = 0;
                        report.shaderDistribution[shaderName]++;
                    }
                }

                foreach (var mr in meshRenderers) InspectRenderer(mr);
                foreach (var smr in skinnedRenderers) InspectRenderer(smr);

                report.uniqueMaterialsCount = uniqueMats.Count;

                // Checagens estruturais de gameplay e rede
                var netObjs = root.GetComponentsInChildren<Unity.Netcode.NetworkObject>(true);
                report.networkObjectsCount = netObjs.Length;

                var stations = root.GetComponentsInChildren<Stations.StationView>(true);
                report.stationsCount = stations.Length;

                var colliders = root.GetComponentsInChildren<Collider>(true);
                report.collidersCount = colliders.Length;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            return report;
        }

        public static bool CompareAudits(AuditReport source, AuditReport variant, out List<string> differences)
        {
            differences = new List<string>();

            if (source.networkObjectsCount != variant.networkObjectsCount)
            {
                differences.Add($"Divergência de NetworkObject: Origem={source.networkObjectsCount}, Variante={variant.networkObjectsCount}");
            }

            if (source.stationsCount != variant.stationsCount)
            {
                differences.Add($"Divergência de Estações de trabalho: Origem={source.stationsCount}, Variante={variant.stationsCount}");
            }

            if (source.collidersCount != variant.collidersCount)
            {
                differences.Add($"Divergência de Colisores: Origem={source.collidersCount}, Variante={variant.collidersCount}");
            }

            return differences.Count == 0;
        }

        public static string SaveAuditReport(AuditReport report, string filename)
        {
            string dir = "Assets/Piaaria/Art/Pizzeria/Toon/Reports";
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            string path = $"{dir}/{filename}";
            string json = EditorJsonUtility.ToJson(report, true);
            File.WriteAllText(path, json, Encoding.UTF8);
            AssetDatabase.Refresh();
            return path;
        }

        private static string GetHierarchyPath(Transform t, Transform root)
        {
            string p = t.name;
            Transform curr = t.parent;
            while (curr != null && curr != root)
            {
                p = $"{curr.name}/{p}";
                curr = curr.parent;
            }
            return p;
        }

        public static string ComputeFileMD5(string filePath)
        {
            if (!File.Exists(filePath)) return string.Empty;
            using (var md5 = MD5.Create())
            using (var stream = File.OpenRead(filePath))
            {
                byte[] hash = md5.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}
#endif

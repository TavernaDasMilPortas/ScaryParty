#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ScaryParty.Pizzeria.Editor.Migration
{
    public class FbxNodeInfo
    {
        public string name;
        public string path;
        public Vector3 localPosition;
        public Vector3 localEulerAngles;
        public Vector3 localScale;
        public Bounds localMeshBounds;
        public Bounds worldBounds;
        public int vertexCount;
        public int triangleCount;
        public bool hasMesh;
        public bool isEmptyMesh;
        public List<string> materialNames = new List<string>();
        public string suggestedCategory; // Architecture, Furniture, Station, ItemCandidate, Decoration, Auxiliary, Ignored
    }

    public class FbxAnalysisResult
    {
        public string fbxAssetPath;
        public string fbxGuid;
        public int totalNodes;
        public int nodesWithMesh;
        public int emptyMeshNodes;
        public int totalTriangles;
        public int totalVertices;
        public Bounds architectureBounds;
        public Bounds totalBounds;
        public List<FbxNodeInfo> nodes = new List<FbxNodeInfo>();
        public HashSet<string> allMaterialNames = new HashSet<string>();
    }

    public static class PizzeriaFbxAnalyzer
    {
        public const string DefaultFbxPath = "Assets/Art/Pizzeria/Source/pizzariamodular (2)2 (1) (1) (2).fbx";

        public static FbxAnalysisResult Analyze(string fbxPath = null)
        {
            if (string.IsNullOrEmpty(fbxPath))
            {
                fbxPath = DefaultFbxPath;
            }

            var mainAsset = AssetDatabase.LoadMainAssetAtPath(fbxPath);
            if (mainAsset == null)
            {
                // Tenta achar qualquer fbx no Source
                string[] guids = AssetDatabase.FindAssets("t:Model", new[] { "Assets/Art/Pizzeria/Source" });
                if (guids.Length > 0)
                {
                    fbxPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                    mainAsset = AssetDatabase.LoadMainAssetAtPath(fbxPath);
                }
            }

            if (mainAsset == null)
            {
                throw new FileNotFoundException($"FBX da Pizzaria não encontrado em: {fbxPath}");
            }

            var go = mainAsset as GameObject;
            if (go == null)
            {
                throw new InvalidOperationException($"Asset em {fbxPath} não é um GameObject/Model válido.");
            }

            var result = new FbxAnalysisResult
            {
                fbxAssetPath = fbxPath,
                fbxGuid = AssetDatabase.AssetPathToGUID(fbxPath)
            };

            Bounds archBounds = new Bounds(Vector3.zero, Vector3.zero);
            bool archInit = false;
            Bounds totBounds = new Bounds(Vector3.zero, Vector3.zero);
            bool totInit = false;

            Transform rootT = go.transform;
            int totalTris = 0;
            int totalVerts = 0;

            // Coleta nós
            foreach (Transform child in rootT)
            {
                var info = new FbxNodeInfo
                {
                    name = child.name,
                    path = GetHierarchyPath(child, rootT),
                    localPosition = child.localPosition,
                    localEulerAngles = child.localEulerAngles,
                    localScale = child.localScale
                };

                var mf = child.GetComponent<MeshFilter>();
                var rend = child.GetComponent<MeshRenderer>();

                if (rend != null)
                {
                    foreach (var mat in rend.sharedMaterials)
                    {
                        if (mat != null)
                        {
                            info.materialNames.Add(mat.name);
                            result.allMaterialNames.Add(mat.name);
                        }
                    }
                }

                if (mf != null && mf.sharedMesh != null)
                {
                    var mesh = mf.sharedMesh;
                    info.hasMesh = true;
                    info.vertexCount = mesh.vertexCount;
                    info.triangleCount = mesh.triangles.Length / 3;
                    info.localMeshBounds = mesh.bounds;

                    // Bounds no espaço do FBX root
                    info.worldBounds = TransformBounds(mesh.bounds, child.localToWorldMatrix, rootT.worldToLocalMatrix);

                    if (mesh.vertexCount == 0 || mesh.triangles.Length == 0)
                    {
                        info.isEmptyMesh = true;
                        result.emptyMeshNodes++;
                    }
                    else
                    {
                        result.nodesWithMesh++;
                        totalVerts += mesh.vertexCount;
                        totalTris += info.triangleCount;

                        if (!totInit)
                        {
                            totBounds = info.worldBounds;
                            totInit = true;
                        }
                        else
                        {
                            totBounds.Encapsulate(info.worldBounds);
                        }
                    }
                }

                info.suggestedCategory = ClassifyNode(info.name, info.isEmptyMesh);

                if (info.suggestedCategory == "Architecture" && !info.isEmptyMesh)
                {
                    if (!archInit)
                    {
                        archBounds = info.worldBounds;
                        archInit = true;
                    }
                    else
                    {
                        archBounds.Encapsulate(info.worldBounds);
                    }
                }

                result.nodes.Add(info);
            }

            result.totalNodes = result.nodes.Count;
            result.totalTriangles = totalTris;
            result.totalVertices = totalVerts;
            result.architectureBounds = archBounds;
            result.totalBounds = totBounds;

            return result;
        }

        public static string GenerateReport(FbxAnalysisResult r)
        {
            var sb = new StringBuilder();
            sb.AppendLine("================================================================================");
            sb.AppendLine($"RELATÓRIO DE ANÁLISE FBX — PIZZARIA MODULAR");
            sb.AppendLine($"Arquivo: {r.fbxAssetPath} (GUID: {r.fbxGuid})");
            sb.AppendLine($"Total de Nós na Raiz: {r.totalNodes}");
            sb.AppendLine($"Nós com Mesh: {r.nodesWithMesh} | Nós Vazios: {r.emptyMeshNodes}");
            sb.AppendLine($"Vértices: {r.totalVertices} | Triângulos: {r.totalTriangles}");
            sb.AppendLine($"Materiais Únicos ({r.allMaterialNames.Count}): {string.Join(", ", r.allMaterialNames)}");
            sb.AppendLine($"Bounds Totais: Centro {r.totalBounds.center}, Tamanho {r.totalBounds.size}");
            sb.AppendLine($"Bounds Arquitetura: Centro {r.architectureBounds.center}, Tamanho {r.architectureBounds.size}");
            sb.AppendLine("================================================================================");

            var catGroups = new Dictionary<string, List<FbxNodeInfo>>();
            foreach (var n in r.nodes)
            {
                if (!catGroups.ContainsKey(n.suggestedCategory))
                    catGroups[n.suggestedCategory] = new List<FbxNodeInfo>();
                catGroups[n.suggestedCategory].Add(n);
            }

            foreach (var kvp in catGroups)
            {
                sb.AppendLine($"\n--- Categoria: {kvp.Key} ({kvp.Value.Count} nós) ---");
                foreach (var n in kvp.Value)
                {
                    string meshInfo = n.hasMesh ? (n.isEmptyMesh ? "[MESH VAZIA]" : $"Tris: {n.triangleCount}, Bounds: {n.worldBounds.size.x:F2}x{n.worldBounds.size.y:F2}x{n.worldBounds.size.z:F2}") : "[SEM MESH]";
                    sb.AppendLine($"  • {n.name.PadRight(28)} | Pos: {n.localPosition} | {meshInfo}");
                }
            }

            return sb.ToString();
        }

        public static string ClassifyNode(string name, bool isEmptyMesh)
        {
            if (isEmptyMesh) return "Ignored_EmptyMesh";

            string lower = name.ToLowerInvariant();

            // Referência de jogador
            if (lower.Contains("player") || lower == "character" || lower == "avatar")
                return "Auxiliary_Player";

            // Arquitetura
            if (lower.Contains("parede") || lower.Contains("chao") || lower.Contains("piso") ||
                lower.Contains("teto") || lower.Contains("telhado") || lower.Contains("coluna") ||
                lower.Contains("pilar") || lower.Contains("viga") || lower.Contains("porta_") ||
                lower.Contains("janela") || lower.Contains("balcao") || lower.Contains("fachada"))
            {
                return "Architecture";
            }

            // Portas móveis
            if (lower.StartsWith("porta") && !lower.Contains("parede"))
                return "Furniture_Door";

            // Equipamentos principais
            if (lower.Contains("geladeira") || lower.Contains("refrigerator") || lower.Contains("fridge"))
                return "Station_Fridge";
            if (lower.Contains("forno") || lower.Contains("oven"))
                return "Station_Oven";
            if (lower.Contains("lixeira") || lower.Contains("trash") || lower.Contains("bin"))
                return "Station_Trash";
            if (lower.Contains("mesa") || lower.Contains("bancada") || lower.Contains("counter"))
                return "Station_Counter";

            // Utensílios e Itens
            if (lower.Contains("faca") || lower.Contains("knife"))
                return "ItemCandidate_Knife";
            if (lower.Contains("cortador") || lower.Contains("cutter"))
                return "ItemCandidate_Cutter";
            if (lower.Contains("tabua") || lower.Contains("board"))
                return "ItemCandidate_Board";
            if (lower.Contains("forma") || lower.Contains("prato") || lower.Contains("pizza"))
                return "ItemCandidate_PizzaPan";
            if (lower.Contains("mochila") || lower.Contains("bag") || lower.Contains("deliverybag"))
                return "ItemCandidate_Backpack";
            if (lower.Contains("caixa") || lower.Contains("box"))
                return "ItemCandidate_Box";
            if (lower.Contains("espatula") || lower.Contains("pa") || lower.Contains("spatula"))
                return "ItemCandidate_Spatula";
            if (lower.Contains("panela") || lower.Contains("concha") || lower.Contains("pot"))
                return "ItemCandidate_Cookware";

            return "Decoration";
        }

        private static Bounds TransformBounds(Bounds b, Matrix4x4 localToWorld, Matrix4x4 worldToLocal)
        {
            Matrix4x4 m = worldToLocal * localToWorld;
            Vector3 center = m.MultiplyPoint3x4(b.center);
            Vector3 extents = b.extents;

            Vector3 axisX = m.MultiplyVector(new Vector3(extents.x, 0, 0));
            Vector3 axisY = m.MultiplyVector(new Vector3(0, extents.y, 0));
            Vector3 axisZ = m.MultiplyVector(new Vector3(0, 0, extents.z));

            Vector3 newExtents = new Vector3(
                Mathf.Abs(axisX.x) + Mathf.Abs(axisY.x) + Mathf.Abs(axisZ.x),
                Mathf.Abs(axisX.y) + Mathf.Abs(axisY.y) + Mathf.Abs(axisZ.y),
                Mathf.Abs(axisX.z) + Mathf.Abs(axisY.z) + Mathf.Abs(axisZ.z)
            );

            return new Bounds(center, newExtents * 2f);
        }

        private static string GetHierarchyPath(Transform t, Transform root)
        {
            if (t == root) return t.name;
            var list = new List<string>();
            var curr = t;
            while (curr != null && curr != root)
            {
                list.Add(curr.name);
                curr = curr.parent;
            }
            list.Reverse();
            return string.Join("/", list);
        }
    }
}
#endif

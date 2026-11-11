using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using ScaryParty.City.Modular;

namespace ScaryParty.City.Editor.Modular
{
    /// <summary>
    /// Gerador no Editor que cria e valida o kit completo de 12 placeholders da cidade modular:
    /// - Materiais compartilhados por papel funcional
    /// - Malhas procedurais canônicas salvas em assets
    /// - Prefabs estáveis com nós separados (ModuleRoot, Visual, Collision, Anchors, MovingParts)
    /// - ScriptableObject CityModuleCatalog configurado
    /// </summary>
    public static class ModularCityKitBuilder
    {
        public const string MaterialsPath = "Assets/Piaaria/Materials/City/Placeholders";
        public const string MeshesPath = "Assets/Piaaria/Art/City/Modular/Meshes";
        public const string PrefabsPath = "Assets/Piaaria/Prefabs/City/Modules";
        public const string CatalogPath = "Assets/Piaaria/ScriptableObjects/City/Modular/CityModuleCatalog.asset";

        [MenuItem("Tools/Scary Party/Setup e Uso Único/Cidade Modular/Criar Kit de Teste (Placeholders)", false, 275)]
        public static void BuildKitMenuItem()
        {
            BuildCompleteKit(false);
        }

        [MenuItem("Tools/Scary Party/Setup e Uso Único/Cidade Modular/Recriar Kit Completo (Sobrescrever Placeholders)", false, 276)]
        public static void RecreateKitMenuItem()
        {
            if (EditorUtility.DisplayDialog("Recriar Kit Modular",
                "Tem certeza que deseja recriar todas as malhas e prefabs provisórios? Modelos visuais personalizados que não estejam protegidos podem ser redefinidos.", "Sim", "Cancelar"))
            {
                BuildCompleteKit(true);
            }
        }

        [MenuItem("Tools/Scary Party/Uso Comum/Cidade/Validar Integridade do Kit Modular", false, 170)]
        public static void ValidateKitMenuItem()
        {
            CityModuleCatalog catalog = AssetDatabase.LoadAssetAtPath<CityModuleCatalog>(CatalogPath);
            if (catalog == null)
            {
                EditorUtility.DisplayDialog("Validação", "Catálogo não encontrado em " + CatalogPath, "OK");
                return;
            }

            var res = ModularCityValidator.ValidateKit(catalog);
            string msg = res.Summary + "\n\n";
            if (res.errors.Count > 0) msg += "ERROS:\n- " + string.Join("\n- ", res.errors) + "\n\n";
            if (res.warnings.Count > 0) msg += "AVISOS:\n- " + string.Join("\n- ", res.warnings) + "\n\n";
            if (res.passes.Count > 0) msg += "PASSOU:\n- " + string.Join("\n- ", res.passes);

            EditorUtility.DisplayDialog("Resultado da Validação", msg, "OK");
            Debug.Log($"[ModularCityKitBuilder] {msg}");
        }

        /// <summary>
        /// Constrói ou atualiza os 12 placeholders e o catálogo.
        /// Idempotente: se forceRecreate for false, preserva prefabs existentes.
        /// </summary>
        public static CityModuleCatalog BuildCompleteKit(bool forceRecreate = false)
        {
            EnsureDirectories();

            // 1. Cria materiais compartilhados
            var materials = CreateOrLoadSharedMaterials();

            // 2. Cria malhas procedurais dos 12 módulos
            var meshes = CreateOrLoadPlaceholderMeshes(forceRecreate);

            // 3. Cria prefabs dos 12 módulos
            var prefabs = CreateOrLoadPrefabs(materials, meshes, forceRecreate);

            // 4. Cria ou atualiza o CityModuleCatalog
            CityModuleCatalog catalog = CreateOrUpdateCatalog(prefabs);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[ModularCityKitBuilder] Kit modular de 12 peças construído com sucesso! Catálogo em: {CatalogPath}");
            return catalog;
        }

        private static void EnsureDirectories()
        {
            string[] dirs = new[]
            {
                "Assets/Piaaria/Materials/City/Placeholders",
                "Assets/Piaaria/Art/City/Modular/Meshes",
                "Assets/Piaaria/Prefabs/City/Modules",
                "Assets/Piaaria/ScriptableObjects/City/Modular",
                "Assets/Piaaria/Scenes/Tests"
            };

            foreach (string d in dirs)
            {
                if (!Directory.Exists(d))
                {
                    Directory.CreateDirectory(d);
                }
            }
        }

        #region Materials
        public static Dictionary<CityModuleMaterialRole, Material> CreateOrLoadSharedMaterials()
        {
            var dict = new Dictionary<CityModuleMaterialRole, Material>();
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            dict[CityModuleMaterialRole.Wall] = GetOrCreateMaterial("Mat_Modular_Wall", new Color(0.85f, 0.82f, 0.76f), litShader);
            dict[CityModuleMaterialRole.Trim] = GetOrCreateMaterial("Mat_Modular_Trim", new Color(0.55f, 0.53f, 0.50f), litShader);
            dict[CityModuleMaterialRole.Glass] = GetOrCreateMaterial("Mat_Modular_Glass", new Color(0.60f, 0.78f, 0.90f, 0.65f), litShader);
            dict[CityModuleMaterialRole.Wood] = GetOrCreateMaterial("Mat_Modular_Wood", new Color(0.55f, 0.35f, 0.17f), litShader);
            dict[CityModuleMaterialRole.Metal] = GetOrCreateMaterial("Mat_Modular_Metal", new Color(0.30f, 0.32f, 0.35f), litShader);
            dict[CityModuleMaterialRole.Fabric] = GetOrCreateMaterial("Mat_Modular_Fabric", new Color(0.75f, 0.22f, 0.17f), litShader);

            return dict;
        }

        private static Material GetOrCreateMaterial(string matName, Color color, Shader shader)
        {
            string path = $"{MaterialsPath}/{matName}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                mat.name = matName;
                mat.color = color;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);

                if (color.a < 0.99f)
                {
                    // Configuração transparente para vidro
                    mat.SetFloat("_Surface", 1); // Transparent
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    mat.SetInt("_ZWrite", 0);
                    mat.renderQueue = 3000;
                }

                AssetDatabase.CreateAsset(mat, path);
            }
            return mat;
        }
        #endregion

        #region Meshes
        public static Dictionary<CityModuleId, Mesh> CreateOrLoadPlaceholderMeshes(bool forceRecreate)
        {
            var meshes = new Dictionary<CityModuleId, Mesh>();

            foreach (CityModuleId id in Enum.GetValues(typeof(CityModuleId)))
            {
                string path = $"{MeshesPath}/Mesh_Modular_{id}.asset";
                Mesh mesh = forceRecreate ? null : AssetDatabase.LoadAssetAtPath<Mesh>(path);

                if (mesh == null)
                {
                    mesh = GenerateCanonicalMesh(id);
                    mesh.name = $"Mesh_Modular_{id}";
                    AssetDatabase.CreateAsset(mesh, path);
                }

                meshes[id] = mesh;
            }

            return meshes;
        }

        private static Mesh GenerateCanonicalMesh(CityModuleId id)
        {
            Mesh mesh = new Mesh();
            List<Vector3> vertices = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();

            // Suporta até 2 submeshes: submesh 0 = corpo principal (parede/trim), submesh 1 = detalhe (vidro/madeira)
            List<int> submesh0Tris = new List<int>();
            List<int> submesh1Tris = new List<int>();

            switch (id)
            {
                case CityModuleId.A:
                    // Parede lisa: X in [-1, 1], Y in [0, 3], Z in [-0.2, 0]
                    AddBoxGeometry(vertices, normals, uvs, submesh0Tris,
                        new Vector3(0f, 1.5f, -0.1f), new Vector3(2.0f, 3.0f, 0.2f));
                    break;

                case CityModuleId.B:
                    // Parede com janela: parede ao redor do vão + vidro no centro
                    // Vão da janela: X in [-0.5, 0.5], Y in [1.0, 2.2], Z in [-0.2, 0]
                    // Painel inferior: Y in [0, 1.0]
                    AddBoxGeometry(vertices, normals, uvs, submesh0Tris, new Vector3(0f, 0.5f, -0.1f), new Vector3(2.0f, 1.0f, 0.2f));
                    // Painel superior: Y in [2.2, 3.0]
                    AddBoxGeometry(vertices, normals, uvs, submesh0Tris, new Vector3(0f, 2.6f, -0.1f), new Vector3(2.0f, 0.8f, 0.2f));
                    // Lateral esquerda: X in [-1.0, -0.5], Y in [1.0, 2.2]
                    AddBoxGeometry(vertices, normals, uvs, submesh0Tris, new Vector3(-0.75f, 1.6f, -0.1f), new Vector3(0.5f, 1.2f, 0.2f));
                    // Lateral direita: X in [0.5, 1.0], Y in [1.0, 2.2]
                    AddBoxGeometry(vertices, normals, uvs, submesh0Tris, new Vector3(0.75f, 1.6f, -0.1f), new Vector3(0.5f, 1.2f, 0.2f));
                    // Vidro no centro (Submesh 1)
                    AddBoxGeometry(vertices, normals, uvs, submesh1Tris, new Vector3(0f, 1.6f, -0.1f), new Vector3(1.0f, 1.2f, 0.04f));
                    break;

                case CityModuleId.C:
                    // Parede com porta simples: vão livre de 1.0m x 2.2m (X in [-0.5, 0.5], Y in [0, 2.2])
                    // Batente esquerdo
                    AddBoxGeometry(vertices, normals, uvs, submesh0Tris, new Vector3(-0.75f, 1.5f, -0.1f), new Vector3(0.5f, 3.0f, 0.2f));
                    // Batente direito
                    AddBoxGeometry(vertices, normals, uvs, submesh0Tris, new Vector3(0.75f, 1.5f, -0.1f), new Vector3(0.5f, 3.0f, 0.2f));
                    // Dintel superior (Y in [2.2, 3.0])
                    AddBoxGeometry(vertices, normals, uvs, submesh0Tris, new Vector3(0f, 2.6f, -0.1f), new Vector3(1.0f, 0.8f, 0.2f));
                    break;

                case CityModuleId.D:
                    // Parede com vitrine: mureta inferior 0.4m, dintel superior 0.4m, laterais 0.15m, vidro grande
                    AddBoxGeometry(vertices, normals, uvs, submesh0Tris, new Vector3(0f, 0.2f, -0.1f), new Vector3(2.0f, 0.4f, 0.2f));
                    AddBoxGeometry(vertices, normals, uvs, submesh0Tris, new Vector3(0f, 2.8f, -0.1f), new Vector3(2.0f, 0.4f, 0.2f));
                    AddBoxGeometry(vertices, normals, uvs, submesh0Tris, new Vector3(-0.925f, 1.5f, -0.1f), new Vector3(0.15f, 2.2f, 0.2f));
                    AddBoxGeometry(vertices, normals, uvs, submesh0Tris, new Vector3(0.925f, 1.5f, -0.1f), new Vector3(0.15f, 2.2f, 0.2f));
                    // Vidro grande (Submesh 1)
                    AddBoxGeometry(vertices, normals, uvs, submesh1Tris, new Vector3(0f, 1.5f, -0.1f), new Vector3(1.7f, 2.2f, 0.04f));
                    break;

                case CityModuleId.E:
                    // Cobertura plana: 2.0 x 0.2 x 2.0. Origem no centro da face inferior
                    AddBoxGeometry(vertices, normals, uvs, submesh0Tris, new Vector3(0f, 0.1f, 0f), new Vector3(2.0f, 0.2f, 2.0f));
                    break;

                case CityModuleId.F:
                    // Platibanda: 2.0 x 0.4 x 0.2. Origem borda inferior frontal
                    AddBoxGeometry(vertices, normals, uvs, submesh0Tris, new Vector3(0f, 0.2f, -0.1f), new Vector3(2.0f, 0.4f, 0.2f));
                    break;

                case CityModuleId.G:
                    // Janela com venezianas: base de B + duas placas de veneziana
                    AddBoxGeometry(vertices, normals, uvs, submesh0Tris, new Vector3(0f, 0.5f, -0.1f), new Vector3(2.0f, 1.0f, 0.2f));
                    AddBoxGeometry(vertices, normals, uvs, submesh0Tris, new Vector3(0f, 2.6f, -0.1f), new Vector3(2.0f, 0.8f, 0.2f));
                    AddBoxGeometry(vertices, normals, uvs, submesh0Tris, new Vector3(-0.75f, 1.6f, -0.1f), new Vector3(0.5f, 1.2f, 0.2f));
                    AddBoxGeometry(vertices, normals, uvs, submesh0Tris, new Vector3(0.75f, 1.6f, -0.1f), new Vector3(0.5f, 1.2f, 0.2f));
                    // Vidro
                    AddBoxGeometry(vertices, normals, uvs, submesh1Tris, new Vector3(0f, 1.6f, -0.1f), new Vector3(1.0f, 1.2f, 0.04f));
                    // Veneziana esquerda e direita (Submesh 1)
                    AddBoxGeometry(vertices, normals, uvs, submesh1Tris, new Vector3(-0.4f, 1.6f, 0.02f), new Vector3(0.2f, 1.15f, 0.04f));
                    AddBoxGeometry(vertices, normals, uvs, submesh1Tris, new Vector3(0.4f, 1.6f, 0.02f), new Vector3(0.2f, 1.15f, 0.04f));
                    break;

                case CityModuleId.H:
                    // Janela com floreira: base de B + caixa de floreira saliente (avanço de 0.30m para fora)
                    AddBoxGeometry(vertices, normals, uvs, submesh0Tris, new Vector3(0f, 0.5f, -0.1f), new Vector3(2.0f, 1.0f, 0.2f));
                    AddBoxGeometry(vertices, normals, uvs, submesh0Tris, new Vector3(0f, 2.6f, -0.1f), new Vector3(2.0f, 0.8f, 0.2f));
                    AddBoxGeometry(vertices, normals, uvs, submesh0Tris, new Vector3(-0.75f, 1.6f, -0.1f), new Vector3(0.5f, 1.2f, 0.2f));
                    AddBoxGeometry(vertices, normals, uvs, submesh0Tris, new Vector3(0.75f, 1.6f, -0.1f), new Vector3(0.5f, 1.2f, 0.2f));
                    // Vidro
                    AddBoxGeometry(vertices, normals, uvs, submesh1Tris, new Vector3(0f, 1.6f, -0.1f), new Vector3(1.0f, 1.2f, 0.04f));
                    // Floreira saliente: avanço Z de 0 a +0.3m
                    AddBoxGeometry(vertices, normals, uvs, submesh1Tris, new Vector3(0f, 0.85f, 0.15f), new Vector3(1.2f, 0.25f, 0.30f));
                    break;

                case CityModuleId.I:
                    // Garagem 4m x 3m x 0.2m: dintel 0.6m, laterais 0.4m, portão fechado no meio
                    AddBoxGeometry(vertices, normals, uvs, submesh0Tris, new Vector3(0f, 2.7f, -0.1f), new Vector3(4.0f, 0.6f, 0.2f));
                    AddBoxGeometry(vertices, normals, uvs, submesh0Tris, new Vector3(-1.8f, 1.2f, -0.1f), new Vector3(0.4f, 2.4f, 0.2f));
                    AddBoxGeometry(vertices, normals, uvs, submesh0Tris, new Vector3(1.8f, 1.2f, -0.1f), new Vector3(0.4f, 2.4f, 0.2f));
                    // Portão fechado (Submesh 1)
                    AddBoxGeometry(vertices, normals, uvs, submesh1Tris, new Vector3(0f, 1.2f, -0.1f), new Vector3(3.2f, 2.4f, 0.08f));
                    break;

                case CityModuleId.J:
                    // Porta dupla: vão livre de 1.6m x 2.2m (X in [-0.8, 0.8], Y in [0, 2.2])
                    AddBoxGeometry(vertices, normals, uvs, submesh0Tris, new Vector3(-0.9f, 1.5f, -0.1f), new Vector3(0.2f, 3.0f, 0.2f));
                    AddBoxGeometry(vertices, normals, uvs, submesh0Tris, new Vector3(0.9f, 1.5f, -0.1f), new Vector3(0.2f, 3.0f, 0.2f));
                    AddBoxGeometry(vertices, normals, uvs, submesh0Tris, new Vector3(0f, 2.6f, -0.1f), new Vector3(1.6f, 0.8f, 0.2f));
                    break;

                case CityModuleId.K:
                    // Toldo separado: 2.0m x 0.4m x 0.8m. Origem no topo traseiro (0, 0, 0)
                    // Malha inclinada saindo de Z=0, Y=0 até Z=0.8, Y=-0.4
                    AddAwningGeometry(vertices, normals, uvs, submesh0Tris, 2.0f, 0.4f, 0.8f);
                    break;

                case CityModuleId.L:
                    // Pilar de canto: 0.20m x 3.0m x 0.20m. Origem no centro da base
                    AddBoxGeometry(vertices, normals, uvs, submesh0Tris, new Vector3(0f, 1.5f, 0f), new Vector3(0.20f, 3.0f, 0.20f));
                    break;
            }

            mesh.vertices = vertices.ToArray();
            mesh.normals = normals.ToArray();
            mesh.uv = uvs.ToArray();

            if (submesh1Tris.Count > 0)
            {
                mesh.subMeshCount = 2;
                mesh.SetTriangles(submesh0Tris, 0);
                mesh.SetTriangles(submesh1Tris, 1);
            }
            else
            {
                mesh.subMeshCount = 1;
                mesh.SetTriangles(submesh0Tris, 0);
            }

            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddBoxGeometry(List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> tris, Vector3 center, Vector3 size)
        {
            Vector3 ext = size * 0.5f;
            int startIdx = verts.Count;

            // 6 faces do paralelepípedo
            Vector3[] faceNormals = new[]
            {
                Vector3.forward, Vector3.back, Vector3.up, Vector3.down, Vector3.right, Vector3.left
            };

            for (int f = 0; f < 6; f++)
            {
                Vector3 n = faceNormals[f];
                Vector3 u = (f == 2 || f == 3) ? Vector3.right : Vector3.Cross(n, Vector3.up);
                Vector3 v = Vector3.Cross(u, n);

                Vector3 p0 = center + n * Vector3.Dot(ext, n) - u * Vector3.Dot(ext, u) - v * Vector3.Dot(ext, v);
                Vector3 p1 = center + n * Vector3.Dot(ext, n) + u * Vector3.Dot(ext, u) - v * Vector3.Dot(ext, v);
                Vector3 p2 = center + n * Vector3.Dot(ext, n) + u * Vector3.Dot(ext, u) + v * Vector3.Dot(ext, v);
                Vector3 p3 = center + n * Vector3.Dot(ext, n) - u * Vector3.Dot(ext, u) + v * Vector3.Dot(ext, v);

                int idx = verts.Count;
                verts.Add(p0); verts.Add(p1); verts.Add(p2); verts.Add(p3);
                norms.Add(n); norms.Add(n); norms.Add(n); norms.Add(n);
                uvs.Add(new Vector2(0f, 0f)); uvs.Add(new Vector2(1f, 0f));
                uvs.Add(new Vector2(1f, 1f)); uvs.Add(new Vector2(0f, 1f));

                tris.Add(idx); tris.Add(idx + 1); tris.Add(idx + 2);
                tris.Add(idx); tris.Add(idx + 2); tris.Add(idx + 3);
            }
        }

        private static void AddAwningGeometry(List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> tris, float width, float height, float depth)
        {
            float halfW = width * 0.5f;

            // Superfície superior inclinada
            Vector3 pTL = new Vector3(-halfW, 0f, 0f);
            Vector3 pTR = new Vector3(halfW, 0f, 0f);
            Vector3 pBR = new Vector3(halfW, -height, depth);
            Vector3 pBL = new Vector3(-halfW, -height, depth);

            Vector3 norm = Vector3.Cross(pTR - pTL, pBL - pTL).normalized;

            int idx = verts.Count;
            verts.Add(pTL); verts.Add(pTR); verts.Add(pBR); verts.Add(pBL);
            norms.Add(norm); norms.Add(norm); norms.Add(norm); norms.Add(norm);
            uvs.Add(new Vector2(0, 1)); uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(1, 0)); uvs.Add(new Vector2(0, 0));
            tris.Add(idx); tris.Add(idx + 1); tris.Add(idx + 2);
            tris.Add(idx); tris.Add(idx + 2); tris.Add(idx + 3);

            // Abas laterais
            Vector3 normL = Vector3.left;
            int idxL = verts.Count;
            verts.Add(pTL); verts.Add(pBL); verts.Add(new Vector3(-halfW, -height, 0f));
            norms.Add(normL); norms.Add(normL); norms.Add(normL);
            uvs.Add(new Vector2(0, 1)); uvs.Add(new Vector2(1, 0)); uvs.Add(new Vector2(0, 0));
            tris.Add(idxL); tris.Add(idxL + 1); tris.Add(idxL + 2);

            Vector3 normR = Vector3.right;
            int idxR = verts.Count;
            verts.Add(pTR); verts.Add(new Vector3(halfW, -height, 0f)); verts.Add(pBR);
            norms.Add(normR); norms.Add(normR); norms.Add(normR);
            uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0));
            tris.Add(idxR); tris.Add(idxR + 1); tris.Add(idxR + 2);
        }
        #endregion

        #region Prefabs
        public static Dictionary<CityModuleId, GameObject> CreateOrLoadPrefabs(
            Dictionary<CityModuleMaterialRole, Material> materials,
            Dictionary<CityModuleId, Mesh> meshes,
            bool forceRecreate)
        {
            var prefabs = new Dictionary<CityModuleId, GameObject>();

            foreach (CityModuleId id in Enum.GetValues(typeof(CityModuleId)))
            {
                string path = $"{PrefabsPath}/Prefab_Modular_{id}.prefab";
                GameObject prefab = forceRecreate ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (prefab == null)
                {
                    GameObject tempGo = BuildModuleGameObject(id, meshes[id], materials);
                    prefab = PrefabUtility.SaveAsPrefabAsset(tempGo, path);
                    UnityEngine.Object.DestroyImmediate(tempGo);
                }

                prefabs[id] = prefab;
            }

            return prefabs;
        }

        private static GameObject BuildModuleGameObject(CityModuleId id, Mesh mesh, Dictionary<CityModuleMaterialRole, Material> materials)
        {
            GameObject rootGo = new GameObject($"Module_{id}");
            CityModuleInstance instance = rootGo.AddComponent<CityModuleInstance>();
            instance.moduleId = id;

            // 1. Nó Visual
            GameObject visGo = new GameObject("Visual");
            visGo.transform.SetParent(rootGo.transform, false);
            MeshFilter mf = visGo.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            MeshRenderer mr = visGo.AddComponent<MeshRenderer>();

            // Atribui materiais apropriados conforme o id e submeshes
            AssignModuleMaterials(id, mr, mesh.subMeshCount, materials);

            // 2. Nó Collision (colisores independentes e controlados)
            GameObject colGo = new GameObject("Collision");
            colGo.transform.SetParent(rootGo.transform, false);
            BuildModuleColliders(id, colGo);

            // 3. Nó Anchors (posições de encaixe, entrada e navegação)
            GameObject ancGo = new GameObject("Anchors");
            ancGo.transform.SetParent(rootGo.transform, false);
            BuildModuleAnchors(id, ancGo);

            // 4. Nó MovingParts (folhas móveis de portas/portões)
            GameObject movGo = new GameObject("MovingParts");
            movGo.transform.SetParent(rootGo.transform, false);
            BuildModuleMovingParts(id, movGo, materials);

            instance.AutoWireHierarchy();
            return rootGo;
        }

        private static void AssignModuleMaterials(CityModuleId id, MeshRenderer mr, int subMeshCount,
            Dictionary<CityModuleMaterialRole, Material> materials)
        {
            Material matWall = materials[CityModuleMaterialRole.Wall];
            Material matTrim = materials[CityModuleMaterialRole.Trim];
            Material matGlass = materials[CityModuleMaterialRole.Glass];
            Material matWood = materials[CityModuleMaterialRole.Wood];
            Material matMetal = materials[CityModuleMaterialRole.Metal];
            Material matFabric = materials[CityModuleMaterialRole.Fabric];

            if (subMeshCount > 1)
            {
                switch (id)
                {
                    case CityModuleId.B:
                    case CityModuleId.D:
                        mr.sharedMaterials = new[] { matWall, matGlass };
                        break;
                    case CityModuleId.G:
                    case CityModuleId.H:
                        mr.sharedMaterials = new[] { matWall, matWood };
                        break;
                    case CityModuleId.I:
                        mr.sharedMaterials = new[] { matWall, matMetal };
                        break;
                    default:
                        mr.sharedMaterials = new[] { matWall, matTrim };
                        break;
                }
            }
            else
            {
                switch (id)
                {
                    case CityModuleId.E:
                    case CityModuleId.F:
                    case CityModuleId.L:
                        mr.sharedMaterial = matTrim;
                        break;
                    case CityModuleId.K:
                        mr.sharedMaterial = matFabric;
                        break;
                    default:
                        mr.sharedMaterial = matWall;
                        break;
                }
            }
        }

        private static void BuildModuleColliders(CityModuleId id, GameObject colGo)
        {
            switch (id)
            {
                case CityModuleId.A:
                case CityModuleId.B:
                case CityModuleId.D:
                case CityModuleId.G:
                case CityModuleId.H:
                    // Colisor inteiro da parede sólida
                    BoxCollider box = colGo.AddComponent<BoxCollider>();
                    box.center = new Vector3(0f, 1.5f, -0.1f);
                    box.size = new Vector3(2.0f, 3.0f, 0.2f);
                    break;

                case CityModuleId.C:
                    // Porta simples: NÃO BLOQUEAR O VÃO!
                    // Batente esquerdo (X in [-1.0, -0.5])
                    BoxCollider colCL = colGo.AddComponent<BoxCollider>();
                    colCL.center = new Vector3(-0.75f, 1.5f, -0.1f);
                    colCL.size = new Vector3(0.5f, 3.0f, 0.2f);

                    // Batente direito (X in [0.5, 1.0])
                    BoxCollider colCR = colGo.AddComponent<BoxCollider>();
                    colCR.center = new Vector3(0.75f, 1.5f, -0.1f);
                    colCR.size = new Vector3(0.5f, 3.0f, 0.2f);

                    // Dintel superior (Y in [2.2, 3.0])
                    BoxCollider colCT = colGo.AddComponent<BoxCollider>();
                    colCT.center = new Vector3(0f, 2.6f, -0.1f);
                    colCT.size = new Vector3(1.0f, 0.8f, 0.2f);
                    break;

                case CityModuleId.E:
                    BoxCollider boxE = colGo.AddComponent<BoxCollider>();
                    boxE.center = new Vector3(0f, 0.1f, 0f);
                    boxE.size = new Vector3(2.0f, 0.2f, 2.0f);
                    break;

                case CityModuleId.F:
                    BoxCollider boxF = colGo.AddComponent<BoxCollider>();
                    boxF.center = new Vector3(0f, 0.2f, -0.1f);
                    boxF.size = new Vector3(2.0f, 0.4f, 0.2f);
                    break;

                case CityModuleId.I:
                    // Garagem: batentes e dintel
                    BoxCollider colIL = colGo.AddComponent<BoxCollider>();
                    colIL.center = new Vector3(-1.8f, 1.5f, -0.1f);
                    colIL.size = new Vector3(0.4f, 3.0f, 0.2f);

                    BoxCollider colIR = colGo.AddComponent<BoxCollider>();
                    colIR.center = new Vector3(1.8f, 1.5f, -0.1f);
                    colIR.size = new Vector3(0.4f, 3.0f, 0.2f);

                    BoxCollider colIT = colGo.AddComponent<BoxCollider>();
                    colIT.center = new Vector3(0f, 2.7f, -0.1f);
                    colIT.size = new Vector3(3.2f, 0.6f, 0.2f);

                    // Colisor do portão fechado
                    BoxCollider colIGate = colGo.AddComponent<BoxCollider>();
                    colIGate.center = new Vector3(0f, 1.2f, -0.1f);
                    colIGate.size = new Vector3(3.2f, 2.4f, 0.08f);
                    break;

                case CityModuleId.J:
                    // Porta dupla: NÃO BLOQUEAR O VÃO!
                    // Batente esquerdo (X in [-1.0, -0.8])
                    BoxCollider colJL = colGo.AddComponent<BoxCollider>();
                    colJL.center = new Vector3(-0.9f, 1.5f, -0.1f);
                    colJL.size = new Vector3(0.2f, 3.0f, 0.2f);

                    // Batente direito (X in [0.8, 1.0])
                    BoxCollider colJR = colGo.AddComponent<BoxCollider>();
                    colJR.center = new Vector3(0.9f, 1.5f, -0.1f);
                    colJR.size = new Vector3(0.2f, 3.0f, 0.2f);

                    // Dintel superior (Y in [2.2, 3.0])
                    BoxCollider colJT = colGo.AddComponent<BoxCollider>();
                    colJT.center = new Vector3(0f, 2.6f, -0.1f);
                    colJT.size = new Vector3(1.6f, 0.8f, 0.2f);
                    break;

                case CityModuleId.K:
                    // Toldo: BoxCollider como trigger para evitar colisão indevida com o personagem
                    BoxCollider boxK = colGo.AddComponent<BoxCollider>();
                    boxK.isTrigger = true;
                    boxK.center = new Vector3(0f, -0.2f, 0.4f);
                    boxK.size = new Vector3(2.0f, 0.4f, 0.8f);
                    break;

                case CityModuleId.L:
                    BoxCollider boxL = colGo.AddComponent<BoxCollider>();
                    boxL.center = new Vector3(0f, 1.5f, 0f);
                    boxL.size = new Vector3(0.2f, 3.0f, 0.2f);
                    break;
            }
        }

        private static void BuildModuleAnchors(CityModuleId id, GameObject ancGo)
        {
            if (id == CityModuleId.C || id == CityModuleId.J)
            {
                CreateAnchor(ancGo, "DoorAnchor", Vector3.zero);
                CreateAnchor(ancGo, "ApproachAnchor", new Vector3(0f, 0f, 1.0f));
                CreateAnchor(ancGo, "InteriorAnchor", new Vector3(0f, 0f, -1.0f));
                CreateAnchor(ancGo, "DeliveryAnchor", new Vector3(0f, 0f, 0.4f));
            }

            if (id == CityModuleId.B || id == CityModuleId.C || id == CityModuleId.D || id == CityModuleId.G || id == CityModuleId.H || id == CityModuleId.J)
            {
                CreateAnchor(ancGo, "AwningAnchor", new Vector3(0f, 2.7f, 0f));
            }
        }

        private static void CreateAnchor(GameObject parent, string name, Vector3 localPos)
        {
            GameObject a = new GameObject(name);
            a.transform.SetParent(parent.transform, false);
            a.transform.localPosition = localPos;
        }

        private static void BuildModuleMovingParts(CityModuleId id, GameObject movGo, Dictionary<CityModuleMaterialRole, Material> materials)
        {
            if (id == CityModuleId.C)
            {
                // Dobradiça da porta simples à esquerda (X = -0.5, Y = 0, Z = -0.1)
                GameObject hinge = new GameObject("DoorLeaf");
                hinge.transform.SetParent(movGo.transform, false);
                hinge.transform.localPosition = new Vector3(-0.5f, 0f, -0.1f);

                // Folha visual da porta (0.98m x 2.18m x 0.05m)
                GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                visual.name = "LeafVisual";
                Collider col = visual.GetComponent<Collider>();
                if (col != null) UnityEngine.Object.DestroyImmediate(col);

                visual.transform.SetParent(hinge.transform, false);
                visual.transform.localPosition = new Vector3(0.49f, 1.1f, 0f);
                visual.transform.localScale = new Vector3(0.98f, 2.18f, 0.05f);
                visual.GetComponent<Renderer>().sharedMaterial = materials[CityModuleMaterialRole.Wood];
            }
            else if (id == CityModuleId.J)
            {
                // Porta dupla: duas folhas
                // Folha esquerda (dobradiça em X = -0.8)
                GameObject hingeL = new GameObject("DoorLeaf_Left");
                hingeL.transform.SetParent(movGo.transform, false);
                hingeL.transform.localPosition = new Vector3(-0.8f, 0f, -0.1f);

                GameObject visL = GameObject.CreatePrimitive(PrimitiveType.Cube);
                visL.name = "LeafVisual";
                Collider colL = visL.GetComponent<Collider>();
                if (colL != null) UnityEngine.Object.DestroyImmediate(colL);

                visL.transform.SetParent(hingeL.transform, false);
                visL.transform.localPosition = new Vector3(0.39f, 1.1f, 0f);
                visL.transform.localScale = new Vector3(0.78f, 2.18f, 0.05f);
                visL.GetComponent<Renderer>().sharedMaterial = materials[CityModuleMaterialRole.Wood];

                // Folha direita (dobradiça em X = +0.8)
                GameObject hingeR = new GameObject("DoorLeaf_Right");
                hingeR.transform.SetParent(movGo.transform, false);
                hingeR.transform.localPosition = new Vector3(0.8f, 0f, -0.1f);

                GameObject visR = GameObject.CreatePrimitive(PrimitiveType.Cube);
                visR.name = "LeafVisual";
                Collider colR = visR.GetComponent<Collider>();
                if (colR != null) UnityEngine.Object.DestroyImmediate(colR);

                visR.transform.SetParent(hingeR.transform, false);
                visR.transform.localPosition = new Vector3(-0.39f, 1.1f, 0f);
                visR.transform.localScale = new Vector3(0.78f, 2.18f, 0.05f);
                visR.GetComponent<Renderer>().sharedMaterial = materials[CityModuleMaterialRole.Wood];
            }
        }
        #endregion

        #region Catalog
        public static CityModuleCatalog CreateOrUpdateCatalog(Dictionary<CityModuleId, GameObject> prefabs)
        {
            CityModuleCatalog catalog = AssetDatabase.LoadAssetAtPath<CityModuleCatalog>(CatalogPath);
            bool isNew = (catalog == null);

            if (isNew)
            {
                catalog = ScriptableObject.CreateInstance<CityModuleCatalog>();
            }

            catalog.EnsureAllCanonicalDefinitions();

            // Atualiza os prefabs
            foreach (var def in catalog.definitions)
            {
                if (prefabs.TryGetValue(def.id, out var prefab))
                {
                    def.prefab = prefab;
                }
            }

            if (isNew)
            {
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            else
            {
                EditorUtility.SetDirty(catalog);
            }

            return catalog;
        }
        #endregion
    }
}

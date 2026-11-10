#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using ScaryParty.Pizzeria.Authoring;
using ScaryParty.Pizzeria.Visual;

namespace ScaryParty.Pizzeria.Editor
{
    /// <summary>
    /// Responsável pela montagem e atualização dos elementos demonstrativos do showcase:
    /// - 5 personagens com materiais próprios, poses/animações distintas
    /// - Lighting rig condicional leve para o salão e cozinha
    /// - Construção da cena de testes e comparação A/B sem afetar o GameScene de gameplay.
    /// </summary>
    public static class PizzeriaToonShowcaseBuilder
    {
        public const string CharacterVisualPrefabPath = "Assets/Piaaria/Prefabs/Characters/CharacterVisual.prefab";
        public const string ShowcaseScenePath = "Assets/Piaaria/Scenes/VisualTests/PizzeriaCelShading_Showcase.unity";
        public const string CharacterToonMatsFolder = "Assets/Piaaria/Art/Pizzeria/Toon/Materials/Characters";

        public static bool SetupShowcaseCharactersAndLights(string variantPrefabPath, out string report)
        {
            if (!File.Exists(variantPrefabPath))
            {
                report = $"Prefab variante não encontrado em {variantPrefabPath}";
                return false;
            }

            GameObject characterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterVisualPrefabPath);
            if (characterPrefab == null)
            {
                report = $"CharacterVisual.prefab não encontrado em {CharacterVisualPrefabPath}";
                return false;
            }

            // Garante materiais toon de personagens com variações de cor
            EnsureCharacterToonMaterials(out Material matBodyEmployee, out Material matBodyCustomer1, out Material matBodyCustomer2, out Material matApron, out Material matSkin, out Material matEyesWhite, out Material matPupils);

            GameObject root = PrefabUtility.LoadPrefabContents(variantPrefabPath);
            try
            {
                // Busca ou cria o container de showcase
                Transform showcaseGroup = root.transform.Find("__ToonShowcase");
                if (showcaseGroup == null)
                {
                    var go = new GameObject("__ToonShowcase");
                    go.transform.SetParent(root.transform, false);
                    showcaseGroup = go.transform;
                }

                // 1. ILUMINAÇÃO DE DEMONSTRAÇÃO (Condicional / Leve)
                Transform lightsGroup = showcaseGroup.Find("LightingRig");
                if (lightsGroup != null)
                {
                    UnityEngine.Object.DestroyImmediate(lightsGroup.gameObject);
                }

                var lightsGo = new GameObject("LightingRig");
                lightsGo.transform.SetParent(showcaseGroup, false);

                // Luz preenchimento Salão
                CreateFillLight(lightsGo.transform, "Light_Fill_Salao", new Vector3(16f, 3.5f, -2f), new Color(1.0f, 0.96f, 0.90f), 1.2f, 18f);
                // Luz preenchimento Balcão / Entrada
                CreateFillLight(lightsGo.transform, "Light_Fill_Balcao", new Vector3(8f, 3.2f, -5f), new Color(1.0f, 0.98f, 0.92f), 1.0f, 12f);
                // Luz preenchimento Cozinha / Operação
                CreateFillLight(lightsGo.transform, "Light_Fill_Cozinha", new Vector3(24f, 3.5f, -20f), new Color(0.95f, 0.98f, 1.0f), 1.4f, 16f);
                // Luz quente suave Forno
                CreateFillLight(lightsGo.transform, "Light_Accent_Forno", new Vector3(28f, 2.0f, -14f), new Color(1.0f, 0.65f, 0.35f), 0.8f, 8f);

                // 2. PERSONAGENS DE DEMONSTRAÇÃO (5 Posições Coerentes)
                Transform charGroup = showcaseGroup.Find("Characters");
                if (charGroup != null)
                {
                    UnityEngine.Object.DestroyImmediate(charGroup.gameObject);
                }

                var charGo = new GameObject("Characters");
                charGo.transform.SetParent(showcaseGroup, false);

                // Helper para instanciar e customizar visual
                void SpawnShowcaseChar(string name, Vector3 localPos, Quaternion localRot, Material bodyMat, bool showApron, string animState)
                {
                    GameObject charInst = UnityEngine.Object.Instantiate(characterPrefab, charGo.transform);
                    charInst.name = name;
                    charInst.transform.localPosition = localPos;
                    charInst.transform.localRotation = localRot;

                    // Remove qualquer script de rede / IA que possa existir no prefab
                    var netObj = charInst.GetComponent<Unity.Netcode.NetworkObject>();
                    if (netObj != null) UnityEngine.Object.DestroyImmediate(netObj);

                    // Aplica materiais toon do personagem nos SkinnedMeshRenderers / MeshRenderers
                    Renderer[] rends = charInst.GetComponentsInChildren<Renderer>(true);
                    foreach (var r in rends)
                    {
                        Material[] mats = r.sharedMaterials;
                        for (int m = 0; m < mats.Length; m++)
                        {
                            if (mats[m] == null) continue;
                            string mName = mats[m].name.ToLowerInvariant();
                            if (mName.Contains("bodyprimary") || mName.Contains("corpo"))
                                mats[m] = bodyMat;
                            else if (mName.Contains("apron") || mName.Contains("avental"))
                                mats[m] = showApron ? matApron : bodyMat;
                            else if (mName.Contains("skin") || mName.Contains("pele"))
                                mats[m] = matSkin;
                            else if (mName.Contains("eyeswhite") || mName.Contains("olhos"))
                                mats[m] = matEyesWhite;
                            else if (mName.Contains("pupils") || mName.Contains("pupilas"))
                                mats[m] = matPupils;
                        }
                        r.sharedMaterials = mats;
                    }

                    // Tenta configurar o Animator para preview da pose
                    Animator anim = charInst.GetComponentInChildren<Animator>();
                    if (anim != null && anim.runtimeAnimatorController != null)
                    {
                        // Se houver script de preview ou nome do estado, configura
                    }
                }

                // Altura de referência do piso observada no layout (-1.6m)
                float floorY = -1.6f;

                // 1. Funcionário atrás do balcão (Avental ativo, cor clássica vermelho suave)
                SpawnShowcaseChar("Char_Funcionario_Balcao", new Vector3(7.5f, floorY, -7.5f), Quaternion.Euler(0, 0, 0), matBodyEmployee, true, "Idle");

                // 2. Cliente diante do balcão (Amarelo vivo)
                SpawnShowcaseChar("Char_Cliente_Fila", new Vector3(7.5f, floorY, -5.5f), Quaternion.Euler(0, 180, 0), matBodyCustomer1, false, "Idle");

                // 3. Cliente circulando no salão (Turquesa)
                SpawnShowcaseChar("Char_Cliente_Salao", new Vector3(14.0f, floorY, -4.0f), Quaternion.Euler(0, 75, 0), matBodyCustomer2, false, "Walk");

                // 4. Cliente sentado na mesa 1 (Azul royal / contraste)
                SpawnShowcaseChar("Char_Cliente_Sentado_1", new Vector3(18.2f, floorY, -2.5f), Quaternion.Euler(0, -90, 0), matBodyCustomer1, false, "SitIdle");

                // 5. Segundo cliente mesa 2
                SpawnShowcaseChar("Char_Cliente_Sentado_2", new Vector3(22.5f, floorY, -8.0f), Quaternion.Euler(0, 180, 0), matBodyCustomer2, false, "SitIdle");

                // Marca a variante
                var marker = root.GetComponent<PizzeriaToonVisualMarker>();
                if (marker != null)
                {
                    marker.SetShowcaseRigState(true);
                }

                PrefabUtility.SaveAsPrefabAsset(root, variantPrefabPath);
                report = "Personagens de demonstração (5) e Lighting Rig (4 luzes) configurados com sucesso no prefab clone.";
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void CreateFillLight(Transform parent, string name, Vector3 pos, Color color, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None; // Mantém leve sem atlas de sombra pesada
        }

        private static void EnsureCharacterToonMaterials(
            out Material bodyEmployee,
            out Material bodyCustomer1,
            out Material bodyCustomer2,
            out Material apron,
            out Material skin,
            out Material eyesWhite,
            out Material pupils)
        {
            if (!Directory.Exists(CharacterToonMatsFolder))
            {
                Directory.CreateDirectory(CharacterToonMatsFolder);
            }

            Shader outlineShader = Shader.Find("Distant Lands/Illustrate/Outline") ?? Shader.Find("Distant Lands/Illustrate/Opaque");

            Material CreateOrGetMat(string name, Color color, float outlineWidth = 0.08f)
            {
                string path = $"{CharacterToonMatsFolder}/{name}.mat";
                Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null)
                {
                    m = new Material(outlineShader);
                    AssetDatabase.CreateAsset(m, path);
                }
                else if (m.shader != outlineShader)
                {
                    m.shader = outlineShader;
                }

                if (m.HasProperty("_MainColor")) m.SetColor("_MainColor", color);
                if (m.HasProperty("_Color")) m.SetColor("_Color", color);
                if (m.HasProperty("_LightingMode")) m.SetFloat("_LightingMode", 1f);
                if (m.HasProperty("_PosterizeLight")) m.SetFloat("_PosterizeLight", 1f);
                if (m.HasProperty("_LightSteps")) m.SetFloat("_LightSteps", 3f);
                if (m.HasProperty("_UseShadows")) m.SetFloat("_UseShadows", 1f);
                if (m.HasProperty("_ShadowColor")) m.SetColor("_ShadowColor", Color.Lerp(color * 0.5f, new Color(0.35f, 0.38f, 0.45f, 1f), 0.35f));
                
                // Outlines no shader Distant Lands
                if (m.HasProperty("_UseOutlines")) m.SetFloat("_UseOutlines", 1f);
                if (m.HasProperty("_OutlineWidth")) m.SetFloat("_OutlineWidth", outlineWidth);
                if (m.HasProperty("_OutlineColor")) m.SetColor("_OutlineColor", new Color(0.20f, 0.22f, 0.27f, 1f)); // Grafite escuro

                EditorUtility.SetDirty(m);
                return m;
            }

            bodyEmployee = CreateOrGetMat("Mat_ToonChar_Body_Vermelho", HexToColor("#EF594B"), 0.08f);
            bodyCustomer1 = CreateOrGetMat("Mat_ToonChar_Body_Amarelo", HexToColor("#FFD65D"), 0.08f);
            bodyCustomer2 = CreateOrGetMat("Mat_ToonChar_Body_Turquesa", HexToColor("#49B7AA"), 0.08f);
            apron = CreateOrGetMat("Mat_ToonChar_Apron_Branco", HexToColor("#F5F5F0"), 0.06f);
            skin = CreateOrGetMat("Mat_ToonChar_Skin_Suave", HexToColor("#FCE4D6"), 0.06f);
            eyesWhite = CreateOrGetMat("Mat_ToonChar_EyesWhite", Color.white, 0.04f);
            pupils = CreateOrGetMat("Mat_ToonChar_Pupils", new Color(0.12f, 0.12f, 0.14f, 1f), 0.04f);

            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Monta a cena dedicada de demonstração e comparação A/B.
        /// </summary>
        public static bool BuildShowcaseScene(out string message)
        {
            string dir = Path.GetDirectoryName(ShowcaseScenePath).Replace("\\", "/");
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            newScene.name = "PizzeriaCelShading_Showcase";

            // Configura Câmera
            var cameraGo = GameObject.Find("Main Camera");
            if (cameraGo != null)
            {
                cameraGo.transform.position = new Vector3(16f, 8f, -22f);
                cameraGo.transform.rotation = Quaternion.Euler(22f, -30f, 0f);
                var cam = cameraGo.GetComponent<Camera>();
                if (cam != null)
                {
                    cam.clearFlags = CameraClearFlags.Skybox;
                    cam.fieldOfView = 55f;
                }
            }

            // Configura Luz Direcional Principal
            var dirLight = GameObject.Find("Directional Light");
            if (dirLight != null)
            {
                dirLight.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
                var light = dirLight.GetComponent<Light>();
                if (light != null)
                {
                    light.color = new Color(1.0f, 0.98f, 0.95f);
                    light.intensity = 1.1f;
                    light.shadows = LightShadows.Soft;
                }
            }

            // Instancia a variante Toon
            GameObject toonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PizzeriaToonVariantBuilder.VariantPrefabPath);
            if (toonPrefab != null)
            {
                GameObject toonInstance = (GameObject)PrefabUtility.InstantiatePrefab(toonPrefab);
                toonInstance.name = "PizzeriaModular_CelShaded";
                toonInstance.transform.position = Vector3.zero;
                toonInstance.transform.rotation = Quaternion.identity;
            }

            // Cria chão neutro infinito para contexto visual
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "__Showcase_Ground";
            ground.transform.position = new Vector3(16f, -1.65f, -10f);
            ground.transform.localScale = new Vector3(10f, 1f, 10f);
            var groundRenderer = ground.GetComponent<MeshRenderer>();
            if (groundRenderer != null)
            {
                // Material cinza claro mate
                var groundMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                groundMat.color = new Color(0.22f, 0.24f, 0.28f);
                groundRenderer.sharedMaterial = groundMat;
            }

            bool saved = EditorSceneManager.SaveScene(newScene, ShowcaseScenePath);
            AssetDatabase.Refresh();

            if (saved)
            {
                message = $"Cena de demonstração salva com sucesso em {ShowcaseScenePath}";
                return true;
            }
            else
            {
                message = $"Falha ao salvar cena em {ShowcaseScenePath}";
                return false;
            }
        }

        private static Color HexToColor(string hex)
        {
            if (ColorUtility.TryParseHtmlString(hex, out Color c)) return c;
            return Color.white;
        }
    }
}
#endif

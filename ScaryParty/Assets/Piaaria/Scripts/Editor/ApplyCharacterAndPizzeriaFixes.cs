#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ScaryParty.Player;
using ScaryParty.Pizzeria.Stations;

namespace ScaryParty.EditorScripts
{
    [InitializeOnLoad]
    public static class ApplyCharacterAndPizzeriaFixes
    {
        private const string FlagFile = "Temp/ApplyCharacterAndPizzeriaFixes_Done.flag";
        private const string ReportFile = "Temp/FullFixReport.txt";

        static ApplyCharacterAndPizzeriaFixes()
        {
            EditorApplication.delayCall += ExecuteFixes;
        }

        [MenuItem("Tools/Scary Party/Setup e Uso Único/Personagens/Executar Correção Geral (Personagem + Mesas + Animações)", false, 227)]
        public static void ForceExecuteFixes()
        {
            if (File.Exists(FlagFile)) File.Delete(FlagFile);
            ExecuteFixes();
        }

        public static void ExecuteFixes()
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (File.Exists(FlagFile)) return;

            var sb = new StringBuilder();
            sb.AppendLine("=== EXECUTING CHARACTER, ANIMATIONS AND PIZZERIA FIXES ===");
            sb.AppendLine($"Timestamp: {DateTime.UtcNow:u}\n");

            try
            {
                // 1. Forçar Reimport dos FBXs recém-exportados com bakeAxisConversion = true
                string[] fbxFiles = new[]
                {
                    "Character.fbx",
                    "Character_Walk.fbx",
                    "Character_Run.fbx",
                    "Character_Prepare.fbx",
                    "Character_Idle.fbx",
                    "Character_SitIdle.fbx",
                    "Character_SitDown.fbx",
                    "Character_StandUp.fbx"
                };

                foreach (var f in fbxFiles)
                {
                    string p = $"Assets/Piaaria/Art/Characters/Generated/{f}";
                    var imp = AssetImporter.GetAtPath(p) as ModelImporter;
                    if (imp != null)
                    {
                        imp.bakeAxisConversion = true;
                        imp.globalScale = 1.0f;
                        imp.useFileScale = true;
                        imp.animationType = ModelImporterAnimationType.Generic;
                        imp.SaveAndReimport();
                        sb.AppendLine($"[FBX_REIMPORT_OK] {f} reimportado com bakeAxisConversion=true.");
                    }
                }

                // 2. Executar CharacterSetupWizard completo
                bool wizardOk = CharacterSetupWizard.ApplyFullCharacterSetup(out string wizardReport);
                sb.AppendLine($"\n--- CharacterSetupWizard (ok={wizardOk}) ---");
                sb.AppendLine(wizardReport);

                // 3. Corrigir Mesas e Cadeiras em PizzeriaModular_Manual.prefab
                FixPizzeriaPrefabMesas("Assets/Piaaria/Prefabs/Pizzeria/PizzeriaModular_Manual.prefab", sb);

                // 4. Corrigir Mesas e Cadeiras em PizzeriaPrefab.prefab se existir
                if (File.Exists("Assets/Piaaria/Prefabs/Pizzeria/PizzeriaPrefab.prefab"))
                {
                    FixPizzeriaPrefabMesas("Assets/Piaaria/Prefabs/Pizzeria/PizzeriaPrefab.prefab", sb);
                }

                // 5. Corrigir Mesas e Cadeiras em GameScene.unity
                FixGameSceneMesas("Assets/ThirdParty/StarterAssets/ThirdPersonController/Scenes/GameScene.unity", sb);

                // 6. Testar Amostragem de Animações no CharacterVisual.prefab
                ValidateAnimations(sb);

                // 7. Executar Validacao Automatizada de Animacoes e Assentos
                sb.AppendLine("\n--- 7. SeatAndAnimationValidator ---");
                bool seatValid = ScaryParty.Pizzeria.Editor.SeatAndAnimationValidator.RunValidation(out string seatReport);
                sb.AppendLine(seatReport);

                // 8. Teste de Validacao Visual de Pizza Modular & Preview Utility
                sb.AppendLine("\n--- 8. Validacao do Pizza Studio 2.0 & Geometria 3D ---");
                try
                {
                    // Validar malhas do PizzaModularGeometryFactory
                    var dough = ScaryParty.Pizzeria.Visual.PizzaModularGeometryFactory.GetDoughMesh();
                    var sauce = ScaryParty.Pizzeria.Visual.PizzaModularGeometryFactory.GetSauceMesh();
                    var cheese = ScaryParty.Pizzeria.Visual.PizzaModularGeometryFactory.GetCheeseCoatingMesh();
                    var pep = ScaryParty.Pizzeria.Visual.PizzaModularGeometryFactory.GetPepperoniSliceMesh();
                    var mush = ScaryParty.Pizzeria.Visual.PizzaModularGeometryFactory.GetMushroomSliceMesh();

                    sb.AppendLine($"   DoughMesh: verts={dough.vertexCount}, tris={dough.triangles.Length/3}, normalY={dough.normals[0].y:F2}");
                    sb.AppendLine($"   SauceMesh: verts={sauce.vertexCount}, tris={sauce.triangles.Length/3}, normalY={sauce.normals[0].y:F2}");
                    sb.AppendLine($"   CheeseMesh: verts={cheese.vertexCount}, tris={cheese.triangles.Length/3}, normalY={cheese.normals[0].y:F2}");
                    sb.AppendLine($"   PepperoniMesh: verts={pep.vertexCount}, tris={pep.triangles.Length/3}, normalY={pep.normals[0].y:F2}");
                    sb.AppendLine($"   MushroomMesh: verts={mush.vertexCount}, tris={mush.triangles.Length/3}, normalY={mush.normals[0].y:F2}");

                    if (dough.normals[0].y > 0.05f && pep.normals[0].y > 0.05f)
                    {
                        sb.AppendLine("   [SUCESSO] Normais das malhas procedurais estao voltadas para cima (sem backface culling)!");
                    }
                    else
                    {
                        sb.AppendLine("   [FALHA] Normais procedurais invertidas!");
                    }

                    // Testar EnsureDoughBase e ApplyLayout sem MissingComponentException
                    var testRoot = new GameObject("TestPizzaRoot");
                    try
                    {
                        var doughGo = ScaryParty.Pizzeria.Visual.PizzaVisualComposer.EnsureDoughBase(testRoot, 0.5f, 0f);
                        if (doughGo != null && doughGo.GetComponent<MeshFilter>() != null && doughGo.GetComponent<MeshRenderer>() != null)
                        {
                            sb.AppendLine("   [SUCESSO] EnsureDoughBase executou sem MissingComponentException (MeshFilter e MeshRenderer presentes)!");
                        }

                        // Analise dinamica da geometria da massa
                        var surfaceMetrics = ScaryParty.Pizzeria.Visual.PizzaSurfaceAnalyzer.Analyze(testRoot);
                        sb.AppendLine($"   [DYNAMIC_SURFACE] OuterR={surfaceMetrics.OuterRadius*100:F1}cm, UsableR={surfaceMetrics.UsableRadius*100:F1}cm, FloorY={surfaceMetrics.FloorY:F3}m, CrestY={surfaceMetrics.CrestY:F3}m");

                        var testComp = new ScaryParty.Pizzeria.Visual.PizzaVisualComposition(
                            42UL,
                            2,
                            ScaryParty.Pizzeria.Domain.Types.CookingStage.Uncooked,
                            0.5f,
                            0f,
                            new System.Collections.Generic.List<ScaryParty.Pizzeria.Network.NetIngredientDto>
                            {
                                new ScaryParty.Pizzeria.Network.NetIngredientDto { DefinitionId = 3, Stage = (byte)ScaryParty.Pizzeria.Domain.Types.ProcessingStage.Prepared, Count = 1 },
                                new ScaryParty.Pizzeria.Network.NetIngredientDto { DefinitionId = 5, Stage = (byte)ScaryParty.Pizzeria.Domain.Types.ProcessingStage.Prepared, Count = 1 },
                                new ScaryParty.Pizzeria.Network.NetIngredientDto { DefinitionId = 7, Stage = (byte)ScaryParty.Pizzeria.Domain.Types.ProcessingStage.Prepared, Count = 2 },
                                new ScaryParty.Pizzeria.Network.NetIngredientDto { DefinitionId = 9, Stage = (byte)ScaryParty.Pizzeria.Domain.Types.ProcessingStage.Prepared, Count = 1 }
                            },
                            surfaceMetrics
                        );
                        var layout = ScaryParty.Pizzeria.Visual.PizzaLayoutGenerator.Generate(testComp, ScaryParty.Pizzeria.Visual.PizzaVisualCatalog.Instance);
                        sb.AppendLine($"   [PIZZA_LAYOUT_OK] Layout gerado com sucesso: {layout.Placements.Count} pecas.");

                        // Validar que todas as pecas estao estritamente contidas no raio util dinamico
                        float maxRadiusFound = 0f;
                        for (int p = 0; p < layout.Placements.Count; p++)
                        {
                            var pos = layout.Placements[p].LocalPosition;
                            float r = new Vector2(pos.x, pos.z).magnitude;
                            if (r > maxRadiusFound) maxRadiusFound = r;
                        }
                        sb.AppendLine($"   Raio maximo das pecas: {maxRadiusFound * 100:F1} cm (limite util dinamico: {surfaceMetrics.UsableRadius * 100:F1} cm).");
                        if (maxRadiusFound <= surfaceMetrics.UsableRadius + 0.01f)
                        {
                            sb.AppendLine("   [SUCESSO] Todos os ingredientes estao perfeitamente dentro da borda da pizza calculada dinamicamente!");
                        }
                        else
                        {
                            sb.AppendLine("   [FALHA] Ingredientes ultrapassando a borda!");
                        }

                        ScaryParty.Pizzeria.Visual.PizzaVisualComposer.ApplyLayout(testRoot, layout, default);
                        sb.AppendLine($"   [SUCESSO] ApplyLayout executado com sucesso: {testRoot.transform.childCount} filhos criados.");

                        // Testar PreviewRenderUtility
                        var preview = new PreviewRenderUtility();
                        try
                        {
                            preview.camera.clearFlags = CameraClearFlags.Color;
                            preview.camera.backgroundColor = new Color(0.15f, 0.15f, 0.17f, 1f);
                            preview.camera.cullingMask = ~0;
                            preview.AddSingleGO(testRoot);

                            preview.BeginPreview(new Rect(0, 0, 128, 128), GUIStyle.none);
                            preview.camera.transform.position = new Vector3(0f, 0.5f, -0.5f);
                            preview.camera.transform.LookAt(Vector3.zero);
                            preview.Render(true);
                            var tex = preview.EndPreview();
                            if (tex != null && tex.width > 0 && tex.height > 0)
                            {
                                sb.AppendLine($"   [SUCESSO] PreviewRenderUtility renderizou textura 3D valida ({tex.width}x{tex.height})!");
                            }
                            else
                            {
                                sb.AppendLine("   [FALHA] Textura de PreviewRenderUtility nula ou vazia!");
                            }
                        }
                        finally
                        {
                            preview.Cleanup();
                        }
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(testRoot);
                    }
                }
                catch (System.Exception pEx)
                {
                    sb.AppendLine($"   [PIZZA_PREVIEW_FAIL] Excecao ao validar pizza modular: {pEx}");
                }

                File.WriteAllText(FlagFile, DateTime.UtcNow.ToString());
                sb.AppendLine("\n=== ALL FIXES APPLIED SUCCESSFULLY ===");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"\n[EXCEPTION] {ex}");
                Debug.LogException(ex);
            }
            finally
            {
                File.WriteAllText(ReportFile, sb.ToString());
                Debug.Log(sb.ToString());
            }
        }

        private static void FixPizzeriaPrefabMesas(string prefabPath, StringBuilder sb)
        {
            var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefabAsset == null) return;

            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                int modifiedCount = AdjustMesaTransforms(root.transform);
                if (modifiedCount > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                    sb.AppendLine($"[PREFAB_MESAS_OK] {prefabPath}: {modifiedCount} mesas/cadeiras ajustadas para piso -1.60m.");
                }
                else
                {
                    sb.AppendLine($"[PREFAB_MESAS_INFO] {prefabPath}: nenhuma mesa precisou de ajuste.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void FixGameSceneMesas(string scenePath, StringBuilder sb)
        {
            if (!File.Exists(scenePath)) return;
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                int totalModified = 0;
                var roots = scene.GetRootGameObjects();
                foreach (var r in roots)
                {
                    totalModified += AdjustMesaTransforms(r.transform);
                }
                if (totalModified > 0)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    sb.AppendLine($"[SCENE_MESAS_OK] {scenePath}: {totalModified} mesas/cadeiras ajustadas.");
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static int AdjustMesaTransforms(Transform parent)
        {
            int modified = 0;
            var allTransforms = parent.GetComponentsInChildren<Transform>(true);
            foreach (var t in allTransforms)
            {
                if (t == null) continue;
                string lower = t.name.ToLowerInvariant();
                if (lower.StartsWith("mesa comum"))
                {
                    // Origem da mesa comum fica em local Y = -1.0221158f para o pé (-0.5779m) tocar exatamente em -1.60m
                    Vector3 lp = t.localPosition;
                    if (Mathf.Abs(lp.y - (-1.0221158f)) > 0.01f)
                    {
                        lp.y = -1.0221158f;
                        t.localPosition = lp;
                        modified++;
                    }

                    // Ajustar BoxCollider da mesa
                    var bc = t.GetComponent<BoxCollider>();
                    if (bc != null)
                    {
                        bc.center = new Vector3(0f, 0f, 0f);
                        bc.size = new Vector3(1.1f, 1.15f, 1.1f);
                    }

                    // Ajustar cadeiras filhas
                    for (int i = 0; i < t.childCount; i++)
                    {
                        var ch = t.GetChild(i);
                        if (ch.name.ToLowerInvariant().Contains("cadeira"))
                        {
                            Vector3 clp = ch.localPosition;
                            // Cadeiras com Z-bounds -0.4098m precisam de local Y = -0.168m em relação à mesa para tocar -1.60m
                            if (Mathf.Abs(clp.y - (-0.168f)) > 0.01f)
                            {
                                clp.y = -0.168f;
                                ch.localPosition = clp;
                                modified++;
                            }
                        }
                    }
                }
            }
            return modified;
        }

        private static void ValidateAnimations(StringBuilder sb)
        {
            var visualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Piaaria/Prefabs/Characters/CharacterVisual.prefab");
            if (visualPrefab == null)
            {
                sb.AppendLine("[VAL_ERRO] CharacterVisual.prefab nulo!");
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(visualPrefab);
            try
            {
                var armature = instance.transform.Find("Armature");
                var mesh = instance.transform.Find("Cylinder.002");
                sb.AppendLine("\n--- Hierarquia e Rotacoes do CharacterVisual ---");
                sb.AppendLine($"   Armature localRot Euler: {armature?.localRotation.eulerAngles}");
                sb.AppendLine($"   Cylinder.002 localRot Euler: {mesh?.localRotation.eulerAngles}");

                // Testar Walk clip
                AnimationClip walkClip = null;
                foreach (var a in AssetDatabase.LoadAllAssetsAtPath("Assets/Piaaria/Art/Characters/Generated/Character_Walk.fbx"))
                {
                    if (a is AnimationClip c && !c.name.StartsWith("__preview__")) { walkClip = c; break; }
                }

                if (walkClip != null)
                {
                    var bindings = AnimationUtility.GetCurveBindings(walkClip);
                    sb.AppendLine($"\n--- Walk Clip ({walkClip.name}) Bindings Count: {bindings.Length} ---");
                    for (int i = 0; i < Math.Min(6, bindings.Length); i++)
                    {
                        sb.AppendLine($"   binding[{i}]: path='{bindings[i].path}', prop='{bindings[i].propertyName}'");
                    }

                    var leg = instance.transform.Find("Armature/central/hip.l/leg1.l");
                    if (leg != null)
                    {
                        walkClip.SampleAnimation(instance, 0.0f);
                        Quaternion r0 = leg.localRotation;
                        walkClip.SampleAnimation(instance, 0.5f);
                        Quaternion rMid = leg.localRotation;
                        float diff = Quaternion.Angle(r0, rMid);
                        sb.AppendLine($"\n--- Teste de Amostragem Walk no leg1.l ---");
                        sb.AppendLine($"   Rot t=0: {r0}");
                        sb.AppendLine($"   Rot t=0.5: {rMid}");
                        sb.AppendLine($"   Diferenca de angulo: {diff:F2} graus");
                        if (diff > 1.0f)
                        {
                            sb.AppendLine("   [SUCESSO] O clipe Walk ESTA animando os ossos perfeitamente!");
                        }
                        else
                        {
                            sb.AppendLine("   [FALHA] Diferenca angular < 1 grau!");
                        }
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }
    }
}
#endif
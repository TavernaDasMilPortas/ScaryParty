using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ScaryParty.ArmatureMapper
{
    public class ArmatureMapperWindow : EditorWindow
    {
        private GameObject selectedPrefab;
        private bool includeHierarchy = true;
        private bool includeAnimations = true;
        private bool includeSkinnedMesh = true;

        private Vector2 scrollPos;

        [MenuItem("Tools/Armature Mapper")]
        public static void ShowWindow()
        {
            var window = GetWindow<ArmatureMapperWindow>("Armature Mapper");
            window.minSize = new Vector2(400, 350);
        }

        private void OnGUI()
        {
            GUILayout.Label("Armature Mapper Utility", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.HelpBox(
                "Este utilitário mapeia a hierarquia de bones e todas as animações extraídas do Animator do prefab selecionado, gerando um JSON detalhado.", 
                MessageType.Info
            );
            
            EditorGUILayout.Space();

            // Selection
            GUILayout.Label("1. Selecione o Prefab", EditorStyles.label);
            selectedPrefab = (GameObject)EditorGUILayout.ObjectField("Prefab Root", selectedPrefab, typeof(GameObject), true);

            if (GUILayout.Button("Usar Seleção Atual"))
            {
                if (Selection.activeGameObject != null)
                {
                    selectedPrefab = Selection.activeGameObject;
                }
            }

            EditorGUILayout.Space();

            // Preview Info
            if (selectedPrefab != null)
            {
                GUILayout.Label("Info do Prefab:", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                
                bool hasAnimator = selectedPrefab.GetComponent<Animator>() != null;
                bool hasSkinnedMesh = selectedPrefab.GetComponentInChildren<SkinnedMeshRenderer>(true) != null;
                
                EditorGUILayout.LabelField("Possui Animator?", hasAnimator ? "Sim" : "Não");
                EditorGUILayout.LabelField("Possui SkinnedMesh?", hasSkinnedMesh ? "Sim" : "Não");
                
                EditorGUI.indentLevel--;
                EditorGUILayout.Space();
            }

            // Options
            GUILayout.Label("2. Opções de Extração", EditorStyles.label);
            EditorGUI.indentLevel++;
            includeHierarchy = EditorGUILayout.Toggle("Extrair Hierarquia (Bones)", includeHierarchy);
            includeSkinnedMesh = EditorGUILayout.Toggle("Extrair Info SkinnedMesh", includeSkinnedMesh);
            includeAnimations = EditorGUILayout.Toggle("Extrair Animações (Keyframes)", includeAnimations);
            EditorGUI.indentLevel--;

            EditorGUILayout.Space();
            EditorGUILayout.Space();

            // Execute Button
            GUI.enabled = selectedPrefab != null;
            if (GUILayout.Button("🗺️ Mapear Armature", GUILayout.Height(40)))
            {
                ExecuteMapping();
            }
            GUI.enabled = true;
        }

        private void ExecuteMapping()
        {
            if (selectedPrefab == null) return;

            try
            {
                EditorUtility.DisplayProgressBar("Armature Mapper", "Iniciando mapeamento...", 0f);

                ArmatureMap map = new ArmatureMap();
                map.prefabInfo = new PrefabInfo
                {
                    name = selectedPrefab.name,
                    assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(selectedPrefab),
                    exportTimestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss")
                };

                if (string.IsNullOrEmpty(map.prefabInfo.assetPath))
                {
                    // Fallback to scene path if it's a scene object
                    map.prefabInfo.assetPath = selectedPrefab.scene.path + "/" + selectedPrefab.name;
                }

                if (includeHierarchy || includeSkinnedMesh)
                {
                    EditorUtility.DisplayProgressBar("Armature Mapper", "Extraindo hierarquia de bones...", 0.3f);
                    BoneHierarchyExtractor.ExtractHierarchy(selectedPrefab, map);
                }

                if (includeAnimations)
                {
                    EditorUtility.DisplayProgressBar("Armature Mapper", "Extraindo animações e keyframes...", 0.6f);
                    AnimationDataExtractor.ExtractAnimationData(selectedPrefab, map);
                }

                EditorUtility.DisplayProgressBar("Armature Mapper", "Salvando JSON...", 0.9f);
                
                string projectPath = Application.dataPath;
                string outputFolder = Path.Combine(projectPath, "ArmatureMapper", "Output");
                
                JsonExporter.Export(map, outputFolder);

            }
            catch (Exception ex)
            {
                Debug.LogError($"Erro ao mapear armature: {ex.Message}\n{ex.StackTrace}");
                EditorUtility.DisplayDialog("Erro", "Ocorreu um erro durante a extração. Verifique o console.", "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }
    }
}

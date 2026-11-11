using UnityEngine;
using UnityEditor;
using ScaryParty.Pizzeria.Presentation;
using System.IO;

namespace ScaryParty.Pizzeria.Editor
{
    [CustomEditor(typeof(StationProgressBar))]
    public class StationProgressBarEditor : UnityEditor.Editor
    {
        private float _testProgress = 0.5f;
        private Color _testColor = Color.green;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            StationProgressBar bar = (StationProgressBar)target;

            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("Ferramentas de Teste e Criação", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _testProgress = EditorGUILayout.Slider("Progresso de Teste", _testProgress, 0f, 1f);
                _testColor = EditorGUILayout.ColorField("Cor de Teste", _testColor);

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Aplicar Teste"))
                {
                    bar.SetProgress(_testProgress, _testColor);
                    EditorUtility.SetDirty(bar);
                }
                if (GUILayout.Button("Ocultar"))
                {
                    bar.Hide();
                    EditorUtility.SetDirty(bar);
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(8);
            if (GUILayout.Button("Criar / Atualizar Prefab em Resources", GUILayout.Height(28)))
            {
                CreateOrUpdatePrefab();
            }
        }

        [MenuItem("Tools/Scary Party/Setup e Uso Único/Pizzaria/Criar Prefab da Barra de Progresso", false, 247)]
        public static void CreateOrUpdatePrefab()
        {
            const string resourcesDir = "Assets/Resources";
            const string subDir = "Assets/Resources/PizzeriaUI";
            const string prefabPath = "Assets/Resources/PizzeriaUI/StationProgressBar.prefab";

            if (!Directory.Exists(resourcesDir))
            {
                Directory.CreateDirectory(resourcesDir);
                AssetDatabase.Refresh();
            }

            if (!Directory.Exists(subDir))
            {
                Directory.CreateDirectory(subDir);
                AssetDatabase.Refresh();
            }

            GameObject tempGo = new GameObject("StationProgressBar");
            var bar = tempGo.AddComponent<StationProgressBar>();
            bar.BuildProceduralUI();

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(tempGo, prefabPath);
            DestroyImmediate(tempGo);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = savedPrefab;
            EditorGUIUtility.PingObject(savedPrefab);

            EditorUtility.DisplayDialog(
                "Barra de Progresso",
                $"Prefab gerado com sucesso em:\n{prefabPath}\n\nVocê pode abrir este Prefab no Unity Editor para alterar dimensões, fontes, cores, sprites ou anexar animações!",
                "OK"
            );
        }
    }
}

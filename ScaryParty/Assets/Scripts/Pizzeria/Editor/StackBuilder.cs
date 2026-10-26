#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using ScaryParty.Pizzeria.Stations;
using ScaryParty.Pizzeria.Integration;
using ScaryParty.Interaction;

namespace ScaryParty.Pizzeria.Editor
{
    public static class StackBuilder
    {
        private const string PrefabPath = "Assets/Prefabs/Pizzeria/PizzeriaPrefab.prefab";

        [MenuItem("Tools/Scary Party/Pizzeria/Builders/Construir Pilha de Staging", false, 122)]
        public static void BuildStagingStack()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                EditorUtility.DisplayDialog("Erro", $"Prefab não encontrado em: {PrefabPath}. Execute 'Build or Update' primeiro.", "OK");
                return;
            }

            string prefabAssetPath = AssetDatabase.GetAssetPath(prefab);
            var root = PrefabUtility.LoadPrefabContents(prefabAssetPath);
            try
            {
                var stationsParent = root.transform.Find("Stations");
                if (stationsParent == null)
                {
                    stationsParent = new GameObject("Stations").transform;
                    stationsParent.SetParent(root.transform, false);
                }

                // Localizar ou criar GameObject da Pilha
                var stackObj = BuilderToolkit.EnsureChild(stationsParent.gameObject, "Station_Stack_Staging", () =>
                {
                    var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.name = "Station_Stack_Staging";
                    return go;
                });

                // Posicionamento no chão ao lado do balcão de staging (expedição)
                stackObj.transform.localPosition = new Vector3(2.0f, 0.05f, -4.5f);
                stackObj.transform.localRotation = Quaternion.identity;
                stackObj.transform.localScale = new Vector3(0.6f, 0.1f, 0.6f);

                // Material de pallet de madeira
                var renderer = stackObj.GetComponent<Renderer>();
                if (renderer != null)
                {
                    var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                    var mat = new Material(shader);
                    mat.color = new Color(0.55f, 0.40f, 0.25f);
                    renderer.sharedMaterial = mat;
                }

                // Componente StackStation
                var stackStation = BuilderToolkit.GetOrAddComponent<StackStation>(stackObj);
                stackStation.stationId = 16;
                stackStation.stationName = "Pilha de Staging";
                stackStation.stackId = 1;
                stackStation.capacity = 6;
                BuilderToolkit.GetOrAddComponent<OutlineTarget>(stackObj);

                // Marcar como gerado
                BuilderToolkit.MarkGenerated(stackObj, "StackBuilder", "Station_Stack_Staging");

                PrefabUtility.SaveAsPrefabAsset(root, prefabAssetPath);
                Debug.Log("[StackBuilder] ✅ Pilha de Staging construída e salva no prefab com sucesso!");
                EditorUtility.DisplayDialog("Scary Party", "Pilha de Staging construída com sucesso no PizzeriaPrefab!", "OK");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
#endif

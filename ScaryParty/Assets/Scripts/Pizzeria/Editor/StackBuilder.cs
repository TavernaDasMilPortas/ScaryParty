#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using ScaryParty.Pizzeria.Authoring;
using ScaryParty.Pizzeria.Editor.Authoring;
using ScaryParty.Pizzeria.Stations;
using ScaryParty.Pizzeria.Integration;
using ScaryParty.Interaction;

namespace ScaryParty.Pizzeria.Editor
{
    public static class StackBuilder
    {
        private const string PrefabPath = "Assets/Prefabs/Pizzeria/PizzeriaPrefab.prefab";

        /// <summary>
        /// Rotina reutilizável que configura StackStation no objeto-alvo sem recriar primitivas nem alterar posição/escala.
        /// </summary>
        public static StackStation ConfigureStackOnTarget(GameObject targetGo, int stationId = 16, ulong stackId = 1, int capacity = 6, string stationName = "Pilha de Caixas")
        {
            if (targetGo == null) return null;
            var stackStation = BuilderToolkit.GetOrAddComponent<StackStation>(targetGo);
            stackStation.stationId = stationId;
            stackStation.stationName = stationName;
            BuilderToolkit.GetOrAddComponent<OutlineTarget>(targetGo);

            PizzeriaSelectionConverter.ConfigureStackStationComponentsOnTarget(targetGo, stackStation, stackId, capacity);
            PizzeriaSelectionConverter.EnsureStationSlots(targetGo, stackStation, 1, PizzeriaStationKind.BoxStack, Vector3.forward);
            return stackStation;
        }

        [MenuItem("Tools/Scary Party/Pizzeria/Builders/Construir Pilha de Staging (Legado)", false, 122)]
        public static void BuildStagingStack()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                EditorUtility.DisplayDialog("Erro", $"Prefab não encontrado em: {PrefabPath}.", "OK");
                return;
            }

            var bindings = prefab.GetComponent<PizzeriaAuthoringBindings>();
            if (bindings != null && bindings.isManualAuthoring)
            {
                Debug.LogWarning("[StackBuilder] O prefab está em modo de Autoria Manual Assistida. Use 'Tools > Scary Party > Pizzaria Modular' (Aba 3: Bancadas e Objetos) para atribuir a pilha de caixas.");
                if (Application.isBatchMode) return;
                EditorUtility.DisplayDialog(
                    "Autoria Manual Ativa",
                    "Este prefab usa o Builder Manual da Pizzaria Modular.\n\nAbra 'Tools > Scary Party > Pizzaria Modular' → Aba '3. Bancadas e Objetos', selecione o objeto e clique em 'Criar Pilha de Caixas'.",
                    "OK");
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

                ConfigureStackOnTarget(stackObj, 16, 1, 6, "Pilha de Staging");
                BuilderToolkit.MarkGenerated(stackObj, "StackBuilder", "Station_Stack_Staging");

                PrefabUtility.SaveAsPrefabAsset(root, prefabAssetPath);
                Debug.Log("[StackBuilder] ✅ Pilha de Staging construída e salva no prefab com sucesso!");
                if (!Application.isBatchMode)
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

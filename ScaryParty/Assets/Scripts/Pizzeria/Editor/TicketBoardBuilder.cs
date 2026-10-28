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
    public static class TicketBoardBuilder
    {
        private const string PrefabPath = "Assets/Prefabs/Pizzeria/PizzeriaPrefab.prefab";

        /// <summary>
        /// Rotina reutilizável que configura TicketBoardStation e slots de comandas no objeto-alvo
        /// sem recriar primitivas nem alterar posição/escala.
        /// </summary>
        public static TicketBoardStation ConfigureTicketBoardOnTarget(GameObject targetGo, int stationId = 15, int slotCount = 6, string stationName = "Quadro de Comandas")
        {
            if (targetGo == null) return null;
            var ticketBoard = BuilderToolkit.GetOrAddComponent<TicketBoardStation>(targetGo);
            ticketBoard.stationId = stationId;
            ticketBoard.stationName = stationName;
            ticketBoard.maxVisibleTickets = slotCount;
            BuilderToolkit.GetOrAddComponent<OutlineTarget>(targetGo);

            PizzeriaSelectionConverter.ConfigureTicketBoardComponentsOnTarget(targetGo, ticketBoard, slotCount);
            return ticketBoard;
        }

        [MenuItem("Tools/Scary Party/Pizzeria/Builders/Construir Quadro de Comandas (Legado)", false, 120)]
        public static void BuildTicketBoard()
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
                Debug.LogWarning("[TicketBoardBuilder] O prefab está em modo de Autoria Manual Assistida. Use 'Tools > Scary Party > Pizzaria Modular' (Aba 3: Bancadas e Objetos) para atribuir o quadro de comandas.");
                if (Application.isBatchMode) return;
                EditorUtility.DisplayDialog(
                    "Autoria Manual Ativa",
                    "Este prefab usa o Builder Manual da Pizzaria Modular.\n\nAbra 'Tools > Scary Party > Pizzaria Modular' → Aba '3. Bancadas e Objetos', selecione o objeto e clique em 'Criar Quadro de Comandas'.",
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

                // Localizar ou criar GameObject do Quadro
                var boardObj = BuilderToolkit.EnsureChild(stationsParent.gameObject, "Station_TicketBoard", () =>
                {
                    var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.name = "Station_TicketBoard";
                    return go;
                });

                // Posicionamento padrão no balcão/parede
                boardObj.transform.localPosition = new Vector3(3.5f, 1.6f, 5.8f);
                boardObj.transform.localRotation = Quaternion.Euler(0, 180, 0);
                boardObj.transform.localScale = new Vector3(2.2f, 0.9f, 0.08f);

                ConfigureTicketBoardOnTarget(boardObj, 15, 4, "Quadro de Comandas");
                BuilderToolkit.MarkGenerated(boardObj, "TicketBoardBuilder", "Station_TicketBoard");

                PrefabUtility.SaveAsPrefabAsset(root, prefabAssetPath);
                Debug.Log("[TicketBoardBuilder] ✅ Quadro de Comandas construído e salvo no prefab com sucesso!");
                if (!Application.isBatchMode)
                    EditorUtility.DisplayDialog("Scary Party", "Quadro de Comandas construído com sucesso no PizzeriaPrefab!", "OK");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
#endif

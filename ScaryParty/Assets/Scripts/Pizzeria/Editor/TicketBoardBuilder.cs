#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using ScaryParty.Pizzeria.Stations;
using ScaryParty.Pizzeria.Integration;

namespace ScaryParty.Pizzeria.Editor
{
    public static class TicketBoardBuilder
    {
        private const string PrefabPath = "Assets/Prefabs/Pizzeria/PizzeriaPrefab.prefab";

        [MenuItem("Tools/Scary Party/Pizzeria/Builders/Construir Quadro de Comandas", false, 120)]
        public static void BuildTicketBoard()
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

                // Material de madeira / quadro de cortiça
                var renderer = boardObj.GetComponent<Renderer>();
                if (renderer != null)
                {
                    var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                    var mat = new Material(shader);
                    mat.color = new Color(0.45f, 0.32f, 0.20f);
                    renderer.sharedMaterial = mat;
                }

                // Componente TicketBoardStation
                var ticketBoard = BuilderToolkit.GetOrAddComponent<TicketBoardStation>(boardObj);
                ticketBoard.stationId = 15;
                ticketBoard.stationName = "Quadro de Comandas";
                ticketBoard.maxVisibleTickets = 4;

                // Marcar como gerado
                BuilderToolkit.MarkGenerated(boardObj, "TicketBoardBuilder", "Station_TicketBoard");

                PrefabUtility.SaveAsPrefabAsset(root, prefabAssetPath);
                Debug.Log("[TicketBoardBuilder] ✅ Quadro de Comandas construído e salvo no prefab com sucesso!");
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

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
    public static class PhoneBuilder
    {
        private const string PrefabPath = "Assets/Prefabs/Pizzeria/PizzeriaPrefab.prefab";

        /// <summary>
        /// Rotina reutilizável que configura PhoneStation, PhoneRinger, AudioSource e BeaconLight
        /// no objeto-alvo informado sem recriar primitivas nem alterar posição/escala.
        /// </summary>
        public static PhoneStation ConfigurePhoneOnTarget(GameObject targetGo, int stationId = 12, string stationName = "Telefone de Pedidos")
        {
            if (targetGo == null) return null;
            var phoneStation = BuilderToolkit.GetOrAddComponent<PhoneStation>(targetGo);
            phoneStation.stationId = stationId;
            phoneStation.stationName = stationName;
            BuilderToolkit.GetOrAddComponent<OutlineTarget>(targetGo);

            PizzeriaSelectionConverter.ConfigurePhoneComponentsOnTarget(targetGo, phoneStation);
            PizzeriaSelectionConverter.EnsureStationSlots(targetGo, phoneStation, 1, PizzeriaStationKind.Phone, Vector3.forward);
            return phoneStation;
        }

        [MenuItem("Tools/Scary Party/Pizzeria/Builders/Construir Telefone (Legado)", false, 121)]
        public static void BuildPhone()
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
                Debug.LogWarning("[PhoneBuilder] O prefab está em modo de Autoria Manual Assistida. Use 'Tools > Scary Party > Pizzaria Modular' (Aba 3: Bancadas e Objetos) para atribuir o telefone ao objeto desejado sem recriar primitivas.");
                if (Application.isBatchMode) return;
                EditorUtility.DisplayDialog(
                    "Autoria Manual Ativa",
                    "Este prefab usa o Builder Manual da Pizzaria Modular.\n\nAbra 'Tools > Scary Party > Pizzaria Modular' → Aba '3. Bancadas e Objetos', selecione o objeto do telefone e clique em 'Criar Telefone'.",
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

                // Localizar ou criar GameObject do Telefone
                var phoneObj = BuilderToolkit.EnsureChild(stationsParent.gameObject, "Station_Phone", () =>
                {
                    var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.name = "Station_Phone";
                    return go;
                });

                // Posicionamento no balcão de atendimento
                phoneObj.transform.localPosition = new Vector3(-3f, 0.6f, -4.5f);
                phoneObj.transform.localRotation = Quaternion.identity;
                phoneObj.transform.localScale = new Vector3(0.5f, 0.4f, 0.5f);

                var col = BuilderToolkit.GetOrAddComponent<BoxCollider>(phoneObj);
                col.size = Vector3.one;

                ConfigurePhoneOnTarget(phoneObj, 12, "Telefone de Pedidos");
                BuilderToolkit.MarkGenerated(phoneObj, "PhoneBuilder", "Station_Phone");

                PrefabUtility.SaveAsPrefabAsset(root, prefabAssetPath);
                Debug.Log("[PhoneBuilder] ✅ Telefone e PhoneRinger construídos e salvos no prefab com sucesso!");
                if (!Application.isBatchMode)
                    EditorUtility.DisplayDialog("Scary Party", "Telefone construído com sucesso no PizzeriaPrefab!", "OK");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
#endif

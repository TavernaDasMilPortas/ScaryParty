#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using ScaryParty.Pizzeria.Stations;
using ScaryParty.Pizzeria.Integration;
using ScaryParty.Interaction;

namespace ScaryParty.Pizzeria.Editor
{
    public static class PhoneBuilder
    {
        private const string PrefabPath = "Assets/Prefabs/Pizzeria/PizzeriaPrefab.prefab";

        [MenuItem("Tools/Scary Party/Pizzeria/Builders/Construir Telefone", false, 121)]
        public static void BuildPhone()
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

                // Material do telefone (escuro / plástico antigo)
                var renderer = phoneObj.GetComponent<Renderer>();
                if (renderer != null)
                {
                    var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                    var mat = new Material(shader);
                    mat.color = new Color(0.2f, 0.2f, 0.22f);
                    renderer.sharedMaterial = mat;
                }

                // Colisor
                var col = BuilderToolkit.GetOrAddComponent<BoxCollider>(phoneObj);
                col.size = Vector3.one;

                // Componente PhoneStation
                var phoneStation = BuilderToolkit.GetOrAddComponent<PhoneStation>(phoneObj);
                phoneStation.stationId = 12;
                phoneStation.stationName = "Telefone de Pedidos";
                BuilderToolkit.GetOrAddComponent<OutlineTarget>(phoneObj);

                // Componente PhoneRinger
                var ringer = BuilderToolkit.GetOrAddComponent<PhoneRinger>(phoneObj);
                ringer.beaconRenderer = renderer;
                ringer.ringingColor = new Color(1.0f, 0.8f, 0.2f);
                phoneStation.ringer = ringer;

                // AudioSource
                var audio = BuilderToolkit.GetOrAddComponent<AudioSource>(phoneObj);
                audio.playOnAwake = false;
                audio.spatialBlend = 1.0f;
                ringer.audioSource = audio;

                // Marcar como gerado
                BuilderToolkit.MarkGenerated(phoneObj, "PhoneBuilder", "Station_Phone");

                PrefabUtility.SaveAsPrefabAsset(root, prefabAssetPath);
                Debug.Log("[PhoneBuilder] ✅ Telefone e PhoneRinger construídos e salvos no prefab com sucesso!");
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

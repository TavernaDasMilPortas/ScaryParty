#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Presentation;
using ScaryParty.Pizzeria.Composition;

namespace ScaryParty.Pizzeria.Editor
{
    public static class DeliveryAddressBuilder
    {
        [MenuItem("Tools/Scary Party/Pizzeria/Builders/Configurar Pontos de Entrega na Cidade", false, 125)]
        public static void ConfigureCityDeliveryPoints()
        {
            var existingPoints = Object.FindObjectsByType<DeliveryPoint>(FindObjectsSortMode.None);
            var buildings = Object.FindObjectsByType<CityBuilding>(FindObjectsSortMode.None);

            var deliveryPointList = new List<DeliveryPoint>(existingPoints);

            // Se não houver pontos de entrega instanciados, cria para os prédios elegíveis
            if (deliveryPointList.Count == 0 && buildings != null && buildings.Length > 0)
            {
                var cityRoot = GameObject.Find("__City__");
                Transform deliveryPointsParent = null;
                if (cityRoot != null)
                {
                    var child = cityRoot.transform.Find("DeliveryPoints");
                    if (child != null) deliveryPointsParent = child;
                    else
                    {
                        var go = new GameObject("DeliveryPoints");
                        go.transform.SetParent(cityRoot.transform);
                        deliveryPointsParent = go.transform;
                    }
                }

                int createdIdx = 0;
                foreach (var b in buildings)
                {
                    if (!b.canHaveDeliveryPoint) continue;

                    var markerObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    markerObj.name = $"DeliveryPoint_{createdIdx}";
                    if (deliveryPointsParent != null) markerObj.transform.SetParent(deliveryPointsParent);
                    markerObj.transform.position = b.entrancePosition + Vector3.up * 0.1f;
                    markerObj.transform.localScale = new Vector3(3f, 0.05f, 3f);

                    var col = markerObj.GetComponent<Collider>();
                    if (col != null) Object.DestroyImmediate(col);

                    var boxCol = markerObj.AddComponent<BoxCollider>();
                    boxCol.isTrigger = true;
                    boxCol.size = new Vector3(1f, 40f, 1f);
                    boxCol.center = new Vector3(0, 20f, 0);

                    var dp = markerObj.AddComponent<DeliveryPoint>();
                    dp.associatedBuilding = b;
                    dp.AddressId = createdIdx;
                    b.deliveryPointIndex = createdIdx;

                    deliveryPointList.Add(dp);
                    createdIdx++;
                }
            }

            // Ordena os pontos por posição espacial para numeração coerente
            deliveryPointList.Sort((a, b) =>
            {
                int cmp = a.transform.position.z.CompareTo(b.transform.position.z);
                return cmp != 0 ? cmp : a.transform.position.x.CompareTo(b.transform.position.x);
            });

            // Configuração sequencial e atribuição de views
            for (int i = 0; i < deliveryPointList.Count; i++)
            {
                var dp = deliveryPointList[i];
                Undo.RecordObject(dp, "Configurar Ponto de Entrega");

                dp.AddressId = i;
                dp.pointIndex = i;
                dp.isActive = true;

                if (dp.associatedBuilding != null)
                {
                    Undo.RecordObject(dp.associatedBuilding, "Atualizar Indice de Predio");
                    dp.associatedBuilding.deliveryPointIndex = i;
                }

                // Assegurar collider trigger
                var collider = dp.GetComponent<Collider>();
                if (collider == null)
                {
                    var boxCol = dp.gameObject.AddComponent<BoxCollider>();
                    boxCol.isTrigger = true;
                    boxCol.size = new Vector3(2.5f, 3f, 2.5f);
                    boxCol.center = new Vector3(0, 1.5f, 0);
                }
                else
                {
                    collider.isTrigger = true;
                }

                // Assegurar DeliveryPointView
                var view = BuilderToolkit.GetOrAddComponent<DeliveryPointView>(dp.gameObject);
                view.AddressId = i;
                view.EnsureVisualElements();

                // Marcar objeto gerado
                BuilderToolkit.MarkGenerated(dp.gameObject, "DeliveryAddressBuilder", $"DeliveryPoint_{i}");
            }

            if (PizzeriaRoot.Instance != null)
            {
                PizzeriaRoot.Instance.RebuildAddressRegistry();
            }

            var activeScene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(activeScene);

            Debug.Log($"[DeliveryAddressBuilder] ✅ Configurados {deliveryPointList.Count} pontos de entrega na cidade com sucesso!");
            EditorUtility.DisplayDialog(
                "Scary Party — Delivery",
                $"Configurados {deliveryPointList.Count} pontos de entrega na cidade com sucesso!\nNumeração atribuída de {AddressFormat.Display(0)} a {AddressFormat.Display(deliveryPointList.Count - 1)}.",
                "OK");
        }
    }
}
#endif

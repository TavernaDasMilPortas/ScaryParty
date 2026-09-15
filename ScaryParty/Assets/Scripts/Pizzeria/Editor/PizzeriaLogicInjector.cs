using UnityEngine;
using UnityEditor;
using ScaryParty.Pizzeria.Composition;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Presentation;

namespace ScaryParty.Pizzeria.Editor
{
    public static class PizzeriaLogicInjector
    {
        [MenuItem("Tools/Scary Party/Pizzeria/Fix Scene Logic (Injeta Lógica Nova)", false, 20)]
        public static void InjectLogic()
        {
            string prefabPath = "Assets/Prefabs/Pizzeria/PizzeriaPrefab.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            if (prefab == null)
            {
                Debug.LogError($"[PizzeriaLogicInjector] Prefab não encontrado em {prefabPath}");
                return;
            }

            // 1. Atualizar o Prefab base
            UpdatePizzeriaObject(prefab, true);
            PrefabUtility.SavePrefabAsset(prefab);
            Debug.Log("[PizzeriaLogicInjector] Prefab base atualizado com a nova lógica.");

            // 2. Atualizar todas as instâncias na cena aberta
            var rootsInScene = Object.FindObjectsByType<PizzeriaRoot>(FindObjectsSortMode.None);
            if (rootsInScene.Length == 0)
            {
                // Tentar procurar por nome caso o Root não exista
                var pizzerias = GameObject.FindObjectsOfType<GameObject>();
                foreach (var go in pizzerias)
                {
                    if (go.name == "The_Pizzeria_Building" || go.name == "PizzeriaPrefab" || go.name == "The_Pizzaria_Building")
                    {
                        UpdatePizzeriaObject(go, false);
                    }
                }
            }
            else
            {
                foreach (var root in rootsInScene)
                {
                    UpdatePizzeriaObject(root.gameObject, false);
                }
            }

            Debug.Log("[PizzeriaLogicInjector] Instâncias da cena atualizadas. Lógica injetada!");
            EditorUtility.DisplayDialog("Scary Party", "Lógica nova injetada no Prefab e na Cena com sucesso!", "OK");
        }

        private static void UpdatePizzeriaObject(GameObject target, bool isPrefab)
        {
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(target);

            if (target.GetComponent<Unity.Netcode.NetworkObject>() == null)
                target.AddComponent<Unity.Netcode.NetworkObject>();

            if (target.GetComponent<PizzeriaRoot>() == null)
                target.AddComponent<PizzeriaRoot>();

            if (target.GetComponent<PizzeriaNetworkState>() == null)
                target.AddComponent<PizzeriaNetworkState>();

            if (target.GetComponent<PizzeriaCommandHandler>() == null)
                target.AddComponent<PizzeriaCommandHandler>();

            if (target.GetComponent<PizzeriaHudBuilder>() == null)
                target.AddComponent<PizzeriaHudBuilder>();

            if (!isPrefab)
            {
                EditorUtility.SetDirty(target);
            }
        }
    }
}

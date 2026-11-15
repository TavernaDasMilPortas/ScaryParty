using UnityEngine;
using UnityEditor;
using ScaryParty.Pizzeria.Composition;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Presentation;

namespace ScaryParty.Pizzeria.Editor
{
    public static class PizzeriaLogicInjector
    {
        [MenuItem("Tools/Scary Party/Setup e Uso Único/Pizzaria/Fix Scene Logic (Injetar Lógica Nova)", false, 246)]
        public static void InjectLogic()
        {
            string prefabPath = "Assets/Piaaria/Prefabs/Pizzeria/PizzeriaPrefab.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            if (prefab == null)
            {
                Debug.LogError($"[PizzeriaLogicInjector] Prefab nÃ£o encontrado em {prefabPath}");
                return;
            }

            // 1. Atualizar o Prefab base
            UpdatePizzeriaObject(prefab, true);
            PrefabUtility.SavePrefabAsset(prefab);
            Debug.Log("[PizzeriaLogicInjector] Prefab base atualizado com a nova lÃ³gica.");

            // 2. Atualizar todas as instÃ¢ncias na cena aberta
            var rootsInScene = Object.FindObjectsByType<PizzeriaRoot>(FindObjectsSortMode.None);
            if (rootsInScene.Length == 0)
            {
                // Tentar procurar por nome caso o Root nÃ£o exista
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

            Debug.Log("[PizzeriaLogicInjector] InstÃ¢ncias da cena atualizadas. LÃ³gica injetada!");
            EditorUtility.DisplayDialog("Scary Party", "LÃ³gica nova injetada no Prefab e na Cena com sucesso!", "OK");
        }

        private static void UpdatePizzeriaObject(GameObject target, bool isPrefab)
        {
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(target);

            var bindings = target.GetComponent<ScaryParty.Pizzeria.Authoring.PizzeriaAuthoringBindings>();
            if (bindings != null)
            {
                var settings = ScaryParty.Pizzeria.Editor.Authoring.PizzeriaAuthoringSettings.FindOrCreateDefault();
                ScaryParty.Pizzeria.Editor.Authoring.PizzeriaPrefabAuthoringService.InjectOrUpdateBaseFunctioning(target, settings, out _);
                return;
            }

            if (target.GetComponent<Unity.Netcode.NetworkObject>() == null)
                target.AddComponent<Unity.Netcode.NetworkObject>();

            if (target.GetComponent<PizzeriaRoot>() == null)
                target.AddComponent<PizzeriaRoot>();

            if (target.GetComponent<PizzeriaNetworkState>() == null)
                target.AddComponent<PizzeriaNetworkState>();

            if (target.GetComponent<PizzeriaCommandHandler>() == null)
                target.AddComponent<PizzeriaCommandHandler>();


            if (!isPrefab)
            {
                EditorUtility.SetDirty(target);
            }
        }
    }
}

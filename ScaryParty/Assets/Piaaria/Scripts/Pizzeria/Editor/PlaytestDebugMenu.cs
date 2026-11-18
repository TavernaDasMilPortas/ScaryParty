using ScaryParty.Pizzeria.Presentation;
using UnityEditor;
using UnityEngine;

namespace ScaryParty.Pizzeria.Editor.Playtest
{
    public static class PlaytestDebugMenu
    {
        [MenuItem("Tools/Scary Party/Uso Comum/Pizzaria/Selecionar Central de Playtest (Inspector)", false, 118)]
        public static void SelectPlaytestCentralInInspector()
        {
            var ctrl = Object.FindFirstObjectByType<PlaytestDebugController>(FindObjectsInactive.Include);
            if (ctrl == null)
            {
                var go = new GameObject("[Debug] Central de Playtest");
                Undo.RegisterCreatedObjectUndo(go, "Criar Central de Playtest");
                ctrl = go.AddComponent<PlaytestDebugController>();
            }

            Selection.activeGameObject = ctrl.gameObject;
            EditorGUIUtility.PingObject(ctrl.gameObject);
        }
    }
}

using UnityEditor;
using UnityEngine;

namespace ScaryParty.EditorScripts
{
    public class FpsSetupBuilder : EditorWindow
    {
        [MenuItem("Tools/Scary Party/FPS Layer Setup")]
        public static void ShowWindow()
        {
            SetupLayers();
            Debug.Log("FPS Layers configured successfully.");
        }

        public static void SetupLayers()
        {
            SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layersProp = tagManager.FindProperty("layers");

            bool foundLocalPlayer = false;
            bool foundFirstPerson = false;

            for (int i = 8; i < layersProp.arraySize; i++)
            {
                SerializedProperty sp = layersProp.GetArrayElementAtIndex(i);
                if (sp.stringValue == "LocalPlayerModel") foundLocalPlayer = true;
                if (sp.stringValue == "FirstPersonOnly") foundFirstPerson = true;
            }

            if (!foundLocalPlayer)
            {
                for (int i = 8; i < layersProp.arraySize; i++)
                {
                    SerializedProperty sp = layersProp.GetArrayElementAtIndex(i);
                    if (string.IsNullOrEmpty(sp.stringValue))
                    {
                        sp.stringValue = "LocalPlayerModel";
                        break;
                    }
                }
            }

            if (!foundFirstPerson)
            {
                for (int i = 8; i < layersProp.arraySize; i++)
                {
                    SerializedProperty sp = layersProp.GetArrayElementAtIndex(i);
                    if (string.IsNullOrEmpty(sp.stringValue))
                    {
                        sp.stringValue = "FirstPersonOnly";
                        break;
                    }
                }
            }

            tagManager.ApplyModifiedProperties();
        }
    }
}

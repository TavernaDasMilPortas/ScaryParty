using System;
using UnityEditor;
using UnityEngine;

#pragma warning disable CS0162 // unreachable code detected

namespace TheraBytes.BetterUi.Editor
{
    public class RootFolderNotificationElement : WizardPageElementBase
    {
        string folderName;
        GUIContent errorLogo;
        Func<bool> showPredicate;


        public RootFolderNotificationElement(string folderName, Func<bool> showPredicate = null)
        {
            this.folderName = folderName;
            this.showPredicate = showPredicate;

            base.markCompleteImmediately = true;
            errorLogo = EditorGUIUtility.IconContent("CollabError");
        }

        public override void DrawGui()
        {
            if (RootDirectory.DefaultRoot == RootDirectory.OverrideRoot)
                return;

            if (showPredicate != null && !showPredicate())
                return;

            EditorGUI.DrawRect(EditorGUILayout.GetControlRect(false, 1), new Color32(218, 52, 43, 255));
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.BeginHorizontal();
            
            EditorGUILayout.LabelField(errorLogo, GUILayout.Width(16), GUILayout.Height(16));

            EditorGUILayout.BeginVertical();

            EditorGUILayout.LabelField("Root Folder", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"The root folder is not the default root folder of Better UI. When you install a package, there is no way to know when unity is ready to move the installed folder to the desired location. So, you will need to do that manually after installation.\n\nAfter install, please move the content of the folder:", EditorStyles.wordWrappedLabel);

            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField($"Assets/{RootDirectory.DefaultRoot}/{folderName}", EditorStyles.boldLabel);
            EditorGUILayout.EndVertical();
            EditorGUILayout.LabelField($"to:");

            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField($"Assets/{RootDirectory.OverrideRoot}/{folderName}", EditorStyles.boldLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndVertical();
            
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }
    }
}

#pragma warning restore CS0162 // unreachable code detected
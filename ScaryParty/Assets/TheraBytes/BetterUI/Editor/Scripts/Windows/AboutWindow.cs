using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using static PlasticGui.Diff.GetDiffPlasticLinkSpec;

namespace TheraBytes.BetterUi
{
    public class AboutWindow : EditorWindow
    {
        const string VERSION = "3.2";

        [MenuItem("Tools/Better UI/Help/Documentation", false, 300)]
        public static void OpenDocumentation()
        {
            Application.OpenURL(RootDirectory.HelpUrl);
        }

        [MenuItem("Tools/Better UI/Help/Get Support (Forum)", false, 330)]
        public static void OpenForum()
        {
            Application.OpenURL("https://discussions.unity.com/t/better-ui/653747");
        }

        [MenuItem("Tools/Better UI/Help/Get Support (Email)", false, 331)]
        public static void WriteMail()
        {
            Application.OpenURL("mailto:info@there-it-is.com?subject=Better%20UI");
        }

        [MenuItem("Tools/Better UI/Help/Get Support (Discord)", false, 331)]
        public static void JoinDiscord()
        {
            Application.OpenURL("https://discord.gg/x6qEv6heYh");
        }

        [MenuItem("Tools/Better UI/Help/Leave a Review", false, 360)]
        public static void OpenAssetStore()
        {
            Application.OpenURL("https://assetstore.unity.com/packages/tools/gui/better-ui-79031#reviews");
        }

        [MenuItem("Tools/Better UI/About", false, 300)]
        public static void ShowWindow()
        {
            var win = EditorWindow.GetWindow(typeof(AboutWindow), true, "About");
            win.minSize = new Vector2(524, 560);
            win.maxSize = win.minSize;
        }


        private GUIStyle linkStyle;
        GUIContent image;
        GUIContent betterUiLogo;
        GUIContent thereItIsLogo;


        void OnEnable()
        {
            image = new GUIContent(Resources.Load<Texture2D>("wizard_banner"));
            betterUiLogo = new GUIContent(Resources.Load<Texture2D>("better-ui-logo"));
            thereItIsLogo = new GUIContent(Resources.Load<Texture2D>("there-it-is-logo"));
        }

        private void EnsureLinkStyle()
        {
            if (linkStyle != null)
                return;

            var linkNormal = new GUIStyleState() { textColor = new Color32(56, 150, 218, 255) };
            var linkHover = new GUIStyleState() { textColor = new Color32(65, 175, 255, 255) };
            linkStyle = new GUIStyle(EditorStyles.label)
            {
                normal = linkNormal,
                active = linkNormal,
                hover = linkHover,
                focused = linkNormal,
                onActive = linkNormal,
                onNormal = linkNormal,
                onHover = linkHover,
                onFocused = linkNormal,
            };
        }

        void OnGUI()
        {
            EnsureLinkStyle();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(image, EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.Space(9); // moving 10 is the same as the -30 above... no idea why.
            EditorGUILayout.LabelField(new GUIContent(betterUiLogo), GUILayout.Width(32), GUILayout.Height(32));
            EditorGUILayout.Space(9); // moving 8 is the same as the -30 above... no idea why.

            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField("Better UI", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Version {VERSION}");

            EditorGUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(20);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.Space(9); // moving 10 is the same as the -30 above... no idea why.
            EditorGUILayout.LabelField(new GUIContent(thereItIsLogo), GUILayout.Width(32), GUILayout.Height(32));
            EditorGUILayout.Space(9); // moving 8 is the same as the -30 above... no idea why.
            EditorGUILayout.BeginVertical();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("© 2026 ", EditorStyles.miniLabel, GUILayout.Width(40));
            if (GUILayout.Button("there-it-is.com", linkStyle))
            {
                Application.OpenURL("https://there-it-is.com");
            }

            EditorGUILayout.LabelField(" - All Rights Reserved", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField("Created by Salomon Zwecker", EditorStyles.miniLabel);

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("License:", EditorStyles.miniBoldLabel, GUILayout.Width(50));
            if (GUILayout.Button("Standard Unity Asset Store End User License Agreement", linkStyle))
            {
                Application.OpenURL("https://assetstore.unity.com/browse/eula-faq");
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
            EditorGUILayout.Space();


            EditorGUILayout.LabelField("Do you need more information how to use Better UI?", EditorStyles.wordWrappedLabel);

            if (GUILayout.Button("Open Documentation", linkStyle))
            {
                OpenDocumentation();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Did you run into a problem or have a question?", EditorStyles.wordWrappedLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Post in the Forum", linkStyle))
            {
                OpenForum();
            }

            EditorGUILayout.LabelField(" • ", EditorStyles.miniLabel, GUILayout.Width(16));

            if (GUILayout.Button("Join Discord", linkStyle))
            {
                JoinDiscord();
            }

            EditorGUILayout.LabelField(" • ", EditorStyles.miniLabel, GUILayout.Width(16));

            if (GUILayout.Button("Write an email", linkStyle))
            {
                Application.OpenURL("mailto:info@there-it-is.com?subject=Data%20Palette");
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();


            EditorGUILayout.LabelField("Do you find Data Palette useful and would like to support the developer?", EditorStyles.wordWrappedLabel);

            if (GUILayout.Button("Rate 5 Stars", linkStyle))
            {
                OpenAssetStore();
            }
        }
    }
}

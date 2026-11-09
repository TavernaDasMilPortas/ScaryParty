#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ScaryParty.EditorScripts
{
    [InitializeOnLoad]
    public static class InspectFbxHierarchy
    {
        static InspectFbxHierarchy()
        {
            EditorApplication.delayCall += RunInspection;
        }

        [MenuItem("Tools/Scary Party/Inspect FBX Hierarchy", false, 100)]
        public static void RunInspection()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== FBX HIERARCHY AND BINDINGS INSPECTION ===");

            // 1. Character.fbx
            var charGo = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Piaaria/Art/Characters/Generated/Character.fbx");
            if (charGo != null)
            {
                sb.AppendLine("--- Character.fbx Hierarchy ---");
                PrintHierarchy(charGo.transform, "", sb);
            }

            // 2. Character_Walk.fbx
            var walkGo = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Piaaria/Art/Characters/Generated/Character_Walk.fbx");
            if (walkGo != null)
            {
                sb.AppendLine("--- Character_Walk.fbx Hierarchy ---");
                PrintHierarchy(walkGo.transform, "", sb);
            }

            // 3. CharacterVisual.prefab
            var visualGo = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Piaaria/Prefabs/Characters/CharacterVisual.prefab");
            if (visualGo != null)
            {
                sb.AppendLine("--- CharacterVisual.prefab Hierarchy ---");
                PrintHierarchy(visualGo.transform, "", sb);
            }

            // 4. Test binding with different roots
            if (walkGo != null && visualGo != null)
            {
                AnimationClip walkClip = null;
                foreach (var a in AssetDatabase.LoadAllAssetsAtPath("Assets/Piaaria/Art/Characters/Generated/Character_Walk.fbx"))
                {
                    if (a is AnimationClip c && !c.name.StartsWith("__preview__")) { walkClip = c; break; }
                }

                if (walkClip != null)
                {
                    sb.AppendLine("\n--- Walk Clip Curve Bindings ---");
                    var bindings = AnimationUtility.GetCurveBindings(walkClip);
                    foreach (var b in bindings)
                    {
                        if (b.propertyName.Contains("LocalRotation.x"))
                        {
                            sb.AppendLine($"   path='{b.path}', prop='{b.propertyName}'");
                        }
                    }

                    // Test sampling on CharacterVisual
                    var visualInstance = (GameObject)PrefabUtility.InstantiatePrefab(visualGo);
                    try
                    {
                        walkClip.SampleAnimation(visualInstance, 0.0f);
                        var boneOnVisual = visualInstance.transform.Find("Armature/central/hip.l/leg1.l");
                        var rotVisual0 = boneOnVisual != null ? boneOnVisual.localRotation : Quaternion.identity;
                        walkClip.SampleAnimation(visualInstance, 0.5f);
                        var rotVisualMid = boneOnVisual != null ? boneOnVisual.localRotation : Quaternion.identity;
                        float diffVisual = Quaternion.Angle(rotVisual0, rotVisualMid);
                        sb.AppendLine($"\nSample on CharacterVisual (Animator on CharacterVisual): bone rot diff = {diffVisual:F2} deg");

                        // Test what happens if we sample directly on Armature
                        var armOnVisual = visualInstance.transform.Find("Armature");
                        if (armOnVisual != null)
                        {
                            walkClip.SampleAnimation(armOnVisual.gameObject, 0.0f);
                            var rotArm0 = boneOnVisual.localRotation;
                            walkClip.SampleAnimation(armOnVisual.gameObject, 0.5f);
                            var rotArmMid = boneOnVisual.localRotation;
                            float diffArm = Quaternion.Angle(rotArm0, rotArmMid);
                            sb.AppendLine($"Sample on Armature (if Animator were on Armature): bone rot diff = {diffArm:F2} deg");
                        }
                    }
                    finally
                    {
                        Object.DestroyImmediate(visualInstance);
                    }
                }
            }

            System.IO.File.WriteAllText("Temp/FbxInspection.txt", sb.ToString());
            Debug.Log(sb.ToString());
        }

        private static void PrintHierarchy(Transform t, string indent, StringBuilder sb)
        {
            sb.AppendLine($"{indent}- {t.name} (pos={t.localPosition}, rot={t.localRotation.eulerAngles}, scale={t.localScale})");
            for (int i = 0; i < t.childCount; i++)
            {
                PrintHierarchy(t.GetChild(i), indent + "  ", sb);
            }
        }
    }
}
#endif

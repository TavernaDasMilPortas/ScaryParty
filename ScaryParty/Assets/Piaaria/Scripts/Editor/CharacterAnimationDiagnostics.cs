#if UNITY_EDITOR
using System;
using System.Text;
using UnityEditor;
using UnityEngine;
using ScaryParty.Player;
using ScaryParty.Pizzeria.Presentation;

namespace ScaryParty.EditorScripts
{
    [InitializeOnLoad]
    public static class CharacterAnimationDiagnostics
    {
        static CharacterAnimationDiagnostics()
        {
            EditorApplication.delayCall += RunDiagnosticsOnce;
        }

        private static void RunDiagnosticsOnce()
        {
            string flag = "Temp/AnimDiagnostics_v1.txt";
            var sb = new StringBuilder();
            sb.AppendLine("=== CHARACTER ANIMATION & NPC DIAGNOSTICS ===");

            // 1. Inspecionar clipes FBX gerados
            string[] clipFiles = new[]
            {
                "Character_Walk.fbx",
                "Character_Run.fbx",
                "Character_Prepare.fbx",
                "Character_Idle.fbx",
                "Character_SitIdle.fbx",
                "Character_SitDown.fbx",
                "Character_StandUp.fbx"
            };

            foreach (var f in clipFiles)
            {
                string p = $"Assets/Piaaria/Art/Characters/Generated/{f}";
                var assets = AssetDatabase.LoadAllAssetsAtPath(p);
                sb.AppendLine($"\n--- Arquivo: {f} (total sub-assets={assets.Length}) ---");
                AnimationClip foundClip = null;
                Avatar foundAvatar = null;
                foreach (var a in assets)
                {
                    if (a is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                    {
                        foundClip = clip;
                        sb.AppendLine($"   Clip: '{clip.name}', length: {clip.length}s, isLooping: {clip.isLooping}, empty: {clip.empty}");
                        var bindings = AnimationUtility.GetCurveBindings(clip);
                        sb.AppendLine($"   Curve Bindings Count: {bindings.Length}");
                        for (int i = 0; i < Math.Min(8, bindings.Length); i++)
                        {
                            sb.AppendLine($"      binding[{i}]: path='{bindings[i].path}', type={bindings[i].type.Name}, prop='{bindings[i].propertyName}'");
                        }
                    }
                    else if (a is Avatar av)
                    {
                        foundAvatar = av;
                        sb.AppendLine($"   Avatar: '{av.name}', isValid={av.isValid}");
                    }
                }
            }

            // 2. Inspecionar CharacterVisual.prefab
            var visualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Piaaria/Prefabs/Characters/CharacterVisual.prefab");
            sb.AppendLine("\n--- CharacterVisual.prefab ---");
            if (visualPrefab != null)
            {
                var anim = visualPrefab.GetComponent<Animator>();
                sb.AppendLine($"   Animator present: {anim != null}");
                if (anim != null)
                {
                    sb.AppendLine($"   Controller: {anim.runtimeAnimatorController?.name}");
                    sb.AppendLine($"   Avatar: {anim.avatar?.name}, isValid={anim.avatar?.isValid}");
                }
            }
            else
            {
                sb.AppendLine("   [ERRO] CharacterVisual.prefab é nulo!");
            }

            // 3. Teste de amostragem de clipe Walk no CharacterVisual
            if (visualPrefab != null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(visualPrefab);
                try
                {
                    var anim = instance.GetComponent<Animator>();
                    var walkClip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Piaaria/Art/Characters/Generated/Character_Walk.fbx");
                    if (walkClip == null)
                    {
                        foreach (var a in AssetDatabase.LoadAllAssetsAtPath("Assets/Piaaria/Art/Characters/Generated/Character_Walk.fbx"))
                        {
                            if (a is AnimationClip c && !c.name.StartsWith("__preview__")) { walkClip = c; break; }
                        }
                    }

                    var leg = instance.transform.Find("Armature/central/hip.l/leg1.l");
                    if (leg != null && walkClip != null)
                    {
                        walkClip.SampleAnimation(instance, 0.0f);
                        Quaternion rot0 = leg.localRotation;
                        walkClip.SampleAnimation(instance, 0.5f);
                        Quaternion rotMid = leg.localRotation;
                        float diff = Quaternion.Angle(rot0, rotMid);
                        sb.AppendLine($"\n--- Teste de Amostragem Walk no leg1.l ---");
                        sb.AppendLine($"   Rot em t=0s: {rot0}");
                        sb.AppendLine($"   Rot em t=0.5s: {rotMid}");
                        sb.AppendLine($"   Diferença de ângulo: {diff:F2} graus");
                        if (diff < 0.1f)
                        {
                            sb.AppendLine("   [ALERTA] Diferença angular < 0.1°! O clipe NÃO está animando o osso!");
                        }
                        else
                        {
                            sb.AppendLine("   [OK] O clipe está deformando o osso perfeitamente!");
                        }
                    }
                    else
                    {
                        sb.AppendLine($"   [ERRO] Não foi possível encontrar leg1.l ou walkClip (leg={leg != null}, clip={walkClip != null})");
                    }
                }
                finally
                {
                    GameObject.DestroyImmediate(instance);
                }
            }

            System.IO.File.WriteAllText(flag, sb.ToString());
            Debug.Log(sb.ToString());
        }
    }
}
#endif

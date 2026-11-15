using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Unity.Netcode.Components;
using UnityEngine;

/// <summary>
/// Permite que o dono (Owner) do objeto sincronize as animacoes,
/// em vez de apenas o servidor. Necessario para animacao do jogador no cliente.
/// Garante que o Animator do CharacterVisual filho esteja vinculado antes do spawn de rede
/// e que o RuntimeAnimatorController nunca permaneca nulo em Play Mode, Host ou Client.
/// </summary>
[DisallowMultipleComponent]
public class OwnerNetworkAnimator : NetworkAnimator
{
    private static readonly HashSet<string> s_ExcludedParameters = new HashSet<string>(StringComparer.Ordinal)
    {
        "Speed",
        "MotionSpeed",
        "Grounded",
        "Jump",
        "FreeFall",
        "IsPreparing",
        "IsSitting",
        "IsMoving",
        "IsSprinting"
    };

    protected override void Awake()
    {
        EnsureAnimatorBound();
        ConfigureParameterSyncFiltering();
        base.Awake();
    }

    public override void OnNetworkSpawn()
    {
        EnsureAnimatorBound();
        base.OnNetworkSpawn();
    }

    private void ConfigureParameterSyncFiltering()
    {
        var entriesField = typeof(NetworkAnimator).GetField("AnimatorParameterEntries", BindingFlags.Instance | BindingFlags.NonPublic);
        if (entriesField == null) return;

        var container = entriesField.GetValue(this);
        if (container == null) return;

        var listField = container.GetType().GetField("ParameterEntries", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (listField?.GetValue(container) is not IList entries) return;

        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (entry == null) continue;

            var entryType = entry.GetType();
            var nameField = entryType.GetField("name", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var syncField = entryType.GetField("Synchronize", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (nameField == null || syncField == null) continue;

            if (nameField.GetValue(entry) is string paramName && s_ExcludedParameters.Contains(paramName))
            {
                syncField.SetValue(entry, false);
            }
        }
    }

    public void EnsureAnimatorBound()
    {
        var visualCtrl = GetComponentInChildren<ScaryParty.Player.CharacterVisualController>(true);
        if (visualCtrl != null)
        {
            visualCtrl.ResolveReferences();
            if (visualCtrl.Animator != null && visualCtrl.Animator.gameObject.activeInHierarchy)
            {
                Animator = visualCtrl.Animator;
                if (Animator.runtimeAnimatorController == null)
                {
                    Animator.runtimeAnimatorController = ResolveFallbackController(visualCtrl);
                }
                Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                return;
            }
        }

        if (Animator != null && Animator.gameObject.activeInHierarchy && Animator.runtimeAnimatorController != null)
        {
            Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            return;
        }

        var childAnim = GetComponentInChildren<Animator>(true);
        if (childAnim != null)
        {
            Animator = childAnim;
            if (Animator.runtimeAnimatorController == null)
            {
                Animator.runtimeAnimatorController = ResolveFallbackController(null);
            }
            Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }
    }

    private RuntimeAnimatorController ResolveFallbackController(ScaryParty.Player.CharacterVisualController visualCtrl)
    {
        if (visualCtrl != null && visualCtrl.DefaultAnimatorController != null)
            return visualCtrl.DefaultAnimatorController;

        var fallbackCtrl = Resources.Load<RuntimeAnimatorController>("CharacterAnimator");
        if (fallbackCtrl != null) return fallbackCtrl;

#if UNITY_EDITOR
        var editorCtrl = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Piaaria/Animations/Characters/CharacterAnimator.controller");
        if (editorCtrl != null) return editorCtrl;
#endif

        return null;
    }

    protected override bool OnIsServerAuthoritative()
    {
        return false;
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        EnsureAnimatorBound();

        // A classe base NetworkAnimator invoca BuildTransitionStateInfoList() que lanca
        // NullReferenceException se Animator != null mas runtimeAnimatorController for nulo.
        if (Animator == null || Animator.runtimeAnimatorController == null)
        {
            return;
        }

        base.OnValidate();
    }
#endif
}

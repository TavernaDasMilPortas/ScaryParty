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
    protected override void Awake()
    {
        EnsureAnimatorBound();
        base.Awake();
    }

    public override void OnNetworkSpawn()
    {
        EnsureAnimatorBound();
        base.OnNetworkSpawn();
    }

    public void EnsureAnimatorBound()
    {
        if (Animator != null && Animator.gameObject.activeInHierarchy && Animator.runtimeAnimatorController != null)
            return;

        var visualCtrl = GetComponentInChildren<ScaryParty.Player.CharacterVisualController>(true);
        if (visualCtrl != null)
        {
            visualCtrl.ResolveReferences();
            if (visualCtrl.Animator != null)
            {
                Animator = visualCtrl.Animator;
                if (Animator.runtimeAnimatorController == null)
                {
                    Animator.runtimeAnimatorController = ResolveFallbackController(visualCtrl);
                }
                return;
            }
        }

        var childAnim = GetComponentInChildren<Animator>(true);
        if (childAnim != null)
        {
            Animator = childAnim;
            if (Animator.runtimeAnimatorController == null)
            {
                Animator.runtimeAnimatorController = ResolveFallbackController(null);
            }
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

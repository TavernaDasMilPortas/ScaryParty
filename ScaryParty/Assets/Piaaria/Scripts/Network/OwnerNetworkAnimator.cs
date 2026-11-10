using Unity.Netcode.Components;
using UnityEngine;

/// <summary>
/// Permite que o dono (Owner) do objeto sincronize as animacoes,
/// em vez de apenas o servidor. Necessario para animacao do jogador no cliente.
/// Garante que o Animator do CharacterVisual filho esteja vinculado antes do spawn de rede.
/// </summary>
[DisallowMultipleComponent]
public class OwnerNetworkAnimator : NetworkAnimator
{
    private void Awake()
    {
        EnsureAnimatorBound();
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
        if (visualCtrl != null && visualCtrl.Animator != null)
        {
            Animator = visualCtrl.Animator;
            if (Animator.runtimeAnimatorController == null)
            {
                var fallbackCtrl = Resources.Load<RuntimeAnimatorController>("CharacterAnimator");
                if (fallbackCtrl != null) Animator.runtimeAnimatorController = fallbackCtrl;
            }
            return;
        }

        var childAnim = GetComponentInChildren<Animator>();
        if (childAnim != null)
        {
            Animator = childAnim;
            if (Animator.runtimeAnimatorController == null)
            {
                var fallbackCtrl = Resources.Load<RuntimeAnimatorController>("CharacterAnimator");
                if (fallbackCtrl != null) Animator.runtimeAnimatorController = fallbackCtrl;
            }
        }
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

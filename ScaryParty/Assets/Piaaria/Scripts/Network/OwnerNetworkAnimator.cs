using Unity.Netcode.Components;
using UnityEngine;

/// <summary>
/// Permite que o dono (Owner) do objeto sincronize as animações,
/// em vez de apenas o servidor. Necessário para animação do jogador no cliente.
/// Garante que o Animator do CharacterVisual filho esteja vinculado antes do spawn de rede.
/// </summary>
[DisallowMultipleComponent]
public class OwnerNetworkAnimator : NetworkAnimator
{
    private void Awake()
    {
        EnsureAnimatorBound();
    }

    public void EnsureAnimatorBound()
    {
        if (Animator != null && Animator.gameObject.activeInHierarchy && Animator.runtimeAnimatorController != null)
            return;

        var visualCtrl = GetComponentInChildren<ScaryParty.Player.CharacterVisualController>(true);
        if (visualCtrl != null && visualCtrl.Animator != null)
        {
            Animator = visualCtrl.Animator;
            return;
        }

        var childAnim = GetComponentInChildren<Animator>();
        if (childAnim != null)
        {
            Animator = childAnim;
        }
    }

    protected override bool OnIsServerAuthoritative()
    {
        return false;
    }
}


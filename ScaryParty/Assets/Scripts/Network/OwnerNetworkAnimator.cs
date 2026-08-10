using Unity.Netcode.Components;
using UnityEngine;

/// <summary>
/// Permite que o dono (Owner) do objeto sincronize as animações,
/// em vez de apenas o servidor. Necessário para animação do jogador no cliente.
/// </summary>
[DisallowMultipleComponent]
public class OwnerNetworkAnimator : NetworkAnimator
{
    protected override bool OnIsServerAuthoritative()
    {
        return false;
    }
}

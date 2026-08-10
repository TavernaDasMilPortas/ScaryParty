using Unity.Netcode.Components;
using UnityEngine;

/// <summary>
/// Permite que o dono (Owner) do objeto de rede controle o Transform,
/// em vez de apenas o servidor. Necessário para movimentação do jogador no cliente.
/// </summary>
[DisallowMultipleComponent]
public class ClientNetworkTransform : NetworkTransform
{
    protected override bool OnIsServerAuthoritative()
    {
        return false;
    }
}

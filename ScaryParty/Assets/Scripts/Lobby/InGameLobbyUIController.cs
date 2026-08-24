using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;

public class InGameLobbyUIController : MonoBehaviour
{
    private void Awake()
    {
        // Script obsoleto. O lobby de espera foi movido para a cena 'ReadyScene'.
        // Destrói a interface antiga (que ainda existe no prefab da cena Playground) para não poluir a tela.
        Destroy(gameObject);
    }
}

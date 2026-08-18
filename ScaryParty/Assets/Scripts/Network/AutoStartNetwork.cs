using UnityEngine;
using Unity.Netcode;
using System.Collections;
#if UNITY_EDITOR
using ParrelSync;
#endif

public class AutoStartNetwork : MonoBehaviour
{
    private string status = "Iniciando...";

    private void Start()
    {
        StartCoroutine(AutoConnectRoutine());
    }

    private IEnumerator AutoConnectRoutine()
    {
        // Aguarda 1 segundo para o ambiente estabilizar (especialmente útil em Clones)
        yield return new WaitForSeconds(1.0f);

        if (NetworkManager.Singleton == null)
        {
            status = "Erro: NetworkManager.Singleton is null.";
            Debug.LogWarning(status);
            yield break;
        }

        // Fica tentando conectar até conseguir
        while (!NetworkManager.Singleton.IsServer && !NetworkManager.Singleton.IsClient)
        {
#if UNITY_EDITOR
            if (ClonesManager.IsClone())
            {
                status = "Tentando iniciar como Cliente...";
                bool success = NetworkManager.Singleton.StartClient();
                if (!success)
                    status = "Falha ao dar StartClient. Tentando novamente...";
            }
            else
            {
                status = "Tentando iniciar como Host...";
                bool success = NetworkManager.Singleton.StartHost();
                if (!success)
                    status = "Falha ao dar StartHost. Tentando novamente...";
            }
#else
            status = "Tentando iniciar como Host (Build)...";
            NetworkManager.Singleton.StartHost();
#endif
            yield return new WaitForSeconds(1f);
        }

        status = "Conectado como: " + (NetworkManager.Singleton.IsServer ? "Host" : "Client");
    }

    private void OnGUI()
    {
        // Mostra na tela para termos certeza de que o script está rodando!
        GUI.color = Color.black;
        GUI.Label(new Rect(10, 10, 500, 30), "AutoStartNetwork: " + status);
        GUI.color = Color.white;
        GUI.Label(new Rect(9, 9, 500, 30), "AutoStartNetwork: " + status);
    }
}

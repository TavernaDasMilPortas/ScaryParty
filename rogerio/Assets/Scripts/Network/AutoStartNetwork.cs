using UnityEngine;
using Unity.Netcode;
using System.Collections;
#if UNITY_EDITOR
using ParrelSync;
#endif
using System.IO;
using Unity.Netcode.Transports.UTP;

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

        ushort portToConnect;
        string ipToConnect = GetServerIPAndPort(out portToConnect);

        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();

        // Fica tentando conectar até conseguir
        while (!NetworkManager.Singleton.IsServer && !NetworkManager.Singleton.IsClient)
        {
#if UNITY_EDITOR
            if (ClonesManager.IsClone())
            {
                // No editor (clone): conecta como cliente no IP do arquivo
                if (transport != null)
                    transport.SetConnectionData(ipToConnect, portToConnect);
                status = "Tentando iniciar como Cliente...";
                bool success = NetworkManager.Singleton.StartClient();
                if (!success)
                    status = "Falha ao dar StartClient. Tentando novamente...";
            }
            else
            {
                // No editor (principal): abre como host escutando em qualquer IP
                if (transport != null)
                    transport.SetConnectionData("0.0.0.0", portToConnect, "0.0.0.0");
                status = "Tentando iniciar como Host...";
                bool success = NetworkManager.Singleton.StartHost();
                if (!success)
                    status = "Falha ao dar StartHost. Tentando novamente...";
            }
#else
            // Na Build: tenta conectar como cliente no IP do arquivo
            if (transport != null)
                transport.SetConnectionData(ipToConnect, portToConnect);

            status = $"Tentando conectar como Cliente em {ipToConnect}:{portToConnect}...";
            NetworkManager.Singleton.StartClient();

            // Dá um tempo curto para ver se o cliente conseguiu conectar ao servidor
            yield return new WaitForSeconds(2.0f);

            if (!NetworkManager.Singleton.IsConnectedClient)
            {
                // Se falhou, para o cliente
                NetworkManager.Singleton.Shutdown();
                yield return new WaitWhile(() => NetworkManager.Singleton.ShutdownInProgress);

                // Aguarda o Windows liberar o socket (evita WinError:0000271d WSAEACCES)
                yield return new WaitForSeconds(0.5f);

                // Configura o transport para escutar em 0.0.0.0 (qualquer IP local) antes de criar o Host
                if (transport != null)
                    transport.SetConnectionData("0.0.0.0", portToConnect, "0.0.0.0");

                status = $"Falha como cliente. Iniciando como Host na porta {portToConnect}...";
                NetworkManager.Singleton.StartHost();
            }
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

    private string GetServerIPAndPort(out ushort port)
    {
        string filePath = Path.Combine(Application.dataPath, "../server_ip.txt");
        string ip = "127.0.0.1"; // Padrão
        port = 7777; // Porta padrão do Unity Netcode. EVITE a porta 5050 (usada pelo Windows/svchost)

        try
        {
            if (File.Exists(filePath))
            {
                string content = File.ReadAllText(filePath).Trim();
                if (content.Contains(":"))
                {
                    string[] parts = content.Split(':');
                    ip = parts[0];
                    if (ushort.TryParse(parts[1], out ushort parsedPort))
                    {
                        port = parsedPort;
                    }
                }
                else
                {
                    ip = content;
                }
            }
            else
            {
                File.WriteAllText(filePath, $"{ip}:{port}");
                Debug.Log($"Arquivo {filePath} criado com o IP e Porta padrão.");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Erro ao ler/escrever arquivo de IP: {e.Message}");
        }

        return ip;
    }
}

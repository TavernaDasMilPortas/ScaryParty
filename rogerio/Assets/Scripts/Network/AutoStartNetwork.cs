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
    private string serverIP = "?";
    private ushort serverPort = 0;

    private void Start()
    {
        StartCoroutine(AutoConnectRoutine());
    }

    // Flag setada pelos callbacks do Netcode quando a conexão como cliente falha
    private bool _clientConnectionFailed = false;

    private void OnClientDisconnected(ulong clientId)
    {
        // Só nos importa se ainda não estamos conectados (ou seja, foi uma falha de conexão)
        if (!NetworkManager.Singleton.IsConnectedClient)
            _clientConnectionFailed = true;
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

        // Salva nos campos para o OnGUI exibir
        serverIP = ipToConnect;
        serverPort = portToConnect;

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
                status = $"Tentando conectar como Cliente em {ipToConnect}:{portToConnect}...";
                bool success = NetworkManager.Singleton.StartClient();
                if (!success)
                    status = $"Falha ao conectar em {ipToConnect}:{portToConnect}. Tentando novamente...";
            }
            else
            {
                // No editor (principal): abre como host escutando em qualquer IP
                if (transport != null)
                    transport.SetConnectionData("0.0.0.0", portToConnect, "0.0.0.0");
                status = $"Tentando iniciar como Host na porta {portToConnect}...";
                bool success = NetworkManager.Singleton.StartHost();
                if (!success)
                    status = $"Falha ao iniciar Host na porta {portToConnect}. Tentando novamente...";
            }
#else
            // Na Build: tenta conectar como cliente no IP do arquivo
            if (transport != null)
                transport.SetConnectionData(ipToConnect, portToConnect);

            // Registra o callback para saber quando a conexão falhar de verdade
            _clientConnectionFailed = false;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

            status = $"Tentando conectar como Cliente em {ipToConnect}:{portToConnect}...";
            NetworkManager.Singleton.StartClient();

            // Aguarda até conectar com sucesso OU até o Netcode confirmar a falha
            // Timeout de segurança de 10 segundos para não travar para sempre
            float timeout = 10f;
            while (!NetworkManager.Singleton.IsConnectedClient && !_clientConnectionFailed && timeout > 0f)
            {
                timeout -= Time.deltaTime;
                status = $"Aguardando conexão em {ipToConnect}:{portToConnect}... ({timeout:F0}s)";
                yield return null;
            }

            // Remove o callback para não acumular listeners
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;

            if (!NetworkManager.Singleton.IsConnectedClient)
            {
                string motivo = _clientConnectionFailed ? "recusada pelo servidor" : "tempo esgotado";
                status = $"Conexão {motivo}. Desligando cliente...";

                NetworkManager.Singleton.Shutdown();
                yield return new WaitWhile(() => NetworkManager.Singleton.ShutdownInProgress);

                // Aguarda o Windows liberar o socket (evita WinError:0000271d WSAEACCES)
                yield return new WaitForSeconds(0.5f);

                // Configura o transport para escutar em 0.0.0.0 (qualquer IP local) antes de criar o Host
                if (transport != null)
                    transport.SetConnectionData("0.0.0.0", portToConnect, "0.0.0.0");

                status = $"Iniciando como Host na porta {portToConnect}...";
                NetworkManager.Singleton.StartHost();
            }
#endif
            yield return new WaitForSeconds(1f);
        }

        status = "Conectado como: " + (NetworkManager.Singleton.IsServer ? "Host" : "Client");
    }


    private void OnGUI()
    {
        string line1 = $"AutoStartNetwork: {status}";
        string line2 = $"IP/Porta configurados: {serverIP}:{serverPort}";

        // Sombra preta
        GUI.color = Color.black;
        GUI.Label(new Rect(10, 10, 600, 25), line1);
        GUI.Label(new Rect(10, 30, 600, 25), line2);

        // Texto branco
        GUI.color = Color.white;
        GUI.Label(new Rect(9, 9, 600, 25), line1);
        GUI.Label(new Rect(9, 29, 600, 25), line2);
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

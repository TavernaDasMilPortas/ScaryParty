using UnityEngine;
using Unity.Netcode;
using System.Collections;
#if UNITY_EDITOR
using ParrelSync;
#endif
using System.IO;
using System.Net;
using System.Net.Sockets;
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
            // Na Build: compara o IP do arquivo com os IPs locais desta máquina.
            // Se o IP do arquivo pertence a ESTA máquina → sou o Host.
            // Se pertence a OUTRA máquina → sou o Cliente.
            bool isThisMachineTheHost = IsLocalIP(ipToConnect);

            if (isThisMachineTheHost)
            {
                if (transport != null)
                    transport.SetConnectionData("0.0.0.0", portToConnect, "0.0.0.0");

                status = $"Este PC é o Host! Abrindo servidor na porta {portToConnect}...";
                bool ok = NetworkManager.Singleton.StartHost();
                if (!ok)
                    status = $"Falha ao abrir Host na porta {portToConnect}. Tentando novamente...";
            }
            else
            {
                if (transport != null)
                    transport.SetConnectionData(ipToConnect, portToConnect);

                status = $"Conectando como Cliente em {ipToConnect}:{portToConnect}...";
                bool ok = NetworkManager.Singleton.StartClient();
                if (!ok)
                    status = $"Falha ao iniciar cliente. Tentando novamente...";
            }
#endif
            yield return new WaitForSeconds(1f);
        }

        status = "Conectado como: " + (NetworkManager.Singleton.IsServer ? "Host" : "Client");
    }

    /// <summary>
    /// Retorna true se o IP informado pertence a esta máquina (incluindo loopback).
    /// </summary>
    private bool IsLocalIP(string ip)
    {
        // Loopback sempre é local
        if (ip == "127.0.0.1" || ip == "localhost" || ip == "0.0.0.0")
            return true;

        try
        {
            // Obtém todos os IPs de todas as interfaces de rede desta máquina
            string hostName = Dns.GetHostName();
            IPAddress[] localAddresses = Dns.GetHostAddresses(hostName);

            foreach (IPAddress addr in localAddresses)
            {
                if (addr.AddressFamily == AddressFamily.InterNetwork) // Só IPv4
                {
                    if (addr.ToString() == ip)
                    {
                        Debug.Log($"[AutoStartNetwork] IP '{ip}' pertence a esta máquina → iniciando como Host.");
                        return true;
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[AutoStartNetwork] Erro ao obter IPs locais: {e.Message}");
        }

        Debug.Log($"[AutoStartNetwork] IP '{ip}' não pertence a esta máquina → iniciando como Cliente.");
        return false;
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

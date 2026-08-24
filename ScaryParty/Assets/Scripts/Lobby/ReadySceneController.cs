using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;

/// <summary>
/// Controlador da cena intermediária de Sala de Espera (ReadyScene).
/// Roda em uma cena mínima (sem personagens, sem física), então não há
/// conflito com InputSystem ou UIDocument timing.
/// </summary>
public class ReadySceneController : MonoBehaviour
{
    private UIDocument _document;
    private VisualElement _playersContainer;
    private Button _btnReady;
    private Button _btnStartGame;
    private Button _btnCancel;

    private float _lastUpdate = -999f;

    private void Start()
    {
        // Garante cursor visível e solto nesta cena
        UnityEngine.Cursor.lockState = CursorLockMode.None;
        UnityEngine.Cursor.visible = true;

        _document = GetComponent<UIDocument>();
        if (_document == null)
        {
            Debug.LogError("[ReadyScene] UIDocument não encontrado!");
            return;
        }

        // UIDocument pode não estar pronto no mesmo frame — usa coroutine
        StartCoroutine(InitUIWhenReady());
    }

    private IEnumerator InitUIWhenReady()
    {
        // Aguarda até o rootVisualElement ser não-nulo E ter filhos
        while (_document.rootVisualElement == null ||
               _document.rootVisualElement.childCount == 0)
        {
            yield return null;
        }

        var root = _document.rootVisualElement;

        _playersContainer = root.Q<VisualElement>("PlayersContainer");
        _btnReady         = root.Q<Button>("BtnReady");
        _btnStartGame     = root.Q<Button>("BtnStartGame");
        _btnCancel        = root.Q<Button>("BtnCancel");

        Debug.Log($"[ReadyScene] UI inicializada. Ready={_btnReady != null}, Start={_btnStartGame != null}, Cancel={_btnCancel != null}");

        if (_btnReady      != null) _btnReady.clicked      += OnReadyClicked;
        if (_btnStartGame  != null) _btnStartGame.clicked  += OnStartGameClicked;
        if (_btnCancel     != null) _btnCancel.clicked     += OnCancelClicked;

        // BtnStartGame só aparece para o Host
        if (_btnStartGame != null)
            _btnStartGame.style.display = DisplayStyle.None;
    }

    private void Update()
    {
        if (_document == null || _document.rootVisualElement == null) return;
        if (NetworkManager.Singleton == null) return;
        if (!NetworkManager.Singleton.IsConnectedClient && !NetworkManager.Singleton.IsServer) return;
        if (_playersContainer == null) return;

        // Throttle: atualiza lista a cada 0.5s
        if (Time.time - _lastUpdate < 0.5f) return;
        _lastUpdate = Time.time;

        var allPlayers = FindObjectsByType<PlayerState>(FindObjectsSortMode.None);

        bool allReady = true;
        int playerCount = 0;

        _playersContainer.Clear();

        foreach (var ps in allPlayers)
        {
            if (ps == null) continue;
            playerCount++;

            var row = new VisualElement();
            row.AddToClassList("player-row");
            row.style.backgroundColor = ps.PlayerColor.Value;

            var nameLbl = new Label(ps.PlayerName.Value.ToString());
            nameLbl.AddToClassList("player-name");

            var statusLbl = new Label(ps.IsReady.Value ? "✅ PRONTO" : "⏳ AGUARDANDO...");
            statusLbl.AddToClassList("player-status");
            statusLbl.style.color = ps.IsReady.Value ? Color.green : Color.yellow;

            row.Add(nameLbl);
            row.Add(statusLbl);
            _playersContainer.Add(row);

            if (!ps.IsReady.Value) allReady = false;
        }

        // Só o Host vê o botão Start
        if (_btnStartGame != null)
        {
            bool isHost = NetworkManager.Singleton.IsServer;
            _btnStartGame.style.display = isHost ? DisplayStyle.Flex : DisplayStyle.None;
            if (isHost)
                _btnStartGame.SetEnabled(allReady && playerCount > 0);
        }
    }

    private void OnReadyClicked()
    {
        Debug.Log("[ReadyScene] Botão READY clicado");

        PlayerState myState = GetLocalPlayerState();
        if (myState == null)
        {
            Debug.LogWarning("[ReadyScene] PlayerState local não encontrado!");
            return;
        }

        bool newState = !myState.IsReady.Value;
        myState.SetReadyServerRpc(newState);

        if (_btnReady != null)
            _btnReady.text = newState ? "❌ NÃO ESTOU PRONTO" : "✅ ESTOU PRONTO";
    }

    private void OnStartGameClicked()
    {
        Debug.Log("[ReadyScene] Botão START clicado");
        if (!NetworkManager.Singleton.IsServer) return;

        // Carrega o Playground para todos via NetworkSceneManager.
        // O PlayerState detecta automaticamente que está no Playground e libera os controles.
        NetworkManager.Singleton.SceneManager.LoadScene(
            "Playground",
            UnityEngine.SceneManagement.LoadSceneMode.Single
        );
    }

    private void OnCancelClicked()
    {
        Debug.Log("[ReadyScene] Botão CANCELAR clicado");
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.Shutdown();

        SceneManager.LoadScene("LobbyScene");
    }

    private PlayerState GetLocalPlayerState()
    {
        // Tenta via SpawnManager primeiro
        if (NetworkManager.Singleton?.SpawnManager != null)
        {
            var po = NetworkManager.Singleton.SpawnManager.GetLocalPlayerObject();
            if (po != null) return po.GetComponent<PlayerState>();
        }

        // Fallback: varre todos e pega o que é dono
        var allPlayers = FindObjectsByType<PlayerState>(FindObjectsSortMode.None);
        foreach (var ps in allPlayers)
        {
            if (ps.IsOwner) return ps;
        }

        return null;
    }
}

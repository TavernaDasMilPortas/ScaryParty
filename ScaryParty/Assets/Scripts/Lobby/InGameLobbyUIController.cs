using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;

public class InGameLobbyUIController : NetworkBehaviour
{
    private UIDocument _document;
    private VisualElement _playersContainer;
    private Button _btnReady;
    private Button _btnStartGame;
    private Button _btnCancel;

    private void OnEnable()
    {
        _document = GetComponent<UIDocument>();
        if (_document == null) return;

        var root = _document.rootVisualElement;
        
        _playersContainer = root.Q<VisualElement>("PlayersContainer");
        _btnReady = root.Q<Button>("BtnReady");
        _btnStartGame = root.Q<Button>("BtnStartGame");
        _btnCancel = root.Q<Button>("BtnCancel");

        if (_btnReady != null) _btnReady.clicked += OnReadyClicked;
        if (_btnStartGame != null) _btnStartGame.clicked += OnStartGameClicked;
        if (_btnCancel != null) _btnCancel.clicked += OnCancelClicked;

        // O botão StartGame só é visível para o host
        if (_btnStartGame != null)
        {
            _btnStartGame.style.display = DisplayStyle.None;
        }
    }

    private float _lastUpdate = 0f;

    private void Update()
    {
        if (!IsSpawned || _document == null) return;

        // Se o jogo começou, esconde essa UI inteira
        bool isGameStarted = false;
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject != null)
            {
                var pState = client.PlayerObject.GetComponent<PlayerState>();
                if (pState != null && pState.IsGameStarted.Value) isGameStarted = true;
            }
        }

        if (isGameStarted)
        {
            _document.rootVisualElement.style.display = DisplayStyle.None;
            return;
        }

        // Throttle UI Rebuilds to avoid crashing UI Toolkit
        if (Time.time - _lastUpdate < 0.5f) return;
        _lastUpdate = Time.time;

        bool allReady = true;
        int playerCount = 0;

        _playersContainer.Clear();

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject == null) continue;

            var pState = client.PlayerObject.GetComponent<PlayerState>();
            if (pState == null) continue;

            playerCount++;

            // Criar UI pra esse jogador
            var row = new VisualElement();
            row.AddToClassList("player-row");
            row.style.backgroundColor = pState.PlayerColor.Value;

            var nameLabel = new Label(pState.PlayerName.Value.ToString());
            nameLabel.AddToClassList("player-name");

            var statusLabel = new Label(pState.IsReady.Value ? "PRONTO" : "AGUARDANDO...");
            statusLabel.AddToClassList("player-status");
            if (pState.IsReady.Value) statusLabel.style.color = Color.green;
            else statusLabel.style.color = Color.yellow;

            row.Add(nameLabel);
            row.Add(statusLabel);

            _playersContainer.Add(row);

            if (!pState.IsReady.Value) allReady = false;
        }

        // Host logic
        if (IsServer && _btnStartGame != null)
        {
            _btnStartGame.style.display = DisplayStyle.Flex;
            _btnStartGame.SetEnabled(allReady && playerCount > 0);
        }
    }

    private void OnReadyClicked()
    {
        if (NetworkManager.Singleton.LocalClient.PlayerObject != null)
        {
            var pState = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<PlayerState>();
            if (pState != null)
            {
                bool newState = !pState.IsReady.Value;
                pState.SetReadyServerRpc(newState);
                _btnReady.text = newState ? "NÃO ESTOU PRONTO" : "ESTOU PRONTO";
            }
        }
    }

    private void OnStartGameClicked()
    {
        if (!IsServer) return;

        // Inicia o jogo para todos
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            var pState = client.PlayerObject.GetComponent<PlayerState>();
            if (pState != null) pState.IsGameStarted.Value = true;
        }
    }

    private void OnCancelClicked()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
        }
        SceneManager.LoadScene("LobbyScene");
    }
}

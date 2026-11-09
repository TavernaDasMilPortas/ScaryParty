using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using StarterAssets;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using System.Collections;

public class PlayerState : NetworkBehaviour
{
    public NetworkVariable<FixedString32Bytes> PlayerName = new NetworkVariable<FixedString32Bytes>("", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<Color> PlayerColor = new NetworkVariable<Color>(Color.white, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> IsReady = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> IsGameStarted = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    /// <summary>
    /// Dinheiro acumulado pelo jogador. Somente o servidor pode alterar (autoridade do servidor).
    /// Sincronizado automaticamente para todos os clientes via NetworkVariable.
    /// </summary>
    public NetworkVariable<int> Money = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    /// <summary>
    /// Quantidade de pizzas que o jogador está carregando (0, 1 ou 2).
    /// Visível para todos os clientes no scoreboard.
    /// </summary>
    public NetworkVariable<int> HeldPizzas = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private ThirdPersonController _tpc;
    private PlayerInput _playerInput;
    private StarterAssetsInputs _inputs;
    private SkinnedMeshRenderer[] _meshRenderers;
    private ScaryParty.Player.CharacterVisualController _visualController;
    private ScaryParty.Player.CharacterAppearance _appearance;

    private void Awake()
    {
        _tpc = GetComponent<ThirdPersonController>();
        _playerInput = GetComponent<PlayerInput>();
        _inputs = GetComponent<StarterAssetsInputs>();
        _visualController = GetComponentInChildren<ScaryParty.Player.CharacterVisualController>(true);
        _appearance = GetComponentInChildren<ScaryParty.Player.CharacterAppearance>(true);
        _meshRenderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
    }

    private void Start()
    {
        // Se estiver rodando offline/sem Netcode ativo (ex: teste rápido direto na cena sem Host)
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
        {
            UpdateMovementLock(true);
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        bool isGameplayScene = (scene.name == "GameScene" || (scene.name != "ReadyScene" && scene.name != "LobbyScene"));

        if (IsServer)
        {
            IsGameStarted.Value = isGameplayScene;
        }

        if (isGameplayScene)
        {
            if (IsOwner)
            {
                RequestTeleportToSpawn();
            }
        }
        else
        {
            if (IsOwner)
            {
                UpdateMovementLock(false);
            }
        }
    }

    public override void OnNetworkSpawn()
    {
        Debug.Log($"[PlayerState] Spawn: owner={OwnerClientId}, local={NetworkManager.LocalClientId}, " +
            $"object={NetworkObjectId}, server={IsServer}, active={gameObject.activeInHierarchy}, " +
            $"scene={SceneManager.GetActiveScene().name}", this);

        // Listeners for state changes
        IsGameStarted.OnValueChanged += OnGameStartedChanged;
        PlayerColor.OnValueChanged += OnColorChanged;
        Money.OnValueChanged += OnMoneyChanged;

        string currentScene = SceneManager.GetActiveScene().name;
        bool isGameplayScene = (currentScene == "GameScene" || (currentScene != "ReadyScene" && currentScene != "LobbyScene"));

        // Se formos o servidor e a cena atual for o GameScene (ou cena de teste), inicia o jogo imediatamente
        if (IsServer)
        {
            if (isGameplayScene)
            {
                IsGameStarted.Value = true;
            }
        }

        // Apply initial states
        UpdateMovementLock(IsGameStarted.Value);
        ApplyColorToMesh(PlayerColor.Value);
        
        // Atualiza o HUD com o dinheiro atual (pode ser 0 no início, mas garante consistência)
        if (IsOwner && UIManager.Instance != null)
            UIManager.Instance.UpdateMoneyDisplay(Money.Value);

        if (IsOwner)
        {
            if (isGameplayScene)
            {
                RequestTeleportToSpawn();
            }

            // O Cliente local lê seus dados salvos e manda pro Servidor de forma diferida (após o registro do spawn)
            StartCoroutine(SubmitProfileDeferred());
        }
    }

    private Coroutine _teleportRoutine;

    /// <summary>
    /// Inicia o processo assíncrono de teleporte para a pizzaria/ponto de spawn assim que a cidade estiver pronta.
    /// </summary>
    public void RequestTeleportToSpawn()
    {
        if (!IsOwner) return;
        if (_teleportRoutine != null) StopCoroutine(_teleportRoutine);
        _teleportRoutine = StartCoroutine(RespawnOrTeleportToSpawnRoutine());
    }

    private IEnumerator RespawnOrTeleportToSpawnRoutine()
    {
        float elapsed = 0f;
        Vector3 targetPos = Vector3.zero;
        Quaternion targetRot = Quaternion.identity;
        bool found = false;

        // Determina o índice de spawn do jogador (0..3)
        int playerIndex = (int)OwnerClientId;
        var allPlayers = FindObjectsByType<PlayerState>(FindObjectsSortMode.None);
        if (allPlayers != null && allPlayers.Length > 0)
        {
            System.Array.Sort(allPlayers, (a, b) => a.OwnerClientId.CompareTo(b.OwnerClientId));
            for (int i = 0; i < allPlayers.Length; i++)
            {
                if (allPlayers[i] != null && allPlayers[i].OwnerClientId == OwnerClientId)
                {
                    playerIndex = i;
                    break;
                }
            }
        }

        while (elapsed < 10f)
        {
            if (ScaryParty.Pizzeria.Composition.PizzeriaRoot.Instance != null)
            {
                targetPos = ScaryParty.Pizzeria.Composition.PizzeriaRoot.Instance.GetPlayerSpawnPosition(playerIndex);
                targetRot = ScaryParty.Pizzeria.Composition.PizzeriaRoot.Instance.GetPlayerSpawnRotation(playerIndex);
                found = true;
                break;
            }

            var spawnPoint = GameObject.Find($"NetworkSpawnPoint_{playerIndex % 4}")
                ?? GameObject.Find("NetworkSpawnPoint_0")
                ?? GameObject.Find("NetworkSpawnPoint");

            if (spawnPoint != null)
            {
                targetPos = spawnPoint.transform.position;
                targetRot = spawnPoint.transform.rotation;
                found = true;
                break;
            }

            elapsed += 0.15f;
            yield return new WaitForSeconds(0.15f);
        }

        if (found)
        {
            PerformTeleport(targetPos, targetRot);
        }
        else
        {
            Debug.LogWarning($"[PlayerState] Timeout aguardando ponto de spawn para Player {OwnerClientId}. Posicionando em fallback (0, 1, 0).");
            PerformTeleport(new Vector3(0f, 1f, 0f), Quaternion.identity);
        }

        UpdateMovementLock(true);
        _teleportRoutine = null;
    }

    /// <summary>
    /// Executa o teletransporte com segurança física (CharacterController) e de rede (NetworkTransform).
    /// </summary>
    public void PerformTeleport(Vector3 targetPos, Quaternion targetRot)
    {
        var cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        Physics.SyncTransforms();

        transform.position = targetPos;
        transform.rotation = targetRot;
        Physics.SyncTransforms();

        var netTransform = GetComponent<NetworkTransform>();
        if (netTransform != null && netTransform.IsSpawned)
        {
            netTransform.Teleport(targetPos, targetRot, transform.localScale);
        }
        Physics.SyncTransforms();

        if (cc != null) cc.enabled = true;

        var fpCam = GetComponent<ScaryParty.Player.FirstPersonCameraController>();
        if (fpCam != null)
        {
            fpCam.SetYawPitch(targetRot.eulerAngles.y, 0f);
        }

        Debug.Log($"[PlayerState] ✅ Player {OwnerClientId} teletransportado com sucesso para {targetPos} (rot={targetRot.eulerAngles}).");
    }

    [ClientRpc]
    public void ForceTeleportClientRpc(Vector3 pos, Quaternion rot)
    {
        if (IsOwner)
        {
            PerformTeleport(pos, rot);
            UpdateMovementLock(true);
        }
    }

    private IEnumerator SubmitProfileDeferred()
    {
        yield return null; // Aguarda 1 frame para estabilização de rede e registro no NetworkManager

        string pName = "";
        Color pColor = Color.white;

        var pd = Resources.Load<PlayerData>("PlayerData");
        if (pd != null)
        {
            pName = pd.PlayerName;
            pColor = pd.PlayerColor;
        }
        else
        {
            pName = PlayerPrefs.GetString("PlayerName", "");
            if (PlayerPrefs.HasKey("PlayerColorR"))
            {
                pColor = new Color(
                    PlayerPrefs.GetFloat("PlayerColorR", 1f),
                    PlayerPrefs.GetFloat("PlayerColorG", 1f),
                    PlayerPrefs.GetFloat("PlayerColorB", 1f),
                    1f
                );
            }
        }

        if (string.IsNullOrWhiteSpace(pName))
        {
            pName = $"Player {OwnerClientId}";
        }

        SubmitProfileServerRpc(pName, pColor);
    }

    public override void OnNetworkDespawn()
    {
        IsGameStarted.OnValueChanged -= OnGameStartedChanged;
        PlayerColor.OnValueChanged -= OnColorChanged;
        Money.OnValueChanged -= OnMoneyChanged;
    }

    [ServerRpc]
    private void SubmitProfileServerRpc(string pName, Color pColor)
    {
        if (string.IsNullOrWhiteSpace(pName))
        {
            pName = $"Player {OwnerClientId}";
        }

        PlayerName.Value = new FixedString32Bytes(pName);
        
        // Verifica se alguém já tem essa cor
        Color finalColor = EnsureUniqueColor(pColor);
        PlayerColor.Value = finalColor;
    }

    [ServerRpc(RequireOwnership = false)]
    public void SetReadyServerRpc(bool ready)
    {
        IsReady.Value = ready;
    }

    /// <summary>
    /// Define o estado de pronto do jogador com suporte a execução direta pelo servidor ou via ServerRpc por clientes.
    /// </summary>
    public void SetReady(bool ready)
    {
        if (IsServer)
        {
            IsReady.Value = ready;
        }
        else
        {
            SetReadyServerRpc(ready);
        }
    }

    private Color EnsureUniqueColor(Color desiredColor)
    {
        Color result = desiredColor;
        float shiftAmount = 0.3f;

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.ConnectedClientsList != null)
        {
            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                if (client == null || client.PlayerObject == null) continue;
                if (client.PlayerObject == this.NetworkObject) continue;
                
                var otherState = client.PlayerObject.GetComponent<PlayerState>();
                if (otherState != null)
                {
                    // Se a cor for quase igual (distância RGB muito curta)
                    float dist = Vector4.Distance(result, otherState.PlayerColor.Value);
                    if (dist < 0.15f)
                    {
                        // Muda o brilho/luminosidade da cor
                        Color.RGBToHSV(result, out float h, out float s, out float v);
                        v = (v > 0.5f) ? v - shiftAmount : v + shiftAmount;
                        result = Color.HSVToRGB(h, s, Mathf.Clamp01(v));
                    }
                }
            }
        }
        return result;
    }

    private void OnGameStartedChanged(bool previous, bool current)
    {
        UpdateMovementLock(current);
    }

    private void OnColorChanged(Color previous, Color current)
    {
        ApplyColorToMesh(current);
    }

    private void UpdateMovementLock(bool started)
    {
        if (!IsOwner) return;

        if (_tpc == null) _tpc = GetComponent<ThirdPersonController>();
        if (_inputs == null) _inputs = GetComponent<StarterAssetsInputs>();

        // Bloqueia ou libera inputs dependendo se o jogo começou
        if (_tpc != null) _tpc.enabled = started;
        
        // Congela o CharacterController para evitar queda livre durante a espera
        var cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = started;
        
        if (_inputs != null)
        {
            _inputs.cursorLocked = started;
            _inputs.cursorInputForLook = started;
        }

        if (started)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private void ApplyColorToMesh(Color c)
    {
        if (_appearance == null)
            _appearance = GetComponentInChildren<ScaryParty.Player.CharacterAppearance>(true);

        if (_appearance != null)
        {
            _appearance.ApplyPrimaryColor(c);
            return;
        }

        if (_meshRenderers == null || _meshRenderers.Length == 0)
            _meshRenderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);

        if (_meshRenderers == null) return;
        var mpb = new MaterialPropertyBlock();
        foreach (var smr in _meshRenderers)
        {
            if (smr == null || smr.sharedMaterials == null) continue;
            for (int i = 0; i < smr.sharedMaterials.Length; i++)
            {
                smr.GetPropertyBlock(mpb, i);
                mpb.SetColor("_BaseColor", c);
                mpb.SetColor("_Color", c);
                mpb.SetColor("_MainColor", c);
                smr.SetPropertyBlock(mpb, i);
            }
        }
    }

    private void LateUpdate()
    {
        UpdatePreparingAnimationState();
    }

    private void UpdatePreparingAnimationState()
    {
        if (_visualController == null)
        {
            _visualController = GetComponentInChildren<ScaryParty.Player.CharacterVisualController>(true);
            if (_visualController == null) return;
        }

        bool isWorking = false;
        var netState = ScaryParty.Pizzeria.Network.PizzeriaNetworkState.Instance;
        if (netState != null && IsSpawned)
        {
            ulong myClientId = OwnerClientId;
            for (int i = 0; i < netState.StationSlots.Count; i++)
            {
                var slot = netState.StationSlots[i];
                if (slot.Phase != (byte)ScaryParty.Pizzeria.Domain.Types.OperationPhase.Running)
                    continue;

                // Somente trabalho real de cozinha (Process com ProcessId > 0, Package), não forno autônomo (ProcessId == 0)
                // nem ações de hold fora de bancada (atender cliente, arquivar comanda, descartar item).
                bool isManualKitchenOp = (slot.Kind == (byte)ScaryParty.Pizzeria.Domain.Types.OperationKind.Process && slot.ProcessId > 0) ||
                                         slot.Kind == (byte)ScaryParty.Pizzeria.Domain.Types.OperationKind.Package;

                if (isManualKitchenOp &&
                    (slot.WorkerId == myClientId ||
                     (slot.InitiatorClientId == myClientId && slot.WorkerId != ScaryParty.Pizzeria.Domain.Models.OperationState.NoWorker)))
                {
                    isWorking = true;
                    break;
                }
            }
        }

        if (_visualController.IsPreparing != isWorking)
        {
            _visualController.SetPreparing(isWorking);
        }
    }

    /// <summary>
    /// Callback disparado quando o dinheiro do jogador muda (via NetworkVariable).
    /// Apenas o dono local atualiza seu HUD.
    /// </summary>
    private void OnMoneyChanged(int previous, int current)
    {
        if (IsOwner && UIManager.Instance != null)
        {
            UIManager.Instance.UpdateMoneyDisplay(current);
        }
    }

    /// <summary>
    /// O cliente informa ao servidor quantas pizzas está carregando.
    /// O servidor atualiza a NetworkVariable que é visível para todos (scoreboard).
    /// </summary>
    [ServerRpc]
    public void UpdateHeldPizzasServerRpc(int count)
    {
        HeldPizzas.Value = Mathf.Clamp(count, 0, 2);
    }
}

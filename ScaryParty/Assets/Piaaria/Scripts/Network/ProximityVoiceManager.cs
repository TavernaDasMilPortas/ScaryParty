using System.Collections;
using UnityEngine;
using Unity.Netcode;

#if !UNITY_SERVER && !UNITY_DEDICATED_SERVER
using Unity.Services.Vivox;
#endif

/// <summary>
/// Gerencia o chat de áudio por proximidade entre jogadores usando o Vivox SDK.
///
/// ARQUITETURA:
///   - VIVOX NATIVO: Usa canais posicionais 3D do próprio Vivox.
///   - CLIENTE:      Inicializa o Vivox, entra no canal posicional (JoinPositionalChannelAsync)
///                   e envia a sua posição 3D local no Update().
///   - SERVIDOR:     Nenhuma lógica de áudio. O servidor Unity headless
///                   não processa voz, reduzindo uso de CPU.
///
/// GUARDS DE BUILD:
///   Todo código Vivox fica dentro de #if !UNITY_SERVER para que a biblioteca
///   nativa libvivoxsdk.so NÃO seja incluída no build do servidor Linux ARM64.
/// </summary>
public class ProximityVoiceManager : NetworkBehaviour
{
    // -------------------------------------------------------------------------
    // Configuração — ajuste no Inspector
    // -------------------------------------------------------------------------

    [Header("Distâncias de Proximidade (Vivox 3D)")]
    [Tooltip("Distância máxima em que o jogador ouve outro (AudibleDistance).")]
    [SerializeField] private int _hearingRangeMax = 25;

    [Tooltip("Distância em que a voz começa a diminuir (ConversationalDistance).")]
    [SerializeField] private int _hearingRangeFull = 5;

    [Tooltip("Intensidade do fade de áudio por distância (AudioFadeIntensityByDistance).")]
    [SerializeField] private float _fadeIntensity = 1.0f;

    [Header("Vivox — Canal de Voz")]
    [Tooltip("Nome base do canal. O Room ID será concatenado para isolar salas.")]
    [SerializeField] private string _channelBaseName = "ScaryParty";

    // -------------------------------------------------------------------------
    // Estado local
    // -------------------------------------------------------------------------

    // Identificador único da sala — gerado pelo host e repassado na inicialização
    private string _roomId = "default";

#if !UNITY_SERVER && !UNITY_DEDICATED_SERVER
    private Transform _localPlayerTransform;
    private bool _isInChannel = false;
#endif

    // -------------------------------------------------------------------------
    // Lifecycle — NetworkBehaviour
    // -------------------------------------------------------------------------

    public override void OnNetworkSpawn()
    {
#if !UNITY_SERVER && !UNITY_DEDICATED_SERVER
        // ── CLIENTE ──────────────────────────────────────────────────────────
        StartCoroutine(InitializeVivoxAsync());
#endif
    }

    public override void OnNetworkDespawn()
    {
#if !UNITY_SERVER && !UNITY_DEDICATED_SERVER
        // ── CLIENTE ──────────────────────────────────────────────────────────
        _isInChannel = false;
        StartCoroutine(LeaveVivoxChannelAsync());
#endif
    }

#if !UNITY_SERVER && !UNITY_DEDICATED_SERVER
    
    private void Update()
    {
        // Atualiza a posição 3D do jogador local no Vivox a cada frame
        if (_isInChannel && _localPlayerTransform != null && VivoxService.Instance.IsLoggedIn)
        {
            VivoxService.Instance.Set3DPosition(
                _localPlayerTransform.gameObject, 
                GetChannelName()
            );
        }
        // Tenta localizar o jogador local caso ainda não tenha encontrado
        else if (_localPlayerTransform == null && NetworkManager.Singleton.LocalClient?.PlayerObject != null)
        {
            _localPlayerTransform = NetworkManager.Singleton.LocalClient.PlayerObject.transform;
        }
    }

    // =========================================================================
    // BLOCO DO CLIENTE — integração com Vivox
    // =========================================================================

    /// <summary>
    /// Configura o ID da sala antes do spawn. Chamado pelo GameManager
    /// antes de instanciar este objeto na rede.
    /// </summary>
    public void SetRoomId(string roomId)
    {
        _roomId = roomId;
    }

    private string GetChannelName()
    {
        return $"{_channelBaseName}_{_roomId}";
    }

    private IEnumerator InitializeVivoxAsync()
    {
        // Aguarda o Unity Gaming Services estar inicializado
        yield return new WaitUntil(() => VivoxService.Instance != null);

        // Só inicializa se ainda não estiver logado
        if (VivoxService.Instance.IsLoggedIn)
        {
            yield return JoinPositionalChannelAsync();
            yield break;
        }

        var loginTask = VivoxService.Instance.LoginAsync(new LoginOptions
        {
            DisplayName = PlayerPrefs.GetString("PlayerName", $"Player_{NetworkManager.Singleton.LocalClientId}")
        });

        yield return new WaitUntil(() => loginTask.IsCompleted);

        if (loginTask.IsFaulted)
        {
            Debug.LogError($"[ProximityVoice] Falha no login Vivox: {loginTask.Exception?.Message}");
            yield break;
        }

        yield return JoinPositionalChannelAsync();
    }

    private IEnumerator JoinPositionalChannelAsync()
    {
        string channelName = GetChannelName();

        var joinTask = VivoxService.Instance.JoinPositionalChannelAsync(
            channelName,
            ChatCapability.AudioOnly,
            new Channel3DProperties(
                audibleDistance:         _hearingRangeMax,
                conversationalDistance:  _hearingRangeFull,
                audioFadeIntensityByDistanceaudio: _fadeIntensity,
                audioFadeModel:          AudioFadeModel.InverseByDistance
            )
        );

        yield return new WaitUntil(() => joinTask.IsCompleted);

        if (joinTask.IsFaulted)
        {
            Debug.LogError($"[ProximityVoice] Falha ao entrar no canal '{channelName}': {joinTask.Exception?.Message}");
        }
        else
        {
            Debug.Log($"[ProximityVoice] Conectado ao canal posicional '{channelName}'.");
            _isInChannel = true;
        }
    }

    private IEnumerator LeaveVivoxChannelAsync()
    {
        if (VivoxService.Instance == null || !VivoxService.Instance.IsLoggedIn)
            yield break;

        var leaveTask = VivoxService.Instance.LeaveChannelAsync(GetChannelName());
        yield return new WaitUntil(() => leaveTask.IsCompleted);
    }

#endif // !UNITY_SERVER && !UNITY_DEDICATED_SERVER
}

using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using StarterAssets;
using UnityEngine.InputSystem;

public class PlayerState : NetworkBehaviour
{
    public NetworkVariable<FixedString32Bytes> PlayerName = new NetworkVariable<FixedString32Bytes>("", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<Color> PlayerColor = new NetworkVariable<Color>(Color.white, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> IsReady = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> IsGameStarted = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private ThirdPersonController _tpc;
    private PlayerInput _playerInput;
    private StarterAssetsInputs _inputs;
    private SkinnedMeshRenderer[] _meshRenderers;

    private void Awake()
    {
        _tpc = GetComponent<ThirdPersonController>();
        _playerInput = GetComponent<PlayerInput>();
        _inputs = GetComponent<StarterAssetsInputs>();
        _meshRenderers = GetComponentsInChildren<SkinnedMeshRenderer>();
    }

    public override void OnNetworkSpawn()
    {
        // Listeners for state changes
        IsGameStarted.OnValueChanged += OnGameStartedChanged;
        PlayerColor.OnValueChanged += OnColorChanged;

        // Apply initial states
        UpdateMovementLock(IsGameStarted.Value);
        ApplyColorToMesh(PlayerColor.Value);

        if (IsOwner)
        {
            // Tenta teleportar para a pizzaria caso a cidade já tenha sido gerada antes de nascer
            var spawnPoint = GameObject.Find("NetworkSpawnPoint");
            if (spawnPoint != null)
            {
                var cc = GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                transform.position = spawnPoint.transform.position;
                transform.rotation = spawnPoint.transform.rotation;
                if (cc != null) cc.enabled = true;
            }

            // O Cliente local lê seus dados salvos e manda pro Servidor
            var pd = Resources.Load<PlayerData>("PlayerData");
            if (pd != null)
            {
                SubmitProfileServerRpc(pd.PlayerName, pd.PlayerColor);
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        IsGameStarted.OnValueChanged -= OnGameStartedChanged;
        PlayerColor.OnValueChanged -= OnColorChanged;
    }

    [ServerRpc]
    private void SubmitProfileServerRpc(string pName, Color pColor)
    {
        PlayerName.Value = new FixedString32Bytes(pName);
        
        // Verifica se alguém já tem essa cor
        Color finalColor = EnsureUniqueColor(pColor);
        PlayerColor.Value = finalColor;
    }

    [ServerRpc]
    public void SetReadyServerRpc(bool ready)
    {
        IsReady.Value = ready;
    }

    private Color EnsureUniqueColor(Color desiredColor)
    {
        Color result = desiredColor;
        float shiftAmount = 0.3f;

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject == null) continue; // <--- NULL CHECK VITAL AQUI
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

        // Bloqueia ou libera inputs dependendo se o jogo começou
        if (_tpc != null) _tpc.enabled = started;
        
        if (_inputs != null)
        {
            _inputs.cursorLocked = started;
            _inputs.cursorInputForLook = started;
        }

        if (started)
        {
            if (_playerInput != null) _playerInput.ActivateInput();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            if (_playerInput != null) _playerInput.DeactivateInput();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private void ApplyColorToMesh(Color c)
    {
        if (_meshRenderers == null) return;
        foreach (var smr in _meshRenderers)
        {
            if (smr.material != null)
            {
                // Clona o material pra não mudar o de todo mundo
                Material mat = new Material(smr.material);
                mat.color = c;
                smr.material = mat;
            }
        }
    }
}

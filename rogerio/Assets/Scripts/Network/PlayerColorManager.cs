using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class PlayerColorManager : NetworkBehaviour
{
    // A NetworkVariable para sincronizar a cor escolhida pelo servidor com todos os clientes
    private NetworkVariable<Color> playerColor = new NetworkVariable<Color>(Color.white, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // Mantemos um Hue global estático para garantir que cores fiquem bem espaçadas
    private static float currentHue = -1f;

    private Dictionary<Renderer, Material[]> clonedMaterials = new Dictionary<Renderer, Material[]>();

    private void Awake()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();

        // Clonamos os materiais para que a alteração de cor não afete os outros jogadores ou o prefab original
        foreach (var r in renderers)
        {
            if (r != null && r.sharedMaterials.Length > 0)
            {
                Material[] mats = new Material[r.sharedMaterials.Length];
                for(int i = 0; i < r.sharedMaterials.Length; i++)
                {
                    if (r.sharedMaterials[i] != null)
                        mats[i] = new Material(r.sharedMaterials[i]);
                }
                r.materials = mats; // Aplica a nova instância
                clonedMaterials[r] = mats; // Salva a referência para mudar a cor depois
            }
        }
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            // O servidor define uma nova cor com saturação e brilho fixos, mas mudando apenas a matiz (Hue)
            if (currentHue < 0f)
            {
                currentHue = Random.Range(0f, 1f); // Hue inicial aleatório
            }
            else
            {
                currentHue += 0.618033988749895f; // Golden ratio, garante que as cores nunca se repitam e fiquem bem espaçadas
                currentHue %= 1f;
            }
            
            // Saturation = 0.8 e Value = 0.9 costumam dar cores "Toon" bem vibrantes e agradáveis
            playerColor.Value = Color.HSVToRGB(currentHue, 0.8f, 0.9f);
        }

        // Aplica a cor inicialmente
        ApplyColor(playerColor.Value);
        
        // Escuta caso a cor mude (para quem entrar depois)
        playerColor.OnValueChanged += (Color previousValue, Color newValue) => { 
            ApplyColor(newValue); 
        };
    }

    private void ApplyColor(Color color)
    {
        foreach (var kvp in clonedMaterials)
        {
            foreach (var mat in kvp.Value)
            {
                if (mat != null)
                {
                    // O material "Illustrate" do Distant Lands usa a propriedade _MainColor
                    if (mat.HasProperty("_MainColor"))
                    {
                        mat.SetColor("_MainColor", color);
                    }
                }
            }
        }
    }
    
    // Método para resetar o index de cores se o servidor for desligado (opcional)
    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            // Poderíamos reciclar a cor aqui, mas como é um party game, resetamos no encerramento apenas
            if (NetworkManager.Singleton.ConnectedClients.Count <= 1) 
            {
                currentHue = -1f;
            }
        }
    }
}

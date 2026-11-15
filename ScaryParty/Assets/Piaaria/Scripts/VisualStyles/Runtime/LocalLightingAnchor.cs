using UnityEngine;

namespace ScaryParty.Lighting
{
    /// <summary>
    /// Ancora leve para iluminacao local ou emissao de vitrine/letreiro em modulos da cidade procedural.
    /// Pode ser atendida por janela emissiva (custo ~0) ou pelo StylizedLightPool (sem sombras).
    /// </summary>
    public class LocalLightingAnchor : MonoBehaviour
    {
        public string anchorId;
        public bool allowRealtimePooledLight = true;
        public bool nightOnly = true;
        public Color lightColor = new Color(1.0f, 0.84f, 0.55f, 1f);
        [Range(0.2f, 4f)] public float baseIntensity = 1.4f;
        [Range(2f, 20f)] public float range = 8.5f;
        [Range(0, 10)] public int priority = 1;

        [NonSerializedIfNeeded] public float currentCooldownUntil;
        [NonSerializedIfNeeded] public bool isCurrentlyAllocated;

        // Nota: atributo simples interno evita depender de namespaces externos
        private sealed class NonSerializedIfNeededAttribute : System.Attribute { }
    }
}

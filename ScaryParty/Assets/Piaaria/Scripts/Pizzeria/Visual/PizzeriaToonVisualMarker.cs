using System;
using UnityEngine;

namespace ScaryParty.Pizzeria.Visual
{
    /// <summary>
    /// Marcador persistido exclusivamente nas cópias e variantes cel shading da pizzaria.
    /// Registra proveniência, versão do perfil e metadados para garantir rastreabilidade
    /// e evitar mutações acidentais nos prefabs canônicos de produção.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("ScaryParty/Pizzeria/Visual/Pizzeria Toon Visual Marker")]
    public class PizzeriaToonVisualMarker : MonoBehaviour
    {
        [Header("Proveniência")]
        [Tooltip("GUID do prefab original a partir do qual esta variante foi clonada.")]
        [SerializeField] private string _sourcePrefabGuid;

        [Tooltip("Caminho do asset original de origem.")]
        [SerializeField] private string _sourcePrefabPath;

        [Header("Perfil e Versão")]
        [Tooltip("GUID do ScriptableObject PizzeriaAppearanceProfile utilizado.")]
        [SerializeField] private string _profileGuid;

        [Tooltip("Versão semântica ou identificador da variante toon.")]
        [SerializeField] private string _variantVersion = "1.0.0-softtoon";

        [Tooltip("Hash determinístico da estrutura do prefab original no momento do clone.")]
        [SerializeField] private string _sourceContentHash;

        [Tooltip("Carimbo de data/hora ISO 8601 da última geração.")]
        [SerializeField] private string _buildTimestamp;

        [Header("Flags de Modo")]
        [Tooltip("Indica se elementos demonstrativos de showcase (iluminação local, personagens preview) estão presentes.")]
        [SerializeField] private bool _hasShowcaseRig = false;

        public string SourcePrefabGuid => _sourcePrefabGuid;
        public string SourcePrefabPath => _sourcePrefabPath;
        public string ProfileGuid => _profileGuid;
        public string VariantVersion => _variantVersion;
        public string SourceContentHash => _sourceContentHash;
        public string BuildTimestamp => _buildTimestamp;
        public bool HasShowcaseRig => _hasShowcaseRig;

        public void Initialize(string sourceGuid, string sourcePath, string profileGuid, string version, string sourceHash, bool hasShowcase)
        {
            _sourcePrefabGuid = sourceGuid;
            _sourcePrefabPath = sourcePath;
            _profileGuid = profileGuid;
            _variantVersion = string.IsNullOrEmpty(version) ? "1.0.0-softtoon" : version;
            _sourceContentHash = sourceHash;
            _buildTimestamp = DateTime.UtcNow.ToString("o");
            _hasShowcaseRig = hasShowcase;
        }

        public void SetShowcaseRigState(bool hasShowcase)
        {
            _hasShowcaseRig = hasShowcase;
            _buildTimestamp = DateTime.UtcNow.ToString("o");
        }
    }
}

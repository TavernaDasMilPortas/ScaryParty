using UnityEngine;

namespace ScaryParty.Player
{
    /// <summary>
    /// Centraliza a aplicação de cores, materiais toon/URP e camadas visuais (ex: LocalPlayerModel)
    /// para personagens jogáveis, previews e clientes (NPCs).
    /// Usa MaterialPropertyBlock para não acumular instâncias clonadas de Material em runtime.
    /// Garante que a malha que recebe a cor é o CORPO (M_Body / Slot 3), preservando o avental (M_Apron) e olhos.
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterAppearance : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int MainColorId = Shader.PropertyToID("_MainColor");

        [Header("Referências de Malha")]
        [Tooltip("SkinnedMeshRenderer principal do personagem (Cylinder.002 no Character.fbx).")]
        [SerializeField] private SkinnedMeshRenderer _primaryRenderer;

        [Tooltip("Índice do slot de material primário do corpo (3 = M_Body / Material.001 no rig canônico).")]
        [SerializeField] private int _primaryColorMaterialSlot = 3;

        [Tooltip("Nome semântico do material do corpo para detecção automática.")]
        [SerializeField] private string _primaryColorMaterialName = "M_Body";

        [Tooltip("Se verdadeiro, aplica a cor apenas no slot do corpo (preservando avental e olhos). Se falso, aplica em todos os slots.")]
        [SerializeField] private bool _colorOnlyPrimarySlot = true;

        [Header("Estado Atual")]
        [SerializeField] private Color _currentPrimaryColor = new Color(0.85f, 0.18f, 0.15f, 1f);

        private SkinnedMeshRenderer[] _allSkinnedRenderers;
        private MeshRenderer[] _allMeshRenderers;
        private MaterialPropertyBlock _propertyBlock;

        public Color CurrentPrimaryColor => _currentPrimaryColor;
        public SkinnedMeshRenderer PrimaryRenderer => _primaryRenderer;
        public int PrimaryColorMaterialSlot => _primaryColorMaterialSlot;

        private void Awake()
        {
            EnsureRenderersCached();
            ApplyPrimaryColor(_currentPrimaryColor);
        }

        public void EnsureRenderersCached()
        {
            if (_propertyBlock == null)
            {
                _propertyBlock = new MaterialPropertyBlock();
            }

            if (_allSkinnedRenderers == null || _allSkinnedRenderers.Length == 0)
            {
                _allSkinnedRenderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            }

            if (_allMeshRenderers == null || _allMeshRenderers.Length == 0)
            {
                _allMeshRenderers = GetComponentsInChildren<MeshRenderer>(true);
            }

            if (_primaryRenderer == null && _allSkinnedRenderers != null && _allSkinnedRenderers.Length > 0)
            {
                _primaryRenderer = _allSkinnedRenderers[0];
            }
        }

        public void SetPrimaryRenderer(SkinnedMeshRenderer smr, int primarySlot = 3, bool onlyPrimarySlot = true)
        {
            _primaryRenderer = smr;
            _primaryColorMaterialSlot = primarySlot;
            _colorOnlyPrimarySlot = onlyPrimarySlot;
            _allSkinnedRenderers = null;
            _allMeshRenderers = null;
            EnsureRenderersCached();
        }

        /// <summary>
        /// Resolve o índice do slot do corpo procurando por nome semântico ("Body", "Corpo") ou caindo no slot 3.
        /// </summary>
        public int ResolveBodySlot(Material[] sharedMats)
        {
            if (sharedMats == null || sharedMats.Length == 0) return 0;

            // 1. Procura por nome configurado ou palavras-chave Body/Corpo
            for (int i = 0; i < sharedMats.Length; i++)
            {
                var mat = sharedMats[i];
                if (mat == null) continue;
                string n = mat.name;
                if (!string.IsNullOrEmpty(_primaryColorMaterialName) &&
                    n.IndexOf(_primaryColorMaterialName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return i;
                }
                if (n.IndexOf("Body", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("Corpo", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return i;
                }
            }

            // 2. Se tiver os 4 slots canônicos (0=Apron, 1=Pupil, 2=Sclera, 3=Body), retorna slot 3
            if (sharedMats.Length == 4)
            {
                return 3;
            }

            // 3. Fallback para slot configurado
            return Mathf.Clamp(_primaryColorMaterialSlot, 0, sharedMats.Length - 1);
        }

        /// <summary>
        /// Aplica a cor primária usando MaterialPropertyBlock (zero vazamento de instâncias de Material).
        /// Pinta exclusivamente o corpo, mantendo o avental e detalhes intactos.
        /// </summary>
        public void ApplyPrimaryColor(Color color)
        {
            _currentPrimaryColor = color;
            EnsureRenderersCached();

            if (_allSkinnedRenderers == null || _allSkinnedRenderers.Length == 0)
                return;

            foreach (var smr in _allSkinnedRenderers)
            {
                if (smr == null) continue;
                var sharedMats = smr.sharedMaterials;
                if (sharedMats == null || sharedMats.Length == 0) continue;

                if (_colorOnlyPrimarySlot && sharedMats.Length > 1)
                {
                    int slot = ResolveBodySlot(sharedMats);
                    ApplyColorBlockToSlot(smr, slot, color);
                }
                else
                {
                    for (int i = 0; i < sharedMats.Length; i++)
                    {
                        ApplyColorBlockToSlot(smr, i, color);
                    }
                }
            }
        }

        private void ApplyColorBlockToSlot(Renderer renderer, int slotIndex, Color color)
        {
            if (renderer == null) return;
            renderer.GetPropertyBlock(_propertyBlock, slotIndex);
            _propertyBlock.SetColor(BaseColorId, color);
            _propertyBlock.SetColor(ColorId, color);
            _propertyBlock.SetColor(MainColorId, color);
            renderer.SetPropertyBlock(_propertyBlock, slotIndex);
        }

        /// <summary>
        /// Configura visibilidade em primeira pessoa (oculta a malha para a câmera local via layer LocalPlayerModel, mantendo sombras).
        /// </summary>
        public void ConfigureFirstPersonVisibility(bool isLocalFirstPerson)
        {
            EnsureRenderersCached();
            int targetLayer = 0;
            if (isLocalFirstPerson)
            {
                int localLayer = LayerMask.NameToLayer("LocalPlayerModel");
                if (localLayer >= 0) targetLayer = localLayer;
            }

            var shadowMode = isLocalFirstPerson
                ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly
                : UnityEngine.Rendering.ShadowCastingMode.On;

            if (_allSkinnedRenderers != null)
            {
                foreach (var smr in _allSkinnedRenderers)
                {
                    if (smr == null) continue;
                    smr.shadowCastingMode = shadowMode;
                    if (targetLayer > 0 || !isLocalFirstPerson)
                        smr.gameObject.layer = targetLayer;
                }
            }

            if (_allMeshRenderers != null)
            {
                foreach (var mr in _allMeshRenderers)
                {
                    if (mr == null) continue;
                    mr.shadowCastingMode = shadowMode;
                    if (targetLayer > 0 || !isLocalFirstPerson)
                        mr.gameObject.layer = targetLayer;
                }
            }
        }
    }
}

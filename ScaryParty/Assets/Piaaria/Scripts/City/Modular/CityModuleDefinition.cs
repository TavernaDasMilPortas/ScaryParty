using System;
using UnityEngine;

namespace ScaryParty.City.Modular
{
    /// <summary>
    /// Metadados e contrato estrito de uma peça do kit de 12 peças da cidade modular.
    /// Define dimensões nominais, bounds de clearance, ocupação de slots e âncoras.
    /// </summary>
    [Serializable]
    public class CityModuleDefinition
    {
        [Header("Identificação")]
        public CityModuleId id;
        public string displayName;
        public CityModuleCategory category;
        public GameObject prefab;

        [Header("Dimensões Estruturais (Metros)")]
        [Tooltip("Largura nominal da peça (X). Painéis comuns = 2m, Garagem = 4m, Pilar = 0.2m.")]
        public float width = 2f;

        [Tooltip("Altura nominal da peça (Y). Painéis = 3m, Cobertura = 0.2m, Platibanda = 0.4m, Toldo = 0.4m.")]
        public float height = 3f;

        [Tooltip("Profundidade/Espessura nominal (Z). Painéis = 0.2m, Cobertura = 2m, Toldo = 0.8m, Pilar = 0.2m.")]
        public float depth = 0.2f;

        [Tooltip("Número de slots horizontais de 2 metros ocupados na fachada. Garagem = 2, painéis normais = 1, acabamentos/pilares = 0.")]
        public int occupiedSlots = 1;

        [Header("Saliências e Clearance")]
        [Tooltip("Avanço além da face externa em direção a +Z (fora da fachada). Ex: H floreira = 0.3m, K toldo = 0.8m.")]
        public float exteriorProtrusion = 0f;

        [Tooltip("Profundidade que a peça avança para dentro no sentido -Z. Padrão de painéis = 0.20m.")]
        public float interiorDepth = 0.2f;

        [Tooltip("Altura livre mínima do solo para montagem (útil para toldos K sobre calçadas).")]
        public float minClearanceHeight = 0f;

        [Header("Propriedades Funcionais")]
        public bool isEntrance = false;
        public bool isDoubleDoor = false;
        public bool isGarage = false;
        public bool isRoof = false;
        public bool isParapet = false;
        public bool isAwning = false;
        public bool isCornerPillar = false;

        [Header("Âncoras Locais")]
        public Vector3 entranceAnchorLocal = Vector3.zero;
        public Vector3 approachAnchorLocal = Vector3.forward * 1.0f;
        public Vector3 interiorAnchorLocal = Vector3.back * 1.0f;
        public Vector3 deliveryAnchorLocal = Vector3.zero;
        public Vector3 awningAnchorLocal = Vector3.zero;
        public Vector3 movingPartHingeLocal = Vector3.zero;

        [Header("Slots de Materiais")]
        public CityModuleMaterialRole[] materialRoles = new CityModuleMaterialRole[0];

        /// <summary>
        /// Bounds estruturais no espaço local da peça.
        /// Convenção:
        /// - Painéis (A-D, G-J): Origem no centro da borda inferior externa. X em [-width/2, +width/2], Y em [0, height], Z em [-depth, 0].
        /// - Cobertura (E): Origem no centro da face inferior. X em [-1, +1], Y em [0, 0.2], Z em [-1, +1].
        /// - Platibanda (F): X em [-width/2, +width/2], Y em [0, height], Z em [-depth, 0].
        /// - Toldo (K): Origem no centro da borda superior traseira junto à fachada. Y em [-0.4, 0], Z em [0, +0.8].
        /// - Pilar (L): Origem no centro da base. X em [-0.1, +0.1], Y em [0, height], Z em [-0.1, +0.1].
        /// </summary>
        public Bounds GetStructuralBounds()
        {
            switch (id)
            {
                case CityModuleId.E: // Cobertura plana: centrada em X e Z, altura para +Y
                    return new Bounds(new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height, depth));

                case CityModuleId.K: // Toldo: estende-se para baixo (-Y) e para fora (+Z) a partir do topo traseiro
                    return new Bounds(new Vector3(0f, -height * 0.5f, depth * 0.5f), new Vector3(width, height, depth));

                case CityModuleId.L: // Pilar: centrado na base (0.2 x 0.2), sobe em +Y
                    return new Bounds(new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height, depth));

                default: // Painéis e platibanda: origem na borda inferior da face externa (+Z frontal, avança -Z para dentro)
                    return new Bounds(new Vector3(0f, height * 0.5f, -depth * 0.5f), new Vector3(width, height, depth));
            }
        }

        /// <summary>
        /// Bounds totais incluindo saliências decorativas externas (ex: floreira H, toldo K).
        /// </summary>
        public Bounds GetClearanceBounds()
        {
            Bounds b = GetStructuralBounds();
            if (exteriorProtrusion > 0f)
            {
                // Expande o envelope para a frente (+Z)
                Vector3 center = b.center + new Vector3(0f, 0f, exteriorProtrusion * 0.5f);
                Vector3 size = new Vector3(b.size.x, b.size.y, b.size.z + exteriorProtrusion);
                return new Bounds(center, size);
            }
            return b;
        }

        /// <summary>
        /// Inicializa valores contratuais canônicos do kit conforme a especificação do documento.
        /// </summary>
        public static CityModuleDefinition CreateCanonical(CityModuleId id)
        {
            CityModuleDefinition def = new CityModuleDefinition();
            def.id = id;

            switch (id)
            {
                case CityModuleId.A:
                    def.displayName = "Parede Lisa";
                    def.category = CityModuleCategory.WallSolid;
                    def.width = 2.0f; def.height = 3.0f; def.depth = 0.2f;
                    def.occupiedSlots = 1;
                    def.materialRoles = new[] { CityModuleMaterialRole.Wall, CityModuleMaterialRole.Trim };
                    break;

                case CityModuleId.B:
                    def.displayName = "Parede com Janela";
                    def.category = CityModuleCategory.WallWindow;
                    def.width = 2.0f; def.height = 3.0f; def.depth = 0.2f;
                    def.occupiedSlots = 1;
                    def.awningAnchorLocal = new Vector3(0f, 2.7f, 0f);
                    def.materialRoles = new[] { CityModuleMaterialRole.Wall, CityModuleMaterialRole.Trim, CityModuleMaterialRole.Glass };
                    break;

                case CityModuleId.C:
                    def.displayName = "Parede com Porta Simples";
                    def.category = CityModuleCategory.WallDoor;
                    def.width = 2.0f; def.height = 3.0f; def.depth = 0.2f;
                    def.occupiedSlots = 1;
                    def.isEntrance = true;
                    // Vão de 1.0m x 2.2m centralizado
                    def.entranceAnchorLocal = new Vector3(0f, 0f, 0f);
                    def.approachAnchorLocal = new Vector3(0f, 0f, 1.0f);
                    def.interiorAnchorLocal = new Vector3(0f, 0f, -1.0f);
                    def.deliveryAnchorLocal = new Vector3(0f, 0f, 0.4f);
                    def.awningAnchorLocal = new Vector3(0f, 2.6f, 0f);
                    def.movingPartHingeLocal = new Vector3(-0.5f, 1.1f, -0.1f);
                    def.materialRoles = new[] { CityModuleMaterialRole.Wall, CityModuleMaterialRole.Trim, CityModuleMaterialRole.Wood };
                    break;

                case CityModuleId.D:
                    def.displayName = "Parede com Vitrine";
                    def.category = CityModuleCategory.WallShopfront;
                    def.width = 2.0f; def.height = 3.0f; def.depth = 0.2f;
                    def.occupiedSlots = 1;
                    def.awningAnchorLocal = new Vector3(0f, 2.7f, 0f);
                    def.materialRoles = new[] { CityModuleMaterialRole.Wall, CityModuleMaterialRole.Trim, CityModuleMaterialRole.Glass };
                    break;

                case CityModuleId.E:
                    def.displayName = "Cobertura Plana";
                    def.category = CityModuleCategory.RoofFlat;
                    def.width = 2.0f; def.height = 0.2f; def.depth = 2.0f;
                    def.occupiedSlots = 1;
                    def.isRoof = true;
                    def.materialRoles = new[] { CityModuleMaterialRole.Trim, CityModuleMaterialRole.Wall };
                    break;

                case CityModuleId.F:
                    def.displayName = "Platibanda";
                    def.category = CityModuleCategory.Parapet;
                    def.width = 2.0f; def.height = 0.4f; def.depth = 0.2f;
                    def.occupiedSlots = 1;
                    def.isParapet = true;
                    def.materialRoles = new[] { CityModuleMaterialRole.Trim, CityModuleMaterialRole.Wall };
                    break;

                case CityModuleId.G:
                    def.displayName = "Janela com Venezianas";
                    def.category = CityModuleCategory.WallShutters;
                    def.width = 2.0f; def.height = 3.0f; def.depth = 0.2f;
                    def.occupiedSlots = 1;
                    def.awningAnchorLocal = new Vector3(0f, 2.7f, 0f);
                    def.materialRoles = new[] { CityModuleMaterialRole.Wall, CityModuleMaterialRole.Trim, CityModuleMaterialRole.Glass, CityModuleMaterialRole.Wood };
                    break;

                case CityModuleId.H:
                    def.displayName = "Janela com Floreira";
                    def.category = CityModuleCategory.WallPlanter;
                    def.width = 2.0f; def.height = 3.0f; def.depth = 0.2f;
                    def.occupiedSlots = 1;
                    def.exteriorProtrusion = 0.30f; // Floreira avança 30cm para fora
                    def.awningAnchorLocal = new Vector3(0f, 2.7f, 0f);
                    def.materialRoles = new[] { CityModuleMaterialRole.Wall, CityModuleMaterialRole.Trim, CityModuleMaterialRole.Glass, CityModuleMaterialRole.Wood };
                    break;

                case CityModuleId.I:
                    def.displayName = "Parede com Garagem";
                    def.category = CityModuleCategory.WallGarage;
                    def.width = 4.0f; def.height = 3.0f; def.depth = 0.2f;
                    def.occupiedSlots = 2; // Ocupa 2 módulos consecutivos (4m)
                    def.isGarage = true;
                    def.approachAnchorLocal = new Vector3(0f, 0f, 2.0f);
                    def.interiorAnchorLocal = new Vector3(0f, 0f, -2.0f);
                    def.materialRoles = new[] { CityModuleMaterialRole.Wall, CityModuleMaterialRole.Trim, CityModuleMaterialRole.Metal };
                    break;

                case CityModuleId.J:
                    def.displayName = "Parede com Porta Dupla";
                    def.category = CityModuleCategory.WallDoubleDoor;
                    def.width = 2.0f; def.height = 3.0f; def.depth = 0.2f;
                    def.occupiedSlots = 1;
                    def.isEntrance = true;
                    def.isDoubleDoor = true;
                    // Vão de 1.6m x 2.2m centralizado
                    def.entranceAnchorLocal = new Vector3(0f, 0f, 0f);
                    def.approachAnchorLocal = new Vector3(0f, 0f, 1.0f);
                    def.interiorAnchorLocal = new Vector3(0f, 0f, -1.0f);
                    def.deliveryAnchorLocal = new Vector3(0f, 0f, 0.4f);
                    def.awningAnchorLocal = new Vector3(0f, 2.6f, 0f);
                    def.movingPartHingeLocal = new Vector3(-0.8f, 1.1f, -0.1f);
                    def.materialRoles = new[] { CityModuleMaterialRole.Wall, CityModuleMaterialRole.Trim, CityModuleMaterialRole.Wood };
                    break;

                case CityModuleId.K:
                    def.displayName = "Toldo Separado";
                    def.category = CityModuleCategory.Awning;
                    def.width = 2.0f; def.height = 0.4f; def.depth = 0.8f;
                    def.occupiedSlots = 1;
                    def.isAwning = true;
                    def.exteriorProtrusion = 0.80f; // Avanço de 80cm para fora
                    def.minClearanceHeight = 2.2f; // Altura livre do pedestre
                    def.materialRoles = new[] { CityModuleMaterialRole.Fabric, CityModuleMaterialRole.Metal };
                    break;

                case CityModuleId.L:
                    def.displayName = "Pilar de Canto";
                    def.category = CityModuleCategory.CornerPillar;
                    def.width = 0.20f; def.height = 3.0f; def.depth = 0.20f;
                    def.occupiedSlots = 0; // Acabamento de canto, não consome espaço no passo de 2m
                    def.isCornerPillar = true;
                    def.materialRoles = new[] { CityModuleMaterialRole.Trim, CityModuleMaterialRole.Wall };
                    break;
            }

            return def;
        }
    }
}

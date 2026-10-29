using System;
using System.Collections.Generic;
using UnityEngine;

namespace ScaryParty.Pizzeria.Authoring
{
    public enum MaterialPaletteRole
    {
        ParedeClara = 0,
        FachadaDestaque = 1,
        Madeira = 2,
        Metal = 3,
        PisoSalao = 4,
        PisoCozinha = 5,
        Escuro = 6,
        DestaqueSecundario = 7,
        Vidro = 8,
        Preserve = 9,
        Custom = 10,

        // Utensílios, mobiliário, ferramentas e cozinha específica
        FornoLenha = 11,
        PortaForno = 12,
        Geladeira = 13,
        Armario = 14,
        Mesa = 15,
        Cadeira = 16,
        Balcao = 17,
        Faca = 18,
        Faqueiro = 19,
        TabuaCorte = 20,
        CortadorPizza = 21,
        Espatula = 22,
        FormaPizza = 23,
        PanelaConcha = 24,
        RecipienteMolho = 25,
        PizzaPronta = 26,
        CaixaPizza = 27,
        DeliveryBag = 28,
        CaixaRegistradora = 29,
        CardapioPrancheta = 30,
        Lixeira = 31
    }

    [System.Serializable]
    public struct ExplicitSlotOverride
    {
        public string nodeGuid;
        public string nodePath;
        public int slotIndex;
        public MaterialPaletteRole role;
        public Material explicitMaterial;
        public Color customColor;
    }

    [System.Serializable]
    public struct SlotMappingRule
    {
        public string pattern;
        public MaterialPaletteRole role;
        public bool useSimpleShader;
        public string note;
    }

    /// <summary>
    /// Perfil de aparência da pizzaria com paleta concisa baseada nos shaders Distant Lands/Illustrate.
    /// Define templates, cores e regras determinísticas de mapeamento por slot.
    /// </summary>
    [CreateAssetMenu(fileName = "PizzeriaAppearanceProfile", menuName = "ScaryParty/Pizzeria/Appearance Profile")]
    public class PizzeriaAppearanceProfile : ScriptableObject
    {
        [Header("Shaders Distant Lands")]
        [Tooltip("Shader padrão principal (opaco com suporte a iluminação toon, triplanar, etc).")]
        public Shader opaqueShader;

        [Tooltip("Shader alternativo leve para objetos sem necessidade de recursos extras.")]
        public Shader simpleShader;

        [Header("Paleta de Cores Concisa — Base Arquitetural")]
        [Tooltip("Paredes principais (#E8DCC8)")]
        public Color paredeClaraColor = HexToColor("#E8DCC8");

        [Tooltip("Fachada, molduras selecionadas e destaque primário (#B84735)")]
        public Color fachadaDestaqueColor = HexToColor("#B84735");

        [Tooltip("Portas, guarnições e peças gerais de madeira (#855A3B)")]
        public Color madeiraColor = HexToColor("#855A3B");

        [Tooltip("Peças metálicas genéricas (#89969B)")]
        public Color metalColor = HexToColor("#89969B");

        [Tooltip("Piso do Salão / Atendimento (#B58C63)")]
        public Color pisoSalaoColor = HexToColor("#B58C63");

        [Tooltip("Piso da Cozinha / Operacional (#C5C4B4)")]
        public Color pisoCozinhaColor = HexToColor("#C5C4B4");

        [Tooltip("Borracha, puxadores, rodapés e detalhes escuros (#30363A)")]
        public Color escuroColor = HexToColor("#30363A");

        [Tooltip("Letreiro e pequenos elementos de destaque (#E2AD42)")]
        public Color destaqueSecundarioColor = HexToColor("#E2AD42");

        [Tooltip("Vidros e janelas (opcional, manter original se especial)")]
        public Color vidroColor = new Color(0.85f, 0.92f, 0.95f, 0.5f);

        [Header("Materiais Específicos — Fornos e Cozinha")]
        [Tooltip("Forno de pizza a lenha — tijolo refratário e terracota (#994828)")]
        public Color fornoLenhaColor = HexToColor("#994828");

        [Tooltip("Porta do forno a lenha — ferro fundido escuro rústico (#25282A)")]
        public Color portaFornoColor = HexToColor("#25282A");

        [Tooltip("Geladeira comercial e portas — esmalte branco gelo (#DCE4E8)")]
        public Color geladeiraColor = HexToColor("#DCE4E8");

        [Tooltip("Armários e prateleiras de despensa/cozinha (#654321)")]
        public Color armarioColor = HexToColor("#654321");

        [Header("Materiais Específicos — Mobiliário")]
        [Tooltip("Mesas de salão e mesas de montagem (#7A4B27)")]
        public Color mesaColor = HexToColor("#7A4B27");

        [Tooltip("Cadeiras de salão (#8C5832)")]
        public Color cadeiraColor = HexToColor("#8C5832");

        [Tooltip("Balcões de atendimento e quinas (#4E3E34)")]
        public Color balcaoColor = HexToColor("#4E3E34");

        [Header("Materiais Específicos — Facas, Utensílios e Ferramentas")]
        [Tooltip("Lâminas de facas — inox polido (#CBD5D8)")]
        public Color facaColor = HexToColor("#CBD5D8");

        [Tooltip("Faqueiro / cepo de suporte de facas em madeira (#54321A)")]
        public Color faqueiroColor = HexToColor("#54321A");

        [Tooltip("Tábuas de corte em madeira tratada (#C29B68)")]
        public Color tabuaCorteColor = HexToColor("#C29B68");

        [Tooltip("Cortador de pizza — carretilha metálica (#98A7AD)")]
        public Color cortadorPizzaColor = HexToColor("#98A7AD");

        [Tooltip("Espátula e pás de pizza (#B59E7E)")]
        public Color espatulaColor = HexToColor("#B59E7E");

        [Tooltip("Formas de pizza, assadeiras e pratos (#A0ABB0)")]
        public Color formaPizzaColor = HexToColor("#A0ABB0");

        [Tooltip("Panelas de molho e conchas (#7E8B91)")]
        public Color panelaConchaColor = HexToColor("#7E8B91");

        [Tooltip("Recipientes e cubas de molho / ingredientes (#A92A22)")]
        public Color recipienteMolhoColor = HexToColor("#A92A22");

        [Tooltip("Pizzas assadas prontas (#D97925)")]
        public Color pizzaProntaColor = HexToColor("#D97925");

        [Header("Materiais Específicos — Embalagens e Operação")]
        [Tooltip("Caixas de pizza em papelão kraft (#C5A069)")]
        public Color caixaPizzaColor = HexToColor("#C5A069");

        [Tooltip("Mochila térmica de entrega Delivery (#BA1C24)")]
        public Color deliveryBagColor = HexToColor("#BA1C24");

        [Tooltip("Caixa registradora e gaveta de dinheiro (#3B5054)")]
        public Color caixaRegistradoraColor = HexToColor("#3B5054");

        [Tooltip("Cardápios e pranchetas de comandas (#8A603E)")]
        public Color cardapioPranchetaColor = HexToColor("#8A603E");

        [Tooltip("Latas e tampas de lixo (#3C4245)")]
        public Color lixeiraColor = HexToColor("#3C4245");

        [Header("Configuração de Shading")]
        [Tooltip("Desativa ondas procedurais em peças arquitetônicas.")]
        public bool disableWaves = true;

        [Tooltip("Desativa variação aleatória de HSV nos blocos principais para manter paleta consistente.")]
        public bool disableHSVVariation = true;

        [Tooltip("Habilita modo de iluminação Toon Lit.")]
        public bool enableToonLighting = true;

        [Tooltip("Habilita sombras no shader toon.")]
        public bool enableShadows = true;

        [Tooltip("Preserva textura original (_Texture/_MainTex) caso o objeto possua.")]
        public bool preserveOriginalTextures = true;

        [Tooltip("Preserva normal maps do material original quando presentes.")]
        public bool preserveNormalMaps = true;

        [Header("Diretório de Saída")]
        public string generatedMaterialsFolder = "Assets/Art/Pizzeria/Materials/Generated";

        [Header("Regras de Mapeamento por Padrão de Nome")]
        public List<SlotMappingRule> defaultRules = new List<SlotMappingRule>();

        [Header("Overrides Explícitos por GUID / Slot")]
        public List<ExplicitSlotOverride> explicitOverrides = new List<ExplicitSlotOverride>();

        private void OnEnable()
        {
            EnsureShaders();
            if (defaultRules == null || defaultRules.Count == 0)
            {
                InitializeDefaultRules();
            }
        }

        public void EnsureShaders()
        {
            if (opaqueShader == null)
                opaqueShader = Shader.Find("Distant Lands/Illustrate/Opaque");
            if (simpleShader == null)
                simpleShader = Shader.Find("Distant Lands/Illustrate/Simple");
        }

        public Color GetRoleColor(MaterialPaletteRole role)
        {
            switch (role)
            {
                case MaterialPaletteRole.ParedeClara: return paredeClaraColor;
                case MaterialPaletteRole.FachadaDestaque: return fachadaDestaqueColor;
                case MaterialPaletteRole.Madeira: return madeiraColor;
                case MaterialPaletteRole.Metal: return metalColor;
                case MaterialPaletteRole.PisoSalao: return pisoSalaoColor;
                case MaterialPaletteRole.PisoCozinha: return pisoCozinhaColor;
                case MaterialPaletteRole.Escuro: return escuroColor;
                case MaterialPaletteRole.DestaqueSecundario: return destaqueSecundarioColor;
                case MaterialPaletteRole.Vidro: return vidroColor;

                case MaterialPaletteRole.FornoLenha: return fornoLenhaColor;
                case MaterialPaletteRole.PortaForno: return portaFornoColor;
                case MaterialPaletteRole.Geladeira: return geladeiraColor;
                case MaterialPaletteRole.Armario: return armarioColor;
                case MaterialPaletteRole.Mesa: return mesaColor;
                case MaterialPaletteRole.Cadeira: return cadeiraColor;
                case MaterialPaletteRole.Balcao: return balcaoColor;
                case MaterialPaletteRole.Faca: return facaColor;
                case MaterialPaletteRole.Faqueiro: return faqueiroColor;
                case MaterialPaletteRole.TabuaCorte: return tabuaCorteColor;
                case MaterialPaletteRole.CortadorPizza: return cortadorPizzaColor;
                case MaterialPaletteRole.Espatula: return espatulaColor;
                case MaterialPaletteRole.FormaPizza: return formaPizzaColor;
                case MaterialPaletteRole.PanelaConcha: return panelaConchaColor;
                case MaterialPaletteRole.RecipienteMolho: return recipienteMolhoColor;
                case MaterialPaletteRole.PizzaPronta: return pizzaProntaColor;
                case MaterialPaletteRole.CaixaPizza: return caixaPizzaColor;
                case MaterialPaletteRole.DeliveryBag: return deliveryBagColor;
                case MaterialPaletteRole.CaixaRegistradora: return caixaRegistradoraColor;
                case MaterialPaletteRole.CardapioPrancheta: return cardapioPranchetaColor;
                case MaterialPaletteRole.Lixeira: return lixeiraColor;

                default: return Color.white;
            }
        }

        public MaterialPaletteRole SuggestRole(string nodeName, string originalMatName, int category = 0)
        {
            string lowerName = (nodeName ?? "").ToLowerInvariant();
            string lowerMat = (originalMatName ?? "").ToLowerInvariant();

            // Preservar textos de comanda e UI
            if (lowerName.Contains("sheettext"))
                return MaterialPaletteRole.Preserve;

            // Pisos específicos
            if (lowerName.Contains("floor_salao") || lowerName.Contains("piso_salao") || lowerName.Contains("piso salao"))
                return MaterialPaletteRole.PisoSalao;
            if (lowerName.Contains("floor_cozinha") || lowerName.Contains("piso_cozinha") || lowerName.Contains("piso cozinha"))
                return MaterialPaletteRole.PisoCozinha;

            // Regras customizadas configuradas
            if (defaultRules != null)
            {
                for (int i = 0; i < defaultRules.Count; i++)
                {
                    string p = (defaultRules[i].pattern ?? "").ToLowerInvariant();
                    if (!string.IsNullOrEmpty(p) && (lowerName.Contains(p) || lowerMat.Contains(p)))
                    {
                        return defaultRules[i].role;
                    }
                }
            }

            // Forno a lenha e porta de ferro fundido
            if (lowerName.Contains("portaforno") || lowerMat.Contains("portaforno"))
                return MaterialPaletteRole.PortaForno;
            if (lowerName.Contains("forno") || lowerMat.Contains("forno"))
                return MaterialPaletteRole.FornoLenha;

            // Geladeiras industriais
            if (lowerName.Contains("geladeira") || lowerName.Contains("portamaiorgeldeira") || lowerName.Contains("portapequenageldeira"))
                return MaterialPaletteRole.Geladeira;

            // Armários e prateleiras
            if (lowerName.Contains("armario") || lowerName.Contains("prateleira"))
                return MaterialPaletteRole.Armario;

            // Facas e faqueiros
            if (lowerName.Contains("suportefaca") || lowerName.Contains("faqueiro"))
                return MaterialPaletteRole.Faqueiro;
            if (lowerName.Contains("faca") || lowerMat.Contains("faca"))
                return MaterialPaletteRole.Faca;

            // Utensílios de corte e preparo
            if (lowerName.Contains("tabua") || lowerName.Contains("cortar"))
                return MaterialPaletteRole.TabuaCorte;
            if (lowerName.Contains("cortador"))
                return MaterialPaletteRole.CortadorPizza;
            if (lowerName.Contains("espatula"))
                return MaterialPaletteRole.Espatula;
            if (lowerName.Contains("prato") || lowerName.Contains("forma"))
                return MaterialPaletteRole.FormaPizza;
            if (lowerName.Contains("panela") || lowerName.Contains("concha"))
                return MaterialPaletteRole.PanelaConcha;
            if (lowerName.Contains("molho") || lowerName.Contains("molhos"))
                return MaterialPaletteRole.RecipienteMolho;
            if (lowerName.Contains("pizzasprontas") || (lowerName.Contains("pizza") && !lowerName.Contains("pizzaria") && !lowerName.Contains("cortador") && !lowerName.Contains("caixa") && !lowerName.Contains("forma")))
                return MaterialPaletteRole.PizzaPronta;

            // Mobiliário
            if (lowerName.Contains("mesa"))
                return MaterialPaletteRole.Mesa;
            if (lowerName.Contains("cadeira") || lowerName.Contains("banco"))
                return MaterialPaletteRole.Cadeira;
            if (lowerName.Contains("balcao") || lowerName.Contains("quinabalcao"))
                return MaterialPaletteRole.Balcao;

            // Embalagens, operação e delivery
            if (lowerName.Contains("caixaregistradora") || lowerName.Contains("gavetacaixa"))
                return MaterialPaletteRole.CaixaRegistradora;
            if (lowerName.Contains("deliverybag") || lowerName.Contains("bag"))
                return MaterialPaletteRole.DeliveryBag;
            if (lowerName.StartsWith("caixa.") || lowerName == "caixa")
                return MaterialPaletteRole.CaixaPizza;
            if (lowerName.Contains("cardapio") || lowerName.Contains("prancheta"))
                return MaterialPaletteRole.CardapioPrancheta;
            if (lowerName.Contains("lixo") || lowerName.Contains("lixeira"))
                return MaterialPaletteRole.Lixeira;

            // Fachada e destaques arquiteturais
            if (lowerName.Contains("fachada") || lowerName.Contains("paredesuperiorfachada") || lowerName.Contains("marquise") || lowerName.Contains("moldura") || lowerName.Contains("abertura") || lowerName.Contains("batentefachada"))
                return MaterialPaletteRole.FachadaDestaque;

            // Detalhes escuros (puxadores, ferragens, borracha, etc)
            if (lowerName.Contains("puxador") || lowerName.Contains("borracha") || lowerName.Contains("rodape") || lowerName.Contains("trilho") || lowerName.Contains("base_escura") || lowerName.Contains("grade"))
                return MaterialPaletteRole.Escuro;

            // Portas e batentes internos
            if (lowerName.Contains("batente") || lowerName.Contains("porta"))
                return MaterialPaletteRole.Madeira;

            // Paredes e estrutura
            if (lowerName.Contains("parede") || lowerName.Contains("wall") || lowerName.Contains("pilar") || lowerName.Contains("pilastra") || lowerName.Contains("coluna"))
                return MaterialPaletteRole.ParedeClara;

            // Letreiros e sinalização
            if (lowerName.Contains("letreiro") || lowerName.Contains("sign") || lowerName.Contains("neon") || lowerName.Contains("logo") || lowerName.Contains("destaque"))
                return MaterialPaletteRole.DestaqueSecundario;

            // Vidro
            if (lowerName.Contains("vidro") || lowerName.Contains("janela") || lowerName.Contains("glass"))
                return MaterialPaletteRole.Vidro;

            // Fallback genérico de metal
            if (lowerName.Contains("inox") || lowerName.Contains("metal") || lowerName.Contains("chapa"))
                return MaterialPaletteRole.Metal;

            return MaterialPaletteRole.ParedeClara;
        }

        public void InitializeDefaultRules()
        {
            defaultRules = new List<SlotMappingRule>
            {
                new SlotMappingRule { pattern = "floor_salao", role = MaterialPaletteRole.PisoSalao, note = "Piso do Salão" },
                new SlotMappingRule { pattern = "floor_cozinha", role = MaterialPaletteRole.PisoCozinha, note = "Piso da Cozinha" },
                new SlotMappingRule { pattern = "portaforno", role = MaterialPaletteRole.PortaForno, note = "Porta Forno a Lenha (Ferro Fundido)" },
                new SlotMappingRule { pattern = "forno", role = MaterialPaletteRole.FornoLenha, note = "Forno a Lenha (Terracota / Tijolo)" },
                new SlotMappingRule { pattern = "geladeira", role = MaterialPaletteRole.Geladeira, note = "Geladeira Comercial (Branco Gelo)" },
                new SlotMappingRule { pattern = "portamaiorgeldeira", role = MaterialPaletteRole.Geladeira, note = "Porta Geladeira" },
                new SlotMappingRule { pattern = "portapequenageldeira", role = MaterialPaletteRole.Geladeira, note = "Porta Geladeira Pequena" },
                new SlotMappingRule { pattern = "armario", role = MaterialPaletteRole.Armario, note = "Armários e Despensa" },
                new SlotMappingRule { pattern = "prateleira", role = MaterialPaletteRole.Armario, note = "Prateleiras" },
                new SlotMappingRule { pattern = "suportefaca", role = MaterialPaletteRole.Faqueiro, note = "Suporte de Facas / Faqueiro" },
                new SlotMappingRule { pattern = "faca", role = MaterialPaletteRole.Faca, note = "Lâminas de Facas" },
                new SlotMappingRule { pattern = "tabua", role = MaterialPaletteRole.TabuaCorte, note = "Tábuas de Corte" },
                new SlotMappingRule { pattern = "cortador", role = MaterialPaletteRole.CortadorPizza, note = "Cortador de Pizza" },
                new SlotMappingRule { pattern = "espatula", role = MaterialPaletteRole.Espatula, note = "Espátula de Pizza" },
                new SlotMappingRule { pattern = "prato/forma", role = MaterialPaletteRole.FormaPizza, note = "Formas de Pizza e Pratos" },
                new SlotMappingRule { pattern = "panela e concha", role = MaterialPaletteRole.PanelaConcha, note = "Panelas e Conchas" },
                new SlotMappingRule { pattern = "molhos", role = MaterialPaletteRole.RecipienteMolho, note = "Cubas de Molho" },
                new SlotMappingRule { pattern = "pizzasprontas", role = MaterialPaletteRole.PizzaPronta, note = "Pizzas Assadas" },
                new SlotMappingRule { pattern = "mesa", role = MaterialPaletteRole.Mesa, note = "Mesas" },
                new SlotMappingRule { pattern = "cadeira", role = MaterialPaletteRole.Cadeira, note = "Cadeiras" },
                new SlotMappingRule { pattern = "balcao", role = MaterialPaletteRole.Balcao, note = "Balcão e Quinas" },
                new SlotMappingRule { pattern = "caixaregistradora", role = MaterialPaletteRole.CaixaRegistradora, note = "Caixa Registradora" },
                new SlotMappingRule { pattern = "gavetacaixa", role = MaterialPaletteRole.CaixaRegistradora, note = "Gaveta do Caixa" },
                new SlotMappingRule { pattern = "deliverybag", role = MaterialPaletteRole.DeliveryBag, note = "Mochila Delivery" },
                new SlotMappingRule { pattern = "cardapio", role = MaterialPaletteRole.CardapioPrancheta, note = "Cardápio" },
                new SlotMappingRule { pattern = "prancheta", role = MaterialPaletteRole.CardapioPrancheta, note = "Prancheta de Comandas" },
                new SlotMappingRule { pattern = "lixo", role = MaterialPaletteRole.Lixeira, note = "Lixeiras e Tampas" },
                new SlotMappingRule { pattern = "paredesuperiorfachada", role = MaterialPaletteRole.FachadaDestaque, note = "Fachada Superior" },
                new SlotMappingRule { pattern = "fachada", role = MaterialPaletteRole.FachadaDestaque, note = "Elementos da Fachada" },
                new SlotMappingRule { pattern = "abertura", role = MaterialPaletteRole.FachadaDestaque, note = "Abertura Fachada" },
                new SlotMappingRule { pattern = "paredecomum", role = MaterialPaletteRole.ParedeClara, note = "Paredes Comuns" },
                new SlotMappingRule { pattern = "parede", role = MaterialPaletteRole.ParedeClara, note = "Paredes Gerais" },
                new SlotMappingRule { pattern = "pilastra", role = MaterialPaletteRole.ParedeClara, note = "Pilastras" },
                new SlotMappingRule { pattern = "batente", role = MaterialPaletteRole.Madeira, note = "Batentes de Portas" },
                new SlotMappingRule { pattern = "puxador", role = MaterialPaletteRole.Escuro, note = "Puxadores e Ferragens" },
                new SlotMappingRule { pattern = "letreiro", role = MaterialPaletteRole.DestaqueSecundario, note = "Letreiro / Placa" },
                new SlotMappingRule { pattern = "vidro", role = MaterialPaletteRole.Vidro, note = "Vidraças" }
            };
        }

        public static Color HexToColor(string hex)
        {
            if (ColorUtility.TryParseHtmlString(hex, out Color c))
                return c;
            return Color.white;
        }
    }
}

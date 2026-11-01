using System;
using UnityEngine;

namespace ScaryParty.UI.Piaria.Core
{
    /// <summary>
    /// Catálogo serializado de assets da interface P.I.A.R.I.A.
    /// Contém referências diretas a Sprites, Texturas e Fontes validadas.
    /// Em runtime, nenhuma busca por string ou AssetDatabase é realizada.
    /// </summary>
    [CreateAssetMenu(fileName = "PiariaUIAssetCatalog", menuName = "Scary Party/UI/Piaria Asset Catalog")]
    public class PiariaUIAssetCatalog : ScriptableObject
    {
        private static PiariaUIAssetCatalog _cachedInstance;
        public static PiariaUIAssetCatalog Instance
        {
            get
            {
                if (_cachedInstance == null)
                {
                    _cachedInstance = Resources.Load<PiariaUIAssetCatalog>("PiariaUIAssetCatalog");
                    if (_cachedInstance == null)
                    {
                        var all = Resources.FindObjectsOfTypeAll<PiariaUIAssetCatalog>();
                        if (all != null && all.Length > 0)
                            _cachedInstance = all[0];
                    }
                }
                return _cachedInstance;
            }
            set => _cachedInstance = value;
        }

        [Header("Menu Principal")]
        [Tooltip("Logotipo P.I.A.R.I.A (Asset 103)")]
        public Sprite logoPiaria;
        [Tooltip("Subtítulo completo da marca (Asset 102)")]
        public Sprite logoSubtitle;
        [Tooltip("Arte de texto Jogar (Asset 95)")]
        public Sprite btnPlayText;
        [Tooltip("Arte de texto Configurações (Asset 94)")]
        public Sprite btnSettingsText;
        [Tooltip("Arte de texto Sair (Asset 93)")]
        public Sprite btnQuitText;
        [Tooltip("Faixa de botão amarela 1 (Asset 97)")]
        public Sprite ribbonButtonBg1;
        [Tooltip("Faixa de botão amarela 2 (Asset 98)")]
        public Sprite ribbonButtonBg2;
        [Tooltip("Faixa de botão amarela 3 (Asset 99)")]
        public Sprite ribbonButtonBg3;
        [Tooltip("Decoração quadriculada vermelha 1 (Asset 100)")]
        public Sprite checkeredDeco1;
        [Tooltip("Decoração quadriculada vermelha 2 (Asset 101)")]
        public Sprite checkeredDeco2;

        [Header("Lobby")]
        [Tooltip("Fundo pêssego texturizado 1920x1080 (fundo lobby.psd)")]
        public Sprite backgroundLobby;
        [Tooltip("Título Lobby (Asset 89)")]
        public Sprite titleLobby;
        [Tooltip("Painel de papel caixinha 3")]
        public Sprite paperPanel1;
        [Tooltip("Painel de papel caixinha 4")]
        public Sprite paperPanel2;
        [Tooltip("Painel de papel caixinha 5")]
        public Sprite paperPanel3;
        [Tooltip("Cabeçalho MEU PERFIL")]
        public Sprite headerMyProfile;
        [Tooltip("Cabeçalho SALAS DISPONÍVEIS")]
        public Sprite headerAvailableRooms;
        [Tooltip("Cabeçalho CRIAR SALA")]
        public Sprite headerCreateRoom;
        [Tooltip("Cabeçalho ENTRAR POR CÓDIGO")]
        public Sprite headerJoinCode;
        [Tooltip("Cabeçalho CONEXÃO LOCAL")]
        public Sprite headerLan;
        [Tooltip("Referência de campo Nome (MEU PERFIL_Nome)")]
        public Sprite fieldMyProfileName;
        [Tooltip("Referência de campo Nome da Sala (CRIAR SALA Nome da sala)")]
        public Sprite fieldCreateRoomName;
        [Tooltip("Referência de campo Código (ENTRAR POR CÓDIGO Código da sala)")]
        public Sprite fieldJoinCode;
        [Tooltip("Referência de campo IP Host (CONEXÃO LOCAL IP do host)")]
        public Sprite fieldLanIp;
        [Tooltip("Moldura 3D Viewer (Caixa Personagem 3D Viewer)")]
        public Sprite frame3DPreview;
        [Tooltip("Seletor de cor: Azul")]
        public Sprite colorBlue;
        [Tooltip("Seletor de cor: Laranja")]
        public Sprite colorOrange;
        [Tooltip("Seletor de cor: Rosa")]
        public Sprite colorPink;
        [Tooltip("Seletor de cor: Verde")]
        public Sprite colorGreen;
        [Tooltip("Seletor de cor: Vermelho")]
        public Sprite colorRed;
        [Tooltip("Template de sala pública")]
        public Sprite templatePublicRoom;
        [Tooltip("Estado vazio da lista de salas (NENHUMA SALA)")]
        public Sprite noRooms;
        [Tooltip("Botão Criar Sala")]
        public Sprite btnCreateRoom;
        [Tooltip("Botão Entrar")]
        public Sprite btnJoin;
        [Tooltip("Botão Entrar 2")]
        public Sprite btnJoin2;
        [Tooltip("Botão Atualizar")]
        public Sprite btnRefresh;
        [Tooltip("Botão Voltar")]
        public Sprite btnBack;

        [Header("Sala de Espera (Ready Scene)")]
        [Tooltip("Título Sala de Espera (Título (2))")]
        public Sprite titleWaitingRoom;
        [Tooltip("Faixa de nome jogador 1 (Caixa para nome (5))")]
        public Sprite playerNameBar1;
        [Tooltip("Faixa de nome jogador 2 (Caixa para nome (6))")]
        public Sprite playerNameBar2;
        [Tooltip("Faixa de nome jogador 3 (Caixa para nome (7))")]
        public Sprite playerNameBar3;
        [Tooltip("Faixa de nome jogador 4 (Caixa para nome (8))")]
        public Sprite playerNameBar4;
        [Tooltip("Painel da sala de espera (Caixinha (2))")]
        public Sprite waitingPaperPanel;
        [Tooltip("Painel alternativo (Asset 61)")]
        public Sprite waitingPaperPanelAlt;
        [Tooltip("Botão Estou Pronto (2)")]
        public Sprite btnReady;
        [Tooltip("Botão Iniciar Jogo (2)")]
        public Sprite btnStartGame;
        [Tooltip("Botão Sair da Sala (2)")]
        public Sprite btnLeaveRoom;
        [Tooltip("Botão Editar Perfil")]
        public Sprite btnEditProfile;

        [Header("HUD e Gameplay")]
        [Tooltip("Ícone de entregas / pizza (Asset 22)")]
        public Sprite iconDeliveries;
        [Tooltip("Moeda de dinheiro (dinheiro moeda)")]
        public Sprite iconMoneyCoin;
        [Tooltip("Nota de dinheiro (dinheiro nota)")]
        public Sprite iconMoneyBill;
        [Tooltip("Moldura da mão esquerda (Asset 38)")]
        public Sprite handFrameLeft;
        [Tooltip("Moldura da mão direita (Asset 39)")]
        public Sprite handFrameRight;
        [Tooltip("Dígito 1 (Asset 35)")]
        public Sprite digit1;
        [Tooltip("Dígito 2 (Asset 34)")]
        public Sprite digit2;
        [Tooltip("Moldura da barra de progresso (Asset 40)")]
        public Sprite progressBarFrame;
        [Tooltip("Preenchimento da barra de progresso (Asset 37)")]
        public Sprite progressBarFill;
        [Tooltip("Base de papel circular do minimapa (Asset 51)")]
        public Sprite minimapPaperBase;
        [Tooltip("Camadas de pizza na mochila 1 a 5")]
        public Sprite[] backpackPizzas = new Sprite[5];
        [Tooltip("Gradientes de pizza na mochila 1 a 5")]
        public Sprite[] backpackGradients = new Sprite[5];

        [Header("Tipografia")]
        [Tooltip("Fonte principal para textos e campos PT-BR (CastleChunk-Regular)")]
        public Font fontCastleChunk;
        [Tooltip("Fonte decorativa para títulos (Lost Tumbler)")]
        public Font fontLostTumbler;
        [Tooltip("Fonte alternativa (Munstar-Regular)")]
        public Font fontMunstar;
        [Tooltip("Fonte estilizada (Deanna)")]
        public Font fontDeanna;

        [Header("Templates UXML (Disponíveis em Standalone Build)")]
        public UnityEngine.UIElements.VisualTreeAsset templateMainMenu;
        public UnityEngine.UIElements.VisualTreeAsset templateLobby;
        public UnityEngine.UIElements.VisualTreeAsset templateReadyRoom;
        public UnityEngine.UIElements.VisualTreeAsset templateMainGameHud;
        public UnityEngine.UIElements.VisualTreeAsset templateSettingsModal;
        public UnityEngine.UIElements.VisualTreeAsset templateGenericModal;
        public UnityEngine.UIElements.VisualTreeAsset templateToastContainer;
        public UnityEngine.UIElements.VisualTreeAsset templateStorageModal;
        public UnityEngine.UIElements.VisualTreeAsset templateStagingModal;
        public UnityEngine.UIElements.VisualTreeAsset templateScoreboardOverlay;
        public UnityEngine.UIElements.VisualTreeAsset templatePlayerReadyCard;

        [Header("Configurações de UI Toolkit")]
        public UnityEngine.UIElements.PanelSettings panelSettings;

        public Sprite GetPlayerNameBar(int slotIndex)
        {
            return (slotIndex % 4) switch
            {
                0 => playerNameBar1 != null ? playerNameBar1 : playerNameBar2,
                1 => playerNameBar2 != null ? playerNameBar2 : playerNameBar1,
                2 => playerNameBar3 != null ? playerNameBar3 : playerNameBar1,
                3 => playerNameBar4 != null ? playerNameBar4 : playerNameBar1,
                _ => playerNameBar1
            };
        }

        public Sprite GetBackpackPizza(int indexOrCount)
        {
            if (backpackPizzas == null || backpackPizzas.Length == 0) return null;
            int idx = Mathf.Clamp(indexOrCount, 0, backpackPizzas.Length - 1);
            return backpackPizzas[idx];
        }

        public Sprite GetBackpackPizzaSprite(int count)
        {
            if (backpackPizzas == null || backpackPizzas.Length == 0) return null;
            int idx = Mathf.Clamp(count - 1, 0, backpackPizzas.Length - 1);
            return backpackPizzas[idx];
        }

        public Sprite GetBackpackGradientSprite(int count)
        {
            if (backpackGradients == null || backpackGradients.Length == 0) return null;
            int idx = Mathf.Clamp(count - 1, 0, backpackGradients.Length - 1);
            return backpackGradients[idx];
        }
    }
}

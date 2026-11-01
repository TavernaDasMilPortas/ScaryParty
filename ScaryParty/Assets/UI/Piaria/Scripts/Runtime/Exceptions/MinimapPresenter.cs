using System;
using UnityEngine;
using UnityEngine.UIElements;
using ScaryParty.UI.Piaria.Core;
using ScaryParty.UI.Piaria.Presenters;

namespace ScaryParty.UI.Piaria.Exceptions
{
    /// <summary>
    /// Gerenciador da integração do Minimapa (exceção autorizada em uGUI / RenderTexture).
    /// Conecta a RenderTexture do WorldImage do Kamgam ao visual de papel do HUD e controla
    /// alternância entre modo HUD circular e modo ampliado (fullscreen) na tecla [M].
    /// </summary>
    public class MinimapPresenter : MonoBehaviour
    {
        public static MinimapPresenter Instance { get; private set; }

        [Header("Referências")]
        public Kamgam.UGUIWorldImage.WorldImage worldImage;
        public MinimapRouteManager routeManager;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(this);
                return;
            }

            if (worldImage == null)
                worldImage = FindFirstObjectByType<Kamgam.UGUIWorldImage.WorldImage>();
            if (routeManager == null)
                routeManager = FindFirstObjectByType<MinimapRouteManager>();
        }

        private void Update()
        {
            if (routeManager == null)
            {
                routeManager = MinimapRouteManager.Instance;
            }
        }

        public void ToggleFullscreen(bool isFullscreen)
        {
            if (HudPresenter.Instance != null)
            {
                HudPresenter.Instance.ToggleMinimapFullscreen(isFullscreen);
            }
        }
    }
}

using System;
using UnityEngine;
using UnityEngine.UIElements;
using ScaryParty.UI.Piaria.Presenters;

namespace ScaryParty.UI.Piaria.Core
{
    /// <summary>
    /// Controlador da Cena de Demonstração Isolada (PiariaUIDemoScene).
    /// Permite testar, navegar e validar visualmente todas as telas, animações,
    /// modais e feedbacks do pacote P.I.A.R.I.A sem depender de rede, servidor ou gameplay ativo.
    /// </summary>
    public class PiariaDemoSceneController : MonoBehaviour
    {
        [Header("UIDocuments da Demonstração")]
        public UIDocument frontendDoc;
        public UIDocument hudDoc;
        public UIDocument modalDoc;

        private int _simulatedMoney = 100;
        private int _simulatedDeliveries = 2;
        private int _simulatedPizzas = 1;
        private float _simulatedProgress = 0f;
        private bool _isProgressRunning = false;

        private void Start()
        {
            // Inicializa cursor livre para teste
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;

            // Inicia apresentando o Menu Principal
            if (UIScreenRouter.Instance != null)
            {
                UIScreenRouter.Instance.NavigateTo("MainMenuScreen");
            }

            SetupDemoHUD();
        }

        private void SetupDemoHUD()
        {
            if (HudPresenter.Instance != null)
            {
                HudPresenter.Instance.SetMoney(_simulatedMoney, immediate: true);
                HudPresenter.Instance.SetDeliveries(_simulatedDeliveries);
                HudPresenter.Instance.SetBackpackCount(_simulatedPizzas);
                HudPresenter.Instance.SetHandState(false, true, "Massa de Pizza");
                HudPresenter.Instance.SetHandState(true, false, "Molho de Tomate");
                HudPresenter.Instance.ShowPrompt("[E] Abrir Forno de Pizza");
            }
        }

        private void Update()
        {
            if (UnityEngine.Cursor.lockState != CursorLockMode.None) UnityEngine.Cursor.lockState = CursorLockMode.None;
            if (!UnityEngine.Cursor.visible) UnityEngine.Cursor.visible = true;
            // Controles de atalho na cena de demonstração
            if (Input.GetKeyDown(KeyCode.F1))
            {
                UIScreenRouter.Instance?.NavigateTo("MainMenuScreen");
            }
            else if (Input.GetKeyDown(KeyCode.F2))
            {
                UIScreenRouter.Instance?.NavigateTo("LobbyScreen");
            }
            else if (Input.GetKeyDown(KeyCode.F3))
            {
                SettingsPresenter.Instance?.OpenModal();
            }
            else if (Input.GetKeyDown(KeyCode.F4))
            {
                StoragePresenter.Instance?.Open(1);
            }
            else if (Input.GetKeyDown(KeyCode.F5))
            {
                StagingPresenter.Instance?.Open(1001, 0);
            }
            else if (Input.GetKeyDown(KeyCode.F6))
            {
                SimulateToast();
            }
            else if (Input.GetKeyDown(KeyCode.F7))
            {
                SimulateMoneyAdd();
            }
            else if (Input.GetKeyDown(KeyCode.F8))
            {
                ToggleProgressDemo();
            }

            if (_isProgressRunning && HudPresenter.Instance != null)
            {
                _simulatedProgress += Time.deltaTime * 0.4f;
                if (_simulatedProgress >= 1f)
                {
                    _simulatedProgress = 0f;
                    _isProgressRunning = false;
                    HudPresenter.Instance.HideProgress();
                    Pizzeria.Presentation.NotificationManager.Show("Pizza assada com sucesso!", Pizzeria.Presentation.NotificationType.Success);
                }
                else
                {
                    HudPresenter.Instance.ShowProgress(_simulatedProgress, $"Assando Pizza ({Mathf.RoundToInt(_simulatedProgress * 100)}%)");
                }
            }
        }

        public void SimulateToast()
        {
            int r = UnityEngine.Random.Range(0, 4);
            switch (r)
            {
                case 0:
                    Pizzeria.Presentation.NotificationManager.Show("Entrega concluída! Gorjeta: R$15", Pizzeria.Presentation.NotificationType.Success);
                    break;
                case 1:
                    Pizzeria.Presentation.NotificationManager.Show("Aviso: Pizza quase queimando no forno!", Pizzeria.Presentation.NotificationType.Warning);
                    break;
                case 2:
                    Pizzeria.Presentation.NotificationManager.Show("Erro: Mão direita já ocupada!", Pizzeria.Presentation.NotificationType.Error);
                    break;
                default:
                    Pizzeria.Presentation.NotificationManager.Show("Nova comanda recebida no telefone!", Pizzeria.Presentation.NotificationType.Info);
                    break;
            }
        }

        public void SimulateMoneyAdd()
        {
            _simulatedMoney += 25;
            _simulatedDeliveries++;
            HudPresenter.Instance?.SetMoney(_simulatedMoney);
            HudPresenter.Instance?.SetDeliveries(_simulatedDeliveries);
        }

        public void ToggleProgressDemo()
        {
            _isProgressRunning = !_isProgressRunning;
            _simulatedProgress = 0f;
            if (!_isProgressRunning)
            {
                HudPresenter.Instance?.HideProgress();
            }
        }
    }
}

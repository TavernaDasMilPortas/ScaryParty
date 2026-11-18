using System;
using UnityEngine;

namespace ScaryParty.Lighting
{
    /// <summary>
    /// Utilitario de teste em runtime (Play Mode) para mudar o horario da partida em tempo real,
    /// pausar o ciclo e alternar instantaneamente entre as 4 ancoras (17h Tarde, 19h Crepusculo,
    /// 22h Noite e 02h Madrugada).
    /// Pressione F6 ou clique no botao [🕒 Hora] na tela para abrir/fechar.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(270)]
    public sealed class TimeOfDayTestOverlay : MonoBehaviour
    {
        [Header("Configuracao")]
        public KeyCode toggleKey = KeyCode.F6;
        public bool isExpanded = false;
        public bool showOnScreenTrigger = true;

        private Rect _windowRect = new Rect(20, 20, 320, 255);
        private GUIStyle _boxStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _labelBoldStyle;
        private GUIStyle _btnAnchorStyle;
        private GUIStyle _pillStyle;
        private bool _stylesInitialized;

        private void Awake()
        {
            // Posiciona a janela proximo ao topo esquerdo, logo abaixo de informacoes basicas
            _windowRect = new Rect(20, 40, 320, 260);
        }

        private void Update()
        {
            // B5.1: Overlay [F6] no GameView desativado permanentemente; controles de horario vivem no PlaytestDebugController (Inspector Odin).
            isExpanded = false;
        }

        private void OnGUI()
        {
            // Overlay removido na B5.1
        }

        private void DrawWindowContent(int windowId)
        {
            var director = TimeOfDayLightingDirector.Active;
            if (director == null)
            {
                GUILayout.Label("TimeOfDayLightingDirector nao encontrado na cena.");
                return;
            }

            float currentHour = director.currentClockHours;
            string phaseName = GetPhaseName(currentHour);
            string timeStr = FormatClock(currentHour);

            GUILayout.BeginVertical();

            // Display do horario atual
            GUILayout.BeginHorizontal();
            GUILayout.Label("Horario:", GUILayout.Width(60));
            GUILayout.Label($"{timeStr} ({phaseName})", _headerStyle);
            if (GUILayout.Button("X", GUILayout.Width(26), GUILayout.Height(20)))
            {
                isExpanded = false;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            // Slider continuo de 0 a 24h
            GUILayout.Label("Scrubber continuo (0h - 24h):", _labelBoldStyle);
            float newHour = GUILayout.HorizontalSlider(currentHour, 0f, 24f);
            if (Mathf.Abs(newHour - currentHour) > 0.02f)
            {
                director.SetClockHours(newHour, evaluateNow: true);
            }

            GUILayout.Space(8);

            // Botoes rapidos das 4 ancoras da direcao de arte
            GUILayout.Label("Ancoras Diretas:", _labelBoldStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("17h\nTarde", _btnAnchorStyle))
            {
                director.JumpToPresetAnchor(17f);
            }
            if (GUILayout.Button("19h\nCrepusculo", _btnAnchorStyle))
            {
                director.JumpToPresetAnchor(19f);
            }
            if (GUILayout.Button("22h\nNoite", _btnAnchorStyle))
            {
                director.JumpToPresetAnchor(22f);
            }
            if (GUILayout.Button("02h\nMadrugada", _btnAnchorStyle))
            {
                director.JumpToPresetAnchor(2f);
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(8);

            // Controle de avanco e velocidade
            GUILayout.BeginHorizontal();
            string pauseLabel = director.autoAdvance ? "⏸ Congelar" : "▶ Avancar";
            if (GUILayout.Button(pauseLabel, GUILayout.Height(24)))
            {
                director.autoAdvance = !director.autoAdvance;
            }

            if (GUILayout.Button("0.5x", GUILayout.Width(42), GUILayout.Height(24)))
            {
                director.gameHoursPerRealMinute = 0.375f;
            }
            if (GUILayout.Button("1x", GUILayout.Width(42), GUILayout.Height(24)))
            {
                director.gameHoursPerRealMinute = 0.75f;
            }
            if (GUILayout.Button("5x", GUILayout.Width(42), GUILayout.Height(24)))
            {
                director.gameHoursPerRealMinute = 3.75f;
            }
            if (GUILayout.Button("20x", GUILayout.Width(42), GUILayout.Height(24)))
            {
                director.gameHoursPerRealMinute = 15f;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.Label("Dica: Pressione F6 para ocultar ou exibir.", GUI.skin.label);

            GUILayout.EndVertical();

            // Permite arrastar a janela livremente
            GUI.DragWindow(new Rect(0, 0, 10000, 25));
        }

        private static string GetPhaseName(float hours)
        {
            hours = ((hours % 24f) + 24f) % 24f;
            if (hours >= 16f && hours < 18.5f) return "Tarde";
            if (hours >= 18.5f && hours < 21f) return "Crepusculo";
            if (hours >= 21f || hours < 4.5f) return "Noite";
            return "Dia / Alvorada";
        }

        private static string FormatClock(float hours)
        {
            hours = ((hours % 24f) + 24f) % 24f;
            int h = (int)hours;
            int m = (int)((hours - h) * 60f);
            return $"{h:00}:{m:00}";
        }

        private void InitStyles()
        {
            if (_stylesInitialized) return;

            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.92f, 0.65f, 1f) }
            };

            _labelBoldStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            _btnAnchorStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                fixedHeight = 36
            };

            _pillStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.95f, 0.75f, 1f) }
            };

            _stylesInitialized = true;
        }
    }
}

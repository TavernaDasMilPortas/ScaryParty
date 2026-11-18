using System;
using System.Text;
using Sirenix.OdinInspector;
using ScaryParty.Lighting;
using ScaryParty.Pizzeria.Composition;
using ScaryParty.Pizzeria.Domain.Services;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Network;
using Unity.Netcode;
using UnityEngine;
using FixedString32Bytes = Unity.Collections.FixedString32Bytes;
using FixedString64Bytes = Unity.Collections.FixedString64Bytes;

namespace ScaryParty.Pizzeria.Presentation
{
    /// <summary>
    /// Central unica de Playtest e Controle de Horario no Inspector com Odin (B5.1 Revisada).
    /// Substitui integralmente os overlays [F8] e [F6] do GameView.
    /// Opera apenas para o Host dentro do Unity Editor via RPCs autoritativas no servidor.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlaytestDebugController : MonoBehaviour
    {
        public static PlaytestDebugController Instance { get; private set; }

        [ShowInInspector, ReadOnly, PropertyOrder(-10)]
        [InfoBox("Mutacoes permitidas somente para o Host durante PlayMode no Unity Editor. Clientes e builds finais operam apenas em modo leitura.")]
        private string EditorHostStatus
        {
            get
            {
                if (!Application.isEditor) return "BLOQUEADO (Build final)";
                if (!Application.isPlaying) return "EditMode — Entre em PlayMode para acionar controles.";
                if (!CanMutateAsHostEditor) return "SOMENTE LEITURA (Cliente ou sem servidor ativo)";
                return "HOST EDITOR ATIVO — Controles autoritativos liberados em qualquer preset";
            }
        }

        private bool CanMutateAsHostEditor
        {
            get
            {
#if UNITY_EDITOR
                if (!Application.isPlaying) return false;
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                    return NetworkManager.Singleton.IsServer;
                return PizzeriaRoot.Instance != null && PizzeriaRoot.Instance.IsServer;
#else
                return false;
#endif
            }
        }

        // =========================================================================
        // ABA 1: NOITE E FLUXO
        // =========================================================================
        [TabGroup("Central", "Noite")]
        [LabelText("Noite Alvo (1..7)"), MinValue(1), MaxValue(7)]
        public int targetNightNumber = 1;

        [TabGroup("Central", "Noite")]
        [LabelText("Preservar Caixa Atual ao Restaurar Checkpoint")]
        public bool preserveCurrentBudgetOnRestore = false;

        [TabGroup("Central", "Noite")]
        [EnableIf(nameof(CanMutateAsHostEditor))]
        [HorizontalGroup("Central/Noite/PrepBtns")]
        [Button("Ir p/ Preparacao da Noite Alvo")]
        private void GoToPreparationForTargetNight()
        {
            PizzeriaCommandHandler.Instance?.DebugOpenPreparationForNightServerRpc(targetNightNumber, preserveCurrentBudgetOnRestore);
        }

        [TabGroup("Central", "Noite")]
        [EnableIf(nameof(CanMutateAsHostEditor))]
        [HorizontalGroup("Central/Noite/PrepBtns")]
        [Button("Voltar p/ Pre-Noite 1 (Prep)")]
        private void ReturnToPreNight1Preparation()
        {
            PizzeriaCommandHandler.Instance?.DebugRestoreCheckpointServerRpc((byte)0, preserveCurrentBudgetOnRestore);
        }

        [TabGroup("Central", "Noite")]
        [EnableIf(nameof(CanMutateAsHostEditor))]
        [HorizontalGroup("Central/Noite/RestartBtns")]
        [Button("Reiniciar Noite Atual (Checkpoint)")]
        private void RestartCurrentNightFromCheckpoint()
        {
            PizzeriaCommandHandler.Instance?.DebugRestoreCheckpointServerRpc((byte)1, preserveCurrentBudgetOnRestore);
        }

        [TabGroup("Central", "Noite")]
        [EnableIf(nameof(CanMutateAsHostEditor))]
        [HorizontalGroup("Central/Noite/RestartBtns")]
        [Button("Reiniciar RUN Completa")]
        private void RestartWholeRun()
        {
            string activeId = PizzeriaNetworkState.Instance != null
                ? PizzeriaNetworkState.Instance.RunState.Value.ActivePresetId.ToString()
                : string.Empty;
            PizzeriaCommandHandler.Instance?.DebugStartNewRunServerRpc(new FixedString32Bytes(activeId));
        }

        [TabGroup("Central", "Noite")]
        [EnableIf(nameof(CanMutateAsHostEditor))]
        [HorizontalGroup("Central/Noite/OutcomeBtns")]
        [Button("Forcar Vitoria da Noite"), GUIColor(0.35f, 0.85f, 0.45f)]
        private void ForceNightVictory()
        {
            PizzeriaCommandHandler.Instance?.DebugForceNightOutcomeServerRpc(true);
        }

        [TabGroup("Central", "Noite")]
        [EnableIf(nameof(CanMutateAsHostEditor))]
        [HorizontalGroup("Central/Noite/OutcomeBtns")]
        [Button("Forcar Derrota Operacional"), GUIColor(0.92f, 0.35f, 0.35f)]
        private void ForceNightDefeat()
        {
            PizzeriaCommandHandler.Instance?.DebugForceNightOutcomeServerRpc(false);
        }

        // =========================================================================
        // ABA 2: TEMPO — EXPEDIENTE (SEPARADO DA ILUMINACAO)
        // =========================================================================
        [TabGroup("Central", "Expediente")]
        [ShowInInspector, ReadOnly, LabelText("Status do Relogio do Turno")]
        private string ShiftClockTelemetry
        {
            get
            {
                if (PizzeriaNetworkState.Instance == null) return "-";
                var n = PizzeriaNetworkState.Instance.NightState.Value;
                string pauseTxt = n.IsShiftClockPaused ? "PAUSADO" : $"ATIVO ({n.ShiftClockSpeedMultiplier:0.##}x)";
                string limitTxt = n.UnlimitedShiftTime
                    ? $"SEM LIMITE (decorrido {n.ElapsedSeconds:0.#}s)"
                    : $"restam {n.RemainingSeconds:0.#}s (decorrido {n.ElapsedSeconds:0.#}s)";
                return $"{pauseTxt} | {limitTxt}";
            }
        }

        [TabGroup("Central", "Expediente")]
        [LabelText("Segundos Restantes Desejados"), MinValue(1f)]
        public float desiredRemainingSeconds = 120f;

        [TabGroup("Central", "Expediente")]
        [LabelText("Velocidade do Expediente"), Range(0.1f, 10f)]
        public float shiftSpeedMultiplier = 1f;

        [TabGroup("Central", "Expediente")]
        [EnableIf(nameof(CanMutateAsHostEditor))]
        [HorizontalGroup("Central/Expediente/PauseRow")]
        [Button("Pausar / Retomar Relogio")]
        private void TogglePauseShiftClock()
        {
            bool curPaused = PizzeriaNetworkState.Instance != null && PizzeriaNetworkState.Instance.NightState.Value.IsShiftClockPaused;
            PizzeriaCommandHandler.Instance?.DebugSetShiftClockControlServerRpc(!curPaused, shiftSpeedMultiplier, -1f, false);
        }

        [TabGroup("Central", "Expediente")]
        [EnableIf(nameof(CanMutateAsHostEditor))]
        [HorizontalGroup("Central/Expediente/PauseRow")]
        [Button("Alternar Sem Limite de Tempo")]
        private void ToggleUnlimitedShiftTime()
        {
            PizzeriaCommandHandler.Instance?.DebugToggleUnlimitedShiftTimeServerRpc();
        }

        [TabGroup("Central", "Expediente")]
        [EnableIf(nameof(CanMutateAsHostEditor))]
        [HorizontalGroup("Central/Expediente/SpeedRow")]
        [Button("Aplicar Velocidade")]
        private void ApplyShiftSpeed()
        {
            bool curPaused = PizzeriaNetworkState.Instance != null && PizzeriaNetworkState.Instance.NightState.Value.IsShiftClockPaused;
            PizzeriaCommandHandler.Instance?.DebugSetShiftClockControlServerRpc(curPaused, shiftSpeedMultiplier, -1f, false);
        }

        [TabGroup("Central", "Expediente")]
        [EnableIf(nameof(CanMutateAsHostEditor))]
        [HorizontalGroup("Central/Expediente/SpeedRow")]
        [Button("Aplicar Segundos Restantes")]
        private void ApplyExactRemainingSeconds()
        {
            bool curPaused = PizzeriaNetworkState.Instance != null && PizzeriaNetworkState.Instance.NightState.Value.IsShiftClockPaused;
            PizzeriaCommandHandler.Instance?.DebugSetShiftClockControlServerRpc(curPaused, shiftSpeedMultiplier, desiredRemainingSeconds, true);
        }

        [TabGroup("Central", "Expediente")]
        [EnableIf(nameof(CanMutateAsHostEditor))]
        [HorizontalGroup("Central/Expediente/JumpRow")]
        [Button("+30s Restantes")]
        private void Add30s() => AdjustRemaining(30f);

        [TabGroup("Central", "Expediente")]
        [EnableIf(nameof(CanMutateAsHostEditor))]
        [HorizontalGroup("Central/Expediente/JumpRow")]
        [Button("-30s Restantes")]
        private void Sub30s() => AdjustRemaining(-30f);

        [TabGroup("Central", "Expediente")]
        [EnableIf(nameof(CanMutateAsHostEditor))]
        [HorizontalGroup("Central/Expediente/JumpRow")]
        [Button("Ultimos 5s (Avaliacao Natural)")]
        private void JumpToLast5s()
        {
            PizzeriaCommandHandler.Instance?.DebugAdvanceClockServerRpc(0f, true);
        }

        private void AdjustRemaining(float deltaSeconds)
        {
            if (deltaSeconds < 0f)
            {
                PizzeriaCommandHandler.Instance?.DebugAdvanceClockServerRpc(-deltaSeconds, false);
            }
            else if (PizzeriaNetworkState.Instance != null)
            {
                float cur = PizzeriaNetworkState.Instance.NightState.Value.RemainingSeconds;
                bool curPaused = PizzeriaNetworkState.Instance.NightState.Value.IsShiftClockPaused;
                PizzeriaCommandHandler.Instance?.DebugSetShiftClockControlServerRpc(curPaused, shiftSpeedMultiplier, cur + deltaSeconds, true);
            }
        }

        // =========================================================================
        // ABA 3: TEMPO — MUNDO / ILUMINACAO (SEPARADO DO EXPEDIENTE)
        // =========================================================================
        [TabGroup("Central", "Mundo e Iluminacao")]
        [InfoBox("Controles visuais do TimeOfDayLightingDirector. Alterar a iluminacao NAO altera o relogio de metas nem o encerramento do turno.")]
        [ShowInInspector, ReadOnly, LabelText("Hora Visual Atual")]
        private string WorldClockTelemetry
        {
            get
            {
                var d = TimeOfDayLightingDirector.Active;
                if (d == null) return "TimeOfDayLightingDirector inativo nesta cena.";
                int h = Mathf.FloorToInt(d.currentClockHours) % 24;
                int m = Mathf.FloorToInt((d.currentClockHours - Mathf.Floor(d.currentClockHours)) * 60f);
                return $"{h:00}:{m:00} | AutoAdvance={d.autoAdvance} | LockForTests={d.lockTimeForTests}";
            }
        }

        [TabGroup("Central", "Mundo e Iluminacao")]
        [LabelText("Hora Alvo (0..24h)"), Range(0f, 24f)]
        public float desiredWorldClockHours = 22f;

        [TabGroup("Central", "Mundo e Iluminacao")]
        [LabelText("Velocidade Visual (h/min real)"), MinValue(0f)]
        public float worldHoursPerMinute = 0.75f;

        [TabGroup("Central", "Mundo e Iluminacao")]
        [LabelText("Pausar Ciclo Visual de Iluminacao")]
        public bool pauseWorldLightingCycle = false;

        [TabGroup("Central", "Mundo e Iluminacao")]
        [LabelText("Sincronizacao Opcional c/ Expediente (OFF por padrao)")]
        public bool optionalSyncLightingWithShift = false;

        [TabGroup("Central", "Mundo e Iluminacao")]
        [EnableIf(nameof(CanMutateAsHostEditor))]
        [HorizontalGroup("Central/Mundo e Iluminacao/Apply")]
        [Button("Aplicar Horario e Ciclo Visual")]
        private void ApplyWorldTime()
        {
            var d = TimeOfDayLightingDirector.Active;
            if (d == null) return;
            d.gameHoursPerRealMinute = worldHoursPerMinute;
            d.autoAdvance = !pauseWorldLightingCycle;
            d.lockTimeForTests = pauseWorldLightingCycle;
            d.SetClockHours(desiredWorldClockHours, evaluateNow: true);
        }

        [TabGroup("Central", "Mundo e Iluminacao")]
        [EnableIf(nameof(CanMutateAsHostEditor))]
        [HorizontalGroup("Central/Mundo e Iluminacao/Presets")]
        [Button("Tarde (17h)")]
        private void SetAfternoon() { desiredWorldClockHours = 17f; ApplyWorldTime(); }

        [TabGroup("Central", "Mundo e Iluminacao")]
        [EnableIf(nameof(CanMutateAsHostEditor))]
        [HorizontalGroup("Central/Mundo e Iluminacao/Presets")]
        [Button("Crepusculo (19h)")]
        private void SetDusk() { desiredWorldClockHours = 19f; ApplyWorldTime(); }

        [TabGroup("Central", "Mundo e Iluminacao")]
        [EnableIf(nameof(CanMutateAsHostEditor))]
        [HorizontalGroup("Central/Mundo e Iluminacao/Presets")]
        [Button("Noite (22h)")]
        private void SetNight() { desiredWorldClockHours = 22f; ApplyWorldTime(); }

        [TabGroup("Central", "Mundo e Iluminacao")]
        [EnableIf(nameof(CanMutateAsHostEditor))]
        [HorizontalGroup("Central/Mundo e Iluminacao/Presets")]
        [Button("Madrugada (02h)")]
        private void SetLateNight() { desiredWorldClockHours = 2f; ApplyWorldTime(); }

        // =========================================================================
        // ABA 4: ECONOMIA
        // =========================================================================
        [TabGroup("Central", "Economia")]
        [LabelText("Caixa Coletivo Exato (R$)"), MinValue(0)]
        public int desiredBudget = 500;

        [TabGroup("Central", "Economia")]
        [EnableIf(nameof(CanMutateAsHostEditor))]
        [HorizontalGroup("Central/Economia/BudgetRow")]
        [Button("Definir Caixa Exato")]
        private void ApplyExactBudget() => PizzeriaCommandHandler.Instance?.DebugSetExactBudgetServerRpc(desiredBudget);

        [TabGroup("Central", "Economia")]
        [EnableIf(nameof(CanMutateAsHostEditor))]
        [HorizontalGroup("Central/Economia/BudgetRow")]
        [Button("+R$ 100")]
        private void Add100Budget() => PizzeriaCommandHandler.Instance?.DebugAddResourcesServerRpc(100, 0);

        [TabGroup("Central", "Economia")]
        [EnableIf(nameof(CanMutateAsHostEditor))]
        [HorizontalGroup("Central/Economia/BudgetRow")]
        [Button("-R$ 100")]
        private void Sub100Budget() => PizzeriaCommandHandler.Instance?.DebugAddResourcesServerRpc(-100, 0);

        [TabGroup("Central", "Economia")]
        [EnableIf(nameof(CanMutateAsHostEditor))]
        [Button("Reestocar +10 em Todos os Slots")]
        private void RestockAllStorage() => PizzeriaCommandHandler.Instance?.DebugAddResourcesServerRpc(0, 10);

        // =========================================================================
        // ABA 5: JOGADORES E EQUIPE
        // =========================================================================
        [TabGroup("Central", "Jogadores")]
        [LabelText("Dano Controlado (HP)"), MinValue(1), MaxValue(100)]
        public int controlledDamage = 25;

        [TabGroup("Central", "Jogadores")]
        [EnableIf(nameof(CanMutateAsHostEditor))]
        [HorizontalGroup("Central/Jogadores/PlayerRow")]
        [Button("Aplicar Dano Controlado")]
        private void ApplyControlledDamage() => PizzeriaCommandHandler.Instance?.DebugApplyControlledDamageServerRpc(controlledDamage);

        [TabGroup("Central", "Jogadores")]
        [EnableIf(nameof(CanMutateAsHostEditor))]
        [HorizontalGroup("Central/Jogadores/PlayerRow")]
        [Button("Curar e Reviver Todos")]
        private void HealAndReviveAllPlayers() => PizzeriaCommandHandler.Instance?.DebugHealAllPlayersServerRpc();

        [TabGroup("Central", "Jogadores")]
        [EnableIf(nameof(CanMutateAsHostEditor))]
        [HorizontalGroup("Central/Jogadores/EmpRow")]
        [Button("Contratar Balconista (Debug)")]
        private void HireCashierDebug() => PizzeriaCommandHandler.Instance?.DebugHireEmployeeServerRpc((byte)EmployeeRole.Cashier);

        [TabGroup("Central", "Jogadores")]
        [EnableIf(nameof(CanMutateAsHostEditor))]
        [HorizontalGroup("Central/Jogadores/EmpRow")]
        [Button("Contratar Pizzaiolo (Debug)")]
        private void HirePizzaioloDebug() => PizzeriaCommandHandler.Instance?.DebugHireEmployeeServerRpc((byte)EmployeeRole.Pizzaiolo);

        // =========================================================================
        // ABA 6: CENARIOS E STRESS
        // =========================================================================
        [TabGroup("Central", "Cenarios")]
        [EnableIf(nameof(CanMutateAsHostEditor))]
        [HorizontalGroup("Central/Cenarios/Orders")]
        [Button("Spawn Cliente Presencial")]
        private void SpawnWalkInCustomer() => PizzeriaCommandHandler.Instance?.DebugSpawnEntityServerRpc(0, true);

        [TabGroup("Central", "Cenarios")]
        [EnableIf(nameof(CanMutateAsHostEditor))]
        [HorizontalGroup("Central/Cenarios/Orders")]
        [Button("Pedido Multi-Receita")]
        private void SpawnMultiRecipeOrder() => PizzeriaCommandHandler.Instance?.DebugCreateMultiRecipeOrderServerRpc();

        [TabGroup("Central", "Cenarios")]
        [EnableIf(nameof(CanMutateAsHostEditor))]
        [HorizontalGroup("Central/Cenarios/Orders")]
        [Button("Pedido Qtd x3")]
        private void SpawnQuantityThreeOrder() => PizzeriaCommandHandler.Instance?.DebugCreateQuantityThreeOrderServerRpc();

        [TabGroup("Central", "Cenarios")]
        [EnableIf(nameof(CanMutateAsHostEditor))]
        [HorizontalGroup("Central/Cenarios/Monsters")]
        [Button("Spawnar Vampiro")]
        private void SpawnVampire() => PizzeriaCommandHandler.Instance?.DebugSpawnEntityServerRpc((byte)NpcSpecies.Vampire, false);

        // =========================================================================
        // ABA 7: BALANCEAMENTO E MODIFICADORES
        // =========================================================================
        [TabGroup("Central", "Balanceamento")]
        [LabelText("ID do Modificador no Catalogo")]
        public string debugModifierId = "blood_moon";

        [TabGroup("Central", "Balanceamento")]
        [EnableIf(nameof(CanMutateAsHostEditor))]
        [HorizontalGroup("Central/Balanceamento/ModRow")]
        [Button("Aplicar Stack do Modificador")]
        private void ApplyDebugModifier()
        {
            PizzeriaCommandHandler.Instance?.DebugApplyModifierServerRpc(new FixedString64Bytes(debugModifierId ?? string.Empty));
        }

        [TabGroup("Central", "Balanceamento")]
        [EnableIf(nameof(CanMutateAsHostEditor))]
        [HorizontalGroup("Central/Balanceamento/ModRow")]
        [Button("Remover Modificador")]
        private void RemoveDebugModifier()
        {
            PizzeriaCommandHandler.Instance?.DebugRemoveModifierServerRpc(new FixedString64Bytes(debugModifierId ?? string.Empty));
        }

        // =========================================================================
        // ABA 8: DIAGNOSTICO E CHECKPOINTS
        // =========================================================================
        [TabGroup("Central", "Diagnostico")]
        [ShowInInspector, ReadOnly, TextArea(8, 16), LabelText("Telemetria Completa")]
        private string DiagnosticSnapshot
        {
            get
            {
                var sb = new StringBuilder();
                var root = PizzeriaRoot.Instance;
                var ns = PizzeriaNetworkState.Instance;
                if (ns != null)
                {
                    var n = ns.NightState.Value;
                    var r = ns.RunState.Value;
                    sb.AppendLine($"Preset Ativo: {r.ActivePresetId} | Dificuldade: {(root?.RunProgressionService?.Difficulty.ToString() ?? "-")}");
                    sb.AppendLine($"Noite Atual: {n.NightNumber} | Noite Alvo: {n.TargetNightNumber} | Fase: {(NightPhase)n.Phase}");
                    sb.AppendLine($"Sem Limite: {n.UnlimitedShiftTime} | Pausado: {n.IsShiftClockPaused} | Velocidade: {n.ShiftClockSpeedMultiplier:0.##}x");
                    sb.AppendLine($"Caixa: R$ {ns.RestaurantBudget.Value} | Faturamento: R$ {n.EarnedRevenue}/{n.RevenueGoal} | Rep: {n.Reputation}");
                    sb.AppendLine($"Run Tainted (Debug): {r.IsDebugTaintedRun} | Outcome Forcado: {n.IsForcedDebugOutcome}");
                }
#if UNITY_EDITOR
                if (root != null && root.CheckpointStore != null)
                {
                    sb.AppendLine($"Checkpoint Pre-Prep: {(root.CheckpointStore.PrePreparationCheckpoint != null ? $"Noite {root.CheckpointStore.PrePreparationCheckpoint.TargetNightNumber}" : "Ausente")}");
                    sb.AppendLine($"Checkpoint NightStart: {(root.CheckpointStore.NightStartCheckpoint != null ? $"Noite {root.CheckpointStore.NightStartCheckpoint.CurrentNightNumber}" : "Ausente")}");
                }
#endif
                if (root != null && root.RunProgressionService != null)
                {
                    sb.Append("Modificadores Ativos: ");
                    bool any = false;
                    foreach (var kvp in root.RunProgressionService.ActiveModifiers)
                    {
                        if (any) sb.Append(", ");
                        sb.Append($"{kvp.Key} (x{kvp.Value.Stacks})");
                        any = true;
                    }
                    if (!any) sb.Append("Nenhum");
                    sb.AppendLine();
                }
                return sb.ToString();
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!CanMutateAsHostEditor || !optionalSyncLightingWithShift) return;
            var ns = PizzeriaNetworkState.Instance;
            var d = TimeOfDayLightingDirector.Active;
            if (ns == null || d == null) return;
            var night = ns.NightState.Value;
            if (night.Phase != (byte)NightPhase.Active) return;

            float duration = night.UnlimitedShiftTime ? 600f : Mathf.Max(30f, night.RemainingSeconds + night.ElapsedSeconds);
            float t = Mathf.Clamp01(night.ElapsedSeconds / duration);
            float mappedHours = Mathf.Lerp(19f, 26f, t) % 24f;
            d.SetClockHours(mappedHours, evaluateNow: true);
        }
    }
}

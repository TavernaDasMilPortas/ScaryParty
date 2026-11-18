# Evidência Consolidada de Testes — Fases B0–B6 (incluindo B5.1 Revisada)

- **Data / Hora:** 18/11/2026 (`14:53Z`–`15:29Z`)
- **Unity Editor:** `6000.3.6f1` (instância aberta na porta `7800`, Pipeline CLI)
- **Branch:** `release/marcos-noturnos`

## 1. Validação Estrutural de Assets e Cenas
- `PlaytestInstallerAndValidator.ValidateAll`: **`Errors: 0, Warnings: 0`**
  - `NoiteDeTeste.asset` (`ScaryParty/Assets/Piaaria/Playtest/Presets/NoiteDeTeste.asset` + `.meta`) persistido e registrado em `PlaytestReferenceManifest.asset` e `PizzeriaConfig_PizzeriaModular_Manual.asset`.
  - `GameScene.unity` e `GameScene_CelShaded.unity` migradas via `PiariaGameplayUIMigration.EnsureGameScene`: `[Debug] Central de Playtest` (`PlaytestDebugController`) presente e `PlaytestDiagnosticsPresenter` / `TimeOfDayTestOverlay` removidos.

## 2. Testes EditMode (Unity CLI — `unity command --port 7800 test run --mode editor`)
Total executado em EditMode: **96/96 Passed** (`0 Failed`, `0 Skipped`).

| Suíte / Fixture | Filtro | Total | Passed | Failed | Skipped |
|---|---|---|---|---|---|
| `WizardEventsShopBalancingTests` (**T01–T36**) | `--filter WizardEventsShopBalancingTests --filter-type TestName` | 36 | **36** | 0 | 0 |
| `P6_PresetsAndDiagnosticsTests` | `--filter P6_PresetsAndDiagnosticsTests --filter-type TestName` | 3 | **3** | 0 | 0 |
| `ScaryParty.Pizzeria.Tests` (`OvernightExperienceTests` 18, `P2_OrderUnitAndEmployeeTests` 9, `P3_ConfigurableGoalsAndRunResetTests` 5, `P4_SupplyAndUiDomainTests` 3, `PizzeriaDomainTests` 19) | `--filter ScaryParty.Pizzeria.Tests --filter-type Assembly` | 54 | **54** | 0 | 0 |
| `P4_UiInputAndNetworkFlowTests` | `--filter P4_UiInputAndNetworkFlowTests --filter-type TestName` | 3 | **3** | 0 | 0 |

### Casos Cobertos em `WizardEventsShopBalancingTests` (T01–T36)
- **T01–T18 (B0–B5):** Migração idempotente e preservação de IDs (`T01`), fonte única `RunBalancingCatalog` (`T02`), ordem `base -> flat -> percentuais relativos -> multiplicadores -> limites` (`T03`), redução relativa de erro (`T04`), `RunDescriptionFormatter` fiel (`T05`), validador estrutural (`T06`), consumidores runtime de vampiros/demanda/paciência/upgrades (`T07`–`T09`), IDs base `1/3/4` pulando `2` e rejeição com estoque cheio (`T10`–`T11`), `MaxStacks` compartilhado (`T12`), simulador isolado 1000 sorteios (`T13`), preparação pré-Noite 1 + votação multi-estágio + desconexão sem deadlock + DTOs (`T14`–`T17`), catálogo base com 0 erros (`T18`).
- **T19–T36 (B5.1 Revisada):** Preset `NoiteDeTeste` selecionável sem timeout por turno (`T19`–`T20`), regras e timers não-relógio ativos com `UnlimitedShiftTime` (`T21`), propagação no resolver (`T22`), ausência de `[F8]`/`[F6]` e presença de `[Debug] Central de Playtest` em ambas as cenas (`T23`), conformidade `Tools/` (`T24`), guarda Host+Editor em todos os presets e bloqueio de cliente remoto/build (`T25`–`T26`), checkpoints `PrePreparation` e `NightStart` com `preserveCurrentBudget` e `RebaseRelativeDeadlines` (`T27`–`T29`), pausa/velocidade e desacoplamento expediente vs iluminação (`T30`–`T32`), `IsForcedDebugOutcome` vs natural (`T33`), reset completo (`T34`), composição `NightGoalMultiplier` (`T35`), integridade de referências do manifesto/config (`T36`).

## 3. Testes PlayMode (Unity CLI — `unity command --port 7800 test run --mode playmode --async-tests`)
Total executado em PlayMode (`PlaytestIntegrationPlayModeTests`): **6/6 Passed** (`0 Failed`, `0 Skipped`, `0 Inconclusive`, duração total `219.61s`, evidência JSON atualizada em [Unity_PlayMode_Summary.json](file:///c:/Users/Rubens/Documents/GitHub/ScaryParty/ScaryParty/Docs/Release/Reports/Evidence/Unity_PlayMode_Summary.json)):

- `PlayMode_ClientStartupWithoutDomainState_SpawnsAndSyncsProxiesSafely`: **Passed** (`1.698s`)
- `PlayMode_CustomPresetGoal_PreservedAfterNightRestart`: **Passed** (`0.164s`)
- `PlayMode_FailedProcessingOperation_LeavesItemAndStockUnmutated`: **Passed** (`0.125s`)
- `PlayMode_GameScene_FiveDeterministicCitySeeds_PassConnectivityAndRooftopValidation`: **Passed** (`168.030s`)
- `PlayMode_GameSceneHost_StartNewRunReconstructsKitchenWithoutDuplicates`: **Passed** (`48.419s`)
- `PlayMode_TwoIdenticalOrders_CompeteForOneReadyPizza_ExclusiveBinding`: **Passed** (`0.832s`)

## 4. Limitações Registradas Honestamente
- Para executar PlayMode TestRunner dentro do Editor aberto, `EditorSceneManager.playModeStartScene` (que apontava para `LobbyScene.unity`) precisou ser limpo temporariamente durante a execução e restaurado imediatamente ao final (`LobbyScene.unity`).
- Validação multiplayer real com **2 processos simultâneos (Host + Cliente remoto via ParrelSync/Build)** não foi executada nesta bateria CLI (apenas Host local e guarda unitária de rejeição remota).
- Inspeção visual/responsividade humana no GameView (`1280x720` / `1920x1080`), audição de UI e teste interativo completo de Inspector Odin permanecem para verificação manual no Unity Editor aberto.

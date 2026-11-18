# Relatório Técnico B0–B6: Wizard de Eventos, Loja, Balanceamento e Central de Playtest (B5.1 Revisada)

- **Branch:** `release/marcos-noturnos`
- **Unity:** `6000.3.6f1` (Universal Render Pipeline / Netcode for GameObjects)
- **Plano Executado:** `ScaryParty/Docs/Release/Plano_Wizard_Eventos_Loja_Balanceamento_ScaryParty_Atualizado.md` (Fases **B0–B6**, incluindo **B5.1 Revisada**)
- **Commits por Fase (`c6163121` base):**
  - **B0 (`42586a64`):** Inventário do caminho efetivo de configuração, schema do catálogo (`RunBalancingCatalog`) e migração idempotente (`RunBalancingMigrationRunner`).
  - **B1 (`ec4b3e09`):** Snapshot imutável único (`RunBalancingSnapshot`), pipeline determinístico de composição e formatador único de descrições (`RunDescriptionFormatter`).
  - **B2 (`9e99646d`):** Validador puro de domínio e simulador isolado de sorteio (`RunBalancingValidator`) + Wizard Odin de 8 abas (`PizzeriaEventsShopBalancingWizard`).
  - **B3 (`e7efb67f`):** Consumidores de runtime para multiplicadores de monstros/agressividade, intervalo/paciência de pedidos, piso de insumos (`1, 3, 4`), rejeição de loja com estoque cheio e `MaxStacks` compartilhado.
  - **B4 (`9748513f`):** Preparação pré-Noite 1 (`TargetNightNumber`), votação multi-estágio por categoria (`Hindrance`, `Opportunity`, `Event`) e suporte a múltiplas cartas/eventos por noite.
  - **B5 (`948a21b1`):** DTOs replicados do servidor (`NetRunVoteOptionDto`, `NetRunShopOfferDto`) e binding dinâmico em `RunPreparationPresenter` e `OperationsHubPresenter`.
  - **B5.1 Revisada (`abe2c8f0`):** Preset selecionável `NoiteDeTeste` (sem limite de tempo de turno), remoção de overlays `[F8]`/`[F6]` do GameView, centralização em Inspector Odin (`PlaytestDebugController` em `[Debug] Central de Playtest`), checkpoints (`PrePreparation`/`NightStart`), rebasing de prazos, separação estrita expediente vs iluminação e resultado real vs forçado.

---

## 1. Arquitetura e Fonte Única de Verdade (B0–B2)

1. **Caminho Efetivo de Configuração (documentado em [B0_INVENTARIO_MIGRACAO.md](file:///c:/Users/Rubens/Documents/GitHub/ScaryParty/ScaryParty/Docs/Release/Reports/B0_INVENTARIO_MIGRACAO.md)):**
   - Cenas `GameScene.unity` e `GameScene_CelShaded.unity` → Prefab `PizzeriaModular_Manual.prefab` / `PizzeriaModular_CelShaded.prefab` → `PizzeriaConfig_PizzeriaModular_Manual.asset` (`PizzeriaConfig`) → `RunBalancingCatalog.asset` (`balancingCatalog`), sob manifesto ativo `PlaytestReferenceManifest.asset`.
   - Migração idempotente via `RunBalancingMigrationRunner.RunIdempotentMigration()`: 1ª execução aplicou `35` alterações; 2ª execução aplicou `0` alterações, preservando todos os IDs legados (`blood_moon`, `vampire_attack`, `heavy_rush`, `express_supply`, `oven_timer`, `reinforced_bag`, `staff_training`).
2. **Pipeline Único de Composição (`RunProgressionService` / `RunBalancingSnapshot`):**
   - Ordem determinística para qualquer efeito e stat: `base -> flat -> deltas percentuais relativos -> multiplicadores -> limites`.
   - Redução de probabilidade de erro de funcionários (`ComputeEffectiveErrorProbability`) usa redução percentual relativa multiplicativa com piso mínimo configurável, evitando subtração linear que zeraria taxas pequenas.
   - `RunDescriptionFormatter` gera descrições exclusivamente a partir dos efeitos configurados no catálogo, eliminando divergências entre texto exibido e comportamento em runtime.
3. **Wizard Odin de 8 Abas (`Tools/Scary Party/Wizards/Pizzaria/Eventos, Loja e Balanceamento`, prioridade `18`):**
   - Páginas em `PizzeriaEventsShopBalancingWizard.cs`:
     1. `1. Visao Geral e Fonte Ativa`
     2. `2. Eventos e Entraves`
     3. `3. Beneficios e Oportunidades`
     4. `4. Loja da Pizzaria`
     5. `5. Funcionarios`
     6. `6. Economia e Reposicao`
     7. `7. Preparacao por Noite`
     8. `8. Simulacao e Validacao`
   - Suporta Undo, alerta visual de dirty não salvo, gravação atômica de `.asset`, validação estrutural (`RunBalancingValidator`) e simulação isolada de sorteio (`SimulateCategoryDraw`, 1000 iterações sem mutar a run ativa).

---

## 2. Runtime, Suprimentos Base, Votação Multi-Estágio e UI Replicada (B3–B5)

- **Consumidores de Efeitos e Economia (B3):**
  - `MonsterPopulationDirector.ComputeVampireSpawnCount` consome multiplicador de spawn (`blood_moon`).
  - `MonsterNpcController` aplica multiplicador de alcance de agressividade (`EffectiveAcquisitionRange` / `_baseAcquisitionRange` para `vampire_attack`).
  - `DemandDirector.ComputeEffectiveSpawnInterval` e `CustomerPatienceMultiplier` aplicam demanda (`heavy_rush`) e paciência sem tornar clientes hostis.
  - `NightCycleService.EnsureMinimumIngredientFloor` e `PizzeriaCommandHandler.ApplySupplyBundleLinesServer` garantem insumos base explícitos `1` (Massa), `3` (Molho) e `4` (Queijo Inteiro), pulando `2` (`Massa Aberta`), e rejeitam compras de loja quando todos os slots alvo estão cheios (sem gastar caixa).
  - `TryEvaluateShopOffer` e `TryPurchaseCatalogOffer` compartilham o controle de `MaxStacks` com a votação.
  - `EmployeeTaskService` resolve contratação, promoção e taxa de erro por `EmployeeRoleProfileData`.
- **Preparação Pré-Noite 1 e Votação Multi-Estágio (B4):**
  - `TargetNightNumber` em `PizzeriaState`, `NetNightStateDto`, `NetRunStateDto`, `ReplicationProjector` e `NightCycleService.StartNight` permite abrir preparação antes da Noite 1 (`OpenPreparationBeforeNightServer`), compor `NightGoalMultiplier` por noite e configurar múltiplos estágios por noite (ex.: `Hindrance` obrigatório + `Opportunity` opcional + `Event`, com 3 eventos na Noite 4).
  - `RunVoteStageState`, `OpenPreparationStages`, `CastStageVote`, `AreAllMandatoryStagesVoted` e `ResolveAllStagesServer` garantem desbloqueio de pronto apenas após votação nos estágios obrigatórios.
- **Replicação Servidor → UI Toolkit (B5):**
  - Adicionados `NetRunVoteOptionDto` e `NetRunShopOfferDto` e `NetworkList`s em `PizzeriaNetworkState`, com truncamento seguro UTF-8 (`TruncateUtf8`) em `ReplicationProjector`.
  - `RunPreparationPresenter` (com `ScrollView` responsivo para `1280x720` e `1920x1080`) e `OperationsHubPresenter` (`PopulateShopTab` e `PopulateEmployeesTab`) exibem somente DTOs e perfis enviados pelo servidor.

---

## 3. Preset `NoiteDeTeste` e Central Única de Playtest no Inspector (B5.1 Revisada)

1. **Preset `NoiteDeTeste.asset` (`ScaryParty/Assets/Piaaria/Playtest/Presets/NoiteDeTeste.asset`):**
   - `presetId = NoiteDeTeste`, `displayName = "Noite de Teste — sem limite de tempo"`, `unlimitedShiftTime = 1`, `allowDebugPanel = 1`.
   - Selecionável ao criar sala (`LobbyPresenter`) e visível na sala pronta (`ReadyRoomPresenter`).
   - Com `UnlimitedShiftTime = true` (`NightGoalParameters`, `NightParameterResolver`, `NightCycleService`), ultrapassar a duração nominal da noite não dispara fechamento automático por relógio; HUD (`HudPresenter`) mostra `SEM LIMITE`. Demais regras (reputação 0 → derrota, timers de pedidos/paciência/suprimentos, fechamento manual ou forçado) continuam normais.
2. **Remoção dos Overlays `[F8]` e `[F6]` e Centralização em Inspector Odin:**
   - Overlays no GameView desativados (`PlaytestDiagnosticsPresenter`, `PizzeriaRoot.EnsureExperiencePresenters`, `TimeOfDayTestOverlay`, `TimeOfDayLightingDirector.EnsureTestOverlay`).
   - `PiariaGameplayUIMigration.EnsureGameScene` e `PlaytestInstallerAndValidator` garantem o GameObject `[Debug] Central de Playtest` com `PlaytestDebugController` nas cenas `GameScene.unity` e `GameScene_CelShaded.unity`.
   - `PlaytestDebugMenu` adiciona atalho compatível com `unity-tools-menu-organization` em `Tools/Scary Party/Uso Comum/Pizzaria/Selecionar Central de Playtest (Inspector)` (prioridade `118`).
   - `PlaytestDebugController` organiza todos os controles em 8 abas Odin: `Noite`, `Expediente`, `Mundo e Iluminacao`, `Economia`, `Jogadores`, `Cenarios`, `Balanceamento` e `Diagnostico`.
3. **Segurança e Fidelidade Operacional:**
   - `PizzeriaCommandHandler.CanExecuteHostDebugMutation` (`#if UNITY_EDITOR`) permite mutações autoritativas **apenas para o Host em PlayMode dentro do Unity Editor em qualquer preset** (incluindo `Entrega_3Noites`, `Tutorial_Noite0`, `Playtest_Integracao` e `NoiteDeTeste`), bloqueando clientes remotos e builds finais.
   - `PlaytestCheckpointService` (`#if UNITY_EDITOR`) captura checkpoints `PrePreparation` e `NightStart`, suporta `preserveCurrentBudget` ao restaurar e executa `RebaseRelativeDeadlines` para proteger pedidos e suprimentos contra timeouts espúrios ao avançar/voltar o relógio.
   - Relógio do expediente (`RemainingSeconds`, `NightElapsedSeconds`, `IsShiftClockPaused`, `ShiftClockSpeedMultiplier`, `UnlimitedShiftTime`) e ciclo visual de iluminação (`TimeOfDayLightingDirector`, com `optionalSyncLightingWithShift = false` por padrão) são independentes.
   - Resultado natural da noite e resultado forçado de debug são distintos via `IsForcedDebugOutcome` (sinalizado `[DEBUG FORÇADO]` em `NightResultsPresenter`).

---

## 4. Validação Executada (Unity CLI na Instância Aberta — Porta `7800`)

| Verificação | Escopo / Comando | Resultado Observado |
|---|---|---|
| Validação Estrutural de Assets e Cenas | `PlaytestInstallerAndValidator.ValidateAll` (após persistir `NoiteDeTeste.asset` e migrar `GameScene.unity` e `GameScene_CelShaded.unity`) | `Validation complete -> Errors: 0, Warnings: 0` |
| EditMode — `WizardEventsShopBalancingTests` | `unity command --port 7800 test run --mode editor --filter WizardEventsShopBalancingTests --filter-type TestName --format json` (**T01–T36**) | **36/36 Passed** (0 Failed, 0 Skipped) |
| EditMode — `P6_PresetsAndDiagnosticsTests` | `unity command --port 7800 test run --mode editor --filter P6_PresetsAndDiagnosticsTests --filter-type TestName --format json` | **3/3 Passed** (0 Failed, 0 Skipped) |
| EditMode — `ScaryParty.Pizzeria.Tests` | `unity command --port 7800 test run --mode editor --filter ScaryParty.Pizzeria.Tests --filter-type Assembly --format json` (`OvernightExperienceTests` 18, `P2_OrderUnitAndEmployeeTests` 9, `P3_ConfigurableGoalsAndRunResetTests` 5, `P4_SupplyAndUiDomainTests` 3, `PizzeriaDomainTests` 19) | **54/54 Passed** (0 Failed, 0 Skipped) |
| EditMode — `P4_UiInputAndNetworkFlowTests` | `unity command --port 7800 test run --mode editor --filter P4_UiInputAndNetworkFlowTests --filter-type TestName --format json` | **3/3 Passed** (0 Failed, 0 Skipped) |
| PlayMode — `PlaytestIntegrationPlayModeTests` | `unity command --port 7800 test run --mode playmode --filter PlaytestIntegrationPlayModeTests --filter-type TestName --async-tests --format json` | **6/6 Passed** (`219.61s`, 0 Failed, 0 Skipped; resumo em `Evidence/Unity_PlayMode_Summary.json`) |

Detalhamento completo em [B6_TEST_SUMMARY.md](file:///c:/Users/Rubens/Documents/GitHub/ScaryParty/ScaryParty/Docs/Release/Reports/Evidence/B6_TEST_SUMMARY.md).

---

## 5. Passo a Passo para Validação Manual no Unity Editor

1. **Inspeção do Wizard e Catalog:** Abra `Tools/Scary Party/Wizards/Pizzaria/Eventos, Loja e Balanceamento`, percorra as 8 abas, confirme `0` erros em `8. Simulacao e Validacao` e execute a simulação de sorteio (1000 iterações) observando variação dentro dos pesos sem alterar o jogo.
2. **Fluxo de Lobby e Preset `NoiteDeTeste`:** No Lobby (`LobbyScene.unity`), crie uma sala selecionando `Noite de Teste — sem limite de tempo` (`NoiteDeTeste`), entre na `GameScene`, verifique que o HUD exibe `SEM LIMITE` no relógio do turno e confirme que pedidos, reputação e temporizadores de pacientes/forno continuam normais.
3. **Central de Playtest no Inspector (Host Editor em Qualquer Preset):**
   - Execute `Tools/Scary Party/Uso Comum/Pizzaria/Selecionar Central de Playtest (Inspector)` (ou selecione `[Debug] Central de Playtest` na Hierarchy).
   - Teste as 8 abas Odin no PlayMode com o Host (em `Entrega_3Noites` e em `NoiteDeTeste`): restaurar checkpoint (`Pre-Noite 1` e `NightStart` com e sem `Preservar Caixa Atual`), pausar/acelerar relógio de expediente, alterar `Hora Alvo` visual (`17h`, `19h`, `22h`, `02h`) confirmando que o relógio de meta do turno não é afetado, ajustar caixa/estoque, simular pedidos multi-receita/Qtd x3/vampiro e comparar desfecho natural vs `Forcar Vitoria/Derrota` (`[DEBUG FORÇADO]`).
   - Confirme que `[F8]` e `[F6]` não abrem overlays no GameView e que o layout da UI Toolkit (`RunPreparation` e `OperationsHub`) se mantém legível e com rolagem funcional em `1280x720` e `1920x1080`.

---

## 6. Limitações Conhecidas

- Testes reais com **2 processos simultâneos (Host + Cliente remoto real via ParrelSync / Build)** não foram executados nesta rodada via CLI (apenas guarda unitária de rejeição de `ClientId != ServerClientId` e execução em Host Editor único).
- Validação visual subjetiva em `1280x720` / `1920x1080` e interação humana completa com mouse/teclado nos fluxos visuais de preparação multi-estágio devem ser conferidas manualmente pelo usuário no GameView do Unity Editor aberto.

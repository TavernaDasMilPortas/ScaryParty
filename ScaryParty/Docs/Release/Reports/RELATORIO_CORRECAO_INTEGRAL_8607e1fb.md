# Relatório Vivo e Tabela de Controle — Correção Integral ScaryParty (`8607e1fb` → `main`)

**Plano base:** `Plano_Correcao_Integral_ScaryParty_8607e1fb.md` / `ScaryParty/Docs/Release/Reports/AUDITORIA_BUGS_NOTURNA_8607e1fb.md`
**Branch:** `main`
**Base inicial encontrada:** `c80a444a` (inclui `6de12cc1` e `c80a444a` pós-`8607e1fb`)
**Política Unity:** `UNITY_EXECUTION=MANUAL_ONLY` (`ScaryParty/Temp/UnityLockfile` ativamente travado pelo Unity Editor aberto; execuções `-batchmode`/`-runTests` não disparadas por agente)

---

## 1. Contagem Consolidada e Status Geral

A auditoria original listou 55 BUG no texto-resumo, mas a tabela completa contém **76 IDs `BUG` + 8 IDs `SUSP` = 84 IDs**.

| Classificação Revisada | Qtd | Observação |
|---|---:|---|
| **Bug confirmado** | **81** | 76 IDs `BUG-*` + 5 suspeitas confirmadas em código (`SUSP-02`, `SUSP-03`, `SUSP-04`, `SUSP-05`, `SUSP-07`) |
| **Melhoria defensiva** | **2** | `SUSP-01` (NGO atual já remove `clientId` antes do callback), `SUSP-06` (Domain Reload ativo em `EditorSettings.asset`) |
| **Suspeita híbrida (runtime + melhoria)** | **1** | `SUSP-08` (Parte A: disputa slot humano vs funcionário exige PlayMode; Parte B: warning na reflection NGO) |
| **Total** | **84** | **76 `BUG` + 8 `SUSP`** |

### Registro de Erro Histórico de Unity CLI e Bloqueio Atual
- **Auditoria `8607e1fb` (`editmode.log:1610-1654`, `playmode.log:681-725`):**
  - `Unity.exe -batchmode -nographics -projectPath "c:\Users\Rubens\Documents\GitHub\ScaryParty\ScaryParty" -runTests -testPlatform EditMode -testResults "...\editmode_results.xml" -logFile "...\editmode.log"` → **`ExitCode=3 (RunError)`** (`XML_EXISTS=False`) causado por `PizzeriaContentStudioTestRunner` (`[InitializeOnLoad]`) sujando cena `Untitled` e acionando `SaveModifiedSceneTask.cs:13` (`DisplayDialog` em batchmode).
- **Ambiente desta execução:**
  - `ScaryParty/Temp/UnityLockfile` está travado por processo Unity aberto e `AGENTS.md` impõe `UNITY_EXECUTION=MANUAL_ONLY`.
  - **Validação automatizada executada nesta sessão:** `dotnet build` nos csprojs (`ScaryParty.Pizzeria.Domain`, `ScaryParty.Pizzeria.Tests`, `ScaryParty.Cutscenes.Runtime`, `ScaryParty.VisualStyles.Runtime`, `Assembly-CSharp`, `Assembly-CSharp-Editor`) + execução real dos testes NUnit puros de domínio via harness `.NET` sobre `ScaryParty.Pizzeria.Tests.dll` + `git diff --check`.
  - **Validação pendente (runtime Unity):** suíte EditMode completa no Test Runner do Unity Editor, PlayMode (11+ testes), Build standalone Windows e Multiplayer 2 processos (Host + Cliente via ParrelSync/Build).

---

## 2. Tabela de Controle Completa (84 IDs)

| ID | Prio | Classificação | Fase | Status Atual | Arquivos Principais | Commit | Evidência / Validação | Pendência Runtime |
|---|---|---|---|---|---|---|---|---|
| `BUG-INFRA-01` | P1 | Bug confirmado | **0** | Corrigido (Compilação/estática validada) | `PizzeriaContentStudioTestRunner.cs`, `PizzeriaContentStudioTests.cs` | *Fase 0* | `[InitializeOnLoad]` e ctor estático removidos; `[MenuItem]` preservado; `Assembly-CSharp-Editor.csproj` compila (`0` erros) | Executar `-batchmode -runTests` / Test Runner com Editor fechado |
| `BUG-FLOW-01` | P0 | Bug confirmado | **A** | Pendente | `PizzeriaSceneLoadingCoordinator.cs:454-457, 524-540` | — | Confirmado em código (`sm.Retry` antes de `retry.Invoke()`) | PlayMode / Build |
| `BUG-FLOW-02` | P0 | Bug confirmado | **A** | Pendente | `PizzeriaSceneLoadingCoordinator.cs:67-68, 391, 703-735` | — | Confirmado em código (`_hookedNetworkEvents` bool) | PlayMode 2ª sessão |
| `BUG-FLOW-03` | P0 | Bug confirmado | **A** | Pendente | `LobbyManager.cs:15, 179-200, 457-470` | — | Confirmado em código (`OnDestroy` deleta lobby UGS) | Host + Cliente Relay |
| `BUG-FLOW-04` | P1 | Bug confirmado | **A** | Pendente | `SessionLaunchConfig.cs:44-45, 99` | — | Confirmado em código (não rastreia `CustomMessagingManager`) | Host + Cliente 2ª sessão |
| `BUG-FLOW-05` | P1 | Bug confirmado | **A** | Pendente | `ReadyRoomPresenter.cs:103-138`, `PizzeriaSceneLoadingCoordinator.cs:740` | — | Confirmado em código | Host + Cliente disconnect |
| `BUG-FLOW-06` | P1 | Bug confirmado | **A** | Pendente | `LobbyManager.cs:168-283`, `PizzeriaSceneLoadingCoordinator.cs:511` | — | Confirmado em código | PlayMode cancelamento |
| `BUG-FLOW-09` | P1 | Bug confirmado | **A** | Parcialmente corrigido (`6de12cc1`) | `PizzeriaRoot.cs:480-483`, `PizzeriaCommandHandler.cs:1278-1319` | `6de12cc1` | `ResolveSessionRunSettings` criado; falta ternário `includeNight0` e guarda server-side `StartNewRunServerRpc` | Host + Cliente Nova RUN |
| `BUG-FLOW-10` | P1 | Bug confirmado | **A** | Parcialmente corrigido (`6de12cc1`) | `GameplayIntroCutsceneFlow.cs:78-104`, `CutscenePlaybackService.cs:126-149, 369-375` | `6de12cc1` | Gating intro→prep feito; falta guarda loading `ErrorVisible`/`Failed` e `Play()` retornando `bool` | PlayMode cutscene + erro loading |
| `BUG-FLOW-11` | P1 | Bug confirmado | **A** | Pendente | `OperationsHubPresenter.cs:187-192, 655-763`, `RunShopPagePresenter.cs:89-180` | — | Confirmado em código (`[J]` não correlacionado; loja sem timer no clique) | Host + Cliente loja |
| `BUG-FLOW-15` | P2 | Bug confirmado | **A** | Pendente | `ReadyRoomPresenter.cs:555-558, 725-738, 930-958`, `CharacterPreviewRig.cs:394-424` | — | Confirmado em código (`_slots[0]` hardcoded) | Host + Cliente ReadyScene |
| `BUG-FLOW-16` | P2 | Bug confirmado | **A** | Pendente | `OperationsHubPresenter.cs:491-493, 796-825`, `NetDtos.cs`, `EmployeeAgentController.cs:2771-2797` | — | Confirmado em código (ETA 1x e custo fallback em cliente) | Host + Cliente `[J]` |
| `BUG-BUILD-01` | P0 | Bug confirmado | **A** | Parcialmente corrigido (`6de12cc1`) | `Assets/Resources/PiariaUIAssetCatalog.asset`, `PiariaUIInstallerWindow.cs` | `6de12cc1` | 8 refs copiadas; faltam 3 `StyleSheet`s e sync no installer | Build standalone |
| `BUG-BUILD-02` | P1 | Bug confirmado | **A** | Pendente | `UIFeedbackService.cs:60-65`, `HudPresenter.cs`, `ToastPresenter.cs`, `ScoreboardPresenter.cs`, `PiariaPresenterUtils.cs` | — | Confirmado em código (`UIClick.wav` em `ThirdParty/` e `Resources.Load` mortos) | Build standalone |
| `BUG-UI-01` | P1 | Bug confirmado | **A** | Pendente | `PauseEscapeMenuPresenter.cs:292-311, 337` | — | Confirmado em código (ESC não checa intermissão/cutscene/loading) | PlayMode ESC |
| `SUSP-01` | P1 | Melhoria | **A** | Refutado no NGO; melhoria pendente | `PlayerLifecycleBridge.cs:29-42`, `PizzeriaCommandHandler.cs:1557-1605` | — | `NetworkConnectionManager.cs:1534-1567` já remove `clientId` antes do callback | Host + Cliente disconnect |
| `SUSP-06` | P2 | Melhoria | **A/F** | Pendente (Domain Reload ativo) | `UIToolkitTweenService.cs`, `GameplayInputGate.cs`, `RunShopPagePresenter.cs` | — | Estáticos sem `SubsystemRegistration` | PlayMode sem Domain Reload |
| `BUG-FLOW-07` | P1 | Bug confirmado | **C** | Parcialmente corrigido (`6de12cc1`) | `NightCycleService.cs:295`, `PlaytestCheckpointService.cs:188-194` | `6de12cc1` | `StartNight` já zera; falta `PlaytestCheckpointStore.TryRestore` | PlayMode checkpoint |
| `BUG-FLOW-08` | P1 | Bug confirmado | **C** | **Já corrigido (`6de12cc1`)** | `RunProgressionService.cs:164, 254`, `PizzeriaCommandHandler.cs:4167-4170` | `6de12cc1` | `ResetIntermissionState()` + `OpenTypedBallots` + `NightTransitionAndPreparationTests` (59/59 pass .NET) | PlayMode Noite 0→1 |
| `BUG-FLOW-12` | P2 | Bug confirmado | **C** | Parcialmente corrigido (`c80a444a`) | `PizzeriaState.cs:52-64`, `NightCycleService.cs:50-92`, `PizzeriaCommandHandler.cs:1242-1276` | `c80a444a` | Sessões limpas no retry; falta `Reputation` em `NightStartSnapshot` | PlayMode retry pós-reputação 0 |
| `BUG-FLOW-13` | P2 | Bug confirmado | **C** | Pendente | `NightSummaryPagePresenter.cs:35-84`, `PizzeriaRoot.cs:560-571`, `NetDtos.cs` | — | Confirmado em código (goal como gastos e título `"NOITE 0 CONCLUÍDA"`) | Editor/Build intermissão |
| `BUG-FLOW-14` | P2 | Bug confirmado | **C** | Pendente | `PlaytestDebugController.cs:203-207, 318`, `PizzeriaCommandHandler.cs:4042, 4093`, `PizzeriaRoot.cs:563` | — | Confirmado em código (`-100` clampado, `JumpToLast5s`, `AllowDebugPanel`) | PlayMode Central Playtest |
| `BUG-ORD-01` | P1 | Bug confirmado | **C** | Pendente | `CustomerState.cs`, `ItemState.cs`, `PlayerHoldService.cs`, `CustomerService.cs`, `ProcessingService.cs`, `ReplicationProjector.cs:791`, `EmployeeAgentController.cs:721` | — | Confirmado em código (sentinela `0` colide com Host `0UL`) | Host + Cliente atendimento/bancada |
| `BUG-ORD-02` | P1 | Bug confirmado | **C** | Parcialmente corrigido (`6de12cc1`) | `PizzeriaRoot.cs:895-927`, `DemandDirector.cs:129` | `6de12cc1` | Guarda `NightPhase.Active` aplicada; faltam pausas e rebase de deadlines | PlayMode pausa/cutscene |
| `BUG-ORD-03` | P2 | Bug confirmado | **C** | Pendente | `OrderService.cs:168-185`, `CustomerService.cs:546-588` | — | Confirmado em código (corrida `IsDineIn` `-15` rep vs arquivamento) | PlayMode mesa expirada |
| `BUG-ORD-04` | P2 | Bug confirmado | **C** | Pendente | `PlayerLifecycleBridge.cs:56-68`, `CustomerService.cs:635-649` | — | Confirmado em código (`BeingAttended` preso após disconnect/morte) | Host + Cliente disconnect |
| `BUG-ORD-05` | P2 | Bug confirmado | **C** | Pendente | `PizzeriaCommandHandler.cs:1233`, `EmployeeTaskService.cs:316`, `TicketBoardStation.cs:205-247` | — | Confirmado em código (solta todas as tarefas; 4 concluídas ocultam ativas) | PlayMode quadro comandas |
| `BUG-DELIV-01` | P0 | Bug confirmado | **C** | Pendente | `DeliverySelectionPresenter.cs:200-212`, `CarriedCargoResolver.cs` | — | Confirmado em código (`HolderId == myId` ignora mochila do Host) | Host + Cliente entrega mochila |
| `BUG-DELIV-02` | P1 | Bug confirmado | **C** | Pendente | `DeliveryPointAdapter.cs:31`, `Ids.cs:94-121` | — | Confirmado em código (`LabelDestinationId <= 0` colide com Endereço 1 `id=0`) | PlayMode entrega endereço 0/1 |
| `BUG-DELIV-03` | P2 | Bug confirmado | **C** | Pendente | `DeliveryEvaluator.cs:278` | — | Confirmado em código (`NightOrdersFailed++` quando `order == null`) | PlayMode entrega errada |
| `BUG-MAP-01` | P1 | Bug confirmado | **C** | Pendente | `MinimapPresenter.cs:409-410`, `MinimapRouteManager.cs:348` | — | Confirmado em código (`ClaimedByClientId > 0`) | Host + Cliente minimapa |
| `BUG-LOG-01` | P2 | Bug confirmado | **C** | Pendente | `BackpackPanelPresenter.cs:156, 272-299, 383`, `LogisticsCommands.cs:265`, `BackpackVisualizer.cs:47-67, 210` | — | Confirmado em código (reparo 0s, ignora `EffectiveCapacity`, `_bagRenderers`) | PlayMode mochila danificada |
| `SUSP-02` | P2 | Bug confirmado | **C** | Pendente (promovido) | `RunProgressionService.cs:588-661` | — | Confirmado em código (`CastCategoryVote` sai no 1º estágio) | PlayMode votação catálogo |
| `SUSP-03` | P2 | Bug confirmado | **C** | Pendente (promovido) | `PizzeriaCommandHandler.cs:1013-1037` | — | Confirmado em código (`AnswerPhoneServerRpc` cria pedido `AddressId(4)`) | PlayMode RPC telefone |
| `SUSP-07` | P3 | Bug confirmado | **C** | Pendente (promovido) | `StoragePresenter.cs:96-118`, `LogisticsCommands.cs:331`, `PizzeriaCommandHandler.cs:1125` | — | Confirmado em código (delta alheio fecha estoque; falta distância entrega/mesa) | Host + Cliente estoque/mesa |
| `BUG-NAV-01` | P0 | Bug confirmado | **B1** | Pendente | `EmployeeAgentController.cs:2348-2356, 2423-2459` | — | Confirmado em código (checa `targetPos` bruto vs `targetNavPos`; sem stuck) | PlayMode NavMesh NPCs |
| `BUG-NAV-02` | P0 | Bug confirmado | **B1** | Pendente | `CustomerSpawner.cs:276-310`, `CustomerCounterStation.cs:51-63`, `PizzeriaDoorAuthoring.cs` | — | Confirmado em código (`rend.bounds` mundo gira obstacle; workpoint `0.85 < 0.95`) | PlayMode 4 rotações pizzaria |
| `BUG-NAV-03` | P0 | Bug confirmado | **B1** | Pendente | `StationDoorPresenter.cs:156-296`, `NavigationWorldManager.cs:68-69` | — | Confirmado em código (`abertura.001` sem `NavMeshModifier.ignoreFromBuild`) | PlayMode Show NavMesh |
| `BUG-NAV-04` | P1 | Bug confirmado | **B1** | Pendente | `EmployeeAgentController.cs:698, 763-840, 2449-2457` | — | Confirmado em código (Balconista silencioso; `SetDestination`/scan todo frame) | PlayMode Balconista bloqueado |
| `BUG-NAV-05` | P1 | Bug confirmado | **B1** | Pendente | `PizzeriaRoot.cs:1009, 1329, 1338`, `CustomerSpawner.cs:121-135` | — | Confirmado em código (tautologia `basePos`, slot 0 deslocado, bake concorrente) | PlayMode spawn funcionários |
| `BUG-NAV-06` | P1 | Bug confirmado | **B1** | Pendente | `EmployeeStationCache.cs:79, 87`, `EmployeeAgentController.cs` | — | Confirmado em código (cache trava vazio; 10 `FindObject*`; depósito remoto) | PlayMode Pizzaiolo estoque |
| `BUG-EMP-01` | P0 | Bug confirmado | **B1** | Pendente | `EmployeeAgentController.cs:1208-1266`, `TransferService.cs:140-154, 231-257` | — | Confirmado em código (`OvenService.RemovePizza` -> `Busy`; `DestroyItem` deixa op órfã) | PlayMode forno Pizzaiolo |
| `BUG-CUST-01` | P0 | Bug confirmado | **B1** | Pendente | `CustomerNpcView.cs:197-201` | — | Confirmado em código (`ApplyState` reativa `Agent.updatePosition`) | PlayMode cliente sentando |
| `BUG-CUST-02` | P2 | Bug confirmado | **B1** | Pendente | `CustomerNpcView.cs:227-298`, `CustomerService.cs:576-602` | — | Confirmado em código (saída sem mesa/`Warp` e vazamento cadeira) | PlayMode cliente saindo |
| `BUG-KIT-01` | P1 | Bug confirmado | **B2** | Pendente | `PlayerLifecycleBridge.cs:84-95`, `ProcessingService.cs:98-102` | — | Confirmado em código (`ReservedByHold` travado após disconnect/morte) | Host + Cliente preparo |
| `BUG-KIT-02` | P1 | Bug confirmado | **B2** | Pendente | `PizzeriaCommandHandler.cs:667-839`, `ProcessingService.cs:369-478`, `PizzeriaRoot.cs:841-866` | — | Confirmado em código (15s hardcoded, reserva, reinserção queimada, deadband) | PlayMode forno completo |
| `BUG-KIT-03` | P1 | Bug confirmado | **B2** | Pendente | `NightCycleService.cs:406-437`, `ReceivingStation.cs:7-10` | — | Confirmado em código (`Arrived` nunca descarrega com estoque cheio) | PlayMode doca recebimento |
| `BUG-KIT-04` | P1 | Bug confirmado | **B2** | Pendente | `PizzeriaCommandHandler.cs:617-664`, `PizzaAssemblyService.cs` | — | Confirmado em código (RPC sem alcance/posse/reserva/`ActiveOperations`) | PlayMode montagem pizza |
| `BUG-KIT-05` | P2 | Bug confirmado | **B2** | Pendente | `TrashStation.cs:7-10`, `StationView.cs:209-212` | — | Confirmado em código (`[E]` apoia item na lixeira) | PlayMode lixeira `[E]`/`[F]` |
| `BUG-KIT-06` | P2 | Bug confirmado | **B2** | Pendente | `ToolRackStation.cs:102-191`, `OvenStation.cs:43-87, 372-461` | — | Confirmado em código (toasts otimistas, slot `< 4`, arma/ferramenta no forno) | PlayMode rack & forno |
| `BUG-KIT-07` | P2 | Bug confirmado | **B2** | Pendente | `ProcessingService.cs:34-48` | — | Confirmado em código (`ToolItemId == toolItemId` bloqueia ferramenta equivalente) | PlayMode troca faca |
| `BUG-KIT-08` | P2 | Bug confirmado | **B2** | Pendente | `KitchenCommands.cs:400-451`, `PizzeriaRoot.cs:841-866` | — | Confirmado em código (`OvenService` não tickado) | PlayMode forno unificado |
| `BUG-PLY-02` | P1 | Bug confirmado | **B2** | Pendente | `PlayerInteraction.cs:516-522, 1078-1137` | — | Confirmado em código (trabalho redirecionado cancela em 1 frame) | PlayMode `[F]` slot vizinho |
| `BUG-PLY-05` | P2 | Bug confirmado | **B2** | Pendente | `PlayerInteraction.cs:825-870`, `PizzeriaCommandHandler.cs:466-586` | — | Confirmado em código (toast global cancela trabalho sem `ServerRpc`) | PlayMode toast durante `[F]` |
| `SUSP-08` | P3 | Suspeita / Melhoria | **B2/F** | Parte A: PlayMode · Parte B: Melhoria | `TransferService.cs:96-193`, `OwnerNetworkAnimator.cs:43-69` | — | Parte A requer experimento PlayMode; Parte B campos NGO validados no pacote atual | PlayMode disputa slot + NGO anim |
| `BUG-CITY-01` | P1 | Bug confirmado | **D** | Pendente | `BlockFiller.cs:43`, `StreetGridGenerator.cs:389, 473`, `CityGenerator.cs:437-488` | — | Confirmado em código (`config.seed` em vez da seed ativa) | Host + Cliente `seed == 0` |
| `BUG-CITY-02` | P2 | Bug confirmado | **D** | Pendente | `CityGenerator.cs:492-596`, `DeliveryPointPlacer.cs:31`, `PizzeriaPlacementSolver.cs:167-260`, `StreetGridGenerator.cs:701-854` | — | Confirmado em código (5 sub-itens) | PlayMode 5 seeds |
| `BUG-PED-01` | P1 | Bug confirmado | **D** | Pendente | `PedestrianNpc.cs:48-61, 253-308` | — | Confirmado em código (desvio da buzina conclui viagem) | PlayMode buzina |
| `BUG-PED-02` | P1 | Bug confirmado | **D** | Pendente | `PedestrianNpc.cs:227-331`, `CityPopulationDirector.cs:151-163, 284-298`, `PedestrianCrossing.cs`, `PedestrianRoutePlanner.cs` | — | Confirmado em código (pool leak, inativos, sem travessia) | PlayMode pedestres/faixas |
| `BUG-TRF-01` | P1 | Bug confirmado | **D** | Pendente | `TrafficManager.cs:348-457`, `TrafficVehicle.cs:24, 78-90, 182-189` | — | Confirmado em código (`CurrentEdgeId` congelado após 1º segmento) | PlayMode semáforos |
| `BUG-TRF-02` | P1 | Bug confirmado | **D** | Pendente | `TrafficManager.cs:284-295, 635-646` | — | Confirmado em código (`OnEdgeBlocked` corta por dentro de quarteirões) | PlayMode evento bloqueio rua |
| `BUG-TRF-03` | P1 | Bug confirmado | **D** | Pendente | `TrafficVehicle.cs:280-296`, `CityPopulationDirector.cs:167` | — | Confirmado em código (`transform.root` = `__City__`) | PlayMode atropelamento |
| `BUG-TRF-04` | P2 | Bug confirmado | **D** | Pendente | `TrafficVehicle.cs:124-135, 210-252` | — | Confirmado em código (impasse perpétuo em cruzamento) | PlayMode cruzamento |
| `BUG-MOTO-01` | P0 | Bug confirmado | **D** | Pendente | `DeliveryMotorcycleController.cs:381-411`, `PizzeriaParkingLotBuilder.cs:85, 97-98` | — | Confirmado em código (calçada bloqueia saída do pátio) | PlayMode saída moto |
| `BUG-MOTO-02` | P1 | Bug confirmado | **D** | Pendente | `DeliveryMotorcycleController.cs:283-291, 477-498`, `PizzeriaParkingLotBuilder.cs:246-253`, `PizzeriaCommandHandler.cs:3707-3747` | — | Confirmado em código (sem sweep/trigger, monstros sem `NetworkObject`, friendly-fire) | PlayMode colisões moto |
| `BUG-MOTO-03` | P1 | Bug confirmado | **D** | Pendente | `PizzeriaCommandHandler.cs:115-123, 2670-2683, 3749-3773`, `DeliveryMotorcycleController.cs:313-356` | — | Confirmado em código (ejeção insegura e CC remoto desativado) | Host + Cliente ejeção moto |
| `BUG-MP-01` | P0 | Bug confirmado | **E** | Pendente | `FirstPersonCameraController.cs:364-372`, `PizzeriaCommandHandler.cs:2260, 2716, 3506-3511`, `DeliveryMotorcycleController.cs:313-320` | — | Confirmado em código (`ConnectedClients` client-side + sentinela `0`) | Host + Cliente espectador/tiro/moto |
| `BUG-PLY-01` | P1 | Bug confirmado | **E** | Pendente | `PlayerHealth.cs:154-175` | — | Confirmado em código (`_tpc.enabled = true` todo frame) | PlayMode moto + ReadyScene |
| `BUG-PLY-03` | P1 | Bug confirmado | **E** | Pendente | `PlayerInteraction.cs:279-331, 662-671, 851-857` | — | Confirmado em código (`StopAssistAlly` omitido + sentinela `0`) | Host + Cliente ajuda aliado |
| `BUG-PLY-04` | P1 | Bug confirmado | **E** | Parcialmente corrigido (`c80a444a`) | `PlayerInteraction.cs:190-272` | `c80a444a` | `TeleportPlayerClientRpc` corrigido; falta `PlayerInteraction.TeleportClientRpc` | Host + Cliente spawn/yaw |
| `BUG-PLY-06` | P2 | Bug confirmado | **E** | Pendente | `PizzeriaCommandHandler.cs:3058`, `PlayerHealth.cs:20, 193` | — | Confirmado em código (`||` anula `enableAllPlayersDeadDefeat = false`) | PlayMode derrota equipe |
| `BUG-CMB-01` | P1 | Bug confirmado | **E** | Pendente | `WeaponController.cs:248-258, 408-414`, `PizzeriaCommandHandler.cs:3442-3475` | — | Confirmado em código (deadlock reserva 0, struct descartada, sem `!IsSlinged`/cooldown) | PlayMode rack + recarga |
| `BUG-CMB-02` | P1 | Bug confirmado | **E** | Pendente | `MonsterNpcController.cs:544-559, 608-613, 738-801` | — | Confirmado em código (tiro no bote congela `NavMeshAgent`) | PlayMode Lobisomem |
| `BUG-CMB-03` | P1 | Bug confirmado | **E** | Pendente | `MonsterNpcController.cs:395-401, 675-681` | — | Confirmado em código (não verifica `IDamageable.IsAlive`) | Host + Cliente monstros |
| `BUG-CMB-04` | P2 | Bug confirmado | **E** | Pendente | `PizzeriaCommandHandler.cs:3108-3215, 3477-3495`, `MonsterNpcController.cs:752-791` | — | Confirmado em código (rack não atômico, melee sem `Linecast`, RPC teste aberto) | PlayMode/Build combate |
| `BUG-NET-01` | P1 | Bug confirmado | **E** | Pendente | `PizzeriaCommandHandler.cs:2883-2920`, `PlayerState.cs:386-390` | — | Confirmado em código (`ReportCustomerAttack` e `SetReadyServerRpc`) | Host + Cliente RPCs |
| `BUG-NET-02` | P1 | Bug confirmado | **E** | Pendente | `PlayerLifecycleBridge.cs:29-42`, `PizzeriaCommandHandler.cs:2685-2701, 2922-2962` | — | Confirmado em código (armas/combate vazam no disconnect) | Host + Cliente disconnect |
| `BUG-NET-03` | P2 | Bug confirmado | **E** | Pendente | `PizzeriaCommandHandler.cs`, `OrderCommands.cs`, `LogisticsCommands.cs`, `MonsterPopulationDirector.cs` | — | Confirmado em código (distância/sessões, `Revision++` 60Hz, O(N²)+GC monstros) | Host + Cliente + Profiler |
| `SUSP-04` | P2 | Bug confirmado | **E** | Pendente (promovido) | `PizzeriaCommandHandler.cs:3346-3357` | — | Confirmado em código (`Linecast` mascarado por colisor filho) | PlayMode tiro perto parede |
| `SUSP-05` | P2 | Bug confirmado | **E** | Pendente (promovido) | `PlayerLifecycleBridge.cs:119-131, 155-169` | — | Confirmado em código (`StationId == 1` é Lixeira/Geladeira nos prefabs) | Host + Cliente disconnect |
| `BUG-PERF-01` | P1 | Bug confirmado | **F** | Pendente | `TimeOfDayLightingDirector.cs:84-94, 180`, `PizzeriaLightingController.cs:131, 191, 204, 270` | — | Confirmado em código (varredura por frame sem cache) | Profiler CPU/GC |
| `BUG-UI-02` | P2 | Bug confirmado | **F** | Pendente | `OutlineService.cs`, `SceneCameraAudioBootstrap.cs`, `HudPresenter.cs`, `ScoreboardPresenter.cs`, `MonsterNpcController.cs` | — | Confirmado em código (`_ZTest` em MPB + leaks + `FindObject*`) | PlayMode visual + Profiler |
| `BUG-ASSET-01` | P1 | Bug confirmado | **F** | Pendente | `VisualStyleMaterialFactory.cs:182-193` + 9 `.mat` (`16` arquivos) >260 chars | — | Confirmado em disco em `custom_style_20261111_225726/Materials/` | Executar utilitário `AssetDatabase.MoveAsset` no Editor |
| `BUG-MISC-01` | P3 | Bug confirmado | **F** | Pendente | `PizzeriaSoundManager.cs:38-45`, `ModifierVotePagePresenter.cs:98-99`, `PiariaUIInstallerWindow.cs:803-846`, `PracticeTargetDummy.cs` | — | Confirmado em código | Editor Installer + Host + Cliente |

---

## 3. Checkpoints por Fase

### Fase 0 — Infraestrutura de Testes (`BUG-INFRA-01`)
- **Alterações aplicadas:**
  - `ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Editor/Tests/PizzeriaContentStudioTestRunner.cs`: removidos `[InitializeOnLoad]` e o construtor estático que agendava `RunAllStudioTests` via `EditorApplication.delayCall` no startup/batchmode, mantendo `[MenuItem("Tools/Scary Party/Uso Comum/Pizzaria/Executar Testes de Autoria & Slots", false, 111)]`.
  - `ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Editor/Tests/PizzeriaContentStudioTests.cs`: adicionado teste `ContentStudioTestRunner_DoesNotAutoRunOnInitializeOnLoad`.
- **Validações executadas:**
  - `dotnet build ScaryParty/ScaryParty.Pizzeria.Domain.csproj --no-restore -v q`: `0` erros.
  - `dotnet build ScaryParty/ScaryParty.Pizzeria.Tests.csproj --no-restore -v q`: `0` erros.
  - `dotnet build ScaryParty/Assembly-CSharp-Editor.csproj --no-restore -v q`: `0` erros.
  - Execução NUnit `.NET` (`DomainTestRunner` sobre `ScaryParty.Pizzeria.Tests.dll`): **7 fixtures, 59/59 aprovados, 0 falhas, 0 ignorados**.
- **Pendências runtime:** rodar `EditMode` e `PlayMode` via Test Runner do Unity Editor (ou via CLI após fechar o Unity Editor).

# Relatório Final de Playtest, Correções P0–P7, UI e Placeholders 3D — ScaryParty

- **Data:** 18/11/2026
- **Branch:** `release/marcos-noturnos`
- **SHA Base (início de P0):** `bdffe5096163c0bac817a94a1c05e4d22fdf4652`
- **Ambiente Validado:** Unity `6000.3.6f1 (bbb010bdb8a3)` | Unity CLI `1.0.1` (`win32-x64`) | `com.unity.pipeline@1.0.0` | `com.unity.multiplayer.playmode@2.0.2` | `.NET` SDK (`dotnet build`)
- **Política Preservada:** Todas as 43 modificações locais pré-existentes do usuário (Cutscene, animators, `StationDefs`, `DefaultNetworkPrefabs.asset`, `Packages/*`, `ScaryParty.slnx`) permaneceram intactas. As execuções via Unity CLI nesta fase ocorreram por autorização explícita da tarefa, sem alterar a política global `UNITY_EXECUTION=MANUAL_ONLY`.

---

## 1. Commits por Fase (`P0–P7`)

| Fase | Commit SHA | Escopo Principal Entregue |
|---|---|---|
| **P0** | `af9e829f` | Inventário técnico (`P0_INVENTARIO_PLAYTEST.md`), `PlaytestReferenceManifest.cs/.asset`, `BuildVersionInfo.txt`, `ReadyScene.unity` com `UIFeedbackService` e instalador/validador idempotente (`PlaytestInstallerAndValidator.cs`). |
| **P1** | `a0013cde` | Sincronização de monstros e funcionários nos clientes sem `DomainState` autoritativo (`NetMonsterDto` com `Yaw`, `NetEmployeeDto` com `Yaw`/`HP`/unidade, delta snapshots em vez de `.Clear()`, lifecycle de proxies visuais, posturas de morte/telegraph). |
| **P2** | `12ad738b` | Operações autônomas reais de funcionários sem conclusão forçada (`HeartbeatWork`, reserva exclusiva de slots/utensílios, fim de dreno/vazamento), identidade exclusiva 1-para-1 pizza ↔ unidade de pedido (`OrderUnitFulfillmentService`), descarte `[F]` na lixeira e elegibilidade de pizza embalada (`ItemCategory.Box`). |
| **P3** | `68a18e49` | `PizzeriaRunPreset`, resolução unificada de metas e tempos (`NightParameterResolver`), congelamento de humanos em startup/restart sem contagem de funcionários, reset completo de Nova RUN (`DemandDirector`, sessões, `PlayerState.Money`) e suporte completo à Noite 0 não-letal. |
| **P4** | `0a40e6b9` | Fluxo UI Toolkit navegável completo: `SessionLaunchConfig` na `LobbyScene`/`ReadyScene`, aviso/erro de sala/reload, `HudPresenter` (Caixa vs pessoal, participantes, bandoleira visível), `OperationsHubPresenter` (`[J]`: Pedidos, Reposição/Alho, Loja, Equipe), `LifeAndSpectatorOverlayPresenter`, `MinimapPresenter` completo, `UIModalStack` + trava de clique `WeaponInputHandler` / `GameplayInputGate`. |
| **P5** | `4c94e6fa` | Instalação persistente de 12 prefabs provisórios 3D e 16 materiais URP Lit em `Assets/Piaaria/Playtest/`, `MonsterVisualCatalog.asset`, `EntityVisualAnchorRig` (moto, funcionários, monstros), terminal de suprimentos/doca/quadro de gestão, Alho (`id=10`), 4º `PackagingStation` (`id=9`) em `PizzeriaModular_Manual.prefab` e anchors/validação de telhado na cidade (`CityBuilding`/`CityGenerator`). |
| **P6** | `170bfe62` | Presets `Entrega_3Noites.asset` (padrão ~36 min, debug desativado), `Playtest_Integracao.asset` (rápido, debug ativo) e `Tutorial_Noite0.asset` ligados em `PizzeriaConfig_PizzeriaModular_Manual.asset`; painel recolhível `PlaytestDiagnosticsPresenter` (`[F8]`, host-only em mutações reais com `IsDebugTaintedRun`, oculto em entrega) instalado em `GameScene` e `GameScene_CelShaded`. |
| **P7** | *(commit final desta entrega)* | Correção de `CutscenePlaybackService.PlayRoutine` (unload seguro), suíte `PlaytestIntegrationPlayModeTests` (6/6), capturas visuais em `1920x1080` e `1280x720`, evidências JSON/XML, este relatório e atualização documental. |

---

## 2. Achados Confirmados, Corrigidos e Pendências Residuais

### 2.1 Achados Corrigidos (`P1–P7`)
1. **Clientes sem corpos/proxies de monstros e funcionários (`P1`):** Corrigida a trava que só chamava `MonsterPopulationDirector.EnsureOn` no servidor e exigia `DomainState != null`; `NetMonsterDto`/`NetEmployeeDto` agora carregam orientação/estado completos e `EmployeePopulationDirector` sincroniza por delta sem recriações duplicadas.
2. **Funcionários forçando resultados e pizzas competindo sem exclusividade (`P2`):** Eliminado `CompleteWork` forçado e consumo sem estoque; `OrderUnitFulfillmentService` amarra `OrderUnitKey(GenerationId, OrderId, LineIndex, UnitIndex)` ↔ `ItemId` garantindo entrega única e reabertura automática se um humano desviar/descartar a pizza. Descarte `[F]` na lixeira reparado em `PlayerInteraction`.
3. **Metas sobrescritas e sujeira entre Nova RUN (`P3`):** `NightParameterResolver` honra tabelas de `PizzeriaRunPreset` / `NightConfig` tanto no startup quanto em `RetryCurrentNight`; `ReinitializeForNewRunServer` reconstrói estações/estoques/mochilas e zera receita/modificadores/sessões sem duplicar entidades.
4. **Telas desconectadas, reposição ausente e clique vazando da UI para arma (`P4`):** `OperationsHubPresenter` (`[J]`) unifica Pedidos, Encomenda de Suprimentos (entrega na Doca / `ReceivingStation`), Loja com limite de stacks e Contratação/Gestão de Equipe; `GameplayInputGate` + `UIModalStack` + checagem de painel em `WeaponInputHandler` bloqueiam disparo acidental; conflito `[H]` Receitas vs `[F1]` Tutorial / `[F2]` Pular Noite 0 resolvido.
5. **Estações sem 3D, falta de Alho e anchors implícitos (`P5`):** `Station_SupplierTerminal`, `Station_Receiving`, `ManagementBoard_Playtest`, moto lateral, funcionários e monstros possuem prefabs/materiais URP persistentes no projeto e no prefab `PizzeriaModular_Manual.prefab`, com `EntityVisualAnchorRig` explícito (sem depender de osso implícito).
6. **Presets e diagnóstico (`P6`):** `Entrega_3Noites` permanece padrão (~36 min, `[F8]` oculto); `Playtest_Integracao` e `Tutorial_Noite0` habilitam `[F8]` para o Host acionar mutações reais nos serviços do servidor com sinalização `RUN ALTERADA POR DEBUG`.
7. **Exceção real ao interromper cutscene em troca de cena (`P7`):** A 1ª execução PlayMode detectou `MissingReferenceException` em `CutscenePlaybackService.PlayRoutine` (`Assets/Piaaria/Scripts/Cutscenes/Runtime/CutscenePlaybackService.cs:274`) quando `CutScene.unity` era descarregada durante `LoadSceneMode.Single`. Corrigido com validação de `_activeStage != null && _activeStage.director != null` ao longo da corrotina e cancelamento no `TearDown`.

### 2.2 Problemas e Limitações Residuais (Informar com Honestidade)
- **Multiplayer real em 2 processos (`Host + Cliente` separados):** **NÃO EXECUTADO.** O pacote `com.unity.multiplayer.playmode@2.0.2` está instalado, porém possui `0` cenários e `0` clones virtuais configurados (`editor.playmode list_instances` retornou apenas o Editor principal), e a política do projeto proíbe abrir segunda instância Unity concorrente no mesmo diretório (`Library/` lock). Toda prova de sincronização remota depende de testes manuais em 2 processos (MPPM ou build + Editor).
- **Sobreposição menor no canto superior direito entre `[F6]` e `[F8]` / `[F1]` (`V15` visual):** Nas capturas de `GameScene`, quando o painel `[F8]` está recolhido, a pílula superior direita `[F6] (Crepúsculo)` sobrepõe parcialmente a pílula `[F8] Diagnóstico` no mesmo quadrante; quando `[F8]` expande, seu fundo translúcido escuro sobrepõe o cartão `GUIA OPERACIONAL [F1]` à direita, mantendo todos os botões de `[F8]` legíveis e clicáveis (pressionar `[F1]` minimiza o guia para leitura limpa).
- **Rede/Vivox sem autenticação UGS:** Em execuções offline no Editor sem login UGS, `VivoxApiClient` loga `HTTP/1.1 401 Unauthorized` no console de áudio/voz — esperado fora de sessão autenticada, sem impedir transporte NGO local.
- **Passagem completa `LobbyScene -> GameScene` via cliques de Lobby:** Na captura da `GameScene`, o carregamento foi disparado via script de teste mantendo o `NetworkManager` persistente da `LobbyScene` (`NetworkAutoStart` destruiu a cópia da `GameScene` porque `NetworkManager.Singleton` já existia em `DontDestroyOnLoad`), exigindo `StartHost()` ativo. No fluxo manual normal em `LobbyScene`, o clique em **Criar Sala** / **Iniciar Partida** já cria a sessão no `LobbyManager` antes da transição.

---

## 3. Qual Cena Abrir, Fluxo de Sessão e Acesso às Funcionalidades

### 3.1 Cenas Exatas para Abrir no Unity Editor
| Finalidade | Cena exata (`Assets/...`) | Quando usar |
|---|---|---|
| **Fluxo Completo (Menu → Lobby → Ready → Gameplay)** | `Assets/Piaaria/Scenes/LobbyScene.unity` | Fluxo recomendado para testar escolha de **Preset / Dificuldade / Tutorial Noite 0 / Seed**, entrada de cliente e transição de cena. |
| **Gameplay Canônica Direta (Host rápido)** | `Assets/ThirdParty/StarterAssets/ThirdPersonController/Scenes/GameScene.unity` | Abrir direto e dar Play para cair como Host na pizzaria (preset padrão da config = `Entrega_3Noites`, ou definir `SessionLaunchConfig` via fluxo Lobby/teste). |
| **Variante Cel-Shaded** | `Assets/Piaaria/Scenes/GameplayVariants/GameScene_CelShaded.unity` | Mesma gameplay da canônica com instalação P4/P5/P6 aplicada e iluminação/contorno estilizado. |
| **Sala de Espera (não dar Play isolado)** | `Assets/Piaaria/Scenes/ReadyScene.unity` | Carregada automaticamente pela `LobbyScene` (`PizzeriaSceneLoadingCoordinator`); mantém `UIFeedbackService` e configuração de sala. |

### 3.2 Presets Configurados (`Assets/Piaaria/Playtest/Presets/`)
| Preset | Duração das Noites | Tutorial Noite 0 | Painel `[F8]` (`AllowDebugPanel`) | Uso Indicado |
|---|---|---|---|---|
| **`Entrega_3Noites`** *(Padrão de `PizzeriaConfig`)* | Noite 1: `660s` (`11m`)<br>Noite 2: `720s` (`12m`)<br>Noite 3: `780s` (`13m`) — Total **36 min** | Desativado | **Oculto (`false`)** | Jogatina completa / entrega final sem interface de debug exposta. |
| **`Playtest_Integracao`** | Noite 1: `90s`<br>Noite 2: `105s`<br>Noite 3: `120s` — Total **5 min 15s** | Desativado | **Habilitado (`true`)** | Playtest rápido para validar fluxo multi-noite, loja entre noites e painel `[F8]`. |
| **`Tutorial_Noite0`** | Noite 0: `420s` (não-letal)<br>+ Noites 1/2/3 (`660s/720s/780s`) | Ativado (`true`) | **Habilitado (`true`)** | Validar Noite 0 educativa, botão/atalho `[F2]` e transição intacta para Noite 1/3. |

### 3.3 Mapa de Controles e Atalhos em Gameplay
- **`[E]`**: Pegar/colocar ingrediente, atender balcão/mesa, usar terminal de suprimentos (`Station_SupplierTerminal`), abrir Central pelo quadro (`ManagementBoard_Playtest`), abrir loja/preparo, montar/desmontar moto.
- **`[F]`** *(segurar)*: Processar em bancada (`Preparation`), descartar pizza/item na lixeira (`Station_Trash`), reparar mochila, resgatar aliado caído.
- **`[J]`**: Abre/fecha a **Central de Operações** (`Pedidos`, `Reposição`, `Loja`, `Funcionários`). Também acessível interagindo com o Terminal de Suprimentos ou Quadro de Gestão.
- **`[F8]`**: Recolhe/expande o **Painel Técnico de Playtest** *(somente visível quando `AllowDebugPanel = true`; botões de mutação exclusivos do Host)*.
- **`[F1]`** / **`[F2]`**: Minimiza/exibe o **Guia Operacional / Tutorial** (`[F1]`) e pula **Noite 0 → Noite 1** (`[F2]` quando em Noite 0).
- **`[H]`**: Livro de **Receitas** (incluindo receita com Alho `id=10`).
- **`[M]`** / **`[B]`** / **`[G]`**: Mapa ampliado com destinos e rotas (`[M]`), **Mochila** (`[B]`), **Bandoleira de espingarda** (`[G]`).
- **`[TAB]`** / **`[ESC]`** / **`[Q]`-`[E]` (espectador)**: Placar/participantes (`[TAB]`), Menu de Pausa e Recuperação (`[ESC]`), troca de câmera em modo espectador (`[Q]`/`[E]`).

---

## 4. Mapa Funcionalidade → UI / Interação → Prefab / Cena → Config → Teste

| Funcionalidade | UI / Interação | Prefab / Cena | Config / Preset | Teste Automatizado que Cobre |
|---|---|---|---|---|
| **Escolha de Preset, Tutorial, Dificuldade e Seed** | Menu da `LobbyScene` (`Jogar → Criar Sala`) e painel Host na `ReadyScene` | `LobbyScene.unity`, `ReadyScene.unity` | `SessionLaunchConfig`, `PizzeriaRunPreset` (`Entrega_3Noites`, `Playtest_Integracao`, `Tutorial_Noite0`) | `P4_UiInputAndNetworkFlowTests.SessionLaunchConfig_*`, `P6_PresetsAndDiagnosticsTests.*` |
| **HUD Operacional, Caixa vs Bolso & Bandoleira** | Barra superior (Noite/Meta/Caixa/Rep), Bolso R$, Mãos 1–2, Vida + Equipe, Mochila/Bandoleira | `GameScene.unity`, `GameScene_CelShaded.unity` (`MainGameHUD.uxml` + `HudPresenter`) | `PizzeriaConfig_PizzeriaModular_Manual.asset`, `PlaytestReferenceManifest.asset` | `PiariaGameplayUIMigrationTests`, `PlaytestIntegrationPlayModeTests.PlayMode_GameSceneHost_*` |
| **Central de Operações (`[J]`): Pedidos, Reposição, Loja e Funcionários** | Tecla `[J]`, Terminal (`Station_SupplierTerminal`), Quadro (`ManagementBoard_Playtest`) | `PizzeriaModular_Manual.prefab` (`Station_SupplierTerminal`, `Station_Receiving`, `ManagementBoard_Playtest`) | `SupplyItem_*.asset` (inclui `SupplyItem_5_Garlic`), `OperationsHubPresenter` | `P4_SupplyAndUiDomainTests.*`, `P4_UiInputAndNetworkFlowTests.NetSupplyDto_*` |
| **Operações Reais de Funcionários & Pizza Exclusiva** | Comandas automáticas e disputa humano ↔ funcionário em bancadas/utensílios | `EmployeeAgentController` em `PizzeriaRoot`, `Visual_Employee_Cashier/Pizzaiolo.prefab` | `PizzeriaConfig.enableEmployees`, `OrderUnitFulfillmentService` | `P2_OrderUnitAndEmployeeTests.*` (9 testes EditMode), `PlayMode_FailedProcessingOperation_*`, `PlayMode_TwoIdenticalOrders_*` |
| **Proxies de Monstros/Funcionários sem `DomainState`** | Colisão/combate e visualização em clientes remotos | `Visual_Monster_*.prefab`, `Visual_Employee_*.prefab` (sem `NetworkObject` no proxy visual) | `MonsterVisualCatalog.asset`, `NetMonsterDto`, `NetEmployeeDto` | `P1_MonsterEmployeeClientSyncTests.*` (5 testes EditMode), `PlayMode_ClientStartupWithoutDomainState_*` |
| **Metas Configuráveis, Noite 0 & Reset de Nova RUN** | Banner da Noite, `NightResultsPresenter`, `RunPreparationPresenter`, botão Nova RUN | `GameScene.unity`, `PizzeriaModular_Manual.prefab` | `NightParameterResolver`, `NightConfig`, `PizzeriaRunPreset` | `P3_ConfigurableGoalsAndRunResetTests.*` (5 testes EditMode), `PlayMode_CustomPresetGoal_*`, `PlayMode_GameSceneHost_StartNewRun*` |
| **Morte, Incapacitação & Espectador** | Barra de sangramento, aviso ao grupo e overlay espectador (`[Q]`/`[E]`), resgate `[F]` | `LifeAndSpectatorOverlayPresenter` instalado em `GameScene` | `PlayerHealth`, `PlayerAllyHelpInteractable` | `OvernightExperienceTests.R1_LifeStateCleanup*`, `PiariaGameplayUIMigrationTests` |
| **Painel de Diagnóstico `[F8]` & Comandos Host** | Toggle `[F8]` / botão superior direito (apenas `AllowDebugPanel=true`) | `PlaytestDiagnosticsPresenter` em `GameScene` / `GameScene_CelShaded` | `Playtest_Integracao`, `Tutorial_Noite0`, `BuildVersionInfo.txt`, `PizzeriaCommandHandler` | `P6_PresetsAndDiagnosticsTests.HostDebugMutationGuard_*` |
| **Cidade Procedural, Moto e Anchors de Telhado** | Geração determinística de quadras/ruas, spawns e rotas no Minimapa `[M]` | `Visual_DeliveryMotorcycle.prefab`, `CityBuilding` rooftop anchors | `DefaultCityConfig.asset`, `CityGenerator`, `NavigationWorldManager` | `P5_PlaceholdersAndAnchorsTests.CityBuilding_*`, `CityGraphPathfinderTests.*`, `PlayMode_GameScene_FiveDeterministicCitySeeds_*` |

---

## 5. Placeholders 3D Instalados e Guia de Substituição pelo Asset Final

Todos os ativos provisórios persistentes foram gerados com materiais compatíveis com **URP Lit** (`Assets/Piaaria/Playtest/Materials/`) e registrados no catálogo central **`Assets/Piaaria/Resources/Playtest/PlaytestReferenceManifest.asset`**. Nenhum prefab visual possui `NetworkObject` redundante em proxies apenas visuais e **nenhuma espécie de monstro exibe label identificador na interface normal** (a identificação técnica fica restrita ao toggle opcional dentro do painel `[F8]`).

| ID no Manifesto | Categoria | Asset Instalado (`Assets/Piaaria/Playtest/...`) | Referência onde está ligado | Como trocar pelo asset definitivo |
|---|---|---|---|---|
| `monster_vampire`, `monster_werewolf`, `monster_ghost`, `monster_zombie` | Monstros | `Prefabs/Visual_Monster_Vampire.prefab`, `_Werewolf.prefab`, `_Ghost.prefab`, `_Zombie.prefab` | `ScriptableObjects/MonsterVisualCatalog.asset` + `PlaytestReferenceManifest` | Trocar a malha/FBX filha sob `VisualRoot` dentro de cada prefab ou apontar outro prefab no `MonsterVisualCatalog.asset` preservando `EntityVisualAnchorRig` e colliders. |
| `employee_cashier`, `employee_pizzaiolo` | Funcionários | `Prefabs/Visual_Employee_Cashier.prefab`, `Visual_Employee_Pizzaiolo.prefab` | `PlaytestReferenceManifest` (usados por `EmployeePopulationDirector`) | Substituir o modelo humanoide dentro dos prefabs preservando `EntityVisualAnchorRig` (anchors `Hand_R`, `Hand_L`, `Carry`, `Head`). |
| `motorcycle` | Veículo | `Prefabs/Visual_DeliveryMotorcycle.prefab` | `PlaytestReferenceManifest` (usado por `PizzeriaParkingLotBuilder` / `DeliveryMotorcycleController`) | Trocar a geometria filha mantendo `DeliveryMotorcycleController`, colliders e anchors do `EntityVisualAnchorRig`. |
| `supplier_terminal` | Cozinha / Suprimentos | `Prefabs/Visual_SupplierTerminal.prefab` | Instanciado como filho visual em `PizzeriaModular_Manual.prefab` (`Station_SupplierTerminal`) | Substituir a malha filha mantendo o `Station_SupplierTerminal` pai, collider e script `SupplierTerminal`. |
| `receiving_zone` | Cozinha / Suprimentos | `Prefabs/Visual_ReceivingZone.prefab` | Instanciado como filho visual em `PizzeriaModular_Manual.prefab` (`Station_Receiving`) | Substituir a malha/palete filha mantendo `Station_Receiving` e seu `slotAnchor`. |
| `management_board` | Cozinha / Operações | `Prefabs/Visual_ManagementBoard.prefab` | `ManagementBoard_Playtest` dentro de `PizzeriaModular_Manual.prefab` | Substituir o mural filho mantendo `ManagementBoardStation` e o `BoxCollider` de interação. |
| `ingredient_garlic`, `ingredient_mushroom` | Ingredientes | `Prefabs/Visual_Ingredient_Garlic.prefab`, `Visual_Ingredient_Mushroom.prefab` | `IngredientDef_10_Garlic.asset`, `Ingredient_8_CogumeloInteiro.asset`, `Ingredient_9_CogumeloFatiado.asset` | Apontar `placeholderPrefab` / visual final nos ScriptableObjects de ingrediente preservando os GUIDs dos `.asset`. |
| `SupplyItem_5_Garlic` | Reposição / Receita | `ScriptableObjects/SupplyItem_5_Garlic.asset` | `PizzeriaConfig_PizzeriaModular_Manual.asset` (`allSupplyItems` + `allIngredients` com ID `10`) | Manter ID `10` e GUID do `.asset` ao ajustar custo/ícone/nome final. |
| Anchors de Telhado (`CityBuilding`) | Cidade / Ameaças | Componentes filhos explícitos gerados por `CityBuilding.EnsurePortal()` + validados em `CityGenerator` | Quadras procedurais geradas em runtime (`GameScene`) | Preservar nomes de transforms `Rooftop_Perch` / `Rooftop_Drop` se criar pré-moldados manuais de telhado. |

**Checklist rápido de swap:** substitua sempre os **filhos visuais (`VisualRoot`)** dentro dos prefabs ou edite os campos apontados no `PlaytestReferenceManifest.asset` / `MonsterVisualCatalog.asset`; **não apague arquivos `.meta`** e rode `Tools/Scary Party/... -> ValidateAll` (via `PlaytestInstallerAndValidator.ValidateAll`) após a troca para confirmar 0 erros/warnings.

---

## 6. Testes Executados vs Verificações Pendentes (Evidências Reais)

| Camada / Mecanismo | Comando / Procedimento Executado | Resultado Real | Duração | Evidência Preservada |
|---|---|---|---|---|
| **Compilação `.NET` C#** | `dotnet build ScaryParty\ScaryParty.Pizzeria.Tests.csproj --no-restore` | **PASSOU** (`0 Erro(s)`, `0 Aviso(s)`, `exit 0`) | `5.24s` | Saída de terminal observada após última edição em C#. |
| **Unity EditMode (Suíte Completa Segura)** | `PlaytestTestResultXmlWriter.RunEditModeRegexSuite` via `unity command --port 7800` | **PASSOU — `102/102`** (`failed=0, skipped=0`) | `52.759s` | `Docs/Release/Reports/Evidence/Unity_EditMode_Suite_Summary.json` + XML NUnit3 local `Unity_EditMode_Suite_Results.xml` (`89.772` bytes). |
| **Unity EditMode (Baterias Incrementais P1–P6)** | Runs direcionados por fase via `unity command --port 7800 test run` | **PASSOU** (`P1: 5/5`, `P2: 46/46`, `P3: 51/51`, `P4: 54/54 + 3/3 + 1/1`, `P5: 5/5`, `P6: 3/3 + 1/1`) | Diversos | JSONs preservados na pasta de resultados (`P1..P6_EditMode_TestRun.json`). |
| **Unity PlayMode (1ª Rodada — Diagnóstico)** | `unity command --port 7800 test run -- --mode playmode --filter PlaytestIntegrationPlayModeTests --async-tests` | **FALHOU — `3/6 Passed, 3 Failed`** | `309.44s` | Revelou bug real em `CutscenePlaybackService.PlayRoutine:274`, requisito `Boxed` antes de entrega em `Pizza` e timeout de `180s` em 5 seeds. |
| **Unity PlayMode (2ª Rodada pós-correção)** | Mesma suíte `PlaytestIntegrationPlayModeTests` após correções em `CutscenePlaybackService.cs` e testes | **PASSOU — `6/6`** (`failed=0, skipped=0, exit 0`) | `503.73s` | `Docs/Release/Reports/Evidence/Unity_PlayMode_Summary.json` + XML local `Unity_PlayMode_Results.xml` (`23.065` bytes) + `P7_PlayMode_TestStatus.txt`. |
| **MPPM / 2º Processo Multiplayer** | `unity command --port 7800 editor.playmode list_scenarios` e `list_instances` | **NÃO EXECUTADO** (`0` cenários MPPM, apenas Main Editor `pid 29024`) | `—` | Bloqueio ambiental (sem clone virtual MPPM configurado e lock em `Library/` impede 2º Editor concorrente). |
| **Inspeção Visual PlayMode GameView (`1920x1080` e `1280x720`)** | `unity command --port 7800 capture game_view` em `LobbyScene` + `GameScene` (`Playtest_Integracao` Host) | **PASSOU — 8/8 PNGs inspecionados** | `—` | `screenshots/Lobby_*.png`, `GameScene_HUD_*.png`, `GameScene_OperationsHub_*.png`, `GameScene_DiagnosticsF8_*.png` (0 materiais pink/magenta, UI legível nas duas resoluções). |
| **Persistência e Restauração do Editor** | `editor.playmode stop` + reabrir `LobbyScene.unity` (`isDirty=False`) + `PlaytestInstallerAndValidator.ValidateAll` | **PASSOU (`Errors=0, Warnings=0`)** | `—` | Console pós-restauração com `compilationFailed: false`; flags `run_in_background` e `auto_tick` restauradas para `false`. |

---

## 7. Matriz Completa `V01–V16` (Status Honesto)

| Caso | Descrição | Status Atual | Evidência / Motivo & Passos Manuais Pendentes |
|---|---|---|---|
| **V01** | Abrir cena/sessão novamente; UI e objetos persistidos | **PASSOU (EditMode / Pós-PlayMode)** | `ValidateAll -> Errors=0, Warnings=0` após sair de PlayMode e reabrir `LobbyScene.unity` (`isDirty=False`); `PiariaGameplayUIMigrationTests` (`1/1`); `Playtest_Integracao` ativo em PlayMode. |
| **V02** | Host + cliente real em processos separados; monstros/funcionários | **NÃO EXECUTADO EM 2 PROCESSOS** *(Unitário/Proxy em PlayMode único PASSOU)* | Unitário/Single-Process: `P1_MonsterEmployeeClientSyncTests` (`5/5`) + `PlayMode_ClientStartupWithoutDomainState_*`. **Manual pendente:** Abrir Host + Cliente via ParrelSync/MPPM ou build + Editor, spawnar monstros/funcionários via `[F8]` e confirmar 1 corpo por entidade sem duplicação. |
| **V03** | Cliente recebe população pré-existente (late-join / snapshot) | **PARCIAL — PLAYMODE SINGLE PROCESS** | Snapshot replicado validado em `PlayMode_ClientStartupWithoutDomainState_*` e `P1_MonsterEmployeeClientSyncTests`. **Manual pendente:** Conectar 2º processo após Host já ter entidades ativas na noite. |
| **V04** | Jogador faz pizza completa do início à entrega | **PARCIAL — DOMÍNIO EDITMODE** | Ciclo de domínio coberto em `PizzeriaDomainTests` (`ProcessingService`, `PizzaAssemblyService`, `OvenService`, `DeliveryEvaluator`). **Manual pendente:** Executar fluxo tátil completo em PlayMode (`[E]`/`[F]` nas estações da `GameScene`, `[B]` mochila, moto/mesa). |
| **V05** | Funcionário faz duas receitas e pedido com quantidade 3 | **PASSOU EM DOMÍNIO EDITMODE** *(Runtime visual 2 processos pendente)* | `P2_OrderUnitAndEmployeeTests.QuantityThreeLine_RequiresThreeDistinctPizzas_*` e `TwoConcurrentEmployees_*`. **Manual pendente:** Acionar no `[F8]` *"Pedido 2 Receitas"* + *"Pedido Qtd 3"* + *"Contratar Pizzaiolo"* e observar execução até entrega. |
| **V06** | Dois pedidos iguais disputam uma pizza pronta na bancada | **PASSOU (EditMode + PlayMode)** | `P2_OrderUnitAndEmployeeTests.TwoIdenticalOrders_*` e `PlayMode_TwoIdenticalOrders_CompeteForOneReadyPizza_ExclusiveBinding`. |
| **V07** | Ferramenta ocupada / operação rejeitada sem consumo/mutação | **PASSOU (EditMode + PlayMode)** | `P2_OrderUnitAndEmployeeTests.HumanHeldOrLeasedTool_*`, `UnavailableIngredient_*` e `PlayMode_FailedProcessingOperation_LeavesItemAndStockUnmutated`. |
| **V08** | Humano assume trabalho do funcionário e reconcilia reservas | **PASSOU EM DOMÍNIO EDITMODE** | `P2_OrderUnitAndEmployeeTests.HumanRemovesMovesAndDeliversBoundPizzaToAnotherOrder_ReopensOriginalUnitAndTask` + `EmployeeTaskService_ExclusiveReservationsHumanTakeover*`. **Manual pendente:** Testar interrupção ao vivo em gameplay. |
| **V09** | Meta customizada e tutorial Noite 0 mantidos após restart | **PASSOU (EditMode + PlayMode)** | `P3_ConfigurableGoalsAndRunResetTests.*` (`5/5`), `P6_PresetsAndDiagnosticsTests.*` (`3/3`), `PlayMode_CustomPresetGoal_PreservedAfterNightRestart`. |
| **V10** | Compra, reposição (`[J]`), loja e votos pela UI sincronizados | **PARCIAL — DOMÍNIO + CAPTURA UI** | Domínio: `P4_SupplyAndUiDomainTests.*` (`3/3`) e `NetSupplyDto` (`P4_UiInputAndNetworkFlowTests`); UI `Reposição [J]` capturada nas 2 resoluções. **Manual pendente:** Testar entrega física na Doca (`ReceivingStation`), empate de votos e compra simultânea entre Host e Cliente. |
| **V11** | Moto + pistola, atropelamento, espingarda/bandoleira (`[G]`) | **NÃO EXECUTADO EM RUNTIME INTERATIVO** | Lógica revisada e prefab `Visual_DeliveryMotorcycle.prefab` + `WeaponInputHandler` compilados. **Manual pendente:** Pegar armas no rack, montar na moto com pistola numa mão, testar `[G]` bandoleira, atropelar monstro/cliente com multiplicidade de colliders. |
| **V12** | Dano, resgate `[F]`, morte, espectador (`[Q]`/`[E]`) e retorno na noite seguinte | **PARCIAL — DOMÍNIO + INSTALAÇÃO UI** | `OvernightExperienceTests.R1_LifeStateCleanup*` + `LifeAndSpectatorOverlayPresenter` instalado em `GameScene`. **Manual pendente em 2 jogadores:** Levar dano fatal, validar bloqueio total de interação como espectador, resgate aliado `[F]` interrompido por dano e retorno automático na noite seguinte. |
| **V13** | Monstro cliente muda para agressivo e libera atendimento/mesa | **PASSOU EM DOMÍNIO EDITMODE** | `OvernightExperienceTests.Etapa4_InterruptCustomerForCombat_FreesQueueTableAndOrder` + `R5_SpeciesHostilityGarlicOffenseAndZombieConversionIdempotency`. **Manual pendente:** Testar visualmente com cliente vampiro/alho (`[E]` / combate na loja). |
| **V14** | Três noites completas + derrota/vitória e Nova RUN limpa | **PASSOU EM RECONSTRUÇÃO PLAYMODE + DOMÍNIO** | `OvernightExperienceTests.RunProgression_ThreeNights*`, `P3_ConfigurableGoalsAndRunResetTests.*` e `PlayMode_GameSceneHost_StartNewRunReconstructsKitchenWithoutDuplicates`. **Manual pendente:** Jogar ciclo de 3 noites completo até transição final em 2 instâncias. |
| **V15** | UI em duas resoluções (`1920x1080` e `1280x720`) e troca de modais sem tiro acidental | **PARCIAL — INSPEÇÃO VISUAL + UNITÁRIO INPUT GATE** | 8 capturas PlayMode inspecionadas nas duas resoluções (sem cortes impeditivos; pequeno overlap `[F6]`/`[F8]` e `[F1]` atrás de `[F8]` documentado); `GameplayInputGate_StackedModals_*` (`Pass`). **Manual pendente:** Clicar botões com pistola equipada verificando ausência de disparo na abertura/fechamento. |
| **V16** | Cinco seeds e trânsito (spawns acessíveis, barreiras e carros circulando) | **PASSOU EM PLAYMODE (`5 seeds + determinismo`)** | `PlayMode_GameScene_FiveDeterministicCitySeeds_PassConnectivityAndRooftopValidation` (`413.69s`) + `CityGraphPathfinderTests.*` (`5/5`). **Manual pendente:** Observação visual prolongada (5 min) dos veículos cruzando semáforos e pedestres. |

---

## 8. Roteiro Curto de Playtest para Rubens (~10–15 Minutos)

1. **Validação rápida com diagnóstico (`Playtest_Integracao`):**
   - Abra `Assets/Piaaria/Scenes/LobbyScene.unity` no Unity Editor já aberto e dê **Play**.
   - Clique **JOGAR → Criar Sala**; na tela da sala (`ReadyScene`), selecione Preset **`Playtest_Integracao`** (`90s/105s/120s`, Debug `[F8]` ativo) e inicie a partida. *(Se houver cutscene introdutória, pule conforme prompt.)*
   - Teste no balcão/cozinha: `[J]` para abrir a Central Operacional e encomendar **Alho (`id=10`)** ou Massa em *Reposição* (confira chegada física na área `Station_Receiving`), `[H]` Receitas, `[F1]` Guia.
   - Pressione **`[F8]`** (somente Host): clique em **Pedido 2 Receitas**, **Pedido Qtd 3**, **Contratar Pizzaiolo**, observe os funcionários trabalharem e ative o **Overlay Técnico de Espécies** para conferir spawn/proxies. Em seguida clique **Nova RUN (Noite 0)** para validar o reset limpo e o botão **`[F2]` Pular para Noite 1**.
2. **Teste Multiplayer (`V02`, `V03`, `V10`, `V12`):**
   - Configure 1 clone no **Multiplayer Play Mode (MPPM)** ou gere 1 build Windows Standalone local enquanto mantém o Unity Editor como Host; conecte o cliente e verifique corpos sincronizados, compras pelo `[J]`, votos A/B entre noites, câmera espectador e ausência de mutações pelo `[F8]` no cliente.
3. **Conferência de Entrega (`Entrega_3Noites`):**
   - Inicie uma sessão pela `LobbyScene` escolhendo **`Entrega_3Noites`** (ou abra `GameScene.unity` direto): confirme noites de 11m/12m/13m (~36 min totais) e que **`[F8]` não aparece nem abre** no modo de entrega.

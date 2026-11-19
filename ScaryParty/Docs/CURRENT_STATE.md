# Estado Atual do Projeto — ScaryParty

**Última Revisão:** 18/11/2026
**Versão Unity:** Unity 6000.3.6f1
**Branch Atual:** `release/marcos-noturnos`
**Commits Recentes:** Base `bdffe509` → `c6163121` + fases **B0–B6** (incluindo **B5.1 Revisada**: catálogo único `RunBalancingCatalog`, Wizard Odin 8 abas, consumidores B3, preparação pré-Noite 1 & votação multi-estágio B4, UI replicada B5, preset `NoiteDeTeste` sem limite de tempo de turno, central Odin `PlaytestDebugController` sem overlays `[F8]`/`[F6]`, EditMode `96/96` + PlayMode `6/6` + `ValidateAll` `0` erros; multiplayer 2 processos pendente).
**Pipeline de Renderização:** Universal Render Pipeline (URP) com Cel Shading / Toon Shader

---

## 1. Visão Geral dos Módulos

| Módulo | Caminho Principal | Status | Descrição Sumária |
|---|---|---|---|
| **Pizzeria** | `Assets/Piaaria/Scripts/Pizzeria/` | Implementado (L2 pendente) | Bancadas de preparo, mesas/cadeiras com animação de sentar, ciclo de noites (`NightCycleService`), quadro de comandas, mochilas danificáveis e posse autoritativa (`TransferService`). |
| **City** | `Assets/Piaaria/Scripts/City/` | Implementado (L2 pendente) | Cidade procedural, módulos com porta dupla `J = 4m / 2 slots`, preenchimento seguro de perímetro, trânsito com ID estável + semáforos A/B determinísticos e moto de entregas (`MOTO` + `O13`). |
| **Combat / Player** | `Assets/Piaaria/Scripts/Weapons/`, `Scripts/Player/` | Implementado (L2 pendente) | Rack de armas (Pistola 1 mão / Escopeta 2 mãos), `PlayerHealth` (100 HP + auto-resgate 10s), `PracticeTargetDummy`, interação e reparo da mochila (`[F]` 3s). |
| **Navigation** | `Assets/Piaaria/Scripts/Navigation/` | Funcional | Geração dinâmica de NavMesh para NPCs, clientes e pedestres (incluindo recuo à buzina). |
| **Network (NGO)** | `Assets/Piaaria/Scripts/Network/`, `Pizzeria/Network/` | Implementado (L2 pendente) | RPCs autoritativos em `PizzeriaCommandHandler` (armas, combate, dano à mochila, noites, recuperação e moto/buzina) e replicação em `PizzeriaNetworkState`/`NetDtos`. |
| **UI / Minimap** | `Assets/Piaaria/Scripts/UI/`, `UI/Piaria/` | Implementado (L2 pendente) | Loading unificado, HUD da noite/vida/munição/mochila, menu de pausa (`PauseEscapeMenuPresenter`), resultados da noite (`NightResultsPresenter`) e dicas contextuais. |
| **VisualStyles / Lighting** | `Assets/Piaaria/Scripts/VisualStyles/`, `Scripts/Lighting/`, `Shaders/` | Implementado (validação manual pendente) | Modos A/B/C na `GameScene` original, shaders próprios `ScaryParty/Illustrate Stylized/*`, ciclo Dia/Noite, zonas da pizzaria, iluminação da cidade e contratos B0–B3. |
| **Editor / Tools** | `Assets/Piaaria/Scripts/Editor/`, `Tests/Editor/` | Funcional / L2 pendente | Wizards Odin/Editor organizados sob `Tools/Scary Party/`, testes `OvernightExperienceTests.cs` e `ModularCityTests.cs`. |

---

## 2. O que Está Funcional
- **Marco M1 — Auditoria e Reparos de Base (Implementação Concluída — Validação Pendente):**
  - Revalidação de `A01–A12` e investigação dos 4 relatos principais documentadas em `Docs/Release/Reports/M1_Relatorio.md`, `Docs/Release/STATUS.md` e `Docs/Release/TEST_MATRIX.md`.
  - **A01 (Disparo semiautomático e trava pós-modal):** `WeaponBase.isAutomatic` (padrão `false`), `WeaponController.IsCurrentWeaponAutomatic`, disparo por borda (`_wasFirePressed`) e `_requireFireRelease` + limpeza de `aim`/`fire` em `WeaponInputHandler.cs`.
  - **A07 (Travessia indevida de NPCs):** `CustomerNpcView.cs` tenta reinserção no NavMesh (`NavMesh.SamplePosition` + `Warp`) antes de qualquer fallback e bloqueia movimento através de obstáculos via `Physics.Linecast`.
  - **A09 (Retorno seguro de utensílios ao ToolRack):** `PizzeriaState.ToolRackStationId` populado por `PizzeriaRoot`, utilizado em `NightCycleService.ClearTransientNightEntities` e `PlayerLifecycleBridge` sem colisão de slots; coberto em `OvernightExperienceTests.cs`.
- **Plano Noturno Revisão 2 (Fases 0–7 — código e estática compilados):**
  - Correções base `A01–A16` (separação servidor/cliente no lobby + `PizzeriaSceneLoadingCoordinator`, validação de posse do Host `clientId==0` no `TransferService`, barras de estação sem offset fixo, mesa/cadeira + animação discreta de sentar, fachada `J=4m/2 slots` + ocupação por intervalo, trânsito e cruzamentos sincronizados por `ServerTime`).
  - Ciclo de noites (`O5`, metas N1/N2/N3, derrota operacional por `NightEarnedRevenue < NightRevenueGoal` e retry por snapshot), quadro de comandas (`O7`), HUD e dicas contextuais (`O1/O2/O8`), menu de pausa + recuperação de personagem/moto (`O3/O20`), combate base + mochila danificável (`12B`) e moto lateral com buzina (`MOTO` + `O13`).
- **Correções de Sessão Relay/WSS, Animações Remotas, ESC & Otimização de Rede (NGO):**
  - `UIFeedbackService.Awake()` corrigido para verificar `audioSource == null` antes de `AddComponent<AudioSource>()` (evitando `MissingComponentException` em `MainGameUI`).
  - `UnityTransport` em `LobbyScene.unity`, `LobbyManager.cs` e `NetworkAutoStart.cs` configurado com `MaxPacketQueueSize >= 2048` e `MaxPayloadSize >= 32768`, eliminando desconexões `Receive queue is full (128)` sob Relay WSS.
  - `UIModalStack.cs` e `PauseEscapeMenuPresenter.cs` blindados contra fechamento no mesmo frame, perda de foco sem pilha, navegação ESC de subpainéis de volta ao menu principal e clique acidental em `"Sair da Partida"` (agora exige subpainel de confirmação e guarda de `0.15s`).
  - Animações remotas restauradas e filtradas (`ThirdPersonController` estimando locomoção remota + `MotionSpeed = 1f` em Idle + cache local, `OwnerNetworkAnimator` vinculando `CharacterVisualController.Animator` com `AlwaysAnimate` e desativando sync de parâmetros contínuos, `CustomerNpcView` medindo deslocamento em aproximação/fallback).
  - Redução de tráfego NGO: `PlayerArmature.prefab`/`PlayerState` com `UseUnreliableDeltas` + compressão half-float/quaternion e thresholds maiores; `ReplicationProjector`/`NetDtos`/`ProcessingService`/`PizzeriaRoot` com tolerâncias maiores e `PublishedRevision` condicionado a mudanças reais; `PlayerInteraction` heartbeats a `0.30s`; `TrafficManager` e `DeliveryMotorcycleController`/`PizzeriaCommandHandler` em RPCs `Unreliable` quantizados/sob delta; `DemandDirector` convertido para `MonoBehaviour`.
- **Ciclo da Pizzaria e Bancadas (Interação, Preparo e Montagem):**
  - **Validação de Alcance Multislot:** `PizzeriaCommandHandler.IsStationWithinReach` expandido para verificar a posição exata do slot alvo, todos os slot anchors e os colliders da bancada (via ponto mais próximo), eliminando rejeições indevidas de distância (`> 6.5m`) em mesas e balcões modulares longos.
  - **Desobstrução de Raycast e Mira de Bancada:** `PlayerInteraction.HandleRaycast` ajustado para não truncar `maxReach` quando o colisor atingido pertencer a um interagível ou posto de trabalho (`StationWorkpostTarget`), permitindo que tábuas e ingredientes sem colisor sejam detectados; `ResolveInteractableFromHits` flexibilizado para resolver o slot mais próximo (até 2m) em estações vinculadas a 3D em vez de bloquear com `blockedByBoundStationBody`.
  - **Redirecionamento Inteligente de Slots e Prompts:** `StationView.TryPickOrPlace`, `StationView.InteractPrompt` e `PrepStation.InteractPrompt` agora redirecionam ações de coleta `[E]` ou montagem `[F]` para slots ocupados da mesma bancada caso o cursor esteja ligeiramente deslocado sobre um slot vazio.
  - **Liberação Confiável do Bloqueio de Câmera [F]:** `PlayerInteraction.UpdateTrackedWork` detecta a remoção do slot de `StationSlots` no término do processo no servidor, liberando imediatamente o look lock e o estado de trabalho sem prender o jogador.
- **Geração da Cidade:** Construção procedural determinística através de `DefaultCityConfig.asset` e seed compartilhada em rede.
- **Minimapa:** Rastreamento dinâmico da câmera do jogador com culling mask apropriada.
- **Ferramentas de Menu:** Organização de itens de menu conforme padrão `unity-tools-menu-organization` (incluindo `Game Feel Studio` em `Wizards/Visuals`).
- **Sistema de Game Feel e Animações Cartoon (`Presentation/Feedback`, `Editor/GameFeel`, `UI/Piaria`):**
  - Arquitetura desacoplada da lógica de rede (`GameFeelTypes`, `GameFeelSettings`, `GameFeelProfile`, `GameFeelVisualPivot`, `GameFeelPresenter`, `GameFeelNetworkBridge`) com presets `Suave`/`Cartoon`/`Sloppy`/`Reduzido`, sincronização com `UIToolkitTweenService.ReduceMotion` e janela `Game Feel Studio`.
  - Pivôs cosméticos isolados em `ItemVisualWrapper` (`PosePivot -> CosmeticPivot -> VisualInstance`), animações por processo em `KitchenProcessEffects` e `PizzaVisualComposer`, proteção contra concorrência em `OvenTransferVisualizer` e pulso seguro de forno em `OvenStation`.
  - `PhoneRinger` e `TicketSheetView` migrados para `MaterialPropertyBlock`; `BackpackVisualizer` com `CosmeticPivot` + sway de carga; reações corporais seguras + LOD (`<= 20m`) em `CharacterVisualController`/`CustomerNpcView`; emissores 3D pooled em `PizzeriaSoundManager`; unificação de callbacks Pointer/Focus em `UIFeedbackService`.
- **Iluminação Cartoon e Estilos Visuais (Solução A + Contratos B0–B3):**
  - **Modos A/B/C:** `VisualStyleRuntimeConfig` separa rota (`Original` vs `CelShadedVariant`) de `VisualApplicationMode`; Modo A pré-visualiza em PlayMode sem alterar `GameScene.unity`, Modo B salva padrão com backup e rollback seletivo (`OriginalGameSceneStylePersistence`), Modo C gera variante protegendo alterações locais.
  - **Shaders Próprios e Troca ao Vivo:** `ScaryParty/Illustrate Stylized/*` (incluindo `IllustrateScreenSpaceOutline.shader` plane-invariante `1/Z`, normal cardinal e color edge sem conflito de `#pragma`/keywords) com Forward+ `_CLUSTER_LIGHT_LOOP` Additional Lights, faixas toon completas `[0..1]`, controle de contraste/teto em `StylizedFrag`, zonas (`_InteriorZoneFillColor`/`_Weight`), ganchos B1 e `_SP_DebugLightingView`.
  - **Dia/Noite, Pizzaria, Cidade e Rede:** `TimeOfDayLightingDirector` (17h/19h/22h/02h), `PizzeriaLightingController` (zonas e `LightShadows.None` em Point Lights), `CityLightingDirector` + `StylizedLightPool`, `NetworkTimeOfDayState` e `LightingReadyGate` (controle manual de horário consolidado na aba `Mundo e Iluminacao` do `PlaytestDebugController`).
- **Eventos, Loja, Balanceamento e Central de Playtest (Fases B0–B6 + B5.1 Revisada):**
  - **Fonte Única & Wizard Odin:** `RunBalancingCatalog.asset` (`RunBalancingCatalog`, `RunBalancingSnapshot`, `RunDescriptionFormatter`, `RunBalancingValidator`, `RunBalancingMigrationRunner`) e Wizard de 8 abas em `Tools/Scary Party/Wizards/Pizzaria/Eventos, Loja e Balanceamento`.
  - **Runtime, Preparação Pré-Noite 1 & UI Replicada:** Consumidores reais em `MonsterPopulationDirector` (`blood_moon`), `MonsterNpcController` (`vampire_attack`), `DemandDirector` (`heavy_rush`/paciência), suprimentos base explícitos `1/3/4` com rejeição por estoque cheio, `TargetNightNumber` + votação multi-estágio (`Hindrance`, `Opportunity`, `Event`) e DTOs replicados (`NetRunVoteOptionDto`, `NetRunShopOfferDto`) em `RunPreparationPresenter` / `OperationsHubPresenter`.
  - **Preset `NoiteDeTeste` e Central Odin no Inspector:** Preset selecionável `NoiteDeTeste.asset` (`UnlimitedShiftTime=true`, HUD `SEM LIMITE`, regras/timers não-relógio ativos); overlays `[F8]` e `[F6]` desativados no GameView; `[Debug] Central de Playtest` (`PlaytestDebugController` com 8 abas Odin) instalado em `GameScene.unity` e `GameScene_CelShaded.unity` (atalho `Tools/Scary Party/Uso Comum/Pizzaria/Selecionar Central de Playtest (Inspector)`), operando apenas para o Host em PlayMode no Editor em qualquer preset, com checkpoints `PrePreparation`/`NightStart`, `RebaseRelativeDeadlines`, expediente separado de iluminação e `IsForcedDebugOutcome`. Relatório completo em `Docs/Release/Reports/WIZARD_EVENTOS_LOJA_BALANCEAMENTO.md`.
- **Funcionários Contratados (Balconista/Pizzaiolo) e Porta do Balcão (Implementado — L2 pendente):**
  - `EmployeeAgentController` e `EmployeePopulationDirector` usando a mesma base de modelo/animações (`CharacterVisual` + `CharacterVisualController`), com navegação estrita no piso (`autoTraverseOffMeshLink = false`, amostragem radial no piso em `TrySampleFloorNavMesh` e `NavMeshObstacle` nas bancadas/equipamentos).
  - `CounterPassageDoorController` em `StationDoorPresenter.cs` / `CustomerSpawner.cs` abrindo e fechando automaticamente a portinhola do balcão (`abertura*`) quando qualquer jogador ou NPC contratado se aproxima.
  - Balconista (`TickCashier`) atendendo clientes e chamadas no `PhoneStation` com interação rápida seguida de cooldown decrescente por nível e barra 3D (`StationProgressBar` + `NetEmployeeDto.WorkStep`/`ActionProgressPercent`).
  - Pizzaiolo exibindo etapa e progresso acima da cabeça, com o pedido em andamento oculto do quadro (`TicketBoardStation`, `TicketBoardPresenter`) e inspecionável mirando no pizzaiolo e pressionando `[E]` (`IInteractable` -> `TicketModalPresenter`).

---

## 3. O que Está Parcial ou em Desenvolvimento
- **Estabilidade Multiplayer em Conexões Instáveis:** Requisitos de validação contínua para sincronização de estados de clientes que entram com a partida em andamento (late-joiners), agora incluindo `NetworkTimeOfDayState`.
- **Padronização Visual Cel-Shaded e Game Feel:** Código C#, shaders (`IllustrateStylizedCore.hlsl` e `IllustrateScreenSpaceOutline.shader`), sistema de Game Feel, perfis `.asset` e materiais calibrados e compilando em `dotnet build`; falta validação visual/tátil em PlayMode (incluindo *late-joiner* via ParrelSync) e execução dos testes EditMode/PlayMode no Unity Editor aberto (`Docs/GameFeel/Validation.md`).
- **Contratos Futuros (Solução B):** Interfaces e stubs B0–B3 (`LightingFeatureRegistry`, ganchos AO/Emissão B1, `FixedInteriorLightingData` B2) ativos com fallback seguro para Solução A; baking futuro fora deste escopo.
- **NPCs e Entregas:** Mecânica de entrega nas portas de clientes pela cidade em fase de polimento de rotas de IA.

---

## 4. Principais Pontos de Entrada
- **Cena Principal de Jogo:** `ScaryParty/Assets/ThirdParty/StarterAssets/ThirdPersonController/Scenes/GameScene.unity` (canônica com suporte a Modo A/B) e variante `ScaryParty/Assets/Piaaria/Scenes/GameplayVariants/GameScene_CelShaded.unity`.
- **Wizard de Estilos & Iluminação:** `Tools/Scary Party/Wizards/Visuals/Global Cel Shading Wizard` (`GlobalCelShadingWizard.cs`).
- **Gerador da Cidade:** `ScaryParty/Assets/Piaaria/Scripts/City/CityGenerator.cs`.
- **Prefab Central da Pizzaria:** `ScaryParty/Assets/Piaaria/Prefabs/Pizzeria/` (`PizzeriaModular_Manual.prefab` ativo no fluxo modular, `PizzeriaModular_CelShaded.prefab` variante cel-shaded e `PizzeriaPrefab.prefab` legado/fallback).
- **Configuração da Cidade:** `ScaryParty/Assets/Piaaria/ScriptableObjects/DefaultCityConfig.asset`.

---

## 5. Riscos Identificados e Cuidados
1. **Concorrência de Arquivos Unity:** O Editor da Unity não deve ser aberto concorrentemente via linha de comando (`-batchmode`), sob risco de travamento de locks em `Library/`.
2. **Preservação de GUIDs:** Alterações em prefabs e assets da pizzaria devem preservar rigorosamente os arquivos `.meta`.
3. **Multiplayer Local vs Remoto:** Validações de rede requerem execução de múltiplos clientes via ParrelSync (`ScaryParty_clone_0`), não apenas simulação no PlayMode local.

---

## 6. Links Úteis
- [Índice Geral de Documentação](INDEX.md)
- [Checklist da Pizzaria](Pizzeria/Checklist.md)
- [ADR-0001: Política de Execução Manual e Regras do Agente](Decisions/ADR-0001-politica-execucao-manual-e-agentes.md)
- [Auditoria de Headless](AgentOperations/HEADLESS_AUDIT.md)

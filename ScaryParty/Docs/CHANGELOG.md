# Registro de Mudanças — ScaryParty (CHANGELOG)

Este registro documenta mudanças relevantes na arquitetura, governança de agentes, código e assets.  
Para alterações rotineiras de código, consulte o histórico do Git (`git log`).

---

## [2026-11-17] — Recuperação e Integração M1–M8 (Etapas R0–R8) em `release/marcos-noturnos`
- **Escopo:** `Pizzeria/Domain/**`, `Pizzeria/Network/**`, `Pizzeria/Composition/PizzeriaRoot.cs`, `Pizzeria/Presentation/{EmployeeAgentController,RunPreparationPresenter,NightResultsPresenter,ContextualTutorialPresenter,CustomerNpcView}.cs`, `Player/{PlayerHealth,PlayerAllyHelpInteractable,FirstPersonCameraController,PlayerInteraction}.cs`, `Weapons/{WeaponController,WeaponInputHandler}.cs`, `Combat/Monsters/{MonsterNpcController,MonsterPopulationDirector}.cs`, `City/{TrafficManager,PedestrianNpc,PizzeriaParkingLotBuilder}.cs`, `UI/Piaria/Scripts/Runtime/Presenters/HudPresenter.cs`, `Editor/CharacterSetupWizard.cs`, `Tests/Editor/OvernightExperienceTests.cs`, `Docs/Release/*`, `Docs/{CURRENT_STATE,CHANGELOG}.md`.
- **Resumo:** Executadas as etapas **R0–R8** do plano `Docs/Release/RECUPERACAO_M1_M8.md` com commits locais por bloco funcional:
  - **R0:** correção de erros P0 de compilação e reclassificação honesta de status.
  - **R1:** estados coerentes `Alive`/`Incapacitated`/`Dead`, cura/reanimação 3s autoritativa (`PlayerAllyHelpInteractable`), cleanup completo de morte/desconexão e câmera espectador `[Q]`/`[E]`.
  - **R2:** contrato único de mãos, duas pistolas por `WeaponId` (`1`/`2`), bandoleira `[G]` com visual 3P e ADS `0.55x`, moto autoritativa (LOS/alcance + saída segura `CheckCapsule`).
  - **R3:** avaliação multi-pizza definitiva, receita/reputação unificadas (`NetNightStateDto`), bounds da pizzaria no servidor, fechamento atômico, piso de estoque/suprimentos e capacidade reduzida de mochila danificada.
  - **R4:** instância única de `RunProgressionService`, `NetRunStateDto`, `RunPreparationPresenter` (votação A/B, loja e `MARCAR PRONTO`), auto-desligamento do forno (`oven_timer`) e vitória da RUN na Noite 3.
  - **R5:** `MonsterPopulationDirector`, `SpeciesHostilityService` (ofensa por alho `IngredientDefId == 10`), `ZombieConversionService`, `Rigidbody` no tráfego e guardas NavMesh (`SamplePosition` + `CalculatePath`).
  - **R6:** correção de isolamento de ator/bloqueio em `EmployeeTaskService`, `NetEmployeeDto` e `EmployeeAgentController`/`EmployeePopulationDirector` (Balconista e Pizzaiolo autônomos com `HumanTakeoverTask`).
  - **R7:** tutorial `[H]` / Noite 0 opcional, trava de clique UI→arma, HUD operacional completo (Caixa, Reputação, Comandas, Equipe) e `CharacterSetupWizard` (`IsRidingBike`, layer `WeaponUpperBody`, preview multi-contexto).
  - **R8:** compilação estática limpa nos 4 `.csproj`, **33/33** testes pure `.NET` de domínio aprovados e documentação consolidada.
- **Validação:** Build estático (0 erros) + 33/33 testes pure `.NET` aprovados + `git diff --check` limpo + auditoria `.meta`. Validações no Unity Editor (EditMode/PlayMode/ParrelSync/Wizard) permanecem pendentes (`UNITY_EXECUTION=MANUAL_ONLY`).

---

## [2026-11-16] — Marcos M2–M8 na branch `release/marcos-noturnos`
- **Escopo:** `City/{TrafficVehicle,TrafficManager,CityGenerator}.cs`, `City/Editor/CityGraphPathfinderTests.cs`, `Weapons/{WeaponBase,WeaponController,WeaponInputHandler}.cs`, `Player/FirstPersonCameraController.cs`, `City/DeliveryMotorcycleController.cs`, `Pizzeria/Player/PlayerInventoryAdapter.cs`, `Pizzeria/Domain/Services/{NightCycleService,SpeciesHostilityService,RunProgressionService,EmployeeTaskService}.cs`, `Combat/Monsters/MonsterNpcController.cs`, `Pizzeria/Network/{NetDtos,PizzeriaNetworkState,PizzeriaCommandHandler}.cs`, `Tests/Editor/OvernightExperienceTests.cs`, `Docs/Release/*`.
- **Resumo:** Implementados em blocos sequenciais com commits locais: M2 (relay de tráfego no `CityGenerator`, sensores A11, colisão e overlay `F7`), M3 (bandoleira `[G]`, moto 1 mão + pistola, ADS 1P, `WeaponId` A08 e atropelamento idempotente), M4 (fechamento no relógio A06, reputação `0..100`, resgate aliado/espectador A04, preservação de munição A10 e suprimentos), M5 (`MonsterNpcController`, `SpeciesHostilityService` e `ZombieConversionService`), M6 (`RunProgressionService` com N noites, votos, modificadores, loja atômica e RNG separado), M7 (`EmployeeTaskService` com reservas exclusivas `T58` e erro por habilidade) e consolidação M8.
- **Validação:** `git diff --check` limpo em cada etapa; Test Runner EditMode e bateria PlayMode/ParrelSync pendentes no Unity Editor (`UNITY_EXECUTION=MANUAL_ONLY`).

---

## [2026-11-16] — Marco M1: Auditoria de Base, A01/A07/A09 e Matriz de Testes
- **Escopo:** `WeaponBase.cs`, `WeaponController.cs`, `WeaponInputHandler.cs`, `CustomerNpcView.cs`, `PizzeriaState.cs`, `PizzeriaRoot.cs`, `NightCycleService.cs`, `PlayerLifecycleBridge.cs`, `OvernightExperienceTests.cs`, `Docs/Release/{STATUS.md,TEST_MATRIX.md,Reports/M1_Relatorio.md}`, `Docs/{CURRENT_STATE.md,CHANGELOG.md}`.
- **Auditoria e Diagnósticos:**
  - Revalidados `A01–A12` na branch `main` (`a7cc598b`) e classificados os 60 casos `T01–T60` em `TEST_MATRIX.md` entre `NÃO EXECUTADO` e `LACUNA — M3..M7`.
  - **Ausência de mira (M3):** confirmada ausência de transições para `WeaponMode`/`IsAiming` em `CharacterAnimator.controller` e ausência de offset ADS/FOV em `FirstPersonCameraController`/`RightHandAnchor`.
  - **Carros imóveis (M2):** confirmada causa no cliente — `CityGenerator` cria `_cityRoot` sem `NetworkObject` e adiciona `TrafficManager : NetworkBehaviour` em runtime, mantendo `IsSpawned == false` e impedindo `SyncVehiclesClientRpc`.
- **Alterações Aplicadas:**
  - **A01:** disparo semiautomático por borda de pressionamento (`WeaponBase.isAutomatic`, `WeaponController.IsCurrentWeaponAutomatic`) e limpeza de `aim`/`fire` com trava `_requireFireRelease` ao travar input/cursor em `WeaponInputHandler.cs`.
  - **A07:** `CustomerNpcView.cs` substituído `MoveTowards` cego por `NavMesh.SamplePosition` + `Agent.Warp` e guarda `Physics.Linecast` para evitar travessia de paredes/móveis.
  - **A09:** adicionado `PizzeriaState.ToolRackStationId` (preenchido por `PizzeriaRoot`), aplicado em `NightCycleService.ClearTransientNightEntities` e `PlayerLifecycleBridge` com alocação sem colisão de slots, mais teste unitário em `OvernightExperienceTests.cs`.
- **Validação:** `git diff --check` limpo (0 erros). `dotnet build` CLI não concluído sem `Temp/obj` restaurado; Test Runner EditMode e bateria manual PlayMode/ParrelSync pendentes no Unity Editor aberto (`UNITY_EXECUTION=MANUAL_ONLY`).

---

## [2026-11-15] — Correção do Ciclo de Interação e Bancadas (Colocação, Coleta e Trabalho)
- **Escopo:** `PizzeriaCommandHandler.cs`, `PlayerInteraction.cs`, `StationView.cs`, `PrepStation.cs`, `Docs/CURRENT_STATE.md`, `Docs/CHANGELOG.md`.
- **Causa Raiz:** Quatro fatores combinados travavam o ciclo de bancadas: 1) `IsStationWithinReach` no servidor media distância apenas até o pivô central do GameObject da mesa, rejeitando jogadores em slots periféricos de bancadas longas (> 6.5m); 2) `HandleRaycast` limitava `maxReach` na malha da própria mesa, impedindo detecção de postos 3D (`StationWorkpostTarget`) e acionando `blockedByBoundStationBody`; 3) resolução rígida de slots não encontrava itens ao mirar ligeiramente fora do centro do slot; 4) `UpdateTrackedWork` aguardava fase `Completed` que nunca era vista porque o servidor remove o slot de `StationSlots` no frame de término, prendendo o look lock de câmera.
- **Alterações:**
  - `PizzeriaCommandHandler.cs`: `IsStationWithinReach` atualizado para aceitar `slotId` e testar posição do slot alvo, todos os slot anchors e colliders via `ClosestPoint`, eliminando falsos positivos de distância em estações modulares.
  - `PlayerInteraction.cs`: Raycast de obstáculos não trunca `maxReach` para interagíveis ou postos de trabalho; `ResolveInteractableFromHits` permite interação com a bancada se o ponto de impacto estiver a até 2m de um slot; `UpdateTrackedWork` detecta término do processo quando o slot é removido de `StationSlots` após ter rodado, liberando o bloqueio de câmera imediatamente; redirecionamento de slot em `ExecuteWorkInteraction` caso o slot mirado esteja vazio mas outro slot da mesma bancada contenha o item/pizza.
  - `StationView.cs`: `ResolveTargetSlotIndex` permite resolver o slot mais próximo (até 2m) mesmo em estações vinculadas a 3D; `TryPickOrPlace` redireciona coleta `[E]` ou montagem `[F]` para slots ocupados da mesma bancada caso o jogador mire em slot vazio adjacente; `InteractPrompt` atualizado para exibir `[E] Pegar` mesmo quando o slot sob o retículo estiver vazio mas outro contiver item.
  - `PrepStation.cs`: `InteractPrompt` atualizado com detecção de itens/ingredientes presentes em qualquer slot da bancada de preparo.
- **Validação:** L0/L1 estáticos concluídos (`git diff --check` limpo, `dotnet build` sem erros). Validação L2 manual detalhada para execução no Unity Editor aberto.

---

## [2026-11-15] — Correções de Sessão Relay/WSS, Animações Remotas, Menu ESC & Otimização de Rede NGO
- **Escopo:** `UIFeedbackService.cs`, `LobbyManager.cs`, `NetworkAutoStart.cs`, `LobbyScene.unity`, `UIModalStack.cs`, `PauseEscapeMenuPresenter.cs`, `CharacterVisualController.cs`, `OwnerNetworkAnimator.cs`, `PlayerArmature.prefab`, `ThirdPersonController.cs`, `CustomerNpcView.cs`, `PlayerState.cs`, `ReplicationProjector.cs`, `ProcessingService.cs`, `PizzeriaRoot.cs`, `NetDtos.cs`, `PlayerInteraction.cs`, `TrafficManager.cs`, `DeliveryMotorcycleController.cs`, `PizzeriaCommandHandler.cs`, `DemandDirector.cs`, `Docs/{CURRENT_STATE,CHANGELOG}.md`.
- **Alterações:**
  - **Áudio UI (`MainGameUI`):** substituição de `??` sobre `UnityEngine.Object` por checagem explícita (`== null`) em `UIFeedbackService.Awake()` antes de `AddComponent<AudioSource>()`.
  - **Fila de pacotes / Relay WSS:** elevação de `MaxPacketQueueSize` (>= `2048`) e `MaxPayloadSize` (>= `32768`) em `LobbyScene.unity`, `LobbyManager.cs` e `NetworkAutoStart.cs`.
  - **Fluxo do ESC / Menu de Pausa:** guarda de mesmo frame em `UIModalStack.PushModal`, subpainel de confirmação com guarda de `0.15s` para `"Sair da Partida"`, volta ao menu principal via ESC nos subpainéis (`CanCloseWithEscape` computado) e fallback quando `!_pushedToStack` em `PauseEscapeMenuPresenter.cs`.
  - **Animações remotas e NPCs:** `AnimatorCullingMode.AlwaysAnimate` em `CharacterVisualController`/`OwnerNetworkAnimator`/`ThirdPersonController`; filtragem via prefab e reflexão em `OwnerNetworkAnimator` desativando sincronização de parâmetros contínuos; estimativa local de velocidade/estado em clientes (`ThirdPersonController.UpdateRemoteVisuals`) e `MotionSpeed = 1f` quando bloqueado; medição real de deslocamento horizontal por frame em `CustomerNpcView.Update()`.
  - **Redução de tráfego NGO:** `UseUnreliableDeltas`, compressão quaternion/half-float e thresholds maiores em `PlayerArmature.prefab`/`PlayerState`; remoção da comparação incondicional de `Revision` em `NetItemDto.Equals`/`ProjectCallsDelta`, `PublishedRevision` atualizado apenas sob mudança real e tolerâncias maiores em `NetDtos`/`ProcessingService`/`PizzeriaRoot`; heartbeats de trabalho/atendimento a `0.30s`; `TrafficManager` (`0.25s`, RPC `Unreliable` só com clientes remotos, yaw `short` + `RGBA byte`) e `DeliveryMotorcycleController`/`PizzeriaCommandHandler` (`0.08s`, envio `Unreliable` por delta de movimento/rotação); conversão de `DemandDirector` para `MonoBehaviour`.
- **Validação:** `git diff --check` limpo nos arquivos editados (preservando `.meta` e alterações prévias) e `dotnet build --no-restore -m:1` sequencial nos 4 `.csproj` com 0 erros (`Domain=0`, `Runtime=0`, `Editor=0`, `Tests=0`). Validação manual PlayMode/ParrelSync no Unity Editor pendente (`UNITY_EXECUTION=MANUAL_ONLY`).

---

## [2026-11-15] — Plano Noturno de Experiência Completa (Revisão 2 — Fases 0–7)
- **Escopo:** `Pizzeria/Domain/*`, `Pizzeria/Network/*`, `Pizzeria/Stations/*`, `Pizzeria/Presentation/*`, `Pizzeria/Player/*`, `Pizzeria/Composition/*`, `Pizzeria/Authoring/*`, `City/*`, `City/Modular/*`, `Player/*`, `Weapons/*`, `UI/Piaria/Scripts/Runtime/Presenters/*`, `Tests/Editor/OvernightExperienceTests.cs`, `Docs/Experience/*`.
- **Alterações:**
  - **Base (`A01–A16` + cadeira):** separação estrita servidor/cliente na entrada do lobby + `PizzeriaSceneLoadingCoordinator`; validação autoritativa de posse nos RPCs e `TransferService` preservando Host `clientId == 0`; âncora dedicada para `StationProgressBar`; associação mesa/cadeira com separação de `PlateAnchor`/`NavMeshObstacle` em espaço local vs mundo e animação discreta de sentar em `CustomerNpcView`; porta dupla modular `J = 4m / 2 slots` + ocupação contínua por intervalo e segunda passagem de perímetro; trânsito e cruzamentos por grupos A/B determinísticos, stop-lines e IDs estáveis.
  - **Experiência aprovada (`O1/O2/O3/O5/O7/O8/O20`):** ciclo de noites (`NightCycleService`, `NightConfig`, `NightResultsPresenter`, derrota operacional exclusivamente por receita da noite vs meta + retry via `NightStartSnapshot`), quadro de comandas (`ToggleClaimOrderServerRpc`), HUD de turno/vida/munição, dicas contextuais (`ContextualTutorialPresenter`) e menu ESC com recuperação segura.
  - **Combate base & mochila danificável (`12B`):** rack de armas (Pistola 1 mão, Escopeta 2 mãos `10x8`), `PlayerHealth` (`100 HP`, auto-resgate `10s`), `PracticeTargetDummy`, ruptura única da mochila derrubando no máximo 1 última caixa em `LocationType.World` e reparo `[F]` de `3s`.
  - **Pátio, Moto & Buzina (`MOTO` + `O13`):** `PizzeriaParkingLotBuilder`, `DeliveryMotorcycleController` restrito às vias/pátio com hands-free check no servidor e buzina `[H]` com pulso visual e recuo em `PedestrianNpc`.
  - **Testes & Docs:** criação de `OvernightExperienceTests.cs` e documentação em `Docs/Experience/*`.
- **Validação:** `dotnet build` sequencial nos 4 `.csproj` (`Domain` 0/0, `Assembly-CSharp` 0 erros, `Assembly-CSharp-Editor` 0 erros, `Tests` 0/0) e `git diff --check` limpo. Testes Unity Test Runner, foco do Editor para gerar `.meta` dos novos scripts, conferência visual em `GameScene.unity` e ParrelSync pendentes (`UNITY_EXECUTION=MANUAL_ONLY`).
- **Risco:** Cena binária `GameScene.unity` não foi editada diretamente; ajustes finos de prefab/override visuais no PlayMode e validação física do lote lateral devem ser confirmados no Editor aberto ([Docs/Experience/Validation.md](Experience/Validation.md)).

---

## [2026-11-14] — Sistema Completo de Game Feel Cartoon, Animações Táteis, Audio 3D e Studio Wizard (Fases 0–6)
- **Escopo:** `Presentation/Feedback/*`, `Pizzeria/Presentation/*`, `Pizzeria/Visual/PizzaVisualComposer.cs`, `Pizzeria/Stations/{OvenStation,PhoneRinger,TicketSheetView,StationView}.cs`, `Player/{PlayerInteraction,FirstPersonCameraController,CharacterVisualController}.cs`, `UI/Piaria/Scripts/Runtime/Core/{UIToolkitTweenService,UIFeedbackService}.cs`, `Editor/GameFeel/GameFeelStudioWindow.cs`, `Pizzeria/Editor/Tests/GameFeelSystemTests.cs`, `Docs/GameFeel/*`, `Docs/{CURRENT_STATE,PizzeriaSetup,INDEX}.md`.
- **Alterações:**
  - Correções base (UI Toolkit tweens rastreados via `ScaleTo`/`PulseScale`, callbacks `Pointer*`/`Focus*` unificados sem duplicidade de `MouseEnter`, remoção do `#if MOREMOUNTAINS_FEEL` inativo e correção de caminhos de prefabs na documentação).
  - Criação do núcleo de Game Feel (`GameFeelPresenter`, `GameFeelNetworkBridge` com snapshot inicial silencioso para *late-joiners*, `GameFeelProfile` com receitas A–D, `GameFeelSettings` com clamps/presets/ReduceMotion e `GameFeelVisualPivot` em pivô cosmético isolado).
  - Enriquecimento tátil de bancadas/processos (`KitchenProcessEffects` com pool e fatias decorativas sem collider, `PizzaVisualComposer`, `OvenTransferVisualizer` com guarda anti-concorrência, `OvenStation`, `PhoneRinger`/`TicketSheetView` via `MaterialPropertyBlock`, `BackpackVisualizer` com sway de carga, `CharacterVisualController`/`CustomerNpcView` com LOD `<=20m` e `PizzeriaSoundManager` 3D pooled).
  - Criação do Wizard `Tools/Scary Party/Wizards/Visuals/Game Feel Studio` (8 abas + rollback seletivo) e suíte `GameFeelSystemTests.cs`.
- **Validação:** Validações estáticas `dotnet build` e `git diff --check` executadas sem tocar cenas/prefabs binários; execução EditMode Test Runner (`GameFeelSystemTests`) e testes PlayMode/ParrelSync pendentes no Editor aberto (`UNITY_EXECUTION=MANUAL_ONLY`).
- **Risco:** Baixo; comportamento autoritativo, colisões, câmera FPS e `NavMeshAgent` preservados intactos com clamps de escala e desligamento completo via `ReduceMotion`.

---

## [2026-11-14] — Espessura de Outlines e Utilitário de Troca de Horas em Play Mode
- **Escopo:** `Shaders/IllustrateScreenSpaceOutline.shader`, `VisualStyles/Runtime/TimeOfDayTestOverlay.cs`, `VisualStyles/Runtime/TimeOfDayLightingDirector.cs`, `VisualStyles/Runtime/VisualStyleProfile.cs`, `VisualStyles/Editor/GlobalCelShadingWizard.cs`, `Art/VisualStyles/Profiles/*.asset`, `Art/VisualStyles/Generated/*/Render/*_ScreenSpaceOutline.mat`.
- **Alterações:**
  - Aumento da espessura base de contorno (`_Width` e `screenSpaceWidth` elevados de 1.15–1.25 para 1.65–1.85 nos presets/materiais) com suporte a amostragem diagonal contínua e teto de `stepPx` expandido para 5.0 para permitir ajuste manual suave no Inspector do perfil (`Screen Space Width`).
  - Criação do utilitário in-game `TimeOfDayTestOverlay` (`F6` ou botão na tela), acoplado automaticamente ao `TimeOfDayLightingDirector` no Play Mode, com slider contínuo de 0h a 24h, 4 botões rápidos para as âncoras (17h Tarde, 19h Crepúsculo, 22h Noite, 02h Madrugada), controle de pausa e velocidades (0.5x, 1x, 5x, 20x).
  - Abertura automática da seção de ciclo dia/noite no `GlobalCelShadingWizard` durante Play Mode.
- **Validação:** `dotnet build` com 0 erros nos 4 `.csproj` e `git diff --check` limpo; teste interativo pendente no Editor aberto (`UNITY_EXECUTION=MANUAL_ONLY`).
- **Risco:** Mínimo; ativos canônicos preservados e utilitário roda inteiramente desacoplado de código de gameplay.

---

## [2026-11-14] — Correção de Compilação/Visibilidade da Outline, Contraste Toon e Reaplicação de Materiais
- **Escopo:** `Shaders/IllustrateScreenSpaceOutline.shader`, `Shaders/IllustrateStylizedCore.hlsl`, `VisualStyles/Runtime`, `Art/VisualStyles/Profiles/*.asset`, `Art/VisualStyles/Generated/*/Render/*_ScreenSpaceOutline.mat`, `VisualStyles/Tests/Editor`.
- **Alterações:**
  - Correção do erro D3D11 em `IllustrateScreenSpaceOutline.shader` (`redefinition of '_USEDEPTHTEST'`), removendo `#pragma shader_feature_local` conflitantes, aplicando `#undef` preventivo e trocando toggles para `[ToggleUI]`, com amostragem cardinal (`1/Z` plane-invariante, normal cardinal e color edge em `Dispatch`/`SoftToon`/`Comic`).
  - Refino de contraste em `IllustrateStylizedCore.hlsl`: buckets completos `[0..1]` em `ComputeToonBandFactor`, suavização restrita a `mainShadow` dentro da pizzaria mantendo `ndotl` 3D nítido, eliminação da dupla contagem de luz indireta/piso no estado sombreado/teto e soft-knee acima de `1.05`.
  - Suporte em `VisualMaterialLibrary` e `VisualMaterialResolver` para reconhecer e preservar materiais já estilizados (`Mat_VS_*`) ao reaplicar estilo em PlayMode, além de desabilitar keywords legadas em `VisualStyleShaderUtility.ApplyOutlineSettingsToPassMaterial`.
  - Atualização de presets (`VisualStyleProfile`, 6 `.asset`), dos 4 materiais `*_ScreenSpaceOutline.mat` (`m_ValidKeywords: []` e thresholds calibrados) e do teste em `VisualStyleLightingAndOriginalSceneTests.cs`.
- **Validação:** `dotnet build` sequencial nos 4 `.csproj` e `git diff --check` executados; testes Unity e conferência visual no PlayMode pendentes no Editor aberto (`UNITY_EXECUTION=MANUAL_ONLY`).
- **Risco:** Ativos canônicos preservados; recomenda-se regerar ou reabrir o Editor para recompilar o shader corrigido antes do PlayMode.

---

## [2026-11-14] — Outline Plane-Invariante, Calibração de Iluminação Interna/Rua e Consistência em Interações
- **Escopo:** `Shaders/IllustrateScreenSpaceOutline.shader`, `Shaders/IllustrateStylizedCore.hlsl`, `VisualStyles/Runtime`, `VisualStyles/Editor`, `Art/VisualStyles/Profiles/*.asset`, `Art/VisualStyles/Generated/*/Render/*_ScreenSpaceOutline.mat`, `VisualStyles/Tests/Editor`.
- **Alterações:**
  - Criação do shader `ScaryParty/Illustrate Stylized/ScreenSpaceOutline` (`IllustrateScreenSpaceOutline.shader`) usando URP `Blit.hlsl`, segunda diferença de profundidade `1/Z` plane-invariante e detecção de crease/normais com atenuação em ângulo rasante (`ndotv`), eliminando manchas escuras em pisos, tetos, ruas e personagens.
  - Refino em `IllustrateStylizedCore.hlsl`: remoção de posterização em `EvaluateAdditionalToonLight` (fim dos anéis concêntricos nas paredes), transição suave da luz direcional dentro de `_InteriorZoneWeight`, elevação do multiplicador mínimo de sombra (`0.45 -> 0.78`) e compressão soft-knee acima de `1.15`.
  - Atualização de `VisualStyleShaderUtility`, `VisualStyleProfile`, `TimeOfDayProfile`, `VisualStyleManager` e `VisualStyleUrpOutlineConfigurator`: sanitização automática de `_Depth_Threshold < 1.5 -> 5.4`, desativação de `suppressOutlineOnInteractionFocus` e `useColorTest` nos presets, serialização explícita dos pesos A2 nos 6 perfis `.asset` e atualização dos 3 materiais `*_ScreenSpaceOutline.mat`.
  - Atualização de `VisualStylePhase5To11Tests.cs` e inclusão de teste de calibração/sanitização em `VisualStyleLightingAndOriginalSceneTests.cs`.
- **Validação:** Verificação de unicidade de GUID, `dotnet build` e `git diff --check` executados após edições; testes EditMode/PlayMode e inspeção visual em PlayMode pendentes no Unity Editor aberto (`UNITY_EXECUTION=MANUAL_ONLY`).
- **Risco:** Ativos canônicos (`GameScene.unity`, `PC_RPAsset.asset`, `PC_Renderer.asset`) permanecem intocados; caso exista material gerado antigo com `Shader Graphs/Outline Effect`, `VisualStyleManager` e `VisualStyleUrpOutlineConfigurator` atualizam ou reescrevem os thresholds no runtime/regeneração.

---

## [2026-11-14] — Iluminação Cartoon Integrada, Modos A/B/C na GameScene Original e Contratos B0–B3
- **Escopo:** `VisualStyles/Runtime`, `VisualStyles/Editor`, `Lighting/`, `Lighting/Editor/`, `Shaders/Illustrate*Stylized*`, `Pizzeria/`, `Player/`.
- **Alterações:**
  - Separação entre rota de cena (`GameplaySceneRoute`) e aplicação visual (`VisualApplicationMode`) em `VisualStyleRuntimeConfig`, com Modos A (`VisualStyleGameplayBootstrap`), B (`OriginalGameSceneStylePersistence`) e C (`VisualStyleGameplayIntegrator`), além de troca de estilos ao vivo (`SetStyle`).
  - Criação dos shaders `ScaryParty/Illustrate Stylized/*` (`IllustrateStylizedCore.hlsl`) com suporte Forward+ Additional Lights, piso de luminosidade/sombra indireta e `_SP_DebugLightingView`.
  - Implementação de `TimeOfDayLightingDirector` + `TimeOfDayProfile` (17h/19h/22h/02h), `PizzeriaLightingController` (zonas e remoção de sombra em Point Lights), `CityLightingDirector` + `StylizedLightPool` sem sombra, `NetworkTimeOfDayState`, `LightingReadyGate`, `LightingQualityBudget` (atlas URP 2048) e contratos B0–B3 (`LightingFeatureRegistry`, `FixedInteriorLightingData`).
- **Validação:** `dotnet build` com 0 erros nas 4 assemblies (`VisualStyles.Runtime`, `Assembly-CSharp`, `Assembly-CSharp-Editor`, `PlayModeTests`); validação estática L0/L1 sem violações de `[MenuItem]` ou `UnityEditor` no runtime. Execução visual e suíte Unity Test Runner pendentes no Editor (`UNITY_EXECUTION=MANUAL_ONLY`).
- **Risco:** Materiais `.mat` gerados previamente exigem re-seed/regeneração pelo Wizard no Unity Editor para vincular os novos shaders e presets de ciclo Dia/Noite; ativos canônicos (`GameScene.unity`, `PC_RPAsset.asset`, `PC_Renderer.asset`, Distant Lands e prefabs originais) foram preservados intactos.

---

## [2026-11-14] — Implantação da Política de Agentes, Regras Modulares e Documentação Econômica
- **Escopo:** Configuração de governança de agentes, sistema de regras e documentação navegável.
- **Alterações:**
  - Criação do contrato principal em `AGENTS.md` e ponteiro em `ScaryParty/AGENTS.md`.
  - Configuração de regras condicionais em `.agents/rules/` (`unity-csharp.md`, `unity-assets.md`, `unity-networking.md`, `unity-procedural-ui.md`).
  - Criação de skills modulares em `.agents/skills/` (`change-safely`, `targeted-validation`, `update-project-docs`, `debug-unity-logs`).
  - Estruturação do hub de documentação em `ScaryParty/Docs/` (`INDEX.md`, `CURRENT_STATE.md`, `CHANGELOG.md`, `Decisions/`, `Changes/`, `Architecture/`).
  - Registro da política `UNITY_EXECUTION=MANUAL_ONLY` em `AgentOperations/HEADLESS_AUDIT.md` e linha de base em `COST_BASELINE.md`.
  - Criação do utilitário de validação estática leve `Tools/AgentChecks/quick-check.ps1`.
  - Atualização do `.gitignore` para ignorar logs temporários como `UnityBatchLog.txt`.
- **Validação:** Inspeção estática de sintaxe dos arquivos markdown, regras e scripts. Unity NÃO foi executada (política estrita L0/L1).
- **Risco:** O usuário deve manter as permissões do Antigravity configuradas adequadamente conforme preferência (TURBO ativo para desenvolvimento ágil, com política documental para operações custosas).
- **Referência:** [Plano_Antigravity_Unity_ScaryParty.md](../../Plano_Antigravity_Unity_ScaryParty.md) e [HEADLESS_AUDIT.md](AgentOperations/HEADLESS_AUDIT.md).

---

## [2026-11-14] — Materiais Cel Shaded (Branch feature/global-cel-shading-wizard)
- **Escopo:** Pipeline visual de Cel Shading / Toon Shader no URP.
- **Alterações:** Geração de novos materiais e variantes de cena para visual estilizado (`GameScene_CelShaded.unity`).
- **Validação:** Visual no Unity Editor pelo desenvolvedor.
- **Status:** Em desenvolvimento e validação visual no Editor.

---

## [2026-10-26] — Sincronização da Cidade, Spawn do Player e Minimapa
- **Escopo:** `CityGenerator`, `MinimapRouteManager` e ponto de spawn de rede.
- **Alterações:**
  - `NetworkVariable<int> _networkSeed` para persistência de seed em late-joiners.
  - Correção de posicionamento do `NetworkSpawnPoint` na rua em frente à pizzaria.
  - Correção do minimapa com posicionamento dinâmico da câmera do minimapa.
- **Validação:** Testes em PlayMode e conexões multiplayer de rede.
- **Referência:** Commit `466c067` e anteriores.

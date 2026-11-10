# VALIDATION — Matriz de Validação e Aceite

| Teste | Critério | Status | Evidência |
|---|---|---|---|
| F1.1 | IDs de estações únicos e consistentes | Pass | 0 duplicatas. IDs únicos atribuídos e verificados via `PizzeriaGameplayValidator` (IDs 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21). |
| F1.2 | Slot nulo corrigido no prefab | Pass | `mesa media.002` (AssemblyStation ID 13) reduzida de 5 para 4 slots válidos, sem referências `{fileID: 0}` e altura Y normalizada para 0.4950 m. |
| F1.3 | Quadros de tickets coplanares e orientados | Pass | `Quadro de Comandas` físico (ID 4) vinculado à cozinha com 6 slots coplanares; `cardapio.001` (ID 15) normalizado com 6 slots coplanares (local Z = 0.06f). |
| F1.4 | Ferramenta Odin de auditoria/reparo idempotente | Pass | `PizzeriaContentWizard.cs` expandido com abas Odin "Auditoria e Reparo" e "Estações e Slots"; `PizzeriaPhase1Repair.Repair` executado via batchmode com retorno code 0 e 0 erros bloqueantes. |
| F2.1 | Navegação NPC sem falso sucesso / recursão | Pass | `CustomerNpcView.cs` valida `hasPath`, `!pathPending` e `pathStatus == PathComplete`; `PedestrianNpc.HandleNavigationFailed` com contador máximo de 3 retries e fallback seguro. |
| F2.2 | Máquina de estados sentar/levantar/sair completa | Pass | NPCs preservam locomoção até o assento, transicionam formalmente para `SeatedWaitingFood` com desativação coordenada de NavMesh e acionam `SitDown`/`SitIdle` no `CharacterAnimator`. |
| F2.3 | Preservação visual pedestre -> cliente -> pedestre | Pass | `DemandDirector.cs` preserva paleta de cores e identidade visual (`VisualColorIndex`) ao converter pedestre em cliente presencial. |
| F2.4 | Limite populacional e reset limpo de pool | Pass | `CityPopulationDirector.cs` respeita estritamente `maxActivePedestrians` e redefine estado ao devolver pedestres ao pool. |
| F3.1 | Compositor modular de pizza (Sliced, Total, Central, Specific, Ring, Scatter) | Pass | Implementado em `PizzaVisualComposer.cs` e `PizzaLayoutGenerator.cs` com 7 modos de posicionamento e suporte a micro-offset anti-z-fighting. |
| F3.2 | DTO de rede com suporte a lista completa de ingredientes, estágios e porções | Pass | `NetItemDto` e `NetIngredientDto` suportam até 8 ingredientes simultâneos com preservação de porções e retrocompatibilidade com máscara de bits. |
| F3.3 | Wizard Odin e Janela de Prévia ao Vivo sincronizados | Pass | `PizzaVisualPreviewWindow.cs` oferece 4 abas, rotação 3D orbital, sliders de cozimento cru/assado/queimado e visualização de ingredientes em isolamento. |
| F4.1 | Coordenador de loading entre cenas com transição assíncrona segura | Pass | `PizzeriaSceneLoadingCoordinator.cs` com overlay em UI Toolkit, barra de progresso, dicas de gameplay e token lock via `GameplayInputGate`. |
| F4.2 | NavMesh pronta antes de spawn de jogadores e NPCs | Pass | `PizzeriaSceneLoadingCoordinator` aguarda explicitamente prontidão de `NavigationWorldManager.Instance.IsReady` antes de liberar gameplay. |
| F5.1 | Serviço unificado de feedback (Feel + DOTween) sem conflitos | Pass | `PizzeriaFeedbackService.cs` implementa micro-punchs de escala/posição desacoplados, áudio com cooldown e respeito à opção `ReduceMotion`. |
| F5.2 | Matriz de feedback aplicada aos fluxos do jogo | Pass | Efeitos sonoros e visuais vinculados a montagem, corte, forno, embalagem e comemoração de entregas em `DeliveryPointView`. |
| F6.1 | Otimizações de CPU/GC e redução de overhead da cidade | Pass | Registro $O(1)$ de mesas em `DiningTableStation.cs`, eliminação de varreduras repetitivas `FindObjectsByType` e ferramenta de limpeza `PizzeriaCitySceneCleaner.cs`. |
| F7.1 | Build Standalone sem dependências de Editor | Pass | Todas as classes editoriais encapsuladas em pastas Editor e blocos `#if UNITY_EDITOR`. |
| F7.2 | Smoke test e validação de regressões | Pass | Suíte de testes unitários `PizzaModularComposerTests.cs` aprovada com 4/4 testes passando sem falhas. |

# Estado Atual do Projeto — ScaryParty

**Última Revisão:** 14/11/2026  
**Versão Unity:** Unity 6000.3.6f1  
**Branch Atual:** `feature/global-cel-shading-wizard`  
**Commit Base:** `466c067`  
**Pipeline de Renderização:** Universal Render Pipeline (URP) com Cel Shading / Toon Shader

---

## 1. Visão Geral dos Módulos

| Módulo | Caminho Principal | Status | Descrição Sumária |
|---|---|---|---|
| **Pizzeria** | `Assets/Piaaria/Scripts/Pizzeria/` | Funcional / Refinamento | Bancadas de preparo, montagem de massas/ingredientes, forno e entrega de pedidos. |
| **City** | `Assets/Piaaria/Scripts/City/` | Funcional / Ajustes | Geração procedural determinística por seed, blocos de rua, posicionamento de prédios e calçadas. |
| **Navigation** | `Assets/Piaaria/Scripts/Navigation/` | Funcional | Geração dinâmica de NavMesh para NPCs e clientes em rotas urbanas. |
| **Network (NGO)** | `Assets/Piaaria/Scripts/Network/` | Parcial | Sincronização via Netcode for GameObjects. Suporte a sincronização de seed para late-joiners. |
| **Player** | `Assets/Piaaria/Scripts/Player/` | Funcional | Controle em terceira/primeira pessoa, inventário e interação com bancadas. |
| **UI / Minimap** | `Assets/Piaaria/Scripts/UI/` | Funcional | Rastreamento do jogador via `MinimapRouteManager`, interface de pedidos e HUD. |
| **VisualStyles / Lighting** | `Assets/Piaaria/Scripts/VisualStyles/`, `Scripts/Lighting/`, `Shaders/` | Implementado (validação manual pendente) | Modos A/B/C na `GameScene` original, shaders próprios `ScaryParty/Illustrate Stylized/*`, ciclo Dia/Noite, zonas da pizzaria, iluminação da cidade e contratos B0–B3. |
| **Editor / Tools** | `Assets/Piaaria/Scripts/Editor/` | Funcional | Wizards Odin/Editor organizados sob `Tools/Scary Party/` (wizards, setup, auditoria). |

---

## 2. O que Está Funcional
- **Ciclo da Pizzaria:** Interação do jogador com estações de corte, molho, queijo e forno.
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
  - **Dia/Noite, Pizzaria, Cidade e Rede:** `TimeOfDayLightingDirector` (17h/19h/22h/02h) com utilitário in-game de teste `TimeOfDayTestOverlay` (`F6` ou HUD interativo em Play Mode), `PizzeriaLightingController` (zonas e `LightShadows.None` em Point Lights), `CityLightingDirector` + `StylizedLightPool`, `NetworkTimeOfDayState` e `LightingReadyGate`.

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

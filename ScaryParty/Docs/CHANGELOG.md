# Registro de Mudanças — ScaryParty (CHANGELOG)

Este registro documenta mudanças relevantes na arquitetura, governança de agentes, código e assets.  
Para alterações rotineiras de código, consulte o histórico do Git (`git log`).

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

# N2-01 — Auditoria do Repositório, Inventário de Assets e Contrato de Intermissão (2.1)

**Data:** 18/11/2026  
**Branch auditada:** `release/marcos-noturnos` (HEAD `08365475`)  
**Plano de referência:** `ScaryParty/Docs/Release/PLANO_PREPARACAO_NOITES_2_1_UI_TOOLKIT_FEEL_DOTWEEN.md`  
**Status desta entrega:** Documentação e contrato de fase concluídos; nenhuma alteração de gameplay aplicada nesta etapa.

---

## 1. Estado Atual do Repositório e Modificações Pré-Existentes

Na abertura da implementação, a working tree da branch `release/marcos-noturnos` possui alterações locais não commitadas (preservadas integralmente sem reversão) relacionadas a funcionários contratados, portas do balcão e cenas (`EmployeeAgentController.cs`, `StationDoorPresenter.cs`, `CustomerSpawner.cs`, `GameplayInputGate.cs`, `RunPreparationPresenter.cs`, `OperationsHubPresenter.cs`, `NetDtos.cs`, `GameScene.unity`, `PizzeriaModular_*.prefab`, `CURRENT_STATE.md`, `CHANGELOG.md`).

Todos os commits `N2-01`..`N2-10` serão aplicados com `git add` explícito por arquivo da respectiva etapa, preservando integralmente o trabalho pré-existente.

### Fronteiras de Assembly Verificadas
- `ScaryParty.Pizzeria.Domain` (`Assets/Piaaria/Scripts/Pizzeria/Domain/ScaryParty.Pizzeria.Domain.asmdef`): possui `"noEngineReferences": true`. Toda lógica pura de catálogo, sorteio ponderado (`RunBallotResolver`) e máquina de estados (`RunProgressionService`) permanece livre de `UnityEngine`, `Unity.Netcode`, `DG.Tweening` ou `Sirenix.OdinInspector`.
- `ScaryParty.Pizzeria.Tests` (`Assets/Piaaria/Tests/Editor/ScaryParty.Pizzeria.Tests.asmdef`): suíte NUnit EditMode de domínio.
- `Assembly-CSharp` / `Assembly-CSharp-Editor`: camada Netcode (`Scripts/Pizzeria/Network`), UI Toolkit (`UI/Piaria/`), Feel (`MoreMountains.Feedbacks`), DOTween Pro (`DG.Tweening`) e Wizards Odin (`Scripts/Pizzeria/Editor`).
- Observação de build: `*.csproj` é ignorado no `.gitignore` e mantém lista explícita de `<Compile Include>`. Novos `.cs` devem ser compilados via `dotnet build` com target auxiliar temporário ou após atualização do Unity Editor sem tocar os `.csproj` no git.

---

## 2. Auditoria dos Sistemas Existentes e Plano de Evolução

| Sistema / Arquivo | Constatação na Branch | Evolução Prevista (`N2-02`..`N2-10`) |
|---|---|---|
| `Assets/Piaaria/Scripts/Pizzeria/Domain/Services/RunProgressionService.cs` | Já evoluiu na fase B4 para `RunVoteStageState` multi-estágio (`OpenPreparationStages`, `ResolveAllStagesServer`), mas resolve vencedor por maior contagem de votos + desempate simples (`ChoiceRng.Next`), e `ResolveAndStartPreparedNightIfReadyServer` inicia a noite imediatamente. | Adicionar `OpenTypedBallots(targetNight, 3, 3)`, `RunBallotResolver` ponderado por bilhetes sem viés de maior votação, obrigação de 2 votos (`Hindrance` + `Opportunity`) para `Ready`, congelamento em lock e estágios temporizados (`Revealing` -> `Countdown` -> `Starting`). Preservar `OpenChoiceVote`/`OpenPreparationStages` para compatibilidade dos testes `T01`–`T36` e `OvernightExperienceTests`. |
| `Assets/Piaaria/Scripts/Pizzeria/Domain/Definitions/RunBalancingSnapshot.cs` & `RunBalancingCatalog.asset` | `Hindrance` possui 3 itens primários (`vampire_attack`, `heavy_rush`, `impatient_customers`) + 1 reserva (`slow_supply`). `Opportunity` possui apenas 2 primários (`express_supply`, `calm_customers`) + 1 reserva (`quiet_night`). `blood_moon` está em `Event`. | Promover `slow_supply` e `quiet_night` para elegíveis normais e adicionar `blood_surge` (`Hindrance`: população vampírica +40% sem quebrar `blood_moon` nos testes B0–B6), `calm_shift` (`Opportunity`: ritmo de pedidos -20%) e `focused_staff` (`Opportunity`: erro relativo da equipe -30%). |
| `Assets/Piaaria/Scripts/Pizzeria/Network/NetDtos.cs`, `PizzeriaNetworkState.cs` & `ReplicationProjector.cs` | Possui `NetRunStateDto`, `NetRunVoteOptionDto` e `NetRunShopOfferDto`, porém falta estágio da intermissão (`Revealing`/`Countdown`), probabilidades congeladas por opção, vencedores das 2 categorias, timestamps do servidor e lista de prontidão/votos individuais por `clientId`. | Estender `NetRunStateDto` e `NetRunVoteOptionDto` e adicionar `NetworkList<NetPlayerIntermissionDto> RunPlayerStates` em `PizzeriaNetworkState`, projetados por `ReplicationProjector`. |
| `Assets/Piaaria/Scripts/Pizzeria/Network/PizzeriaCommandHandler.cs` & `PizzeriaRoot.cs` | `AdvanceToNextNightServerRpc` abre preparação; `SetRunReadyServerRpc` inicia a noite no mesmo frame quando `allReady`. Falta agendador por `Clock.Now` (`NetworkManager.ServerTime.Time`) e RPC tipada por categoria com ACK explícito. | Adicionar `CastRunCategoryVoteServerRpc`, ACK `NotifyIntermissionActionResultClientRpc`, bloqueio pós-lock de votos/compras/ready e tick server `TickServerIntermissionLifecycle()` guiado por `ServerTime`. |
| `Assets/Piaaria/Scripts/Pizzeria/Presentation/NightResultsPresenter.cs` | Renderiza overlay escuro procedural C# para `Results` e `Defeat`. | Preservar exclusivamente para `Defeat`, Tutorial (`NightNumber == 0`) e Vitória Final (`isRunFinalVictory`). Vitórias intermediárias passam a abrir diretamente a aba `Resumo` no shell único `NightIntermissionPresenter`. |
| `Assets/Piaaria/Scripts/Pizzeria/Presentation/RunPreparationPresenter.cs` | Modal escuro procedural de 960px com botões simples de voto e loja. | Converter em adapter leve que delega a abertura/estado para `NightIntermissionPresenter` sem sobrepor dois overlays. |
| `Assets/Piaaria/UI/Piaria/Scripts/Runtime/Core/UIToolkitTweenService.cs` | Wrapper DOTween funcional com `FadeIn/Out`, `PopIn/Out`, `SlideIn/Out`, `Shake`, `TweenCounter`, `ScaleTo`, `PulseScale` e `ReduceMotion`. | Estender com `AnimateCardDrop`, `RevealCardDetails`, `PressAndRelease`, `SetSelectedState`, `AnimateValue`, `AnimateProgress`, `AnimateBadge`, `NudgeError`, `AnimateTabTransition`, `HighlightWeightedResult`, `PlayCountdownFrame`, `ResetAnimatedState` e `KillTreeTweens`. |
| `Assets/Piaaria/UI/Piaria/Scripts/Runtime/Presenters/ScoreboardPresenter.cs` & `HudPresenter.cs` | `ScoreboardOverlay.uxml` usa `paperPanel3` com colunas `PIZZAS (MÃOS)` e `DINHEIRO`. `HudPresenter` concatena Noite/Fase/Meta/Caixa/REP no banner superior e lista jogadores/funcionários na tela. | Reformar TAB para a Central de Situação (`OperationsStatusOverlay.uxml` / `OperationsStatusPresenter.cs`) com 4 blocos (Noite/Meta, Reputação/Operação, Efeitos Ativos/Upgrades, Equipe Online), mantendo compatibilidade de cena em `ScoreboardPresenter`. Enxugar `HudPresenter` mantendo apenas Noite/Relógio crítico e contador `👥 X/Y online`. |

---

## 3. Inventário de Assets, Tokens Visuais e Áudio Disponíveis

### Tokens Visuais P.I.A.R.I.A (`Assets/Piaaria/UI/Piaria/USS/PiariaCommon.uss` & `Lobby.uss`)
- **Paleta base:** Pêssego `#f8d5c4`, Creme claro `#ffebd5`, Superfície de card `#fffbf5`, Creme escuro `#fce8d5` / `#f0caa8`, Tinta/Contorno marrom `#402318` (`3px` a `4px`), Texto secundário `#7a503e` / `#96745f`.
- **Cores semânticas:** Laranja `#ff741f`, Amarelo `#f8c045` / `#fcd34d`, Verde `#3ca34d` / `#8cb836`, Azul `#2e79b9` / `#2563eb`, Vermelho `#c83232` / `#cb3428`, Rosa `#d94578`.
- **Tipografia local:**
  - `Assets/Piaaria/Art/UI/Piaaria/uis giugiu/fonts/castle-chunk/CastleChunk-Regular.otf` (corpo / números / botões)
  - `Assets/Piaaria/Art/UI/Piaaria/uis giugiu/fonts/lost-tumbler/Lost Tumbler.otf` (títulos `.title-font`)
- **Diretriz estética inegociável (Rev 2.1):** Todos os painéis da intermissão e do TAB utilizam containers digitais cartoon sólidos (contorno `#402318`, fundo `#ffebd5`/`#fffbf5`, acentos coloridos por categoria). **Não** reutilizar `paperPanel1/2/3`, rasgos de papel, clipes, fita adesiva, carimbos físicos ou viradas de página.

### Ícones e Áudios Locais Inventariados (`PiariaUIAssetCatalog.asset` e projeto)
- **Sprites úteis já catalogados (`PiariaUIAssetCatalog.asset`):** `iconDeliveries` (`Asset 22.png`), `iconMoneyCoin` (`dinheiro moeda.png`), `iconMoneyBill` (`dinheiro nota.png`), `digit1` (`Asset 35.png`), `digit2` (`Asset 34.png`).
- **Áudios locais presentes no repositório (sem dependência externa em runtime):**
  - `Assets/EasyGridBuilder Pro/Misc Assets/Sound/UIClick.wav`
  - Clipes locais de `ThirdParty/Feel` e `PizzeriaSoundManager` (`pickupSound`, `placeSound`, `ovenReadySound`, `packageSound`, `deliverySound`, `errorSound`, `uiHoverSound`, `uiClickSound`), configuráveis via `NightIntermissionTheme` / `NightIntermissionFeelBridge` e `MMF_Player`.
- **Ícones funcionais e de modificadores:** Estruturados via `NightIntermissionIconCatalog` (com fallback vetorial/símbolo limpo em UI Toolkit e vínculo opcional a sprites locais importados para relógio, caixa, pedido, troféu, alerta, equipe, vampiro, entrega, forno, mochila).

---

## 4. Contrato Formal de Fases, Navegação e Algoritmo Ponderado

### 4.1. Estados da Intermissão (`IntermissionStage`)
```text
None (0)         -> Fora da intermissão
Summary (1)      -> Vitória intermediária em NightPhase.Results (exibe aba Resumo no shell)
BallotsOpen (2)  -> NightPhase.PreparingNextNight aberto para votos (3 Imprevistos + 3 Oportunidades), compras na Loja e Ready
Locked (3)       -> Todos elegíveis votaram 2x e marcaram Ready; votos/compras/ready congelados
Revealing (4)    -> Sorteios ponderados executados e replicados; UI apresenta varredura + vencedores lado a lado
Countdown (5)    -> Contagem 3, 2, 1 sincronizada por NetworkManager.ServerTime.Time
Starting (6)     -> Transição idempotente para NightPhase.Preparing / Active da próxima noite
```

### 4.2. Separação Local vs Autoritativa
- **Local por cliente:** aba selecionada (`Summary`, `Complications`, `Opportunities`, `Shop`, `Prepare`), controle de primeira visita da aba (para disparar a queda dos 3 cards apenas uma vez por sessão de preparação) e estado visual otimista `pending` enquanto aguarda confirmação do servidor.
- **Autoritativo no servidor:** oferta exata de 3 `Hindrance` + 3 `Opportunity`, votos por `(clientId, category)`, estado `Ready` por `clientId`, saldo/estoque/upgrades da Loja, validação `allReady`, snapshot congelado de votos/porcentagens, sorteio ponderado via `ChoiceRng`, timestamps `RevealStartedAtServerTime`, `CountdownStartsAtServerTime`, `StartNightAtServerTime` e disparo único do início da noite.

### 4.3. Contrato do Sorteio Ponderado (`RunBallotResolver`)
- Para cada categoria (`Hindrance` e `Opportunity`) independentemente:
  - Soma `total = sum(votes[0..2])`.
  - Se `total <= 0`, bloqueia o sorteio e mantém `BallotsOpen` com aviso (nunca seleciona aleatoriamente sem votos).
  - Sorteia `ticket = choiceRng.Next(0, total)` e escolhe a primeira opção cuja soma acumulada supera `ticket`.
  - As porcentagens exibidas na UI (`DisplayProbabilityPercent`) utilizam o método do maior resto (*Largest Remainder*) sobre `votes[i] * 100.0 / total` para garantir soma visual exata de `100%` com inteiros.
  - Em caso de desconexão antes de `Locked`, os votos/ready do desconectado são removidos e a elegibilidade é reavaliada. Após `Locked`, o snapshot permanece imutável.

---

## 5. Critério de Aceite `N2-01`
- Dependências e paths mapeados: **Sim**
- Contrato de estados, DTOs e sorteio documentados: **Sim**
- Nenhuma alteração de gameplay introduzida neste commit: **Sim**

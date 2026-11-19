# Estado das fases

Status atualizado após conclusão das fases **P0–P7** (`Docs/Release/Reports/PLAYTEST_UI_PLACEHOLDERS.md`) na branch `release/marcos-noturnos` a partir de `bdffe509`:
- **Build `.NET` (`ScaryParty.Pizzeria.Tests.csproj`):** `0` erros, `0` avisos.
- **Unity Test Runner EditMode (Suíte Segura):** `102/102 Passed` (`Docs/Release/Reports/Evidence/Unity_EditMode_Suite_Summary.json` + XML NUnit3 local).
- **Unity Test Runner PlayMode (`PlaytestIntegrationPlayModeTests`):** `6/6 Passed` em `503.73s` (`Docs/Release/Reports/Evidence/Unity_PlayMode_Summary.json` + XML NUnit3 local).
- **Visual GameView (`1920x1080` e `1280x720`):** `LobbyScene` + `GameScene` (HUD, Central `[J]`, Diagnóstico `[F8]`) capturadas e inspecionadas sem shaders quebrados; `ValidateAll` pós-PlayMode com `0` erros/warnings.
- **Multiplayer em 2 processos reais (`Host + Cliente` separados):** **NÃO EXECUTADO** (`com.unity.multiplayer.playmode@2.0.2` com `0` cenários/clones virtuais; bloqueio de lock `Library/` para 2ª instância).

| Marco | Status | Relatório |
|---|---|---|
| M1 — Auditoria | IMPLEMENTAÇÃO INTEGRADA — VALIDAÇÃO EDITOR/PLAYMODE PENDENTE | [M1_Relatorio](Reports/M1_Relatorio.md) |
| M2 — Cidade Navegacao | IMPLEMENTAÇÃO INTEGRADA — VALIDAÇÃO EDITOR/PLAYMODE PENDENTE | [M2_Relatorio](Reports/M2_Relatorio.md) |
| M3 — Maos Armas Moto | IMPLEMENTAÇÃO INTEGRADA — VALIDAÇÃO EDITOR/PLAYMODE PENDENTE | [M3_Relatorio](Reports/M3_Relatorio.md) |
| M4 — Noite Completa | IMPLEMENTAÇÃO INTEGRADA — VALIDAÇÃO EDITOR/PLAYMODE PENDENTE | [M4_Relatorio](Reports/M4_Relatorio.md) |
| M5 — Monstros | IMPLEMENTAÇÃO INTEGRADA — VALIDAÇÃO EDITOR/PLAYMODE PENDENTE | [M5_Relatorio](Reports/M5_Relatorio.md) |
| M6 — Run Votos Loja | IMPLEMENTAÇÃO INTEGRADA — VALIDAÇÃO EDITOR/PLAYMODE PENDENTE | [M6_Relatorio](Reports/M6_Relatorio.md) |
| M7 — Funcionarios | IMPLEMENTAÇÃO INTEGRADA — VALIDAÇÃO EDITOR/PLAYMODE PENDENTE | [M7_Relatorio](Reports/M7_Relatorio.md) |
| M8 — Entrega Final | IMPLEMENTAÇÃO INTEGRADA — VALIDAÇÃO FINAL MANUAL PENDENTE | [M8_Relatorio](Reports/M8_Relatorio.md) |

## Checkpoint ativo

- Fase: **Auditoria & Plano de Correção R2 (`P0–P11` / `R01–R14`) em `fix/auditoria-marcos-noturnos-r2`** (base `615990b8`; rastreabilidade em `Docs/Release/Reports/AUDIT_FIX_R2.md`).
  - **R01–R14 corrigidos em código:** Operação de conexão com token/timeout no loading e contratos de prontidão por destino; validação real de spawn com gate de `NavigationWorldManager.IsReady` e liberação de reservas por funcionário; proxy físico sólido de tráfego no cliente + contenção de faixas/curvas pela largura da rua; rejeição de muralha sobre rua e medição real de gaps em `CityBorderValidationReport`; alinhamento do piso do pátio ao footprint + `ExteriorExitFrame` + Undo/profile no wizard L/T; 14 eventos `GameFeelEventId` com emissores e áudio mapeados; orçamento de moto independente da taxa de RPC; agregação transacional de `SupplyBundle`; e estado pendente da loja compartilhado entre abas.
  - **C01–C05 (Intermissão, Loja, UI Toolkit, TAB, Feel, Tweens):** Guardas `TrySetPlayerReady` no RPC, congelamento de participantes no lock e início atômico, loja bloqueada no lock + compra correlacionada por `requestId` + pacote de suprimentos transacional, USS embarcado nos UXMLs e fallback em `PiariaUIAssetCatalog`, painel TAB `OperationsStatusPresenter` conectado em `ScoreboardPresenter` usando `RunPlayerStates` replicado, fallback real em `NightIntermissionFeelBridge`, conclusão explícita no zero do sorteio e `KillTweensRecursive`.
  - **C06–C08 & C16 (Pátio Modular, Reservas & Muralhas):** `PizzeriaLayoutProfile`, `PizzeriaExteriorAnchors`, Wizard Odin `PizzeriaPatioLayoutWizard` (preview L/T, scan de peças, aplicar idempotente, validar), reserva de `exteriorRegions` e `accessCorridors` no `PizzeriaPlacementSolver`, `IsWorldPositionInsidePizzeria` restrito às regiões estruturais, `PizzeriaParkingLotBuilder` determinístico no recuo do L com colisor de piso e `MaterialPropertyBlock`, e `StreetGridGenerator` com cobertura residual por intervalos, arestas curtas e pilares de quina + `LastBorderValidationReport`.
  - **C09–C11 (Loading Unificado, Moto/Tiro, Hotspots):** `PizzeriaSceneLoadingCoordinator` com `PanelSettings` persistente (`sortingOrder = 5000`), rotas unificadas de `LobbyManager`/`LobbyPresenter`/`ReadySceneController`/`MainMenuPresenter`/`PauseEscapeMenuPresenter`/`NightResultsPresenter`/`ReadyRoomPresenter`, primeiro frame antes de `LoadScene`, prontidão por destino (`MenuOrLobby`/`ReadyRoom`/`Gameplay`), progresso suavizado e `NavigationWorldManager` assíncrono via `UpdateNavMesh`; `UpdateMotorcyclePoseServerRpc` por delta de servidor e `FireWeaponServerRpc` com validação de origem antes do débito e buffers `NonAlloc`.
  - **C13–C15 & C17 (Tráfego Físico, Spawns, IA Contratados, Feel Transversal):** `TrafficManager`/`TrafficVehicle` em `FixedUpdate` com `Rigidbody` dinâmico, colisor sólido + sensor de antecipação, faixas e arcos de cruzamento, colisão com episódio único, dano proporcional e recuo direcional em `PlayerState` (`CharacterController`) e `PedestrianNpc` (`SphereCast` + `NavMesh`); spawns autorados/reservados em `PizzeriaRoot` e `EmployeeAgentController`; alinhamento de frente antes de operar; e eventos `GameFeelEventId` transversais (veículos, combate, contratados).
- Próxima ação: Abrir o Unity Editor (respeitando `UNITY_EXECUTION=MANUAL_ONLY`) para executar `T01–T44` em PlayMode / ParrelSync (Host + Cliente), rodar os wizards de pátio (`Tools/Scary Party/Wizards/Pizzaria/Pátio e Layout Modular (L-T)`) caso deseje persistir no prefab `PizzeriaModular_Manual.prefab`, e validar visualmente muralhas, tráfego, loading e Game Feel.
- Bloqueios: Validações em runtime (`T01–T44`), PlayMode, e multiplayer em 2 processos permanecem pendentes (`UNITY_EXECUTION=MANUAL_ONLY`).

Atualizar depois de blocos importantes e antes de encerrar uma sessão. Não avançar fases nem marcar marcos como concluídos com sistemas desconectados ou sem UI/prefab configurados.

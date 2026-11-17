# Relatório M1 — Auditoria e Reparos de Base

**Status:** IMPLEMENTAÇÃO CONCLUÍDA — VALIDAÇÃO PENDENTE  
**Data:** 2026-11-16  
**Versão Unity:** `6000.3.6f1`  
**Branch / SHA Base:** `main` @ `a7cc598bc199f6ccf64c8ca0e103979e99326b9f`  
**Branch de Trabalho / Commit M1 (código):** `release/marcos-noturnos` @ `e4845a28`

---

## 1. Baseline Local Identificada

- **Estado Git pré-existente (`git status --short`):**
  - `M ScaryParty/Assets/Piaaria/Scripts/Cutscenes/Editor/CutsceneMaterialCatalog.cs` (preservado)
  - `M ScaryParty/Assets/Piaaria/Scripts/Cutscenes/Editor/CutsceneSceneAssembler.cs` (preservado)
  - `?? ScaryParty/Docs/Release/`
- **Cena canônica:** `ScaryParty/Assets/ThirdParty/StarterAssets/ThirdPersonController/Scenes/GameScene.unity` (variante `GameScene_CelShaded.unity`)
- **Prefab ativo da pizzaria:** `ScaryParty/Assets/Piaaria/Prefabs/Pizzeria/PizzeriaModular_Manual.prefab`
- **Configs principais:** `ScaryParty/Assets/Piaaria/ScriptableObjects/DefaultCityConfig.asset` (`maxTrafficVehicles: 10`, `trafficBaseSpeed: 8`), `City config.asset` (`10`, `8`), `NightConfig.cs`.
- **Limitação de ambiente (`UNITY_EXECUTION=MANUAL_ONLY`):** Unity Editor não foi acionado por linha de comando; portanto, Test Runner (EditMode/PlayMode) e validação visual/física/rede em cena dependem de execução manual no Editor aberto.

---

## 2. Revalidação de A01–A12 na Branch Local

Todos os 12 achados iniciais do `ROADMAP.md` foram confirmados na branch `main` (`a7cc598b`):

| ID | Status na Branch | Severidade | Ação em M1 / Marco Dono |
|---|---|---|---|
| **A01** | Confirmado (`WeaponInputHandler.cs:74`) | P1 | **Corrigido em M1** (borda de pressionamento + guarda ao sair de modal). Consolidar em M3. |
| **A02** | Confirmado (`WeaponController.cs:164`) | P2 (design) | Mantido para **M3** (reserva de mãos moto + pistola). |
| **A03** | Confirmado (`PizzeriaCommandHandler.cs:2037`) | P2 (design) | Mantido para **M3** (validação de uma mão livre). |
| **A04** | Confirmado (`PizzeriaCommandHandler.cs:1626`) | P2 (design) | Mantido para **M4** (substituir auto-resgate 10s por incapacitação/resgate/morte). |
| **A05** | Confirmado (`NightCycleService.cs:165`) | P2 | Mantido para **M4/M6** (isolar retry como debug, reiniciar RUN na derrota). |
| **A06** | Confirmado (`NightCycleService.cs:228`) | P2 | Mantido para **M4** (fechar no relógio sem prorrogação `Closing`). |
| **A07** | Confirmado (`CustomerNpcView.cs:321`) | P1 | **Corrigido em M1** (reinserção NavMesh + bloqueio de travessia em obstáculos). |
| **A08** | Confirmado (`PizzeriaCommandHandler.cs:1774`) | P2 | Mantido para **M3** (cooldown e identidade por ID de instância da arma). |
| **A09** | Confirmado (`NightCycleService.cs:132`, `PlayerLifecycleBridge.cs:127,154`) | P1 | **Corrigido em M1** (`PizzeriaState.ToolRackStationId` + alocação sem colisão de slot). |
| **A10** | Confirmado (`PizzeriaCommandHandler.cs:1433`) | P2 | Mantido para **M4/M6** (não repor munição gratuitamente no reset de noite). |
| **A11** | Confirmado (`TrafficVehicle.cs:136`) | P2 | Mantido para **M2** (`GetComponentInParent<TrafficVehicle>()` + máscara/pedestres). |
| **A12** | Confirmado (`NightConfig.cs:23`) | P2 | Mantido para **M4/M6** (presets de 10min e preparação por prontidão coletiva). |

---

## 3. Investigação dos Quatro Relatos Principais

1. **Disparo repetido ao segurar o botão (Confirmado — P1, CORRIGIDO em M1):**
   - *Causa raiz:* `WeaponInputHandler.Update()` chamava `weaponController.TriggerFire()` em todo frame em que `starterAssetsInputs.fire` era `true`, dependendo apenas do cooldown `fireRate`. Além disso, ao abrir um menu (`GameplayInputGate.IsLocked` ou cursor visível), `Update()` retornava sem limpar `aim`/`fire` nem exigir novo clique ao fechar.
   - *Correção aplicada:* `WeaponBase.isAutomatic` (padrão `false` para Pistola e Escopeta), `WeaponController.IsCurrentWeaponAutomatic`, detecção de borda (`firePressed && !_wasFirePressed`) e trava `_requireFireRelease` em `WeaponInputHandler.cs`.

2. **Ausência de mira (Confirmado — P2, alocado a M3):**
   - *Causa raiz verificada:* O fluxo de entrada (`StarterAssetsInputs.OnAim` → `WeaponInputHandler` → `WeaponController.StartAim()`) atualiza `isAiming` e `Animator.SetBool("IsAiming", true)`. Porém:
     - `CharacterAnimator.controller` declara `WeaponMode` e `IsAiming` (linhas 4494 e 4500), mas **não possui estados nem transições** usando esses parâmetros.
     - Em 1ª pessoa (`FirstPersonCameraController`), as armas locais são filhas de `RightHandAnchor`, sem offset de ADS nem ajuste de FOV ao mirar.
   - *Ação:* Cancelamento de mira ao abrir menus corrigido em M1; implementação de pose ADS, FOV e sincronização remota alocada a **M3** conforme `ROADMAP.md`.

3. **Carros imóveis (Confirmado P1 em cliente + diagnóstico de host — alocado a M2):**
   - *Causa confirmada no Cliente:* `CityGenerator.EnsureSubGenerators()` cria `_cityRoot = new GameObject("__City__")` sem `NetworkObject` e adiciona `TrafficManager : NetworkBehaviour` em runtime (`_cityRoot.AddComponent<TrafficManager>()`). Como `TrafficManager.IsSpawned` é sempre `false`, `SyncVehiclesClientRpc` nunca é enviado aos clientes remotos (`ClientUpdate()` itera sobre `_vehicleStates` vazio).
   - *Estado no Host/Local:* `CityGenerator.InitializeRuntimeSystems()` chama `_trafficManager.Initialize(...)` quando `Application.isPlaying && config.maxTrafficVehicles > 0`. `DefaultCityConfig.asset` e `City config.asset` têm `maxTrafficVehicles = 10` e `trafficBaseSpeed = 8`. `TrafficVehicle.AssignRandomPath` não preenche `CurrentEdgeId` quando `_pathfinder.FindPath` tem sucesso (forçando fallback por `signalGroupIndex`), e `CheckVehicleAhead()` ignora colliders filhos (A11).
   - *Próximo passo (M2):* Mover replicação do tráfego para objeto `NetworkObject` devidamente spawnado, corrigir `CurrentEdgeId`/sensores e adicionar overlay de diagnóstico para validar movimento no Editor.

4. **Travessia indevida de NPCs por paredes/móveis (Confirmado — P1, CORRIGIDO em M1):**
   - *Causa raiz:* `CustomerNpcView.cs:321` executava `Vector3.MoveTowards(transform.position, approachPos, ...)` sem checagem de colisão quando `Agent` não estava ativo/no NavMesh durante a aproximação da cadeira.
   - *Correção aplicada:* Tenta reinserção no NavMesh via `NavMesh.SamplePosition(..., 2.5f)` + `Agent.Warp`; se indisponível, só permite passo direto se `dist <= 1.5m` e `Physics.Linecast` estiver livre, registrando aviso único (`_loggedNoNavMeshWarning`) em caso de bloqueio.

---

## 4. Alterações Realizadas em M1

- [WeaponBase.cs](file:///c:/Users/Rubens/Documents/GitHub/ScaryParty/ScaryParty/Assets/Piaaria/Scripts/Weapons/WeaponBase.cs): adicionado campo `public bool isAutomatic = false;`.
- [WeaponController.cs](file:///c:/Users/Rubens/Documents/GitHub/ScaryParty/ScaryParty/Assets/Piaaria/Scripts/Weapons/WeaponController.cs): exposta propriedade `IsCurrentWeaponAutomatic`.
- [WeaponInputHandler.cs](file:///c:/Users/Rubens/Documents/GitHub/ScaryParty/ScaryParty/Assets/Piaaria/Scripts/Weapons/WeaponInputHandler.cs): disparo semiautomático por borda, suporte a `isAutomatic`, limpeza de `aim`/`fire` e `_requireFireRelease` ao travar input/abrir menu.
- [CustomerNpcView.cs](file:///c:/Users/Rubens/Documents/GitHub/ScaryParty/ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Presentation/CustomerNpcView.cs): substituído `MoveTowards` cego por recuperação NavMesh (`SamplePosition` + `Warp`) e guarda `Physics.Linecast` (A07).
- [PizzeriaState.cs](file:///c:/Users/Rubens/Documents/GitHub/ScaryParty/ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Domain/Models/PizzeriaState.cs), [PizzeriaRoot.cs](file:///c:/Users/Rubens/Documents/GitHub/ScaryParty/ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Composition/PizzeriaRoot.cs), [NightCycleService.cs](file:///c:/Users/Rubens/Documents/GitHub/ScaryParty/ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Domain/Services/NightCycleService.cs), [PlayerLifecycleBridge.cs](file:///c:/Users/Rubens/Documents/GitHub/ScaryParty/ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Network/PlayerLifecycleBridge.cs): adicionado `ToolRackStationId` dinâmico e alocação de slots livres sem colisão no retorno de utensílios (A09).
- [OvernightExperienceTests.cs](file:///c:/Users/Rubens/Documents/GitHub/ScaryParty/ScaryParty/Assets/Piaaria/Tests/Editor/OvernightExperienceTests.cs): adicionado teste `ClearTransientNightEntities_ReturnsToolsToConfiguredToolRackWithoutSlotCollision`.

---

## 5. Verificações Executadas e Pendentes

- **Executado (L0/L1):**
  - `git diff --check`: **PASSOU** (0 erros de formatação/espaços).
- **Não executado (requer Unity Editor aberto — `UNITY_EXECUTION=MANUAL_ONLY`):**
  - Compilação completa no Unity Editor e execução do **Test Runner (EditMode)** (`OvernightExperienceTests`, `PizzeriaDomainTests`, `ModularCityTests`, `CityGraphPathfinderTests`, `PizzaAssemblyServiceTests`, `ProcessingInterruptionTests`, `BackpackServiceTests`, `BackpackLifecycleTests`).
  - `dotnet build` via CLI foi cancelado porque `Temp/obj/*/project.assets.json` não estava restaurado sem o Editor.
  - Bateria manual **T01–T60** em PlayMode (Host + Cliente via ParrelSync), classificada em `TEST_MATRIX.md` entre `NÃO EXECUTADO` (sistemas existentes) e `LACUNA — Mx` (funcionalidades futuras de M3–M7).

---

## 6. Próxima Ação Exata

1. No **Unity Editor aberto**, aguardar recompilação dos scripts modificados e confirmar **Console limpo**.
2. Rodar a suíte **EditMode** no Test Runner (incluindo o novo teste de A09).
3. Executar os testes manuais prioritários de M1 em PlayMode (`T01–T28`, `T29–T32`, `T35`, `T38–T42`, `T45–T49`) e registrar resultados em `TEST_MATRIX.md`.
4. Só avançar para **M2** após aprovação/validação dos testes críticos de M1.

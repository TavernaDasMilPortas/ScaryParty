# Relatório Técnico de Recuperação e Integração (M1–M8 / R0–R8)

- **Branch:** `release/marcos-noturnos`
- **Baseline inicial (pré-R0):** `ff864cc5aa4afe7a8c371918ae9fe4e7bd9aa490`
- **Especificação:** [RECUPERACAO_M1_M8.md](../RECUPERACAO_M1_M8.md)
- **Política Unity:** `UNITY_EXECUTION=MANUAL_ONLY` (sem invocação de `Unity.exe`, `-batchmode` ou `-runTests`; validação estática via `dotnet build` em scratch e passo a passo manual registrado para o Unity Editor).

## Registro por Etapa

### R0 — Restauração de Compilação (P0) e Reclassificação Documental
- **Baseline preservado:** Alterações pré-existentes do usuário em `CutsceneMaterialCatalog.cs` e `CutsceneSceneAssembler.cs` mantidas intactas e não incluídas em staging/commits. Metadados `.meta` já existentes em disco (`Combat.meta`, `Combat/Monsters.meta`, `MonsterNpcController.cs.meta`, `EmployeeTaskService.cs.meta`, `RunProgressionService.cs.meta`, `SpeciesHostilityService.cs.meta`) auditados e preservados.
- **Causas raiz corrigidas:**
  1. `LocationRef` (`Assets/Piaaria/Scripts/Pizzeria/Domain/Types/LocationRef.cs`): adicionada propriedade de compatibilidade `StationId` exigida por `NightCycleService`.
  2. `SupplyOrderState` (`Assets/Piaaria/Scripts/Pizzeria/Domain/Models/SupplyState.cs`): adicionado construtor de compatibilidade de 7 parâmetros delegando ao construtor principal + `AddLine`.
  3. `NightConfig.cs` / `PizzeriaConfig.cs` (`Assets/Piaaria/Scripts/Pizzeria/Authoring/`): adicionado `enableAllPlayersDeadDefeat = true`; `PlayerHealth.cs` alinhado para `true`.
  4. `PizzeriaCommandHandler.cs`: corrigida assinatura do delegate `OnServerResetNightCombatRequested` via handler `HandleResetNightCombatRequestedServer`, adicionados `OnServerResetNightCombatWithPolicyRequested` / `RequestResetNightCombatServer(bool)`, `TargetSender(ulong)`, `ResolveAllPlayersDeadDefeatPolicy(PizzeriaRoot)`, declarações locais `var root = PizzeriaRoot.Instance;` faltantes, e troca da chamada inexistente `BroadcastWeaponFireVisualClientRpc` para `PlayWeaponFireEffectsClientRpc`.
- **Verificação estática R0:** Compilação limpa (0 erros) via script scratch sobre `ScaryParty.Pizzeria.Domain.csproj`, `ScaryParty.Pizzeria.Tests.csproj`, `Assembly-CSharp.csproj` e `Assembly-CSharp-Editor.csproj`, além de `git diff --check` limpo. `STATUS.md` reclassificado de forma honesta (`PARCIAL` para M1–M3, `INTEGRAÇÃO INCOMPLETA` para M4–M7, `NÃO CONCLUÍDA` para M8).

### R1 — M4: Estados de Vida Coerentes, Cura/Resgate 3s, Consequências de Morte e Espectador
- **Contrato de estado de vida e permissões (`NetDtos.cs`, `PlayerHealth.cs`, `PizzeriaCommandHandler.cs`, `KitchenCommands.cs`, `LogisticsCommands.cs`, `OrderCommands.cs`):**
  - `NetPlayerCombatDto` estendido com `RescuedByClientId`, `RescueMode` (`0=Nenhum`, `1=Cura`, `2=Reanimação`) e `SpectatingTargetClientId`.
  - `PlayerHealth` agora distingue `IsAlive`, `IsIncapacitated` (rastejo lento a `0.70f` m/s, bloqueio de pulo/corrida/armas/interação) e `IsDead` (desativa `ThirdPersonController` local e entra em espectador), bloqueando também `WeaponController`, `PlayerInteraction` e `BackpackController`.
  - Todas as ServerRpcs de cozinha, logística, pedidos, armas e moto passam por `CanPlayerPerformGameplayActionServer`.
- **Cura e reanimação autoritativas no servidor (`PlayerAllyHelpInteractable.cs`, `PlayerInteraction.cs`, `PizzeriaCommandHandler.cs`):**
  - Criado `PlayerAllyHelpInteractable` (+ `.meta`) anexado em `PlayerHealth.Awake()`.
  - `BeginAssistAllyServerRpc`, `HeartbeatAssistAllyServerRpc` e `CancelAssistAllyServerRpc` controlam o progresso de 3,0s exclusivamente pelo `dt` do servidor (`AdvanceAllyRescueServerRpc` mantido apenas como alias de heartbeat sem aceitar `dt` do cliente), interrompendo em dano (alvo ou ajudante), soltura de `[E]`, distância > `2.85m`, montagem na moto ou desconexão.
- **Consequências de morte, espectador e queda da moto (`PlayerLifecycleBridge.cs`, `CustomerService.cs`, `OrderService.cs`, `FirstPersonCameraController.cs`, `HudPresenter.cs`):**
  - `PlayerLifecycleBridge.CleanupPlayerWorldAndDomainStateServer` unifica desconexão e transição para `IsDead`, liberando atendimento de clientes (`ReleaseCustomersAttendedByPlayer`), comandas assumidas (`ReleaseOrdersClaimedByPlayer`), holds, ferramentas e mochila em segurança.
  - `HandlePlayerTransitionToDeadServer` ejeta da moto, devolve armas ao rack, elege `SpectatingTargetClientId` e verifica derrota coletiva.
  - `FirstPersonCameraController` orbita o aliado espectado com troca via `[Q]`/`[E]` ou botões do mouse (`CycleSpectatorTargetServerRpc`); `HandleResetNightCombatServer` restaura vivos e reposiciona mortos/nocauteados na próxima noite.
  - Dano de queda da moto (`motorcycleRiderFallDamage`) roteado por `ApplyDamageToPlayerServer` (`DamageSourceType.Collision`).
- **Teste e verificação R1:** Teste unitário `R1_LifeStateCleanupReleasesAttendedCustomersClaimedOrdersAndBackpack` adicionado em `OvernightExperienceTests.cs`. Build estático (`Domain`, `Tests`, `Assembly-CSharp`) com 0 erros e `git diff --check` limpo.

### R2 — M3: Contrato Único de Mãos, Duas Pistolas por `WeaponId`, Bandoleira e Moto Autoritativa
- **Contrato unificado de mãos (`PizzeriaCommandHandler.cs`, `KitchenCommands.cs`, `LogisticsCommands.cs`, `PlayerInventoryAdapter.cs`):**
  - Centralizado `TryPrepareHandForItemOrToolPickupServer`: esquerda = moto/item, direita = pistola/item, duas mãos = escopeta ativa, bandoleira = 0 mãos. Pegar item/ferramenta segurando escopeta move automaticamente a escopeta para a bandoleira (`IsSlinged = true`); montar na moto com escopeta também a move para a bandoleira, permitindo pilotar (mão esquerda) com pistola (mão direita).
- **Instâncias distintas por `WeaponId` (`PizzeriaCommandHandler.cs`, `WeaponRackStation.cs`):**
  - `EnsureDefaultWeaponsInitializedServer` passa a inicializar 3 instâncias distintas: `WeaponId=1` (Pistola #1, 12/48), `WeaponId=2` (Pistola #2, 12/48) e `WeaponId=3` (Escopeta #1, 6/24), permitindo que dois jogadores equipem pistolas simultaneamente via `InteractWeaponRackServerRpc` / `InteractWeaponRackByIdServerRpc`.
- **Visual 3P na bandoleira, mira remota e ADS (`NetDtos.cs`, `WeaponController.cs`, `FirstPersonCameraController.cs`):**
  - `NetPlayerCombatDto.IsAiming` sincronizado via `SetWeaponAimServerRpc` para pose de mira remota em `WeaponController`.
  - `WeaponController` instancia `SlungShotgun3PVisual` ancorado em `ChestBone`/`UpperChest` quando `IsSlinged == true`.
  - `FirstPersonCameraController` aplica `adsSensitivityMultiplier = 0.55f` durante ADS.
- **Moto autoritativa (`PizzeriaCommandHandler.cs`, `DeliveryMotorcycleController.cs`, `PizzeriaParkingLotBuilder.cs`):**
  - `PizzeriaParkingLotBuilder` cria `Rigidbody` cinemático + âncoras explícitas (`HandlebarLeftAnchor`, `HandlebarRightAnchor`, `LeftExitAnchor`, `RightExitAnchor`, `CargoRackAnchor`).
  - `ReportMotorcycleImpactServerRpc` valida distância máxima (`5m`) e linha de visão (`Physics.Linecast`) no servidor; `HandleResetNightCombatServer` limpa `_processedMotorcycleImpacts` e `_lastWeaponFireServerTime`.
  - `ResolveMotorcycleExitPosition` valida saídas com `Physics.CheckCapsule` para evitar clipping em paredes.
- **Verificação estática R2:** Build estático (`Domain`, `Tests`, `Assembly-CSharp`) com 0 erros e `git diff --check` limpo.

## Matriz de Rastreabilidade R0–R8 / M1–M8 / T01–T60

| Etapa | Marco | Casos (`TEST_MATRIX`) | Escopo Principal | Status Código / Estático | Validação Unity Editor (`MANUAL_ONLY`) |
|---|---|---|---|---|---|
| R0 | Base P0 | Compilação / `.meta` / Docs | Correções CS0103/CS1061/CS1503/CS1729 e reclassificação documental | **Concluído** (0 erros nos 4 `.csproj`) | Pendente (focar Editor e verificar Console) |
| R1 | M4 | T31, T32 | Estados de vida, permissões, cura/resgate 3s, cleanup morte, espectador, queda moto | **Concluído** (0 erros; teste domínio adicionado) | Pendente (PlayMode/ParrelSync) |
| R2 | M3 | T25–T35 | Contrato único mãos, 2 pistolas por `WeaponId`, bandoleira 3P/1P, sweep moto e saída segura | **Concluído** (0 erros nos 3 `.csproj`) | Pendente (PlayMode/ParrelSync) |
| R3 | M4 | T18, T36–T43 | Entrega multi-pizza, receita/reputação unificadas, bounds pizzeria no servidor, fechamento atômico | Pendente | Pendente |
| R4 | M6 | T49–T53 | `RunProgressionService` único por RUN, `NetRunStateDto`, votos/ready/loja e UI completa | Pendente | Pendente |
| R5 | M2/M5 | T10, T11, T44–T48 | `Rigidbody` tráfego, guards NavMesh, `MonsterPopulationDirector`, hostilidade/arquétipos e zumbi | Pendente | Pendente |
| R6 | M7 | T54–T59 | Identidade ator/bloqueio `EmployeeTaskService`, `EmployeeAgentController` Caixa/Pizzaiolo e takeover | Pendente | Pendente |
| R7 | M3/M8 | T29, T30, T60 | Tutorial Noite 0 opcional, trava clique UI->arma, HUD operacional completo e `CharacterSetupWizard` | Pendente | Pendente |
| R8 | M1–M8 | T01–T60 | Compilação final, execução runner pure `.NET` de domínio, atualização documental final | Pendente | Pendente |

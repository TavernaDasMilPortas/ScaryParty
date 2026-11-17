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

## Matriz de Rastreabilidade R0–R8 / M1–M8 / T01–T60

| Etapa | Marco | Casos (`TEST_MATRIX`) | Escopo Principal | Status Código / Estático | Validação Unity Editor (`MANUAL_ONLY`) |
|---|---|---|---|---|---|
| R0 | Base P0 | Compilação / `.meta` / Docs | Correções CS0103/CS1061/CS1503/CS1729 e reclassificação documental | **Concluído** (0 erros nos 4 `.csproj`) | Pendente (focar Editor e verificar Console) |
| R1 | M4 | T31, T32 | Estados de vida, permissões, cura/resgate 3s, cleanup morte, espectador, queda moto | Pendente | Pendente |
| R2 | M3 | T25–T35 | Contrato único mãos, 2 pistolas por `WeaponId`, bandoleira 3P/1P, sweep moto e saída segura | Pendente | Pendente |
| R3 | M4 | T18, T36–T43 | Entrega multi-pizza, receita/reputação unificadas, bounds pizzeria no servidor, fechamento atômico | Pendente | Pendente |
| R4 | M6 | T49–T53 | `RunProgressionService` único por RUN, `NetRunStateDto`, votos/ready/loja e UI completa | Pendente | Pendente |
| R5 | M2/M5 | T10, T11, T44–T48 | `Rigidbody` tráfego, guards NavMesh, `MonsterPopulationDirector`, hostilidade/arquétipos e zumbi | Pendente | Pendente |
| R6 | M7 | T54–T59 | Identidade ator/bloqueio `EmployeeTaskService`, `EmployeeAgentController` Caixa/Pizzaiolo e takeover | Pendente | Pendente |
| R7 | M3/M8 | T29, T30, T60 | Tutorial Noite 0 opcional, trava clique UI->arma, HUD operacional completo e `CharacterSetupWizard` | Pendente | Pendente |
| R8 | M1–M8 | T01–T60 | Compilação final, execução runner pure `.NET` de domínio, atualização documental final | Pendente | Pendente |

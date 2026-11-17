# Relatório M4 — Uma Noite Completa

**Status:** IMPLEMENTAÇÃO CONCLUÍDA — VALIDAÇÃO PENDENTE  
**Data:** 2026-11-16  
**Branch / Commit M4 (código):** `release/marcos-noturnos` @ `7f24b45b`

---

## 1. Implementação Realizada

1. **Fechamento no Relógio sem Prorrogação (`A06` — `NightCycleService.cs`, `NightConfig.cs`):**
   - Ao zerar `NightRemainingSeconds`, `TickNight` agora executa `SettleOpenOrdersAtNightEnd` (expira pedidos `Accepted` uma única vez, incrementa `NightOrdersFailed` e aplica penalidade de reputação `-10` por pedido) e avalia o resultado imediatamente, sem entrar em `NightPhase.Closing`.
   - `NightConfig` atualizado para `durationSeconds = 600f` (10 min) e `closingGraceSeconds = 0f` (**A12**).
2. **Reputação Compartilhada e Separação Faturamento/Saldo (`Ids.cs`, `PizzeriaState.cs`, `NightCycleService.cs`, `PizzeriaCommandHandler.cs`):**
   - Adicionados `Reputation` (`0..100`), `NightReputationMinGoal`, `NightPizzasMinGoal`, `RunTotalRevenue` e `NightDefeatReason.ReputationZeroed`.
   - `ReportCustomerAttackInPizzeriaServerRpc` aplica penalidade territorial (`-15` com cooldown de 3s) apenas quando o ataque ocorre dentro do volume da pizzaria (`T50`).
3. **Incapacitação, Resgate Aliado e Espectador (`A04` — `NetDtos.cs`, `PizzeriaCommandHandler.cs`):**
   - Removido auto-resgate de 10s; incapacitado entra em sangramento (`45s`) e passa para `IsDead = true` (espectador até a próxima noite vencida).
   - Adicionado `AdvanceAllyRescueServerRpc` (3s de interação); qualquer dano ao alvo ou ao ajudante em `ApplyDamageServer` zera `RescueProgress` (`T43`).
   - `AreAllPlayersIncapacitatedServer` considera incapacitados e mortos para derrota operacional (`T45`).
4. **Preservação de Munição (`A10`) e Suprimentos entre Noites:**
   - `HandleResetNightCombatServer(bool resetWeaponsAndAmmo = false)` não recarrega mais armas gratuitamente ao avançar de noite.
   - `ClearTransientNightEntities(..., preservePendingSupplies: true)` preserva `SupplyOrders` pagas ainda em trânsito entre noites.
5. **Teste EditMode (`OvernightExperienceTests.cs`):**
   - Adicionado `NightCycle_ClockZero_SettlesOpenOrdersOnceWithoutClosingGraceAndPreservesSupplies`.

---

## 2. Validação e Pendências (PlayMode / Editor)

- **Executado:** `git diff --check` limpo.
- **Pendente no Unity Editor aberto (`T07–T20`, `T41–T52`):**
  - Rodar `OvernightExperienceTests` e `PizzeriaDomainTests` no Test Runner EditMode.
  - Em PlayMode (Host + Cliente ParrelSync), validar noite completa com entrega multipizza, compras de suprimentos, incapacitação/resgate/morte/espectador e retorno na noite seguinte.

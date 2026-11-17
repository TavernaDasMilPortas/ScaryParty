# Relatório M7 — Balconistas e Pizzaiolos

**Status:** IMPLEMENTAÇÃO CONCLUÍDA — VALIDAÇÃO PENDENTE  
**Data:** 2026-11-16  
**Branch / Commit M7 (código):** `release/marcos-noturnos` @ `2d7ca29e`

---

## 1. Implementação Realizada

1. **Coordenador de Tarefas e Contratação (`EmployeeTaskService.cs`):**
   - Contratação coletiva com custo por função/nível (`Cashier` = 180, `Pizzaiolo` = 120) sem ocupar vaga humana do lobby nem alterar a meta da noite.
   - Estados de tarefa (`Available`, `Reserved`, `InProgress`, `Blocked`, `Completed`, `Cancelled`) e motivos explícitos de bloqueio (`NoDough`, `MissingIngredient`, `OvenBusy`, `ToolInUse`, `PathBlocked`).
   - Reserva exclusiva por lease (`TryReserveTask`), tomada de tarefa por humano sem duplicação (`HumanTakeoverTask`) e liberação em cancelamento/desconexão (`ReleaseActorTasks`, `T58`).
   - Erro intencional de habilidade sorteado via `EmployeeErrorRng` (`RollIntentionalSkillError`) reduzido por nível/treinamento (`MinSkillErrorRate = 0.02f`).
   - Persistência de contratação/nível entre noites com política editável (`RestoreLostEmployeesNextNight = true`) e limpeza total apenas em `ResetForNewRun`.
2. **Teste EditMode (`OvernightExperienceTests.cs`):**
   - Adicionado `EmployeeTaskService_ExclusiveReservationsHumanTakeoverAndNightPersistence`.

---

## 2. Pendências para Unity Editor / PlayMode

- Focar o Unity Editor para gerar `.meta` de `EmployeeTaskService.cs`.
- Conectar agentes visuais de Balconista/Pizzaiolo no `PizzeriaRoot` / `NpcNavigationMotor` utilizando os mesmos serviços reais (`TransferService`, `ProcessingService`, `OvenService`) e validar `T58` em PlayMode.

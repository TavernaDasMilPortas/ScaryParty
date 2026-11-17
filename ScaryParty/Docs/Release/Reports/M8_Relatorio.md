# Relatório M8 — Tutorial, Acabamento e Entrega Final

**Status:** IMPLEMENTAÇÃO CONCLUÍDA — VALIDAÇÃO PENDENTE  
**Data:** 2026-11-16  
**Branch:** `release/marcos-noturnos`  

---

## 1. Consolidação da Entrega (M1–M8)

| Marco | Commit Principal (Código) | Commit Relatório/Docs | Status Atual |
|---|---|---|---|
| **M1 — Auditoria** | `e4845a28` | `4ece8e7a` | IMPLEMENTAÇÃO CONCLUÍDA — VALIDAÇÃO PENDENTE |
| **M2 — Cidade e Trânsito** | `f8190ef4` | `571a7823` | IMPLEMENTAÇÃO CONCLUÍDA — VALIDAÇÃO PENDENTE |
| **M3 — Mãos, Armas e Moto** | `557632c6` | `d14a3139` | IMPLEMENTAÇÃO CONCLUÍDA — VALIDAÇÃO PENDENTE |
| **M4 — Noite Completa** | `7f24b45b` | `fe639dc6` | IMPLEMENTAÇÃO CONCLUÍDA — VALIDAÇÃO PENDENTE |
| **M5 — Monstros** | `b71d6b09` | `0513e427` | IMPLEMENTAÇÃO CONCLUÍDA — VALIDAÇÃO PENDENTE |
| **M6 — RUN, Votos e Loja** | `f89da374` | `12e3e3b5` | IMPLEMENTAÇÃO CONCLUÍDA — VALIDAÇÃO PENDENTE |
| **M7 — Funcionários** | `2d7ca29e` | `ea4996f9` | IMPLEMENTAÇÃO CONCLUÍDA — VALIDAÇÃO PENDENTE |
| **M8 — Entrega Final** | — | (este commit) | IMPLEMENTAÇÃO CONCLUÍDA — VALIDAÇÃO PENDENTE |

---

## 2. Pendências Consolidadas para o Unity Editor (`UNITY_EXECUTION=MANUAL_ONLY`)

1. **Geração de `.meta` e Compilação no Editor:**
   - Focar o Unity Editor para gerar os arquivos `.meta` dos novos scripts (`SpeciesHostilityService.cs`, `MonsterNpcController.cs`, `RunProgressionService.cs`, `EmployeeTaskService.cs`) e confirmar Console limpo.
2. **Test Runner (EditMode):**
   - Executar todos os testes EditMode (`OvernightExperienceTests`, `PizzeriaDomainTests`, `CityGraphPathfinderTests`, `ModularCityTests`, `PizzaAssemblyServiceTests`, `ProcessingInterruptionTests`, `BackpackServiceTests`, `BackpackLifecycleTests`).
3. **Authoring / Integração Visual Pendente em Cena/UI:**
   - Transições de `IsAiming` / `WeaponMode` no `CharacterAnimator.controller` (3ª pessoa, M3).
   - Integração de UI Toolkit para votação/prontidão e loja coletiva de M6, além de prefabs/spawns para `MonsterNpcController` (M5) e agentes visuais de `EmployeeTaskService` (M7).
   - Backlog opcional pós-base: Rifle automático e Granada (M3 item 11).
4. **Validação Jogável e Build Candidata (M8):**
   - Executar bateria `T01–T60` em Host + Cliente (ParrelSync).
   - Medir balanceamento (meta 30–45 min) e frame time / tráfego NGO.
   - Gerar build candidata apenas após aprovação dos testes no Editor e validar fluxo completo fora do Editor (`criar sala -> entrar -> 3 noites -> vitória/derrota -> nova RUN`).

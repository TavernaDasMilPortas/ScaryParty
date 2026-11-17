# Relatório M6 — RUN, Votos, Loja e Variedade

**Status:** IMPLEMENTAÇÃO CONCLUÍDA — VALIDAÇÃO PENDENTE  
**Data:** 2026-11-16  
**Branch / Commit M6 (código):** `release/marcos-noturnos` @ `f89da374`

---

## 1. Implementação Realizada

1. **Serviço de Progressão da RUN (`RunProgressionService.cs`):**
   - Suporte a `MaxNights` (`1..7`, padrão `3`), `RunDifficulty` (`Normal` / `Hard`) e metas tabeladas por `(noite, humanos)` congeladas no início da noite (`FreezeNightStartParameters`, `T52`).
   - Streams RNG separados (`MapRng`, `ChoiceRng`, `EmployeeErrorRng`) derivados da seed.
   - **Votação e Prontidão Coletiva (`T53`, `T54`):** `OpenChoiceVote` garante >= 2 opções elegíveis (com fallback se pool esgotar), 1 voto alterável por cliente, limpeza de voto/prontidão em desconexão e desempate determinístico no servidor (`ResolveVoteServer`).
   - **Modificadores e Loja Atômica (`T55`, `T56`):** catálogo com ID, duração (`OneNight` / `Permanent`), incompatibilidades, stacks e recálculo a partir da base (`RecalculateEffectiveStats`); `TryPurchaseShopItem` atômico contra saldo negativo ou compra única duplicada.
   - **Fim da RUN e Nova RUN Limpa (`T59`):** `TryCompleteNightAndAdvance` encerra na última noite com `IsRunVictory = true`; `ResetRun` limpa todos os modificadores, votos, prontidão e `SupplyOrders`.
2. **Teste EditMode (`OvernightExperienceTests.cs`):**
   - Adicionado `RunProgression_ThreeNightsVotesModifiersShopAndCleanReset`.

---

## 2. Pendências para Unity Editor / PlayMode

- Focar o Unity Editor para gerar `.meta` de `RunProgressionService.cs`.
- Integrar UI Toolkit de votação/prontidão e loja coletiva na preparação entre noites e executar `T46–T56`, `T59–T60` em PlayMode (Host + Cliente).

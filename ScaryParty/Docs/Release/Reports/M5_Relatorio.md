# Relatório M5 — Monstros e Hostilidade

**Status:** IMPLEMENTAÇÃO CONCLUÍDA — VALIDAÇÃO PENDENTE  
**Data:** 2026-11-16  
**Branch / Commit M5 (código):** `release/marcos-noturnos` @ `b71d6b09`

---

## 1. Implementação Realizada

1. **Domínio de Espécies, Hostilidade e Conversão (`SpeciesHostilityService.cs`):**
   - Base única por capacidades (`SpeciesCapabilityProfile`) para `Human`, `Vampire`, `Werewolf`, `Ghost` e `Zombie`.
   - `SpeciesHostilityService` com teto (`MaxHostilityLevel = 100`), duração, ofensa orientada a dado do catálogo (`OffenseIngredientId = 10` para alho vs vampiro) e política padrão configurável (`PersistAcrossNights = false`, documentada como proposta no roadmap).
   - `ZombieConversionService` com teto configurável (`MaxInfectedCap = 6`), cooldown e idempotência por entidade (`_convertedIds`).
2. **Base Comum Runtime/Rede (`MonsterNpcController.cs`):**
   - `NetworkBehaviour` + `IDamageable` com `NetworkVariable` para `NpcBehaviorState` e HP, separando decisão no servidor da apresentação visual (olhos vermelhos via `MaterialPropertyBlock`).
   - Aquisição/desistência por distância + tempo (`T57`), descida segura de telhado do Vampiro (`TryDropFromRooftop`), investida comprometida com recuperação do Lobisomem e blink com `NavMesh.SamplePosition` + máscara isolada do Fantasma.
3. **Teste EditMode (`OvernightExperienceTests.cs`):**
   - Adicionado `SpeciesHostilityAndZombieConversion_EnforceCatalogOffenseCapResetAndIdempotency`.

---

## 2. Pendências para Unity Editor / PlayMode

- Focar o Unity Editor para gerar `.meta` dos dois scripts novos (`SpeciesHostilityService.cs` e `MonsterNpcController.cs`).
- Associar `MonsterNpcController` aos prefabs/spawns das espécies e executar `T43–T45`, `T50`, `T57` em PlayMode (Host + Cliente).

# B0 — Inventário Técnico e Caminho Efetivo de Configuração

- **Data:** 18/11/2026
- **Branch:** `release/marcos-noturnos`
- **SHA Base Inspecionado:** `c6163121`
- **Unity Version:** `6000.3.6f1`

## 1. Arquivos Locais Pré-Existentes (Preservados)
- `M ScaryParty/Assets/Piaaria/ScriptableObjects/DefaultCityConfig.asset`
- `M ScaryParty/Assets/Piaaria/ScriptableObjects/GeneratedCityData.asset`
- `M ScaryParty/Assets/Piaaria/Scripts/VisualStyles/Runtime/VisualStyleGameplayBootstrap.cs`
- `M ScaryParty/Assets/ThirdParty/CoInspector/Editor/Settings/UserData.asset`
- `M ScaryParty/Assets/ThirdParty/StarterAssets/ThirdPersonController/Scenes/GameScene.unity`
- `M ScaryParty/CityGenerationLog.json`
- `?? ScaryParty/Docs/Release/Plano_Wizard_Eventos_Loja_Balanceamento_ScaryParty.md`
- `?? ScaryParty/Docs/Release/Plano_Wizard_Eventos_Loja_Balanceamento_ScaryParty_Atualizado.md`

## 2. Caminho Efetivo de Configuração (Cenas → Prefab → Config → Preset → Catálogo)
1. **Cenas Canônicas de Gameplay:**
   - `Assets/ThirdParty/StarterAssets/ThirdPersonController/Scenes/GameScene.unity` (`CityGenerator.pizzeriaPrefab` → `PizzeriaModular_Manual.prefab`)
   - `Assets/Piaaria/Scenes/GameplayVariants/GameScene_CelShaded.unity` (`CityGenerator.pizzeriaPrefab` → `PizzeriaModular_Manual.prefab` / variante cel-shaded)
   - `Assets/Piaaria/Scenes/LobbyScene.unity` + `Assets/Piaaria/Scenes/ReadyScene.unity` (`SessionLaunchConfig` seleciona `PresetId`, `Difficulty`, `MaxNights`, `SeedOverride`, `IncludeNight0Tutorial`)
2. **Prefab da Pizzaria:**
   - `Assets/Piaaria/Prefabs/Pizzeria/PizzeriaModular_Manual.prefab` (`PizzeriaRoot._config` → `5dd786dcdcb30634c8c6bc3b582da4d0`)
   - `Assets/Piaaria/Prefabs/Pizzeria/PizzeriaModular_CelShaded.prefab` (`PizzeriaRoot._config` → `5dd786dcdcb30634c8c6bc3b582da4d0`)
3. **`PizzeriaConfig` Efetivo em Jogo:**
   - `Assets/Piaaria/ScriptableObjects/Pizzeria/PizzeriaConfig_PizzeriaModular_Manual.asset` (`guid: 5dd786dcdcb30634c8c6bc3b582da4d0`)
   - Manifesto em Resources: `Assets/Piaaria/Resources/Playtest/PlaytestReferenceManifest.asset`
   - `defaultPreset`: `Assets/Piaaria/Playtest/Presets/Entrega_3Noites.asset`
   - `availablePresets`: `Entrega_3Noites.asset`, `Playtest_Integracao.asset`, `Tutorial_Noite0.asset` (e `NoiteDeTeste.asset` em B5.1)
   - `balancingCatalog`: `Assets/Piaaria/ScriptableObjects/Pizzeria/Balancing/RunBalancingCatalog.asset` (criado e vinculado por B0)

## 3. Inventário Antes da Migração (Origem → Consumidores → UI)

### 3.1 Modificadores (`RunProgressionService.RegisterDefaultCatalog`)
| ID | Nome | Categoria | Duração | MaxStacks | Valor Base | Consumidor Confirmado |
|---|---|---|---|---|---|---|
| `blood_moon` | Lua vermelha | Event | OneNight | 1 | `+0.5` | `MonsterPopulationDirector.SpawnPopulationForNightServer` |
| `vampire_attack` | Ataque dos vampiros | Hindrance | OneNight | 1 | `+0.35` | **Sem consumidor na IA** (calculado em `RunEffectiveStats`, corrigido em B3) |
| `oven_timer` | Timer do forno | PermanentUpgrade | Permanent | 1 | `1.0` | `ProcessingService.TickOven` (`ovenAutoShutoff`) |
| `reinforced_bag` | Bolsa reforçada | PermanentUpgrade | Permanent | 2 | `+1` | `PizzeriaCommandHandler.ApplyRunBackpackCapacityBonusServer` (só capacidade; descrição antiga citava resistência) |
| `heavy_rush` | Movimento intenso | Hindrance | OneNight | 1 | `+0.3` | **Sem consumidor na demanda** (`DemandDirector` ignorava `DemandMultiplier`, corrigido em B3) |
| `express_supply` | Fornecimento expresso | Opportunity | OneNight | 1 | `-0.4` | `PizzeriaCommandHandler.PlaceSupplyOrderServerRpc` -> `NightCycleService.TryPlaceSupplyOrder` |
| `staff_training` | Treinamento | PermanentUpgrade | Permanent | 2 | `-0.25` | `EmployeeTaskService.ComputeEffectiveErrorProbability` |

### 3.2 Ofertas de Loja (`PizzeriaCommandHandler.PurchaseRunShopItemServerRpc`)
| Byte ID | Oferta | Preço Antigo (Hardcoded) | Origem UI Antiga | Problema Identificado |
|---|---|---|---|---|
| `1` | Timer do Forno | `90` | `RunPreparationPresenter` + `OperationsHubPresenter` | Preço duplicado em 3 arquivos; ID numérico fixo |
| `2` | Bolsa Reforçada | `75` | `RunPreparationPresenter` + `OperationsHubPresenter` | Preço/stack duplicado na UI |
| `3` | Treinamento Equipe | `85` | `RunPreparationPresenter` + `OperationsHubPresenter` | Preço e fórmula de stacks duplicados na UI |
| `4` | Suprimentos Base +5 | `45` | `RunPreparationPresenter` + `OperationsHubPresenter` | Loop usava `ingredientDefId >= 1 && <= 3` (Massa=1, Massa Aberta=2, Molho=3), ignorando **Queijo Inteiro (`id=4`)** e permitindo compra sem efeito com estoque cheio |
| `5` | Contratar Balconista | `180` | `RunPreparationPresenter` + `OperationsHubPresenter` | `EmployeeTaskService.GetHireCost` hardcoded `180 * level` |
| `6` | Contratar Pizzaiolo | `120` | `RunPreparationPresenter` + `OperationsHubPresenter` | `EmployeeTaskService.GetHireCost` hardcoded `120 * level` e erro base `0.20f` / `0.05f` hardcoded |

### 3.3 Suprimentos, Ingredientes Base e Receitas (`PizzeriaConfig_PizzeriaModular_Manual.asset`)
- **Ingredientes Base:** `Ingredient_1_Massa` (`id=1`), `Ingredient_3_Molho` (`id=3`), `Ingredient_4_QueijoInteiro` (`id=4`). (`id=2` é `Massa Aberta` preparada; `id=5` é `Queijo Ralado`).
- **Pacotes de Reposição (`SupplyItems`):**
  - `SupplyItem_1` (`id=1`): Pacote Massa (5x), `ingredientId=1`, `packPrice=5`, `baseETA=20s`
  - `SupplyItem_2` (`id=2`): Pacote Molho (5x), `ingredientId=3`, `packPrice=5`, `baseETA=20s`
  - `SupplyItem_3` (`id=3`): Pacote Queijo (5x), `ingredientId=4`, `packPrice=10`, `baseETA=20s`
  - `SupplyItem_4` (`id=4`): Pacote Calabresa (5x), `ingredientId=6`, `packPrice=10`, `baseETA=20s`
  - `SupplyItem_5_Garlic` (`id=5`): Pacote Alho (`ingredientId=10`)
- **Receitas:**
  - `Recipe_1_Mussarela` (`id=1`): `basePrice=15`, `baseBakeDuration=15s`
  - `Recipe_2_Calabresa` (`id=2`): `basePrice=20`, `baseBakeDuration=15s`
- **Parâmetros de RUN (`Entrega_3Noites` padrão):**
  - `startingBudget=500`, `startingReputation=100`, `totalNights=3`, `includeNight0Tutorial=false`.
  - Noite 1: `660s`, metas `[60, 96, 129, 159]` (1–4 humanos).
  - Noite 2: `720s`, metas `[110, 176, 236, 292]`.
  - Noite 3: `780s`, metas `[170, 272, 365, 450]`.

# Global Cel Shading — Phase 0 Coverage & Baseline Audit

- **Timestamp:** 2026-11-11 03:54:00 UTC
- **Unity:** 6000.3.6f1
- **Active Render Pipeline:** `PC_RPAsset`
- **Active Renderer Data:** `PC_Renderer` (`Assets/Settings/PC_Renderer.asset`)
- **All Distant Lands Illustrate Shaders Present:** `True`
- **Renderers Inspected:** 13602 | **Slots:** 17568 | **Unique Materials:** 114
- **Classification Breakdown:** Convert=17531, Preserve/Skip=37, ManualReview=0
- **CityModuleCatalog Pieces:** 12 | **ItemPresentation Profiles:** 14 | **Network Prefabs:** 16

## Warnings
- Broken/missing material at 'Canonical:PizzeriaManual' -> 'Comodo_02_Cozinha/Equipamentos/Forno_01/OvenSmokeParticles[0]'.
- Broken/missing material at 'Canonical:PizzeriaManual' -> 'Comodo_02_Cozinha/Equipamentos/Forno_02/OvenSmokeParticles[0]'.
- Broken/missing material at 'Canonical:PizzeriaManual' -> 'Comodo_02_Cozinha/Equipamentos/Forno_03/OvenSmokeParticles[0]'.
- Broken/missing material at 'ItemCatalog:PizzaBox' -> 'BoxMesh[0]'.
- Broken/missing material at 'NetPrefab:PizzeriaModular_Manual' -> 'Comodo_02_Cozinha/Equipamentos/Forno_01/OvenSmokeParticles[0]'.
- Broken/missing material at 'NetPrefab:PizzeriaModular_Manual' -> 'Comodo_02_Cozinha/Equipamentos/Forno_02/OvenSmokeParticles[0]'.
- Broken/missing material at 'NetPrefab:PizzeriaModular_Manual' -> 'Comodo_02_Cozinha/Equipamentos/Forno_03/OvenSmokeParticles[0]'.
- Broken/missing material at 'Canonical:GameScene' -> 'Blocks/Block_0/The_Pizzeria_Building/Comodo_02_Cozinha/Equipamentos/Forno_01/OvenSmokeParticles[0]'.
- Broken/missing material at 'Canonical:GameScene' -> 'Blocks/Block_0/The_Pizzeria_Building/Comodo_02_Cozinha/Equipamentos/Forno_02/OvenSmokeParticles[0]'.
- Broken/missing material at 'Canonical:GameScene' -> 'Blocks/Block_0/The_Pizzeria_Building/Comodo_02_Cozinha/Equipamentos/Forno_03/OvenSmokeParticles[0]'.

## Sample Audited Slots (First 120)
| Context | Object | Slot | Material | Shader | Category | Action | Confidence |
|---|---|---|---|---|---|---|---|
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Equipamentos/Lixeira_01/lata de lixo.001` | 0 | `Mat_Gen_Lixeira` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Equipamentos/Lixeira_01/tampa do lixo.001` | 0 | `Mat_Gen_Lixeira` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Equipamentos/Lixeira_02/lata de lixo.002` | 0 | `Mat_Gen_Lixeira` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Equipamentos/Lixeira_02/tampa do lixo.002` | 0 | `Mat_Gen_Lixeira` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Equipamentos/Forno_01/forno.001` | 0 | `Mat_Gen_FornoLenha` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Equipamentos/Forno_01/portaforno.001` | 0 | `Mat_Gen_PortaForno` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Equipamentos/Forno_01/OvenSmokeParticles` | 0 | `<Null Material>` | `<Missing Shader>` | `Preserve` | Preserve | 1,00 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Equipamentos/Forno_02/forno.002` | 0 | `Mat_Gen_FornoLenha` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Equipamentos/Forno_02/portaforno.002` | 0 | `Mat_Gen_PortaForno` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Equipamentos/Forno_02/OvenSmokeParticles` | 0 | `<Null Material>` | `<Missing Shader>` | `Preserve` | Preserve | 1,00 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Equipamentos/Forno_03/forno.003` | 0 | `Mat_Gen_FornoLenha` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Equipamentos/Forno_03/portaforno.003` | 0 | `Mat_Gen_PortaForno` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Equipamentos/Forno_03/OvenSmokeParticles` | 0 | `<Null Material>` | `<Missing Shader>` | `Preserve` | Preserve | 1,00 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Equipamentos/Geladeira_02/Geladeira.002` | 0 | `Mat_Gen_Geladeira` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Equipamentos/Geladeira_02/DoorPivot_Upper/portamaiorgeldeira.001` | 0 | `Mat_Gen_Geladeira` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Equipamentos/Geladeira_02/DoorPivot_Lower/portapequenageldeira.001` | 0 | `Mat_Gen_Geladeira` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Equipamentos/Geladeira_04/Geladeira.004` | 0 | `Mat_Gen_Geladeira` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Equipamentos/Geladeira_04/DoorPivot_Upper/portamaiorgeldeira.002` | 0 | `Mat_Gen_Geladeira` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Equipamentos/Geladeira_04/DoorPivot_Lower/portapequenageldeira.002` | 0 | `Mat_Gen_Geladeira` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Equipamentos/Dispensa_01/armario.001` | 0 | `Mat_Gen_Armario` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Equipamentos/Dispensa_02/armario.001 (1)` | 0 | `Mat_Gen_Armario` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Quadro de Comandas/cardapio.001` | 0 | `Mat_Gen_CardapioPrancheta` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Quadro de Comandas/cardapio.001/TicketSlots/TicketSlot_0/SheetText` | 0 | `LiberationSans SDF Material` | `TextMeshPro/Mobile/Distance Field` | `Preserve` | Preserve | 1,00 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Quadro de Comandas/cardapio.001/TicketSlots/TicketSlot_1/SheetText` | 0 | `LiberationSans SDF Material` | `TextMeshPro/Mobile/Distance Field` | `Preserve` | Preserve | 1,00 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Quadro de Comandas/cardapio.001/TicketSlots/TicketSlot_2/SheetText` | 0 | `LiberationSans SDF Material` | `TextMeshPro/Mobile/Distance Field` | `Preserve` | Preserve | 1,00 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Quadro de Comandas/cardapio.001/TicketSlots/TicketSlot_3/SheetText` | 0 | `LiberationSans SDF Material` | `TextMeshPro/Mobile/Distance Field` | `Preserve` | Preserve | 1,00 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Quadro de Comandas/cardapio.001/TicketSlots/TicketSlot_4/SheetText` | 0 | `LiberationSans SDF Material` | `TextMeshPro/Mobile/Distance Field` | `Preserve` | Preserve | 1,00 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Quadro de Comandas/cardapio.001/TicketSlots/TicketSlot_5/SheetText` | 0 | `LiberationSans SDF Material` | `TextMeshPro/Mobile/Distance Field` | `Preserve` | Preserve | 1,00 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Arquitetura/Aberturas/ConjuntoPorta_03/batenteportapequena.001` | 0 | `Mat_Gen_Madeira` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Arquitetura/Paredes/paredecomum.021` | 0 | `Mat_Gen_ParedeClara` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Arquitetura/Paredes/paredecomum.022` | 0 | `Mat_Gen_ParedeClara` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Arquitetura/Paredes/paredecomum.023` | 0 | `Mat_Gen_ParedeClara` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Arquitetura/Paredes/paredecomum.024` | 0 | `Mat_Gen_ParedeClara` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Arquitetura/Paredes/paredecomum.025` | 0 | `Mat_Gen_ParedeClara` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Arquitetura/Paredes/paredecomum.026` | 0 | `Mat_Gen_ParedeClara` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Arquitetura/Paredes/paredecomum.027` | 0 | `Mat_Gen_ParedeClara` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Arquitetura/Paredes/paredecomum.028` | 0 | `Mat_Gen_ParedeClara` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Arquitetura/Paredes/paredecomum.029` | 0 | `Mat_Gen_ParedeClara` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Arquitetura/Paredes/paredecomum.030` | 0 | `Mat_Gen_ParedeClara` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Arquitetura/Paredes/paredecomum.032` | 0 | `Mat_Gen_ParedeClara` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Arquitetura/Paredes/paredecomum.035` | 0 | `Mat_Gen_ParedeClara` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Arquitetura/Paredes/paredecomum.040` | 0 | `Mat_Gen_ParedeClara` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Arquitetura/Paredes/paredecomum.041` | 0 | `Mat_Gen_ParedeClara` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Arquitetura/Paredes/paredecomum.043` | 0 | `Mat_Gen_ParedeClara` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Arquitetura/Paredes/paredecomum.045` | 0 | `Mat_Gen_ParedeClara` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Arquitetura/Paredes/paredecomum.046` | 0 | `Mat_Gen_ParedeClara` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Arquitetura/Paredes/paredecomum.047` | 0 | `Mat_Gen_ParedeClara` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Arquitetura/Paredes/paredecomum.049` | 0 | `Mat_Gen_ParedeClara` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Arquitetura/Paredes/paredecomum.050` | 0 | `Mat_Gen_ParedeClara` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Arquitetura/Paredes/paredecomum.051` | 0 | `Mat_Gen_ParedeClara` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Arquitetura/Paredes/paredecomum.052` | 0 | `Mat_Gen_ParedeClara` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Arquitetura/Paredes/paredecomum.053` | 0 | `Mat_Gen_ParedeClara` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Arquitetura/Paredes/paredecomum.054` | 0 | `Mat_Gen_ParedeClara` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Arquitetura/Paredes/paredecomum.055` | 0 | `Mat_Gen_ParedeClara` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Arquitetura/Paredes/paredecomum.056` | 0 | `Mat_Gen_ParedeClara` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Arquitetura/Paredes/paredecomum.057` | 0 | `Mat_Gen_ParedeClara` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Arquitetura/Paredes/paredecomum.058` | 0 | `Mat_Gen_ParedeClara` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Arquitetura/Paredes/paredecomum.060` | 0 | `Mat_Gen_ParedeClara` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Arquitetura/Paredes/Externas/paredeportapequena.001` | 0 | `Mat_Gen_Madeira` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Arquitetura/Paredes/Externas/paredesuperiorexterna.022` | 0 | `Mat_Gen_ParedeClara` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Arquitetura/Paredes/Externas/paredesuperiorexterna.023` | 0 | `Mat_Gen_ParedeClara` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Arquitetura/Paredes/Externas/paredesuperiorexterna.024` | 0 | `Mat_Gen_ParedeClara` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Arquitetura/Paredes/Externas/paredesuperiorexterna.025` | 0 | `Mat_Gen_ParedeClara` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Arquitetura/Paredes/Externas/paredesuperiorexterna.026` | 0 | `Mat_Gen_ParedeClara` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Arquitetura/Paredes/Externas/paredesuperiorexterna.027` | 0 | `Mat_Gen_ParedeClara` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Arquitetura/Paredes/Externas/paredesuperiorexterna.028` | 0 | `Mat_Gen_ParedeClara` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Arquitetura/Paredes/Externas/paredesuperiorexterna.029` | 0 | `Mat_Gen_ParedeClara` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Arquitetura/Paredes/Externas/paredesuperiorexterna.031` | 0 | `Mat_Gen_ParedeClara` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Arquitetura/Paredes/Externas/paredesuperiorexterna.032` | 0 | `Mat_Gen_ParedeClara` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Arquitetura/Paredes/Externas/paredesuperiorexterna.033` | 0 | `Mat_Gen_ParedeClara` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Arquitetura/Paredes/Externas/paredesuperiorexterna.034` | 0 | `Mat_Gen_ParedeClara` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Arquitetura/Paredes/Externas/paredesuperiorexterna.035` | 0 | `Mat_Gen_ParedeClara` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Arquitetura/Paredes/Externas/paredesuperiorexterna.036` | 0 | `Mat_Gen_ParedeClara` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Arquitetura/Paredes/Externas/paredesuperiorexterna.037` | 0 | `Mat_Gen_ParedeClara` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Arquitetura/Paredes/Externas/paredesuperiorexterna.038` | 0 | `Mat_Gen_ParedeClara` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Arquitetura/Paredes/Externas/paredesuperiorexterna.039` | 0 | `Mat_Gen_ParedeClara` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Arquitetura/Paredes/Externas/paredesuperiorexterna.040` | 0 | `Mat_Gen_ParedeClara` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Arquitetura/Paredes/Externas/paredesuperiorexterna.041` | 0 | `Mat_Gen_ParedeClara` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Arquitetura/Paredes/Divisorias_Internas/paredesuperiorinterna.010` | 0 | `Mat_Gen_ParedeClara` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Arquitetura/Paredes/Divisorias_Internas/paredesuperiorinterna.011` | 0 | `Mat_Gen_ParedeClara` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Arquitetura/Paredes/Divisorias_Internas/paredesuperiorinterna.012` | 0 | `Mat_Gen_ParedeClara` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Arquitetura/Paredes/Divisorias_Internas/paredesuperiorinterna.013` | 0 | `Mat_Gen_ParedeClara` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Arquitetura/Paredes/Divisorias_Internas/paredesuperiorinterna.014` | 0 | `Mat_Gen_ParedeClara` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Arquitetura/Paredes/Divisorias_Internas/paredesuperiorinterna.015` | 0 | `Mat_Gen_ParedeClara` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Arquitetura/Paredes/Divisorias_Internas/paredesuperiorinterna.016` | 0 | `Mat_Gen_ParedeClara` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Arquitetura/Paredes/Divisorias_Internas/paredesuperiorinterna.017` | 0 | `Mat_Gen_ParedeClara` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Arquitetura/Paredes/Divisorias_Internas/paredesuperiorinterna.018` | 0 | `Mat_Gen_ParedeClara` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Arquitetura/Paredes/Divisorias_Internas/paredesuperiorinterna.019` | 0 | `Mat_Gen_ParedeClara` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Arquitetura/Paredes/Divisorias_Internas/paredesuperiorinterna.020` | 0 | `Mat_Gen_ParedeClara` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Arquitetura/Estrutura/pilastra.007` | 0 | `Mat_Gen_ParedeClara` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Arquitetura/Estrutura/pilastra.008` | 0 | `Mat_Gen_ParedeClara` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Mobiliario/mesa media.001` | 0 | `Mat_Gen_Mesa` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Mobiliario/mesa media.001/suportefaca.001` | 0 | `Mat_Gen_Faqueiro` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Mobiliario/mesa media.001/suportefaca.002` | 0 | `Mat_Gen_Faqueiro` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Mobiliario/mesa media.001/suportefaca.003` | 0 | `Mat_Gen_Faqueiro` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Mobiliario/mesa media.001/suportefaca.005` | 0 | `Mat_Gen_Faqueiro` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Mobiliario/mesa media.001/faca.001` | 0 | `Mat_Gen_Faca` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Mobiliario/mesa media.001/faca.002` | 0 | `Mat_Gen_Faca` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Mobiliario/mesa media.001/faca.003` | 0 | `Mat_Gen_Faca` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Mobiliario/mesa media.001/faca.004` | 0 | `Mat_Gen_Faca` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Mobiliario/mesa media.001/faca.005` | 0 | `Mat_Gen_Faca` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Mobiliario/mesa media.001/faca.006` | 0 | `Mat_Gen_Faca` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Mobiliario/mesa media.001/faca.007` | 0 | `Mat_Gen_Faca` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Mobiliario/mesa media.001/faca.008` | 0 | `Mat_Gen_Faca` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Mobiliario/mesa media.001/tabua de cortar.002` | 0 | `Mat_Gen_TabuaCorte` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Mobiliario/mesa media.001/tabua de cortar.003` | 0 | `Mat_Gen_TabuaCorte` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Mobiliario/mesa media.001/tabua de cortar.004` | 0 | `Mat_Gen_TabuaCorte` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Mobiliario/mesa media.001/tabua de cortar.005` | 0 | `Mat_Gen_TabuaCorte` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Mobiliario/mesa media.002` | 0 | `Mat_Gen_Mesa` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Mobiliario/mesa media.002/panela e concha.001` | 0 | `Mat_Gen_PanelaConcha` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Mobiliario/mesa media.002/panela e concha.002` | 0 | `Mat_Gen_PanelaConcha` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Mobiliario/mesa media.002/panela e concha.003` | 0 | `Mat_Gen_PanelaConcha` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Mobiliario/mesa media.002/panela e concha.004` | 0 | `Mat_Gen_PanelaConcha` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Mobiliario/mesa media.002/prato/forma.001` | 0 | `Mat_Gen_FormaPizza` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Mobiliario/mesa media.002/prato/forma.002` | 0 | `Mat_Gen_FormaPizza` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Mobiliario/mesa media.002/prato/forma.003` | 0 | `Mat_Gen_FormaPizza` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Mobiliario/mesa media.002/prato/forma.004` | 0 | `Mat_Gen_FormaPizza` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Utensilios/caixa.001` | 0 | `Mat_Gen_CaixaPizza` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Utensilios/caixa.002` | 0 | `Mat_Gen_CaixaPizza` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |
| `Canonical:PizzeriaManual` | `Comodo_02_Cozinha/Utensilios/caixa.003` | 0 | `Mat_Gen_CaixaPizza` | `Distant Lands/Illustrate/Opaque` | `Architecture` | Convert | 0,96 |

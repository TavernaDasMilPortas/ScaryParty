# Relatório de Correção Pós-Auditoria — ScaryParty (Rodada R2)

- **Data de Início:** 19/11/2026
- **Branch de Origem:** `fix/auditoria-marcos-noturnos`
- **SHA Inicial (Baseline):** `615990b8b9752b5881019e6987b3d118aeb0a0f7`
- **Branch de Correção:** `fix/auditoria-marcos-noturnos-r2`
- **Versão Unity:** `6000.3.6f1` (`UNITY_EXECUTION=MANUAL_ONLY`)

---

## Matriz de Rastreabilidade R01–R14

| Achado | Bloco | Status | Commit | Teste Automatizado | Teste Manual | Evidência | Pendência |
|---|---|---|---|---|---|---|---|
| **R01** Loading preso | P1 | Código integrado (validação Editor pendente) | `b6622596` | `LoadingTransitionStateMachineTests` (EditMode pendente no Editor) | Testar Relay inválido, LAN indisponível, cancelar e retry | `LoadingTransitionStateMachine` + `LobbyManager` + `PizzeriaSceneLoadingCoordinator` (botão Cancelar, Retry, tokens e liberação única de input) | Rodar `LoadingTransitionStateMachineTests` e bateria manual no Unity Editor |
| **R02** Spawn bloqueado aceito | P3 | Código integrado (validação Editor pendente) | `f377b769` | `SpawnReservationTests` (EditMode pendente no Editor) | Testar obstáculo sobre anchor, bake atrasado e contratação durante rebuild | `SpawnResolver.TryResolveCandidate` + `ValidateUnityCandidate` + fila/backoff em `EmployeePopulationDirector` + gate `NavigationWorldManager.IsReady` | Rodar `SpawnReservationTests` e bateria manual no Editor |
| **R03** Reserva/âncoras | P3 / P7 | Código integrado (prefab/Editor pendente) | `f377b769` / `4c8a04bf` | `SpawnReservationTests`, `PizzeriaPatioLayoutTests` (EditMode pendente) | Testar entrada/saída repetida, contratar/remover e aplicar no prefab | `SpawnReservationService` com reserva por dono + `PizzeriaParkingLotBuilder` + `PizzeriaPatioLayoutWizard.ApplyToSelectedPrefab` | Aplicar Wizard no prefab `PizzeriaModular_Manual.prefab` e validar no Editor |
| **R04** Carro sem collider cliente | P5 | Código integrado (validação Editor/Host+Cliente pendente) | `ee8f230b` | `TrafficGeometryAndPhysicsTests` (EditMode pendente no Editor) | Testar colisão em carro parado/em movimento no Host e Cliente | Proxy cliente com `BoxCollider` sólido (`VehicleDimensions`) + `Rigidbody` cinemático interpolado, sem dano local duplicado | Rodar testes e validar Host + Cliente no Editor |
| **R05** Carro invade calçada | P5 | Código integrado (validação Editor/PlayMode pendente) | `ee8f230b` | `TrafficGeometryAndPhysicsTests` (EditMode pendente no Editor) | Testar retas, curvas 90° e cruzamentos em PlayMode | `ComputeSafeLaneOffset`, `BuildLaneAndCornerWaypoints` (Bézier + clamp ao corredor), `IsEnvelopeContainedInCorridor` e `BoxCastNonAlloc` | Rodar testes e validar visualmente no Editor |
| **R06** Muralha sobre rua | P6 | Código integrado (validação Editor pendente) | `21d2fc31` | `CityBorderValidationTests` (EditMode pendente no Editor) | Inspecionar múltiplas seeds e remover parede proposital | `TryPlaceWallSegment` + subdivisão + `rejectedSegments`/`streetOverlapCount` | Rodar testes e inspecionar seeds no Unity Editor |
| **R07** Validador falso positivo | P6 | Código integrado (validação Editor pendente) | `21d2fc31` | `CityBorderValidationTests` (EditMode pendente no Editor) | Testar remoção manual de parede para confirmar reprovação | `ComputeGapMetrics` + `IsComplete` exigindo `totalEdgesEvaluated > 0`, `maxUncoveredGapMeters <= 0.25f` e `streetOverlapCount == 0` | Rodar testes e validar no Unity Editor |
| **R08** Piso do pátio rotacionado | P7 | Código integrado (validação Editor pendente) | `4c8a04bf` | `PizzeriaPatioLayoutTests` (EditMode pendente no Editor) | Testar rotações 0°, 90°, 180°, 270° no Editor | `PizzeriaParkingLotBuilder` mantém piso no frame local do footprint e separa `ExteriorExitFrame` | Rodar `PizzeriaPatioLayoutTests` no Unity Editor |
| **R09** Wizard L/T incompleto | P7 | Código integrado (aplicação no prefab pendente) | `4c8a04bf` | `PizzeriaPatioLayoutTests` (EditMode pendente no Editor) | Abrir Wizard `Pátio e Layout Modular (L-T)` e aplicar no prefab `PizzeriaModular_Manual.prefab` | Preview L/T, scan de peças, `ApplyToSelectedPrefab` via `PrefabUtility`, validação de overlap e Undo | Executar Wizard no Unity Editor para persistir no prefab |
| **R10** Scripts sem `.meta` | P0 | Parcial (metas existentes versionados; novos `.cs` aguardam refresh do Editor) | `9d25b69e` | `AuditR2BaselineTests` (EditMode pendente no Editor) | Verificar importação limpa no Unity Editor | `.meta` de `PizzeriaExteriorAnchors.cs` e `PizzeriaPatioLayoutWizard.cs` versionados; `PizzeriaLayoutProfile` separado | Versionar `.meta` gerados pelo Unity Editor para os novos scripts R2 |
| **R11** Feel sem emissores | P4 / P10 | Código integrado (validação Editor/Host+Cliente pendente) | `5ec2e7df` / `3c92a9c8` | `GameFeelTransversalTests` (EditMode pendente no Editor) | Testar os 14 eventos com Reduce Motion ligado e desligado | 14 eventos ligados a emissores reais + `PizzeriaSoundManager` + `GameFeelPresenter.TriggerAtPosition` + deduplicação | Rodar `GameFeelTransversalTests` e validar no Editor |
| **R12** Moto depende de RPC | P9 | Código integrado (validação Editor/Host+Cliente pendente) | `bc305d51` | `NetworkCombatAndBikeTests` (EditMode pendente no Editor) | Testar moto em 10/50/100 RPC/s e armas em cena densa | `MotorcycleMovementBudget` temporal independente da taxa de RPC + fallback `RaycastAll` + `WeaponRaycastOverflowCount` | Rodar `NetworkCombatAndBikeTests` e bateria manual no Editor |
| **R13** Compra parcial | P8 | Código integrado (validação Editor pendente) | `b966b1c0` | `RunIntermissionNetworkTests` (EditMode pendente no Editor) | Testar loja com duplicata, capacidade exata, troca de aba e timeout | Agregação por `IngredientId`, rollback em divergência e estado pendente estático em `RunShopPagePresenter` | Rodar `RunIntermissionNetworkTests` e bateria manual no Editor |
| **R14** Readiness permissivo | P1 / P2 | Código integrado (validação Editor pendente) | `b6622596` / `65a2398c` | `SceneReadinessContractTests` (EditMode pendente no Editor) | Testar loading com dependência ausente e transição Menu→Lobby→Ready→Gameplay | `SceneReadinessContract` + `StageTimingRecorder` integrados em `PizzeriaSceneLoadingCoordinator` (obrigatório vs opcional + diagnóstico de timeout) | Rodar `SceneReadinessContractTests` e bateria manual no Unity Editor |

---

## Registro por Bloco

### Bloco P0 — Baseline, rastreabilidade e segurança de assets
- **Branch criada:** `fix/auditoria-marcos-noturnos-r2` a partir de `615990b8b9752b5881019e6987b3d118aeb0a0f7`.
- **Inventário de referências para `PizzeriaLayoutProfile`:** busca por GUID `5882b9758e4b3fd45a34fd022ae34400` e por tipo em `ScaryParty/Assets` confirmou zero `.asset`, `.prefab` ou `.unity` serializando `PizzeriaLayoutProfile` antes da separação.
- **Separação:** `PizzeriaLayoutProfile`, `PizzeriaLayoutVariant` e `ModularPieceCatalogEntry` movidos para `ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Composition/PizzeriaLayoutProfile.cs` mantendo namespace `ScaryParty.Pizzeria.Composition`.
- **Metas versionados:** `PizzeriaExteriorAnchors.cs.meta` e `PizzeriaPatioLayoutWizard.cs.meta`.
- **Pendência de Editor:** Ao abrir/focar o Unity Editor, gerar e versionar `.meta` de `PizzeriaLayoutProfile.cs` e `AuditR2BaselineTests.cs`, e rodar `AuditR2BaselineTests` no Test Runner EditMode.

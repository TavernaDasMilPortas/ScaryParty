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
| **R01** Loading preso | P1 | Código integrado (validação Editor pendente) | P1 | `LoadingTransitionStateMachineTests` (EditMode pendente no Editor) | Testar Relay inválido, LAN indisponível, cancelar e retry | `LoadingTransitionStateMachine` + `LobbyManager` + `PizzeriaSceneLoadingCoordinator` (botão Cancelar, Retry, tokens e liberação única de input) | Rodar `LoadingTransitionStateMachineTests` e bateria manual no Unity Editor |
| **R02** Spawn bloqueado aceito | P3 | Código integrado (validação Editor pendente) | P3 | `SpawnReservationTests` (EditMode pendente no Editor) | Testar obstáculo sobre anchor, bake atrasado e contratação durante rebuild | `SpawnResolver.TryResolveCandidate` + `ValidateUnityCandidate` + fila/backoff em `EmployeePopulationDirector` + gate `NavigationWorldManager.IsReady` | Rodar `SpawnReservationTests` e bateria manual no Editor |
| **R03** Reserva/âncoras | P3 / P7 | Parcial (serviço P3 integrado; prefab P7 pendente) | P3 | `SpawnReservationTests` (EditMode pendente no Editor) | Testar entrada/saída repetida e contratar/remover/recontratar | `SpawnReservationService` com reserva por dono, capacidade explícita e release em remoção | Autorar âncoras no prefab (P7) e validar no Editor |
| **R04** Carro sem collider cliente | P5 | Código integrado (validação Editor/Host+Cliente pendente) | P5 | `TrafficGeometryAndPhysicsTests` (EditMode pendente no Editor) | Testar colisão em carro parado/em movimento no Host e Cliente | Proxy cliente com `BoxCollider` sólido (`VehicleDimensions`) + `Rigidbody` cinemático interpolado, sem dano local duplicado | Rodar testes e validar Host + Cliente no Editor |
| **R05** Carro invade calçada | P5 | Código integrado (validação Editor/PlayMode pendente) | P5 | `TrafficGeometryAndPhysicsTests` (EditMode pendente no Editor) | Testar retas, curvas 90° e cruzamentos em PlayMode | `ComputeSafeLaneOffset`, `BuildLaneAndCornerWaypoints` (Bézier + clamp ao corredor), `IsEnvelopeContainedInCorridor` e `BoxCastNonAlloc` | Rodar testes e validar visualmente no Editor |
| **R06** Muralha sobre rua | P6 | Código integrado (validação Editor pendente) | P6 | `CityBorderValidationTests` (EditMode pendente no Editor) | Inspecionar múltiplas seeds e remover parede proposital | `TryPlaceWallSegment` + subdivisão + `rejectedSegments`/`streetOverlapCount` | Rodar testes e inspecionar seeds no Unity Editor |
| **R07** Validador falso positivo | P6 | Código integrado (validação Editor pendente) | P6 | `CityBorderValidationTests` (EditMode pendente no Editor) | Testar remoção manual de parede para confirmar reprovação | `ComputeGapMetrics` + `IsComplete` exigindo `totalEdgesEvaluated > 0`, `maxUncoveredGapMeters <= 0.25f` e `streetOverlapCount == 0` | Rodar testes e validar no Unity Editor |
| **R08** Piso do pátio rotacionado | P7 | Código integrado (validação Editor pendente) | P7 | `PizzeriaPatioLayoutTests` (EditMode pendente no Editor) | Testar rotações 0°, 90°, 180°, 270° no Editor | `PizzeriaParkingLotBuilder` mantém piso no frame local do footprint e separa `ExteriorExitFrame` | Rodar `PizzeriaPatioLayoutTests` no Unity Editor |
| **R09** Wizard L/T incompleto | P7 | Código integrado (aplicação no prefab pendente) | P7 | `PizzeriaPatioLayoutTests` (EditMode pendente no Editor) | Abrir Wizard `Pátio e Layout Modular (L-T)` e aplicar no prefab `PizzeriaModular_Manual.prefab` | Preview L/T, scan de peças, `ApplyToSelectedPrefab` via `PrefabUtility`, validação de overlap e Undo | Executar Wizard no Unity Editor para persistir no prefab |
| **R10** Scripts sem `.meta` | P0 | Em progresso | P0 | `AuditR2BaselineTests` (EditMode pendente no Editor) | Verificar importação limpa no Unity Editor | `.meta` de `PizzeriaExteriorAnchors.cs` e `PizzeriaPatioLayoutWizard.cs` versionados; `PizzeriaLayoutProfile` separado | Versionar `.meta` de `PizzeriaLayoutProfile.cs` e `AuditR2BaselineTests.cs` após refresh do Unity Editor |
| **R11** Feel sem emissores | P4 / P10 | Pendente | — | Não executado | Pendente | — | Ligar 14 eventos obrigatórios com emissor e receita |
| **R12** Moto depende de RPC | P9 | Pendente | — | Não executado | Pendente | — | Orçamento temporal + saturação `RaycastNonAlloc` |
| **R13** Compra parcial | P8 | Pendente | — | Não executado | Pendente | — | Agregação transacional + estado pendente persistente |
| **R14** Readiness permissivo | P1 / P2 | Código integrado (validação Editor pendente) | P2 | `SceneReadinessContractTests` (EditMode pendente no Editor) | Testar loading com dependência ausente e transição Menu→Lobby→Ready→Gameplay | `SceneReadinessContract` + `StageTimingRecorder` integrados em `PizzeriaSceneLoadingCoordinator` (obrigatório vs opcional + diagnóstico de timeout) | Rodar `SceneReadinessContractTests` e bateria manual no Unity Editor |

---

## Registro por Bloco

### Bloco P0 — Baseline, rastreabilidade e segurança de assets
- **Branch criada:** `fix/auditoria-marcos-noturnos-r2` a partir de `615990b8b9752b5881019e6987b3d118aeb0a0f7`.
- **Inventário de referências para `PizzeriaLayoutProfile`:** busca por GUID `5882b9758e4b3fd45a34fd022ae34400` e por tipo em `ScaryParty/Assets` confirmou zero `.asset`, `.prefab` ou `.unity` serializando `PizzeriaLayoutProfile` antes da separação.
- **Separação:** `PizzeriaLayoutProfile`, `PizzeriaLayoutVariant` e `ModularPieceCatalogEntry` movidos para `ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Composition/PizzeriaLayoutProfile.cs` mantendo namespace `ScaryParty.Pizzeria.Composition`.
- **Metas versionados:** `PizzeriaExteriorAnchors.cs.meta` e `PizzeriaPatioLayoutWizard.cs.meta`.
- **Pendência de Editor:** Ao abrir/focar o Unity Editor, gerar e versionar `.meta` de `PizzeriaLayoutProfile.cs` e `AuditR2BaselineTests.cs`, e rodar `AuditR2BaselineTests` no Test Runner EditMode.

# Relatório M2 — Cidade, Navegação e Trânsito

**Status:** IMPLEMENTAÇÃO CONCLUÍDA — VALIDAÇÃO PENDENTE  
**Data:** 2026-11-16  
**Branch / Commit M2 (código):** `release/marcos-noturnos` @ `f8190ef4`

---

## 1. Causas Diagnosticadas e Corrigidas

1. **Replicação de Trânsito (`TrafficManager` sem `NetworkObject`):**
   - *Causa:* `CityGenerator` cria `_cityRoot` sem `NetworkObject` e adiciona `TrafficManager : NetworkBehaviour` em runtime, mantendo `IsSpawned == false` e bloqueando `SyncVehiclesClientRpc`.
   - *Correção:* `CityGenerator` agora atua como relay autoritativo (`BroadcastTrafficStates` → `SyncTrafficVehiclesClientRpc` → `TrafficManager.ApplySyncedVehicleStatesFromRelay`), mantendo compatibilidade caso `TrafficManager` venha a ser spawnado diretamente no futuro.
2. **Sensores, Colliders Filhos (A11), Faixas e Pedestres (`TrafficVehicle.cs`):**
   - `CheckVehicleAhead()` substituído por `CheckObstacleAhead()` com `Physics.RaycastNonAlloc` ignorando colliders do próprio veículo, detectando `GetComponentInParent<TrafficVehicle>()` (A11), ignorando fluxo em sentido oposto (`Dot <= -0.35f`) e freando preventivamente diante de `PedestrianNpc`, `PlayerState` e `DeliveryMotorcycleController`.
   - Colisão real separada da frenagem preventiva em `OnTriggerEnter` (somente servidor/local, `speed >= minCollisionSpeed`, cooldown de `2.0s` por alvo), aplicando `ReactToHorn` em pedestres e `DamageSourceType.Collision` (`collisionDamage = 15`) em `IDamageable`.
3. **Semáforos e Diagnóstico (`TrafficManager.cs` / `TrafficVehicle.cs`):**
   - `AssignRandomPath` agora resolve `CurrentEdgeId` pela aresta mais próxima quando o A* retorna rota completa.
   - Adicionado `TrafficStopReason` (`None`, `NoPath`, `TrafficLight`, `VehicleAhead`, `PedestrianOrPlayerAhead`) e overlay opcional via tecla **`F7`** (`_showDebugOverlay`) sem flood de logs.
4. **Cobertura de Teste EditMode:**
   - Adicionado `TrafficVehicle_AdvancesWaypointsAndDetectsChildColliderAhead` em [CityGraphPathfinderTests.cs](file:///c:/Users/Rubens/Documents/GitHub/ScaryParty/ScaryParty/Assets/Piaaria/Scripts/City/Editor/CityGraphPathfinderTests.cs).

---

## 2. Validação e Pendências (PlayMode / Editor)

- **Executado:** `git diff --check` limpo.
- **Pendente no Unity Editor aberto (`T19–T28`, `T39–T40`, 5 seeds):**
  - Rodar `CityGraphPathfinderTests` e `ModularCityTests` no Test Runner EditMode.
  - Em PlayMode (Host + Cliente ParrelSync), pressionar `F7` para confirmar `CurrentStopReason`, movimento contínuo no host e interpolação nos clientes remotos, além de ciclos de semáforos e travessia de pedestres.

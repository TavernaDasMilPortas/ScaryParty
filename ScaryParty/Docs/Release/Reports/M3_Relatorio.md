# Relatório M3 — Mãos, Armas, Bandoleira e Moto

**Status:** IMPLEMENTAÇÃO CONCLUÍDA — VALIDAÇÃO PENDENTE  
**Data:** 2026-11-16  
**Branch / Commit M3 (código):** `release/marcos-noturnos` @ `557632c6`

---

## 1. Implementação Realizada

1. **IDs de Instância de Armas e Bandoleira (`NetDtos.cs`, `PizzeriaNetworkState.cs`, `PizzeriaCommandHandler.cs`):**
   - `NetWeaponStateDto` agora possui `WeaponId` (`uint`) e `IsSlinged` (`bool`).
   - `_lastWeaponFireServerTime` em `PizzeriaCommandHandler` agora é indexado por `WeaponId` (**A08**).
   - `ToggleSlingShotgunServerRpc` (tecla `[G]` em `WeaponController`) guarda a Escopeta na bandoleira (`IsSlinged = true`), liberando ambas as mãos sem devolvê-la ao rack nem repor munição, e realiza o saque como transação atômica (exige não estar pilotando e ambas as mãos livres).
2. **Reserva Unificada de Mãos: Moto + Pistola (`PizzeriaNetworkState.cs`, `PizzeriaCommandHandler.cs`, `WeaponController.cs`, `PlayerInventoryAdapter.cs`):**
   - Moto reserva a mão esquerda (`HandSlotIndex.Left`) em `PizzeriaNetworkState.IsHandOccupiedByWeapon` e `MountMotorcycleServerRpc` (**A02 / A03**).
   - Pistola (mão direita) pode ser usada, mirada e disparada durante a condução; Escopeta ativa continua exigindo 2 mãos e sendo recusada na moto.
3. **Mira ADS em 1ª Pessoa e Obstrução de Cano (`FirstPersonCameraController.cs`, `WeaponController.cs`, `PizzeriaCommandHandler.cs`):**
   - `FirstPersonCameraController.SetAimState(bool)` interpola suavemente `RightHandAnchor` (`adsRightHandOffset`) e `fieldOfView` (`adsFovDelta = -12`).
   - `FireWeaponServerRpc` verifica obstrução física entre o peito do atirador e `rayOrigin` para impedir disparo através de parede colada à arma (`T32`).
4. **Atropelamento Idempotente (`DeliveryMotorcycleController.cs`, `PizzeriaCommandHandler.cs`):**
   - `OnTriggerEnter` emite `ReportMotorcycleImpactServerRpc` no máximo uma vez por `Revision` de montaria quando `|_localSpeed| >= 3.5f`.
   - Servidor deduplica por `(st.Revision, impactEpisodeId)`, aplica dano único ao alvo `IDamageable` (`motorcycleHitDamage = 40`), desmonta o condutor para posição lateral segura (`motorcycleRiderFallDamage = 0` por padrão) e libera a mão de direção.

---

## 2. Cortes/Pendências Registrados (Roadmap M3)

- **Itens 8–10 (Authoring visual no Editor / Animator 3ª pessoa):** Transições de `IsAiming` / `WeaponMode` em `CharacterAnimator.controller` e preview de bones head/tail no wizard dependem de authoring direto no Unity Editor.
- **Item 11 (Rifle automático e Granada):** Mantidos no backlog pós-entrega base conforme permitido no roadmap.

---

## 3. Validação Pendente no Unity Editor

- `git diff --check`: **PASSOU**.
- Testar manualmente em PlayMode (Host + Cliente ParrelSync): `T12`, `T29–T42` (moto + pistola no cliente, bandoleira `[G]`, mira ADS, atropelamento idempotente).

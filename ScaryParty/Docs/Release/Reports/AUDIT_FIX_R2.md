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
| **R01** Loading preso | P1 | Pendente | — | Não executado | Pendente | — | Implementar máquina de estados explícita + recuperação |
| **R02** Spawn bloqueado aceito | P3 | Pendente | — | Não executado | Pendente | — | Implementar `TryResolveSpawn` + fila aguardando NavMesh |
| **R03** Reserva/âncoras | P3 / P7 | Pendente | — | Não executado | Pendente | — | `SpawnReservationService` + âncoras autoradas no prefab |
| **R04** Carro sem collider cliente | P5 | Pendente | — | Não executado | Pendente | — | Proxy físico sólido no cliente + impacto autoritativo único |
| **R05** Carro invade calçada | P5 | Pendente | — | Não executado | Pendente | — | Trajetória derivada da geometria da rua + envelope |
| **R06** Muralha sobre rua | P6 | Pendente | — | Não executado | Pendente | — | Rejeição de OBB real sobre rua/calçada |
| **R07** Validador falso positivo | P6 | Pendente | — | Não executado | Pendente | — | Medição final do perímetro construído |
| **R08** Piso do pátio rotacionado | P7 | Pendente | — | Não executado | Pendente | — | Piso no frame local do footprint nas 4 rotações |
| **R09** Wizard L/T incompleto | P7 | Pendente | — | Não executado | Pendente | — | Preview, validação, peças reais, Undo e persistência no prefab |
| **R10** Scripts sem `.meta` | P0 | Em progresso | P0 | `AuditR2BaselineTests` (EditMode pendente no Editor) | Verificar importação limpa no Unity Editor | `.meta` de `PizzeriaExteriorAnchors.cs` e `PizzeriaPatioLayoutWizard.cs` versionados; `PizzeriaLayoutProfile` separado | Versionar `.meta` de `PizzeriaLayoutProfile.cs` e `AuditR2BaselineTests.cs` após refresh do Unity Editor |
| **R11** Feel sem emissores | P4 / P10 | Pendente | — | Não executado | Pendente | — | Ligar 14 eventos obrigatórios com emissor e receita |
| **R12** Moto depende de RPC | P9 | Pendente | — | Não executado | Pendente | — | Orçamento temporal + saturação `RaycastNonAlloc` |
| **R13** Compra parcial | P8 | Pendente | — | Não executado | Pendente | — | Agregação transacional + estado pendente persistente |
| **R14** Readiness permissivo | P1 / P2 | Pendente | — | Não executado | Pendente | — | Contratos de prontidão por destino + timeout recuperável |

---

## Registro por Bloco

### Bloco P0 — Baseline, rastreabilidade e segurança de assets
- **Branch criada:** `fix/auditoria-marcos-noturnos-r2` a partir de `615990b8b9752b5881019e6987b3d118aeb0a0f7`.
- **Inventário de referências para `PizzeriaLayoutProfile`:** busca por GUID `5882b9758e4b3fd45a34fd022ae34400` e por tipo em `ScaryParty/Assets` confirmou zero `.asset`, `.prefab` ou `.unity` serializando `PizzeriaLayoutProfile` antes da separação.
- **Separação:** `PizzeriaLayoutProfile`, `PizzeriaLayoutVariant` e `ModularPieceCatalogEntry` movidos para `ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Composition/PizzeriaLayoutProfile.cs` mantendo namespace `ScaryParty.Pizzeria.Composition`.
- **Metas versionados:** `PizzeriaExteriorAnchors.cs.meta` e `PizzeriaPatioLayoutWizard.cs.meta`.
- **Pendência de Editor:** Ao abrir/focar o Unity Editor, gerar e versionar `.meta` de `PizzeriaLayoutProfile.cs` e `AuditR2BaselineTests.cs`, e rodar `AuditR2BaselineTests` no Test Runner EditMode.

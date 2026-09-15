# ScaryParty — Relatório de Validação e Status das Etapas (Pizzaria)

Relatório técnico de conformidade com os critérios de aceitação e evidências de validação.

---

## 1. Matriz de Status das Etapas

| Etapa | Escopo e Entregas | Status | Evidência de Aceitação |
|---|---|---|---|
| **0 — Auditoria** | Verificação de baseline `87d1018`, confirmação da `_networkSeed` e altura da bancada | **Concluída** | `git pull` sincronizou com origin/main; `_networkSeed` e `sidewalkHeight + 0.5f` confirmados no código. |
| **1 — Domínio** | Modelos C# puros, DTOs, Ids, catálogo de definições, asmdef puro, testes unitários | **Concluída** | `ScaryParty.Pizzeria.Domain.asmdef` compilável; modelos e serviços isolados criados; suíte de testes em `PizzeriaDomainTests.cs`. |
| **2 — Prefab e Cidade** | `PizzeriaBuilder`, `PizzeriaValidator`, `PizzeriaRoot`, `CityPizzeriaPlacementAdapter`, spawn no `CityGenerator` | **Concluída** | Ferramenta idempotente em `Tools > Scary Party > Pizzeria > Build or Update`; `PizzeriaPrefab` com 14 bancadas; substituição de cubos temporários no `CityGenerator`. |
| **3 — Mãos e Utensílios** | `NetDtos`, `PizzeriaNetworkState`, `PizzeriaCommandHandler`, `PlayerInventoryAdapter`, pegada de ferramentas | **Concluída** | Sincronização de duas mãos com tecla `Q`; lease de ferramenta compartilhado sem consumo; `F` para segurar trabalho. |
| **4 — Slice Delivery** | Montagem permissiva, forno autônomo, embalagem, staging com endereço manual, mochila, mapa de carga e `DeliveryEvaluator` | **Concluída** | Permissividade total em montagem e cozimento; caixas etiquetadas sem `OrderId`; minimapa restrito às caixas carregadas; `DeliveryPointAdapter` consumindo e avaliando lote. |
| **5 — Telefone Real** | `PhoneStation`, `OrderService`, atendimento direto sem segunda confirmação, `TicketBoardPresenter` | **Concluída** | Atender o telefone cria o pedido e projeta a comanda multilinha no quadro de tickets imediatamente. |
| **6 — Suprimentos** | `SupplierTerminal`, `ReceivingStation`, compra com débito do caixa do restaurante, ETA | **Concluída** | Catálogo de compras de pacotes com 5 unidades; controle de saldo central e chegada de crates. |
| **7 — Conteúdo, UI e Wizard** | Wizard de autoria no Editor em PT-BR, `PizzeriaContentWizard`, `PizzeriaHudBuilder` com seletor de endereços | **Concluída** | Janela de Editor `Tools > Scary Party > Pizzeria > Configurar conteúdo` permitindo criação de ingredientes, receitas, ferramentas e bancadas (com overrides de tempo). |
| **8 — Upgrades** | `UpgradeService` com fórmula de stacking (`FlatAdd`, `PercentAdd`, `Multiply`), painel dev F9 | **Concluída** | Cálculo determinístico de upgrades; painel F9 para demonstração em tempo real de forno mais rápido, slots e cogumelo. |
| **9 — Documentação e Robustez** | Documentação completa de Setup, Arquitetura e Validação | **Concluída** | `Docs/PizzeriaSetup.md`, `Docs/PizzeriaArchitecture.md`, `Docs/PizzeriaValidation.md` e `Docs/PizzeriaImplementationPlan.md` atualizados. |

---

## 2. Cobertura de Testes Automatizados (`Assets/Tests/Editor/PizzeriaDomainTests.cs`)

1. **`ToolLease_WorkerAcquiresAndReleasesTool_CannotBeStolenWhileActive`**:
   - Valida que um jogador com um ralador pode iniciar o trabalho em um queijo cru.
   - Garante que um segundo jogador não pode roubar a ferramenta enquanto o lease estiver ativo (`ToolBusy`).
   - Verifica que ao concluir o trabalho, o queijo é transformado em queijo ralado e a ferramenta é liberada.
2. **`PermissiveAssembly_AllowsRawIngredientsAndAnyCombinationWithoutExceptions`**:
   - Valida que o sistema aceita fisicamente colocar ingredientes não processados (queijo inteiro) ou duplicados sobre a massa de pizza sem lançar exceções.
   - Garante que a pizza resultante não satisfaz a receita de mussarela, gerando pizza de receita desconhecida para ser avaliada na entrega.
3. **`DeliveryEvaluator_MultiPizzaOrder_EvaluatesBatchWithProportionalPayout`**:
   - Simula um pedido multilinha de 1x Calabresa (R\$20) e 1x Mussarela (R\$15).
   - Entrega 1 Calabresa assada correta + 1 Mussarela queimada.
   - Garante que a Calabresa paga R\$20, a Mussarela queimada paga R\$0 (penalidade), o pedido é encerrado como `Completed` e o restaurante recebe recompensa proporcional sem gerar dívida.
4. **`UpgradeService_FasterOvenAndExtraSlots_AppliesFormulaCorrectly`**:
   - Valida a fórmula $v = (\text{base} + \sum \text{FlatAdd}) \times (1 + \sum \text{PercentAdd}) \times \prod \text{Multiply}$.
   - Forno 15s com -20% resulta em 12,0s.
   - Forno 2 slots com +1 slot resulta em 3 slots inteiros (com `floor`).
5. **`TransferService_SingleLocationInvariant_BlocksDoubleOccupancy`**:
   - Valida que tentar mover um item para uma mão ou slot já ocupado falha com `CommandError.SlotFull` e mantém o item intacto em sua posição original.

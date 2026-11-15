# Catálogo de Eventos e Receitas de Game Feel (`GameFeelEventId`)

## Famílias de Material (`GameFeelMaterialFamily`)
- **Dough (`Massa / Pizza`):** Compressão larga (~0.79–0.81 Y), recuperação lenta (280–380ms) e rajada leve de farinha.
- **SmallBounceIngredient (`Tomate / Cogumelo / Pepperoni`):** Arcos curtos, rotação alternada por seed e pouso elástico rápido.
- **FluidGranular (`Molho / Queijo`):** Foco em espalhamento/queda dos elementos internos (`PizzaPieceEntryAnimator`) com mínima deformação do recipiente.
- **CardboardPaper (`Caixa / Comanda`):** Seco e rápido, estalo/flutter e overshoot angular moderado (520ms no empacotamento).
- **RigidMetal (`Forno / Utensílios / Telefone`):** Vibração curta sem elasticidade exagerada (`SquashScale` próximo de `1.0`), som metálico 3D.
- **FabricCargo (`Mochila`):** Compressão na inserção/retirada e balanço contínuo amortecido proporcional à velocidade horizontal e carga.
- **CharacterBody (`Clientes NPC & Jogador`):** Clamp rígido `[0.95..1.05]` e bloqueio de deslocamento vertical (`LockVerticalAndHipOffset`) quando sentado.
- **UIElement:** Tweens centralizados via `UIToolkitTweenService` (`ScaleTo` / `PulseScale`).

## Principais Eventos Mapeados
| Categoria | Eventos (`GameFeelEventId`) | Emissores Principais |
| :--- | :--- | :--- |
| Mãos & Bancadas | `FocusEnter`, `ActiveHandChanged`, `ItemPickedUp`, `ToolPickedUp`, `ItemPlaced`, `BoxPlaced`, `ActionRejected`, `StorageDispensed`, `DoorOpened`, `DoorClosed` | `PlayerInteraction`, `StationView`, `GameFeelNetworkBridge`, `NotificationManager` |
| Processos Cozinha | `ProcessStarted`, `RollDoughStep`, `SliceTomatoPepperoniStep`, `SliceMushroomStep`, `GrateCheeseStep`, `SpreadSauce`, `SprinkleCheese`, `PlaceToppings`, `ProcessCompleted`, `ProcessInterrupted` | `KitchenProcessEffects`, `PizzaVisualComposer`, `GameFeelNetworkBridge` |
| Forno, Caixa & Mochila | `OvenInserted`, `OvenReadyDing`, `OvenBurningWarning`, `OvenBurned`, `OvenRemoved`, `BoxPackaged`, `StackPlaced`, `StackRemoved`, `BackpackEquipped`, `BackpackStored`, `BackpackRemoved`, `BackpackFullRejected` | `OvenTransferVisualizer`, `OvenStation`, `PizzeriaItemVisualizer`, `BackpackVisualizer`, `GameFeelNetworkBridge` |
| Telefone & Comandas | `PhoneRingPulse`, `OrderPosted`, `OrderInspected`, `OrderDeadlineWarning` | `PhoneRinger`, `TicketSheetView`, `GameFeelNetworkBridge` |
| Personagens & Entregas | `NpcCounterArrived`, `NpcSeatedSettle`, `NpcImpatient`, `NpcOrderGood`, `NpcLeavingAngry`, `NpcEatingLoop`, `DeliverySuccess`, `DeliveryPartial`, `DeliveryFailed` | `CustomerNpcView`, `CharacterVisualController`, `GameFeelNetworkBridge` |
| UI Toolkit | `UIHover`, `UIClick`, `UISuccess`, `UIError` | `UIFeedbackService`, `UIToolkitTweenService` |

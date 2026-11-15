# Validação do Sistema de Game Feel (`UNITY_EXECUTION=MANUAL_ONLY`)

## Testes Estáticos e EditMode Disponíveis
- `Assets/Piaaria/Scripts/Pizzeria/Editor/Tests/GameFeelSystemTests.cs`:
  - `Deduplicator_SuppressesDuplicateWithinWindow_AndAcceptsNewRevisionOrEpoch`
  - `Settings_ReduceMotionSync_ZeroesMovementAndUiIntensity_AndEnforcesClamps`
  - `MaterialClassifier_MapsCategoriesAndProcessesConsistently`
  - `VisualPivot_PreservesBasePose_RespectsCharacterLocks_AndStopsCleanly`
  - `UIToolkitTweenService_ScaleToAndPulseScale_TrackAndKillTweensSafely`
  - `Profile_RuntimeDefault_ProvidesSafeCalibratedRecipes`

Como rodar no Unity Editor aberto: `Window > General > Test Runner > EditMode > Run Selected (GameFeelSystemTests)`.

## Checklist de Validação Manual no Unity Editor (PlayMode)
1. **Estúdio & Perfil:** Abrir `Tools > Scary Party > Wizards > Visuals > Game Feel Studio`, rodar *Auditoria* e verificar que `PizzeriaModular_Manual` e `PizzeriaModular_CelShaded` são encontrados sem erro; opcionalmente criar `Assets/Piaaria/Resources/GameFeelDefaultProfile.asset` na aba *Aplicação*.
2. **Mãos, Objetos e Colisões:** Pegar e soltar tomate, queijo e massa; confirmar squash/stretch sem encolhimento permanente ao repetir 10x, mãos FPS responsivas e câmera perfeitamente estável.
3. **Processos & Forno:** Cortar tomate/pepperoni (verificar 3 fatias decorativas saltando sem alterar inventário), abrir massa com rolo (partículas leves de farinha), ralar queijo, assar e retirar do forno com `OvenTransferVisualizer` (sem travar `IsInTransit` ao interromper).
4. **Comanda, Telefone & Mochila:** Fazer telefone tocar (pulsos de vibração casados com `0.0-0.4s` / `0.6-1.0s` sem erro de material), ler nova comanda e confirmar cor estável no hover/leitura, equipar mochila e correr (balanço contínuo amortecido sem alterar colisor).
5. **Clientes NPC & Acessibilidade:** Servir pedido correto e observar reação corporal; forçar entrega ruim/paciência baixa para diferenciar resposta. Alternar `ReduceMotion` / preset `Reduzido` e confirmar silenciamento de balanços/flashes em 3D e UI Toolkit.
6. **Multiplayer (ParrelSync):** Abrir um cliente *late-joiner* durante partida com itens na bancada, pizzas assando e clientes sentados — verificar ausência de rajada inicial espúria de sons/efeitos (*baseline snapshot* silencioso) e ausência de eventos duplicados no Host.

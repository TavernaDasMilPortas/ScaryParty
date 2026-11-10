# FINDINGS — Diagnóstico e Descobertas Técnicas

Registro detalhado dos achados auditados confrontados com o código e prefab locais.

| ID | Status Local | Descrição / Detalhes Encontrados & Resolução |
|---|---|---|
| A01 | Resolvido | Colisões de stationId resolvidas: Dispensa_01 (6), Dispensa_02 (17), Geladeira_02 (5), Geladeira_04 (18), Staging (10), BoxStack (16), Receiving (14), SupplierTerminal (20), Counter (21). StationDefs criados e configurados no PizzeriaConfig com slots de armazenamento estruturados. |
| A02 | Resolvido | mesa media.002 (AssemblyStation ID 13): 5º slot nulo removido do array `slotAnchors`, agora com exatamente 4 slots válidos (Slot_0 a Slot_3). |
| A03 | Resolvido | TicketBoardStation removido de Comodo_02_Cozinha e vinculado exclusivamente ao GameObject físico `Quadro de Comandas` com ID 4. |
| A04 | Resolvido | TicketSlots reparentado para `Quadro de Comandas/TicketSlots`, com rotação Euler(0, 0, 0) voltada para o interior do cômodo e slots perfeitamente coplanares. |
| A05 | Resolvido | cardapio.001 (TicketBoardStation ID 15) corrigido: todos os 6 slots reorientados e posicionados no mesmo Z local (0.06f) na face do cardápio. |
| A06 | Resolvido | `PizzeriaSelectionConverter.ConfigureTicketBoardComponentsOnTarget` atualizado: rejeita objetos de ambiente/cômodo (`Comodo_`, `Cenario`) e calcula posições e rotações coplanares no espaço local da face do quadro. |
| A07 | Confirmado | CustomerNpcView.cs aceita remainingDistance sem validar hasPath/pathStatus. |
| A08 | Confirmado | CustomerNpcView mistura NavMeshAgent com escrita direta no Transform ao sentar. |
| A09 | Confirmado | Locomotion saltava para velocidade 2; Animator blend tree tem Idle 0, Walk 2, Run 5.335. |
| A10 | Confirmado | Falta transição e atribuição consistente de SeatedWaitingFood no CustomerService. |
| A11 | Confirmado | FBXs têm animações válidas (Walk, SitDown, StandUp); binding generic e controllers precisam de alinhamento. |
| A12 | Confirmado | PedestrianNpc.HandleNavigationFailed com risco de recursão síncrona. |
| A13 | Confirmado | NpcNavigationMotor.FollowTarget limpa target imediatamente em MoveTo. |
| A14 | Confirmado | CustomerSpawner refaz NPCs em coleções síncronas a cada 0.25s com raycasts. |
| A15 | Confirmado | DemandDirector descarta pedestre recrutado e instancia novo visual. |
| A16 | Confirmado | Pool de pedestres sem reset completo e limite no retorno. |
| A17 | Confirmado | CityGenerator instancia cidade descartável `__City__` de 64k objetos na cena. |
| A18 | Confirmado | Pipeline de geração e NavMesh síncronos sem barreira de sincronização / prontidão. |
| A19 | Confirmado | Fallback local síncrono quebra sincronia de rede em transições de cena. |
| A20 | Confirmado | UIFeedbackService cria tweens diretos fora de UIToolkitTweenService. |
| A21 | Confirmado | BindButton duplicando MouseEnter e PointerEnter sem desmontagem. |
| A22 | Confirmado | MOREMOUNTAINS_FEEL condicional ausente em defines do Standalone. |
| A23 | Confirmado | TicketBoardStation chama BindOrder a cada Update sem checagem de dirty state. |
| A24 | Confirmado | PizzeriaItemVisualizer com 4 grupos fixos por máscara de ingredientes e tint destrutivo de material. |
| A25 | Confirmado | ReplicationProjector e visualizers com varreduras lineares periódicas. |
| A26 | Confirmado | Obstáculos de NavMesh usando bounds globais em campo local e colisores duplicados. |

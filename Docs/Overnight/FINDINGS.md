# FINDINGS — Diagnóstico e Descobertas Técnicas

Registro detalhado dos achados auditados confrontados com o código e prefab locais.

| ID | Status Local | Descrição / Detalhes Encontrados |
|---|---|---|
| A01 | Confirmado | StorageStation id 6 em Geladeira_04, Dispensa_02, Dispensa_01; id 10 em Stack e Staging. Dicionário por stationId sobrescreve entradas. |
| A02 | Confirmado | AssemblyStation id 13 (mesa media.002): 5 slots, último slot é nulo. |
| A03 | Confirmado | TicketBoardStation id 4 anexado a Comodo_02_Cozinha inteiro. |
| A04 | Confirmado | Layout de tickets da cozinha fora do espaço local do Quadro de Comandas. |
| A05 | Confirmado | Quadro cardapio.001 com slots não-coplanares (z diferentes). |
| A06 | Confirmado | Builder de tickets usa bounds mundiais e não valida superfície. |
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

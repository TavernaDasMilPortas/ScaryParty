# Relatório de Alterações e Implementações do Projeto ScaryParty

Este documento detalha **todas as alterações, adições e integrações** realizadas no projeto Unity `ScaryParty` durante o plano de implementação de:
1. **Novo Personagem Unificado (FBX, Animações e Visual)**
2. **Pizzas por Composição de Ingredientes (Substituição de Receitas Fixas)**
3. **Sistema de Clientes Presenciais (NPCs, Filas, Mesas, Paciência, Traços, Pagamento e Diretor de Demanda)**

---

## 1. Contexto Git e Estrutura de Branches
- **Repositório**: `https://github.com/TavernaDasMilPortas/ScaryParty`
- **Base Commit**: `cf13a4a915f850b7868f59bb03ee08dcd9166de8` (`add animations`)
- **Branch de Trabalho Criada**: `feature/characters-pizzas-customers`
- **Preservação de Fontes**: Os arquivos `.blend` em `Assets/animations 1/` (`personagemanda.blend`, `personagemcorrendo.blend`, `personagempreparando.blend`) foram 100% mantidos como fonte primária sem alterações destrutivas.

---

## 2. Visão Geral das Alterações por Módulo

### Módulo 1: Pipeline de Personagem e Animação (FBX, Visual e Rig)
- **Exportação Blender -> FBX Reprodutível**:
  - `ScaryParty/Tools/Blender/export_character_fbx.py`: Script de exportação headless para Blender que extrai a malha limpa e o esqueleto (Generic Rig de 20 ossos) em `Character.fbx`, além de separar clipes em `Character@Walk.fbx`, `Character@Run.fbx` e `Character@Prepare.fbx`. Também gera animações procedurais compatíveis: `Idle`, `SitIdle`, `SitDown` e `StandUp`.
  - `ScaryParty/Tools/Blender/character_export_manifest.json`: Manifesto com configurações de taxa de quadros, nomes de ossos, normalização de rotações e mapeamento de animações.
  - `ScaryParty/Assets/Piaaria/Scripts/Editor/CharacterFbxExporterWindow.cs`: Janela de Editor (`Tools > Scary Party > Character > Export Character FBX from Blender`) para rodar a exportação diretamente de dentro da Unity.
  - `ScaryParty/Assets/Piaaria/Scripts/Editor/CharacterModelPostprocessor.cs`: `AssetPostprocessor` que configura automaticamente FBX importados como rig Generic e configura looping in-place para Walk/Run.

- **Componentes Visuais Compartilhados**:
  - `ScaryParty/Assets/Piaaria/Scripts/Character/CharacterAppearance.cs`: Gerenciador centralizado de cor do personagem via `MaterialPropertyBlock` (`_BaseColor` e `_Color`), prevenindo instâncias clonadas de material e vazamento de memória.
  - `ScaryParty/Assets/Piaaria/Scripts/Character/CharacterVisualController.cs`: Ponte entre o gameplay e o `Animator`. Controla os parâmetros:
    - Movimento: `Speed`, `MotionSpeed`, `Grounded`, `Jump`, `FreeFall`.
    - Trabalho: `IsPreparing` (ativado apenas em bancadas reais de trabalho, nunca ao atender clientes).
    - Posturas: `IsSitting`, `SitDown`, `StandUp`.
  - `ScaryParty/Assets/Piaaria/Scripts/Editor/CharacterSetupWizard.cs`: Utilitário para gerar e atualizar `CharacterAnimator.controller`, máscara `CharacterUpperBody.mask` e o prefab `CharacterVisual.prefab`.

- **Integração no Player e no Gameplay**:
  - `ScaryParty/Assets/ThirdParty/StarterAssets/ThirdPersonController/Scripts/ThirdPersonController.cs`: Atualizado para vincular-se ao `CharacterVisualController` em vez de buscar apenas o `Animator` diretamente na raiz.
  - `ScaryParty/Assets/Piaaria/Scripts/Player/PlayerState.cs`: Integrado para aplicar a cor sincronizada da rede ao `CharacterAppearance`.
  - `ScaryParty/Assets/Piaaria/Scripts/Player/PlayerInteraction.cs`:
    - Adicionado suporte a Hold de atendimento no balcão (0,6 s segurando `F`).
    - Trava pós-atendimento (`_workCompletedAwaitingFRelease`), evitando disparos repetidos enquanto o botão continuar pressionado.
    - Entrega presencial em mesas com pizza assada sem caixa direto das mãos via tecla `F` ou `E`.
  - `ScaryParty/Assets/Piaaria/Scripts/UI/CharacterPreviewRig.cs` e `ScaryParty/Assets/Piaaria/Scripts/Editor/PlayerPreviewPrefabGenerator.cs`: Atualizados para renderizar e gerar previews utilizando a nova malha `CharacterVisual`.

---

### Módulo 2: Pizzas por Composição de Ingredientes (Substituição de Receita Fixa)
- **Modelagem de Domínio Pura (Sem Dependências da Unity - `noEngineReferences: true`)**:
  - `ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Domain/Models/PizzaSpecification.cs`: Modelo imutável de pedido de pizza com massa base (`BaseDoughId = 2`), molho (`SauceId = 3`) e lista ordenada canonicamente de coberturas (`ToppingRequirement[]`).
  - `ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Domain/Definitions/PizzaCompositionRulesData.cs`: Regras de composição configuráveis (mínimo e máximo de coberturas, candidatos elegíveis, compatibilidade).
  - `ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Domain/Services/PizzaOrderGenerator.cs`: Gerador determinístico de pedidos baseados em especificações compostas de 1 a 2 coberturas por padrão (suporte a 1–3+ via dados), respeitando ingredientes ainda bloqueados na sessão (ex: Cogumelo).
  - `ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Domain/Services/PizzaSpecificationEvaluator.cs`: Avaliador detalhado (`PizzaMatchDetail`) que analisa massa, molho, coberturas faltantes, coberturas excedentes e ponto de cozimento, sendo 100% independente da ordem de aplicação dos ingredientes.
  - `ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Domain/Models/ItemState.cs`: Atualizado com campos explícitos de identidade da massa (`BaseDoughDefinitionId` e `BaseDoughStage`) preservados mesmo quando a categoria vira `ItemCategory.Pizza`.
  - `ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Domain/Services/ProcessingService.cs` e `OvenService.cs`: Atualizados para manter e validar o tempo de forno e preparo através da composição e não de IDs fixos de receita.

- **Authoring e ScriptableObjects**:
  - `ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Authoring/PizzaCompositionRules.cs`: ScriptableObject para configuração de regras de pizza no Inspector.
  - `ScaryParty/Assets/Piaaria/Data/PizzaCompositionRules.asset`: Asset configurado com Queijo Ralado (`ID 5`) e Calabresa Fatiada (`ID 7`) como coberturas padrão e Cogumelo (`ID 9`) bloqueado.

- **Rede e Replicação (Netcode for GameObjects)**:
  - `ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Network/NetPizzeriaDtos.cs`:
    - Adicionado struct serializável `NetPizzaSpecificationDto`.
    - Atualizados `NetOrderDto`, `NetPhoneCallDto`, `NetCustomerDto` e `NetItemDto` para trafegar especificações completas de pizza e estágios de massa.
  - `ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Network/ReplicationProjector.cs`: Atualizado para projetar os novos DTOs nos modelos de domínio de forma determinística.

- **Apresentação e UI**:
  - `ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Presentation/TicketBoardPresenter.cs` e `TicketSheetView.cs`: Atualizados para exibir detalhes dos ingredientes reais da pizza na comanda e cronômetro decrescente (`[MM:SS]`), além de identificar se o destino é `Mesa X (Salão)` ou endereço de entrega (`Delivery`).

---

### Módulo 3: Destinos e Transações (Salão vs. Delivery)
- `ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Domain/Types/DestinationRef.cs`: Tipo discriminado que suporta tanto `DeliveryAddress` (rua/número) quanto `Table` (número da mesa do restaurante).
- `ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Domain/Services/PlayerHoldService.cs`: Implementação do hold de atendimento de balcão (0,6 s de duração, lease de rede e cancelamento automático caso `F` seja liberado antes).
- Políticas de Entrega:
  - **Delivery**: Exige pizza assada e embalada em caixa (`PackagingState.Boxed`).
  - **Mesa (Salão)**: Aceita pizza assada sem caixa (`PackagingState.Unboxed`) entregue em mãos diretamente pelo jogador.

---

### Módulo 4: Clientes Presenciais (NPCs), Filas e Mesas
- **Serviços de Domínio**:
  - `ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Domain/Services/QueueService.cs`: Gerenciador de 2 filas de entrada independentes. Clientes escolhem a fila mais vazia na chegada, mantêm-se nela sem troca contínua e avançam imediatamente os slots quando o cliente da frente é atendido.
  - `ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Domain/Services/TableService.cs`: Controle de mesas exclusivas (1 mesa por NPC). A mesa é reservada atomicamente no momento da anotação do pedido no balcão; se não houver mesa livre, o pedido não é criado.
  - `ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Domain/Services/CustomerService.cs`: Ciclo de vida completo do cliente:
    `EnteringQueue -> WaitingInQueue -> BeingAttended -> WalkingToTable -> WaitingForFood -> Eating -> Leaving -> Despawned`.
  - Sistema de Paciência:
    - Paciência de Fila (60 a 100 s) e Paciência de Preparo (180 a 300 s).
    - Desgaste por estagnação quando a fila não avança.
    - Expiração limpa: quando a paciência acaba, o cliente vai embora, o pedido é cancelado e a mesa é liberada, sem destruir pizzas que já estejam sendo carregadas ou assadas.

- **Componentes de Cena e Visuais**:
  - `ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Stations/CustomerCounterStation.cs`: Componente para os 2 postos de atendimento no balcão com slots de fila mapeados.
  - `ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Stations/DiningTableStation.cs`: Componente para mesas do salão com posições de cadeira (`SeatAnchor`), aproximação e entrega de pratos.
  - `ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Presentation/CustomerNpcView.cs`: Visual do NPC usando a nova malha `CharacterVisual`, integrado ao `NavMeshAgent` com transições suaves para sentar na cadeira e levantar.
  - `ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Presentation/CustomerPatienceHudPresenter.cs`: Camada de UI Toolkit pooled no topo da tela (`customer-patience-hud-layer`) que projeta barras de paciência com interpolação de cores (verde -> amarelo -> vermelho), tags de traços e oclusão por paredes/frustum da câmera.

---

### Módulo 5: Sistema de Traços de Clientes (10 Traços)
- **Definições**:
  - `ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Domain/Definitions/CustomerTraitData.cs`: Modelo de dados de traço puro.
  - `ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Authoring/CustomerTraitDef.cs`: ScriptableObject para os traços.
- **Os 10 Traços Implementados**:
  - **5 Positivos**:
    1. *Paciente*: +30% de tolerância de espera na fila.
    2. *Generoso*: +40% no teto potencial de gorjeta.
    3. *Compreensivo*: Reduz pela metade penalidades de insatisfação por demora.
    4. *Tranquilo*: Metade da taxa de desgaste por estagnação na fila.
    5. *Apreciador do capricho*: Bônus extra de gorjeta para pizzas com composição e cocção perfeitas.
  - **5 Negativos**:
    1. *Apressado*: -25% de paciência durante o preparo do pedido.
    2. *Impaciente*: -25% de paciência na fila (incompatível com *Paciente*).
    3. *Exigente*: Penalidade ampliada para erros de ingredientes ou queima.
    4. *Pão-duro*: -50% no teto de gorjeta (incompatível com *Generoso*).
    5. *Inquieto*: Estagnação na fila acelera frustração mais rápido (incompatível com *Tranquilo*).
- **Regra de Sorteio**: Cada cliente recebe exatamente 1 traço positivo e 1 traço negativo compatíveis entre si.
- **Ferramenta de Editor**:
  - `ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Editor/CustomerTraitWizard.cs`: Menu `Tools > Scary Party > Pizzeria > Clientes e Traços (Customer Trait Wizard)` para criação, inspeção e auto-sincronização de traços.

---

### Módulo 6: Avaliação de Satisfação, Pagamento e Diretor de Demanda
- `ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Domain/Services/CustomerSatisfactionEvaluator.cs`: Avalia a experiência total do cliente levando em conta tempos de fila, tempo de preparo e precisão dos ingredientes.
- `ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Domain/Services/PayoutEvaluator.cs`: Calcula valor final pago (valor cotado da pizza + gorjeta baseada em traços e satisfação), distribuindo o valor de forma consistente entre o caixa do restaurante e as recompensas individuais.
- `ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Composition/DemandDirector.cs` e `Night1Bootstrapper.cs`: Diretor de demanda que equilibra 50% clientes presenciais e 50% delivery via telefone, com proteção contra sequências monótonas (`maxConsecutiveSameChannel = 2`) e fallback inteligente caso o salão esteja lotado.

---

### Módulo 7: Ferramentas de Automação de Cenário (Wizards Não-Destrutivos)
- `ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Editor/Authoring/DiningSetupWizard.cs`: Janela `Tools > Scary Party > Pizzeria > Salão, Filas e Mesas (Dining Setup Wizard)` que posiciona e configura os balcões de atendimento, filas, mesas e spawner no prefab da pizzaria preservando integralmente `PizzeriaAuthoringBindings`.
- `ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Editor/Authoring/PizzeriaSetupValidatorWindow.cs`: Validador de consistência para checar se todas as bancadas, regras de composição e pontos de atendimento estão válidos antes do PlayMode.

---

## 3. Testes Automatizados e Verificação (100% de Aprovação)
A suíte completa em `ScaryParty/Assets/Piaaria/Tests/Editor/PizzeriaDomainTests.cs` conta com **19 testes unitários** executados e aprovados:

| # | Teste Unitário | Status | O que Valida |
|---|---|:---:|---|
| 1 | `ToolLease_WorkerAcquiresAndReleasesTool_CannotBeStolenWhileActive` | **PASS** | Bloqueio de ferramentas em uso por outro trabalhador |
| 2 | `ProcessingService_TimedProgress_PauseAndResumeWorksAccurately` | **PASS** | Pausa e retomada de trabalho em bancada com lease |
| 3 | `PermissiveAssembly_AllowsRawIngredientsAndAnyCombinationWithoutExceptions` | **PASS** | Montagem livre de ingredientes sem travar a máquina |
| 4 | `PizzaSpecification_CanonicalSortingAndEquality_OrderIndependent` | **PASS** | Igualdade canônica de pizzas independente da ordem dos ingredientes |
| 5 | `PizzaSpecificationEvaluator_MatchesDoughIdentityEvenWhenCategoryIsPizzaOrBoxed` | **PASS** | Preservação da massa base e avaliação detalhada da cobertura |
| 6 | `PizzaOrderGenerator_GeneratesValid1To2Toppings_RespectsLockedMushroom` | **PASS** | Geração de pedidos com 1 a 2 coberturas e trava de ingredientes bloqueados |
| 7 | `DeliveryEvaluator_MultiPizzaOrder_EvaluatesBatchWithProportionalPayout_FixedA26` | **PASS** | Avaliação proporcional de lotes de entrega delivery |
| 8 | `OvenService_Formulas_CookAndBurnProgressAndReinsertPreservesState` | **PASS** | Curva de cozimento, queima e retirada/reinserção no forno |
| 9 | `StackService_DepositAndTakeTop_CapacityAndOrdering` | **PASS** | Empilhamento e retirada LIFO de caixas de pizza |
| 10 | `BackpackService_EquipStoreRetrieveDrop_Workflow` | **PASS** | Armazenamento e retirada de pedidos na mochila |
| 11 | `CallService_EnqueueTickAnswer_GeneratesOrderAtomically` | **PASS** | Chamadas telefônicas de delivery e geração atômica de comanda |
| 12 | `PlayerHoldService_DiscardAndArchiveTicket_ExecutesWithServerLease` | **PASS** | Descarte e arquivamento de comandas via hold |
| 13 | `StationOverrides_DefinitionAndInstanceOverrides_CalculatesCorrectDuration` | **PASS** | Duração customizada de processos por bancada |
| 14 | `TransferService_SingleLocationInvariant_BlocksDoubleOccupancy` | **PASS** | Invariante de exclusão mútua de slots de bancada |
| 15 | `QueueService_TwoQueues_ChoosesShorterAndCompactsOnFrontLeave` | **PASS** | Balanceamento das 2 filas de clientes e avanço ordenado |
| 16 | `CustomerTraits_DefaultTenTraits_BlocksIncompatiblePairs` | **PASS** | Regra de incompatibilidade de traços positivos e negativos |
| 17 | `CustomerFlow_HoldAttendanceReservesTable_ServesUnboxedPizzaAndPaysOnce` | **PASS** | Fluxo de atendimento no balcão, reserva de mesa e entrega sem caixa |
| 18 | `CustomerPatience_ExpiryReleasesTableAndOrder_WithoutDeletingHeldPizzas` | **PASS** | Expiração de paciência, liberação de mesa e preservação de pizzas |
| 19 | `DemandDirector_WeightedChannelSelection_EnforcesStreakProtection` | **PASS** | Diretor de demanda 50/50 e proteção contra repetição excessiva |

### Compilação dos Assemblies C#:
- `ScaryParty.Pizzeria.Domain.dll`: **0 erros** (`noEngineReferences: true` intacto).
- `ScaryParty.Pizzeria.Tests.dll`: **0 erros**.
- `Assembly-CSharp.dll`: **0 erros**.
- `Assembly-CSharp-Editor.dll`: **0 erros**.

---

## 5. Resolução de Problemas Críticos (Orientação, Curvas de Animação e Altura das Mesas)

### 5.1. Orientação do Modelo e Posição Ereta (Upside Down Fix)
- **Causa Raiz**: O script de exportação do Blender já exportava com convenção de eixos `axis_up='Y'`, `axis_forward='-Z'`. No entanto, uma rotação residual de `-90°` no eixo X havia sido aplicada em `Armature` e `Cylinder.002` em `CharacterSetupWizard.cs` e nos prefabs, invertendo o personagem de cabeça para baixo (pernas para cima).
- **Correção**:
  - `CharacterSetupWizard.cs`, `CharacterModelPostprocessor.cs` e `CharacterVisual.prefab` reconfigurados com `localRotation = Quaternion.identity` (`(0, 0, 0)`) e `bakeAxisConversion = true`.
  - Posição do osso `central` (pélvis) restaurada para $Y = 0.47\text{ m}$ (altura anatômica correta acima do piso $Y = 0\text{ m}$).
  - Câmera de preview do rig em `CharacterPreviewRig.cs` mantida perfeitamente alinhada para enquadrar o personagem ereto de 1,60 m.

### 5.2. Vinculação e Deformação das Curvas de Animação (0.00° -> 32.54°)
- **Causa Raiz**: Os 7 FBXs de animação (`Character_*.fbx`) eram exportados sem a malha (`include_mesh=False`), fazendo com que o FBX contivesse apenas a `Armature` como raiz. Isso gerava curvas no Unity com caminhos `path='central/...'`, enquanto o `CharacterVisual.prefab` possui a hierarquia `CharacterVisual/Armature/central/...`. Como `central` não é filho direto de `CharacterVisual`, o Animator falhava silenciosamente e mantinha os ossos 100% estáticos (diferença angular de $0.00^\circ$).
- **Correção**:
  - `export_character_fbx.py` atualizado para exportar todos os clipes de animação com `include_mesh=True`, preservando a hierarquia canônica idêntica de `Armature` e `Cylinder.002`.
  - Curvas de animação agora possuem caminhos correspondentes `path='Armature/central/...'`.
  - Teste de amostragem no osso `leg1.l` comprovou deformação com amplitude de **$32.54^\circ$** (contra $0.00^\circ$ anteriormente), restaurando movimentação fluida de caminhada, corrida, preparo e sentar.

### 5.3. Altura das Mesas e Cadeiras sem Atravessar o Piso
- **Causa Raiz**: No FBX original da pizzaria, as pernas das mesas (`mesa comum.*`) terminavam em local $Y = -1.777\text{ m}$, enquanto a superfície superior do colisor do piso (`Floor_Salao`) fica em local $Y = -1.60\text{ m}$, fazendo com que as mesas afundassem quase $18\text{ cm}$ no chão.
- **Correção**:
  - Posição local Y de todas as mesas `mesa comum.*` em `PizzeriaModular_Manual.prefab` e `GameScene.unity` ajustada de `-1.1991158f` para `-1.0221158f`. Com isso, as pernas das mesas tocam exatamente a superfície superior do piso em local $Y = -1.60\text{ m}$.
  - Posição das cadeiras filhas ajustada para local $Y = -0.168\text{ m}$ em relação à mesa, alinhando as 4 pernas das cadeiras com o piso.
  - Colisor de caixa da mesa (`BoxCollider`) reconfigurado para `center: (0, 0, 0)` e `size: (1.1, 1.15, 1.1)`.

### 5.4. Fixação ao Piso e Posição de Sentar (NPCs Flutuando / Afundando)
- **Causa Raiz**: `CustomerSpawner.SnapToFloor` realizava raycast genérico (`LayerMask = ~0`) que colidia com cadeiras, tampos de mesa e outros personagens, jogando a cota dos NPCs para o alto. Adicionalmente, `DiningTableStation.GetSeatPosition` chamava `SnapToFloor` sobre o assento da cadeira, forçando o NPC sentado para o chão.
- **Correção**:
  - `CustomerSpawner.SnapToFloor` aprimorado com raycast seletivo que filtra especificamente colisores de piso (`Floor_Salao`, `Floor_Cozinha`, etc.) e recorre à cota plana matemática do salão caso não atinja piso direto.
  - `DiningTableStation.GetSeatPosition` agora preserva diretamente a posição do assento (`seatAnchor.position`), posicionando os NPCs sentados sobre a almofada da cadeira sem afundá-los no piso.

1. Na Unity, abra o menu `Tools > Scary Party > Pizzeria > Clientes e Traços` e clique em **Sincronizar Traços Padrão**.
2. Abra `Window > General > Test Runner` e clique em **Run All** na aba *EditMode* (confirma os 19 testes verdes).
3. Abra a cena `Assets/Piaaria/Scenes/PizzeriaPlaytest.unity` e dê Play:
   - Ande, corra e pule com o novo personagem.
   - Segure `F` por 0,6 s no primeiro cliente da fila do balcão para aceitar o pedido e reservar a mesa.
   - Monte a pizza pelos ingredientes descritos na comanda e asse no forno.
   - Sirva a pizza diretamente na mesa do cliente (**sem caixa**); o cliente pagará com gorjeta proporcional e sairá do restaurante.
   - Pedidos pelo telefone continuam funcionando no modo delivery (**com caixa**).

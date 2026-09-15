# ScaryParty — Plano de implementação da pizzaria jogável
**Versão atualizada — delivery, utensílios e erros permitidos · Documento de planejamento, sem implementação executada**

---

## 1. Atualização de direção e uso

Revisão de mecânicas após validação com o autor. Este documento substitui a versão anterior. As mudanças estão incorporadas nos modelos, comandos, etapas e testes; não são um adendo opcional.

### Decisões confirmadas
- **Wizard de autoria no Editor**: Ferramenta no Editor para configurar ingredientes, receitas, utensílios, processos, tempos e bancadas sem C# para comportamentos existentes.
- **Primeira implementação focada em delivery**: Atendimento presencial fica como extensão preparada; futuramente o cliente pode esperar no balcão/fila ou sentado e receber pizza sem embalagem.
- **Duas mãos + Utensílios físicos compartilhados**: Duas mãos para carregar dois objetos. Utensílios físicos compartilhados acrescentam uma camada ao preparo.
- **Preparo com ferramentas**: Queijo precisa ser ralado com ralador; queijo ralado pode ficar numa bancada e ser levado à pizza por outra pessoa. Nem todo processo exige utensílio.
- **Telefone direto**: Atender o telefone já registra/publica o pedido imediatamente. Não há uma segunda confirmação de anotação. A futura conversa com cliente presencial terá o mesmo comportamento.
- **Comandas multilinha**: Pedidos com várias pizzas (1 a 3 pizzas) fazem parte da primeira versão.
- **Erros e queimaduras permitidos**: Combinações erradas e pizzas queimadas podem ser assadas, embaladas e entregues. O jogo não corrige a equipe automaticamente.
- **Endereçamento manual na retirada**: Ao colocar cada caixa na bancada de retirada, escolher manualmente seu endereço. O seletor não mostra pedido, receita ou correspondência correta; essa informação está no painel de pedidos.
- **Mapa pessoal por carga**: O mapa de cada entregador mostra os endereços escritos nas caixas que ele carrega em mãos/mochila.
- **Entrega e avaliação de conjunto**: Entrega confirmada consome todas as pizzas selecionadas e encerra o pedido atendido. Erros, queimaduras e faltas reduzem parte ou toda a recompensa. Não exigir reposição.

### O que permanece como proposta configurável
Valores de tempo, capacidade, preço, percentuais de penalidade, quantidade de utensílios, tecla de trabalho, qualidade por resfriamento e divisão de dinheiro pessoal/restaurante são defaults técnicos/de balanceamento, não decisões adicionais aprovadas pelo autor. Qualidade por tempo permanece desativada no cenário inicial; queimadura é avaliada independentemente disso.

### Evidência do projeto
A auditoria anterior dos scripts usou o commit `87d1018b3b979bf042d6c5386bdc1fb53b73beb0`. `CityGenerator.cs` em `main` já contém `_networkSeed` e altura explícita da bancada (`config.sidewalkHeight + 0.5f`). Não duplicar essas correções.

---

## 2. Objetivo e escopo

### Loop inicial
Comprar ingredientes → receber/reabastecer → atender chamada e ler comanda → buscar ingredientes/utensílios → preparar porções → montar/assar → embalar → escolher endereço individualmente na retirada → carregar caixas → seguir os marcadores da própria carga → entregar o conjunto escolhido → receber resultado e recompensa.

Qualquer jogador pode assumir qualquer tarefa. A cooperação depende de comunicação e organização reais. Composição da pizza, endereço da etiqueta e solicitação do cliente são informações independentes. Somente a avaliação final compara comida entregue e pedido.

### Incluído nesta implementação
Telefone de pedidos; comandas com múltiplas pizzas; estoque finito e suprimentos com ETA; duas mãos; utensílios compartilhados; porções preparadas transportáveis; bancadas/forno; embalagem de pizza errada/queimada; endereçamento manual na retirada; mochila; mapa por carga; avaliação do conjunto entregue; aprimoramentos; prefab/builders/UI; wizard de autoria; multiplayer desde o início.

### Preparado para depois
Modelo de fonte presencial, pedido com `CustomerId`, ponto de atendimento e política de servir sem caixa. Não exigir NPCs, fila animada, assentos, mesas ou navegação para concluir o delivery. Essas extensões usarão os mesmos serviços de pedido e avaliação. Ciclo de noites, loja, save, motos, monstros completos e host migration continuam fora da implementação atual.

### Defaults iniciais
- Duas mãos, mochila com duas caixas, uma pizza por caixa.
- Pedidos de 1 a 3 pizzas inicialmente, incluindo receitas repetidas.
- Receitas de demonstração: mussarela e calabresa; cogumelo para extensão/desbloqueio. Mussarela usa massa aberta, molho e queijo ralado; calabresa acrescenta calabresa preparada.
- Embalagens ilimitadas; ingredientes finitos.
- Bancada de utensílios começa com um ralador e uma faca (configurável). Utensílios não são consumidos, não desgastam e não exigem lavagem nesta versão.
- Abrir massa é sem utensílio no default; ralar queijo exige ralador e fatiar calabresa exige faca.
- Dinheiro pessoal (`PlayerState.Money`) e caixa do restaurante continuam separados como proposta ajustável. A avaliação ruim reduz os dois créditos.

---

## 3. Auditoria local e migração necessária

Todos os caminhos abaixo são relativos à raiz Unity (`ScaryParty/` no repositório).

| Área e arquivos existentes | Mudança necessária | Cuidado de integração |
|---|---|---|
| `ProjectSettings/ProjectVersion.txt`, `Packages/manifest.json` | Confirmar Unity 6000.3.6f1, NGO 2.13.1, Input System 1.20.0 e URP 17.5.0 | Não atualizar dependências para executar este plano. |
| `Assets/Scripts/City/CityGenerator.cs` | Trocar criação de cubos por reserva de lote e spawn único do prefab | Preservar seed existente (`_networkSeed`), callbacks e geração local da geometria. |
| `CityData.cs`, `CityConfig.cs`, `BlockFiller.cs`, `DeliveryPointPlacer.cs` | Registrar colocação, footprint e destinos válidos | Não deixar destino no prédio removido; ordenar candidatos explicitamente. |
| `Assets/Scripts/Gameplay/PizzariaManager.cs` | Migrar demanda, pedidos, recompensas e liberação do jogo | Não continuar gerando caixas prontas; fachada temporária sem estado paralelo. |
| `PizzaItem.cs` | Substituir pickup local + despawn independente | Não deixar item antigo acessível ao fluxo novo. |
| `Assets/Scripts/Player/PlayerInteraction.cs` | Mãos autoritativas, Input Actions e resolução contextual | Preservar foco; adicionar oclusão e integrar teleporte por anchors. |
| `Assets/Scripts/City/PlayerState.cs` | Contagens derivadas e readiness de novos jogadores | Não colocar orçamento compartilhado em cada PlayerState. |
| `Assets/Scripts/City/DeliveryPoint.cs` | Solicitar entrega ao servidor via adapter | Retirar item e premiar somente após transação válida. |
| `Assets/Scripts/UI/UIManager.cs`, `MinimapRouteManager.cs` | Projetar comandas por ID; mapa pessoal por endereços da carga | Preservar minimapa/scoreboard e limpar subscriptions. |
| `Assets/Scripts/Weapons/WeaponInputHandler.cs`, `WeaponController.cs`, `StarterAssets inputs` | Contextos de input e exclusão com cozinha | Validar ownership no local apropriado; não refazer o sistema de armas. |
| `Assets/Scripts/City/Editor/CitySceneBuilder.cs` | Usar novo builder e parar de recriar fluxo antigo | Preservar configuração autoral e referências. |
| `Assets/DefaultNetworkPrefabs.asset`, prefabs de player e pizza | Registro e migração dos componentes | Preservar GUIDs; não recriar o player inteiro. |
| `ProjectSettings/EditorBuildSettings.asset`, fluxo lobby/ready | Identificar cena real de gameplay e configurar integração | Confirmada referência a `Assets/StarterAssets/ThirdPersonController/Scenes/GameScene.unity`. |

---

## 4. Arquitetura, arquivos e autoridade

A arquitetura usa serviços pequenos e transações explícitas. Não exigir uma classe por ação trivial nem uma interface para cada tipo.

### 4.1 Estrutura proposta

Base: `Assets/Scripts/Pizzeria/`. Caminhos nesta tabela são relativos a essa base.

| Grupo e arquivos | Responsabilidade | Dependências / dono do estado |
|---|---|---|
| `Domain/Models/PizzeriaState.cs`, `ItemState.cs`, `OrderState.cs`, `OperationState.cs`, `SupplyState.cs`, `CustomerState.cs` | Estado mutável único do restaurante no servidor | C# puro; sem MonoBehaviour, ScriptableObject, NGO ou PlayerState. |
| `Domain/Definitions/DefinitionCatalog.cs` | Dados imutáveis de receitas, ingredientes, processos e stats | DTOs C# construídos a partir dos assets. |
| `Domain/Types/Ids.cs`, `CommandResult.cs`, `LocationRef.cs` | IDs, erros, localização e invariantes | Sem dependências de cena. |
| `Domain/Services/TransferService.cs`, `RecipeEvaluator.cs`, `ProcessingService.cs` | Transferência, composição e trabalho | Estado compartilhado + catálogo + relógio. |
| `Domain/Services/OrderService.cs`, `SupplyService.cs`, `UpgradeService.cs`, `FulfillmentService.cs`, `DeliveryEvaluator.cs` | Pedidos, compras, melhorias e avaliação do conjunto entregue | Estado compartilhado; serviços anteriores quando necessário. |
| `Domain/Ports/IClock.cs`, `IEconomyPort.cs`, `IDestinationCatalog.cs`, `IOrderSource.cs` | Substituições reais para relógio, economia, destinos e demanda | Contratos pequenos; domínio não depende de adapters. |
| `Authoring/IngredientDef.cs`, `RecipeDef.cs`, `ProcessDef.cs`, `StationDef.cs`, `SupplyItemDef.cs`, `ToolDef.cs`, `UpgradeDef.cs`, `PizzeriaConfig.cs` | ScriptableObjects e conversão para catálogo de domínio | Unity; nenhum estado de sessão gravado nos assets. |
| `Network/PizzeriaCommandHandler.cs` | Receber intenção, extrair remetente, validar acesso e chamar serviços | NGO; não implementa receitas/estoque dentro dos RPCs. |
| `Network/PizzeriaNetworkState.cs`, `NetDtos.cs`, `ReplicationProjector.cs` | Projeção do estado confirmado e snapshots | Escrita somente do servidor; clientes não alteram regras. |
| `Network/PhysicalItem.cs`, `NetworkItemRegistry.cs` | Relação entre identidade lógica e representação física | Visual/âncoras; sem inventário paralelo. |
| `Stations/StationView.cs`, `StorageStation.cs`, `PrepStation.cs`, `AssemblyStation.cs`, `OvenStation.cs`, `PackagingStation.cs`, `ToolRackStation.cs`, `StagingStation.cs`, `PhoneStation.cs`, `SupplierTerminal.cs` | Adaptadores de interação e apresentação | Leem estado e enviam comandos. Locks nunca locais. |
| `Stations/CounterStation.cs`, `ReceivingStation.cs`, `TrashStation.cs` | Bancadas neutras/retirada, recebimento, descarte | Composição de slots e filtros; não criar classe duplicada só por visual. |
| `Customers/DemandGenerator.cs`, `PhoneOrderSource.cs` | Geração server-only; chamadas com múltiplas pizzas | Pedidos vivem no domínio; RNG separado da cidade. Contrato IOrderSource suporta futura fonte presencial. |
| `Player/PlayerInventoryAdapter.cs`, `BackpackController.cs`, `InteractionContextResolver.cs`, `InputContextController.cs` | Input e integração das mãos/mochila | Inventário vem do estado; UI pertence ao jogador local. |
| `Integration/CityPizzeriaPlacementAdapter.cs`, `DeliveryPointAdapter.cs`, `CargoMapAdapter.cs`, `PlayerEconomyAdapter.cs` | Ponte para scripts existentes | Adapters podem depender de Assembly-CSharp. |
| `Composition/PizzeriaRoot.cs`, `PizzeriaSpawnCoordinator.cs` | Construção de serviços, readiness, spawn/cleanup | Root do restaurante e adapter da sessão existente. |
| `Presentation/PizzeriaHudBuilder.cs`, presenters de telefone, pedidos, fornecedor, seleção, mochila, progresso e recompensa | UI Toolkit e visuais locais | Sem estado financeiro/de receita próprio. |
| `Editor/PizzeriaBuilder.cs`, `PizzeriaValidator.cs`, `PizzeriaTestSceneBuilder.cs`, `PizzeriaContentWizard.cs`, `ContentDraft.cs`, `PizzeriaContentWriter.cs` | Assets, prefab, wizard de autoria, setup e validação automáticos | APIs de Editor isoladas. |

**Assemblies**: Criar apenas `Domain/ScaryParty.Pizzeria.Domain.asmdef` para o domínio independente, com `noEngineReferences: true` quando compatível. Os adapters Unity permanecem em `Assembly-CSharp` e referenciam o domínio.

**Fonte da verdade**: `PizzeriaState` pertence ao servidor. Services não mantêm dicionários concorrentes dos mesmos dados. `ReplicationProjector` é o único caminho de escrita nas `NetworkList`s. `PlayerState.Money` continua sendo o saldo pessoal existente.

---

## 5. Modelo de dados e invariantes

### 5.1 Dados independentes

IDs de sessão usam wrappers `ulong`, com zero ausente; IDs de definição são estáveis e não dependem de nomes/índices. `NetworkObjectId` não substitui `ItemId`.

- **Item**: `ItemId`, tipo (`ingrediente`/`utensilio`/`pizza`), `DefinitionId`, preparação, composição real, cozimento, embalagem, localização, qualidade opcional e revisão.
- **Caixa**: É a pizza embalada ou um wrapper um-para-um; preserva identidade lógica/conteúdo. Guarda `LabelDestinationId` opcional. Não guarda allocation, `OrderLineId` ou `OrderId` correto.
- **Utensílio**: `ToolItemId`, `ToolDefinitionId`, capacidades (`Grate`/`Slice`), localização, reserva operacional; não é consumível.
- **Processo**: Entrada/saída, duração, preparação exigida e `ToolCapability` opcional.
- **Operação**: `OperationId`, `StationSlotId`, `InputItemId`, `ToolItemId` opcional, `WorkerId`, progresso, duração capturada, lease e revisão.
- **Localização**: Tipo + `HolderId` + `SlotId`; mundo inclui pose server-side.
- **Composição**: Ingrediente/preparação e quantidade, entradas canônicas limitadas; `RecipeId` pode estar ausente para mistura sem receita.
- **Pedido**: `OrderId`, source, destino/cliente, linhas com `RecipeId` e `Quantity`, datas, lifecycle e recompensa potencial. Nenhum vínculo obrigatório com caixas durante produção.
- **Tentativa de entrega**: `AttemptId`, `OrderId` resolvido no servidor pelo destino, `DestinationId` real, lista de `ItemIds`, avaliação e créditos; resultado idempotente.
- **Estoque/crate**: Ingrediente, estado e quantidade agregada; capacidades; suprimento mantém preço/ETA e remanescentes.
- **Restaurante**: `PizzeriaId`, `GenerationId`, lifecycle, saldo, revisão global.

O pedido pode conter duas linhas diferentes ou quantidade maior que um da mesma receita. Exemplo obrigatório: Pedido 1, endereço 4, uma calabresa e uma mussarela. Cada caixa é marcada separadamente como endereço 4, por decisão humana.

### 5.2 Invariantes

1. Cada item individualizado, inclusive utensílio, tem uma localização única. Retirada de estoque reduz quantidade e cria unidade na mesma transação.
2. Ingrediente adicionado é consumido e passa a compor a pizza. Utensílio permanece existente e reutilizável.
3. Slots/mãos não duplicam ocupação. Dois jogadores não podem usar o mesmo ralador simultaneamente.
4. Só restrições físicas/operacionais impedem ações: capacidade, posse, distância, ferramenta correta, estado estrutural e limite de dados. Receita ou endereço errado não são rejeição de gameplay.
5. Não existe reserva de unidade do pedido ao embalar/endereço. Várias caixas podem ter o mesmo endereço, mesmo com conteúdo errado ou excesso de quantidade.
6. Entrega válida como ação consome todas as caixas selecionadas, inclusive inadequadas, e aplica resultado uma vez. Pedido encerrado não pode pagar outra vez.
7. Falhas de protocolo (stale, duplicata, falta de posse, fora de alcance) não consomem itens. Erros de cozinha/logística confirmados são avaliados e podem consumir sem pagar.
8. Estado server-side e assets imutáveis; qualidade, tool locks e créditos não vêm do cliente.

---

## 6. Pedidos, suprimentos e estados

### 6.1 Telefone e comanda

- **Chamada**: `Queued` → `Ringing` → `Registered`, ou `Missed` se não atendida no prazo.
- Interagir com telefone tocando publica o pedido `Accepted` imediatamente e mostra a solicitação. Sem botão de confirmação adicional.
- Duas respostas simultâneas publicam uma comanda; pedido não some ao fechar a UI.
- Antes de tocar, reservar capacidade de demanda para o aceite. Se a lista de pedidos está cheia, chamadas aguardam.
- O painel mostra número do pedido, endereço e todas as pizzas/quantidades. Jogadores podem consultar a comanda.
- No máximo um pedido aberto por endereço. O mesmo pedido pode ter várias pizzas. Depois de encerrado, destino pode receber nova demanda; uma caixa antiga ali entregue é julgada pelo pedido atual.

### 6.2 Lifecycle

- **Pedido**: `Accepted` → `Completed`, `Cancelled` ou `Expired`. Resultado de `Completed` pode ser integral, parcial ou zero; não chamar pagamento zero de rejeição de entrega.
- Status de produção no quadro é anotação manual opcional da equipe, sem validação automática por receita. Embalagem/posição das caixas não tornam pedido concluído ou automaticamente correto.
- Prazo de entrega é configurável no servidor (`now >= deadline`). Caixa etiquetada para destino sem pedido ativo ainda pode ser entregue: mostrar confirmação genérica de transferência irreversível, consumir o conjunto e pagar zero (`NoActiveOrder`). Repetição da mesma tentativa não consome novamente.
- Cancelamento/expiração não apagam pizzas nem alteram etiquetas. Caixa pode voltar à bancada para endereço novo.

### 6.3 Atendimento presencial futuro

Atender cliente publicará comanda automaticamente. A política de espera poderá usar fila no balcão ou assento. Servir pizza sem embalagem será permitido, inclusive errada/queimada, usando o mesmo avaliador. Nesta entrega criar contratos/`SourceType`, sem simulação presencial completa nem torná-la gate do delivery.

### 6.4 Suprimentos e storage

- Terminal separado permite comprar pacotes, ver saldo/custo/ETA e pendências. Servidor debita e cria `SupplyOrder` uma vez.
- Estados: `InTransit` → `WaitingForReceivingSlot` → `Arrived` → `Unloaded`.
- Crate mantém quantidades agregadas. Reabastecer porções exige jogador ao alcance da crate e storage; mão pode transportar unidade. Capacidade insuficiente mantém resto na crate. Crate vazia libera recebimento. Compras pendentes limite inicial 3.
- Geladeira/armário têm vários ingredientes e seletor individual por jogador. Queijo recebido é peça inteira (não ralada). Queijo ralado é item preparado transportável e pode esperar numa bancada.

---

## 7. Duas mãos, utensílios e cozinha

### 7.1 Inputs e interação

- Mãos: duas mãos com seleção explícita.
- Ações: `E` interagir/transferir, `F` segurar para trabalhar, `Q` trocar mão, `B` mochila, `G` soltar, `Escape` voltar.
- Menus bloqueiam disparos/ações por trás e restauram o contexto anterior ao fechar.
- Bancadas recebem itens em slots, inclusive porções preparadas e utensílios. A bancada de utensílios guarda os exemplares reais, não gera um novo a cada interação.

### 7.2 Exemplo obrigatório: queijo e ralador

1. Jogador pega queijo numa mão e ralador na outra.
2. Coloca queijo no slot de preparo, ficando com uma mão livre e ralador na outra.
3. Segura trabalhar (`F`): processo verifica o ralador equipado em qualquer mão e reserva esse `ToolItemId`.
4. Queijo vira porção ralada no slot. Ralador continua na mão e não é consumido.
5. Jogador pega queijo ralado com mão livre e pode colocá-lo na pizza ou numa bancada de espera.
6. Pode deixar o ralador em outra bancada para outro jogador usar.

O comando `StartWork` procura ferramenta nas mãos do remetente (não precisa ser a mão selecionada). Todo processo informa ferramenta necessária ou nenhuma. Faca para fatiar calabresa e cogumelo é default; abrir massa sem ferramenta é default.

### 7.3 Trabalho, pausa e disputa

- Lease por operação/worker/ferramenta. Um worker e um utensílio por operação. Heartbeat 0,2 s, timeout 0,75 s.
- Soltar ação, sair de alcance, desconectar, deixar a ferramenta ou transferir o ingrediente interrompe trabalho: materializar progresso, liberar reservas e manter porção/WIP.
- Outro jogador não rouba ferramenta da mão; pode pegar quando deixada. Retomada exige ferramenta compatível disponível. Progresso pertence à porção/operação, não ao ralador.
- Utensílios não entram na composição e não cabem na mochila de pizzas.

### 7.4 Montagem permissiva

- Massa aberta forma base. Adicionar ingredientes consome porções e registra composição real.
- Queijo ralado satisfaz receita de mussarela; queijo inteiro não satisfaz.
- Colocar ingrediente cru/não processado na base é aceito quando estruturalmente possível, resultando em composição inadequada na avaliação. Ferramentas e objetos não alimentares são rejeitados como ingredientes.
- Adicionar duplicados, extras, faltar queijo ou usar cobertura errada não trava montagem nem forno. UI mostra conteúdo visual da pizza, sem filtro de compatibilidade ou correção automática. Limite inicial de 8 entradas distintas por pizza.
- Ingredientes congelados após início do cozimento.

### 7.5 Forno

- Forno aceita base de pizza estruturalmente válida, mesmo composição sem receita.
- Cozimento autônomo: cru/parcial → pronto → queimado.
  $$p_{\text{agora}} = \min(1, p + (t - t_0) / D)$$
- Retirar antes de pronto pausa progresso; reinserir retoma. Capturar nova duração do forno de destino a cada inserção.
- Quando pronto, acumular exposição ao calor até queimar; retirar/reinserir não reinicia graça.
- Queimado é terminal de cozimento, mas continua transportável, embalável e entregável.
- Embalar pizza retirada crua/parcial é permitido (avaliação aplica penalidade).

### 7.6 Embalagem e endereço na retirada

- Embalagem exige objeto pizza não embalado e espaço, não receita/cozimento corretos. Transforma pizza em caixa preservando conteúdo e identidade. Não abre seletor de pedido nesta estação. Caixas ilimitadas inicialmente.
- Ao tentar colocar caixa no balcão de retirada, reservar um slot por lease e abrir seletor de endereços. A caixa fica na mão durante a escolha; confirmar grava `LabelDestinationId` e transfere para o slot atomicamente. Cancelar libera slot e mantém caixa na mão.
- Seletor lista endereços válidos da cidade em ordem estável, inclusive sem pedido ativo. Não mostra número de pedido, receita, quantidade esperada ou cor de compatibilidade. Cada caixa exige escolha individual.
- Etiqueta contém somente endereço escolhido. Mochila/caixa não revelam `OrderId` escondido. Múltiplas caixas com mesmo endereço são permitidas. Reendereçar é ação deliberada no balcão de retirada.

---

## 8. Mapa, entrega e avaliação

### 8.1 Mapa pessoal por carga

- Marcadores derivam exclusivamente de `LabelDestinationId` das caixas que o jogador carrega em mãos/mochila.
- Caixa no balcão não cria rota no mapa de outro jogador.
- Duas caixas endereço 4 e uma endereço 25 geram dois pontos. Ao remover a última caixa de um endereço, remover marcador.
- Etiqueta errada gera marcador do endereço errado. O mapa não verifica receita ou pedido ativo.

### 8.2 Entrega como conjunto

- No destino, jogador seleciona caixas da própria mão/mochila e confirma entregar.
- Não há seleção automática das corretas nem bloqueio de caixas com etiqueta diferente do local real. Confirmação genérica sem preview de desconto.
- Servidor resolve pedido aberto daquele destino, valida posse/alcance e consome todo o conjunto selecionado.
- Avalia conteúdo real contra a comanda, encerra pedido mesmo incompleto e paga resultado. Caixas não selecionadas ficam com entregador.
- Entregar em endereço sem pedido: consome e paga zero (`NoActiveOrder`).

### 8.3 Avaliador configurável (`DeliveryEvaluator`)

1. Expandir linhas em unidades solicitadas, incluindo receitas repetidas.
2. Comparar receita por composição exata com preparação/multiplicidade.
3. Parear cada pizza entregue a no máximo uma unidade solicitada (maior valor primeiro, desempate por ID).
4. Pizza correta e bem assada: 100% do valor da unidade.
5. Pizza queimada / crua / errada: 0% (configurável).
6. Unidade faltante ou extra: zero contribuição. Extras consumidos sem acréscimo.
7. Pagar soma das contribuições, limitada ao valor integral do pedido. Não gerar dívida.
8. Exemplo: Pedido com calabresa (20) e mussarela (15). Entregar calabresa correta + mussarela queimada consome ambas e paga 20 de 35. Entregar duas erradas consome ambas e paga zero. Entregar só mussarela correta paga 15 e encerra todo o pedido.

### 8.4 Créditos e qualidade opcional

- Pessoal recebe soma calculada via `PlayerState.Money`.
- Restaurante recebe base por pedido multiplicada pela fração atendida em valor.
- Qualidade por resfriamento desativada inicialmente; se ativada, reduz contribuição sem ultrapassar 100%.

### 8.5 Recuperação

Desconexão pausa operações e libera ferramenta/slot. Itens em mãos/mochila vão para caixa de recuperação no restaurante. Preservar conteúdo e etiqueta sem vincular a pedido.

---

## 9. Transações e protocolo multiplayer

### 9.1 Envelope comum

Toda mutação remota inclui `GenerationId`, `RequestId`, alvo e revisão esperada. Remetente obtido do RPC. Validação server-side estrita. Cache de resultados por `(conexão, geração, RequestId)` com limite de 256 entradas.

### 9.2 Comandos mínimos

| Comando | Validação e commit |
|---|---|
| `Dispense(storage, ingredient, hand)` | Quantidade/capacidade; retirar uma unidade e criar item na mão. |
| `Transfer/Pickup/Drop(item, source, target)` | Localização e posse reais; item/tool move uma vez. Ferramenta ativa pausa operação antes de transferência. |
| `StartWork(slot, process, toolId opcional)` | Ingrediente e capacidade de ferramenta; ferramenta está na mão do worker e não reservada; criar lease. |
| `HeartbeatWork/CancelWork(operation)` | Só worker; acumular progresso e renovar/liberar tool lock. |
| `AddIngredient(hand, assemblySlot)` | Alimento e limites estruturais; não exigir compatibilidade com receita/pedido. |
| `InsertOven/RemoveOven` | Base, slot e mão; conservar cozimento, aceitar composição desconhecida. |
| `StartPackaging(slot)` | Pizza não embalada; aceitar errada/queimada; concluir caixa sem pedido. |
| `BeginStage(box, slot)` | Reservar slot e caixa enquanto UI escolhe endereço. |
| `ConfirmStage(reservation, destination)` | Endereço existente, lease/posse/capacidade; gravar etiqueta e colocar caixa, sem lookup de pedido. |
| `CancelStage(reservation)` / `RelabelStagedBox(slot, destination)` | Liberar reserva ou mudar endereço deliberadamente; nunca alterar comida. |
| `LoadBackpack/UnloadBackpack` | Caixa e espaço; mapa deriva da carga após commit. |
| `AnswerPhone(call)` | Chamada disponível e não expirada; criar pedido e publicar comanda imediatamente, uma vez. |
| `PurchaseSupply(lines)` / `Restock(crate, storage, ingredient, qty)` | Preço/saldo/limites server-side; compra ou transferência conservativa. |
| `SubmitDelivery(destination, selectedItemIds)` | Itens distintos na posse, perto do destino; consumir conjunto, avaliar e concluir/pagar uma vez. Receita ruim não é erro de RPC. |
| `BeginDiscard/ConfirmDiscard` | Hold e posse; consumir alimento. Descarte de utensílios desabilitado no default. |
| `SetUpgradeLevel` | Host/dev autorizado; nível/alvo/limites; recalcular. |

### 9.3 Replicação e late join

- DTOs em `NetworkList<T>` implementam `INetworkSerializable, IEquatable<T>`.
- `PublishedRevision` no `PizzeriaNetworkState` como barreira de coerência.
- Relógio: `NetworkManager.ServerTime.Time` via `IClock`.

### 9.4 Limites de demonstração

- Pedidos ativos: 6 max
- Solicitações por telefone: 4 max
- Itens individualizados: 128 max
- Ingredientes distintos por pizza: 8 max
- Linhas/unidades de pedido: até 4 linhas/8 unidades; geração inicial de 1–3 pizzas
- Compras pendentes: 3 max
- Recebimento físico: 2 crates max
- Forno / staging / mochila máximos: 4 / 8 / 8

---

## 10. Prefab, cidade e inicialização

### 10.1 Prefab da Pizzaria (`PizzeriaPrefab.prefab`)

Root escala (1,1,1), pivot no chão, um único `NetworkObject`.
- `Building/`: Piso, paredes com porta aberta, interior acessível, colliders.
- `Stations/`: Geladeira, armário, bancada de utensílios, bancada neutra/espera, preparo, montagem, forno, embalagem, retirada, fornecedor, telefone, recebimento, lixeira. Anchor para serviço futuro.
- `Anchors/`: Spawns (4x), recebimento, slots de retirada, recuperação.
- `WorldUI/`: Quadro de pedidos, placas e indicadores.
- `PizzeriaRoot`, `PizzeriaNetworkState`, `PizzeriaCommandHandler`.

### 10.2 Ordem de reserva no CityGenerator

1. Gerar malha/quadras e candidatos de construção determinísticos com a seed.
2. Selecionar lote compatível com footprint (12×12 m) antes de gerar destinos.
3. Marcar candidato reservado e excluir somente sua geometria.
4. Gerar destinos de entrega apenas nos candidatos remanescentes.
5. Servidor spawna `PizzeriaPrefab`. Clientes recebem via NGO; nenhum cubo duplicado.
6. Altura do chão: `config.sidewalkHeight`.

### 10.3 Readiness e Late Join

Servidor habilita gameplay quando geração válida + restaurante spawned + domínio inicializado + destinos registrados + sessão autorizada. Clientes habilitam quando cidade local gerada + root conectado + projeção coerente. Late-joiners utilizam `_networkSeed` já replicada e recebem snapshot das `NetworkList`s.

---

## 11. Aprimoramentos funcionais

### 11.1 Fórmula única

Para stat numérico:
$$v = (\text{base} + \sum \text{FlatAdd}) \times (1 + \sum \text{PercentAdd}) \times \prod \text{Multiply}$$

Se existir Override: usar valor de maior prioridade. Aplicar clamp final. Capacidades inteiras usam `floor`. Durações, taxas e fatores mantêm decimais.

### 11.2 Exemplos demonstráveis

1. `FasterOven`: Categoria Oven, duração PercentAdd −0,20 (15 s → 12 s; graça permanece 10 s).
2. `FasterPrep`: Categoria Prep, duração PercentAdd −0,15 (3 s → 2,55 s).
3. `FasterPackaging`: Categoria Packaging, duração PercentAdd −0,25 (2 s → 1,5 s).
4. `ExtraOvenSlot`: Forno específico, FlatAdd +1 (2 → 3 slots).
5. `ExtraStorageCapacity`: Storage, capacidade FlatAdd +10 (20 → 30 por ingrediente).
6. `LargerBackpack`: Player/mochila, capacidade FlatAdd +1 (2 → 3 slots).
7. `BackpackInsulation`: Player/mochila, taxa Multiply 0,5 (0,003/s → 0,0015/s).
8. `UnlockMushroom`: Ingrediente + receita + processo (habilita catálogo e demanda).
9. `FasterSupply`: Restaurante, ETA PercentAdd −0,25 (20 s → 15 s).

Painel debug F9/Editor restrito ao host/operador autorizado.

---

## 12. Builders, UI e placeholders

### 12.1 Ferramentas de Editor

- `Tools > Scary Party > Pizzeria > Build or Update`: Idempotente; cria assets, prefabs, registra NetworkPrefabs.
- `Tools > Scary Party > Pizzeria > Validate Setup`: Inspeciona integridade sem mutação.
- `Tools > Scary Party > Pizzeria > Build Test Scene`: Cria `Assets/Scenes/PizzeriaTestScene.unity`.
- `Tools > Scary Party > Pizzeria > Configurar conteúdo`: Wizard de autoria.

### 12.2 Wizard de configuração de conteúdo — obrigatório

Janela de Editor `Tools > Scary Party > Pizzeria > Configurar conteúdo`, em português brasileiro. Ferramenta de autoria para o designer:
- Criar, editar, duplicar e validar ingredientes, receitas de pizza, utensílios, processos e bancadas sem escrever C#.
- Preview de mecânica e simulação.
- Rascunho em memória com cancelamento seguro.
- Precedência de tempo: override da bancada para aquele processo, se definido; caso contrário duração do `ProcessDef` → modificadores de upgrades → divisão por velocidade da estação e eficiência do utensílio.

#### Exemplo obrigatório de configuração
1. Ingrediente: queijo, estado inteiro.
2. Saída: uma porção de queijo, estado ralado.
3. Utensílio: ralador, capacidade `Grate`, um exemplar compartilhado.
4. Processo: Ralar queijo, um inteiro → um ralado, duração 3 s, trabalho ativo com ferramenta em uma mão.
5. Bancada: Preparo A, aceita Ralar queijo, um slot; saída permanece no mesmo slot.
6. Bancada: Preparo B, mesmo processo, override de duração 2 s.
7. Receita: mussarela exige queijo ralado. A pizza com queijo inteiro continua montável/entregável e será avaliada como incorreta.
8. Outro exemplo: Abrir massa, sem utensílio, por 3 s.

### 12.3 UI por código

`PizzeriaHudBuilder` constrói elementos UI Toolkit:
- Telefone: chamada; atender publica comanda.
- Painel de pedidos: pedido, endereço, receitas/quantidades e prazo.
- Fornecedor/storage: catálogo, custo, ETA, saldo, quantidade; seletores locais.
- Retirada: endereços da cidade, sem pedido/receita/sugestão de compatibilidade. Uma escolha por caixa.
- Mochila/mãos: conteúdo carregado e etiquetas, sem `OrderId` ou receita esperada.
- Mapa: pontos dos endereços escritos nas caixas carregadas pelo jogador local.
- Entrega: escolher caixas da própria carga; confirmar envio; resultado/recompensa após entrega.
- Debug: testes/upgrades claramente separados do jogo normal.

---

## 13. Etapas e critérios de aceitação

| Etapa | Implementação | Gate jogável |
|---|---|---|
| **0 — Auditoria** | Conferir checkout, referências, planos antigos e diferenças | Não duplicar seed/readiness já existentes. |
| **1 — Domínio** | Modelos de item/ferramenta, composição, pedido multilinha, etiqueta e avaliador; schemas e validação do wizard | Testes de pontuação e localização única. Sem allocation de caixas a pedidos. |
| **2 — Prefab/cidade** | Builder mínimo, Root, placement/readiness; wizard inicial de ingrediente, ferramenta, processo e bancada | Uma pizzaria acessível por host/cliente, anchors e destinos válidos. |
| **3 — Mãos/utensílios** | Inventory, tool rack, preparo, tool lease, HUD mínimo | Pegar queijo+ralador, ralar, deixar porção, outro jogador usar; um ralador não duplica. Cortar pickup legado. |
| **4 — Slice delivery** | Montagem permissiva, forno, embalagem, retirada/endereço, mochila, mapa por carga e entrega de conjunto | Pedido debug calabresa+mussarela; dois endereços de teste; entrega correta e queimada avaliadas. Desligar caixas/pagamento legados. |
| **5 — Telefone real** | DemandGenerator, atendimento imediato, quadro multilinha | Atender registra uma vez; ler painel e endereçar individualmente sem ajuda automática. Sem NPC presencial obrigatório. |
| **6 — Suprimentos** | Compra/ETA/crate/storage múltiplo | Estoque finito e reposição conservativa; sem entrega paga sumir por falta de espaço. |
| **7 — Conteúdo/UI** | Wizard completo de criação/edição/duplicação; receitas e processos configurados por ele; visual e presenters | Mistura errada pode assar/embalar/entregar; mapa segue etiqueta errada; resultado aparece após envio. |
| **8 — Upgrades** | Durações, capacidades, isolamento opcional, unlock de cogumelo, painel dev | Upgrades funcionam sem alterar contratos em andamento indevidamente; preparar suporte a eficiência de utensílio. |
| **9 — Robustez** | Snapshot, reset/recovery, testes multiplayer/build, docs | Sem duplicação de tool/item/recompensa; sem restauração das validações antigas contra erros de gameplay. |

### Checklist de migração de planos anteriores
- Remover validações NotReady/Burned/WrongRecipe como bloqueios culinários;
- Retirar ConfirmOrder adicional;
- Eliminar allocations de caixa a OrderId;
- Mover etiqueta para staging;
- Substituir mapa global de pedidos por mapa de carga;
- Implementar multi-pizza e tools agora;
- Retirar NPC/fila/assentos dos gates iniciais.

---

## 14. Tuning e cenário de demonstração

| Parâmetro | Default |
|---|---|
| Abrir massa / ralar queijo / fatiar calabresa / embalar | 3 s / 3 s / 2 s / 2 s |
| Cozinhar / graça após pronto | 15 s / 10 s |
| Utensílios | 1 ralador, 1 faca; sem desgaste/lavagem |
| Pedidos | 1–3 pizzas por solicitação; repetidas permitidas; 1 aberto por endereço |
| Toque / prazo delivery | 30 s / 300 s |
| Demanda | 15–35 s, até 6 pedidos; somente telefone inicialmente |
| ETA / compras pendentes | 20 s / 3 |
| Estoque inicial | Massa, molho, queijo inteiro: 20 cada; calabresa: 10; cogumelo bloqueado e sem estoque |
| Storage | Geladeira 20 por tipo/80 total; armário 30 por tipo/60 total |
| Pacotes | 5 unidades; massa/molho 5, queijo/calabresa/cogumelo 10 unidades monetárias |
| Recompensas de unidades | Mussarela 15, calabresa 20, cogumelo 25 |
| Correta bem assada / queimada / crua ou errada | 100% / 0% / 0% da unidade; configuráveis |
| Caixa do restaurante | 500 inicial; base 10 por pedido × fração de recompensa conquistada |
| Mãos / mochila / forno / retirada / receiving | 2 / 2 / 2 / 4 / 2 slots |

---

## 15. Como ampliar depois

- **Atendimento presencial**: Implementar fonte/cliente e espera em fila ou assento; falar registra comanda, servir sem embalagem usa `DeliveryEvaluator`.
- **Novo ingrediente/receita/processo/bancada**: Configurar diretamente pelo Wizard no Editor sem escrever código.
- **Monstro que leva caixas**: Transferência interna server-only para `ExternalHolder`.
- **Moto**: Leitura da carga via interface do `BackpackController`.

---

## 16. Entrega esperada da implementação

Ao implementar este plano, o Antigravity deve atualizar `Docs/PizzeriaImplementationPlan.md`, criar `Docs/PizzeriaSetup.md`, `Docs/PizzeriaArchitecture.md` e `Docs/PizzeriaValidation.md`, além do código e assets.

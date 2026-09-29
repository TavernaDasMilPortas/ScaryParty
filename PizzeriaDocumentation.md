# Documentação Técnica e Arquitetural do Sistema de Pizzaria e Criação de Pizza (ScaryParty)

Este documento detalha de ponta a ponta o funcionamento, a arquitetura de código, a física espacial, a sincronização em rede e as regras de negócio de todo o ecossistema da **Pizzaria** e da **Criação de Pizza** no projeto **ScaryParty**.

---

## 1. Visão Geral e Filosofia Arquitetural

A Pizzaria é o **coração operacional e social** do ScaryParty. Ela funciona como o refúgio dos jogadores, o ponto central de spawn cooperativo, a fonte de renda e o motor da economia da equipe durante a sobrevivência na cidade.

### Princípios de Design e Engenharia
O sistema foi construído seguindo **Clean Architecture** e **Domain-Driven Design (DDD)** adaptados para Unity e Netcode for GameObjects (NGO):
1. **Domínio Puro (Pure Domain Layer):** Classes C# puras (sem herança de `MonoBehaviour` ou dependência da engine gráfica), contendo regras de negócio, receitas, validações matemáticas, prazos e inventários.
2. **Autoridade Rígida do Servidor (Server-Authoritative):** Nenhuma alteração de estado (pegar ingrediente, fatiar, assar, gastar dinheiro) acontece no cliente. O cliente solicita a ação via `ServerRpc`; o domínio no servidor valida e processa atomicamente; o estado é replicado para os clientes.
3. **Projeção e Visualização Reativa (Replication Projector):** O estado de domínio do servidor é projetado em `NetworkList` e `NetworkVariable`. Clientes escutam alterações e instanciam/atualizam os modelos 3D locais (`PizzeriaItemVisualizer`), barras de progresso (`StationProgressBar`) e comanda (`TicketBoardPresenter`).
4. **Resiliência a Concorrência e Quedas:** Utensílios e bancadas operam com sistema de **Lease Temporizado (Time-leased locks)** com renovação por heartbeat, evitando que a desconexão ou morte de um jogador bloqueie uma bancada permanentemente.

---

## 2. Geração Procedural e Integração com a Cidade

A Pizzaria não possui um local estático fixo pré-definido no cenário: ela é posicionada organicamente na cidade procedural pelo `CityGenerator`.

```
                    +---------------------------+
                    |    CityGenerator.cs       |
                    | (Street Grid & Blocks)    |
                    +-------------+-------------+
                                  |
                                  v
                    +---------------------------+
                    |  FindBestPizzariaBlock()  |
                    |  - Menor Distância (0,0)  |
                    |  - Bônus por Tipo de Zona |
                    +-------------+-------------+
                                  |
                                  v
                    +---------------------------+
                    |     Desobstrução do Lote  |
                    | (Destrói prédios em AABB) |
                    +-------------+-------------+
                                  |
                                  v
                    +---------------------------+
                    | CityPizzeriaPlacement     |
                    | Adapter.SpawnPizzeria()   |
                    +-------------+-------------+
                                  |
          +-----------------------+-----------------------+
          |                                               |
          v                                               v
+--------------------+                         +--------------------+
|  PizzeriaPrefab    |                         | Bancada Externa    |
| (Rede, 14 Estações)|                         | e Spawns de Player |
+--------------------+                         +--------------------+
```

### 2.1 Algoritmo de Escolha do Quarteirão (`FindBestPizzariaBlock`)
O gerador avalia todos os quarteirões da malha procedural calculando uma pontuação (*Score*):
$$\text{Score} = \text{Distância ao Centro } (0, 0, 0) + \text{Bônus da Zona}$$

- **Zona Comercial:** Bônus = `0` (Preferência máxima para ficar no coração comercial seguro).
- **Zona Residencial:** Bônus = `20`.
- **Zona Industrial:** Bônus = `50`.
- **Zona dos Monstros:** Bônus = `100` (Penalidade máxima).

O quarteirão com o **menor Score** é eleito.

### 2.2 Desobstrução e Orientação
1. O gerador calcula o volume do prédio da pizzaria (12m x 12m) com rotação encarando a rua (`LookRotation(-streetDir)`).
2. Qualquer prédio residencial ou comercial pré-gerado que colida com o volume delimitador da pizzaria é destruído atomicamente.
3. A base da pizzaria é assentada perfeitamente no nível da calçada (`sidewalkHeight`).
4. A **Bancada Externa** é gerada a 7.5m à frente da porta principal, na calçada, acompanhada pelos 4 pontos de spawn de rede dos jogadores (`NetworkSpawnPoint_0..3`).

---

## 3. Estrutura Física e o Mapa das 14 Estações

O interior da Pizzaria (12m x 12m) é planejado de forma ergonômica para suportar múltiplos jogadores trabalhando em cooperação sem gargalos:

```
+-----------------------------------------------------------------------+
|                              [8] FORNO                                |
|                           (OvenStation)                               |
|                                                                       |
| [1] GELADEIRA       [5] PREPARO A    [6] PREPARO B     [7] MONTAGEM   |
| (Molho/Queijo/Top)  (Abrir Massa)    (Corte/Ralo)      (Assembly)     |
|                                                                       |
| [2] ARMÁRIO         [4] BANCADA NEUTRA                 [9] EMBALAGEM  |
| (Massa Seca)            (Buffer)                         (Packaging)  |
|                                                                       |
| [3] SUPORTE UTENS.                                     [10] EXPEDIÇÃO |
| (Faca / Ralador)                                       (Staging/Rotas)|
|                                                                       |
| [11] TERMINAL SUP.  [12] TELEFONE                      [14] LIXEIRA   |
| (Compras Insumos)    (Comandas)                         (Descarte)    |
+--------------------------[ PORTA / ENTRADA ]--------------------------+
                               |
              [13] RECEBIMENTO EXTERNO (Cargas/Suprimentos)
```

### Detalhamento das Estações (`ScaryParty.Pizzeria.Stations`)

| ID | Nome no Código | Tipo de Componente | Função Primária | Interações |
|---|---|---|---|---|
| **1** | `Storage_Fridge` | `StorageStation` | Geladeira de ingredientes perecíveis (Molho, Queijo Inteiro, Calabresa, Cogumelo). | `[E]` Abre o menu da geladeira para sacar insumos. |
| **2** | `Storage_Cupboard` | `StorageStation` | Armário seco para bolas de massa. | `[E]` Abre o menu para retirar porções de massa. |
| **3** | `Station_ToolRack` | `ToolRackStation` | Suporte compartilhado de ferramentas (Faca e Ralador). | `[E]` Pega ou devolve utensílios nas mãos. |
| **4** | `Station_NeutralCounter`| `CounterStation` | Bancada lisa de apoio para descanso e troca de itens entre jogadores. | `[E]` Coloca o item da mão ou pega o item depositado. |
| **5** | `Station_Prep_A` | `PrepStation` | Bancada prioritária para abertura de massa (Processo 1). | `[E]` Apoia item / `[F]` Segura para abrir a massa. |
| **6** | `Station_Prep_B` | `PrepStation` | Bancada prioritária para corte e ralagem com utensílios. | `[E]` Apoia item / `[F]` Segura com faca ou ralador na mão. |
| **7** | `Station_Assembly` | `AssemblyStation` | Estação de montagem das pizzas (camadas sobre a massa aberta). | `[E]` Adiciona molho, queijo ou coberturas à massa. |
| **8** | `Station_Oven` | `OvenStation` | Forno industrial de alta capacidade com cozimento autônomo. | `[E]` Insere pizza crua / `[E]` Retira pizza assada. |
| **9** | `Station_Packaging` | `PackagingStation` | Bancada com caixas térmicas de papelão. | `[F]` Segura para embalar a pizza em caixa. |
| **10** | `Station_Staging` | `StagingStation` | Balcão de despacho com etiquetadora de endereço de entrega. | `[E]` Abre interface de etiquetagem com o destino. |
| **11** | `Terminal_Supplier` | `SupplierTerminal`| Computador de gestão de suprimentos e fornecedores. | `[E]` Abre catálogo para comprar insumos com orçamento da pizzaria. |
| **12** | `Station_Phone` | `PhoneStation` | Telefone analógico de clientes da cidade. | `[E]` Atende chamada quando estiver tocando, gerando comanda. |
| **13** | `Area_Receiving` | `ReceivingStation` | Pátio externo na calçada onde os fornecedores descarregam engradados. | `[E]` Descarrega caixas de compras para o estoque interno. |
| **14** | `Station_Trash` | `TrashStation` | Lixeira industrial para descarte rápido de erros ou cinzas. | `[F]` Segura para incinerar o item segurado. |

---

## 4. O Ciclo de Vida da Pizza (Passo a Passo)

A produção de uma pizza no jogo segue um fluxo industrial rigoroso:

```
[ Armário ] ----> Massa Bruta (ID 1)
                      |
                      v  [PrepStation A] (Segurar F - 3.0s)
                  Massa Aberta (ID 2)
                      |
                      | <------- Insumos Preparados:
                      |          - Molho Pronto (ID 3)
                      |          - Queijo Inteiro (ID 4) + Ralador ---> Queijo Ralado (ID 5)
                      |          - Calabresa Inteira (ID 6) + Faca ---> Calabresa Fatiada (ID 7)
                      v
            [ AssemblyStation ] (Montagem das Camadas via [E])
                      |
                      v  Pizza Crua Montada
              [ OvenStation ] (Forno Autônomo - 15.0s)
                      |
                      +---> Perfeita: Pizza Assada (Baked)
                      |
                      +---> Se passar +10s no forno: QUEIMADA (Burned)
                      |
                      v
            [ PackagingStation ] (Segurar F)
                      |
                      v  Caixa de Pizza
             [ StagingStation ] (Etiquetar Endereço do Pedido)
                      |
                      v  Caixa Etiquetada
          [ Mão / Mochila do Entregador ]
                      |
                      v  Rastreamento via Minimapa (CargoMapAdapter)
             [ DeliveryPoint ] (Entrega na Cidade e Pagamento)
```

### Passo 1: Obter e Abrir a Massa
1. O jogador vai ao Armário (`StorageStation` ID 2), abre o painel e clica em "Pegar" na **Massa** (ID 1, `Category: Dough`).
2. A massa surge instantaneamente na mão ativa do jogador (esquerda ou direita).
3. O jogador vai à `PrepStation A` (ID 5) e pressiona `[E]` para colocá-la ou segura `[F]` diretamente.
4. Segurar `[F]` executa `StartWorkAtomicServerRpc` para o processo `Process_1_AbrirMassa`. 
5. Durante os 3 segundos de trabalho, a barra de progresso verde sobe. Se o jogador soltar a tecla antes, a operação pausa; ao segurar novamente, continua de onde parou.
6. Ao término, a massa bruta é transformada atomicamente em **Massa Aberta** (`DefinitionId: 2`, `PrepStage: Prepared`).

### Passo 2: Preparação dos Ingredientes (Processos e Utensílios)
Alguns ingredientes exigem processamento prévio com ferramentas:
- **Queijo Ralado:**
  1. Pegar o **Ralador** (`Tool_Grater`, ID 1, `ToolCapability.Grate`) no Suporte de Utensílios (`ToolRackStation` ID 3).
  2. Pegar o **Queijo Inteiro** (ID 4) na Geladeira (`StorageStation` ID 1).
  3. Ir à bancada de preparo (`PrepStation B` ID 6) e segurar `[F]`.
  4. O servidor valida se o jogador possui o item e a ferramenta com a habilidade necessária (`Grate`). Duração: 3.0s.
  5. Resultado: **Queijo Ralado** (ID 5, `PrepStage: Prepared`).
- **Calabresa Fatiada:**
  1. Pegar a **Faca** (`Tool_Knife`, ID 2, `ToolCapability.Slice`).
  2. Pegar a **Calabresa Inteira** (ID 6) na Geladeira.
  3. Ir à `PrepStation` e segurar `[F]`. Duração: 2.0s.
  4. Resultado: **Calabresa Fatiada** (ID 7, `PrepStage: Prepared`).
- **Molho:** O molho já é dispensado pronto para uso (`requiresPrep: false`).
- **Cogumelos (Especial):** Desbloqueados através do sistema de upgrades.

### Passo 3: Montagem da Pizza (`AssemblyStation`)
1. A **Massa Aberta** é colocada sobre a `AssemblyStation` (ID 7).
2. O cozinheiro carrega os ingredientes preparados na mão ativa e pressiona `[E]` mirando na massa (`AddIngredientToPizzaServerRpc`).
3. O domínio adiciona o ingrediente à lista interna (`ItemState.Ingredients`), consome o item segurado e atualiza a máscara visual (`IngredientMask`):
   - **Bit 0 (Valor 1):** Molho
   - **Bit 1 (Valor 2):** Queijo
   - **Bit 2 (Valor 4):** Cobertura (Calabresa / Cogumelo)
4. O `PizzeriaItemVisualizer` detecta as alterações na rede em tempo real e projeta as camadas na pizza:
   - Uma camada cilíndrica vermelha fina para o molho.
   - Uma camada amarela por cima para o queijo.
   - Quatro fatias esféricas distribuídas proceduralmente em ângulos radiais para as carnes/cogumelos.

### Passo 4: O Forno (`OvenStation`)
1. O cozinheiro segura a pizza montada e pressiona `[E]` no forno (`InsertOvenServerRpc`).
2. O servidor assume o cozimento como processo autônomo (`OperationState.ServerWorker`).
3. **Estágios do Forno:**
   - **0% a 99% (`CookingStage.Cooking`):** Pizza crua cozinhando. A barra de progresso exibe verde até 80%, tornando-se amarela perto do término. Tempo base: **15 segundos**.
   - **100% (`CookingStage.Baked`):** Pizza perfeitamente assada! O HUD e o prompt indicam `[E] Retirar Pizza (Pronta! 100%)`.
   - **Janela de Tolerância de Queima (`ovenBurnGraceDuration` = 10s):** Se a pizza não for retirada a tempo, a contagem de queima é iniciada. A barra passa para vermelho vivo.
   - **Queimada (`CookingStage.Burned`):** Após os 10 segundos extras, a pizza carboniza completamente (a textura do modelo fica preta fosca e a remuneração cai para zero).
4. O jogador aperta `[E]` para retirar a pizza para a mão livre.

### Passo 5: Embalagem (`PackagingStation`)
1. O cozinheiro coloca a pizza assada na bancada de embalagem (`PackagingStation` ID 9).
2. Segura a tecla `[F]` (`StartPackagingServerRpc`).
3. A pizza é convertida em um pacote lacrado: `PackagingState = Boxed`, mudando seu aspecto no mundo para uma caixa de papelão marrom 3D.

### Passo 6: Expedição e Etiquetagem de Rota (`StagingStation`)
1. A caixa é transportada até o balcão de expedição (`StagingStation` ID 10).
2. O entregador pressiona `[E]`, abrindo o painel UI de endereçamento (`OpenStagingAddressPicker`).
3. O jogador consulta a comanda de pedidos no Quadro de Avisos (`TicketBoardPresenter`), seleciona o número do destino desejado (ex: Endereço `#4`) e confirma.
4. A caixa recebe o atributo permanente `LabelDestinationId = 4`.

### Passo 7: Logística de Transporte (Mãos, Mochila e Minimapa)
- **Mãos do Jogador:** Teclas `[1]` e `[2]` alternam entre as mãos esquerda e direita. O jogador pode carregar até duas caixas ou itens visíveis nas mãos.
- **Mochila do Entregador (`BackpackController`):** Capacidade inicial para **2 caixas extras** guardadas nas costas (liberando as mãos para armas ou lanternas). Expansível via upgrades.
- **Integração com Minimapa (`CargoMapAdapter`):** Assim que o jogador coloca caixas etiquetadas em suas mãos ou mochila, o adaptador cria automaticamente trajetos iluminados no minimapa ligando a posição atual até as casas dos clientes correspondentes na cidade.

### Passo 8: Entrega e Liquidação Financeira
1. Ao chegar no destino na cidade, o entregador se aproxima do marcador `DeliveryPoint` e aperta `[E]`.
2. O `DeliveryPointAdapter` coleta todas as caixas compatíveis carregadas pelo jogador e dispara `SubmitDeliveryServerRpc`.
3. O avaliador do servidor (`DeliveryEvaluator`) calcula a remuneração:
   - **Correspondência da Receita:** Avalia se os ingredientes da pizza batem exatamente com o pedido do cliente.
   - **Ponto de Cozimento:**
     - Assada perfeita: 100% do valor da receita.
     - Crua: 0% do valor.
     - Queimada: 0% do valor.
   - **Divisão Financeira:**
     - **Dinheiro Pessoal (`PlayerState.Money`):** O entregador recebe o valor das pizzas entregues diretamente em sua carteira individual para gastar no lobby/loja pessoal.
     - **Orçamento da Pizzaria (`PizzeriaState.RestaurantBudget`):** O restaurante ganha uma taxa de serviço fixa (`restaurantBaseBonus = R$ 10`) por pedido concluído com sucesso, usada para repor insumos e comprar melhorias na cozinha.
4. As caixas são destruídas do mundo e o pedido é marcado como concluído (`OrderLifecycle.Completed`).

---

## 5. Engenharia de Domínio e Regras de Negócio

### 5.1 Catálogo de Receitas Base
O arquivo de configuração `PizzeriaConfig.asset` traz as receitas fundamentais do jogo:

| ID | Receita | Base | Ingredientes Exigidos | Preço Base | Tempo Forno |
|---|---|---|---|---|---|
| **1** | **Mussarela** | Massa Aberta (ID 2) | 1x Molho (ID 3) + 1x Queijo Ralado (ID 5) | R$ 15 | 15s |
| **2** | **Calabresa** | Massa Aberta (ID 2) | 1x Molho (ID 3) + 1x Queijo Ralado (ID 5) + 1x Calabresa Fatiada (ID 7) | R$ 20 | 15s |
| **3** | **Cogumelo** *(Upgrade)* | Massa Aberta (ID 2) | 1x Molho (ID 3) + 1x Queijo Ralado (ID 5) + 1x Cogumelo Fatiado (ID 9) | R$ 25 | 15s |

### 5.2 Avaliação de Entrega (`DeliveryEvaluator`)
A função de avaliação faz um emparelhamento guloso (*greedy match*) decrescente por preço entre as pizzas entregues e as unidades solicitadas na comanda:

- **Recompensa Pessoal:** Soma do preço base das receitas multiplicada pelo fator de cozimento (1.0 para assada, 0.0 para crua ou queimada).
- **Ganho do Restaurante:** Bônus base de R$ 10 proporcional à taxa de sucesso da entrega.

### 5.3 Sistema de Leases de Utensílios e Bancadas
Para evitar que dois jogadores manipulem a mesma estação simultaneamente ou que uma desconexão trave uma bancada:
- Ao iniciar o corte ou ralagem, o jogador adquire um **Lease** de 0.75 segundos no utensílio e no slot da estação (`ProcessingService.StartWork`).
- Enquanto mantém a tecla `[F]` pressionada, o cliente envia mensagens de `HeartbeatWorkServerRpc` a cada 0.2 segundos, renovando a expiração do lease.
- Se a tecla for solta, o jogador morrer ou a conexão cair, o lease expira em menos de 1 segundo, pausando o progresso acumulado e liberando a bancada e a ferramenta para os outros jogadores.

---

## 6. Sincronização em Rede e DTOs (NGO)

Para manter a taxa de atualização rápida e consumo de banda mínimo, a pizzaria utiliza **NetworkLists de DTOs compactos** em vez de sincronizar GameObjects e Transforms pesados via `NetworkTransform`.

```
           SERVIDOR (Autoridade)                          CLIENTES (Espelhos Visuais)
   +------------------------------------+             +------------------------------------+
   |   PizzeriaState (Pure Domain)      |             |   PizzeriaNetworkState (NGO)       |
   | - Items (Dict<ItemId, ItemState>)  |             | - Items: NetworkList<NetItemDto>   |
   | - ActiveOperations (Baking, Prep)  |             | - Orders: NetworkList<NetOrderDto> |
   +-----------------+------------------+             +-----------------+------------------+
                     |                                                  |
                     v (CommitAndReplicate)                             v (Eventos OnListChanged)
   +------------------------------------+             +------------------------------------+
   |   ReplicationProjector             |             |   PizzeriaItemVisualizer           |
   | - Calcula deltas de structs        |============>| - Instancia / Move primitivas 3D   |
   | - Atualiza NetworkLists            |   (Rede)    | - Aplica materiais e cores         |
   |                                    |             | - Fixa itens nos ossos das mãos    |
   +------------------------------------+             +------------------------------------+
```

### Principais DTOs Serializáveis (`NetDtos.cs`)
1. `NetItemDto`: ID único do item, definição, categoria, estágio de preparo, estágio de cozimento, estado da caixa, tipo de localização (mão, mochila, bancada, estoque), máscara de ingredientes e revisor de versão.
2. `NetStationSlotDto`: ID da estação, slot, item depositado, ferramenta em uso, workerId do jogador, progresso de trabalho (0.0 a 1.0), ID do processo e tempo de início.
3. `NetOrderDto`: ID do pedido, destino da entrega, ciclo de vida, prazo limite, até 3 linhas de receitas e quantidades solicitadas.
4. `NetStorageDto`: ID do armazém/geladeira, ID do ingrediente, quantidade restante e capacidade máxima.
5. `NetUpgradeDto`: ID da melhoria e nível atual adquirido.

---

## 7. Economia, Suprimentos e Upgrades

### 7.1 Gestão de Estoque e Suprimentos
Os ingredientes não são infinitos:
- Geladeira e armário começam com cotas iniciais (ex: 20 massas, 20 molhos, 20 queijos, 10 calabresas).
- Conforme as pizzas são preparadas, o estoque diminui.
- **Reposição:** O gerente usa o `SupplierTerminal` (ID 11) e gasta o caixa da pizzaria (`RestaurantBudget`) para solicitar caixas de insumos.
- A entrega demora um tempo base de trânsito (ETA aproximado de 20s) e chega na `ReceivingStation` (ID 13) na calçada externa.
- Os jogadores devem descarregar as caixas para reabastecer as estações internas.

### 7.2 Catálogo de Upgrades (`UpgradeDef`)
O sistema suporta árvores de melhorias acumuláveis compradas pelo time:

| ID | Nome | Escopo | Estatística Modificada | Operação | Efeito por Nível |
|---|---|---|---|---|---|
| **1** | `FasterOven` | Forno | `cookDuration` | `PercentAdd` | -20% no tempo de forno (pizza assa mais rápido). |
| **2** | `ExtraOvenSlot` | Forno | `slotCount` | `FlatAdd` | +1 slot para assar mais pizzas simultaneamente. |
| **3** | `LargerBackpack`| Mochila | `slotCount` | `FlatAdd` | +1 compartimento de caixa na mochila do entregador. |
| **4** | `BackpackInsulation` | Mochila | `decayMultiplier` | `Multiply` | Reduz perda de temperatura da pizza no trajeto. |
| **5** | `UnlockMushroom` | Receita | `3` (ID da receita) | `BoolSet` | Desbloqueia a receita de Pizza de Cogumelo. |
| **6** | `FasterSupply` | Restaurante | `supplyETA` | `PercentAdd` | -25% no tempo de frete das compras de insumos. |

---

## 8. Ferramentas de Editor e Teste (Tooling)

O projeto conta com ferramentas integradas no menu superior do Unity (`Tools > Scary Party > Pizzeria`):

1. **Build or Update (`PizzeriaBuilder.cs`):**
   - Cria todos os materiais com shaders URP Lit e cores padronizadas.
   - Gera os prefabs base de itens e ferramentas.
   - Cria o ScriptableObject `PizzeriaConfig` com todos os ingredientes e receitas caso não existam.
   - Constrói o `PizzeriaPrefab` completo com paredes, pisos, as 14 estações e âncoras de spawn.
   - Registra automaticamente o prefab na lista `DefaultNetworkPrefabs.asset` do Netcode.
   - Atribui o prefab ao `CityGenerator` presente na cena aberta.

2. **Validate Setup (`PizzeriaValidator.cs`):**
   - Varre a base de dados verificando duplicidade de IDs em ingredientes, receitas e processos.
   - Valida se o `PizzeriaPrefab` possui `NetworkObject`, `PizzeriaRoot` e as 4 âncoras de jogadores.
   - Confirma a presença no catálogo do Netcode, emitindo relatório de erros e alertas.

3. **Configurar Conteúdo (`PizzeriaContentWizard.cs`):**
   - Janela visual construída com Odin Inspector permitindo criar, editar e balancear ingredientes, tempos de preparo, preços e novos upgrades sem precisar mexer em código.

4. **Painel de Testes In-Game (Tecla `F9`):**
   - Ao pressionar `F9` durante uma partida, o `PizzeriaHudBuilder` exibe um menu de desenvolvedor na tela:
     - Forçar disparo de pedidos de teste para endereços específicos.
     - Comprar suprimentos de teste instantâneos.
     - Aplicar upgrades em tempo real (Forno rápido, mochila maior, desbloqueio de cogumelo) para testar o balanceamento da gameplay.

---

## 9. Mapeamento de Arquivos no Projeto

| Subpasta | Arquivo | Descrição |
|---|---|---|
| `Authoring/` | `IngredientDef.cs`, `RecipeDef.cs`, `ProcessDef.cs`, `ToolDef.cs`, `StationDef.cs`, `SupplyItemDef.cs`, `UpgradeDef.cs`, `PizzeriaConfig.cs` | ScriptableObjects de configuração e dados de catálogo. |
| `Domain/Definitions/` | `DefinitionCatalog.cs` | Registro central em memória de todas as definições ativas. |
| `Domain/Models/` | `ItemState.cs`, `OperationState.cs`, `OrderState.cs`, `PizzeriaState.cs`, `SupplyState.cs` | Modelos de dados puros que representam o estado em tempo real no servidor. |
| `Domain/Types/` | `Ids.cs`, `LocationRef.cs`, `CommandResult.cs` | Value objects fortemente tipados (IDs, enums de categorias e resultados). |
| `Domain/Services/` | `RecipeEvaluator.cs`, `ProcessingService.cs`, `OrderService.cs`, `DeliveryEvaluator.cs`, `TransferService.cs`, `UpgradeService.cs` | Serviços matemáticos e executores de regras de negócio. |
| `Stations/` | `StationView.cs`, `PrepStation.cs`, `AssemblyStation.cs`, `OvenStation.cs`, `PackagingStation.cs`, `StagingStation.cs`, `StorageStation.cs`, `ToolRackStation.cs`, `PhoneStation.cs`, `SupplierTerminal.cs`, `ReceivingStation.cs`, `TrashStation.cs`, `CounterStation.cs` | Classes de estações interativas com raycast, prompts dinâmicos e slots 3D. |
| `Network/` | `NetDtos.cs`, `PizzeriaNetworkState.cs`, `PizzeriaCommandHandler.cs`, `ReplicationProjector.cs`, `PhysicalItem.cs` | Camada de rede autoritativa (RPCs, DTOs serializáveis e sincronização NGO). |
| `Composition/` | `PizzeriaRoot.cs`, `Night1Bootstrapper.cs` | Composição e inicialização da pizzaria e orquestrador de pedidos da Noite 1. |
| `Integration/` | `CityPizzeriaPlacementAdapter.cs`, `DeliveryPointAdapter.cs`, `CargoMapAdapter.cs` | Adaptadores entre o módulo da pizzaria e a cidade procedural, pontos de entrega e minimapa. |
| `Player/` | `PlayerInventoryAdapter.cs`, `BackpackController.cs` | Controle de mãos ativas (teclas 1 e 2), HUD de itens e mochila de caixas. |
| `Presentation/` | `PizzeriaItemVisualizer.cs`, `PizzeriaHudBuilder.cs`, `TicketBoardPresenter.cs`, `StationProgressBar.cs`, `NotificationManager.cs`, `PizzeriaSoundManager.cs` | Renderização procedural de pizzas, UI Toolkit, áudio e toasts na tela. |
| `Editor/` | `PizzeriaBuilder.cs`, `PizzeriaContentWizard.cs`, `PizzeriaLogicInjector.cs`, `PizzeriaValidator.cs` | Ferramentas de menu para criação, validação e configuração da pizzaria no Unity Editor. |

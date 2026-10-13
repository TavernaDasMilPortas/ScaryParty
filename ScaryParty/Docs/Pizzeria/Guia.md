# Guia do Módulo Pizzaria, Comandas e Delivery

**ScaryParty — Módulo de Cozinha, Comandas e Delivery**  
**Versão:** 2.0 (Etapas 0 a 9 concluídas)  
**Idioma:** Português do Brasil (PT-BR)

---

## 1. Visão Geral

O Módulo Pizzaria é um sistema completo e autoritativo de cozinha cooperativa e logística de entregas no mundo aberto da cidade. Ele é construído sobre Unity Netcode for GameObjects (NGO) e arquitetura de domínio puro com separação total entre regras de negócio (`Domain`), replicação de rede (`Network`), interação física (`Stations`), e apresentação visual (`Presentation`).

---

## 2. Acesso ao Menu e Ferramentas (Wizard & Builders)

Todas as ferramentas do módulo estão disponíveis no menu superior da Unity: `Tools > Scary Party > Pizzeria`.

### 2.1 Wizard de Conteúdo (Odin Inspector)
- **Menu:** `Tools > Scary Party > Pizzeria > Configurar conteúdo`
- **Seções Disponíveis:**
  - **Visão Geral:** Edição do ScriptableObject global `PizzeriaConfig.asset` (capacidades, orçamentos, multiplicadores, tempos).
  - **Ingredientes:** Catálogo de ingredientes base e processados (`IngredientDef`).
  - **Utensílios:** Catálogo de ferramentas (`ToolDef` — facas, raladores) e suas capacidades.
  - **Processos:** Transições de estado culinário (`ProcessDef` — ralar, fatiar, abrir massa).
  - **Receitas:** Definição das pizzas (`RecipeDef` — Margherita, Calabresa, Cogumelos, Quatro Queijos).
  - **Cenário e Validação:** Atalhos para validar o setup e executar builders idempotentes.

### 2.2 Builders Idempotentes
Todos os builders utilizam `BuilderToolkit` e gravam `GeneratedMarker`, garantindo que múltiplas execuções nunca dupliquem objetos ou estraguem configurações manuais:
- `Tools > Scary Party > Pizzeria > Builders > Reconstruir Pizzeria Prefab`: Gera ou atualiza o prefab central da pizzaria (`PizzeriaPrefab.prefab`), registrando no `DefaultNetworkPrefabs.asset`.
- `Tools > Scary Party > Pizzeria > Builders > Construir Telefone e Campainha`: Configura a estação telefônica física com `PhoneRinger` e chimes sonoros harmônicos.
- `Tools > Scary Party > Pizzeria > Builders > Construir Quadro Físico de Comandas`: Constrói o quadro de comandas 3D (`TicketBoardStation`) com slots físicos e papéis dinâmicos.
- `Tools > Scary Party > Pizzeria > Builders > Construir Pilha de Staging`: Configura o pallet físico de pilhas de caixas (`StackStation`) com empilhamento vertical (+0.12m/caixa).
- `Tools > Scary Party > Pizzeria > Builders > Configurar Pontos de Entrega na Cidade`: Percorre os prédios da cidade, atribuindo `AddressId` sequencial, beacons pulsantes e textos 3D de endereço (`DeliveryPointView`).
- `Tools > Scary Party > Pizzeria > Validate Setup`: Realiza diagnóstico completo de IDs, estações, prefabs e configurações.

---

## 3. Controles do Jogador

Os controles integram-se ao Input System e contam com controle de foco via `GameplayInputGate` (bloqueando mira/ataque durante modais e consumindo a tecla Esc):

| Tecla / Ação | Função no Gameplay |
|---|---|
| **E (Pressionar)** | Interagir com estações, atender telefone, colocar/retirar itens das bancadas e forno, interagir com ponto de entrega da residência. |
| **F (Segurar)** | Executar trabalho contínuo (abrir massa, ralar queijo, picar calabresa/cogumelos, embalar pizza na caixa, descartar na lixeira, arquivar comanda). |
| **1 / 2** | Alternar entre Mão Esquerda (Slot 0) e Mão Direita (Slot 1). *(Scroll do mouse e Q também preservados)*. |
| **B (Pressionar)** | Abrir / fechar o painel interativo da **Mochila Térmica** (guardar caixa da mão ativa, retirar para mão, largar mochila). |
| **H (Pressionar)** | Abrir / fechar o **Livro de Receitas** interativo na tela. |
| **F9 (Pressionar)** | Abrir / fechar o **Painel Dev / Debug** (exclusivo para testes no editor/host). |
| **Esc** | Fechar qualquer janela ou modal aberto com prioridade sobre o menu de pausa. |

---

## 4. Fluxo de Jogo Completo (Game Loop)

### 1. Atendimento de Chamadas (Telefone)
- O `Night1Bootstrapper` agenda chamadas com clientes da cidade periodicamente.
- O telefone físico toca com sinal luminoso e sonoro (`PhoneRinger`).
- Aproxime-se e pressione **[E]** para atender. A chamada é convertida atomicamente em um pedido publicado no quadro.

### 2. Quadro Físico de Comandas
- A comanda surge fisicamente no `TicketBoardStation` com o número do pedido, endereço formatado (`Endereço X`), pizzas pedidas e tempo limite regressivo.
- Mirar na folha e pressionar **[E]** abre o modal de zoom em tela cheia com detalhes de ingredientes.

### 3. Preparo dos Ingredientes
- Pegue massa no armário e coloque na bancada de preparo. Segure **[F]** para abrir a massa.
- Pegue molho na geladeira e aplique com **[E]**.
- Pegue queijo inteiro e ralador no suporte; na bancada de preparo, segure **[F]** para obter queijo ralado.
- Fatie calabresa ou cogumelos com a faca segurando **[F]**.
- Adicione as coberturas na pizza montada.

### 4. Forneamento (Forno Dinâmico)
- Leve a pizza montada ao forno e deposite com **[E]**.
- O forno emite luz de chamas trêmula (Perlin noise) e fumaça dinâmica.
- Aos 100% do cozimento, um sino harmônico (*ding!*) soa.
- **Atenção:** Retire a pizza assada a tempo! Se permanecer no forno após o tempo de tolerância, ela começará a queimar e emitirá fumaça preta espessa.

### 5. Embalagem e Expedição
- Leve a pizza assada à bancada de embalagem e segure **[F]** (2 segundos) para colocá-la na caixa.
- Rotule o endereço de destino na bancada de staging ou deposite na pilha de caixas (`StackStation`).

### 6. Mochila Térmica e Transporte
- Pressione **[B]** para inspecionar sua mochila térmica de 3 compartimentos.
- Guarde caixas na mochila para transportar até 3 caixas nas costas simultaneamente, além de 2 caixas nas mãos.
- Outros jogadores conseguem ver a mochila e as caixas empilhadas nas suas costas em tempo real (`BackpackVisualizer`).

### 7. Navegação e Entrega na Cidade
- O minimapa gera automaticamente rotas com linhas-guia para as casas das caixas que você está carregando (`CargoMapAdapter`).
- Ao chegar na residência, o beacon luminoso e a luz de varanda (`DeliveryPointView`) piscam para indicar que há um pedido ativo para aquela casa.
- Aproxime-se e pressione **[E]** para abrir a tela de seleção de caixas (`DeliverySelectionPresenter`).
- Selecione as caixas desejadas e confirme.
- O servidor valida a posse e os itens, credita o dinheiro pessoal do entregador (`PlayerState.Money`) e o orçamento da pizzaria (`RestaurantBudget`), e exibe o recibo detalhado na tela (`DeliveryResultPresenter`).

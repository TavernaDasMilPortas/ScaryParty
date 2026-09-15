# ScaryParty — Guia de Configuração e Uso da Pizzaria (Setup)

Este documento fornece as instruções para configurar, autoralizar e jogar o módulo cooperativo de pizzaria no **ScaryParty**.

---

## 1. Menus de Editor e Geração Automática

Todos os menus da pizzaria estão centralizados em `Tools > Scary Party > Pizzeria`:

1. **`Tools > Scary Party > Pizzeria > Build or Update`**:
   - Cria as pastas de assets caso não existam (`Assets/Prefabs/Pizzeria/`, `Assets/ScriptableObjects/Pizzeria/`, etc.).
   - Gera materiais e prefabs placeholders de itens físicos (`PizzaBox`, `PizzaWIP`, `IngredientItem`, `Tool_Grater`, `Tool_Knife`, `SupplyCrate`).
   - Registra todos os prefabs em `Assets/DefaultNetworkPrefabs.asset` de forma idempotente (sem duplicatas).
   - Cria o ScriptableObject `PizzeriaConfig.asset` com definições padrão de receitas, ingredientes, processos e upgrades.
   - Constrói e salva o `Assets/Prefabs/Pizzeria/PizzeriaPrefab.prefab` com paredes, portas abertas, 14 bancadas/estações especializadas, e âncoras de spawn para 4 jogadores.

2. **`Tools > Scary Party > Pizzeria > Validate Setup`**:
   - Executa uma varredura de integridade estrutural sem alterar arquivos.
   - Verifica IDs duplicados, consistência de definições, registros em `DefaultNetworkPrefabs` e integridade do prefab.

3. **`Tools > Scary Party > Pizzeria > Configurar conteúdo`**:
   - Abre o Wizard de Autoria em português brasileiro (veja seção 2).

---

## 2. Tutorial do Wizard de Autoria (`Configurar conteúdo`)

O Wizard permite que designers criem novos ingredientes, receitas de pizza, processos, ferramentas e bancadas **sem tocar em código C#**.

### Exemplo Demonstrativo 1: Ralar Queijo com Ralador
1. Abra `Tools > Scary Party > Pizzeria > Configurar conteúdo`.
2. Na aba **Ingredientes**:
   - Crie `Queijo Inteiro` (ID 4, Categoria `Cheese`, Requer Preparo = Sim, ID Resultante = 5).
   - Crie `Queijo Ralado` (ID 5, Categoria `Cheese`, Requer Preparo = Não).
3. Na aba **Utensílios**:
   - Crie `Ralador` (ID 1, Capacidade = `Grate`, Qtd Inicial = 1).
4. Na aba **Processos**:
   - Crie `Ralar Queijo`:
     - Insumo: ID 4 (`Queijo Inteiro`), Estágio `Raw`
     - Saída: ID 5 (`Queijo Ralado`), Estágio `Prepared`
     - Ferramenta Exigida: `Grate`
     - Duração Base: `3.0` segundos.
5. Na aba **Bancadas**:
   - `Bancada Preparo A`: aceita o processo 2 (`Ralar Queijo`) com duração base de 3s.
   - `Bancada Preparo B`: aceita o mesmo processo 2, mas com **override local de duração de 2s**.

### Exemplo Demonstrativo 2: Abrir Massa sem Utensílio
1. Na aba **Processos**:
   - Insumo: ID 1 (`Massa`), Estágio `Raw`
   - Saída: ID 2 (`Massa Aberta`), Estágio `Prepared`
   - Ferramenta Exigida: `None`
   - Duração Base: `3.0` segundos.

Ao clicar em **Salvar**, os ScriptableObjects são salvos e registrados no `PizzeriaConfig.asset` imediatamente.

---

## 3. Controles do Jogador

| Tecla | Ação | Descrição |
|---|---|---|
| `E` | **Interagir / Transferir** | Pega itens do chão/bancada, dispensa do armário/geladeira, atende o telefone, insere/retira do forno, ou entrega no destino. |
| `F` (Segurar) | **Trabalhar** | Segure enquanto olha para uma bancada com ingrediente (ex: ralar queijo, fatiar calabresa ou embalar pizza). |
| `Q` | **Alternar Mão Ativa** | Alterna o foco entre a mão esquerda e direita (`[★]` no HUD indica a mão ativa). |
| `B` | **Mochila** | Carrega/descarrega caixas de pizza na mochila de entregas. |
| `G` | **Soltar Item** | Larga o item da mão ativa no chão. |
| `F9` | **Painel Dev** | Abre o menu de testes para publicar pedidos de teste, comprar suprimentos e aplicar upgrades em tempo real. |

---

## 4. Passo a Passo do Cenário de Demonstração (Loop Completo)

1. **Iniciar Jogo**:
   - Abra a cena `GameScene.unity` (ou `ReadyScene.unity` via lobby).
   - Ao iniciar como Host ou Servidor, a cidade é gerada e a pizzaria é spawnada via rede na calçada. O jogador nasce nas âncoras dentro da pizzaria.
2. **Atendimento do Pedido**:
   - O telefone começa a tocar. Aproxime-se e pressione `E` no `Station_Phone`.
   - O pedido é publicado imediatamente na comanda do quadro `TicketBoard` (ex: Pedido #1, Endereço #4, 1x Mussarela e 1x Calabresa).
3. **Preparo com Utensílios Compartilhados**:
   - Vá ao armário (`Storage_Cupboard`) e pressione `E` para retirar a Massa. Coloque-a na bancada de preparo e segure `F` para abrir a massa.
   - Pegue o `Ralador` da bancada de ferramentas (`Station_ToolRack`) com uma das mãos.
   - Retire o `Queijo Inteiro` da geladeira (`Storage_Fridge`) com a outra mão.
   - Coloque o queijo na bancada `Station_Prep_A` e segure `F` para ralar. O queijo se transforma em `Queijo Ralado` no slot, e o ralador permanece na mão sem ser consumido!
   - Deixe a porção de queijo ralado na bancada de espera (`Station_NeutralCounter`) para um parceiro pegar.
4. **Montagem e Forno**:
   - Coloque a massa aberta na `Station_Assembly`.
   - Adicione molho e queijo ralado (ou qualquer ingrediente alimentício desejado — montagem permissiva!).
   - Pegue a pizza montada e pressione `E` no forno (`Station_Oven`).
   - Observe a barra de progresso. Ao atingir 100%, ela está assada. Se deixada tempo demais, ela queima, mas **continua embalável e entregável**.
5. **Embalagem e Staging (Endereçamento Manual)**:
   - Retire a pizza do forno com `E` e leve à `Station_Packaging`.
   - Segure `F` para embalar na caixa.
   - Leve a caixa à bancada de retirada (`Station_Staging`). Ao colocar, o seletor de endereço abrirá na tela.
   - Selecione o endereço **#4** (conforme a comanda no quadro) e confirme a etiqueta.
6. **Mochila e Mapa de Carga**:
   - Pegue a caixa rotulada e guarde-a na mochila (`BackpackController`).
   - Observe o minimapa: apenas o marcador do endereço **#4** aparecerá no mapa pessoal de quem está carregando a caixa!
7. **Entrega e Avaliação Proporcional**:
   - Vá até o ponto de entrega #4 na cidade e pressione `E`.
   - O servidor avalia a entrega: pizzas corretas pagam 100%, queimadas pagam penalidade proporcional e a caixa é consumida, encerrando o pedido e creditando o dinheiro pessoal e o caixa do restaurante!

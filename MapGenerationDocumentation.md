# Documentação de Geração Procedural do Mapa (ScaryParty)

Este documento explica detalhadamente como o sistema de geração do mapa funciona, sua arquitetura e os cálculos envolvidos na construção da cidade procedural no projeto ScaryParty.

## 1. Visão Geral (CityGenerator)

O `CityGenerator` é o maestro principal da geração do mapa. O sistema funciona usando uma abordagem **Baseada em Semente (Seed)** para garantir que o mapa seja exatamente igual para todos os jogadores na rede.

### Sincronização de Rede
- O servidor gera uma semente aleatória e a armazena em uma `NetworkVariable<int>`.
- **Clientes Atuais:** Recebem a semente através de um `ClientRpc` no momento em que o servidor inicia a geração.
- **Late-Joiners (Jogadores que entram depois):** Quando conectam e rodam o `OnNetworkSpawn`, leem diretamente a `NetworkVariable<int>` com a semente que o servidor já definiu e geram a mesma cidade localmente. Isso evita ter que enviar toda a malha da cidade pela rede.

### Ordem de Geração
A geração segue um pipeline estruturado de forma sequencial:
1. **Hierarquia:** Cria todos os GameObjects raízes (Ruas, Blocos, Interseções, etc.).
2. **Grafo de Ruas:** Usa o `StreetGridGenerator` para gerar as rotas e extrair os polígonos dos quarteirões.
3. **Quarteirões e Prédios:** Processa os polígonos com o `BlockFiller` para criar prédios que não se sobrepõem.
4. **Interseções e Vias:** Usa o `IntersectionBuilder` e o `UnifiedRoadMesher` para gerar a malha 3D das vias.
5. **Pontos de Entrega:** O `DeliveryPointPlacer` espalha locais de entrega pelo mapa.
6. **Pathfinding:** Inicializa o `CityGraphPathfinder` para a navegação das IAs (carros e inimigos).
7. **Pizzaria:** Coloca a base central dos jogadores usando um sistema de pontuação de quarteirões.
8. **Inimigos e Tráfego:** Instancia as posições de spawn de inimigos e inicializa o sistema de carros baseando-se no grafo gerado.

---

## 2. Geração do Grafo de Ruas (StreetGridGenerator)

A fundação de todo o mapa é calculada não através de "tiles" (blocos pré-prontos), mas através de um **Grafo Planar Hierárquico**. 

O sistema usa um processo similar ao de *L-Systems* (Sistemas de Lindenmayer) expandido em ramos (branches), criando Avenidas principais (Chunk borders) e ruas secundárias (Locais) que preenchem os espaços. O estilo visual tenta emular um padrão de malha fraturada ("Estilo São Paulo"), misturando grades retas que colidem em ângulos sutis.

### Passo a Passo do Cálculo das Ruas:
1. **Crescimento:** Começa de um nó central `(0, 0, 0)` e expande ramos com ângulos baseados em múltiplos de 90 graus, além de uma grade interceptante inclinada aleatoriamente.
2. **Desvios (Noise):** Adiciona leves desvios nos ângulos de retas perfeitas para dar o aspecto de uma cidade real imperfeita.
3. **Aggressive Snapping (Conexão Forçada):** Para evitar becos sem saída, sempre que uma rua local cresce e chega perto de uma Avenida, ela é forçada a "grudar" no nó da avenida mais próxima (distâncias menores que 30 a 40 unidades).
4. **Verificação de Interseção:** Um cálculo matemático rigoroso verifica se o segmento de reta criado cruza com qualquer outro segmento previamente gerado (`SegmentsIntersect`). Se cruzar, o galho é invalidado ou quebrado para não criar ruas sobrepostas que corrompem a geometria.
5. **Pruning (Poda):** Após criar o grafo bruto, o sistema busca todas as ruas sem saída (nós com apenas 1 conexão). Essas ruas são ativamente deletadas do grafo de forma recursiva até que o grafo seja composto inteiramente por **ciclos fechados**. Isso garante que é possível dar a volta em qualquer quarteirão.

### Extração das Faces (Os Quarteirões)
Para transformar as retas em lotes de terra (quarteirões), o código mapeia os polígonos fechados formados pelo grafo.
- Ele ordena as conexões de cada nó de forma horária baseada nos ângulos entre os vértices (Face Extraction em Grafo Planar).
- Extrai todos os ciclos mínimos fechados.
- Calcula a **Área Sinalizada (Signed Area)** para diferenciar o que são quarteirões da cidade (faces internas, anti-horárias) e o que é o limite externo ("void faces", horárias).

---

## 3. Preenchimento de Quarteirões e Zonas (CityGenerator e BlockFiller)

Quando o polígono base de um quarteirão é extraído, ele representa o centro das ruas, e não o lote construível em si. Construir na borda bloquearia as vias.

### Cálculo de Inset (Recuo)
1. **Miter Math:** O sistema identifica o polígono bruto e gera um recuo (Inset) interno correspondente à `(Largura da Rua / 2) + Largura da Calçada`.
2. O cálculo da normal de cada vértice ajusta o vetor "Miter" para que as quinas recuem proporcionalmente e formem a forma do quarteirão exata, paralela à calçada.
3. Polígonos de recuo que geram áreas úteis muito pequenas (por auto-interseção de quinas muito agudas) ou muito finos (razão Área vs Perímetro²) são descartados para evitar erros gráficos.

### Sistema de Zoneamento (Distância de Chebyshev)
Cada quarteirão validado recebe um tipo de "Zona" influenciando visuais, spawns e dificuldade. A cidade é dividida em "Chunks" usando a maior distância absoluta (X ou Z) a partir do centro (`Mathf.Max(Abs(X), Abs(Z))`):
- **Centro (Chunk 1x1):** Zona Comercial (Segura).
- **Bairros (Chunks 3x3):** Zona Residencial.
- **Periferia (Chunks 5x5):** Zona Industrial.
- **Bordas (+ que 5x5):** Zona dos Monstros (Perigo Máximo).

### O BlockFiller (Instanciação Otimizada)
Em vez de depender de testes de colisão da física do Unity (como Rigidbodies e BoxColliders), o preenchimento de lotes é 100% matemático em 2D.
- Tenta encaixar "caixas" no polígono de recuo criando OBBs (*Oriented Bounding Boxes*) em diferentes posições.
- Ele as testa usando o **SAT** (*Separating Axis Theorem*) contra todas as outras OBBs já colocadas na rua para evitar sobreposições.
- Só depois que o array matemático de caixas é finalizado, as casas em 3D são de fato instanciadas.

---

## 4. Pathfinding Rápido (CityGraphPathfinder)

O grafo abstrato (nós e arestas de ruas) gerado também é usado como inteligência base para IAs (carros e possivelmente monstros).

- O `CityGraphPathfinder` transforma a geometria das ruas num grafo de navegação (A*).
- Buscar trajetórias nele é consideravelmente mais performático do que usar o NavMesh tradicional em superfícies inteiras (composto de milhares de triângulos do chão), por usar apenas dezenas a centenas de *Nodes*.
- As distâncias métricas reais entre os nós servem de "peso" (Heurística) para achar o menor trajeto.
- O sistema de Tráfego cria veículos baseados em distâncias curtas desse grafo de pathfinding para otimizar carros perambulando pelo cenário.

---

## 5. Locais de Interesse Especiais

### A Pizzaria (Base)
Ao invés de spawmar obrigatoriamente no 0,0 do mundo, o algoritmo tenta colocar a pizzaria de forma orgânica, usando um sistema de *Score* (Pontuação) dos blocos:
1. Ele itera sobre todos os quarteirões da cidade e avalia dois pesos: a distância do centro (0,0) + o bônus negativo do tipo de zona (Zonas Periféricas tem peso maior).
2. O quarteirão de Menor *Score* vence (priorizando zonas centrais residenciais ou comerciais próximas ao meio).
3. O prédio instanciado de maior volume nesse quarteirão é apagado, e a estrutura da Pizzaria (Ponto de Spawn dos Players e Bancada de Pizzas) é colocada exatamente no mesmo lugar usando sua rotação para encarar a rua perfeitamente.

### Spawns de Inimigos (EnemySpawnPointGenerator)
O gerador calcula posições usando como base os dados dos blocos extraídos. Distribui os inimigos pelo mapa, muitas vezes ponderados para as zonas industriais ou "MonsterZones", onde a ameaça deve ser maior. Assim os locais periféricos de entrega ganham uma progressão de perigo implícita sem *level design* manual.

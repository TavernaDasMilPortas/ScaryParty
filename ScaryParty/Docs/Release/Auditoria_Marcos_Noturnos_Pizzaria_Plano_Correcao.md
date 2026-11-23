# ScaryParty — Auditoria da release e plano de correção

Data: 19/11/2026. **Revisão 2: trânsito físico, spawns/IA, muralhas e Feel transversal; 18 blocos e 44 testes planejados.**
Branch: **release/marcos-noturnos**.
Snapshot: **2dacefe61be6f9cf98b350530505795bbd629b53**.
Base comparada: main em a7cc598bc199f6ccf64c8ca0e103979e99326b9f.

## 1. Parecer e alcance

A implementação avançou bastante em domínio, ciclo de noites, funcionários, pedidos por unidade e replicação. Entretanto, **não considero esta release pronta para ser tratada como experiência integrada e validada**. A rodada N2 introduziu componentes desconectados do fluxo, guardas do domínio não usadas pelos RPCs, estilos dependentes do Editor e feedbacks sem configuração. O pátio é improvisado em runtime e o carregamento não cobre todas as rotas.

A prioridade é corrigir integração e invariantes, montar um pátio reservado com as peças existentes, unificar transições e só então polir e medir desempenho.

**Esta entrega é uma auditoria estática e um plano de implementação. Não altera o jogo nem reconstrói o prefab.** Nenhuma execução de Unity, build, teste EditMode/PlayMode, medição de FPS ou sessão multiplayer foi realizada nesta auditoria. Aparência, colisões em movimento e sensação das animações precisam de verificação no Editor.

### Cobertura

- Inventário de todo o Git: **14.031 arquivos**, incluindo **2.025 C#**, **313 prefabs**, **98 cenas**, **436 assets serializados** e **1.478 materiais**. Inclui terceiros e demonstrações.
- Conteúdo próprio em Assets/Piaaria: **4.748 arquivos**, incluindo **474 C#**.
- Release contra main: **427 arquivos alterados**, **144 C# alterados**, **59.013 linhas adicionadas** e **8.460 removidas**.
- Revisão dirigida de caminhos de execução, chamadas e referências de lobby, carregamento, cidade, footprint, pátio, cozinha, pedidos, funcionários, combate, moto, noites, votos, loja, replicação, UI, efeitos, testes e documentação.
- Não encontrei GUIDs duplicados nos .meta materializados. Há 54 referências de script não resolvidas somente pelo mapa de Assets; várias pertencem a pacotes. **Isso não comprova 54 Missing Scripts.** Resolver com PackageCache/AssetDatabase antes de classificar.
- Não houve inspeção visual de todos os binários, revisão linha a linha de bibliotecas de terceiros ou execução de todas as variantes. Inventariar todo o conteúdo não equivale a validar todo o seu funcionamento.
- O apêndice lista todo o conjunto alterado. As áreas sem reprodução em runtime permanecem explicitamente pendentes.

## 2. O que foi entregue na branch

| Rodada | Conteúdo identificado | Avaliação |
|---|---|---|
| M1–M8 | Navegação/trânsito, armas/moto, vida, noites, monstros, votos/loja e funcionários | Base ampla, seguida por várias correções de integração |
| R0–R8 | Mãos, bandoleira, morte, receitas múltiplas, NPCs e UI | Recuperação de integração dos primeiros marcos |
| Etapas 1–6 | Proxies cliente, reset da cozinha, produção real dos funcionários, monstros e tutorial | Preservar essas correções ao remodelar |
| P0–P7 | Manifesto, placeholders, vínculo exclusivo de pizzas, UI, presets e testes | Pátio ainda depende de offsets |
| B0–B6/B5.1 | Catálogo de balanceamento, Odin, preparação pré-N1, debug no Inspector e NoiteDeTeste | Boa direção; verificar divergências N2 |
| N2-01–N2-10 e correção de spawn | Shell de preparação, votos tipados, sorteio, contagem e layouts | Principal concentração de problemas de integração atuais |

Pontos positivos verificáveis: OrderUnitFulfillmentService usa identidade por geração/pedido/linha/unidade e vínculo exclusivo com item físico; sorteio e contagem usam timestamps de servidor; PizzeriaFootprint aceita múltiplas regiões; há serviço DOTween para VisualElements; balanceamento possui catálogo e presets. Reaproveitar essas estruturas.

## 3. Achados priorizados

Caminhos relativos à raiz Unity ScaryParty/. **P1:** corrigir antes de aprovar fluxo. **P2:** robustez, polimento ou desempenho. **P3:** manutenção. “Confirmado” significa constatado no código, não reproduzido no jogo.

### A01 — P1 — Carregador criado sem PanelSettings

**Confirmado.** Scripts/Pizzeria/Presentation/PizzeriaSceneLoadingCoordinator.cs, EnsureInstance/EnsureUIOverlay, cria GameObject e UIDocument e define sortingOrder, mas não atribui panelSettings. Não encontrei instância serializada desse script nos assets materializados que resolva a dependência. O input pode ser bloqueado sem apresentação válida.

**Correção:** bootstrap persistente com PanelSettings, UXML e USS explícitos; validar painel antes da operação. Ordenar também o PanelSettings, não apenas o UIDocument.

### A02 — P1 — Trocas de cena contornam o carregador

**Confirmado.** Há LoadScene direto em LobbyManager, LobbyPresenter, ReadySceneController, MainMenuPresenter, ReadyRoomPresenter, PauseEscapeMenuPresenter, NightResultsPresenter e no retorno de erro do coordenador. Listener de rede filtra cenas de gameplay. LobbyManager também usa fallback local após falha do carregamento NGO.

**Impacto:** Ready, retorno ao menu e erros não compartilham apresentação/gates; fallback local pode deixar a sessão com cenas divergentes.

**Correção:** uma fachada para transições locais, rede e retorno. Servidor inicia troca sincronizada; falha NGO não deve virar mudança de cena apenas local mantendo sessão ativa.

### A03 — P1 — Primeiro frame e prontidão não garantidos

**Confirmado.** BeginNetworkedTransition inicia coroutine de fade e chama LoadScene de rede imediatamente, sem esperar frame apresentado. RoutineTransition espera jogador até 6 segundos, depois pode concluir sem ele. Timeout permite liberar partida se IsPizzeriaReady retornar true. No cliente, essa checagem exige root/catálogo, sem comprovar snapshot aplicado. NavMesh ausente é ignorado. Não há gate explícito de iluminação/câmera.

**Correção:** overlay e bloqueio no clique; primeiro frame antes de trabalho pesado; requisitos por destino. Timeout deve gerar erro recuperável, nunca 100% com jogador/mundo incompletos.

### A04 — P1 — RPC de “Pronto” ignora validação nova

**Confirmado.** Scripts/Pizzeria/Network/PizzeriaCommandHandler.cs:1481, SetRunReadyServerRpc chama SetPlayerReady diretamente. TrySetPlayerReady, que verifica votos individuais obrigatórios e lock, não é chamado pela rota.

**Impacto:** rota antiga/cliente pode marcar pronto sem dois votos ou cancelar durante revelação. AreAllMandatoryStagesVoted é agregada por etapa, não exige voto de todos.

**Correção:** todos os comandos usam a guarda do domínio, com rejeição explicada. Testes devem passar pelo handler, não somente invocar o método correto do domínio.

### A05 — P1 — Entrada tardia pode bloquear início após contagem

**Risco identificado no fluxo.** TickServerIntermissionLifecycle reseta a intermissão antes de ResolveAndStartPreparedNightIfReadyServer. Este consulta todos os clientes conectados e retorna se algum não estiver pronto. Quem entrou durante o lock pode bloquear a noite depois que estado/timestamps foram resetados.

**Correção:** congelar participantes no lock; definir entrada tardia como espectador ou próxima preparação; iniciar noite atomicamente e resetar somente após sucesso. Reproduzir com entrada, saída e reconexão no fim da contagem.

### A06 — P1 — Loja permite compra durante revelação

**Confirmado.** ExecuteShopOfferPurchaseServer e RunProgressionService.TryEvaluateShopOffer validam fase/oferta/saldo, mas não IsIntermissionLocked. A fase continua PreparingNextNight durante sorteio/contagem.

**Correção:** guarda autoritativa e DTO de bloqueio. RPC atrasado recebe rejeição sem débito. Preservar compras no turno quando a configuração da oferta permitir.

### A07 — P1 — Pendência de compra não bloqueia segundo clique

**Confirmado.** UI/Piaria/Scripts/Runtime/Presenters/RunShopPagePresenter.cs:89–117 captura canBuy no callback e apenas grava _pendingOfferId. Não desabilita/reconstrói o botão imediatamente. Outro clique ainda envia RPC. Shell limpa pendência em qualquer revisão geral, inclusive voto de outro jogador. OnIntermissionActionResultReceived não tem consumidor encontrado; compra usa outra rota de notificações.

**Correção:** request ID por operação, bloqueio imediato, resposta correlacionada e idempotência no servidor. Timeout visual não reenvia compra automaticamente.

### A08 — P1 — Estilos da intermissão dependem do Editor

**Confirmado.** NightIntermissionPresenter.EnsureBuilt carrega NightIntermission.uss e NightModifierCards.uss dentro de UNITY_EDITOR. NightIntermissionScreen.uxml não referencia esses estilos. O catálogo contém os templates: o problema é o caminho de estilos no Player, não ausência geral de UXML.

**Correção:** referências explícitas no UXML ou catálogo runtime. Validar páginas sem fallback AssetDatabase, fontes, resolução e cores.

### A09 — P1 — Novo painel TAB não está conectado

**Confirmado nas buscas de código próprio.** OperationsStatusPresenter só aparece em declaração/construtor, sem instanciação. ScoreboardPresenter ainda usa overlay antigo; recebeu guarda de modal, não integração do painel novo.

**Correção:** um controlador de TAB instancia OperationsStatusOverlay.uxml, aplica USS e chama Bind. O presenter novo também consulta ConnectedClientsList; substituir por roster replicado apropriado ao cliente antes de instalar.

### A10 — P1 — Ponte nova do Feel nasce vazia

**Confirmado no caminho automático.** NightIntermissionFeelBridge.EnsureInstance cria componente com _bindings vazio; não encontrei referências serializadas do script nos assets materializados. PlayInternal só toca efeito/áudio se houver binding. IDs de abertura, troca de aba e compra bem-sucedida não estão todos conectados.

**Correção:** prefab/catálogo de bindings MMF_Player e áudio reais. Validar três coisas: evento definido, evento emitido e efeito associado. ReduceMotion/intensidade também precisam valer no Feel.

### A11 — P1 — Pátio sem reserva geométrica

**Confirmado.** Scripts/Pizzeria/Composition/PizzeriaParkingLotBuilder.cs:16–84 posiciona com 6,8 m lateral e 0,4 m frontal; testa pontos com CheckSphere; se ambos os lados estiverem bloqueados, escolhe direita mesmo assim. Raycast aceita qualquer colisor. Rack/alvo usam offsets.

PizzeriaPlacementSolver reserva regiões estruturais; o prefab manual tem accessCorridors vazio. A área operacional externa não faz parte do contrato de implantação. Queries físicas locais podem divergir conforme ordem de criação em cada máquina.

**Correção:** pátio autorado, regiões externas reservadas, âncoras e layout determinístico. Sem fallback silencioso para rua/teto/parede. O piso atual remove seu collider e depende do chão existente: validar apoio real.

### A12 — P1 — Interior tratado como caixa, não planta em L

**Confirmado.** PizzeriaRoot.GetPizzeriaInteriorBounds/IsWorldPositionInsidePizzeria, aproximadamente linhas 949–973, usa BoxCollider ou bounds aproximados. Caixa pode incluir vazio de L/T.

**Correção:** regiões semânticas locais de interior, pátio, recebimento, treino e rua. Bounds apenas para rejeição rápida. Rever consumidores de “dentro da pizzaria”, especialmente hostilidade e regras de entrega/recuperação.

### A13 — P2 — Limpeza de tween não percorre filhos

**Confirmado.** UIToolkitTweenService.KillTweens cancela somente elemento exato. Shell usa _root; páginas chamam Clear em containers. Cards/counters são descendentes registrados individualmente.

**Correção:** escopo por página ou cancelamento recursivo antes de remover árvore; Dispose de presenters; limpeza em Disable/Destroy. Teste atual cobre só um elemento. Hover também não deve interromper entrada deixando opacidade/posição incompleta.

### A14 — P2 — Estados e mensagens de UI incompletos

**Confirmado.** Loja mostra “MÁX.” para causas diferentes de indisponibilidade; resumo exibe IDs de votos; pré-N1 mostra “NOITE 0 CONCLUÍDA”; abas permanecem navegáveis durante revelação; presenter do sorteio não apresenta conclusão explícita no zero.

**Correção:** razões específicas, nomes/ícones, “Preparar primeira noite”, abas travadas no lock e final sincronizado. Feedback local confirma intenção; sucesso deve acompanhar servidor.

### A15 — P2 — Schedule anterior ignorado por N2

**Confirmado.** PizzeriaRoot.OpenPreparationBeforeNightServer:549 chama OpenTypedBallots(resolvedTarget, 3, 3). Este cria schedule de um estágio por categoria, sem evento adicional.

Pode ser decisão intencional da 2.1, não necessariamente bug. Mas diverge de controles da fase B. **Correção:** declarar contrato vigente, parametrizar o que permanece configurável e ocultar/depreciar campos sem consumidor. Inspector não pode prometer controle ignorado pelo runtime.

### A16 — P1/P2 — Trabalho síncrono pode congelar loading

**Confirmado na implementação; impacto exige medição.** NavigationWorldManager.BuildCityNavMesh:75 chama BuildNavMesh síncrono e coleta colliders globalmente. Geração urbana também tem etapas grandes síncronas.

**Correção:** primeiro frame do overlay, construção em lotes com orçamento por frame e atualização assíncrona de NavMesh onde suportada. Coroutine não torna método bloqueante assíncrono. Não usar Task.Run para APIs Unity de cena.

### A17 — P2 — Moto limitada por pacote, não por tempo

**Confirmado.** UpdateMotorcyclePoseServerRpc:3420 limita a 3,5 m por mensagem. Frequência de RPC altera deslocamento aceito. Verifica posição/velocidade finitas, mas não componentes do quaternion.

**Correção:** limite por delta de servidor, velocidade/aceleração, validação completa e correção suave. Hoje a arquitetura aceita pose do cliente com validação; não descrever isso como simulação totalmente autoritativa de input.

### A18 — P2 — Origem de tiro sem limite explícito de distância

**Confirmado.** FireWeaponServerRpc valida arma/cooldown e Linecast entre peito/origem, mas não limita explicitamente distância origem–jogador nem valida todos os componentes finitos antes de consumir munição.

**Correção:** origem derivada/restrita à pose validada, direção finita, rejeição antes do débito. Preservar posse/cooldown existentes e testar parede próxima, latência e pistola na moto.

### A19 — P2 — Suprimento parcial cobra valor integral

**Confirmado.** TryPurchaseCatalogOffer, SupplyBundle, cobra preço total quando added > 0. Rejeita estoque totalmente cheio, mas não garante pacote completo.

**Correção proposta:** pré-validar capacidade e aplicar transação integral. Se parcial for intencional, informar quantidades/preço. Testar capacidade faltando para um ingrediente.

### A20 — P2 — Pátio recriado e materiais temporários

**Confirmado.** EnsureBuilt destrói/recria container; Destroy diferido pode manter física antiga durante nova escolha de lado. TintObject cria Material ou acessa renderer.material sem estratégia explícita de descarte.

**Correção:** instalação autorada idempotente, atualização seletiva, identidade estável da moto, materiais compartilhados e MaterialPropertyBlock. Quando reconstruir, separar teardown de queries.

### A21 — P2 — Hotspots candidatos e monólitos

**Observação, não diagnóstico de FPS.** Handler: 3.930 linhas; EmployeeAgentController: 3.065. Há buscas globais, coleções temporárias e cálculo de caminhos nos fluxos de funcionários; tiro usa RaycastAll, sort e dicionários por disparo; projeção de rede monta coleções.

**Correção:** medir CPU/GC com cidade pequena/grande e 1/2/4 jogadores, registrar estações/cachear referências, reutilizar buffers e limitar frequência de decisões. Refatoração gradual sem alterar contrato NGO inadvertidamente.

### A22 — P2/P3 — Evidências e documentos desatualizados

**Confirmado.** CURRENT_STATE resume B; STATUS destaca P/F8, apesar de B5.1 e N2 posteriores. RunIntermissionNetworkTests testa igualdade de DTOs e um tween, não sessão host/cliente. Relatórios históricos deixam multiplayer real pendente.

**Correção:** evidência por SHA/cenário/ambiente; atualizar STATUS/CURRENT_STATE/CHANGELOG. Não reutilizar “96/96” ou “102/102” antigo como aprovação do HEAD. Rever também SampleScene/PiariaUIDemoScene habilitadas no BuildSettings antes de distribuição.

## 4. Avaliação transversal

| Sistema | Preservar | Validar na revisão |
|---|---|---|
| Cozinha/mãos | Transferência, posse, leases e slots | Duas mãos, F, câmera destravada, referências após remodelagem |
| Pedidos/entrega | GenerationId e vínculo exclusivo por unidade | Três pizzas, receitas distintas, queima/destruição/endereço errado, pagamento único |
| Funcionários | Operações reais, ferramentas e takeover | Cozinha–expedição, posto ocupado, estoque vazio, tarefa interrompida |
| Vida/combate | Estados, resgate, bandoleira | Reset/retry, todos mortos, mochila danificada, tiro inválido |
| Monstros/clientes | Hostilidade por espécie e proxies | Interior/pátio, conversão zumbi, atendimento interrompido, late join |
| Cidade/trânsito | Seed e reserva geométrica | Pátio contido, saída da moto, pedestres, cruzamentos e entregas |
| Noites/RUN | Resolução de parâmetros e snapshot | Vitória/derrota/retry/nova RUN sem vazar compras/votos/modificadores |
| Balanceamento | Catálogo central/Odin | Consumidor real de cada efeito, schedule N2 e suprimento parcial |
| UI | UI Toolkit e tween service | Runtime sem Editor, cursor/foco, overlay, pending/erro e resoluções |
| Visual/iluminação | Perfis existentes e pivôs cosméticos | Pátio no estilo atual, exposição e iluminação pronta antes do reveal |
| Cutscene | Fluxo aditivo e bindings | Câmera/input/overlay na entrada/saída; preservar cena do jogo |
| Ferramentas | Debug no Inspector | Preview, validação e propriedades efetivas; NoiteDeTeste continua sem limite de expediente |

## 5. Arquitetura modular: L com pátio e alternativa T

### 5.1 Recomendação

Começar por **L com pátio operacional no recuo**, e gerar uma alternativa em T para comparação no Editor. A meta é circulação clara; o formato é consequência. Preservar cozinha/salão inicialmente reduz risco de quebrar slots e referências.

O prefab registra:

| Região | X local | Z local |
|---|---|---|
| Salão | 0 a 32,8 m | −13,2 a 9,2 m |
| Cozinha | 16,4 a 32,8 m | −27,6 a −13,2 m |
| Recuo candidato | 0 a 16,4 m | −27,6 a −13,2 m |

Piso: Y = −1,6 m local. Recuo candidato: aproximadamente 236 m² pelo envelope. **São metadados existentes, não inspeção de espaço livre nas meshes.** Conferir beirais, objetos, terreno e conexão à rua.

| Alternativa | Vantagem | Condição |
|---|---|---|
| L + recuo operacional | Preserva relações internas e aproveita vazio | Reservar recuo no gerador e criar piso/acesso |
| T + pátio lateral | Pode separar salão/cozinha/expedição melhor | Remontar paredes/cobertura, footprint, rotas e slots afetados |

### 5.2 Setores

| Setor | Conteúdo | Fluxo |
|---|---|---|
| Atendimento | Entrada, fila e salão | Independente da saída das motos |
| Expedição | Pizzas prontas, mochilas, organização de entregas | Cozinha → embalagem → expedição → moto |
| Garagem | Moto inicial e vagas de expansão | Saída para rua; montagem/desmontagem/recuperação livres |
| Recebimento | Fornecedores e suprimentos | Separado da fila e da manobra |
| Treino | Rack, atirador e boneco | Divisórias/anteparo; não apontar sobre circulação |
| Conexão urbana | Piso, soleira/rampa, passeio | Sem invasão de rua, buraco ou degrau impeditivo |

“Pontos de entrega” é interpretado aqui como **expedição/retirada e recebimento na base**. Endereços finais de clientes continuam espalhados na cidade; não registrar bancada de expedição como destino residencial.

Medidas iniciais de projeto, a validar com colisores reais: vaga 1,6 × 3 m; passagem pedestre 1,5–2 m; corredor de serviço 2 m; manobra 3–4 m; treino 3 × 5 m. Não são dimensões medidas dos módulos.

### 5.3 Peças e montagem

O prefab possui 1.099 GameObjects nomeados. Famílias encontradas incluem paredecomum (72), paredesuperiorexterna (68), paredesuperiorinterna (19), paredejanelameio, paredejanelaesq/dir, paredeportapequena, paredeporta2variante, paredefachadaesq/dir e grupos Pisos/PisosGerados.

Criar catálogo das peças reais a partir de MeshFilter/Renderer, pivôs, bounds, materiais, filhos e colisores. Grupo não equivale a módulo independente. Preservar conjuntos de porta e partes animadas. Usar paredes como divisórias/anteparo e pisos existentes; cobertura somente com peças comprovadamente adequadas. Não esticar paredes arbitrariamente. Placeholders somente para lacunas identificadas.

### 5.4 Contrato de implementação

- PizzeriaLayoutProfile: variante L/T, grade, módulos, regiões e âncoras. Odin com Layout, Peças, Pátio, Spawns, Navegação e Validação.
- PizzeriaExteriorAnchors: MotorcycleSpawn_01, MotorcycleRecovery, DismountLeft/Right, Dispatch, SupplierReceiving, WeaponRack, PracticeTarget, PracticeShooter, ExteriorGround e StreetExit.
- Separar regiões construídas, externas reservadas, corredores, cobertura e regiões de gameplay. Pátio não vira interior só para reservar terreno.
- Estender solver/BlockFiller para consumir reserva externa e corredores; preencher campos sem consumidor não basta.
- Layout/pose determinísticos, com perfil/versão conhecidos na rede. Cliente não decide lado por Physics.CheckSphere.
- Builder no Editor: Preview, Aplicar, Validar, Reverter, Undo. PrefabUtility/SerializedObject; preservar GUIDs, station IDs, slots e overrides. Não editar YAML manualmente em massa.
- Runtime resolve âncoras autoradas e instancia entidades dinâmicas, sem reconstruir instalação válida.
- Piso com collider e navegação; decoração não deve obstruir. Testar ligação pátio–cozinha–rua e saída da moto.
- Materiais do pipeline visual existente, sem URP/Lit genérico por primitiva. Mesma funcionalidade em variante canônica e cel-shaded.

**Múltiplas motos:** hoje existem DeliveryMotorcycleController.Instance e MotorcycleState singular. Quatro vagas não implementam quatro veículos. Escopo mínimo: uma moto funcional e âncoras de expansão. Várias motos exigem IDs, lista replicada, posse, seleção, impacto e recuperação por veículo em bloco separado.

## 6. Carregamento fluido em UI Toolkit

### 6.1 Comportamento

1. Clique de navegação/conexão abre overlay sobre a cena atual e impede repetição imediatamente.
2. Apresentar pelo menos um frame antes de iniciar trabalho pesado; animação curta, sem atraso artificial longo.
3. Overlay persistente cobre a cena de destino enquanto ela se monta.
4. Porcentagem numérica e etapa legível; suavizar progresso observado, sem cronômetro fingindo conclusão.
5. Só 100% após requisitos do destino; câmera/HUD renderizados; fade-out e liberação de input uma vez.
6. Erro/desconexão permanece visível com retorno seguro. Não “Fechar” para liberar gameplay incompleto.

### 6.2 Estrutura

Evoluir PizzeriaSceneLoadingCoordinator, evitando carregador concorrente. Separar SceneTransitionService, LoadingOverlayPresenter, SceneReadinessProfile e fontes de progresso. Transition ID ignora callbacks antigos; apenas uma operação ativa.

LoadingOverlay.uxml/.uss e LoadingPanelSettings explícitos no bootstrap persistente. Painel screen-space acima dos demais; testar contra intermissão, pausa, erros e cutscene. Ordenação entre painéis e entre documentos são controles distintos.

| Destino | Prontidão |
|---|---|
| Menu | UI, câmera e input |
| Ready | Sessão, roster e UI |
| Gameplay | Cidade/pátio, navegação, domínio ou snapshot cliente, jogador posicionado, câmera, iluminação |
| Cutscene | Cena aditiva, bindings/câmera e retorno preservado |

Não esperar PizzeriaRoot no Lobby. Em rede, overlay começa na intenção local e em evento de cena nos outros participantes; servidor inicia troca sincronizada. Cliente atrasado/desconectado segue política explícita, separando prontidão local de equipe inteira.

### 6.3 Progresso

Pesos iniciais configuráveis: conexão 5%; cena/recursos 35%; cidade/pátio 30%; navegação 15%; replicação/jogador 10%; câmera/iluminação/primeiro frame 5%. Representam progresso composto, não estimativa de tempo restante.

Fases publicam concluído/total quando disponível. Sem progresso intermediário real, usar indicador indeterminado e concluir por evento. Na carga local com ativação retida, normalizar a subfase por progress/0.9, mantendo ativação e prontidão separadas. Não controlar allowSceneActivation indiscriminadamente em operações gerenciadas pelo NGO.

Valor exibido monotônico e nunca acima do progresso confirmado. Nova transição zera estado anterior. Timeout registra etapa, não força 100%. sceneLoaded sozinho não comprova primeiro frame pronto.

### 6.4 Animação

Visual digital/cartoon do jogo, sem estética de caderno. Proposta: pizza girando suavemente ou moto com rodas animadas, vapor discreto, barra fluida, transição de etapa por crossfade e saída curta. Porcentagem legível.

DOTween em tempo não escalado: entrada 0,15–0,25 s e saída 0,2–0,35 s; não criar tween por frame. Feel para clique aceito, etapa relevante, conclusão e erro, com volume/concorrência limitados. ReduceMotion troca loops/overshoot por opacidade.

Textos são de jogador, sem enums como CONNECTINGSESSION. Dividir geração/instanciação em lotes e estudar atualização assíncrona de NavMesh; spinner não compensa main thread congelada.

## 7. Execução — blocos originais C01–C12 e ampliação C13–C18

| Bloco | Entrega | Aceite |
|---|---|---|
| C01 | Baseline, reprodução e relatório por SHA | Estado local preservado; achados revalidados |
| C02 | Guardas, lock, participantes congelados, start atômico | RPC inválido rejeitado; late join não trava |
| C03 | Compra correlacionada e suprimento transacional | Clique duplo não compra duas vezes |
| C04 | USS runtime, TAB, mensagens e Feel configurado | Páginas completas sem depender do Editor |
| C05 | Lifecycle e tween scopes | Sem filhos animando após fechamento |
| C06 | Catálogo modular e preview L/T | Peças/pivôs medidos; preview reversível |
| C07 | Reserva do pátio e regiões semânticas | Sem invadir rua/vizinhos; pátio não é interior |
| C08 | Montagem, âncoras, navegação e materiais | Todos os postos alcançáveis nas variantes visuais |
| C09 | Loading persistente e rotas unificadas | Overlay no clique e sobre destino |
| C10 | Readiness, progresso e geração repartida | Sem 100% prematuro; animação durante carga |
| C11 | Moto/tiro e hotspots medidos | Movimento por tempo; entradas inválidas rejeitadas |
| C12 | Regressão intermediária do escopo original | Evidências por cenário/SHA; aprovação final em C18 |

Um commit por bloco quando coerente; subdividir os grandes. Não usar a contagem para justificar commits gigantes. Não publicar/mesclar automaticamente.

## 8. Matriz de testes no Editor

Usar Unity Editor já aberto. Respeitar UNITY_EXECUTION=MANUAL_ONLY. Não iniciar Unity/headless/build por conta própria. Verificar git status antes de editar; preservar GUIDs e mudanças locais.

| Teste | Procedimento | Esperado |
|---|---|---|
| T01 | Lobby → criar/entrar → Ready → jogo | Overlay em todas as ações |
| T02 | Pausa/derrota/erro → menu | Sessão limpa, cursor/foco restaurados |
| T03 | Repetir clique de início | Uma transição e um gate |
| T04 | Conexão/destino inválido | Erro recuperável, sem 100% falso |
| T05 | Atrasar snapshot/jogador/NavMesh/câmera | Overlay aguarda requisito real |
| T06 | timeScale zero e ReduceMotion | Loading legível e opção respeitada |
| T07 | Pré-N1 e entre N1/N2/N3 | Meta/título/votos corretos |
| T08 | Pronto sem votos e cancelar após lock | Servidor rejeita com motivo |
| T09 | Compra simultânea e clique duplo | Débito/estoque/pendência consistentes |
| T10 | Compra durante revelação/contagem | Sem mutação |
| T11 | Entrar/sair/reconectar no lock/zero | Noite começa uma vez |
| T12 | TAB no host e cliente | Quatro blocos, equipe correta |
| T13 | Trocar/fechar páginas 30 vezes | Tweens e locks voltam ao baseline |
| T14 | UI sem fallback AssetDatabase | Estilos/assets completos |
| T15 | Seeds pequenas/grandes, rotações | Pátio contido e acessível |
| T16 | Montar/desmontar/recuperar moto | Sem atravessar parede ou bloquear entrada |
| T17 | Rack/alvo com circulação | Treino separado e alcançável |
| T18 | Três pizzas e múltiplas receitas | Produção real e pagamento único |
| T19 | Takeover/estoque vazio/posto ocupado | Reserva liberada e tarefa replanejada |
| T20 | Morte/resgate/todos mortos/retry/nova RUN | Estados e cozinha restaurados |
| T21 | NoiteDeTeste e preset normal | Só expediente ilimitado; debug no Inspector |
| T22 | Pistola/escopeta/bandoleira/moto/mochila | Mãos, munição e dano consistentes |
| T23 | Host + cliente real; 1/2/4 jogadores | Mesmo estado/layout; sem acesso indevido a estado servidor |
| T24 | 720p/1080p/ultrawide; mouse/teclado/controle | Layout e foco funcionais |
| T25 | CPU/GC/fases em cidade pequena/grande | Comparativo medido |
| T26 | Canônica/cel-shaded/cutscene aditiva | Materiais, câmera e overlay restaurados |

Registrar SHA, seed, preset, cena, participantes, resultado e log/captura pertinentes. Teste não executado permanece pendente. Os testes antigos do repositório não foram reexecutados aqui.

## 9. Definição de concluído

- P1 corrigidos e comprovados; componente essencial instalado, não só presente no código.
- Pátio autorado/reservado/acessível; arquitetura escolhida após comparação L/T.
- Overlay é primeira resposta visual e fica sobre destino até prontidão real.
- UI Toolkit, Feel e DOTween configurados; acessibilidade e limpeza funcionando.
- Multiplayer real e regressão completos com evidência por SHA.
- Documentação, presets e manifests coerentes com o estado entregue.

## 10. Referências

- [Snapshot auditado](https://github.com/TavernaDasMilPortas/ScaryParty/tree/2dacefe61be6f9cf98b350530505795bbd629b53).
- [Unity 6000.3 — PanelSettings](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/uielements/panelsettings).
- [Unity 6000.3 — UIDocument.sortingOrder](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/uielements/uidocument/sortingorder).
- [Unity — AsyncOperation.allowSceneActivation](https://docs.unity3d.com/jp/current/ScriptReference/AsyncOperation-allowSceneActivation.html).
- [Unity — NavMeshBuilder.UpdateNavMeshDataAsync](https://docs.unity3d.com/ja/current/ScriptReference/AI.NavMeshBuilder.UpdateNavMeshDataAsync.html).

## 11. Prompt para implementação no Antigravity

~~~text
Leia este documento e os AGENTS.md aplicáveis. Trabalhe em uma branch de correção derivada da release/marcos-noturnos atual. Compare o HEAD com o snapshot auditado e revalide achados já modificados. Não trate testes históricos como prova do estado atual.

Implemente C01–C18 em ordem de dependência, incluindo integralmente a seção 12. C12 é validação intermediária; a aprovação final ocorre em C18. Priorize P1 de intermissão, loja, estilos e loading. UI deve usar UI Toolkit, Feel com bindings reais e DOTween para VisualElements. Preserve o debug no Inspector Odin e NoiteDeTeste.

Monte o pátio com peças reais do prefab modular, começando pelo preview L com recuo operacional e alternativa T. Preserve GUIDs, IDs de estações, slots e overrides. Reserve exterior/corredores e instale âncoras de moto, recuperação, expedição, recebimento, rack e alvo. Não use offsets mágicos nem escolha independente por física no cliente. Uma moto funcional é escopo mínimo; várias exigem IDs e replicação próprios.

Centralize transições: overlay no clique sobre cena atual, primeiro frame antes da carga, porcentagem baseada em fases reais, animação em tempo não escalado e saída só com prontidão da cena nova. Reparta trabalho pesado; não simule progresso por cronômetro.

Implemente trânsito exclusivamente nas avenidas, com corpo sólido, física e atropelamento que desloque a vítima. Configure âncoras seguras de spawn de jogadores e contratados. Melhore a IA, mãos, animações e fallbacks visuais dos funcionários para ações legíveis como as dos jogadores. Investigue no Git a regressão das muralhas e garanta cobertura visual e física integral, inclusive arestas curtas e quinas. Amplie Feel por cozinha, pedidos, personagens, veículos, combate, mundo e UI, respeitando autoridade, pivôs cosméticos, acessibilidade e desempenho. Execute a matriz T01–T44 nos limites autorizados.

Faça alterações revisáveis e atualize CURRENT_STATE, STATUS e CHANGELOG. Respeite UNITY_EXECUTION=MANUAL_ONLY: não iniciar Unity/headless/testes/builds automaticamente. Faça checks estáticos adequados e prepare validação no Editor. Marque testes não executados como pendentes. Não declare prefab montado ou multiplayer validado sem evidência. Não publique/mescle automaticamente.
~~~




## 12. Ampliação obrigatória — trânsito, spawns, contratados, muralhas e Feel

Revisão de escopo em 19/11/2026. Estes requisitos passam a fazer parte da entrega, dos critérios de aceite e do prompt de execução. Preservar A01–A22 e C01–C12. A presente revisão usa o mesmo snapshot de código da auditoria; não afirma que houve nova execução ou análise do HEAD remoto.

### 12.1 Carros: colisão física, avenida e atropelamento

**Evidência adicional no snapshot:** TrafficManager.SpawnVehicle configura o colisor como trigger e adiciona Rigidbody cinemático; TrafficVehicle movimenta transform.position/rotation diretamente. OnTriggerEnter aplica dano a IDamageable e manda pedestres reagirem à buzina. Isso não equivale ao comportamento solicitado de corpo sólido com deslocamento físico de atropelamento.

**Resultado obrigatório:** carros circulam exclusivamente na área viária autorizada das avenidas; não cortam calçadas, quarteirões, pátio ou interior de prédios. Atropelamento produz recuo corporal perceptível, proporcional ao impacto, além das regras de dano.

1. **Fonte única de área dirigível.** Usar a geometria/grafo que gera a avenida para produzir faixas e corredores de tráfego. Separar explicitamente pista, calçada, cruzamento e lote. O centro do carro sobre uma linha não basta: validar a largura, comprimento e volume varrido do veículo, especialmente em curvas e cruzamentos. Se houver ruas secundárias não autorizadas, excluí-las pelo tipo de via.
2. **Trajetórias de curva.** Entradas/saídas por faixa, curvas com espaço para a carroceria, sentido correto, pontos de parada e semáforos. Proibir conexão reta entre waypoints que corte uma esquina. Validar conexão completa antes de autorizar rota; se impossível, parar/replanejar, nunca atravessar calçada para recuperar caminho.
3. **Corpo físico real.** Colisor sólido não trigger para carroceria; sensores de antecipação em triggers separados. Definir massa, centro de massa, atrito, limites de inclinação e detecção de colisão adequados às velocidades, com parâmetros no Inspector.
4. **Um único dono do movimento.** Simulação do carro no servidor, em passo físico, com Rigidbody dinâmico e controle de direção/velocidade por forças ou torque limitado. O controlador não pode escrever Transform a cada frame nem sobrepor a resolução física para perseguir um waypoint. Se o projeto exigir alternativa cinemática, justificar e validar explicitamente as colisões e respostas; somente trocar isTrigger não satisfaz este bloco.
5. **Proteção de borda.** Antecipar colisão com varredura de volume e frear antes de invadir calçada. Restringir percurso à área viária; corrigir erro de rota com freada/replanejamento. Recuperação de carro irrecuperável deve ocorrer sem teleporte visível, sem ocupantes afetados e sem criar nova colisão.
6. **Atropelamento autoritativo.** Calcular impacto pela velocidade relativa e direção do contato, com limiar mínimo, teto de impulso e regra de dano. Um episódio de impacto por veículo/vítima, impedindo múltiplos danos pelos vários colisores ou pelo mesmo contato contínuo. Nova colisão real após separação pode gerar outro episódio.
7. **Recuo da vítima.** Empurrar para longe do contato/no sentido transferido pelo carro, de forma que o personagem recue em relação à batida, e não sempre no eixo traseiro global. Controlador de personagem, Rigidbody ou NavMeshAgent precisam de adaptadores próprios de deslocamento externo. Suspender temporariamente locomoção da IA quando necessário e depois reconciliar posição/piso/caminho. Evitar disputa entre física, animação e navegação.
8. **Proteção geométrica.** Movimento de recuo considera paredes, chão e outros obstáculos; não atravessa muralha, afunda ou lança ao infinito. Se houver queda/atordoamento, duração e recuperação explícitas. Jogador e NPC devem receber a mesma política básica de impacto, respeitando tutorial e regras de vida.
9. **Rede.** Replicar estado e evento de impacto com IDs estáveis; clientes interpolam apresentação e não recalculam dano. Não simular carros dinâmicos independentes em cada cliente. Recuo deve ser integrado à autoridade real do player, sem o próximo pacote do dono desfazer o impacto.
10. **Feel.** Antecipação por motor/freada, inclinação cosmética, som de pneu/contato, poeira ou partículas no ponto correto, reação corporal e câmera local moderada para vítima. Sem hitstop global que pause relógio/servidor. Colisores e trajetória física nunca recebem squash/overshoot decorativo.

**Arquivos de entrada:** Scripts/City/TrafficManager.cs, TrafficVehicle.cs, CityGraphPathfinder.cs, IntersectionTrafficController.cs, UnifiedRoadMesher.cs; PlayerHealth, controlador de movimento e CombatContracts. Reutilizar DamageEvent e eventos existentes; não criar sistema de dano paralelo.

**Odin/validação:** gizmos de faixa, volume do carro, curva, sensor e área proibida; perfil por veículo e limites globais; histórico curto de motivo de parada no Inspector. Preservar GameView sem painel de debug obrigatório.

### 12.2 Spawns configurados para jogadores e contratados

**Evidência adicional:** EmployeeAgentController.SpawnEmployeeBody usa offsets por função e EmployeeId somados à posição da raiz. O offset não acompanha automaticamente rotação/layout e não representa ponto autorado. Há amostragem de piso/NavMesh, mas não um conjunto explícito de vagas com reserva e caminho garantido.

**Resultado obrigatório:** jogador aparece em local previsível e seguro; contratado chega por um ponto coerente da pizzaria, entra e vai ao posto. O fluxo deve funcionar nos layouts L/T e nas variantes visuais.

- Autorizar no prefab PlayerSpawn_01…04, PlayerRecovery, EmployeeArrival, EmployeeSpawn_Cashier, EmployeeSpawn_Cook, EmployeeWaiting e EmployeeRecovery. Referências explícitas no perfil, IDs estáveis e orientação definida.
- Usar TransformPoint/orientação local do layout, em vez de somar offsets globais. Resolver todas as âncoras depois da implantação final da pizzaria.
- Validar chão, cápsula livre, altura, margem de paredes, fora da avenida, sem coincidir com moto/rack/filas e com caminho até o posto. Amostrar NavMesh não basta se o ponto ficar em ilha desconectada.
- Reservar vaga durante spawn e liberar ao sair. Spawns simultâneos usam fila e pontos diferentes; sem empilhar quatro pessoas na mesma posição.
- Aguardar cidade, instalação da pizzaria, piso e navegação prontos. Só instanciar corpo uma vez por EmployeeId/geração.
- Fallback ordenado: âncora preferida → alternativa da mesma função → espera segura com caminho válido → adiar spawn e explicar impedimento. Não recorrer à origem do mundo nem ao ponto mais próximo em cima de bancada/telhado.
- Contratação pode registrar o funcionário como “A caminho”; se chegada estiver bloqueada, informar e tentar novamente de modo limitado. Não debitar novamente a cada tentativa nem afirmar que ele está trabalhando antes de chegar.
- Nova RUN, retry, reconexão e troca de layout limpam reservas antigas e não duplicam corpos. Recuperação respeita estado do funcionário.
- Wizard Odin: visualizar cápsula/orientação, testar caminho, simular ocupação e validar todas as vagas. Aplicação com Undo e referências salvas no prefab.

### 12.3 IA dos contratados com comportamento e apresentação de jogador

**Meta:** balconista/pizzaiolo precisam parecer personagens realizando ações compreensíveis: andar, escolher posto, virar para objeto, alcançar, pegar, carregar, preparar, entregar e reagir. Melhorar a IA atual, preservando produção real, posse, reservas e takeover humano.

**Planejamento e execução:**

- Separar decisão de tarefa, deslocamento, execução da interação e apresentação. Usar estados explícitos: Aguardando, EscolhendoTarefa, IndoAoPosto, Alinhando, Pegando, Carregando, Trabalhando, Entregando, Bloqueado, Replanejando e Recuperando.
- Priorizar tarefas por urgência/prazo, distância e dependências. Completar uma etapa coerente antes de mudar de intenção; usar histerese para não alternar a cada frame entre postos.
- Compartilhar comandos de domínio com ações equivalentes dos jogadores. Não gerar ingrediente/pizza por atalho invisível; não concluir processo antes de chegar ao alcance válido e satisfazer ferramenta/insumo.
- Reservar item, posto e vaga de interação; renovar lease enquanto executa; liberar em cancelamento, intervenção humana, destruição de item, morte, nova RUN e timeout.
- Vagas de aproximação por estação definem posição, orientação e alcance. NPC desacelera, para, gira e só então inicia. Não trabalhar de costas, atravessar bancada ou esticar braços por parede.
- Replanejar em eventos relevantes e intervalos limitados, não buscar tudo globalmente a cada frame. Desvio local entre NPCs, preferência de passagem e fila em portas/estações.
- Detectar falta de progresso de caminho/tarefa. Tentar nova vaga → recalcular rota → aguardar brevemente → tarefa alternativa → recuperação controlada. Limitar tentativas; registrar motivo no Inspector. Teleporte apenas como último recurso diagnosticado, não locomoção normal.
- Takeover humano cancela a ação do NPC de forma limpa, sem disputar mãos/item nem recolocar automaticamente o objeto tomado pelo jogador.
- Distinguir “sem trabalho”, “sem ingrediente”, “posto ocupado” e “sem caminho”. Espera com olhar/postura discreta e sinal contextual curto; não inundar HUD com erros repetidos.

**Animação e feedback visual:**

- Reutilizar CharacterVisual/Animator e os clips reais disponíveis para jogadores. Caminhada/corrida acompanham velocidade medida, com aceleração/frenagem/giro suaves e sem deslizar parado.
- Itens e utensílios aparecem nos slots das mãos corretos; entrega visual acompanha a transferência confirmada. Usar âncoras de estação para contato e pose de trabalho.
- Sincronizar começo, fase e término da ação. Evento de animação pode disparar efeito cosmético, mas não é a única fonte de conclusão do processo no domínio.
- Feedback de pegar, apoiar, iniciar processo, progresso, concluir, falhar, ceder passagem e receber impacto. Gestos/olhares e pausas curtas coerentes, sem atrasar artificialmente a operação.
- Corrigir binding de modelo/Animator antes de ativar corpo; impedir T-pose quando há controller disponível.
- **Fallback visual obrigatório:** clip específico → clip genérico compatível → pose base com movimento cosmético discreto de braços/objeto e indicador contextual em UI Toolkit. Registrar clip ausente no validador. Fallback não altera duração/resultado e não deve mascarar indefinidamente animação faltante.
- Para carregamento de item sem animação específica, usar pose de transporte e âncora de mão; para ação interrompida, blend de saída e cleanup do tween/prop. Não duplicar item visual e físico.
- Replicar tarefa, alvo, item, etapa e tempo/progresso suficientes para cliente apresentar a mesma ação. Interpolar posição/rotação; cliente não executa um segundo planejador nem precisa de DomainState servidor.
- Aplicar LOD aos detalhes cosméticos de NPCs distantes, mantendo estado funcional. Nenhum efeito de rotina deve sacudir a câmera de todos.

### 12.4 Muralhas externas: correção de regressão e cobertura contínua

**Relato do usuário:** voltaram a existir falhas onde o perímetro da cidade não fica completamente coberto. Tratar como regressão obrigatória, não como polimento opcional.

**Evidência adicional:** StreetGridGenerator gera CityBorders a partir de voidFaces, simplifica vértices por distância/ângulo e ignora arestas abaixo de minEdgeLength (4,2 m no caminho modular). Isso oferece pontos concretos para investigar cobertura, mas não prova isoladamente a causa de todos os vãos relatados.

1. Encontrar no histórico a correção anterior usando git log/blame e os símbolos da geração de perímetro. Comparar algoritmo, configurações e assets entre a versão que fechava e o snapshot atual. Registrar causa/reprodução; não simplesmente reaplicar patch antigo sobre algoritmo diferente.
2. Capturar seeds com falha, tamanho/configuração, modo de geração e variante visual. Se a seed do relato não estiver disponível, procurar casos com arestas curtas, diagonais, concavidades e encontros agudos.
3. Extrair contornos externos válidos e classificá-los; não confundir cada vazio interno com perímetro externo. Garantir loops fechados, orientação consistente e nenhuma aresta perdida por limpeza.
4. Representar a cobertura de cada aresta como intervalos. Reservar aberturas intencionais explicitamente; preencher todo intervalo residual com peça de acabamento/adaptação válida. Aresta curta não pode simplesmente desaparecer porque não cabe módulo padrão.
5. Resolver quinas, junções e fechamento entre última/primeira peça. Usar conectores ou preenchimento apropriado; evitar sobreposição exagerada, z-fighting e prolongamento dentro da via.
6. Se fachada/prédio modular for rejeitado por colisão, manter solução alternativa de muralha para o intervalo. Distanciar o contorno da via quando necessário; não aceitar escolha entre deixar buraco e bloquear avenida.
7. Cobertura **visual e física**: mesh fechada nos encontros e colisores contínuos, com espessura/altura consistentes; conferir apoio em desníveis e faces visíveis dos lados acessíveis. Parede invisível não resolve buraco visual.
8. Aberturas/portões permitidos só com ID e intenção documentada. Nenhum vão acidental classificado como saída.
9. Validador calcula intervalos descobertos acima de tolerância configurada, quinas sem fechamento, ausência de collider e interseção com via/pátio/entrada. Gerar gizmos vermelhos nos intervalos faltantes e relatório por seed.
10. Integração com loading: só marcar geometria pronta quando geração/validação do perímetro terminar. Em erro, informar fase e diagnóstico; não declarar cidade pronta com trecho descartado.
11. Rede e regeneração: mesma seed/configuração produz mesmo perímetro; destruir/recriar cidade não deixa peças duplicadas nem mantém buracos da geração anterior.
12. Adicionar seeds reproduzíveis à suíte de regressão, incluindo a seed da correção antiga se recuperável. Validar contorno inteiro, não apenas uma captura frontal.

**Arquivos de entrada:** Scripts/City/StreetGridGenerator.cs, CityConfig.cs, PolygonUtils.cs, BlockFiller.cs e módulos chamados pelo caminho CityBorders. O problema de muralha física é distinto de road closures/arestas de trânsito bloqueadas; não misturar correções só pelo termo “fechamento”.

### 12.5 Feel distribuído pelo jogo inteiro

A ampliação não se limita à intermissão. **Feel é a camada principal de feedback**, reutilizando GameFeelPresenter, GameFeelNetworkBridge, perfis existentes, MMF_Player e a ponte de UI. DOTween complementa transições de UI e pivôs visuais. Evitar criar um terceiro sistema paralelo.

| Área | Eventos obrigatórios | Feedback proposto |
|---|---|---|
| Interação/mãos | Foco, pegar, trocar mão, apoiar, rejeitar, alcance inválido | Highlight suave, som contextual, acomodação do objeto e resposta curta |
| Cozinha | Abrir massa, ralar/cortar, montar, iniciar/parar processo | Ritmo visual/sonoro por ação, partículas discretas, progresso interpolado |
| Forno | Abrir, inserir/retirar, assar, alerta, queimar | Porta suave, calor/vapor, sinal de ponto e alerta distinto de falha |
| Pedidos | Chegar, aceitar, produzir unidade, concluir, expirar | Entrada/saída animada, destaque e som sem repetição excessiva |
| Embalagem/entrega | Embalar, carregar mochila, selecionar destino, entregar certo/errado | Confirmação tátil/visual, objeto acomodando e retorno de recompensa/erro |
| Contratados | Chegar, pegar tarefa/item, operar, esperar, falhar, concluir | Gestos, props sincronizados, reações e sinal contextual |
| Clientes/monstros | Entrar/sentar/levantar, impaciência, reação, alerta/hostilidade | Antecipação corporal e sons por estado |
| Veículos | Montar, acelerar, frear, buzinar, colidir/atropelar | Inclinação cosmética, partículas/áudio e impacto local |
| Combate/vida | Mirar, disparar, recarregar, acertar, dano, queda, resgate | Recuo visual, flash limitado, hit reaction e retorno gradual |
| Mundo | Portas, armários, luzes e elementos interativos do pátio | Movimento com amortecimento e áudio espacial consistente |
| Noites/UI | Meta, resultado, votos, loja, contagem, loading | Hierarquia de destaque, transições, celebração/erro breves |

**Regras de implementação e fluidez:**

- Cada evento precisa de emissor, binding, perfil de intensidade, teste e fallback verificado. Um enum ou chamada vazia não conta como entrega.
- Usar pivôs cosméticos filhos para squash, punch e sway. Não animar por Feel/DOTween o Transform físico/replicado do carro, personagem, collider, NavMeshAgent ou slot lógico.
- Feedback de intenção local pode ser imediato; acerto, compra, dano e recompensa dependem de confirmação autoritativa. Deduplicar apresentação por event ID para não tocar duas vezes no host.
- Medir o que é fluidez de movimento versus intensidade de efeitos: aceleração/frenagem, blend de animação, interpolação de rede, atualização de barras e transições de foco são parte do trabalho.
- Criar perfis Suave/Cartoon/Reduzido com intensidade por categoria. Preset padrão deve ser suave e legível; evitar tremor permanente e excesso de bounce.
- Tempo de apresentação desacoplado do resultado lógico. Nenhuma animação obrigatória bloqueia input além da ação real; cancelamento e interrupção sempre limpam estados.
- Limitar vozes, partículas e efeitos concorrentes por categoria/distância. Usar pooling e cooldown; efeito distante não domina mix nem câmera.
- Testar interrupção, objeto destruído, troca de cena, pausa, ReduceMotion e entrada tardia. Parar loops e restaurar poses/escala/volume no teardown.
- UI nova e indicadores de estado usam UI Toolkit; não introduzir novos Canvas/OnGUI para contornar integração. Configuração, diagnóstico e previews ficam no Odin Inspector/wizards.
- Entregar catálogo de cobertura por evento com estado: configurado, instalado, verificado, pendente. Priorizar feedback que comunica ação e consequência.

### 12.6 Blocos adicionais e dependências

O plano passa de **12 para 18 blocos**. Manter IDs antigos para não invalidar trabalho já iniciado. C12 torna-se uma verificação intermediária do escopo original; a aprovação completa é C18.

| Bloco | Entrega | Dependências e aceite |
|---|---|---|
| C13 | Tráfego nas avenidas, corpos sólidos e atropelamento com recuo | C07/C11; carroceria inteira dentro da pista, colisão física e um impacto por episódio |
| C14 | Spawns autorados e reservados de jogadores/contratados | C08/C10; vaga livre, piso/caminho válidos, sem duplicação |
| C15 | IA dos contratados, interação corporal e fallbacks visuais | C14/C05; operação real, aproximação correta, recuperação e proxies consistentes |
| C16 | Regressão das muralhas e validador de cobertura | C07; zero intervalo não intencional aberto acima da tolerância |
| C17 | Feel transversal e polimento de movimento/transições | C04/C05 e eventos C13–C16; bindings reais, acessibilidade e orçamento de efeitos |
| C18 | Regressão integrada final e evidências | Todos os blocos; testes T01–T44 e documentação coerente |

C16 pode ser antecipado junto da geometria de C07; C14 deve ser projetado durante C06/C08. C13 só ativa trânsito após geometria e navegação válidas. C17 instala feedback ao longo de cada sistema e consolida no final, em vez de esperar toda a lógica terminar para descobrir pontos de integração.

### 12.7 Testes adicionais obrigatórios

| Teste | Cenário | Aceite |
|---|---|---|
| T27 | Carro em reta, curva apertada e cruzamento | Carroceria/volume varrido não entra na calçada; sem cortar esquina |
| T28 | Parede, carro parado e obstáculo móvel à frente | Freada/contato físico, sem atravessar ou empilhar veículos |
| T29 | Atropelamento frontal/lateral de jogador e NPC | Recuo direcional visível, dano proporcional limitado e recuperação correta |
| T30 | Contato contínuo/múltiplos colisores e nova batida | Um evento por episódio; nova batida válida volta a funcionar |
| T31 | Impacto perto de muralha, bancada e borda | Vítima não atravessa geometria; movimento do owner não desfaz recuo |
| T32 | Trânsito host/cliente, latência e taxa de frames variáveis | Simulação autoritativa, visual suave e sem dano duplicado |
| T33 | Spawn de 1–4 jogadores, chegada tardia e retry | Vagas distintas, orientação correta, sem origem/avenida/telhado |
| T34 | Contratar balconistas/pizzaiolos simultaneamente | Chegada por âncoras reservadas, sem corpos duplicados/empilhados |
| T35 | Todas as vagas bloqueadas ou NavMesh ainda indisponível | Adiar/esperar com estado claro; sem débito repetido nem spawn inválido |
| T36 | NPC pega/carrega/prepara/entrega e jogador assume tarefa | Mãos/props coerentes, domínio real, reserva liberada |
| T37 | Posto ocupado, caminho bloqueado, ingrediente ausente | Espera/replanejamento limitado, feedback legível, sem loop infinito |
| T38 | Remover clip específico e interromper animação | Fallback visível; sem T-pose, item duplicado ou tween órfão |
| T39 | Cliente entra com contratado trabalhando | Mesmo item/etapa/pose aproximada, sem planejador duplicado |
| T40 | Muralhas: seeds antigas/com falha, pequenas/grandes e concavidades | Perímetro visual/físico completo, inclusive arestas curtas e última junção |
| T41 | Fachada rejeitada, via perto da borda, desnível | Fechamento alternativo sem vão e sem bloquear avenida |
| T42 | Regenerar mesma seed e alternar variantes/host/cliente | Cobertura determinística e sem peças duplicadas |
| T43 | Eventos Feel simultâneos, ReduceMotion, pausa e troca de cena | Feedback único/limitado, limpeza e acessibilidade |
| T44 | Cozinha + funcionários + tráfego + combate em carga | CPU/GC/rede comparados ao baseline; fluidez sem regressão funcional |

Muralhas exigem validação de intervalos + inspeção visual, não só collider contínuo. Trânsito exige teste do volume completo, não só waypoint no asfalto. Contratados exigem observar o ciclo de trabalho inteiro, não apenas spawn e caminhada.

### 12.8 Critérios novos de conclusão

- Carros possuem corpo sólido e resposta física; circulam somente nas avenidas autorizadas, incluindo curvas.
- Atropelamento desloca jogador/NPC para longe do contato com colisão, sincronização e recuperação consistentes.
- Spawns de jogadores/contratados ficam configurados nos prefabs e disponíveis para edição visual no Odin.
- Funcionários operam objetos de maneira legível, com aproximação, mãos, animações, feedback e fallbacks testados.
- Muralhas ficam visual e fisicamente completas, com regressão anterior investigada e seeds protegidas por validação.
- Feel cobre o ciclo do jogo, com efeitos instalados e verificados, sem disputar autoridade física/rede ou comprometer desempenho.



## Apêndice A — Fontes de código fixadas no commit

- [PizzeriaSceneLoadingCoordinator.cs](https://github.com/TavernaDasMilPortas/ScaryParty/blob/2dacefe61be6f9cf98b350530505795bbd629b53/ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Presentation/PizzeriaSceneLoadingCoordinator.cs)
- [PizzeriaCommandHandler.cs](https://github.com/TavernaDasMilPortas/ScaryParty/blob/2dacefe61be6f9cf98b350530505795bbd629b53/ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Network/PizzeriaCommandHandler.cs)
- [RunProgressionService.cs](https://github.com/TavernaDasMilPortas/ScaryParty/blob/2dacefe61be6f9cf98b350530505795bbd629b53/ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Domain/Services/RunProgressionService.cs)
- [NightIntermissionPresenter.cs](https://github.com/TavernaDasMilPortas/ScaryParty/blob/2dacefe61be6f9cf98b350530505795bbd629b53/ScaryParty/Assets/Piaaria/UI/Piaria/Scripts/Runtime/Presenters/NightIntermissionPresenter.cs)
- [NightIntermissionScreen.uxml](https://github.com/TavernaDasMilPortas/ScaryParty/blob/2dacefe61be6f9cf98b350530505795bbd629b53/ScaryParty/Assets/Piaaria/UI/Piaria/UXML/NightIntermissionScreen.uxml)
- [RunShopPagePresenter.cs](https://github.com/TavernaDasMilPortas/ScaryParty/blob/2dacefe61be6f9cf98b350530505795bbd629b53/ScaryParty/Assets/Piaaria/UI/Piaria/Scripts/Runtime/Presenters/RunShopPagePresenter.cs)
- [NightIntermissionFeelBridge.cs](https://github.com/TavernaDasMilPortas/ScaryParty/blob/2dacefe61be6f9cf98b350530505795bbd629b53/ScaryParty/Assets/Piaaria/UI/Piaria/Scripts/Runtime/Core/NightIntermissionFeelBridge.cs)
- [OperationsStatusPresenter.cs](https://github.com/TavernaDasMilPortas/ScaryParty/blob/2dacefe61be6f9cf98b350530505795bbd629b53/ScaryParty/Assets/Piaaria/UI/Piaria/Scripts/Runtime/Presenters/OperationsStatusPresenter.cs)
- [ScoreboardPresenter.cs](https://github.com/TavernaDasMilPortas/ScaryParty/blob/2dacefe61be6f9cf98b350530505795bbd629b53/ScaryParty/Assets/Piaaria/UI/Piaria/Scripts/Runtime/Presenters/ScoreboardPresenter.cs)
- [PizzeriaParkingLotBuilder.cs](https://github.com/TavernaDasMilPortas/ScaryParty/blob/2dacefe61be6f9cf98b350530505795bbd629b53/ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Composition/PizzeriaParkingLotBuilder.cs)
- [PizzeriaFootprint.cs](https://github.com/TavernaDasMilPortas/ScaryParty/blob/2dacefe61be6f9cf98b350530505795bbd629b53/ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Composition/PizzeriaFootprint.cs)
- [PizzeriaPlacementSolver.cs](https://github.com/TavernaDasMilPortas/ScaryParty/blob/2dacefe61be6f9cf98b350530505795bbd629b53/ScaryParty/Assets/Piaaria/Scripts/City/PizzeriaPlacementSolver.cs)
- [PizzeriaRoot.cs](https://github.com/TavernaDasMilPortas/ScaryParty/blob/2dacefe61be6f9cf98b350530505795bbd629b53/ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Composition/PizzeriaRoot.cs)
- [UIToolkitTweenService.cs](https://github.com/TavernaDasMilPortas/ScaryParty/blob/2dacefe61be6f9cf98b350530505795bbd629b53/ScaryParty/Assets/Piaaria/UI/Piaria/Scripts/Runtime/Core/UIToolkitTweenService.cs)
- [NavigationWorldManager.cs](https://github.com/TavernaDasMilPortas/ScaryParty/blob/2dacefe61be6f9cf98b350530505795bbd629b53/ScaryParty/Assets/Piaaria/Scripts/Navigation/Core/NavigationWorldManager.cs)
- [OrderUnitFulfillmentService.cs](https://github.com/TavernaDasMilPortas/ScaryParty/blob/2dacefe61be6f9cf98b350530505795bbd629b53/ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Domain/Services/OrderUnitFulfillmentService.cs)
- [EmployeeAgentController.cs](https://github.com/TavernaDasMilPortas/ScaryParty/blob/2dacefe61be6f9cf98b350530505795bbd629b53/ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Presentation/EmployeeAgentController.cs)
- [RunIntermissionNetworkTests.cs](https://github.com/TavernaDasMilPortas/ScaryParty/blob/2dacefe61be6f9cf98b350530505795bbd629b53/ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Editor/Tests/RunIntermissionNetworkTests.cs)
- [PizzeriaModular_Manual.prefab](https://github.com/TavernaDasMilPortas/ScaryParty/blob/2dacefe61be6f9cf98b350530505795bbd629b53/ScaryParty/Assets/Piaaria/Prefabs/Pizzeria/PizzeriaModular_Manual.prefab)

## Apêndice B — Inventário integral da alteração

427 caminhos obtidos por git diff --name-only entre base e snapshot. Lista de inventário, não revisão manual integral de cada arquivo.

~~~text
ScaryParty/Assets/Piaaria/Animations/Characters/CharacterAnimator.controller
ScaryParty/Assets/Piaaria/Cutscene/CutsceneBindingProfile.asset
ScaryParty/Assets/Piaaria/Cutscene/Materials/cutscene_01/Mat_CS_Material.016.mat
ScaryParty/Assets/Piaaria/Cutscene/Materials/cutscene_01/Mat_CS_Material.017.mat
ScaryParty/Assets/Piaaria/Cutscene/Materials/cutscene_01/Mat_CS_Material.018.mat
ScaryParty/Assets/Piaaria/Cutscene/Materials/cutscene_01/Mat_CS_Material.035.mat
ScaryParty/Assets/Piaaria/Cutscene/Materials/cutscene_01/Mat_CS_Material.060.mat
ScaryParty/Assets/Piaaria/Cutscene/Materials/cutscene_01/Mat_CS_Material.061.mat
ScaryParty/Assets/Piaaria/Cutscene/Materials/cutscene_01/Mat_CS_Material.062.mat
ScaryParty/Assets/Piaaria/Cutscene/Materials/cutscene_01/Mat_CS_Material.066.mat
ScaryParty/Assets/Piaaria/Cutscene/Materials/cutscene_01/Mat_CS_Material.067.mat
ScaryParty/Assets/Piaaria/Cutscene/Materials/cutscene_01/Mat_CS_Material.068.mat
ScaryParty/Assets/Piaaria/Cutscene/Materials/cutscene_01/Mat_CS_Material.mat
ScaryParty/Assets/Piaaria/Playtest.meta
ScaryParty/Assets/Piaaria/Playtest/Materials.meta
ScaryParty/Assets/Piaaria/Playtest/Materials/Mat_Playtest_BoardFrame.mat
ScaryParty/Assets/Piaaria/Playtest/Materials/Mat_Playtest_BoardFrame.mat.meta
ScaryParty/Assets/Piaaria/Playtest/Materials/Mat_Playtest_Cashier.mat
ScaryParty/Assets/Piaaria/Playtest/Materials/Mat_Playtest_Cashier.mat.meta
ScaryParty/Assets/Piaaria/Playtest/Materials/Mat_Playtest_CashierVisor.mat
ScaryParty/Assets/Piaaria/Playtest/Materials/Mat_Playtest_CashierVisor.mat.meta
ScaryParty/Assets/Piaaria/Playtest/Materials/Mat_Playtest_ChefHat.mat
ScaryParty/Assets/Piaaria/Playtest/Materials/Mat_Playtest_ChefHat.mat.meta
ScaryParty/Assets/Piaaria/Playtest/Materials/Mat_Playtest_CrateWood.mat
ScaryParty/Assets/Piaaria/Playtest/Materials/Mat_Playtest_CrateWood.mat.meta
ScaryParty/Assets/Piaaria/Playtest/Materials/Mat_Playtest_Eyes.mat
ScaryParty/Assets/Piaaria/Playtest/Materials/Mat_Playtest_Eyes.mat.meta
ScaryParty/Assets/Piaaria/Playtest/Materials/Mat_Playtest_Garlic.mat
ScaryParty/Assets/Piaaria/Playtest/Materials/Mat_Playtest_Garlic.mat.meta
ScaryParty/Assets/Piaaria/Playtest/Materials/Mat_Playtest_GarlicStem.mat
ScaryParty/Assets/Piaaria/Playtest/Materials/Mat_Playtest_GarlicStem.mat.meta
ScaryParty/Assets/Piaaria/Playtest/Materials/Mat_Playtest_MotoBody.mat
ScaryParty/Assets/Piaaria/Playtest/Materials/Mat_Playtest_MotoBody.mat.meta
ScaryParty/Assets/Piaaria/Playtest/Materials/Mat_Playtest_MotoMetal.mat
ScaryParty/Assets/Piaaria/Playtest/Materials/Mat_Playtest_MotoMetal.mat.meta
ScaryParty/Assets/Piaaria/Playtest/Materials/Mat_Playtest_MotoTire.mat
ScaryParty/Assets/Piaaria/Playtest/Materials/Mat_Playtest_MotoTire.mat.meta
ScaryParty/Assets/Piaaria/Playtest/Materials/Mat_Playtest_Mushroom.mat
ScaryParty/Assets/Piaaria/Playtest/Materials/Mat_Playtest_Mushroom.mat.meta
ScaryParty/Assets/Piaaria/Playtest/Materials/Mat_Playtest_Pizzaiolo.mat
ScaryParty/Assets/Piaaria/Playtest/Materials/Mat_Playtest_Pizzaiolo.mat.meta
ScaryParty/Assets/Piaaria/Playtest/Materials/Mat_Playtest_ReceivingFloor.mat
ScaryParty/Assets/Piaaria/Playtest/Materials/Mat_Playtest_ReceivingFloor.mat.meta
ScaryParty/Assets/Piaaria/Playtest/Materials/Mat_Playtest_ScreenGlow.mat
ScaryParty/Assets/Piaaria/Playtest/Materials/Mat_Playtest_ScreenGlow.mat.meta
ScaryParty/Assets/Piaaria/Playtest/Materials/Mat_Playtest_Terminal.mat
ScaryParty/Assets/Piaaria/Playtest/Materials/Mat_Playtest_Terminal.mat.meta
ScaryParty/Assets/Piaaria/Playtest/Prefabs.meta
ScaryParty/Assets/Piaaria/Playtest/Prefabs/Visual_DeliveryMotorcycle.prefab
ScaryParty/Assets/Piaaria/Playtest/Prefabs/Visual_DeliveryMotorcycle.prefab.meta
ScaryParty/Assets/Piaaria/Playtest/Prefabs/Visual_Employee_Cashier.prefab
ScaryParty/Assets/Piaaria/Playtest/Prefabs/Visual_Employee_Cashier.prefab.meta
ScaryParty/Assets/Piaaria/Playtest/Prefabs/Visual_Employee_Pizzaiolo.prefab
ScaryParty/Assets/Piaaria/Playtest/Prefabs/Visual_Employee_Pizzaiolo.prefab.meta
ScaryParty/Assets/Piaaria/Playtest/Prefabs/Visual_Ingredient_Garlic.prefab
ScaryParty/Assets/Piaaria/Playtest/Prefabs/Visual_Ingredient_Garlic.prefab.meta
ScaryParty/Assets/Piaaria/Playtest/Prefabs/Visual_Ingredient_Mushroom.prefab
ScaryParty/Assets/Piaaria/Playtest/Prefabs/Visual_Ingredient_Mushroom.prefab.meta
ScaryParty/Assets/Piaaria/Playtest/Prefabs/Visual_ManagementBoard.prefab
ScaryParty/Assets/Piaaria/Playtest/Prefabs/Visual_ManagementBoard.prefab.meta
ScaryParty/Assets/Piaaria/Playtest/Prefabs/Visual_Monster_Ghost.prefab
ScaryParty/Assets/Piaaria/Playtest/Prefabs/Visual_Monster_Ghost.prefab.meta
ScaryParty/Assets/Piaaria/Playtest/Prefabs/Visual_Monster_Vampire.prefab
ScaryParty/Assets/Piaaria/Playtest/Prefabs/Visual_Monster_Vampire.prefab.meta
ScaryParty/Assets/Piaaria/Playtest/Prefabs/Visual_Monster_Werewolf.prefab
ScaryParty/Assets/Piaaria/Playtest/Prefabs/Visual_Monster_Werewolf.prefab.meta
ScaryParty/Assets/Piaaria/Playtest/Prefabs/Visual_Monster_Zombie.prefab
ScaryParty/Assets/Piaaria/Playtest/Prefabs/Visual_Monster_Zombie.prefab.meta
ScaryParty/Assets/Piaaria/Playtest/Prefabs/Visual_ReceivingZone.prefab
ScaryParty/Assets/Piaaria/Playtest/Prefabs/Visual_ReceivingZone.prefab.meta
ScaryParty/Assets/Piaaria/Playtest/Prefabs/Visual_SupplierTerminal.prefab
ScaryParty/Assets/Piaaria/Playtest/Prefabs/Visual_SupplierTerminal.prefab.meta
ScaryParty/Assets/Piaaria/Playtest/Presets.meta
ScaryParty/Assets/Piaaria/Playtest/Presets/Entrega_3Noites.asset
ScaryParty/Assets/Piaaria/Playtest/Presets/Entrega_3Noites.asset.meta
ScaryParty/Assets/Piaaria/Playtest/Presets/NoiteDeTeste.asset
ScaryParty/Assets/Piaaria/Playtest/Presets/NoiteDeTeste.asset.meta
ScaryParty/Assets/Piaaria/Playtest/Presets/Playtest_Integracao.asset
ScaryParty/Assets/Piaaria/Playtest/Presets/Playtest_Integracao.asset.meta
ScaryParty/Assets/Piaaria/Playtest/Presets/Tutorial_Noite0.asset
ScaryParty/Assets/Piaaria/Playtest/Presets/Tutorial_Noite0.asset.meta
ScaryParty/Assets/Piaaria/Playtest/ScriptableObjects.meta
ScaryParty/Assets/Piaaria/Playtest/ScriptableObjects/IngredientDef_10_Garlic.asset
ScaryParty/Assets/Piaaria/Playtest/ScriptableObjects/IngredientDef_10_Garlic.asset.meta
ScaryParty/Assets/Piaaria/Playtest/ScriptableObjects/MonsterVisualCatalog.asset
ScaryParty/Assets/Piaaria/Playtest/ScriptableObjects/MonsterVisualCatalog.asset.meta
ScaryParty/Assets/Piaaria/Playtest/ScriptableObjects/SupplyItem_5_Garlic.asset
ScaryParty/Assets/Piaaria/Playtest/ScriptableObjects/SupplyItem_5_Garlic.asset.meta
ScaryParty/Assets/Piaaria/Prefabs/City/Modules/Prefab_Modular_A.prefab
ScaryParty/Assets/Piaaria/Prefabs/City/Modules/Prefab_Modular_B.prefab
ScaryParty/Assets/Piaaria/Prefabs/City/Modules/Prefab_Modular_D.prefab
ScaryParty/Assets/Piaaria/Prefabs/City/Modules/Prefab_Modular_E.prefab
ScaryParty/Assets/Piaaria/Prefabs/City/Modules/Prefab_Modular_E.prefab.meta
ScaryParty/Assets/Piaaria/Prefabs/City/Modules/Prefab_Modular_F.prefab
ScaryParty/Assets/Piaaria/Prefabs/City/Modules/Prefab_Modular_F.prefab.meta
ScaryParty/Assets/Piaaria/Prefabs/City/Modules/Prefab_Modular_G.prefab
ScaryParty/Assets/Piaaria/Prefabs/City/Modules/Prefab_Modular_G.prefab.meta
ScaryParty/Assets/Piaaria/Prefabs/City/Modules/Prefab_Modular_H.prefab
ScaryParty/Assets/Piaaria/Prefabs/City/Modules/Prefab_Modular_H.prefab.meta
ScaryParty/Assets/Piaaria/Prefabs/City/Modules/Prefab_Modular_I.prefab
ScaryParty/Assets/Piaaria/Prefabs/City/Modules/Prefab_Modular_I.prefab.meta
ScaryParty/Assets/Piaaria/Prefabs/City/Modules/Prefab_Modular_J.prefab.meta
ScaryParty/Assets/Piaaria/Prefabs/City/Modules/Prefab_Modular_K.prefab
ScaryParty/Assets/Piaaria/Prefabs/City/Modules/Prefab_Modular_K.prefab.meta
ScaryParty/Assets/Piaaria/Prefabs/City/Modules/Prefab_Modular_L.prefab
ScaryParty/Assets/Piaaria/Prefabs/City/Modules/Prefab_Modular_L.prefab.meta
ScaryParty/Assets/Piaaria/Prefabs/Pizzeria/PizzeriaModular_CelShaded.prefab
ScaryParty/Assets/Piaaria/Prefabs/Pizzeria/PizzeriaModular_Manual.prefab
ScaryParty/Assets/Piaaria/Prefabs/Pizzeria/PizzeriaPrefab.prefab
ScaryParty/Assets/Piaaria/Resources/CharacterAnimator.controller
ScaryParty/Assets/Piaaria/Resources/Playtest.meta
ScaryParty/Assets/Piaaria/Resources/Playtest/BuildVersionInfo.txt
ScaryParty/Assets/Piaaria/Resources/Playtest/BuildVersionInfo.txt.meta
ScaryParty/Assets/Piaaria/Resources/Playtest/PlaytestReferenceManifest.asset
ScaryParty/Assets/Piaaria/Resources/Playtest/PlaytestReferenceManifest.asset.meta
ScaryParty/Assets/Piaaria/Scenes/Cutscenes/CutScene.unity
ScaryParty/Assets/Piaaria/Scenes/GameplayVariants/GameScene_CelShaded.unity
ScaryParty/Assets/Piaaria/Scenes/LobbyScene.unity
ScaryParty/Assets/Piaaria/Scenes/ReadyScene.unity
ScaryParty/Assets/Piaaria/ScriptableObjects/City/Modular/CityModuleCatalog.asset
ScaryParty/Assets/Piaaria/ScriptableObjects/DefaultCityConfig.asset
ScaryParty/Assets/Piaaria/ScriptableObjects/GeneratedCityData.asset
ScaryParty/Assets/Piaaria/ScriptableObjects/Pizzeria/Balancing.meta
ScaryParty/Assets/Piaaria/ScriptableObjects/Pizzeria/Balancing/RunBalancingCatalog.asset
ScaryParty/Assets/Piaaria/ScriptableObjects/Pizzeria/Balancing/RunBalancingCatalog.asset.meta
ScaryParty/Assets/Piaaria/ScriptableObjects/Pizzeria/Ingredient_8_CogumeloInteiro.asset
ScaryParty/Assets/Piaaria/ScriptableObjects/Pizzeria/Ingredient_9_CogumeloFatiado.asset
ScaryParty/Assets/Piaaria/ScriptableObjects/Pizzeria/PizzeriaConfig.asset
ScaryParty/Assets/Piaaria/ScriptableObjects/Pizzeria/PizzeriaConfig_PizzeriaModular_Manual.asset
ScaryParty/Assets/Piaaria/ScriptableObjects/Pizzeria/Stations/StationDef_10_Staging.asset
ScaryParty/Assets/Piaaria/ScriptableObjects/Pizzeria/Stations/StationDef_11_Counter.asset
ScaryParty/Assets/Piaaria/ScriptableObjects/Pizzeria/Stations/StationDef_12_Phone.asset
ScaryParty/Assets/Piaaria/ScriptableObjects/Pizzeria/Stations/StationDef_13_SupplierTerminal.asset
ScaryParty/Assets/Piaaria/ScriptableObjects/Pizzeria/Stations/StationDef_14_Receiving.asset
ScaryParty/Assets/Piaaria/ScriptableObjects/Pizzeria/Stations/StationDef_15_TicketBoard.asset
ScaryParty/Assets/Piaaria/ScriptableObjects/Pizzeria/Stations/StationDef_16_BoxStack.asset
ScaryParty/Assets/Piaaria/ScriptableObjects/Pizzeria/Stations/StationDef_19_Trash.asset
ScaryParty/Assets/Piaaria/ScriptableObjects/Pizzeria/Stations/StationDef_1_Counter.asset
ScaryParty/Assets/Piaaria/ScriptableObjects/Pizzeria/Stations/StationDef_1_Fridge.asset
ScaryParty/Assets/Piaaria/ScriptableObjects/Pizzeria/Stations/StationDef_2_Counter.asset
ScaryParty/Assets/Piaaria/ScriptableObjects/Pizzeria/Stations/StationDef_2_Pantry.asset
ScaryParty/Assets/Piaaria/ScriptableObjects/Pizzeria/Stations/StationDef_3_ToolRack.asset
ScaryParty/Assets/Piaaria/ScriptableObjects/Pizzeria/Stations/StationDef_4_Prep.asset
ScaryParty/Assets/Piaaria/ScriptableObjects/Pizzeria/Stations/StationDef_6_Assembly.asset
ScaryParty/Assets/Piaaria/ScriptableObjects/Pizzeria/Stations/StationDef_8_Oven.asset
ScaryParty/Assets/Piaaria/ScriptableObjects/Pizzeria/Stations/StationDef_9_Packaging.asset
ScaryParty/Assets/Piaaria/Scripts/City/CityBuilding.cs
ScaryParty/Assets/Piaaria/Scripts/City/CityGenerator.cs
ScaryParty/Assets/Piaaria/Scripts/City/DeliveryMotorcycleController.cs
ScaryParty/Assets/Piaaria/Scripts/City/Editor/CityGraphPathfinderTests.cs
ScaryParty/Assets/Piaaria/Scripts/City/PedestrianNpc.cs
ScaryParty/Assets/Piaaria/Scripts/City/TrafficManager.cs
ScaryParty/Assets/Piaaria/Scripts/City/TrafficVehicle.cs
ScaryParty/Assets/Piaaria/Scripts/Combat.meta
ScaryParty/Assets/Piaaria/Scripts/Combat/Monsters.meta
ScaryParty/Assets/Piaaria/Scripts/Combat/Monsters/MonsterNpcController.cs
ScaryParty/Assets/Piaaria/Scripts/Combat/Monsters/MonsterNpcController.cs.meta
ScaryParty/Assets/Piaaria/Scripts/Combat/Monsters/MonsterPopulationDirector.cs
ScaryParty/Assets/Piaaria/Scripts/Combat/Monsters/MonsterPopulationDirector.cs.meta
ScaryParty/Assets/Piaaria/Scripts/Combat/Monsters/MonsterVisualCatalog.cs
ScaryParty/Assets/Piaaria/Scripts/Combat/Monsters/MonsterVisualCatalog.cs.meta
ScaryParty/Assets/Piaaria/Scripts/Cutscenes/Editor/CutsceneMaterialCatalog.cs
ScaryParty/Assets/Piaaria/Scripts/Cutscenes/Editor/CutsceneSceneAssembler.cs
ScaryParty/Assets/Piaaria/Scripts/Cutscenes/Runtime/CutscenePlaybackService.cs
ScaryParty/Assets/Piaaria/Scripts/Editor/CharacterSetupWizard.cs
ScaryParty/Assets/Piaaria/Scripts/Editor/Playtest.meta
ScaryParty/Assets/Piaaria/Scripts/Editor/Playtest/PlaytestInstallerAndValidator.cs
ScaryParty/Assets/Piaaria/Scripts/Editor/Playtest/PlaytestInstallerAndValidator.cs.meta
ScaryParty/Assets/Piaaria/Scripts/Editor/Playtest/PlaytestPlaceholderAssetBuilder.cs
ScaryParty/Assets/Piaaria/Scripts/Editor/Playtest/PlaytestPlaceholderAssetBuilder.cs.meta
ScaryParty/Assets/Piaaria/Scripts/Editor/Playtest/PlaytestPresetBuilder.cs
ScaryParty/Assets/Piaaria/Scripts/Editor/Playtest/PlaytestPresetBuilder.cs.meta
ScaryParty/Assets/Piaaria/Scripts/Editor/Playtest/PlaytestTestResultXmlWriter.cs
ScaryParty/Assets/Piaaria/Scripts/Editor/Playtest/PlaytestTestResultXmlWriter.cs.meta
ScaryParty/Assets/Piaaria/Scripts/Lobby/LobbyManager.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Authoring/NightConfig.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Authoring/PizzeriaConfig.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Authoring/PizzeriaRunPreset.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Authoring/PizzeriaRunPreset.cs.meta
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Authoring/PlaytestReferenceManifest.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Authoring/PlaytestReferenceManifest.cs.meta
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Authoring/RunBalancingCatalog.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Authoring/RunBalancingCatalog.cs.meta
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Composition/DemandDirector.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Composition/PizzeriaParkingLotBuilder.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Composition/PizzeriaRoot.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Domain/Definitions/DefinitionCatalog.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Domain/Definitions/RunBalancingSnapshot.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Domain/Definitions/RunBalancingSnapshot.cs.meta
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Domain/Definitions/RunBalancingTypes.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Domain/Definitions/RunBalancingTypes.cs.meta
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Domain/Definitions/RunDescriptionFormatter.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Domain/Definitions/RunDescriptionFormatter.cs.meta
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Domain/Models/CustomerState.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Domain/Models/NewModels.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Domain/Models/OrderUnitFulfillment.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Domain/Models/OrderUnitFulfillment.cs.meta
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Domain/Models/PizzeriaState.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Domain/Models/SupplyState.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Domain/Services/CustomerService.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Domain/Services/DeliveryEvaluator.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Domain/Services/EmployeeTaskService.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Domain/Services/EmployeeTaskService.cs.meta
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Domain/Services/NightCycleService.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Domain/Services/NightParameterResolver.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Domain/Services/NightParameterResolver.cs.meta
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Domain/Services/OrderService.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Domain/Services/OrderUnitFulfillmentService.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Domain/Services/OrderUnitFulfillmentService.cs.meta
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Domain/Services/OvenService.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Domain/Services/PlayerHoldService.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Domain/Services/PlaytestCheckpointService.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Domain/Services/PlaytestCheckpointService.cs.meta
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Domain/Services/ProcessingService.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Domain/Services/RunBalancingValidator.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Domain/Services/RunBalancingValidator.cs.meta
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Domain/Services/RunBallotResolver.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Domain/Services/RunBallotResolver.cs.meta
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Domain/Services/RunProgressionService.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Domain/Services/RunProgressionService.cs.meta
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Domain/Services/SpeciesHostilityService.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Domain/Services/SpeciesHostilityService.cs.meta
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Domain/Services/TransferService.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Domain/Types/Ids.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Domain/Types/LocationRef.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Editor/Authoring/PizzeriaDoorAuthoring.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Editor/Migration/PiariaGameplayUIMigration.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Editor/Migration/RunBalancingMigrationRunner.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Editor/Migration/RunBalancingMigrationRunner.cs.meta
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Editor/NightIntermissionPreviewWizard.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Editor/NightIntermissionPreviewWizard.cs.meta
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Editor/PizzeriaEventsShopBalancingWizard.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Editor/PizzeriaEventsShopBalancingWizard.cs.meta
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Editor/PlaytestDebugMenu.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Editor/PlaytestDebugMenu.cs.meta
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Editor/Tests/P1_MonsterEmployeeClientSyncTests.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Editor/Tests/P1_MonsterEmployeeClientSyncTests.cs.meta
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Editor/Tests/P4_UiInputAndNetworkFlowTests.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Editor/Tests/P4_UiInputAndNetworkFlowTests.cs.meta
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Editor/Tests/P5_PlaceholdersAndAnchorsTests.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Editor/Tests/P5_PlaceholdersAndAnchorsTests.cs.meta
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Editor/Tests/P6_PresetsAndDiagnosticsTests.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Editor/Tests/P6_PresetsAndDiagnosticsTests.cs.meta
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Editor/Tests/RunBallotResolverTests.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Editor/Tests/RunBallotResolverTests.cs.meta
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Editor/Tests/RunIntermissionNetworkTests.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Editor/Tests/RunIntermissionNetworkTests.cs.meta
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Editor/Tests/WizardEventsShopBalancingTests.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Editor/Tests/WizardEventsShopBalancingTests.cs.meta
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Network/KitchenCommands.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Network/LogisticsCommands.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Network/NetDtos.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Network/OrderCommands.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Network/PizzeriaCommandHandler.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Network/PizzeriaDebugCommands.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Network/PizzeriaNetworkState.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Network/PizzeriaNotifier.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Network/PlayerLifecycleBridge.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Network/ReplicationProjector.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Network/SessionLaunchConfig.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Network/SessionLaunchConfig.cs.meta
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Player/BackpackController.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Player/PlayerInventoryAdapter.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Presentation/BackpackVisualizer.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Presentation/ContextualTutorialPresenter.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Presentation/CustomerNpcView.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Presentation/CustomerSpawner.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Presentation/DeliveryResultPresenter.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Presentation/DeliverySelectionPresenter.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Presentation/EmployeeAgentController.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Presentation/EmployeeAgentController.cs.meta
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Presentation/EntityVisualAnchorRig.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Presentation/EntityVisualAnchorRig.cs.meta
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Presentation/GameplayInputGate.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Presentation/NightResultsPresenter.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Presentation/PizzeriaItemVisualizer.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Presentation/PizzeriaSceneLoadingCoordinator.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Presentation/PlaytestDebugController.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Presentation/PlaytestDebugController.cs.meta
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Presentation/RecipeBookPresenter.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Presentation/RunPreparationPresenter.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Presentation/RunPreparationPresenter.cs.meta
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Presentation/StationDoorPresenter.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Presentation/StationProgressBar.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Presentation/TicketBoardPresenter.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Presentation/TicketModalPresenter.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Stations/ManagementBoardStation.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Stations/ManagementBoardStation.cs.meta
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Stations/NpcWorkPoint.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Stations/NpcWorkPoint.cs.meta
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Stations/StationView.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Stations/SupplierTerminal.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Stations/TicketBoardStation.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Stations/WeaponRackStation.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Tests.meta
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Tests/PlayMode.meta
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Tests/PlayMode/PlaytestIntegrationPlayModeTests.cs
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Tests/PlayMode/PlaytestIntegrationPlayModeTests.cs.meta
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Tests/PlayMode/ScaryParty.Pizzeria.PlayModeTests.asmdef
ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Tests/PlayMode/ScaryParty.Pizzeria.PlayModeTests.asmdef.meta
ScaryParty/Assets/Piaaria/Scripts/Player/FirstPersonCameraController.cs
ScaryParty/Assets/Piaaria/Scripts/Player/PlayerAllyHelpInteractable.cs
ScaryParty/Assets/Piaaria/Scripts/Player/PlayerAllyHelpInteractable.cs.meta
ScaryParty/Assets/Piaaria/Scripts/Player/PlayerHealth.cs
ScaryParty/Assets/Piaaria/Scripts/Player/PlayerInteraction.cs
ScaryParty/Assets/Piaaria/Scripts/VisualStyles/Runtime/TimeOfDayLightingDirector.cs
ScaryParty/Assets/Piaaria/Scripts/VisualStyles/Runtime/TimeOfDayTestOverlay.cs
ScaryParty/Assets/Piaaria/Scripts/VisualStyles/Runtime/VisualStyleGameplayBootstrap.cs
ScaryParty/Assets/Piaaria/Scripts/Weapons/WeaponBase.cs
ScaryParty/Assets/Piaaria/Scripts/Weapons/WeaponController.cs
ScaryParty/Assets/Piaaria/Scripts/Weapons/WeaponInputHandler.cs
ScaryParty/Assets/Piaaria/Tests/Editor/OvernightExperienceTests.cs
ScaryParty/Assets/Piaaria/Tests/Editor/P2_OrderUnitAndEmployeeTests.cs
ScaryParty/Assets/Piaaria/Tests/Editor/P2_OrderUnitAndEmployeeTests.cs.meta
ScaryParty/Assets/Piaaria/Tests/Editor/P3_ConfigurableGoalsAndRunResetTests.cs
ScaryParty/Assets/Piaaria/Tests/Editor/P3_ConfigurableGoalsAndRunResetTests.cs.meta
ScaryParty/Assets/Piaaria/Tests/Editor/P4_SupplyAndUiDomainTests.cs
ScaryParty/Assets/Piaaria/Tests/Editor/P4_SupplyAndUiDomainTests.cs.meta
ScaryParty/Assets/Piaaria/UI/Piaria/Config/NightIntermissionIconCatalog.asset
ScaryParty/Assets/Piaaria/UI/Piaria/Config/NightIntermissionIconCatalog.asset.meta
ScaryParty/Assets/Piaaria/UI/Piaria/Config/NightIntermissionTheme.asset
ScaryParty/Assets/Piaaria/UI/Piaria/Config/NightIntermissionTheme.asset.meta
ScaryParty/Assets/Piaaria/UI/Piaria/Config/PiariaUIAssetCatalog.asset
ScaryParty/Assets/Piaaria/UI/Piaria/Scripts/Runtime/Core/NightIntermissionFeelBridge.cs
ScaryParty/Assets/Piaaria/UI/Piaria/Scripts/Runtime/Core/NightIntermissionFeelBridge.cs.meta
ScaryParty/Assets/Piaaria/UI/Piaria/Scripts/Runtime/Core/NightIntermissionIconCatalog.cs
ScaryParty/Assets/Piaaria/UI/Piaria/Scripts/Runtime/Core/NightIntermissionIconCatalog.cs.meta
ScaryParty/Assets/Piaaria/UI/Piaria/Scripts/Runtime/Core/NightIntermissionTheme.cs
ScaryParty/Assets/Piaaria/UI/Piaria/Scripts/Runtime/Core/NightIntermissionTheme.cs.meta
ScaryParty/Assets/Piaaria/UI/Piaria/Scripts/Runtime/Core/PiariaUIAssetCatalog.cs
ScaryParty/Assets/Piaaria/UI/Piaria/Scripts/Runtime/Core/UIScreenRouter.cs
ScaryParty/Assets/Piaaria/UI/Piaria/Scripts/Runtime/Core/UIToolkitTweenService.cs
ScaryParty/Assets/Piaaria/UI/Piaria/Scripts/Runtime/Exceptions/MinimapPresenter.cs
ScaryParty/Assets/Piaaria/UI/Piaria/Scripts/Runtime/Presenters/HudPresenter.cs
ScaryParty/Assets/Piaaria/UI/Piaria/Scripts/Runtime/Presenters/LifeAndSpectatorOverlayPresenter.cs
ScaryParty/Assets/Piaaria/UI/Piaria/Scripts/Runtime/Presenters/LifeAndSpectatorOverlayPresenter.cs.meta
ScaryParty/Assets/Piaaria/UI/Piaria/Scripts/Runtime/Presenters/LobbyPresenter.cs
ScaryParty/Assets/Piaaria/UI/Piaria/Scripts/Runtime/Presenters/LotteryRevealPresenter.cs
ScaryParty/Assets/Piaaria/UI/Piaria/Scripts/Runtime/Presenters/LotteryRevealPresenter.cs.meta
ScaryParty/Assets/Piaaria/UI/Piaria/Scripts/Runtime/Presenters/ModifierVotePagePresenter.cs
ScaryParty/Assets/Piaaria/UI/Piaria/Scripts/Runtime/Presenters/ModifierVotePagePresenter.cs.meta
ScaryParty/Assets/Piaaria/UI/Piaria/Scripts/Runtime/Presenters/NightIntermissionPresenter.cs
ScaryParty/Assets/Piaaria/UI/Piaria/Scripts/Runtime/Presenters/NightIntermissionPresenter.cs.meta
ScaryParty/Assets/Piaaria/UI/Piaria/Scripts/Runtime/Presenters/NightSummaryPagePresenter.cs
ScaryParty/Assets/Piaaria/UI/Piaria/Scripts/Runtime/Presenters/NightSummaryPagePresenter.cs.meta
ScaryParty/Assets/Piaaria/UI/Piaria/Scripts/Runtime/Presenters/OperationsHubPresenter.cs
ScaryParty/Assets/Piaaria/UI/Piaria/Scripts/Runtime/Presenters/OperationsHubPresenter.cs.meta
ScaryParty/Assets/Piaaria/UI/Piaria/Scripts/Runtime/Presenters/OperationsStatusPresenter.cs
ScaryParty/Assets/Piaaria/UI/Piaria/Scripts/Runtime/Presenters/OperationsStatusPresenter.cs.meta
ScaryParty/Assets/Piaaria/UI/Piaria/Scripts/Runtime/Presenters/PauseEscapeMenuPresenter.cs
ScaryParty/Assets/Piaaria/UI/Piaria/Scripts/Runtime/Presenters/PlaytestDiagnosticsPresenter.cs
ScaryParty/Assets/Piaaria/UI/Piaria/Scripts/Runtime/Presenters/PlaytestDiagnosticsPresenter.cs.meta
ScaryParty/Assets/Piaaria/UI/Piaria/Scripts/Runtime/Presenters/ReadyRoomPresenter.cs
ScaryParty/Assets/Piaaria/UI/Piaria/Scripts/Runtime/Presenters/RunShopPagePresenter.cs
ScaryParty/Assets/Piaaria/UI/Piaria/Scripts/Runtime/Presenters/RunShopPagePresenter.cs.meta
ScaryParty/Assets/Piaaria/UI/Piaria/Scripts/Runtime/Presenters/ScoreboardPresenter.cs
ScaryParty/Assets/Piaaria/UI/Piaria/Scripts/Runtime/Presenters/StagingPresenter.cs
ScaryParty/Assets/Piaaria/UI/Piaria/Scripts/Runtime/Presenters/StoragePresenter.cs
ScaryParty/Assets/Piaaria/UI/Piaria/Scripts/Runtime/Presenters/ToastPresenter.cs
ScaryParty/Assets/Piaaria/UI/Piaria/USS/NightIntermission.uss
ScaryParty/Assets/Piaaria/UI/Piaria/USS/NightIntermission.uss.meta
ScaryParty/Assets/Piaaria/UI/Piaria/USS/NightModifierCards.uss
ScaryParty/Assets/Piaaria/UI/Piaria/USS/NightModifierCards.uss.meta
ScaryParty/Assets/Piaaria/UI/Piaria/USS/OperationsStatus.uss
ScaryParty/Assets/Piaaria/UI/Piaria/USS/OperationsStatus.uss.meta
ScaryParty/Assets/Piaaria/UI/Piaria/UXML/LotteryRevealPage.uxml
ScaryParty/Assets/Piaaria/UI/Piaria/UXML/LotteryRevealPage.uxml.meta
ScaryParty/Assets/Piaaria/UI/Piaria/UXML/ModifierVotePage.uxml
ScaryParty/Assets/Piaaria/UI/Piaria/UXML/ModifierVotePage.uxml.meta
ScaryParty/Assets/Piaaria/UI/Piaria/UXML/NightIntermissionScreen.uxml
ScaryParty/Assets/Piaaria/UI/Piaria/UXML/NightIntermissionScreen.uxml.meta
ScaryParty/Assets/Piaaria/UI/Piaria/UXML/NightSummaryPage.uxml
ScaryParty/Assets/Piaaria/UI/Piaria/UXML/NightSummaryPage.uxml.meta
ScaryParty/Assets/Piaaria/UI/Piaria/UXML/OperationsStatusOverlay.uxml
ScaryParty/Assets/Piaaria/UI/Piaria/UXML/OperationsStatusOverlay.uxml.meta
ScaryParty/Assets/Piaaria/UI/Piaria/UXML/ShopPreparationPage.uxml
ScaryParty/Assets/Piaaria/UI/Piaria/UXML/ShopPreparationPage.uxml.meta
ScaryParty/Assets/ThirdParty/CoInspector/Editor/Settings/UserData.asset
ScaryParty/Assets/ThirdParty/StarterAssets/ThirdPersonController/Prefabs/PlayerArmature.prefab
ScaryParty/Assets/ThirdParty/StarterAssets/ThirdPersonController/Prefabs/PlayerArmature_Preview.prefab
ScaryParty/Assets/ThirdParty/StarterAssets/ThirdPersonController/Scenes/GameScene.unity
ScaryParty/CityGenerationLog.json
ScaryParty/Docs/CHANGELOG.md
ScaryParty/Docs/CURRENT_STATE.md
ScaryParty/Docs/Release/COMANDOS_ANTIGRAVITY.md
ScaryParty/Docs/Release/CONTRATO_EXECUCAO.md
"ScaryParty/Docs/Release/Orienta\303\247\303\265es aos estudantes_Gerais_35\302\272 Encontro de IC_2026_17 e 18 de novembro de 2026.pdf"
ScaryParty/Docs/Release/PLANO_PREPARACAO_NOITES_2_1_UI_TOOLKIT_FEEL_DOTWEEN.md
ScaryParty/Docs/Release/Plano_Correcoes_UI_Placeholders_Playtest_ScaryParty.md
ScaryParty/Docs/Release/Plano_Recuperacao_Integracao_M1_M8_ScaryParty.md
ScaryParty/Docs/Release/Plano_Wizard_Eventos_Loja_Balanceamento_ScaryParty.md
ScaryParty/Docs/Release/Plano_Wizard_Eventos_Loja_Balanceamento_ScaryParty_Atualizado.md
ScaryParty/Docs/Release/Prompts/M1_Auditoria.md
ScaryParty/Docs/Release/Prompts/M2_Cidade_Navegacao.md
ScaryParty/Docs/Release/Prompts/M3_Maos_Armas_Moto.md
ScaryParty/Docs/Release/Prompts/M4_Noite_Completa.md
ScaryParty/Docs/Release/Prompts/M5_Monstros.md
ScaryParty/Docs/Release/Prompts/M6_Run_Votos_Loja.md
ScaryParty/Docs/Release/Prompts/M7_Funcionarios.md
ScaryParty/Docs/Release/Prompts/M8_Entrega_Final.md
ScaryParty/Docs/Release/RECUPERACAO_M1_M8.md
ScaryParty/Docs/Release/ROADMAP.md
ScaryParty/Docs/Release/Reports/B0_INVENTARIO_MIGRACAO.md
ScaryParty/Docs/Release/Reports/Evidence/B6_TEST_SUMMARY.md
ScaryParty/Docs/Release/Reports/Evidence/Unity_EditMode_Suite_Summary.json
ScaryParty/Docs/Release/Reports/Evidence/Unity_EditMode_Summary.json
ScaryParty/Docs/Release/Reports/Evidence/Unity_PlayMode_Summary.json
ScaryParty/Docs/Release/Reports/M1_Relatorio.md
ScaryParty/Docs/Release/Reports/M2_Relatorio.md
ScaryParty/Docs/Release/Reports/M3_Relatorio.md
ScaryParty/Docs/Release/Reports/M4_Relatorio.md
ScaryParty/Docs/Release/Reports/M5_Relatorio.md
ScaryParty/Docs/Release/Reports/M6_Relatorio.md
ScaryParty/Docs/Release/Reports/M7_Relatorio.md
ScaryParty/Docs/Release/Reports/M8_Relatorio.md
ScaryParty/Docs/Release/Reports/N2_01_AUDITORIA_E_CONTRATO_INTERMISSAO.md
ScaryParty/Docs/Release/Reports/P0_INVENTARIO_PLAYTEST.md
ScaryParty/Docs/Release/Reports/PLAYTEST_UI_PLACEHOLDERS.md
ScaryParty/Docs/Release/Reports/RECUPERACAO_M1_M8.md
ScaryParty/Docs/Release/Reports/WIZARD_EVENTOS_LOJA_BALANCEAMENTO.md
ScaryParty/Docs/Release/STATUS.md
ScaryParty/Docs/Release/TEST_MATRIX.md
ScaryParty/Packages/manifest.json
ScaryParty/Packages/packages-lock.json
ScaryParty/ProjectSettings/Packages/com.unity.services.core/Settings.json
ScaryParty/ProjectSettings/ProjectSettings.asset
ScaryParty/ScaryParty.slnx
~~~

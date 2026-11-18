# ScaryParty — Correções de integração e cenas prontas para playtest

## Objetivo e referência

Entregar um ciclo jogável de ponta a ponta, acessível por interfaces e objetos já configurados nas cenas. Corrigir as falhas da auditoria e permitir testar sem adicionar componentes, preencher referências ou executar métodos pelo Inspector durante a partida.

Base da última auditoria: branch `release/marcos-noturnos`, commit `bdffe5096163c0bac817a94a1c05e4d22fdf4652`. Antes de implementar, conferir o HEAD atual e preservar correções posteriores. Este documento é um plano de execução; não afirma que as correções ou testes abaixo já foram realizados.

Regras do jogo: RUN de três noites no preset de entrega, 1–4 humanos, demonstração com dois; faturamento separado do saldo; dinheiro coletivo; votos entre alternativas e prontidão coletiva; nova RUN limpa progressão; monstros também podem ser clientes; funcionários usam recursos reais. Moto ocupa uma mão, pistola pode usar a outra, espingarda exige duas mãos e bandoleira libera ambas. Comunicação por voz é externa.

## Contrato de entrega

Uma funcionalidade só está entregue quando tiver lógica conectada, entrada acessível, representação visual, feedback e estado replicado quando necessário. Um script, botão sem ação ou gerador que ainda precisa ser executado não atende ao objetivo.

- Reaproveitar UI, modelos, materiais, prefabs, Animators e serviços existentes. Placeholder somente para lacunas reais; preservar arte da equipe.
- UI de tela com UI Toolkit. Quadro físico de comandas e mapa podem conservar Canvas/uGUI quando já forem funcionais. Não fazer migração gratuita.
- Persistir configuração em cenas, prefabs e assets com APIs do Editor, Undo e validação. Não editar YAML manualmente nem recriar GUIDs.
- Objetos de cidade procedural precisam estar ligados ao pipeline de geração; não salvar apenas objetos temporários do PlayMode.
- Separar visual substituível, colisão/interação e identidade lógica. Trocar uma mesh depois não pode quebrar IDs ou comportamento.
- Preservar trabalho local e overrides. Sem reset destrutivo, push ou merge. Um commit por fase coesa; não incluir alterações alheias.
- Investigar os achados no código atual antes de corrigir. Registrar os que já tiverem sido corrigidos.

## P0 — Inventário, cena de playtest e instalação persistente

1. Ler AGENTS, INDEX, CURRENT_STATE e relatório de recuperação. Registrar branch, SHA, Unity em ProjectVersion.txt, cenas de entrada/lobby/gameplay e prefabs realmente usados.
2. Inventariar cada funcionalidade deste plano: serviço, entrada de UI/interação, prefab, cena, configuração, representação host/cliente e lacuna. Classificar existente, parcial, ausente ou não verificado.
3. Usar a cena canônica como destino da integração. Se precisar de uma variante de testes, criá-la com referências compartilhadas e nome claro, deixando o caminho de entrada disponível. Não entregar apenas uma cena isolada que não participa do fluxo menu → lobby → gameplay.
4. Criar ou ampliar um instalador de Editor idempotente, por exemplo `Tools/Scary Party/Playtest/Preparar e validar`. Ele cria somente o que falta, preserva configuração manual e informa o que alterou. Executá-lo nesta tarefa, salvar e validar o resultado; não deixá-lo como trabalho futuro para o usuário.
5. Organizar novos assets sob uma pasta identificável de playtest. Geradores devem produzir assets persistentes e manifesto de referências. Rodar o instalador duas vezes não duplica UI, estações, pontos de spawn nem entidades.
6. Validar Missing Scripts, referências obrigatórias, IDs, layers, máscaras, colisores, anchors, Animator, PanelSettings e registros de prefabs de rede quando aplicáveis.

Aceite: abrir a cena indicada e iniciar a sessão sem montagem manual. O relatório informa os caminhos exatos das cenas e assets.

## P1 — Correção dos monstros nos clientes

Achado em `MonsterPopulationDirector` e `PizzeriaRoot`:
- `EnsureOn(this)` é chamado dentro da inicialização exclusiva do servidor.
- `Update()` exige `DomainState != null` antes de alcançar o caminho do cliente, embora esse domínio exista somente no servidor.

Ações:
1. Instalar o diretor nos participantes necessários por um ponto comum do ciclo de rede.
2. Separar inicialização/apresentação cliente da simulação servidor. Cliente depende de estado replicado e catálogo visual, não de DomainState.
3. Manter IA, dano e decisões no servidor. Não criar um domínio artificial no cliente para contornar a falha.
4. Assinar listas quando o estado de rede estiver disponível, consumir snapshot inicial e remover assinaturas no despawn/destruição. Suportar ordem variável de inicialização.
5. Garantir um corpo por ID em cada instância, sem duplicação no host; interpolar movimento e mostrar postura, dano e morte.
6. Limpar entidades em mudança de noite, nova RUN e saída da sessão. Evitar callbacks antigos recriando corpos.
7. Aplicar as mesmas verificações à representação dos funcionários.

Teste obrigatório: cliente remoto com DomainState nulo recebe monstros já existentes e os criados depois; observa movimento e remoção. Incluir entrada tardia apenas se permitida pela sessão; respeitar espectador durante noite ativa.

## P2 — Funcionários sem resultados forçados ou contagem duplicada

### Processamento e montagem

Achados em `EmployeeAgentController`: falha em CompleteWork prepara massa diretamente; falha na montagem adiciona ingrediente e retorna sucesso.

1. Remover os atalhos que transformam falha em sucesso. Nunca marcar ingrediente/preparo correto quando a operação foi rejeitada.
2. Executar os serviços reais ao longo dos seus tempos e estados. Não chamar início e conclusão imediatamente para simular uma operação temporizada.
3. Validar resultado de cada transferência, processamento e montagem antes da próxima ação.
4. Reservar bancada, slot e ferramenta necessários. Respeitar ferramentas seguradas por humanos, distância e capacidade real das estações.
5. Falha deve preservar ou devolver o ingrediente para uma posição válida. Evitar consumo repetido de estoque a cada tentativa e vazamento de itens na mão virtual do funcionário.
6. Registrar motivo de bloqueio e tentar novamente com intervalo controlado, sem log por frame.
7. Cancelamento, fim de pedido, morte e intervenção humana liberam operações/reservas sem apagar recursos indevidamente.
8. Erro proposital de habilidade ocorre em etapa explicitamente definida. Falha técnica nunca é contabilizada como erro de habilidade.

### Identidade de cada pizza produzida

Achado: `CountReadyMatchingPizzasForLine()` conta pizzas compatíveis na bancada sem vínculo exclusivo a uma unidade de trabalho.

1. Representar responsabilidade por unidade com identidade equivalente a RUN/geração + pedido + linha + índice da unidade.
2. Associar o ItemId real a no máximo uma unidade ativa. Fazer atribuição/reserva atomicamente no servidor.
3. Uma pizza humana compatível pode ser assumida por uma tarefa, mas não satisfazer simultaneamente outra. Associação de trabalho não precisa impedir o jogador de entregar uma pizza em outro pedido; nesse caso, reconciliar e liberar a tarefa original.
4. Considerar pizza removida da bancada, levada em mochila, entregue, destruída ou descartada; nunca usar apenas a quantidade visível como prova de conclusão.
5. Preservar progresso em intervenção humana e retomada. Repetir sincronização não avança unidades novamente.
6. Pedidos cancelados/expirados liberam vínculos; nova RUN invalida IDs antigos.

Testes: dois pedidos iguais e uma pizza pronta; linha com três unidades; duas receitas diferentes; dois funcionários concorrentes; humano retira ou entrega pizza atribuída; ingrediente indisponível; ferramenta ocupada; falha de montagem; cancelamento no meio do preparo. Verificar estoque, itens, operações e reservas antes/depois.

## P3 — Metas configuráveis e inicialização consistente

Achado: FreezeNightStartParameters sobrescreve metas do preset com ComputeTabulatedGoal fixo, inclusive ao reiniciar tutorial. Startup e reinício têm caminhos diferentes de escala.

1. Definir uma única resolução de parâmetros efetivos, usada no startup, nova RUN, próxima noite e saída do tutorial.
2. Ler metas por noite e 1/2/3/4 humanos de configuração editável. Fórmula pode servir como fallback explícito, nunca substituir silenciosamente a tabela.
3. Congelar a quantidade de humanos no começo da noite; funcionários não contam, morte/desconexão não reduzem meta em andamento.
4. Noite 0 usa duração/meta e política de derrota próprias, sem herdar fórmula da Noite 1. Tutorial não consome uma das três noites.
5. Evitar aplicar modificadores duas vezes. Exibir base, escala e valor final no painel de desenvolvimento.
6. Garantir que nova RUN reponha a infraestrutura da cozinha e limpe progressão; entre noites, preservar os recursos definidos no roadmap.

Testes: alterar meta para um valor distinto (ex.: 137) e verificar aplicação; tutorial configurado em 15 permanece em 15 após restart; comparar startup/restart com dois humanos; iniciar Noite 1 após tutorial; repetir nova RUN na mesma sessão.

## P4 — UI provisória completa, aplicada e navegável

Reaproveitar presenters e documentos existentes. Textos em português, contraste legível, hierarquia simples e componentes reutilizáveis. Valores vêm do estado real; não preencher telas com números fictícios. Testar 1280×720 e 1920×1080, textos longos e listas com scroll.

| Interface | Conteúdo e ações mínimas | Conexão necessária |
|---|---|---|
| Entrada/lobby | Criar/entrar pelos fluxos existentes, participantes, preset/dificuldade/seed quando previstos, tutorial opcional | Sessão real e configuração do host |
| HUD | Noite, relógio, faturamento/meta, saldo, reputação, vida, participantes, duas mãos, munição, bandoleira e mochila | Estado autoritativo e inventário local correto |
| Interação | Nome, ação, tecla, progresso e motivo de rejeição | Mesmo alcance/estado aceito pelo servidor |
| Pedidos | Quadro, assumir/liberar, consultar receita/endereço, prazo de cada pedido assumido sempre visível | Comandas reais; não mostrar todas as receitas permanentemente |
| Reposição | Ingredientes, estoque, quantidade, preço total, comprar, encomendas e ETA | Saldo coletivo, pedido de suprimento e chegada real |
| Preparação da noite | Alternativas sorteadas, descrição, duração, votos, empate, resultado e prontidão de cada participante | Votos/ready sincronizados e início somente quando válido |
| Loja | Upgrades, armas/conteúdo já suportado e funcionários; custo, nível, disponibilidade e resultado | Compra atômica e impacto efetivo no jogo |
| Funcionários | Função, pedido/unidade atual, habilidade, bloqueio, ações de gestão já previstas | Tarefas e reservas reais |
| Tutorial | Guia lateral recolhível, objetivos e ir para Noite 1 quando autorizado | Noite 0 real e permissões claras de host |
| Resultados | Receita/meta, gastos separados, reputação, sucesso/causa da derrota, continuar ou nova RUN/menu | Transição verdadeira, sem retry de noite perdido no fluxo de produção |
| Morte/espectador | Estado, ajuda disponível, progresso do resgate e troca de jogador observado | Estados de vida reais |
| Mapa | Cidade, pizzaria, jogadores, endereços e rotas pertinentes, zoom/pan | Geração e pedidos atuais |
| Pausa/loading | Fechar, voltar, confirmação de saída, estado de carregamento/erro | Modal e ciclo de sessão existentes |

Requisitos transversais:
- Criar caminhos normais para abrir loja, reposição, pedidos e mapa; botão visível ou interação/atalho explicado. Nenhuma ação essencial escondida apenas em debug.
- Listas vazias, carregamento, saldo insuficiente, voto pendente e erro têm feedback legível.
- Cliques na UI não disparam armas. Abrir/fechar modal restaura cursor/câmera corretamente, respeita pilha e exige novo clique de tiro.
- Evitar listeners duplicados em reabertura e nova RUN.
- Host e cliente podem realizar as ações permitidas; controles exclusivos do host indicam a restrição.
- Feedback de compra mostra confirmação ou rejeição do servidor; não aparentar sucesso antes da resposta.
- Ícones provisórios podem ser formas simples acompanhadas de texto. Não depender apenas de cor.

Aceite: percorrer o fluxo pelas telas, incluindo compras simultâneas sem dinheiro suficiente para ambas, voto empatado e desconexão na preparação.

## P5 — Objetos 3D provisórios instalados

Antes de criar, procurar asset correspondente. Reutilizar a moto real e o personagem existente; criar variantes visuais leves onde faltar conteúdo. Não substituir a pizzaria ou cidade inteira.

| Objeto/conjunto | Montagem necessária |
|---|---|
| Monstros | Variantes configuráveis por espécie, modelo ou geometria simples identificável, collider, vida, movimento e feedback de agressividade; prefab/catálogo utilizado pelo spawn real |
| Funcionários | Balconista e pizzaiolo distinguíveis por acessório/cor, rig existente quando disponível e pontos de spawn navegáveis |
| Armas | Pistola de uma mão e espingarda de duas mãos, pickup/rack acessível, pivôs de empunhadura/mira/disparo e anchor da bandoleira |
| Moto | Modelo existente ligado ao controlador, assento/guidão/saída e colisores corretos; posição inicial acessível |
| Cozinha | Apenas completar lacunas de bancadas, forno, estoque, utensílios, telefone, quadro, lixo e embalagem; slots reais visíveis em debug |
| Ingredientes/pizzas | Representações distinguíveis para itens sem modelo e estados cru/preparado/assado/queimado/embalado; ligação ao catálogo e posição nas mãos/bancadas |
| Mochilas | Modelo existente ou substituto, vestir/retirar/inserir pizza, capacidade e dano visíveis, anchor de costas |
| Atendimento e entrega | Filas, mesas/cadeiras, ponto de atendimento, porta/endereço e posição de consumo/saída ligados à navegação |
| Suprimentos e loja | Ponto físico/interação para abrir interface, chegada de suprimentos representada conforme a regra implementada |
| Cidade/ameaças | Anchors de telhado/descida, trajetos e áreas elegíveis; integrar à geração procedural e validar conectividade |

Todos os objetos devem ter escala e posição coerentes, materiais compatíveis com o render pipeline, colisores sem bloquear caminhos indevidamente e referências preenchidas. Não adicionar labels de espécie que revelem automaticamente o perigo no modo normal; identificação técnica fica no debug.

Persistência: salvar prefabs/configs; registrar no gerador/catálogo/NetworkManager conforme a arquitetura. Para proxies visuais, não adicionar NetworkObject desnecessário. Para objetos spawnados pelo Netcode, validar registro e identidade.

Adicionar gizmos/preview de anchors no Editor. Bone e extremidade usam anchor explícito/offset configurável, sem presumir que o tail do Blender existe no Transform importado. A troca futura do visual preserva collider, scripts e referências.

Aceite: sem objetos invisíveis/pink, pickups inacessíveis ou animação obrigatória sem controller. Fechar/reabrir a cena e regenerar cidade preserva a montagem.

## P6 — Preset e painel de diagnóstico para playtest

Criar/reaproveitar presets `Entrega_3Noites`, `Playtest_Integracao` e `Tutorial_Noite0`. O preset rápido serve somente para testar; não alterar silenciosamente a duração de entrega de 30–45 minutos por RUN.

Painel de desenvolvimento recolhível, exclusivo do host para mutações, desativado no preset de entrega:
- Exibir SHA/build quando disponível, seed, fase, participantes congelados, metas e modificadores efetivos.
- Gerar pedido de duas receitas e pedido com quantidade três; criar cliente/monstro de espécie escolhida; contratar funcionário; adicionar recursos de teste; aplicar dano controlado; acelerar/encerrar relógio; iniciar nova RUN.
- Chamar os mesmos serviços/comandos reais, com flags explícitas de teste. Não fabricar aprovações nem alterar estado apenas no cliente.
- Mostrar contagens de entidades/proxies, operações, reservas e vínculos pizza–unidade para diagnosticar duplicação.
- Registrar que comandos de debug foram usados. Vitórias aceleradas não contam como playtest completo.

## P7 — Validação real na Unity e relatório

O usuário informou que instalou Unity CLI para testes. Nesta tarefa, executar verificações direcionadas de compilação, EditMode e PlayMode usando a ferramenta disponível, respeitando as capacidades reais dela. Não assumir sintaxe, licença ou suporte multiplayer: inspecionar ajuda/configuração primeiro. Esta orientação autoriza os testes desta tarefa; não alterar a política global do projeto para execução irrestrita.

Se precisar abrir outra instância, evitar conflito com o projeto aberto. Não matar o Editor, apagar locks ou salvar cenas em execução sem controle. Usar mecanismo suportado ou clone de teste isolado. Não iniciar loops de builds ou repetir bateria sem mudança relevante.

1. Compilar/importar no ambiente Unity correto; registrar erros de compilação, importação, referências e Console.
2. Executar testes de domínio pertinentes e testes de integração que exercitem os fluxos corrigidos. Não testar apenas contadores chamados manualmente.
3. PlayMode: startup cliente sem DomainState; falha de processamento sem transformação; disputa por uma pizza pronta; meta configurada mantida após restart; reconstrução da cozinha.
4. Host + cliente real, em processos separados ou mecanismo de integração equivalente: entidades visíveis, compras, votos, mãos, pedidos e transições. Dois objetos simulados num único domínio não provam multiplayer remoto.
5. Validar montagem/UI visualmente com capturas quando a ferramenta permitir. Sucesso headless não comprova renderização, legibilidade, posição da câmera ou sensação de controle.
6. Executar a matriz abaixo. Se a ferramenta não conseguir algum caso, marcar NÃO EXECUTADO com motivo e passos manuais exatos; continuar os demais.

| Caso | Resultado esperado |
|---|---|
| V01 Abrir cena/sessão novamente | UI e objetos montados, referências persistidas |
| V02 Host + cliente e spawn de monstros/funcionários | Um corpo por entidade em cada instância, movimento e remoção consistentes |
| V03 Cliente recebe população pré-existente | Snapshot produz representação correta |
| V04 Jogador faz pizza do início à entrega | Ingredientes, preparo, forno, embalagem, mochila e pagamento reais |
| V05 Funcionário faz duas receitas/quantidade três | Unidades distintas e consumo correto |
| V06 Dois pedidos iguais, uma pizza na bancada | Somente uma unidade pode assumir aquela pizza |
| V07 Ferramenta ocupada/operação rejeitada | Sem sucesso forçado, consumo duplicado ou item perdido |
| V08 Humano assume trabalho do funcionário | Reservas e progresso reconciliados |
| V09 Meta customizada e tutorial após restart | Valores configurados preservados |
| V10 Compra/reposição/votos pela UI | Estado sincronizado, estoque chega, escolhas aplicadas uma vez |
| V11 Moto+pistola, atropelamento, espingarda/bandoleira | Regras de mãos, dano único e queda preservadas |
| V12 Dano/resgate/morte/espectador | Interrupção zera progresso; retorno na noite seguinte conforme regra |
| V13 Monstro cliente muda para agressivo | Atendimento/mesa liberados, dano e reputação coerentes |
| V14 Três noites e nova RUN na mesma sessão | Resultado correto; cozinha funcional; nenhuma persistência indevida |
| V15 UI em duas resoluções e troca de modais | Sem cortes impeditivos, tiro acidental ou cursor preso |
| V16 Cinco seeds e trânsito | Spawns acessíveis; NPCs respeitam barreiras; carros circulam |

Evidências: salvar XML original dos testes, logs relevantes, comandos/versão da ferramenta, duração, exit code, SHA e estado local testado. Relatório diferencia testes .NET, Unity EditMode, Unity PlayMode, multiplayer e inspeção visual. Não copiar Library/Temp, binários grandes ou dados sensíveis para o Git. Preservar evidência original local e incluir no commit arquivos de diagnóstico adequados e pequenos.

## Ordem, commits e conclusão

Executar P0 → P1 → P2 → P3 → P4 → P5 → P6 → P7. UI e 3D devem integrar os sistemas corrigidos. Problemas bloqueadores descobertos nos testes entram na fase correspondente; validar novamente somente os afetados e regressões relevantes.

Relatório final sugerido: `Docs/Release/Reports/PLAYTEST_UI_PLACEHOLDERS.md`, contendo:
- SHA inicial/final e commits por fase;
- achados confirmados/corrigidos e pendências;
- cenas exatas para abrir e fluxo para iniciar sessão;
- mapa funcionalidade → UI/interação → prefab → configuração → teste;
- placeholders criados e referência a trocar quando chegar o asset final;
- testes executados, falhas e não executados com evidência;
- roteiro curto de playtest para Rubens.

Atualizar CURRENT_STATE, CHANGELOG e matriz de testes sem declarar aprovado o que não foi verificado. Não concluir apenas com “rode o wizard”: executar a instalação, salvar a configuração e provar que permanece após reabrir. Se o ambiente bloquear a instalação ou teste, declarar precisamente a parte não entregue.

## Prompt de execução

Leia este arquivo integralmente e os AGENTS.md aplicáveis. Execute P0–P7, corrigindo as falhas e entregando as interfaces e objetos provisórios já aplicados às cenas/prefabs reais. Confirme os achados no HEAD atual, preserve trabalho existente e reaproveite os sistemas. Não pare na elaboração de outro plano nem entregue apenas scripts/geradores sem aplicar. Faça commits separados por fase. Use a Unity CLI instalada para os testes direcionados autorizados neste documento, inspecionando suas capacidades antes; não faça loops de execução. Registre XML/logs e diferencie domínio, Unity, multiplayer e validação visual. Continue o trabalho independente quando uma ferramenta bloquear algum teste, e documente exatamente o que não foi executado. Ao terminar, informe qual cena abrir, como acessar cada funcionalidade, quais placeholders foram instalados e o resultado real dos testes. Não faça push ou merge.

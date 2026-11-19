# ScaryParty — Wizard de Eventos, Loja e Balanceamento

## Missão para o Antigravity

Execute este plano integralmente no projeto existente. Entregue um wizard Odin para editar preços, porcentagens, pools, duração e progressão das escolhas sem alterar código, com as configurações ligadas aos sistemas reais, às interfaces e às cenas. Implemente a preparação antes da primeira noite e corrija os efeitos incompletos. Não pare na criação de outro plano ou de classes sem integração.

Referência inspecionada: `release/marcos-noturnos`, commit `c616312`, em 18/11/2026. Confira o HEAD atual antes de alterar: achados podem já ter sido corrigidos. Leia AGENTS.md, INDEX, CURRENT_STATE e relatórios pertinentes. Preserve modificações locais, GUIDs, prefabs, materiais e overrides. Faça commits por fase; sem push, merge ou reset destrutivo.

Este documento define implementação futura. Os valores iniciais são os encontrados na auditoria; os parâmetros novos explicitamente propostos abaixo são defaults de playtest, não decisões imutáveis de balanceamento.

## 1. Resultado esperado para Rubens

Abrir `Tools/Scary Party/Wizards/Pizzaria/Eventos, Loja e Balanceamento` e conseguir:
- Alterar o preço do timer do forno, contratação, promoção e suprimentos.
- Alterar percentuais de demanda, vampiros, treinamento e prazo de entrega.
- Configurar duração temporária/permanente, limites de níveis, elegibilidade e pesos.
- Definir quantas votações e alternativas existem antes de cada noite.
- Separar benefícios, entraves obrigatórios e eventos.
- Editar presets de playtest e entrega, comparando valores efetivos.
- Ver uma prévia do card e do efeito, localizar onde o asset é usado e validar erros.
- Criar novos itens combinando efeitos já suportados sem escrever código. Um efeito de gameplay completamente novo ainda pode exigir um handler novo; o wizard deve dizer isso claramente.

Ao mudar um preço, o mesmo valor deve aparecer no wizard, UI do jogo e cobrança do servidor. Ao mudar uma porcentagem, a descrição e o efeito devem concordar. Ao iniciar uma RUN, o grupo deve passar pela preparação antes da Noite 1.

## 2. Situação atual e falhas a resolver

| Área | Evidência da auditoria | Ação |
|---|---|---|
| Eventos | Sete definições dentro de `RunProgressionService.RegisterDefaultCatalog()` | Migrar para catálogo editável preservando IDs |
| Compras | Switch com preços em `PizzeriaCommandHandler.PurchaseRunShopItemServerRpc()` | Resolver ofertas por ID no catálogo do servidor |
| UI | Textos/preços repetidos em `RunPreparationPresenter` e `OperationsHubPresenter` | Renderizar ofertas efetivas, sem preços literais duplicados |
| Funcionários | Preço base 180/120 e promoção em `EmployeeTaskService` | Perfil editável por função/nível |
| Suprimentos | “Base +5” usa IDs 1–3; queijo cru padrão é 4 | Referências explícitas a ingredientes do catálogo |
| Ataque dos vampiros | Multiplicador calculado sem consumidor identificado na IA | Aplicar a parâmetros concretos de comportamento |
| Movimento intenso | Multiplicador calculado sem consumidor identificado na demanda | Aplicar à geração real de pedidos |
| Bolsa reforçada | Descrição promete resistência; efeito confirmado é capacidade | Separar capacidade e resistência; não anunciar efeito inexistente |
| Preparação | Sorteio aberto apenas entre noites, com duas opções e um vencedor | Reutilizar fluxo antes da Noite 1 e suportar etapas por categoria |
| Conteúdo existente | `SupplyItemDef`, `UpgradeDef`, `PizzeriaConfig`, `NightConfig`, `PizzeriaRunPreset`, receita e traits já existem | Integrar e reaproveitar; evitar segundo sistema de configuração |

Não assumir que todos os campos existentes já são consumidos. Rastrear origem → cálculo → consumidor → UI → teste.

## 3. Regras de produto e defaults propostos

Regras preservadas:
- Caixa coletivo; qualquer jogador compra itens permitidos, sem votação para cada compra.
- Alternativas sorteadas são apresentadas ao grupo; empate decidido no servidor.
- Escolhas e anúncios acontecem antes da noite. Não introduzir novos sorteios surpresa durante o expediente.
- Modificadores temporários expiram após a noite aplicável; permanentes duram até o fim da RUN.
- Todas as escolhas obrigatórias devem estar resolvidas e todos os humanos conectados devem confirmar pronto antes do começo.
- Meta congela com os humanos presentes; funcionários não contam. Saldo não substitui faturamento.
- A loja existente continua disponível durante o expediente para ofertas autorizadas; disponibilidade por fase passa a ser configurável, preservando o comportamento atual na migração.

Defaults propostos para validar:
- Tutorial não recebe entraves; ao concluí-lo, abrir preparação da Noite 1.
- Uma etapa obrigatória de entrave, com duas alternativas, antes de cada noite.
- Uma etapa de oportunidade/benefício gratuito, com duas alternativas, antes de cada noite. Benefício escolhido não substitui a etapa de entrave.
- Eventos adicionais: inicialmente 0/1/1 nas noites 1/2/3. É uma tabela editável; permitir configurar três eventos na Noite 4 sem alterar código.
- “Obrigatória” significa escolher uma das penalidades oferecidas, não poder pular a categoria.
- Peso de sorteio não é porcentagem garantida de inclusão num conjunto sem reposição. Mostrar essa distinção no wizard.

Se o conteúdo elegível não sustentar duas alternativas distintas, não fabricar cópias nem converter entrave em benefício: a validação deve apontar o problema. Configurar alternativas de reserva reais. Em runtime, usar fallback elegível da mesma categoria; se nem ele existir, bloquear o início com mensagem clara e diagnóstico, sem loop infinito.

## 4. B0 — Inventário e migração segura

1. Registrar SHA, arquivos locais modificados e assets usados pelas cenas de gameplay.
2. Inventariar os sete modificadores, seis compras, promoções, suprimentos, receitas e parâmetros de RUN com origem e consumidores.
3. Identificar o caminho efetivo de configuração: preset escolhido, referências da pizzaria e overrides. Evitar que um asset editado no wizard não seja o usado no jogo.
4. Criar uma migração de Editor idempotente com preview das alterações. Preservar IDs como `blood_moon`, `oven_timer`, `reinforced_bag` etc.
5. Criar assets somente quando faltarem; uma segunda execução não sobrescreve ajustes manuais nem duplica catálogos.
6. Executar a migração e persistir referências nos assets/cenas. Não deixar esse passo para o usuário.

Aceite: relatório de antes/depois e comportamento inicial preservado, exceto correções explicitamente descritas.

## 5. B1 — Fonte única de configuração

Usar ScriptableObjects para authoring e dados puros imutáveis para domínio/runtime. Os nomes abaixo são sugestões; reaproveitar tipos existentes quando possível.

### Definição de modificador

Campos: ID estável; nome; descrição com valores derivados; ícone opcional; categoria; ativado; origem permitida (votação, loja ou ambas); duração; níveis máximos; regra de acúmulo; noite mínima/máxima; dificuldades; pré-requisitos; incompatibilidades; tags; peso; lista de efeitos tipados.

Cada efeito deve declarar alvo, operação, unidade, valor por nível, limites e handler válido. Evitar strings livres como único contrato de execução.

Operações mínimas: adicionar valor, aplicar variação percentual, multiplicar e habilitar capacidade. Definir ordem única de composição: base → adições → variações/multiplicadores na ordem documentada → limites. Recalcular a partir da base, nunca multiplicar repetidamente a cada atualização.

Editor apresenta percentuais legíveis, por exemplo 25%, e converte explicitamente para a representação do domínio. Distinguir redução relativa de probabilidade de pontos percentuais: erro base 20% com redução relativa de 25% resulta em 15%, não em -5%.

### Ofertas de loja

ID da oferta separado do ID do efeito. Campos: nome/ícone; tipo de benefício; referências de conteúdo; preço base; preço por nível ou fórmula editável; limite por RUN/noite; fases de compra; desbloqueio; efeito/quantidade; limite de estoque se houver.

A loja e a votação que concedem o mesmo modificador devem compartilhar o limite de níveis. Não permitir pagar por um efeito já maximizado. Oferta de pacote deve validar todas as partes antes de cobrar.

### Funcionários, suprimentos e presets

- Perfil por função: contratação, níveis, custo de promoção, erro base e evolução, tempos relevantes já suportados.
- Reposição continua usando `SupplyItemDef` como fonte; receita continua no asset de receita. O wizard oferece edição vinculada, não cópias divergentes.
- Pacote “Suprimentos base” referencia explicitamente ingredientes e quantidades. Capacidade cheia: bloquear compra sem benefício; mostrar quantidade efetiva se entrega parcial for permitida. Política deve ser explícita e testada.
- Preset referencia catálogo e tabela de preparação por noite. Resolver overrides em ordem clara e mostrar origem de cada valor.

Servidor cria um snapshot da configuração no começo da RUN. Edição de asset durante PlayMode não altera a sessão silenciosamente. Clientes recebem preços, descrições efetivas, opções e estado necessários; nunca enviam preço ou magnitude a aplicar. IDs estáveis não devem depender da posição de botão ou de byte fixo do switch antigo.

## 6. B2 — Wizard Odin completo

Menu sugerido: `Tools/Scary Party/Wizards/Pizzaria/Eventos, Loja e Balanceamento`.

Abas:
1. Visão geral: preset ativo, uso em cenas, erros, contagem de conteúdo, atalhos para assets reais.
2. Eventos e entraves: tabela filtrável, pesos, elegibilidade, duração e efeitos.
3. Benefícios: níveis, efeitos e disponibilidade em loja/votação.
4. Loja: preço, evolução, disponibilidade e preview de saldo após compra.
5. Funcionários: contratação/promoção, habilidade e valores por nível.
6. Economia e reposição: edição vinculada de receitas, suprimentos e pacote base.
7. Preparação por noite: número de etapas por categoria, alternativas, pools e fallbacks.
8. Simulação e validação: seed, noite, dificuldade, humanos, modificadores já ativos e orçamento.

Requisitos:
- Lista à esquerda e detalhes/preview à direita; busca, filtros por categoria e botão localizar asset.
- Criar, duplicar com novo ID, desativar e validar. Alertar referências antes de remover conteúdo.
- Undo/Redo, dirty state e salvamento explícito; nada de regravar assets a cada repaint.
- Preview usa o mesmo formatador de descrições da UI em runtime.
- Mostrar base, contribuição por nível, resultado final e limites. Exemplos numéricos tornam alterações verificáveis.
- Simular sorteio com cópia/stream independente de RNG, sem consumir a aleatoriedade da RUN ativa.
- Validador: IDs repetidos, preço negativo, NaN, porcentagem inválida, handler ausente, pré-requisito impossível, incompatibilidade, pool insuficiente e oferta sem benefício.
- Exibir cobertura de integração por efeito: consumidor, teste e último status conhecido. Não exibir “funciona” só porque existe definição.

Aceite: alterar preço do forno de 90 para 137 e redução de treinamento de 25% para 10%; salvar, fechar/reabrir wizard e iniciar nova RUN. A UI cobra 137 e erro base 20% vira 18% com um nível. Restaurar valores do preset de entrega após o teste ou manter um preset de teste separado.

## 7. B3 — Efeitos reais e catálogo inicial

Migrar os sete efeitos conhecidos preservando o comportamento confirmado:

| ID | Base inicial | Integração/aceite |
|---|---|---|
| blood_moon | +50% população de vampiros, uma noite | Consumir no diretor de população; definir arredondamento e teto de agentes |
| vampire_attack | +35% agressividade, uma noite | Proposta concreta: +35% raio de aquisição de alvo de vampiros já elegíveis à agressão; não tornar todos hostis automaticamente nem aumentar dano/velocidade implicitamente. Renomear descrição para o efeito exato |
| heavy_rush | +30% demanda, uma noite | Aumentar taxa de chegada dos pedidos em 30%: intervalo base / 1,3; definir canais afetados. Respeitar limites de fila/pedidos e evitar aumentar duas vezes |
| express_supply | -40% ETA, uma noite | Aplicar às novas encomendas; pedidos pagos mantêm ETA contratado, sem alterar retroativamente |
| oven_timer | Habilita proteção contra queimar, permanente | Consumido pelo forno; preservar cozimento correto, evitar pizza ficar presa |
| reinforced_bag | +1 capacidade por nível, máximo 2 | Capacidade é efeito obrigatório; resistência é efeito separado, só anunciar se houver implementação/teste real |
| staff_training | -25% relativo por nível sobre erro base, máximo 2 | Um nível multiplica por 0,75; dois por 0,5 no default migrado; consumidor real do funcionário |

Os detalhes de agressividade acima são uma proposta de implementação para remover a ambiguidade de “mais agressivos”. Deixar alvo e percentual editáveis, preservando a regra de monstros pacíficos e hostilidade por espécie.

Para garantir alternativas distintas nas etapas obrigatórias, adicionar somente os novos modificadores necessários às pools. Propostas simples: “Clientes impacientes” (redução configurável de paciência de novos clientes) e “Entrega demorada” (aumento configurável do ETA de novas encomendas). Eles precisam de integração e teste antes de entrar no catálogo ativo. Configurar incompatibilidade entre entrega demorada e fornecimento expresso. Não usar cards sem efeito para completar uma pool.

Migrar ofertas: forno 90; bolsa 75; treinamento 85; suprimentos base 45; balconista 180; pizzaiolo 120. Promoções preservam a fórmula existente inicialmente, agora editável. Capacidades, preços e contagens devem aparecer na UI a partir dos mesmos dados.

Cada efeito precisa de teste com parâmetro neutro, valor alterado, término da duração e nova RUN. Valores acumulados devem respeitar teto. Nenhum efeito temporário deve vazar para a noite seguinte.

## 8. B4 — Preparação antes da primeira noite e votação em etapas

Fluxo único:
Lobby → tutorial opcional → preparação da Noite 1 → noite → resultados → preparação da próxima noite → vitória/derrota.

1. Reaproveitar máquina de estados existente; não criar uma preparação paralela só para a Noite 1.
2. Separar noite concluída e noite alvo da preparação (`TargetNightNumber` ou equivalente). Evitar calcular sempre `CurrentNightNumber + 1`, o que pularia a Noite 1 na entrada.
3. Pausar relógio e geração de pedidos/inimigos durante preparação. Mundo/loja podem continuar acessíveis conforme regras, mas nenhuma noite começa por timeout automático.
4. Servidor cria as etapas e alternativas uma vez por preparação. Reabrir UI, entrar cliente ou comprar não pode sortear novamente.
5. Cada votação possui ID próprio e estado: aberta, resolvida, aplicada. Resolver/aplicar uma vez. Grupo vota em todas as etapas obrigatórias antes do pronto final.
6. Empate é sorteado pelo servidor. Não permitir que todos marquem pronto sem votar e isso resolva silenciosamente todas as categorias.
7. Saída de jogador remove votos/prontidão e recalcula elegíveis. Novo participante na preparação recebe snapshot e começa não pronto. Política existente de espectador vale na noite ativa.
8. Alteração de escolhas invalida prontidão pertinente. Quando todos estiverem prontos, validar pendências e iniciar exatamente a noite alvo.
9. Compras são independentes dos votos, atômicas e usam saldo coletivo. Pedidos duplicados de rede não duplicam débito/efeito; duas compras concorrentes não deixam saldo negativo.
10. Ofertas cujo efeito altera a população inicial só devem estar disponíveis antes do spawn, salvo suporte explícito à aplicação durante a noite.
11. Snapshot de rede inclui etapas, opções, votos, resultados, modificadores ativos, preços, limites e prontidão. Cliente não decide elegibilidade.
12. Após última noite, não abrir preparação fantasma. Nova RUN limpa opções, votos e modificadores e volta à preparação da Noite 1 (ou tutorial escolhido).

Aceite: com e sem tutorial, primeira noite nunca começa antes da confirmação coletiva; etapas de entrave não são substituídas por benefícios; três noites não viram quatro por erro de contagem.

## 9. B5 — UI ligada ao catálogo e aplicada às cenas

Atualizar RunPreparationPresenter e OperationsHubPresenter em UI Toolkit. Preservar o atalho J e caminhos já montados; manter compatibilidade do quadro físico de gestão.

- Cabeçalho: noite alvo, saldo coletivo, metas e participantes.
- Etapas por categoria com progresso e quantidade de escolhas restantes.
- Cards: nome, ícone, efeito numérico exato, duração, níveis, votos e seleção local.
- Loja: preço efetivo, nível atual/máximo, benefício, disponibilidade e motivo de bloqueio.
- Resumo dos efeitos escolhidos/comprados antes de confirmar pronto.
- Feedback de compra apenas após confirmação do servidor.
- Descrições derivadas do catálogo, sem percentuais ou preços repetidos em strings de código.
- Mostrar diferença entre duração da noite e permanência até fim da RUN.
- Rolagem, navegação, estado vazio e mensagens de erro legíveis; testar 1280×720 e 1920×1080.
- Modais não disparam arma, duplicam callbacks nem deixam câmera/cursor presos.

Salvar referências nos presets e cenas usadas. Executar o instalador/migração e verificar persistência ao reabrir o projeto. Não entregar apenas um botão de instalação ainda não executado.

## 10. B6 — Testes e evidências

Usar a Unity CLI instalada para verificações direcionadas desta implementação, conforme autorização de testes já dada pelo usuário. Inspecionar capacidades/comandos reais da ferramenta. Sem loops de build, apagar locks ou matar Editor. Se não houver ambiente para multiplayer real, registrar a limitação sem fingir que teste local o substitui.

| Caso | Resultado esperado |
|---|---|
| T01 Migração executada duas vezes | IDs/referências estáveis, sem duplicação nem perda de ajuste |
| T02 Preço 137 no wizard | Preview, loja, snapshot e débito iguais |
| T03 Treinamento 10% sobre erro 20% | Resultado 18%, sem ambiguidade de unidade |
| T04 Duas compras e saldo para apenas uma | Uma aceita, outra rejeitada; saldo e níveis corretos |
| T05 Benefício obtido por voto e loja | Limite compartilhado; nenhuma cobrança após máximo |
| T06 Pacote base | Massa/molho/queijo reais repostos; estoque cheio não gera compra sem benefício |
| T07 Lua vermelha e limite de população | Mais vampiros conforme arredondamento/teto configurados |
| T08 Ataque dos vampiros | Raio efetivo alterado e revertido; pacíficos continuam seguindo gatilhos |
| T09 Movimento intenso | Taxa efetiva aumentada, sem duplicar aplicação ou exceder limites |
| T10 Expiração/stack | Temporário removido; permanente preservado; refresh não acumula novamente |
| T11 RUN nova com/sem tutorial | Preparação da Noite 1, sem relógio correndo antes do pronto |
| T12 Etapas e empate | Todas as categorias obrigatórias resolvidas; desempate único no servidor |
| T13 Saída/entrada na preparação | Votos e elegíveis reconciliados; snapshot correto |
| T14 Pool insuficiente/incompatível | Validação clara e fallback válido, sem duplicatas nem loop |
| T15 Cliente com valores locais diferentes | Preço/efeito usados são do servidor |
| T16 Edição durante PlayMode | Sem mutação silenciosa; próxima RUN aplica snapshot novo |
| T17 Três noites + nova RUN | Contagem correta e persistências limpas |
| T18 UI host/cliente e reabertura | Mesmos valores/opções, sem cliques duplicados ou tiro acidental |

Testar consumidores reais; comparar só DTOs calculados não comprova que demanda ou IA mudaram. Automatizar domínio e integrações pertinentes, depois validar host+cliente separado e UI visual quando disponível. Preservar XML/logs pequenos com SHA, seed, preset e data. Distinguir .NET, EditMode, PlayMode, multiplayer e inspeção visual.

## 11. Entrega e commits

Executar B0 → B1 → B2 → B3 → B4 → B5 → B6, com commits coesos. Corrigir falhas impeditivas encontradas no próprio trabalho e repetir somente testes afetados/regressões relevantes.

Entregar `Docs/Release/Reports/WIZARD_EVENTOS_LOJA_BALANCEAMENTO.md` com:
- Arquivos e assets modificados, SHA e commits.
- Caminho exato do wizard e do preset ativo.
- Guia curto: como alterar preço, percentual, contratação, pool e quantidade de eventos.
- Matriz efeito → configuração → consumidor → UI → teste.
- Valores migrados e defaults novos identificados como propostas de playtest.
- Evidências executadas e pendências reais.

Atualizar CURRENT_STATE e CHANGELOG. Não declarar conclusão porque compilou. Critério final: Rubens altera dados no wizard, inicia uma RUN, vê a preparação antes da Noite 1 e observa o preço/efeito correto no jogo, sem editar C# ou preencher referências manualmente.

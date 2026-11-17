# ScaryParty — Recuperação e integração das fases M1–M8

Data: 17/11/2026. Entrega: 02/12/2026.

## 1. Objetivo, referência e prioridade

Concluir as fases originais reaproveitando o trabalho da madrugada. Este documento complementa `Docs/Release/ROADMAP.md`; não redefine o jogo nem autoriza cortar requisitos silenciosamente.

Referência auditada: branch `release/marcos-noturnos`, SHA `ff864cc5aa4afe7a8c371918ae9fe4e7bd9aa490`. Base anterior: `a7cc598bc199f6ccf64c8ca0e103979e99326b9f`. A auditoria foi estática e direcionada; Unity, compilação, EditMode, PlayMode e rede não foram executados nesta sessão. Achados precisam ser revalidados contra a branch local antes de editar, pois ela pode conter correções posteriores.

Problema principal: serviços novos foram criados, mas parte deles não está ligada a entrada do jogador, composição, execução, replicação e apresentação. Há também bloqueadores estáticos de compilação e regressão no estado de morte. Corrigir esses problemas antes de ampliar conteúdo.

### Critério de implementação completa

Cada funcionalidade deve demonstrar a cadeia:

**entrada real → comando validado pelo servidor → regra/domínio → estado replicado → execução física/visual → feedback e limpeza.**

Uma classe isolada, teste que instancia serviço diretamente, RPC sem chamador, método sem uso, bool sem efeito real ou relatório escrito não satisfaz esse critério. UI, spawns, prefabs, authoring e conexões runtime são implementação; não classificá-los como mera validação manual.

Se não houver acesso ao Editor, implementar código e ferramentas de configuração idempotentes possíveis, mas manter a etapa explicitamente incompleta até aplicar e verificar suas referências. Não alegar que um menu de setup não executado já configurou a cena.

## 2. Contrato operacional

1. Identificar raiz Git e raiz Unity (`ScaryParty/`, contendo Assets/Packages/ProjectSettings). Os caminhos de Docs abaixo partem da raiz Unity.
2. Ler AGENTS, INDEX, CURRENT_STATE, ROADMAP e relatórios pertinentes. Conferir branch/SHA/status. Preservar alterações preexistentes; não executar reset/clean/rebase nem substituir tudo pela versão auditada.
3. Continuar na branch de recuperação apropriada a partir do trabalho atual. A autorização anterior permite commits locais por bloco funcional; não fazer push/merge automaticamente. Registrar SHA inicial e commits no relatório.
4. Manter `UNITY_EXECUTION=MANUAL_ONLY`: sem abrir Unity/Hub/headless/builds por conta própria. Usar Editor já aberto quando houver controle disponível. Compilação não verificada deve continuar assim registrada.
5. Preservar GUIDs, `.meta`, overrides e referências de assets. Integrar scripts novos e seus `.meta` antes de considerar a entrega reproduzível em outro checkout. Não regenerar `.meta` existente.
6. Reutilizar os serviços existentes. Não criar uma segunda economia, outro inventário ou um segundo controlador de noites concorrente.
7. Avançar sequencialmente pelas etapas R0–R8. Se restar apenas teste manual, continuar trabalho independente; se houver dependência quebrada, não construir sobre ela como se funcionasse.
8. Fazer commits pequenos por comportamento. Um commit de checkpoint incompleto deve dizer isso claramente; não rotular como funcional sem evidência.
9. Manter relatório/checkpoint depois de cada bloco: implementado, conectado, configurado, testado, pendente. Não repetir auditorias ou builds em loop.

## 3. R0 — Restaurar compilação e verdade documental

**Prioridade P0; cobre M1 e desbloqueia todas as demais.**

Em `Assets/Piaaria/Scripts/Pizzeria/Network/PizzeriaCommandHandler.cs`, revalidar e corrigir:

- `HandleResetNightCombatServer(bool resetWeaponsAndAmmo = false)` é associado a `System.Action` sem parâmetros. Criar wrapper sem argumentos que chame a implementação com política explícita, ou alterar o contrato de todos os produtores/consumidores coerentemente. Preservar unsubscribe correto; não usar lambdas anônimas diferentes para inscrever/desinscrever.
- `TickServerCombat` referencia `enableAllPlayersDeadDefeat` fora de escopo. Mover a configuração para fonte autoritativa de sessão, carregada do preset. Não simplesmente substituir por `false`, pois derrota coletiva é requisito.
- `ToggleSlingShotgunServerRpc` chama `TargetSender` sem definição encontrada. Reutilizar/consolidar a construção existente de `ClientRpcParams`, incluindo host ID 0.

Depois verificar assinaturas de eventos, DTOs, namespaces, enums, dependências e assembly definitions de todos os arquivos novos/alterados. Corrigir novos erros reais encontrados; não apagar código/testes para obter compilação aparente.

Reclassificar STATUS e relatórios: M1/M2/M3 parciais ou pendentes conforme evidência; M4–M7 integração incompleta; M8 não concluída. Preservar histórico do trabalho realizado e acrescentar revisão, sem fingir que não existiu.

**Aceite:** nenhuma incompatibilidade estática conhecida; compilação real do Editor sem erros quando disponível. Se não foi possível compilação, registrar “correções implementadas; compilação não verificada”. Não declarar P0 resolvido operacionalmente apenas por `git diff --check`.

## 4. R1 — Vida, incapacitação, cura, morte e espectador

**Cobre M4; depende de R0.**

### Estado e permissões

Estabelecer estados mutuamente coerentes: vivo, incapacitado, morto. Se preservar flags atuais, centralizar invariantes em vez de permitir combinações arbitrárias. Usar o mesmo estado no servidor e na apresentação.

Corrigir a regressão em que sangramento define `IsIncapacitated=false`/`IsDead=true` e PlayerHealth/armas liberam ações por só consultarem incapacitação. Expor permissões derivadas para mover, interagir, trabalhar, montar, comprar, disparar e ajudar. Validar também nos RPCs; bloquear input local sozinho é insuficiente.

Morto deve liberar bancada, comanda e reservas conforme regra, desmontar, manter itens com destino definido e entrar em espectador. Implementar câmera seguindo aliado vivo, troca de alvo e fallback quando não houver alvo; bloquear ações de gameplay. Retornar apenas na próxima noite vencida ou nova RUN. Todos sem possibilidade de resgate causam derrota uma vez.

### Ajuda real do jogador

Conectar raycast/prompt/input de ajuda a comandos begin/cancel e heartbeat quando necessário. O servidor mede o tempo; não acumular `dt` fornecido pelo cliente como relógio de verdade.

Validar: ajudante vivo, alvo válido, distância, mesma sessão, fase permitida, continuidade e exclusividade da ajuda. Soltar, afastar, desconectar, morrer, invalidar alvo ou sofrer dano deve zerar o progresso. Proposta do roadmap: dano ao ajudante ou alvo interrompe. Heartbeat expirado cancela; mensagens repetidas não aceleram a cura.

Distinguir cura de vivo ferido e resgate de incapacitado. Ambos demoram, sem consumível, com duração/vida recuperada editáveis; morto não pode ser ressuscitado por essa interação. Replicar progresso e motivo da interrupção.

Encaminhar dano de queda da moto pelo serviço normal de dano, para que HP zero dispare incapacitação/morte, desgaste e interrupções corretamente.

**Testes:** T43–T45; cliente ajuda host e inverso; interrupção a 90%; spam de RPC não acelera; ajuda à distância é rejeitada; morto tenta andar/atirar/interagir/montar; desconexão do ajudante; retorno na próxima noite.

## 5. R2 — Mãos, armas, bandoleira e moto completamente integradas

**Cobre M3; depende de R0/R1.**

1. Verificar um único contrato de ocupação: direção reserva uma mão; pistola uma; espingarda duas; bandoleira nenhuma. Aplicar às interações, ao rack, ao inventário e ao servidor. Não permitir que cliente pegue utensílio na mão reservada pela moto.
2. Completar a representação da espingarda guardada no corpo em primeira/terceira pessoa. Preservar instância, dono e munição; guardar não devolve ao rack. Saque valida ambas livres e confirma atomicamente; falha deixa o estado original intacto.
3. Verificar input semiautomático com eventos reais de pressionar/soltar, UI, mudança de foco e reequipo. Não depender de bool que permaneça preso. Automaticidade deve ser atributo da arma.
4. Integrar ADS a câmera, pose, sensibilidade e HUD; terceira pessoa deve refletir postura. Recarregar/cair/morrer/abrir UI limpa a mira. Arma próxima à parede não dispara através dela.
5. IDs de instância únicos, incluindo duas pistolas diferentes. Cooldown, munição, dono e bandoleira usam ID da instância, não tipo.
6. Atropelamento: servidor confirma condutor, velocidade, alvo elegível e contato/proximidade compatíveis com movimento recente. Usar teste de colisão/sweep autoritativo com tolerância de rede, não aceitar alvo arbitrário indicado pelo cliente.
7. Deduplicar por episódio de pilotagem/impacto estável. Uma colisão válida aplica um dano e desliga a pilotagem antes de aceitar outro impacto. Limpar histórico quando a sessão termina; evitar coleção crescente indefinidamente.
8. Queda deve usar saída segura validada por colisão/chão e mecanismo de reposicionamento de rede apropriado. Não simplesmente deslocar Transform lateralmente para dentro de parede. Restaurar câmera, movimento e mão de direção; manter a pistola coerente.
9. Modelo real da moto e anchors configuráveis. Preview de item por bone, origem/extremidade e offsets nos wizards existentes; representar tail do Blender por anchor/offset explícito quando necessário. Não classificar essa ferramenta ausente como “só testar”.

**Testes:** T29–T42 e nova repetição com latência; duas armas iguais; espingarda guardada + cozinha; moto + pistola; morte/queda/desconexão durante pilotagem; alvo fora de alcance rejeitado; multicollider causa só um dano. Rifle/granada continuam pendências de conteúdo identificadas, sem esconder espingarda/bandoleira obrigatórias.

## 6. R3 — Fechar a noite e integrar economia/reputação

**Cobre M4; depende de R1/R2.**

- Entrega multipizza definitiva: rastrear o caminho real de porta/balcão até OrderService. Validar conjunto escolhido, encerrar uma única vez com erro por pizza faltante/incorreta, impedir complemento e pagamento duplicado. Registrar regra editável de recompensa parcial conforme proposta do roadmap.
- Integrar os contadores efetivamente usados: faturamento da noite, saldo, total da RUN e pizzas entregues. `RecordNightRevenue` novo não deve ficar sem chamadores; se serviços existentes já contabilizam, consolidar ou remover duplicação. Não somar duas vezes ao inserir hook.
- Ligar acertos/atrasos/erros à reputação por evento único e replicar valor/razão ao HUD. Acerto recupera/mantém dentro do teto.
- Remover confiança no booleano `insidePizzeriaVolume` recebido do cliente. Gerar penalidade a partir de dano confirmado pelo servidor a cliente existente e posição dentro do volume correto da pizzaria. Reutilizar ID do evento de dano para deduplicação; cooldown sozinho não comprova que houve ataque.
- Fechar expediente atomicamente: bloquear novos comandos incompatíveis, liquidar pendências, aplicar penalidades e avaliar resultado. Resolver corrida entrega/relógio com regra temporal única do servidor.
- Preservar encomendas pagas, armas/munição, estoque e upgrades; repor ingredientes base configurados. Revisar `Items.Clear()` e resets para não apagar recursos que deveriam persistir.
- Implementar desgaste da mochila, capacidade reduzida/reparo e inutilização configurada de pizzas; não apenas uma mensagem de rasgo.
- Completar consulta de comandas, prazos assumidos e devolução da responsabilidade após morte/desconexão.
- Expor parâmetros, evitando -15, -10, 3s, 45s e metas espalhados como constantes incontornáveis.

**Testes:** T07–T20, T41–T52; ganhar meta 100 e gastar não desfaz sucesso; pedido errado encerra; acerto recupera reputação; ataque externo não penaliza; entrega no último instante; recursos persistem e munição não reaparece grátis.

## 7. R4 — Colocar RUN, votação, loja e modificadores no fluxo real

**Cobre M6; depende de R3.**

### Composição e rede

Instanciar e manter `RunProgressionService` na composição autoritativa (PizzeriaRoot ou coordenador existente apropriado), uma vez por RUN. Limpar/desinscrever ao encerrar. Não criar instância nova por frame ou por RPC.

Migrar os caminhos existentes de avançar/retry para o coordenador. `AdvanceToNextNightServerRpc` não pode continuar pulando diretamente ao ciclo antigo. Preparação deve aguardar votos e prontidão, não simplesmente expirar em quatro segundos. Na terceira noite encerrar RUN; derrota reinicia tudo, sem retry de produção por snapshot.

Criar comandos validados de voto, troca de voto, pronto/desmarcar e compra. Replicar opções oferecidas, votos/resultado, participantes elegíveis, prontidão, saldo, modificadores ativos e fase. Late join/reconexão recebe snapshot e não resorteia.

### UI e efeitos reais

Integrar telas UI Toolkit existentes com opções, duração/efeito, votos, confirmação e prontidão. Compras livres usam dinheiro coletivo sem votação por compra. Só confirmar compra depois do sucesso do servidor; item recusado não consome saldo.

Conectar resultado da compra à entrega do benefício. Cobrar dinheiro e registrar ID não entrega um item automaticamente. Conectar estatísticas calculadas aos consumidores reais: demanda, spawns/agressividade, forno, capacidade da mochila, ETA de suprimento e habilidade dos funcionários.

Expirar temporários depois de resolver a noite; permanentes não acumulam novamente em cada refresh. Recalcular desde base, preservar catálogos/configuração e impedir combinações tecnicamente impossíveis. Todos os sorteios relevantes oferecem alternativas votáveis.

Fixar metas por humanos no início e mantê-las após morte/desconexão; recalcular na seguinte. Dificuldade separada da seed. Nova RUN deve limpar funcionários, hostilidade conforme política, compras únicas, RNGs, estoque, dinheiro, reputação e modificadores sem restos da anterior.

**Testes:** T46–T56 e T59; empates, desconexão, duas compras com saldo para uma, alteração de pronto; três noites completas; timer do forno realmente impede queima; treinamento realmente muda erro; nova RUN realmente limpa estado. Serviço unitário isolado não aprova esse marco.

## 8. R5 — Navegação e monstros no mundo

**Cobre M2/M5; depende de R1/R3/R4 para efeitos de escolhas.**

### Cidade e trânsito

Revalidar correções de TrafficManager/CityGenerator: inicialização, relay, interpolação, IDs, late join e reentrada. Usar F7 para separar carro realmente parado de posição congelada só no cliente. Testar grafo/semáforo/sensor antes de remover regras de trânsito.

Conferir colisores/triggers/rigidbodies exigidos pelos callbacks novos. Código `OnTriggerEnter` sem configuração física válida não gera atropelamento. Sensor de frenagem e dano físico devem permanecer separados.

NPC normal não pode usar fallback que atravessa barreiras; `SamplePosition` próximo não garante mesmo lado de parede nem conectividade. Validar rotas completas, portas e áreas restritas. Preservar movimento curto de sentar/levantar apenas em trajetória validada.

### Integração das espécies

1. Conectar o controller aos prefabs/spawner/população, registrar prefabs de rede e parâmetros por espécie. Garantir IDs e replicação de posição/animação; evitar dois motores disputando Transform.
2. Remover aquisição agressiva incondicional nos estados Routine/Customer. Separar gatilho de ficar agressivo da seleção de alvo após agressão. NPC pacífico próximo deve poder circular, pedir e comer.
3. Instanciar hostilidade por espécie na sessão e ligar ofensas de pedidos/ingredientes ao serviço. Controller consulta valor efetivo para agressividade, mantendo distância/tempo, sem introduzir visão obrigatória.
4. Vampiro: anchors elegíveis de telhado, antecipação, descida e pouso seguros. Método que apenas Warp para o chão não entrega esse comportamento. Rotina/cliente precisa retomar após desistência.
5. Lobisomem: preparar, investir, detectar obstáculo por sweep/cast, aplicar um dano e recuperar. Não somar deslocamento no Transform através de paredes nem manter NavMeshAgent brigando com a investida.
6. Fantasma: flutuação discreta, passagem seletiva e blink com aviso/cooldown/destino seguro; não basta um teleporte no NavMesh. Exceção de obstáculos exclusiva da espécie.
7. Zumbi: ligar ataque elegível a conversão uma vez, com teto/cooldown; cancelar ou migrar corretamente pedido/cadeira/rotina do alvo. Não duplicar NPC e penalidade. Manter como protótipo configurável conforme decisão original.
8. Reset noturno limpa população anterior e hostilidade conforme política, sem acumular spawns. Ataques a clientes passam pelo evento real da reputação.

**Testes:** T19–T28, T50/T57; cinco seeds; cada espécie pacífica/agressiva/desistência; vampiro vindo de cima; lobisomem bloqueado por parede; fantasma sem sair do mundo; conversão limitada; host e cliente veem o mesmo estado.

## 9. R6 — Funcionários que executam trabalho real

**Cobre M7; depende de R3/R4/R5.**

Instanciar `EmployeeTaskService`, conectar contratação/upgrades à loja e replicar funcionários/níveis/atribuições. Criar agentes físicos e spawns navegáveis. Identidade de funcionário e de jogador precisa de tipo e ID, evitando colisões entre funcionário 1 e cliente 1; host ID 0 é válido, não sinônimo de ausência.

Corrigir exclusividade: comparação de dono de reserva precisa considerar categoria do ator. Reservas bloqueadas ainda precisam de política de propriedade/expiração; não permitir duplicar trabalho só porque estado mudou de InProgress para Blocked.

Balconista percorre atendimento real: localizar cliente, reservar posto/cliente, atender/registrar pedido e liberar. Pizzaiolo percorre pipeline: assumir pedido, verificar receita, buscar recursos, usar bancadas/utensílios, montar, assar e encaminhar produto. Reutilizar TransferService, ProcessingService, OvenService e avaliação existente; sem pizza instantânea ou recursos infinitos.

Adaptar interfaces de execução para ator NPC quando necessário, sem fingir que um funcionário é um cliente de rede humano. Cada etapa confirma precondições, reserva recursos e recupera falhas. Exibir motivo de bloqueio e permitir intervenção humana.

Erro intencional sorteado em ponto definido do processo altera ingrediente/cozimento realmente; upgrade reduz chance efetiva. Falha de caminho não é erro de habilidade. Cancelamento, morte, takeover, fim de noite e desconexão liberam reservas sem apagar produto válido nem criar cópia.

Persistir contratação/nível, limpar tarefas obsoletas e aplicar política de retorno noturno editável. Funcionários não ocupam vagas humanas nem aumentam meta inicialmente.

**Testes:** T58 ampliado: funcionário atende cliente; pizzaiolo prepara pizza correta consumindo estoque; erro forçado altera produto; treino muda taxa; disputa com humano; host ID 0 e NPC ID coincidente; caminho bloqueado; takeover no forno; transição de noite.

## 10. R7 — Authoring, tutorial e apresentação faltantes

**Cobre partes de M3 e M8; depende dos sistemas funcionais.**

- Aplicar e salvar configurações necessárias a prefabs/cena/Animator/UI. Reaproveitar wizards; novos comandos de setup devem ter dry-run/preview, Undo quando aplicável e idempotência, sem sobrescrever valores personalizados.
- Completar poses de mão/bandoleira, anchors de moto e preview de bones/offsets; usar rigs/modelos reais existentes.
- Tutorial noite zero opcional e previsível, caixa lateral recolhível, sem confundir clique UI com disparo. Cobrir produção/entrega/ajuda básica.
- HUD de reputação, dinheiro versus faturamento/meta, vida/morte, mãos, munição, mochila e prazos; tela de votos, loja, resultado final da RUN e nova RUN.
- Integrar Feel/DOTween/áudio já disponíveis a eventos reais, sem duplicar efeitos por RPC e sem alterar física autoritativa por animação cosmética.
- Manter loading e retorno ao menu coerentes; não criar progressão narrativa não pedida.

**Aceite:** funcionalidade acessível ao jogador sem Inspector; todos os estados de UI têm caminho de entrada/saída e restauram input corretamente. Registrar separadamente o que foi criado em código, aplicado em assets e verificado em cena.

## 11. R8 — Regressão e aceite dos oito marcos

Executar os T01–T60 originais e casos adicionais deste documento. Reutilizar testes relevantes existentes; corrigir expectativas antigas que contradizem o produto. Rodar Test Runner no Editor disponível, sem inventar resultados.

### Verificações obrigatórias adicionais

| Caso | Esperado |
|---|---|
| Sangramento termina; morto tenta todas as ações | Espectador sem permissões de gameplay |
| Ajuda repetida/à distância/descontinua | Não acelera nem mantém progresso inválido |
| Pedido correto e ataque territorial | Reputação muda pelo evento confirmado e replica |
| Terceira noite termina | Vitória final; não oferece noite 4 no preset da entrega |
| Compra de upgrade | Benefício real entregue, não apenas desconto de saldo |
| NPC pacífico perto do jogador | Não ataca sem gatilho/política válida |
| Duas entidades disputam tarefa com mesmo número de ID | Identidades distintas, exclusividade preservada |
| Queda de moto perto de parede com dano letal configurado | Saída válida e estado de vida correto |
| Reiniciar RUN duas vezes na mesma sessão | Sem spawns, handlers, votos ou recursos duplicados |
| Checkout limpo em outra instância | Scripts/assets/.meta e referências presentes |

Registrar para cada execução: SHA, data, seed/preset, host/cliente, passos, esperado/observado e evidência. Estática, compilação, EditMode e PlayMode são camadas diferentes. Não marcar um caso de gameplay como PASSOU por inspeção de código.

Finalizar com uma RUN completa de duas pessoas/instâncias, duração alvo 30–45 minutos incluindo escolhas, e pelo menos uma derrota/reinício. Verificar escala 1/2/4 na configuração/testes de domínio e multiplayer disponível. Medir memória/agentes/frame time/tráfego e registrar baseline, sem prometer desempenho não medido.

Build candidata só quando gerada de forma autorizada no ambiente do projeto; jogar fora do Editor antes de afirmar pronta para entrega. Sem acesso à execução, deixar validação pendente sem esconder implementação faltante.

## 12. Matriz de responsabilidade e critérios de parada

| Etapa | Corrige fases | Resultado concreto |
|---|---|---|
| R0 | M1/todas | Compilação e documentação coerentes |
| R1 | M4 | Morte/resgate/cura/espectador integrados |
| R2 | M3 | Mãos, moto e armas coerentes |
| R3 | M4 | Uma noite com economia e consequências reais |
| R4 | M6 | Três noites com escolhas, loja e efeitos |
| R5 | M2/M5 | Cidade e espécies integradas |
| R6 | M7 | Funcionários atendem e produzem |
| R7 | M3/M8 | Authoring e UI acessíveis |
| R8 | M1–M8 | Evidência de regressão e entrega |

Estados por etapa: NÃO INICIADA; EM IMPLEMENTAÇÃO; INTEGRAÇÃO INCOMPLETA; IMPLEMENTAÇÃO CONCLUÍDA — VALIDAÇÃO PENDENTE; VALIDADA; BLOQUEADA. Se faltar chamador/runtime/spawn/UI obrigatória, usar INTEGRAÇÃO INCOMPLETA.

Em `Docs/Release/Reports/RECUPERACAO_M1_M8.md`, manter tabela por requisito com: origem (M/T), arquivo/ponto de entrada, consumidor real, configuração, replicação, teste, resultado, commit e pendências. Atualizar STATUS, TEST_MATRIX, CURRENT_STATE e CHANGELOG sem apagar histórico. Pendências materiais não podem ser escondidas no rótulo “polimento”.

Não há necessidade de refazer tudo. Preservar correções úteis de disparo, trânsito, IDs e serviços puros; completar e corrigir as conexões que faltam. Não usar a data da entrega para fingir conclusão: cortes de escopo precisam ser explicitados ao usuário.

## 13. Prompt pronto para o Antigravity

Copie este documento para `Docs/Release/RECUPERACAO_M1_M8.md`, relativo à raiz Unity, e envie:

```text
Leia Docs/Release/RECUPERACAO_M1_M8.md, o ROADMAP e os AGENTS aplicáveis.
Execute as etapas R0–R8 em sequência, aproveitando a implementação atual.
Esta solicitação autoriza continuar entre etapas e criar commits locais
por bloco funcional, sem push ou merge. Preserve alterações preexistentes.

Comece pelos bloqueadores de compilação; depois integre vida/resgate,
mãos/moto, noite/economia, RUN/votos/loja, monstros e funcionários.
Não considere classe isolada, RPC sem chamador, prefab não configurado
ou UI obrigatória ausente como implementação concluída.

Para cada requisito, registre a cadeia real de entrada, comando, regra,
replicação, execução e apresentação. Corrija os problemas encontrados
na branch atual, sem assumir que linhas ou SHAs históricos não mudaram.

Respeite MANUAL_ONLY. Não abra Unity headless nem gere builds automáticas.
Execute verificações disponíveis; testes não executados permanecem pendentes.
Continue tarefas independentes quando faltar apenas validação manual,
mas não avance sobre erro conhecido de compilação ou dependência quebrada.

Atualize STATUS.md, TEST_MATRIX.md, CURRENT_STATE.md, CHANGELOG.md e
Docs/Release/Reports/RECUPERACAO_M1_M8.md a cada checkpoint.
Ao finalizar, entregue commits, funcionalidades realmente integradas,
implementação ainda faltante e passos priorizados para meu playtest.
```

Se o Antigravity estiver aberto na raiz Git, prefixar os caminhos acima com `ScaryParty/`. Este plano não altera o código por si só e não afirma execução de testes.

# ScaryParty — Superplano de conclusão por marcos

Entrega acadêmica: **2 de dezembro de 2026**. Planejamento consolidado em 16/11/2026, horário de São Paulo.

## 1. Objetivo e modo de execução

Entregar uma RUN cooperativa de três noites, com duração de referência de 30–45 minutos incluindo intervalos, para 1–4 jogadores humanos, demonstrada por pelo menos dois. A versão futura comporta sete noites. O coração é o friendslop: divisão de trabalho, comunicação externa, confusão, escolhas coletivas e consequências engraçadas/perigosas. O grupo prepara pizzas, atende clientes, entrega pela cidade, enfrenta ameaças e precisa sobreviver e cumprir metas.

Este documento é o roteiro mestre para executar **um marco por vez**. Não autoriza um agente a tentar implementar tudo numa única sessão. Cada marco entrega comportamento jogável, integração multiplayer, parâmetros editáveis, evidências e pendências. Não confundir código escrito com comportamento aprovado em PlayMode.

Equipe: cinco pessoas; desenvolvimento concentrado no Rubens. Não incluir gestão de produção de assets nem cronograma de disponibilidade da equipe. Integrar modelos existentes por configuração, preservando materiais, animações, prefabs e GUIDs.

### Base inspecionada e limites da auditoria

- Repositório: https://github.com/TavernaDasMilPortas/ScaryParty
- Referência utilizada: `main`, commit `072a2d3ff471839d45e47f114148a769078b6d7b`, de 15/11/2026, merge de `feature/global-cel-shading-wizard`.
- A branch de trabalho local do usuário não foi informada. Antes de executar M1, registrar branch, SHA, alterações locais e diferenças para esta referência. Não sobrescrever trabalho local.
- Foram lidos contratos AGENTS, INDEX, CURRENT_STATE e trechos direcionados de armas, moto, trânsito, navegação, clientes, pedidos, saúde e noites. Esta é uma auditoria estática direcionada, não uma inspeção exaustiva de todos os assets.
- Executada inspeção de 12 padrões de código, com arquivo e linha registrados no apêndice. `git diff --check` terminou sem erros; nenhum código do jogo foi alterado. Isso não equivale a compilação ou testes de gameplay.
- **Não executados:** Unity Editor, NUnit/EditMode, PlayMode, simulação física, builds, testes de rede ou playtests. Não há Editor Unity disponível nesta sessão. Os casos abaixo são a bateria a executar no ambiente do projeto.
- O repositório define `UNITY_EXECUTION=MANUAL_ONLY`: usar o Editor já aberto para física, cena, UI e rede. Não iniciar loops headless, builds ou servidores automaticamente. A execução de testes solicitada aqui foi atendida na camada estática possível; os testes de cena permanecem explicitamente pendentes.

## 2. Regras de produto consolidadas

### RUN, relógio e derrota

1. A noite termina somente pelo relógio. Cumprir a meta antecipadamente não encerra o expediente.
2. Ao zerar o relógio: bloquear novas transações de atendimento/entrega; resolver pedidos pendentes; aplicar penalidades; avaliar condições; publicar resultado uma única vez. Remover prorrogação de atendimento do preset da entrega.
3. Vitória depende de faturamento da noite; dificuldades podem acrescentar reputação mínima e quantidade de pizzas entregues. Completar a terceira noite encerra a RUN com vitória.
4. Derrota: todos mortos/incapacitados sem possibilidade de resgate, reputação zerada ou exigências finais não cumpridas. Saldo zerado isoladamente não derrota.
5. Contabilizar **faturamento da noite**, **saldo disponível** e **total da RUN** separadamente. Ganhar 100 e gastar 80 satisfaz meta 100.
6. Fixar participantes humanos e metas quando a noite começa. Mortes e desconexões não barateiam a noite; recalcular na próxima. Funcionários não aumentam a meta inicialmente.
7. Não há salvamento para retomar nem progresso permanente. Perder reinicia a RUN do zero. Retry da mesma noite fica, se útil, apenas como ferramenta de desenvolvimento claramente isolada.
8. Preparação só termina após escolhas resolvidas e todos os participantes conectados confirmarem pronto. Desconexão durante preparação remove prontidão/voto daquele participante e recalcula o grupo elegível. Entrada durante noite: proposta inicial é espectar até a próxima, evitando alterar meta congelada.
9. Horário, meta, dinheiro, reputação, vida e estado dos jogadores ficam legíveis no HUD. Receitas completas exigem consulta; prazos dos pedidos assumidos ficam sempre visíveis.

### Persistência

| Estado | Entre noites | Nova RUN |
|---|---|---|
| Saldo, upgrades, ingredientes especiais e funcionários | Preservar | Resetar |
| Reputação | Preservar, com recuperação por acertos conforme parâmetros | Começar cheia |
| Massa, queijo e molho | Repor para quantidade base configurada; proposta: completar até o piso, sem apagar excedente comprado | Estoque inicial |
| Vida, incapacitação e danos à mochila | Restaurar; mortos retornam após sucesso dos demais | Estado inicial |
| Armas e munição | Preservar aquisições e consumo; não ganhar munição grátis por reset | Kit inicial |
| Modificadores permanentes da RUN | Preservar | Remover |
| Modificadores temporários | Remover após a noite e suas consequências finais | Remover |
| Pedidos, clientes e inimigos da noite | Encerrar e reconstruir rotinas; sem duplicação | Reconstruir |
| Cidade | Preservar inicialmente | Gerar pela seed escolhida |

Hostilidade da espécie persistir entre noites ainda não foi explicitamente decidido. **Padrão proposto:** reiniciar a cada noite; disponibilizar opção para persistir na RUN. Documentar esse padrão como proposta, não como decisão já confirmada.

Pedidos de suprimentos pagos e ainda não entregues não podem desaparecer silenciosamente. Proposta: conservar a encomenda e suspender seu prazo durante preparação; ao retomar, continuar a contagem. Pizzas e itens preparados excedentes devem ter política explícita; preservar quando viável, pois a intenção do usuário é manter recursos. Nunca usar um `Items.Clear()` indiscriminado sem classificar o que precisa sobreviver.

### Pedidos, cozinha e clientes

- Balcão e telefone; sem fluxo de recusa. Delivery paga mais e admite várias pizzas. Cliente presencial recebe, come, levanta e sai.
- Correção depende de ingredientes, cozimento e prazo. Não acrescentar temperatura ou corte como exigências nesta versão.
- Entrega é uma transação definitiva do conjunto escolhido. Faltas e pizzas erradas contam como erro. Não deixar pedido aberto para completar depois.
- Proposta editável: recompensar partes corretas e penalizar erros por pizza; o pedido pode ser parcialmente incorreto, mas está encerrado. Evitar pagamento ou penalidade duplicada.
- O jogador pode refazer antes de entregar e descartar a pizza inteira. Não implementar retirada de toppings, desmontagem ou reaproveitamento.
- Comanda pode ser assumida, consultada por botão e liberada/transferida. Morte/desconexão devolve a responsabilidade ao grupo; não apagar o pedido.
- Massa, queijo e molho podem acabar durante a noite. Compra de reposição tem UI, preço, estoque e prazo de chegada. Outros ingredientes dependem de compra/escolhas.
- Ataques a clientes dentro da pizzaria reduzem reputação. Delimitar volume da pizzaria, incluindo a planta não retangular; evitar penalizar brigas distantes.
- Pizzaria não tem vida nem destruição. Inimigos podem entrar e atacar pessoas.

### Mãos, armas, transporte e armazenamento

| Estado/objeto | Ocupação das mãos | Regra |
|---|---:|---|
| Moto sendo pilotada | 1 | Reserva uma mão para condução; a outra continua disponível |
| Pistola em uso | 1 | Pode mirar e disparar enquanto dirige |
| Espingarda em uso | 2 | Exige ambas livres; incompatível com pilotagem ativa |
| Espingarda na bandoleira | 0 | Continua pertencendo ao jogador e visível; não permite disparar guardada |
| Mochila vestida | 0 | Capacidade limitada por número de pizzas/caixas conforme definição atual |
| Ingrediente/utensílio/caixa | Configurável | Usa o mesmo sistema de reserva de mãos |

Direção usa uma reserva de mão, não transformar a moto num pequeno item segurado. Proposta inicial: mão esquerda para direção e direita para pistola, invertível em configuração.

- Atropelamento válido aplica dano ao alvo **uma única vez e derruba imediatamente o condutor**. O usuário não determinou dano obrigatório ao condutor: deixar separado/configurável, com zero como proposta inicial.
- Vários colliders e callbacks no mesmo impacto não causam múltiplos danos. Encerrar a pilotagem no servidor antes de aceitar outro atropelamento do mesmo episódio.
- Moto desocupada em movimento não deve continuar sendo uma arma que gera impactos repetidos sem condutor. Usar regra separada, inicialmente sem dano de atropelamento nesse estado.
- Desmontagem/queda libera exatamente a mão de direção, restaura câmera/locomoção e preserva corretamente a pistola. Se houver queda de itens, isso deve ser uma regra explícita, não efeito colateral.
- Sacar arma de duas mãos é uma transação atômica: verificar ambas livres; reservar; mover da bandoleira; replicar. Falha mantém estado anterior e informa o motivo. Não destruir, largar ou guardar outro item automaticamente sem regra definida.
- Uma bandoleira para arma longa por jogador é a proposta inicial. Quantidade configurável. Rifle e granada mantidos no backlog desta entrega, após pistola/espingarda estáveis; não descartados silenciosamente.
- Disparo semiautomático por borda de pressionamento. Automático somente em armas configuradas para isso. Não usar o cooldown como substituto da escolha do modo de disparo.
- Mira precisa ter efeito visual verificável (pose, alinhamento e ajuste de câmera configurável), não apenas um bool no Animator. Validar primeira e terceira pessoa.

### Inimigos e funcionários

Humanos e monstros podem ser clientes e ter rotinas. A pizza é desejada por todos; agressão é mudança de postura, não transformação automática de humano em monstro. Não existe identificação antecipada garantida do perigo. Olhos vermelhos podem comunicar agressividade já ativa.

- **Vampiro, prioridade:** aproximações a partir de telhados/pontos altos; descida com antecipação; perseguição por distância/tempo. Sem sistema de visão como requisito.
- **Lobisomem:** perseguição, preparação de investida, investida e recuperação que dá oportunidade de fugir/atacar.
- **Fantasma:** leve flutuação, atravessa obstáculos e dá blinks; local de chegada válido e aviso antes do ataque são propostas de legibilidade. Atravessar parede não autoriza sair do mapa ou cair abaixo do chão.
- **Zumbi:** conversão de NPCs atacados é proposta a prototipar. Chance, cooldown e teto de infectados configuráveis; sem crescimento ilimitado. Conversão não duplica cliente, pagamento, entidade nem penalidade.
- **Humano agressivo:** perseguição e ataque simples.
- Indivíduo perde agressividade após distância mínima mantida por X segundos. A espécie pode ficar mais hostil com ofensas, como alho para vampiros, afetando a facilidade de perseguição contra a equipe.
- Combate é uma solução escolhível, não apenas último recurso.
- Cura/resgate por interação demorada; sofrer ataque interrompe e zera. Proposta: interromper se ajudante ou alvo sofrer dano, perder alcance ou soltar interação. Morte leva a espectador; retorno só na noite seguinte após sucesso.
- Balconista atende; custa caro. Pizzaiolo assume pedidos e prepara pizzas; chance controlada de erro diminui com upgrades. Ambos usam os mesmos serviços e recursos dos humanos.

## 3. Arquitetura a aproveitar e completar

Não criar um segundo jogo ao lado do existente. Pontos encontrados:

| Base atual | Aproveitar para |
|---|---|
| `PizzeriaRoot`, domínio e `PizzeriaCommandHandler` | Autoridade das ações e integração |
| `NightCycleService`, `NightConfig` | Relógio, faturamento e fases; adaptar para RUN e preparação votada |
| `PizzeriaNetworkState`, `NetDtos`, `ReplicationProjector` | Snapshots e replicação dos novos estados |
| `TransferService`, `PlayerInventoryAdapter` | Posse e reservas de mãos unificadas |
| `OrderState` / `OrderLineState` | Pedido com múltiplas linhas já representado; revisar conclusão definitiva |
| `UpgradeService`, definições de upgrades e suprimentos | Loja e evolução sem duplicar catálogo |
| `WeaponController`, `WeaponInputHandler`, rack | Disparo, mira, munição e bandoleira |
| `DeliveryMotorcycleController`, `TrafficManager` | Pilotagem, atropelamento e tráfego |
| `NpcNavigationMotor`, `NavigationWorldManager`, `CustomerNpcView` | Rotas, movimento e transições de atendimento |
| Wizards existentes, `NightConfig`, perfis visuais | Authoring, presets e inspeção |
| Game Feel, UI Toolkit, loading e resultados existentes | Apresentação e integração final |

Separar regras puras das representações Unity. RPC valida remetente, posse, alcance, estado, fase, cooldown e recursos. Cliente pode prever apresentação, mas servidor decide dinheiro, pedidos, dano, morte, mãos, votos e atropelamento.

Usar IDs únicos de instância para armas, veículos, pedidos, NPCs e operações. Não usar tipo da arma como identidade. Entradas repetidas e resultados reenviados não podem duplicar dinheiro, dano ou itens.

O servidor mantém snapshot completo para entrada/reconexão; eventos cosméticos não substituem estado persistente. Ao desconectar: liberar reservas de mãos/bancadas/votos e definir destino de itens, sem reduzir a meta corrente.

### Configuração para playtest

Expor no Inspector/Odin e em um painel de desenvolvimento os parâmetros de noites, metas por 1/2/3/4 humanos, dificuldade, reputação, demanda, receitas, suprimentos, inimigos, funcionários, armas, mochila, moto, trânsito e modificadores. Reaproveitar assets existentes; novos perfis apenas onde houver responsabilidade distinta.

Presets sugeridos: `Entrega_3Noites`, `TesteRapido`, `Tutorial_Noite0`. O preset de entrega pode começar com 10 minutos por noite; não impor limite obrigatório aos intervalos de prontidão, portanto 30–45 minutos é alvo de playtest, não garantia matemática.

Mostrar valor base, modificadores ativos e valor final. Na execução multiplayer, valores aplicados são do servidor. Alterações incompatíveis só valem na próxima noite/RUN. Oferecer restaurar preset e registrar parâmetros usados no relatório. Não salvar mudanças de teste automaticamente sobre os assets de produção.

## 4. Ordem dos marcos

| Marco | Entrega verificável | Dependência |
|---|---|---|
| M1 | Auditoria, regressão e correções dos bloqueadores atuais | Base local identificada |
| M2 | Circulação confiável de NPCs e trânsito | M1 |
| M3 | Mãos, bandoleira, combate e moto coerentes | M1; integrar colisões com M2 |
| M4 | Uma noite completa com pedidos, economia e sobrevivência | M2 + M3 |
| M5 | Monstros e hostilidade das espécies | M2 + M3 + M4 |
| M6 | RUN de três noites, votos, loja e modificadores | M4; integrar M5 |
| M7 | Funcionários e evolução das funções | M2 + M4 + M6 |
| M8 | Tutorial, apresentação, balanceamento e build candidata | Todos os marcos anteriores |

Todo marco deve ser verificado como host e cliente remoto. Multiplayer não fica para a última fase.

## 5. M1 — Auditoria e reparos de base

### Objetivo

Descobrir erros além dos relatados antes de adicionar sistemas. A primeira fase contém a bateria de testes, o inventário de achados e a correção de bloqueadores. Funcionalidades ainda inexistentes entram como lacunas planejadas com marco responsável, sem exigir implementação de todo o roadmap em M1.

### Passos

1. Registrar branch/SHA, `git status`, versão Unity, cena canônica, prefab ativo e configs. Ler AGENTS e documentação atual. Comparar com esta auditoria.
2. Abrir a cena canônica já usada no projeto. Validar dependências, scripts ausentes, referências de slots, Animator, NetworkObject, colliders e IDs duplicados usando ferramentas existentes do Editor.
3. Capturar Console limpo e baseline reproduzível. Não considerar mensagens históricas como defeitos atuais sem reproduzir.
4. Executar EditMode pertinente no Test Runner aberto: serviços de pizza, mochila, interrupção, posicionamento, cidade e testes de experiência já existentes. Atualizar expectativas que contradigam regras novas.
5. Executar a matriz manual abaixo. Começar por host + um cliente; casos de escala recebem 1/2/4 participantes. Não é necessário disponibilidade de quatro pessoas: usar instâncias permitidas pelo ambiente existente.
6. Para cada falha: registrar reprodução mínima, esperado/observado, severidade, log, seed, SHA, lado da rede, hipótese e marco dono. Separar confirmado, provável, não reproduzido e requisito ausente.
7. Corrigir P0/P1 que impeçam a continuidade da experiência existente. Cobrir regressões com testes pertinentes. Não adicionar testes que apenas comparem texto de código.
8. Reexecutar casos afetados e uma passada curta da noite. Atualizar CURRENT_STATE, CHANGELOG e relatório de M1 sem declarar testes pendentes como aprovados.

### Achados iniciais para incorporar

Os caminhos são relativos a `ScaryParty/Assets/Piaaria/Scripts/`. Linhas referem-se ao SHA auditado.

| ID | Evidência | Classificação e ação |
|---|---|---|
| A01 | `Weapons/WeaponInputHandler.cs:74` chama tiro enquanto `fire` é verdadeiro | Divergência confirmada: não diferencia semiautomático. Causa compatível com relato; verificar também cancelamento de input. Corrigir em M1, consolidar em M3. |
| A02 | `Weapons/WeaponController.cs:164` e `TriggerFire` bloqueiam direção | Divergência confirmada com moto+pistola. Migrar bloqueio geral para reserva de mãos em M3. |
| A03 | `Pizzeria/Network/PizzeriaCommandHandler.cs:2037` exige duas mãos vazias; também rejeita arma equipada | Divergência confirmada. Cliente e servidor precisam concordar sobre uma mão. M3. |
| A04 | Mesmo handler, linha 1626: reanimação automática em 10s | Divergência confirmada. Substituir por incapacitação/resgate/morte; não apenas remover timer sem novo fluxo. M4. |
| A05 | `NightCycleService.cs:165`, RPC e UI de retry | Divergência confirmada com reiniciar RUN. Desligar no produto, preservar debug separado se útil. M4/M6. |
| A06 | `NightCycleService.cs:228` entra em Closing com prazo extra | Divergência confirmada; preset e fluxo devem fechar no horário e liquidar pendências. M4. |
| A07 | `CustomerNpcView.cs:321`: aproximação por `MoveTowards` quando Agent não está disponível | Risco concreto de atravessar obstáculo; não comprovado como causa do relato. Reproduzir rota sem NavMesh e corrigir fallback. M1/M2. |
| A08 | Handler linha 1774: cooldown indexado por `WeaponType` | Risco para múltiplas armas do mesmo tipo; rack atual é limitado. Adotar ID de instância antes de expandir arsenal. M3. |
| A09 | `NightCycleService.cs:132`: utensílio retorna para estação `3` | Risco de slot inexistente/colisão após mudanças no layout. Testar mudança de noite com ferramenta na mão e estação removida. M1/M4. |
| A10 | Handler linha 1433: enche carregador e reserva no reset | Divergência com preservar recursos/munição. Separar cura de reabastecimento gratuito. M4/M6. |
| A11 | `TrafficVehicle.cs:136`: `GetComponent<TrafficVehicle>()` direto no collider | Risco de ignorar carros com collider filho; sensor atual também não trata pedestres nesse ramo. Não explica sozinho carros imóveis. M2. |
| A12 | `NightConfig.cs:23`: defaults 360/390/420s, preparação 4s | Defaults não correspondem à duração desejada nem à prontidão coletiva. Assets serializados podem sobrescrever; conferir ativos. M4/M6. |

Outras verificações direcionadas: `ClearTransientNightEntities` limpa itens/encomendas/mãos; avaliar perdas entre noites. `StartAim` apenas altera flag/Animator no trecho inspecionado: conferir bindings, câmera e poses antes de concluir a causa da mira ausente. O diagnóstico dos carros parados continua aberto; investigar inicialização, grafo, caminhos, sinais, sensores, velocidade efetiva e replicação.

### Bateria de testes de M1 e regressão

**Status inicial de todos os testes T01–T60: NÃO EXECUTADO EM UNITY.** Casos de funcionalidade ainda ausente serão marcados `lacuna — Mx`, preservados e executados no marco correspondente. A bateria é também o contrato de regressão do projeto.

| ID | Procedimento | Resultado esperado |
|---|---|---|
| T01 | Abrir menu, criar sala, cliente entrar e iniciar | Mesma seed/cena; loading conclui; nenhum jogador sem controle |
| T02 | Sair para menu e criar outra RUN sem reiniciar aplicação | Sem singletons, callbacks, pedidos ou objetos duplicados |
| T03 | Repetir coleta/processamento com host ID 0 e cliente | Mesmas permissões e resultado autoritativo |
| T04 | Dois jogadores tentam pegar o mesmo item no mesmo instante | Um único vencedor; sem duplicação ou perda |
| T05 | Soltar F, afastar-se, abrir menu e desconectar durante preparo | Operação cancela conforme regra; câmera e bancada liberadas |
| T06 | Acessar slots de bancada longa, extremidades e objetos sobrepostos | Interação corresponde ao slot visível, sem alcance atravessando parede |
| T07 | Fazer receita completa: abrir massa, molho, queijo, toppings, forno, caixa | Estado, visual e consumo corretos nas duas instâncias |
| T08 | Colocar ingrediente incompatível e repetir input rápido | Rejeição clara; estoque e pizza íntegros |
| T09 | Assar e deixar queimar; repetir com timer de forno quando existir | Sem upgrade pode queimar; com upgrade desliga no ponto configurado |
| T10 | Descartar pizza pronta, crua e queimada | Destrói só o item escolhido; libera slots e reservas |
| T11 | Encher mochila e tentar adicionar mais uma caixa | Capacidade respeitada sem perda da caixa recusada |
| T12 | Alternar itens entre mãos, bancada, mochila e chão | Posse única; visual remoto coincide |
| T13 | Assumir comanda, consultar e liberar | Pedido legível; prazo não reinicia; painel atualiza |
| T14 | Morrer/desconectar carregando comanda e caixas | Grupo recupera responsabilidade e recursos conforme regra |
| T15 | Entregar pedido de várias pizzas correto de uma vez | Fecha uma vez; recompensa correta; sem saldo duplicado |
| T16 | Entregar conjunto faltando pizza ou com uma errada | Pedido encerra com erro; não aceita complemento posterior |
| T17 | Entregar em endereço errado ou após prazo | Resultado e consequências inequívocos e únicos |
| T18 | Fazer entrega no mesmo instante em que relógio termina | Ordem determinística no servidor; nenhuma entrega duplamente resolvida |
| T19 | Cliente entra, espera, senta, come, levanta e sai | Rota e animação coerentes; cadeira liberada |
| T20 | Mesa ocupada e fila cheia; vários clientes desistem | Sem sobreposição, cadeira órfã ou fila permanentemente presa |
| T21 | Colocar destino além de parede/área restrita | NPC usa rota válida ou recusa; não cruza parede |
| T22 | Desabilitar acesso ao NavMesh para um NPC de teste | Recuperação válida ou diagnóstico; nunca caminhar em linha reta por obstáculos |
| T23 | Bloquear corredor durante rota e liberar depois | Replanejamento controlado; retomada sem teleporte arbitrário |
| T24 | Gerar três seeds pequenas e duas maiores, repetir cada seed | Portas/ruas/endereço acessíveis; mesma configuração reproduz geometria |
| T25 | Observar dez carros por cinco minutos | Cada veículo tem caminho e movimento ou motivo explícito de parada |
| T26 | Acompanhar dois ciclos completos de semáforo | Filas avançam; nenhum travamento geral ou entrada incompatível |
| T27 | Aproximar carros com colliders filhos e carro parado à frente | Sensor identifica veículo correto e retoma após liberação |
| T28 | Pedestre cruza rua e moto bloqueia faixa | Frenagem/colisão seguem regras; sem atravessamento silencioso |
| T29 | Segurar tiro da pistola por dois segundos e soltar | Um tiro por pressionamento; outro só após novo clique |
| T30 | Dez cliques válidos, pente vazio, recarga e troca rápida | Munição e cadência coerentes; nenhuma recarga duplicada |
| T31 | Mirar, abrir mapa/menu, soltar botão e fechar | Sem tiro residual; mira/cursor/câmera voltam ao estado correto |
| T32 | Mirar e atirar perto de parede e em alvo remoto | Impacto coerente; não disparar através de obstáculo junto à arma |
| T33 | Dois jogadores usam duas pistolas distintas | Cooldowns e munições independentes por instância |
| T34 | Montar moto com uma mão livre e pistola na outra | Dirige, mira e atira; servidor aceita composição |
| T35 | Tentar montar com arma de duas mãos em uso | Recusa clara; nenhuma mão ou arma perdida |
| T36 | Guardar espingarda, cozinhar e sacar com ambas livres | Bandoleira libera mãos; saque reserva duas atomicamente |
| T37 | Tentar sacar espingarda pilotando ou com item numa mão | Recusa mantém estado anterior |
| T38 | Atropelar NPC com múltiplos colliders | Um dano e uma queda; não repetir ao permanecer encostado |
| T39 | Repetir atropelamento como cliente remoto | Mesma decisão no servidor; câmera/posição corrigidas |
| T40 | Cair ao lado de parede, morrer pilotando e desconectar pilotando | Saída válida, moto liberada, mãos e controles restaurados |
| T41 | Dano rompe mochila cheia | Quedas de caixas e capacidade consistentes; sem duplicação |
| T42 | Reparar mochila; interromper antes de terminar | Regra consistente e replicada; nada termina sozinho após cancelar |
| T43 | Ferir aliado, curar e sofrer ataque aos 90% da interação | Cura interrompe e volta a zero |
| T44 | Incapacitar, resgatar, deixar morrer e espectar | Sem auto-resgate; espectador não interage; retorno na próxima noite vencida |
| T45 | Incapacitar todos, inclusive condutor | Derrota única; nenhum avanço de noite posterior |
| T46 | Ganhar meta 100 e gastar até saldo 20 | Vitória financeira mantida |
| T47 | Bater meta com tempo restante | Noite continua; novo faturamento é contabilizado |
| T48 | Encerrar noite com pedidos abertos | Penalidades aplicadas uma vez antes da avaliação; sem graça extra |
| T49 | Avançar carregando ferramentas, arma, pizza e suprimento pendente | Recursos preservados conforme tabela; sem ID fixo inválido nem munição grátis |
| T50 | Atacar cliente dentro e fora do volume da pizzaria | Penalidade territorial correta, sem multiplicação por collider |
| T51 | Completar pedidos com reputação cheia e baixa | Recuperação respeita teto; interface mostra causa |
| T52 | Começar com 1/2/4 humanos; alguém morre ou sai | Meta congelada na noite; recalculada somente depois |
| T53 | Votação empatada, alteração de voto e desconexão | Um voto válido por pessoa; empate sorteado uma vez; sem deadlock |
| T54 | Todos prontos, cliente desmarca antes do início | Transição atômica; não iniciar duas vezes nem ignorar estado vigente |
| T55 | Aplicar modificador temporário e permanente por duas noites | Temporário removido; permanente não duplica aplicação |
| T56 | Comprar simultaneamente com saldo para uma compra | Uma compra aceita; saldo nunca negativo; item único |
| T57 | NPC perde alvo por distância/tempo e encontra outro jogador | Reset individual e hostilidade da espécie seguem regras |
| T58 | Funcionários e humano disputam mesmo pedido/utensílio | Reservas exclusivas; cancelamento libera tarefa |
| T59 | Completar terceira noite, perder e iniciar outra RUN | Vitória final ou derrota corretas; nova RUN limpa todas as persistências |
| T60 | Jogar RUN inteira com duas instâncias e observar desempenho | Sem bloqueio, erros recorrentes, crescimento contínuo de objetos ou divergência de estado |

### Testes automatizados úteis a acrescentar

Usar o Test Runner do Editor e serviços puros existentes. Priorizar: transação definitiva de pedido multipizza; faturamento independente do saldo; liquidação única no fechamento; escala congelada; reservas de mãos e bandoleira; idempotência do atropelamento; cura interrompida por dano; expiração de modificadores; votação empatada/desconexão; compra concorrente; reservas de funcionário e conversão de zumbi com teto. Testes físicos/UI/rede exigem PlayMode, não simulações textuais.

### Critério de saída de M1

Baseline identificada, achados classificados, nenhuma falha P0/P1 bloqueando o ciclo já existente, causas dos relatos principais investigadas, testes afetados repetidos. Lacunas novas têm marco e teste vinculados. Não exigir que M1 entregue funcionários ou monstros; exigir que não sejam esquecidos.

Severidades: P0 impede iniciar/terminar ou corrompe estado; P1 quebra ação central/rede; P2 prejudica clareza ou balanceamento com contorno; P3 cosmético. Falha nova P0/P1 entra na própria fase e bloqueia sua conclusão. P2/P3 pode ser alocada ao marco responsável, com justificativa.

## 6. M2 — Cidade, navegação e trânsito

1. Diagnosticar carros parados por cadeia: `Initialize` chamado? grafo pronto? rota com dois pontos? velocidade > 0? servidor atualiza? sensor acusa qual objeto? semáforo libera qual faixa? cliente recebe e interpola? Criar overlay opcional com ID/rota/estado/motivo da parada, sem flood de logs.
2. Separar superfícies caminháveis, portas válidas, interiores autorizados, áreas restritas e posições de cadeira. Validar que a geração dos prédios/calçadas produz continuidade real.
3. Recuperação de NPC fora da malha usa ponto validado e caminho compatível; `SamplePosition` perto do destino não prova conectividade. Não atravessar parede para corrigir travamento.
4. Reservar movimentos diretos de sentar/levantar ao trecho curto validado da cadeira. Autoridade lógica e representação remota não podem disputar o Transform.
5. Trânsito usa faixas/direções, prioridade em cruzamento, sensores com máscara e hierarquia corretas, frenagem e retomada. Evitar frear pelo próprio collider ou por carro da pista oposta.
6. Colisões reais com pedestres/moto, resposta de dano parametrizada e servidor decidindo resultado. Sensor de prevenção e atropelamento são coisas distintas; nem toda parada gera dano.
7. Rotinas: circular, entrar/sair de prédio, ir à pizzaria, atendimento, saída. Endereços de delivery são subconjunto da cidade e escalam com seu tamanho.
8. Manter a cidade durante a RUN. Expansão é extensão posterior: adicionar novos blocos preservando IDs, ruas, endereços e seed existente; habilitar somente após protótipo sem regressão.

**Aceite:** T19–T28, T39–T40 e cinco seeds aprovados; NPC normal não atravessa barreiras e carro parado apresenta motivo recuperável. Fantasma terá exceção explícita, nunca liberada globalmente para todos.

## 7. M3 — Mãos, armas, moto e attachment visual

1. Unificar ocupação lógica: mão esquerda/direita, reserva de direção, arma de duas mãos, bandoleira e mochila. Tirar bloqueios globais incompatíveis.
2. Criar IDs de instância de armas preservando rack e definições. Guardar não significa desequipar para o rack nem repor munição.
3. Definir transições pegar/guardar/sacar/largar/montar/desmontar/cair/morrer; cada transição valida e confirma no servidor, depois atualiza visual.
4. Corrigir pistola semiautomática, cancelamento do input, recarga, cooldown e feedback. Diferenciar o clique de disparo dos cliques de UI. Ao fechar modal exigir novo pressionamento.
5. Implementar mira funcional e sincronização da pose remota. Calcular direção pela câmera com verificação de obstrução próxima à arma; preservar distinção entre intenção e origem física do tiro.
6. Integrar moto real por prefab configurável e anchors de assento/guidão/saída, sem depender da orientação arbitrária da mesh. Moto permite uma mão livre.
7. Atropelamento: validar condutor, alvo, velocidade mínima e contato; emitir ID do impacto; aplicar dano uma vez; desmontar; escolher posição livre; liberar reserva e câmera. Sem loop de colisão acumulando dano.
8. Criar/reaproveitar authoring visual no wizard: contexto mão esquerda/direita, bancada, pizza e bandoleira; bone/path escolhido; origem e extremidade desejadas; offsets de posição/rotação/escala; preview animado.
9. Para escolha “head/nose ou tail” do bone, não presumir que o Transform importado contém o tail do Blender. Representar extremidade por anchor filho explícito ou offset de comprimento/direção configurado. Permitir editar e validar em preview, sem renomear o rig.
10. Diferenciar posição lógica do item e pivô cosmético de Feel. Animação não deve alterar collider/estado autoritativo inadvertidamente.
11. Depois da base: rifle automático configurável e granada com lançamento, fusível, explosão única, dano por alvo e sincronização. Se prazo impedir, registrar corte de conteúdo explicitamente; espingarda/bandoleira permanecem requisito desta conversa.

**Aceite:** T12 e T29–T42; moto+pistola funciona como cliente; espingarda guardada libera mãos e não dispara; atropelamento dá um dano e derruba uma vez.

## 8. M4 — Uma noite completa

1. Ajustar começo/fim de expediente, contabilização e resultado. Evitar lógica duplicada em HUD, servidor e serviços.
2. Fechar todo o ciclo balcão/telefone → comanda → preparo → forno → embalagem → mochila/mesa → avaliação → pagamento/penalidade → descarte de entidades.
3. Entrega multipizza confirma o conjunto numa única ação. Produzir resumo por pizza e resultado total. Não usar `DeliveredQuantity` para manter uma entrega parcial aberta contra a regra definida.
4. Reputação compartilhada com teto/piso, razões visíveis e deduplicação de eventos. Volume territorial da pizzaria preciso.
5. UI de reposição, encomenda paga com ETA, chegada acessível, quantidade recebida e estoque. Novo ingrediente só entra na demanda quando seu desbloqueio e meios de preparo forem válidos; falta por má gestão continua possível.
6. Separar vivos, incapacitados e mortos. Remover auto-resgate somente ao integrar ajuda, timer de morte configurável, espectador e retorno noturno. Cura é interrompida por dano e reiniciada do zero.
7. Definir tratamento de todos incapacitados: se ninguém puder ajudar, encerrar como derrota, sem esperar indefinidamente.
8. Mochila rasgada: capacidade reduzida, caixas caem, pizzas podem inutilizar-se, reparo. Não consumir a comanda por perda de pizza; refazer até a entrega definitiva/prazo.
9. No fim da noite, resolver todos os pedidos e preservar recursos conforme tabela. Usar registro de estações e slots livres, não ID 3 fixo.
10. Provar vitória e derrota por cada condição em uma noite curta de desenvolvimento, depois no tempo real previsto.

**Aceite:** T07–T20, T41–T52. Uma noite completa sem intervenção de Inspector; faturamento correto mesmo após compras; cliente morto especta; todos retornam após noite vencida.

## 9. M5 — Monstros e hostilidade

Construir uma base comum de estado com rotina, cliente, agressivo, ataque, recuperação, desistência e morto. Usar capacidades por espécie; não duplicar cinco implementações completas de NPC.

1. Humanos agressivos validam perseguição/ataque/dano/reset.
2. Vampiros usam anchors de telhado elegíveis, posição segura de descida e ataque antecipado. Sem teleporte para local inválido; distância/tempo governam aquisição e abandono.
3. Lobisomem usa investida com direção comprometida e recuperação. Evitar dano por frame durante contato.
4. Fantasma ignora obstáculos selecionados apenas em sua locomoção; altura acompanha chão válido, blinks têm destinos válidos e cooldown. Não compartilhar máscara permissiva com humanos.
5. Zumbi protótipo converte alvo elegível uma vez; preserva ID ou migra referência com encerramento correto da rotina/pedido anterior. Teto impede explosão de agentes.
6. Hostilidade por espécie contra equipe, com teto, causas e duração configuráveis. Alho contra vampiro é um caso de dado do catálogo, não condicional espalhada em vários scripts.
7. Invasões afetam pessoas; dano a cliente dentro do volume prejudica reputação. Evitar penalidade por frame e cascata ilimitada de reputação por uma única colisão.
8. Separar apresentação (olhos, som, efeitos) da decisão; clientes veem mesma postura e vida.

**Aceite:** cada espécie circula/atende, agride sob gatilho, causa dano, permite resposta e reseta conforme regra; T43–T45, T50, T57 e testes de limites/conversão. Vampiro primeiro; demais entram um a um após aprovação.

## 10. M6 — RUN, votos, loja e variedade

Fluxo: lobby → tutorial opcional → preparação/escolhas → todos prontos → noite → liquidação/resultado → próxima preparação ou fim da RUN.

1. Ampliar configuração para N noites, preset de entrega com três e futura extensão para sete. Parar na última, não avançar indefinidamente.
2. Metas tabeladas por humanos e noite são preferíveis inicialmente a fórmula oculta. Dificuldade define restrições adicionais. Mostrar exigências antes do início.
3. Todas as escolhas sorteadas oferecem mais de uma opção. Pools por noite/dificuldade; grupo vota; empate sorteado no servidor. Sem resultados diferentes por cliente.
4. Eventos/entraves escolhidos são anunciados antes da noite; seu efeito pode se manifestar durante ela, mas não adicionar um sistema de novos sorteios surpresa no meio da noite.
5. Loja livre usa dinheiro coletivo e qualquer jogador compra. Compras simultâneas são transações atômicas; não adicionar votação de compra individual que o usuário não pediu.
6. Modificadores: ID, duração, escopo, elegibilidade, incompatibilidades, stacks, valor e descrição. Recalcular a partir da base; evitar multiplicar repetidamente a cada refresh.
7. Sorteio precisa oferecer alternativas válidas. Se pool esgotar, usar fallback elegível documentado; nunca abrir votação com zero opções ou duas cópias equivalentes.
8. Streams de aleatoriedade separados para mapa, escolhas e erros dos funcionários, de modo que um efeito cosmético não altere o resultado futuro das escolhas. Registrar seed + preset + versão; entradas dos jogadores também afetam o resultado da RUN.
9. Dificuldade normal aumenta demanda/distâncias/agressividade/conteúdo, sem reduzir duração nem aumentar preços automaticamente. Preset difícil pode reduzir tempo explicitamente.
10. Vitória final e derrota oferecem nova RUN/menu. Persistência só durante a RUN; nenhum snapshot de produção permite retomar a noite perdida.

Catálogo inicial proposto, com números configuráveis:

| Opção | Tipo/duração sugerida | Efeito |
|---|---|---|
| Lua vermelha | Evento de uma noite | Mais vampiros na população |
| Ataque dos vampiros | Entrave de uma noite | Vampiros mais agressivos |
| Timer do forno | Melhoria permanente | Desliga ao alcançar tempo correto |
| Nova variedade | Desbloqueio permanente | Novo ingrediente e receitas/preparo correspondentes |
| Bolsa reforçada | Melhoria permanente | Maior resistência/capacidade conforme nível |
| Movimento intenso | Entrave temporário | Mais pedidos simultâneos |
| Fornecimento expresso | Oportunidade temporária | Menor ETA de reposição |
| Treinamento | Melhoria permanente | Menor erro do pizzaiolo |

Esses exemplos formam uma pool inicial, não conteúdo já implementado. Evitar combinações que exijam ingrediente/estação inexistente; distinguir impossibilidade técnica de falta de recurso causada por escolhas do grupo.

**Aceite:** T46–T56 e T59–T60; três noites com pelo menos uma escolha temporária e uma permanente, resultado final correto e nova RUN limpa.

## 11. M7 — Balconistas e pizzaiolos

1. Contratar pela loja coletiva, preço por função/nível e spawn navegável. Balconista caro; efeito da contratação visível.
2. Balconista atende e encaminha pedidos, sem fabricar pizza nem criar dinheiro por atalho.
3. Pizzaiolo assume comanda, verifica receita/estoque/equipamento, reserva recursos e percorre as etapas reais. Não criar pizza pronta instantaneamente atrás do jogador.
4. Coordenador de tarefas com estados disponível/reservada/em execução/bloqueada/concluída/cancelada. Reserva expira ou é liberada por morte, desconexão, intervenção e fim da noite.
5. Mostrar pedido atual e motivo de bloqueio: sem massa, forno ocupado, utensílio usado, caminho bloqueado. Jogador pode retirar tarefa ou assumir.
6. Erro intencional é sorteado em etapa definida e reproduzível: ingrediente errado ou ponto, por exemplo. Defeito de pathfinding não conta como erro de habilidade. Upgrades reduzem probabilidade dentro de limites.
7. Funcionários compartilham estoque, utensílios e bancadas. Não alterar meta apenas por contratá-los inicialmente. NPC extra contratado não ocupa vaga humana do lobby.
8. Persistir contratação/nível entre noites e limpar só na nova RUN. Proposta: restituir funcionário perdido ao começar a próxima noite junto do reset da população; deixar essa política editável.

**Aceite:** T58; balconista realiza um ciclo de atendimento; pizzaiolo produz uma pizza correta usando recursos reais; erro forçado funciona; humano interrompe e assume sem duplicar nem travar.

## 12. M8 — Tutorial, acabamento e entrega

1. Noite zero opcional/previsível: pegar itens, preparar pizza, assar, embalar, assumir comanda, entregar, repor e ajudar colega. Sem meta punitiva inicial. Caixa lateral recolhível; clique só quando cursor estiver disponível, sem conflito com tiro.
2. HUD e telas: relógio, faturamento/meta, saldo, reputação, vida, participantes, munição, mãos, bandoleira, mochila; detalhes de pedidos sob consulta e prazos assumidos sempre visíveis. Loading, pausa, votos e resultados com estados claros.
3. Integrar áudio, Feel/DOTween já presentes, impactos, antecipações e feedback de rejeição; não deixar efeitos mudarem autoridade, física ou posição lógica.
4. Manter estilo cartoon consistente entre personagem, ingredientes, prédios e moto. Cutscene existente entra por integração opcional, sem inventar história progressiva entre noites.
5. Validar mapa, endereços, zoom/pan e orientação; arma não dispara ao clicar na UI.
6. Rodar RUN inteira de 30–45 minutos como alvo de balanceamento; medir ociosidade, tempo de produção/entrega, taxa de erros, mortes, faturamento e uso de upgrades. Ajustar presets à mão.
7. Medir frame time, quantidade de agentes, memória e tráfego. Evitar custos por frame proporcionais a todos os NPCs quando for possível atualizar por intervalos. Definir orçamento a partir da máquina usada, sem inventar meta de FPS já garantida.
8. Build candidata só após verificações do Editor; testar fluxo completo fora do Editor quando a build for gerada no ambiente do projeto. Não afirmar que compilar garante jogar.

**Aceite final:** abrir build → criar sala → entrar com colega → jogar três noites → ganhar/perder → voltar/recomeçar; sem Inspector, sem explicação verbal obrigatória e sem estado herdado da RUN anterior.

## 13. Janela sugerida até 2 de dezembro

Estimativa de organização, não promessa de esforço. Marcos continuam sequenciais por aceite; ajustar após M1 revelar o volume real.

| Janela | Foco |
|---|---|
| 17–18/11 | M1: auditoria e bloqueadores |
| 19–20/11 | M2: navegação e trânsito |
| 21–22/11 | M3: mãos, combate e moto |
| 23–24/11 | M4: noite completa |
| 25–26/11 | M5: vampiro e demais espécies por capacidade |
| 27–28/11 | M6: três noites, votos e modificadores |
| 29/11 | M7: funcionários; alto risco, reavaliar após M1 |
| 30/11–01/12 | M8: regressão, balanceamento e build candidata |
| 02/12 | Entrega da versão já validada |

A ambição é alta para desenvolvimento concentrado numa pessoa. Principal risco: IA de pizzaiolo com manipulação completa, seguida de quatro espécies, trânsito físico e migração de inventário. Se houver atraso, propor primeiro reduzir variedade de modificadores, expansão da cidade e arsenal adicional. Depois simplificar autonomia dos funcionários mantendo função clara. Qualquer corte de requisito confirmado deve ser apresentado como decisão de escopo, não omitido.

Não trocar estabilidade de duas pessoas por muitos sistemas que só funcionam no host. Reservar o final para regressão; não estrear uma arquitetura nova em 1º de dezembro.

## 14. Entregáveis e protocolo por marco

Para cada marco registrar:

- SHA inicial/final, arquivos alterados e assets configurados.
- Comportamento antes/depois e parâmetros expostos.
- Testes executados, resultados e evidências; testes não executados separados.
- Novas falhas com severidade e marco dono.
- Passo a passo curto para o Rubens validar no Editor.
- Resultado do aceite e próximo marco liberado.

Arquivos sugeridos no repositório, a criar quando a implementação começar: `Docs/Release/ROADMAP.md`, `M1_AUDIT.md`, `TEST_MATRIX.md`, `PLAYTEST_RESULTS.md`, além de manter CURRENT_STATE e CHANGELOG existentes. Evitar duplicar documentação longa em cada sessão.

Modelo de ocorrência:

`ID | severidade | status | SHA | seed/preset | host/cliente | reprodução | esperado | observado | evidência | causa confirmada/hipótese | correção | teste de regressão | marco`

### Prompt para executar somente a primeira fase

> Leia este roadmap e os AGENTS.md aplicáveis. Execute somente M1. Primeiro identifique branch/SHA e alterações locais, leia INDEX e CURRENT_STATE e compare os achados A01–A12 com o código atual. Preserve mudanças locais, GUIDs e overrides. Não implemente os marcos seguintes em lote. Audite o ciclo inteiro usando T01–T60, marcando recursos ainda inexistentes como lacunas do marco correspondente. Faça as verificações estáticas disponíveis e execute testes pertinentes pelo Unity Editor já aberto quando houver controle disponível. Se não houver, forneça os passos e marque NÃO EXECUTADO; não simule resultado. Não inicie Unity headless nem builds automáticas. Corrija bloqueadores P0/P1 do ciclo existente, investigue especificamente tiro repetido, mira, NPCs atravessando obstáculos e carros imóveis, e incorpore novos erros à fase com evidências. Distinga fatos de hipóteses. Atualize documentação e entregue relatório de M1, matriz de testes e passos de aceite. Só avance de marco após a validação do resultado jogável.

### Observação final de evidência

Este plano já incorpora problemas adicionais encontrados na inspeção estática, mas **não declara que a bateria de gameplay foi executada**. A presença de testes no projeto e o status “implementado” da documentação não substituem a validação em Unity. O primeiro marco existe justamente para transformar essas pendências em resultados verificáveis antes de ampliar o jogo.

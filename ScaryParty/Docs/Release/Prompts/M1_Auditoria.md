# Executar M1 — Auditoria

## Instrução principal

Execute integralmente esta fase dentro do escopo abaixo, incluindo implementação/correções, integração, verificações disponíveis e documentação. Leia `Docs/Release/CONTRATO_EXECUCAO.md` e siga suas regras. Não se limite a propor um plano e não avance para M2 automaticamente.

Caminhos relativos à raiz Unity. Roadmap: `Docs/Release/ROADMAP.md`. Relatório: `Docs/Release/Reports/M1_Relatorio.md`.

## Preparação

Nenhuma fase anterior.

Registre branch/SHA e alterações locais. Confirme o estado real dos serviços citados antes de editar. Este prompt é uma versão inicial preparada com o roadmap; adapte detalhes de implementação à branch atual sem mudar decisões de produto. Registre divergências e propostas no relatório.

## Orientação específica

Audite todos os T01–T60, mas execute apenas casos possíveis sobre sistemas existentes. Classifique o restante como LACUNA PREVISTA com fase responsável; não tente implementar todo o roadmap para fazer a matriz passar. Revalide A01–A12 na branch local: podem já estar corrigidos. Corrija P0/P1 do ciclo existente, incluindo defeitos adicionais encontrados. Investigue os quatro relatos: disparo repetido, ausência de mira, carros imóveis e travessia indevida de NPCs. Não declare causa raiz apenas por coincidência de código. Para A07 reproduza a condição de fallback; para trânsito capture inicialização, grafo, velocidade, sensores e semáforo. Preserve migrações maiores de mãos/moto e sistemas novos para as fases responsáveis. Gere baseline, achados priorizados e evidência de cada correção.

## Tarefas e critérios do marco

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



## Entrega obrigatória

- Execute todas as tarefas independentes possíveis desta fase.
- Atualize os casos pertinentes em `Docs/Release/TEST_MATRIX.md`, preservando IDs e evidências.
- Registre novos defeitos e respectivas prioridades/marcos no relatório.
- Atualize `Docs/Release/STATUS.md`, CURRENT_STATE e CHANGELOG, sem declarar validação inexistente.
- Entregue instruções manuais reproduzíveis para os testes não executados.
- Pare no término desta fase. Se faltar validação jogável, use IMPLEMENTAÇÃO CONCLUÍDA — VALIDAÇÃO PENDENTE.

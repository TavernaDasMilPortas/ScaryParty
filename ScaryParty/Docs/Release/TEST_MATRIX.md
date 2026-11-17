# Matriz de testes — acompanhamento

O ROADMAP contém a especificação original. Este arquivo registra a execução e não comprova que ela ocorreu. Todos os casos começam NÃO EXECUTADO. Para sistema ausente usar LACUNA — Mx. Resultado anterior não substitui regressão na versão alterada.

Estados: NÃO EXECUTADO / PASSOU / FALHOU / BLOQUEADO / LACUNA — Mx. Para cada execução registrar data, SHA, preset/seed e host/cliente em evidência ou relatório. Preservar histórico relevante.

| ID | Procedimento | Esperado | Status | Evidência/relatório |
|---|---|---|---|---|
| T01 | Abrir menu, criar sala, cliente entrar e iniciar | Mesma seed/cena; loading conclui; nenhum jogador sem controle | NÃO EXECUTADO | — |
| T02 | Sair para menu e criar outra RUN sem reiniciar aplicação | Sem singletons, callbacks, pedidos ou objetos duplicados | NÃO EXECUTADO | — |
| T03 | Repetir coleta/processamento com host ID 0 e cliente | Mesmas permissões e resultado autoritativo | NÃO EXECUTADO | — |
| T04 | Dois jogadores tentam pegar o mesmo item no mesmo instante | Um único vencedor; sem duplicação ou perda | NÃO EXECUTADO | — |
| T05 | Soltar F, afastar-se, abrir menu e desconectar durante preparo | Operação cancela conforme regra; câmera e bancada liberadas | NÃO EXECUTADO | — |
| T06 | Acessar slots de bancada longa, extremidades e objetos sobrepostos | Interação corresponde ao slot visível, sem alcance atravessando parede | NÃO EXECUTADO | — |
| T07 | Fazer receita completa: abrir massa, molho, queijo, toppings, forno, caixa | Estado, visual e consumo corretos nas duas instâncias | NÃO EXECUTADO | — |
| T08 | Colocar ingrediente incompatível e repetir input rápido | Rejeição clara; estoque e pizza íntegros | NÃO EXECUTADO | — |
| T09 | Assar e deixar queimar; repetir com timer de forno quando existir | Sem upgrade pode queimar; com upgrade desliga no ponto configurado | NÃO EXECUTADO | — |
| T10 | Descartar pizza pronta, crua e queimada | Destrói só o item escolhido; libera slots e reservas | NÃO EXECUTADO | — |
| T11 | Encher mochila e tentar adicionar mais uma caixa | Capacidade respeitada sem perda da caixa recusada | NÃO EXECUTADO | — |
| T12 | Alternar itens entre mãos, bancada, mochila e chão | Posse única; visual remoto coincide | NÃO EXECUTADO | — |
| T13 | Assumir comanda, consultar e liberar | Pedido legível; prazo não reinicia; painel atualiza | NÃO EXECUTADO | — |
| T14 | Morrer/desconectar carregando comanda e caixas | Grupo recupera responsabilidade e recursos conforme regra | NÃO EXECUTADO | — |
| T15 | Entregar pedido de várias pizzas correto de uma vez | Fecha uma vez; recompensa correta; sem saldo duplicado | NÃO EXECUTADO | — |
| T16 | Entregar conjunto faltando pizza ou com uma errada | Pedido encerra com erro; não aceita complemento posterior | NÃO EXECUTADO | — |
| T17 | Entregar em endereço errado ou após prazo | Resultado e consequências inequívocos e únicos | NÃO EXECUTADO | — |
| T18 | Fazer entrega no mesmo instante em que relógio termina | Ordem determinística no servidor; nenhuma entrega duplamente resolvida | NÃO EXECUTADO | — |
| T19 | Cliente entra, espera, senta, come, levanta e sai | Rota e animação coerentes; cadeira liberada | NÃO EXECUTADO | — |
| T20 | Mesa ocupada e fila cheia; vários clientes desistem | Sem sobreposição, cadeira órfã ou fila permanentemente presa | NÃO EXECUTADO | — |
| T21 | Colocar destino além de parede/área restrita | NPC usa rota válida ou recusa; não cruza parede | NÃO EXECUTADO | — |
| T22 | Desabilitar acesso ao NavMesh para um NPC de teste | Recuperação válida ou diagnóstico; nunca caminhar em linha reta por obstáculos | NÃO EXECUTADO | — |
| T23 | Bloquear corredor durante rota e liberar depois | Replanejamento controlado; retomada sem teleporte arbitrário | NÃO EXECUTADO | — |
| T24 | Gerar três seeds pequenas e duas maiores, repetir cada seed | Portas/ruas/endereço acessíveis; mesma configuração reproduz geometria | NÃO EXECUTADO | — |
| T25 | Observar dez carros por cinco minutos | Cada veículo tem caminho e movimento ou motivo explícito de parada | NÃO EXECUTADO | — |
| T26 | Acompanhar dois ciclos completos de semáforo | Filas avançam; nenhum travamento geral ou entrada incompatível | NÃO EXECUTADO | — |
| T27 | Aproximar carros com colliders filhos e carro parado à frente | Sensor identifica veículo correto e retoma após liberação | NÃO EXECUTADO | — |
| T28 | Pedestre cruza rua e moto bloqueia faixa | Frenagem/colisão seguem regras; sem atravessamento silencioso | NÃO EXECUTADO | — |
| T29 | Segurar tiro da pistola por dois segundos e soltar | Um tiro por pressionamento; outro só após novo clique | NÃO EXECUTADO | — |
| T30 | Dez cliques válidos, pente vazio, recarga e troca rápida | Munição e cadência coerentes; nenhuma recarga duplicada | NÃO EXECUTADO | — |
| T31 | Mirar, abrir mapa/menu, soltar botão e fechar | Sem tiro residual; mira/cursor/câmera voltam ao estado correto | NÃO EXECUTADO | — |
| T32 | Mirar e atirar perto de parede e em alvo remoto | Impacto coerente; não disparar através de obstáculo junto à arma | NÃO EXECUTADO | — |
| T33 | Dois jogadores usam duas pistolas distintas | Cooldowns e munições independentes por instância | LACUNA — M3 | [M1_Relatorio](Reports/M1_Relatorio.md) (A08: cooldown por WeaponType) |
| T34 | Montar moto com uma mão livre e pistola na outra | Dirige, mira e atira; servidor aceita composição | LACUNA — M3 | [M1_Relatorio](Reports/M1_Relatorio.md) (A02/A03: bloqueio total de mãos/direção) |
| T35 | Tentar montar com arma de duas mãos em uso | Recusa clara; nenhuma mão ou arma perdida | NÃO EXECUTADO | — |
| T36 | Guardar espingarda, cozinhar e sacar com ambas livres | Bandoleira libera mãos; saque reserva duas atomicamente | LACUNA — M3 | [M1_Relatorio](Reports/M1_Relatorio.md) (bandoleira ausente) |
| T37 | Tentar sacar espingarda pilotando ou com item numa mão | Recusa mantém estado anterior | LACUNA — M3 | [M1_Relatorio](Reports/M1_Relatorio.md) (bandoleira ausente) |
| T38 | Atropelar NPC com múltiplos colliders | Um dano e uma queda; não repetir ao permanecer encostado | NÃO EXECUTADO | — |
| T39 | Repetir atropelamento como cliente remoto | Mesma decisão no servidor; câmera/posição corrigidas | NÃO EXECUTADO | — |
| T40 | Cair ao lado de parede, morrer pilotando e desconectar pilotando | Saída válida, moto liberada, mãos e controles restaurados | NÃO EXECUTADO | — |
| T41 | Dano rompe mochila cheia | Quedas de caixas e capacidade consistentes; sem duplicação | NÃO EXECUTADO | — |
| T42 | Reparar mochila; interromper antes de terminar | Regra consistente e replicada; nada termina sozinho após cancelar | NÃO EXECUTADO | — |
| T43 | Ferir aliado, curar e sofrer ataque aos 90% da interação | Cura interrompe e volta a zero | LACUNA — M4 | [M1_Relatorio](Reports/M1_Relatorio.md) (A04: cura aliada ausente) |
| T44 | Incapacitar, resgatar, deixar morrer e espectar | Sem auto-resgate; espectador não interage; retorno na próxima noite vencida | LACUNA — M4 | [M1_Relatorio](Reports/M1_Relatorio.md) (A04: auto-resgate 10s ativo) |
| T45 | Incapacitar todos, inclusive condutor | Derrota única; nenhum avanço de noite posterior | NÃO EXECUTADO | — |
| T46 | Ganhar meta 100 e gastar até saldo 20 | Vitória financeira mantida | NÃO EXECUTADO | — |
| T47 | Bater meta com tempo restante | Noite continua; novo faturamento é contabilizado | NÃO EXECUTADO | — |
| T48 | Encerrar noite com pedidos abertos | Penalidades aplicadas uma vez antes da avaliação; sem graça extra | NÃO EXECUTADO | [M1_Relatorio](Reports/M1_Relatorio.md) (A06: Closing atual dá prazo extra) |
| T49 | Avançar carregando ferramentas, arma, pizza e suprimento pendente | Recursos preservados conforme tabela; sem ID fixo inválido nem munição grátis | NÃO EXECUTADO | [M1_Relatorio](Reports/M1_Relatorio.md) (A09 corrigido; A10 pendente M4/M6) |
| T50 | Atacar cliente dentro e fora do volume da pizzaria | Penalidade territorial correta, sem multiplicação por collider | LACUNA — M4 | [M1_Relatorio](Reports/M1_Relatorio.md) (reputação territorial ausente) |
| T51 | Completar pedidos com reputação cheia e baixa | Recuperação respeita teto; interface mostra causa | LACUNA — M4 | [M1_Relatorio](Reports/M1_Relatorio.md) (sistema de reputação ausente) |
| T52 | Começar com 1/2/4 humanos; alguém morre ou sai | Meta congelada na noite; recalculada somente depois | LACUNA — M6 | [M1_Relatorio](Reports/M1_Relatorio.md) (escala dinâmica ausente) |
| T53 | Votação empatada, alteração de voto e desconexão | Um voto válido por pessoa; empate sorteado uma vez; sem deadlock | LACUNA — M6 | [M1_Relatorio](Reports/M1_Relatorio.md) (sistema de votos ausente) |
| T54 | Todos prontos, cliente desmarca antes do início | Transição atômica; não iniciar duas vezes nem ignorar estado vigente | LACUNA — M6 | [M1_Relatorio](Reports/M1_Relatorio.md) (preparação votada ausente) |
| T55 | Aplicar modificador temporário e permanente por duas noites | Temporário removido; permanente não duplica aplicação | LACUNA — M6 | [M1_Relatorio](Reports/M1_Relatorio.md) (modificadores ausentes) |
| T56 | Comprar simultaneamente com saldo para uma compra | Uma compra aceita; saldo nunca negativo; item único | LACUNA — M6 | [M1_Relatorio](Reports/M1_Relatorio.md) (loja coletiva da RUN ausente) |
| T57 | NPC perde alvo por distância/tempo e encontra outro jogador | Reset individual e hostilidade da espécie seguem regras | LACUNA — M5 | [M1_Relatorio](Reports/M1_Relatorio.md) (monstros/hostilidade ausentes) |
| T58 | Funcionários e humano disputam mesmo pedido/utensílio | Reservas exclusivas; cancelamento libera tarefa | LACUNA — M7 | [M1_Relatorio](Reports/M1_Relatorio.md) (funcionários ausentes) |
| T59 | Completar terceira noite, perder e iniciar outra RUN | Vitória final ou derrota corretas; nova RUN limpa todas as persistências | LACUNA — M6 | [M1_Relatorio](Reports/M1_Relatorio.md) (fluxo completo de RUN em M6) |
| T60 | Jogar RUN inteira com duas instâncias e observar desempenho | Sem bloqueio, erros recorrentes, crescimento contínuo de objetos ou divergência de estado | LACUNA — M6 | [M1_Relatorio](Reports/M1_Relatorio.md) (RUN completa em M6/M8) |

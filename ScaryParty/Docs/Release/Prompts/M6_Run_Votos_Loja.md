# Executar M6 — Run Votos Loja

## Instrução principal

Execute integralmente esta fase dentro do escopo abaixo, incluindo implementação/correções, integração, verificações disponíveis e documentação. Leia `Docs/Release/CONTRATO_EXECUCAO.md` e siga suas regras. Não se limite a propor um plano e não avance para M7 automaticamente.

Caminhos relativos à raiz Unity. Roadmap: `Docs/Release/ROADMAP.md`. Relatório: `Docs/Release/Reports/M6_Relatorio.md`.

## Preparação

Leia `Reports/M5_Relatorio.md` e os relatórios das dependências indicadas no ROADMAP. Verifique pendências e aceite; não presuma sucesso.

Registre branch/SHA e alterações locais. Confirme o estado real dos serviços citados antes de editar. Este prompt é uma versão inicial preparada com o roadmap; adapte detalhes de implementação à branch atual sem mudar decisões de produto. Registre divergências e propostas no relatório.

## Orientação específica

Todo sorteio de escolhas apresenta alternativas votáveis, incluindo entraves e eventos. Loja livre continua sem votação de cada compra. Todos confirmam pronto após resolver escolhas. Número de humanos/metas fica congelado por noite. Eventos são escolhidos/anunciados antes da noite, sem acrescentar sorteios surpresa durante o expediente. Garantir expiração de temporários, persistência de permanentes e limpeza de nova RUN. Separar dificuldade e seed. Não avançar além da última noite configurada.

## Tarefas e critérios do marco

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



## Entrega obrigatória

- Execute todas as tarefas independentes possíveis desta fase.
- Atualize os casos pertinentes em `Docs/Release/TEST_MATRIX.md`, preservando IDs e evidências.
- Registre novos defeitos e respectivas prioridades/marcos no relatório.
- Atualize `Docs/Release/STATUS.md`, CURRENT_STATE e CHANGELOG, sem declarar validação inexistente.
- Entregue instruções manuais reproduzíveis para os testes não executados.
- Pare no término desta fase. Se faltar validação jogável, use IMPLEMENTAÇÃO CONCLUÍDA — VALIDAÇÃO PENDENTE.

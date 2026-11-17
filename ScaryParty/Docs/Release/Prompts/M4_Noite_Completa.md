# Executar M4 — Noite Completa

## Instrução principal

Execute integralmente esta fase dentro do escopo abaixo, incluindo implementação/correções, integração, verificações disponíveis e documentação. Leia `Docs/Release/CONTRATO_EXECUCAO.md` e siga suas regras. Não se limite a propor um plano e não avance para M5 automaticamente.

Caminhos relativos à raiz Unity. Roadmap: `Docs/Release/ROADMAP.md`. Relatório: `Docs/Release/Reports/M4_Relatorio.md`.

## Preparação

Leia `Reports/M3_Relatorio.md` e os relatórios das dependências indicadas no ROADMAP. Verifique pendências e aceite; não presuma sucesso.

Registre branch/SHA e alterações locais. Confirme o estado real dos serviços citados antes de editar. Este prompt é uma versão inicial preparada com o roadmap; adapte detalhes de implementação à branch atual sem mudar decisões de produto. Registre divergências e propostas no relatório.

## Orientação específica

Este marco entrega uma noite funcional, não exige todas as votações de M6. Não deixar atalhos temporários de prontidão/retry expostos como regra final. Entrega multipizza é definitiva, incluindo erro; não aceitar complemento depois. Considere saldo e faturamento separadamente. Remover auto-resgate apenas junto com o fluxo completo de incapacitação, resgate, morte e espectador. Fechar pedidos e consequências antes de avaliar vitória. Nenhum reset pode apagar suprimento pago ou oferecer munição grátis sem regra explícita.

## Tarefas e critérios do marco

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



## Entrega obrigatória

- Execute todas as tarefas independentes possíveis desta fase.
- Atualize os casos pertinentes em `Docs/Release/TEST_MATRIX.md`, preservando IDs e evidências.
- Registre novos defeitos e respectivas prioridades/marcos no relatório.
- Atualize `Docs/Release/STATUS.md`, CURRENT_STATE e CHANGELOG, sem declarar validação inexistente.
- Entregue instruções manuais reproduzíveis para os testes não executados.
- Pare no término desta fase. Se faltar validação jogável, use IMPLEMENTAÇÃO CONCLUÍDA — VALIDAÇÃO PENDENTE.

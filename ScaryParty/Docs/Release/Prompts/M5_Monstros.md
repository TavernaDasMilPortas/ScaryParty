# Executar M5 — Monstros

## Instrução principal

Execute integralmente esta fase dentro do escopo abaixo, incluindo implementação/correções, integração, verificações disponíveis e documentação. Leia `Docs/Release/CONTRATO_EXECUCAO.md` e siga suas regras. Não se limite a propor um plano e não avance para M6 automaticamente.

Caminhos relativos à raiz Unity. Roadmap: `Docs/Release/ROADMAP.md`. Relatório: `Docs/Release/Reports/M5_Relatorio.md`.

## Preparação

Leia `Reports/M4_Relatorio.md` e os relatórios das dependências indicadas no ROADMAP. Verifique pendências e aceite; não presuma sucesso.

Registre branch/SHA e alterações locais. Confirme o estado real dos serviços citados antes de editar. Este prompt é uma versão inicial preparada com o roadmap; adapte detalhes de implementação à branch atual sem mudar decisões de produto. Registre divergências e propostas no relatório.

## Orientação específica

Implemente capacidades compartilhadas e espécies em sequência: humano agressivo, vampiro prioritário, lobisomem, fantasma e protótipo de zumbi. Não introduza visão/oclusão como requisito de perseguição. Use distância e tempo. Diferencie reset individual e hostilidade da espécie. Propostas ainda não confirmadas no roadmap devem ficar configuráveis e documentadas como proposta. Zumbi precisa de teto de conversão/população; fantasma precisa de destino seguro; vampiro precisa de pontos altos válidos. Nenhuma espécie pode furar autoridade de dano ou duplicar pedidos ao mudar de estado.

## Tarefas e critérios do marco

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



## Entrega obrigatória

- Execute todas as tarefas independentes possíveis desta fase.
- Atualize os casos pertinentes em `Docs/Release/TEST_MATRIX.md`, preservando IDs e evidências.
- Registre novos defeitos e respectivas prioridades/marcos no relatório.
- Atualize `Docs/Release/STATUS.md`, CURRENT_STATE e CHANGELOG, sem declarar validação inexistente.
- Entregue instruções manuais reproduzíveis para os testes não executados.
- Pare no término desta fase. Se faltar validação jogável, use IMPLEMENTAÇÃO CONCLUÍDA — VALIDAÇÃO PENDENTE.

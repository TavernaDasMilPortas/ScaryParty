# Executar M7 — Funcionarios

## Instrução principal

Execute integralmente esta fase dentro do escopo abaixo, incluindo implementação/correções, integração, verificações disponíveis e documentação. Leia `Docs/Release/CONTRATO_EXECUCAO.md` e siga suas regras. Não se limite a propor um plano e não avance para M8 automaticamente.

Caminhos relativos à raiz Unity. Roadmap: `Docs/Release/ROADMAP.md`. Relatório: `Docs/Release/Reports/M7_Relatorio.md`.

## Preparação

Leia `Reports/M6_Relatorio.md` e os relatórios das dependências indicadas no ROADMAP. Verifique pendências e aceite; não presuma sucesso.

Registre branch/SHA e alterações locais. Confirme o estado real dos serviços citados antes de editar. Este prompt é uma versão inicial preparada com o roadmap; adapte detalhes de implementação à branch atual sem mudar decisões de produto. Registre divergências e propostas no relatório.

## Orientação específica

Não criar bot onisciente substituindo jogador. Entregar balconista e pizzaiolo com funções, reservas e motivos de bloqueio claros. Ambos usam recursos e serviços reais. Pizzaiolo erra por regra controlada, não por travamento. Contratação/upgrades persistem; não ocupam vaga humana nem alteram automaticamente a meta. Priorize um pedido completo com intervenção humana e concorrência antes de multiplicar funcionários. Não gerar pizza pronta sem consumo de ingredientes como atalho final.

## Tarefas e critérios do marco

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



## Entrega obrigatória

- Execute todas as tarefas independentes possíveis desta fase.
- Atualize os casos pertinentes em `Docs/Release/TEST_MATRIX.md`, preservando IDs e evidências.
- Registre novos defeitos e respectivas prioridades/marcos no relatório.
- Atualize `Docs/Release/STATUS.md`, CURRENT_STATE e CHANGELOG, sem declarar validação inexistente.
- Entregue instruções manuais reproduzíveis para os testes não executados.
- Pare no término desta fase. Se faltar validação jogável, use IMPLEMENTAÇÃO CONCLUÍDA — VALIDAÇÃO PENDENTE.

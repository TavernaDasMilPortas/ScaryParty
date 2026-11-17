# Executar M8 — Entrega Final

## Instrução principal

Execute integralmente esta fase dentro do escopo abaixo, incluindo implementação/correções, integração, verificações disponíveis e documentação. Leia `Docs/Release/CONTRATO_EXECUCAO.md` e siga suas regras. Não se limite a propor um plano e não avance para Mseguinte automaticamente.

Caminhos relativos à raiz Unity. Roadmap: `Docs/Release/ROADMAP.md`. Relatório: `Docs/Release/Reports/M8_Relatorio.md`.

## Preparação

Leia `Reports/M7_Relatorio.md` e os relatórios das dependências indicadas no ROADMAP. Verifique pendências e aceite; não presuma sucesso.

Registre branch/SHA e alterações locais. Confirme o estado real dos serviços citados antes de editar. Este prompt é uma versão inicial preparada com o roadmap; adapte detalhes de implementação à branch atual sem mudar decisões de produto. Registre divergências e propostas no relatório.

## Orientação específica

Não gastar esta fase criando sistemas fundamentais que ficaram sem dono. Reconcile pendências e classifique bloqueadores antes do acabamento. Integre tutorial opcional e HUD ao jogo existente. Teste menus, UI versus tiro, reinício de RUN, vitória/derrota e duas instâncias. Build não está autorizada automaticamente pelo prompt: respeite política atual e forneça os passos para geração/teste no ambiente do usuário. Não chame uma build de validada sem jogar o ciclo fora do Editor. Registre eventuais cortes de escopo explicitamente.

## Tarefas e critérios do marco

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



## Entrega obrigatória

- Execute todas as tarefas independentes possíveis desta fase.
- Atualize os casos pertinentes em `Docs/Release/TEST_MATRIX.md`, preservando IDs e evidências.
- Registre novos defeitos e respectivas prioridades/marcos no relatório.
- Atualize `Docs/Release/STATUS.md`, CURRENT_STATE e CHANGELOG, sem declarar validação inexistente.
- Entregue instruções manuais reproduzíveis para os testes não executados.
- Pare no término desta fase. Se faltar validação jogável, use IMPLEMENTAÇÃO CONCLUÍDA — VALIDAÇÃO PENDENTE.

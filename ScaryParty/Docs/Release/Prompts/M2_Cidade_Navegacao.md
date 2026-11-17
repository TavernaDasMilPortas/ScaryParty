# Executar M2 — Cidade Navegacao

## Instrução principal

Execute integralmente esta fase dentro do escopo abaixo, incluindo implementação/correções, integração, verificações disponíveis e documentação. Leia `Docs/Release/CONTRATO_EXECUCAO.md` e siga suas regras. Não se limite a propor um plano e não avance para M3 automaticamente.

Caminhos relativos à raiz Unity. Roadmap: `Docs/Release/ROADMAP.md`. Relatório: `Docs/Release/Reports/M2_Relatorio.md`.

## Preparação

Leia `Reports/M1_Relatorio.md` e os relatórios das dependências indicadas no ROADMAP. Verifique pendências e aceite; não presuma sucesso.

Registre branch/SHA e alterações locais. Confirme o estado real dos serviços citados antes de editar. Este prompt é uma versão inicial preparada com o roadmap; adapte detalhes de implementação à branch atual sem mudar decisões de produto. Registre divergências e propostas no relatório.

## Orientação específica

Use o relatório M1 para atacar causas confirmadas. Não substitua o sistema de navegação inteiro sem evidência de necessidade. Distinga deslocamento de sentar/levantar validado de fallback atravessando obstáculos. Identifique o motivo de parada de cada carro antes de modificar velocidade ou remover sensores. A exceção futura do fantasma não pode liberar colisões para toda a população. Expansão da cidade não é pré-requisito; preservar mapa durante a RUN é a base.

## Tarefas e critérios do marco

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



## Entrega obrigatória

- Execute todas as tarefas independentes possíveis desta fase.
- Atualize os casos pertinentes em `Docs/Release/TEST_MATRIX.md`, preservando IDs e evidências.
- Registre novos defeitos e respectivas prioridades/marcos no relatório.
- Atualize `Docs/Release/STATUS.md`, CURRENT_STATE e CHANGELOG, sem declarar validação inexistente.
- Entregue instruções manuais reproduzíveis para os testes não executados.
- Pare no término desta fase. Se faltar validação jogável, use IMPLEMENTAÇÃO CONCLUÍDA — VALIDAÇÃO PENDENTE.

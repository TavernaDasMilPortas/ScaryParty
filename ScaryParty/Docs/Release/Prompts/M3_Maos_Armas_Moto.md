# Executar M3 — Maos Armas Moto

## Instrução principal

Execute integralmente esta fase dentro do escopo abaixo, incluindo implementação/correções, integração, verificações disponíveis e documentação. Leia `Docs/Release/CONTRATO_EXECUCAO.md` e siga suas regras. Não se limite a propor um plano e não avance para M4 automaticamente.

Caminhos relativos à raiz Unity. Roadmap: `Docs/Release/ROADMAP.md`. Relatório: `Docs/Release/Reports/M3_Relatorio.md`.

## Preparação

Leia `Reports/M2_Relatorio.md` e os relatórios das dependências indicadas no ROADMAP. Verifique pendências e aceite; não presuma sucesso.

Registre branch/SHA e alterações locais. Confirme o estado real dos serviços citados antes de editar. Este prompt é uma versão inicial preparada com o roadmap; adapte detalhes de implementação à branch atual sem mudar decisões de produto. Registre divergências e propostas no relatório.

## Orientação específica

Este marco inclui obrigatoriamente moto ocupando UMA mão, pistola funcional na outra, espingarda guardada em bandoleira sem ocupar mãos e exigência de DUAS mãos para sacar/usar. Alterar tanto validações do servidor quanto apresentação/input. Atropelamento válido causa um dano e uma queda, deduplicados por episódio, mesmo com vários colliders. Não criar dano obrigatório ao condutor sem configuração. Priorize essas regras, mira e semiautomático; rifle/granada são conteúdo posterior conforme roadmap, e qualquer pendência deve aparecer no relatório. Não remover a espingarda para simplificar o escopo. Integre o modelo real da moto por referências existentes, nunca por caminho presumido.

## Tarefas e critérios do marco

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



## Entrega obrigatória

- Execute todas as tarefas independentes possíveis desta fase.
- Atualize os casos pertinentes em `Docs/Release/TEST_MATRIX.md`, preservando IDs e evidências.
- Registre novos defeitos e respectivas prioridades/marcos no relatório.
- Atualize `Docs/Release/STATUS.md`, CURRENT_STATE e CHANGELOG, sem declarar validação inexistente.
- Entregue instruções manuais reproduzíveis para os testes não executados.
- Pare no término desta fase. Se faltar validação jogável, use IMPLEMENTAÇÃO CONCLUÍDA — VALIDAÇÃO PENDENTE.

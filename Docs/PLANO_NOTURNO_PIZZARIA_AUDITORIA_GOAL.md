# Pizzaria — auditoria técnica e plano integrado de implementação noturna

Data: 10/11/2026 UTC (noite de 09/11 no Brasil).
Repositório: https://github.com/TavernaDasMilPortas/ScaryParty
Base auditada: main, commit `945e6c60a0dabc9df7c14699e45903a92221de92`, “City modular and new characters”.

## 1. Como usar este documento

Coloque este arquivo em `Docs/PLANO_NOTURNO_PIZZARIA_AUDITORIA_GOAL.md` no checkout local e envie o prompt da seção 15 ao Antigravity em modo Goal. Este documento reúne diagnóstico, ordem de execução, critérios de aceitação e o plano anterior de pizza modular, preservado integralmente no apêndice.

Entrega atual: auditoria e plano; nenhuma correção foi aplicada ao repositório. A implementação deve acontecer no computador com Unity. A análise leu código, configurações, prefab YAML, objetos serializados da cena binária e animações FBX importadas no Blender. Não houve execução do jogo na Unity, medição de FPS, captura do visual final nem teste multiplayer nesta auditoria. Hipóteses visuais e desempenho precisam ser confirmados na execução.

O plano anterior foi baseado em `74196fdadc0554c8895abe1f668fcd3c27636748`. Para diagnóstico e prioridades, prevalece esta auditoria mais recente. O apêndice permanece como especificação funcional; caminhos e nomes devem ser reconciliados com o HEAD local antes de editar. Não implementar duas versões concorrentes da mesma solução.

## 2. Conclusão técnica

Os problemas não se resumem a animações ausentes ou à falta de efeitos. Há falhas de autoria e de integração entre domínio, apresentação, navegação e rede. Espalhar tweens agora agravaria a concorrência sobre os mesmos transforms.

Quatro prioridades:

1. Corrigir referências, identidades de estação e posições no prefab.
2. Definir um único responsável pela movimentação dos NPCs, com estados de sentar e levantar completos.
3. Remover o custo de carregar uma cidade descartável e coordenar geração, pizzaria, navegação e spawn.
4. Aplicar feedback visual por eventos, com orçamento e cancelamento, sobre uma base estável.

O caráter friendslop deve vir de reações exageradas, som, timing e pequenas surpresas visuais. O alvo de interação, a câmera em primeira pessoa e a lógica de entrega precisam continuar previsíveis.

## 3. Inventário verificado

- Unity `6000.3.6f1`; Netcode for GameObjects `2.13.1`; URP `17.5.0`; AI Navigation `2.0.14`; Input System `1.20.0`; Cinemachine `2.10.5`.
- Odin já integrado. O projeto declara `ODIN_INSPECTOR_EDITOR_ONLY`; novas ferramentas ficam no Editor, sem dependência de `OdinEditor` no player.
- DOTween/Pro e Feel estão presentes. A ponte atual de Feel usa uma define que não aparece nas defines Standalone auditadas.
- Cenas habilitadas no build: LobbyScene, ReadyScene, SampleScene, GameScene e PiariaUIDemoScene. Confirmar referências antes de retirar cenas de demonstração do build de produção.
- Prefab principal: `ScaryParty/Assets/Piaaria/Prefabs/Pizzeria/PizzeriaModular_Manual.prefab`.
- Cena principal: `ScaryParty/Assets/ThirdParty/StarterAssets/ThirdPersonController/Scenes/GameScene.unity`.
- O prefab tem 479 GameObjects, 329 MeshRenderers e 147 BoxColliders serializados. Isso não equivale automaticamente a draw calls ou colliders ativos.
- GameScene está em formato binário, com aproximadamente 30 MiB. Foram lidos 178.414 objetos serializados: 64.761 GameObjects, 12.155 MeshRenderers, 12.153 BoxColliders e 12.406 MonoBehaviours.
- O ramo ativo `__City__` contém 64.743 GameObjects. Há 11.629 componentes CityModuleInstance; grande parte da hierarquia repete os nós Visual, Collision, Anchors e MovingParts. Esses números são inventário serializado, não medição de custo em execução.

## 4. Achados e evidências

Caminhos de scripts abaixo são relativos a `ScaryParty/Assets/Piaaria/Scripts/`, salvo indicação. Prioridade P0 = integridade/risco de travamento; P1 = fluxo e experiência; P2 = refinamento medido.

| ID | Prioridade / certeza | Evidência | Correção necessária |
|---|---|---|---|
| A01 | P0, confirmado | Prefab: StorageStation id 6 em Geladeira_04, Dispensa_02 e Dispensa_01; id 10 em Stack e Staging. PizzeriaItemVisualizer.cs:155 usa atribuição em dicionário por stationId. | Identidade única e migração coerente dos bindings/configurações. Hoje a última estação registrada pode substituir outra no cache. |
| A02 | P0, confirmado | Prefab linha 23888: AssemblyStation id 13, mesa media.002, cinco entradas de slot, última referência nula. | Reparar o slot e validar capacidade física contra domínio/configuração. |
| A03 | P1, confirmado | Prefab linha 40222: TicketBoardStation id 4 anexado a Comodo_02_Cozinha, o cômodo inteiro. | Mover a responsabilidade ao quadro correto com migração de referências. |
| A04 | P1, confirmado | Tickets da cozinha aproximadamente em x 29,94–30,50 e z 7,437; objeto Quadro de Comandas em x 16,6 e z -4,77 no espaço do prefab. | Recriar layout no espaço local da superfície correta. Não ajustar à mão um deslocamento global que esconda o erro. |
| A05 | P1, confirmado | Segundo quadro cardapio.001: slots têm z aproximadamente -5,271, -6,718 e -6,050; não estão no mesmo plano. | Normalizar coplanaridade, orientação, margens e escala; verificar a face visível na Unity. |
| A06 | P1, confirmado | Builder ConfigureTicketBoardComponentsOnTarget calcula layout com bounds mundiais do objeto selecionado e só cria posições ausentes. | Exigir superfície explícita, impedir seleção de cômodo como quadro e permitir reparar slots existentes. |
| A07 | P1, confirmado | CustomerNpcView.cs:154 aceita remainingDistance pequeno sem validar hasPath/pathStatus. Há fallback de sentado quando navegação falha. | Chegada exige caminho completo e posição válida; falha não deve virar sucesso. |
| A08 | P1, confirmado no código; impacto visual a reproduzir | CustomerNpcView mistura NavMeshAgent com escrita direta no Transform ao sentar e reativa movimento antes de concluir levantar. | Movimento com proprietário único por estado e transições explícitas. |
| A09 | P1, confirmado | SetLocomotion recebe salto de velocidade para mínimo próximo de 2; blend tree usa Idle 0, Walk 2, Run 5,335. | Calibrar velocidade real, damping, histerese e cadência do clip. |
| A10 | P1, confirmado | CustomerService escreve WalkingToTable; não foi encontrada atribuição de SeatedWaitingFood na busca dos scripts. | Completar máquina de estados do domínio e confirmação autoritativa de chegada. |
| A11 | P1, confirmado | FBXs Walk, SitDown e StandUp têm curvas não constantes e esqueleto compatível na inspeção Blender. Controller tem SitDown/SitIdle/StandUp. | Não substituir animações por placeholders. Verificar bindings/Avatar/pivô no Unity e corrigir integração. |
| A12 | P0, caminho de falha identificado | PedestrianNpc.HandleNavigationFailed pode pedir nova viagem imediatamente; MoveTo pode falhar e invocar callback síncrono novamente. | Retentativa agendada, limitada, sem recursão; destino de recuperação e diagnóstico. |
| A13 | P1, confirmado | NpcNavigationMotor.FollowTarget define TargetTransform e chama MoveTo, que o limpa. | Separar seguir alvo de destino estático; testar alvo móvel. |
| A14 | P1, confirmado | CustomerSpawner refaz todos os clientes em eventos e a cada 0,25 s, com coleções e raycasts; cada cliente executa navegação própria. | Atualização por diferenças; autoridade clara; cache e orçamento. |
| A15 | P1, confirmado | DemandDirector chama TryRecruitPedestrianForPizzeria, mas descarta o pedestre retornado e cria cliente de domínio separadamente. | Associar identidade/visual do pedestre recrutado ao cliente; evitar duas pessoas representando a mesma chegada. |
| A16 | P1, confirmado | Reuso do pool de pedestres apenas reativa; retorno de CustomerNpcView adiciona PedestrianNpc sem a mesma assinatura de evento de reciclagem. | Reset completo, assinatura idempotente, limite populacional também no retorno. |
| A17 | P1, confirmado | CityGenerator.Awake/OnNetworkSpawn desativa e destrói __City__; GenerateLocally gera outra cidade. | Tirar cidade de preview descartável da cena de produção, preservando configuração/autoria. |
| A18 | P1, confirmado | GenerateLocally e BuildNavMesh síncronos; NavigationWorldManager coleta All e marca pronto; interior sem referência na cena. | Pipeline com dependências, coleta delimitada e validação de conectividade antes do spawn. |
| A19 | P0/P1, confirmado | Há LoadScene síncrono e fallback local após falha de carregamento via Netcode. | Coordenador único; falha de transição de rede não deve colocar apenas um peer noutra cena. |
| A20 | P1, confirmado | UIFeedbackService chama KillTweens, mas cria tweens diretos fora do registro de UIToolkitTweenService. | Registrar/cancelar por alvo e propriedade; partir do estado atual; limpar no detach. |
| A21 | P1, confirmado | BindButton registra MouseEnter e PointerEnter; callbacks anônimos e HashSet de botões não têm ciclo de desmontagem equivalente. | Um caminho de hover, assinatura removível, sem duplicação de som ou retenção de telas. |
| A22 | P1, confirmado | UIFeedbackService condiciona Feel a MOREMOUNTAINS_FEEL; define ausente nas defines Standalone lidas. | Ponte explícita para o pacote instalado; compilar editor e player. Não apenas espalhar uma define sem validar assemblies. |
| A23 | P1, confirmado | TicketBoardStation percorre pedidos e chama BindOrder a cada Update. | Atualizar tickets por revisão/evento e separar estado visual do estado lógico. |
| A24 | P1, confirmado | PizzeriaItemVisualizer ainda usa camadas fixas de ingredientes; tint via material pode apagar cores originais. | Executar compositor modular e migração do apêndice; preservar materiais e usar overrides compatíveis com shader. |
| A25 | P2, custo a medir | ReplicationProjector procura entradas em listas durante reconciliação; visualizers fazem scans globais e GetComponentsInChildren recorrentes. | Medir, indexar por IDs e atualizar somente dados alterados; não otimizar por suposição. |
| A26 | P1, confirmado no cálculo; reproduzir por objeto | Obstáculos usam tamanho de bounds mundiais em campo local; há criação por nome de balcões/mesas. | Bounds no espaço local correto, evitar obstáculos duplicados e carving desnecessário. |

O posicionamento perpendicular relatado pelo usuário é compatível com os problemas de layout encontrados, mas a orientação exata da face de cada mesh precisa de inspeção visual na Unity. Não aplicar rotação universal de 90° a todos os tickets.

## 5. Fase 0 — baseline, proteção e execução reproduzível

1. Ler AGENTS.md e instruções locais. Registrar branch, HEAD, alterações preexistentes e versão Unity. Não sobrescrever trabalho do usuário; criar branch de trabalho local ou checkout isolado se necessário.
2. Confrontar o HEAD local com esta auditoria. Itens já corrigidos devem ser verificados e marcados como tal, não refeitos.
3. Abrir uma única instância Unity. Em máquina com 8 GB, executar importação, testes e build sequencialmente. Não abrir vários editores ou builds simultâneos.
4. Registrar baseline antes das mudanças: tempo Lobby→Ready→Game; geração da cidade; navegação; objetos/triângulos/batches; CPU/GPU frame time; GC; memória; tráfego. Usar mesma seed, resolução, preset, quantidade de jogadores e NPCs antes/depois.
5. Registrar vídeo curto: NPC andando/sentando/levantando, tickets, ciclo completo da pizza. Se captura não for possível, declarar limitação, nunca inventar evidência.
6. Criar `Docs/Overnight/PROGRESS.md`, `FINDINGS.md`, `VALIDATION.md`, `PERFORMANCE.md` e `FINAL_REPORT.md`. Cada tarefa tem estado Pending/InProgress/Done/Blocked, evidência e próximo passo.
7. Commit local por fase coerente. Não publicar, fazer push, merge ou atualizar Unity/pacotes sem necessidade desta tarefa.

Aceite: baseline compilável ou falhas preexistentes registradas; lista de cenas e sistemas responsáveis; plano reconciliado com o checkout real.

## 6. Fase 1 — prefab, slots, identidade e ferramentas Odin

### 6.1 Identidade

Criar auditoria que compare stationId, bindings, catálogos, capacidade e referências de consumidores. Determinar quais aliases são intencionais antes de migrar. Não simplesmente renumerar campos no Inspector: atualizar todos os consumidores relacionados, inclusive receitas/configurações e referências de armazenamento/empacotamento/pilhas.

A instância de estação deve ter identidade estável; tipo de estação não substitui identidade. Se introduzir GUID de autoria e ID numérico de runtime, manter mapa determinístico e migração versionada. Arrays não podem usar a ordem da busca na cena como identidade.

### 6.2 Layout do quadro

- Definir BoardSurface com origem, eixos locais de largura/altura e normal da face. A frente do texto deve apontar para a normal escolhida.
- Layout usa coordenadas locais de uma área útil explícita; respeita escala do pai e margens.
- Slots devem ser filhos da superfície/raiz de slots do quadro, nunca do cômodo ou armário.
- Cada ticket tem fundo físico/visual, texto legível, collider de interação e identidade de pedido coerente. Auditar diferença entre criação pelo builder e fallback runtime.
- Gizmos: retângulo útil, normal, frente do texto, numeração, sobreposição e distância da superfície.
- Reparent com preservação consciente de pose; depois calcular nova pose local. Não corrigir instâncias deixando o prefab fonte errado.
- Detectar excesso de pedidos: fila/paginação ou expansão prevista. Evitar crescimento ilimitado e texto atualizado sem alteração.

### 6.3 Auditoria geral de anchors

Inspecionar slots de ingredientes, assembly, prep, armazenamento, forno, embalagem, pilhas, utensílios, telefone, mãos, mochila, balcão/fila, entrada/saída e cadeiras. Verificar nulos, repetidos, pai incorreto, capacidade divergente, distância absurda, escala negativa, colisão com bancada/parede, ponto dentro de forno e pivôs das portas. Exceções legítimas devem ser configuráveis, não apagadas automaticamente.

Separar Anchor de item, interação e animação quando têm funções diferentes. Validar com a pizzaria em origem e também deslocada/rotacionada; nunca depender de coordenadas mundiais fixas.

### 6.4 Odin

Ampliar o wizard existente com abas Auditoria, Estações/Slots, NPCs/Cadeiras, Loading e Feedback. Expor referências, offsets, limites e presets. Usar botões “Validar”, “Selecionar objeto”, “Enquadrar”, “Prévia da correção” e “Aplicar correções selecionadas”.

Toda edição deve respeitar Undo/Redo, dirty state, prefab overrides e idempotência. Não rodar correção destrutiva em OnValidate/InitializeOnLoad. Ferramentas de reparo devem explicar o que mudará e salvar apenas assets alterados. Não depender de Odin em runtime para carregar os dados.

Aceite: IDs sem colisões indevidas; slot nulo corrigido; ambos os quadros coplanares e legíveis; nenhuma estação errada recebe itens; salvar/reabrir mantém resultado; executar builder novamente não duplica componentes/slots.

## 7. Fase 2 — NPCs, navegação e animações

### 7.1 Movimento e máquina de estados

Fluxo esperado: entrando → fila → atendimento → indo à cadeira → alinhando → sentando → sentado aguardando → comendo → levantando → liberando cadeira → saindo → pedestre/reciclagem.

O domínio decide atendimento, pedido, reserva e consumo. A navegação informa chegada validada. A apresentação executa animações. Eventos devem ser correlacionados por identidade e versão da transição para que callbacks atrasados não concluam um estado novo.

- Caminhando: NavMeshAgent/motor controla deslocamento; animator recebe velocidade planar filtrada e turn rate. Não escrever simultaneamente Transform, Rigidbody e Agent.
- Aproximando: destino é ApproachAnchor acessível no NavMesh, não o centro físico da cadeira.
- Sentando: parar agente; alinhar ao SeatAnchor com proprietário explícito; executar SitDown; entrar em SitIdle. Desabilitar updatePosition/updateRotation quando necessário e restaurar de maneira coordenada, sincronizando nextPosition/posição navegável na saída.
- Levantando: executar StandUp antes de iniciar percurso; manter reserva da cadeira até liberação segura; ir ao ExitAnchor e só então retomar navegação.
- Não usar root motion e deslocamento por agente sem uma política explícita. Preservar a estratégia atual sem root motion se os clips funcionarem com alinhamento calibrado.
- Validar chegada com pathPending false, pathStatus completo, distância finita, alvo dentro da tolerância e ausência de falha. Uma trajetória parcial não é sucesso.
- Replanejar com intervalo/backoff, orçamento e máximo de tentativas. Warp somente em recuperação documentada, nunca a cada frame.
- Não usar Vector3.zero como marcador de ausência de destino.

### 7.2 Animator e clips

Usar os FBXs existentes. Verificar binding de todos os ossos do rig Generic, Avatar de origem, máscaras/layers, culling, clip ranges e offsets. As curvas existem; importação correta no Blender não prova binding final no Unity.

Calibrar velocidade do agente com passada do Walk; usar damping curto e histerese para entrar/sair de Idle. Não forçar velocidade de animação 2 instantaneamente para qualquer movimento mínimo. Evitar disparar triggers todo frame. No controller auditado as transições de sentar usam IsSitting; padronizar bool/trigger e remover ambiguidade somente após identificar consumidores.

Animar corpo/mesh em filho visual quando houver exagero cômico. Collider e raiz de navegação permanecem estáveis. Usar evento/StateMachineBehaviour ou observação de estado para concluir sentar/levantar, com fallback de timeout baseado no clip real — não uma espera arbitrária que ignore velocidade da animação.

### 7.3 Rede e população

Clientes relevantes à gameplay precisam de posição/estado autoritativos ou outra estratégia explicitamente comprovada, com interpolação nos peers remotos. Não presumir que a mesma seed torna NavMeshAgents independentes determinísticos.

Vincular pedestre recrutado ao CustomerId e preservar aparência. Evitar um pedestre indo à pizzaria enquanto CustomerSpawner cria outro visual na entrada. Na saída, devolver a mesma identidade quando aplicável, resetar motor/estado/eventos e respeitar limite populacional. Não criar crescimento infinito a cada cliente atendido.

Pedestres apenas decorativos podem usar simulação local simplificada, desde que não bloqueiem gameplay de modo divergente entre peers. Atualizar destinos e percepções em frequências escalonadas, não todos no mesmo frame. Pool com reset explícito e assinaturas simétricas.

### 7.4 Navegação do mundo

Um coordenador publica prontidão apenas depois de cidade, pizzaria, obstáculos e NavMesh concluídos e validados. Eliminar rebuilds concorrentes no CustomerSpawner. Delimitar coleta por volume/hierarquia/layer de navegação; excluir props e VFX sem função de caminho. Verificar conexão rua/calçada/porta/interior/mesa com caminhos de ida e volta.

Aceite: 20 ciclos de sentar/comer/levantar/sair, incluindo abandono e falha de caminho; 10 minutos de circulação sem aumento ilimitado de NPCs; cadeira ocupada por no máximo um cliente; nenhum sentado longe da cadeira; host+cliente e late join mostram estados coerentes. Quantidade de NPCs do teste deve ser registrada e incluir preset de estresse.

## 8. Fase 3 — pizza modular e prévia ao vivo

Implementar integralmente os requisitos do apêndice, com estes pontos de integração obrigatórios:

| Modo | Uso | Regra espacial |
|---|---|---|
| Sliced / Fatiado | Fatias individuais de cogumelo, tomate, pepperoni | Recalcular conjuntamente peças automáticas; rotação Y e escala limitada; sem sobreposição no plano útil. |
| Total / Cobertura | Molho e queijo | Camada sobre área total; não bloquear todas as fatias como um obstáculo sólido. |
| Central | Peça no centro | Reserva de área quando configurada como sólida. |
| Specific / Específico | Poses manuais | Preservar coordenadas/rotação/escala; outras peças respeitam área ocupada. |
| Ring / Anel | Distribuição periférica | Raio, margem e quantidade configuráveis. |
| Scatter / Espalhado | Grãos/ervas/pedaços pequenos | Densidade e política de sobreposição próprias. |
| None / Sem visual | Ingrediente sem representação adicional | Continua no domínio quando válido. |

Dados lógicos de ingredientes permanecem no servidor. O visual é reconstituído de composição, versão e seed estável. Resolver a limitação atual dos quatro IDs no DTO, incluindo estágio e quantidade com limites explícitos e validação. Não consumir ingredientes novamente durante rebuild visual.

Compositor compartilhado entre runtime e editor, com algoritmo determinístico, espaço da pizza, footprint configurável e tentativas limitadas. Quando não couber, política explícita e diagnóstico; não loop infinito nem redução silenciosa da quantidade lógica. Preservar poses manuais. Reutilizar instâncias quando possível; recriar visual não significa destruir o item de domínio.

O wizard ganha modo de posicionamento e parâmetros condicionais. A janela Odin independente tem abas Modelo original, Modelo processado, Comparação e Montagem. Pode ficar ao lado do wizard e atualiza inclusive antes de salvar, com Undo/Redo, sem Play Mode. Usar cena/câmera de preview isolada, limpeza de recursos e atualização somente quando houver alteração/arrasto.

Exemplo de aceite: cogumelo + tomate; trocar cogumelo para Specific; arrastar coordenadas e ver imediatamente a fatia se mover e o tomate se reorganizar; Undo restaura; runtime reproduz a mesma composição/seed. Testar pizza na mão, bancada, forno, caixa, pilha e mochila; cores próprias dos ingredientes permanecem.

## 9. Fase 4 — carregamento entre cenas e inicialização

Criar um coordenador persistente de transição com UI Toolkit e integração com a cena de rede. Cobrir Lobby→Ready, Ready→Game, retorno ao Lobby, desconexão e late join. Centralizar também os caminhos legados; não deixar botões antigos chamando LoadScene em paralelo.

Fluxo: mostrar overlay e permitir primeiro frame de render → fade → transição de rede/cena → geração → posicionamento da pizzaria → navegação → spawn/snapshot inicial → prontidão → fade de saída → liberar controle.

A barra não deve mostrar 100% antes da prontidão. Usar progresso real por etapa quando disponível e indicador indeterminado quando não houver estimativa honesta. Exibir etapa textual, dica curta, ícone de pizza/forno e animação leve. Em falha, mensagem compreensível com voltar/tentar novamente conforme estado da sessão; timeout configurável não encerra uma geração válida sem diagnóstico.

Substituir trabalho monolítico por etapas com orçamento de frame quando tecnicamente viável. Avaliar UpdateNavMeshDataAsync na versão instalada, respeitando ciclo de vida de fontes e dependências. Não mover acesso a GameObjects/Unity APIs para thread arbitrária. Somente desenho de loading não elimina travadas de geração síncrona.

Input lock por token durante loading/modal, câmera e cursor restaurados corretamente. Não usar Time.timeScale global para pausar uma partida multiplayer. Animação de UI pode usar tempo não escalado; timers do jogo continuam na fonte autoritativa apropriada.

Tirar cidade gerada de preview da cena de produção com ferramenta Editor e backup de autoria. Manter parâmetros, referências, hand placements e identidade da pizzaria. Preview de cidade deve viver em cena de preview dedicada ou raiz explicitamente descartável que não vá para o build. Testar a geração antes de apagar a cópia antiga. Não editar binário de cena por substituição textual.

Aceite: transição sem frame de gameplay incompleto exposto; câmera não nasce dentro do chão; NPCs só iniciam depois de navmesh pronta; erro de rede não provoca cena divergente; repetir entrar/sair cinco vezes sem duplicar serviços ou aumentar memória continuamente.

## 10. Fase 5 — Feel e DOTween em todo o jogo

### 10.1 Infraestrutura antes dos efeitos

Criar catálogo de feedback por evento e perfil ScriptableObject editável no Odin. Reutilizar UIToolkitTweenService e UIFeedbackService após corrigir registro, interrupção e limpeza. Um proprietário por propriedade animada. Interromper a partir do valor atual, restaurar pose base e liberar callbacks no detach/disable/despawn.

DOTween: trajetórias, fades, escalas e sequências controladas. Feel: combinação de som, partículas, squash/stretch e reações. Não acionar ambos sobre o mesmo transform/propriedade em paralelo. Portas já têm TransformDoorView: manter um controlador do deslocamento; feedback adicional em filhos ou eventos, não um segundo motor na porta.

Cada perfil tem intensidade, duração, cooldown, prioridade, limite de simultaneidade, distância de culling, canal de áudio, política de interrupção e opção reduced motion. Presets Discreto, Padrão e Festa. O padrão deve ser expressivo sem dificultar mira ou leitura. Configuração global de shake, flashes, volume de reações e movimento de UI.

Eventos de sucesso de gameplay disparam após confirmação autoritativa; hover/foco podem ser locais imediatos. Deduplicar eventos de rede/reconexão, para uma entrega não tocar confete duas vezes. Efeito cancelado não cancela operação lógica nem reexecuta consumo.

### 10.2 Matriz de aplicação

| Cena / situação | Feedback proposto | Limite e validação |
|---|---|---|
| Lobby | Entrada de painel, hover curto, confirmação de seleção, personagem reage ao mudar cor | Sem loop de tween por frame; cores preservadas. |
| Ready | Ícone de pronto com pop, reação do avatar, contagem com acento sonoro | Pronto confirmado pela rede; evitar áudio duplicado. |
| Loading | Pizza girando suavemente, pontos de etapa, fade | Reduced motion mantém indicador legível e estático; sem VFX pesado. |
| Spawn na partida | Fade curto e indicação de objetivo | Sem shake de câmera automático ao nascer. |
| Foco/interação | Outline estável, pequena resposta no prop visual, prompt aparece | Não deslocar collider/slot e perder alvo do raycast. |
| Pegar/soltar item | Ease curto à pose da mão, som material, pequeno squash ao apoiar | Cancelar em transferência rápida, restaurar escala, não mover raiz de rede indevidamente. |
| Abrir massa | Pulso visual de pressão e som de bancada | Coerente com progresso; nenhuma conclusão antecipada. |
| Cortar/ralar | Ritmo de utensílio, partículas pequenas pooled, som com variação limitada | Orçamento por bancada; desligar ao cancelar. |
| Adicionar topping | Pequena queda/acomodação das peças novas e reposição breve das existentes | Resultado final exatamente igual ao compositor; não jogar peças em física permanente. |
| Molho/queijo | Expansão visual curta da cobertura | Não alterar quantidade lógica ou ocultar a pizza. |
| Forno | Porta com easing atual, calor/vapor leves, aviso sonoro de pronto | Evitar luz dinâmica/partículas por frame em cada forno; fumaça por estado. |
| Pizza queimada | Puff e reação cômica discreta, som de falha | Informação clara, sem flash forte ou tela inteira coberta. |
| Embalagem | Tampa fecha, pequeno bounce da caixa, som de papelão | Um dono da tampa; estado final de caixa correto. |
| Pilha | Assentamento das caixas, som por contato | Não oscilar slots físicos nem permitir clipping cumulativo. |
| Mochila | Feedback ao vestir, guardar e retirar; contador pulsa | Não animar a câmera com cada pizza; poses corretas em terceiros. |
| Telefone | Ring visual/sonoro, resposta ao atender | Cooldown, mixer e interrupção quando chamada acaba. |
| Quadro | Ticket entra/desliza, destaca mudança, sai ao concluir | Texto legível, sem jitter; atualizar por evento. |
| NPC fila | Olhar/gesto de espera, impaciência graduada, reação ao receber pedido | Em filho visual; paciência lógica intacta. |
| Sentar/levantar | Animações reais com alinhamento suave | Sem squash no collider ou cadeira durante navegação. |
| Entrega certa | Reação do cliente, som satisfatório, confete curto opcional | Deduplicação de evento; cap de partículas. |
| Entrega errada | Reação curta engraçada e mensagem clara | Não esconder endereço/regra nem punir com espera visual longa. |
| Minimap | Abertura/fade, marcadores e seleção de rota | Sem recalcular geometria de rota a cada pulso; pan/zoom responsivos. |
| Pausa/opções/retorno | Transição curta, foco navegável, confirmação | Restaurar input/cursor; não parar servidor via timescale. |
| Ambiente/cidade | Porta reage, poucos elementos ambientais animados próximos | Culling por distância; nenhum MMF/tween eterno em milhares de módulos. |
| Sample/PiariaUIDemo | Mostruário de presets se mantidas como laboratório | Não entram no build de produção sem necessidade. |

Valores iniciais sugeridos, ajustáveis: hover 0,08–0,12 s; clique 0,06–0,10 s; pop de sucesso 0,18–0,30 s; troca de painel 0,15–0,25 s. São ponto de partida artístico, não medições. Exagerar reações de personagens/props é preferível a balançar a câmera em primeira pessoa.

## 11. Otimizações com orçamento e prova

### Carregamento e hierarquia

Eliminar carga/destruição da cidade serializada descartável. Reduzir hierarquia runtime de módulos: nós vazios de autoria só devem existir quando necessários; preservar anchors utilizados. Avaliar geração por quarteirão, combinação de geometria estática compatível e instancing após medir materiais, sombras e batching. Não combinar toda a cidade em uma mesh gigante que destrua culling ou interação.

### CPU e memória

- Trocar scans globais periódicos por registro/desregistro e caches invalidados corretamente.
- Atualizar NPCs/tickets/itens por mudança de estado, com fallback diagnóstico de baixa frequência.
- Reutilizar coleções em caminhos quentes; dimensionar pools pela carga real. Pool não deve reter recursos indefinidamente.
- Não instanciar materiais toda vez que tinge um item. Avaliar MaterialPropertyBlock versus compatibilidade do shader/SRP Batcher; preservar materiais dos toppings.
- Evitar rebuild da pizza quando apenas pose da mão mudou.
- Liberar RenderTextures, câmeras, clones e materiais de preview ao fechar janela; evitar repaint contínuo quando o preview está parado.

### GPU, física e áudio

- Medir sombras de luzes internas, distância de sombras, transparência de fumaça, pós-processamento e overdraw de UI.
- Definir presets gráficos, com opção acessível para máquina modesta. Não baixar qualidade global para encobrir bug de CPU.
- Simplificar colliders estáticos por unidade arquitetônica quando preserva navegação/colisão; desativar interações entre layers irrelevantes.
- NavMeshObstacle apenas onde necessário; carving dinâmico com critério. Prop decorativo pequeno não precisa cortar NavMesh.
- Partículas pooled com limite global e culling. Som com mixer por categoria, limite de vozes e cooldown.
- Animator culling/LOD para pedestres distantes; manter callbacks críticos independentes de animação que pode deixar de avaliar fora da tela.

### Rede

Medir bytes/s, mensagens e frequência de replicação; snapshots/deltas coerentes e limites de coleção. Registrar revisão por item para reconstrução visual e dirty updates. Evitar RPC por fatia/tween. Uma seed visual não substitui replicação de estado lógico. Testar jogadores entrando durante loading e durante uma pizza em processamento.

Metas: zero crescimento contínuo em ciclos repetidos; redução comprovada dos gargalos identificados; alvo inicial de 60 FPS no preset/hardware escolhido, com alternativa de 30 FPS estáveis em hardware limitado. Não declarar a meta atingida sem hardware e captura identificados. Relatar mediana/p95/p99 de frame time e pico de memória, não apenas FPS médio.

## 12. Qualidade de vida e humor com legibilidade

1. Prompt de interação explica ação disponível e motivo de bloqueio: mão cheia, ingrediente não preparado, forno ocupado, caixa fechada.
2. Mãos 1/2 claramente indicadas; feedback ao alternar e ao falhar uma transferência. Preservar atalhos existentes e não introduzir nova disputa por F/E.
3. Barra de processo interpolada com estado real do servidor; cancelamento limpa efeitos e libera câmera conforme regras existentes.
4. Quadro distingue pedido novo, em preparo, pronto e arquivado; não revelar automaticamente o endereço correto na escolha de embalagem se isso contrariar a mecânica atual.
5. Timers de forno e paciência legíveis, cor acompanhada de ícone/som; opção para reduzir notificações repetidas.
6. Recuperação de item visual preso: restaurar a apresentação a partir do estado autoritativo, sem duplicar item lógico.
7. Feedback de mochila cheia e pizza inválida; marcador claro de qual pizza foi selecionada para entrega.
8. NPCs têm reações curtas e variadas: expectativa, indignação leve, alegria; variação cosmética determinística quando compartilhada.
9. Opções de redução de movimento, shake, volume de UI e sensibilidade; navegação por teclado/controle quando já suportada.
10. Ferramenta Odin para cenário de teste: gerar pedido, montar pizza, simular forno, lotar fila, forçar caminho inválido e inspecionar estado. Exclusivamente editor/desenvolvimento, sem cheats em build final.

## 13. Ordem de execução, checkpoints e rollback

| Etapa | Entrega verificável | Dependência |
|---|---|---|
| F0 | Baseline, inventário, branch e logs | Nenhuma |
| F1 | IDs/bindings/slots/quadro reparados e auditor Odin | F0 |
| F2 | Motor NPC, cadeiras, estados, pooling e autoridade corrigidos | F1 |
| F3 | Compositor + rede de ingredientes + wizard + preview Odin | F1 |
| F4 | Loading coordenado, cidade de produção limpa, navmesh pronta antes de spawn | F2 e integração com F3 |
| F5 | Serviço de feedback corrigido e matriz de efeitos aplicada | F2–F4 |
| F6 | Perfil de desempenho, QoL e ajustes medidos | F5 |
| F7 | Regressão, build, evidências, relatório e desligamento condicional | Todas |

A ordem expressa dependências, não promessa de terminar numa quantidade fixa de horas. Não reduzir o escopo silenciosamente para caber na noite. Se houver bloqueio, preservar progresso, continuar itens independentes e deixar a pendência explícita. Não manter loops infinitos de recompilação: após falhas repetidas iguais, diagnosticar a causa e registrar limite/alternativa.

Cada fase deve compilar antes de acumular a próxima. Reverter apenas mudanças próprias comprovadamente defeituosas, sem reset destrutivo. Não reimportar todo o projeto nem apagar Library como primeira reação a erro de código. Não alterar fontes de pacotes comerciais quando um adaptador local resolve.

## 14. Matriz de validação e definição de pronto

| Teste | Evidência mínima |
|---|---|
| Prefab salvo/reaberto e builder executado duas vezes | Sem duplicação, IDs coerentes, nenhum slot nulo obrigatório, screenshots dos quadros |
| Pizzaria em posição/rotação diferentes | Slots, tickets, portas e cadeiras continuam corretos |
| NPC com caminho normal, bloqueado, parcial e destino zero | Sem falso sucesso, recursão ou Warp por frame; logs de recuperação |
| NPC com 20 ciclos completos e abandono | SitDown/SitIdle/StandUp visíveis; reserva liberada corretamente |
| Conversão pedestre→cliente→pedestre | Uma identidade/visual, contagem populacional limitada |
| Pizza cogumelo+tomate e 3+ toppings | Sem sobreposição indevida; quantidades preservadas; rebuild determinístico |
| Specific + edição live + Undo/Redo | Preview reage imediatamente e mantém configuração ao reabrir |
| Mais de quatro ingredientes, estágios e quantidades | Runtime host/cliente e late join representam mesma composição |
| Ciclo massa→preparo→montagem→forno→caixa→mochila→entrega | Nenhuma regressão funcional nem duplicação de itens |
| Loading e falha de rede | Nenhum peer entra sozinho em cena incorreta; cancelamento/retorno limpos |
| Hover rápido, abrir/fechar telas e spam de interações válidas | Sem tweens acumulados, callbacks duplicados ou escala residual |
| Reduced motion e preset baixo | Toda informação continua disponível; sem efeitos obrigatórios pesados |
| Cinco transições de ida/volta e soak de 10 minutos | Memória, listeners, materiais e objetos estabilizam |
| Host + cliente e late join | Estado, posições relevantes e feedback coerentes; falhas registradas |
| Build Standalone | Compila sem referências UnityEditor/OdinEditor, cenas corretas e smoke test |

Adicionar testes automatizados para comportamentos de risco: determinismo/ocupação do compositor, migração de IDs e DTO, falha de navegação sem recursão, transições de estado e ciclo de feedback. Validação artística exige inspeção visual: um teste unitário que apenas espelha o código não substitui o vídeo de sentar/levantar.

Definição de pronto: requisitos desta seção e do apêndice rastreados a arquivos/evidências; build e testes essenciais aprovados; não há P0/P1 conhecido aberto; todas as alterações salvas; relatório identifica o que foi medido e eventuais limitações. Se um teste essencial não pôde rodar, status é PARCIAL/BLOQUEADO, nunca SUCESSO.

## 15. Prompt para colar no Antigravity Goal

```text
/goal Implemente integralmente Docs/PLANO_NOTURNO_PIZZARIA_AUDITORIA_GOAL.md, incluindo seu apêndice de pizza modular, wizard e preview Odin. Trabalhe no projeto ScaryParty local. Leia as instruções do repositório e reconcilie o HEAD com o commit auditado 945e6c60a0dabc9df7c14699e45903a92221de92 antes de modificar arquivos.

Quero a implementação, não apenas outro plano. Execute F0–F7 na ordem de dependência. Corrija estações/slots/quadro, NPCs andando/sentando/levantando, navegação e população, pizza modular e rede completa de ingredientes, janela Odin com preview ao vivo, loading entre cenas, feedback Feel/DOTween em todos os fluxos aplicáveis e otimizações medidas. Preserve as regras atuais de gameplay e trabalho local preexistente.

Decida detalhes rotineiros autonomamente. Use uma única instância Unity e testes/builds sequenciais para controlar memória. Não atualize Unity/pacotes indiscriminadamente. Não publique/push/merge. Faça commits locais coerentes quando possível e mantenha Docs/Overnight/PROGRESS.md, FINDINGS.md, VALIDATION.md, PERFORMANCE.md e FINAL_REPORT.md atualizados. Quando o contexto acabar, retome desses arquivos, sem repetir migrações.

Não pare depois de escrever código: compile, execute testes adequados, valide visualmente os fluxos na Unity, teste host/cliente/late join e gere build Standalone. Não invente screenshots, números de desempenho ou testes aprovados. Registre bloqueios, continue tarefas independentes e não marque sucesso com requisito essencial não verificado.

Ao final, deixe relatório com arquivos alterados, evidências, comparação antes/depois, como abrir as ferramentas Odin e eventuais pendências. Salve assets/cenas, encerre testes/builds e finalize commits locais antes de qualquer desligamento.

Autorizo tentar desligar este PC Windows SOMENTE se a implementação estiver concluída e os critérios essenciais de validação tiverem passado, sem tarefa pendente nem gravação/build em andamento. Depois de gravar o relatório final e confirmar os arquivos salvos, execute shutdown.exe /s /t 0, sem /f. Não use timeout positivo de shutdown, pois implica fechamento forçado. Não feche à força aplicativos, não altere políticas de energia/permissões e não tente burlar bloqueios. Se houver falha, limitação de permissão, teste essencial não executado ou implementação parcial, não desligue: registre o estado e deixe o trabalho disponível para retomada.
```

A configuração do computador para continuar ligado com a tampa fechada já foi feita pelo usuário; não alterá-la. Goal não garante conclusão em uma noite nem remove limites de ferramentas, energia, memória ou permissões. O desligamento é pedido ao agente local, não executado por esta entrega.

## 16. Fontes e rastreabilidade

- Repositório auditado: https://github.com/TavernaDasMilPortas/ScaryParty/tree/945e6c60a0dabc9df7c14699e45903a92221de92
- Prefab principal: Assets/Piaaria/Prefabs/Pizzeria/PizzeriaModular_Manual.prefab, dentro de ScaryParty.
- Scripts principais: City/CityGenerator.cs, City/CityPopulationDirector.cs, City/PedestrianNpc.cs; Navigation; Pizzeria/Presentation/CustomerNpcView.cs, CustomerSpawner.cs, CharacterVisualController.cs, PizzeriaItemVisualizer.cs; Pizzeria/Composition/DemandDirector.cs; Pizzeria/Domain/Services/CustomerService.cs; Pizzeria/Stations/TicketBoardStation.cs e DiningTableStation.cs; UI/Piaria/Scripts/UIFeedbackService.cs e UIToolkitTweenService.cs (localizar caminho exato pelo nome no checkout).
- Controller: Assets/Piaaria/Animations/Characters/CharacterAnimator.controller.
- Configurações: ProjectSettings/EditorBuildSettings.asset, ProjectSettings/ProjectSettings.asset, ProjectSettings/ProjectVersion.txt e Packages/manifest.json.
- Goal: https://antigravity.google/docs/commands/goal/
- Windows shutdown: https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/shutdown — /s desliga; /f força; /t maior que zero implica /f. Por isso a instrução usa /t 0 e não solicita fechamento forçado.

---

# Apêndice — plano anterior completo de pizza modular, wizard e preview Odin

O conteúdo abaixo é preservado para não perder requisitos. Sua base de commit é histórica; conciliar com a auditoria acima. A implementação continua sendo uma só, usando os sistemas atuais do projeto.

# Plano de ação — pizza modular, wizard e prévia ao vivo com Odin

Data: 09/11/2026. Projeto: ScaryParty / pizzaria.
Escopo: plano de implementação; nenhuma alteração no repositório foi realizada nesta entrega.

## 1. Resultado esperado

Substituir o visual fixo de toppings por uma composição orientada a dados. Cada ingrediente define como aparece sobre a pizza. Ao adicionar um ingrediente, o sistema recalcula conjuntamente as peças automáticas, respeitando as posições manuais e o espaço disponível.

Ampliar o wizard existente e criar uma janela independente de visualização. O usuário pode deixar ambas abertas, comparar modelos antes/depois do processamento, montar uma pizza e observar imediatamente as alterações feitas no wizard. O gerador usado na prévia deve ser o mesmo usado no jogo.

Exemplo obrigatório: colocar cogumelo e tomate na pizza de prévia; mudar o cogumelo para Específico; editar suas coordenadas no wizard; observar o cogumelo se mover imediatamente e o tomate se redistribuir ao redor dele, sem apertar Play ou reconstruir a pizzaria.

## 2. Base técnica verificada

Referência inicial: main, commit 74196fdadc0554c8895abe1f668fcd3c27636748. Antes da implementação, conferir diferenças no HEAD e adaptar os pontos de integração. As constatações abaixo vêm de leitura do código, sem execução na Unity.

Todos os caminhos abaixo são relativos a ScaryParty/Assets/Piaaria/Scripts/Pizzeria/.

| Arquivo existente | Constatação e ação |
| --- | --- |
| Presentation/PizzeriaItemVisualizer.cs | UpdatePizzaIngredientLayers cria quatro grupos fixos a partir de IngredientMask. Substituir essa montagem pelo compositor genérico. Preservar anexação à mão, bancada, caixa e efeitos. |
| Editor/PizzeriaContentWizard.cs | Já deriva de OdinMenuEditorWindow. Ampliar seu menu e os editores de ingrediente; não criar um segundo wizard concorrente. |
| Authoring/IngredientDef.cs | Possui preparedIngredient e EffectivePreparedIngredientId. Aproveitar o vínculo para comparar cru e processado. |
| Presentation/ItemPresentationProfile.cs | Define visualPrefab e poses do ingrediente como item. Preservar como fonte de modelos de mão/bancada. |
| Domain/Models/ItemState.cs | Ingredients já contém ID, estágio e quantidade por ingrediente. Usar essa composição como origem lógica. |
| Network/NetDtos.cs | NetItemDto contém máscara e somente quatro IDs, sem estágio/quantidade por entrada. Corrigir essa limitação para a apresentação genérica. |
| Authoring/PizzaCompositionRules.cs | Há limites diferentes para ingredientes na pizza e toppings de pedidos. Alinhar capacidade de rede e validações; não confundir os dois limites. |
| Domain/Services/PizzaAssemblyService.cs | Complete registra o ingrediente e consome o item utilizado. Manter essa responsabilidade intacta. |
| Editor/IngredientVisualSetup.cs | Executa setup após carga do editor e salva prefabs/perfis. Tornar a migração não destrutiva para evitar sobrescrever modelos configurados manualmente. |

Tomate em fatias é um exemplo de novo topping: não existe uma camada específica para ele no método visual analisado. Não inventar IDs nem confundir esse ingrediente com molho de tomate.

## 3. Modelo de dados e responsabilidades

Criar PizzaIngredientVisualProfile como ScriptableObject de apresentação, referenciado pelo IngredientDef aplicável. Preferir associar a aparência na pizza ao ingrediente preparado, pois esse é o ID registrado pela montagem. Ingredientes que dispensam processamento podem referenciar diretamente seu perfil.

Separar explicitamente três representações:

1. Modelo cru como item: resolvido pelo catálogo/perfil existente.
2. Modelo processado como item: resolvido pelo preparedIngredient e catálogo existente.
3. Modelo aplicado na pizza: prefab de uma fatia, cobertura ou elemento decorativo, definido no novo perfil.

Um prato com cogumelos processados não pode ser instanciado automaticamente como uma fatia sobre a pizza. Evitar duplicar as referências dos modelos cru/processado no novo perfil: a janela deve editar a fonte original.

Campos comuns do novo perfil:

- Modo de posicionamento e prefab aplicado na pizza; variantes opcionais de uma mesma peça.
- Pose base: posição local, rotação e escala. Unidade local em metros, com raiz da pizza em escala unitária.
- Quantidade de peças por porção, quantidade visual mínima e máxima.
- Variação de escala uniforme e rotação em Y; permitir amplitude zero.
- Raio de ocupação em XZ, margem entre peças e folga da borda.
- Offset de apoio em Y, grupo de superfície e ordem de cobertura.
- Política de sobreposição explícita e configuração de ocupação.
- Aparência crua, assada e queimada: parâmetros/material por parte, sem alterar os materiais originais.
- Campos específicos do modo escolhido, exibidos condicionalmente no Odin.

Criar PizzaSurfaceProfile para definir origem, raio útil, altura da superfície, margem de borda e orçamento total de peças. Usar IngredientLayerAnchor como origem quando disponível. Não inferir o raio pela caixa, prato ou partículas; permitir medir a massa e corrigir o valor no editor.

## 4. Tipos de posicionamento

Usar enum com valores numéricos explícitos e estáveis. Exibir os nomes abaixo no wizard; não reordenar valores serializados após a migração.

| Modo | Comportamento | Controles próprios | Exemplo |
| --- | --- | --- | --- |
| Sliced / Fatiado | Distribui várias peças individuais pelo espaço livre da pizza. Recalcula junto aos demais toppings automáticos. | Peças por porção, escala, rotação Y, raio de ocupação, distância mínima. | Cogumelo, tomate, calabresa. |
| Total / Cobertura | Aplica uma cobertura contínua sobre a área útil. Não usa a malha inteira como bloqueio para fatias. | Escala da cobertura, altura, ordem, espessura/variante visual. | Molho, queijo fundido. |
| Central | Coloca uma peça no centro, acrescida da pose configurada. Reserva sua área para os automáticos. | Offset XYZ, rotação, escala e ocupação. | Folha ou decoração central. |
| Specific / Específico | Usa uma lista de posições explícitas, sem sorteio ou correção silenciosa das coordenadas. | Lista de poses XYZ, rotação e escala por peça; variante opcional. | Desenho autoral ou decoração fixa. |
| Ring / Anel | Distribui peças em uma circunferência. Participa da verificação de ocupação. | Raio do anel, quantidade, ângulo inicial, orientação e pequena tolerância angular. | Azeitonas em volta da pizza. |
| Scatter / Disperso | Distribui muitas peças pequenas com densidade e orçamento próprios. | Densidade, limite de peças, escala e espaçamento reduzido. | Ervas ou queijo ralado. |
| None / Sem visual | Mantém a presença lógica sem gerar geometria. | Justificativa/aviso de configuração. | Ingrediente sem representação geométrica deliberadamente. |

Sliced e Scatter podem compartilhar o mesmo algoritmo com parâmetros diferentes. Evitar sistemas paralelos que produzam resultados incompatíveis.

Quantidade em Total e Central: por padrão, porções adicionais não duplicam meshes exatamente sobrepostas. Manter uma representação única; permitir variante/intensidade quando houver arte preparada para isso. Em Específico, a lista define a representação completa e não é duplicada automaticamente por porção. O wizard deve explicar essas regras.

## 5. Ocupação, prioridade e distribuição conjunta

Processar primeiro coberturas e calcular a altura de apoio. Depois reservar Central e Específico; em seguida posicionar Anel e distribuir Sliced/Scatter no espaço restante. Separar ordem de renderização/altura de prioridade de ocupação.

No modo Específico, as coordenadas são soberanas. Se duas peças manuais se atravessarem ou saírem da massa, mostrar aviso e gizmo vermelho. Não mover uma delas automaticamente. Para uma sobreposição artística intencional, exigir que a política de sobreposição esteja explicitamente habilitada no perfil.

Algoritmo proposto para peças automáticas:

1. Receber composição ordenada por ID e estágio, quantidades, superfície, perfis e semente.
2. Expandir porções em peças visuais dentro dos limites de cada perfil e do orçamento global.
3. Determinar variante, escala e raio efetivo de cada peça antes de testar sua posição.
4. Reservar ocupações fixas e tentar colocar peças grandes antes das pequenas; desempatar de forma determinística e alternar ingredientes semelhantes.
5. Sortear candidatos uniformemente na área circular, descontando o raio da própria peça e a borda. Evitar concentração involuntária no centro.
6. Aceitar somente se distância entre centros for pelo menos a soma dos raios efetivos mais a folga.
7. Para falhas, tentar um número limitado de candidatos e reinícios determinísticos do conjunto automático.
8. Se ainda não couber, reduzir apenas quantidades visuais até os mínimos configurados e tentar novamente. Não alterar porções lógicas, preço ou receita.
9. Se nem os mínimos couberem, retornar diagnóstico de configuração inválida e resultado parcial identificado; jamais forçar interpenetração ou executar loop infinito. Bloquear a aprovação editorial dessa configuração antes da entrega.

O círculo de ocupação deve envolver a geometria em todas as rotações permitidas, considerando escala. Um raio subdimensionado invalida a promessa de evitar sobreposição. Elipses/polígonos são melhoria futura, não requisito inicial.

No modo Anel, conflitos podem ser resolvidos apenas dentro das tolerâncias definidas. Sem solução, informar o conflito; não converter silenciosamente em Sliced.

## 6. Gerador compartilhado entre jogo e editor

Separar cálculo, instanciação e integração:

| Componente proposto | Responsabilidade |
| --- | --- |
| PizzaVisualComposition | Snapshot completo de ID, estágio, quantidade, massa e semente. |
| PizzaLayoutGenerator | Calcula poses, variantes e diagnósticos. Sem referências a UnityEditor, câmera, rede ou estado global aleatório. |
| PizzaLayoutResult | Lista de peças com identidade estável, pose, perfil e ocupação; lista de avisos/falhas. |
| PizzaVisualComposer | Cria/atualiza os objetos visuais a partir do resultado e aplica aparência de cozimento. |
| PizzaVisualCatalog | Resolve perfis por ingrediente/estágio, validando referências ausentes e duplicadas. Pode adaptar o catálogo existente em vez de duplicar sua função. |
| PizzaVisualPreviewWindow | Usa o mesmo gerador e compositor em ambiente isolado do editor. |

Manter base, coberturas e toppings em raízes separadas. Ao mudar a composição, recalcular todo o conjunto automático. Reutilizar instâncias quando simples; recriar os filhos de toppings é aceitável na primeira versão, com destruição/limpeza correta. Nunca destruir a pizza lógica para atualizar o visual.

Gerar fingerprint apenas da composição, superfície, semente e conteúdo visual relevante. Não usar Revision geral como gatilho suficiente: ela também muda em operações e transferências. Cozimento atualiza materiais; transferência atualiza o pai; nenhum dos dois deve sortear posições novamente.

Não aplicar indiscriminadamente a cor da massa a todos os Renderer. Cada grupo preserva suas cores e recebe seu tratamento de cozimento. Reutilizar materiais e evitar vazamentos de materiais instanciados. As peças decorativas não precisam de Rigidbody, Collider ou NetworkObject.

## 7. Rede e composição completa — etapa obrigatória

O domínio possui entradas com quantidade e estágio, mas NetItemDto não transmite essa estrutura completa. A máscara legada não distingue ingredientes novos nem porções repetidas. Resolver isso antes de considerar o compositor pronto para multiplayer.

Adicionar um snapshot de ingredientes compatível com a versão instalada de Netcode, com capacidade explícita suficiente para maxDistinctIngredientsOnPizza. Cada entrada deve carregar ID, estágio e quantidade. Atualizar serialização, Equals, montagem do DTO e todos os consumidores afetados. Validar a configuração para nunca permitir mais entradas do que a capacidade transmitida.

Manter IngredientMask somente para compatibilidade temporária onde necessário; a nova apresentação não depende dela. Auditar rótulos e resumos que ainda usem máscara para não apresentarem uma pizza diferente da composição real.

Derivar uma semente estável do ItemId, preservado durante o ciclo de vida, ou armazenar VisualSeed no servidor se essa estabilidade não puder ser garantida. Não usar GetInstanceID, horário, hash de string dependente da execução ou UnityEngine.Random global. Ordenar entradas explicitamente e usar gerador pseudoaleatório de algoritmo fixo.

Todos os clientes usam composição e perfis equivalentes. Não sincronizar cada Transform. Validar host, cliente e entrada tardia; não prometer determinismo entre plataformas sem teste. Se diferenças numéricas alterarem escolhas, quantizar o cálculo de ocupação ou transmitir o layout final.

## 8. Integração ao wizard Odin

Ampliar PizzeriaContentWizard mantendo ingredientes, utensílios, processos, receitas e manutenção existentes.

Adicionar as entradas de menu: Ingredientes e visuais; Montagem de pizza; Superfícies de pizza; Validação visual. A entrada Montagem de pizza mostra resumo e botão Abrir janela de visualização. A janela deve continuar independente e aberta enquanto outro ingrediente é selecionado no wizard.

No editor de ingrediente, apresentar grupos:

- Identificação e processamento: dados existentes e destino do preparo.
- Modelos antes/depois: referências reais do catálogo, com botão para localizar/editar o perfil correspondente.
- Visual na pizza: perfil, prefab unitário/cobertura e seletor de posicionamento.
- Distribuição: somente campos pertinentes ao modo atual.
- Ocupação e limites: raio, margens, orçamento, avisos de conflito.
- Cozimento: aparência por estado.
- Ações: visualizar ingrediente, adicionar à pizza de prévia e localizar asset.

Usar recursos Odin já disponíveis no projeto: grupos/abas, edição inline, listas editáveis, campos condicionais, validação e botões. Encapsular editores em classes do assembly Editor. Os assets runtime devem permanecer serializáveis no build sem depender de Sirenix.OdinInspector.Editor.

Editar os assets verdadeiros com Undo/Redo e marcação de alterações. A prévia deve responder às alterações em memória antes do salvamento em disco. Salvar consolida os assets; não é requisito para atualizar a imagem.

Não recriar a árvore inteira do wizard a cada movimento de um slider: preservar seleção, foco e rolagem.

## 9. Janela independente de visualização

Criar PizzaVisualPreviewWindow com Odin, aberta por menu Tools/Scary Party/Pizzeria/Visualização de pizza e pelo wizard. Usar uma janela única reutilizável, com controles 3D de órbita, zoom, enquadrar, vista superior e lateral.

### Aba Ingrediente

- Seleção do ingrediente e resolução explícita do vínculo de processamento.
- Visores lado a lado: Antes do processamento e Depois do processamento.
- Terceira visualização opcional: Peça aplicada na pizza, deixando clara a diferença para o item processado completo.
- Escala física consistente entre os visores, régua/grelha opcional e indicação de dimensões. Enquadramento individual disponível sem ocultar essa diferença de escala.
- Aviso legível para prefab ausente, vínculo inválido/cíclico, ou ingrediente sem processamento. Nunca mostrar uma primitiva como se fosse o modelo final.
- Botão Adicionar versão processada à pizza; para ingredientes sem preparo, adicionar sua versão aplicável.

### Aba Montagem de pizza

- Seletor de massa/superfície e composição temporária com ingrediente, estágio e porções.
- Adicionar, remover, limpar e carregar uma receita existente como cópia de teste.
- Simulação dos estados crua, assada e queimada, sem executar operações de gameplay.
- Semente visível e fixa; botão Nova distribuição altera somente a semente de teste.
- Mostrar contagem de peças por ingrediente, raios de ocupação, área útil, e diagnósticos.
- Destacar o ingrediente selecionado; opções de isolar e ocultar apenas para inspeção.
- Ocupar o espaço com a composição completa mesmo quando um ingrediente estiver oculto para inspeção. Remover da receita de teste é uma ação distinta.
- Mostrar a pizza com materiais e shader reais, iluminação ajustável e fundo neutro.

Carregar uma receita não modifica o asset original. Alterar porções na montagem muda apenas o teste. Editar um perfil visual pelo wizard modifica o asset real e afeta todas as prévias que o utilizam. Essa distinção deve aparecer na interface.

### Edição ao vivo e coordenadas específicas

O MVP obrigatório permite editar XYZ, rotação e escala no wizard e ver a atualização imediata na janela separada. A lista de poses deve ter rótulos estáveis para identificar cada peça.

Adicionar seleção e manipuladores de posição/rotação/escala no visor como segunda etapa de usabilidade. Esses manipuladores escrevem na mesma pose do perfil e atualizam o wizard. Se a integração de handles no visor exigir mais trabalho, usar inicialmente os campos numéricos; isso não pode atrasar a atualização ao vivo obrigatória.

## 10. Atualização ao vivo e isolamento do editor

Implementar um serviço editorial de notificação de mudança por asset/ID. Após edição Odin, invalidar as prévias dependentes; agrupar alterações do mesmo ciclo do editor e repintar. Durante arraste contínuo, limitar a frequência de reconstrução sem exigir que o usuário solte o controle para enxergar o resultado.

Observar também Undo/Redo, alterações pelo Inspector, reimportação de prefab/material e mudança do vínculo preparado. Invalidar somente composições afetadas. Evitar trabalho pesado dentro de OnValidate; agendar a reconstrução para o ciclo seguro do editor.

Usar infraestrutura de preview com cena/câmera isoladas e suporte ao pipeline gráfico instalado. Validar cedo o shader toon real na prévia; não trocar silenciosamente por material básico. As instâncias devem ser temporárias e não aparecer na cena de gameplay nem marcar cenas como modificadas.

Desabilitar componentes de gameplay nos modelos de prévia. Liberar objetos, materiais temporários, câmeras, render textures, inscrições em eventos e árvores de propriedades ao fechar, recompilar scripts e alternar Play Mode. Restaurar composição e seleção editorial quando apropriado, sem duplicar objetos temporários.

## 11. Migração segura

1. Inventariar modelos e perfis atuais; distinguir arte manual de placeholders gerados.
2. Corrigir IngredientVisualSetup para não sobrescrever assets customizados após recarga do editor. Inicialização cria somente o que falta; regeneração destrutiva fica em ação explícita e claramente identificada.
3. Criar perfis Total para molho/queijo e Sliced para calabresa/cogumelo, preservando IDs e vínculos.
4. Reaproveitar modelos existentes quando adequados; usar placeholders identificados se a fatia individual ainda não estiver pronta.
5. Criar tomate fatiado somente com ID validado, modelos e processamento configurados. Não adicioná-lo automaticamente aos pedidos apenas por existir como opção visual.
6. Atualizar catálogo canônico e cópia de Resources, caso ambos continuem em uso, evitando configurações divergentes.
7. Integrar o compositor e remover a criação fixa de camadas para impedir desenhos duplicados.
8. Validar reexecução da migração: não duplica perfis, não muda IDs e não perde ajustes do usuário.

Orientação para modelagem: uma peça por prefab, origem centralizada em XZ e apoio em Y=0 no prefab final da Unity; tamanho real em metros; transforms aplicados e raiz unitária. Validar conversão de eixos Blender/Unity. Modelos de cobertura devem conter somente a cobertura, sem massa duplicada. Permitir múltiplos materiais e preservá-los.

## 12. Ordem de execução e entregáveis

| Fase | Entrega | Condição para avançar |
| --- | --- | --- |
| 1 — Auditoria | Mapa dos perfis, builders, composição e capacidade de rede. | Pontos de sobrescrita e limites identificados. |
| 2 — Dados | Perfis visuais, superfície e modos com validação Odin. | Configurações serializam e sobrevivem à recarga. |
| 3 — Composição/rede | Snapshot completo de ingredientes e resolução de catálogo. | ID, estágio e porções chegam corretamente ao cliente. |
| 4 — Distribuidor | Gerador determinístico com ocupação e diagnósticos. | Combinações válidas respeitam bordas e não se atravessam. |
| 5 — Runtime | Compositor integrado ao visual existente. | Mudança de composição redistribui; transporte/cozimento preservam layout. |
| 6 — Wizard | Edição de modelos, perfil e posicionamento no wizard existente. | Editar e desfazer preserva o asset e os valores. |
| 7 — Prévia | Janela independente com Ingrediente e Montagem de pizza. | Funciona sem Play e usa o mesmo compositor do jogo. |
| 8 — Sincronização | Atualização ao vivo, Inspector, Undo/Redo e reimportação. | O exemplo cogumelo Específico + tomate funciona com ambas as janelas abertas. |
| 9 — Migração/QA | Assets migrados, regressões e build verificados. | Reabrir Unity não apaga modelos ou configurações. |

Prioridade funcional: Sliced, Total, Central, Específico e atualização ao vivo primeiro. Anel e Disperso completam os modos previstos antes da entrega final. Manipuladores 3D e pooling elaborado podem vir depois; não são pré-requisitos para a edição numérica ao vivo.

## 13. Validação e critérios de aceite

Testes automatizados relevantes para o gerador e rede:

- Mesma composição, semente e configuração produzem poses iguais; ordem de adição não altera o resultado final equivalente.
- Cada peça automática respeita a borda e os raios ocupados, incluindo escala máxima.
- Central e Específico reservam espaço; posições específicas não são corrigidas silenciosamente.
- Composição superlotada termina em tempo limitado e retorna diagnóstico; porções lógicas permanecem intactas.
- Serialização preserva todas as entradas, estágios e quantidades até o limite suportado, incluindo porções repetidas e IDs não legados.
- Mudar somente localização ou progresso de forno não altera poses.

Roteiro manual de aceite:

1. Abrir wizard e janela de prévia lado a lado, fora do Play Mode.
2. Selecionar cogumelo e conferir os modelos cru, processado e fatia da pizza.
3. Montar massa, molho, queijo, cogumelo e tomate; verificar distribuição conjunta.
4. Alterar cogumelo para Específico e editar X/Z/Y, rotação e escala: o resultado aparece imediatamente.
5. Confirmar que tomate evita a nova ocupação e que posições específicas persistem após nova distribuição.
6. Criar conflito entre duas poses manuais: aviso visível, sem deslocamento silencioso.
7. Desfazer/refazer e conferir wizard e prévia sincronizados.
8. Trocar prefab/material pelo Inspector e reimportar: prévia atualiza sem recriar a composição de teste.
9. Aumentar porções e exceder ocupação: visualizar diagnóstico e limites, sem travamento.
10. Cozinhar visualmente: massa, molho e toppings conservam identidades e materiais próprios.
11. No jogo, adicionar topping, pegar pizza, trocar de mão/bancada, assar e embalar sem perda de ingredientes.
12. Conferir host, cliente e jogador que entra depois com a mesma pizza.
13. Fechar/reabrir janela, recompilar e reabrir Unity: sem objetos órfãos nem sobrescrita dos modelos.
14. Gerar build: nenhuma dependência UnityEditor/Odin Editor incluída no runtime.

Concluir somente após comparar a mesma composição e semente na prévia e no jogo. Registrar limitações visuais reais e assets ainda provisórios.

## 14. Referências do projeto

- [Visualizador atual](https://github.com/TavernaDasMilPortas/ScaryParty/blob/74196fdadc0554c8895abe1f668fcd3c27636748/ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Presentation/PizzeriaItemVisualizer.cs)
- [Wizard Odin existente](https://github.com/TavernaDasMilPortas/ScaryParty/blob/74196fdadc0554c8895abe1f668fcd3c27636748/ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Editor/PizzeriaContentWizard.cs)
- [Composição de domínio](https://github.com/TavernaDasMilPortas/ScaryParty/blob/74196fdadc0554c8895abe1f668fcd3c27636748/ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Domain/Models/ItemState.cs)
- [DTOs de rede](https://github.com/TavernaDasMilPortas/ScaryParty/blob/74196fdadc0554c8895abe1f668fcd3c27636748/ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Network/NetDtos.cs)
- [Setup de modelos](https://github.com/TavernaDasMilPortas/ScaryParty/blob/74196fdadc0554c8895abe1f668fcd3c27636748/ScaryParty/Assets/Piaaria/Scripts/Pizzeria/Editor/IngredientVisualSetup.cs)

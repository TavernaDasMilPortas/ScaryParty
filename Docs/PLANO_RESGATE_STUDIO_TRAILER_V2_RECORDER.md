# Plano de resgate v2 — Studio de Trailer e exportação automática

**Objetivo:** obter takes de trailer convincentes, graváveis e reproduzíveis com o mínimo de alterações no gameplay. Base: inspeção visual dos 16 MP4 enviados em 27/11/2026; este documento **não** é auditoria de código do repositório.

**Entrega obrigatória:** 16 arquivos MP4 finais efetivamente gravados, validados e localizados em `TrailerOutput/Final/`. Implementar a ferramenta sem executar a gravação não constitui conclusão. Unity Recorder já instalado, segundo o responsável pelo projeto; confirmar versão local. Este plano substitui o anterior.

## Diagnóstico visual

1. O Studio tem movimentações de câmera e mudanças de estado de props, mas não encena gestos corporais suficientemente convincentes.
2. Há obstruções de câmera/clip de mesh muito severas em T02B, T05B, T06A, T08A, T09B e T10A; T07A e o final de T11A também sofrem.
3. Ações como abrir massa, adicionar ingredientes, montagem e embalagem são principalmente alterações de objetos; precisam da ilusão visual de manipulação.
4. O sistema parece operar a cena como simulação com câmeras programadas, mas trailer exige direção de cena: poses, props, câmera e instantes-chave controlados.

## Decisão arquitetural

Manter cenário, prefabs visuais, personagens, materiais e shaders do projeto, mas criar uma **camada de captura cinematográfica** separada de gameplay. Evitar tentar corrigir NavMesh, física de todos os utensílios, IA de NPCs ou lógica completa de pedidos só para gravar o trailer.

- `CinematicActor`: avatar visual clonado do prefab existente, sem NavMeshAgent, movimento por rede, CharacterController, interações de gameplay nem Rigidbodies dinâmicos. Preservar renderer, Animator, rig, materiais, shaders, acessórios. Não destruir dados do prefab original. Preferir desativar componentes indesejados somente na instância de captura. Desabilitar root motion quando a trajetória é controlada pela Timeline; movimentar o root pela sequência para evitar disputa.
- `CinematicActionController`: ações nomeadas (`Idle`, `Walk`, `Run`, `Knead`, `Reach`, `Chop`, `Assemble`, `InsertOven`, `Pack`, `EquipBackpack`, `React`, `Celebrate`) por Playables/Animator com crossfade. Primeiro reutilizar clips de animação existentes. Se faltarem, gerar **fallbacks cinematográficos** usando Animation Rigging/IK ou poses por Transform/bones com curvas e arcos suaves. Nunca apresentar uma pose T estática como movimento válido.
- `CinematicPropDirector`: controlar objetos via estados explícitos e sockets (`RightHand`, `LeftHand`, `Table`, `Oven`, `Box`, `Backpack`). Transições por curvas: pegar -> carregar -> soltar. Controlar visibilidade em instantes predefinidos somente durante oclusão, gestos ou corte; não usar teleportes evidentes no meio do quadro.
- `CinematicShotDirector`: shot por Timeline/PlayableDirector (ou sequenciador determinístico já existente). Câmeras com posição, alvo, dolly e easing próprios. Nunca parentear câmera diretamente ao crânio/torso de avatar sem offset seguro. Cada take deve poder ser reproduzido e capturado isoladamente.
- `CinematicPreflightValidator`: antes de renderizar, amostrar câmera e atores em diversos tempos, verificar interseção de câmera com colliders/renders, obstrução no segmento câmera->alvo (SphereCast + critérios de bounds), objetos fora do quadro, actor/prop/animation bindings nulos, câmera em parede ou dentro do corpo, distância e escala mínima do assunto. Além das verificações geométricas, gerar screenshots de início/meio/fim do take: pré-validação numérica sozinha não identifica composição ruim.

**Nota:** não zerar todas as colisões indiscriminadamente. Desabilitar colisão dinâmica dos *doubles*, manter colliders do cenário para validação e usar poses, offsets/sockets e correção visual dos contatos. `Physics.IgnoreLayerCollision` pode isolar a camada de captura, mas não resolve câmera atravessando paredes.

## Ordem de execução para evitar retrabalho

### P0 — Bloqueadores visuais (primeiro)

1. Congelar alterações de gameplay; criar cena/prefab/namespace exclusivos do Studio e recuperar a capacidade de reproduzir os 16 takes atuais.
2. Inspecionar a configuração de câmeras, rigs, alvos, escalas, Transform parent, FOV, near clipping, eventos e relação de tempo. Corrigir T02B, T05B, T06A, T08A, T09B, T10A e obstruções T07A/T11A.
3. Criar validação de enquadramento e thumbnails automáticas para todos os shots. O take não pode ser aprovado apenas porque o MP4 foi gerado.
4. Substituir atores movidos em pose rígida por dublês visuais animáveis, preservando os shaders/estética do jogo.
5. Garantir que cada take tenha início e fim com posições válidas, tempo determinístico e reset completo de câmera, props, estados e bindings.

### P1 — Ações convincentes sem física real

1. Checar importações de rigs humanoid/generic, Avatar, Animator Controller, AnimationClip e compatibilidade; priorizar reutilização de animações existentes. Não presumir que todo rig é Humanoid.
2. Ligar ações a gestos: mão chega ao objeto antes de o objeto se mover; ferramenta presa ao socket; torso/ombros reagem; objeto aterrissa na superfície.
3. Adicionar microanimações de idle, cabeça, braços e tronco (respiração, transferência de peso, olhar para colega/objetivo) para preencher a ausência de vida entre gestos.
4. Usar animações procedurais com curvas suaves apenas como fallback quando não houver clip apropriado: movimento de braços alternados, squash/stretch controlado, balanço dos ombros, passos simples. Respeitar rig e evitar alterar bones arbitrariamente sem validação.
5. Cada ação segue `anticipação -> contato -> transformação/efeito -> recuperação`; usar efeitos, vapor, partículas e cortes de câmera para esconder mudanças discretas.

### P2 — Direção do trailer e exportação

1. Reencenar takes fracos sem insistir no ângulo original; preferir enquadramentos próximos de mãos, utensílios, pizza, forno e expressão para reduzir necessidade de animação completa.
2. Usar cortes curtos entre ações-chave; evitar 7–11 segundos de imobilidade; incluir movimentos de câmera com parallax e foco claro.
3. Exportar primeiro os três pilotos em baixa resolução, corrigir e validar; somente depois gerar previews completos dos 16 takes. Inspecionar movimento e composição antes da gravação final.
4. Capturar em resolução/framerate consistentes, sem game UI indesejada, com preset visual/shader atual, luzes e sombras configuradas.
5. Se o Studio exporta em batchmode, verificar de fato o rendering backend do ambiente. `-nographics` pode impedir a captura normal de frames em workflows dependentes da GPU.

## Redireção take a take

| Take | Nova ação / enquadramento recomendado | Critério de aceite |
|---|---|---|
| T01 City Reveal A | Começar acima da rua, revelar esquina, descer/avançar em direção à porta da pizzaria. Caminhante animado no primeiro plano. | Entrada reconhecível, parallax e nenhum corte de parede/fachada. |
| T02 Kitchen Team A | Plano aberto com três funcionários realmente executando ações distintas (preparo, forno, caixa). Movimentos em tempos desencontrados. | Há atividade corporal legível em todos os atores; nenhum clipping. |
| T02 Kitchen Team B | Trocar câmera que atravessa o personagem por travelling lateral em altura segura, com câmera e alvo calculados. | Durante 100% do take, cozinha visível e foreground não tapa a ação. |
| T03 Dough A | Plano 3/4 das mãos; antecipação -> pressionar/rolar -> massa expandir -> soltar. | Crescimento da massa sincronizado com gesto, sem instrumento flutuante. |
| T03 Dough B | Top-down controlado, sem cabeça/avental na frente; massa muda ao passar rolo. | Mesa ocupa centro e a ferramenta fica próxima da massa. |
| T04 Ingredients A | Close de faca e ingrediente com mão/antebraço visíveis; fatias surgem no contato, depois mão transfere. | Sem teleporte explícito de ingrediente/faca. |
| T05 Assembly A | Top-down da pizza; toppings entram em etapas com mão ou utensílio atravessando quadro. | Transformação inteligível e coerente. |
| T05 Assembly B | Substituir por plano lateral 3/4 com superfície, mãos e pizza; nunca colocar camera no corpo do avatar. | Nenhuma obstrução por roupa/cabeça. |
| T06 Oven A | Posição frontal 3/4; abrir porta, pá com pizza atravessar entrada, pizza pousar, retirar pá, fechar. | Pizza e boca do forno visíveis. |
| T07 Packaging A | Reposicionar câmera antes do início, sem parede no foreground; dar destaque a pizza e caixa. | A ação é visível desde o primeiro frame. |
| T07 Packaging B | Pizza entra na caixa, tampa fecha, caixa é apanhada. | Etapas ligadas por contato e animação, não troca brusca de props. |
| T08 Tickets A | Câmera em frente ao mural, push-in; novas comandas surgem sincronizadas com sineta/ação. | Mural e pedido legíveis. |
| T09 Kitchen Chaos A | Movimento coreografado: um passa correndo, outro reage, pizza quase cai, gesto de recuperação; câmera acompanha. | Sensação de urgência sem personagens deslizando em pose rígida. |
| T09 Kitchen Chaos B | Descartar a câmera atual; substituí-la por handheld lateral ou close de confusão. | Não há roupa/mesh encobrindo a tela. |
| T10 Burnt Pizza A | Close do forno e pizza escurecendo, fumaça crescendo; personagem olha/reage. | Pizza queimada identificável sem explicação. |
| T11 Backpack A | Plano médio 3/4 de funcionário pegando a caixa, colocando na mochila e partindo a pé. | Pegada/encaixe sincronizados, caminhada animada e câmera não entra na parede. |

## Validação e testes

- Criar comando/editor tool `Validate All Trailer Shots` que gera relatório Markdown/JSON com shot, tempo, falha, objeto e coordenadas; minimizar falsos positivos por colliders que não são obstáculos visuais.
- Captura de ao menos 3 screenshots por take (início, meio, fim), além de storyboard visual/contact sheet; sinalizar se >30% do quadro está obstruído por uma mesma malha inesperada e se o assunto sai do quadro.
- Registrar para cada ator: Animator presente? Avatar válido? Clip reproduzido? Pose/ossos mudam no tempo? Há animação nula? Se não, falhar e aplicar fallback.
- Validar sobreposição visual de mão->prop, base do objeto->bancada e pés->piso em instantes de contato.
- Rodar repetição do mesmo take 2x para confirmar determinismo; resetar estado entre tomadas; não depender de flags globais nem da ordem anterior de renderização.
- Testar no editor e, se houver ferramenta de captura em batchmode, testá-la separadamente. Testes puramente em batchmode não substituem inspeção de artefatos visuais.
- Realizar commits pequenos: `cinema/p0-camera`, `cinema/p1-actors`, `cinema/p1-actions`, `cinema/p2-shot-redirection`, `cinema/p2-validation`, `cinema/p3-recorder` e `cinema/p4-final-capture` (nomes ilustrativos; vídeos não entram nos commits).

## Critérios de aceite para liberar os takes

- **0 câmera dentro da geometria** e 0 intervalos longos inteiramente encobertos.
- **0 personagem principal deslizando em T-pose/idle congelado** enquanto deveria agir ou andar.
- Todas as interações principais possuem gesto perceptível e mudança de prop sincronizada.
- Nenhuma ferramenta atravessa descaradamente mãos/bancada/personagens no foco da cena.
- **Cenário, personagens e shaders do projeto mantidos** (exceto ajustes de instância exclusivos ao Studio).
- 16 takes continuam exportáveis individualmente com nomes e duração identificáveis; qualidade de cada um revisada visualmente, não só por sucesso do export.

## Anti-objetivos

Não refatorar: IA de clientes, combate, pedidos, delivery real, Netcode, física global, NavMesh, arquitetura da pizzaria, geração procedural da cidade ou gameplay cooperativo. Não converter todo o jogo para Timeline. Não substituir o elenco por modelos novos sem necessidade; não instalar um grande pacote externo se há clips/rigs aproveitáveis. Não recriar todo o Studio do zero: preservar estrutura útil de shot/export/câmeras, trocando apenas partes que impedem encenação de qualidade.

## Execução recomendada

O implementador deve primeiro auditar o código real do Studio, identificar os componentes, cenas, clips, prefabs e bindings existentes, elaborar uma matriz `reaproveitar / corrigir / isolar / substituir` e só então modificar. Fazer um piloto fim a fim com T03 Dough A, T06 Oven A e T11 Backpack A: um gesto de bancada, uma câmera bloqueada e uma ação de personagem em locomoção. Validar os 3; então replicar a infraestrutura aos outros takes. **Não gastar tempo polindo VFX antes de assegurar enquadramento, animação e contato.**


## P3 — Integração obrigatória com Unity Recorder

### 3.1 Auditoria e prova de captura

- Ler `Packages/manifest.json`, `Packages/packages-lock.json` e a documentação/API da versão instalada. Confirmar versão da Unity, pipeline gráfico, Recorder e ferramentas de animação disponíveis. Não reinstalar nem atualizar pacotes sem necessidade.
- Localizar o exportador existente e reaproveitar sua fila, catálogo de takes, nomenclatura e interface. Usar as APIs compatíveis do Recorder (por exemplo, `RecorderController`, `RecorderControllerSettings`, `MovieRecorderSettings` e input de câmera, se disponíveis na versão instalada). Não presumir assinaturas de API.
- Manter código dependente de `UnityEditor` em assembly/pasta Editor; separar orquestração runtime quando necessário. Não introduzir dependência do Recorder no build do jogo.
- Fazer um smoke test de poucos segundos com imagem em movimento usando a câmera real do Studio. Verificar que o MP4 abre, não está preto e contém frames distintos. Só depois executar a fila piloto.
- Preferir Editor gráfico com GPU e Play Mode se necessário. Batchmode somente se a combinação Unity/Recorder/backend demonstrar captura válida; nunca usar `-nographics` para captura. Não abrir outra instância do mesmo projeto enquanto estiver bloqueado por uma Unity já aberta. Não fechar outras instâncias/projetos sem autorização.

### 3.2 Perfis de captura

| Parâmetro | Preview | Final |
|---|---|---|
| Resolução | 960 × 540 | 1920 × 1080 |
| FPS | 30 constante | 60 constante |
| Formato | MP4 | MP4 |
| Qualidade | Média | Alta |
| Áudio | Desligado | Desligado |
| HUD/debug | Oculto | Oculto |
| Preset visual | O mesmo do jogo | O mesmo do jogo |

Usar duração individual existente ou ajustada pela direção do take. Preservar uma pequena margem de edição no início/fim quando possível, sem poses paradas artificiais. Não reduzir silenciosamente resolução, FPS ou qualidade final; registrar incompatibilidades e tentar configuração suportada equivalente antes de declarar bloqueio. Não alterar configurações globais permanentes do projeto para exportar.

### 3.3 Fila determinística de gravação

Implementar máquina de estados com registro persistente: `Pending → Setup → Warmup → Validate → Record → Finalize → Verify → Done/Failed`. Cada take tem ID estável, câmera, duração, seed, bindings, perfil, caminho e status.

1. Carregar cena/shot correto e restaurar baseline de atores, props, Timeline, partículas, Animator, IK e DOTween. Cancelar tweens e callbacks pendentes da tomada anterior.
2. Preparar câmera exclusiva de captura e ocultar interfaces indesejadas somente no Studio. Confirmar enquadramento, iluminação, materiais e bindings.
3. Executar warm-up suficiente para carregar recursos, avaliar rig e estabilizar iluminação/partículas. Não gravar a inicialização. Restaurar o instante inicial planejado após warm-up sem reintroduzir os problemas de inicialização.
4. Sincronizar Recorder e sequenciador por uma única autoridade temporal. A duração da animação deve ser independente da velocidade real da máquina; não usar relógio de parede para eventos coreografados. Evitar dupla avaliação do Animator/Timeline/DOTween. Testar que o primeiro e último frames correspondem à ação prevista e que o gesto não muda de velocidade entre 30 e 60 FPS.
5. Iniciar gravação somente quando o Recorder estiver pronto, reproduzir o take, encerrar no frame planejado e aguardar finalização efetiva do arquivo antes de iniciar outro.
6. Validar arquivo e registrar resultados. Em caso de falha, parar/limpar Recorder, restaurar estado, preservar diagnóstico e permitir nova tentativa limitada. Continuar os demais takes independentes se for seguro; não ficar em loop infinito.
7. Restaurar configurações de câmera, tempo, HUD, qualidade e editor ao terminar ou cancelar. Garantir cancelamento seguro, sem considerar um vídeo incompleto como aprovado.

Persistir manifesto fora do estado volátil do Play Mode para suportar reloads e retomada. Usar timeout por etapa, fila serial e proteção contra duas gravações simultâneas. Se houver falha compartilhada na infraestrutura, interromper a fila e corrigir a causa antes de repetir.

### 3.4 Ferramentas no Editor

Reaproveitar a janela atual do Studio; usar Odin onde já existir integração. Adicionar somente controles faltantes:

- `Validate All Takes`
- `Render Selected Take`
- `Render Pilot Takes`
- `Render All Previews`
- `Render All Final Takes`
- `Resume Failed/Pending Takes`
- `Cancel Render Queue`
- `Open Output Folder`

Mostrar take ativo, etapa, progresso por frames/takes, perfil, último erro e caminhos. Não depender de cliques manuais entre tomadas. Disponibilizar entrypoint automatizável compatível com o ambiente local para iniciar a fila; documentar o comando real usado, sem inventar caminhos ou métodos antes da auditoria.

### 3.5 Organização dos arquivos

```text
TrailerOutput/
  Previews/       # 16 MP4, incluindo versões aprovadas dos pilotos
  Final/          # 16 MP4 finais aprovados
  Reports/        # manifesto, logs, contact sheets, resultados e relatório
```

Preservar exatamente os IDs dos arquivos originais (T01_CityReveal_A até T11_Backpack_A, incluindo variações A/B). Não sobrescrever os vídeos originais enviados. Gravar tentativas em staging ou nome temporário com extensão compatível; publicar no destino final somente após verificar a integridade. Ignorar vídeos, capturas pesadas e arquivos temporários no Git; manter documentação e código versionados. Verificar espaço livre suficiente antes da fila. Não subir MP4 para GitHub.

## P4 — Execução efetiva e gates de qualidade

| Gate | Ação obrigatória | Condição para avançar |
|---|---|---|
| G0 | Auditar e testar Recorder | Compilação válida e MP4 de smoke test reproduzível |
| G1 | Corrigir câmeras, atores e ações dos pilotos | T03 Dough A, T06 Oven A, T11 Backpack A passam preflight |
| G2 | Gravar e revisar os 3 pilotos em 540p30 | Movimento corporal, contato e câmera convincentes; corrigir e regravar quando necessário |
| G3 | Aplicar infraestrutura aos demais takes | 16 takes configurados, resetáveis e com bindings válidos |
| G4 | Gravar e revisar os 16 previews | Todos passam critérios visuais; regravar apenas os reprovados |
| G5 | Executar fila final 1080p60 | 16 MP4 íntegros e revisados, ou bloqueadores individualizados |

Não avançar a fila final em massa se os pilotos estiverem ruins. Um take aprovado não precisa ser renderizado novamente quando o ajuste for isolado em outro take. Mudanças compartilhadas de animação, câmera ou tempo exigem revalidação dos takes afetados.

### Verificação técnica e visual de cada MP4

- Confirmar existência, tamanho não nulo, duração coerente com o shot, resolução, taxa de quadros e decodificação até o final. Usar ffprobe/ffmpeg se disponíveis; caso contrário, usar alternativa local suportada. Não tratar tamanho de arquivo como prova de qualidade.
- Gerar contact sheet com início, meio, fim e instantes de contato/transformação; inspecionar também sequência temporal do vídeo, pois frames isolados não revelam deslizamento, tremor ou teleporte.
- Verificar frames pretos, congelamentos não intencionais, clipping, camera dentro de mesh, gestos ausentes, ferramentas soltas, continuidade, shader rosa e HUD indevido. Distinguir ausência de áudio intencional de erro.
- Comparar velocidade e sincronização entre preview e final. Revalidar os MP4 finais, não apenas as capturas da cena.
- Se o agente não tiver capacidade de inspeção visual de vídeo ou imagens, registrar explicitamente `visual review pending`; não declarar aprovação fictícia. Gerar as evidências para revisão, sem exigir gravação manual do usuário.

### Manifesto e relatório final

Para cada um dos 16 takes, registrar ID, nome do arquivo, caminho, duração prevista/real, resolução, FPS, perfil, resultado do preflight, resultado técnico do arquivo, resultado visual, número de tentativas, observações e bloqueios. Separar `renderizado`, `validado tecnicamente` e `aprovado visualmente`. Incluir resumo  N/16 em cada categoria, lista de arquivos produzidos e instrução exata para retomar apenas pendências.

**Definition of Done:** código compilando, três pilotos validados, 16 previews revisados, 16 MP4 finais 1080p60 efetivamente existentes e tecnicamente verificados, revisão visual registrada, relatório e fila reutilizável. Se ambiente, rig, GPU ou Recorder impedirem alguma etapa, entregar todos os resultados possíveis e declarar exatamente o que falta; nunca afirmar sucesso sem evidência.

## Ordem de prioridade para o prazo

1. Enquadramento e animação corporal/contato nos pilotos, junto com prova de captura.
2. Coreografia mínima convincente em todos os 16 takes; evitar perfeccionismo em VFX.
3. Previews, revisão e correções localizadas.
4. Exportação final automática e relatório.

Preservar o gameplay, evitar mudanças em prefabs compartilhados e não recomeçar o Studio do zero. Não usar screenshots estáticos como substituto dos MP4. Não encerrar ao implementar os botões: iniciar e acompanhar a fila real no ambiente disponível.

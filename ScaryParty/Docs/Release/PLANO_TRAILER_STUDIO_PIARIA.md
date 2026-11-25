# P.I.A.R.I.A — Plano de implementação da cena de gravação do trailer

**Versão:** 1.0 · 25/11/2026  
**Destino:** Antigravity / Unity · execução no checkout local  
**Repositório:** https://github.com/TavernaDasMilPortas/ScaryParty  
**Base consultada:** `main`, `5f508f457deffefcfd35dd242acc64a5712d24d9`. Existem também `fix/auditoria-marcos-noturnos` e `fix/auditoria-marcos-noturnos-r2`; conferir o HEAD local antes de executar. Não trocar de branch automaticamente.  
**Objetivo:** produzir 15 takes de gameplay encenado, repetíveis, para gravação pelo usuário e edição externa de um trailer de 40–60 segundos. Não é um sistema de produção cinematográfica genérico nem um novo modo de jogo.

## 0. Contrato de entrega (ler primeiro)

Ao final, o usuário deverá abrir `TrailerStudio.unity`, apertar Play, selecionar um take no Inspector, apertar Reproduzir e gravar o Game View. Os NPCs executarão as ações automaticamente e as câmeras estarão prontas. Cada take é independente: não precisa executar T01 para gravar T12. Todos devem repetir sem modificar a cidade ou os assets de produção.

**Decisões fixas:** 15 Timelines separadas; um único diretor runtime; quatro atores principais com a aparência real dos jogadores; cidade gerada uma vez por sessão (ou baked isoladamente); pizzaria e estilo da gameplay atual; resolução de referência 1920×1080, 16:9, 60 fps se a máquina sustentar; áudio de jogo separado da música do trailer; nenhum texto de trailer/logotipo/trilha obrigatório na cena. A introdução de demissão e anúncio já planejada será editada à parte; não reconstruí-la. A entrega final é a cena Unity e seus assets, não um vídeo.

**Escopo de autenticidade:** trata-se primariamente de reconstituição visual dirigida das mecânicas existentes. Reaproveitar a apresentação real sem exigir que todos os NPCs sejam clientes NGO. Não fingir que uma animação cinematográfica é captura de uma funcionalidade jogável inexistente. POV cinematográfico deve corresponder à câmera normal do jogo, mas não deve ser rotulado como gameplay bruto se for encenado. Incluir opção de HUD somente se for possível usar a apresentação real sem efeitos colaterais; caso contrário, não criar HUD falso. Nenhum take depende de rede externa. Não reimplementar domínio, economia, pedidos, navegação global ou multiplayer.

**Não fazer:** trocar materiais originais; sobrescrever cena/prefab de produção; modificar `ProjectSettings/EditorBuildSettings.asset`; instalar pacotes; alterar regras globais, `Time.timeScale` global, Render Pipeline Asset, input ou cenas do lobby; adicionar assets externos sem autorização; abrir build headless ou rodar Unity em paralelo com o editor já aberto. Sem exportador de vídeo, editor de coreografias, sistema genérico de cutscenes, login ou menus novos. Sem gerar 15 cidades. Sem alterações de arte escondidas para mascarar problemas de gameplay. Se uma ação estiver indisponível, documentar e usar fallback visual claramente descrito; se não houver visual equivalente, substituir por enquadramento de uma ação real.

**Gate inicial:** antes de editar, registrar HEAD, branch e `git status --short`, localizar projeto Unity (`ScaryParty/`), preservar mudanças locais, identificar versão do Unity e pacotes instalados. Ler `AGENTS.md` e instruções relevantes do checkout. Fazer as alterações em área isolada; não commitar sem orientação explícita do usuário. Não alterar cenas originais nem gerar arquivos gigantes no repositório por acidente.

## 1. Inventário de código e dependências verificadas

Caminhos relativos a `ScaryParty/Assets/Piaaria/`, salvo indicação. Os nomes e métodos desta tabela foram consultados na branch `main`; fazer a reconciliação local, não presumir que todos os nomes permaneceram idênticos. Os prefabs grandes de pizzaria não retornaram YAML textual utilizável pela consulta remota: **as coordenadas e referências reais devem ser extraídas pelo editor local**, não inventadas a partir de nomes ou desenhos.

| Fonte | Evidência / como usar | Restrição |
|---|---|---|
| `Scripts/City/CityGenerator.cs` | `OnNetworkSpawn` usa `config.seed` quando diferente de zero; `GenerateCity()` sorteia outra seed, modifica `config.seed`; `GenerateLocally(int)` é privado; `CityData`, `CurrentSeed`, `OnCityGenerated`, `GetPathfinder()`; gera malha, pizzaria, entregas e NavMesh | `Awake` em Play remove `__City__`; em Edit Mode grava `GeneratedCityData.asset` em pasta de produção; não chamar Quick Rebuild nem usar reflection como solução permanente |
| `Scripts/City/CityConfig.cs` | Configuração de grid, ruas, módulos, seed, população, tráfego, paredes e pontos de entrega | Clonar config, não salvar sobre o original |
| `Scripts/Pizzeria/Integration/CityPizzeriaPlacementAdapter.cs` | Instancia localmente sem rede; usa `PizzeriaFootprint`, posiciona no piso, registra posição e rotação em `CityData` | Não instanciar pizzaria duas vezes; `PizzeriaSpawner.SpawnPizzeria` requer servidor |
| `Prefabs/Pizzeria/PizzeriaModular_Manual.prefab`, `PizzeriaModular_CelShaded.prefab` | Candidatos reais; preferir o prefab efetivamente escolhido na gameplay atual e seu estilo ativo | Não assumir que o cel shaded é o mais atualizado funcionalmente; verificar a cena ativa e `VisualStyleManager` |
| `Scripts/Pizzeria/Composition/PizzeriaFootprint.cs` | `localBounds`, `floorSurfaceLocalY`, `entranceDirectionLocal`, `entranceAnchor`, `kitchenRect`, `GetStructuralRegions()`, `GetExteriorRegions()` | Bounds e valores default não provam geometria atual; inspecionar instância local |
| `Scripts/Pizzeria/Composition/PizzeriaExteriorAnchors.cs` | `motorcycleSpawn_01`, `streetExit`, `dispatch`, `dismountLeft/Right`, `playerSpawns`, `employeeSpawnCook`, `employeeSpawnCashier` | Usar quando válidas; criar overrides apenas na cena de trailer |
| `Scripts/Pizzeria/Stations/StationView.cs` e `Scripts/Pizzeria/Authoring/StationWorkpostTarget.cs` | `stationId`, `slotAnchors[]`, `targetStation`, `slotIndex`, `slotAnchor`; estações tipadas (`PrepStation`, `AssemblyStation`, `OvenStation`, `PackagingStation`, `TicketBoardStation`, `PhoneStation`, `CustomerCounterStation`, `StorageStation`, `ToolRackStation`) | Resolver por tipo + slot + referência serializada; não fixar IDs globais ou posições arbitrárias |
| `Prefabs/Characters/CharacterVisual.prefab`, `Resources/CharacterVisual.prefab`, `Resources/CharacterAnimator.controller` | Visual real; confirmar qual é referenciado pelo player prefab atual | Não usar capsule, manequim ou NPC alternativo como personagem final |
| `Scripts/Player/CharacterVisualController.cs` | `SetLocomotion(float,float,bool)`, `SetPreparing(bool)`, `SetSitting(bool,bool)`, `SetColor(Color)`, `PlayBodyReaction(...)`, `LeftHandBone`, `RightHandBone`, `ChestBone`, `HeadBone` | Uma autoridade por Transform; `SetSitting` é pose de sentar, não presumir que é pose de motocicleta |
| `Scripts/Pizzeria/Presentation/EmployeeAgentController.cs` | Exemplo de locomoção e preparação de funcionário; IA acoplada à tarefa, domínio e rede | Não usar IA autônoma para os atores principais |
| `Scripts/Pizzeria/Presentation/ItemPresentationCatalog.cs`, `ItemPresentationProfile.cs` | `FindProfile(...)`, `GetProfileByKey(...)`, `visualPrefab`, `remoteLeftHandPose`, `remoteRightHandPose`, `stationSlotPose`, `gripPointName`, `supportPointName` | Usar visuais sem NetworkObject; confirmar definição correta, pois `FindProfile` pode retornar fallback incorreto |
| `Scripts/Pizzeria/Presentation/PizzeriaItemVisualizer.cs` | Visual de item, cache de estações/mãos, camadas de pizza | Dependente de estado de rede; não habilitar como se fosse um renderer offline |
| `Scripts/Pizzeria/Presentation/KitchenProcessEffects.cs` | Efeitos e trajetórias de utensílios/ingredientes; `CreateEffectPreview(Vector3)`; `VFX_Mess.vfx` e `VFX_Smoke.vfx` | `LateUpdate` exige `PizzeriaNetworkState` e NGO ativo; não instanciar este componente sozinho para simular processamento |
| `Scripts/Pizzeria/Presentation/StationDoorPresenter.cs` | `SetSessionOpen`, `SetTransferActive`, `SnapClosed` | Reusar em instância isolada se funcional sem dependências |
| `Scripts/Pizzeria/Stations/OvenStation.cs` | `GetEntryAnchor()`, `entryAnchor`, `slotAnchors`, `ovenFireLight`, `smokeParticles` | `GetEntryAnchor()` pode criar fallback; validar a abertura real |
| `Scripts/Pizzeria/Presentation/OvenTransferVisualizer.cs` | Fluxo mão → entrada → slot; tempos padrão 0,25/0,40/0,25 s | Depende de `PizzeriaItemVisualizer`, registro de estação e coroutines; reconstituir apenas a trajetória visual no modo offline |
| `Scripts/Pizzeria/Presentation/BackpackVisualizer.cs`, `Authoring/DeliveryBagAuthoring.cs` | Visual de bolsa, encaixe no tronco, referência a cargo e apresentação | `Update` depende de rede; usar mesh autorada e mount validado, não adicionar lógica de mochila fake |
| `Scripts/Pizzeria/Presentation/EntityVisualAnchorRig.cs` | `seatAnchor`, `handlebarLeftAnchor`, `handlebarRightAnchor`, `cargoAnchor`, `backpackBackAnchor` | Priorizar âncoras reais; não chamar `ResolveAnchor` com coordenadas inventadas sem revisão |
| `Scripts/City/DeliveryMotorcycleController.cs` | Controlador real da moto, referências para banco, guidão, saídas, carga, aceleração | Para take offline, mover a instância visual em rota dirigida; não depender de ownership/inputs/RPC |
| `Scripts/City/DeliveryPoint.cs` | `AddressId`, `associatedBuilding`, marcador flutuante | A entrega normal ocorre por ponto de endereço, não necessariamente por NPC recebendo caixa |
| `Scripts/Pizzeria/Presentation/PizzeriaSoundManager.cs` | Clipes de pickup, preparo, forno, embalagem, entrega, erro, etc. | Usar apenas clipes realmente atribuídos; não disparar evento que depende de rede |
| `Scripts/Pizzeria/Presentation/PlaytestDebugController.cs` | Horário, pausa e controles de teste existentes | Reusar estratégia de iluminação e não seus RPCs; descobrir serviço de iluminação real no checkout |
| `Scripts/Cutscenes/Editor/CutsceneSceneAssembler.cs`, `Scripts/Cutscenes/Runtime/` | Timeline, montagem de cenas, câmera e binding existentes | Não invocar builder de cutscene se ele alterar Build Settings ou Resources globais; usar como referência |
| `Scripts/Player/FirstPersonCameraController.cs`, `PlayerCameraSetup.cs` | Câmera FP real e dependências de NGO/input | Reproduzir pose/lens de câmera sem ativar input nem disputar câmera com Timeline |
| `ScaryParty/Packages/manifest.json` (raiz repo) | Cinemachine 2.10.5 na base consultada; conferir Timeline e versões | Não escrever APIs de Cinemachine 3 se o projeto usa 2.10; verificar asmdefs |

## 2. Arquitetura mínima e arquivos

Todos os novos assets dentro de `Assets/Piaaria/TrailerStudio/`. Nomes de classes abaixo são **novos componentes propostos**, não existentes. Pode manter menos arquivos se estiver mais simples, desde que as responsabilidades e os testes sejam preservados.

```text
TrailerStudio/
  Scenes/TrailerStudio.unity
  Runtime/TrailerDirector.cs
  Runtime/TrailerActor.cs
  Runtime/TrailerActions.cs
  Runtime/TrailerTake.cs              # dados serializados simples
  Editor/TrailerStudioBuilder.cs
  Editor/TrailerStudioValidator.cs
  Profiles/TrailerStudioProfile.asset
  Profiles/TrailerCityConfig.asset     # cópia, se necessária
  Timelines/T01_CityReveal.playable
  ... T02–T15 ...
  Generated/                          # só assets pequenos necessários, nada de cache enorme
  Tests/                               # testes leves e isolados
  README_GRAVACAO.md
```

Hierarquia prevista na cena:

```text
TrailerStudio
  Environment (cidade, pizzaria, iluminação)
  Anchors (poses/rotas/câmeras vinculadas à geometria real)
  Cast (A, B, C, D, figurantes)
  Props (objetos visuais exclusivos da cena)
  Vehicles (moto + figurantes de tráfego)
  Cameras (MainCamera, CinemachineBrain, rigs virtuais T01–T15)
  Director (PlayableDirector, TrailerDirector, AudioSource)
```

A cena é a entrega, não a ferramenta. O builder é apenas um comando idempotente para montar/atualizar os assets uma vez. Não criar editor visual de ações. Usar serialização simples `[Serializable]` + listas e referências fortes; sem DSL textual, parser ou sistema de comandos genérico. Um `TrailerTake` pode ser um bloco serializado no profile em vez de 15 ScriptableObjects adicionais. Cenas e assets devem existir salvos, com bindings preenchidos, não apenas scripts que o usuário precisa configurar.

### 2.1 City setup — gerar uma vez, não duplicar a cidade

1. Identificar a cena de gameplay que realmente está sendo usada pelo fluxo atual de lobby/launch. Conferir componente `CityGenerator`, `config`, `pizzeriaPrefab`, materiais viários, modular catalog/profile, estilo e iluminação. Não supor que `GameScene_CelShaded` é a cena final só pelo nome.
2. Criar cena nova vazia. Copiar apenas configuração/referências de ambiente da cena real, nunca seu NetworkManager, player, UIs, lógica de run ou objetos `DontDestroyOnLoad` indiscriminadamente. Clonar `CityConfig` (incluindo refs de catálogos/materiais) e configurar seed não zero, `gerarEmTempoReal` desligado se necessário para evitar regeneração. Começar com o tamanho real ou grid reduzido suficiente para os planos; não trocar o estilo urbano.
3. Escolher entre os caminhos abaixo, nesta ordem de preferência. Testar apenas um vertical slice antes de implementar os 15 takes:
   - **A — geração offline isolada em Play:** utilizar a mesma rotina do `CityGenerator` com uma entrada explícita de seed, sem alterar Quick Rebuild. Se não houver API adequada no HEAD, permitir uma alteração mínima e opt-in em `CityGenerator` (método público específico ou sobrecarga que delegue a `GenerateLocally(seed)` em ambiente offline, sem `SetDirty`, sem rede, sem regenerar config original), com guarda de `Application.isPlaying`, `!IsSpawned` e `NetworkManager` não ouvindo. Desabilitar por opção exclusiva do trailer qualquer inicialização de eventos/tráfego/autonomia que conflite com a coreografia. Não modificar a rota normal de produção. Documentar diff e teste de regressão; se o usuário exigir zero diff no código de produção, usar B.
   - **B — cidade estática isolada:** usar geração/preview do editor em uma cópia temporária e redirecionar a persistência do `CityData` exclusivamente para `TrailerStudio/Generated/`; nunca acionar `GenerateCity()` na cena de produção. Salvar apenas a hierarquia visual/colliders essenciais como cena/prefab isolado; remover/desabilitar o gerador na cena baked para não perder `__City__` em `Awake`. Preservar pose da pizzaria, âncoras, grafo/rotas úteis e materiais; validar tamanho do diff. Não exportar uma cidade gigantesca se o modo A for viável.
   - **C — host local real somente se A/B falharem por dependência incontornável:** clonar setup de gameplay em cena isolada, configurar seed no clone antes de spawn, aguardar `OnCityGenerated`, NavMesh e prontidão da pizzaria; desabilitar fluxo de cutscene/menus/noite somente na instância de trailer. Não criar 4 clientes simulados, não forjar IDs RPC, não invocar serviços privados por reflection. Atores principais continuam dirigidos; se um sistema de rede exigir identidade real, usar sua apresentação offline para os planos encenados. Não introduzir uma arquitetura híbrida para todos os takes sem necessidade.
4. Seed inicial sugerida `20261125` (apenas candidata, não validada). Se não houver avenida ou fachada legível, testar no máximo três seeds iniciais e selecionar uma. Registrar seed escolhida, grid, posição/rotação da pizzaria e endereço escolhido. Não recriar a cidade ao selecionar take.
5. Usar a pizzaria que o gerador posiciona; exatamente uma instância. Em offline, `CityPizzeriaPlacementAdapter` já tem fallback de instanciação local. Verificar que a pizzaria e o pátio têm malhas e iluminação atuais. Não confiar na presença do `PizzeriaSpawner` para offline. Se a cidade gerar lógica inesperada em componentes de rede, desabilitar apenas esses componentes no clone/instância de trailer antes de execução relevante, mantendo visuais e colliders. Nunca desabilitar um pai que contém malhas necessárias.
6. Aguardar ambiente pronto (cidade gerada, instância válida, estações indexadas, NavMesh quando utilizado e rotas validadas). Não usar `WaitForSeconds(5)` para mascarar prontidão. Timeout explícito e diagnóstico no Inspector, sem iniciar take incompleto.
7. Pedestres e tráfego: usar poucos figurantes coreografados ou tráfego real se estável, com seeds e poses iniciais controladas. Preferir dois pedestres e 1–2 carros em enquadramentos externos a uma cidade cheia de IA imprevisível. Carros no eixo de vias, pedestres na calçada. Desabilitar bloqueios, eventos de cidade e hostilidade no trailer; não modificar seus assets originais. Evitar áreas de muralha defeituosa no enquadramento sem alterar geometria de produção.

### 2.2 Resolução de âncoras (uma vez após gerar)

`TrailerStudioBuilder`/bootstrap deve produzir um inventário real da cena e do prefab efetivo: tipo de estação, `stationId`, índice/Transform do slot, collider, posição mundial/local, nome e caminho de hierarquia, dimensões e âncoras de interação. Registrar no README ou `Generated/TrailerBindingsReport.md` (sem dumps extensos de YAML).

Chaves exigidas (nomes novos do studio, não nomes presumidos do prefab):

| Chave | Resolução prioritária | Uso |
|---|---|---|
| `PREP_DOUGH` | `PrepStation` compatível com processo de massa + slot válido | T02/T03/T09 |
| `PREP_CUT` | `PrepStation` compatível com corte + slot válido | T02/T04/T09 |
| `ASSEMBLY` | `AssemblyStation.slotAnchors` | T02/T05/T09 |
| `OVEN_SLOT`, `OVEN_ENTRY` | `OvenStation.slotAnchors`, `GetEntryAnchor()` | T02/T06/T10 |
| `PACKAGING` | `PackagingStation.slotAnchors` | T02/T07/T09 |
| `TICKETS` | `TicketBoardStation` e âncoras de tickets reais | T08/T09 |
| `STORAGE`, `TOOLS` | `StorageStation` e `ToolRackStation` válidos | T02/T09 |
| `DISPATCH`, `BAG` | `PizzeriaExteriorAnchors.dispatch` + bolsa visual real | T11 |
| `ENTRANCE`, `FACADE` | `PizzeriaFootprint.entranceAnchor`, direção da entrada; exterior | T01/T11 |
| `PARKING`, `STREET_EXIT` | `PizzeriaExteriorAnchors.motorcycleSpawn_01`, `streetExit` | T12 |
| `DELIVERY` | `DeliveryPoint` escolhido por caminho válido e fachada legível | T13/T14/T15 |
| `ROUTE_START/END`, `ROUTE_POINTS` | Grafo viário / pathfinder real; pontos centrais de pista | T12/T13/T14 |

Não usar primeira estação encontrada por tipo se houver múltiplas: enumerar, filtrar pela operação, escolher deterministicamente por slot válido e ergonomia, gravar no profile/cena. Se ID duplicado na produção, não corrigi-lo incidentalmente; usar referência direta na cena de trailer e reportar. Se posições reais só estiverem disponíveis após geração, resolver por tipo + identificador estável e coordenada local da pizzaria; nunca persistir refs a objetos runtime no asset.

Para cada estação: definir `workAnchor` (slot) e `actorPose` (pé do ator e yaw para a bancada), separados. Derivar direção de trabalho de `StationWorkpostTarget`, geometria e lado caminhável, não presumir `station.forward` correto. Usar offsets iniciais 0,65–1,1 m em frente ao ponto de trabalho, ajustar ao tamanho do modelo e validar por raycast de piso/NavMesh. Testar mão/mesa sem interpenetração. Se o offset for insuficiente, usar override serializado na cena local. Nunca alterar `slotAnchors` do prefab de produção.

### 2.3 Atores, objetos e apresentação

A (azul): massa/ingredientes. B (vermelho): montagem. C (verde): forno e embalagem. D (amarelo/laranja ou cor existente): comandas e entrega. Usar quatro instâncias do visual real, cores do catálogo de gameplay. NPCs de fundo usam mesmo modelo/variações legítimas; não precisam simular pedidos. Não usar `PlayerState`, `NetworkTransform`, `NetworkAnimator`, `PlayerInteraction` ou `EmployeeAgentController` como responsáveis pelos atores dirigidos. Verificar Animator/Avatar e `CharacterVisualController` em todos.

`TrailerActor` contém root de posição, referência a visual/Animator, sockets de mãos e mochila, estado de locomoção e ações atuais. Movimentação preferencial: caminhos de NavMesh pré-calculados, seguidos pelo script com interpolação determinística, `Animator.applyRootMotion=false` enquanto o script possui o root. Se usar NavMeshAgent, ele é único dono durante movimento e é desligado antes de pose/assento; não animar Transform do root pela Timeline. Calibrar velocidade observando clips (base anterior indicava Walk 2 e Run ~5,335; validar controlador atual). Pontos de partida: andar 1,8–2,2 m/s, correr 3,5–5,0 m/s, giro suave ~360°/s. Não acelerar personagem sem sincronizar animação; calcular comprimento do caminho e ajustar janela do take em vez de teleporte visível. NavMesh path completo ou rota explícita de pontos validados; sem atravessar mesas, portas, paredes ou outros atores.

`TrailerActions`: poucos métodos visuais locais e explícitos, chamados em intervalos do relógio do take. `Move`, `Face`, `Prepare`, `Attach`, `Place`, `SwapVisual`, `OvenTransfer`, `Pack`, `BagEquip/Store`, `Mount`, `Drive`, `React`, `PlayFx`, `PlaySound`. Nomes propostos. Reusar `SetLocomotion`, `SetPreparing`, `SetColor` e reação corporal real quando for apropriado. Se um evento Feel não funcionar offline, aplicar apenas reação simples local com o mesmo visual, sem adicionar pacote. Não mover os ossos de mão de forma arbitrária por cima de animações; utilizar sockets e offsets do `ItemPresentationProfile`, fixar orientação do grip e suporte. IK local somente se necessário e já compatível com rig; enquadramento de plano fechado pode evitar problemas de mão. Um prop tem exatamente um proprietário (slot, mão, mochila, veículo, ponto ou root de props) de cada vez. Nunca duplicar visual na mão e na estação durante transferência; troca de modelo ocorre em frame de contato. Reusar materiais, cores e composição real da pizza, sem criar texturas novas. Criar catálogo local explícito dos estágios efetivamente suportados; `FindProfile` tem fallback, validar `visualPrefab` e `category/definitionId` para cada item.

Efeitos: preferir assets reais (`VFX_Mess`, `VFX_Smoke`, `smokeParticles`, luz do forno), instanciados diretamente como efeitos isolados, com seed visual fixa se suportada. `KitchenProcessEffects.LateUpdate` retorna sem rede; não deixar dependência silenciosa. Para a pizza no forno, usar âncoras mão → boca → slot e arco suave inspirado no `OvenTransferVisualizer`; não chamar a coroutine de rede com ID fake. Portas: `SetTransferActive`/`SetSessionOpen` em instâncias isoladas quando seguro; senão animar apenas o transform da folha usando poses reais do `TransformDoorView` e restaurar com `SnapClosed`. Efeitos de preparo só enquanto ação ocorre, sem nuvem cobrindo toda a pizza. Sons: obter clipes existentes da instância real; AudioSource local, sem trilha; se clip estiver vazio, não inventar áudio nem bloquear take, reportar ausência. Gravar som do jogo limpo para pós-produção.

Mochila: extrair visual autorado e encaixar em `backpackBackAnchor` ou no `ChestBone` com offset real. Não depender de `BackpackVisualizer.Update` offline. Moto: reutilizar mesh/prefab efetivamente usado (o placeholder `Playtest/Prefabs/Visual_DeliveryMotorcycle.prefab` só se for o visual atual); obter seat/handlebars/cargo anchors. Usar a pose de montagem/assento real se existir; se não existir, começar take com ator já montado e gravar transição por corte, não fazer pose sentada quebrada. A moto move pelo eixo da faixa com yaw tangente à rota e rodas girando se houver pivôs; inclinação limitada 0–10° se visual atual suportar. Não fazer corrida em calçada. Em T14, entregar no endereço real: se a gameplay não possui NPC recebendo em mãos, mostrar caixa sumindo no ponto/feedback autêntico, não inventar um sistema de handoff.

## 3. Timelines, relógio e reset — especificação operacional

Criar 15 `TimelineAsset`s independentes, 60 fps de referência (confirmar API disponível). Um único `PlayableDirector` recebe o asset selecionado e configura bindings correspondentes. Cada Timeline contém uma Cinemachine Track ligada à `CinemachineBrain` da câmera principal (Cinemachine 2.x), clips de câmera/shot e, se necessário, tracks de animação para rigs de câmera **não para roots de atores**. Bindings por referência explícita a vCams e rigs, não por nome global. Câmeras alternativas A/B/FP usam o mesmo relógio e ações, sem duplicar take; podem ser tracks mutuamente exclusivas ou vCam override simples. Sem câmera alternativa quando ela não agrega valor.

Usar clock único: `TrailerDirector` calcula `t` em segundos desde o início após setup, atualiza ações e `PlayableDirector.time`, com `DirectorUpdateMode.Manual` e `Evaluate()` (ou modo equivalente comprovado na versão local). Não usar `Timeline Signal` como fonte exclusiva de ações com estado, porque seek/replay pode ignorar notificações. Implementar eventos de cruzamento de tempo uma vez (`oldT < cue <= newT`, incluindo cues de t=0 no Start), e ações contínuas avaliadas por progresso local; ordenar ações de mesmo timestamp por índice estável. Atores A–D têm trilhas de ações simultâneas e independentes. O director controla início, pausa e fim; quando pausado, congelar Animator e efeitos do trailer de maneira local, não `Time.timeScale`. Evitar DOTween/coroutine com relógio separado; se utilizar tween/efeito existente, fazer cancelamento e garantir que pause/reset funcione. Em fim, segurar último frame por margem ou parar. `Loop` reinicia estado, não apenas voltar o playhead. Sem scrub arbitrário no Inspector: seletor de take e restart são suficientes; se usuário arrastar Timeline no Editor, preview de câmera é aceitável, mas ação runtime deve ser reproduzida do início.

Cada take possui 0,75 s de pré-roll (estado inicial e câmera estável), janela de ação de duração na tabela, 0,75 s de pós-roll. Duração bruta nominal abaixo exclui margens: 109 s de ação + 22,5 s de margens (131,5 s total se todos gravados uma vez). Cues nas fichas são relativos a t=0 da ação; no Timeline físico, adicionar 0,75 s. Não inserir fade a preto entre takes — editar depois. Expor tempo local e estado no Inspector. Se gravação de ação começar em t=0, configurar Animator/props antes do pré-roll.

Reset obrigatório antes de cada take:
1. Parar diretor e cancelar tweens/coroutines/eventos/efeitos locais; desativar câmeras anteriores e limpar blend.
2. Restaurar props e porta/forno, luzes, fumaça, áudio, materiais/MPBs e poses de câmera; destruir apenas props temporários do take ou retornar ao pool. Não regenerar cidade.
3. Restaurar transforms dos atores, cores, estado de animação, speed, `SetPreparing(false)`, `SetSitting(false,false)`, mãos vazias; resetar Animator para estado inicial válido (validar `Rebind` + `Update(0)` quando aplicável). Resetar posição/rotação e estado da moto; limpar ownership de prop.
4. Aplicar preset de iluminação, cenário, props e posições iniciais do take específico, inclusive figurantes e tráfego no plano. Aguardar um frame de estabilização de bindings/Animator quando necessário, antes da captura.
5. Reconfigurar asset/bindings da Timeline, tempo zero, escolher câmera ativa, avaliar, mostrar `Ready`. Só então iniciar pré-roll. Em reset após pausa, restaurar velocidades locais.

Teste determinístico: repetir cada take 3 vezes; posições iniciais e finais dos atores/props iguais dentro de 5 cm e 2° (para entidades coreografadas); mesmos cues ±1 frame a 60 fps em modo de teste manual. Não exigir determinismo bit a bit de partículas ou tráfego decorativo, mas trajetórias de personagens/moto e enquadramento devem ser equivalentes. Usar testes manuais em Play Mode; não alegar renderização verificada apenas por compilação.

## 4. Convenções de câmera e fotografia

Usar câmera normal do projeto como referência de lens/FOV e URP, sem importar controlador de input. Uma única câmera de saída e AudioListener. Para câmera virtual, Cinemachine 2.x `CinemachineVirtualCamera`/`CinemachineBrain`, hard cut entre planos, blend apenas quando especificado; se camera rig com animação de Transform, configurar Body/Aim para não sobrescrever seu movimento. Em geral, movimentar um dolly/rig e mirar target de ação; evitar 15 cópias da câmera de render. Não usar motion blur forte ou DOF que esconda interação. As cores da gameplay e a geometria devem permanecer reconhecíveis; usar luzes existentes e ajuste local discreto somente se necessário.

Sistema de coordenadas para fichas: `Q` = ponto focal real (pizza/ator/entrada etc.); `F` = direção horizontal da fachada para rua ou direção de frente da estação (do trabalho para o corredor); `R = cross(up,F)` = direita do observador que usa esse referencial; `U` = cima. Em fichas de moto, `F` = tangente de avanço e `R = cross(U,F)`. Posição de câmera `Q + R*x + U*y + F*z`. `Q` já inclui altura do alvo; y é offset relativo, não altura absoluta. Todos os valores em metros, **pontos de partida** que precisam ser ajustados ao tamanho e orientação reais do prefab, não coordenadas globais arbitrárias. Em câmera superior, usar up fixo de imagem alinhado a F, evitar singularidade de LookRotation. Campo de visão vertical inicial: geral 50–65°, médio 42–55°, close 32–45°, FP = valor do player real. 16:9; câmera near 0,03–0,1 conforme necessidade (evitar clipping), far adequado à cidade; manter configuração de produção quando possível.

Validação espacial para cada câmera: raycast/spherecast de câmera a alvo (ignorar o próprio alvo e colliders não visuais), verificar quinas, teto, portas e props; mover câmera ou ajustar rig se houver oclusão. Não atravessar parede para obter plano; não esconder teto globalmente — somente se plano aéreo for explicitamente estilizado e sem confusão com gameplay. Verificar início, meio e fim da trajetória e frustum para atores importantes. Enquadramento seguro: ação central visível; deixar 8–10% de respiro de bordas; evitar horizonte inclinado sem intenção; evitar mover câmera e ator rápido na mesma direção se obscurecer interação. Planos fechados devem ter 1–2 s de leitura limpa. Aumentar duração de ação se necessário em vez de esconder erro com corte.

Iluminação padrão: pôr do sol/começo da noite, candidato 19h no sistema do jogo, cozinha com luzes atuais. Fixar a hora e exposição por sessão/take; não avançar ciclo enquanto gravando. Se 19h estiver escuro ou estética divergente, selecionar uma hora única validada (17h–22h) e registrar. T10 pode ter luz do forno/fumaça, sem alterar céu. Priorizar continuidade das cores em todos os planos.

## 5. Shot list — 15 Timelines com ações e cues

### Visão geral

| ID / asset | Ação | Janela de ação | Prioridade | Enquadramento |
|---|---|---:|---|---|
| T01_CityReveal | Revelação da cidade e fachada | 8 s | P1 | Aéreo em descida |
| T02_KitchenTeam | Equipe cooperando | 10 s | P0 | Geral interior + alternativa FP |
| T03_Dough | Abrir massa | 6 s | P0 | Close lateral/superior |
| T04_Ingredients | Cortar ingrediente | 6 s | P1 | Close diagonal |
| T05_Assembly | Montar pizza | 8 s | P0 | Superior + alternativa FP |
| T06_Oven | Colocar pizza no forno | 6 s | P0 | Baixo três quartos |
| T07_Packaging | Embalar pizza | 6 s | P0 | Close três quartos |
| T08_Tickets | Comandas acumulando | 5 s | P1 | Médio para close |
| T09_KitchenChaos | Correria cooperativa | 10 s | P0 | Travelling interno |
| T10_BurntPizza | Pizza queimando | 6 s | P1 | Médio + reação |
| T11_Backpack | Preparar entrega | 6 s | P0 | Médio e acompanhamento |
| T12_MotorcycleStart | Saída da moto | 8 s | P0 | Baixo lateral |
| T13_CityRide | Percurso de moto | 10 s | P0 | Travelling lateral/traseiro |
| T14_Delivery | Entrega no endereço | 8 s | P0 | Plano médio |
| T15_FinalGag | Pizza cai novamente | 6 s | P1 | Médio/close de reação |

P0 significa essencial para mostrar o loop de jogo; P1 complementa a montagem. Implementar todos os 15; se houver bloqueio, entregar P0 primeiro e registrar claramente o que falta, não substituir silenciosamente. Tempos abaixo incluem atuação mas não pré/pós-roll. Ações de cozinha em takes de detalhe podem começar com os ingredientes já preparados; não esperar o tempo real de uma receita completa. Mostrar transformação plausível, sem alegar que esse é o tempo real de gameplay. Mesma pizza hero (receita visual válida escolhida após inspeção), mesma caixa, mesmos atores e cores entre takes, salvo pizza queimada T10 e gag T15.

### T01 — CityReveal (8 s)
**Objetivo:** mostrar que é uma cidade jogável e localizar a pizzaria. **Inicial:** fachada legível, rua livre, D perto do pátio, A/B/C em interior quando visíveis, 2 pedestres de fundo, 1 carro. Q = centro visual da fachada perto da entrada (altura ~1,6 m acima do piso). Câmera A: de `Q + (R*-10 + U*14 + F*24)` para `Q + (R*5 + U*3 + F*13)`; FOV 58°, easing suave, trajetória sem atravessar telhados; se a cidade tiver quadras maiores, aumentar início. Mirar de centro da quadra à fachada. **Cues:** 0–2 s vista da cidade; 2–5 s descer e deslocar rumo à fachada; 5–8 s segurar a revelação, pedestres passam na calçada e D se aproxima da entrada; carro cruza fundo apenas na via. **Áudio:** ambiente e carro se disponível. **Aceite:** pizzaria reconhecível por >=2 s, nenhuma muralha/cidade cortada no fundo, rota sem clipping, nenhum pedestre obstrui logo/fachada. **Alternativa:** plano fixo mais baixo da fachada se descida não couber no layout.

### T02 — KitchenTeam (10 s)
**Objetivo:** vender o multiplayer sem texto. **Inicial:** A no preparo com massa e rolo, B na montagem com base e toppings, C no forno/embalagem com pizza pronta, D na comanda/telefone; cada um ocupa estação distinta e props exclusivos. Q = centro da área operacional obtido pelos 4 work anchors, altura média ~1,35 m. Câmera A geral `Q + (R*-3 + U*1.8 + F*5)` → `Q + (R*3 + U*1.5 + F*5)` (ajustar eixo da cozinha ao lado caminhável), FOV 60°. Câmera B FP de D, lente do jogo, observando os colegas a partir de pose livre, sem mesh do próprio rosto na frente. **Cues:** A 0–5 preparar/5–8 levar massa para apoio; B 0–3 olhar/preparar base/3–7 adicionar ingredientes/7–10 segurar resultado; C 0–3 retirar pizza pronta/3–7 embalar/7–10 levar caixa ao apoio; D 0–3 observar quadro/3–6 pegar comanda/6–10 andar para bancada e virar aos demais. Ações paralelas, evitar trajetórias cruzadas. **Aceite:** no geral aparecem >=3 personagens durante >=4 s, funções distintas, ao menos uma interação visível por ator, sem teleportes/oclusão de bancada. Se o quarto não couber, reenquadrar para ver 3 e mostrar o quarto na alternativa, não remover paredes.

### T03 — Dough (6 s)
**Objetivo:** interação tátil/satisfatória. **Inicial:** A diante de `PREP_DOUGH`, massa crua e rolo reais sobre slot/apoio; fundo B desfocado naturalmente pela distância, não por DOF agressivo. Q = centro da massa. Câmera A `Q + (R*0.9 + U*0.65 + F*1.15)` → `Q + (R*0.55 + U*0.55 + F*0.95)`, FOV 38–45°. Câmera B superior `Q + U*1.8 + F*0.12` ou FP à altura dos olhos (usar uma alternativa). **Cues:** 0–0,8 s A pega rolo; 0,8–4,2 s `SetPreparing(true)`, rolo avança/retorna 2–3 vezes conforme apresentação real, VFX curto; 4,2 s swap massa para estágio aberto quando rolo encobre contato; 4,2–5,1 s parar preparo, devolver rolo; 5,1–6 s mostrar disco aberto. **Áudio:** dough/colocação. **Aceite:** massa inicial e final distintas e reais, rolo sem atravessar mesa, transformação não ocorre longe das mãos, partículas não encobrem resultado. Se não houver malha intermediária, usar os dois estágios reais sem morph inventado.

### T04 — Ingredients (6 s)
**Objetivo:** mostrar variedade e VFX de corte. **Inicial:** A ou B diante de `PREP_CUT`, faca e ingrediente que possui processo válido (tomate/cogumelo/calabresa conforme catálogo atual), tábua/slot. Q = ingrediente. Câmera A `Q + (R*-0.75 + U*0.65 + F*1.05)` com arco curto até `Q + (R*-0.45 + U*0.72 + F*1.0)`, FOV 40°. **Cues:** 0–0,8 pega faca; 0,8–3,9 prepara/corta com 3 batidas e FX sincronizado; 3,9–4,4 troca para fatias reais; 4,4–5,2 deixa faca; 5,2–6 mostra fatias. **Áudio:** cut real, sem efeitos duplicados. **Aceite:** lâmina não atravessa corpo, fatias reconhecíveis, resultado fica no slot, câmera vê ação. Se rig não permite faca segura, usar animação de ferramenta flutuante do jogo com enquadramento fechado.

### T05 — Assembly (8 s)
**Objetivo:** revelar composição da pizza. **Inicial:** B em `ASSEMBLY`, base aberta, molho/queijo/toppings conforme receita real; mesma pizza hero. Q = centro do disco. Câmera A quase superior, `Q + U*1.8 + F*0.12` → `Q + U*1.55 + F*0.12`, FOV 38–45°, orientação estável. Câmera B FP, ver mãos/slot na perspectiva real, sem cabeça/torso próprio. **Cues:** 0–1,1 base legível; 1,1–2,6 adicionar molho; 2,6–4,0 queijo; 4,0–5,6 topping 1; 5,6–6,8 topping 2 se receita válida (senão repetir adição real de outro ingrediente); 6,8–8 revelar pizza montada. Utilizar camadas/prefabs e posições do sistema de composição; se o visualizador precisar de rede, pré-instanciar estágios derivados dos mesmos assets e trocar por contato. **Áudio:** sauce/cheese/place. **Aceite:** ordem visual coerente, ingredientes não flutuam no resultado, pizza não muda de receita entre takes, todos os estágios claramente visíveis. Não simular ingredientes inexistentes.

### T06 — Oven (6 s)
**Objetivo:** destacar a porta e animação do forno. **Inicial:** C diante de `OVEN_ENTRY` com pizza montada na mão, slot vazio, porta fechada, luz/fumaça padrão desligadas. Q = boca do forno. Câmera A `Q + (R*1.0 + U*0.30 + F*2.1)` → `Q + (R*0.7 + U*0.25 + F*1.8)`, FOV 45°, não dentro da porta. **Cues:** 0–0,8 personagem se posiciona; 0,8–1,05 abrir porta; 1,05–1,5 trajetória mão→entrada→slot (0,4 s aproximado, arco pequeno real); 1,5–1,8 fechar; 1,8–3,5 luz/efeito de forno assando se visual atual comportar; 3,5–5,1 C olha para estação seguinte; 5,1–6 segura forno/resultado. Não mostrar pizza pronta instantânea como mecânica: T07 começa separadamente com pizza já assada. **Áudio:** porta/inserção, fogo se existir. **Aceite:** pizza entra pela abertura, não atravessa folha fechada, porta fecha por completo, prop único no slot.

### T07 — Packaging (6 s)
**Objetivo:** mostrar resultado e transição para entrega. **Inicial:** C em `PACKAGING`, pizza assada (mesma receita), visual de caixa aberta se existe no jogo e válido. Q = centro da área de embalagem. Câmera A `Q + (R*1.0 + U*0.75 + F*1.25)` → `Q + (R*0.8 + U*0.65 + F*1.1)`, FOV 42°. **Cues:** 0–1,0 mostrar pizza pronta; 1,0–2,7 iniciar preparação/embalagem; 2,7–3,5 pizza/caixa muda para visual boxed real; 3,5–4,4 fechar tampa somente se mesh/rig real permite, caso contrário mostrar troca/feedback igual ao jogo; 4,4–5,2 C pega caixa; 5,2–6 vira para apoio/dispatch. **Áudio:** cardboard/pack. **Aceite:** pizza permanece visível antes da caixa, caixa final real, tampa não atravessa pizza, não inventar animação de caixa articulada se não existe. **Alternativa FP:** câmera do ator C com FOV real, visuais das mãos coerentes com gameplay.

### T08 — Tickets (5 s)
**Objetivo:** introduzir pressão sem texto explicativo. **Inicial:** D diante do quadro de comandas, 1 comanda visível, 2–3 slots válidos livres. Q = centro do quadro, usar frente real. Câmera A `Q + (R*0.8 + U*0.1 + F*2.1)` → `Q + (R*0.4 + U*0.05 + F*1.2)`, FOV 44°. **Cues:** 0–1 s D observa; 1,0/1,7/2,4 s novas comandas entram em slots reais, uma por vez, com os assets do jogo (não fabricar texto ilegível); 2,4–3,4 D vira cabeça/torso para cozinha; 3,4–5 s reação curta real de Feel ou mudança de postura, sem animação inédita complexa. **Áudio:** telefone/comanda se existente. **Aceite:** comandas não sobrepostas e quadro legível; D visível no plano médio ou em lateral, sem ocupar quadro; não inserir mais comandas do que slots válidos. Se quadro da gameplay estiver visualmente quebrado, usar outra tomada de pressão (telefone e equipe), reportar.

### T09 — KitchenChaos (10 s)
**Objetivo:** take principal de comédia cooperativa, sem depender de bugs. **Inicial:** quatro atores, cada qual com ação e prop, 2 pedidos visíveis, rotas pré-calculadas e sem conflito. Q móvel = D no corredor, com visibilidade dos demais. Câmera A acompanha a partir de `D + R*1.7 + U*1.1 - F*2.3` (Q no peito de D), FOV 58°, dolly estabilizado, manter plano médio/geral; não prender rigidamente à cabeça. Alternativa FP de D, ver colegas. **Cues:** A 0–3 corta/prepara, 3–6 leva item ao apoio, 6–10 retorna para preparo; B 0–2 pega ingrediente, 2–6 monta, 6–10 reação e trabalha; C 0–3 forno, 3–7 embala, 7–10 segura caixa no apoio; D 0–2 comanda, 2–5 corre pelo corredor, 5–6 pausa/olha colega, 6–9 corre para apoio, 9–10 olha o forno ou outro colega. Em 4–6 s planejar aproximação de A e D com distância mínima ~0,7 m (ajustar aos colliders), sem atravessar corpos; D pode fazer pequena parada cômica. **Áudio:** efeitos de passos e estações, sem excesso. **Aceite:** sensação de movimento e humor, pelo menos 3 atores reconhecíveis, nenhuma colisão de coreografia, sem travamento de NavMesh; repetir com a mesma rota. Não usar ragdoll inventado ou física caótica sem controle.

### T10 — BurntPizza (6 s)
**Objetivo:** humor e consequência do atraso. **Inicial:** C perto do forno, pizza já assada próxima do estado queimado, fumaça desligada, B ao fundo. Q = abertura do forno. Câmera A `Q + (R*1.0 + U*0.55 + F*2.0)` → `Q + (R*1.35 + U*0.85 + F*2.6)`, FOV 46–52°, recuo revela reação. **Cues:** 0–1 s aviso/forno quente; 1–2,3 fumaça real aumenta; 2,3–3,0 C se vira/aproxima; 3,0–3,3 porta abre; 3,3–3,8 pizza queimada visual real sai ou é revelada no slot; 3,8–4,8 C reage ao erro; 4,8–6 segurar pizza escura/fumaça e B olhando. **Áudio:** aviso/erro/forno. **Aceite:** fumaça não cobre personagem inteiro, pizza queimada distingue-se da hero, não usar incêndio/explosão inexistente. Se não houver visual queimado, usar efeito e reação sem inventar textura.

### T11 — Backpack (6 s)
**Objetivo:** conectar caixa ao entregador. **Inicial:** D em apoio/dispatch com caixa(s), mochila real perto ou já no torso dependendo da mecânica e pose possível. Q = peito/costas D, preservar mão e mochila no frame. Câmera A `Q + (R*1.8 + U*0.3 + F*0.7)` → `Q + (R*1.4 + U*0.35 - F*0.6)`, FOV 50°, vira para entrada quando D sai. **Cues:** 0–1,2 equipa mochila, se a animação real estiver disponível; senão iniciar equipada e mostrar claramente; 1,2–3,4 pega caixa e guarda (se suportado, segunda caixa em 3,4–4,0); 4–6 D inicia corrida para saída, câmera acompanha 2–4 m. Mostrar exatamente a apresentação da mochila do jogo (se caixas não aparecem no mesh, mostrar gesto/caixa entrando, não inventar geometria de carga externa). **Áudio:** pickup/pack/backpack. **Aceite:** mochila bem encaixada no tronco, sem clipping severo, caixa desaparece só quando guardada, D tem mãos livres ao terminar.

### T12 — MotorcycleStart (8 s)
**Objetivo:** mostrar partida do entregador e ligar cozinha à cidade. **Inicial:** moto no `PARKING` com alinhamento de saída e D com mochila, caminho até `STREET_EXIT` e faixa livre; usar pose real de montaria, se existir. Q = moto (altura do chassi/assento). Câmera A `Q + (R*-2.5 + U*0.2 + F*2.5)` com pequeno pan até D, FOV 55°, câmera próxima ao solo mas acima dele. **Cues:** 0–1,8 D chega ao lado da moto por anchor de desmontagem; 1,8–2,8 monta (somente se pose validada; caso contrário iniciar já montado em 0, usar 0–2,8 como preparação e close); 2,8–3,4 segurar guidão/assento; 3,4–6,5 acelera pela saída, mantendo rodas no chão; 6,5–8 entra na via, passa pelo plano ou sai de frame. **Áudio:** buzina/motor somente se existentes; sem áudio de motor inventado. **Aceite:** sem penetração no assento/guidão, sem teleporte visível, não passa pela calçada fora do acesso autorizado, enquadramento mostra moto e mochila. Se montagem não houver, documentar e gravar partida já montada; não bloquear T12.

### T13 — CityRide (10 s)
**Objetivo:** plano de velocidade e mundo procedural. **Inicial:** D montado, mochila, moto na faixa correta de trecho de via validado; escolher rota com 1 curva suave ou cruzamento e prédios que não ocluam a câmera. Comprimento sugerido ~55–90 m para 10 s (calcular com velocidade/corredor real; reduzir se necessário). Câmera A lateral `Q + (R*3.5 + U*1.0 - F*1.5)` com suavização controlada no tempo e yaw pela tangente, FOV 56–60°. Alternativa traseira `Q + (R*0.6 + U*1.6 - F*4.2)` semelhante à perspectiva de perseguição, ou FP só se jogo suporta visão na moto. **Cues:** 0–2 aceleração suave; 2–6 percorrer avenida; 6–8 curva/cruzamento validado, velocidade menor; 8–10 estabiliza e segue. Figura de tráfego em faixa oposta ou distante, nunca depender de quase atropelo. **Áudio:** ambiente/moto existente. **Aceite:** câmera não entra em edifício ou veículo, moto não invade calçada, rodas tocam chão, curva sem salto ou rotação súbita, fachadas e rua visíveis. Se rota longa não permitir travelling lateral sem oclusão, usar traseiro e trecho menor.

### T14 — Delivery (8 s)
**Objetivo:** fechar o ciclo de gameplay com feedback de entrega. **Inicial:** endereço real escolhido, moto próxima parada em local permitido, D montado com mochila/caixa armazenada. Q = ponto de entrega e D, não centralizar marcador na frente do rosto. Câmera A `Q + (R*2.0 + U*0.75 + F*2.5)` → `Q + (R*1.4 + U*0.65 + F*2.1)` usando direção de frente do prédio, FOV 50°. **Cues:** 0–1,2 parar (ou moto já parada); 1,2–2,2 desmontar apenas se pose válida; alternativa começar D ao lado da moto; 2,2–4,3 D caminha ao ponto; 4,3–5,2 pega caixa da mochila; 5,2–6,2 entrega no `DeliveryPoint` com feedback real quando disponível; 6,2–8 reação de sucesso e breve hold. Não criar NPC receptor se a entrega real é por endereço/marcador; pode ter pedestre figurante no fundo. **Áudio:** delivery/success. **Aceite:** endereço/ação coerentes com jogo, caixa sai do estado de carga uma vez, sem mão vazia antes da entrega, moto fica fora da entrada do prédio. Se feedback UI exige rede, mostrar confirmação visual existente ou usar reação corporal, não inventar interface.

### T15 — FinalGag (6 s)
**Objetivo:** callback à pizza derrubada da introdução. **Inicial:** D (ou A se continuidade narrativa pedir) com pizza/caixa real em mãos, no pátio ou frente de entrega, área de piso clara. Q inicial = peito do ator, Q secundário = prop no chão. Câmera A `Q + (R*0.6 + U*0.15 + F*2.4)`, FOV 45°, leve tilt/pan acompanha a queda, depois volta ao ator; sem corte obrigatório. **Cues:** 0–1,2 personagem caminha ou para com caixa; 1,2–1,9 solta visual local, queda controlada por curva/parábola até piso medido (usar física somente se reprodutível), pequeno bounce/impacto com asset real; 1,9–3,0 segura pizza/caixa no chão; 3,0–4,2 ator olha para baixo; 4,2–5,2 olha para câmera por rotação de corpo/cabeça existente; 5,2–6 hold cômico. **Áudio:** impacto/erro; reservar espaço para corte da música na edição. **Aceite:** impacto visível, prop não atravessa chão, olhar compreensível, sem nova mecânica fictícia (se caixa não pode abrir ao cair no jogo, mantenha caixa fechada). Gag é cinematográfica; não é evidência de uma mecânica de queda inexistente.

## 6. Inspector e operação (Odin)

No GameObject `Director`, expor apenas:

- `Take`: dropdown T01–T15 com nome legível.
- `Camera`: A (padrão), B/FP quando existe; selecionar só as disponíveis.
- `Loop Take`: false por padrão.
- `Play All`: sequência dos takes sem fade e com reset entre eles; opcional se implementação custar pouco.
- `Lighting`: preset fixo por padrão; override 17h/19h/22h opcional, respeitando a iluminação real.
- `Speed`: 1× padrão; 0,5× apenas para inspeção se efeitos e animações sincronizam. Não gastar tempo em suporte a speed se isso quebrar sincronização.
- Botões `Prepare / Reset Take`, `Play`, `Pause / Resume`, `Stop`, `Next Take`; mostrar `Environment Ready`, `Current Take`, `Local Time`, `State`, `Last Validation Error`, seed e câmera ativa em read-only.
- `HUD`: false; disponibilizar somente se há integração real segura; ocultar debug, gizmos e cursor no Game View de gravação, sem remover recursos de gameplay globalmente.

Usar atributos Odin somente sob condições e asmdefs compatíveis com o projeto; sem `UnityEditor`/OdinEditor em código runtime. Se Odin estiver indisponível, Inspector nativo com Editor custom mínimo ou botões contextuais é suficiente. Não criar UI Toolkit nova. Botões perigosos ou regeneração apenas em Edit Mode no builder; seleção de take e reprodução em Play Mode. Nenhum controle deve aparecer na imagem gravada. Se a cena for aberta diretamente, ela deve funcionar sem passar pelo lobby.

## 7. Builder e validação — tarefas concretas

### 7.1 Menu de autoria

Criar `Tools/P.I.A.R.I.A/Trailer Studio/Build or Update Scene` e `Validate Scene` apenas. O builder deverá:

1. Resolver cena e assets reais, validar versões, obter referências fortes para config, catálogos, modelo, animator, prefabs e iluminação. Se duas opções plausíveis, escolher a efetivamente usada no gameplay; reportar decisão.
2. Criar pastas e cena isolada se não existirem; preservar overrides de câmera/âncoras já ajustados. Atualizar somente objetos/arquivos com identificador inequívoco do builder. Não apagar arquivos preexistentes sem checar origem.
3. Gerar roots, profile, instâncias/refs do elenco e câmera principal, vCams, diretor, bindings, Timelines T01–T15 com duração e nomes especificados. Onde a cidade existe apenas em runtime, salvar dados e rigs dependentes de âncoras locais; bind pós-geração de maneira explícita e reproduzível. Não tentar salvar referências de objetos destruídos ao sair do Play Mode.
4. Criar pontos de trabalho e rotas a partir do inventário; salvar overrides locais em profile serializado quando ajustes forem necessários. Não editar prefab source nem usar nomes de children como única fonte de verdade.
5. Salvar a cena e assets; incluir `.meta` correspondentes; confirmar que todas as Timelines reabrem com bindings válidos. Rodar builder duas vezes e verificar ausência de duplicatas, refs novas desnecessárias ou mudanças na produção. Não adicionar cena a Build Settings.
6. Fornecer `Validate Scene` com resultado por take: refs obrigatórias, slots, rig, câmera, rota, poses, duração, ações e possíveis fallbacks. Falha em take requerido bloqueia a reprodução daquele take e produz mensagem objetiva (ex.: `T06: OvenSlot 0 não tem Transform`). Aviso em asset opcional não bloqueia tudo.

### 7.2 Testes mínimos de código

Criar somente testes que comprovem o comportamento do estúdio e não iniciem uma bateria gigantesca. Adaptar assemblies de teste existentes, sem alterar globais. Preferir:

- Edit Mode: 15 IDs únicos e Timelines com duração correta (ação + 1,5 s); cues ordenados e dentro da janela; referências obrigatórias válidas; nenhuma instância com controller de ator faltando; perfil não altera asset de origem; geração do builder idempotente, quando viável.
- Play Mode: seleção direta de T07 sem executar anteriores; `Play → Pause → Resume → Reset → Play` sem duplicar evento/prop; loop T03 e T12 duas vezes; `T03 → T12 → T03` sem regenerar cidade e sem resíduo de moto/props; atores A–D com ações paralelas; uma câmera de saída; `Ready` só depois do ambiente; nenhuma exceção ou null ref.
- Teste de integração de seed, se houver mudança mínima em `CityGenerator`: duas inicializações isoladas com mesma seed produzem mesma posição da pizzaria e endereço escolhido; rotina normal de produção mantém comportamento anterior. Nunca iniciar duas cidades simultaneamente com o mesmo singleton.
- Se usar câmera alternativa, confirmar que trocar ângulo preserva a coreografia (não duplica props/efeitos). Se usar Play All, testar sequência T01→T15 com resets.
- Teste de saída do Play Mode: assets de produção, config original, Build Settings e cena original intactos. Limpar recursos locais e callbacks; verificar ausência de listeners duplicados ao reentrar.

A validação programática não substitui ver o Game View. A maior parte dos riscos aqui é visual, não erro de compilação.

### 7.3 Checklist visual obrigatório (um registro por take)

| Item | Critério |
|---|---|
| Enquadramento | A ação principal está inteira dentro do frame, com margem e sem parede/ator na frente |
| Identidade | Modelo, pizza, utensílio, caixa, mochila, moto e materiais correspondem aos assets reais |
| Movimento | Pés no piso; mãos/props alinhados; sem teleporte, sliding excessivo ou clipping evidente |
| Temporalidade | Cues disparam no intervalo previsto; há pré/pós-roll; plano tem tempo suficiente para leitura |
| Limpeza | Sem HUD de debug, gizmos, texto de seleção, console, câmera duplicada, popups ou tela de loading |
| Continuidade | Mesmo visual, iluminação coerente, receita e atores consistentes |
| Reset | Repetição do take e troca para outro take funcionam sem estado residual |
| Desempenho | Gravação do Game View estável na máquina local; se não sustentar 60 fps, registrar e gravar 30 fps estáveis ou ajustar somente perfil do trailer |
| Áudio | Sem clipe duplicado, sem trilha incorporada e sem pico/clipping evidente; ausência reportada |

Revisar o início, ponto mais importante e fim de cada take no Game View. Para planos dinâmicos (T01, T09, T12, T13), assistir integralmente pelo menos duas vezes. Registrar screenshot de 1 frame representativo por take se ferramenta/Editor permitir, sem commitar 15 imagens pesadas; gerar relatório com resultado `PASS / AJUSTAR / BLOQUEADO`, caminho e motivo. Sem declarar PASS se não foi aberto na Unity. Se o agente não tiver acesso ao editor, entregar scripts/scene/assets possíveis, reportar exatamente o que falta validar e deixar uma lista mínima de verificação para o usuário, não inventar sucesso.

## 8. Ordem de execução — reduzir retrabalho e gasto de cota

Executar em quatro fases sequenciais, sem paralelizar modificações nos mesmos scripts/scene. Não construir toda a biblioteca antes de validar o pipeline.

| Fase | Entrega | Gate obrigatório |
|---|---|---|
| A — Auditoria e ambiente | Inventário HEAD local, cena isolada, seed e pizzaria única, materiais, âncoras, câmera e 4 atores | A cena abre e gera o ambiente; pose de estação e câmera corretas, sem rede externa |
| B — Vertical slice | Diretor/clock/reset, T03 massa, T07 embalagem, T12 moto (uma Timeline por take) | Três takes reproduzem, resetam e mantêm a cidade; validar visual de cada categoria antes de replicar |
| C — Biblioteca completa | T01/T02/T04/T05/T06/T08/T09/T10/T11/T13/T14/T15 e alternativas de câmera onde especificadas | Todos os bindings e ações preenchidos; nenhuma dependência surpresa de multiplayer; executar checklist |
| D — Entrega | Inspector, validação final, README_GRAVACAO, relatório de limitações, cena salva | Abrir cena e gravar T02/T05/T07/T09/T12/T13/T14 sem intervenção manual; restantes prontos ou bloqueio explícito |

Se fase B falhar, não construir 12 takes com o mesmo problema. Resolver primeiro: modelo/animator, prop, porta, posição de câmera, moto, clock ou reset. Usar o editor Unity já aberto ou uma sessão local única. Não rodar headless repetidamente, não abrir uma segunda Unity no mesmo checkout. Evitar leitura repetida de pacotes inteiros, gerar inventário compacto, verificar Console após cada fase e corrigir erros diretamente relacionados. Não fazer otimização do jogo inteiro. Não introduzir framework de teste, DI, ECS, novo pipeline de render ou multiplayer artificial.

**Política de fallback:** corrigir primeiro o take usando a âncora/asset real; depois usar override só no studio; depois substituir o enquadramento por outro que mostre a mesma ação; último recurso, omitir ação indisponível e sinalizar no relatório. Não criar placeholders visuais que destoem. Não prometer que o trailer representa comportamento jogável que não foi comprovado.

## 9. Gravação e montagem sugerida (referência para o usuário)

O usuário editará os takes no programa que preferir. A Timeline não deve realizar edição final, fades ou adicionar trilha. Gravar `Game View` em 16:9, 1920×1080; testar 60 fps, usar 30 fps estáveis se necessário. Desativar overlays do editor e notificações, ocultar cursor e usar gravação de áudio do jogo em faixa separada, se o gravador permitir. Para cada take: selecionar, `Prepare`, conferir câmera, iniciar OBS, `Play`, aguardar fim, parar OBS. Gravar câmera A e, quando útil, B/FP em arquivos distintos. Nomenclatura `T03_Dough_A_01.mp4`, `T03_Dough_B_01.mp4` etc. Nenhuma integração OBS/Recorder obrigatória.

Exemplo de montagem de 54 s (não programar esta edição na cena):

| Tempo final | Material / duração |
|---|---|
| 0–11 s | Cutscene de demissão e anúncio já existente, reduzida na edição (11 s) |
| 11–13 s | T01: fachada/cidade (2 s) |
| 13–17 s | T02: equipe trabalhando (4 s) |
| 17–19 s | T03: massa (2 s) |
| 19–21 s | T04: corte (2 s) |
| 21–24 s | T05: montagem (3 s) |
| 24–26 s | T06: forno (2 s) |
| 26–28 s | T07: embalagem (2 s) |
| 28–30 s | T08: comandas (2 s) |
| 30–35 s | T09: caos cooperativo (5 s) |
| 35–37 s | T10: queimou (2 s) |
| 37–39 s | T11: mochila (2 s) |
| 39–42 s | T12: partida (3 s) |
| 42–46 s | T13: moto em movimento (4 s) |
| 46–48 s | T14: entrega (2 s) |
| 48–50 s | T15: piada final (2 s) |
| 50–54 s | Logo original + frase de encerramento (4 s) |

Se a introdução não puder ser reduzida para 11 s, selecionar menos inserts, mas priorizar 2+ jogadores, montagem da pizza, embalagem, moto e entrega. A edição acima pode ser adaptada à batida da trilha sloppy; não escolher música sem autorização.

## 10. Relatório de entrega que o agente deve produzir

Criar `Assets/Piaaria/TrailerStudio/README_GRAVACAO.md` ou documento equivalente curto, contendo:

- Branch/HEAD inicial; caminho da cena; como abrir e operar; versão Unity/pacotes relevantes.
- Seed realmente escolhida, prefab de pizzaria real, estilo e horário; nome do visual da moto e personagens usados.
- Tabela T01–T15 com duração, câmera(s), status visual, principais bindings e fallbacks. Distinguir encenado offline de captura de gameplay real se algum take usar host.
- Inventário de estações resolvidas e overrides locais, sem dump de centenas de linhas; identificar qualquer referência indisponível.
- Testes executados com resultado, screenshots/arquivos locais se existirem; testes não executados e por quê.
- Lista de arquivos adicionados/modificados e justificativa para qualquer exceção fora da pasta do studio. Confirmar que nenhuma cena/prefab/configuração original foi sobrescrita e Build Settings intacto.
- O que o usuário precisa fazer, idealmente apenas abrir cena, Play, selecionar take e gravar. Se exigir ação manual adicional, listar claramente e explicar por que não foi automatizada.

## 11. Prompt para enviar junto com este arquivo ao Antigravity

```text
Leia integralmente o arquivo PLANO_TRAILER_STUDIO_PIARIA.md e execute o plano no checkout atual do P.I.A.R.I.A. O objetivo é entregar uma cena Unity pronta para capturar 15 takes de trailer; não é construir um framework cinematográfico reutilizável nem refatorar o jogo.

Comece conferindo HEAD, git status, AGENTS.md, versão Unity, cena de gameplay efetivamente ativa, prefabs e assets locais. Não sobrescreva alterações existentes, cenas/prefabs originais, configurações ou Build Settings. Use Timelines separadas e um diretor com relógio único para NPCs, props, câmeras e reset. Use assets, animações, materiais e efeitos reais; resolva as posições a partir das âncoras dos prefabs. Não presuma coordenadas, IDs de estação, pose de moto ou disponibilidade de clipes. A alternativa de city generation opt-in só é permitida nos termos do plano, com diff mínimo e teste de regressão.

Execute em ordem A → B → C → D. Valide o vertical slice T03/T07/T12 no Unity Editor antes de criar o restante. Reaproveite a cidade e a pizzaria, sem gerar tudo novamente entre takes. Prefira atuação visual offline isolada, sem fake client IDs, RPCs artificiais ou dependência de servidor externo. Não faça alterações de gameplay por causa do trailer. Não crie sistemas além do necessário para os 15 takes.

Entregue a cena salva e configurada, 15 Timelines com bindings, NPCs dirigidos, câmeras posicionadas, controles no Inspector e reset reproduzível. Teste todos os takes, especialmente câmera/props/locomoção/moto e continuidade visual. Não declare que testou o que não conseguiu executar. Se algum asset ou pose não existir, use o fallback previsto na ficha do take e documente. Não rode Unity headless em loop nem abra duas instâncias no mesmo projeto. Não faça commit/push sem autorização.

No final, escreva README_GRAVACAO.md e um relatório curto com resultados, limitações e arquivos alterados. A entrega esperada é que eu consiga abrir TrailerStudio.unity, entrar no Play Mode, selecionar qualquer take e gravar o Game View sem configurar atores ou câmeras manualmente.
```

## 12. Limites desta especificação

O plano foi elaborado a partir do código e estrutura do repositório, não de execução local da Unity. O conteúdo textual dos prefabs grandes da pizzaria não foi recuperável pela consulta remota; os nomes de scripts e âncoras foram confirmados, mas coordenadas, poses finais, shaders efetivamente ativos, qualidade visual, clipes atribuídos e trajeto de moto ainda precisam de resolução/validação local. As posições de câmera são offsets iniciais deliberados e não devem ser aplicadas cegamente. As durações, seed candidata, cores dos atores, receita hero, estratégia offline e ordem dos planos são escolhas de direção propostas, sujeitas ao inventário real; o objetivo e os 15 takes permanecem fixos. Nenhuma implementação foi feita no repositório por este documento.

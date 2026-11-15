# Auditoria e Correções Base — Plano Noturno (A01–A16)

Este documento mapeia os achados concretos (`A01`–`A16`) da auditoria de experiência completa do **ScaryParty** para os arquivos corrigidos no código-fonte.

---

## 1. Entrada, Carregamento e Posse de Comandos (A01–A02, A14–A16)

| ID | Achado Original | Correção Implementada | Principais Arquivos |
|---|---|---|---|
| **A01 / A02 / A16** | `ReadyRoomPresenter.OnStartGameClicked` permitia transição local fora do servidor e invocava `LoadScene` sem coordenador unificado. | Separação estrita entre Host/Servidor autoritativo e cliente conectado; unificação da transição e tratamento de falha com feedback de contexto em tela de loading. | `ReadyRoomPresenter.cs`, `PizzeriaSceneLoadingCoordinator.cs`, `PizzeriaRoot.cs` |
| **A14** | `TransferItemServerRpc` e `TransferService.TransferItem` não validavam posse do remetente; `requesterId=0` podia colidir com o ID legítimo do Host (`clientId == 0`). | Repasse explícito de `SenderClientId` nos comandos de cozinha/logística, validação de alcance/sessão e `enforceRequesterOwnership` em `TransferService` tratando `0` como Host válido. | `TransferService.cs`, `KitchenCommands.cs`, `LogisticsCommands.cs`, `PizzeriaCommandHandler.cs` |
| **A15** | Embalagem aceitava pizza crua/queimada e resultado de entrega não diferenciava claramente itens entregues e motivo. | Preservação da regra explícita de validação na entrega (`DeliveryEvaluator`) com mensagens claras para pedido ativo vs entregue/inválido. | `DeliveryEvaluator.cs`, `LogisticsCommands.cs` |

---

## 2. Bancadas, Mesas, Cadeiras e Animação de Sentar (A05–A08)

| ID | Achado Original | Correção Implementada | Principais Arquivos |
|---|---|---|---|
| **A05** | `StationProgressBar.Create` aplicava deslocamento fixo `(0, 0.4, 0)` ignorando âncoras e bounds customizados de estações/prefabs. | Uso de âncora dedicada de UI e cálculo por bounds filtrados com margem em metros, sem sobrescrever âncoras configuradas. | `StationProgressBar.cs`, `StationView.cs`, `CustomerCounterStation.cs` |
| **A06 / A07 / A08** | `DiningTableStation` misturava bounds de mundo como tamanho local de `NavMeshObstacle`, misturava espaço local/mundo no `PlateAnchor` e usava a cadeira diretamente como âncora (com `SnapToFloor`), podendo causar cadeiras afundadas ou NPCs mal posicionados. | Associação explícita mesa/cadeira, separação entre âncora de aproximação e âncora de quadril (`SeatHipAnchor`), conversão correta de espaço/escala/rotação para `NavMeshObstacle`/`PlateAnchor`, animação discreta ao sentar na cadeira sem alterar altura arbitrária da raiz do NPC e registro diagnóstico para inspeção na `GameScene.unity` binária no Editor. | `DiningTableStation.cs`, `CustomerNpcView.cs` |

> [!IMPORTANT]
> A cena `Assets/ThirdParty/StarterAssets/ThirdPersonController/Scenes/GameScene.unity` é serializada em formato binário. A confirmação visual de overrides de prefabs/cadeiras instanciadas em cena depende de PlayMode/Editor local aberto.

---

## 3. Cidade Modular e Fachadas (A03, A04, A09)

| ID | Achado Original | Correção Implementada | Principais Arquivos |
|---|---|---|---|
| **A03** | `CityModuleDefinition` (`J` / porta dupla) usava `2m` / `1 slot`, divergindo do requisito de ocupar `4m` / `2 slots`. | Migração da definição `J` para `4m` / `2 slots`, atualização do catálogo (`CityModuleCatalog`), construtor de kit (`ModularCityKitBuilder`) e testes (`ModularCityTests`). | `CityModuleDefinition.cs`, `CityModuleCatalog.cs`, `CityModuleCatalog.asset`, `ModularCityKitBuilder.cs`, `ModularCityTests.cs` |
| **A04** | Reserva fixa de slot de porta e risco de sobreposição/estouro em `ModularBuildingAssembler` e planejamento de fachadas. | Política unificada de ocupação por intervalo contínuo de slots, centro calculado pela largura real do módulo e verificação explícita de encaixe antes da instância. | `ModularBuildingAssembler.cs`, `ModularCityValidator.cs` |
| **A09** | `StreetGridGenerator` descartava segmentos de perímetro inferiores a `10m`, deixando vãos sem acabamento sem segunda passagem de preenchimento seguro. | Diagnóstico de intervalos residuais e segunda passagem para encaixe de módulos/acabamentos compatíveis preservando colisões. | `StreetGridGenerator.cs` |

---

## 4. Trânsito e Cruzamentos (A10–A13)

| ID | Achado Original | Correção Implementada | Principais Arquivos |
|---|---|---|---|
| **A10** | `TrafficManager.AssignRandomPath` escolhia origem aleatória inclusive para veículos já em rota e criava fallback ligando nós em linha reta através de quarteirões. | Replanejamento a partir do `CurrentEndNode` atual com fallback estrito para vizinhos conectados do grafo, proibindo travessia de quarteirões. | `TrafficManager.cs`, `TrafficVehicle.cs` |
| **A11** | `IntersectionTrafficController` usava temporizador local sem diferenciação efetiva entre grupos A e B dos semáforos. | Relógio determinístico baseado em `ServerTime.Time`, grupos independentes Norte/Sul (`0`) vs Leste/Oeste (`1`) alinhados ao `PedestrianCrossing`. | `IntersectionTrafficController.cs`, `IntersectionBuilder.cs`, `TrafficLight.cs` |
| **A12** | `TrafficVehicle` dependia de raycast frontal no mesh/poste e somava deslocamento sem limitar ao waypoint. | Registro estático `TrafficLight.ActiveLights`, detecção por stop-line da faixa/direção do cruzamento e deslocamento limitado via `Vector3.MoveTowards`. | `TrafficVehicle.cs`, `TrafficLight.cs` |
| **A13** | `TrafficManager` indexava veículos visuais pela posição no array RPC sem remover excedentes nem aplicar pose inicial antes de ativar. | Identidade estável por `vehicleId`, dicionário cliente indexado por `vehicleId`, remoção de excedentes e pose aplicada antes de ativar visuais (seguro para *late-join* e reset). | `TrafficManager.cs`, `TrafficVehicle.cs` |

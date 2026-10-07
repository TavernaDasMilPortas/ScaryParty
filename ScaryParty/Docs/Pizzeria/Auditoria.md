# Relatório de Auditoria (Etapa 0)

Este relatório confirma as suspeitas levantadas na leitura inicial e define o caminho técnico para a implementação v2 (Cozinha, Comandas e Delivery).

## 1. Ambiente
* `Unity.exe` e `dotnet` não foram encontrados nos caminhos padrões do terminal do agente, portanto os testes automatizados via CLI não foram executados nesta etapa. No entanto, o `Domain` é puro e poderá ser validado no Unity posteriormente através do Test Runner.
* Decisão: o desenvolvimento prosseguirá de forma estática (inspeção), mantendo a arquitetura segura estabelecida no plano e postergando a verificação `M` (manual/Play Mode) para quando o projeto for rodado no Unity.

## 2. Determinismo da Geração da Cidade
* **Confirmado:** A seed da cidade é gerada pelo servidor (ou host) e atribuída a `_networkSeed` (uma `NetworkVariable<int>`).
* **Confirmado:** Em `CityGenerator.cs`, `System.Random(seed)` é instanciado e passado para todos os sub-geradores (como `DeliveryPointPlacer`, `TrafficManager`, etc.).
* **Conclusão:** O `pointIndex` (que será nosso `AddressId`) atribuído aos pontos de entrega é completamente determinístico. A verificação do `AddressRegistryHash` prevista na Etapa 1 servirá apenas como um *safety net* útil, pois na prática todos os clientes já concordarão sobre os IDs de cada prédio.

## 3. Hierarquia da GameScene e Duplicação (A23/A24)
* **Confirmado:** O arquivo `GameScene.unity` contém um GameObject persistido na cena chamado `__City__` (gerado por uma execução anterior do `CityGenerator`).
* **Confirmado:** Dentro dessa cidade estática salva na cena, no `Block_1`, existe o GameObject `The_Pizzeria_Building` (fileID 1713738686).
* **Confirmado:** Este objeto possui o `NetworkObject` com hash de id 870760880, o componente `PizzeriaRoot` e o `PizzeriaNetworkState`.
* **Conclusão:** A cena tem uma pizzaria e uma cidade chumbadas nela. Quando o servidor iniciar, o `CityGenerator.DestroyStaticCity()` deletará o objeto `__City__` original (e a pizzaria antiga), e o adaptador fará o spawn de uma nova. O `SceneMigrationBuilder` (descrito na Etapa 4 do catálogo de builders) será crucial para limpar a cena, removendo o `__City__` estático e preparando-a para receber o Spawner.

## 4. Input e Menu de Pausa
* **Confirmado:** Não existe um sistema nativo de menu de pausa atrelado à tecla `Esc` ou `Escape` nos scripts do projeto ou do StarterAssets.
* **Conclusão:** A tecla `Esc` (e modais como o de zoom da comanda ou do painel da mochila) pode consumir o input de teclado (via `GameplayInputGate`) de maneira livre e sem causar conflitos com outros sistemas de interface existentes.

## 5. Outline e Shader Illustrate
* **Confirmado:** O shader `Distant Lands/Illustrate/Alpha Clipped` é um shader URP completo (criado via Amplify Shader Editor) e não um overlay post-processing.
* **Confirmado:** O shader não possui propriedades embutidas para gerar outline (como `_OutlineColor` ou `_OutlineWidth`).
* **Conclusão:** Conforme previsto pelo "caminho alternativo" do plano de ação (Etapa 4), o sistema de destaque interativo (`FocusOutlineService`) deverá ser implementado utilizando o método de casco invertido (reverse-hull) através de `Graphics.DrawMesh`. Isso será feito sem duplicar materiais, simplesmente desenhando a malha alvo com um material exclusivo e global `ScaryParty/FocusOutline`.

## Resumo para Execução
As descobertas solidificam o caminho da arquitetura v2. O plano de execução pode seguir exatamente as diretrizes traçadas (Etapas 1 a 9) usando os construtos confirmados acima. A Etapa 0 foi **concluída com sucesso**.

# Registro de Decisões de Arquitetura (ADR)

**ScaryParty — Módulo de Cozinha, Comandas e Delivery**  
**Data:** Outubro de 2026  
**Status:** Consolidado e Implementado

---

## 1. Autoridade e Camada de Domínio Puro

- **Decisão:** Manter todo o domínio (`PizzeriaState`, `OrderService`, `DeliveryEvaluator`, `ProcessingService`, etc.) em um assembly separado (`ScaryParty.Pizzeria.Domain.asmdef`) com `"noEngineReferences": true`.
- **Motivação:** Garante que toda a lógica de negócios possa ser testada sem depender do motor gráfico da Unity ou de chamadas nativas de frame.
- **Sincronização:** O servidor executa as mutações de domínio e projeta deltas via `ReplicationProjector` no `PizzeriaNetworkState`. Clientes leem listas de rede (`NetworkList`) e variáveis (`NetworkVariable`), garantindo determinismo sem race conditions.

---

## 2. Numeração e Exibição de Endereços (`AddressFormat`)

- **Decisão:** Padronizar a numeração exibida aos jogadores como `AddressId + 1` através dos métodos `AddressFormat.Display(id)` e `AddressFormat.Format(id)`.
- **Motivação:** Internamente no código e arrays de cidade, os índices começam em 0 (ex.: `0, 1, 2, ...`). No gameplay e na lore da cidade, residências começam em 1 (ex.: `Casa 1, Casa 2, ...`). Essa abstração evita bugs de "off-by-one" entre visual, minimapa e verificação do servidor.

---

## 3. Descarte de Upgrades e Adoção de Spawning Dinâmico

- **Decisão:** Os upgrades (melhorias de bancadas/forno) foram removidos da interface de usuário e do fluxo de gameplay a pedido do usuário. Em contrapartida, permitiu-se o spawn dinâmico de NetworkObjects.
- **Implementação:** Criação do `PizzeriaSpawner` para instanciar e executar `netObj.Spawn()` na pizzaria e em seus componentes no servidor sem hierarquia pai sob `__City__`. Isso previne destruição acidental da pizzaria ao regenerar blocos da cidade.

---

## 4. Estações de Cozinha e Forno Dinâmico

- **Decisão:** Cozimento com 3 estágios visuais e comportamentais (`Raw`, `Baked`, `Burned`).
- **Implementação:**
  - O forno possui temporizadores individuais por slot.
  - Luz de chamas procedural com flickering via Perlin noise.
  - Aos 100% de preparo (estágio `Baked`), dispara um sino harmônico procedural (*ding!*).
  - Se a pizza permanecer no forno além da janela de tolerância, transiciona para `Burned`, escurece para cinza/preto e emite partículas dinâmicas de fumaça escura.

---

## 5. Pilhas de Expedição e Mochila Térmica

- **Decisão:**
  - Bancada de expedição possui um pallet (`StackStation`) que empilha caixas verticalmente a cada +0.12m.
  - O jogador pode carregar até 3 caixas na mochila térmica nas costas, além de 2 caixas nas mãos.
  - `BackpackVisualizer` renderiza a mochila e caixas nas costas de outros jogadores no modo multiplayer, mas oculta ou ajusta para nunca obstruir a câmera FPS do jogador local.
  - Ao largar a mochila no chão, seus conteúdos acompanham o `BackpackId` com raycast seguro para evitar travessia de paredes.

---

## 6. Entrada e Navegação de Modais (`GameplayInputGate`)

- **Decisão:** Implementar pilha de modais (`GameplayInputGate`) centralizada.
- **Motivação:** Evita que a mira do mouse continue movimentando a câmera enquanto o jogador está navegando em listas (como no Livro de Receitas, Quadro de Comandas ou Seleção de Entrega). A tecla Escape fecha os modais em ordem LIFO antes de acionar a tela de pause do jogo.

---

## 7. Builders Idempotentes e Preservação de Dados Locais

- **Decisão:** Todas as modificações em prefabs ou cenas são gerenciadas por builders que utilizam `BuilderToolkit.EnsureChild` e `GeneratedMarker`.
- **Preservação:** Os arquivos modificados localmente pelo usuário (`DefaultCityConfig.asset`, `UserData.asset`, `CityGenerationLog.json`, `ProjectSettings.asset`) foram rigorosamente preservados sem interferência de git checkout/revert.

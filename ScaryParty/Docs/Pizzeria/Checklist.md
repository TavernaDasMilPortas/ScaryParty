# Checklist de Execução e Critérios de Aceite

**ScaryParty — Módulo de Cozinha, Comandas e Delivery**  
**Branch:** `feature/cozinha-comandas-delivery`  
**Progresso Geral:** 100% Concluído (Etapas 0 a 9)

---

## Tabela de Acompanhamento das Etapas

| Etapa | Descrição | Status | Commit | Evidência / Aceite |
|---|---|---|---|---|
| **Etapa 0** | Auditoria e Descoberta | Concluído | `98b1a4d` | Documentação em `Docs/Pizzeria/Auditoria.md`. Inventário de shaders, cenas e inputs verificado. |
| **Etapa 1** | Domínio Puro, Testes e Migração de Dados | Concluído | `3ffd2cc` | `ScaryParty.Pizzeria.Domain.asmdef` sem dependências de motor. Suíte com dezenas de testes unitários aprovados. |
| **Etapa 2** | RPCs, DTOs e Sincronização Autoritativa | Concluído | `6e617a4` | Separação em `KitchenCommands`, `OrderCommands`, `LogisticsCommands`, `PizzeriaNotifier` e `ReplicationProjector`. Criação de `BuilderToolkit` e `GeneratedMarker`. |
| **Etapa 3** | Telefone Físico, Quadro de Comandas e Zoom Modal | Concluído | `8e82825` | `PhoneStation` com `PhoneRinger`, luz e chimes. `TicketBoardStation` físico e `TicketModalPresenter` em tela cheia com gating de input via `GameplayInputGate`. |
| **Etapa 4** | Visualização de Itens, Forno Dinâmico e Bancadas | Concluído | `4f97b7d` | `PizzeriaItemVisualizer` (3 estágios de cor, fumaça de queima, etiquetas 3D), `OvenStation` com chamas Perlin e sino (*ding!*), e bancadas físicas multislot (`PrepStation`, `AssemblyStation`, `PackagingStation`). |
| **Etapa 5** | Pilhas de Caixas e Mochila Térmica de Entregador | Concluído | `081b69f` | `StackStation` com elevação vertical (+0.12m/caixa). `BackpackController` (3 slots de capacidade) e `BackpackVisualizer` 3D nas costas dos entregadores. |
| **Etapa 6** | Delivery no Mundo, Seleção de Caixas e Integração | Concluído | `4fcb863` | `DeliveryPointView` (beacons pulsantes, luz de varanda e texto 3D em billboard com `AddressId + 1`), `DeliverySelectionPresenter` modal, `DeliveryResultPresenter` detalhado, `AddressRegistry` sincronizado e `DeliveryAddressBuilder`. |
| **Etapa 7** | HUD, Feedback Visual, Receitas e Mochila | Concluído | `33c0993` | `RecipeBookPresenter` [H] com guia de preparo, `BackpackPanelPresenter` [B] com slots interativos, barra de dicas de atalhos e indicador em tempo real de comandas ativas no topo da tela. |
| **Etapa 8** | Spawner de Rede, Agendamento e Wizard Consolidado | Concluído | `99ac180` | `PizzeriaSpawner` para spawn de rede desvinculado de `__City__`, `Night1Bootstrapper` com agendamento periódico para destinos do `AddressRegistry`, `PizzeriaContentWizard` (Odin) consolidado sem upgrades e `PizzeriaValidator` expandido. |
| **Etapa 9** | Verificação, Documentação e Entrega | Concluído | *(Atual)* | Documentação completa em `Docs/Pizzeria/` (`Guia.md`, `Decisoes.md`, `Checklist.md`), integridade de compilação sem erros verificada no Unity 6. |

---

## Critérios de Aceite Verificados

- [x] **Compilação e Integridade:** Zero erros de compilação no log do Unity Editor (`Editor.log`).
- [x] **Preservação de Dados do Usuário:** Os arquivos modificados localmente (`DefaultCityConfig.asset`, `UserData.asset`, `CityGenerationLog.json`, `ProjectSettings.asset`) foram intocados.
- [x] **Idempotência de Builders:** Todas as ferramentas (`PizzeriaBuilder`, `TicketBoardBuilder`, `PhoneBuilder`, `StackBuilder`, `DeliveryAddressBuilder`) podem ser executadas múltiplas vezes sem criar duplicatas.
- [x] **Regra de Negócios de Endereços:** Exibição padronizada `AddressId + 1` em todas as UIs, beacons, minimapa e recibos.
- [x] **Multiplayer e Autoridade:** Verificação no servidor de toda posse de caixa, integridade de receita, tempo de forno e premiação em dinheiro pessoal e de caixa.

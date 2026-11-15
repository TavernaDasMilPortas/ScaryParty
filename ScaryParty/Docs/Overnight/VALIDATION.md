# VALIDATION — Matriz de Validação e Aceite

| Teste | Critério | Status | Evidência |
|---|---|---|---|
| F1.1 | IDs de estações únicos e consistentes | Pass | 0 duplicatas. IDs únicos atribuídos e verificados via `PizzeriaGameplayValidator` (IDs 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21). |
| F1.2 | Slot nulo corrigido no prefab | Pass | `mesa media.002` (AssemblyStation ID 13) reduzida de 5 para 4 slots válidos, sem referências `{fileID: 0}`. |
| F1.3 | Quadros de tickets coplanares e orientados | Pass | `Quadro de Comandas` físico (ID 4) vinculado à cozinha com 6 slots coplanares; `cardapio.001` (ID 15) normalizado com 6 slots coplanares (local Z = 0.06f). |
| F1.4 | Ferramenta Odin de auditoria/reparo idempotente | Pass | `PizzeriaContentWizard.cs` expandido com abas Odin "Auditoria e Reparo" e "Estações e Slots"; `PizzeriaPhase1Repair.Repair` executado via batchmode com retorno code 0 e 0 erros bloqueantes. |
| F2.1 | Navegação NPC sem falso sucesso / recursão | Pending | |
| F2.2 | Máquina de estados sentar/levantar/sair completa | Pending | |
| F2.3 | Preservação visual pedestre -> cliente -> pedestre | Pending | |
| F2.4 | Limite populacional e reset limpo de pool | Pending | |
| F3.1 | Compositor modular de pizza (Sliced, Total, Central, Specific, Ring, Scatter) | Pending | |
| F3.2 | DTO de rede com suporte a lista completa de ingredientes, estágios e porções | Pending | |
| F3.3 | Wizard Odin e Janela de Prévia ao Vivo sincronizados | Pending | |
| F4.1 | Coordenador de loading entre cenas com transição assíncrona segura | Pending | |
| F4.2 | NavMesh pronta antes de spawn de jogadores e NPCs | Pending | |
| F5.1 | Serviço unificado de feedback (Feel + DOTween) sem conflitos | Pending | |
| F5.2 | Matriz de feedback aplicada aos fluxos do jogo | Pending | |
| F6.1 | Otimizações de CPU/GC e redução de overhead da cidade | Pending | |
| F7.1 | Build Standalone sem dependências de Editor | Pending | |
| F7.2 | Smoke test e validação de regressões | Pending | |

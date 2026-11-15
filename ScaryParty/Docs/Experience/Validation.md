# Validação — Plano Noturno de Experiência Completa

Registro transparente entre validações estáticas executadas localmente (`L0/L1`) e testes em aberto para execução manual no **Unity Editor já aberto** (`L2`, política `UNITY_EXECUTION=MANUAL_ONLY`).

---

## 1. Validações Estáticas Executadas (`L0 / L1`)

| Verificação | Escopo | Resultado Observado |
|---|---|---|
| `dotnet build ScaryParty/ScaryParty.Pizzeria.Domain.csproj -p:BuildProjectReferences=false --no-restore -v:minimal` | Assembly de domínio puro (sem `UnityEngine`) | **Sucesso:** 0 avisos, 0 erros |
| `dotnet build ScaryParty/Assembly-CSharp.csproj -p:BuildProjectReferences=false --no-restore -v:minimal` | Runtime completo (incluindo Rede, Cidade, Combate, Moto, UI) | **Sucesso:** 0 erros (84 avisos pré-existentes na primeira compilação, 0 incrementais) |
| `dotnet build ScaryParty/Assembly-CSharp-Editor.csproj -p:BuildProjectReferences=false --no-restore -m:1 -v:minimal` | Ferramentas Editor e construtores modulares | **Sucesso:** 0 erros (28 avisos pré-existentes) |
| `dotnet build ScaryParty/ScaryParty.Pizzeria.Tests.csproj -p:BuildProjectReferences=false --no-restore -m:1 -v:minimal` | Testes EditMode (`PizzeriaDomainTests`, `OvernightExperienceTests`) | **Sucesso:** 0 avisos, 0 erros |
| `git diff --check` | Integridade de diff e espaçamento | **Sucesso:** código de saída 0, sem erros de conflito ou whitespace |

> **Não executado (política `UNITY_EXECUTION=MANUAL_ONLY`):** Execução do `Unity Test Runner` (EditMode/PlayMode), inspeção visual da cena binária `GameScene.unity`, ParrelSync Host+Client e build player.

---

## 2. Testes NUnit EditMode Adicionados (Pendente de Execução no Editor)
Arquivo: `ScaryParty/Assets/Piaaria/Tests/Editor/OvernightExperienceTests.cs`
1. `NightCycle_VictoryAndOperationalDefeat_UseOnlyNightEarnedRevenueAndRestoreSnapshotOnRetry` — confirma que derrota operacional compara apenas `NightEarnedRevenue` com `NightRevenueGoal` (ignorando `RestaurantBudget`) e que `RetryCurrentNight` restaura snapshot de orçamento, estoque, upgrades e integridade de mochilas.
2. `OrderTaskBoard_ClaimToggleAndStages_TransitionThroughPrepReadyAndDelivery` — valida assunção/liberação de comanda (inclusive `clientId == 0` do Host) e estágios `New -> InPrep -> Ready -> OutForDelivery -> Completed`.
3. `BackpackService_DamageRuptureDropsAtMostOneLastBox_BlocksStoreAllowsRetrieveAndRepairs` — valida desgaste parcial, ruptura com queda de **no máximo 1 caixa** no mundo (`LocationType.World`), bloqueio de depósito enquanto rasgada, permissão de retirada e reparo para `100%`.
4. `TransferService_EnforceRequesterOwnership_AllowsHostClientZeroAndBlocksOtherClient` — confirma que `enforceRequesterOwnership` aceita o Host (`clientId == 0`) e bloqueia outro cliente (`clientId == 1`).

Existente atualizado: `ScaryParty/Assets/Piaaria/Scripts/City/Editor/Tests/ModularCityTests.cs` (porta dupla `J = 4m / 2 slots` e ocupação por intervalo).

---

## 3. Roteiro Passo a Passo no Unity Editor Aberto (`L2`)

1. **Refresh de Assets & `.meta`:**
   - Foque a janela do **Unity Editor** para importar os novos scripts e gerar automaticamente os arquivos `.meta` pendentes. Confira se a Console está livre de erros de compilação.
2. **Test Runner (EditMode):**
   - Abra `Window > General > Test Runner > EditMode` e execute `OvernightExperienceTests`, `PizzeriaDomainTests` e `ModularCityTests`.
3. **Checkpoint Visual na Cena Binária (`GameScene.unity`):**
   - Abra `LobbyScene` ou `ReadyScene` -> clique em **Iniciar** como cliente desconectado e como **Host**; valide que apenas o servidor inicia a transição online para `GameScene.unity` via coordenador de loading.
   - Entre na pizzaria em PlayMode: verifique barras de progresso (`StationProgressBar`), mesas/cadeiras (`DiningTableStation`), posicionamento de NPCs e animação discreta de sentar nas cadeiras (`CustomerNpcView`). Registre caso algum override local na `GameScene.unity` binária exija ajuste no Inspector.
   - Verifique fachadas modulares da cidade (`J` ocupando 2 slots = `4m`, sem sobreposição nem buracos anômalos).
4. **Ciclo de Noites, Quadro de Comandas, Pausa e Tutorial:**
   - Valide no HUD o timer/meta da Noite 1 (`360s`, `R$ 60`), assuma/libere uma comanda no quadro (`O7`), verifique dicas contextuais (`O8`) e abra o menu de pausa (`ESC`, `O3`/`O20`) para testar **Recuperar Personagem** e **Recuperar Moto**.
5. **Combate Base & Mochila Danificável (Pátio Externo):**
   - No pátio lateral gerado por `PizzeriaParkingLotBuilder`, pegue a **Pistola** (mão direita ocupada, mão esquerda `[1]` livre), dispare contra o `PracticeTargetDummy`, recarregue (`[R]`) e teste a **Escopeta** (`2 mãos`, `8` projéteis x `10`).
   - Interaja `[E]` com o dummy para receber `25` de dano, validar queda no HP (`0 HP` -> auto-resgate após `10s`, sem encerrar a noite) e ruptura da mochila (`1` última caixa cai no chão com `[E]` para recolher, bloqueio de guarda até reparar com `[F]` segurando `3s`).
6. **Moto de Entregas, Buzina (`O13`) & Trânsito (`A10–A13`):**
   - Com **mãos vazias** e **sem arma equipada** (com ou sem mochila nas costas), monte na moto (`[E]`), dirija pelas ruas (`[WASD]` + `[Espaço]`), teste contenção no meio-fio/calçada, pressione `[H]` para buzinar (sem abrir livro de receitas enquanto pilota) e observe pedestres dando passo lateral.
   - Observe veículos de trânsito parando nas stop-lines corretas por grupo (N/S vs L/O) e veículos replanejando rotas sem atravessar quarteirões.
   - Em **ParrelSync** (Host + Client), repita montaria/pose da moto, posse de itens do Host (`clientId == 0`) vs Client e sincronização inicial para *late-joiner*.

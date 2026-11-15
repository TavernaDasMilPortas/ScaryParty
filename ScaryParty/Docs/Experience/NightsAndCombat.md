# Ciclo de Noites, Experiência Aprovada, Combate Base, Mochila e Moto

Este documento resume as funcionalidades aprovadas (`O1`, `O2`, `O3`, `O5`, `O7`, `O8`, `O13`, `O20`), o **Ciclo de Noites**, a **Fundação de Combate + Mochila Danificável (12B)** e a **Moto de Entregas (`MOTO`)**.

> **Escopo respeitado:** Permanecem explicitamente fora da implementação as opções `O4`, `O6`, `O9`, `O10`, `O11`, `O12`, `O14`, `O15` (qualidade da pizza por colisão), `O16`, `O17`, `O18` e `O19`.

---

## 1. Ciclo de Noites e Derrota Operacional (`O5`)
- **Domínio autoritativo (`NightCycleService.cs`, `PizzeriaState.cs`, `NightConfig.cs`):**
  - Progressão padrão: **Noite 1** (`360s`, meta `R$ 60`), **Noite 2** (`390s`, meta `R$ 110`), **Noite 3** (`420s`, meta `R$ 170`) e escalonamento contínuo.
  - Estados: `Preparing` -> `Active` -> `Closing` (grace para comandas abertas) -> `Results` (vitória) ou `Defeat` (derrota operacional).
  - **Regra canônica de derrota operacional:** avalia exclusivamente `NightEarnedRevenue >= NightRevenueGoal`, ignorando o saldo pré-existente (`RestaurantBudget`) da pizzaria.
  - **Retry (`RetryCurrentNightServerRpc`):** restaura o `NightStartSnapshot` capturado na entrada da noite (orçamento, estoque, níveis de upgrade e integridade `100` das mochilas) e reseta entidade transitórias, armas, combate e moto.
  - **Tela de Resultados (`NightResultsPresenter.cs`):** exibe resumo da noite e botões autoritativos ao Host para tentar novamente ou avançar.

---

## 2. Quadro de Equipe, HUD, Tutorial e Pausa (`O1/O2/O3/O7/O8/O20`)
- **Quadro de Comandas / Claim (`O7`):** `TicketSheetView.cs`, `TicketModalPresenter.cs` e `ToggleClaimOrderServerRpc` exibem etapas reais (`New -> InPrep -> Ready -> OutForDelivery -> Completed/Failed`) e permitem assumir/liberar comandas por jogador (inclusive `clientId == 0` do Host).
- **HUD, Acessibilidade e Dicas Contextuais (`O1/O2/O8`):** `HudPresenter.cs` e `ContextualTutorialPresenter.cs` exibem timer/fase/meta da noite, munição da arma equipada, barra de vida (`100 HP`) e dicas contextuais sem travar a tela (dano, mochila rasgada, direção da moto).
- **Menu de Pausa & Recuperação (`O3/O20`):** `PauseEscapeMenuPresenter.cs` concentra controles de pausa, ajustes rápidos e botões de recuperação (`RequestRecoverPlayerServerRpc` e `RequestRecoverMotorcycleServerRpc`) com cooldown de `60s` no servidor.

---

## 3. Fundação de Combate e Mochila Danificável (`12B`)
- **Rack Externo de Armas (`WeaponRackStation.cs`, `PizzeriaCommandHandler.cs`):**
  - **Pistola (`WeaponType.Pistol`):** 1 mão (`Right` ocupada, `Left [1]` livre para caixas/ingredientes/moto sem ocupar), pente `12`, reserva `36`, recarga `[R]`, `25` dmg por disparo (`65m`).
  - **Escopeta (`WeaponType.Shotgun`):** 2 mãos (exige ambas as mãos livres), pente `4`, reserva `16`, `8` projéteis de `10` dmg (`80` dmg máximo, `38m`).
  - Bloqueio autoritativo de coleta de itens/ferramentas/mochila enquanto as mãos estiverem ocupadas por arma.
- **Vida e Resgate (`PlayerHealth.cs`, `CombatContracts.cs`):**
  - `100 HP` iniciais por jogador, `enableAllPlayersDeadDefeat = false` (morte de jogadores **não** encerra a noite), estado incapacitado com auto-resgate no servidor após `10s`.
  - Removido qualquer `OnGUI()` de mira legado em `WeaponController.cs`.
- **Alvo de Prática (`PracticeTargetDummy.cs`):**
  - Recebe disparos via `IDamageable` com feedback visual e reset automático após `2.5s`; interação `[E]` solicita `RequestTestPlayerDamageServerRpc(25)` ao servidor para testar HUD de HP e desgaste/ruptura de mochila com segurança.
- **Mochila Danificável (`StackBackpackService.cs`, `BackpackController.cs`, `BackpackVisualizer.cs`, `BackpackPanelPresenter.cs`, `PizzeriaItemVisualizer.cs`):**
  - Integridade inicial `100`; ao chegar a `0` (`IsTorn = true`), derruba **no máximo 1 caixa** (a última inserida) no mundo (`LocationType.World`) e fica bloqueada apenas para guardar novas caixas. Retirar caixas restantes continua permitido.
  - Dano adicional já rasgada não derruba mais caixas.
  - Caixas derrubadas são renderizadas no mundo com `BoxCollider` e recolhíveis via `[E]`.
  - **Reparo (`[F]` segurando `3.0s` ou botão no painel da mochila -> `RepairBackpackServerRpc`):** restaura `Integrity = 100` e reabilita armazenamento.

---

## 4. Estacionamento Lateral, Moto de Entregas e Buzina (`MOTO` + `O13`)
- **Construção Runtime (`PizzeriaParkingLotBuilder.cs`, `PizzeriaRoot.cs`):** instancia pátio asfáltico lateral (escolhendo lado livre de colisão externa), `DeliveryMotorcycleController`, `WeaponRackStation` e `PracticeTargetDummy` no `OnNetworkSpawn`, herdando cel-shading via `VisualStyleManager`.
- **Moto Autorizada por Rede (`DeliveryMotorcycleController.cs`, `NetMotorcycleDto`, `PizzeriaNetworkState.cs`, `PizzeriaCommandHandler.cs`):**
  - Montar (`[E]` -> `ToggleMotorcycleMountServerRpc`): exige proximidade (`<= 4.5m`), jogador não incapacitado, nenhuma arma equipada no rack/mãos e **ambas as mãos livres** (mochila equipada é permitida). Desmontar coloca o jogador em piso seguro ao lado da moto.
  - Circulação restrita à área do estacionamento e grafo de ruas (`ClampToAllowedSurface` com curb-slide tanto no cliente quanto no servidor autoritativo).
  - Limpa automaticamente montaria ao desconectar jogador (`ReleasePlayerSessions`) ou reiniciar/avançar noite (`HandleResetNightCombatServer`).
- **Buzina (`O13`, `[H]`):**
  - `RecipeBookPresenter.cs` ignora `[H]` enquanto `WeaponController.LocalDrivingMotorcycle` estiver ativo.
  - `HonkMotorcycleServerRpc` aplica cooldown de `1.5s` no servidor, incrementa `HornCounter` (pulso visual nos clientes) e aciona `PedestrianNpc.ReactToHorn` num raio de `16m`.

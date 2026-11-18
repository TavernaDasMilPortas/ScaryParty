# Estado das fases

Status atualizado após conclusão das fases **P0–P7** (`Docs/Release/Reports/PLAYTEST_UI_PLACEHOLDERS.md`) na branch `release/marcos-noturnos` a partir de `bdffe509`:
- **Build `.NET` (`ScaryParty.Pizzeria.Tests.csproj`):** `0` erros, `0` avisos.
- **Unity Test Runner EditMode (Suíte Segura):** `102/102 Passed` (`Docs/Release/Reports/Evidence/Unity_EditMode_Suite_Summary.json` + XML NUnit3 local).
- **Unity Test Runner PlayMode (`PlaytestIntegrationPlayModeTests`):** `6/6 Passed` em `503.73s` (`Docs/Release/Reports/Evidence/Unity_PlayMode_Summary.json` + XML NUnit3 local).
- **Visual GameView (`1920x1080` e `1280x720`):** `LobbyScene` + `GameScene` (HUD, Central `[J]`, Diagnóstico `[F8]`) capturadas e inspecionadas sem shaders quebrados; `ValidateAll` pós-PlayMode com `0` erros/warnings.
- **Multiplayer em 2 processos reais (`Host + Cliente` separados):** **NÃO EXECUTADO** (`com.unity.multiplayer.playmode@2.0.2` com `0` cenários/clones virtuais; bloqueio de lock `Library/` para 2ª instância).

| Marco | Status | Relatório |
|---|---|---|
| M1 — Auditoria | IMPLEMENTAÇÃO INTEGRADA — VALIDAÇÃO EDITOR/PLAYMODE PENDENTE | [M1_Relatorio](Reports/M1_Relatorio.md) |
| M2 — Cidade Navegacao | IMPLEMENTAÇÃO INTEGRADA — VALIDAÇÃO EDITOR/PLAYMODE PENDENTE | [M2_Relatorio](Reports/M2_Relatorio.md) |
| M3 — Maos Armas Moto | IMPLEMENTAÇÃO INTEGRADA — VALIDAÇÃO EDITOR/PLAYMODE PENDENTE | [M3_Relatorio](Reports/M3_Relatorio.md) |
| M4 — Noite Completa | IMPLEMENTAÇÃO INTEGRADA — VALIDAÇÃO EDITOR/PLAYMODE PENDENTE | [M4_Relatorio](Reports/M4_Relatorio.md) |
| M5 — Monstros | IMPLEMENTAÇÃO INTEGRADA — VALIDAÇÃO EDITOR/PLAYMODE PENDENTE | [M5_Relatorio](Reports/M5_Relatorio.md) |
| M6 — Run Votos Loja | IMPLEMENTAÇÃO INTEGRADA — VALIDAÇÃO EDITOR/PLAYMODE PENDENTE | [M6_Relatorio](Reports/M6_Relatorio.md) |
| M7 — Funcionarios | IMPLEMENTAÇÃO INTEGRADA — VALIDAÇÃO EDITOR/PLAYMODE PENDENTE | [M7_Relatorio](Reports/M7_Relatorio.md) |
| M8 — Entrega Final | IMPLEMENTAÇÃO INTEGRADA — VALIDAÇÃO FINAL MANUAL PENDENTE | [M8_Relatorio](Reports/M8_Relatorio.md) |

## Checkpoint ativo

- Fase: **P0–P7 concluídas** (correções, UI Toolkit navegável, placeholders 3D instalados, presets/diagnóstico `[F8]`, EditMode `102/102`, PlayMode `6/6` e 8 capturas GameView `1920x1080`/`1280x720`). Ver [PLAYTEST_UI_PLACEHOLDERS](Reports/PLAYTEST_UI_PLACEHOLDERS.md).
- Próxima ação: Execução do roteiro manual de playtest em `LobbyScene.unity` e testes em **2 processos reais (`Host + Cliente` via MPPM ou Build Standalone + Editor)** para `V02`, `V03`, `V10`–`V15` e substituição gradual dos placeholders pelos assets finais.
- Bloqueios: Multiplayer em 2 processos não pôde rodar em CLI (`0` cenários MPPM configurados + lock em `Library/`); alterações manuais de animators/prefabs finais e builds locais seguem sob demanda do usuário (`UNITY_EXECUTION=MANUAL_ONLY`).

Atualizar depois de blocos importantes e antes de encerrar uma sessão. Não avançar fases nem marcar marcos como concluídos com sistemas desconectados ou sem UI/prefab configurados.

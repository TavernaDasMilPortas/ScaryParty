# Estado das fases

Status atualizado após conclusão das etapas **R0–R8** da recuperação M1–M8 (`Docs/Release/RECUPERACAO_M1_M8.md`) na branch `release/marcos-noturnos`. O código e integrações dos marcos M1–M8 estão compilados (0 erros nos 4 `.csproj`) com **33/33** testes unitários puros de domínio aprovados em runner `.NET`; **nenhum** teste de Unity Editor (EditMode/PlayMode/ParrelSync/UI/Física/NavMesh) foi executado por política `UNITY_EXECUTION=MANUAL_ONLY`.

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

- Fase: Recuperação M1–M8 concluída em código/estática (**R0–R8**). Ver [RECUPERACAO_M1_M8](Reports/RECUPERACAO_M1_M8.md).
- Próxima ação: Execução manual pelo usuário no Unity Editor aberto (Console, Test Runner EditMode, `CharacterSetupWizard` e bateria PlayMode/ParrelSync T01–T60).
- Bloqueios: Validações EditMode/PlayMode/ParrelSync, authoring de prefabs/cena/Animator no Editor e build final dependem do usuário no Unity Editor aberto (`UNITY_EXECUTION=MANUAL_ONLY`).

Atualizar depois de blocos importantes e antes de encerrar uma sessão. Não avançar fases nem marcar marcos como concluídos com sistemas desconectados ou sem UI/prefab configurados.

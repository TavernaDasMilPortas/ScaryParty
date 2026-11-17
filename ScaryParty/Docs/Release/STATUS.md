# Estado das fases

Status reclassificado na etapa **R0** da recuperação M1–M8 (`Docs/Release/RECUPERACAO_M1_M8.md`): a classificação anterior marcava todos os marcos como concluídos apesar de existir código desconectado, UI/HUD ausentes e erros P0 de compilação. O status abaixo reflete apenas código compilado e integrado na branch `release/marcos-noturnos`; validações PlayMode/EditMode no Unity Editor continuam pendentes (`UNITY_EXECUTION=MANUAL_ONLY`).

| Marco | Status | Relatório |
|---|---|---|
| M1 — Auditoria | PARCIAL — INTEGRAÇÃO/VALIDAÇÃO PENDENTE | [M1_Relatorio](Reports/M1_Relatorio.md) |
| M2 — Cidade Navegacao | PARCIAL — INTEGRAÇÃO/VALIDAÇÃO PENDENTE | [M2_Relatorio](Reports/M2_Relatorio.md) |
| M3 — Maos Armas Moto | PARCIAL — INTEGRAÇÃO/VALIDAÇÃO PENDENTE | [M3_Relatorio](Reports/M3_Relatorio.md) |
| M4 — Noite Completa | INTEGRAÇÃO INCOMPLETA | [M4_Relatorio](Reports/M4_Relatorio.md) |
| M5 — Monstros | INTEGRAÇÃO INCOMPLETA | [M5_Relatorio](Reports/M5_Relatorio.md) |
| M6 — Run Votos Loja | INTEGRAÇÃO INCOMPLETA | [M6_Relatorio](Reports/M6_Relatorio.md) |
| M7 — Funcionarios | INTEGRAÇÃO INCOMPLETA | [M7_Relatorio](Reports/M7_Relatorio.md) |
| M8 — Entrega Final | NÃO CONCLUÍDA | [M8_Relatorio](Reports/M8_Relatorio.md) |

## Checkpoint ativo

- Fase: Recuperação M1–M8 em andamento — Etapa **R0 concluída** (compilação P0 restaurada e status reclassificado). Ver [RECUPERACAO_M1_M8](Reports/RECUPERACAO_M1_M8.md).
- Próxima ação: Executar etapas **R1–R7** de integração funcional e depois **R8** (verificação final + checklist para execução manual no Unity Editor aberto).
- Bloqueios: Validações EditMode/PlayMode/ParrelSync, authoring de prefabs/cena/Animator no Editor e build final dependem do usuário no Unity Editor aberto (`UNITY_EXECUTION=MANUAL_ONLY`).

Atualizar depois de blocos importantes e antes de encerrar uma sessão. Não avançar fases nem marcar marcos como concluídos com sistemas desconectados ou sem UI/prefab configurados.

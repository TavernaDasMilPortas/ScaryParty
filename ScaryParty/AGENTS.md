# ScaryParty — Diretrizes de Agente (Subdiretório Unity)

Este diretório é a raiz do projeto Unity.
- O contrato principal do agente e as restrições de execução headless estão definidos em `../AGENTS.md`.
- Para indexação da documentação, consulte `Docs/INDEX.md` e `Docs/CURRENT_STATE.md`.
- Política estrita: `UNITY_EXECUTION=MANUAL_ONLY`. Nunca execute Unity em batchmode/headless sem autorização explícita na tarefa.
- Validações devem ser orientadas para execução no Unity Editor aberto pelo desenvolvedor.

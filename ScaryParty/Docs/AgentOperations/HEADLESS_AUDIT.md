# Auditoria Estática de Execuções Unity Headless / Batchmode

**Data:** 14/11/2026  
**Projeto:** `ScaryParty` (Unity 6000.3.6f1)  
**Status da Política:** `UNITY_EXECUTION=MANUAL_ONLY` (Ativa)

---

## 1. Contexto e Motivação
Em versões anteriores do desenvolvimento, observou-se uso de chamadas de linha de comando (`Unity.exe -batchmode -quit -executeMethod ...`) registradas no repositório (`UnityBatchLog.txt`).  
O objetivo desta auditoria é identificar todas as potenciais origens de invocação não supervisionada do motor Unity e garantir que nenhuma ferramenta, script ou agente inicie a Unity em background/headless sem consentimento explícito por tarefa.

---

## 2. Inventário de Invocadores e Gatilhos

| Origem / Caminho | Gatilho Identificado | Classificação | Custo Percebido | Ação Aplicada | Estado Atual |
|---|---|---|---|---|---|
| `UnityBatchLog.txt` (raiz) | Execução manual prévia via CLI (`FirstPersonInteractionSetupBuilder.ApplyFromCommandLine`) em 26/10/2026 | (a) Histórico / manual | Alto (carregamento completo da Library no batch) | Preservado como histórico; ignorado para futuros commits via `.gitignore` | Inativo |
| Plugin `unity` (Antigravity Global `0.1.9-beta`) | Skills do plugin Unity (`unity-cli`, `migrate-birp-to-urp`, etc.) | (e) Agente/Skill externa | Muito Alto se invocado em loop ou via headless | Política `UNITY_EXECUTION=MANUAL_ONLY` em `AGENTS.md` proíbe auto-invocação | Controlado por Regra |
| `.vscode/tasks.json` | Nenhum arquivo de tarefas automáticas configurado | (c) Tarefa IDE | Zero | Nenhuma ação necessária | Inexistente |
| `.github/workflows/` | Nenhum workflow de CI com Unity configurado | (d) Pipeline CI | Zero | Nenhuma ação necessária | Inexistente |
| Scripts `*.ps1`, `*.bat`, `*.cmd` | Nenhum script automático de execução em background detectado | (b) Ferramenta manual | Zero | Criado `Tools/AgentChecks/quick-check.ps1` puramente estático | Seguro / Manual |
| Documentos `Docs/Overnight/*.md` | Relatórios de metas noturnas anteriores mencionando validações | (a) Histórico | Baixo | Indexados para consulta sem reescrita | Documental |

---

## 3. Classificação das Chamadas (Legenda do Plano)
- **(a) Comando histórico:** Execução passada isolada registrada em logs (`UnityBatchLog.txt`).
- **(b) Ferramenta manual:** Scripts utilitários acionados deliberadamente pelo desenvolvedor no terminal.
- **(c) Tarefa IDE automática:** Não há tarefas configuradas no `.vscode`.
- **(d) Pipeline CI:** Sem pipelines no momento.
- **(e) Agente/Skill que chama Unity:** Habilidades fornecidas pelo ecossistema global do assistente. Restritas categoricamente pelo contrato do agente.
- **(f) Servidor de multiplayer intencional:** Executável dedicado de servidor se necessário para testes futuros; atualmente os testes multiplayer são realizados com ParrelSync / Editor local.

---

## 4. Política Efetiva
1. **Regra Absoluta:** O assistente NUNCA deve chamar `Unity.exe`, `-batchmode`, `-nographics`, `-runTests`, `-executeMethod`, builds ou testes em lote sem que o usuário requisite especificamente na mensagem da tarefa corrente.
2. **Editor Concorrente:** A Unity não permite duas instâncias acessando a mesma pasta de projeto (`Library/`). Se o Editor estiver aberto, qualquer tentativa de batch falhará ou corromperá o lock da base de dados.
3. **Preferência de Validação:** Sempre fornecer instruções para testes pelo **Unity Editor** já aberto (EditMode/PlayMode e Console), ou executar verificações estáticas leves locais.

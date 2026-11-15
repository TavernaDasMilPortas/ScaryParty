---
name: debug-unity-logs
description: "Diagnostica erros em logs do Unity Editor ou de compilação C#, extraindo apenas a causa raiz e linhas essenciais sem despejar saídas gigantes."
---

# Skill: debug-unity-logs

Use esta habilidade quando o usuário fornecer um arquivo de log ou colar trechos de erro do Console da Unity.

## Protocolo de Diagnóstico

1. **Extração Seletiva:**
   - NUNCA leia arquivos de log gigantes na totalidade.
   - Use comandos com filtros para isolar linhas com `Exception`, `Error`, `NullReferenceException` ou `CS\d{4}`.
   - Limite a captura inicial às primeiras 30–60 linhas da primeira causa raiz.

2. **Deduplicação de Stacktraces:**
   - Muitas vezes a Unity repete a mesma exceção a cada frame no `Update()`. Identifique a primeira ocorrência e descarte as repetições idênticas subsequentes.
   - Extraia o arquivo C# e a linha exata indicados no topo do stacktrace.

3. **Proibição de Reexecução Automática:**
   - NUNCA execute `Unity.exe` ou `-batchmode` para reproduzir o erro ou gerar novos logs de depuração.
   - Se for necessário inspecionar o código, abra apenas a classe e o método citados no erro.

4. **Regra de 1 Tentativa de Auto-Correção:**
   - Apresente ao usuário a explicação clara da falha.
   - Se a solução for evidente, proponha a edição do arquivo com impacto cirúrgico.
   - Se a alteração não resolver, pare imediatamente e solicite a orientação do desenvolvedor.

# Observabilidade de Cota e Linha de Base de Custos

**Projeto:** `ScaryParty`  
**Última atualização:** 14/11/2026

---

## 1. Princípios de Economia de Tokens e Cota
O consumo excessivo de recursos em tarefas assistidas por IA não é causado pela execução da Unity em si, mas sim por:
1. **Leitura indiscriminada de contexto:** Injeção repetitiva de arquivos inteiros (>30 KB) ou diretórios extensos a cada turno.
2. **Logs gigantes:** Despejar milhares de linhas de stdout/stderr de builds ou testes no contexto do modelo.
3. **Loops de auto-reparo:** Tentativas repetidas de corrigir erros de compilação sem intervenção humana após a primeira falha.
4. **Skills e regras estáticas pesadas:** Regras `always_on` que ocupam milhares de tokens permanentemente.

---

## 2. Tabela de Amostragem de Custos

Esta tabela deve ser alimentada manualmente com dados observáveis reais da interface (sem estimativas inventadas):

| Data | Tarefa Realizada | Modelo / Ferramenta | Invocou Unity Headless? | Linhas de Log no Chat | Chamadas de Ferramenta | Tokens / Créditos Reportados | Observações |
|---|---|---|---|---|---|---|---|
| 2026-11-14 | Implementação do plano Antigravity | Flash / Claude / Pro | NÃO | < 60 linhas por comando | ~25 chamadas | Conforme painel local | Criação de regras, skills e docs |

---

## 3. Protocolo para Manter Baixo Consumo
1. **Comandos de Terminal:** Limitar saídas com filtros (`Select-Object -First 20`, `grep`, `Select-String`).
2. **Logs de Erro:** Em caso de exceção no Unity Console ou compilação, recortar apenas as 10-30 primeiras linhas relevantes (stacktrace raiz).
3. **Regra de 1 Tentativa:** Se uma correção automática falhar, parar imediatamente, relatar o diagnóstico e pedir orientação.
4. **Skills sob Demanda:** Carregar instruções detalhadas apenas quando o domínio específico for acionado.

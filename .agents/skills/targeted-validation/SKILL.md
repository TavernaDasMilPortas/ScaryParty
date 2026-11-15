---
name: targeted-validation
description: "Aplica validação em camadas (L0 a L3) para alterações no ScaryParty sem acionar Unity em headless ou batchmode por conta própria."
---

# Skill: targeted-validation

Use esta habilidade para definir e executar a estratégia de validação adequada para qualquer modificação no projeto, garantindo economia de tokens e evitando chamadas automáticas à Unity.

## Matriz de Validação em Camadas

| Nível | Escopo | Ferramenta / Ação | Unity.exe Headless? |
|---|---|---|---|
| **L0** | Qualquer alteração | Revisão estática de diff, `git diff --check`, checagem de integridade de `.meta` | **NÃO** |
| **L1** | Código C# / Lógica | Verificação estática de tipos, checagem de referências a `UnityEditor`, scripts leves (`quick-check.ps1`) | **NÃO** |
| **L2** | Integração Unity / Cena / UI | Fornecer instruções objetivas de teste manual para o usuário executar no **Unity Editor já aberto** | **NÃO** |
| **L3** | Testes completos CLI / Build | Test Runner via CLI, compilação de pacotes ou exportação de build | **SIM, somente sob aprovação explícita para esta tarefa** |

## Regras Obrigatórias

1. **Padrão Econômico (L0 + L1 + L2):**
   - O agente NUNCA deve escalar para L3 por iniciativa própria.
   - Mesmo após erros de compilação apontados pelo usuário, não tente rodar `Unity.exe -batchmode` para testar se corrigiu.
2. **Relatório Honesto:**
   - Ao finalizar a tarefa, indique explicitamente o nível de validação aplicado:
     - L0/L1: "Verificação estática realizada com sucesso."
     - L2: "Instruções de teste manual no Editor fornecidas abaixo."
     - L3: "Não executado (aguarda aprovação explícita caso desejado pelo usuário)."

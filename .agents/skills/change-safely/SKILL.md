---
name: change-safely
description: "Executa plano de alteração com impacto mínimo no código ou assets do ScaryParty, garantindo preservação de .meta, referências e estratégia de rollback."
---

# Skill: change-safely

Use esta habilidade ao planejar e aplicar alterações de código ou assets no ScaryParty para evitar efeitos colaterais em prefabs, cenas e serialização.

## Protocolo de Execução

1. **Inspeção de Estado Inicial:**
   - Execute `git status --short` no escopo da tarefa para identificar modificações pendentes do usuário.
   - NUNCA reverta ou sobrescreva arquivos modificados pelo usuário sem confirmação.

2. **Mapeamento de Impacto:**
   - Identifique os arquivos estritamente necessários para a alteração.
   - Verifique dependências públicas, campos `[SerializeField]` e conexões com o Unity Editor.
   - Se um asset for criado ou movido, assegure a integridade do `.meta` correspondente.

3. **Aplicação Cirúrgica:**
   - Aplique apenas as alterações descritas no plano.
   - Não refatore módulos vizinhos fora do escopo.
   - Não reescreva arquivos inteiros se apenas poucas linhas precisarem de alteração.

4. **Verificação de Regressão Local:**
   - Execute `git diff --check` para verificar formatação, conflitos ou caracteres indesejados.
   - Valide se nenhuma referência a `UnityEditor` vazou para código de runtime.

5. **Rollback Preparado:**
   - Em caso de inconsistência irreparável ou incompatibilidade de assets, utilize `git checkout -- <arquivo>` exclusivamente nos arquivos tocados por esta tarefa.

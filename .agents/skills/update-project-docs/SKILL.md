---
name: update-project-docs
description: "Atualiza a documentação de estado (CURRENT_STATE.md, CHANGELOG.md, ADRs) de forma concisa e idempotente, sem inflar o contexto."
---

# Skill: update-project-docs

Use esta habilidade ao concluir alterações estruturais ou implementações relevantes no ScaryParty para registrar o novo estado do projeto sem duplicar informações existentes.

## Diretrizes de Atualização

1. **Leitura Focalizada:**
   - Abra apenas os arquivos de documentação diretamente relacionados à alteração (`ScaryParty/Docs/CURRENT_STATE.md`, `CHANGELOG.md` e o documento específico do módulo se necessário).
   - Não leia documentos históricos extensos na íntegra.

2. **Atualização do `CURRENT_STATE.md`:**
   - Mantenha o arquivo com no máximo ~150–200 linhas.
   - Atualize a seção correspondente ao módulo tocado (`City`, `Navigation`, `Pizzeria`, `Network`, `Player`, `UI`).
   - Indique claramente o status: `Implementado`, `Parcial` ou `Pendente`.

3. **Registro no `CHANGELOG.md`:**
   - Formate uma entrada concisa de 3 a 8 linhas contendo:
     - Data e título da alteração;
     - Escopo e arquivos afetados;
     - Validação estática executada e validação manual pendente;
     - Riscos ou observações de compatibilidade.

4. **Decisões Estruturais (ADR):**
   - Apenas crie um novo arquivo em `ScaryParty/Docs/Decisions/` (ex: `ADR-0002-...md`) se houver uma nova decisão arquitetural duradoura (ex: migração de sistema de física, autoridade de rede, pipeline de renderização).
   - Não crie ADRs para correções rotineiras de bugs.

# Registros de Alterações Estruturais (Changes)

Este diretório armazena planos detalhados de mudanças arquiteturais ou migrações complexas.  
**Não crie arquivos aqui para ajustes rotineiros ou correções de bugs simples** — use `CHANGELOG.md` para esses casos.

---

## Template de Mudança (`YYYY-MM-DD_assunto.md`)

```markdown
# Mudança: [Título Descritivo]

- **Data:** AAAA-MM-DD
- **Autor / Agente:**
- **Status:** Proposto / Concluído / Revertido

## 1. Problema e Motivação
[Por que esta alteração é necessária?]

## 2. Arquivos e Componentes Afetados
- `Caminho/Para/Arquivo1.cs`
- `Caminho/Para/Prefab.prefab`

## 3. Decisão e Abordagem Adotada
[O que foi alterado e como?]

## 4. Dependências e Riscos
- [ ] Preservação de GUIDs em arquivos .meta
- [ ] Impacto em Netcode / Sincronização multiplayer
- [ ] Preservação de serialização no Inspector

## 5. Validação Realizada
- [x] L0/L1: Verificações estáticas
- [ ] L2: Teste manual no Unity Editor

## 6. Procedimento de Rollback
[Como reverter rapidamente caso ocorra regressão]
```

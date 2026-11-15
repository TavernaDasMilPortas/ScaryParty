# Índice Geral de Documentação — ScaryParty

Este índice reúne a documentação técnica do projeto com links relativos e descrições de uma linha.  
**Instrução para agentes e desenvolvedores:** consulte este sumário e abra apenas os documentos necessários para a tarefa em andamento.

---

## 1. Documentos da Raiz do Repositório
- [README.md](../../README.md) — Visão geral do repositório ScaryParty.
- [PizzeriaDocumentation.md](../../PizzeriaDocumentation.md) — Documentação técnica abrangente do sistema da pizzaria, bancadas e ciclo de pedidos.
- [MapGenerationDocumentation.md](../../MapGenerationDocumentation.md) — Especificação detalhada da geração procedural do mapa e malhas urbanas.
- [RELATORIO_IMPLEMENTACAO_PERSONAGENS_PIZZAS_CLIENTES.md](../../RELATORIO_IMPLEMENTACAO_PERSONAGENS_PIZZAS_CLIENTES.md) — Histórico de implementação de NPCs, pizzas e clientes.
- [Plano_Antigravity_Unity_ScaryParty.md](../../Plano_Antigravity_Unity_ScaryParty.md) — Plano de engenharia de contexto, regras e proibição de headless.

---

## 2. Documentação Técnica dos Módulos (`ScaryParty/Docs/`)
- [AnimationSystem.md](AnimationSystem.md) — Sistema de animação de personagens, transições e sincronização de rede.
- [CityGeneration.md](CityGeneration.md) — Arquitetura de geração da cidade procedural, lotes, ruas e determinismo por seed.
- [PizzeriaArchitecture.md](PizzeriaArchitecture.md) — Arquitetura dos componentes da pizzaria, fluxo de estado e dependências.
- [PizzeriaImplementationPlan.md](PizzeriaImplementationPlan.md) — Plano de implementação original das mecânicas da pizzaria.
- [PizzeriaSetup.md](PizzeriaSetup.md) — Guia de configuração de cena, prefabs e referências da pizzaria no Unity Editor.
- [PizzeriaValidation.md](PizzeriaValidation.md) — Critérios de validação e testes para montagem de pizzas e pedidos.

---

## 3. Submódulo Pizzaria (`ScaryParty/Docs/Pizzeria/`) & Game Feel (`ScaryParty/Docs/GameFeel/`)
- [Auditoria.md](Pizzeria/Auditoria.md) — Auditoria estática de componentes, referências e conformidade com NGO.
- [Checklist.md](Pizzeria/Checklist.md) — Lista de checagem para validação manual de bancadas e interação no Editor.
- [Decisoes.md](Pizzeria/Decisoes.md) — Registro de decisões técnicas tomadas durante o desenvolvimento da pizzaria.
- [Guia.md](Pizzeria/Guia.md) — Guia prático de desenvolvimento e extensão de receitas e ingredientes.
- [GameFeel/README.md](GameFeel/README.md) — Arquitetura do sistema de Game Feel, pivôs cosméticos, acessibilidade e presets.
- [GameFeel/EventCatalog.md](GameFeel/EventCatalog.md) — Catálogo de `GameFeelEventId` e famílias de materiais.
- [GameFeel/BindingAudit.md](GameFeel/BindingAudit.md) — Auditoria de acoplamento aos scripts da pizzaria, NPCs e UI Toolkit.
- [GameFeel/Validation.md](GameFeel/Validation.md) — Testes NUnit EditMode e checklist de validação manual no PlayMode.

---

## 4. Governança do Agente e Operações (`ScaryParty/Docs/AgentOperations/`)
- [HEADLESS_AUDIT.md](AgentOperations/HEADLESS_AUDIT.md) — Auditoria estática de invocadores e política `UNITY_EXECUTION=MANUAL_ONLY`.
- [COST_BASELINE.md](AgentOperations/COST_BASELINE.md) — Matriz de observabilidade de cota, tokens e ferramentas.
- [LAST_HANDOFF.md](AgentOperations/LAST_HANDOFF.md) — Registro de contexto e estado da última sessão de desenvolvimento.

---

## 5. Arquitetura, Decisões e Histórico de Mudanças
- [CURRENT_STATE.md](CURRENT_STATE.md) — Estado corrente resumido do projeto (módulos, pendências e riscos).
- [CHANGELOG.md](CHANGELOG.md) — Histórico de mudanças relevantes agrupadas por data/versão.
- [Decisions/](Decisions/) — Registros de Decisões Arquiteturais (ADRs).
  - [ADR-0001](Decisions/ADR-0001-politica-execucao-manual-e-agentes.md) — Política de execução manual da Unity e regras de contexto.
- [Changes/](Changes/) — Modelos e relatórios de mudanças arquiteturais profundas.
- [Architecture/](Architecture/) — Visão sistêmica dos domínios do jogo.
- [Overnight/](Overnight/) — Relatórios históricos de auditorias e planos noturnos anteriores.

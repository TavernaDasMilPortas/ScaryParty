# ScaryParty — Contrato Mínimo do Agente

- **Escopo e Versão:** Projeto Unity em `ScaryParty/`; versão Unity 6000.3.6f1. Scripts e assets principais em `ScaryParty/Assets/Piaaria/`.
- **Ponto de Partida Obrigatório:** Antes de tarefas complexas, consulte `ScaryParty/Docs/INDEX.md` e `ScaryParty/Docs/CURRENT_STATE.md`. Leia apenas seções e sumários pertinentes à tarefa.
- **Respeito ao Estado Local:** Sempre verifique `git status --short`. Nunca descarte, reverta ou sobrescreva modificações pré-existentes do usuário sem consentimento expresso.
- **Leitura Econômica:** Faça buscas filtradas por caminho/símbolo antes de abrir arquivos completos. Não despeje arquivos inteiros (>20 KB) no contexto. Limite saídas de terminal a trechos curtos.
- **Política de Execução Headless (`UNITY_EXECUTION=MANUAL_ONLY`):**
  - NUNCA execute `Unity.exe`, Unity Hub, `-batchmode`, `-nographics`, `-runTests`, `-executeMethod`, builds locais, headless browser, servidores dedicados ou agentes em loop sem solicitação explícita para a tarefa atual.
  - Para validação de cena, física, renderização, UI Toolkit, áudio ou Netcode, oriente o usuário a testar no **Unity Editor já aberto**.
- **Preservação de Integridade:**
  - NUNCA apague ou recrie arquivos `.meta`. Preserve rigorosamente os GUIDs de assets existentes.
  - Não edite arquivos YAML serializados (`.unity`, `.prefab`, `.asset`) diretamente sem plano seguro. Preserve overrides de prefabs e campos serializados no Inspector.
  - Não altere assinaturas públicas ou serialização de rede sem verificar impacto no Netcode for GameObjects (NGO).
- **Validação em Camadas:**
  1. *L0/L1:* Revisão estática de diff local (`git diff --check`, syntax check, consistência de `.meta`).
  2. *L2:* Instruções passo a passo para teste manual no Unity Editor / PlayMode.
  3. *L3:* Validações CLI/Headless e builds APENAS se o usuário autorizar explicitamente aquela execução específica.
- **Honestidade Operacional:** Nunca declare que um teste foi executado se ele não rodou. Relate claramente `não executado` e detalhe os passos manuais necessários.
- **Prevenção de Loops:** Se uma alteração ou comando falhar, diagnostique a causa raiz. Faça no máximo UMA tentativa de correção automatizada; caso persista o erro, pare e solicite orientação ao usuário.
- **Manutenção Documental:** Após alterações relevantes, atualize de forma concisa `ScaryParty/Docs/CURRENT_STATE.md` e `ScaryParty/Docs/CHANGELOG.md`.
- **Entrega Padrão:** Ao concluir qualquer alteração, reporte:
  1. Arquivos alterados;
  2. Resumo da mudança de comportamento;
  3. Riscos avaliados;
  4. Validações estáticas executadas;
  5. Passo a passo para teste manual no Unity Editor;
  6. Pendências residuais.

# Plano de implementação — Antigravity econômico e confiável para Unity / ScaryParty

**Data:** 13/11/2026  
**Projeto:** `TavernaDasMilPortas/ScaryParty`  
**Objetivo:** reduzir desperdício de cota/tokens, proibir execuções headless não autorizadas, melhorar navegação no código e documentação, e aumentar qualidade de implementação sem quebrar o projeto.

> **Este é um plano para ser executado pelo Antigravity no checkout LOCAL do repositório.** Não trate exemplos de arquivo como alterações já realizadas. O agente deverá inspecionar e adaptar os caminhos e configurações conforme a instalação. O primeiro ciclo de implantação é estritamente documental/configuração: NÃO iniciar a Unity em batchmode para configurar estas regras.

## 0. Evidências e premissas já verificadas

Inspeção do `main` público do GitHub em 13/11/2026:

- Repositório: `https://github.com/TavernaDasMilPortas/ScaryParty`.
- **Raiz Git:** contém `README.md` (mínimo), `PizzeriaDocumentation.md` (~30 KB), `MapGenerationDocumentation.md`, `RELATORIO_IMPLEMENTACAO_PERSONAGENS_PIZZAS_CLIENTES.md` e `UnityBatchLog.txt` (~59 KB, versionado).
- **Raiz Unity:** `ScaryParty/`, portanto os scripts estão em `ScaryParty/Assets/Piaaria/Scripts/` e os documentos adicionais em `ScaryParty/Docs/`.
- Versão em `ScaryParty/ProjectSettings/ProjectVersion.txt`: **Unity 6000.3.6f1**.
- `UnityBatchLog.txt` contém execução anterior do Unity.exe com `-batchmode -quit -executeMethod ScaryParty.EditorScripts.FirstPersonInteractionSetupBuilder.ApplyFromCommandLine`. Isso comprova uso anterior de batch, **não** que exista um daemon/headless automático atualmente.
- O `.gitignore` da raiz **já** cobre `Library/`, `Temp/`, `Obj/`, `Build/`, `Builds/`, `Logs/`, `*.log`, `UserSettings/` e clones ParrelSync. Um `UnityBatchLog.txt` não é abrangido por `*.log`, e um arquivo já rastreado continua rastreado mesmo depois de adicionado ao ignore.
- `ScaryParty/.vscode/settings.json` tem muitos `files.exclude`, mas isso é principalmente apresentação no explorador; **não deve ser confundido com uma política garantida de contexto/indexação do agente**.
- Domínios de script atuais: `City`, `Gameplay`, `Interaction`, `Lobby`, `Navigation`, `Network`, `Pizzeria`, `Player`, `UI`, `Weapons`, além de `Editor`.
- Há código de teste em `ScaryParty/Assets/Piaaria/Tests/Editor` e `com.unity.test-framework`, `com.unity.netcode.gameobjects`, URP e navegação no manifesto. Preservar compatibilidade de Netcode e assets.

**Regra de segurança:** criar uma branch de trabalho e registrar estado inicial; não fazer limpeza, migração de assemblies, renomeações, reimports, builds, commits ou reescrita de assets em massa sem necessidade e autorização.

## 1. Prioridades e definição de pronto

1. **P0 — impedir headless automático:** nenhuma invocação da Unity por terminal, `-batchmode`, `-nographics`, `-runTests`, `-executeMethod`, build, teste PlayMode ou servidor dedicado sem pedido explícito do usuário **para aquela tarefa**.
2. **P0 — impedir loops caros:** no máximo uma tentativa automatizada de correção por erro diagnosticado; depois apresentar causa, arquivo e próximo passo. Nada de rodar múltiplos agentes/testes por conta própria.
3. **P1 — documentação navegável:** um índice simples aponta para arquivos existentes e um estado corrente curto, sem copiar 30 KB de documentação em toda sessão.
4. **P1 — regras e skills com carregamento progressivo:** regras mínimas e contínuas; instruções extensas apenas por domínio ou habilidade acionada.
5. **P1 — validação em camadas:** leitura e diff local primeiro; validação Unity dentro do Editor pelo usuário ou via comando explicitamente autorizado.
6. **P2 — manutenção e qualidade:** contrato de alterações, checagem de `.meta`, referências, multiplayer, determinismo procedural, UI Toolkit e testes focalizados.
7. **P2 — medir eficácia:** comparar número de chamadas headless, tamanho das saídas lidas pelo agente, quantidade de documentos consultados e saldo/cota antes/depois em tarefas comparáveis. **Não inventar % de economia.**

---

## 2. Fase P0: desativar headless e demais execuções caras por padrão

### 2.1 Auditoria exclusivamente estática (sem executar Unity)

- Determinar qual pasta está aberta como workspace (raiz do Git ou `ScaryParty/`); garantir a descoberta das regras a partir dela.
- Listar regras e skills atuais: `AGENTS.md`, `GEMINI.md`, `.agents/`, `.agent/`, `.gemini/` do workspace e, se o usuário autorizar, as globais da instalação. **Não sobrescrever regras preexistentes**: reconciliar conflito/duplicação e reportar precedência.
- Pesquisar com saída curta e caminhos limitados por chamadas a `Unity.exe`, `-batchmode`, `-nographics`, `-runTests`, `-executeMethod`, scripts `*.ps1`, `*.bat`, `*.cmd`, `.vscode/tasks.json`, configurações locais, GitHub Actions e instruções de documentação. Pesquisar primeiro nomes; abrir só achados relevantes. Não analisar `Library` e bibliotecas de terceiros indiscriminadamente.
- Classificar cada invocação como **(a)** comando histórico, **(b)** ferramenta manual explicitamente acionada, **(c)** tarefa IDE automática, **(d)** pipeline CI, **(e)** agente/skill que chama Unity, **(f)** servidor de multiplayer intencional.
- **Só desabilitar comportamento automático**. Não apagar ferramentas manuais úteis, scripts de CI legítimos, nem impedir que o jogo rode normalmente no Editor.
- Registrar `Docs/AgentOperations/HEADLESS_AUDIT.md` com caminho, origem, gatilho, custo percebido, ação e estado.

### 2.2 Política inegociável para o agente

- Padrão: **`UNITY_EXECUTION=MANUAL_ONLY`** (valor documental/política; **não** afirmar que é uma configuração nativa do Antigravity ou da Unity).
- Não abrir outra instância da Unity quando o projeto estiver aberto no Editor; a Unity não suporta duas instâncias simultâneas do mesmo projeto via batch.
- Não rodar automaticamente Unity.exe, Unity Hub, Test Runner, simulador multiplayer, compilação Unity batch, bake, geração de lightmaps, importação em massa, nem headless de browser ou um `agy -p` recursivo.
- Para teste visual, shader, material, prefab, Canvas 3D, mapa, animação, cena ou integração de Netcode: preparar alterações + checklist + instruções de teste manual no Editor, e parar.
- Para testar por terminal quando explicitamente solicitado: confirmar finalidade, modo, escopo/filtro, caminho da saída, custo operacional, Editor fechado e mecanismo de cancelamento. Uma execução por aprovação; repetir exige nova decisão. Não transmitir stdout integral ao contexto.
- Não inventar um booleano de configuração para desabilitar Unity headless: identificar qual **script/tarefa/ação** o invoca e desabilitar o gatilho correspondente.
- Se o usuário pedir **explicitamente** build/testes headless para CI, isso é uma exceção da tarefa, não uma alteração permanente desta política.

### 2.3 Proteção por configuração real do Antigravity no Windows

No aplicativo, o usuário deve selecionar **Settings → General → Global Permissions → Permission Preset → Request Review** ou, quando disponível, usar a configuração equivalente específica deste projeto em **Settings → Projects**. Versões antigas/Business podem exibir **Terminal Command Auto Execution → Require/Request Review**. Solicitar que o usuário confira manualmente, pois uma skill do repositório **não altera uma preferência global da interface de maneira confiável**.

- **Trade-off:** Request Review pergunta antes de **todo comando de terminal**, inclusive `git status`, o que pode incomodar. É a opção mais segura para impedir headless sem supervisão. Se preferir menos prompts depois, avaliar sandbox + regras nativas de permissionamento com *ask* somente para Unity, build, scripts de editor e automações de navegador; confirmar na interface a semântica atual.
- **Não** usar Turbo, Always Proceed ou `--dangerously-skip-permissions` neste projeto.
- Se houver permissões nativas por comando, configurar **Ask**, não **Deny permanente**, para Unity.exe/Unity Hub e invocadores auxiliares. Não presumir formato de JSON nem nome de chave sem verificar a versão real.
- Evitar auto-subagentes, auto-browser e servidores MCP ociosos que causem chamadas ou consumam contexto. Desligar só o que não for necessário.

### 2.4 Observabilidade de cota

Criar `Docs/AgentOperations/COST_BASELINE.md` com uma tabela pequena, preenchida a cada amostra **manual**: tarefa, data, ferramenta/modelo, comandos usados, execuções headless, tamanho aproximado dos logs inseridos no chat, chamadas de ferramentas e consumo de tokens/créditos **se a interface realmente mostrar**.

Separar:
- tokens consumidos por **o agente ler logs e produzir respostas**;
- CPU/RAM/tempo do **processo Unity headless em si**;
- custos de chamadas extras a modelos/subagentes/MCPs.

**Executar headless não gasta tokens por natureza; o desperdício vem, sobretudo, do ciclo de ferramentas, interpretação de saídas grandes e tentativas repetidas.**

---

## 3. Fase P1: sistema de regras leve + skills sob demanda

### 3.1 Estrutura proposta na raiz Git

```text
ScaryParty/                          ← raiz Git (nome da pasta do checkout)
├─ AGENTS.md                         ← 40–80 linhas, sempre ativo
├─ .agents/
│  ├─ rules/
│  │  ├─ unity-csharp.md             ← trigger: glob para C#
│  │  ├─ unity-assets.md             ← trigger: glob para cenas/prefabs/assets
│  │  ├─ unity-networking.md         ← trigger: model_decision
│  │  └─ unity-procedural-ui.md       ← trigger: model_decision
│  └─ skills/
│     ├─ change-safely/SKILL.md
│     ├─ targeted-validation/SKILL.md
│     ├─ update-project-docs/SKILL.md
│     └─ debug-unity-logs/SKILL.md
└─ ScaryParty/                       ← raiz do Unity Project
   └─ Docs/
      ├─ INDEX.md
      ├─ CURRENT_STATE.md
      ├─ CHANGELOG.md
      ├─ AgentOperations/
      │  ├─ HEADLESS_AUDIT.md
      │  └─ COST_BASELINE.md
      ├─ Architecture/
      ├─ Decisions/
      └─ Changes/
```

**Atenção:** se o workspace estiver aberto apenas em `ScaryParty/` (raiz Unity), verificar descoberta das regras na raiz Git, conforme a versão instalada. Se necessário, usar `ScaryParty/AGENTS.md` curto apontando ao índice, **sem duplicar conteúdo**. Não criar `.agents` dentro de `Assets/` para não gerar `.meta` de customizações e ruído no AssetDatabase.

**Não criar mais um `GEMINI.md` completo se já existir `AGENTS.md`**: evitar instruções sempre ativas duplicadas. Não copiar documentação longa para dentro de regras `always_on`.

### 3.2 Conteúdo sugerido para `AGENTS.md` (copiar/adaptar)

```md
# ScaryParty — contrato mínimo do agente

- Unity Project: `ScaryParty/`; Unity 6000.3.6f1; código principal em `ScaryParty/Assets/Piaaria/`.
- Comece com `ScaryParty/Docs/INDEX.md` e `CURRENT_STATE.md` (leia sumários; abra só seções ligadas à tarefa).
- Respeite alterações locais: confira `git status --short`; nunca sobrescreva modificações do usuário.
- Faça busca por caminho/símbolo antes de ler arquivos completos. Limite resultados e truncamento de logs.
- NÃO execute Unity.exe, Unity Hub, `-batchmode`, `-nographics`, `-runTests`, `-executeMethod`, build, headless browser, servidor dedicado ou agentes recursivos sem pedido explícito nesta tarefa. Preferir validação manual no Editor.
- NÃO instale pacotes, remova arquivos/`.meta`, formate em massa, regrave prefabs/cenas, rode migrações, faça commit/push ou refatore sistemas vizinhos sem autorização.
- Modifique apenas o necessário, preservando GUIDs de assets, serialização do Inspector, prefab overrides, compatibilidade Netcode e APIs públicas.
- Validar em ordem: revisão estática de diff → verificações baratas → instruções de Test Runner manual; testes headless só sob autorização.
- Nunca invente um teste executado. Reporte `não executado` quando cabível.
- Se uma ação falhar, analise a causa; no máximo uma tentativa de reparo automático; depois explique o bloqueio.
- Depois de alteração relevante, atualize `CURRENT_STATE.md`, `CHANGELOG.md` e documentação da área de modo conciso, sem recontar todo o projeto.
- Ao concluir, entregue: arquivos alterados, comportamento, riscos, validação realmente feita, teste manual necessário e pendências.
```

**Meta interna sugerida:** manter `AGENTS.md` em aproximadamente 500–900 tokens; os outros arquivos devem ser consultados por ativação, não carregados integralmente a cada turno. Isso é uma meta do projeto, **não** um limite oficial do produto.

### 3.3 Regras condicionais

Exemplo válido de regra para `.agents/rules/unity-csharp.md`:

```md
---
trigger: glob
globs: "**/*.cs"
description: "Regras C# Unity, serialização e testes focalizados."
---
# C# Unity
- Preserve campos serializados, APIs e referências de componentes.
- Não use UnityEditor em runtime assemblies.
- Prefira dependências explícitas e cache de referências sobre buscas por frame.
- Cuidado com alocações em Update, FixedUpdate, handlers e geração procedural.
- Evite logs frequentes por frame ou por NPC; use diagnóstico condicional.
- Mantenha classes/arquivos menores e teste de forma localizada.
- Não introduza asmdef sem auditoria de referências.
```

Exemplo de `.agents/rules/unity-networking.md`:

```md
---
trigger: model_decision
description: "Carregar ao tocar Netcode, RPCs, NetworkBehaviour, multiplayer, autoridade, spawn e sincronização."
---
# Multiplayer
- Identifique servidor, proprietário e clientes antes de alterar fluxos de estado.
- Preserve autoridade do servidor e validação de entrada.
- Não faça testes equivalentes a multiplayer real só em modo single-player.
- Planeje teste com host + cliente, considerando ParrelSync e estado de spawn/despawn.
```

Criar regras semelhantes **curtas** para:
- `unity-assets.md`: `trigger: glob` para `**/*.prefab, **/*.unity, **/*.asset, **/*.mat, **/*.meta` (**testar** se o padrão glob casa com os arquivos desejados na versão instalada); não editar YAML serializado às cegas, não recriar GUID.
- `unity-procedural-ui.md`: `trigger: model_decision`; seed/determinismo de cidade, navegação, UI Toolkit, câmera, minimapa, shaders/URP.

**Não** usar `always_on` para todos os domínios, porque a documentação oficial especifica que esse conteúdo entra em toda sessão. Cada arquivo de `rules` precisa de frontmatter válido (`trigger: always_on | model_decision | glob | manual`).

### 3.4 Skills (procedimentos acionados quando necessários)

Criar cada `SKILL.md` com frontmatter `name` e `description` claros, seguindo o padrão oficial:

1. `change-safely`: identifica arquivos, faz plano de impacto mínimo, aplica alterações, preserva `.meta` e rollback.
2. `targeted-validation`: executa nível **0/1** por padrão, indica níveis **2/3** apenas com autorização. Nunca inicia Unity sem solicitação específica.
3. `update-project-docs`: lê só documentação afetada, atualiza estado, changelog e decisões após mudanças substanciais. Atualização idempotente, sem novo documento por mudança trivial.
4. `debug-unity-logs`: recebe caminho do log, extrai **só** primeiras causas raízes, com linhas e arquivos, deduplica stacktraces, retorna no máximo 30–60 linhas por rodada. Nunca executa Unity para gerar novos logs automaticamente.

Usar `/<nome-da-skill>` quando quiser invocá-la diretamente. Não investir em antigos workflows monolíticos: a documentação nova recomenda Skills e marca os workflows antigos como deprecados. Migração só quando necessário, sem duplicar arquivos.

---

## 4. Fase P1: ecossistema de documentação que não desperdiça contexto

### 4.1 Índice, estado atual e documentação viva

Criar `ScaryParty/Docs/INDEX.md` com **links e resumos de uma linha**, sem colar todo o conteúdo. Integrar links para documentos já existentes, em particular:

- `../../PizzeriaDocumentation.md` — detalhamento técnico da pizzaria (checar links relativos a partir de `Docs/`).
- `../../MapGenerationDocumentation.md` — geração de mapas.
- `../../RELATORIO_IMPLEMENTACAO_PERSONAGENS_PIZZAS_CLIENTES.md` — relatório histórico de NPCs/personagens.
- `AnimationSystem.md`, `CityGeneration.md`, `PizzeriaArchitecture.md`, `PizzeriaImplementationPlan.md`, `PizzeriaSetup.md`, `PizzeriaValidation.md`.
- `Pizzeria/Auditoria.md`, `Pizzeria/Checklist.md`, `Pizzeria/Decisoes.md`, `Pizzeria/Guia.md`.

Não reescrever automaticamente documentos grandes nem criar versões concorrentes de seu conteúdo. Sinalizar itens desatualizados com data, status e link para implementação real.

`CURRENT_STATE.md` terá **até ~150–200 linhas**, incluindo:
- versão Unity, branch e commit-base da última revisão documental;
- o que funciona / o que está parcial / o que está pendente;
- módulos relevantes (`City`, `Navigation`, `Pizzeria`, `Network`, `Player`, `UI`, `Editor`);
- riscos confirmados e caminhos dos principais pontos de entrada (sem inventar);
- links para decisões e checklists.

**Atenção:** não transformar todo trabalho concluído em um boletim extenso. Estado corrente é um mapa navegável, não um diário.

### 4.2 Registro de mudanças e decisões

`CHANGELOG.md`: registrar apenas alterações significativas agrupadas por data (ou versão), **3–8 linhas por alteração**:

```md
## 2026-11-13 — Política de agentes e documentação
- Escopo: configuração de assistentes e documentação.
- Alterações: regras de execução manual, índice e skills sob demanda.
- Validação: inspeção de arquivos e `git diff --check`; Unity não executada.
- Risco: necessário confirmar permissão do Antigravity no app local.
- Referência: `AgentOperations/HEADLESS_AUDIT.md`.
```

`Changes/YYYY-MM-DD_assunto.md`: criar **somente** para tarefa complexa (mudança arquitetural, sistema novo, migração, regressão) e usar template com: problema, arquivos exatos, decisão, dependências, alternativas, testes realizados, testes manuais, limitações, rollback. Não criar 10 documentos repetidos para 10 pequenos ajustes.

`Decisions/ADR-0001-....md`: decisões estruturais (e.g. autoridade de servidor, navegação procedural, pipeline de materiais) com **contexto → decisão → consequências → status**. Evitar duplicar `Pizzeria/Decisoes.md`; indexar decisões antigas e migrar só quando houver real benefício.

### 4.3 Protocolo de leitura econômico (obrigatório)

1. `AGENTS.md` (já carregado pelo agente) e até 2 trechos de `INDEX.md` / `CURRENT_STATE.md` relevantes.
2. Determinar **área da tarefa** a partir do caminho do script.
3. Buscar por nomes/métodos em `ScaryParty/Assets/Piaaria/` e documentos indexados; limitar arquivos e linhas. Evitar árvores completas, `cat` de arquivos gigantes e grep em `Library`, `ThirdParty`, plugins sem necessidade.
4. Abrir **somente** até 3–5 arquivos de código centrais e docs relevantes na primeira rodada; expandir se a evidência pedir.
5. Comparar com `git diff`/`git status` de escopo reduzido, não com todo o projeto.
6. Registrar links e resumo conciso, não copiar stacktraces ou toda a classe para relatórios.

Quando a tarefa atravessar vários módulos, é legítimo ampliar o escopo **de modo justificado**. Os números acima são padrões operacionais, não limites cegos.

### 4.4 Handoff de sessão

Manter `ScaryParty/Docs/AgentOperations/LAST_HANDOFF.md` pequeno (<100 linhas) **apenas** quando uma tarefa ficar interrompida/complexa: último commit observado, branch, arquivos tocados, intenção, riscos, o que foi verificado, próximo passo. Ao começar novo assunto, usar novo chat/sessão e ler esse handoff apenas se relacionado.

Não editar esse arquivo a cada interação trivial; caso contrário, vira mais custo que economia.

---

## 5. Fase P1: estratégia de validação sem batchmode cotidiano

| Nível | Quando | Ação | Unity.exe headless? |
|---|---|---|---|
| L0 | qualquer tarefa | revisar arquivo/diff, `git diff --check`, inspeção estática, invariantes | **NÃO** |
| L1 | mudança de código | testes de lógica puros disponíveis, revisão C# focalizada, verificadores leves sem Editor | **NÃO** |
| L2 | integração Unity | instruções para o usuário testar no **Unity Editor já aberto**, EditMode/PlayMode manual, Console filtrado | **NÃO** |
| L3 | a pedido explícito | testes Unity CLI filtrados, build, validação de prefab/cena, performance/multiplayer completo | **SIM, somente aprovado** |

- Não afirmar que `dotnet build` da solução Unity substitui o compilador do Editor; dependências e geração de projeto podem divergir.
- Para mudanças em gameplay/NGO: checklist host + cliente, troca de cena, spawn, RPCs e estados de desconexão. PlayMode local isolado não valida rede completa.
- Para shaders/URP, materiais, UI Toolkit e animação: testar visualmente; headless `-nographics` não valida renderização.
- **Não habilitar `-ignorecompilererrors`** para fazer builds parecerem válidos.
- **Não desabilitar Domain Reload por otimização automática**: o projeto já tem `EnterPlayModeOptions` que exige auditoria de estado estático, eventos e Netcode; primeiro medir e validar compatibilidade.
- Comandos de teste CLI só devem entrar no script/guia de exceção, nunca em hooks automáticos. A execução aprovada gravará XML/log em diretório ignorado, análise com sumário curto (status de saída, falhas filtradas, caminho do log).

### Checagens leves úteis

Implementar opcionalmente `Tools/AgentChecks/quick-check.ps1`, sem dependência externa, rodando **apenas quando o usuário autorizar comandos**:
- `git status --short` e `git diff --check`;
- listar C# alterados e arquivos `.meta` associados a assets novos/movidos;
- detectar `.meta` faltantes / sem par **nos arquivos alterados**, excluindo pastas e exceções conhecidas;
- detectar `UnityEditor` acidental em runtime, tipos duplicados e diffs contendo GUID alterado (alerta, sem autofix);
- relatar erros resumidos em vez de despejar logs gigantes;
- exit code não-zero em caso de falha de integridade; não alterar arquivos.

**Não** introduzir ganchos pré-commit, watchers contínuos, scripts de validação `OnPostprocessAllAssets` ou verificação pesada em `InitializeOnLoad` neste projeto por padrão.

---

## 6. Fase P2: boas práticas específicas ao código do jogo

Aplicar apenas nas áreas **tocadas pela tarefa**, sem uma refatoração massiva:

**C# / Unity**
- Preferir funções pequenas, responsabilidades claras e dados de configuração em `ScriptableObject` já existentes; não criar abstrações desnecessárias.
- Evitar `Find...` dentro de `Update`/`FixedUpdate`, LINQ/strings/logs repetidos no hot path e `Instantiate/Destroy` massivos sem medir; cache, pooling e reutilização quando justificável.
- Não reescrever objetos serializados à mão nem alterar nome/tipo de `[SerializeField]` sem plano de migração de referência (considerar `[FormerlySerializedAs]` quando adequado).
- `.asmdef` pode reduzir recompilação, mas **não** espalhar asmdefs automaticamente. Mapear referências, asmdefs de testes/editor e dependências ThirdParty antes de criar um experimento isolado.
- `.editorconfig`: aplicar convenções só a **linhas/arquivos tocados**; evitar reformatar milhares de arquivos, conflitos de EOL e alterações cosméticas gigantes.

**Network / NGO**
- Verificar autoridade do servidor, validação de pedidos, controle de ownership, assinaturas/desinscrições de eventos, spawn/despawn e sincronização que afeta 2+ clientes.
- Nunca tratar teste de cliente local único como prova de funcionalidade multiplayer.

**Cidade procedural e navegação**
- Garantir que seed, parâmetros e ordem de geração estejam explicitados; evitar dependência involuntária de `Random` global ou da ordem de execução.
- Medir antes de otimizar geração de malhas, Navigation/AI, NPCs, objetos na cena e atualizações de minimapa; validar comportamento com cidades pequenas/médias/grandes manualmente.

**Pizzaria, editor, assets e UI**
- Preservar prefabs e seus overrides, slots de bancada/quadro, referências de materiais e animators; evitar scripts de reparo que salvam automaticamente.
- Wizards Odin/Editor devem ter preview e operação explícita, `Validate` antes de `Apply`, idempotência e capacidade de cancelar.
- Para UI Toolkit, manter UXML/USS/Controllers separados por responsabilidade; não refazer UI ou materiais fora do escopo.
- Para toon/URP, evitar substituir materiais de `ThirdParty` diretamente; usar presets/cópias versionadas, com caminho de rollback.

---

## 7. Fase P2: reduzir lixo e ruído no repositório/IDE

1. **Não substituir o `.gitignore` existente.** Acrescentar exceções **estreitas** para logs de texto gerados pela equipe, começando por `UnityBatchLog.txt`; validar se há outras saídas rastreadas.
2. Se confirmado que `UnityBatchLog.txt` é artefato gerado e ninguém o utiliza como documentação histórica, propor `git rm --cached UnityBatchLog.txt` (mantém arquivo local) com autorização explícita e adicionar o padrão ao `.gitignore`. **Isso não remove o arquivo do histórico Git**.
3. Revisar `ScaryParty/CityGenerationLog.json`, `PizzeriaMigrationValidationReport.txt`, `PizzeriaFbxInventoryReport.txt`, `PiariaInstallationResult.txt` e similares: classificar histórico útil x artefato transitório; NÃO desversionar por semelhança de nome.
4. Preservar `.meta` de Unity, cenas `.unity`, `.prefab`, `.asset`, `.mat`, UXML, USS e shaders **no versionamento**, mesmo se alguns estiverem ocultos no Explorador da IDE.
5. Ajustar cuidadosamente `files.watcherExclude` e `search.exclude` na configuração de workspace quando a IDE suportar, priorizando `**/Library/**`, `**/Temp/**`, `**/Logs/**`, `**/Obj/**`, `**/Build*/**` e `**/.git/**`. `files.exclude` pode ocultar arquivos importantes visualmente; não usá-lo como mecanismo único de indexação do agente.
6. Não adicionar ignores amplos como `**/*.txt`, `**/*.asset`, `**/*.prefab`, `**/*.unity`, `**/*.meta` ou `**/*.md`.
7. Limitar outputs de terminal por coleta na origem (arquivo de log + extração focalizada), não por despejar 100 mil linhas e pedir ao modelo que resuma depois.

---

## 8. Execução controlada em commits/fases

### Commit A — P0 sem riscos (primeira execução)

- Inventariar caminhos/configurações sem iniciar Unity.
- Criar `AGENTS.md` e política de execução manual.
- Verificar/preparar instruções para o usuário escolher Request Review nas configurações do Antigravity (a UI global pode exigir ação manual).
- Criar `HEADLESS_AUDIT.md` e `COST_BASELINE.md`.
- Nenhuma mudança em gameplay, cenas, packages ou compilação.
- **Parada de segurança:** o usuário confirma que chamadas headless automáticas não aparecem mais.

### Commit B — documentação e skills

- Criar índice e resumo de estado com links para docs já existentes.
- Criar rules condicionais (`glob`, `model_decision`) e skills de validação/documentação/debug.
- Deduplicar regras existentes, mantendo apenas o necessário no contexto constante.
- Criar documentação de mudanças com exemplos e nenhum conteúdo fictício sobre implementações.

### Commit C — checagens leves e higiene

- Revisar ignores (nunca perder assets), logs temporários e configs de search/watchers.
- Adicionar script de checagem estática **sem autorun**.
- Testar o próprio script em arquivos alterados, se o usuário autorizar sua execução pelo terminal.

### Commit D — refinamento após uso real

- Fazer 2–3 tarefas representativas **sem executar headless** (por exemplo um ajuste isolado de UI Toolkit, um componente Pizzeria, uma regra de geração da cidade).
- Medir com dados observáveis e comparar; registrar gargalos restantes.
- Propor ajustes finos; não alterar grandes assemblies, builds ou projetos sem novo plano.

**Observação:** esses são lotes lógicos de implementação; não realizar `git commit`/`push` automaticamente. O usuário decide se e quando registrá-los no Git.

---

## 9. Critérios de aceitação

- [ ] O agente não chama Unity.exe / batchmode por iniciativa própria, inclusive depois de erro de compilação.
- [ ] Chamar a Unity manualmente pelo Editor permanece funcional.
- [ ] O controle da IDE exige consentimento para comandos perigosos; qualquer mudança de configuração global necessária foi explicada, sem alegação de que o agente a efetuou sozinho.
- [ ] Não há regra grande carregada permanentemente; regras condicionais têm frontmatter válido.
- [ ] A skill de validação não invoca Unity e produz lista curta de testes manuais.
- [ ] Documentação atual está indexada sem exclusão/reescrita desnecessária.
- [ ] Uma nova tarefa pode começar lendo `INDEX` + `CURRENT_STATE` e descobrindo documentos do módulo certo.
- [ ] O `CHANGELOG` distingue **implementado** de **planejado** e identifica validação não executada.
- [ ] Nenhum GUID de `.meta`, prefab, cena, pacote, material ou arquivo de usuário foi alterado por efeito colateral da configuração.
- [ ] O agente consegue explicar o que realmente consumia cota, sem atribuir automaticamente todo custo ao motor Unity.
- [ ] Diff final restrito a regras, skills, documentação, scripts leves e eventuais ajustes aprovados de configuração/ignore.

## 10. Instrução para a primeira sessão do Antigravity

**Cole o prompt abaixo em uma nova sessão, anexando este arquivo ou informando seu caminho local.**

> Implemente o plano `Plano_Antigravity_Unity_ScaryParty.md` no repositório ScaryParty, seguindo estritamente a ordem P0 → P1 → P2. O primeiro objetivo é impedir toda execução automatizada da Unity em modo headless ou batchmode, preservar a possibilidade de usar o Editor normalmente, e reduzir gastos de cota via leitura seletiva, logs pequenos, documentação indexada e skills sob demanda. Comece com auditoria estática de regras, scripts, tarefas e configurações existentes, sem rodar Unity.exe, builds, Test Runner, scripts de importação, browser headless, nem agentes recursivos. Crie regras mínimas e skills conforme o plano, sem duplicar instruções já existentes. Nunca edite cenas, prefabs, assets de jogo ou packages nesta tarefa. Preserve alterações locais. Caso uma configuração da interface do Antigravity dependa de ação minha, explique o passo exato e prossiga no que puder ser feito no repositório. Não faça commit/push automaticamente. Ao finalizar, entregue arquivos alterados, diagnóstico de origem do headless, configuração efetiva, validação realmente realizada e limitações, sem inventar resultados.

---

## Fontes primárias consultadas

- Regras Antigravity: https://antigravity.google/docs/rules/
- Skills Antigravity: https://antigravity.google/docs/skills
- Migração workflows → skills: https://antigravity.google/docs/migration/workflows-to-skills
- Permissões de agente: https://antigravity.google/docs/agent-settings
- Painel de contexto CLI: https://antigravity.google/docs/cli/commands/context/
- Unity CLI e batchmode: https://docs.unity.com/en-us/engine/6000.5/manual/unity-editor/command-line-arguments/editor (confirmar parâmetros compatíveis com Unity 6000.3)
- Unity assembly definitions: https://docs.unity3d.com/es/2021.1/Manual/ScriptCompilationAssemblyDefinitionFiles.html
- Unity `.meta`/versionamento: https://docs.unity3d.com/cn/2018.3/Manual/ExternalVersionControlSystemSupport.html
- Unity GC e pooling: https://docs.unity3d.com/cn/2022.3/Manual/performance-garbage-collection-best-practices.html
- ScaryParty: https://github.com/TavernaDasMilPortas/ScaryParty

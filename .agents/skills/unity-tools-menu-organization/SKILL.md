---
name: unity-tools-menu-organization
description: Padroniza e audita a organização da aba Tools do Unity Editor no projeto Scary Party sob Tools/Scary Party/, separando estritamente Wizards, Uso Comum, Setup e Uso Único e Legado. Use sempre ao criar, refatorar, depreciar ou revisar qualquer [MenuItem("Tools/...")] em Scripts Editor.
---

# Organização do Menu `Tools` — Scary Party

Todo `[MenuItem("Tools/...")]` criado pelo projeto deve seguir uma taxonomia única sob **`Tools/Scary Party/`**. Nunca adicione itens diretamente na raiz `Tools/`, nem em namespaces paralelos como `Tools/Piaaria/`, `Tools/Pizzeria/` ou `Tools/Weapons/`, nem misture janelas, ações recorrentes, migrações únicas e ferramentas antigas no mesmo submenu.

---

## 1. Árvore de Decisão: Onde colocar um `[MenuItem]`?

Classifique toda ferramenta de Editor em **exatamente um** dos 4 baldes principais:

| Balde | O que entra aqui | Faixa de Prioridade |
| :--- | :--- | :---: |
| **`Wizards`** | Janelas interativas (`EditorWindow` / `OdinEditorWindow`) usadas continuamente para autoria, edição visual, validação interativa e configuração ativa de conteúdo/cenas. | `10 – 99` |
| **`Uso Comum`** | Ações de 1 clique ou utilitários usados no dia a dia: validadores, suítes de teste, vinculação de slots a objetos selecionados, reparo recorrente do prefab ativo, inspeção e atalhos de cena. | `110 – 199` |
| **`Setup e Uso Único`** | Configurações iniciais de projeto/camadas, migrações arquiteturais (FBX/UI/SOs), seeders de presets iniciais, reparos pontuais de fase específica e limpadores únicos de legado. | `210 – 299` |
| **`Legado`** | Builders de prefabs antigos, sistemas desativados (ex.: Weapons) e scripts substituídos mantidos apenas para consulta ou compatibilidade temporária. | `310 – 399` |

> **Regra de Migração para Legado:** Assim que um sistema ou prefab for substituído por um fluxo novo, **mova** imediatamente seus `[MenuItem]`s antigos para `Tools/Scary Party/Legado/<Sistema>/...` e atualize qualquer `Debug.LogWarning` / `DisplayDialog` apontando para o novo caminho em `Wizards` ou `Uso Comum`.

---

## 2. Submenus Padrão e Faixas de Prioridade

Respeite as faixas numéricas abaixo para garantir ordenação consistente no menu do Unity e separadores horizontais legíveis:

### `Tools/Scary Party/Wizards/` (`10 – 99`)
- **`Wizards/Pizzaria/`** (`10 – 49`): Estúdios e wizards da pizzaria (`Conteúdo da Pizzaria (Odin Wizard)`, `Estúdio Visual 3D`, `Pizza Studio 2.0`, `Pizzaria Modular (Autoria de Prefab)`, `Clientes e Traços`, `Salão, Filas e Mesas`, `Aparência e Posicionamento`, `Cel Shading Showcase Builder`).
- **`Wizards/Cidade/`** (`50 – 69`): `Cidade Modular (Painel de Controle)`, `Scene Builder`, `Validador de Navegação e População`.
- **`Wizards/Personagem & Interação/`** (`70 – 89`): `Personagens (Character Setup Wizard)`, `First Person & Interaction Setup`, `Armature Mapper`.
- **`Wizards/UI/`** (`90 – 99`): `Montar P.I.A.R.I.A (UI Installer)`.

### `Tools/Scary Party/Uso Comum/` (`110 – 199`)
- **`Uso Comum/Pizzaria/`** (`110 – 159`):
  - `110 – 119`: Validadores e testes (`Validar Setup da Pizzaria`, `Executar Testes de Autoria & Slots`, `Validar Animações e Assentos`).
  - `130 – 139`: Ações contextuais na Hierarchy (`Vincular Slots aos Objetos Selecionados (Substituir)/(Adicionar)`).
  - `150 – 159`: Reparos seguros do prefab modular (`Reparar Prefab Modular Manual...`, `Reparar Prefab Selecionado...`).
- **`Uso Comum/Cidade/`** (`170 – 179`): `Validar Integridade do Kit Modular`, `Configurar Pontos de Entrega na Cidade`.
- **`Uso Comum/UI/`** (`185 – 189`): Atalhos frequentes (`Abrir LobbyScene`, `Configurar Cena Atual`).
- **`Uso Comum/Inspeção/`** (`190 – 199`): Diagnósticos pontuais sob demanda (`Inspecionar Hierarquia FBX`).

### `Tools/Scary Party/Setup e Uso Único/` (`210 – 299`)
- **`Setup e Uso Único/Projeto & Multiplayer/`** (`210 – 219`): Layers/tags e migrações de componentes de rede.
- **`Setup e Uso Único/Personagens/`** (`225 – 239`): Batches de setup inicial, geração de prefab de preview e correções históricas únicas.
- **`Setup e Uso Único/Pizzaria/`** (`240 – 259`): Reparos de fase (`Fase 1`, `Fase 4`), migração de referências SO/FBX, criação única de prefabs auxiliares e injeções de cena.
- **`Setup e Uso Único/VFX & Visuals/`** (`260 – 274`): Builders únicos de VFX e seeders iniciais de `VisualStyle`.
- **`Setup e Uso Único/Cidade Modular/`** (`275 – 284`): Geração/sobrescrita de placeholders e cena de teste do kit.
- **`Setup e Uso Único/UI (P.I.A.R.I.A)/`** (`285 – 294`): Migração inicial de HUD e execução em lote de cenas.
- **`Setup e Uso Único/Limpeza de Legados/`** (`295 – 299`): `Legacy Cleanup Builder` e remoção 1-click de sistemas antigos.

### `Tools/Scary Party/Legado/` (`310 – 399`)
- **`Legado/Pizzaria (Prefab Antigo)/`** (`310 – 329`): Builders procedurais de `PizzeriaPrefab.prefab` antigo (`Reconstruir Pizzeria Prefab`, `Construir Quadro de Comandas`, `Construir Telefone`, `Construir Pilha de Staging`).
- **`Legado/Multiplayer/`** (`330 – 349`): Setups antigos substituídos (ex.: `Setup Multiplayer (Passos 4 a 6)`).
- **`Legado/Weapons (Desativado)/`** (`350 – 369`): Setup, gerador e previewer do sistema de combate/armas desativado.

---

## 3. Regras Técnicas Obrigatórias ao Escrever `[MenuItem]`

1. **Prioridade explícita sempre:** Nunca omita o 3º parâmetro de prioridade (o padrão `1000` do Unity desordena o submenu). Use sempre `[MenuItem("Tools/Scary Party/<Balde>/<Grupo>/<Item>", false, <prioridade>)]`.
2. **Separadores horizontais (`>= 11`):** Para criar uma linha separadora entre subgrupos funcionais dentro do mesmo menu, deixe diferença de **pelo menos `11`** entre as prioridades adjacentes (ex.: `112` e `130`).
3. **Sem atributos duplicados:** Nunca empilhe dois `[MenuItem(...)]` diferentes no mesmo método estático (mantém o menu limpo e sem apelidos redundantes).
4. **Paridade de `validate = true`:** Se houver função de validação `[MenuItem("...", true)]`, a string do caminho deve ser **idêntica** à da ação correspondente (`validate = false`).
5. **Encoding UTF-8 limpo:** Escreva acentos em português corretamente (`Animações`, `Lógica`, `Único`, `Migração`) em arquivos salvos como UTF-8. Nunca introduza mojibake.
6. **Atualize referências internas junto:** Ao criar ou mover um `[MenuItem]`, procure por strings com o caminho antigo em:
   - Comentários XML/topo (`/// Menu: ...`, `/// Acesso via: ...`)
   - `HelpBox`, `EditorUtility.DisplayDialog` e `Debug.LogWarning`
   - Documentação ativa em `Docs/`.

---

## 4. Verificação Obrigatória Pós-Alteração

Após adicionar, mover ou revisar qualquer `[MenuItem("Tools/...")]`:

1. **Audite todos os atributos do projeto** com PowerShell na raiz do repositório:
```powershell
Get-ChildItem -Path "ScaryParty/Assets/Piaaria" -Recurse -Include *.cs |
  Select-String -Pattern '\[MenuItem\("Tools' |
  ForEach-Object { "$($_.Path):$($_.LineNumber): $($_.Line.Trim())" }
```
   - Confirme que **100%** dos resultados começam com um dos 4 prefixos válidos:
     - `Tools/Scary Party/Wizards/`
     - `Tools/Scary Party/Uso Comum/`
     - `Tools/Scary Party/Setup e Uso Único/`
     - `Tools/Scary Party/Legado/`
   - Confirme também fora de `Assets/Piaaria` (excluindo pacotes de terceiros) que nenhum script do projeto criou entradas soltas em `Tools/`.

2. **Compile o assembly de Editor (sem reconstruir pacotes Unity em paralelo):**
```powershell
dotnet build ScaryParty/ScaryParty.VisualStyles.Runtime.csproj -p:BuildProjectReferences=false -v:minimal
dotnet build ScaryParty/Assembly-CSharp.csproj -p:BuildProjectReferences=false --no-restore -v:minimal
dotnet build ScaryParty/Assembly-CSharp-Editor.csproj -p:BuildProjectReferences=false --no-restore -v:minimal
```
   - Exija **0 erros de compilação** antes de encerrar a tarefa. (Caso algum `project.assets.json` ainda não exista em `Temp/obj/`, omita `--no-restore` nesse projeto ou execute `dotnet build` após o Unity gerar os artefatos base.)

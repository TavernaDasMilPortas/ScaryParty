---
trigger: glob
globs: "**/*.cs"
description: "Regras de C# Unity, ciclo de vida, performance e testes focalizados."
---
# Diretrizes C# Unity (ScaryParty)

- **Serialização e APIs:**
  - Preserve campos serializados (`[SerializeField]`), modificadores de visibilidade e referências de componentes existentes.
  - Ao renomear variáveis serializadas, avalie o uso de `[FormerlySerializedAs("nomeAntigo")]` para não perder referências em prefabs ou cenas.
- **Assemblies e Dependências:**
  - NUNCA referencie `UnityEditor` em classes de runtime. Scripts de editor devem ficar isolados em pastas `Editor/` ou dentro de diretivas `#if UNITY_EDITOR`.
  - Não introduza novos `.asmdef` sem mapeamento prévio de dependências de terceiros e de testes.
- **Performance e Hot Paths:**
  - Evite chamadas a `GetComponent`, `FindObjectOfType`, `GameObject.Find` ou buscas repetidas dentro de `Update`, `FixedUpdate` ou loops de NPCs; faça cache dessas referências no `Awake` ou `Start`.
  - Evite alocações excessivas de lixo (GC) no frame rate crítico: prefira pooling de objetos, evite concatenação contínua de strings e uso de LINQ em rotinas por frame.
  - Não deixe `Debug.Log` frequente ativo em updates; utilize logs condicionais ou flags de depuração.
- **Testabilidade:**
  - Mantenha classes e métodos pequenos com responsabilidades bem definidas, facilitando testes de lógica pura sem dependência do Editor.

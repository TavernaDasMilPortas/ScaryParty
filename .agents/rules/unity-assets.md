---
trigger: glob
globs: "**/*.prefab, **/*.unity, **/*.asset, **/*.mat, **/*.meta"
description: "Regras de integridade para assets Unity, prefabs, cenas, materiais e arquivos meta."
---
# Diretrizes de Assets e Serialização Unity (ScaryParty)

- **Preservação de `.meta` e GUIDs:**
  - NUNCA delete, renomeie ou recrie arquivos `.meta` fora de operações coordenadas com o Unity Editor. A perda de um `.meta` corrompe referências em todas as cenas e prefabs do projeto.
  - Ao criar novos assets via script ou texto, certifique-se de que o arquivo `.meta` correspondente seja gerado e preservado com GUID consistente.
- **Edição de Arquivos YAML:**
  - Evite editar manualmente arquivos `.unity`, `.prefab` ou `.asset` serializados em YAML, a menos que seja uma correção cirúrgica estritamente verificada.
  - Não altere propriedades `fileID` ou GUIDs referenciados internamente.
- **Hierarquias e Prefab Overrides:**
  - Ao modificar prefabs de bancadas, jogadores ou pizzaria (`PizzeriaPrefab.prefab`, `PlayerArmature.prefab`), garanta que os overrides existentes e conexões de componentes não sejam quebrados.
  - Respeite slots de ancoragem existentes (`PlayerSpawn_*`, `EntranceAnchor`, `Station_*`).
- **Materiais e Shaders:**
  - Para alterações de cel shading / toon URP, não sobrescreva materiais da pasta `ThirdParty/` diretamente. Crie presets ou cópias versionadas no diretório do projeto (`Assets/Piaaria/`).

---
trigger: model_decision
description: "Carregar ao trabalhar com geração procedural da cidade, navegação/NavMesh, UI Toolkit, câmera e minimapa."
---
# Diretrizes para Cidade Procedural, Navegação e UI (ScaryParty)

- **Determinismo e Geração Procedural:**
  - Toda a geração da cidade (`CityGenerator`) deve ser estritamente determinística a partir de uma `seed` centralizada.
  - Evite uso descontrolado de `UnityEngine.Random` global que possa dessincronizar clientes; use instâncias isoladas de `System.Random(seed)` onde a ordem de avaliação possa variar entre threads ou frames.
  - O ponto de spawn do jogador deve ser posicionado de forma segura na calçada/rua, sem sobrepor bancadas ou colisores de edifícios.
- **Navegação e NavMesh:**
  - Após a geração dinâmica dos blocos e ruas, superfícies de NavMesh devem ser atualizadas/geradas respeitando o tamanho dos agentes e obstáculos dinâmicos (`NavMeshObstacle`).
  - Em alterações no sistema de tráfego ou rotas de NPCs, valide com configurações de cidade pequena, média e grande.
- **UI Toolkit e Interfaces de Usuário:**
  - Mantenha a separação de responsabilidades recomendada pelo UI Toolkit: estrutura visual em arquivos `.uxml`, estilização em `.uss`, e lógica de interação em classes C# (`UIDocument` / controllers).
  - Evite recriar ou alocar elementos de UI repetidamente por frame; atualize dados por binding ou eventos quando o estado do jogo mudar.
- **Minimapa e Câmeras:**
  - Assegure-se de que câmeras secundárias (minimapa/render textures) configurem máscaras de culling apropriadas para evitar custo duplicado de renderização.

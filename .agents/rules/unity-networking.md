---
trigger: model_decision
description: "Carregar ao tocar Netcode, RPCs, NetworkBehaviour, multiplayer, autoridade, spawn e sincronização."
---
# Diretrizes de Rede e Multiplayer — Netcode for GameObjects (NGO)

- **Autoridade e Fluxo de Estado:**
  - Identifique claramente se a lógica pertence ao Servidor/Host ou aos Clientes antes de qualquer alteração de estado.
  - Modificações de estado autoritativo (inventário, progresso de pedidos, entrega de pizzas, spawn de itens) devem ser processadas no servidor.
  - Clientes enviam comandos via `ServerRpc` com validação de parâmetros; o servidor replica via `NetworkVariable` ou `ClientRpc`.
- **Spawn e Ciclo de Vida:**
  - Objetos sincronizados em rede devem possuir `NetworkObject` e estar registrados na lista de NetworkPrefabs do `NetworkManager`.
  - Respeite o fluxo `OnNetworkSpawn` e `OnNetworkDespawn`. Clientes que entram depois (late-joiners) devem obter o estado atualizado (ex: seed da cidade via `NetworkVariable` em vez de depender apenas de `ClientRpc` disparado anteriormente).
  - Nunca chame `Destroy()` diretamente em um objeto em rede: use `NetworkObject.Despawn()` no servidor.
- **Validação de Testes Multiplayer:**
  - O teste em modo single-player local NÃO valida comportamento em rede, controle de propriedade (`IsOwner`) nem sincronização de latência.
  - Oriente o usuário a validar cenários de rede utilizando o **ParrelSync** (clones de projeto) ou duas instâncias conectadas (Host + Client).

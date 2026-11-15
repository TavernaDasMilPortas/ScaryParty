# Visão Geral de Arquitetura — ScaryParty

**Projeto:** ScaryParty  
**Engine:** Unity 6000.3.6f1 (URP)  
**Multiplayer:** Unity Netcode for GameObjects (NGO)

---

## 1. Visão Sistêmica e Domínios

O projeto está organizado nos seguintes domínios centrais:

1. **City (Geração Procedural):**
   - Criação determinística do mapa urbano por seed (`CityGenerator`).
   - Geração de calçadas, ruas, prédios, cruzamentos e pontos de navegação.

2. **Pizzeria (Gameplay e Estações):**
   - Lógica de bancadas de corte, molho, queijo, assamento no forno e empacotamento.
   - Gerenciamento de receitas, ingredientes e pontuação.

3. **Navigation (IA e Tráfego):**
   - Superfície dinâmica de NavMesh sobre a cidade procedural.
   - Rotas de clientes e tráfego de NPCs pedestres.

4. **Network (Sincronização Multiplayer):**
   - Servidor autoritativo para regras de jogo, inventário e entrega.
   - Sincronização de seed via `NetworkVariable` para suporte a late-joiners.

5. **Player & Interaction:**
   - Controle de movimentação e câmera (primeira e terceira pessoa).
   - Sistema de interação com bancadas e objetos do cenário.

6. **UI & Minimap:**
   - Interface de usuário (UI Toolkit e uGUI) e rastreamento dinâmico de mapa via `MinimapRouteManager`.

---

## 2. Documentação Relacionada
- [Arquitetura da Pizzaria](../PizzeriaArchitecture.md)
- [Geração de Mapa](../../MapGenerationDocumentation.md)
- [Sistema de Animações](../AnimationSystem.md)
- [Índice Geral de Documentação](../INDEX.md)

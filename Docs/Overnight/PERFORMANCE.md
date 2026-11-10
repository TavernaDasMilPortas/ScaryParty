# PERFORMANCE — Medições e Orçamento de Desempenho

## Baseline (Pré-implementação)
- Data/Hora: 10/11/2026 UTC
- Hardware: Windows, 8 GB RAM
- Alvo: 60 FPS estáveis (ou 30 FPS mínimos com frame time consistente)
- Objetos na cena GameScene serializada: ~64k GameObjects (ramo `__City__`)
- Memória e GC: Varreduras recorrentes em `CustomerSpawner`, `ReplicationProjector` e `PizzeriaItemVisualizer`

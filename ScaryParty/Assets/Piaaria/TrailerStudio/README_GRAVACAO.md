# Trailer Studio — Guia de Gravação (P.I.A.R.I.A.)

**Cena:** `ScaryParty/Assets/Piaaria/TrailerStudio/Scenes/TrailerStudio.unity`  
**Checkout Base:** `main` (`c9fccd25`)  
**Versão Unity:** `6000.3.6f1` (`UNITY_EXECUTION=MANUAL_ONLY`)

---

## 1. Como Abrir e Gravar (Sem Reposicionar Objetos Manualmente)

1. Abra `ScaryParty/Assets/Piaaria/TrailerStudio/Scenes/TrailerStudio.unity` no Unity Editor.
2. Defina o Game View para **16:9 (1920x1080)** e desative Gizmos / Stats no Game View.
3. Entre em **Play Mode**.
4. Na Hierarchy, selecione **`TrailerStudio/Director`**.
5. No Inspector do componente **`TrailerDirector`**:
   - Escolha o take no dropdown **`Take`** (ex.: `T03_Dough`).
   - Escolha **`Camera Mode`** (`CameraA` ou `CameraB_Alt` quando disponível).
   - Clique em **`Prepare / Reset Take`** (ou deixe preparado automaticamente ao trocar `Take`).
   - Confirme `Environment Ready = true` e `State = Ready` (e `Last Validation Error` vazio).
   - Inicie sua captura externa (ex.: OBS), clique em **`Play`** (botão verde), aguarde `State = Finished` (inclui `0.75 s` de pré-roll antes e `0.75 s` de pós-roll depois) e pare a captura.
   - Use **`Pause / Resume`** e **`Stop`** para pausar/reiniciar com reset determinístico, ou **`Next Take`** para avançar.

> **Takes-piloto prioritários:** `T03_Dough`, `T07_Packaging` e `T12_MotorcycleStart` estão configurados para gravação imediata assim que você entrar em Play Mode e confirmar no Game View.

---

## 2. Catálogo dos 15 Takes (`T01`–`T15`)

| Take | Ação | Janela (+1,5s margens) | Câmera A / B | Participantes | Status de Validação Atual |
|---|---|---:|---|---|---|
| `T01_CityReveal` | Cidade e fachada | 8s (9,5s) | Aéreo -> Fachada / — | D + 2 pedestres + 1 carro | Sem verificação visual (estática ok) |
| `T02_KitchenTeam` | 4 funções na cozinha | 10s (11,5s) | Geral / FP (D) | A, B, C, D | Sem verificação visual (estática ok) |
| `T03_Dough` *(Piloto)* | Abrir massa | 6s (7,5s) | Close lateral / Superior | A | Sem verificação visual (estática ok) |
| `T04_Ingredients` | Cortar calabresa | 6s (7,5s) | Close diagonal / — | A | Sem verificação visual (estática ok) |
| `T05_Assembly` | Montar pizza | 8s (9,5s) | Superior / FP (B) | B | Sem verificação visual (estática ok) |
| `T06_Oven` | Porta e inserção no forno | 6s (7,5s) | 3/4 baixo / — | C | Sem verificação visual (estática ok) |
| `T07_Packaging` *(Piloto)* | Embalar pizza assada | 6s (7,5s) | Close 3/4 / Alt 3/4 | C | Sem verificação visual (estática ok) |
| `T08_Tickets` | Comandas acumulando | 5s (6,5s) | Médio -> Close / — | D | Sem verificação visual (estática ok) |
| `T09_KitchenChaos` | Correria cooperativa | 10s (11,5s) | Travelling / FP (D) | A, B, C, D | Sem verificação visual (estática ok) |
| `T10_BurntPizza` | Pizza queimada e reação | 6s (7,5s) | Médio recuo / — | B, C | Sem verificação visual (estática ok) |
| `T11_Backpack` | Guardar caixa na mochila | 6s (7,5s) | Médio acompanhamento / — | D | Sem verificação visual (estática ok) |
| `T12_MotorcycleStart` *(Piloto)* | Montar e sair de moto | 8s (9,5s) | Baixo lateral / — | D + Moto | Sem verificação visual (estática ok) |
| `T13_CityRide` | Percurso na via | 10s (11,5s) | Lateral / Traseira | D + Moto + 1 carro | Sem verificação visual (estática ok) |
| `T14_Delivery` | Entrega no endereço | 8s (9,5s) | Médio endereço / — | D + Moto | Sem verificação visual (estática ok) |
| `T15_FinalGag` | Queda da pizza e reação | 6s (7,5s) | Médio + tilt / — | D | Sem verificação visual (estática ok) |

---

## 3. Menus Editor (`Tools/Scary Party/`)

- `Tools/Scary Party/Uso Comum/Trailer Studio/Construir ou Atualizar Cena`
- `Tools/Scary Party/Uso Comum/Trailer Studio/Validar Cena`

---

## 4. Integridade e Preservação

- Nenhuma cena ou prefab de produção (`GameScene.unity`, `PizzeriaModular_Manual.prefab`, `EditorBuildSettings.asset`, `UserData.asset`) foi sobrescrito.
- Todos os GUIDs e arquivos `.meta` existentes foram preservados.
- `TrailerCityConfig.asset` mantém desativados tráfego/pedestres/eventos aleatórios globais para não interferir nos takes.

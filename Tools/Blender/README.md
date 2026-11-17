# ScaryParty Cutscene Exporter (Blender)

Add-on do Blender para exportar cutscenes para o **Cutscene Builder** da Unity (`Tools/Scary Party/Wizards/Cutscene Builder`).

## Instalação
1. No Blender, abra **Edit > Preferences > Add-ons > Install...**
2. Selecione `Tools/Blender/scaryparty_cutscene_exporter.py`.
3. Ative **Import-Export: ScaryParty Cutscene Exporter**.

## Uso
1. Na **3D Viewport**, abra a barra lateral (`N`) e selecione a aba **ScaryParty**.
2. Preencha o **Cutscene ID**, a **Collection** (opcional), o intervalo de frames e a pasta de saída.
3. Certifique-se de que os **Timeline Markers** estão vinculados às câmeras (`Ctrl+B` na Timeline).
4. Clique em **Validate & Export Cutscene**.
5. O add-on gera:
   - `<cutscene_id>.fbx`
   - `<cutscene_id>.manifest.json` (schema `1.0.0`)

## Limitações conhecidas (MVP)
- Cortes são lidos a partir de **Timeline Markers** vinculados a câmeras (Video Sequencer / transições editoriais não são convertidos automaticamente).
- Simulações (Cloth, Fluid, Soft Body) e Geometry Nodes dinâmicos emitem aviso e requerem bake prévio compatível.

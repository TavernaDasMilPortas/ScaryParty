# Animation Studio 3D — ScaryParty

Visualizador local WebGL para revisar **geometria, poses e animações dos assets reais do Blender** pelo navegador do Antigravity ou navegador normal. Não depende da Unity, não usa geração de imagens e não altera os assets originais.

## 1. Instalação (Windows / PowerShell)

Requisitos: Blender com exportador glTF (recomendado Blender 4.5 LTS ou 5.x compatível com seus arquivos), Node.js 20+ e Python 3.10+.

A partir da raiz do repositório:

```powershell
cd Tools/AnimationStudio
npm install --ignore-scripts
npm run check
npm start
```

Abra **http://127.0.0.1:8765/**. Mantenha o servidor aberto enquanto utiliza o visualizador. Use outra aba do terminal para publicar previews.

O servidor serve somente arquivos do visualizador, Three.js instalado localmente e GLBs publicados, em loopback. Não tem endpoints de execução ou upload. Não é necessário instalar MCP. Após instalar a dependência, não requer CDN/internet. Para outra porta, defina ANIMATION_STUDIO_PORT antes de iniciar.

## 2. Publicar modelos reais

Coloque os arquivos .blend em SourceModels/ (crie a pasta; está no gitignore) ou informe os caminhos reais.

```powershell
python studio.py inspect SourceModels/personagem.blend --output WorkScenes/rig-personagem.json
python studio.py publish SourceModels/personagem.blend --name personagem-base --mode pose
python studio.py publish SourceModels/moto.blend --name moto-base --mode pose
```

Se o Blender não for localizado, use o caminho do seu executável:
```powershell
python studio.py --blender "C:\Program Files\Blender Foundation\Blender 5.0\blender.exe" inspect SourceModels/personagem.blend
```

O argumento --blender vem antes de inspect/publish; também aceita a variável BLENDER_EXE. O Blender precisa ser compatível com a versão do arquivo de origem.

Para uma pose com personagem e moto, peça ao agente para analisar os dois relatórios, criar um script de pose usando os ossos reais e executar:

```powershell
python studio.py publish SourceModels/personagem.blend --append SourceModels/moto.blend --script WorkScenes/pose_moto.py --name moto-v01 --mode pose --save-copy
```

O script é criado pelo agente para seu rig, não é um preset universal. O append importa os objetos sem alinhar ou montar automaticamente. Para editar outra versão, trabalhe na cópia salva em WorkScenes/ ou reexecute o script sobre o original. Não use scripts não confiáveis. O worker desativa autoexec do .blend e executa somente o script adicional escolhido explicitamente.

Para exportar clipes de uma cena animada:
```powershell
python studio.py publish WorkScenes/cena-animada.blend --name corrida-v01 --mode animation
```

## 3. Como revisar

- Biblioteca: atualiza a cada 5 s. Abra uma versão em A e outra em B. Cada publicação é imutável por padrão e tem ID único.
- Orbit: arrastar, scroll e botão direito; enquadramento e cinco direções de câmera. As vistas A/B compartilham a câmera e o relógio.
- Timeline: escolha os clipes, reproduza, pause, altere velocidade ou mova o cursor. Clipes com durações diferentes terminam no próprio final; o relógio é em segundos.
- Grade, wireframe e esqueleto (disponível somente para modelos com bones exportados).
- PNG da vista A: captura WebGL do asset real. Também é possível abrir GLBs autocontidos locais por seleção/arrastar.
- O modo **pose** congela a geometria avaliada do frame, inclusive deformação da armature. O GLB não contém os ossos nesse modo, mas --save-copy conserva a cena editável anterior ao bake.
- O modo **animation** mantém rig e clipes suportados pelo exportador glTF; a presença e o nome dos clipes dependem das ações/NLA da cena. Confirme a lista no visualizador.

## 4. Fluxo no Antigravity

Peça ao agente: "Leia Tools/AnimationStudio/AGENTS.md e README.md. Inspecione meus dois .blend, monte a pose no Blender via bpy, publique a versão em GLB e salve uma cópia editável. Abra o visualizador local no browser se disponível e confira as vistas frontal e lateral. Não substitua meus assets por imagens geradas."

O visualizador funciona em navegador Chromium com WebGL. A disponibilidade do browser embutido e de previews dentro do chat depende da versão/permissões do Antigravity. Use a URL no navegador normal se necessário. Nenhuma API privada do Antigravity é utilizada.

## 5. Limitações e teste de aceitação

O viewport usa iluminação WebGL própria, não os shaders URP nem um render pixel a pixel de Cycles/Eevee. Materiais procedurais do Blender podem exigir bake; texturas ausentes precisam ser resolvidas na origem. O eixo frontal padrão é +Z do glTF; confirme a orientação de cada asset. Instâncias de geometria, hair/particles e modificadores especiais podem exigir preparação específica. A inspeção de pesos contabiliza grupos de deformação; não substitui revisão visual. A ferramenta não é um editor de ossos no browser: alterações de pose são feitas pelo agente via bpy, e o navegador serve para revisão.

Validação local: npm run check (sintaxe e rotas HTTP); python -m py_compile studio.py blender_worker.py. Em seguida publique os dois modelos reais, compare A/B, teste orbit/zoom, PNG, reload, uma cena animada com rig/clipe e verifique que os originais não foram alterados. Testes Blender/WebGL dependem da máquina local. Não abra Unity para validar esta ferramenta.

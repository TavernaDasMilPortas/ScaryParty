# Sistema de Game Feel e Animações — ScaryParty

Este documento resume a arquitetura implementada para o sistema de game feel cartoon, tátil e elástico ("sloppy") da pizzaria e cidade do ScaryParty.

## Direção de Arte e Regras de Ouro
1. **Lógica autoritativa separada da estética:** Serviços de domínio e `NetworkBehaviour` decidem receitas, progresso, inventário, dinheiro, paciência e conclusão de entrega. O fim de um tween ou efeito visual **nunca** concede item/dinheiro nem altera colisões, `NavMeshAgent`, `Time.timeScale` global ou câmera de primeira pessoa (mantida estável por padrão).
2. **Pivô Cosmético Isolado (`GameFeelVisualPivot`):** Itens, mochila e personagens nunca sobrescrevem a escala autoritativa (`VisualInstance.localScale` / cápsulas / colliders). Animações aditivas operam em um pivô cosmético filho (`PosePivot -> CosmeticPivot -> VisualInstance`), preservando pose base e restaurando no `OnDisable`/`StopAndReset`.
3. **Respeito ao Visual Styles & MaterialPropertyBlock:** Mudanças visuais em telefone (`PhoneRinger`) e comandas (`TicketSheetView`) e flashes temporários usam `MaterialPropertyBlock` (`VisualStyleShaderUtility`), sem instanciar `renderer.material` em runtime.
4. **Acessibilidade (`ReduceMotion`):** `GameFeelSettings.ReduceMotion` é sincronizado bidirecionalmente com `UIToolkitTweenService.ReduceMotion`. Em modo reduzido ou preset `Reduzido`, escalas/oscilações de UI e 3D zeram imediatamente, flashes piscantes desligam e apenas áudio e partículas leves essenciais permanecem.

## Arquitetura de Arquivos Principais
- `Assets/Piaaria/Scripts/Presentation/Feedback/GameFeelTypes.cs` — `GameFeelEventId`, `GameFeelMaterialFamily`, `GameFeelPresetMode`, `GameFeelChannel`, `GameFeelContext`, `GameFeelDeduplicator`.
- `Assets/Piaaria/Scripts/Presentation/Feedback/GameFeelSettings.cs` — Presets (`Suave`, `Cartoon`, `Sloppy`, `Reduzido`), limites/orçamentos (`MaxSimultaneousBursts = 12`, `MaxKitchenAudioVoices = 8`) e clamps rígidos (`Itens [0.78..1.22]`, `Personagens [0.95..1.05]`, `Tilt <= 12°`, `UI [0.94..1.08]`).
- `Assets/Piaaria/Scripts/Presentation/Feedback/GameFeelProfile.cs` — `ScriptableObject` de receitas calibradas + `GameFeelMaterialClassifier` e fallback em memória (`CreateRuntimeDefault()`).
- `Assets/Piaaria/Scripts/Presentation/Feedback/GameFeelVisualPivot.cs` — Avaliador aditivo de antecipação/impacto/overshoot/acomodação + assimetria XZ sloppy + destaque emissivo em `MaterialPropertyBlock`.
- `Assets/Piaaria/Scripts/Presentation/Feedback/GameFeelPresenter.cs` — Orquestrador central (singleton opcional auto-instanciado, pool de partículas, suporte a `MMF_Player` override do Feel, impulso local das mãos FPS, roteamento 3D de áudio e feedback UI).
- `Assets/Piaaria/Scripts/Presentation/Feedback/GameFeelNetworkBridge.cs` — Observador não-invasivo de `PizzeriaNetworkState` com snapshot silencioso inicial para clientes *late-joiners*.
- `Assets/Piaaria/Scripts/Editor/GameFeel/GameFeelStudioWindow.cs` — Ferramenta unificada no Editor (`Tools/Scary Party/Wizards/Visuals/Game Feel Studio`).

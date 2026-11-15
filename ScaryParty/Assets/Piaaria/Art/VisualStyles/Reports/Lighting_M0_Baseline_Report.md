# Relatorio M0 — Auditoria de Iluminacao e Atlas URP

- **Timestamp:** 2026-11-14 19:51:21 UTC
- **Unity:** 6000.3.6f1
- **RP Asset:** `PC_RPAsset` (`Assets/Settings/PC_RPAsset.asset`)
- **Renderer Mode:** `ForwardPlus` | **Shadow Distance:** `50m` | **Cascades:** `4`
- **Additional Lights Atlas:** `2048` (Tiers Low/Med/High: `256/512/1024`)
- **SSAO:** Present=True | Intensity=`0,40` | DirectLightingStrength=`0,25`
- **Lights Found:** `43` | **Shadowed Local Lights:** `18` | **Estimated Slices:** `88` | **Atlas Downgrade Risk:** `True`
- **Runtime Config:** Route=`CelShadedVariant`, VisualMode=`PersistentDefault`, Style=`dispatch_inspired`

## Achados e Causas Raiz
- CityModuleCatalog carregado com 12 modulos.
- RISCO CRITICO ATLAS URP: Tier High=1024 com 18 luzes locais com sombra (88 slices) excede atlas de 2048.
- SSAO DirectLightingStrength=0,25 pode escurecer cavidades iluminadas em estilo cartoon (recomendado <= 0.15).

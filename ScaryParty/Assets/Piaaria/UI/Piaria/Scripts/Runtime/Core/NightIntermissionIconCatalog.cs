using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using ScaryParty.Pizzeria.Domain.Services;

namespace ScaryParty.UI.Piaria.Core
{
    /// <summary>
    /// Definição visual de apresentação para modificadores e itens da preparação entre noites.
    /// Desacoplada das regras puras de domínio (ScaryParty.Pizzeria.Domain).
    /// </summary>
    [Serializable]
    public class ModifierVisualDefinition
    {
        [LabelText("ID do Modificador/Oferta")]
        public string id = string.Empty;

        [LabelText("Título Curto (Opcional)")]
        public string titleOverride = string.Empty;

        [LabelText("Resumo Curto (Opcional)")]
        [TextArea(1, 2)]
        public string shortDescriptionOverride = string.Empty;

        [LabelText("Ícone Local (Sprite)")]
        public Sprite icon;

        [LabelText("Símbolo Fallback")]
        public string fallbackSymbol = "★";

        [LabelText("Tonalidade")]
        public Color tintColor = new Color(1f, 0.45f, 0.12f, 1f);

        [LabelText("Categoria")]
        public RunModifierKind category = RunModifierKind.Hindrance;

        [LabelText("Prioridade de Exibição")]
        public int displayPriority = 10;

        [LabelText("Limite de Linhas"), Range(1, 5)]
        public int maxDescriptionLines = 3;
    }

    [Serializable]
    public class SystemIconEntry
    {
        public string iconKey = string.Empty;
        public Sprite sprite;
        public string fallbackSymbol = "•";
    }

    /// <summary>
    /// Catálogo local de ícones e definições visuais para cards da Intermissão e painel TAB.
    /// Sem dependências de rede em runtime; reaproveita sprites do projeto quando disponíveis.
    /// </summary>
    [CreateAssetMenu(fileName = "NightIntermissionIconCatalog", menuName = "Scary Party/UI/Night Intermission Icon Catalog")]
    public class NightIntermissionIconCatalog : ScriptableObject
    {
        private static NightIntermissionIconCatalog _runtimeFallback;

        public static NightIntermissionIconCatalog ResolveActive()
        {
            var catalog = PiariaUIAssetCatalog.Instance;
            if (catalog != null && catalog.nightIntermissionIconCatalog != null)
            {
                return catalog.nightIntermissionIconCatalog;
            }

#if UNITY_EDITOR
            var editorAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<NightIntermissionIconCatalog>(
                "Assets/Piaaria/UI/Piaria/Config/NightIntermissionIconCatalog.asset");
            if (editorAsset != null) return editorAsset;
#endif
            if (_runtimeFallback == null)
            {
                _runtimeFallback = CreateInstance<NightIntermissionIconCatalog>();
                _runtimeFallback.name = "NightIntermissionIconCatalog (RuntimeDefault)";
                _runtimeFallback.hideFlags = HideFlags.HideAndDontSave;
                _runtimeFallback.EnsureDefaultEntriesPopulated();
            }
            return _runtimeFallback;
        }

        [BoxGroup("Ícones Funcionais do Sistema")]
        [ListDrawerSettings(ShowFoldout = true)]
        public List<SystemIconEntry> systemIcons = new List<SystemIconEntry>();

        [BoxGroup("Definições Visuais de Modificadores e Ofertas")]
        [ListDrawerSettings(ShowFoldout = true)]
        public List<ModifierVisualDefinition> entries = new List<ModifierVisualDefinition>();

        public void EnsureDefaultEntriesPopulated()
        {
            var uiCatalog = PiariaUIAssetCatalog.Instance;

            EnsureSystemIcon("clock", null, "⏱");
            EnsureSystemIcon("cash", uiCatalog != null ? uiCatalog.iconMoneyBill : null, "R$");
            EnsureSystemIcon("coin", uiCatalog != null ? uiCatalog.iconMoneyCoin : null, "$");
            EnsureSystemIcon("orders", uiCatalog != null ? uiCatalog.iconDeliveries : null, "🍕");
            EnsureSystemIcon("trophy", null, "🏆");
            EnsureSystemIcon("alert", null, "⚠");
            EnsureSystemIcon("team", null, "👥");
            EnsureSystemIcon("reputation", null, "★");

            // Imprevistos (Hindrance)
            EnsureModifier("vampire_attack", "🧛", new Color(0.82f, 0.22f, 0.24f, 1f), RunModifierKind.Hindrance, 10);
            EnsureModifier("heavy_rush", "🔥", new Color(0.95f, 0.42f, 0.14f, 1f), RunModifierKind.Hindrance, 20);
            EnsureModifier("impatient_customers", "⏳", new Color(0.88f, 0.32f, 0.18f, 1f), RunModifierKind.Hindrance, 30);
            EnsureModifier("slow_supply", "🚚", new Color(0.76f, 0.36f, 0.16f, 1f), RunModifierKind.Hindrance, 40);
            EnsureModifier("blood_surge", "🩸", new Color(0.75f, 0.14f, 0.22f, 1f), RunModifierKind.Hindrance, 50);

            // Oportunidades (Opportunity)
            EnsureModifier("express_supply", "⚡", new Color(0.20f, 0.65f, 0.32f, 1f), RunModifierKind.Opportunity, 10);
            EnsureModifier("calm_customers", "😊", new Color(0.24f, 0.68f, 0.42f, 1f), RunModifierKind.Opportunity, 20);
            EnsureModifier("calm_shift", "🍃", new Color(0.26f, 0.62f, 0.36f, 1f), RunModifierKind.Opportunity, 30);
            EnsureModifier("focused_staff", "🎯", new Color(0.88f, 0.64f, 0.14f, 1f), RunModifierKind.Opportunity, 40);
            EnsureModifier("quiet_night", "🌙", new Color(0.22f, 0.54f, 0.66f, 1f), RunModifierKind.Opportunity, 50);

            // Eventos adicionais / legados
            EnsureModifier("blood_moon", "🌑", new Color(0.78f, 0.18f, 0.24f, 1f), RunModifierKind.Event, 60);
            EnsureModifier("festival_demand", "🎉", new Color(0.92f, 0.56f, 0.14f, 1f), RunModifierKind.Event, 61);
            EnsureModifier("vampire_prowl", "🦇", new Color(0.62f, 0.20f, 0.36f, 1f), RunModifierKind.Event, 62);
            EnsureModifier("storm_logistics", "⛈", new Color(0.32f, 0.46f, 0.68f, 1f), RunModifierKind.Event, 63);
            EnsureModifier("midnight_surge", "🌘", new Color(0.68f, 0.22f, 0.30f, 1f), RunModifierKind.Event, 64);
            EnsureModifier("restless_patrons", "💢", new Color(0.85f, 0.42f, 0.20f, 1f), RunModifierKind.Event, 65);

            // Ofertas da Loja e Upgrades Permanentes
            EnsureModifier("oven_timer_offer", "⏲", new Color(0.20f, 0.50f, 0.78f, 1f), RunModifierKind.PermanentUpgrade, 70);
            EnsureModifier("oven_timer", "⏲", new Color(0.20f, 0.50f, 0.78f, 1f), RunModifierKind.PermanentUpgrade, 70);
            EnsureModifier("reinforced_bag_offer", "🎒", new Color(0.25f, 0.56f, 0.78f, 1f), RunModifierKind.PermanentUpgrade, 71);
            EnsureModifier("reinforced_bag", "🎒", new Color(0.25f, 0.56f, 0.78f, 1f), RunModifierKind.PermanentUpgrade, 71);
            EnsureModifier("staff_training_offer", "📘", new Color(0.22f, 0.52f, 0.74f, 1f), RunModifierKind.PermanentUpgrade, 72);
            EnsureModifier("staff_training", "📘", new Color(0.22f, 0.52f, 0.74f, 1f), RunModifierKind.PermanentUpgrade, 72);
            EnsureModifier("base_supplies_offer", "📦", new Color(0.32f, 0.62f, 0.30f, 1f), RunModifierKind.PermanentUpgrade, 73);
            EnsureModifier("hire_cashier_offer", "🛎", new Color(0.86f, 0.52f, 0.16f, 1f), RunModifierKind.PermanentUpgrade, 74);
            EnsureModifier("hire_pizzaiolo_offer", "🍕", new Color(0.92f, 0.44f, 0.16f, 1f), RunModifierKind.PermanentUpgrade, 75);
        }

        public ModifierVisualDefinition FindModifierVisual(string id)
        {
            if (entries.Count == 0) EnsureDefaultEntriesPopulated();
            if (!string.IsNullOrEmpty(id))
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    if (entries[i] != null && string.Equals(entries[i].id, id, StringComparison.Ordinal))
                    {
                        return entries[i];
                    }
                }
            }
            return null;
        }

        public SystemIconEntry FindSystemIcon(string key)
        {
            if (systemIcons.Count == 0) EnsureDefaultEntriesPopulated();
            for (int i = 0; i < systemIcons.Count; i++)
            {
                if (systemIcons[i] != null && string.Equals(systemIcons[i].iconKey, key, StringComparison.Ordinal))
                {
                    return systemIcons[i];
                }
            }
            return null;
        }

        public string ResolveSymbol(string id, RunModifierKind fallbackKind)
        {
            var visual = FindModifierVisual(id);
            if (visual != null && !string.IsNullOrEmpty(visual.fallbackSymbol))
                return visual.fallbackSymbol;

            return fallbackKind switch
            {
                RunModifierKind.Hindrance => "⚠",
                RunModifierKind.Opportunity => "★",
                RunModifierKind.PermanentUpgrade => "⚙",
                _ => "•"
            };
        }

        private void EnsureSystemIcon(string key, Sprite sprite, string symbol)
        {
            for (int i = 0; i < systemIcons.Count; i++)
            {
                if (systemIcons[i] != null && string.Equals(systemIcons[i].iconKey, key, StringComparison.Ordinal))
                    return;
            }
            systemIcons.Add(new SystemIconEntry { iconKey = key, sprite = sprite, fallbackSymbol = symbol });
        }

        private void EnsureModifier(string id, string symbol, Color tint, RunModifierKind kind, int priority)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null && string.Equals(entries[i].id, id, StringComparison.Ordinal))
                    return;
            }
            entries.Add(new ModifierVisualDefinition
            {
                id = id,
                fallbackSymbol = symbol,
                tintColor = tint,
                category = kind,
                displayPriority = priority,
                maxDescriptionLines = 3
            });
        }
    }
}

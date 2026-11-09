using System.Collections.Generic;
using UnityEngine;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Presentation
{
    [CreateAssetMenu(fileName = "ItemPresentationCatalog", menuName = "Scary Party/Pizzeria/Item Presentation Catalog")]
    public class ItemPresentationCatalog : ScriptableObject
    {
        public List<ItemPresentationProfile> profiles = new List<ItemPresentationProfile>();

        public ItemPresentationProfile FindProfile(ItemCategory category, int definitionId, bool isTool, bool isBoxed)
        {
            if (isBoxed || category == ItemCategory.Box)
            {
                var boxed = profiles.Find(p => p != null && (p.presentationKey == "PizzaBox" || p.category == ItemCategory.Box));
                if (boxed != null) return boxed;
            }

            if (isTool)
            {
                var tool = profiles.Find(p => p != null && p.isTool && p.definitionId == definitionId);
                if (tool != null) return tool;

                tool = profiles.Find(p => p != null && p.isTool);
                if (tool != null) return tool;
            }

            // Se já virou Pizza montada (ou PizzaBase), usar perfil de Pizza antes de buscar pelo definitionId original da massa
            if (category == ItemCategory.Pizza || category == ItemCategory.PizzaBase)
            {
                var pizzaMatch = profiles.Find(p => p != null && !p.isTool && (p.category == ItemCategory.Pizza || p.presentationKey == "Pizza"));
                if (pizzaMatch != null) return pizzaMatch;
            }

            // Busca por definição exata
            if (definitionId > 0)
            {
                var match = profiles.Find(p => p != null && !p.isTool && p.definitionId == definitionId);
                if (match != null) return match;
            }

            // Busca por categoria
            var catMatch = profiles.Find(p => p != null && !p.isTool && p.category == category);
            if (catMatch != null) return catMatch;

            // Fallback
            if (profiles.Count > 0 && profiles[0] != null) return profiles[0];
            return null;
        }

        public ItemPresentationProfile GetProfileByKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            return profiles.Find(p => p != null && p.presentationKey == key);
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using ScaryParty.Pizzeria.Authoring;

namespace ScaryParty.Pizzeria.Authoring
{
    public enum StationSlotRole
    {
        Workpost = 0,       // Ponto de trabalho (corte, montagem, abertura de massa)
        Holding = 1,        // Ponto de espera / apoio (pratos, pizzas prontas, itens intermediários)
        Storage = 2,        // Ponto de armazenamento (ingredientes em prateleiras, geladeira, dispensa)
        ToolRest = 3,       // Encaixe de suporte de utensílio (faca, ralador)
        Ticket = 4,         // Apresentação de comanda (quadro de tickets)
        PackagingStack = 5, // Pilha vertical de caixas de pizza
        Trash = 6,          // Descarte
        Custom = 7          // Função personalizada
    }

    public enum StationPresetType
    {
        Individual = 0,    // 1 slot de trabalho central
        Line = 1,          // Fileira linear (ex: suporte de utensílios, balcão)
        Grid = 2,          // Grade 2D (ex: bancada 2x2, quadro de tickets)
        Shelves = 3,       // Prateleiras em níveis verticais (geladeira, dispensa)
        VerticalStack = 4, // Pilha empilhada ao longo do eixo Y (caixas de pizza)
        Free = 5           // Composição manual livre
    }

    [Serializable]
    public class StationSlotDefinition
    {
        public int slotIndex;
        public string slotName = "Slot_0";
        public StationSlotRole role = StationSlotRole.Workpost;
        public Vector3 localPosition = Vector3.zero;
        public Vector3 localEulerAngles = Vector3.zero;
        public Vector3 localScale = Vector3.one;

        [Tooltip("Se marcado, este slot foi ajustado manualmente e não será movido quando o preset for recalculado.")]
        public bool isManualOverride = false;

        [Tooltip("Notas ou metadados de autoria deste slot.")]
        public string notes = "";

        public StationSlotDefinition Clone()
        {
            return new StationSlotDefinition
            {
                slotIndex = this.slotIndex,
                slotName = this.slotName,
                role = this.role,
                localPosition = this.localPosition,
                localEulerAngles = this.localEulerAngles,
                localScale = this.localScale,
                isManualOverride = this.isManualOverride,
                notes = this.notes
            };
        }
    }
}

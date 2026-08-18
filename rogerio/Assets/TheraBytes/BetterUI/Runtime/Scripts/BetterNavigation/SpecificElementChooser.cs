using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace TheraBytes.BetterUi
{
    public class SpecificElementChooser : ISelectableChooser
    {
        private readonly Selectable selectable;

        public SpecificElementChooser(Selectable selectable)
        {
            this.selectable = selectable;
        }

        public Selectable ChooseFrom(IEnumerable<Selectable> options, Selectable fallback)
        {
            if(options.Contains(selectable))
            {
                return selectable;
            }
            else
            {
                Debug.LogWarning($"The specified selectable {selectable.name} is not part of the provided options.");
                return fallback;
            }
        }
    }
}

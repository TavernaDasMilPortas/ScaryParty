using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.UI;

namespace TheraBytes.BetterUi
{
    /// <summary>
    /// The TabCollection is a collection of Selectables that represent tabs. It is used for the <see cref="TabSwitchController"/>. It is almost lika a <see cref="SelectableCollection"/> but doesn't auto-select elements"/>
    /// </summary>
    [Serializable]
    public class TabCollection : SelectableCollection
    {
        Selectable currentTab;

        protected override Selectable GetCurrentElement()
        {
            return currentTab;
        }
        protected override void SelectElement(Selectable selectable)
        {
            currentTab = selectable;
        }

        protected override void OnFocus()
        {
            currentTab = initialElement ?? GetInitialElement();
        }
    }
}

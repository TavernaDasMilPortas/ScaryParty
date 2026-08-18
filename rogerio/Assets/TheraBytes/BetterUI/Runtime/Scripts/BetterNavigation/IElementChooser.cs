using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TheraBytes.BetterUi
{
    public interface ISelectableChooser : IElementChooser<Selectable>
    {
    }

    public interface IElementChooser<T>
    {
        T ChooseFrom(IEnumerable<T> options, T fallback);
    }
}

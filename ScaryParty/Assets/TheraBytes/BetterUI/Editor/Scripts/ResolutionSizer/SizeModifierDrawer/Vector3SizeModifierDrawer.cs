using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace TheraBytes.BetterUi.Editor
{ 
    [CustomPropertyDrawer(typeof(Vector3SizeModifier))]
    public class Vector3SizeModifierDrawer : ScreenDependentSizeDrawer<Vector3>
    {

        protected override void DrawModifiers(SerializedProperty property)
        {
            var modx = property.FindPropertyRelative("ModX");
            DrawModifierList(modx, "X Modification");

            var mody = property.FindPropertyRelative("ModY");
            DrawModifierList(mody, "Y Modification");

            var modz = property.FindPropertyRelative("ModZ");
            DrawModifierList(modz, "Z Modification");

        }

        protected override string GetValueString(Vector3 obj)
        {
            return string.Format(CultureInfo.InvariantCulture, "({0:0.##}, {1:0.##}, {2:0.##})", obj.x, obj.y, obj.z);
        }
    }
}

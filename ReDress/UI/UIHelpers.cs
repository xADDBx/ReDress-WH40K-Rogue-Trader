using UnityEngine;

namespace ReDress;
public static class UIHelpers {
    private static Dictionary<Type, Array> m_EnumCache = new();
    private static Dictionary<Type, Dictionary<object, int>> m_IndexToEnumCache = new();
    private static Dictionary<Type, string[]> m_EnumNameCache = new();
    public static bool SelectionGrid<TEnum>(ref TEnum selected, int xCols, Func<TEnum, string>? titler, params GUILayoutOption[] options) where TEnum : Enum {
        if (!m_EnumCache.TryGetValue(typeof(TEnum), out var vals)) {
            vals = Enum.GetValues(typeof(TEnum));
            m_EnumCache[typeof(TEnum)] = vals;
        }
        if (!m_EnumNameCache.TryGetValue(typeof(TEnum), out var names)) {
            Dictionary<object, int> indexToEnum = new();
            List<string> tmpNames = new();
            for (int i = 0; i < vals.Length; i++) {
                string name;
                var val = vals.GetValue(i);
                indexToEnum[val] = i;
                if (titler != null) {
                    name = titler((TEnum)val);
                } else {
                    name = Enum.GetName(typeof(TEnum), val);
                }
                tmpNames.Add(name);
            }
            names = [.. tmpNames];
            m_EnumNameCache[typeof(TEnum)] = names;
            m_IndexToEnumCache[typeof(TEnum)] = indexToEnum;
        }
        if (xCols <= 0) {
            xCols = vals.Length;
        }
        var selectedInt = m_IndexToEnumCache[typeof(TEnum)][selected];
        // Create a copy to not recolour the selected element permanently
        names = [.. names];
        names[selectedInt] = $"<color=orange>{names[selectedInt]}</color>";
        var newSel = GUILayout.SelectionGrid(selectedInt, names, xCols, options);
        bool changed = selectedInt != newSel;
        if (changed) {
            selected = (TEnum)vals.GetValue(newSel);
        }
        return changed;
    }
    public static void DrawDiv() {
        using (new GUILayout.VerticalScope()) {
            GUILayout.Space(10);
        }
        float indent = 0;
        float height = 0;
        float width = 0;
        Color color = new(1f, 1f, 1f, 0.65f);
        Texture2D fillTexture = new(1, 1);
        var divStyle = new GUIStyle {
            fixedHeight = 1,
        };
        fillTexture.SetPixel(0, 0, color);
        fillTexture.Apply();
        divStyle.normal.background = fillTexture;
        if (divStyle.margin == null) {
            divStyle.margin = new RectOffset((int)indent, 0, 4, 4);
        } else {
            divStyle.margin.left = (int)indent + 3;
        }
        if (width > 0)
            divStyle.fixedWidth = width;
        else
            divStyle.fixedWidth = 0;
        GUILayout.Space((2f * height) / 3f);
        GUILayout.Box(GUIContent.none, divStyle);
        GUILayout.Space(height / 3f);
        using (new GUILayout.VerticalScope()) {
            GUILayout.Space(5);
        }
    }
    public static bool IntTextField(ref int value, params GUILayoutOption[] options) {
        var text = GUILayout.TextField(value.ToString(), options);
        if (int.TryParse(text, out var num)) {
            if (num != value) {
                value = num;
                return true;
            }
        }
        return false;
    }
}

using UnityEngine;
using UnityModManagerNet;

namespace ReDress; 
public static class UIHelpers {
    public static bool PressedEnterInControl(string controlName) {
        Event e = Event.current;

        if (e.type == EventType.KeyUp && e.keyCode == KeyCode.Return && GUI.GetNameOfFocusedControl() == controlName) {
            e.Use();
            return true;
        }
        return false;
    }
    public static bool ActionTextField(ref string content, string name, Action<(string oldContent, string newContent)>? onContentChanged, Action<string>? onEnterPressed, params GUILayoutOption[] options) {
        bool hasChanged = false;
        if (name != null) {
            GUI.SetNextControlName(name);
        }
        hasChanged = TextField(ref content, onContentChanged, options);
        if (name != null && onEnterPressed != null && PressedEnterInControl(name)) {
            onEnterPressed(content);
        }
        return hasChanged;
    }
    public static bool Button(string? title = null, Action? onPressed = null, GUIStyle? style = null, params GUILayoutOption[] options) {
        options = options.Length == 0 ? [AutoWidth()] : options;
        bool pressed = false;
        if (GUILayout.Button(title ?? "", style ?? GUI.skin.button, options)) {
            onPressed?.Invoke();
            pressed = true;
        }
        return pressed;
    }
    public static void Label(string? title = null, params GUILayoutOption[] options) {
        options = options.Length == 0 ? [AutoWidth()] : options;
        GUILayout.Label(title ?? "", options);
    }
    public static string Color(this string s, string color) => $"<color={color}>{s}</color>";
    public static string Bold(this string s) => $"<b>{s}</b>";
    public static string Cyan(this string s) => s.Color("cyan");
    public static string Green(this string s) => s.Color("#00ff00ff");
    public static string Blue(this string s) => s.Color("blue");
    public static string Red(this string s) => s.Color("#C04040E0");
    public static string Orange(this string s) => s.Color("orange");
    public static void Space(float pixels) => GUILayout.Space(pixels);
    public static GUILayoutOption Width(float width) => GUILayout.Width(width);
    public static GUILayout.HorizontalScope HorizontalScope(params GUILayoutOption[] options) => new(options);
    public static GUILayout.VerticalScope VerticalScope(float width) => new(GUILayout.Width(width));
    public static GUILayout.VerticalScope VerticalScope(params GUILayoutOption[] options) => new(options);
    public static GUILayoutOption AutoWidth() => AutoWidth();
    public static float EffectiveWindowWidth() => 0.98f * UnityModManager.Params.WindowWidth;
    public static float CalculateLargestLabelSize(IEnumerable<string> items, GUIStyle? style = null) {
        style ??= GUI.skin.label;
        if (!items.Any()) {
            return 1f;
        }
        return items.Max(item => style.CalcSize(new(item)).x);
    }
    private static GUIStyle m_DisclosureToggleStyle {
        get {
            field ??= new GUIStyle(GUI.skin.label) { imagePosition = ImagePosition.ImageLeft, alignment = TextAnchor.MiddleLeft };
            return field;
        }
    }
    public static string DisclosureOn = "▼";
    public static string DisclosureOff = "▶";
    public static string Edit = "✎";
    public static string CheckOn = "✔";
    public static string CheckOff = "✖";
    private static Dictionary<Type, Array> m_EnumCache = new();
    private static Dictionary<Type, Dictionary<object, int>> m_IndexToEnumCache = new();
    private static Dictionary<Type, string[]> m_EnumNameCache = new(); public static bool DisclosureToggle(ref bool state, string? name = null, params GUILayoutOption[] options) {
        options = options.Length == 0 ? [AutoWidth()] : options;
        string glyph = state ? DisclosureOn : DisclosureOff;
        var newValue = GUILayout.Toggle(state, glyph + (name ?? ""), m_DisclosureToggleStyle, options);
        if (newValue != state) {
            state = newValue;
            return true;
        } else {
            return false;
        }
    }
    public static bool TextField(ref string content, Action<(string oldContent, string newContent)>? onContentChanged, params GUILayoutOption[] options) {
        options = options.Length == 0 ? [AutoWidth(), GUILayout.Width(600)] : options;
        bool hasChanged = false;
        var oldContent = content;
        var newText = GUILayout.TextField(oldContent, options);
        if (newText != oldContent) {
            content = newText;
            onContentChanged?.Invoke((oldContent, content));
            hasChanged = true;

        }
        return hasChanged;
    }
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
        using (VerticalScope()) {
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
        using (VerticalScope()) {
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

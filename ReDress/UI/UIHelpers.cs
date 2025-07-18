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
    public static GUILayoutOption AutoWidth() => GUILayout.ExpandWidth(false);
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
    private static GUIStyle? m_DivGUIStyle;
    public static void DrawDiv() {
        using (VerticalScope()) {
            GUILayout.Space(5);
            if (m_DivGUIStyle == null) {
                m_DivGUIStyle = new GUIStyle {
                    fixedHeight = 1,
                };
                Color color = new(1f, 1f, 1f, 0.65f);
                var divFillTexture = new Texture2D(1, 1);
                divFillTexture.SetPixel(0, 0, color);
                divFillTexture.Apply();
                m_DivGUIStyle.normal.background = divFillTexture;
                if (m_DivGUIStyle.margin == null) {
                    m_DivGUIStyle.margin = new RectOffset(0, 0, 4, 4);
                } else {
                    m_DivGUIStyle.margin.left = 3;
                }
                m_DivGUIStyle.fixedWidth = 0;
            }
            GUILayout.Box(GUIContent.none, m_DivGUIStyle);
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

    private static GUIStyle LinkStyle {
        get {
            if (field == null) {
                field = new GUIStyle(GUI.skin.label) {
                    wordWrap = false,
                    normal = { textColor = new Color(0f, 0.75f, 1f) },
                    hover = { textColor = new Color(0.2f, 0.85f, 1f) },
                    margin = { left = 0, right = 0, top = 0, bottom = 0 },
                    padding = new RectOffset(0, 0, 0, 0),
                };
                field.stretchWidth = false;
            }
            return field;
        }
    }

    public static void LinkButton(string title, string url) {
        using (HorizontalScope()) {
            Space(4);
            Rect rect = GUILayoutUtility.GetRect(new GUIContent(title), LinkStyle, GUILayout.ExpandWidth(false));

            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition)) {
                Application.OpenURL(url);
                Event.current.Use();
            }

            GUI.Label(rect, title, LinkStyle);

            var underlineThickness = 1f;
            var underlineY = rect.yMax - underlineThickness / 2f + 2f;
            var underlineRect = new Rect(rect.x, underlineY, rect.width, underlineThickness);

            GUI.DrawTexture(underlineRect, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, LinkStyle.normal.textColor, 0f, 0f);
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace ReDress;
public static class UIHelpers {
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

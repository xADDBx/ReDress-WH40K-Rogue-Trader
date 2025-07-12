using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ReDress.EntityPartStorage;
using UnityEngine;
using static ReDress.UIHelpers;

namespace ReDress;
public static class CustomColors {
    internal static CustomColorTex colorPicker1 = null;
    internal static CustomColorTex colorPicker2 = null;
    internal static CustomColorTex colorPicker3 = null;
    internal static int colorPicker1col = 0;
    internal static int colorPicker2col = 0;
    internal static int colorPicker3col = 0;
    public static int height1 = 1;
    public static int width1 = 1;
    public static int height2 = 1;
    public static int width2 = 1;
    public static int height3 = 1;
    public static int width3 = 1;
    public static bool doClamp = true;
    public static bool doRepeat = false;
    public static bool ColorPickerGUI(int ColorPicker) {
        CustomColor current;
        if (ColorPicker == 1) {
            ColorPickerGrid(ref colorPicker1, ref colorPicker1col, ref height1, ref width1);
            current = colorPicker1.colors[colorPicker1col];
        } else if (ColorPicker == 2) {
            ColorPickerGrid(ref colorPicker2, ref colorPicker2col, ref height2, ref width2);
            current = colorPicker2.colors[colorPicker2col];
        } else {
            ColorPickerGrid(ref colorPicker3, ref colorPicker3col, ref height3, ref width3);
            current = colorPicker3.colors[colorPicker3col];
        }
        using (new GUILayout.HorizontalScope()) {
            GUILayout.Space(20);
            using (new GUILayout.VerticalScope()) {
                GUILayout.Label("Custom RGB Color Picker");

                GUILayout.Label("Red: " + Mathf.RoundToInt(current.R * 255), GUILayout.ExpandWidth(false));
                current.R = GUILayout.HorizontalSlider(current.R, 0f, 1f, GUILayout.Width(500));

                GUILayout.Label("Green: " + Mathf.RoundToInt(current.G * 255), GUILayout.ExpandWidth(false));
                current.G = GUILayout.HorizontalSlider(current.G, 0f, 1f, GUILayout.Width(500));

                GUILayout.Label("Blue: " + Mathf.RoundToInt(current.B * 255), GUILayout.ExpandWidth(false));
                current.B = GUILayout.HorizontalSlider(current.B, 0f, 1f, GUILayout.Width(500));

                /*
                GUILayout.Label("Picked Color");
                GUIStyle colorStyle = new GUIStyle(GUI.skin.box);
                colorStyle.normal.background = MakeTex(2, 2, current);
                GUILayout.Box(GUIContent.none, colorStyle, GUILayout.Width(100), GUILayout.Height(100));
                */
                if (GUILayout.Button("Apply Custom Color", GUILayout.ExpandWidth(false))) {
                    return true;
                }
                return false;
            }
        }
    }
    public static void ColorPickerGrid(ref CustomColorTex customColors, ref int customColorIndex, ref int height, ref int width) {
        bool changedSize = false;
        using (new GUILayout.HorizontalScope()) {
            GUILayout.Label("Texture Height: ", GUILayout.ExpandWidth(false));
            changedSize |= IntTextField(ref height, GUILayout.MinWidth(60), GUILayout.ExpandWidth(false));
        }
        using (new GUILayout.HorizontalScope()) {
            GUILayout.Label("Texture Width: ", GUILayout.ExpandWidth(false));
            changedSize |= IntTextField(ref width, GUILayout.MinWidth(60), GUILayout.ExpandWidth(false));
        }
        if (changedSize) {
            height = Math.Max(1, height);
            width = Math.Max(1, width);
            customColorIndex = Math.Min(customColorIndex, height * width - 1);
        }
        using (new GUILayout.HorizontalScope()) {
            bool changed = false;
            if (GUILayout.Toggle(doClamp, "Clamp Texture", GUILayout.ExpandWidth(false))) {
                doClamp = true;
                doRepeat = false;
                changed = true;
            }
            if (GUILayout.Toggle(doRepeat, "Repeat Texture", GUILayout.ExpandWidth(false))) {
                doClamp = false;
                doRepeat = true;
                changed = true;
            }
            if (changed) {
                customColors.wrapMode = doClamp ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            }
        }
        customColors.height = height;
        customColors.width = width;
        customColors.colors ??= new();
        while (customColors.colors.Count < width * height) {
            customColors.colors.Add(new());
        }
        while (customColors.colors.Count > width * height) {
            customColors.colors.Remove(customColors.colors.Last());
        }
        using (new GUILayout.VerticalScope()) {
            for (int i = 0; i < height; i++) {
                using (new GUILayout.HorizontalScope()) {
                    GUILayout.Space(20);
                    for (int j = 0; j < width; j++) {
                        GUIStyle colorStyle = new GUIStyle(GUI.skin.box);
                        int ind = i * width + j;
                        colorStyle.normal.background = customColors.colors[ind].MakeBoxTex();
                        int size = customColorIndex == ind ? 60 : 50;
                        if (GUILayout.Button(GUIContent.none, colorStyle, GUILayout.Width(size), GUILayout.Height(size))) {
                            customColorIndex = ind;
                        }
                    }
                }
            }
        }
    }
}

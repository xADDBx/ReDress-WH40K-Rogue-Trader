using static ReDress.EntityPartStorage;
using UnityEngine;
using static ReDress.UIHelpers;
using Kingmaker.Utility.DotNetExtensions;
using HarmonyLib;

namespace ReDress;
public class CustomTexCreator {
    private CustomColorTex m_CurrentTex = new(TextureWrapMode.Clamp);
    public string EEName;
    private int m_CurrentlyColorIdx = 0;
    private int m_Height = 1;
    private int m_Width = 1;
    public CustomTexCreator(string eeName, CustomColorTex? customTexture = null) {
        m_CurrentTex = customTexture ?? new(TextureWrapMode.Clamp);
        m_Height = m_CurrentTex.height;
        m_Width = m_CurrentTex.width;
        EEName = eeName;
        RefitTexture();
    }
    public CustomColorTex GetTexCopy() {
        return AccessTools.MakeDeepCopy<CustomColorTex>(m_CurrentTex);
    }
    internal CustomColor CurrentColor {
        get {
            return m_CurrentTex.colors[m_CurrentlyColorIdx];
        }
    }
    internal int AmountColors {
        get {
            return m_Width * m_Height;
        }
    }
    private int GetIdx(int row, int col) {
        return row * m_Width + col;
    }
    public bool ColorPickerGUI() {
        ColorPickerGrid();
        using (new GUILayout.HorizontalScope()) {
            GUILayout.Space(20);
            using (new GUILayout.VerticalScope()) {
                GUILayout.Label("Custom RGB Color Picker");

                GUILayout.Label("Red: " + Mathf.RoundToInt(CurrentColor.R * 255), GUILayout.ExpandWidth(false));
                CurrentColor.R = GUILayout.HorizontalSlider(CurrentColor.R, 0f, 1f, GUILayout.Width(500));

                GUILayout.Label("Green: " + Mathf.RoundToInt(CurrentColor.G * 255), GUILayout.ExpandWidth(false));
                CurrentColor.G = GUILayout.HorizontalSlider(CurrentColor.G, 0f, 1f, GUILayout.Width(500));

                GUILayout.Label("Blue: " + Mathf.RoundToInt(CurrentColor.B * 255), GUILayout.ExpandWidth(false));
                CurrentColor.B = GUILayout.HorizontalSlider(CurrentColor.B, 0f, 1f, GUILayout.Width(500));

                /*
                GUILayout.Label("Picked Color");
                GUIStyle colorStyle = new GUIStyle(GUI.skin.box);
                colorStyle.normal.background = MakeTex(2, 2, current);
                GUILayout.Box(GUIContent.none, colorStyle, GUILayout.Width(100), GUILayout.Height(100));
                */
                if (GUILayout.Button("Apply Custom Texture", GUILayout.ExpandWidth(false))) {
                    return true;
                }
                return false;
            }
        }
    }
    private void RefitTexture() {
        m_CurrentTex.height = m_Height;
        m_CurrentTex.width = m_Width;
        m_CurrentTex.colors ??= new();
        while (m_CurrentTex.colors.Count < AmountColors) {
            m_CurrentTex.colors.Add(new());
        }
        while (m_CurrentTex.colors.Count > AmountColors) {
            m_CurrentTex.colors.RemoveLast();
        }
        m_CurrentlyColorIdx = Math.Min(m_CurrentlyColorIdx, AmountColors - 1);
    }
    public void ColorPickerGrid() {
        bool changedSize = false;
        using (new GUILayout.HorizontalScope()) {
            GUILayout.Label("Texture Height: ", GUILayout.ExpandWidth(false));
            changedSize |= IntTextField(ref m_Height, GUILayout.MinWidth(60), GUILayout.ExpandWidth(false));
        }
        using (new GUILayout.HorizontalScope()) {
            GUILayout.Label("Texture Width: ", GUILayout.ExpandWidth(false));
            changedSize |= IntTextField(ref m_Width, GUILayout.MinWidth(60), GUILayout.ExpandWidth(false));
        }
        if (changedSize) {
            m_Height = Math.Max(1, m_Height);
            m_Width = Math.Max(1, m_Width);
            RefitTexture();
        }
        using (new GUILayout.HorizontalScope()) {
            GUILayout.Label("Texture Wrap Mode (What happens at the edges of the texture)", GUILayout.ExpandWidth(false));
            GUILayout.Space(20);
            if (SelectionGrid(ref m_CurrentTex.wrapMode, 4, null, GUILayout.ExpandWidth(false))) {

            }
        }
        using (new GUILayout.VerticalScope()) {
            for (int i = 0; i < m_Height; i++) {
                using (new GUILayout.HorizontalScope()) {
                    GUILayout.Space(20);
                    for (int j = 0; j < m_Width; j++) {
                        GUIStyle colorStyle = new GUIStyle(GUI.skin.box);
                        int ind = GetIdx(i, j);
                        colorStyle.normal.background = m_CurrentTex.colors[ind].MakeBoxTex();
                        int size = m_CurrentlyColorIdx == ind ? 60 : 50;
                        if (GUILayout.Button(GUIContent.none, colorStyle, GUILayout.Width(size), GUILayout.Height(size))) {
                            m_CurrentlyColorIdx = ind;
                        }
                    }
                }
            }
        }
    }
}

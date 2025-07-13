using static ReDress.EntityPartStorage;
using UnityEngine;
using static ReDress.UIHelpers;
using Kingmaker.Utility.DotNetExtensions;
using HarmonyLib;

namespace ReDress;
public class CustomTexCreator {
    private string m_ColorName = "";
    private static Browser<CustomColor>? m_CustomColorPresetBrowser;
    private static Browser<CustomColorTex>? m_CustomColorTexPresetBrowser;
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
        using (HorizontalScope()) {
            using (VerticalScope()) {
                bool active = m_CustomColorPresetBrowser != null;
                if (GUILayout.Toggle(active, "Show Color Presets")) {
                    m_CustomColorPresetBrowser ??= new(c => c.ToString(), c => c.ToString(), CustomColorPresets.CustomColors, showDivBetweenItems: false);
                } else {
                    m_CustomColorPresetBrowser = null;
                }
                if (m_CustomColorPresetBrowser != null) {
                    m_CustomColorPresetBrowser.OnGUI(color => {
                        using (HorizontalScope()) {
                            GUILayout.Label((color.Name ?? "<null>").Green(), GUILayout.ExpandWidth(false));
                            GUILayout.Space(20);
                            GUILayout.Label(color.ToString().Cyan(), GUILayout.ExpandWidth(false));
                            GUILayout.Space(20);
                            if (GUILayout.Button("Load".Green())) {
                                CurrentColor.Become(color);
                            }
                            GUILayout.Space(10);
                            if (GUILayout.Button("Remove".Red())) {
                                CustomColorPresets.CustomColors.Remove(color);
                                CustomColorPresets.Save();
                                m_CustomColorPresetBrowser.QueueUpdateItems(CustomColorPresets.CustomColors);
                            }
                        }
                    });
                    using (HorizontalScope()) {
                        GUILayout.Label("Name: ", AutoWidth());
                        GUILayout.Space(10);
                        TextField(ref m_ColorName, null, GUILayout.MinWidth(50), GUILayout.MaxWidth(300), AutoWidth());
                        GUILayout.Space(10); 
                        if (!string.IsNullOrWhiteSpace(m_ColorName)) {
                            if (GUILayout.Button("Save As Preset".Cyan(), AutoWidth())) {
                                var c = AccessTools.MakeDeepCopy<CustomColor>(CurrentColor);
                                c.Name = m_ColorName;
                                CustomColorPresets.CustomColors.Add(c);
                                CustomColorPresets.Save();
                                m_CustomColorPresetBrowser?.QueueUpdateItems(CustomColorPresets.CustomColors);
                            }
                        } else {
                            GUILayout.Label("Enter a name first!".Red(), AutoWidth());
                        }
                    }
                }
                GUILayout.Label("Red: ".Red() + Mathf.RoundToInt(CurrentColor.R * 255), AutoWidth());
                CurrentColor.R = GUILayout.HorizontalSlider(CurrentColor.R, 0f, 1f, Width(500));

                GUILayout.Label("Green: ".Green() + Mathf.RoundToInt(CurrentColor.G * 255), AutoWidth());
                CurrentColor.G = GUILayout.HorizontalSlider(CurrentColor.G, 0f, 1f, Width(500));

                GUILayout.Label("Blue: ".Blue() + Mathf.RoundToInt(CurrentColor.B * 255), AutoWidth());
                CurrentColor.B = GUILayout.HorizontalSlider(CurrentColor.B, 0f, 1f, Width(500));

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
        m_CurrentTex.colors ??= [];
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
        using (HorizontalScope()) {
            GUILayout.Label("Texture Height: ", GUILayout.ExpandWidth(false));
            changedSize |= IntTextField(ref m_Height, GUILayout.MinWidth(60), GUILayout.ExpandWidth(false));
        }
        using (HorizontalScope()) {
            GUILayout.Label("Texture Width: ", GUILayout.ExpandWidth(false));
            changedSize |= IntTextField(ref m_Width, GUILayout.MinWidth(60), GUILayout.ExpandWidth(false));
        }
        if (changedSize) {
            m_Height = Math.Max(1, m_Height);
            m_Width = Math.Max(1, m_Width);
            RefitTexture();
        }
        using (HorizontalScope()) {
            GUILayout.Label("Texture Wrap Mode (What happens at the edges of the texture)", GUILayout.ExpandWidth(false));
            GUILayout.Space(20);
            if (SelectionGrid(ref m_CurrentTex.wrapMode, 4, null, GUILayout.ExpandWidth(false))) {

            }
        }
        using (VerticalScope()) {
            for (int i = 0; i < m_Height; i++) {
                using (HorizontalScope()) {
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

using Kingmaker.EntitySystem.Persistence;
using Kingmaker;
using Newtonsoft.Json;
using Kingmaker.EntitySystem.Entities.Base;
using Kingmaker.Visual.CharacterSystem;
using UnityEngine;
using Kingmaker.Blueprints;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.UI.Common;

namespace ReDress {
    public static class EntityPartStorage {
        public class CustomColor {
            [JsonIgnore]
            public static Texture2D? CachedTex;
            [JsonProperty]
            public string? Name;
            [JsonProperty]
            public float R;
            [JsonProperty]
            public float G;
            [JsonProperty]
            public float B;

            public void Become(CustomColor c) {
                Name = c.Name;
                R = c.R;
                G = c.G;
                B = c.B;
            }
            public static implicit operator Color(CustomColor c) {
                return new Color(c.R, c.G, c.B);
            }
            public override string ToString() {
                return $"R: {Mathf.RoundToInt(R * 255)}, G: {Mathf.RoundToInt(G * 255)}, B: {Mathf.RoundToInt(B * 255)}";
            }
            public Texture2D MakeBoxTex() {
                if (CachedTex == null) {
                    CachedTex = new(1, 1, textureFormat: TextureFormat.RGBA32, 1, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                }
                CachedTex.SetPixels([this]);
                CachedTex.Apply();
                return CachedTex;
            }
        }
        public class CustomColorTex {
            [JsonIgnore]
            public static Dictionary<(int, int), Texture2D>? CachedTextures;
            [JsonProperty]
            public int height = 1;
            [JsonProperty]
            public int width = 1;
            [JsonProperty]
            public List<CustomColor> colors;
            [JsonProperty]
            public TextureWrapMode wrapMode = TextureWrapMode.Clamp;
#pragma warning disable CS8618 // Constructor exists only for Serializer
            public CustomColorTex() { }
#pragma warning restore CS8618
            public CustomColorTex(TextureWrapMode wrapMode) {
                colors = [new() { B = 0, G = 0, R = 0 }];
                this.wrapMode = wrapMode;
            }
            public CustomColorTex(CustomColor c) {
                colors = [c];
            }
            public Texture2D MakeTex() {
                CachedTextures ??= new();
                if (!CachedTextures.TryGetValue((width, height), out var CachedTex) || CachedTex == null) {
                    CachedTex = new Texture2D(width, height, textureFormat: TextureFormat.RGBA32, 1, false) { filterMode = FilterMode.Bilinear };
                    CachedTextures[(width, height)] = CachedTex;
                }
                Color[] pix = new Color[width * height];
                for (int i = 0; i < pix.Length; i++) {
                    pix[i] = colors[i];
                }
                CachedTex.wrapMode = wrapMode;
                CachedTex.SetPixels(pix);
                // Maybe result.Compress() if size > 1x1?
                CachedTex.Apply();
                return CachedTex;
            }
            public override string ToString() {
                return $"{height}x{width} Texture with {wrapMode} mode.";
            }
        }
        public class PerSaveSettings : EntityPart {
            public const string ID = "ReDress.PerSaveSettings";
            [JsonProperty]
            public Dictionary<string, List<string>> AddClothes = new();
            [JsonProperty]
            public Dictionary<string, HashSet<string>> ExcludeByName = new();
            [JsonProperty]
            public Dictionary<string, HashSet<string>> IncludeByName = new();
            [JsonProperty]
            public Dictionary<string, Dictionary<string, RampColorPreset.IndexSet>> RampOverrideByName = new();
            [Obsolete]
            [JsonProperty]
            public Dictionary<string, Dictionary<string, (CustomColor, CustomColor)>>? CustomColorByName = new();
            // The first two are Primary and Secondary overrides. The third one is a placeholder fo rmain tex. What are the other 3 for?
            [JsonProperty]
            public Dictionary<string, Dictionary<string, (CustomColorTex?, CustomColorTex?, CustomColorTex?, CustomColorTex?, CustomColorTex?, CustomColorTex?)>> CustomColorsByName = new();
            [JsonProperty]
            public Dictionary<string, bool> NakedFlag = new();
        }
        private static PerSaveSettings? m_CachedPerSave = null;
        public static void ClearCachedPerSave() => m_CachedPerSave = null;
        public static void ReloadPerSaveSettings() {
            if (Game.Instance?.Player == null || Game.Instance.SaveManager.CurrentState == SaveManager.State.Loading) return;
            if (Game.Instance.State.InGameSettings.List.TryGetValue(PerSaveSettings.ID, out var obj) && obj is string json) {
                try {
                    m_CachedPerSave = JsonConvert.DeserializeObject<PerSaveSettings>(json);
                } catch (Exception e) {
                    Main.Log.Log(e.ToString());
                }
            }
            if (m_CachedPerSave == null) {
                m_CachedPerSave = new PerSaveSettings();
                SavePerSaveSettings(false);
            } else {
#pragma warning disable CS0612 // Type or member is obsolete
                if (m_CachedPerSave.CustomColorByName != null) {
                    foreach (var charEntry in m_CachedPerSave.CustomColorByName) {
                        m_CachedPerSave.CustomColorsByName[charEntry.Key] = new();
                        foreach (var itemEntry in charEntry.Value) {
                            m_CachedPerSave.CustomColorsByName[charEntry.Key][itemEntry.Key] = (new(itemEntry.Value.Item1), new(itemEntry.Value.Item2), null, null, null, null);
                        }
                    }
                    m_CachedPerSave.CustomColorByName = null;
                    SavePerSaveSettings(false);
                }
#pragma warning restore CS0612 // Type or member is obsolete
            }
        }
        public static void SavePerSaveSettings(bool reloadCharacterClothing = true) {
            if (Game.Instance?.Player == null) return;
            if (m_CachedPerSave == null) ReloadPerSaveSettings();
            var json = JsonConvert.SerializeObject(m_CachedPerSave);
            Game.Instance.State.InGameSettings.List[PerSaveSettings.ID] = json;
            try {
                if (reloadCharacterClothing) {
                    var polymorphBuff = ResourcesLibrary.BlueprintsCache.Load("b5fe711b0755440093599873b4b4caf6") as BlueprintBuff;
                    Main.PickedUnit!.Buffs.Add(polymorphBuff);
                    Main.PickedUnit.Buffs.Remove(polymorphBuff);
                    var unit = UIDollRooms.Instance?.CharacterDollRoom?.Unit;
                    if (unit != null) {
                        unit.Buffs.Add(polymorphBuff);
                        unit.Buffs.Remove(polymorphBuff);
                        UIDollRooms.Instance?.CharacterDollRoom.Cleanup();
                        UIDollRooms.Instance?.CharacterDollRoom?.SetupUnit(unit);
                    }
                }
            } catch (Exception ex) {
                Main.Log.Log(ex.ToString());
            }
        }
        public static PerSaveSettings perSave {
            get {
                try {
                    if (m_CachedPerSave != null) {
                        return m_CachedPerSave;
                    }
                    ReloadPerSaveSettings();
                } catch (Exception e) {
                    Main.Log.Log(e.ToString());
                }
                return m_CachedPerSave!;
            }
        }
    }
}

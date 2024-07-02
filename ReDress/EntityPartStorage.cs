using Kingmaker.EntitySystem.Persistence;
using Kingmaker;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ReDress.Main;
using Kingmaker.EntitySystem.Entities.Base;
using Kingmaker.Visual.CharacterSystem;
using UnityEngine;

namespace ReDress {
    public static class EntityPartStorage {
        public class CustomColor {
            public static Dictionary<(float, float, float), Texture2D> TextureCache = new();
            public float R;
            public float G;
            public float B;

            public static implicit operator UnityEngine.Color(CustomColor c) {
                return new UnityEngine.Color(c.R, c.G, c.B);
            }
            public override string ToString() {
                return $"R: {Mathf.RoundToInt(R * 255)}, G: {Mathf.RoundToInt(G * 255)}, B: {Mathf.RoundToInt(B * 255)}";
            }
            public Texture2D MakeBoxTex() {
                if (TextureCache.TryGetValue((R, G, B), out Texture2D tex)) {
                    return tex;
                } else {
                    Texture2D result = new Texture2D(1, 1, textureFormat: TextureFormat.RGBA32, 1, false) { filterMode = FilterMode.Bilinear };
                    result.wrapMode = TextureWrapMode.Clamp;
                    result.SetPixels([this]);
                    result.Apply(false, true);
                    TextureCache.Add((R, G, B), result);
                    return result;
                }
            }
        }
        public class CustomColorTex {
            public static Dictionary<CustomColorTex, Texture2D> TextureCache = new();
            public int height = 1;
            public int width = 1;
            public List<CustomColor> colors;
            public TextureWrapMode wrapMode = TextureWrapMode.Clamp;
            public CustomColorTex() {

            }
            public CustomColorTex(TextureWrapMode wrapMode) {
                colors = [];
                this.wrapMode = wrapMode;
            }
            public CustomColorTex(CustomColor c) {
                colors = [c];
            }
            public Texture2D MakeTex() {
                var MaybeTex = TextureCache.Keys.FirstOrDefault(c => {
                    bool b = c.height == height && c.width == width && c.wrapMode == wrapMode;
                    if (!b) return b;
                    for (int i = 0; i < height * width; i++) {
                        b &= c.colors[i].R == colors[i].R && c.colors[i].G == colors[i].G && c.colors[i].B == colors[i].B;
                    }
                    return b;
                });
                if (MaybeTex != null) return TextureCache[MaybeTex];
                Color[] pix = new Color[width * height];
                for (int i = 0; i < pix.Length; i++) {
                    pix[i] = colors[i];
                }
                Texture2D result = new Texture2D(width, height, textureFormat: TextureFormat.RGBA32, 1, false) { filterMode = FilterMode.Bilinear };
                result.wrapMode = wrapMode;
                result.SetPixels(pix);
                // Maybe result.Compress() if size > 1x1?
                TextureCache.Add(this, result);
                result.Apply(false, true);
                return result;
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
            public Dictionary<string, Dictionary<string, (CustomColor, CustomColor)>> CustomColorByName = new();
            [JsonProperty]
            public Dictionary<string, Dictionary<string, (CustomColorTex, CustomColorTex)>> CustomColorsByName = new();
            [JsonProperty]
            public Dictionary<string, bool> NakedFlag = new();
        }
        private static PerSaveSettings cachedPerSave = null;
        public static void ClearCachedPerSave() => cachedPerSave = null;
        public static void ReloadPerSaveSettings() {
            var player = Game.Instance?.Player;
            if (player == null || Game.Instance.SaveManager.CurrentState == SaveManager.State.Loading) return;
            if (Game.Instance.State.InGameSettings.List.TryGetValue(PerSaveSettings.ID, out var obj) && obj is string json) {
                try {
                    cachedPerSave = JsonConvert.DeserializeObject<PerSaveSettings>(json);
                } catch (Exception e) {
                    log.Log(e.ToString());
                }
            }
            if (cachedPerSave == null) {
                cachedPerSave = new PerSaveSettings();
                SavePerSaveSettings();
            } else {
#pragma warning disable CS0612 // Type or member is obsolete
                if (cachedPerSave.CustomColorByName != null) {
                    foreach (var charEntry in cachedPerSave.CustomColorByName) {
                        cachedPerSave.CustomColorsByName[charEntry.Key] = new();
                        foreach (var itemEntry in charEntry.Value) {
                            cachedPerSave.CustomColorsByName[charEntry.Key][itemEntry.Key] = (new(itemEntry.Value.Item1), new(itemEntry.Value.Item2));
                        }
                    }
                    cachedPerSave.CustomColorByName = null;
                }
#pragma warning restore CS0612 // Type or member is obsolete
            }
        }
        public static void SavePerSaveSettings() {
            var player = Game.Instance?.Player;
            if (player == null) return;
            if (cachedPerSave == null) ReloadPerSaveSettings();
            var json = JsonConvert.SerializeObject(cachedPerSave);
            Game.Instance.State.InGameSettings.List[PerSaveSettings.ID] = json;
        }
        public static PerSaveSettings perSave {
            get {
                try {
                    if (cachedPerSave != null) return cachedPerSave;
                    ReloadPerSaveSettings();
                } catch (Exception e) {
                    log.Log(e.ToString());
                }
                return cachedPerSave;
            }
        }
    }
}

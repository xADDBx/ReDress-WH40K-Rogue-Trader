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
using Kingmaker.Blueprints;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.UI.Common;

namespace ReDress {
    public static class EntityPartStorage {
        public class CustomColor {
            [JsonIgnore]
            public static Texture2D CachedTex;
            [JsonProperty]
            public float R;
            [JsonProperty]
            public float G;
            [JsonProperty]
            public float B;

            public static implicit operator UnityEngine.Color(CustomColor c) {
                return new UnityEngine.Color(c.R, c.G, c.B);
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
            public static Dictionary<(int, int), Texture2D> CachedTextures;
            [JsonProperty]
            public int height = 1;
            [JsonProperty]
            public int width = 1;
            [JsonProperty]
            public List<CustomColor> colors;
            [JsonProperty]
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
            public Dictionary<string, Dictionary<string, (CustomColor, CustomColor)>> CustomColorByName = new();
            [JsonProperty]
            public Dictionary<string, Dictionary<string, (CustomColorTex, CustomColorTex, CustomColorTex, CustomColorTex, CustomColorTex, CustomColorTex)>> CustomColorsByName = new();
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
                SavePerSaveSettings(false);
            } else {
#pragma warning disable CS0612 // Type or member is obsolete
                if (cachedPerSave.CustomColorByName != null) {
                    foreach (var charEntry in cachedPerSave.CustomColorByName) {
                        cachedPerSave.CustomColorsByName[charEntry.Key] = new();
                        foreach (var itemEntry in charEntry.Value) {
                            cachedPerSave.CustomColorsByName[charEntry.Key][itemEntry.Key] = (new(itemEntry.Value.Item1), new(itemEntry.Value.Item2), null, null, null, null);
                        }
                    }
                    cachedPerSave.CustomColorByName = null;
                    SavePerSaveSettings(false);
                }
#pragma warning restore CS0612 // Type or member is obsolete
            }
        }
        public static void SavePerSaveSettings(bool reloadCharacterClothing = true) {
            var player = Game.Instance?.Player;
            if (player == null) return;
            if (cachedPerSave == null) ReloadPerSaveSettings();
            var json = JsonConvert.SerializeObject(cachedPerSave);
            Game.Instance.State.InGameSettings.List[PerSaveSettings.ID] = json;
            try {
                if (reloadCharacterClothing) {
                    var polymorphBuff = ResourcesLibrary.BlueprintsCache.Load("b5fe711b0755440093599873b4b4caf6") as BlueprintBuff;
                    Main.pickedUnit.Buffs.Add(polymorphBuff);
                    Main.pickedUnit.Buffs.Remove(polymorphBuff);
                    var unit = UIDollRooms.Instance?.CharacterDollRoom?.Unit;
                    if (unit != null) {
                        unit.Buffs.Add(polymorphBuff);
                        unit.Buffs.Remove(polymorphBuff);
                        UIDollRooms.Instance?.CharacterDollRoom.Cleanup();
                        UIDollRooms.Instance?.CharacterDollRoom?.SetupUnit(unit);
                    }
                }
            } catch (Exception ex) {
                Main.log.Log(ex.ToString());
            }
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

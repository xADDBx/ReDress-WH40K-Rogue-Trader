using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.EntitySystem.Entities.Base;
using Kingmaker.EntitySystem.Persistence;
using Kingmaker.Mechanics.Entities;
using Kingmaker.PubSubSystem;
using Kingmaker.PubSubSystem.Core;
using Kingmaker.UI.Common;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.Visual.CharacterSystem;
using Newtonsoft.Json;
using UnityEngine;

namespace ReDress; 
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
        public CustomColor Clone() => new() {
            Name = Name,
            R = R,
            G = G,
            B = B
        };
    }
    public class CustomColorTex {
        [JsonIgnore] 
        private static Dictionary<ulong, Texture2D> m_TexCache = [];
        [JsonProperty]
        public ulong? HashCode;
        [JsonProperty]
        public string? Name;
        [JsonProperty]
        public int height = 1;
        [JsonProperty]
        public int width = 1;
        [JsonProperty]
        public List<CustomColor> colors;
        [JsonProperty]
        public TextureWrapMode wrapMode = TextureWrapMode.Clamp;
        public void Become(CustomColorTex tex) {
            Name = tex.Name;
            height = tex.height;
            width = tex.width;
            colors = tex.colors;
            wrapMode = tex.wrapMode;
        }
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
            if (HashCode == null) {
                HashCode = ArrayHasher.ComputeHash(colors, height, width);
            }
            if (!m_TexCache.TryGetValue(HashCode.Value, out var tex)) {
                tex = new Texture2D(width, height, textureFormat: TextureFormat.RGBA32, 1, false) { filterMode = FilterMode.Bilinear };
                var pix = new Color[width * height];
                for (int i = 0; i < pix.Length; i++) {
                    pix[i] = colors[i];
                }
                tex.wrapMode = wrapMode;
                tex.SetPixels(pix);
                tex.Apply();
                m_TexCache[HashCode.Value] = tex;
            }
            return tex;
        }
        public CustomColorTex Clone() {
            CustomColorTex tex = new() {
                Name = Name,
                height = height,
                width = width,
                wrapMode = wrapMode,
                colors = []
            };
            foreach (var c in colors) {
                tex.colors.Add(c.Clone());
            }
            return tex;
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
                Main.Log.Log($"Updating per-save settings + Updating unit {Main.PickedUnit}");
                Helpers.DoForEachValidUnit(UpdateUnit);
                var u = UIDollRooms.Instance?.CharacterDollRoom?.Unit;
                if (u != null) {
                    UIDollRooms.Instance!.CharacterDollRoom.m_Unit = null;
                    UIDollRooms.Instance.CharacterDollRoom.SetupUnit(u);
                }
            }
        } catch (Exception ex) {
            Main.Log.Log(ex.ToString());
        }
    }
    private static void UpdateUnit(AbstractUnitEntity unit) {
        /*
        var oldView = unit.View;
        var newView = unit.ViewSettings.Instantiate(true);
        unit.AttachView(newView);
        List<BaseUnitMark> list = ListPool<BaseUnitMark>.Claim();
        oldView.GetComponentsInChildren<BaseUnitMark>(list);
        foreach (BaseUnitMark baseUnitMark in list) {
            baseUnitMark.gameObject.SetActive(false);
        }
        UnitAnimationManager? unitAnimationManager = oldView.AnimationManager;
        if (unitAnimationManager != null) {
            unitAnimationManager.StopEvents();
            unitAnimationManager.Disabled = true;
        }
        UnityEngine.Object.Destroy(oldView);
        */

        var polymorphBuff = ResourcesLibrary.BlueprintsCache.Load("b5fe711b0755440093599873b4b4caf6") as BlueprintBuff;
        unit.Buffs.Add(polymorphBuff);
        unit.Buffs.Remove(polymorphBuff);
        EventBus.RaiseEvent(unit, (IUnitVisualChangeHandler h) => {
            h.HandleUnitChangeEquipmentColor(-1, false);
        }, true);
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

using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Designers.EventConditionActionSystem.Evaluators;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.EntitySystem.Entities.Base;
using Kingmaker.EntitySystem.Interfaces;
using Kingmaker.EntitySystem.Persistence;
using Kingmaker.Mechanics.Entities;
using Kingmaker.PubSubSystem;
using Kingmaker.PubSubSystem.Core;
using Kingmaker.ResourceLinks;
using Kingmaker.UI.Common;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.Utility.DotNetExtensions;
using Kingmaker.View;
using Kingmaker.View.Mechanics.Entities;
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
            HashCode ??= ArrayHasher.ComputeHash(colors, height, width);
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
        // Character UID => EE => BodyPart
        public Dictionary<string, Dictionary<string, HashSet<string>>> ExcludeBodyPartByName = new();
        [JsonProperty]
        public Dictionary<string, HashSet<string>> IncludeByName = new();
        [JsonProperty]
        public Dictionary<string, Dictionary<string, RampColorPreset.IndexSet>> RampOverrideByName = new();
        [Obsolete]
        [JsonProperty]
        public Dictionary<string, Dictionary<string, (CustomColor, CustomColor)>>? CustomColorByName = new();
        // The first two are Primary and Secondary overrides. The third one is a placeholder for main tex. What are the other 3 for?
        [JsonProperty]
        public Dictionary<string, Dictionary<string, (CustomColorTex?, CustomColorTex?, CustomColorTex?, CustomColorTex?, CustomColorTex?, CustomColorTex?)>> CustomColorsByName = new();
        [JsonProperty]
        public Dictionary<string, bool> NakedFlag = new();
        [JsonProperty]
        public HashSet<string> UnbakedChars = [];
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
    public static bool CharacterDollRoomNormalUnitHandling = true;
    public static void SavePerSaveSettings(bool reloadCharacterClothing = true) {
        if (Game.Instance?.Player == null) return;
        if (m_CachedPerSave == null) ReloadPerSaveSettings();
        var json = JsonConvert.SerializeObject(m_CachedPerSave);
        Game.Instance.State.InGameSettings.List[PerSaveSettings.ID] = json;
        try {
            if (reloadCharacterClothing) {
                Main.Log.Log($"Updating per-save settings + Updating unit {Main.PickedUnit}");
                Helpers.DoForEachValidUnit(UpdateUnit);
                Main.InvalidatePreviews();
                var u = UIDollRooms.Instance?.CharacterDollRoom?.Unit;
                if (u != null) {
                    CharacterDollRoomNormalUnitHandling = false;
                    UIDollRooms.Instance!.CharacterDollRoom.SetupUnit(u);
                    CharacterDollRoomNormalUnitHandling = true;
                }
            }
        } catch (Exception ex) {
            Main.Log.Log(ex.ToString());
        }
    }
    public static void Unbake(AbstractUnitEntity unit) {
        var oldView = unit.View;
        var currentEEs = unit.View.CharacterAvatar.SavedEquipmentEntities.Select(l => l.Load());

        if (!perSave.IncludeByName.TryGetValue(unit.UniqueId, out var includes)) {
            includes = [];
        }
        includes.AddRange(unit.View.CharacterAvatar.SavedEquipmentEntities.Select(ee => ee.AssetId));
        perSave.IncludeByName[unit.UniqueId] = includes;


        unit.DetachView();

        unit.ViewSettings.m_CustomPrefabGuid = unit.Gender == Kingmaker.Blueprints.Base.Gender.Male ? "4e901c9c06a71c045804730a9e934106" : "f0df7f3f33090404c9457166c5bea90b";
        IEntityViewBase newView;
        if (unit is BaseUnitEntity baseUnit) {
            newView = baseUnit.CreateView();
        } else {
            var unitEntityView4 = ResourcesLibrary.TryGetResource<UnitEntityView>(unit.ViewSettings.PrefabGuid);

            Quaternion rotation3 = (unitEntityView4.ForbidRotation ? Quaternion.identity : Quaternion.Euler(0f, unit.Orientation, 0f));
            var unitEntityView5 = UnityEngine.Object.Instantiate(unitEntityView4, unit.Position, rotation3);
            if (unit is LightweightUnitEntity) {
                var viewGo = unitEntityView5.gameObject;
                var softColliderPlaceholder = unitEntityView5.SoftColliderPlaceholder;
                var rigidbodyController = unitEntityView5.RigidbodyController;
                var footprints = unitEntityView5.Footprints ?? [];
                UnityEngine.Object.DestroyImmediate(unitEntityView5);
                var lightweightView = viewGo.AddComponent<LightweightUnitEntityView>();
                lightweightView.SoftColliderPlaceholder = softColliderPlaceholder;
                lightweightView.RigidbodyController = rigidbodyController;
                lightweightView.Footprints = footprints;
                lightweightView.Blueprint = unit.Blueprint;
                newView = lightweightView;
            } else {
                newView = unitEntityView5;
            }
        }
        unit.AttachView(newView);
        var prefabEEs = unit.View.CharacterAvatar.EquipmentEntities?.Union(unit.View.CharacterAvatar.SavedEquipmentEntities?.Select(l => new EquipmentEntityLink() { AssetId = l.AssetId }.LoadAsset()).NotNull() ?? []) ?? [];

        var toExclude = prefabEEs.Except(currentEEs);
        if (!perSave.ExcludeByName.TryGetValue(unit.UniqueId, out var excludes)) {
            excludes = [];
        }
        excludes.AddRange(toExclude.Select(ee => ee.name));
        perSave.ExcludeByName[unit.UniqueId] = excludes;
        Main.IncludeBrowser.QueueUpdateItems(includes);
        perSave.UnbakedChars.Add(unit.UniqueId);
        oldView.DestroyViewObject();
        SavePerSaveSettings();
    }
    public static void Rebake(AbstractUnitEntity unit) {
        unit.ViewSettings.m_CustomPrefabGuid = "";
        perSave.UnbakedChars.Remove(unit.UniqueId);
        SavePerSaveSettings();
        UpdateUnit(unit);
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

        /* Neat:
        var oldView = unit.View;
        var newView = unit.CreateViewForData();
        unit.AttachView(newView);
        oldView.OnDisable();
        oldView.DestroyViewObject();
        */
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

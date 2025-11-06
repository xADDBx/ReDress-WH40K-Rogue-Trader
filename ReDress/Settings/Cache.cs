using HarmonyLib;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.BundlesLoading;
using Kingmaker.GameInfo;
using Kingmaker.Modding;
using Kingmaker.Visual.CharacterSystem;
using Newtonsoft.Json;

namespace ReDress; 
internal class Cache : AbstractSettings {
    public int BrowserPageLimit = 20;
    public bool ToggleSearchAsYouType = true;
    public float SearchDelay = 0.3f;
    public string CachedVersion = "";
    public Dictionary<string, string> BaseGameEEs = [];
    public Dictionary<string, string> ModdedEEs = [];
    [JsonIgnore]
    public Dictionary<string, string>? AssetMapping {
        get {
            if (field == null) {
                field = new(BaseGameEEs);
                foreach (var item in ModdedEEs) {
                    if (!field.ContainsKey(item.Key)) {
                        field[item.Key] = item.Value;
                    } else {
                        Main.Log.Error($"Id collision for modded EE: {item.Key}");
                    }
                }
            }
            return field;
        }
        private set;
    }
    public HashSet<(string, string)> OmmSet = [];
    [Obsolete]
    public bool ShouldExcludeNewEEs = false;
    protected override string Name => "Settings.json";
    private static readonly Lazy<Cache> _instance = new Lazy<Cache>(() => {
        var instance = new Cache();
        instance.Load();
        return instance;
    });
    public static Cache CacheInstance => _instance.Value;
    private static bool m_BaseGameRebuild = false;
    public static bool? NeedsCacheRebuilt {
        get {
            if (CacheInstance.AssetMapping!.Count < 1) {
                Main.Log.Log($"ReDress Cache empty!");
                m_BaseGameRebuild = true;
                return true;
            }
            if (field.HasValue) {
                return field.Value && !m_IsRebuilding;
            }

            bool gameVersionChanged = GameVersion.GetVersion() != CacheInstance.CachedVersion;
            field = gameVersionChanged;
            m_BaseGameRebuild = gameVersionChanged;
            Main.Log.Log($"Test for ReDress Cache consistency: Did Game Version change?: {gameVersionChanged}");

            bool ommModsChanged = !(CacheInstance.OmmSet.Count == OwlcatModificationsManager.s_Instance.AppliedModifications.Length);
            if (!ommModsChanged) {
                foreach (var modEntry in OwlcatModificationsManager.s_Instance.AppliedModifications) {
                    if (!CacheInstance.OmmSet.Contains(new(modEntry.Manifest.UniqueName, modEntry.Manifest.Version))) {
                        ommModsChanged = true;
                        break;
                    }
                }
            }
            field |= ommModsChanged;
            Main.Log.Log($"^- Test for ReDress Cache consistency: Did Owlmods change?: {ommModsChanged}");
            return field.Value && !m_IsRebuilding;
        }
    }
    private static bool m_IsRebuilding = false;
    internal static void RebuildCache() {
        Main.Log.Log($"Rebuilding ReDress Cache...");
        m_IsRebuilding = true;
        if (m_BaseGameRebuild) {
            CacheInstance.BaseGameEEs.Clear();
            foreach (var guid in BundlesLoadService.Instance.m_LocationList.Guids.Where(g => BundlesLoadService.Instance.m_LocationList.GuidToBundle[g] == "equipment")) {
                var obj = ResourcesLibrary.TryGetResource<UnityEngine.Object>(guid, true, false);
                if (obj is EquipmentEntity ee) {
                    CacheInstance.BaseGameEEs[guid] = ee.name;
                }
            }
        }
        CacheInstance.ModdedEEs.Clear();
        foreach (var mod in OwlcatModificationsManager.Instance.AppliedModifications) {
            try {
                var mapping = mod.Settings.BundlesLayout.GuidToBundle.GroupBy(p => p.Value).ToDictionary(g => g.Key, g => g.Select(pp => pp.Key).ToList());
                foreach (var item in mapping) {
                    var bundle = BundlesLoadService.Instance.RequestBundle(item.Key);
                    foreach (var assetGuid in item.Value) {
                        var obj = bundle.LoadAsset(assetGuid);
                        if (obj is EquipmentEntity ee) {
                            CacheInstance.ModdedEEs[assetGuid] = ee.name;
                        }
                    }
                }
            } catch (Exception ex) { 
                Main.Log.Log($"Encountered exception while caching Assets from Owlmod: {mod.UniqueName}\n{ex}");
            }
        }
        ResourcesLibrary.CleanupLoadedCache(ResourcesLibrary.CleanupMode.UnloadNonRequested);
        CacheInstance.CachedVersion = GameVersion.GetVersion();
        CacheInstance.OmmSet = [.. OwlcatModificationsManager.s_Instance.AppliedModifications.Select<OwlcatModification, (string, string)>(m => new(m.Manifest.UniqueName, m.Manifest.Version))];
        CacheInstance.AssetMapping = null;
        CacheInstance.Save();
        m_BaseGameRebuild = false;
        m_IsRebuilding = false;
    }
    [HarmonyPatch(typeof(GameMainMenu))]
    public static class RebuildCachePatch {
        [HarmonyPatch(nameof(GameMainMenu.Start)), HarmonyPostfix]
        public static void Start() {
            if (NeedsCacheRebuilt.HasValue && NeedsCacheRebuilt.Value) {
                RebuildCache();
            }
        }
    }
}

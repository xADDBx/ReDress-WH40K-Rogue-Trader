using HarmonyLib;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.BundlesLoading;
using Kingmaker.GameInfo;
using Kingmaker.Visual.CharacterSystem;

namespace ReDress; 
internal class Cache : AbstractSettings {
    public int BrowserPageLimit = 20;
    public bool ToggleSearchAsYouType = true;
    public float SearchDelay = 0.3f;
    public string CachedVersion = "";
    public Dictionary<string, string> AssetMapping = [];
    public bool ShouldExcludeNewEEs = false;
    protected override string Name => "Settings.json";
    private static readonly Lazy<Cache> _instance = new Lazy<Cache>(() => {
        var instance = new Cache();
        instance.Load();
        return instance;
    });
    public static Cache CacheInstance => _instance.Value;
    public static bool NeedsCacheRebuilt => (GameVersion.GetVersion() != CacheInstance.CachedVersion) && !m_IsRebuilding && CacheInstance.AssetMapping?.Count <= 0;
    private static bool m_IsRebuilding = false;
    internal static void RebuildCache() {
        m_IsRebuilding = true;
        CacheInstance.AssetMapping.Clear();
        foreach (var guid in BundlesLoadService.Instance.m_LocationList.Guids.Where(g => BundlesLoadService.Instance.m_LocationList.GuidToBundle[g] == "equipment")) {
            var obj = ResourcesLibrary.TryGetResource<UnityEngine.Object>(guid, true, false);
            if (obj is EquipmentEntity ee) {
                CacheInstance.AssetMapping[guid] = ee.name;
            }
        }
        ResourcesLibrary.CleanupLoadedCache(ResourcesLibrary.CleanupMode.UnloadNonRequested);
        CacheInstance.CachedVersion = GameVersion.GetVersion();
        CacheInstance.Save();
        m_IsRebuilding = false;
    }
    [HarmonyPatch(typeof(GameMainMenu))]
    public static class RebuildCachePatch {
        [HarmonyPatch(nameof(GameMainMenu.Start)), HarmonyPostfix]
        public static void Start() {
            if (NeedsCacheRebuilt) {
                RebuildCache();
            }
        }
    }
}

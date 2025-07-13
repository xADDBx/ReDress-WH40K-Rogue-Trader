using HarmonyLib;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.BundlesLoading;
using Kingmaker.GameInfo;
using Kingmaker.Visual.CharacterSystem;

namespace ReDress {
    public static class Cache {
        public static bool NeedsCacheRebuilt => (GameVersion.GetVersion() != Main.m_Settings.CachedVersion) && !isRebuilding;
        public static bool isRebuilding = false;
        internal static void RebuildCache() {
            isRebuilding = true;
            Main.m_Settings.AssetIds = new();
            foreach (var guid in BundlesLoadService.Instance.m_LocationList.Guids.Where(g => BundlesLoadService.Instance.m_LocationList.GuidToBundle[g] == "equipment")) {
                var obj = ResourcesLibrary.TryGetResource<UnityEngine.Object>(guid, true, false);
                if (obj is EquipmentEntity ee) {
                    Main.m_Settings.AssetIds.Add((guid, ee.name));
                }
            }
            ResourcesLibrary.CleanupLoadedCache(ResourcesLibrary.CleanupMode.UnloadNonRequested);
            Main.m_Settings.CachedVersion = GameVersion.GetVersion();
            Main.m_Settings.Save(Main.Mod);
            isRebuilding = false;
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
}

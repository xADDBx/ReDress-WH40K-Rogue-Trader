using HarmonyLib;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.BundlesLoading;
using Kingmaker.GameInfo;
using Kingmaker.Visual.CharacterSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Kingmaker.UnitLogic.Buffs.Components.DOTLogic;

namespace ReDress {
    public static class Cache {
        public static bool needsCacheRebuilt => (GameVersion.GetVersion() != Main.settings.CachedVersion) && !isRebuilding;
        public static bool isRebuilding = false;
        internal static void RebuildCache() {
            isRebuilding = true;
            Main.settings.AssetIds = new();
            foreach (var guid in BundlesLoadService.Instance.m_LocationList.Guids.Where(g => BundlesLoadService.Instance.m_LocationList.GuidToBundle[g] == "equipment")) {
                var obj = ResourcesLibrary.TryGetResource<UnityEngine.Object>(guid, true, false);
                if (obj is EquipmentEntity ee) {
                    Main.settings.AssetIds.Add((guid, ee.name));
                }
            }
            ResourcesLibrary.CleanupLoadedCache(ResourcesLibrary.CleanupMode.UnloadNonRequested);
            Main.settings.CachedVersion = GameVersion.GetVersion();
            Main.settings.Save(Main.mod);
            isRebuilding = false;
        }
    }
}

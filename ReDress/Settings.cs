using UnityModManagerNet;

namespace ReDress {
    public class Settings : UnityModManager.ModSettings {
        public string CachedVersion = "";
        public List<(string, string)> AssetIds = new();
        public bool ShouldExcludeNewEEs = false;
        public override void Save(UnityModManager.ModEntry modEntry) {
            Save(this, modEntry);
        }
    }
}
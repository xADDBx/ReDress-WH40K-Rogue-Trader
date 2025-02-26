using ReDress;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
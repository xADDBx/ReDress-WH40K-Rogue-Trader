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
            public float R;
            public float G;
            public float B;

            public static implicit operator UnityEngine.Color(CustomColor c) {
                return new UnityEngine.Color(c.R, c.G, c.B);
            }
            public override string ToString() {
                return $"R: {Mathf.RoundToInt(R * 255)}, G: {Mathf.RoundToInt(G * 255)}, B: {Mathf.RoundToInt(B * 255)}";
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
            [JsonProperty]
            public Dictionary<string, Dictionary<string, (CustomColor, CustomColor)>> CustomColorByName = new();
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

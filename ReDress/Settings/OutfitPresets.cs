using Kingmaker.Visual.CharacterSystem;
using Newtonsoft.Json;

namespace ReDress;
public class OutfitPreset {
    [JsonIgnore]
    internal string? SourceFile;
    [JsonProperty]
    public string Id = Guid.NewGuid().ToString("N");
    [JsonProperty]
    public string Name = "";
    [JsonProperty]
    public List<string>? AddClothes;
    [JsonProperty]
    public HashSet<string>? Excludes;
    [JsonProperty]
    public Dictionary<string, HashSet<string>>? BodyPartExcludes;
    [JsonProperty]
    public HashSet<string>? Includes;
    [JsonProperty]
    public Dictionary<string, RampColorPreset.IndexSet>? RampOverrides;
    [JsonProperty]
    public Dictionary<string, (EntityPartStorage.CustomColorTex?, EntityPartStorage.CustomColorTex?, EntityPartStorage.CustomColorTex?, EntityPartStorage.CustomColorTex?, EntityPartStorage.CustomColorTex?, EntityPartStorage.CustomColorTex?)>? CustomColors;
    [JsonProperty]
    public bool Naked;

    public static OutfitPreset Capture(string uid, string name) {
        var perSave = EntityPartStorage.perSave;
        var preset = new OutfitPreset {
            Name = name,
            AddClothes = perSave.AddClothes.TryGetValue(uid, out var addClothes) ? addClothes : null,
            Excludes = perSave.ExcludeByName.TryGetValue(uid, out var excludes) ? excludes : null,
            BodyPartExcludes = perSave.ExcludeBodyPartByName.TryGetValue(uid, out var bodyParts) ? bodyParts : null,
            Includes = perSave.IncludeByName.TryGetValue(uid, out var includes) ? includes : null,
            RampOverrides = perSave.RampOverrideByName.TryGetValue(uid, out var ramps) ? ramps : null,
            CustomColors = perSave.CustomColorsByName.TryGetValue(uid, out var colors) ? colors : null,
            Naked = perSave.NakedFlag.TryGetValue(uid, out var naked) && naked
        };
        return preset.Clone();
    }
    public void ApplyTo(string uid) {
        var perSave = EntityPartStorage.perSave;
        var copy = Clone();
        Set(perSave.AddClothes, copy.AddClothes);
        Set(perSave.ExcludeByName, copy.Excludes);
        Set(perSave.ExcludeBodyPartByName, copy.BodyPartExcludes);
        Set(perSave.IncludeByName, copy.Includes);
        Set(perSave.RampOverrideByName, copy.RampOverrides);
        Set(perSave.CustomColorsByName, copy.CustomColors);
        if (Naked) {
            perSave.NakedFlag[uid] = true;
        } else {
            perSave.NakedFlag.Remove(uid);
        }
        EntityPartStorage.SavePerSaveSettings();

        void Set<T>(Dictionary<string, T> target, T? value) where T : class {
            if (value == null) {
                target.Remove(uid);
            } else {
                target[uid] = value;
            }
        }
    }
    private OutfitPreset Clone() => JsonConvert.DeserializeObject<OutfitPreset>(JsonConvert.SerializeObject(this))!;
}
internal class OutfitPresets {
    private static readonly Lazy<OutfitPresets> _instance = new(() => {
        var instance = new OutfitPresets();
        instance.Load();
        return instance;
    });
    public static OutfitPresets SavedOutfitPresets => _instance.Value;
    public List<OutfitPreset> Presets = [];
    internal string GetFolderPath() {
        var folder = Path.Combine(Main.Mod.Path, "Settings", "OutfitPresets");
        Directory.CreateDirectory(folder);
        return folder;
    }
    internal void Save(OutfitPreset preset) {
        if (preset.SourceFile == null) {
            var namePart = string.Concat(preset.Name.Split(Path.GetInvalidFileNameChars()));
            if (namePart.Length == 0) {
                namePart = "Preset";
            }
            preset.SourceFile = Path.Combine(GetFolderPath(), $"{namePart}_{preset.Id}.json");
        }
        File.WriteAllText(preset.SourceFile, JsonConvert.SerializeObject(preset, Formatting.Indented));
        if (!Presets.Contains(preset)) {
            Presets.Add(preset);
        }
    }
    internal void Delete(OutfitPreset preset) {
        Presets.Remove(preset);
        if (preset.SourceFile != null && File.Exists(preset.SourceFile)) {
            File.Delete(preset.SourceFile);
        }
    }
    public void Reload() {
        Presets.Clear();
        Load();
    }
    private void Load() {
        foreach (var file in Directory.GetFiles(GetFolderPath(), "*.json")) {
            try {
                var preset = JsonConvert.DeserializeObject<OutfitPreset>(File.ReadAllText(file));
                if (preset == null) {
                    continue;
                }
                preset.SourceFile = file;
                Presets.Add(preset);
            } catch (Exception e) {
                Main.Log.Log($"[Error] Failed to load outfit preset at {file}: {e}");
            }
        }
    }
}

global using static ReDress.ColorPresets;
global using static ReDress.TexturePresets;
using HarmonyLib;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.ResourceLinks;
using Kingmaker.Utility.UnityExtensions;
using Kingmaker.Visual.CharacterSystem;
using System.Collections.Concurrent;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityModManagerNet;
using static ReDress.UIHelpers;

namespace ReDress;

static class Main {
    internal static UnityModManager.ModEntry Mod = null!;
    internal static Harmony HarmonyInstance = null!;
    internal static UnityModManager.ModEntry.ModLogger Log = null!;
    internal static Cache m_Settings => Cache.CacheInstance;
    internal static BaseUnitEntity? PickedUnit = null;

    private static readonly ConcurrentQueue<Action> m_MainThreadTaskQueue = [];
    private static readonly ConcurrentQueue<Action> m_GuiThreadTaskQueue = [];
    private static Exception? m_Error = null;
    private static Outfit m_SelectedOutfit = Outfit.Current;
    private static bool m_OpenedGuide = false;
    private static bool m_OpenedExclude = false;
    private static bool m_OpenedInclude = false;
    private static bool m_OpenedClothingSection = false;
    private static bool m_OpenedColorSection = false;
    private static CustomTexCreator? m_PrimaryTexCreator;
    private static CustomTexCreator? m_SecondaryTexCreator;
    private static string m_ShowRampsForEEName = "";
    internal static Browser<string> IncludeBrowser = null!;
    private static float? m_IncludeBrowserLabelWidth = null;

    internal static bool Load(UnityModManager.ModEntry modEntry) {
        Log = modEntry.Logger;
        Mod = modEntry;
        modEntry.OnGUI = OnGUI;
        modEntry.OnHideGUI = OnHideGUI;
        modEntry.OnFixedUpdate = OnFixedUpdate;
        IncludeBrowser = new(s => $"{m_Settings.AssetMapping[s]} {s}", s => $"{m_Settings.AssetMapping[s]} {s}", null, (Action<IEnumerable<string>> a) => {
            a(m_Settings.AssetMapping.Keys);
        }, false, (int)(EffectiveWindowWidth() * 0.95f));
        ScheduleForGuiThread(IncludeBrowser.ForceShowAll);
        HarmonyInstance = new Harmony(modEntry.Info.Id);
        HarmonyInstance.PatchAll(Assembly.GetExecutingAssembly());
        return true;
    }

    private static void OnHideGUI(UnityModManager.ModEntry modEntry) {
        m_OpenedGuide = false;
        m_OpenedExclude = false;
        m_OpenedInclude = false;
        m_OpenedClothingSection = false;
        m_OpenedColorSection = false;
        m_SelectedOutfit = Outfit.Current;
    }

    private static void OnGUI(UnityModManager.ModEntry modEntry) {
        try {
            if (m_GuiThreadTaskQueue.TryDequeue(out var task)) {
                task();
            }
        } catch (Exception ex) {
            Log.LogException(ex);
        }
        if (m_Error != null) {
            GUILayout.Label(m_Error.ToString());
            if (GUILayout.Button("Reset Error")) {
                m_Error = null;
            }
        } else {
            try {
                m_OpenedGuide = GUILayout.Toggle(m_OpenedGuide, "Show Guide", GUILayout.ExpandWidth(false));
                if (m_OpenedGuide) {
                    using (HorizontalScope()) {
                        GUILayout.Space(50);
                        using (VerticalScope()) {
                            GUILayout.Label("Pick one of the listed characters", GUILayout.ExpandWidth(false));
                            GUILayout.Label("To change the appeareance for the chosen character just click the \"Change Appeareance\" button.", GUILayout.ExpandWidth(false));
                            GUILayout.Label("To change the outfit just click on the button for the respective outfit (to reset to default use the \"Reset Oufit\" button.", GUILayout.ExpandWidth(false));
                            GUILayout.Label("To change the outfit of your companions you will probably need to manually exclude the old one. To do that you need to open the exclude section and click on \"Exclude\" for the respective outfit parts.", GUILayout.ExpandWidth(false));
                            GUILayout.Label("As an example, to change the outfit of Heinrix you need to add \"EE_BaseoutfitHeinrix...\", \"EE_CapeBaseoutfitHeinrix...\" and \"EE_PantsHeinrix...\" to the excludes.", GUILayout.ExpandWidth(false));
                            GUILayout.Label("To reset the excludes just click \"Reset Excludes\" or remove them from the \"Current Excludes\" section", GUILayout.ExpandWidth(false));
                            GUILayout.Label("The Include Section allows you to pick from all the visual entities in the game and add them to your character.", GUILayout.ExpandWidth(false));
                            GUILayout.Label("Together with the Exclude Section this <i>basically</i> allows building arbitrary outfits using the outfit parts built into the game.", GUILayout.ExpandWidth(false));
                        }
                    }
                }

                DrawDiv();
                var units = Game.Instance?.Player?.PartyAndPets?.Where(u => u != null && !u.IsDisposed && !u.IsDisposingNow && u.IsViewActive)?.ToList() ?? [];
                if (units.Count > 0) {
                    GUILayout.Label("Character to change:");

                    int selectedIndex = PickedUnit != null ? units.IndexOf(PickedUnit) : 0;
                    if (selectedIndex < 0) {
                        selectedIndex = 0;
                        PickedUnit = null;
                    }
                    int newIndex = GUILayout.SelectionGrid(selectedIndex, units.Select(m => m.CharacterName).ToArray(), 6);
                    if (selectedIndex != newIndex || PickedUnit == null) {
                        PickedUnit = units[newIndex];
                        EntityPartStorage.perSave.IncludeByName.TryGetValue(PickedUnit!.UniqueId, out var tmp);
                        IncludeBrowser.QueueUpdateItems(tmp ?? []);
                        m_SelectedOutfit = Outfit.Current;
                    }
                    DrawDiv();
                    if (GUILayout.Button("Change Appeareance", GUILayout.ExpandWidth(false))) {
                        Helpers.OpenAppeareanceChanger(PickedUnit);
                    }
                    DrawDiv();

                    ClothingGUI();

                    ColorGUI();
                    var newVal = GUILayout.Toggle(m_Settings.ShouldExcludeNewEEs, "Automatically exclude new items on all characters");
                    if (newVal != m_Settings.ShouldExcludeNewEEs) {
                        m_Settings.ShouldExcludeNewEEs = newVal;
                        m_Settings.Save();
                    }
                } else {
                    GUILayout.Label("Load a save first!", GUILayout.ExpandWidth(false));
                }
            } catch (Exception ex) {
                Log.Log(ex.ToString());
                m_Error = ex;
            }
        }
    }

    private static void ClothingGUI() {
        m_OpenedClothingSection = GUILayout.Toggle(m_OpenedClothingSection, "Show Clothing Section", GUILayout.ExpandWidth(false));
        if (m_OpenedClothingSection) {
            using (HorizontalScope()) {
                GUILayout.Space(25);
                using (VerticalScope()) {
                    GUILayout.Label("Set Outfit to the following:", GUILayout.ExpandWidth(false));
                    if (SelectionGrid(ref m_SelectedOutfit, 5, m => m.ToDescriptionString())) {
                        if (m_SelectedOutfit != Outfit.Current) {
                            if (m_SelectedOutfit == Outfit.Naked) {
                                EntityPartStorage.perSave.AddClothes.Remove(PickedUnit!.UniqueId);
                                EntityPartStorage.perSave.NakedFlag[PickedUnit.UniqueId] = true;
                            } else {
                                var kee = ResourcesLibrary.BlueprintsCache.Load(Helpers.JobClothesIDs[m_SelectedOutfit]) as KingmakerEquipmentEntity;
                                if (kee == null) {

                                } else {
                                    Log.Log($"Error trying to save job clothing for job: {Helpers.JobClothesIDs[m_SelectedOutfit]}");
                                    EntityPartStorage.perSave.AddClothes[PickedUnit!.UniqueId] = PickedUnit.Gender == Kingmaker.Blueprints.Base.Gender.Male ? kee.m_MaleArray.Select(f => f.AssetId).ToList() : kee.m_FemaleArray.Select(f => f.AssetId).ToList();
                                    EntityPartStorage.perSave.NakedFlag.Remove(PickedUnit.UniqueId);
                                }
                            }
                        }
                        EntityPartStorage.SavePerSaveSettings();
                    }

                    DrawDiv();
                    if (GUILayout.Button("Reset Outfit", GUILayout.ExpandWidth(false))) {
                        EntityPartStorage.perSave.AddClothes.Remove(PickedUnit!.UniqueId);
                        EntityPartStorage.perSave.NakedFlag.Remove(PickedUnit.UniqueId);
                        EntityPartStorage.SavePerSaveSettings();
                    }

                    DrawDiv();
                    m_OpenedExclude = GUILayout.Toggle(m_OpenedExclude, "Show Exclude Section", GUILayout.ExpandWidth(false));
                    if (m_OpenedExclude) {
                        if (GUILayout.Button("Reset Excludes", GUILayout.ExpandWidth(false))) {
                            EntityPartStorage.perSave.ExcludeByName.Remove(PickedUnit!.UniqueId);
                            EntityPartStorage.SavePerSaveSettings();
                        }
                        foreach (var ee in PickedUnit!.View.CharacterAvatar.EquipmentEntities.Union(PickedUnit.View.CharacterAvatar.SavedEquipmentEntities.Select(l => new EquipmentEntityLink() { AssetId = l.AssetId }.LoadAsset()))) {
                            using (HorizontalScope()) {
                                if (GUILayout.Button("Exclude", GUILayout.ExpandWidth(false))) {
                                    EntityPartStorage.perSave.ExcludeByName.TryGetValue(PickedUnit.UniqueId, out var tmpExcludes);
                                    if (tmpExcludes == null) tmpExcludes = new();
                                    tmpExcludes.Add(ee.name);
                                    EntityPartStorage.perSave.ExcludeByName[PickedUnit.UniqueId] = tmpExcludes;
                                    EntityPartStorage.SavePerSaveSettings();
                                }
                                GUILayout.Label($"    {ee?.name ?? "Null????????????"}");
                            }
                        }
                        GUILayout.Label("------------------------------------------");
                        GUILayout.Label("Current Excludes:");
                        EntityPartStorage.perSave.ExcludeByName.TryGetValue(PickedUnit.UniqueId, out var currentExcludes);
                        if (currentExcludes?.Count > 0) {
                            foreach (var eeName in currentExcludes.ToList()) {
                                using (HorizontalScope()) {
                                    if (GUILayout.Button("Remove Exclusion", GUILayout.ExpandWidth(false))) {
                                        EntityPartStorage.perSave.ExcludeByName.TryGetValue(PickedUnit.UniqueId, out var tmpExcludes);
                                        if (tmpExcludes == null) tmpExcludes = new();
                                        tmpExcludes.Remove(eeName);
                                        EntityPartStorage.perSave.ExcludeByName[PickedUnit.UniqueId] = tmpExcludes;
                                        EntityPartStorage.SavePerSaveSettings();
                                    }
                                    GUILayout.Label($"    {eeName}");
                                }
                            }
                        }
                    }

                    DrawDiv();
                    m_OpenedInclude = GUILayout.Toggle(m_OpenedInclude, "Show Include Section", GUILayout.ExpandWidth(false));
                    if (m_OpenedInclude) {
                        if (GUILayout.Button("Reset Includes", GUILayout.ExpandWidth(false))) {
                            EntityPartStorage.perSave.IncludeByName.Remove(PickedUnit!.UniqueId);
                            IncludeBrowser.QueueUpdateItems([]);
                            EntityPartStorage.SavePerSaveSettings();
                        }
                        EntityPartStorage.perSave.IncludeByName.TryGetValue(PickedUnit!.UniqueId, out var currentIncludes);
                        m_IncludeBrowserLabelWidth ??= CalculateLargestLabelSize(["Remove", "Include"], GUI.skin.button);
                        IncludeBrowser.OnGUI(guid => {
                            using (HorizontalScope()) {
                                if (currentIncludes?.Contains(guid) ?? false) {
                                    GUILayout.Label($"{m_Settings.AssetMapping[guid]}".Cyan(), Width(IncludeBrowser.TrackedWidth!.Value));
                                    GUILayout.Space(20);
                                    if (GUILayout.Button("Remove", GUILayout.Width(m_IncludeBrowserLabelWidth.Value))) {
                                        currentIncludes.Remove(guid);
                                        IncludeBrowser.QueueUpdateItems(currentIncludes);
                                        EntityPartStorage.perSave.IncludeByName[PickedUnit.UniqueId] = currentIncludes;
                                        EntityPartStorage.SavePerSaveSettings();
                                    }
                                    GUILayout.Space(20);
                                    GUILayout.TextArea($"{guid}", Width(IncludeBrowser.TrackedWidth2!.Value));
                                } else {
                                    GUILayout.Label($"{m_Settings.AssetMapping[guid]}".Green(), Width(IncludeBrowser.TrackedWidth!.Value));
                                    GUILayout.Space(20);
                                    if (GUILayout.Button("Include", GUILayout.Width(m_IncludeBrowserLabelWidth.Value))) {
                                        currentIncludes ??= [];
                                        currentIncludes.Add(guid);
                                        IncludeBrowser.QueueUpdateItems(currentIncludes);
                                        EntityPartStorage.perSave.IncludeByName[PickedUnit.UniqueId] = currentIncludes;
                                        EntityPartStorage.SavePerSaveSettings();
                                    }
                                    GUILayout.Space(20);
                                    GUILayout.TextArea($"{guid}", Width(IncludeBrowser.TrackedWidth2!.Value));
                                }
                            }
                        });
                    }
                }
            }
        }
        DrawDiv();
    }

    private static void ColorGUI() {
        m_OpenedColorSection = GUILayout.Toggle(m_OpenedColorSection, "Show Color Section", GUILayout.ExpandWidth(false));
        if (m_OpenedColorSection) {
            using (HorizontalScope()) {
                GUILayout.Space(25);
                EntityPartStorage.perSave.RampOverrideByName.TryGetValue(PickedUnit!.UniqueId, out var overrides);
                EntityPartStorage.perSave.CustomColorsByName.TryGetValue(PickedUnit.UniqueId, out var customOverrides);
                overrides ??= new();
                customOverrides ??= new();
                using (VerticalScope()) {
                    var entries = PickedUnit.View.CharacterAvatar.EquipmentEntities.Union(PickedUnit.View.CharacterAvatar.SavedEquipmentEntities.Select(l => new EquipmentEntityLink() { AssetId = l.AssetId }.LoadAsset()));
                    var width = CalculateLargestLabelSize(entries.Select(ee => ee.name.IsNullOrEmpty() ? ee.ToString() : ee.name));
                    foreach (var entry in entries) {
                        var ee = entry;
                        var eeName = ee.name.IsNullOrEmpty() ? ee.ToString() : ee.name;
                        using (HorizontalScope()) {
                            GUILayout.Label($"{eeName}:", Width(width));
                            using (VerticalScope()) {
                                if (customOverrides.ContainsKey(eeName)) {
                                    if (GUILayout.Button("Remove Custom Color Override", GUILayout.ExpandWidth(false))) {
                                        customOverrides.Remove(eeName);
                                        EntityPartStorage.perSave.CustomColorsByName[PickedUnit.UniqueId] = customOverrides;
                                        EntityPartStorage.SavePerSaveSettings();
                                    }
                                }
                                customOverrides.TryGetValue(eeName, out var oldOverrides);
                                if (oldOverrides.Item1 != null) {
                                    GUILayout.Label($"Current Primary Override: {oldOverrides.Item1}");
                                }
                                bool isActive = m_PrimaryTexCreator?.EEName == eeName;
                                bool isNowActive = GUILayout.Toggle(isActive, "Show Custom Primary Color Picker", GUILayout.ExpandWidth(false));
                                if (isNowActive != isActive) {
                                    if (isNowActive) {
                                        m_PrimaryTexCreator = new(eeName, oldOverrides.Item1);
                                    } else {
                                        m_PrimaryTexCreator = null;
                                    }
                                    isActive = isNowActive;
                                }
                                if (isActive) {
                                    if (m_PrimaryTexCreator!.ColorPickerGUI()) {
                                        oldOverrides.Item1 = m_PrimaryTexCreator.GetTexCopy();
                                        customOverrides[eeName] = oldOverrides;
                                        EntityPartStorage.perSave.CustomColorsByName[PickedUnit.UniqueId] = customOverrides;
                                        EntityPartStorage.SavePerSaveSettings();
                                        Helpers.SetColorPair(ee, new() { PrimaryIndex = -1, SecondaryIndex = -1 });
                                    }
                                }
                                if (oldOverrides.Item2 != null) {
                                    GUILayout.Label($"Current Secondary Override: {oldOverrides.Item2}");
                                }
                                bool isActive2 = m_SecondaryTexCreator?.EEName == eeName;
                                bool isNowActive2 = GUILayout.Toggle(isActive2, "Show Custom Secondary Color Picker", GUILayout.ExpandWidth(false));
                                if (isActive2 != isNowActive2) {
                                    if (isActive2) {
                                        m_SecondaryTexCreator = new(eeName, oldOverrides.Item2);
                                    } else {
                                        m_SecondaryTexCreator = null;
                                    }
                                    isActive2 = isNowActive2;
                                }
                                if (isActive2) {
                                    if (m_SecondaryTexCreator!.ColorPickerGUI()) {
                                        oldOverrides.Item2 = m_SecondaryTexCreator.GetTexCopy();
                                        customOverrides[eeName] = oldOverrides;
                                        EntityPartStorage.perSave.CustomColorsByName[PickedUnit.UniqueId] = customOverrides;
                                        EntityPartStorage.SavePerSaveSettings();
                                        Helpers.SetColorPair(ee, new() { PrimaryIndex = -1, SecondaryIndex = -1 });
                                    }
                                }

                                Helpers.GetClothColorsProfile(entry, out var colorPresets, false);
                                if (colorPresets != null) {
                                    bool isActive3 = m_ShowRampsForEEName == eeName;
                                    bool isNowActive3 = GUILayout.Toggle(isActive3, "Show RampTextureProfiles", GUILayout.ExpandWidth(false));
                                    if (isNowActive3) {
                                        m_ShowRampsForEEName = eeName;
                                        if (overrides.ContainsKey(eeName)) {
                                            if (GUILayout.Button("Remove Color Override", GUILayout.ExpandWidth(false))) {
                                                overrides.Remove(eeName);
                                                EntityPartStorage.perSave.RampOverrideByName[PickedUnit.UniqueId] = overrides;
                                                EntityPartStorage.SavePerSaveSettings();
                                            }
                                        }
                                        GUILayout.Space(50);
                                        using (VerticalScope()) {
                                            foreach (var pair in colorPresets.IndexPairs) {
                                                using (HorizontalScope()) {
                                                    GUILayout.Label($"{pair.Name ?? "Null Name"} - {pair.PrimaryIndex} - {pair.SecondaryIndex}", GUILayout.ExpandWidth(false));
                                                    if (GUILayout.Button("Select", GUILayout.ExpandWidth(false))) {
                                                        overrides[eeName] = pair;
                                                        EntityPartStorage.perSave.RampOverrideByName[PickedUnit.UniqueId] = overrides;
                                                        EntityPartStorage.SavePerSaveSettings();
                                                        Helpers.SetColorPair(ee, pair);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        DrawDiv();
    }
    private static void OnFixedUpdate(UnityModManager.ModEntry modEntry, float z) {
        try {
            if (m_MainThreadTaskQueue.TryDequeue(out var task)) {
                task();
            }
        } catch (Exception ex) {
            Log.LogException(ex);
        }
    }
    public static void ScheduleForMainThread(this Action action) {
        m_MainThreadTaskQueue.Enqueue(action);
    }
    public static void ScheduleForGuiThread(this Action action) {
        m_GuiThreadTaskQueue.Enqueue(action);
    }
}
using HarmonyLib;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.Visual.CharacterSystem;
using System.Reflection;
using UnityEngine;
using UnityModManagerNet;
using Kingmaker.ResourceLinks;
using Kingmaker.Utility.UnityExtensions;
using static ReDress.UIHelpers;

namespace ReDress;

static class Main {
    internal static UnityModManager.ModEntry Mod = null!;
    internal static Harmony HarmonyInstance = null!;
    internal static UnityModManager.ModEntry.ModLogger Log = null!;
    internal static Settings m_Settings = null!;
    internal static BaseUnitEntity? PickedUnit = null;

    private static Exception? m_Error = null;
    private static Outfit m_SelectedOutfit = Outfit.Current;
    private static bool m_OpenedGuide = false;
    private static bool m_OpenedExclude = false;
    private static bool m_OpenedInclude = false;
    private static bool m_OpenedClothingSection = false;
    private static bool m_OpenedColorSection = false;
    private static CustomTexCreator? m_PrimaryTexCreator;
    private static CustomTexCreator? m_SecondaryTexCreator;
    private static Browser<(string, string), (string, string)> m_IncludeBrowser = new(true);

    internal static bool Load(UnityModManager.ModEntry modEntry) {
        Log = modEntry.Logger;
        Mod = modEntry;
        modEntry.OnGUI = OnGUI;
        modEntry.OnHideGUI = OnHideGUI;
        modEntry.OnSaveGUI = OnSaveGUI;
        m_Settings = Settings.Load<Settings>(modEntry);
        HarmonyInstance = new Harmony(modEntry.Info.Id);
        HarmonyInstance.PatchAll(Assembly.GetExecutingAssembly());
        return true;
    }

    private static void OnSaveGUI(UnityModManager.ModEntry modEntry) {
        m_Settings.Save(modEntry);
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
        if (m_Error != null) {
            GUILayout.Label(m_Error.ToString());
            if (GUILayout.Button("Reset Error")) {
                m_Error = null;
            }
        } else {
            try {
                m_OpenedGuide = GUILayout.Toggle(m_OpenedGuide, "Show Guide", GUILayout.ExpandWidth(false));
                if (m_OpenedGuide) {
                    using (new GUILayout.HorizontalScope()) {
                        GUILayout.Space(50);
                        using (new GUILayout.VerticalScope()) {
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
                        m_SelectedOutfit = Outfit.Current;
                    }
                    DrawDiv();
                    if (GUILayout.Button("Change Appeareance", GUILayout.ExpandWidth(false))) {
                        Helpers.OpenAppeareanceChanger(PickedUnit);
                    }
                    DrawDiv();

                    ClothingGUI();

                    ColorGUI();
                    m_Settings.ShouldExcludeNewEEs = GUILayout.Toggle(m_Settings.ShouldExcludeNewEEs, "Automatically exclude new items on all characters");
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
            using (new GUILayout.HorizontalScope()) {
                GUILayout.Space(25);
                using (new GUILayout.VerticalScope()) {
                    GUILayout.Label("Set Outfit to the following:", GUILayout.ExpandWidth(false));
                    Outfit[] outfits = (Outfit[])Enum.GetValues(typeof(Outfit));
                    var selectedIndex2 = Array.IndexOf(outfits, m_SelectedOutfit);
                    int newIndex = GUILayout.SelectionGrid(selectedIndex2, outfits.Select(m => m.ToDescriptionString()).ToArray(), 5);
                    if (selectedIndex2 != newIndex) {
                        m_SelectedOutfit = outfits[newIndex];
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
                            using (new GUILayout.HorizontalScope()) {
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
                                using (new GUILayout.HorizontalScope()) {
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
                    GUILayout.Label("Opening the Include section might make the game freeze for a few seconds to build a cache of existing EquipmentEntities.");
                    m_OpenedInclude = GUILayout.Toggle(m_OpenedInclude, "Show Include Section", GUILayout.ExpandWidth(false));
                    if (m_OpenedInclude) {
                        if (GUILayout.Button("Reset Includes", GUILayout.ExpandWidth(false))) {
                            EntityPartStorage.perSave.IncludeByName.Remove(PickedUnit!.UniqueId);
                            EntityPartStorage.SavePerSaveSettings();
                        }
                        EntityPartStorage.perSave.IncludeByName.TryGetValue(PickedUnit!.UniqueId, out var currentIncludes);
                        m_IncludeBrowser.OnGUI(m_Settings.AssetIds, s => s, s => $"{s.Item2} {s.Item1}", s => new[] { s.Item2, s.Item1 }, (pair1, pair2) => {
                            using (new GUILayout.HorizontalScope()) {
                                if (currentIncludes?.Contains(pair1.Item1) ?? false) {
                                    GUILayout.Label(" ");
                                } else {
                                    if (GUILayout.Button("Include", GUILayout.ExpandWidth(false))) {
                                        EntityPartStorage.perSave.IncludeByName.TryGetValue(PickedUnit.UniqueId, out var tmpIncludes);
                                        if (tmpIncludes == null) tmpIncludes = new();
                                        tmpIncludes.Add(pair1.Item1);
                                        EntityPartStorage.perSave.IncludeByName[PickedUnit.UniqueId] = tmpIncludes;
                                        EntityPartStorage.SavePerSaveSettings();
                                    }
                                }
                                GUILayout.Label($"    {pair1.Item2}", GUILayout.ExpandWidth(false));
                                GUILayout.TextArea($"    {pair1.Item1}", GUILayout.ExpandWidth(false));
                            }
                        });
                        GUILayout.Label("------------------------------------------");
                        GUILayout.Label("Current Includes:");
                        EntityPartStorage.perSave.IncludeByName.TryGetValue(PickedUnit.UniqueId, out currentIncludes);
                        if (currentIncludes?.Count > 0) {
                            foreach (var eeName in currentIncludes.ToList()) {
                                using (new GUILayout.HorizontalScope()) {
                                    if (GUILayout.Button("Remove Inclusion", GUILayout.ExpandWidth(false))) {
                                        EntityPartStorage.perSave.IncludeByName.TryGetValue(PickedUnit.UniqueId, out var tmpIncludes);
                                        if (tmpIncludes == null) tmpIncludes = new();
                                        tmpIncludes.Remove(eeName);
                                        EntityPartStorage.perSave.IncludeByName[PickedUnit.UniqueId] = tmpIncludes;
                                        EntityPartStorage.SavePerSaveSettings();
                                    }
                                    string itemName = m_Settings.AssetIds.Where(t => t.Item1 == eeName).Select(t => t.Item2).FirstOrDefault();
                                    GUILayout.Label($"    {itemName}", GUILayout.ExpandWidth(false));
                                    GUILayout.TextArea($"    {eeName}", GUILayout.ExpandWidth(false));
                                }
                            }
                        }
                    }
                }
            }
        }
        DrawDiv();
    }

    private static void ColorGUI() {
        m_OpenedColorSection = GUILayout.Toggle(m_OpenedColorSection, "Show Color Section", GUILayout.ExpandWidth(false));
        if (m_OpenedColorSection) {
            using (new GUILayout.HorizontalScope()) {
                GUILayout.Space(25);
                EntityPartStorage.perSave.RampOverrideByName.TryGetValue(PickedUnit!.UniqueId, out var overrides);
                EntityPartStorage.perSave.CustomColorsByName.TryGetValue(PickedUnit.UniqueId, out var customOverrides);
                overrides ??= new();
                customOverrides ??= new();
                using (new GUILayout.VerticalScope()) {
                    foreach (var entry in PickedUnit.View.CharacterAvatar.EquipmentEntities.Union(PickedUnit.View.CharacterAvatar.SavedEquipmentEntities.Select(l => new EquipmentEntityLink() { AssetId = l.AssetId }.LoadAsset()))) {
                        var ee = entry;
                        var eeName = ee.name.IsNullOrEmpty() ? ee.ToString() : ee.name;
                        using (new GUILayout.HorizontalScope()) {
                            GUILayout.Label($"{eeName}:", GUILayout.Width(400));
                            using (new GUILayout.VerticalScope()) {
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
                                bool isActive = m_PrimaryTexCreator != null && (m_PrimaryTexCreator.EEName == eeName);
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
                                bool isActive2 = m_SecondaryTexCreator != null && (m_SecondaryTexCreator.EEName == eeName);
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
                                /*
                                if (oldOverrides.Item3 != null) {
                                    GUILayout.Label($"Current Main Texture Override: {oldOverrides.Item3}");
                                }
                                isActive = ShowCustomMain && (showMainForEE == eeName);
                                ShouldShowCustomMain = GUILayout.Toggle(isActive, "Show Custom Main Texture Creator", GUILayout.ExpandWidth(false));
                                if ((isActive && !ShouldShowCustomMain) || (!isActive && ShouldShowCustomMain)) {
                                    ShowCustomMain = ShouldShowCustomMain;
                                    colorPicker3 = null;
                                }
                                if (ShouldShowCustomMain) {
                                    if (colorPicker3 == null) {
                                        colorPicker3 = oldOverrides.Item3 ?? new(doClamp ? TextureWrapMode.Clamp : TextureWrapMode.Repeat);
                                        width3 = colorPicker3.width;
                                        height3 = colorPicker3.height;
                                        colorPicker3col = 0;
                                    }
                                    showMainForEE = eeName;
                                    if (ColorPickerGUI(3)) {
                                        oldOverrides.Item3 = AccessTools.MakeDeepCopy<CustomColorTex>(colorPicker3);
                                        customOverrides[eeName] = oldOverrides;
                                        EntityPartStorage.perSave.CustomColorsByName[pickedUnit.UniqueId] = customOverrides;
                                        EntityPartStorage.SavePerSaveSettings();
                                        SetColorPair(ee, new() { PrimaryIndex = -1, SecondaryIndex = -1 });
                                    }
                                }
                                */
                            }
                            Helpers.GetClothColorsProfile(entry, out var colorPresets, false);
                            if (colorPresets != null) {
                                if (overrides.ContainsKey(eeName)) {
                                    if (GUILayout.Button("Remove Color Override", GUILayout.ExpandWidth(false))) {
                                        overrides.Remove(eeName);
                                        EntityPartStorage.perSave.RampOverrideByName[PickedUnit.UniqueId] = overrides;
                                        EntityPartStorage.SavePerSaveSettings();
                                    }
                                }
                                GUILayout.Space(50);
                                using (new GUILayout.VerticalScope()) {
                                    foreach (var pair in colorPresets.IndexPairs) {
                                        using (new GUILayout.HorizontalScope()) {
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

        DrawDiv();
    }
}
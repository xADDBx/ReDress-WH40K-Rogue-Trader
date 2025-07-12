using HarmonyLib;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UI.MVVM.VM.CharGen;
using Kingmaker.View;
using Kingmaker.Visual.CharacterSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityModManagerNet;
using Kingmaker.ResourceLinks;
using static UnityModManagerNet.UnityModManager;
using Kingmaker.PubSubSystem.Core;
using Kingmaker.PubSubSystem;
using Kingmaker.Utility.UnityExtensions;
using static ReDress.UIHelpers;

namespace ReDress;

static class Main {
    internal static ModEntry mod;
    internal static Harmony HarmonyInstance;
    internal static UnityModManager.ModEntry.ModLogger log;
    internal static bool doExcludeNewEEs = false;
    public static Settings settings;
    internal static bool Load(UnityModManager.ModEntry modEntry) {
        log = modEntry.Logger;
        mod = modEntry;
        modEntry.OnGUI = OnGUI;
        modEntry.OnHideGUI = OnHideGUI;
        modEntry.OnSaveGUI = OnSaveGUI;
        settings = Settings.Load<Settings>(modEntry);
        HarmonyInstance = new Harmony(modEntry.Info.Id);
        HarmonyInstance.PatchAll(Assembly.GetExecutingAssembly());
        return true;
    }
    static void OnSaveGUI(ModEntry modEntry) {
        settings.Save(modEntry);
    }

    internal static Exception error = null;
    internal static bool printError = false;
    internal static bool shouldResetError = false;
    internal static BaseUnitEntity pickedUnit = null;
    internal static Outfit selectedOutfit = Outfit.Current;
    internal static bool openedGuide = false;
    internal static bool shouldOpenGuide = false;
    internal static bool openedExclude = false;
    internal static bool shouldOpenExclude = false;
    internal static bool openedInclude = false;
    internal static bool shouldOpenInclude = false;
    internal static bool shouldOpenClothingSection = false;
    internal static bool openedClothingSection = false;
    internal static bool openedColorSection = false;
    internal static string showPrimaryForEE = "";
    internal static string showSecondaryForEE = "";
    internal static bool ShowCustomPrimary = false;
    internal static bool ShowCustomSecondary = false;
    internal static bool ShouldShowCustomPrimary = false;
    internal static bool ShouldShowCustomSecondary = false;
    internal static bool shouldOpenColorSection = false;
    internal static Browser<(string, string), (string, string)> includeBrowser = new(true);
    static void OnHideGUI(UnityModManager.ModEntry modEntry) {
        openedGuide = false;
        openedExclude = false;
        openedInclude = false;
        openedClothingSection = false;
        openedColorSection = false;
        selectedOutfit = Outfit.Current;
    }

    static void OnGUI(UnityModManager.ModEntry modEntry) {
        if (Event.current.type == EventType.Layout && (error != null || shouldResetError)) {
            if (!shouldResetError) {
                printError = true;
            } else {
                printError = false;
                error = null;
                shouldResetError = false;
            }
        }
        if (printError) {
            GUILayout.Label(error.ToString());
            if (GUILayout.Button("Reset Error")) {
                shouldResetError = true;
            }
        } else {
            try {
                shouldOpenGuide = GUILayout.Toggle(shouldOpenGuide, "Show Guide", GUILayout.ExpandWidth(false));
                if (Event.current.type == EventType.Layout) {
                    openedGuide = shouldOpenGuide;
                }
                if (openedGuide) {
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

                    int selectedIndex = pickedUnit != null ? units.IndexOf(pickedUnit) : 0;
                    if (selectedIndex < 0) {
                        selectedIndex = 0;
                        pickedUnit = null;
                    }
                    int newIndex = GUILayout.SelectionGrid(selectedIndex, units.Select(m => m.CharacterName).ToArray(), 6);
                    if (selectedIndex != newIndex || pickedUnit == null) {
                        pickedUnit = units[newIndex];
                        selectedOutfit = Outfit.Current;
                    }
                    DrawDiv();
                    if (GUILayout.Button("Change Appeareance", GUILayout.ExpandWidth(false))) {
                        Helpers.OpenAppeareanceChanger(pickedUnit);
                    }
                    DrawDiv();
                    shouldOpenClothingSection = GUILayout.Toggle(shouldOpenClothingSection, "Show Clothing Section", GUILayout.ExpandWidth(false));
                    if (Event.current.type == EventType.Layout) {
                        openedClothingSection = shouldOpenClothingSection;
                    }
                    if (openedClothingSection) {
                        using (new GUILayout.HorizontalScope()) {
                            GUILayout.Space(25);
                            using (new GUILayout.VerticalScope()) {
                                GUILayout.Label("Set Outfit to the following:", GUILayout.ExpandWidth(false));
                                Outfit[] outfits = (Outfit[])Enum.GetValues(typeof(Outfit));
                                var selectedIndex2 = Array.IndexOf(outfits, selectedOutfit);
                                newIndex = GUILayout.SelectionGrid(selectedIndex2, outfits.Select(m => m.ToDescriptionString()).ToArray(), 5);
                                if (selectedIndex2 != newIndex) {
                                    selectedOutfit = outfits[newIndex];
                                    if (selectedOutfit != Outfit.Current) {
                                        if (selectedOutfit == Outfit.Naked) {
                                            EntityPartStorage.perSave.AddClothes.Remove(pickedUnit.UniqueId);
                                            EntityPartStorage.perSave.NakedFlag[pickedUnit.UniqueId] = true;
                                        } else {
                                            var kee = ResourcesLibrary.BlueprintsCache.Load(Helpers.JobClothesIDs[selectedOutfit]) as KingmakerEquipmentEntity;
                                            EntityPartStorage.perSave.AddClothes[pickedUnit.UniqueId] = pickedUnit.Gender == Kingmaker.Blueprints.Base.Gender.Male ? kee.m_MaleArray.Select(f => f.AssetId).ToList() : kee.m_FemaleArray.Select(f => f.AssetId).ToList();
                                            EntityPartStorage.perSave.NakedFlag.Remove(pickedUnit.UniqueId);
                                        }
                                    }
                                    EntityPartStorage.SavePerSaveSettings();
                                }

                                DrawDiv();
                                if (GUILayout.Button("Reset Outfit", GUILayout.ExpandWidth(false))) {
                                    EntityPartStorage.perSave.AddClothes.Remove(pickedUnit.UniqueId);
                                    EntityPartStorage.perSave.NakedFlag.Remove(pickedUnit.UniqueId);
                                    EntityPartStorage.SavePerSaveSettings();
                                }

                                DrawDiv();
                                shouldOpenExclude = GUILayout.Toggle(shouldOpenExclude, "Show Exclude Section", GUILayout.ExpandWidth(false));
                                if (Event.current.type == EventType.Layout) {
                                    openedExclude = shouldOpenExclude;
                                }

                                if (openedExclude) {
                                    if (GUILayout.Button("Reset Excludes", GUILayout.ExpandWidth(false))) {
                                        EntityPartStorage.perSave.ExcludeByName.Remove(pickedUnit.UniqueId);
                                        EntityPartStorage.SavePerSaveSettings();
                                    }
                                    foreach (var ee in pickedUnit.View.CharacterAvatar.EquipmentEntities.Union(pickedUnit.View.CharacterAvatar.SavedEquipmentEntities.Select(l => new EquipmentEntityLink() { AssetId = l.AssetId }.LoadAsset()))) {
                                        using (new GUILayout.HorizontalScope()) {
                                            if (GUILayout.Button("Exclude", GUILayout.ExpandWidth(false))) {
                                                EntityPartStorage.perSave.ExcludeByName.TryGetValue(pickedUnit.UniqueId, out var tmpExcludes);
                                                if (tmpExcludes == null) tmpExcludes = new();
                                                tmpExcludes.Add(ee.name);
                                                EntityPartStorage.perSave.ExcludeByName[pickedUnit.UniqueId] = tmpExcludes;
                                                EntityPartStorage.SavePerSaveSettings();
                                            }
                                            GUILayout.Label($"    {ee?.name ?? "Null????????????"}");
                                        }
                                    }
                                    GUILayout.Label("------------------------------------------");
                                    GUILayout.Label("Current Excludes:");
                                    EntityPartStorage.perSave.ExcludeByName.TryGetValue(pickedUnit.UniqueId, out var currentExcludes);
                                    if (currentExcludes?.Count > 0) {
                                        foreach (var eeName in currentExcludes.ToList()) {
                                            using (new GUILayout.HorizontalScope()) {
                                                if (GUILayout.Button("Remove Exclusion", GUILayout.ExpandWidth(false))) {
                                                    EntityPartStorage.perSave.ExcludeByName.TryGetValue(pickedUnit.UniqueId, out var tmpExcludes);
                                                    if (tmpExcludes == null) tmpExcludes = new();
                                                    tmpExcludes.Remove(eeName);
                                                    EntityPartStorage.perSave.ExcludeByName[pickedUnit.UniqueId] = tmpExcludes;
                                                    EntityPartStorage.SavePerSaveSettings();
                                                }
                                                GUILayout.Label($"    {eeName}");
                                            }
                                        }
                                    }
                                }

                                DrawDiv();
                                GUILayout.Label("Opening the Include section might make the game freeze for a few seconds to build a cache of existing EquipmentEntities.");
                                shouldOpenInclude = GUILayout.Toggle(shouldOpenInclude, "Show Include Section", GUILayout.ExpandWidth(false));
                                if (Event.current.type == EventType.Layout) {
                                    openedInclude = shouldOpenInclude;
                                }

                                if (openedInclude) {
                                    if (GUILayout.Button("Reset Includes", GUILayout.ExpandWidth(false))) {
                                        EntityPartStorage.perSave.IncludeByName.Remove(pickedUnit.UniqueId);
                                        EntityPartStorage.SavePerSaveSettings();
                                    }
                                    EntityPartStorage.perSave.IncludeByName.TryGetValue(pickedUnit.UniqueId, out var currentIncludes);
                                    includeBrowser.OnGUI(settings.AssetIds, s => s, s => $"{s.Item2} {s.Item1}", s => new[] { s.Item2, s.Item1 }, (pair1, pair2) => {
                                        using (new GUILayout.HorizontalScope()) {
                                            if (currentIncludes?.Contains(pair1.Item1) ?? false) {
                                                GUILayout.Label(" ");
                                            } else {
                                                if (GUILayout.Button("Include", GUILayout.ExpandWidth(false))) {
                                                    EntityPartStorage.perSave.IncludeByName.TryGetValue(pickedUnit.UniqueId, out var tmpIncludes);
                                                    if (tmpIncludes == null) tmpIncludes = new();
                                                    tmpIncludes.Add(pair1.Item1);
                                                    EntityPartStorage.perSave.IncludeByName[pickedUnit.UniqueId] = tmpIncludes;
                                                    EntityPartStorage.SavePerSaveSettings();
                                                }
                                            }
                                            GUILayout.Label($"    {pair1.Item2}", GUILayout.ExpandWidth(false));
                                            GUILayout.TextArea($"    {pair1.Item1}", GUILayout.ExpandWidth(false));
                                        }
                                    });
                                    GUILayout.Label("------------------------------------------");
                                    GUILayout.Label("Current Includes:");
                                    EntityPartStorage.perSave.IncludeByName.TryGetValue(pickedUnit.UniqueId, out currentIncludes);
                                    if (currentIncludes?.Count > 0) {
                                        foreach (var eeName in currentIncludes.ToList()) {
                                            using (new GUILayout.HorizontalScope()) {
                                                if (GUILayout.Button("Remove Inclusion", GUILayout.ExpandWidth(false))) {
                                                    EntityPartStorage.perSave.IncludeByName.TryGetValue(pickedUnit.UniqueId, out var tmpIncludes);
                                                    if (tmpIncludes == null) tmpIncludes = new();
                                                    tmpIncludes.Remove(eeName);
                                                    EntityPartStorage.perSave.IncludeByName[pickedUnit.UniqueId] = tmpIncludes;
                                                    EntityPartStorage.SavePerSaveSettings();
                                                }
                                                string itemName = settings.AssetIds.Where(t => t.Item1 == eeName).Select(t => t.Item2).FirstOrDefault();
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
                    shouldOpenColorSection = GUILayout.Toggle(shouldOpenColorSection, "Show Color Section", GUILayout.ExpandWidth(false));
                    if (Event.current.type == EventType.Layout) {
                        openedColorSection = shouldOpenColorSection;
                    }
                    if (openedColorSection) {
                        using (new GUILayout.HorizontalScope()) {
                            GUILayout.Space(25);
                            EntityPartStorage.perSave.RampOverrideByName.TryGetValue(pickedUnit.UniqueId, out var overrides);
                            EntityPartStorage.perSave.CustomColorsByName.TryGetValue(pickedUnit.UniqueId, out var customOverrides);
                            overrides ??= new();
                            customOverrides ??= new();
                            using (new GUILayout.VerticalScope()) {
                                foreach (var entry in pickedUnit.View.CharacterAvatar.EquipmentEntities.Union(pickedUnit.View.CharacterAvatar.SavedEquipmentEntities.Select(l => new EquipmentEntityLink() { AssetId = l.AssetId }.LoadAsset()))) {
                                    var ee = entry;
                                    var eeName = ee.name.IsNullOrEmpty() ? ee.ToString() : ee.name;
                                    using (new GUILayout.HorizontalScope()) {
                                        GUILayout.Label($"{eeName}:", GUILayout.Width(400));
                                        using (new GUILayout.VerticalScope()) {
                                            if (customOverrides.ContainsKey(eeName)) {
                                                if (GUILayout.Button("Remove Custom Color Override", GUILayout.ExpandWidth(false))) {
                                                    customOverrides.Remove(eeName);
                                                    EntityPartStorage.perSave.CustomColorsByName[pickedUnit.UniqueId] = customOverrides;
                                                    EntityPartStorage.SavePerSaveSettings();
                                                }
                                            }
                                            customOverrides.TryGetValue(eeName, out var oldOverrides);
                                            if (oldOverrides.Item1 != null) {
                                                GUILayout.Label($"Current Primary Override: {oldOverrides.Item1}");
                                            }
                                            bool isActive = ShowCustomPrimary && (showPrimaryForEE == eeName);
                                            ShouldShowCustomPrimary = GUILayout.Toggle(isActive, "Show Custom Primary Color Picker", GUILayout.ExpandWidth(false));
                                            if ((isActive && !ShouldShowCustomPrimary) || (!isActive && ShouldShowCustomPrimary)) {
                                                ShowCustomPrimary = ShouldShowCustomPrimary;
                                                CustomColors.colorPicker1 = null;
                                            }
                                            if (ShouldShowCustomPrimary) {
                                                if (CustomColors.colorPicker1 == null) {
                                                    CustomColors.colorPicker1 = oldOverrides.Item1 ?? new(CustomColors.doClamp ? TextureWrapMode.Clamp : TextureWrapMode.Repeat);
                                                    CustomColors.width1 = CustomColors.colorPicker1.width;
                                                    CustomColors.height1 = CustomColors.colorPicker1.height;
                                                    CustomColors.colorPicker1col = 0;
                                                }
                                                showPrimaryForEE = eeName;
                                                if (CustomColors.ColorPickerGUI(1)) {
                                                    oldOverrides.Item1 = AccessTools.MakeDeepCopy<EntityPartStorage.CustomColorTex>(CustomColors.colorPicker1);
                                                    customOverrides[eeName] = oldOverrides;
                                                    EntityPartStorage.perSave.CustomColorsByName[pickedUnit.UniqueId] = customOverrides;
                                                    EntityPartStorage.SavePerSaveSettings();
                                                    SetColorPair(ee, new() { PrimaryIndex = -1, SecondaryIndex = -1 });
                                                }
                                            }
                                            if (oldOverrides.Item2 != null) {
                                                GUILayout.Label($"Current Secondary Override: {oldOverrides.Item2}");
                                            }
                                            isActive = ShowCustomSecondary && (showSecondaryForEE == eeName);
                                            ShouldShowCustomSecondary = GUILayout.Toggle(isActive, "Show Custom Secondary Color Picker", GUILayout.ExpandWidth(false));
                                            if ((isActive && !ShouldShowCustomSecondary) || (!isActive && ShouldShowCustomSecondary)) {
                                                ShowCustomSecondary = ShouldShowCustomSecondary;
                                                CustomColors.colorPicker2 = null;
                                            }
                                            if (ShouldShowCustomSecondary) {
                                                if (CustomColors.colorPicker2 == null) {
                                                    CustomColors.colorPicker2 = oldOverrides.Item2 ?? new(CustomColors.doClamp ? TextureWrapMode.Clamp : TextureWrapMode.Repeat);
                                                    CustomColors.width2 = CustomColors.colorPicker2.width;
                                                    CustomColors.height2 = CustomColors.colorPicker2.height;
                                                    CustomColors.colorPicker2col = 0;
                                                }
                                                showSecondaryForEE = eeName;
                                                if (CustomColors.ColorPickerGUI(2)) {
                                                    oldOverrides.Item2 = AccessTools.MakeDeepCopy<EntityPartStorage.CustomColorTex>(CustomColors.colorPicker2);
                                                    customOverrides[eeName] = oldOverrides;
                                                    EntityPartStorage.perSave.CustomColorsByName[pickedUnit.UniqueId] = customOverrides;
                                                    EntityPartStorage.SavePerSaveSettings();
                                                    SetColorPair(ee, new() { PrimaryIndex = -1, SecondaryIndex = -1 });
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
                                        GetClothColorsProfile(entry, out var colorPresets, false);
                                        if (colorPresets != null) {
                                            if (overrides.ContainsKey(eeName)) {
                                                if (GUILayout.Button("Remove Color Override", GUILayout.ExpandWidth(false))) {
                                                    overrides.Remove(eeName);
                                                    EntityPartStorage.perSave.RampOverrideByName[pickedUnit.UniqueId] = overrides;
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
                                                            EntityPartStorage.perSave.RampOverrideByName[pickedUnit.UniqueId] = overrides;
                                                            EntityPartStorage.SavePerSaveSettings();
                                                            SetColorPair(ee, pair);
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
                    settings.ShouldExcludeNewEEs = GUILayout.Toggle(settings.ShouldExcludeNewEEs, "Automatically exclude new items on all characters");
                } else {
                    GUILayout.Label("Load a save first!", GUILayout.ExpandWidth(false));
                }
            } catch (Exception ex) {
                log.Log(ex.ToString());
                error = ex;
            }
        }
    }
    public static CharacterColorsProfile GetClothColorsProfile(EquipmentEntity equipmentEntity, out RampColorPreset colorPreset, bool secondary) {
        if (!(equipmentEntity == null)) {
            CharacterColorsProfile characterColorsProfile = (secondary ? equipmentEntity.SecondaryColorsProfile : equipmentEntity.PrimaryColorsProfile);
            if (characterColorsProfile != null) {
                colorPreset = equipmentEntity.ColorPresets;
                return characterColorsProfile;
            }
        }
        colorPreset = null;
        return null;
    }

    public static void SetColorPair(EquipmentEntity ee, RampColorPreset.IndexSet pair) {
        if (pair.PrimaryIndex >= 0) {
            pickedUnit.View.CharacterAvatar.SetPrimaryRampIndex(ee, pair.PrimaryIndex);
        }
        if (pair.SecondaryIndex >= 0) {
            pickedUnit.View.CharacterAvatar.SetSecondaryRampIndex(ee, pair.SecondaryIndex);
        }
        pickedUnit.View.CharacterAvatar.IsAtlasesDirty = true;
        EventBus.RaiseEvent<IUnitVisualChangeHandler>(pickedUnit, delegate (IUnitVisualChangeHandler h) {
            h.HandleUnitChangeEquipmentColor(pair.PrimaryIndex, false);
        }, true);
    }
}
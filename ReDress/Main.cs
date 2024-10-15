using HarmonyLib;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UI.MVVM.VM.CharGen;
using Kingmaker.UnitLogic.Parts;
using Kingmaker.View;
using Kingmaker.Visual.CharacterSystem;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityModManagerNet;
using Kingmaker.ResourceLinks;
using System.Reflection.Emit;
using System.Xml.Linq;
using static UnityModManagerNet.UnityModManager;
using Kingmaker.Blueprints.Area;
using Kingmaker.EntitySystem.Persistence;
using Kingmaker.UnitLogic.Progression.Features;
using Kingmaker.PubSubSystem.Core;
using Kingmaker.PubSubSystem;
using Microsoft.Cci.Pdb;
using Kingmaker.UI.DollRoom;
using static ReDress.EntityPartStorage;
using Kingmaker.Utility.UnityExtensions;
using Kingmaker.UI.Common;

namespace ReDress;

#if DEBUG
[EnableReloading]
#endif
static class Main {
    internal static ModEntry mod;
    internal static Harmony HarmonyInstance;
    internal static UnityModManager.ModEntry.ModLogger log;
    internal static bool isInRoom = false;
    public static Settings settings;
    static bool Load(UnityModManager.ModEntry modEntry) {
        log = modEntry.Logger;
        mod = modEntry;
#if DEBUG
        modEntry.OnUnload = OnUnload;
#endif
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
    internal static BaseUnitEntity cachedUnit = null;
    internal static bool openedGuide = false;
    internal static bool shouldOpenGuide = false;
    internal static bool openedExclude = false;
    internal static bool shouldOpenExclude = false;
    internal static bool openedInclude = false;
    internal static bool shouldOpenInclude = false;
    internal static bool openedColorPicker = false;
    internal static bool shouldOpenColorPicker = false;
    internal static bool colorPickerIsPrimary = false;
    internal static bool shouldOpenClothingSection = false;
    internal static bool openedClothingSection = false;
    internal static bool openedColorSection = false;
    internal static string showPrimaryForEE = "";
    internal static string showSecondaryForEE = "";
    internal static string showMainForEE = "";
    internal static bool ShowCustomPrimary = false;
    internal static bool ShowCustomSecondary = false;
    internal static bool ShowCustomMain = false;
    internal static bool ShouldShowCustomPrimary = false;
    internal static bool ShouldShowCustomSecondary = false;
    internal static bool ShouldShowCustomMain = false;
    internal static bool shouldOpenColorSection = false;
    internal static CustomColorTex colorPicker1 = null;
    internal static CustomColorTex colorPicker2 = null;
    internal static CustomColorTex colorPicker3 = null;
    internal static int colorPicker1col = 0;
    internal static int colorPicker2col = 0;
    internal static int colorPicker3col = 0;
    internal static string colorPickerItem = "";
    internal static Browser<(string, string), (string, string)> includeBrowser = new(true);
    internal static Browser<Texture2D, Texture2D> rampOverrideBrowser = new(true);
    static void OnHideGUI(UnityModManager.ModEntry modEntry) {
        openedGuide = false;
        openedExclude = false;
        openedInclude = false;
        openedColorPicker = false;
        openedClothingSection = false;
        openedColorSection = false;
        colorPickerItem = "";
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
                            GUILayout.Label("<b>For any changes to take effect you need to save and reload.</b>", GUILayout.ExpandWidth(false));
                            GUILayout.Label("The Include Section allows you to pick from all the visual entities in the game and add them to your character.");
                            GUILayout.Label("Together with the Exclude Section this <i>basically</i> allows building arbitrary outfits using the outfit parts built into the game.");
                        }
                    }
                }
                DrawDiv();
                var units = new List<BaseUnitEntity>() { Game.Instance?.Player?.MainCharacterEntity };
                units.AddRange(Game.Instance.Player.ActiveCompanions ?? new());
                units = units.Where(u => u != null).ToList();
                if (units.Count > 0) {
                    GUILayout.Label("Character to change:");

                    int selectedIndex = pickedUnit != null ? Array.IndexOf(units.ToArray(), pickedUnit) : 0;
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
                    if (GUILayout.Button("Change Appeareance (opens character creation dialog).", GUILayout.ExpandWidth(false))) {
                        cachedUnit = pickedUnit;
                        UnityModManager.UI.Instance.ToggleWindow();
                        isInRoom = true;
                        Game.Instance.Player.CreateCustomCompanion(newCompanion => {
                            try {
                                isInRoom = false;
                                cachedUnit.ViewSettings.SetDoll(newCompanion.ViewSettings.Doll);
                            } catch (Exception ex) {
                                log.Log(ex.ToString());
                                error = ex;
                            }
                        }, null, CharGenConfig.CharGenCompanionType.Common);
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
                                if (selectedOutfit == Outfit.Naked) {
                                    EntityPartStorage.perSave.AddClothes.Remove(pickedUnit.UniqueId);
                                    EntityPartStorage.perSave.NakedFlag[pickedUnit.UniqueId] = true;
                                } else {
                                    var kee = ResourcesLibrary.BlueprintsCache.Load(JobClothesIDs[selectedOutfit]) as KingmakerEquipmentEntity;
                                    EntityPartStorage.perSave.AddClothes[pickedUnit.UniqueId] = pickedUnit.Gender == Kingmaker.Blueprints.Base.Gender.Male ? kee.m_MaleArray.Select(f => f.AssetId).ToList() : kee.m_FemaleArray.Select(f => f.AssetId).ToList();
                                    EntityPartStorage.perSave.NakedFlag.Remove(pickedUnit.UniqueId);
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
                            if (shouldOpenInclude && Cache.needsCacheRebuilt) {
                                Cache.RebuildCache();
                            }
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
                                            GUILayout.Label($"    {pair1.Item2}");
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
                                                colorPicker1 = null;
                                            }
                                            if (ShouldShowCustomPrimary) {
                                                if (colorPicker1 == null) {
                                                    colorPicker1 = oldOverrides.Item1 ?? new(doClamp ? TextureWrapMode.Clamp : TextureWrapMode.Repeat);
                                                    width1 = colorPicker1.width;
                                                    height1 = colorPicker1.height;
                                                    colorPicker1col = 0;
                                                }
                                                showPrimaryForEE = eeName;
                                                if (ColorPickerGUI(1)) {
                                                    oldOverrides.Item1 = AccessTools.MakeDeepCopy<CustomColorTex>(colorPicker1);
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
                                                colorPicker2 = null;
                                            }
                                            if (ShouldShowCustomSecondary) {
                                                if (colorPicker2 == null) {
                                                    colorPicker2 = oldOverrides.Item2 ?? new(doClamp ? TextureWrapMode.Clamp : TextureWrapMode.Repeat);
                                                    width2 = colorPicker2.width;
                                                    height2 = colorPicker2.height;
                                                    colorPicker2col = 0;
                                                }
                                                showSecondaryForEE = eeName;
                                                if (ColorPickerGUI(2)) {
                                                    oldOverrides.Item2 = AccessTools.MakeDeepCopy<CustomColorTex>(colorPicker2);
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
    public static int height1 = 1;
    public static int width1 = 1;
    public static int height2 = 1;
    public static int width2 = 1;
    public static int height3 = 1;
    public static int width3 = 1;
    public static bool doClamp = true;
    public static bool doRepeat = false;
    public static bool ColorPickerGUI(int ColorPicker) {
        CustomColor current;
        if (ColorPicker == 1) {
            ColorPickerGrid(ref colorPicker1, ref colorPicker1col, ref height1, ref width1);
            current = colorPicker1.colors[colorPicker1col];
        } else if (ColorPicker == 2) {
            ColorPickerGrid(ref colorPicker2, ref colorPicker2col, ref height2, ref width2);
            current = colorPicker2.colors[colorPicker2col];
        } else {
            ColorPickerGrid(ref colorPicker3, ref colorPicker3col, ref height3, ref width3);
            current = colorPicker3.colors[colorPicker3col];
        }
        using (new GUILayout.HorizontalScope()) {
            GUILayout.Space(20);
            using (new GUILayout.VerticalScope()) {
                GUILayout.Label("Custom RGB Color Picker");

                GUILayout.Label("Red: " + Mathf.RoundToInt(current.R * 255), GUILayout.ExpandWidth(false));
                current.R = GUILayout.HorizontalSlider(current.R, 0f, 1f, GUILayout.Width(500));

                GUILayout.Label("Green: " + Mathf.RoundToInt(current.G * 255), GUILayout.ExpandWidth(false));
                current.G = GUILayout.HorizontalSlider(current.G, 0f, 1f, GUILayout.Width(500));

                GUILayout.Label("Blue: " + Mathf.RoundToInt(current.B * 255), GUILayout.ExpandWidth(false));
                current.B = GUILayout.HorizontalSlider(current.B, 0f, 1f, GUILayout.Width(500));

                /*
                GUILayout.Label("Picked Color");
                GUIStyle colorStyle = new GUIStyle(GUI.skin.box);
                colorStyle.normal.background = MakeTex(2, 2, current);
                GUILayout.Box(GUIContent.none, colorStyle, GUILayout.Width(100), GUILayout.Height(100));
                */
                if (GUILayout.Button("Apply Custom Color", GUILayout.ExpandWidth(false))) {
                    return true;
                }
                return false;
            }
        }
    }
    public static void ColorPickerGrid(ref CustomColorTex customColors, ref int customColorIndex, ref int height, ref int width) {
        bool changedSize = false; 
        using (new GUILayout.HorizontalScope()) {
            GUILayout.Label("Texture Height: ", GUILayout.ExpandWidth(false));
            changedSize |= IntTextField(ref height, GUILayout.MinWidth(60), GUILayout.ExpandWidth(false));
        }
        using (new GUILayout.HorizontalScope()) {
            GUILayout.Label("Texture Width: ", GUILayout.ExpandWidth(false));
            changedSize |= IntTextField(ref width, GUILayout.MinWidth(60), GUILayout.ExpandWidth(false));
        }
        if (changedSize) {
            customColorIndex = Math.Max(customColorIndex, height * width - 1);
        }
        using (new GUILayout.HorizontalScope()) {
            bool changed = false;
            if (GUILayout.Toggle(doClamp, "Clamp Texture", GUILayout.ExpandWidth(false))) {
                doClamp = true;
                doRepeat = false;
                changed = true;
            }
            if (GUILayout.Toggle(doRepeat, "Repeat Texture", GUILayout.ExpandWidth(false))) {
                doClamp = false;
                doRepeat = true;
                changed = true;
            }
            if (changed) {
                customColors.wrapMode = doClamp ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            }
        }
        customColors.height = height;
        customColors.width = width;
        customColors.colors ??= new();
        while (customColors.colors.Count < width * height) {
            customColors.colors.Add(new());
        }
        while (customColors.colors.Count > width * height) {
            customColors.colors.Remove(customColors.colors.Last());
        }
        using (new GUILayout.VerticalScope()) {
            for (int i = 0; i < height; i++) {
                using (new GUILayout.HorizontalScope()) {
                    GUILayout.Space(20);
                    for (int j = 0; j < width; j++) {
                        GUIStyle colorStyle = new GUIStyle(GUI.skin.box);
                        int ind = i * width + j;
                        colorStyle.normal.background = customColors.colors[ind].MakeBoxTex();
                        int size = customColorIndex == ind ? 60 : 50;
                        if (GUILayout.Button(GUIContent.none, colorStyle, GUILayout.Width(size), GUILayout.Height(size))) {
                            customColorIndex = ind;
                        }
                    }
                }
            }
        }
    }
    public static bool IntTextField(ref int value, params GUILayoutOption[] options) {
        var text = GUILayout.TextField(value.ToString(), options);
        if (int.TryParse(text, out var num)) {
            if (num != value) {
                value = num;
                return true;
            }
        }
        return false;
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
    public enum Outfit {
        [Description("Unchanged")]
        Current,
        [Description("Criminal")]
        Criminal,
        [Description("Nobility")]
        Nobility,
        [Description("Commissar")]
        Commissar,
        [Description("Navy Officer")]
        Navy,
        [Description("Astra Militarum")]
        Militarum,
        [Description("Sanctioned Psyker")]
        Psyker,
        [Description("Ministorum Crusader")]
        Crusader,
        [Description("Navigator")]
        Navigator,
        [Description("None of the other (Naked for non-companions)")]
        Naked
    }
    internal static readonly Dictionary<Outfit, string> JobClothesIDs = new() { { Outfit.Criminal, "2415ba44fb9e4bd5b22f4f574b5f0cd8" }, { Outfit.Nobility, "86c28cf33b7e42ecb190cd6c1b2aa4cc" },
        { Outfit.Commissar, "28b88e24ffa341a7b1dfc286583f226d" }, {Outfit.Navy, "e28fc5d840134da892c24d24738ceb63" }, { Outfit.Militarum, "394e4b94f1284fefa4f47f1df0e42161" },
        { Outfit.Psyker,"76c6f2ce3e5d4c8d9cc0c22145d4d630" }, { Outfit.Crusader, "b63a4c7a04fd47dcb8c6dfc24f92f33d" }, { Outfit.Navigator, "3afcd2d9ccb24e85844857ba852c1d88" } };
    internal static Dictionary<string, HashSet<EquipmentEntity>> cachedLinks = new();
    [HarmonyPatch(typeof(PartUnitViewSettings), nameof(PartUnitViewSettings.Instantiate))]
    internal static class PartUnitViewSettings_Instantiate_Patch {
        [HarmonyPostfix]
        private static void Instantiate(PartUnitViewSettings __instance, ref UnitEntityView __result) {
            var context = __instance.Owner;
            EquipmentEntity_RepaintTextures_Patch.currentUID = context.UniqueId ?? null;
            log.Log($"Check for unit: {context.Name}-{context.UniqueId}");
            EntityPartStorage.perSave.AddClothes.TryGetValue(context.UniqueId, out var outfitIds);
            EntityPartStorage.perSave.IncludeByName.TryGetValue(context.UniqueId, out var includeIds);
            EntityPartStorage.perSave.ExcludeByName.TryGetValue(context.UniqueId, out var excludeIds);
            EntityPartStorage.perSave.NakedFlag.TryGetValue(context.UniqueId, out var nakedFlag);
            if (outfitIds?.Count > 0 || includeIds?.Count > 0 || excludeIds?.Count > 0) {
                log.Log("Found Overrides. Changing UnitEntityView.");
                var charac = __result.GetComponent<Character>();
                if (charac != null) {
                    if (excludeIds?.Count > 0) {
                        foreach (var ee in charac.EquipmentEntities.ToArray()) {
                            if (excludeIds.Contains(ee.name)) {
                                charac.EquipmentEntities.Remove(ee);
                            }
                        }
                        foreach (var eel in charac.SavedEquipmentEntities.ToArray()) {
                            eel.Load();
                            if (excludeIds.Contains(eel.m_Handle?.Object?.name)) {
                                charac.SavedEquipmentEntities.Remove(eel);
                            }
                        }
                    }
                    if (!cachedLinks.ContainsKey(context.UniqueId)) cachedLinks[context.UniqueId] = new();
                    if (outfitIds?.Count > 0 || nakedFlag) {
                        foreach (var job in JobClothesIDs.Values) {
                            var kee = ResourcesLibrary.BlueprintsCache.Load(job) as KingmakerEquipmentEntity;
                            foreach (var entry in kee?.m_MaleArray) {
                                charac.RemoveEquipmentEntity(entry);
                                cachedLinks[context.UniqueId].Add(entry.Load());
                            }
                            foreach (var entry in kee?.m_FemaleArray) {
                                charac.RemoveEquipmentEntity(entry);
                                cachedLinks[context.UniqueId].Add(entry.Load());
                            }
                        }
                    }
                    if (outfitIds?.Count > 0)
                        foreach (var id in outfitIds) {
                            var eel = new EquipmentEntityLink() { AssetId = id };
                            charac.EquipmentEntitiesForPreload.Add(eel);
                            var ee = eel.Load();
                            cachedLinks[context.UniqueId].Remove(ee);
                            charac.AddEquipmentEntity(ee);
                        }
                    if (includeIds?.Count > 0) {
                        foreach (var id in includeIds) {
                            var eel = new EquipmentEntityLink() { AssetId = id };
                            charac.EquipmentEntitiesForPreload.Add(eel);
                            var ee = eel.Load();
                            cachedLinks[context.UniqueId].Remove(ee);
                            charac.AddEquipmentEntity(ee);
                        }
                    }
                }
            }
        }
    }
    [HarmonyPatch(typeof(EquipmentEntity), nameof(EquipmentEntity.RepaintTextures), [typeof(EquipmentEntity.PaintedTextures), typeof(int), typeof(int)])]
    internal static class EquipmentEntity_RepaintTextures_Patch {
        internal static string currentUID;
        internal static (CustomColorTex, CustomColorTex, CustomColorTex) customOverride = (null, null, null);
        [HarmonyPrefix]
        private static void RepaintRextures(EquipmentEntity __instance, EquipmentEntity.PaintedTextures paintedTextures, ref int primaryRampIndex,ref int secondaryRampIndex) {
            customOverride = (null, null, null);
            if (currentUID == null) {
                log.Log(new System.Diagnostics.StackTrace().ToString());
                return;
            }
            if (EntityPartStorage.perSave.RampOverrideByName.TryGetValue(currentUID, out var overrides)) {
                var eeName = __instance.name ?? __instance.ToString();
                if (eeName == null) {
                    log.Log(new System.Diagnostics.StackTrace().ToString());
                    return;
                }
                if (overrides.TryGetValue(eeName, out var pair)) {
                    primaryRampIndex = pair.PrimaryIndex;
                    secondaryRampIndex = pair.SecondaryIndex;
                }
            }
            if (EntityPartStorage.perSave.CustomColorsByName.TryGetValue(currentUID, out var overrides2)) {
                var eeName = __instance.name ?? __instance.ToString();
                if (eeName == null) {
                    log.Log(new System.Diagnostics.StackTrace().ToString());
                    return;
                }
                if (overrides2.TryGetValue(eeName, out var customColor)) {
                    customOverride = (customColor.Item1, customColor.Item2, customColor.Item3);
                }
            }
        }
    }
    [HarmonyPatch(typeof(CharacterTextureDescription), nameof(CharacterTextureDescription.Repaint))]
    internal static class CharacterTextureDescription_Repaint_Patch {
        private static Texture2D prim = null;
        private static Texture2D sec = null;
        private static Texture2D main = null;
        [HarmonyPrefix]
        private static void RepaintPre(CharacterTextureDescription __instance, RenderTexture rtToPaint, ref Texture2D primaryRamp, ref Texture2D secondaryRamp) {
            var ov = EquipmentEntity_RepaintTextures_Patch.customOverride;
            prim = primaryRamp;
            sec = secondaryRamp;
            main = __instance.ActiveTexture;
            if (ov.Item1 != null) {
                primaryRamp = ov.Item1.MakeTex();
            }
            if (ov.Item2 != null) {
                secondaryRamp = ov.Item2.MakeTex();
            }
            if (ov.Item3 != null) {
                __instance.ActiveTexture = ov.Item3.MakeTex();
            }
        }
        [HarmonyPostfix]
        private static void RepaintPost(CharacterTextureDescription __instance, ref Texture2D primaryRamp, ref Texture2D secondaryRamp) {
            primaryRamp = prim;
            secondaryRamp = sec;
            __instance.ActiveTexture = main;
            prim = null;
            sec = null;
            main = null;
        }
    }
    [HarmonyPatch(typeof(CharacterDollRoom), nameof(CharacterDollRoom.SetupUnit))]
    internal static class CharacterDollRoom_SetupUnit_Patch {
        [HarmonyPrefix]
        private static void SetupUnit(BaseUnitEntity player) {
            var tmp = player?.UniqueId ?? null;
            if (tmp != null) EquipmentEntity_RepaintTextures_Patch.currentUID = tmp;
        }
    }
    [HarmonyPatch(typeof(Character))]
    internal static class Character_Patch {
        [HarmonyPatch(nameof(Character.OnRenderObject))]
        [HarmonyPrefix]
        private static void OnRenderObject(Character __instance) {
            var tmp = __instance.GetComponent<UnitEntityView>()?.Data?.UniqueId ?? null;
            if (tmp != null) EquipmentEntity_RepaintTextures_Patch.currentUID = tmp;
            if (__instance.IsInDollRoom) {
                tmp = UIDollRooms.Instance.CharacterDollRoom?.m_OriginalAvatar?.GetComponent<UnitEntityView>()?.Data?.UniqueId ?? null;
                if (tmp != null) EquipmentEntity_RepaintTextures_Patch.currentUID = tmp;
            }
        }
        [HarmonyPatch(nameof(Character.MergeOverlays))]
        [HarmonyPrefix]
        private static void MergeOverlays(Character __instance) {
            var tmp = __instance.GetComponent<UnitEntityView>()?.Data?.UniqueId ?? null;
            if (tmp != null) EquipmentEntity_RepaintTextures_Patch.currentUID = tmp;
        }
        [HarmonyPatch(nameof(Character.DoUpdate))]
        [HarmonyPrefix]
        private static void DoUpdate(Character __instance) {
            var tmp = __instance.GetComponent<UnitEntityView>()?.Data?.UniqueId ?? null;
            if (tmp != null) EquipmentEntity_RepaintTextures_Patch.currentUID = tmp;
        }
        [HarmonyPatch(nameof(Character.AddEquipmentEntity), [typeof(EquipmentEntity), typeof(bool)])]
        [HarmonyPrefix]
        private static bool AddEquipmentEntity(Character __instance, EquipmentEntity ee) {
            try {
                var uniqueId = __instance.GetComponent<UnitEntityView>()?.Data?.UniqueId ?? null;
                if (uniqueId != null) {
                    EntityPartStorage.perSave.AddClothes.TryGetValue(uniqueId, out var ids);
                    EntityPartStorage.perSave.NakedFlag.TryGetValue(uniqueId, out var nakedFlag);
                    if (ids?.Count > 0 || nakedFlag) {
                        cachedLinks.TryGetValue(uniqueId, out var defaultClothes);
                        if (defaultClothes?.Contains(ee) ?? false) return false;
                    }
                    EntityPartStorage.perSave.ExcludeByName.TryGetValue(uniqueId, out var excludeIds);
                    if (excludeIds?.Count > 0) {
                        if (excludeIds.Contains(ee.name)) return false;
                    }
                }
            } catch (Exception e) {
                log.Log(e.ToString());
            }
            return true;
        }
    }
    [HarmonyPatch(typeof(Game))]
    internal static class Game_Patch {
        private static bool isLoadGame = false;
        [HarmonyPatch(nameof(Game.LoadGameForce))]
        [HarmonyPrefix]
        private static void LoadGameForce() {
            isLoadGame = true;
        }
        [HarmonyPatch(nameof(Game.LoadArea), [typeof(BlueprintArea), typeof(BlueprintAreaEnterPoint), typeof(AutoSaveMode), typeof(SaveInfo), typeof(Action)])]
        [HarmonyPrefix]
        private static void LoadArea() {
            EntityPartStorage.ClearCachedPerSave();
            if (isLoadGame) {
                cachedLinks.Clear();
                isLoadGame = false;
            }
        }
    }
    public static string ToDescriptionString(this Outfit val) {
        DescriptionAttribute[] attributes = (DescriptionAttribute[])val
           .GetType()
           .GetField(val.ToString())
           .GetCustomAttributes(typeof(DescriptionAttribute), false);
        return attributes.Length > 0 ? attributes[0].Description : string.Empty;
    }
    public static void DrawDiv() {
        using (new GUILayout.VerticalScope()) {
            GUILayout.Space(10);
        }
        float indent = 0;
        float height = 0;
        float width = 0;
        Color color = new(1f, 1f, 1f, 0.65f);
        Texture2D fillTexture = new(1, 1);
        var divStyle = new GUIStyle {
            fixedHeight = 1,
        };
        fillTexture.SetPixel(0, 0, color);
        fillTexture.Apply();
        divStyle.normal.background = fillTexture;
        if (divStyle.margin == null) {
            divStyle.margin = new RectOffset((int)indent, 0, 4, 4);
        } else {
            divStyle.margin.left = (int)indent + 3;
        }
        if (width > 0)
            divStyle.fixedWidth = width;
        else
            divStyle.fixedWidth = 0;
        GUILayout.Space((2f * height) / 3f);
        GUILayout.Box(GUIContent.none, divStyle);
        GUILayout.Space(height / 3f);
        using (new GUILayout.VerticalScope()) {
            GUILayout.Space(5);
        }
    }
    [HarmonyPatch(typeof(Traverse), nameof(Traverse.SetValue))]
    public static class Traverse_SetValue_Patch {
        [HarmonyFinalizer]
        public static Exception Catch(Exception __exception, Traverse __instance, ref Traverse __result) {
            if (__exception is FieldAccessException) {
                __result = __instance;
                return null;
            }
            return __exception;
        }
    }

#if DEBUG
    static bool OnUnload(UnityModManager.ModEntry modEntry) {
        HarmonyInstance.UnpatchAll(modEntry.Info.Id);
        return true;
    }
#endif
}
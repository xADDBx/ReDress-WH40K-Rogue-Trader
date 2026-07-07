global using static ReDress.ColorPresets;
global using static ReDress.TexturePresets;
using HarmonyLib;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.PubSubSystem.Core;
using Kingmaker.ResourceLinks;
using Kingmaker.Utility.UnityExtensions;
using Kingmaker.Visual.CharacterSystem;
using Owlcat.Runtime.Core;
using StbDxtSharp;
using System.Collections.Concurrent;
using System.Reflection;
using UnityEngine;
using UnityModManagerNet;
using static ReDress.UIHelpers;

namespace ReDress;

public static class Main {
    private enum CurrentColorWindow {
        None,
        PrimaryCustomTex,
        SecondaryCustomTex,
        Ramps
    }
    private enum GenderFilter {
        Any,
        Female,
        Male
    }
    private enum RaceFilter {
        Any,
        Human,
        SpaceMarine,
        Eldar
    }
    internal static UnityModManager.ModEntry Mod = null!;
    internal static Harmony HarmonyInstance = null!;
    internal static UnityModManager.ModEntry.ModLogger Log = null!;
    internal static Cache m_Settings => Cache.CacheInstance;
    internal static BaseUnitEntity? PickedUnit = null;

    private static CurrentColorWindow m_CurrentMode = CurrentColorWindow.None;
    private static string m_CurrentColorSectionEE = "";
    private static readonly ConcurrentQueue<Action> m_MainThreadTaskQueue = [];
    private static readonly ConcurrentQueue<Action> m_GuiThreadTaskQueue = [];
    private static Exception? m_Error = null;
    private static Outfit m_SelectedOutfit = Outfit.Current;
    private static bool m_OpenedGuide = false;
    private static bool m_OpenedExclude = false;
    private static bool m_OpenedInclude = false;
    private static bool m_OpenedClothingSection = false;
    private static bool m_OpenedColorSection = false;
    private static bool m_OpenedClothingSets = false;
    private static bool m_OpenedBodyPartSection = false;
    internal static CustomTexCreator? PrimaryTexCreator;
    internal static CustomTexCreator? SecondaryTexCreator;
    internal static Browser<string> IncludeBrowser = null!;
    private static float? m_IncludeBrowserLabelWidth = null;
    private static volatile GenderFilter m_GenderFilter = GenderFilter.Any;
    private static volatile RaceFilter m_RaceFilter = RaceFilter.Any;
    private static LiveEEPreview? m_Preview;
    private static float m_BrowserWidth = (int)(EffectiveWindowWidth() * 0.95f);
    private static readonly int[] m_CellsPerRowOptions = [2, 3, 4, 5, 6, 8];
    private static int CellsPerRow => Mathf.Clamp(m_Settings.PreviewCellsPerRow, 2, 8);
    private static float m_CellWidth = m_BrowserWidth / 4.0f;
    internal static bool Load(UnityModManager.ModEntry modEntry) {
        Log = modEntry.Logger;
        Mod = modEntry;
        modEntry.OnGUI = OnGUI;
        modEntry.OnHideGUI = OnHideGUI;
        modEntry.OnUpdate = OnUpdate;
        IncludeBrowser = new(s => $"{m_Settings.AssetMapping![s]} {s}", s => $"{m_Settings.AssetMapping![s]} {s}", null, (Action<IEnumerable<string>> a) => {
            a(m_Settings.AssetMapping!.Keys);
        }, false, (int)m_BrowserWidth, Math.Max(1, m_Settings.IncludePageSize));
        IncludeBrowser.ItemFilter = PassesEEFilters;
        ScheduleForGuiThread(IncludeBrowser.ForceShowAll);
        HarmonyInstance = new Harmony(modEntry.Info.Id);
        HarmonyInstance.PatchAll(Assembly.GetExecutingAssembly());
        return true;
    }
    private static void OnGUI(UnityModManager.ModEntry modEntry) {
        try {
            while (m_GuiThreadTaskQueue.TryDequeue(out var task)) {
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
                var useLive = m_Settings.UseLivePreviews;
                var newUseLive = GUILayout.Toggle(useLive, " Use Live Render Previews".Bold() + "   (drag: rotate, middle/shift drag: pan, ctrl+scroll: zoom, double-click: reset; turn off for the plain text UI)".Orange(), AutoWidth());
                if (newUseLive != useLive) {
                    m_Settings.UseLivePreviews = newUseLive;
                    m_Settings.Save();
                    if (!newUseLive) {
                        DisposePreview();
                    }
                }
                if (newUseLive) {
                    m_Preview ??= new();
                    m_CellWidth = m_BrowserWidth / CellsPerRow;
                    using (HorizontalScope()) {
                        GUILayout.Label("Preview cells per row: ", AutoWidth());
                        foreach (var n in m_CellsPerRowOptions) {
                            var label = n == CellsPerRow ? n.ToString().Orange().Bold() : n.ToString();
                            if (GUILayout.Button(label, AutoWidth())) {
                                m_Settings.PreviewCellsPerRow = n;
                                m_Settings.Save();
                            }
                        }
                        GUILayout.Label("   (fewer per row = larger previews)".Green(), AutoWidth());
                    }
                }
                DrawDiv();
                DisclosureToggle(ref m_OpenedGuide, "Show Explanations", AutoWidth());
                if (m_OpenedGuide) {
                    using (HorizontalScope()) {
                        GUILayout.Space(25);
                        using (VerticalScope()) {
                            GUILayout.Label("Character Creation Window".Orange(), AutoWidth());
                            using (HorizontalScope()) {
                                Space(20);
                                using (VerticalScope()) {
                                    GUILayout.Label("This feature is basically the mirror in the Captain's Quarters, just that it's possible to use it for every unit.".Green(), AutoWidth());
                                    GUILayout.Label("It allows going through the appeareance-related steps of the character creation again.".Green(), AutoWidth());
                                }
                            }
                            GUILayout.Label("Clothing Section".Orange(), AutoWidth());
                            using (HorizontalScope()) {
                                Space(20);
                                using (VerticalScope()) {
                                    GUILayout.Label("This section allows modifying the outfit pieces characters have. There are 3 subsections.".Green(), AutoWidth());
                                    LinkButton("Link to a Google Document showing all the EquipmentEntities; Made by andvarieve", "https://docs.google.com/document/d/1HYRtdrcfq7SbdqK_qmDXC_v9DUH2YY0z_k9lCBJ2SW4/edit?tab=t.0#heading=h.pynrd3mueab2");
                                    GUILayout.Label("Origin Outfit Subsection:".Green(), AutoWidth());
                                    using (HorizontalScope()) {
                                        Space(20);
                                        using (VerticalScope()) {
                                            GUILayout.Label("This allows changing your current origin outfit to one of the other ones.".Cyan(), AutoWidth());
                                            GUILayout.Label("Internally this is done by excluding the outfit parts of all other origins and including the ones of the origin you pick.".Cyan(), AutoWidth());
                                        }
                                    }
                                    GUILayout.Label("Exclude Subsection:".Green(), AutoWidth());
                                    using (HorizontalScope()) {
                                        Space(20);
                                        using (VerticalScope()) {
                                            GUILayout.Label("This section shows you the EquipmentEntities (EE, the pieces of clothing you wear and body parts) of the current unit.".Cyan(), AutoWidth());
                                            GUILayout.Label("Excluding an EE will cause it to disappear from the unit.".Cyan(), AutoWidth());
                                        }
                                    }
                                    GUILayout.Label("Include Subsection:".Green(), AutoWidth());
                                    using (HorizontalScope()) {
                                        Space(20);
                                        using (VerticalScope()) {
                                            GUILayout.Label("This section allows you to add additional EEs to the currently selected unit.".Cyan(), AutoWidth());
                                            GUILayout.Label("Turn off the \"Show All\" toggle to display only the EEs you are currently including (e.g. to make removing them easier).".Cyan(), AutoWidth());
                                        }
                                    }
                                }
                            }
                            GUILayout.Label("Color Section".Orange(), AutoWidth());
                            using (HorizontalScope()) {
                                Space(20);
                                using (VerticalScope()) {
                                    GUILayout.Label("This section shows a list of EEs on the current unit and <i>can</i> allow recoloring them.".Green(), AutoWidth());
                                    GUILayout.Label("Ramps are basically built-in variations of outfits. Not every EE has ramps. If an EE has ramps, you can select one of the preset pairs as override.".Green(), AutoWidth());
                                    GUILayout.Label("Each EE has a primary and a secondary ramp texture. How they are used (or if they are used at all) depends on the EE.".Green(), AutoWidth());
                                    GUILayout.Label("The Custom Color Pickers allow creating custom textures for the primary and secondary ramp.".Green(), AutoWidth());
                                }
                            }
                            GUILayout.Label("BodyPart Section".Orange(), AutoWidth());
                            using (HorizontalScope()) {
                                Space(20);
                                using (VerticalScope()) {
                                    GUILayout.Label("This section shows a list of EEs on the current unit and allows excluding parts of them.".Green(), AutoWidth());
                                    GUILayout.Label("EEs are often built from multiple body parts (e.g. Skirt, Belt, Upper Legs and Lower Legs).".Green(), AutoWidth());
                                    GUILayout.Label("Excluding specific body parts will therefore allow using only parts of an outfit.".Green(), AutoWidth());
                                }
                            }
                            GUILayout.Label("Baking".Orange(), AutoWidth());
                            using (HorizontalScope()) {
                                Space(20);
                                using (VerticalScope()) {
                                    GUILayout.Label("To optimize a bit, the game \"bakes\" the views of most non-playable characters in the game (including playable characters on the deck".Green(), AutoWidth());
                                    GUILayout.Label("This prevents any modifications you do to them from appearing.".Green(), AutoWidth());
                                    GUILayout.Label("ReDress allows \"Unbaking\", and it works by replacing the baked view with one from another character that is not baked.".Green(), AutoWidth());
                                    GUILayout.Label("It automatically adds all EEs that should be on the view to Includes and all EEs that are on the replacement view to Excludes; but the unbaked character might be missing some clothing you need to manually add!".Green(), AutoWidth());
                                }
                            }
                        }
                    }
                }
                DrawDiv();
                CharacterPicker.OnFilterPickerGUI(null, GUILayout.Width(0.98f * UnityModManager.Params.WindowWidth));
                DrawDiv();
                bool changed = CharacterPicker.OnCharacterPickerGUI(null, GUILayout.Width(0.98f * UnityModManager.Params.WindowWidth));
                GUILayout.Label("Pick the character you want to modify:");
                Space(5);
                if (changed) {
                    PickedUnit = CharacterPicker.CurrentUnit;
                    EntityPartStorage.perSave.IncludeByName.TryGetValue(PickedUnit!.UniqueId, out var tmp);
                    IncludeBrowser.QueueUpdateItems(tmp ?? []);
                    m_SelectedOutfit = Outfit.Current;
                }
                if (PickedUnit != CharacterPicker.CurrentUnit) {
                    PickedUnit = null;
                }
                DrawDiv();

                if (PickedUnit != null) {
                    if (PickedUnit.View?.CharacterAvatar?.BakedCharacter) {
                        if (GUILayout.Button("Unbake", AutoWidth())) {
                            EntityPartStorage.Unbake(PickedUnit);
                        }
                    } else {
                        if (EntityPartStorage.perSave.UnbakedChars.Contains(PickedUnit.UniqueId)) {
                            if (GUILayout.Button("Rebake (turn the appearance back to the default one)", AutoWidth())) {
                                EntityPartStorage.Rebake(PickedUnit);
                            }
                        }
                        if (GUILayout.Button("Open the Character Creation window for this character", AutoWidth())) {
                            Helpers.OpenAppeareanceChanger(PickedUnit);
                        }
                        DrawDiv();

                        ClothingGUI();

                        ColorGUI();

                        BodyPartGUI();
                    }
                } else {
                    GUILayout.Label("Please pick a unit to modify first!".Green(), AutoWidth());
                }
                if (GUILayout.Button("Force Refresh EE Cache", AutoWidth())) {
                    Cache.RebuildCache();
                }
            } catch (Exception ex) {
                Log.Log(ex.ToString());
                PickedUnit = null;
                m_OpenedGuide = false;
                m_OpenedExclude = false;
                m_OpenedInclude = false;
                m_OpenedClothingSection = false;
                m_OpenedColorSection = false;
                m_OpenedClothingSets = false;
                m_Error = ex;
            }
        }
    }

    private static void ClothingGUI() {
        DisclosureToggle(ref m_OpenedClothingSection, "Show Clothing Section", AutoWidth());
        if (m_OpenedClothingSection) {
            using (HorizontalScope()) {
                GUILayout.Space(25);
                using (VerticalScope()) {
                    DisclosureToggle(ref m_OpenedClothingSets, "Show Origin Outfit Section", AutoWidth());
                    if (m_OpenedClothingSets) {
                        using (HorizontalScope()) {
                            Space(25);
                            using (VerticalScope()) {
                                GUILayout.Label("Set Outfit to the following:", AutoWidth());
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
                                if (GUILayout.Button("Reset Outfit", AutoWidth())) {
                                    EntityPartStorage.perSave.AddClothes.Remove(PickedUnit!.UniqueId);
                                    EntityPartStorage.perSave.NakedFlag.Remove(PickedUnit.UniqueId);
                                    EntityPartStorage.SavePerSaveSettings();
                                }
                            }
                        }
                    }

                    DrawDiv();
                    DisclosureToggle(ref m_OpenedExclude, "Show Exclude Section", AutoWidth());
                    if (m_OpenedExclude) {
                        using (HorizontalScope()) {
                            Space(25);
                            using (VerticalScope()) {
                                if (GUILayout.Button("Reset Excludes", AutoWidth())) {
                                    EntityPartStorage.perSave.ExcludeByName.Remove(PickedUnit!.UniqueId);
                                    EntityPartStorage.SavePerSaveSettings();
                                }
                                EntityPartStorage.perSave.ExcludeByName.TryGetValue(PickedUnit!.UniqueId, out var tmpExcludes);
                                tmpExcludes ??= [];
                                void Exclude(string eeName) {
                                    tmpExcludes.Add(eeName);
                                    EntityPartStorage.perSave.ExcludeByName[PickedUnit!.UniqueId] = tmpExcludes;
                                    EntityPartStorage.SavePerSaveSettings();
                                }
                                var excludableEEs = new List<EquipmentEntity>();
                                foreach (var ee in PickedUnit!.View.CharacterAvatar.EquipmentEntities?.Union(PickedUnit.View.CharacterAvatar.SavedEquipmentEntities?.Select(l => new EquipmentEntityLink() { AssetId = l.AssetId }.LoadAsset()) ?? []) ?? []) {
                                    if (ee == null) {
                                        Log.Log($"Warning: Iterating over EEs of unit {PickedUnit.CharacterName} encountered disposed Unity Object");
                                        GUILayout.Label($"Error! EE is disposed.".Orange());
                                        continue;
                                    }
                                    if (tmpExcludes.Contains(ee.name)) {
                                        continue;
                                    }
                                    excludableEEs.Add(ee);
                                }
                                if (m_Settings.UseLivePreviews) {
                                    GUILayout.Label("The previews show how the unit would look ".Green() + "after".Orange() + " excluding the piece.".Green(), AutoWidth());
                                    DrawCellGrid(excludableEEs, ee => {
                                        using (new GUILayout.VerticalScope(GUILayout.Width(m_CellWidth))) {
                                            var rect = GUILayoutUtility.GetRect(m_CellWidth, m_CellWidth);
                                            m_Preview!.DrawCell(rect, $"rem:{ee.name}", PreviewSpec.Remove(ee.name));
                                            GUILayout.Label($"{ee.name}".Green(), Width(m_CellWidth));
                                            if (GUILayout.Button("Exclude", GUILayout.Width(m_CellWidth))) {
                                                Exclude(ee.name);
                                            }
                                        }
                                    });
                                } else {
                                    foreach (var ee in excludableEEs) {
                                        using (HorizontalScope()) {
                                            if (GUILayout.Button("Exclude", AutoWidth())) {
                                                Exclude(ee.name);
                                            }
                                            GUILayout.Label($"    {ee?.name ?? "Null????????????"}");
                                        }
                                    }
                                }
                                GUILayout.Label("------------------------------------------");
                                GUILayout.Label("Current Excludes:");
                                EntityPartStorage.perSave.ExcludeByName.TryGetValue(PickedUnit.UniqueId, out var currentExcludes);
                                void RemoveExclusion(string eeName) {
                                    EntityPartStorage.perSave.ExcludeByName.TryGetValue(PickedUnit!.UniqueId, out var tmpExcludes2);
                                    tmpExcludes2 ??= [];
                                    tmpExcludes2.Remove(eeName);
                                    EntityPartStorage.perSave.ExcludeByName[PickedUnit.UniqueId] = tmpExcludes2;
                                    EntityPartStorage.SavePerSaveSettings();
                                }
                                if (currentExcludes?.Count > 0) {
                                    if (m_Settings.UseLivePreviews) {
                                        GUILayout.Label("The previews show how the unit would look ".Green() + "after".Orange() + " removing the exclusion.".Green(), AutoWidth());
                                        DrawCellGrid(currentExcludes.ToList(), eeName => {
                                            using (new GUILayout.VerticalScope(GUILayout.Width(m_CellWidth))) {
                                                if (m_Settings.NameToGuid.TryGetValue(eeName, out var guid)) {
                                                    var rect = GUILayoutUtility.GetRect(m_CellWidth, m_CellWidth);
                                                    m_Preview!.DrawCell(rect, $"add:{guid}", PreviewSpec.Add(guid));
                                                }
                                                GUILayout.Label($"{eeName}".Cyan(), Width(m_CellWidth));
                                                if (GUILayout.Button("Remove Exclusion", GUILayout.Width(m_CellWidth))) {
                                                    RemoveExclusion(eeName);
                                                }
                                            }
                                        });
                                    } else {
                                        foreach (var eeName in currentExcludes.ToList()) {
                                            using (HorizontalScope()) {
                                                if (GUILayout.Button("Remove Exclusion", AutoWidth())) {
                                                    RemoveExclusion(eeName);
                                                }
                                                GUILayout.Label($"    {eeName}");
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }

                    DrawDiv();
                    DisclosureToggle(ref m_OpenedInclude, "Show Include Section", AutoWidth());
                    if (m_OpenedInclude) {
                        using (HorizontalScope()) {
                            Space(25);
                            using (VerticalScope()) {
                                if (GUILayout.Button("Reset Includes", AutoWidth())) {
                                    EntityPartStorage.perSave.IncludeByName.Remove(PickedUnit!.UniqueId);
                                    IncludeBrowser.QueueUpdateItems([]);
                                    EntityPartStorage.SavePerSaveSettings();
                                }
                                using (HorizontalScope()) {
                                    GUILayout.Label("Gender: ", AutoWidth());
                                    var genderFilter = m_GenderFilter;
                                    if (SelectionGrid(ref genderFilter, 3, f => f switch {
                                        GenderFilter.Female => "Female (_F)",
                                        GenderFilter.Male => "Male (_M)",
                                        _ => "Any"
                                    }, AutoWidth())) {
                                        m_GenderFilter = genderFilter;
                                        IncludeBrowser.RedoSearch();
                                    }
                                    Space(30);
                                    GUILayout.Label("Race: ", AutoWidth());
                                    var raceFilter = m_RaceFilter;
                                    if (SelectionGrid(ref raceFilter, 4, f => f switch {
                                        RaceFilter.Human => "Human (_HM)",
                                        RaceFilter.SpaceMarine => "Space Marine (_SM)",
                                        RaceFilter.Eldar => "Eldar (_EL)",
                                        _ => "Any"
                                    }, AutoWidth())) {
                                        m_RaceFilter = raceFilter;
                                        IncludeBrowser.RedoSearch();
                                    }
                                    Space(30);
                                    GUILayout.Label("(EEs without a gender/race suffix always stay visible)".Green(), AutoWidth());
                                }
                                using (HorizontalScope()) {
                                    GUILayout.Label("Items per page: ", AutoWidth());
                                    int pageSize = m_Settings.IncludePageSize;
                                    if (IntTextField(ref pageSize, Width(60))) {
                                        m_Settings.IncludePageSize = Mathf.Clamp(pageSize, 1, 200);
                                        m_Settings.Save();
                                        IncludeBrowser.SetPageLimit(m_Settings.IncludePageSize);
                                    }
                                }
                                EntityPartStorage.perSave.IncludeByName.TryGetValue(PickedUnit!.UniqueId, out var currentIncludes);
                                m_IncludeBrowserLabelWidth ??= CalculateLargestLabelSize(["Remove", "Include"], GUI.skin.button);
                                void ToggleInclude(string guid, bool isIncluded) {
                                    currentIncludes ??= [];
                                    if (isIncluded) {
                                        currentIncludes.Remove(guid);
                                    } else {
                                        currentIncludes.Add(guid);
                                    }
                                    IncludeBrowser.QueueUpdateItems(currentIncludes);
                                    EntityPartStorage.perSave.IncludeByName[PickedUnit!.UniqueId] = currentIncludes;
                                    EntityPartStorage.SavePerSaveSettings();
                                }
                                if (m_Settings.UseLivePreviews) {
                                    int col = 0;
                                    var perRow = CellsPerRow;
                                    IncludeBrowser.OnGUI(guid => {
                                        if (col % perRow == 0) {
                                            GUILayout.BeginHorizontal();
                                        }
                                        col++;
                                        DrawEECell(guid);
                                        if (col % perRow == 0 || IncludeBrowser.CurrentlyIsLastElement) {
                                            if (IncludeBrowser.CurrentlyIsLastElement) {
                                                while (col % perRow != 0) {
                                                    col++;
                                                    GUILayout.Space(m_CellWidth);
                                                }
                                            }
                                            GUILayout.EndHorizontal();
                                            GUILayout.Space(4);
                                        }
                                        void DrawEECell(string guid) {
                                            using (new GUILayout.VerticalScope(GUILayout.Width(m_CellWidth))) {
                                                var rect = GUILayoutUtility.GetRect(m_CellWidth, m_CellWidth);
                                                m_Preview!.DrawCell(rect, $"add:{guid}", PreviewSpec.Add(guid));

                                                bool isIncluded = currentIncludes?.Contains(guid) ?? false;
                                                GUILayout.Label(isIncluded ? $"{m_Settings.AssetMapping![guid]}".Cyan() : $"{m_Settings.AssetMapping![guid]}".Green(), Width(m_CellWidth));
                                                if (GUILayout.Button(isIncluded ? "Remove" : "Include", GUILayout.Width(m_CellWidth))) {
                                                    ToggleInclude(guid, isIncluded);
                                                }
                                                GUILayout.TextArea($"{guid}", Width(m_CellWidth));
                                            }
                                        }
                                    });
                                } else {
                                    IncludeBrowser.OnGUI(guid => {
                                        using (HorizontalScope()) {
                                            bool isIncluded = currentIncludes?.Contains(guid) ?? false;
                                            var name = m_Settings.AssetMapping!.TryGetValue(guid, out var n) ? n : guid;
                                            GUILayout.Label(isIncluded ? name.Cyan() : name.Green(), Width(IncludeBrowser.TrackedWidth ?? 400));
                                            Space(10);
                                            if (GUILayout.Button(isIncluded ? "Remove" : "Include", Width(m_IncludeBrowserLabelWidth!.Value))) {
                                                ToggleInclude(guid, isIncluded);
                                            }
                                            Space(10);
                                            GUILayout.TextArea($"{guid}", Width(IncludeBrowser.TrackedWidth2 ?? 400));
                                        }
                                    });
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
        DisclosureToggle(ref m_OpenedColorSection, "Show Color Section", AutoWidth());
        if (m_OpenedColorSection) {
            using (HorizontalScope()) {
                GUILayout.Space(25);
                EntityPartStorage.perSave.RampOverrideByName.TryGetValue(PickedUnit!.UniqueId, out var rampOverrides);
                EntityPartStorage.perSave.CustomColorsByName.TryGetValue(PickedUnit.UniqueId, out var customTexOverrides);
                rampOverrides ??= new();
                customTexOverrides ??= new();
                using (VerticalScope()) {
                    var entries = PickedUnit.View.CharacterAvatar.EquipmentEntities.Union(PickedUnit.View.CharacterAvatar.SavedEquipmentEntities.Select(l => new EquipmentEntityLink() { AssetId = l.AssetId }.LoadAsset())).Where(ee => ee != null);
                    var width = CalculateLargestLabelSize(entries.Select(ee => (ee.name.IsNullOrEmpty() ? ee.ToString() : ee.name) + ":"));
                    foreach (var entry in entries) {
                        var ee = entry;
                        var eeName = ee.name.IsNullOrEmpty() ? ee.ToString() : ee.name;
                        using (HorizontalScope()) {
                            if (m_CurrentColorSectionEE == eeName) {
                                GUILayout.Label($"{eeName}:".Cyan(), Width(width));
                            } else {
                                GUILayout.Label($"{eeName}:", Width(width));
                            }
                            Space(10);
                            rampOverrides.TryGetValue(eeName, out var maybeRampOverride);
                            using (VerticalScope()) {
                                Helpers.GetClothColorsProfile(entry, out var colorPresets, false);
                                using (HorizontalScope()) {
                                    if (customTexOverrides.ContainsKey(eeName)) {
                                        if (GUILayout.Button("Remove Custom Color Override", AutoWidth())) {
                                            customTexOverrides.Remove(eeName);
                                            EntityPartStorage.perSave.CustomColorsByName[PickedUnit.UniqueId] = customTexOverrides;
                                            EntityPartStorage.SavePerSaveSettings();
                                        }
                                        Space(10);
                                    }
                                    if (maybeRampOverride != null) {
                                        if (GUILayout.Button("Remove Ramps Override", AutoWidth())) {
                                            rampOverrides.Remove(eeName);
                                            EntityPartStorage.perSave.RampOverrideByName[PickedUnit.UniqueId] = rampOverrides;
                                            EntityPartStorage.SavePerSaveSettings();
                                        }
                                        Space(10);
                                    }
                                    bool isPrimaryCustomActive = m_CurrentMode == CurrentColorWindow.PrimaryCustomTex && m_CurrentColorSectionEE == eeName;
                                    if (DisclosureToggle(ref isPrimaryCustomActive, "Show Custom Primary Color Picker")) {
                                        if (isPrimaryCustomActive) {
                                            m_CurrentMode = CurrentColorWindow.PrimaryCustomTex;
                                            m_CurrentColorSectionEE = eeName;
                                        } else {
                                            m_CurrentMode = CurrentColorWindow.None;
                                            m_CurrentColorSectionEE = "";
                                        }
                                    }
                                    Space(10);
                                    bool isSecondaryCustomActive = m_CurrentMode == CurrentColorWindow.SecondaryCustomTex && m_CurrentColorSectionEE == eeName;
                                    if (DisclosureToggle(ref isSecondaryCustomActive, "Show Custom Secondary Color Picker")) {
                                        if (isSecondaryCustomActive) {
                                            m_CurrentMode = CurrentColorWindow.SecondaryCustomTex;
                                            m_CurrentColorSectionEE = eeName;
                                        } else {
                                            m_CurrentMode = CurrentColorWindow.None;
                                            m_CurrentColorSectionEE = "";
                                        }
                                    }
                                    if (colorPresets != null) {
                                        Space(10);
                                        bool isRampsActive = m_CurrentMode == CurrentColorWindow.Ramps && m_CurrentColorSectionEE == eeName;
                                        if (DisclosureToggle(ref isRampsActive, "Show Ramps Picker")) {
                                            if (isRampsActive) {
                                                m_CurrentMode = CurrentColorWindow.Ramps;
                                                m_CurrentColorSectionEE = eeName;
                                            } else {
                                                m_CurrentMode = CurrentColorWindow.None;
                                                m_CurrentColorSectionEE = "";
                                            }
                                        }
                                    }
                                }
                                customTexOverrides.TryGetValue(eeName, out var texOverrides);
                                if (m_CurrentColorSectionEE == eeName) {
                                    if (m_CurrentMode == CurrentColorWindow.PrimaryCustomTex) {
                                        using (HorizontalScope()) {
                                            using (VerticalScope()) {
                                                if (texOverrides.Item1 != null) {
                                                    GUILayout.Label($"Current Primary Override: {texOverrides.Item1}");
                                                }
                                                if (PrimaryTexCreator?.EEName != eeName) {
                                                    PrimaryTexCreator = new(eeName, texOverrides.Item1);
                                                }
                                                if (PrimaryTexCreator.ColorPickerGUI()) {
                                                    texOverrides.Item1 = PrimaryTexCreator.GetTexCopy();
                                                    customTexOverrides[eeName] = texOverrides;
                                                    EntityPartStorage.perSave.CustomColorsByName[PickedUnit.UniqueId] = customTexOverrides;
                                                    EntityPartStorage.SavePerSaveSettings();
                                                    Helpers.SetColorPair(ee, new() { PrimaryIndex = -1, SecondaryIndex = -1 });
                                                }
                                            }
                                            DrawCurrentLookPreviewCell();
                                        }
                                    } else if (m_CurrentMode == CurrentColorWindow.SecondaryCustomTex) {
                                        using (HorizontalScope()) {
                                            using (VerticalScope()) {
                                                if (texOverrides.Item2 != null) {
                                                    GUILayout.Label($"Current Secondary Override: {texOverrides.Item2}");
                                                }
                                                if (SecondaryTexCreator?.EEName != eeName) {
                                                    SecondaryTexCreator = new(eeName, texOverrides.Item2);
                                                }
                                                if (SecondaryTexCreator.ColorPickerGUI()) {
                                                    texOverrides.Item2 = SecondaryTexCreator.GetTexCopy();
                                                    customTexOverrides[eeName] = texOverrides;
                                                    EntityPartStorage.perSave.CustomColorsByName[PickedUnit.UniqueId] = customTexOverrides;
                                                    EntityPartStorage.SavePerSaveSettings();
                                                    Helpers.SetColorPair(ee, new() { PrimaryIndex = -1, SecondaryIndex = -1 });
                                                }
                                            }
                                            DrawCurrentLookPreviewCell();
                                        }
                                    } else if (m_CurrentMode == CurrentColorWindow.Ramps) {
                                        void SelectPair(RampColorPreset.IndexSet pair) {
                                            rampOverrides[eeName] = pair;
                                            EntityPartStorage.perSave.RampOverrideByName[PickedUnit!.UniqueId] = rampOverrides;
                                            EntityPartStorage.SavePerSaveSettings();
                                            Helpers.SetColorPair(ee, pair);
                                        }
                                        if (m_Settings.UseLivePreviews) {
                                            DrawCellGrid(colorPresets!.IndexPairs, pair => {
                                                using (new GUILayout.VerticalScope(GUILayout.Width(m_CellWidth))) {
                                                    var rect = GUILayoutUtility.GetRect(m_CellWidth, m_CellWidth);
                                                    m_Preview!.DrawCell(rect, $"ramp:{eeName}:{pair.PrimaryIndex}:{pair.SecondaryIndex}", PreviewSpec.Ramps(eeName, pair.PrimaryIndex, pair.SecondaryIndex));
                                                    var pairLabel = $"{pair.Name ?? "Null Name"} - {pair.PrimaryIndex} - {pair.SecondaryIndex}";
                                                    if (maybeRampOverride != pair) {
                                                        GUILayout.Label(pairLabel, Width(m_CellWidth));
                                                        if (GUILayout.Button("Select", GUILayout.Width(m_CellWidth))) {
                                                            SelectPair(pair);
                                                        }
                                                    } else {
                                                        GUILayout.Label(pairLabel.Cyan(), Width(m_CellWidth));
                                                        GUILayout.Label("Selected".Cyan(), Width(m_CellWidth));
                                                    }
                                                }
                                            });
                                        } else {
                                            foreach (var pair in colorPresets!.IndexPairs) {
                                                using (HorizontalScope()) {
                                                    if (maybeRampOverride != pair) {
                                                        GUILayout.Label($"{pair.Name ?? "Null Name"} - {pair.PrimaryIndex} - {pair.SecondaryIndex}", AutoWidth());
                                                        if (GUILayout.Button("Select", AutoWidth())) {
                                                            SelectPair(pair);
                                                        }
                                                    } else {
                                                        GUILayout.Label($"{pair.Name ?? "Null Name"} - {pair.PrimaryIndex} - {pair.SecondaryIndex}".Cyan(), AutoWidth());
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

    private static void BodyPartGUI() {
        DisclosureToggle(ref m_OpenedBodyPartSection, "Show BodyPart Section", AutoWidth());

        if (m_OpenedBodyPartSection) {
            using (HorizontalScope()) {
                GUILayout.Space(25);

                EntityPartStorage.perSave.ExcludeBodyPartByName.TryGetValue(PickedUnit!.UniqueId, out var bodyPartExclusions);
                bodyPartExclusions ??= [];

                using (VerticalScope()) {
                    var entries = PickedUnit.View.CharacterAvatar.EquipmentEntities.Union(PickedUnit.View.CharacterAvatar.SavedEquipmentEntities.Select(l => new EquipmentEntityLink() { AssetId = l.AssetId }.LoadAsset())).Where(ee => ee != null);
                    var width = CalculateLargestLabelSize(entries.Select(ee => (ee.name.IsNullOrEmpty() ? ee.ToString() : ee.name) + ":"));

                    foreach (var entry in entries) {
                        var ee = entry;
                        var eeName = ee.name.IsNullOrEmpty() ? ee.ToString() : ee.name;

                        bodyPartExclusions.TryGetValue(eeName, out var excludedParts);
                        excludedParts ??= [];

                        using (HorizontalScope()) {
                            GUILayout.Label($"{eeName}:", Width(width));
                            Space(10);

                            using (VerticalScope()) {
                                if (ee.BodyParts == null || ee.BodyParts.Count == 0) {
                                    using (HorizontalScope()) {
                                        Space(5);
                                        GUILayout.Label("No BodyParts");
                                    }
                                } else {
                                    foreach (var part in ee.BodyParts) {
                                        var partName = part.GetBodyPartMapping()!;
                                        if (string.IsNullOrEmpty(partName)) {
                                            continue;
                                        }

                                        bool isExcluded = excludedParts.Contains(partName);
                                        var s = isExcluded ? $"Exclude {partName}".Cyan() : $"Include {partName}".Green();
                                        bool newIsExcluded = !GUILayout.Toggle(!isExcluded, s + $" ({part.Type.ToString()})".Orange());

                                        if (newIsExcluded != isExcluded) {
                                            if (newIsExcluded) {
                                                excludedParts.Add(partName);
                                            } else {
                                                excludedParts.Remove(partName);
                                            }

                                            bodyPartExclusions[eeName] = excludedParts;
                                            EntityPartStorage.perSave.ExcludeBodyPartByName[PickedUnit.UniqueId] = bodyPartExclusions;
                                            EntityPartStorage.SavePerSaveSettings();

                                        }
                                    }
                                }
                            }
                        }
                        Space(5);
                    }
                }
            }
        }

        DrawDiv();
    }

    private static void ParseEESuffix(string eeName, out string? gender, out string? race) {
        gender = null;
        race = null;
        var parts = eeName.Split('_');
        for (int i = parts.Length - 1; i >= 0 && i >= parts.Length - 2; i--) {
            switch (parts[i]) {
                case "HM":
                case "SM":
                case "EL":
                    race ??= parts[i];
                    break;
                case "M":
                case "F":
                    gender ??= parts[i];
                    break;
            }
        }
    }
    private static bool PassesEEFilters(string guid) {
        var genderFilter = m_GenderFilter;
        var raceFilter = m_RaceFilter;
        if (genderFilter == GenderFilter.Any && raceFilter == RaceFilter.Any) {
            return true;
        }
        if (!(m_Settings.AssetMapping?.TryGetValue(guid, out var name) ?? false)) {
            return true;
        }
        ParseEESuffix(name, out var gender, out var race);
        if (genderFilter != GenderFilter.Any && gender != null
            && gender != (genderFilter == GenderFilter.Male ? "M" : "F")) {
            return false;
        }
        if (raceFilter != RaceFilter.Any && race != null
            && race != raceFilter switch { RaceFilter.Human => "HM", RaceFilter.SpaceMarine => "SM", _ => "EL" }) {
            return false;
        }
        return true;
    }
    private static void DrawCellGrid<T>(IReadOnlyList<T> items, Action<T> drawCell, int perRow = 0) {
        if (perRow <= 0) {
            perRow = CellsPerRow;
        }
        for (int i = 0; i < items.Count; i++) {
            if (i % perRow == 0) {
                GUILayout.BeginHorizontal();
            }
            drawCell(items[i]);
            if (i % perRow == perRow - 1 || i == items.Count - 1) {
                GUILayout.EndHorizontal();
                GUILayout.Space(4);
            }
        }
    }
    private static void DrawCurrentLookPreviewCell() {
        if (!m_Settings.UseLivePreviews || m_Preview == null) {
            return;
        }
        Space(20);
        using (new GUILayout.VerticalScope(GUILayout.Width(m_CellWidth))) {
            var rect = GUILayoutUtility.GetRect(m_CellWidth, m_CellWidth);
            m_Preview.DrawCell(rect, "asis", PreviewSpec.AsIs);
            GUILayout.Label("Current Look".Green(), Width(m_CellWidth));
        }
    }
    internal static void InvalidatePreviews() {
        m_Preview?.InvalidateAll();
    }
    internal static void DisposePreview() {
        m_Preview?.Dispose();
        m_Preview = null;
    }
    private static void OnHideGUI(UnityModManager.ModEntry modEntry) {
        DisposePreview();
    }
    private static void OnUpdate(UnityModManager.ModEntry modEntry, float z) {
        try {
            while (m_MainThreadTaskQueue.TryDequeue(out var task)) {
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
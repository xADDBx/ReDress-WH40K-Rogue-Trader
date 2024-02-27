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
using Kingmaker.GameInfo;
using UnityEngine.UIElements;
using Kingmaker.Blueprints.Area;
using Kingmaker.EntitySystem.Persistence;

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
    internal static BaseUnitEntity choosen = null;
    internal static Outfit selectedOutfit = Outfit.Current;
    internal static BaseUnitEntity cachedUnit = null;
    internal static bool openedGuide = false;
    internal static bool shouldOpenGuide = false;
    internal static bool openedExclude = false;
    internal static bool shouldOpenExclude = false;
    internal static bool openedInclude = false;
    internal static bool shouldOpenInclude = false;
    internal static Browser<(string, string), (string, string)> includeBrowser = new(true);
    static void OnHideGUI(UnityModManager.ModEntry modEntry) {
        choosen = null;
        selectedOutfit = Outfit.Current;
    }
    static void OnGUI(UnityModManager.ModEntry modEntry) {
        if (Event.current.type == EventType.Layout && (error != null || shouldResetError)) {
            if (!shouldResetError) {
                printError = true;
            }
            else {
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
        }
        else {
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

                    int selectedIndex = choosen != null ? Array.IndexOf(units.ToArray(), choosen) : 0;
                    if (selectedIndex < 0) {
                        selectedIndex = 0;
                        choosen = null;
                    }
                    int newIndex = GUILayout.SelectionGrid(selectedIndex, units.Select(m => m.CharacterName).ToArray(), 6);
                    if (selectedIndex != newIndex || choosen == null) {
                        choosen = units[newIndex];
                        selectedOutfit = Outfit.Current;
                    }
                    DrawDiv();
                    if (GUILayout.Button("Change Appeareance (opens character creation dialog).", GUILayout.ExpandWidth(false))) {
                        cachedUnit = choosen;
                        UnityModManager.UI.Instance.ToggleWindow();
                        isInRoom = true;

                        /*
                        SoundState.Instance.OnMusicStateChange(MusicStateHandler.MusicState.Chargen);
                        CharGenConfig.Create(choosen, CharGenConfig.CharGenMode.NewCompanion, CharGenConfig.CharGenCompanionType.Common, true).SetOnComplete(
                        */
                        Game.Instance.Player.CreateCustomCompanion(newCompanion => {
                            try {
                                isInRoom = false;
                                cachedUnit.ViewSettings.SetDoll(newCompanion.ViewSettings.Doll);
                            }
                            catch (Exception ex) {
                                log.Log(ex.ToString());
                                error = ex;
                            }
                        }, null, CharGenConfig.CharGenCompanionType.Common);/*.SetOnClose(() => { }).SetOnCloseSoundAction(() => SoundState.Instance.OnMusicStateChange(MusicStateHandler.MusicState.Setting)).OpenUI();
                        */
                    }
                    DrawDiv();

                    GUILayout.Label("Set Outfit to the following:", GUILayout.ExpandWidth(false));
                    Outfit[] outfits = (Outfit[])Enum.GetValues(typeof(Outfit));
                    var selectedIndex2 = Array.IndexOf(outfits, selectedOutfit);
                    newIndex = GUILayout.SelectionGrid(selectedIndex2, outfits.Select(m => m.ToDescriptionString()).ToArray(), 5);
                    if (selectedIndex2 != newIndex) {
                        selectedOutfit = outfits[newIndex];
                        if (selectedOutfit == Outfit.Naked) {
                            EntityPartStorage.perSave.AddClothes.Remove(choosen.UniqueId);
                            EntityPartStorage.perSave.NakedFlag[choosen.UniqueId] = true;
                        }
                        else {
                            var kee = ResourcesLibrary.BlueprintsCache.Load(JobClothesIDs[selectedOutfit]) as KingmakerEquipmentEntity;
                            EntityPartStorage.perSave.AddClothes[choosen.UniqueId] = choosen.Gender == Kingmaker.Blueprints.Base.Gender.Male ? kee.m_MaleArray.Select(f => f.AssetId).ToList() : kee.m_FemaleArray.Select(f => f.AssetId).ToList();
                            EntityPartStorage.perSave.NakedFlag.Remove(choosen.UniqueId);
                        }
                        EntityPartStorage.SavePerSaveSettings();
                    }

                    DrawDiv();
                    if (GUILayout.Button("Reset Outfit", GUILayout.ExpandWidth(false))) {
                        EntityPartStorage.perSave.AddClothes.Remove(choosen.UniqueId);
                        EntityPartStorage.perSave.NakedFlag.Remove(choosen.UniqueId);
                        EntityPartStorage.SavePerSaveSettings();
                    }

                    DrawDiv();
                    shouldOpenExclude = GUILayout.Toggle(shouldOpenExclude, "Show Exclude Section", GUILayout.ExpandWidth(false));
                    if (Event.current.type == EventType.Layout) {
                        openedExclude = shouldOpenExclude;
                    }

                    if (openedExclude) {
                        if (GUILayout.Button("Reset Excludes", GUILayout.ExpandWidth(false))) {
                            EntityPartStorage.perSave.ExcludeByName.Remove(choosen.UniqueId);
                            EntityPartStorage.SavePerSaveSettings();
                        }
                        foreach (var ee in choosen.View.CharacterAvatar.EquipmentEntities.Union(choosen.View.CharacterAvatar.SavedEquipmentEntities.Select(l => new EquipmentEntityLink() { AssetId = l.AssetId }.LoadAsset()))) {
                            using (new GUILayout.HorizontalScope()) {
                                if (GUILayout.Button("Exclude", GUILayout.ExpandWidth(false))) {
                                    EntityPartStorage.perSave.ExcludeByName.TryGetValue(choosen.UniqueId, out var tmpExcludes);
                                    if (tmpExcludes == null) tmpExcludes = new();
                                    tmpExcludes.Add(ee.name);
                                    EntityPartStorage.perSave.ExcludeByName[choosen.UniqueId] = tmpExcludes;
                                    EntityPartStorage.SavePerSaveSettings();
                                }
                                GUILayout.Label($"    {ee?.name ?? "Null????????????"}");
                            }
                        }
                        GUILayout.Label("------------------------------------------");
                        GUILayout.Label("Current Excludes:");
                        EntityPartStorage.perSave.ExcludeByName.TryGetValue(choosen.UniqueId, out var currentExcludes);
                        if (currentExcludes?.Count > 0) {
                            foreach (var eeName in currentExcludes.ToList()) {
                                using (new GUILayout.HorizontalScope()) {
                                    if (GUILayout.Button("Remove Exclusion", GUILayout.ExpandWidth(false))) {
                                        EntityPartStorage.perSave.ExcludeByName.TryGetValue(choosen.UniqueId, out var tmpExcludes);
                                        if (tmpExcludes == null) tmpExcludes = new();
                                        tmpExcludes.Remove(eeName);
                                        EntityPartStorage.perSave.ExcludeByName[choosen.UniqueId] = tmpExcludes;
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
                            EntityPartStorage.perSave.IncludeByName.Remove(choosen.UniqueId);
                            EntityPartStorage.SavePerSaveSettings();
                        }
                        EntityPartStorage.perSave.IncludeByName.TryGetValue(choosen.UniqueId, out var currentIncludes);
                        includeBrowser.OnGUI(settings.AssetIds, s => s, s => $"{s.Item2} {s.Item1}", s => new[] { s.Item2, s.Item1 }, (pair1, pair2) => {
                            using (new GUILayout.HorizontalScope()) {
                                if (currentIncludes?.Contains(pair1.Item1) ?? false) {
                                    GUILayout.Label(" ");
                                }
                                else {
                                    if (GUILayout.Button("Include", GUILayout.ExpandWidth(false))) {
                                        EntityPartStorage.perSave.IncludeByName.TryGetValue(choosen.UniqueId, out var tmpIncludes);
                                        if (tmpIncludes == null) tmpIncludes = new();
                                        tmpIncludes.Add(pair1.Item1);
                                        EntityPartStorage.perSave.IncludeByName[choosen.UniqueId] = tmpIncludes;
                                        EntityPartStorage.SavePerSaveSettings();
                                    }
                                }
                                GUILayout.Label($"    {pair1.Item2}");
                            }
                        });
                        GUILayout.Label("------------------------------------------");
                        GUILayout.Label("Current Includes:");
                        EntityPartStorage.perSave.IncludeByName.TryGetValue(choosen.UniqueId, out currentIncludes);
                        if (currentIncludes?.Count > 0) {
                            foreach (var eeName in currentIncludes.ToList()) {
                                using (new GUILayout.HorizontalScope()) {
                                    if (GUILayout.Button("Remove Inclusion", GUILayout.ExpandWidth(false))) {
                                        EntityPartStorage.perSave.IncludeByName.TryGetValue(choosen.UniqueId, out var tmpIncludes);
                                        if (tmpIncludes == null) tmpIncludes = new();
                                        tmpIncludes.Remove(eeName);
                                        EntityPartStorage.perSave.IncludeByName[choosen.UniqueId] = tmpIncludes;
                                        EntityPartStorage.SavePerSaveSettings();
                                    }
                                    GUILayout.TextArea($"    {eeName}", GUILayout.ExpandWidth(false));
                                }
                            }
                        }
                    }

                }
                else {
                    GUILayout.Label("Load a save first!", GUILayout.ExpandWidth(false));
                }
            }
            catch (Exception ex) {
                log.Log(ex.ToString());
                error = ex;
            }
        }
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
    [HarmonyPatch(typeof(Character), nameof(Character.AddEquipmentEntity), new Type[] { typeof(EquipmentEntity), typeof(bool) })]
    internal static class Character_AddEquipmentEntity_Patch {
        [HarmonyPrefix]
        private static bool AddEquipmentEntity(Character __instance, EquipmentEntity ee) {
            try {
                var uniqueId = __instance.GetComponent<UnitEntityView>()?.UniqueId ?? null;
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
            }
            catch (Exception e) {
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
        [HarmonyPatch(nameof(Game.LoadArea), new Type[] { typeof(BlueprintArea), typeof(BlueprintAreaEnterPoint), typeof(AutoSaveMode), typeof(SaveInfo), typeof(Action) })]
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
            GUILayout.Space(25);
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
        }
        else {
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
            GUILayout.Space(25);
        }
    }

#if DEBUG
    static bool OnUnload(UnityModManager.ModEntry modEntry) {
        HarmonyInstance.UnpatchAll(modEntry.Info.Id);
        return true;
    }
#endif
}
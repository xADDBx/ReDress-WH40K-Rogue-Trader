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

namespace ReDress;

#if DEBUG
[EnableReloading]
#endif
static class Main {
    internal static Harmony HarmonyInstance;
    internal static UnityModManager.ModEntry.ModLogger log;
    internal static bool isInRoom = false;
    static bool Load(UnityModManager.ModEntry modEntry) {
        log = modEntry.Logger;
#if DEBUG
        modEntry.OnUnload = OnUnload;
#endif
        modEntry.OnGUI = OnGUI;
        modEntry.OnHideGUI = OnHideGUI;
        HarmonyInstance = new Harmony(modEntry.Info.Id);
        HarmonyInstance.PatchAll(Assembly.GetExecutingAssembly());
        return true;
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
    static void OnHideGUI(UnityModManager.ModEntry modEntry) {
        choosen = null;
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
                    GUILayout.Label("");
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
                        }
                    }
                    GUILayout.Label("");
                }
                var units = new List<BaseUnitEntity>() { Game.Instance?.Player?.MainCharacterEntity };
                units.AddRange(Game.Instance.Player.ActiveCompanions ?? new());
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
                    GUILayout.Label("");
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
                    GUILayout.Label("");

                    GUILayout.Label("Set Outfit to the following:", GUILayout.ExpandWidth(false));
                    Outfit[] outfits = { Outfit.Current, Outfit.Criminal, Outfit.Nobility, Outfit.Commissar, Outfit.Navy, Outfit.Militarum, Outfit.Psyker, Outfit.Crusader, Outfit.Navigator };
                    var selectedIndex2 = Array.IndexOf(outfits, selectedOutfit);
                    newIndex = GUILayout.SelectionGrid(selectedIndex2, outfits.Select(m => m.ToDescriptionString()).ToArray(), 9);
                    if (selectedIndex2 != newIndex) {
                        selectedOutfit = outfits[newIndex];
                        var kee = ResourcesLibrary.BlueprintsCache.Load(JobClothesIDs[selectedOutfit]) as KingmakerEquipmentEntity;
                        EntityPartStorage.perSave.AddClothes[choosen.UniqueId] = choosen.Gender == Kingmaker.Blueprints.Base.Gender.Male ? kee.m_MaleArray.Select(f => f.AssetId).ToList() : kee.m_FemaleArray.Select(f => f.AssetId).ToList();
                        EntityPartStorage.SavePerSaveSettings();
                    }

                    if (GUILayout.Button("Reset Outfit", GUILayout.ExpandWidth(false))) {
                        EntityPartStorage.perSave.AddClothes.Remove(choosen.UniqueId);
                        EntityPartStorage.SavePerSaveSettings();
                    }

                    GUILayout.Label("");
                    shouldOpenExclude = GUILayout.Toggle(shouldOpenExclude, "Show Exclude Section", GUILayout.ExpandWidth(false));
                    if (Event.current.type == EventType.Layout) {
                        openedExclude = shouldOpenExclude;
                    }

                    if (openedExclude) {
                        if (GUILayout.Button("Reset Excludes", GUILayout.ExpandWidth(false))) {
                            EntityPartStorage.perSave.ExcludeByName.Remove(choosen.UniqueId);
                        }
                        foreach (var ee in choosen.View.CharacterAvatar.EquipmentEntities) {
                            using (new GUILayout.HorizontalScope()) {
                                if (GUILayout.Button("Exclude", GUILayout.ExpandWidth(false))) {
                                    EntityPartStorage.perSave.ExcludeByName.TryGetValue(choosen.UniqueId, out var tmpExcludes);
                                    if (tmpExcludes == null) tmpExcludes = new();
                                    tmpExcludes.Add(ee.name);
                                    EntityPartStorage.perSave.ExcludeByName[choosen.UniqueId] = tmpExcludes;
                                    EntityPartStorage.SavePerSaveSettings();
                                }
                                GUILayout.Label($"    {ee.name}");
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

                } else {
                    GUILayout.Label("Load a save first!", GUILayout.ExpandWidth(false));
                }
            } catch (Exception ex) {
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
        Navigator
    }
    internal static readonly Dictionary<Outfit, string> JobClothesIDs = new() { { Outfit.Criminal, "2415ba44fb9e4bd5b22f4f574b5f0cd8" }, { Outfit.Nobility, "86c28cf33b7e42ecb190cd6c1b2aa4cc" },
        { Outfit.Commissar, "28b88e24ffa341a7b1dfc286583f226d" }, {Outfit.Navy, "e28fc5d840134da892c24d24738ceb63" }, { Outfit.Militarum, "394e4b94f1284fefa4f47f1df0e42161" }, 
        { Outfit.Psyker,"76c6f2ce3e5d4c8d9cc0c22145d4d630" }, { Outfit.Crusader, "b63a4c7a04fd47dcb8c6dfc24f92f33d" }, { Outfit.Navigator, "3afcd2d9ccb24e85844857ba852c1d88" } };
    internal static HashSet<EquipmentEntity> cachedLinks = new();
    [HarmonyPatch(typeof(PartUnitViewSettings), nameof(PartUnitViewSettings.Instantiate))]
    internal static class PartUnitViewSettings_Instantiate_Patch {
        [HarmonyPostfix]
        private static void Instantiate(PartUnitViewSettings __instance, ref UnitEntityView __result) {
            var context = __instance.Owner;
            log.Log($"Check for unit: {context.Name}-{context.UniqueId}");
            if (EntityPartStorage.perSave.AddClothes.TryGetValue(context.UniqueId, out var ids)) {
                log.Log("Found Overrides. Changing UnitEntityView.");
                var charac = __result.GetComponent<Character>();
                if (charac != null) {
                    foreach (var job in JobClothesIDs.Values) {
                        var kee = ResourcesLibrary.BlueprintsCache.Load(job) as KingmakerEquipmentEntity;
                        foreach (var entry in kee?.m_MaleArray) {
                            charac.RemoveEquipmentEntity(entry);
                            cachedLinks.Add(entry.Load());
                        }
                        foreach (var entry in kee?.m_FemaleArray) {
                            charac.RemoveEquipmentEntity(entry);
                            cachedLinks.Add(entry.Load());
                        }
                        if (EntityPartStorage.perSave.ExcludeByName.TryGetValue(context.UniqueId, out var excludes)) {
                            log.Log("---------------------------");
                            foreach (var ee in charac.EquipmentEntities.ToArray()) {
                                if (excludes.Contains(ee.name)) {
                                    charac.EquipmentEntities.Remove(ee);
                                }
                            }
                            foreach (var eel in charac.SavedEquipmentEntities.ToArray()) {
                                eel.Load();
                                if (excludes.Contains(eel.m_Handle?.Object?.name)) {
                                    charac.SavedEquipmentEntities.Remove(eel);
                                }
                            }
                        }
                    }
                    foreach (var id in ids) {
                        log.Log($"Adding {id}");
                        var eel = new EquipmentEntityLink() { AssetId = id };
                        charac.EquipmentEntitiesForPreload.Add(eel);
                        var ee = eel.Load();
                        cachedLinks.Add(ee);
                        charac.AddEquipmentEntity(ee);
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
                if (EntityPartStorage.perSave.AddClothes.TryGetValue(__instance.GetComponent<UnitEntityView>().UniqueId, out var ids)) {
                    if (cachedLinks.Contains(ee)) return false;
                    if (EntityPartStorage.perSave.ExcludeByName.TryGetValue(__instance.GetComponent<UnitEntityView>().UniqueId, out var excludes)) {
                        if (excludes.Contains(ee.name)) return false;
                    }
                }
            } catch (Exception e) {
                log.Log(e.ToString());
            }
            return true;
        }
    }
    public static string ToDescriptionString(this Outfit val) {
        DescriptionAttribute[] attributes = (DescriptionAttribute[])val
           .GetType()
           .GetField(val.ToString())
           .GetCustomAttributes(typeof(DescriptionAttribute), false);
        return attributes.Length > 0 ? attributes[0].Description : string.Empty;
    }

#if DEBUG
    static bool OnUnload(UnityModManager.ModEntry modEntry)
    {
        HarmonyInstance.UnpatchAll(modEntry.Info.Id);
        return true;
    }
#endif
}
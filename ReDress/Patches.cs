using HarmonyLib;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Area;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.EntitySystem.Persistence;
using Kingmaker.ResourceLinks;
using Kingmaker.UI.Common;
using Kingmaker.UI.DollRoom;
using Kingmaker.UnitLogic.Parts;
using Kingmaker.View;
using Kingmaker.Visual.CharacterSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static ReDress.Main;

namespace ReDress;
public class Patches {
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
                        foreach (var job in Helpers.JobClothesIDs.Values) {
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
        internal static (EntityPartStorage.CustomColorTex, EntityPartStorage.CustomColorTex, EntityPartStorage.CustomColorTex) customOverride = (null, null, null);
        [HarmonyPrefix]
        private static void RepaintRextures(EquipmentEntity __instance, EquipmentEntity.PaintedTextures paintedTextures, ref int primaryRampIndex, ref int secondaryRampIndex) {
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
                    if (doExcludeNewEEs) {
                        EntityPartStorage.perSave.ExcludeByName.TryGetValue(uniqueId, out var tmpExcludes);
                        if (tmpExcludes == null) tmpExcludes = new();

                        cachedLinks.TryGetValue(uniqueId, out var defaultClothes);
                        if (!(defaultClothes?.Contains(ee) ?? false)) {
                            tmpExcludes.Add(ee.name);
                            EntityPartStorage.perSave.ExcludeByName[uniqueId] = tmpExcludes;
                            EntityPartStorage.SavePerSaveSettings(false);
                        }
                    }
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


    [HarmonyPatch(typeof(CharacterDollRoom), nameof(CharacterDollRoom.Show))]
    public static class setEEFlagOn {
        [HarmonyPrefix]
        public static void Show() {
            doExcludeNewEEs = settings.ShouldExcludeNewEEs;
        }
    }

    [HarmonyPatch(typeof(CharacterDollRoom), nameof(CharacterDollRoom.Hide))]
    public static class setEFlagOff {
        [HarmonyPrefix]
        private static void Hide() {
            doExcludeNewEEs = false;
        }
    }
}

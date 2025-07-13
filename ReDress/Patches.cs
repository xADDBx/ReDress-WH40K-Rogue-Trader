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
using UnityEngine;
using static ReDress.Main;

namespace ReDress;
[HarmonyPatch]
public static class Patches {
    private static bool m_IsCurrentlyLoadingGame = false;
    private static bool m_ForceNoExcludeNewEEs = false;
    private static string? m_CurrentUid;
    private static (EntityPartStorage.CustomColorTex?, EntityPartStorage.CustomColorTex?, EntityPartStorage.CustomColorTex?) m_CustomOverride = (null, null, null);
    private static Dictionary<string, HashSet<EquipmentEntity>> m_CachedLinks = new();

    [HarmonyPatch(typeof(PartUnitViewSettings), nameof(PartUnitViewSettings.Instantiate)), HarmonyPostfix]
    private static void PartUnitViewSettings_Instantiate(PartUnitViewSettings __instance, ref UnitEntityView __result) {
        m_CurrentUid = __instance.Owner.UniqueId;
        Log.Log($"Check for unit: {__instance.Owner.Name}-{m_CurrentUid}");
        EntityPartStorage.perSave.AddClothes.TryGetValue(m_CurrentUid, out var outfitIds);
        EntityPartStorage.perSave.IncludeByName.TryGetValue(m_CurrentUid, out var includeIds);
        EntityPartStorage.perSave.ExcludeByName.TryGetValue(m_CurrentUid, out var excludeIds);
        EntityPartStorage.perSave.NakedFlag.TryGetValue(m_CurrentUid, out var nakedFlag);
        if (outfitIds?.Count > 0 || includeIds?.Count > 0 || excludeIds?.Count > 0) {
            Log.Log("Found Overrides. Changing UnitEntityView.");
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
                        string? key = eel.m_Handle?.Object?.name;
                        if (key == null) {
                            Log.Log($"Error trying to check Excludes for unit {__instance.Owner} for eel: {eel.m_Handle?.AssetId ?? "null"}");
                        } else {
                            if (excludeIds.Contains(key)) {
                                charac.SavedEquipmentEntities.Remove(eel);
                            }
                        }
                    }
                }
                if (!m_CachedLinks.ContainsKey(m_CurrentUid)) {
                    m_CachedLinks[m_CurrentUid] = new();
                }
                if (outfitIds?.Count > 0 || nakedFlag) {
                    foreach (var job in Helpers.JobClothesIDs.Values) {
                        var kee = ResourcesLibrary.BlueprintsCache.Load(job) as KingmakerEquipmentEntity;
                        if (kee == null) {
                            Log.Log($"Error, null KEE for job clothes {job}");
                        } else {
                            foreach (var entry in kee.m_MaleArray) {
                                charac.RemoveEquipmentEntity(entry);
                                m_CachedLinks[m_CurrentUid].Add(entry.Load());
                            }
                            foreach (var entry in kee.m_FemaleArray) {
                                charac.RemoveEquipmentEntity(entry);
                                m_CachedLinks[m_CurrentUid].Add(entry.Load());
                            }
                        }
                    }
                }
                if (outfitIds?.Count > 0) {
                    foreach (var id in outfitIds) {
                        var eel = new EquipmentEntityLink() { AssetId = id };
                        charac.EquipmentEntitiesForPreload.Add(eel);
                        var ee = eel.Load();
                        m_CachedLinks[m_CurrentUid].Remove(ee);
                        charac.AddEquipmentEntity(ee);
                    }
                }
                if (includeIds?.Count > 0) {
                    foreach (var id in includeIds) {
                        var eel = new EquipmentEntityLink() { AssetId = id };
                        charac.EquipmentEntitiesForPreload.Add(eel);
                        var ee = eel.Load();
                        m_CachedLinks[m_CurrentUid].Remove(ee);
                        charac.AddEquipmentEntity(ee);
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(EquipmentEntity), nameof(EquipmentEntity.RepaintTextures), [typeof(EquipmentEntity.PaintedTextures), typeof(int), typeof(int)]), HarmonyPrefix]
    private static void EquipmentEntity_RepaintTextures(EquipmentEntity __instance, EquipmentEntity.PaintedTextures paintedTextures, ref int primaryRampIndex, ref int secondaryRampIndex) {
        m_CustomOverride = (null, null, null);
        if (m_CurrentUid == null) {
            Log.Log(new System.Diagnostics.StackTrace().ToString());
            return;
        }
        if (EntityPartStorage.perSave.RampOverrideByName.TryGetValue(m_CurrentUid, out var overrides)) {
            var eeName = __instance.name ?? __instance.ToString();
            if (eeName == null) {
                Log.Log(new System.Diagnostics.StackTrace().ToString());
                return;
            }
            if (overrides.TryGetValue(eeName, out var pair)) {
                primaryRampIndex = pair.PrimaryIndex;
                secondaryRampIndex = pair.SecondaryIndex;
            }
        }
        if (EntityPartStorage.perSave.CustomColorsByName.TryGetValue(m_CurrentUid, out var overrides2)) {
            var eeName = __instance.name ?? __instance.ToString();
            if (eeName == null) {
                Log.Log(new System.Diagnostics.StackTrace().ToString());
                return;
            }
            if (overrides2.TryGetValue(eeName, out var customColor)) {
                m_CustomOverride = (customColor.Item1, customColor.Item2, customColor.Item3);
            }
        }
    }

    [HarmonyPatch(typeof(CharacterTextureDescription), nameof(CharacterTextureDescription.Repaint))]
    private static class CharacterTextureDescription_Repaint_Patch {
        private static Texture2D? m_PrimTex = null;
        private static Texture2D? m_SecTex = null;
        private static Texture2D? m_MainTex = null;
        [HarmonyPrefix]
        private static void RepaintPre(CharacterTextureDescription __instance, RenderTexture rtToPaint, ref Texture2D primaryRamp, ref Texture2D secondaryRamp) {
            var ov = m_CustomOverride;
            m_PrimTex = primaryRamp;
            m_SecTex = secondaryRamp;
            m_MainTex = __instance.ActiveTexture;
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
            primaryRamp = m_PrimTex!;
            secondaryRamp = m_SecTex!;
            __instance.ActiveTexture = m_MainTex;
            m_PrimTex = null;
            m_SecTex = null;
            m_MainTex = null;
        }
    }

    [HarmonyPatch(typeof(CharacterDollRoom), nameof(CharacterDollRoom.SetupUnit)), HarmonyPrefix]
    private static void CharacterDollRoom_SetupUnit(BaseUnitEntity player) {
        var tmp = player?.UniqueId ?? null;
        if (tmp != null) {
            m_CurrentUid = tmp;
        }
    }

    [HarmonyPatch(typeof(Character))]
    private static class Character_Patch {
        [HarmonyPatch(nameof(Character.OnRenderObject)), HarmonyPrefix]
        private static void OnRenderObject(Character __instance) {
            var tmp = __instance.GetComponent<UnitEntityView>()?.Data?.UniqueId ?? null;
            if (tmp != null) {
                m_CurrentUid = tmp;
            }
            if (__instance.IsInDollRoom) {
                tmp = UIDollRooms.Instance.CharacterDollRoom?.m_OriginalAvatar?.GetComponent<UnitEntityView>()?.Data?.UniqueId ?? null;
                if (tmp != null) m_CurrentUid = tmp;
            }
        }

        [HarmonyPatch(nameof(Character.MergeOverlays)), HarmonyPrefix]
        private static void MergeOverlays(Character __instance) {
            var tmp = __instance.GetComponent<UnitEntityView>()?.Data?.UniqueId ?? null;
            if (tmp != null) {
                m_CurrentUid = tmp;
            }
        }

        [HarmonyPatch(nameof(Character.DoUpdate)), HarmonyPrefix]
        private static void DoUpdate(Character __instance) {
            var tmp = __instance.GetComponent<UnitEntityView>()?.Data?.UniqueId ?? null;
            if (tmp != null) {
                m_CurrentUid = tmp;
            }
        }

        [HarmonyPatch(nameof(Character.AddEquipmentEntity), [typeof(EquipmentEntity), typeof(bool)]), HarmonyPrefix]
        private static bool AddEquipmentEntity(Character __instance, EquipmentEntity ee) {
            try {
                var uniqueId = __instance.GetComponent<UnitEntityView>()?.Data?.UniqueId ?? null;
                if (uniqueId != null) {
                    if (m_ForceNoExcludeNewEEs ? false : m_Settings.ShouldExcludeNewEEs) {
                        EntityPartStorage.perSave.ExcludeByName.TryGetValue(uniqueId, out var tmpExcludes);
                        if (tmpExcludes == null) tmpExcludes = new();

                        m_CachedLinks.TryGetValue(uniqueId, out var defaultClothes);
                        if (!(defaultClothes?.Contains(ee) ?? false)) {
                            tmpExcludes.Add(ee.name);
                            EntityPartStorage.perSave.ExcludeByName[uniqueId] = tmpExcludes;
                            EntityPartStorage.SavePerSaveSettings(false);
                        }
                    }
                    EntityPartStorage.perSave.AddClothes.TryGetValue(uniqueId, out var ids);
                    EntityPartStorage.perSave.NakedFlag.TryGetValue(uniqueId, out var nakedFlag);
                    if (ids?.Count > 0 || nakedFlag) {
                        m_CachedLinks.TryGetValue(uniqueId, out var defaultClothes);
                        if (defaultClothes?.Contains(ee) ?? false) return false;
                    }
                    EntityPartStorage.perSave.ExcludeByName.TryGetValue(uniqueId, out var excludeIds);
                    if (excludeIds?.Count > 0) {
                        if (excludeIds.Contains(ee.name)) return false;
                    }
                }
            } catch (Exception e) {
                Log.Log(e.ToString());
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(Game))]
    private static class Game_Patch {
        [HarmonyPatch(nameof(Game.LoadGameForce)), HarmonyPrefix]
        private static void LoadGameForce() {
            m_IsCurrentlyLoadingGame = true;
        }

        [HarmonyPatch(nameof(Game.LoadArea), [typeof(BlueprintArea), typeof(BlueprintAreaEnterPoint), typeof(AutoSaveMode), typeof(SaveInfo), typeof(Action)]), HarmonyPrefix]
        private static void LoadArea() {
            EntityPartStorage.ClearCachedPerSave();
            if (m_IsCurrentlyLoadingGame) {
                m_CachedLinks.Clear();
                m_IsCurrentlyLoadingGame = false;
            }
        }
    }

    [HarmonyPatch(typeof(Traverse), nameof(Traverse.SetValue)), HarmonyFinalizer]
    private static Exception Traverse_SetValue(Exception __exception, Traverse __instance, ref Traverse __result) {
        if (__exception is FieldAccessException) {
            __result = __instance;
            return null!;
        }
        return __exception;
    }

    [HarmonyPatch(typeof(CharacterDollRoom))]
    private static class CharacterDollRoom_Patch {
        [HarmonyPatch(nameof(CharacterDollRoom.Show)), HarmonyPrefix]
        private static void Show() {
            m_ForceNoExcludeNewEEs = false;
        }

        [HarmonyPatch(nameof(CharacterDollRoom.Hide)), HarmonyPrefix]
        private static void Hide() {
            m_ForceNoExcludeNewEEs = true;
        }
    }
}

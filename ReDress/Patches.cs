using HarmonyLib;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Area;
using Kingmaker.EntitySystem.Persistence;
using Kingmaker.ResourceLinks;
using Kingmaker.UI.DollRoom;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Parts;
using Kingmaker.View;
using Kingmaker.View.Mechanics.Entities;
using Kingmaker.Visual.CharacterSystem;
using RogueTrader.Code.ShaderConsts;
using System.Reflection.Emit;
using UnityEngine;
using static ReDress.Main;

namespace ReDress;
[HarmonyPatch]
public static class Patches {
    private static bool m_IsCurrentlyLoadingGame = false;
    private static bool m_IsInDollRoom;
    private static string? m_CurrentUid;
    private static (EntityPartStorage.CustomColorTex?, EntityPartStorage.CustomColorTex?, EntityPartStorage.CustomColorTex?) m_CustomOverride = (null, null, null);
    private static Dictionary<string, HashSet<EquipmentEntity>> m_CachedLinks = new();
    internal static bool IsOutfitColoured = false;
    [HarmonyPatch(typeof(AbstractUnitEntityView), nameof(AbstractUnitEntityView.SetupCharacterAvatar)), HarmonyPrefix]
    private static void AbstractUnitEntityView_SetupCharacterAvatar(Character character, AbstractUnitEntityView __instance) {
        if (character != null) {
            try {
                Helpers.UIdCache.Add(character, __instance.UniqueId);
            } catch (ArgumentException) { }
        }
    }
    [HarmonyPatch(typeof(DollData), nameof(DollData.CreateUnitView)), HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> DollData_CreateUnitView(IEnumerable<CodeInstruction> instructions) {
        var method = AccessTools.Method(typeof(Character), nameof(Character.RemoveAllEquipmentEntities));
        foreach (var inst in instructions) {
            yield return inst;
            if (inst.Calls(method)) {
                yield return new(OpCodes.Ldloc_3);
                yield return CodeInstruction.Call((Character c) => SaveCharacterForId(c));
            }
        }
    }
    private static void SaveCharacterForId(Character c) {
        if (m_CurrentUid != null && c != null) {
            try {
                Helpers.UIdCache.Add(c, m_CurrentUid);
            } catch (ArgumentException) { }
        }
    }
    [HarmonyPatch(typeof(PartUnitViewSettings), nameof(PartUnitViewSettings.Instantiate)), HarmonyPrefix]
    private static void PartUnitViewSettings_Instantiate_Pre(PartUnitViewSettings __instance) {
        m_CurrentUid = __instance.Owner.UniqueId;
    }
    [HarmonyPatch(typeof(PartUnitViewSettings), nameof(PartUnitViewSettings.Instantiate)), HarmonyPostfix]
    private static void PartUnitViewSettings_Instantiate(PartUnitViewSettings __instance, ref UnitEntityView __result) {
        m_CurrentUid = Helpers.GetUIdFromUnit(__instance.Owner)!;
#if DEBUG
        Log.Log($"Check for unit: {__instance.Owner.Name}-{m_CurrentUid}");
#endif
        EntityPartStorage.perSave.AddClothes.TryGetValue(m_CurrentUid, out var outfitIds);
        EntityPartStorage.perSave.IncludeByName.TryGetValue(m_CurrentUid, out var includeIds);
        EntityPartStorage.perSave.ExcludeByName.TryGetValue(m_CurrentUid, out var excludeIds);
        EntityPartStorage.perSave.NakedFlag.TryGetValue(m_CurrentUid, out var nakedFlag);
        if (outfitIds?.Count > 0 || includeIds?.Count > 0 || excludeIds?.Count > 0) {
#if DEBUG
            Log.Log("Found Overrides. Changing UnitEntityView.");
#endif
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

    [HarmonyPatch(typeof(Character))]
    private static class Character_Patch {
        [HarmonyPatch(nameof(Character.AddEquipmentEntity), [typeof(EquipmentEntity), typeof(bool)]), HarmonyPrefix]
        private static bool AddEquipmentEntity(Character __instance, EquipmentEntity ee) {
            try {
                var uniqueId = Helpers.GetUIdFromCharacter(__instance);
                if (uniqueId == null) {
                    return true;
                }
                if (m_IsInDollRoom && m_Settings.ShouldExcludeNewEEs) {
                    EntityPartStorage.perSave.ExcludeByName.TryGetValue(uniqueId, out var tmpExcludes);
                    tmpExcludes ??= [];

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
                    if (defaultClothes?.Contains(ee) ?? false) {
                        return false;
                    }
                }
                EntityPartStorage.perSave.ExcludeByName.TryGetValue(uniqueId, out var excludeIds);
                if (excludeIds?.Count > 0) {
                    if (excludeIds.Contains(ee.name)) {
                        return false;
                    }
                }
            } catch (Exception e) {
                Log.Log(e.ToString());
            }
            return true;
        }
        [HarmonyPatch(nameof(Character.ColorizeOutfitPart)), HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> ColorizeOutfitPart(IEnumerable<CodeInstruction> instructions) {
            var method = AccessTools.PropertySetter(typeof(Renderer), nameof(Renderer.sharedMaterials));
            foreach (var inst in instructions) {
                yield return inst;
                if (inst.Calls(method)) {
                    yield return new(OpCodes.Ldloc_3);
                    yield return new(OpCodes.Ldarg_0);
                    yield return new(OpCodes.Ldarg_2);
                    yield return CodeInstruction.Call((List<Material> mats, Character c, EquipmentEntity ee) => Helpers.ColourOutfitPart(mats, c, ee));
                }
            }
        }
        [HarmonyPatch(nameof(Character.ColorizeOutfitPart)), HarmonyPostfix]
        private static void ColorizeOutfitPart_Post(Character __instance, EquipmentEntity.OutfitPart outfitPart, GameObject newOutfitObject, EquipmentEntity ee) {
            if (IsOutfitColoured) {
                IsOutfitColoured = false;
            } else {
                // Case: Base game doesn't try to clorize the outfit (e.g. because ramps are not set)
                var renderer = newOutfitObject.GetComponentInChildren<Renderer>();
                if (renderer != null && !__instance.ColorizedOutfitParts.Contains(renderer)) {
                    Helpers.ColourOutfitPart(renderer, __instance, ee, outfitPart, newOutfitObject.name);
                }
            }
        }
        [HarmonyPatch(nameof(Character.UpdateColorizedOutfitRamps)), HarmonyPostfix]
        private static void UpdateColorizedOutfitRamps(Character __instance, EquipmentEntity ee) {
            var uid = Helpers.GetUIdFromCharacter(__instance);
            if (uid == null) {
                return;
            }
            List<Material>? mats = null;
            if (EntityPartStorage.perSave.RampOverrideByName.TryGetValue(uid, out var overrides)) {
                mats = Helpers.GetMats(__instance, ee);
                var eeName = ee.name ?? ee.ToString();
                if (eeName == null) {
                    Log.Log(new System.Diagnostics.StackTrace().ToString());
                    return;
                }
                if (overrides.TryGetValue(eeName, out var pair)) {
                    foreach (var mat in mats) {
                        mat.SetTexture(ShaderProps._Ramp1, ee.PrimaryRamps[pair.PrimaryIndex]);
                        mat.SetTexture(ShaderProps._Ramp2, ee.SecondaryRamps[pair.PrimaryIndex]);
                    }
                }
            }
            if (EntityPartStorage.perSave.CustomColorsByName.TryGetValue(uid, out var overrides2)) {
                mats = Helpers.GetMats(__instance, ee);
                var eeName = ee.name ?? ee.ToString();
                if (eeName == null) {
                    Log.Log(new System.Diagnostics.StackTrace().ToString());
                    return;
                }
                if (overrides2.TryGetValue(eeName, out var customColor)) {
                    foreach (var mat in mats) {
                        if (customColor.Item1 != null) {
                            mat.SetTexture(ShaderProps._Ramp1, customColor.Item1.MakeTex());
                        }
                        if (customColor.Item2 != null) {
                            mat.SetTexture(ShaderProps._Ramp2, customColor.Item2.MakeTex());
                        }
                    }
                }
            }
        }
        [HarmonyPatch(nameof(Character.MergeOverlays)), HarmonyPrefix]
        private static void MergeOverlays(Character __instance) {
            m_CurrentUid = Helpers.GetUIdFromCharacter(__instance);
        }
        [HarmonyPatch(nameof(Character.OnRenderObject)), HarmonyPrefix]
        private static void OnRenderObject(Character __instance) {
            m_CurrentUid = Helpers.GetUIdFromCharacter(__instance);
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

    [HarmonyPatch(typeof(CharacterDollRoom))]
    private static class CharacterDollRoom_Patch {
        [HarmonyPatch(nameof(CharacterDollRoom.Show)), HarmonyPrefix]
        private static void Show() {
            m_IsInDollRoom = true;
        }

        [HarmonyPatch(nameof(CharacterDollRoom.Hide)), HarmonyPrefix]
        private static void Hide() {
            m_IsInDollRoom = false;
        }
    }
}

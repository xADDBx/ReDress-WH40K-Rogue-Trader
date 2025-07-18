using Kingmaker;
using Kingmaker.Blueprints.Root;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.Mechanics.Entities;
using Kingmaker.PubSubSystem;
using Kingmaker.PubSubSystem.Core;
using Kingmaker.UI.MVVM.VM.CharGen;
using Kingmaker.View;
using Kingmaker.Visual.CharacterSystem;
using Kingmaker.Visual.Sound;
using RogueTrader.Code.ShaderConsts;
using System.ComponentModel;
using UnityEngine;
using UnityModManagerNet;

namespace ReDress;
public static class Helpers {
    internal static readonly Dictionary<Outfit, string> JobClothesIDs = new() { { Outfit.Criminal, "2415ba44fb9e4bd5b22f4f574b5f0cd8" }, { Outfit.Nobility, "86c28cf33b7e42ecb190cd6c1b2aa4cc" },
        { Outfit.Commissar, "28b88e24ffa341a7b1dfc286583f226d" }, {Outfit.Navy, "e28fc5d840134da892c24d24738ceb63" }, { Outfit.Militarum, "394e4b94f1284fefa4f47f1df0e42161" },
        { Outfit.Psyker,"76c6f2ce3e5d4c8d9cc0c22145d4d630" }, { Outfit.Crusader, "b63a4c7a04fd47dcb8c6dfc24f92f33d" }, { Outfit.Navigator, "3afcd2d9ccb24e85844857ba852c1d88" },
        { Outfit.Arbitrator, "de047b29d30d487cb5899ea1ec5b890f" } };
    public static string ToDescriptionString(this Outfit val) {
        DescriptionAttribute[] attributes = (DescriptionAttribute[])val
           .GetType()
           .GetField(val.ToString())
           .GetCustomAttributes(typeof(DescriptionAttribute), false);
        return attributes.Length > 0 ? attributes[0].Description : string.Empty;
    }
    internal static void OpenAppeareanceChanger(BaseUnitEntity u) {
        // Copied from ChangeAppearance GameAction
        UnityModManager.UI.Instance.ToggleWindow();
        SoundState.Instance.OnMusicStateChange(MusicStateHandler.MusicState.Chargen);
        CharGenConfig.Create(u, CharGenConfig.CharGenMode.Appearance).SetOnComplete(unit => {
            UnitEntityView view = unit.CreateView();
            UnitEntityView view2 = unit.View;
            unit.DetachView();
            view2.DestroyViewObject();
            unit.AttachView(view);
            Game.Instance.Player.UpdateClaimedDlcRewardsByChosenAppearance(unit);
        }).SetOnClose(() => { }).SetOnCloseSoundAction(() => {
            SoundState.Instance.OnMusicStateChange(MusicStateHandler.MusicState.Setting);
        }).OpenUI();
    }
    public static CharacterColorsProfile? GetClothColorsProfile(EquipmentEntity equipmentEntity, out RampColorPreset? colorPreset, bool secondary) {
        if (equipmentEntity != null) {
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
        DoForEachValidUnit(unit => {
            if (pair.PrimaryIndex >= 0) {
                unit.View.CharacterAvatar.SetPrimaryRampIndex(ee, pair.PrimaryIndex);
            }
            if (pair.SecondaryIndex >= 0) {
                unit.View.CharacterAvatar.SetSecondaryRampIndex(ee, pair.SecondaryIndex);
            }
            unit.View.CharacterAvatar.IsAtlasesDirty = true;
            EventBus.RaiseEvent(unit, (IUnitVisualChangeHandler h) => {
                h.HandleUnitChangeEquipmentColor(pair.PrimaryIndex, false);
            }, true);
        });
    }
    internal static Dictionary<Character, string> UIdCache = [];
    public static string? GetUIdFromCharacter(Character c) {
        if (UIdCache.TryGetValue(c, out var uId)) {
            return uId;
        } else {
            Main.Log.Log($"Can't find Owner Uid for Character {c.name} - {c}");
            Main.Log.Log(new System.Diagnostics.StackTrace().ToString());
            return null;
        }
    }
    public static bool IsSave(AbstractUnitEntity? unit) {
        return unit != null && !unit.IsDisposed && !unit.IsDisposingNow;
    }
    public static string? GetUIdFromUnit(AbstractUnitEntity? unit, bool onlyWhenMatchingPickedUnit = true) {
        return IsSave(unit) ? unit!.UniqueId : null;
    }
    public static void DoForEachValidUnit(Action<AbstractUnitEntity> applyToUnit) {
        if (IsSave(Main.PickedUnit)) {
            applyToUnit(Main.PickedUnit!);
        }
    }
    public static List<Material> GetMats(Character __instance, EquipmentEntity ee) {
        List<Material> mats = [];
        foreach (Character.OutfitPartInfo outfitPartInfo in __instance.m_OutfitObjectsSpawned) {
            if (outfitPartInfo.Ee == ee) {
                if (outfitPartInfo.OutfitPart.ColorMask == null) {
                    break;
                }
                var renderer = outfitPartInfo.GameObject.GetComponentInChildren<Renderer>();
                foreach (Material material in renderer.sharedMaterials) {
                    mats.Add(material);
                }
            }
        }
        return mats;
    }
    private static List<Material> GetMats(Renderer renderer, Character c, EquipmentEntity ee, EquipmentEntity.OutfitPart outfit, string goName) {
        List<Material> ret = [];
        if (renderer != null) {
            foreach (var mat in renderer.sharedMaterials) {
                Material mat2 = new(BlueprintRoot.Instance.CharGenRoot.EquipmentColorizerShader);
                mat2.SetTexture(ShaderProps._BaseMap, mat.GetTexture(ShaderProps._BaseMap));
                mat2.SetTexture(ShaderProps._BumpMap, mat.GetTexture(ShaderProps._BumpMap));
                mat2.SetTexture(ShaderProps._MasksMap, mat.GetTexture(ShaderProps._MasksMap));
                mat2.SetTexture(ShaderProps._ColorMask, outfit.ColorMask);
                Character.SelectedRampIndices selectedRampIndices = c.RampIndices.FirstOrDefault((Character.SelectedRampIndices i) => i.EquipmentEntity == ee);
                if (selectedRampIndices != null) {
                    if (ee.PrimaryRamps.Count > selectedRampIndices.PrimaryIndex && selectedRampIndices.PrimaryIndex >= 0) {
                        mat2.SetTexture(ShaderProps._Ramp1, ee.PrimaryRamps[selectedRampIndices.PrimaryIndex]);
                    }
                    if (ee.SecondaryRamps.Count > selectedRampIndices.SecondaryIndex && selectedRampIndices.SecondaryIndex >= 0) {
                        mat2.SetTexture(ShaderProps._Ramp2, ee.SecondaryRamps[selectedRampIndices.SecondaryIndex]);
                    }
                }
                mat2.name = goName + "_material";
                ret.Add(mat2);
            }
        }
        return ret;
    }
    public static void ColourOutfitPart(Renderer renderer, Character c, EquipmentEntity ee, EquipmentEntity.OutfitPart outfitPart, string goName) {
        var uid = GetUIdFromCharacter(c);
        if (uid == null) {
            Main.Log.Log(new System.Diagnostics.StackTrace().ToString());
            return;
        }
        List<Material>? mats = null;
        if (EntityPartStorage.perSave.RampOverrideByName.TryGetValue(uid, out var overrides)) {
            mats = GetMats(renderer, c, ee, outfitPart, goName);
            var eeName = ee.name ?? ee.ToString();
            if (eeName == null) {
                Main.Log.Log(new System.Diagnostics.StackTrace().ToString());
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
            mats = GetMats(renderer, c, ee, outfitPart, goName);
            var eeName = ee.name ?? ee.ToString();
            if (eeName == null) {
                Main.Log.Log(new System.Diagnostics.StackTrace().ToString());
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
    public static void ColourOutfitPart(List<Material> mats, Character c, EquipmentEntity ee) {
        Patches.IsOutfitColoured = true;
        ColourOutfitPartInternal(mats, c, ee);
    }
    private static void ColourOutfitPartInternal(List<Material> mats, Character c, EquipmentEntity ee) {
        var uid = GetUIdFromCharacter(c);
        if (uid == null) {
            Main.Log.Log(new System.Diagnostics.StackTrace().ToString());
            return;
        }
        if (EntityPartStorage.perSave.RampOverrideByName.TryGetValue(uid, out var overrides)) {
            var eeName = ee.name ?? ee.ToString();
            if (eeName == null) {
                Main.Log.Log(new System.Diagnostics.StackTrace().ToString());
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
            var eeName = ee.name ?? ee.ToString();
            if (eeName == null) {
                Main.Log.Log(new System.Diagnostics.StackTrace().ToString());
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
}

using Kingmaker;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.PubSubSystem.Core;
using Kingmaker.PubSubSystem;
using Kingmaker.UI.MVVM.VM.CharGen;
using Kingmaker.View;
using Kingmaker.Visual.CharacterSystem;
using Kingmaker.Visual.Sound;
using System.ComponentModel;
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
        if (pair.PrimaryIndex >= 0) {
            Main.PickedUnit!.View.CharacterAvatar.SetPrimaryRampIndex(ee, pair.PrimaryIndex);
        }
        if (pair.SecondaryIndex >= 0) {
            Main.PickedUnit!.View.CharacterAvatar.SetSecondaryRampIndex(ee, pair.SecondaryIndex);
        }
        Main.PickedUnit!.View.CharacterAvatar.IsAtlasesDirty = true;
        EventBus.RaiseEvent(Main.PickedUnit, (IUnitVisualChangeHandler h) => {
            h.HandleUnitChangeEquipmentColor(pair.PrimaryIndex, false);
        }, true);
    }
}

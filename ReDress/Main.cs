using HarmonyLib;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.Blueprints.Root;
using Kingmaker.UI.MVVM.View.CharGen.Common;
using Kingmaker.UI.MVVM.VM.CharGen;
using Kingmaker.UnitLogic.FactLogic;
using Kingmaker.UnitLogic.Levelup.Selections;
using Kingmaker.UnitLogic.Levelup.Selections.Doll;
using Kingmaker.UnitLogic.Progression.Paths;
using Kingmaker.View;
using Kingmaker.Visual.CharacterSystem;
using Kingmaker.Visual.Particles;
using Owlcat.Runtime.Core.Utility;
using Owlcat.Runtime.Core.Utility.Locator;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityModManagerNet;

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
        HarmonyInstance = new Harmony(modEntry.Info.Id);
        HarmonyInstance.PatchAll(Assembly.GetExecutingAssembly());
        return true;
    }
    internal static Exception error = null;
    internal static bool printError = false;
    internal static bool shouldResetError = false;
    //internal static BlueprintComponent cachedComponent = null;
    //internal static BlueprintOriginPath pregenPath = null;
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
                foreach (var charac in Game.Instance.Player.Party) {
                    if (GUILayout.Button($"Redress {charac.Name}")) {
                        UnityModManager.UI.Instance.ToggleWindow();
                        isInRoom = true;
                        Game.Instance.Player.CreateCustomCompanion(newCompanion => {
                            try {
                                isInRoom = false;
                                // pregenPath.Components[1] = cachedComponent;
                                Game.Instance.Player.MainCharacterEntity.ViewSettings.SetDoll(newCompanion.ViewSettings.Doll);
                                var newKmm = newCompanion.Facts.m_Facts.First(f => f.Blueprint.name.Contains("Occupation"))?.GetComponent<AddKingmakerEquipmentEntity>();
                                var oldKmm = Game.Instance.Player.MainCharacterEntity.Facts.m_Facts.First(f => f.Blueprint.name.Contains("Occupation"))?.GetComponent<AddKingmakerEquipmentEntity>();
                                if (newKmm != null && oldKmm != null) {
                                    oldKmm.m_EquipmentEntity = newKmm.m_EquipmentEntity;
                                }
                                /* Bruh
                                var uev = Game.Instance.Player.MainCharacterEntity.ViewSettings.Doll.CreateUnitView(true);
                                uev.ViewTransform.position = Game.Instance.Player.MainCharacterEntity.Position;
                                uev.ViewTransform.rotation = Quaternion.Euler(0f, Game.Instance.Player.MainCharacterEntity.Orientation, 0f);
                                Quaternion quaternion2 = (uev.ForbidRotation ? Quaternion.identity : Quaternion.Euler(0f, Game.Instance.Player.MainCharacterEntity.Orientation, 0f));
                                var uev2 = UnityEngine.Object.Instantiate<UnitEntityView>(uev, Game.Instance.Player.MainCharacterEntity.Position, quaternion2);
                                Game.Instance.Player.AttachView(uev2);
                                Services.GetInstance<CharacterAtlasService>().Update();
                                */
                            } catch (Exception ex) {
                                log.Log(ex.ToString());
                                error = ex;
                            }
                        }, null, CharGenConfig.CharGenCompanionType.Common);
                    }
                }
            } catch (Exception ex) {
                log.Log(ex.ToString());
                error = ex;
            }
        }
    }
    /* This allows picking different occupations thatn the default ones. Useless because they don't have default clothing.
    [HarmonyPatch(typeof(CharGenContext))]
    public static class CharGenView_Patch {
        [HarmonyPatch(nameof(CharGenContext.GetOriginPath))]
        [HarmonyPostfix]
        public static void GetOriginPath(ref BlueprintOriginPath __result) {
            if (isInRoom) {
                pregenPath = __result;
                cachedComponent = __result.Components[1];
                __result.Components[1] = (ResourcesLibrary.BlueprintsCache.Load("68eaf96bad9748739ca44fedc7b5c7c4") as BlueprintOriginPath).Components[1];
            }
        }
    }
    */

#if DEBUG
    static bool OnUnload(UnityModManager.ModEntry modEntry)
    {
        HarmonyInstance.UnpatchAll(modEntry.Info.Id);
        return true;
    }
#endif
}
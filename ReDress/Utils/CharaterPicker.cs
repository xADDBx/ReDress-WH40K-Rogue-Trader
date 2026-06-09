using Kingmaker;
using Kingmaker.Designers;
using Kingmaker.EntitySystem;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.Mechanics.Entities;
using Kingmaker.UnitLogic;
using Kingmaker.Utility.DotNetExtensions;
using ReDress;
using UnityEngine;

namespace ReDress;
internal enum CharacterListType {
    Party,
    PartyAndPets,
    AllCharacters,
    Active,
    CustomCompanions,
    Nearby,
    Friendly,
    Enemies
}
internal static partial class CharacterListType_Localizer {
    internal static string GetLocalized(this CharacterListType type) {
        return type switch {
            CharacterListType.Party => "Party",
            CharacterListType.PartyAndPets => "Party & Pets",
            CharacterListType.AllCharacters => "All Characters",
            CharacterListType.Active => "Active",
            CharacterListType.CustomCompanions => "Custom Companions",
            CharacterListType.Nearby => "Nearby",
            CharacterListType.Friendly => "Friendly",
            CharacterListType.Enemies => "Enemies",
            _ => "!!Error Unknown CharacterList!!",
        };
    }
}
internal static partial class CharacterPicker {
    private static readonly int m_CacheDuration = 1;
    private static readonly Dictionary<CharacterListType, TimedCache<List<BaseUnitEntity>>> m_Lists = new() {
        [CharacterListType.Party] = new(() => Game.Instance.Player.Party?.NotNull().Where(u => !u!.IsDisposed && !u.IsDisposingNow && u.View?.CharacterAvatar != null)?.ToList() ?? [], m_CacheDuration),
        [CharacterListType.PartyAndPets] = new(() => Game.Instance.Player.PartyAndPets?.NotNull().Where(u => !u!.IsDisposed && !u.IsDisposingNow && u.View?.CharacterAvatar != null)?.ToList() ?? [], m_CacheDuration),
        [CharacterListType.AllCharacters] = new(() => Game.Instance.Player.AllCharacters?.NotNull().Where(u => !u!.IsDisposed && !u.IsDisposingNow && u.View?.CharacterAvatar != null)?.ToList() ?? [], m_CacheDuration),
        [CharacterListType.Active] = new(() => Game.Instance.Player.ActiveCompanions?.NotNull().Where(u => !u!.IsDisposed && !u.IsDisposingNow && u.View?.CharacterAvatar != null)?.ToList() ?? [], m_CacheDuration),
        [CharacterListType.CustomCompanions] = new(() => Game.Instance.Player.AllCharacters.Where(u => u.IsCustomCompanion())?.NotNull().Where(u => !u!.IsDisposed && !u.IsDisposingNow && u.View?.CharacterAvatar != null)?.ToList() ?? [], m_CacheDuration),
        [CharacterListType.Nearby] = new(() => {
            var player = GameHelper.GetPlayerCharacter();
            var result = GameHelper.GetTargetsAround(player.Position, 40, false, false)?.NotNull().Where(u => !u!.IsDisposed && !u.IsDisposingNow && u.View?.CharacterAvatar != null)?.ToList()?? [];
            result.Sort((BaseUnitEntity x, BaseUnitEntity y) => (int)(x.DistanceTo(GameHelper.GetPlayerCharacter()) - y.DistanceTo(GameHelper.GetPlayerCharacter())));
            return result;
        }, m_CacheDuration),
        [CharacterListType.Friendly] = new(() => {
            var player = GameHelper.GetPlayerCharacter();
            return Game.Instance.State.AllBaseUnits.Where(u => u != null && !u.IsEnemy(player))?.NotNull().Where(u => !u!.IsDisposed && !u.IsDisposingNow && u.View?.CharacterAvatar != null)?.ToList() ?? [];
        }, m_CacheDuration),
        [CharacterListType.Enemies] = new(() => {
            var player = GameHelper.GetPlayerCharacter();
            return Game.Instance.State.AllBaseUnits.Where(u => u != null && u.IsEnemy(player))?.NotNull().Where(u => !u!.IsDisposed && !u.IsDisposingNow && u.View?.CharacterAvatar != null)?.ToList() ?? [];
        }, m_CacheDuration)
    };
    private static CharacterListType m_CurrentList;
    private static WeakReference<BaseUnitEntity>? m_CurrentUnit;
    public static BaseUnitEntity? CurrentUnit {
        get {
            if (m_CurrentUnit is not null && m_CurrentUnit.TryGetTarget(out var unit) && !unit.IsDisposed && !unit.IsDisposingNow) {
                return unit;
            } else {
                return null;
            }
        }
    }
    public static List<BaseUnitEntity> CurrentUnits {
        get {
            return m_Lists[m_CurrentList];
        }
    }
    public static bool OnFilterPickerGUI(int? xcols = null, params GUILayoutOption[] options) {
        if (!IsInGame()) {
            Label("This cannot be used from Main Menu".Red());
            return false;
        }
        if (SelectionGrid(ref m_CurrentList, xcols ?? Math.Min(11, m_Lists.Count), type => type.GetLocalized(), options)) {
            m_CurrentUnit = null;
            return true;
        }
        return false;
    }
    public static bool OnCharacterPickerGUI(int? xcols = null, params GUILayoutOption[] options) {
        if (!IsInGame()) {
            Label("This cannot be used from Main Menu".Red());
            return false;
        }
        var charactersList = CurrentUnits;
        if (charactersList.Count == 0) {
            Label("There are no characters in this list".Orange(), options);
        } else {
            var tmp = CurrentUnit;
            if (SelectionGrid(ref tmp, charactersList, xcols ?? Math.Min(8, (charactersList.Count + 1)), unit => GetUnitName(unit), options)) {
                if (tmp != null) {
                    m_CurrentUnit = new(tmp);
                } else {
                    m_CurrentUnit = null;
                }
                return true;
            }
        }
        return false;
    }
    private static bool IsInGame() {
        return Game.Instance.Player?.Party?.Count > 0;
    }
    private static void Label(string? title = null, params GUILayoutOption[] options) {
        options = options.Length == 0 ? [GUILayout.ExpandWidth(false)] : options;
        GUILayout.Label(title ?? "", options);
    }
    private static readonly TimedCache<Dictionary<BaseUnitEntity, float>> m_DistanceToCache = new(() => []);
    private static string GetUnitName(BaseUnitEntity? unit, bool includeId = false) {
        if (unit == null) {
            return "!!Null Unit!!";
        }
        try {
            var name = unit.CharacterName;
            if (string.IsNullOrWhiteSpace(name)) {
                name = unit.Blueprint.name;
            }
            if (includeId) {
                name += $" ({unit.UniqueId})";
            }
            Dictionary<BaseUnitEntity, float> distanceCache = m_DistanceToCache;
            if (!distanceCache.TryGetValue(unit, out var dist)) {
                dist = GameHelper.GetPlayerCharacter().DistanceTo(unit);
                distanceCache[unit] = dist;
            }
            if (dist > 1) {
                name += " " + dist.ToString("0") + "m";
            }
            return name;
        } catch (Exception ex) {
            var id = unit.Blueprint?.AssetGuid.ToString() ?? "??NULL??";
            Main.Log.Log($"Encountered exception while getting name for unit with bp {id}: \n{ex}");
            return $"AssetId: {id}";
        }
    }
    private static Dictionary<Type, Array> m_EnumCache = [];
    private static Dictionary<Type, Dictionary<object, int>> m_IndexToEnumCache = [];
    private static Dictionary<Type, string[]> m_EnumNameCache = [];
    private static bool SelectionGrid<TEnum>(ref TEnum selected, int xCols, Func<TEnum, string>? titler, params GUILayoutOption[] options) where TEnum : Enum {
        if (!m_EnumCache.TryGetValue(typeof(TEnum), out var vals)) {
            vals = Enum.GetValues(typeof(TEnum));
            m_EnumCache[typeof(TEnum)] = vals;
        }
        if (!m_EnumNameCache.TryGetValue(typeof(TEnum), out var names)) {
            Dictionary<object, int> indexToEnum = [];
            List<string> tmpNames = [];
            for (var i = 0; i < vals.Length; i++) {
                string name;
                var val = vals.GetValue(i);
                indexToEnum[val] = i;
                if (titler != null) {
                    name = titler((TEnum)val);
                } else {
                    name = Enum.GetName(typeof(TEnum), val);
                }
                tmpNames.Add(name);
            }
            names = [.. tmpNames];
            m_EnumNameCache[typeof(TEnum)] = names;
            m_IndexToEnumCache[typeof(TEnum)] = indexToEnum;
        }
        if (xCols <= 0) {
            xCols = vals.Length;
        }
        var selectedInt = m_IndexToEnumCache[typeof(TEnum)][selected];
        // Create a copy to not recolour the selected element permanently
        // names = [.. names];
        // Better idea: Just cache that one name and change it back after
        var uncolored = names[selectedInt];
        names[selectedInt] = uncolored.Orange();
        var newSel = GUILayout.SelectionGrid(selectedInt, names, xCols, options);
        names[selectedInt] = uncolored;
        var changed = selectedInt != newSel;
        if (changed) {
            selected = (TEnum)vals.GetValue(newSel);
        }
        return changed;
    }
    private static bool SelectionGrid<T>(ref T? selected, IList<T> vals, int xCols, Func<T, string>? titler, params GUILayoutOption[] options) where T : notnull {
        if (xCols <= 0) {
            xCols = vals.Count;
        }
        var selectedInt = selected != null ? vals.IndexOf(selected) + 1 : 0;
        string[] names = ["None", .. vals.Select(x => {
            if (titler != null) {
                return titler(x);
            } else {
                return x.ToString();
            }
        })];
        names[selectedInt] = names[selectedInt].Orange();
        var newSel = GUILayout.SelectionGrid(selectedInt, names, xCols, options);
        var changed = selectedInt != newSel;
        if (changed) {
            if (newSel == 0) {
                selected = default;
            } else {
                selected = vals[newSel - 1];
            }
        }
        return changed;
    }
}

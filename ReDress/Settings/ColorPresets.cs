namespace ReDress;
internal class ColorPresets : AbstractSettings {
    private static readonly Lazy<ColorPresets> _instance = new Lazy<ColorPresets>(() => {
        var instance = new ColorPresets();
        instance.Load();
        return instance;
    });
    public static ColorPresets CustomColorPresets => _instance.Value;
    protected override string Name => "SavedColors.json";
    public List<EntityPartStorage.CustomColor> CustomColors = [];
}

namespace ReDress;
internal class TexturePresets : AbstractSettings {
    private static readonly Lazy<TexturePresets> _instance = new Lazy<TexturePresets>(() => {
        var instance = new TexturePresets();
        instance.Load();
        return instance;
    });
    public static TexturePresets CustomTexturePresets => _instance.Value;
    protected override string Name => "SavedTextures.json";
    public List<EntityPartStorage.CustomColorTex> CustomColorTextures = [];
}

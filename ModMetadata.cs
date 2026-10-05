namespace SprocketKeybinds;

internal sealed record ModInfo(string Author, string Name);
internal sealed record PackInfo(string Id, string Name);
internal sealed record Presentation(string Owner, ModInfo Mod, string GroupKey, string GroupName, bool Pack);

// Display metadata never touches input identities, overrides or menu transactions.
internal sealed class ModMetadata
{
    private readonly Dictionary<string, ModInfo> mods = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PackInfo> packs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> packNames = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, ModInfo> defaults = new(StringComparer.Ordinal)
    {
        ["nl.roan.sprocket.hydropneumatic"] = new("Nero", "HPS"),
        ["nl.roan.sprocket.telescopicmast"] = new("Nero", "Mast"),
        ["nl.roan.sprocket.thermalsight"] = new("Nero", "Thermal")
    };
    private static string Display(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 128 || value.Any(c => char.IsControl(c) || c == '<' || c == '>'))
            throw new ArgumentException("Display metadata requires 1–128 printable characters without markup.");
        return value.Trim();
    }
    public void Register(string owner, string author, string name)
    {
        BindingStore.Identity(owner, "metadata");
        var info = new ModInfo(Display(author), Display(name));
        if (mods.TryGetValue(owner, out var old) && old != info) throw new ArgumentException("Conflicting mod metadata for " + owner);
        mods[owner] = info;
    }
    public void RegisterPack(string owner, string packId, string name)
    {
        BindingStore.Identity(owner, "metadata"); BindingStore.Identity(packId, "metadata");
        var info = new PackInfo(packId, Display(name));
        if (packs.TryGetValue(owner, out var old) && old != info) throw new ArgumentException("An owner may declare only one primary pack.");
        if (packNames.TryGetValue(packId, out var oldName) && oldName != info.Name) throw new ArgumentException("Conflicting pack display name.");
        packs[owner] = info; packNames[packId] = info.Name;
    }
    public Presentation Resolve(string owner, string fallback, bool byPack)
    {
        var info = mods.GetValueOrDefault(owner) ?? defaults.GetValueOrDefault(owner)
            ?? new ModInfo("Unknown", new string(fallback.Select(c => char.IsControl(c) ? ' ' : c).ToArray()));
        if (byPack && packs.TryGetValue(owner, out var pack)) return new(owner, info, "pack:" + pack.Id, pack.Name, true);
        return new(owner, info, "author:" + info.Author, info.Author, false);
    }
    public Presentation[] Order(IEnumerable<(string Owner, string Name)> owners, bool byPack) => owners
        .Select(o => Resolve(o.Owner, o.Name, byPack)).OrderBy(p => p.GroupName, StringComparer.Ordinal)
        .ThenBy(p => p.GroupKey, StringComparer.Ordinal).ThenBy(p => p.Mod.Author, StringComparer.Ordinal)
        .ThenBy(p => p.Mod.Name, StringComparer.Ordinal).ThenBy(p => p.Owner, StringComparer.Ordinal).ToArray();
}

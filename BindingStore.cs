using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SprocketKeybinds;

// No Unity dependencies: persistence and menu transaction behavior can be tested outside the game.
internal sealed class BindingStore
{
    public Dictionary<string, string> Values { get; private set; } = new(StringComparer.Ordinal);
    private Dictionary<string, string>? snapshot;
    private readonly string path;
    public bool Editing => snapshot != null;
    public BindingStore(string path) => this.path = path;
    public void Load()
    {
        if (!File.Exists(path)) return;
        var document = JsonSerializer.Deserialize<StoreDocument>(File.ReadAllText(path))
            ?? throw new InvalidDataException("Empty keybind document.");
        if (document.Version != 1 || document.Bindings == null || document.Bindings.Any(p => string.IsNullOrEmpty(p.Key) || p.Value == null))
            throw new InvalidDataException("Unsupported or invalid keybind document.");
        Values = new(document.Bindings, StringComparer.Ordinal);
    }
    public void Begin()
    {
        if (Editing) throw new InvalidOperationException("A keybind edit session is already open.");
        snapshot = new(Values, StringComparer.Ordinal);
    }
    public bool ImportOnce(string identity, string value)
    {
        if (Editing) throw new InvalidOperationException("Import legacy settings outside a menu edit session.");
        if (Values.ContainsKey(identity)) return false;
        Values.Add(identity, value);
        try { Commit(); }
        catch { Values.Remove(identity); throw; }
        return true;
    }
    public void Commit()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(new StoreDocument { Bindings = Values }, new JsonSerializerOptions { WriteIndented = true }));
        if (File.Exists(path)) File.Replace(temp, path, path + ".bak");
        else File.Move(temp, path);
        if (Editing) snapshot = new(Values, StringComparer.Ordinal);
    }
    public void Rollback()
    {
        if (snapshot != null) Values = new(snapshot, StringComparer.Ordinal);
    }
    public void End() { Rollback(); snapshot = null; }
    public static string Identity(string owner, string action)
    {
        ValidateId(owner, nameof(owner)); ValidateId(action, nameof(action));
        return owner + "/" + action;
    }
    private static void ValidateId(string id, string name)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Any(c => c > 127 || !(char.IsLetterOrDigit(c) || c is '.' or '_' or '-')))
            throw new ArgumentException("Use a stable ID containing ASCII letters, digits, dots, underscores or hyphens.", name);
    }
    public static string StableGuid(string identity, string part) => new Guid(SHA256.HashData(Encoding.UTF8.GetBytes("SprocketKeybinds/v1/" + identity + "/" + part)).AsSpan(0, 16)).ToString();
    private sealed class StoreDocument
    {
        public int Version { get; set; } = 1;
        public Dictionary<string, string>? Bindings { get; set; } = new();
    }
}


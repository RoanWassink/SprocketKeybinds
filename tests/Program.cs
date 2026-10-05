using SprocketKeybinds;
using System.Text.Json;

var temp = Path.Combine(Path.GetTempPath(), "SprocketKeybinds.Tests." + Guid.NewGuid());
Directory.CreateDirectory(temp);
int passed = 0;
void Check(bool value, string name) { if (!value) throw new Exception(name); Console.WriteLine("PASS " + name); passed++; }
void Reject(Action action, string name) { try { action(); } catch { Check(true, name); return; } throw new Exception("Expected rejection: " + name); }
try
{
    var path = Path.Combine(temp, "keybinds.json");
    var identity = BindingStore.Identity("example.mast", "raise");
    Check(identity == "example.mast/raise", "stable namespaced identity");
    var guid = BindingStore.StableGuid(identity, "binding0");
    Check(Guid.TryParse(guid, out _) && guid == BindingStore.StableGuid(identity, "binding0"), "stable restart GUID");
    Check(guid != BindingStore.StableGuid(identity, "binding1") && guid != BindingStore.StableGuid("example.thermal/raise", "binding0"), "secondary and owner isolation");
    Reject(() => BindingStore.Identity("example/mast", "raise"), "ambiguous IDs rejected");
    Reject(() => BindingStore.Identity("example.mast", ""), "empty IDs rejected");
    var store = new BindingStore(path); store.Load();
    store.Values[identity] = "initial"; store.Values["absent.mod/action"] = "preserved"; store.Commit();
    store.Begin(); store.Values[identity] = "edited"; store.End();
    Check(store.Values[identity] == "initial" && !store.Editing, "Cancel restores initial state");
    store.Begin(); store.Values[identity] = "applied"; store.Commit();
    store.Values[identity] = "after apply"; store.End();
    Check(store.Values[identity] == "applied", "Apply then Cancel retains applied binding");
    var loaded = new BindingStore(path); loaded.Load();
    Check(loaded.Values[identity] == "applied", "restart reloads binding");
    Check(loaded.Values["absent.mod/action"] == "preserved", "settings for absent mods retained");
    var backup = new BindingStore(path + ".bak"); backup.Load();
    Check(backup.Values[identity] == "initial", "atomic save keeps prior file backup");
    loaded.Begin(); Reject(loaded.Begin, "nested sessions rejected"); loaded.Values["late.mod/action"] = "late"; loaded.End();
    Check(!loaded.Values.ContainsKey("late.mod/action"), "late registration cancellation removes new persisted override");
    File.WriteAllText(path, "{bad json");
    Reject(() => new BindingStore(path).Load(), "corrupt configuration rejected");
    Check(File.ReadAllText(path) == "{bad json", "corrupt configuration left intact");
    File.WriteAllText(path, JsonSerializer.Serialize(new { Version = 999, Bindings = new Dictionary<string,string>() }));
    Reject(() => new BindingStore(path).Load(), "unsupported future schema rejected");
    var migration = new BindingStore(Path.Combine(temp, "migration.json"));
    Check(migration.ImportOnce(identity, "custom F9"), "legacy custom binding imported on first use");
    Check(!migration.ImportOnce(identity, "old CFG F8") && migration.Values[identity] == "custom F9", "subsequent import cannot overwrite API choice");
    migration.Values[identity] = "{\"bindings\":[]}"; migration.Commit();
    var reset = new BindingStore(Path.Combine(temp, "migration.json")); reset.Load();
    Check(!reset.ImportOnce(identity, "old CFG F8"), "factory reset survives restart without reimporting CFG");
    Check(reset.ImportOnce("other.mod/action", "unbound"), "independent external mod can import separately");
    reset.Begin(); Reject(() => reset.ImportOnce("late.mod/action", "legacy"), "import cannot bypass open menu transaction"); reset.End();
    var impossible = new BindingStore(temp); // A directory cannot be replaced by a configuration file.
    Reject(() => impossible.ImportOnce(identity, "legacy"), "failed import save reports failure");
    Check(!impossible.Values.ContainsKey(identity), "failed import removes incomplete migration marker");
    var metadata = new ModMetadata();
    const string hps = "nl.roan.sprocket.hydropneumatic", mast = "nl.roan.sprocket.telescopicmast";
    Check(metadata.Resolve(hps, "old", false).Mod == new ModInfo("Nero", "HPS"), "known alias without consumer changes");
    Check(metadata.Resolve("external.mod", "Example", false).Mod == new ModInfo("Unknown", "Example"), "unknown author uses supplied mod name");
    Check(metadata.Resolve(hps, "old", false).GroupKey == metadata.Resolve(mast, "old", false).GroupKey, "two owners share author parent");
    var before = BindingStore.StableGuid(hps + "/raise", "binding0");
    metadata.Register(hps, "External author", "Renamed");
    Check(metadata.Resolve(hps, "old", false).Mod == new ModInfo("External author", "Renamed"), "late explicit metadata overrides alias");
    Check(before == BindingStore.StableGuid(hps + "/raise", "binding0"), "metadata preserves input GUID");
    metadata.Register(hps, "External author", "Renamed");
    Reject(() => metadata.Register(hps, "Other", "Renamed"), "conflicting metadata rejected");
    metadata.Register("external.one", "Author A", "Same"); metadata.Register("external.two", "Author A", "Same");
    var ordered = metadata.Order(new[] { ("external.two", "x"), ("external.one", "x") }, false);
    Check(ordered.Length == 2 && ordered[0].Owner == "external.one", "duplicate display names retain owners and deterministic order");
    metadata.Register("external.three", "Author B", "Same");
    Check(metadata.Resolve("external.three", "x", false).GroupKey != ordered[0].GroupKey, "identical mod names across authors isolated");
    Reject(() => metadata.Register("external.bad", "", "Name"), "empty metadata rejected");
    Reject(() => metadata.Register("external.bad", "A\nB", "Name"), "control characters rejected");
    Reject(() => metadata.Register("external.bad", "Author", "<size=90>Bad</size>"), "rich text metadata rejected");
    metadata.Register("external.long", "Unicode Ω & Author", new string('x', 128));
    Check(metadata.Resolve("external.long", "x", false).Mod.Name.Length == 128, "long Unicode display text accepted for ellipsis rendering");
    Reject(() => metadata.Register("external.bad", "Author", new string('x', 129)), "excessive display length rejected");
    metadata.RegisterPack(hps, "example.pack", "Example Pack");
    Check(metadata.Resolve(hps, "x", true).GroupKey == "pack:example.pack", "explicit primary pack grouping");
    Check(metadata.Resolve(mast, "x", true).GroupKey == "author:Nero", "standalone mod does not inherit pack membership");
    Reject(() => metadata.RegisterPack(hps, "second.pack", "Other"), "ambiguous pack membership rejected");
    Reject(() => metadata.RegisterPack(mast, "example.pack", "Conflicting name"), "conflicting pack label rejected");
    var transaction = new BindingStore(Path.Combine(temp, "grouping.json")); transaction.Values[identity] = "saved"; transaction.Commit();
    transaction.Begin(); transaction.Values[identity] = "pending";
    metadata.Order(new[] { (hps, "HPS"), (mast, "Mast") }, false);
    metadata.Order(new[] { (hps, "HPS"), (mast, "Mast") }, true);
    Check(transaction.Editing && transaction.Values[identity] == "pending", "group switch preserves pending transaction");
    transaction.End(); Check(transaction.Values[identity] == "saved", "Cancel still restores after group switch");
    var unbound = "{\"bindings\":[{\"path\":\"\"}]}";
    transaction.Values[identity] = unbound; transaction.Commit();
    var restarted = new BindingStore(Path.Combine(temp, "grouping.json")); restarted.Load();
    Check(restarted.Values[identity] == unbound && !restarted.ImportOnce(identity, "old key"), "explicit unbound persists and blocks legacy reimport");
    Console.WriteLine($"{passed} checks passed.");
}
finally { Directory.Delete(temp, true); }

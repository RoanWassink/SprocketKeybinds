using System.Text.Json;
using System.Text.Json.Nodes;

namespace SprocketKeybinds;

// Only the known Unity overrides schema for this API's two simple button slots.
internal static class BindingIdentityMigration
{
    internal static string Remap(string json, string oldIdentity, string newIdentity)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        ValidateProperties(root, new[] { "bindings" });
        if (!root.TryGetProperty("bindings", out var bindings) || bindings.ValueKind != JsonValueKind.Array || bindings.GetArrayLength() > 2)
            throw new InvalidDataException("Unsupported binding override schema.");
        string oldAction = oldIdentity.Split('/')[1], newAction = newIdentity.Split('/')[1];
        var seen = new HashSet<int>();
        var remapped = new JsonArray();
        foreach (var binding in bindings.EnumerateArray())
        {
            ValidateProperties(binding, new[] { "action", "id", "path", "interactions", "processors" });
            if (!binding.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.String || !Guid.TryParse(id.GetString(), out var guid))
                throw new InvalidDataException("Missing or invalid binding GUID.");
            int slot = Enumerable.Range(0, 2).FirstOrDefault(i => guid == Guid.Parse(BindingStore.StableGuid(oldIdentity, "binding" + i)), -1);
            if (slot < 0 || !seen.Add(slot)) throw new InvalidDataException("Unknown or duplicate binding slot.");
            if (!binding.TryGetProperty("action", out var action) || action.ValueKind != JsonValueKind.String)
                throw new InvalidDataException("Missing action identity.");
            var actionText = action.GetString();
            bool actionGuid = Guid.TryParse(actionText, out var parsedAction) && parsedAction == Guid.Parse(BindingStore.StableGuid(oldIdentity, "action"));
            if (actionText != "Mod/" + oldAction && !actionGuid) throw new InvalidDataException("Unexpected action identity.");
            foreach (var property in binding.EnumerateObject())
                if (property.Value.ValueKind is not (JsonValueKind.String or JsonValueKind.Null))
                    throw new InvalidDataException("Overrides must be strings or null.");
            if (!binding.TryGetProperty("path", out _)) throw new InvalidDataException("Missing override path.");
            var copy = JsonNode.Parse(binding.GetRawText())!.AsObject();
            copy["id"] = BindingStore.StableGuid(newIdentity, "binding" + slot);
            copy["action"] = actionGuid ? BindingStore.StableGuid(newIdentity, "action") : "Mod/" + newAction;
            remapped.Add(copy);
        }
        return new JsonObject { ["bindings"] = remapped }.ToJsonString();
    }
    private static void ValidateProperties(JsonElement element, string[] allowed)
    {
        if (element.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Expected an override object.");
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
            if (!allowed.Contains(property.Name, StringComparer.Ordinal) || !names.Add(property.Name))
                throw new InvalidDataException("Unknown or duplicate override field: " + property.Name);
    }
}

using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Sprocket.SettingConfiguration;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SprocketKeybinds;

[BepInPlugin(Keybinds.PluginGuid, "Sprocket Keybinds", "0.1.5")]
public sealed class Plugin : BasePlugin
{
    internal static ManualLogSource Logger = null!;
    public override void Load()
    {
        Logger = Log;
        Keybinds.Initialize(Path.Combine(Paths.ConfigPath, "sprocket.keybinds.json"));
        var harmony = new Harmony(Keybinds.PluginGuid + ".ui");
        try
        {
            harmony.PatchAll(typeof(Plugin).Assembly);
            AddComponent<MenuLifetimeDriver>();
            Log.LogInfo("Sprocket Keybinds 0.1.5: shared API and vanilla keymapping hooks ready.");
        }
        catch (Exception e)
        {
            harmony.UnpatchSelf();
            Log.LogError("Keybind API loaded, but vanilla UI hooks are unavailable: " + e);
        }
    }
    internal static void LogInfo(string message) => Logger.LogInfo(message);
    internal static void Safe(Action operation)
    {
        try { operation(); }
        catch (Exception e) { Logger.LogError("[Keybinds UI] " + e); }
    }
}

public sealed class MenuLifetimeDriver : MonoBehaviour
{
    private string lastScenes = "";
    public MenuLifetimeDriver(IntPtr ptr) : base(ptr) { }
    public void LateUpdate() => Plugin.Safe(() =>
    {
        if (Keybinds.Store.Editing && !VanillaMenu.IsOpen)
        {
            Keybinds.End();
            VanillaMenu.Cleanup();
        }
        if (!VanillaMenu.IsOpen && !VanillaMenu.Rebinding)
            foreach (var entry in Keybinds.Entries) entry.EnsureEnabled();
        var scenes = string.Join("|", Enumerable.Range(0, UnityEngine.SceneManagement.SceneManager.sceneCount)
            .Select(i => UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).name));
        if (scenes != lastScenes)
        {
            lastScenes = scenes;
            Plugin.LogInfo("[Input lifetime] Scenes=" + scenes);
            foreach (var entry in Keybinds.Entries) Plugin.LogInfo("[Input lifetime] " + entry.Health);
        }
    });
}

internal static class VanillaMenu
{
    internal sealed record Row(ModKeybind Binding, GameObject Root, RebindActionUI UI, string Label);
    internal static readonly List<Row> Rows = new();
    private static SettingsMenu? menu;
    internal static bool IsOpen => menu != null && menu.gameObject != null && menu.gameObject.activeInHierarchy;
    internal static bool Rebinding => Rows.Any(r => r.UI != null && r.UI.ongoingRebind != null);
    internal static void Attach(SettingsMenu instance)
    {
        menu = instance;
        Refresh();
    }
    internal static void Refresh()
    {
        if (menu == null || menu.keymappingMenu == null || menu.keymappingGUI == null) return;
        var root = menu.keymappingGUI;
        var template = root.GetComponentsInChildren<RebindActionUI>(true).FirstOrDefault(r =>
            r != null && r.ActionLabel != null && r.BindingText != null && !Rows.Any(x => x.UI.Pointer == r.Pointer));
        if (template == null) throw new InvalidOperationException("Vanilla keybind row template not found. Game UI may have changed.");
        // Clone the smallest ancestor that contains the component and both labels. This preserves native button events.
        var rowTransform = template.transform;
        while (!template.ActionLabel.transform.IsChildOf(rowTransform) || !template.BindingText.transform.IsChildOf(rowTransform))
        {
            rowTransform = rowTransform.parent;
            if (rowTransform == null || rowTransform.Pointer == root.transform.Pointer)
                throw new InvalidOperationException("Cannot identify a self-contained vanilla keybind row.");
        }
        if (rowTransform.GetComponentsInChildren<RebindActionUI>(true).Length != 1)
            throw new InvalidOperationException("Vanilla template contains multiple actions; refusing to clone a whole page.");
        ModPanel.Ensure(menu, rowTransform.gameObject, template);
        foreach (var binding in Keybinds.Entries.OrderBy(e => e.ModName, StringComparer.Ordinal).ThenBy(e => e.Id, StringComparer.Ordinal))
        {
            for (var index = 0; index < binding.Action.bindings.Count; index++)
            {
                var bindingId = binding.Action.bindings[index].id.ToString();
                if (Rows.Any(r => r.Binding.Id == binding.Id && r.UI.BindingId == bindingId)) continue;
                var staging = new GameObject("SprocketKeybinds.Staging");
                staging.SetActive(false);
                GameObject? clone = null;
                try
                {
                    clone = Object.Instantiate(rowTransform.gameObject, staging.transform, false).Cast<GameObject>();
                    clone.name = "ModKeybind." + binding.Id + "." + index;
                    var ui = clone.GetComponentInChildren<RebindActionUI>(true);
                    if (ui == null) throw new InvalidOperationException("Cloned row has no rebind component.");
                    ui.ActionReference = binding.Reference;
                    ui.BindingId = bindingId;
                    ui.RebindOverlay = menu.rebindOverlay;
                    ui.RebindPrompt = menu.rebindMessageDisplay;
                    WireButtons(clone, ui);
                    var label = binding.Label + (index == 0 ? "" : " (secondary)");
                    Rows.Add(new(binding, clone, ui, label));
                    clone.transform.SetParent(ModPanel.Content, false);
                    clone.transform.SetAsLastSibling();
                    clone.SetActive(true);
                    ui.Initiate();
                    ui.UpdateActionLabel();
                    ui.UpdateBindingDisplay();
                }
                catch
                {
                    if (clone != null) { Rows.RemoveAll(r => r.Root.Pointer == clone.Pointer); Object.Destroy(clone); }
                    throw;
                }
                finally { Object.Destroy(staging); }
            }
        }
        UpdateLabels();
        ModPanel.Layout();
    }
    private static void WireButtons(GameObject clone, RebindActionUI ui)
    {
        // Cloning visible rows does not guarantee that serialized/runtime listeners target the new action.
        // Give each cloned control an explicit callback rather than retaining the template's events.
        int rebindControls = 0, resetControls = 0;
        foreach (var tab in clone.GetComponentsInChildren<Sprocket.UI.Tab>(true))
        {
            bool rebind = ui.BindingText.transform.IsChildOf(tab.transform);
            tab.Clicked = null;
            tab.onClick = new UnityEvent();
            tab.onClick.AddListener(DelegateSupport.ConvertDelegate<UnityAction>((Action)(() =>
                Plugin.Safe(rebind ? ui.StartInteractiveRebind : ui.ResetToDefault))));
            tab.enabled = true;
            tab.interactable = true;
            tab.SetState(Sprocket.Selection.TabState.Normal);
            if (rebind) rebindControls++; else resetControls++;
        }
        foreach (var button in clone.GetComponentsInChildren<Button>(true))
        {
            // Tab derives from Selectable; a separate Button may coexist on some UI versions.
            if (button.GetComponent<Sprocket.UI.Tab>() != null) continue;
            bool rebind = ui.BindingText.transform.IsChildOf(button.transform);
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(DelegateSupport.ConvertDelegate<UnityAction>((Action)(() =>
                Plugin.Safe(rebind ? ui.StartInteractiveRebind : ui.ResetToDefault))));
            button.enabled = true;
            button.interactable = true;
            if (rebind) rebindControls++; else resetControls++;
        }
        if (rebindControls != 1 || resetControls != 1)
            throw new InvalidOperationException($"Unexpected cloned row controls: rebind={rebindControls}, reset={resetControls}.");
        Plugin.LogInfo("Explicit native rebind/reset callbacks assigned to " + clone.name);
    }
    internal static void Label(RebindActionUI ui)
    {
        var row = Rows.FirstOrDefault(r => r.UI.Pointer == ui.Pointer);
        if (row != null && ui.ActionLabel != null) ui.ActionLabel.text = row.Label;
    }
    internal static void UpdateLabels()
    {
        foreach (var row in Rows)
            if (row.UI != null) { row.UI.UpdateBindingDisplay(); Label(row.UI); }
    }
    internal static void StopRebinding()
    {
        foreach (var row in Rows.ToArray())
            if (row.UI != null && row.UI.ongoingRebind != null) row.UI.ongoingRebind.Cancel();
        if (menu != null && menu.rebindOverlay != null) menu.rebindOverlay.SetActive(false);
    }
    internal static void Remove(ModKeybind binding)
    {
        foreach (var row in Rows.Where(r => ReferenceEquals(r.Binding, binding)).ToArray())
        {
            if (row.UI != null && row.UI.ongoingRebind != null) row.UI.ongoingRebind.Cancel();
            if (row.Root != null) { row.Root.SetActive(false); Object.Destroy(row.Root); }
            Rows.Remove(row);
        }
        ModPanel.Layout();
    }
    internal static void Cleanup()
    {
        StopRebinding();
        foreach (var row in Rows) if (row.Root != null) { row.Root.SetActive(false); Object.Destroy(row.Root); }
        Rows.Clear(); menu = null;
        ModPanel.Cleanup();
    }
}

[HarmonyPatch(typeof(SettingsMenu), nameof(SettingsMenu.Initiate))]
internal static class OpenPatch
{
    static void Prefix() => Plugin.Safe(() => { VanillaMenu.Cleanup(); Keybinds.Begin(); });
    static void Postfix(SettingsMenu __instance) => Plugin.Safe(() => VanillaMenu.Attach(__instance));
}
[HarmonyPatch(typeof(SettingsMenu), nameof(SettingsMenu.SetEditProfile))]
internal static class RebuildPatch
{
    static void Postfix(SettingsMenu __instance) => Plugin.Safe(() => VanillaMenu.Attach(__instance));
}
[HarmonyPatch(typeof(SettingsMenu), nameof(SettingsMenu.Apply))]
internal static class ApplyPatch
{
    static void Prefix() => Plugin.Safe(VanillaMenu.StopRebinding);
    static void Postfix() => Plugin.Safe(Keybinds.Commit);
}
[HarmonyPatch(typeof(SettingsMenu), nameof(SettingsMenu.Accept))]
internal static class AcceptPatch
{
    static void Prefix() => Plugin.Safe(VanillaMenu.StopRebinding);
    static void Postfix() => Plugin.Safe(() => { Keybinds.Commit(); Keybinds.End(); });
}
[HarmonyPatch(typeof(SettingsMenu), nameof(SettingsMenu.Cancel))]
internal static class CancelPatch
{
    static void Prefix() => Plugin.Safe(() => { VanillaMenu.StopRebinding(); Keybinds.End(); });
}
[HarmonyPatch(typeof(SettingsMenu), nameof(SettingsMenu.Cleanup))]
internal static class ClosePatch
{
    static void Postfix() => Plugin.Safe(() => { VanillaMenu.Cleanup(); Keybinds.End(); });
}
[HarmonyPatch(typeof(RebindActionUI), nameof(RebindActionUI.UpdateActionLabel))]
internal static class LabelPatch
{
    static void Postfix(RebindActionUI __instance) => Plugin.Safe(() => VanillaMenu.Label(__instance));
}





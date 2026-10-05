using BepInEx;
using BepInEx.Unity.IL2CPP;
using BepInEx.Logging;
using SprocketKeybinds;
using UnityEngine;

[BepInPlugin("example.sprocket.keybinddemo", "Keybind API Demo", "0.1.0")]
[BepInDependency(Keybinds.PluginGuid)]
public sealed class DemoPlugin : BasePlugin
{
    internal static ModKeybind Toggle = null!, Raise = null!, Lower = null!;
    internal static ManualLogSource Logger = null!;
    internal static bool Active;
    public override void Load()
    {
        Logger = Log;
        Toggle = Keybinds.RegisterButton("example.sprocket.keybinddemo", "Keybind API Demo", "toggle", "Toggle demo", "<Keyboard>/n");
        Raise = Keybinds.RegisterButton("example.sprocket.keybinddemo", "Keybind API Demo", "raise", "Raise demo", "<Keyboard>/insert");
        Lower = Keybinds.RegisterButton("example.sprocket.keybinddemo", "Keybind API Demo", "lower", "Lower demo", "<Keyboard>/delete");
        Active = true;
        AddComponent<DemoDriver>();
    }
    public override bool Unload()
    {
        Active = false;
        Keybinds.Unregister("example.sprocket.keybinddemo", "toggle");
        Keybinds.Unregister("example.sprocket.keybinddemo", "raise");
        Keybinds.Unregister("example.sprocket.keybinddemo", "lower");
        return true;
    }
}
public sealed class DemoDriver : MonoBehaviour
{
    public DemoDriver(IntPtr ptr) : base(ptr) { }
    public void Update()
    {
        if (!DemoPlugin.Active) return;
        // Replace these logs with your mod's actions, and keep your gameplay/context checks here.
        if (DemoPlugin.Toggle.WasPressedThisFrame) DemoPlugin.Logger.LogInfo("Demo toggled");
        if (DemoPlugin.Raise.WasPressedThisFrame) DemoPlugin.Logger.LogInfo("Demo raise pressed");
        if (DemoPlugin.Lower.WasPressedThisFrame) DemoPlugin.Logger.LogInfo("Demo lower pressed");
        // A moving part normally consumes Raise.IsHeld and Lower.IsHeld each frame.
    }
}


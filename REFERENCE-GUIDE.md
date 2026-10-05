# Sprocket Keybinds 0.1.5

Shared configurable button bindings for Sprocket addons inside the vanilla Settings keybind menu. Open **Mod keybinds / Open**.

The panel has two collapsible levels: **author → mod → actions**. Existing HPS, Mast and Thermal appear beneath **Nero** without rebuilding those addons. Switch **By author / By pack** for explicitly declared pack membership. Standalone mods do not automatically belong to any pack. Other mods fall back to their author, or **Unknown** if none is declared. Scroll with the mouse wheel. Duplicate display names retain separate owners, with an owner-ID detail when needed. Long labels use ellipsis.

**Clear** removes one binding slot; the reset icon restores its registered factory default. **Apply** saves and establishes a new baseline. **Accept** saves and closes. **Cancel** restores the last applied settings. Panel Back, collapse and grouping switches do not save or cancel edits. Primary and optional secondary slots are independent.

## Compatibility and installation

Built against Sprocket 0.2.55.5 / Unity 6000.3.21f1, BepInEx 6 IL2CPP with local generated interop, .NET 6; SDK 8 builds the source. Other game/loader versions need verification. Use one shared BepInEx/plugins/SprocketKeybinds/SprocketKeybinds.dll; consumers reference it with Private=false and must not ship private runtime copies.

Existing compiled consumers using >=0.1.3 <0.2.0 remain compatible. New optional metadata callers require **>=0.1.5 <0.2.0**. Existing exported signatures and input identities are retained. The API is still 0.x, not a stable 1.0 promise. A bare dependency string such as 0.1.3 means an exact version in this loader.

Close the game before replacing the API DLL and back up the DLL and configuration. Preserve BepInEx/config/sprocket.keybinds.json on updates. Uninstalling the API prevents hard-dependent addons from loading. The runtime does not download updates or install addons.

## Storage and recovery

The JSON stores overrides under stable owner/action identities, including explicitly unbound choices. Atomic writes retain the previous file as .bak. Settings for absent mods remain available when they return. Display author/mod/pack changes do not rename storage keys. Metadata is supplied at runtime by mods or a pack integration; no metadata file is introduced.

Legacy CFG import is optional and happens once, only when no API record exists. Register factory defaults before importing: Reset must restore the factory key, not an old personal key. Import cannot run during an edit transaction. Invalid/future-schema JSON fails initialization without overwriting the file. Close the game, preserve a copy, inspect the error and restore a known-good compatible backup; do not discard all owners' settings to repair one binding.

See [API integration](INTEGRATION.md). Concrete keyboard and mouse **buttons** are supported; axes, composites and modifier chords are outside this version. Conflict reports cover exact overlaps between mod actions, not all vanilla bindings. Polling is non-consuming; gameplay contexts remain the consumer's responsibility. Attribution and pack membership are declarations, not authenticated authorship.

Build and 42 managed checks pass. Existing public signatures and installed consumer references are checked separately. Native hierarchy layout, scrolling, Clear/Reset, switching during edits, Apply/Cancel, restart, map transitions and press/hold gameplay still require live confirmation for this build.

[Support development](https://www.paypal.com/donate/?hosted_button_id=7PE3SDBETXFQ6)

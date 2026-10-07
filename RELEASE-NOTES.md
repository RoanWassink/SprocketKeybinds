<!-- sp-compat {"hamish.sprocket": "0.2.55.5", "bepinex.bepinex": "6.0.0-be.788"} -->

Compatible addons can now migrate saved controls when their addon identity changes, preserving primary and secondary bindings. Existing choices remain authoritative, including unbound keys; previous records remain available for rollback.

The keybind menu and its controls are unchanged. Existing compatible addons continue to work. Thermal Sight 0.2.6 requires this update.

Close Sprocket and replace the shared API DLL. Preserve `BepInEx/config/sprocket.keybinds.json` and its backup. Requires Sprocket 0.2.55.5 and BepInEx 6 IL2CPP 6.0.0-be.788.

[Support my ChatGPT budget and help me reverse engineer Sprocket to make more mods.](https://www.paypal.com/donate/?hosted_button_id=7PE3SDBETXFQ6)
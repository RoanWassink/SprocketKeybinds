using Il2CppInterop.Runtime;
using Sprocket.SettingConfiguration;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SprocketKeybinds;

// Owns presentation only: existing mod registrations, IDs and save data are unchanged.
internal static class ModPanel
{
    private static GameObject? panel, launcher;
    private static RectTransform? content;
    private static TMP_FontAsset? font;
    private static bool byPack;
    private static readonly HashSet<string> expandedParents = new(StringComparer.Ordinal);
    private static readonly HashSet<string> expandedMods = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, GameObject> parentHeaders = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, GameObject> headers = new(StringComparer.Ordinal);
    private static readonly Dictionary<IntPtr, GameObject> clearButtons = new();
    internal static Transform Content => content!;
    private static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name);
        var rect = go.AddComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.localScale = Vector3.one;
        return rect;
    }
    private static void Fill(RectTransform rect, float left = 0, float bottom = 0, float right = 0, float top = 0)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom); rect.offsetMax = new Vector2(-right, -top);
    }
    private static TMP_Text Text(Transform parent, string value, float size = 20)
    {
        var rect = Rect("Label", parent); Fill(rect, 10, 0, 10, 0);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font; text.fontSize = size; text.text = value;
        text.color = new Color(1f, .93f, .8f, 1f);
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.enableWordWrapping = false; text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false; text.richText = false;
        return text;
    }
    private static Button Button(RectTransform rect, string label, Action click)
    {
        var image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(.17f, .16f, .14f, 1f);
        var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(DelegateSupport.ConvertDelegate<UnityAction>((Action)(() => Plugin.Safe(click))));
        Text(rect, label);
        return button;
    }
    internal static void Ensure(SettingsMenu menu, GameObject templateRoot, RebindActionUI template)
    {
        if (panel != null) return;
        font = template.ActionLabel.font;
        var panelRect = Rect("ModKeybinds.Panel", menu.keymappingGUI.transform);
        panelRect.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        Fill(panelRect, 18, 18, 18, 18);
        panel = panelRect.gameObject;
        var background = panel.AddComponent<Image>(); background.color = new Color(.06f, .055f, .045f, .98f);
        var title = Rect("Title", panelRect); title.anchorMin = new Vector2(0, 1); title.anchorMax = Vector2.one;
        title.pivot = new Vector2(.5f, 1); title.sizeDelta = new Vector2(-130, 38); title.anchoredPosition = new Vector2(-55, -10);
        Text(title, "Mod keybinds", 25);
        var mode = Rect("Grouping", panelRect); mode.anchorMin = mode.anchorMax = new Vector2(.65f, 1);
        mode.pivot = new Vector2(.5f, 1); mode.sizeDelta = new Vector2(180, 36); mode.anchoredPosition = new Vector2(0, -10);
        var modeButton = Button(mode, "By author", () =>
        {
            VanillaMenu.StopRebinding(); byPack = !byPack; Layout();
            mode.gameObject.GetComponentInChildren<TMP_Text>().text = byPack ? "By pack" : "By author";
        });
        var help = Rect("Help", panelRect); help.anchorMin = new Vector2(0, 1); help.anchorMax = Vector2.one;
        help.pivot = new Vector2(.5f, 1); help.sizeDelta = new Vector2(-20, 32); help.anchoredPosition = new Vector2(0, -48);
        Text(help, "Expand a mod. Clear removes a key; reset restores its default. Use Apply / Accept to save.", 17);
        var close = Rect("Close", panelRect); close.anchorMin = close.anchorMax = Vector2.one;
        close.pivot = Vector2.one; close.sizeDelta = new Vector2(100, 36); close.anchoredPosition = new Vector2(-12, -10);
        Button(close, "Back", () => { VanillaMenu.StopRebinding(); panel?.SetActive(false); });
        var scrollRect = Rect("Scroll", panelRect); Fill(scrollRect, 12, 12, 12, 88);
        var scroll = scrollRect.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false; scroll.vertical = true; scroll.scrollSensitivity = 35;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        var viewport = Rect("Viewport", scrollRect); Fill(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();
        var viewportImage = viewport.gameObject.AddComponent<Image>(); viewportImage.color = new Color(0, 0, 0, .01f);
        content = Rect("Content", viewport); content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
        content.pivot = new Vector2(.5f, 1); content.anchoredPosition = Vector2.zero;
        scroll.viewport = viewport; scroll.content = content;
        panel.SetActive(false);

        var staging = new GameObject("ModKeybinds.LauncherStaging"); staging.SetActive(false);
        try
        {
            launcher = Object.Instantiate(templateRoot, staging.transform, false).Cast<GameObject>();
            launcher.name = "ModKeybinds.Open";
            var ui = launcher.GetComponentInChildren<RebindActionUI>(true);
            ui.enabled = false;
            ui.ActionLabel.text = "Mod keybinds";
            ui.BindingText.text = "Open";
            foreach (var tab in launcher.GetComponentsInChildren<Sprocket.UI.Tab>(true))
            {
                bool opens = ui.BindingText.transform.IsChildOf(tab.transform);
                if (!opens) { tab.gameObject.SetActive(false); continue; }
                tab.Clicked = null; tab.onClick = new UnityEvent();
                tab.onClick.AddListener(DelegateSupport.ConvertDelegate<UnityAction>((Action)Open));
                tab.enabled = true; tab.interactable = true; tab.SetState(Sprocket.Selection.TabState.Normal);
            }
            foreach (var button in launcher.GetComponentsInChildren<UnityEngine.UI.Button>(true))
            {
                if (button.GetComponent<Sprocket.UI.Tab>() != null) continue;
                if (!ui.BindingText.transform.IsChildOf(button.transform)) { button.gameObject.SetActive(false); continue; }
                button.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
                button.onClick.AddListener(DelegateSupport.ConvertDelegate<UnityAction>((Action)Open));
                button.interactable = true;
            }
            Object.Destroy(ui);
            launcher.transform.SetParent(templateRoot.transform.parent, false);
            launcher.transform.SetAsLastSibling(); launcher.SetActive(true);
        }
        finally { Object.Destroy(staging); }
    }
    private static void Open() { if (panel == null) return; Layout(); panel.transform.SetAsLastSibling(); panel.SetActive(true); }
    private static void Place(RectTransform rect, float y, float height)
    {
        rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, 1);
        rect.sizeDelta = new Vector2(0, height); rect.anchoredPosition = new Vector2(0, -y);
    }
    internal static void Layout()
    {
        if (content == null) return;
        foreach (var item in clearButtons.Where(p => !VanillaMenu.Rows.Any(r => r.Root.Pointer == p.Key)).ToArray())
        {
            if (item.Value != null) Object.Destroy(item.Value);
            clearButtons.Remove(item.Key);
        }
        foreach (var item in headers.Where(p => !VanillaMenu.Rows.Any(r => r.Binding.Id.Split('/')[0] == p.Key)).ToArray())
        {
            if (item.Value != null) Object.Destroy(item.Value);
            headers.Remove(item.Key);
        }
        float y = 0;
        var rowGroups = VanillaMenu.Rows.GroupBy(r => r.Binding.Id.Split('/')[0]).ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.Ordinal);
        var presentation = Keybinds.Metadata.Order(rowGroups.Select(g => (g.Key, g.Value[0].Binding.ModName)), byPack);
        foreach (var item in parentHeaders.Where(h => !presentation.Any(p => p.GroupKey == h.Key)).ToArray())
        {
            if (item.Value != null) Object.Destroy(item.Value); parentHeaders.Remove(item.Key);
        }
        foreach (var parent in presentation.GroupBy(p => p.GroupKey))
        {
            var parentKey = parent.Key;
            if (!parentHeaders.TryGetValue(parentKey, out var parentHeader) || parentHeader == null)
            {
                var parentRect = Rect("Parent." + parentKey, content); parentHeader = parentRect.gameObject;
                parentHeaders[parentKey] = parentHeader;
                Button(parentRect, "", () => { VanillaMenu.StopRebinding(); if (!expandedParents.Remove(parentKey)) expandedParents.Add(parentKey); Layout(); });
            }
            bool parentOpen = expandedParents.Contains(parentKey);
            parentHeader.GetComponentInChildren<TMP_Text>().text = (parentOpen ? "[-] " : "[+] ") + parent.First().GroupName + " (" + parent.Count() + " mods)";
            Place(parentHeader.GetComponent<RectTransform>(), y, 38); y += 44;
        foreach (var info in parent)
        {
            var owner = info.Owner;
            var group = rowGroups[owner];
            if (!headers.TryGetValue(owner, out var header) || header == null)
            {
                var rect = Rect("Group." + owner, content); header = rect.gameObject; headers[owner] = header;
                Button(rect, "", () => { VanillaMenu.StopRebinding(); if (!expandedMods.Remove(owner)) expandedMods.Add(owner); Layout(); });
                var detail = Text(rect, owner, 12); detail.gameObject.name = "OwnerDetail";
                Fill(detail.GetComponent<RectTransform>(), 30, 0, 10, 30);
            }
            bool show = parentOpen && expandedMods.Contains(owner);
            bool duplicate = parent.Count(p => p.Mod == info.Mod) > 1;
            var labels = header.GetComponentsInChildren<TMP_Text>(true);
            var label = labels.First(t => t.gameObject.name != "OwnerDetail");
            var detailLabel = labels.First(t => t.gameObject.name == "OwnerDetail");
            detailLabel.gameObject.SetActive(duplicate);
            Fill(label.GetComponent<RectTransform>(), 28, duplicate ? 20 : 0, 10, 0);
            label.text = (expandedMods.Contains(owner) ? "[-] " : "[+] ") + (info.Pack ? info.Mod.Author + "/" : "") + info.Mod.Name + " (" + group.Length + ")";
            header.SetActive(parentOpen);
            if (parentOpen) { Place(header.GetComponent<RectTransform>(), y, duplicate ? 54 : 38); y += duplicate ? 60 : 44; }
            foreach (var row in group)
            {
                var rect = row.Root.GetComponent<RectTransform>();
                row.Root.SetActive(show);
                if (!clearButtons.TryGetValue(row.Root.Pointer, out var clear) || clear == null)
                {
                    var clearRect = Rect("Clear." + row.Binding.Id, content); clear = clearRect.gameObject;
                    clearButtons[row.Root.Pointer] = clear;
                    int slot = Enumerable.Range(0, row.Binding.Action.bindings.Count).First(i => row.Binding.Action.bindings[i].id.ToString() == row.UI.BindingId);
                    Button(clearRect, "Clear", () => { VanillaMenu.StopRebinding(); row.Binding.Unbind(slot); });
                }
                clear.SetActive(show);
                if (!show) continue;
                Place(rect, y, 44); rect.sizeDelta = new Vector2(-86, 44); rect.anchoredPosition = new Vector2(-43, -y);
                var clearPosition = clear.GetComponent<RectTransform>();
                clearPosition.anchorMin = clearPosition.anchorMax = Vector2.one; clearPosition.pivot = Vector2.one;
                clearPosition.sizeDelta = new Vector2(80, 40); clearPosition.anchoredPosition = new Vector2(0, -y);
                y += 50;
            }
        }
        }
        content.sizeDelta = new Vector2(0, Math.Max(y + 8, 1));
    }
    internal static void Cleanup()
    {
        if (panel != null) Object.Destroy(panel);
        if (launcher != null) Object.Destroy(launcher);
        panel = null; launcher = null; content = null; font = null;
        headers.Clear(); parentHeaders.Clear(); clearButtons.Clear();
    }
}

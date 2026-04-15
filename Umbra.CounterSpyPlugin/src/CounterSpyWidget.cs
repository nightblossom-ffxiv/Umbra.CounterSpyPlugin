using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using Umbra.Common;
using Umbra.Game;
using Umbra.Widgets;

namespace Umbra.CounterSpyPlugin;

[ToolbarWidget(
    "Umbra_CounterSpyWidget",
    "Counter Spy Widget",
    "Shows a list of players and NPCs that are targeting you."
)]
public class CounterSpyWidget(
    WidgetInfo                  info,
    string?                     guid         = null,
    Dictionary<string, object>? configValues = null
) : StandardToolbarWidget(info, guid, configValues)
{
    public override MenuPopup Popup { get; } = new();

    protected override StandardWidgetFeatures Features =>
        StandardWidgetFeatures.Text |
        StandardWidgetFeatures.Icon;

    private readonly Dictionary<string, Dictionary<string, MenuPopup.Button>> _menuItems      = [];
    private readonly Dictionary<string, MenuPopup.Button>                     _historyButtons = [];
    private readonly MenuPopup.Group                                          _playerGroup    = new("Players");
    private readonly MenuPopup.Group                                          _npcGroup       = new("NPCs");
    private readonly MenuPopup.Group                                          _recentGroup    = new("Recent");

    private CounterSpyRepository   Repository    { get; } = Framework.Service<CounterSpyRepository>();
    private CounterSpyHistoryStore History       { get; } = Framework.Service<CounterSpyHistoryStore>();
    private IPlayer                Player        { get; } = Framework.Service<IPlayer>();
    private ITargetManager         TargetManager { get; } = Framework.Service<ITargetManager>();
    private IObjectTable           ObjectTable   { get; } = Framework.Service<IObjectTable>();

    protected override void OnLoad()
    {
        Popup.Add(_playerGroup);
        Popup.Add(_npcGroup);
        Popup.Add(_recentGroup);

        _menuItems["Players"] = [];
        _menuItems["NPCs"]    = [];
    }

    protected override void OnDraw()
    {
        var showPlayers = GetConfigValue<bool>("ShowPlayers");
        var showNpcs    = GetConfigValue<bool>("ShowNPCs");
        var playerList  = Repository.GetTargets(showPlayers, false);
        var npcList     = Repository.GetTargets(false, showNpcs);
        var isEmpty     = playerList.Count == 0 && npcList.Count == 0;

        var iconId = npcList.Count > 0 && playerList.Count == 0
            ? (uint)GetConfigValue<int>("NPCIconId")
            : (uint)GetConfigValue<int>("PlayerIconId");

        SetGameIconId(iconId);

        IsVisible = !(isEmpty && GetConfigValue<bool>("HideIfEmpty"));
        if (!IsVisible) return;

        var showPlayersCfg = GetConfigValue<bool>("ShowPlayers");
        var showNpcsCfg    = GetConfigValue<bool>("ShowNPCs");

        var playersLabel = showPlayersCfg ? $"Players: {playerList.Count}" : "";
        var npcLabel     = showNpcsCfg    ? $"NPCs: {npcList.Count}"       : "";
        SetText($"{playersLabel} {npcLabel}".Trim());

        UpdateMenuItems(playerList, _playerGroup);
        UpdateMenuItems(npcList, _npcGroup);
        UpdateHistoryItems();
    }

    private void UpdateHistoryItems()
    {
        var entries = History.GetAll();
        var usedIds = new List<string>();

        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            var id    = $"hist_{entry.Name}@{entry.World}";
            usedIds.Add(id);

            var label = string.IsNullOrEmpty(entry.World)
                ? entry.Name
                : $"{entry.Name} @ {entry.World}";
            var age = FormatAge(DateTime.UtcNow - entry.LastSeenUtc);

            var entryRef = entry;
            if (!_historyButtons.TryGetValue(id, out var button))
            {
                button = new MenuPopup.Button(label)
                {
                    AltText   = age,
                    SortIndex = i,
                    OnClick   = () => TargetByHistoryEntry(entryRef),
                };
                _historyButtons[id] = button;
                _recentGroup.Add(button);
            }
            else
            {
                button.AltText   = age;
                button.SortIndex = i;
            }
        }

        foreach (var (id, btn) in _historyButtons.ToDictionary())
        {
            if (!usedIds.Contains(id))
            {
                _recentGroup.Remove(btn);
                _historyButtons.Remove(id);
            }
        }
    }

    private void TargetByHistoryEntry(TargetHistoryEntry entry)
    {
        foreach (var obj in ObjectTable)
        {
            if (obj is IPlayerCharacter p
                && p.Name.TextValue == entry.Name)
            {
                if (!string.IsNullOrEmpty(entry.World))
                {
                    string worldName;
                    try { worldName = p.HomeWorld.Value.Name.ToString(); }
                    catch { continue; }
                    if (worldName != entry.World) continue;
                }

                TargetManager.Target = obj;
                return;
            }
        }
    }

    private static string FormatAge(TimeSpan t)
    {
        if (t.TotalSeconds < 60) return $"{(int)t.TotalSeconds}s ago";
        if (t.TotalMinutes < 60) return $"{(int)t.TotalMinutes}m ago";
        if (t.TotalHours   < 24) return $"{(int)t.TotalHours}h ago";
        return $"{(int)t.TotalDays}d ago";
    }

    private void UpdateMenuItems(List<IGameObject> list, MenuPopup.Group group)
    {
        if (!_menuItems.ContainsKey(group.Label!)) _menuItems[group.Label!] = [];

        List<string> usedIds = [];

        foreach (var obj in list)
        {
            var   id   = $"obj_{obj.GameObjectId}";
            float d    = Vector3.Distance(Player.Position, obj.Position);
            var   dist = $"{d:N0} yalms";

            usedIds.Add(id);

            if (!_menuItems[group.Label!].ContainsKey(id))
            {
                _menuItems[group.Label!][id] = new MenuPopup.Button(obj.Name.TextValue)
                {
                    IsDisabled = d > 50,
                    Icon       = obj is IPlayerCharacter p ? p.ClassJob.RowId + 62000 : null,
                    AltText    = dist,
                    SortIndex  = obj.ObjectIndex,
                    OnClick    = () => TargetManager.Target = obj,
                };
            }

            var button = _menuItems[group.Label!][id];
            group.Add(button);
        }

        foreach (var (id, btn) in _menuItems[group.Label!].ToDictionary())
        {
            if (!usedIds.Contains(id))
            {
                group.Remove(btn);
                _menuItems[group.Label!].Remove(id);
            }
        }
    }

    protected override IEnumerable<IWidgetConfigVariable> GetConfigVariables()
    {
        return
        [
            ..base.GetConfigVariables(),

            new BooleanWidgetConfigVariable(
                "HideIfEmpty",
                "Hide the widget if nothing targets you.",
                "Hide the widget if there are no players or NPCs are currently targeting you.",
                false
            ),
            new BooleanWidgetConfigVariable(
                "ShowPlayers",
                "Show players",
                "Include player characters in the list of entities currently targeting you.",
                true
            ),
            new BooleanWidgetConfigVariable(
                "ShowNPCs",
                "Show NPCs",
                "Include non-player characters in the list of entities currently targeting you.",
                false
            ),
            new IntegerWidgetConfigVariable(
                "PlayerIconId",
                "Icon ID for players targeting you",
                "The icon ID to use for the world marker. Use value 0 to disable the icon. Type \"/xldata icons\" in the chat to access the icon browser.",
                60407,
                0
            ),
            new IntegerWidgetConfigVariable(
                "NPCIconId",
                "Icon ID for NPCs targeting you",
                "The icon ID to use for the world marker. Use value 0 to disable the icon. Type \"/xldata icons\" in the chat to access the icon browser.",
                61510,
                0
            ),
        ];
    }
}
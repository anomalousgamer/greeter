using System;
using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Plugin.Services;
using Greeter.Models;

namespace Greeter.Services;

public sealed class ContextMenuService : IDisposable
{
    private readonly IContextMenu contextMenu;
    private readonly IChatGui chatGui;
    private readonly RuleManager rules;
    private readonly GreetingEngine engine;

    public ContextMenuService(
        IContextMenu contextMenu,
        IChatGui chatGui,
        RuleManager rules,
        GreetingEngine engine)
    {
        this.contextMenu = contextMenu;
        this.chatGui = chatGui;
        this.rules = rules;
        this.engine = engine;
        contextMenu.OnMenuOpened += OnMenuOpened;
    }

    public void Dispose() => contextMenu.OnMenuOpened -= OnMenuOpened;

    private void OnMenuOpened(IMenuOpenedArgs args)
    {
        if (args.Target is not MenuTargetDefault target
            || !target.TargetHomeWorld.IsValid)
        {
            return;
        }

        var player = new PlayerKey(target.TargetName, target.TargetHomeWorld.Value.Name.ToString());
        if (!player.IsUsable)
        {
            return;
        }

        args.AddMenuItem(new MenuItem
        {
            Name = "Greeter",
            PrefixChar = 'G',
            IsSubmenu = true,
            OnClicked = clicked => clicked.OpenSubmenu(BuildSubmenu(player)),
        });
    }

    private IMenuItem[] BuildSubmenu(PlayerKey player)
    {
        var rule = rules.GetRule(player);
        return
        [
            new MenuItem
            {
                Name = rule == GreetingRule.Always ? "Remove Always Greet" : "Add to Always Greet",
                OnClicked = _ => ToggleAlways(player),
            },
            new MenuItem
            {
                Name = rule == GreetingRule.Never ? "Remove Never Greet" : "Add to Never Greet",
                OnClicked = _ => ToggleNever(player),
            },
            new MenuItem
            {
                Name = engine.WasGreeted(player) ? "Clear Greeted This Session" : "Mark Greeted This Session",
                OnClicked = _ => ToggleSessionGreeting(player),
            },
        ];
    }

    private void ToggleAlways(PlayerKey player)
    {
        if (rules.GetRule(player) == GreetingRule.Always)
        {
            rules.RemoveAlways(player);
            Print($"Removed {player.DisplayName} from Always Greet.");
        }
        else
        {
            rules.AddAlways(player);
            Print($"Added {player.DisplayName} to Always Greet.");
        }
    }

    private void ToggleNever(PlayerKey player)
    {
        if (rules.GetRule(player) == GreetingRule.Never)
        {
            rules.RemoveNever(player);
            Print($"Removed {player.DisplayName} from Never Greet.");
        }
        else
        {
            rules.AddNever(player);
            Print($"Added {player.DisplayName} to Never Greet. This overrides Always Greet.");
        }
    }

    private void ToggleSessionGreeting(PlayerKey player)
    {
        if (engine.WasGreeted(player))
        {
            engine.ClearGreeted(player);
            Print($"Cleared the session greeting for {player.DisplayName}.");
        }
        else
        {
            engine.MarkGreeted(player);
            Print($"Marked {player.DisplayName} as greeted for this session.");
        }
    }

    private void Print(string message) => chatGui.Print(message, "Greeter");
}

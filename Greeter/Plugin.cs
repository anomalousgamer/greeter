using System;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Greeter.Models;
using Greeter.Services;
using Greeter.Windows;

namespace Greeter;

public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/greeter";

    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static IContextMenu ContextMenu { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private readonly HousingService housing;
    private readonly RuleManager rules;
    private readonly ChatSender chatSender;
    private readonly GreetingEngine engine;
    private readonly ContextMenuService contextMenuService;
    private readonly MainWindow mainWindow;

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Configuration.Initialize(PluginInterface);

        housing = new HousingService();
        rules = new RuleManager(Configuration);
        chatSender = new ChatSender(Log);
        engine = new GreetingEngine(
            Configuration,
            housing,
            rules,
            chatSender,
            Framework,
            ClientState,
            PlayerState,
            ObjectTable,
            ChatGui,
            Log);
        contextMenuService = new ContextMenuService(ContextMenu, ChatGui, rules, engine);

        mainWindow = new MainWindow(this, Configuration, housing, rules, engine);
        WindowSystem.AddWindow(mainWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage =
                "Opens Greeter settings.\n" +
                "/greeter on|off — Enables or disables automatic greeting.\n" +
                "/greeter status — Shows the current status.\n" +
                "/greeter setvenue — Uses the house you are currently inside.\n" +
                "/greeter unsetvenue — Clears the configured house.\n" +
                "/greeter test — Sends one test greeting while inside the configured venue.\n" +
                "/greeter clear — Clears greeted-this-session records.\n" +
                "/greeter debug on|off — Toggles diagnostic chat messages.",
        });

        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += OpenUi;
        PluginInterface.UiBuilder.OpenMainUi += OpenUi;
        Log.Information("Greeter 1.0.0.0 loaded.");
    }

    public Configuration Configuration { get; }
    public WindowSystem WindowSystem { get; } = new("Greeter");

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= OpenUi;
        PluginInterface.UiBuilder.OpenMainUi -= OpenUi;
        CommandManager.RemoveHandler(CommandName);
        WindowSystem.RemoveAllWindows();
        mainWindow.Dispose();
        contextMenuService.Dispose();
        engine.Dispose();
    }

    public bool SetCurrentHouseAsVenue(out string message)
    {
        if (!housing.TryGetCurrentIndoorHouse(out var house))
        {
            message = "You must be inside the venue house before setting it.";
            return false;
        }

        Configuration.ConfiguredHouseId = house.HouseId;
        Configuration.ConfiguredHouseLabel = house.Label;
        Configuration.Save();
        engine.ResetVenueTracking("The configured venue changed; arrival tracking was reset.");
        message = $"Configured this venue: {house.Label}.";
        return true;
    }

    public void ClearConfiguredVenue()
    {
        Configuration.ConfiguredHouseId = 0;
        Configuration.ConfiguredHouseLabel = string.Empty;
        Configuration.Save();
        engine.ResetVenueTracking("The configured venue was cleared.");
    }

    private void OpenUi() => mainWindow.IsOpen = true;

    private void OnCommand(string _, string arguments)
    {
        var args = arguments.Trim();
        if (string.IsNullOrEmpty(args))
        {
            OpenUi();
            return;
        }

        if (args.Equals("on", StringComparison.OrdinalIgnoreCase))
        {
            Configuration.AutomaticGreetingEnabled = true;
            Configuration.Save();
            Print("Automatic greeting enabled.");
            return;
        }

        if (args.Equals("off", StringComparison.OrdinalIgnoreCase))
        {
            Configuration.AutomaticGreetingEnabled = false;
            Configuration.Save();
            engine.ResetVenueTracking("Automatic greeting was disabled.");
            Print("Automatic greeting disabled.");
            return;
        }

        if (args.Equals("status", StringComparison.OrdinalIgnoreCase))
        {
            Print($"{engine.StatusText}. Present: {engine.PresentCount}; queued: {engine.QueueCount}; greeted this session: {engine.SessionGreetedCount}.");
            return;
        }

        if (args.Equals("setvenue", StringComparison.OrdinalIgnoreCase))
        {
            SetCurrentHouseAsVenue(out var message);
            Print(message);
            return;
        }

        if (args.Equals("unsetvenue", StringComparison.OrdinalIgnoreCase))
        {
            ClearConfiguredVenue();
            Print("The configured venue was cleared.");
            return;
        }

        if (args.Equals("clear", StringComparison.OrdinalIgnoreCase))
        {
            engine.ClearSessionGreeted();
            Print("Cleared greeted-this-session records.");
            return;
        }

        if (args.Equals("test", StringComparison.OrdinalIgnoreCase))
        {
            var testPlayer = new PlayerKey("Test Guest", "Test World");
            Print(engine.TrySendTest(testPlayer, out var error)
                ? "Test greeting sent."
                : error);
            return;
        }

        if (args.Equals("debug on", StringComparison.OrdinalIgnoreCase)
            || args.Equals("debug off", StringComparison.OrdinalIgnoreCase))
        {
            Configuration.DebugChatEnabled = args.EndsWith("on", StringComparison.OrdinalIgnoreCase);
            Configuration.Save();
            Print($"Diagnostic chat messages {(Configuration.DebugChatEnabled ? "enabled" : "disabled")}.");
            return;
        }

        PrintCommandHelp();
    }

    private static void PrintCommandHelp()
    {
        Print("Commands: /greeter, on, off, status, setvenue, unsetvenue, test, clear, debug on, debug off, help.");
    }

    private static void Print(string message) => ChatGui.Print(message, "Greeter");
}

using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Greeter.Models;
using Greeter.Services;

namespace Greeter.Windows;

public sealed class MainWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private readonly Configuration configuration;
    private readonly HousingService housing;
    private readonly RuleManager rules;
    private readonly WorldListService worldList;
    private readonly GreetingEngine engine;

    private string manualName = string.Empty;
    private string manualWorld = string.Empty;
    private string previewText = string.Empty;

    public MainWindow(
        Plugin plugin,
        Configuration configuration,
        HousingService housing,
        RuleManager rules,
        WorldListService worldList,
        GreetingEngine engine)
        : base("Greeter###GreeterMainWindow")
    {
        this.plugin = plugin;
        this.configuration = configuration;
        this.housing = housing;
        this.rules = rules;
        this.worldList = worldList;
        this.engine = engine;

        Size = new Vector2(760, 700);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(640, 520),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
    }

    public void Dispose()
    {
    }

    public override void Draw()
    {
        DrawStatus();
        ImGui.Separator();

        if (ImGui.BeginTabBar("GreeterTabs"))
        {
            if (ImGui.BeginTabItem("Venue & Automation"))
            {
                DrawVenueAndAutomation();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Greetings"))
            {
                DrawTemplates();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Guest Rules"))
            {
                DrawRules();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Activity"))
            {
                DrawActivity();
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }
    }

    private void DrawStatus()
    {
        var color = engine.IsInConfiguredVenue
            ? new Vector4(0.35f, 0.9f, 0.45f, 1f)
            : new Vector4(0.95f, 0.75f, 0.3f, 1f);
        ImGui.TextColored(color, engine.StatusText);
        ImGui.TextDisabled(
            $"Present: {engine.PresentCount}   Queued: {engine.QueueCount}   Greeted this session: {engine.SessionGreetedCount}");
    }

    private void DrawVenueAndAutomation()
    {
        ImGui.Spacing();
        ImGui.Text("Venue house");
        ImGui.TextWrapped(configuration.ConfiguredHouseId == 0
            ? "No house configured. Enter the venue interior and click Set Current House."
            : configuration.ConfiguredHouseLabel);

        if (housing.TryGetCurrentIndoorHouse(out var currentHouse))
        {
            ImGui.TextDisabled($"Current interior: {currentHouse.Label}");
        }
        else
        {
            ImGui.TextDisabled("Current interior: none");
        }

        if (ImGui.Button("Set Current House as Venue"))
        {
            plugin.SetCurrentHouseAsVenue(out previewText);
        }

        ImGui.SameLine();
        if (ImGui.Button("Clear Venue"))
        {
            plugin.ClearConfiguredVenue();
            previewText = "The configured venue was cleared.";
        }

        if (!string.IsNullOrWhiteSpace(previewText))
        {
            ImGui.TextWrapped(previewText);
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        var venueName = configuration.VenueName;
        if (ImGui.InputText("Venue name", ref venueName, 100))
        {
            configuration.VenueName = venueName;
            configuration.Save();
        }

        ImGui.TextDisabled("Used by the {venue} greeting variable.");

        var automatic = configuration.AutomaticGreetingEnabled;
        if (ImGui.Checkbox("Enable automatic greetings", ref automatic))
        {
            configuration.AutomaticGreetingEnabled = automatic;
            configuration.Save();
            if (!automatic)
            {
                engine.ResetVenueTracking("Automatic greeting was disabled.");
            }
        }

        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.65f, 0.25f, 1f));
        ImGui.TextWrapped(
            "Remember, if if she doesn't say it enough - Ima loves you ♥");
        ImGui.PopStyleColor();

        var greetExisting = configuration.GreetPlayersPresentOnActivation;
        if (ImGui.Checkbox("Greet guests already present when monitoring starts", ref greetExisting))
        {
            configuration.GreetPlayersPresentOnActivation = greetExisting;
            configuration.Save();
        }

        var debug = configuration.DebugChatEnabled;
        if (ImGui.Checkbox("Show diagnostic messages in your chat", ref debug))
        {
            configuration.DebugChatEnabled = debug;
            configuration.Save();
        }

        ImGui.Spacing();
        ImGui.Text("Chat channel");
        DrawChannelPicker();

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        ImGui.Text("Arrival safeguards");

        DrawIntSetting("Initial room scan (seconds)", configuration.InitialScanSeconds, 3, 30,
            value => configuration.InitialScanSeconds = value);
        DrawIntSetting("Arrival confirmation (seconds)", configuration.ArrivalConfirmationSeconds, 1, 15,
            value => configuration.ArrivalConfirmationSeconds = value);
        DrawIntSetting("Departure confirmation (seconds)", configuration.AbsenceConfirmationSeconds, 5, 60,
            value => configuration.AbsenceConfirmationSeconds = value);
        DrawIntSetting("Minimum greeting delay (seconds)", configuration.MinimumGreetingDelaySeconds, 3, 30,
            value => configuration.MinimumGreetingDelaySeconds = value);
        DrawIntSetting("Maximum greeting delay (seconds)", configuration.MaximumGreetingDelaySeconds,
            configuration.MinimumGreetingDelaySeconds, 60,
            value => configuration.MaximumGreetingDelaySeconds = value);
        DrawIntSetting("Discard queued greeting after (seconds)", configuration.MaximumQueueAgeSeconds, 30, 300,
            value => configuration.MaximumQueueAgeSeconds = value);
    }

    private void DrawChannelPicker()
    {
        var label = configuration.GreetingChannel.ToString();
        if (!ImGui.BeginCombo("##GreetingChannel", label))
        {
            return;
        }

        foreach (var channel in Enum.GetValues<GreetingChannel>())
        {
            if (ImGui.Selectable(channel.ToString(), channel == configuration.GreetingChannel))
            {
                configuration.GreetingChannel = channel;
                configuration.Save();
            }
        }

        ImGui.EndCombo();
    }

    private void DrawTemplates()
    {
        ImGui.Spacing();
        ImGui.TextWrapped("Available variables: {first}, {last}, {fullname}, {world}, and {venue}.");
        ImGui.TextDisabled("One enabled greeting is selected at random for each guest.");
        ImGui.Spacing();

        var removeIndex = -1;
        for (var index = 0; index < configuration.GreetingTemplates.Count; index++)
        {
            var template = configuration.GreetingTemplates[index];
            ImGui.PushID(index);

            var enabled = template.Enabled;
            if (ImGui.Checkbox("##Enabled", ref enabled))
            {
                template.Enabled = enabled;
                configuration.Save();
            }

            ImGui.SameLine();
            ImGui.SetNextItemWidth(-85);
            var text = template.Text;
            if (ImGui.InputText("##Template", ref text, 400))
            {
                template.Text = text;
                configuration.Save();
            }

            ImGui.SameLine();
            if (ImGui.Button("Remove"))
            {
                removeIndex = index;
            }

            ImGui.PopID();
        }

        if (removeIndex >= 0)
        {
            configuration.GreetingTemplates.RemoveAt(removeIndex);
            configuration.Save();
        }

        if (ImGui.Button("Add Greeting"))
        {
            configuration.GreetingTemplates.Add(new GreetingTemplate("Welcome to {venue}, {first}!"));
            configuration.Save();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        var previewPlayer = new PlayerKey("Sample Guest", "Sample World");
        if (ImGui.Button("Preview"))
        {
            previewText = engine.PreviewGreeting(previewPlayer) ?? "No enabled greeting template is available.";
        }

        ImGui.SameLine();
        if (ImGui.Button("Send Actual Test"))
        {
            previewText = engine.TrySendTest(previewPlayer, out var error)
                ? "Test greeting sent to game chat."
                : error;
        }

        if (!string.IsNullOrWhiteSpace(previewText))
        {
            ImGui.TextWrapped(previewText);
        }
    }

    private void DrawRules()
    {
        ImGui.Spacing();
        ImGui.TextWrapped(
            "Rules use the character's full name and Home World. Never Greet overrides Always Greet. Adding a guest to either list removes them from the other.");

        ImGui.SetNextItemWidth(260);
        ImGui.InputText("Character name", ref manualName, 80);
        DrawWorldPicker();

        var player = new PlayerKey(manualName, manualWorld);
        if (ImGui.Button("Add to Always Greet") && rules.AddAlways(player))
        {
            manualName = string.Empty;
            manualWorld = string.Empty;
        }

        ImGui.SameLine();
        if (ImGui.Button("Add to Never Greet") && rules.AddNever(player))
        {
            manualName = string.Empty;
            manualWorld = string.Empty;
        }

        ImGui.Spacing();
        ImGui.TextDisabled("Tip: right-click a player in game and open the Greeter submenu to add them quickly.");
        ImGui.Separator();

        ImGui.Columns(2, "RuleColumns", true);
        ImGui.Text("Always Greet");
        ImGui.NextColumn();
        ImGui.Text("Never Greet");
        ImGui.NextColumn();
        DrawRuleList(configuration.AlwaysGreet, false);
        ImGui.NextColumn();
        DrawRuleList(configuration.NeverGreet, true);
        ImGui.Columns(1);
    }

    private void DrawRuleList(System.Collections.Generic.IReadOnlyList<CharacterRuleEntry> entries, bool never)
    {
        CharacterRuleEntry? remove = null;
        foreach (var entry in entries.ToArray())
        {
            var player = entry.ToPlayerKey();
            ImGui.PushID($"{(never ? "never" : "always")}-{player.NormalizedKey}");
            ImGui.TextWrapped(player.DisplayName);
            ImGui.SameLine();
            if (ImGui.SmallButton("Remove"))
            {
                remove = entry;
            }

            ImGui.PopID();
        }

        if (remove != null)
        {
            if (never)
            {
                rules.RemoveNever(remove.ToPlayerKey());
            }
            else
            {
                rules.RemoveAlways(remove.ToPlayerKey());
            }
        }
    }

    private void DrawActivity()
    {
        ImGui.Spacing();
        if (ImGui.Button("Clear Greeted This Session"))
        {
            engine.ClearSessionGreeted();
        }

        ImGui.SameLine();
        ImGui.TextDisabled($"{engine.SessionGreetedCount} recorded");

        if (ImGui.Button("Clear Activity Log"))
        {
            engine.ClearActivity();
        }

        ImGui.Separator();

        if (engine.RecentActivity.Count == 0)
        {
            ImGui.TextDisabled("No Greeter activity yet.");
            return;
        }

        foreach (var item in engine.RecentActivity)
        {
            ImGui.TextDisabled(item.TimeUtc.ToLocalTime().ToString("HH:mm:ss"));
            ImGui.SameLine();
            ImGui.TextWrapped(item.Message);
        }
    }

    private void DrawWorldPicker()
    {
        ImGui.SetNextItemWidth(260);
        var preview = string.IsNullOrWhiteSpace(manualWorld)
            ? "Select a Home World..."
            : manualWorld;

        if (!ImGui.BeginCombo("Home World", preview))
        {
            return;
        }

        if (worldList.Worlds.Count == 0)
        {
            ImGui.TextDisabled("World list unavailable.");
            if (ImGui.Selectable("Reload world list"))
            {
                worldList.Reload();
            }
        }
        else
        {
            foreach (var world in worldList.Worlds)
            {
                var selected = string.Equals(manualWorld, world.Name, StringComparison.OrdinalIgnoreCase);
                if (ImGui.Selectable(world.DisplayName, selected))
                {
                    manualWorld = world.Name;
                }

                if (selected)
                {
                    ImGui.SetItemDefaultFocus();
                }
            }
        }

        ImGui.EndCombo();
    }

    private void DrawIntSetting(string label, int current, int minimum, int maximum, Action<int> setter)
    {
        var value = current;
        if (ImGui.SliderInt(label, ref value, minimum, maximum))
        {
            setter(value);
            configuration.Save();
        }
    }
}

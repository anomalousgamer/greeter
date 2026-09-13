using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using Greeter.Models;

namespace Greeter.Services;

public sealed class GreetingEngine : IDisposable
{
    private static readonly TimeSpan ScanInterval = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan VisibleGracePeriod = TimeSpan.FromSeconds(1);

    private readonly Configuration configuration;
    private readonly HousingService housing;
    private readonly RuleManager rules;
    private readonly ChatSender chatSender;
    private readonly IFramework framework;
    private readonly IClientState clientState;
    private readonly IPlayerState playerState;
    private readonly IObjectTable objectTable;
    private readonly IChatGui chatGui;
    private readonly IPluginLog log;
    private readonly Dictionary<string, PresenceRecord> presence = new(StringComparer.Ordinal);
    private readonly List<QueuedGreeting> queue = [];
    private readonly HashSet<string> greetedThisSession = new(StringComparer.Ordinal);
    private readonly List<GreetingActivity> activity = [];

    private DateTime nextScanUtc = DateTime.MinValue;
    private DateTime nextSendAllowedUtc = DateTime.MinValue;
    private DateTime venueEnteredUtc = DateTime.MinValue;
    private ulong activeHouseId;

    public GreetingEngine(
        Configuration configuration,
        HousingService housing,
        RuleManager rules,
        ChatSender chatSender,
        IFramework framework,
        IClientState clientState,
        IPlayerState playerState,
        IObjectTable objectTable,
        IChatGui chatGui,
        IPluginLog log)
    {
        this.configuration = configuration;
        this.housing = housing;
        this.rules = rules;
        this.chatSender = chatSender;
        this.framework = framework;
        this.clientState = clientState;
        this.playerState = playerState;
        this.objectTable = objectTable;
        this.chatGui = chatGui;
        this.log = log;

        LoadOrStartSession();
        framework.Update += OnFrameworkUpdate;
        clientState.Logout += OnLogout;
    }

    public bool IsInConfiguredVenue { get; private set; }
    public HouseSnapshot? CurrentHouse { get; private set; }
    public int PresentCount => presence.Count;
    public int QueueCount => queue.Count;
    public int SessionGreetedCount => greetedThisSession.Count;
    public IReadOnlyList<GreetingActivity> RecentActivity => activity;

    public string StatusText
    {
        get
        {
            if (!configuration.AutomaticGreetingEnabled)
            {
                return "Automatic greeting is off";
            }

            if (configuration.ConfiguredHouseId == 0)
            {
                return "No venue house is configured";
            }

            return IsInConfiguredVenue
                ? "Active in the configured venue"
                : "Waiting until you enter the configured venue";
        }
    }

    public void Dispose()
    {
        framework.Update -= OnFrameworkUpdate;
        clientState.Logout -= OnLogout;
    }

    public bool WasGreeted(PlayerKey player) => greetedThisSession.Contains(player.NormalizedKey);

    public void MarkGreeted(PlayerKey player)
    {
        if (!player.IsUsable || !greetedThisSession.Add(player.NormalizedKey))
        {
            return;
        }

        SaveSessionState();
        AddActivity($"Marked {player.DisplayName} as greeted for this session.");
    }

    public void ClearGreeted(PlayerKey player)
    {
        if (!greetedThisSession.Remove(player.NormalizedKey))
        {
            return;
        }

        SaveSessionState();
        AddActivity($"Cleared the session greeting for {player.DisplayName}.");
    }

    public void ClearSessionGreeted()
    {
        greetedThisSession.Clear();
        SaveSessionState();
        AddActivity("Cleared all greeted-this-session records.");
    }

    public bool TrySendTest(PlayerKey player, out string error)
    {
        if (!IsInConfiguredVenue)
        {
            error = "Enter the configured venue before sending a test greeting.";
            return false;
        }

        var message = RenderGreeting(player);
        if (message == null)
        {
            error = "Enable at least one non-empty greeting template.";
            return false;
        }

        var sent = chatSender.TrySend(configuration.GreetingChannel, message, out error);
        if (sent)
        {
            AddActivity($"Sent a manual test greeting: {message}");
        }

        return sent;
    }

    public string? PreviewGreeting(PlayerKey player) => RenderGreeting(player);

    public void ResetVenueTracking(string reason)
    {
        if (presence.Count > 0 || queue.Count > 0 || activeHouseId != 0)
        {
            Debug(reason);
        }

        presence.Clear();
        queue.Clear();
        activeHouseId = 0;
        venueEnteredUtc = DateTime.MinValue;
        nextSendAllowedUtc = DateTime.MinValue;
        IsInConfiguredVenue = false;
    }

    private void OnFrameworkUpdate(IFramework _)
    {
        var now = DateTime.UtcNow;
        if (now < nextScanUtc)
        {
            return;
        }

        nextScanUtc = now + ScanInterval;

        try
        {
            Scan(now);
        }
        catch (Exception exception)
        {
            log.Error(exception, "Greeter scan failed.");
        }
    }

    private void Scan(DateTime now)
    {
        EnsureCharacterSession();

        CurrentHouse = housing.TryGetCurrentIndoorHouse(out var house) ? house : null;
        var isActive = clientState.IsLoggedIn
            && configuration.AutomaticGreetingEnabled
            && configuration.ConfiguredHouseId != 0
            && CurrentHouse is { } current
            && current.HouseId == configuration.ConfiguredHouseId;

        if (!isActive)
        {
            if (IsInConfiguredVenue)
            {
                ResetVenueTracking("Left the configured venue; arrival tracking was reset.");
            }

            IsInConfiguredVenue = false;
            return;
        }

        IsInConfiguredVenue = true;
        if (activeHouseId != house.HouseId)
        {
            presence.Clear();
            queue.Clear();
            activeHouseId = house.HouseId;
            venueEnteredUtc = now;
            nextSendAllowedUtc = now;
            AddActivity($"Venue monitoring started at {house.Label}.");
        }

        var baselineComplete = now >= venueEnteredUtc.AddSeconds(configuration.InitialScanSeconds);
        var visible = ReadVisiblePlayers();

        foreach (var player in visible.Values)
        {
            if (!presence.TryGetValue(player.NormalizedKey, out var record))
            {
                record = new PresenceRecord(player, now)
                {
                    SuppressThisArrival = !configuration.GreetPlayersPresentOnActivation && !baselineComplete,
                };
                presence.Add(player.NormalizedKey, record);
                Debug($"Observed arrival candidate: {player.DisplayName}.");
            }

            record.LastSeenUtc = now;
        }

        var confirmedGone = presence.Values
            .Where(record => now - record.LastSeenUtc >= TimeSpan.FromSeconds(configuration.AbsenceConfirmationSeconds))
            .ToArray();

        foreach (var record in confirmedGone)
        {
            presence.Remove(record.Player.NormalizedKey);
            queue.RemoveAll(item => item.Player.NormalizedKey == record.Player.NormalizedKey);
            Debug($"Confirmed departure: {record.Player.DisplayName}.");
        }

        if (baselineComplete)
        {
            EvaluateArrivals(now);
            ProcessQueue(now, visible);
        }
    }

    private Dictionary<string, PlayerKey> ReadVisiblePlayers()
    {
        var result = new Dictionary<string, PlayerKey>(StringComparer.Ordinal);
        var localKey = GetLocalPlayerKey();

        foreach (var battleCharacter in objectTable.PlayerObjects)
        {
            if (battleCharacter is not IPlayerCharacter player || !player.HomeWorld.IsValid)
            {
                continue;
            }

            var key = new PlayerKey(player.Name.TextValue, player.HomeWorld.Value.Name.ToString());
            if (!key.IsUsable || key.NormalizedKey == localKey.NormalizedKey)
            {
                continue;
            }

            result[key.NormalizedKey] = key;
        }

        return result;
    }

    private void EvaluateArrivals(DateTime now)
    {
        foreach (var record in presence.Values.Where(item => !item.Evaluated).ToArray())
        {
            if (now - record.FirstSeenUtc < TimeSpan.FromSeconds(configuration.ArrivalConfirmationSeconds))
            {
                continue;
            }

            record.Evaluated = true;
            if (record.SuppressThisArrival)
            {
                Debug($"Baseline guest was not greeted: {record.Player.DisplayName}.");
                continue;
            }

            var rule = rules.GetRule(record.Player);
            if (rule == GreetingRule.Never)
            {
                Debug($"Never Greet rule skipped {record.Player.DisplayName}.");
                continue;
            }

            if (rule == GreetingRule.Normal && WasGreeted(record.Player))
            {
                Debug($"Session duplicate protection skipped {record.Player.DisplayName}.");
                continue;
            }

            Enqueue(record.Player, now);
        }
    }

    private void Enqueue(PlayerKey player, DateTime now)
    {
        if (queue.Any(item => item.Player.NormalizedKey == player.NormalizedKey))
        {
            return;
        }

        var delay = Random.Shared.Next(
            configuration.MinimumGreetingDelaySeconds,
            configuration.MaximumGreetingDelaySeconds + 1);
        var due = (nextSendAllowedUtc > now ? nextSendAllowedUtc : now).AddSeconds(delay);
        queue.Add(new QueuedGreeting(player, now, due));
        nextSendAllowedUtc = due;
        AddActivity($"Queued a greeting for {player.DisplayName}.");
    }

    private void ProcessQueue(DateTime now, IReadOnlyDictionary<string, PlayerKey> visible)
    {
        foreach (var expired in queue
                     .Where(item => now - item.QueuedUtc >= TimeSpan.FromSeconds(configuration.MaximumQueueAgeSeconds))
                     .ToArray())
        {
            queue.Remove(expired);
            AddActivity($"Dropped a stale greeting for {expired.Player.DisplayName}.");
        }

        var pending = queue.OrderBy(item => item.DueUtc).FirstOrDefault();
        if (pending == null || pending.DueUtc > now)
        {
            return;
        }

        if (!presence.TryGetValue(pending.Player.NormalizedKey, out var record))
        {
            queue.Remove(pending);
            AddActivity($"Cancelled the greeting because {pending.Player.DisplayName} has left.");
            return;
        }

        if (!visible.ContainsKey(pending.Player.NormalizedKey)
            || now - record.LastSeenUtc > VisibleGracePeriod)
        {
            // A crowded room can temporarily cull a player from the object table.
            // Keep the job queued until they reappear, their departure is confirmed,
            // or the queue item expires.
            pending.DueUtc = now.AddSeconds(1);
            return;
        }

        var currentRule = rules.GetRule(pending.Player);
        if (currentRule == GreetingRule.Never)
        {
            queue.Remove(pending);
            AddActivity($"Cancelled the greeting because {pending.Player.DisplayName} is now on Never Greet.");
            return;
        }

        if (currentRule == GreetingRule.Normal && WasGreeted(pending.Player))
        {
            queue.Remove(pending);
            AddActivity($"Cancelled a duplicate session greeting for {pending.Player.DisplayName}.");
            return;
        }

        var message = RenderGreeting(pending.Player);
        if (message == null)
        {
            queue.Remove(pending);
            AddActivity("No enabled greeting template was available; the queued greeting was cancelled.");
            return;
        }

        if (!chatSender.TrySend(configuration.GreetingChannel, message, out var error))
        {
            pending.Attempts++;
            if (pending.Attempts >= 3)
            {
                queue.Remove(pending);
                AddActivity($"Could not send the greeting for {pending.Player.DisplayName}: {error}");
            }
            else
            {
                pending.DueUtc = now.AddSeconds(5);
            }

            return;
        }

        queue.Remove(pending);
        greetedThisSession.Add(pending.Player.NormalizedKey);
        SaveSessionState();
        AddActivity($"Greeted {pending.Player.DisplayName}: {message}");
    }

    private string? RenderGreeting(PlayerKey player)
    {
        var templates = configuration.GreetingTemplates
            .Where(template => template.Enabled && !string.IsNullOrWhiteSpace(template.Text))
            .ToArray();
        if (templates.Length == 0)
        {
            return null;
        }

        var selected = templates[Random.Shared.Next(templates.Length)];
        return TemplateRenderer.Render(selected.Text, player, configuration.VenueName);
    }

    private void LoadOrStartSession()
    {
        var marker = GetProcessSessionMarker();
        if (!string.Equals(configuration.RuntimeSessionMarker, marker, StringComparison.Ordinal))
        {
            configuration.RuntimeSessionMarker = marker;
            configuration.RuntimeCharacterKey = string.Empty;
            configuration.RuntimeGreetedKeys.Clear();
            configuration.Save();
        }

        foreach (var key in configuration.RuntimeGreetedKeys.Where(key => !string.IsNullOrWhiteSpace(key)))
        {
            greetedThisSession.Add(key);
        }
    }

    private void EnsureCharacterSession()
    {
        if (!clientState.IsLoggedIn || !playerState.IsLoaded)
        {
            return;
        }

        var character = GetLocalPlayerKey().NormalizedKey;
        if (string.IsNullOrWhiteSpace(character) || character == "@")
        {
            return;
        }

        if (string.Equals(configuration.RuntimeCharacterKey, character, StringComparison.Ordinal))
        {
            return;
        }

        greetedThisSession.Clear();
        configuration.RuntimeCharacterKey = character;
        SaveSessionState();
        ResetVenueTracking("The logged-in character changed; the greeting session was reset.");
    }

    private PlayerKey GetLocalPlayerKey()
    {
        if (!playerState.IsLoaded || !playerState.HomeWorld.IsValid)
        {
            return default;
        }

        return new PlayerKey(playerState.CharacterName, playerState.HomeWorld.Value.Name.ToString());
    }

    private void OnLogout(int _, int __)
    {
        greetedThisSession.Clear();
        configuration.RuntimeCharacterKey = string.Empty;
        SaveSessionState();
        ResetVenueTracking("Logged out; the greeting session was reset.");
    }

    private void SaveSessionState()
    {
        configuration.RuntimeGreetedKeys = greetedThisSession.Order().ToList();
        configuration.Save();
    }

    private void AddActivity(string message)
    {
        activity.Insert(0, new GreetingActivity(DateTime.UtcNow, message));
        if (activity.Count > 100)
        {
            activity.RemoveRange(100, activity.Count - 100);
        }

        log.Information(message);
        Debug(message);
    }

    private void Debug(string message)
    {
        log.Debug(message);
        if (configuration.DebugChatEnabled)
        {
            chatGui.Print(message, "Greeter");
        }
    }

    private static string GetProcessSessionMarker()
    {
        using var process = Process.GetCurrentProcess();
        return $"{Environment.ProcessId}:{process.StartTime.ToUniversalTime().Ticks}";
    }

    private sealed class PresenceRecord(PlayerKey player, DateTime firstSeenUtc)
    {
        public PlayerKey Player { get; } = player;
        public DateTime FirstSeenUtc { get; } = firstSeenUtc;
        public DateTime LastSeenUtc { get; set; } = firstSeenUtc;
        public bool Evaluated { get; set; }
        public bool SuppressThisArrival { get; init; }
    }

    private sealed class QueuedGreeting(PlayerKey player, DateTime queuedUtc, DateTime dueUtc)
    {
        public PlayerKey Player { get; } = player;
        public DateTime QueuedUtc { get; } = queuedUtc;
        public DateTime DueUtc { get; set; } = dueUtc;
        public int Attempts { get; set; }
    }
}

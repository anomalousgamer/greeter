using System;
using System.Collections.Generic;
using Dalamud.Configuration;
using Dalamud.Plugin;
using Greeter.Models;
using Newtonsoft.Json;

namespace Greeter;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public bool AutomaticGreetingEnabled { get; set; } = true;
    public bool DebugChatEnabled { get; set; }
    public bool GreetPlayersPresentOnActivation { get; set; }

    public ulong ConfiguredHouseId { get; set; }
    public string ConfiguredHouseLabel { get; set; } = string.Empty;
    public string VenueName { get; set; } = "the venue";
    public GreetingChannel GreetingChannel { get; set; } = GreetingChannel.Say;

    public int InitialScanSeconds { get; set; } = 8;
    public int ArrivalConfirmationSeconds { get; set; } = 3;
    public int AbsenceConfirmationSeconds { get; set; } = 15;
    public int MinimumGreetingDelaySeconds { get; set; } = 4;
    public int MaximumGreetingDelaySeconds { get; set; } = 7;
    public int MaximumQueueAgeSeconds { get; set; } = 90;

    [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public List<GreetingTemplate> GreetingTemplates { get; set; } =
    [
        new GreetingTemplate("Welcome to {venue}, {first}!"),
        new GreetingTemplate("Hello {first}! Welcome to {venue}."),
    ];

    [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public List<CharacterRuleEntry> AlwaysGreet { get; set; } = [];

    [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public List<CharacterRuleEntry> NeverGreet { get; set; } = [];

    // Runtime session data is persisted only so a Dalamud plugin reload does not
    // cause duplicate greetings during the same running FFXIV session.
    public string RuntimeSessionMarker { get; set; } = string.Empty;
    public string RuntimeCharacterKey { get; set; } = string.Empty;
    [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public List<string> RuntimeGreetedKeys { get; set; } = [];

    [NonSerialized]
    private IDalamudPluginInterface? pluginInterface;

    public void Initialize(IDalamudPluginInterface interfaceInstance)
    {
        pluginInterface = interfaceInstance;
        Normalize();
    }

    public void Normalize()
    {
        VenueName = string.IsNullOrWhiteSpace(VenueName) ? "the venue" : VenueName.Trim();
        InitialScanSeconds = Math.Clamp(InitialScanSeconds, 3, 30);
        ArrivalConfirmationSeconds = Math.Clamp(ArrivalConfirmationSeconds, 1, 15);
        AbsenceConfirmationSeconds = Math.Clamp(AbsenceConfirmationSeconds, 5, 60);
        MinimumGreetingDelaySeconds = Math.Clamp(MinimumGreetingDelaySeconds, 3, 30);
        MaximumGreetingDelaySeconds = Math.Clamp(MaximumGreetingDelaySeconds, MinimumGreetingDelaySeconds, 60);
        MaximumQueueAgeSeconds = Math.Clamp(MaximumQueueAgeSeconds, 30, 300);

        GreetingTemplates ??= [];
        AlwaysGreet ??= [];
        NeverGreet ??= [];
        RuntimeGreetedKeys ??= [];

    }

    public void Save()
    {
        Normalize();
        pluginInterface?.SavePluginConfig(this);
    }
}

using System;

namespace Greeter.Models;

[Serializable]
public sealed class CharacterRuleEntry
{
    public string Name { get; set; } = string.Empty;
    public string World { get; set; } = string.Empty;

    public CharacterRuleEntry()
    {
    }

    public CharacterRuleEntry(PlayerKey player)
    {
        Name = player.Name;
        World = player.World;
    }

    public PlayerKey ToPlayerKey() => new(Name, World);
}


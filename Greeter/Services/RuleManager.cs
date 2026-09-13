using System;
using System.Collections.Generic;
using System.Linq;
using Greeter.Models;

namespace Greeter.Services;

public enum GreetingRule
{
    Normal,
    Always,
    Never,
}

public sealed class RuleManager(Configuration configuration)
{
    public GreetingRule GetRule(PlayerKey player)
    {
        if (Contains(configuration.NeverGreet, player))
        {
            return GreetingRule.Never;
        }

        return Contains(configuration.AlwaysGreet, player)
            ? GreetingRule.Always
            : GreetingRule.Normal;
    }

    public bool AddAlways(PlayerKey player)
    {
        if (!player.IsUsable)
        {
            return false;
        }

        Remove(configuration.NeverGreet, player);
        if (!Contains(configuration.AlwaysGreet, player))
        {
            configuration.AlwaysGreet.Add(new CharacterRuleEntry(player));
        }

        configuration.Save();
        return true;
    }

    public bool AddNever(PlayerKey player)
    {
        if (!player.IsUsable)
        {
            return false;
        }

        Remove(configuration.AlwaysGreet, player);
        if (!Contains(configuration.NeverGreet, player))
        {
            configuration.NeverGreet.Add(new CharacterRuleEntry(player));
        }

        configuration.Save();
        return true;
    }

    public bool RemoveAlways(PlayerKey player)
    {
        var changed = Remove(configuration.AlwaysGreet, player);
        if (changed)
        {
            configuration.Save();
        }

        return changed;
    }

    public bool RemoveNever(PlayerKey player)
    {
        var changed = Remove(configuration.NeverGreet, player);
        if (changed)
        {
            configuration.Save();
        }

        return changed;
    }

    public static bool Contains(IEnumerable<CharacterRuleEntry> list, PlayerKey player) =>
        list.Any(entry => string.Equals(
            entry.ToPlayerKey().NormalizedKey,
            player.NormalizedKey,
            StringComparison.Ordinal));

    private static bool Remove(ICollection<CharacterRuleEntry> list, PlayerKey player)
    {
        var matches = list
            .Where(entry => string.Equals(
                entry.ToPlayerKey().NormalizedKey,
                player.NormalizedKey,
                StringComparison.Ordinal))
            .ToArray();

        foreach (var match in matches)
        {
            list.Remove(match);
        }

        return matches.Length > 0;
    }
}

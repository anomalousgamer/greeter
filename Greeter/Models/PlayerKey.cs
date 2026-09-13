using System;
using System.Linq;

namespace Greeter.Models;

public readonly record struct PlayerKey
{
    public PlayerKey(string name, string world)
    {
        Name = NormalizeDisplayPart(name);
        World = NormalizeDisplayPart(world);
    }

    public string Name { get; }
    public string World { get; }
    public string DisplayName => string.IsNullOrWhiteSpace(World) ? Name : $"{Name} @ {World}";
    public string NormalizedKey => $"{Name.Trim().ToUpperInvariant()}@{World.Trim().ToUpperInvariant()}";

    public string FirstName
    {
        get
        {
            var parts = Name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.FirstOrDefault() ?? Name;
        }
    }

    public string LastName
    {
        get
        {
            var parts = Name.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 1 ? parts[1] : string.Empty;
        }
    }

    public bool IsUsable =>
        Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 2
        && !string.IsNullOrWhiteSpace(World);

    private static string NormalizeDisplayPart(string value) =>
        string.Join(' ', (value ?? string.Empty)
            .Trim()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
}

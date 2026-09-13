namespace Greeter.Models;

public readonly record struct WorldOption(string Name, string DataCenter)
{
    public string DisplayName => string.IsNullOrWhiteSpace(DataCenter)
        ? Name
        : $"{Name} ({DataCenter})";
}

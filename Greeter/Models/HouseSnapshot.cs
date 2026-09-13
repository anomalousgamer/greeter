namespace Greeter.Models;

public readonly record struct HouseSnapshot(
    ulong HouseId,
    ushort WorldId,
    ushort TerritoryTypeId,
    int Ward,
    int Plot,
    int Room,
    bool IsApartment)
{
    public string Label
    {
        get
        {
            var location = IsApartment
                ? $"Ward {Ward}, apartment room {Room}"
                : Room > 0
                    ? $"Ward {Ward}, plot {Plot}, room {Room}"
                    : $"Ward {Ward}, plot {Plot}";

            return $"{location} (world {WorldId}, territory {TerritoryTypeId})";
        }
    }
}

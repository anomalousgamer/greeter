using FFXIVClientStructs.FFXIV.Client.Game;
using Greeter.Models;

namespace Greeter.Services;

public sealed unsafe class HousingService
{
    public bool TryGetCurrentIndoorHouse(out HouseSnapshot house)
    {
        house = default;

        var manager = HousingManager.Instance();
        if (manager == null || !manager->IsInside() || manager->IsInWorkshop())
        {
            return false;
        }

        var houseId = manager->GetCurrentIndoorHouseId();
        if (houseId.Id == 0)
        {
            houseId = manager->GetCurrentHouseId();
        }

        if (houseId.Id == 0)
        {
            return false;
        }

        var ward = manager->GetCurrentWard() + 1;
        var plot = houseId.IsApartment ? 0 : houseId.PlotIndex + 1;
        var room = manager->GetCurrentRoom();

        house = new HouseSnapshot(
            houseId.Id,
            houseId.WorldId,
            houseId.TerritoryTypeId,
            ward,
            plot,
            room,
            houseId.IsApartment);

        return true;
    }
}

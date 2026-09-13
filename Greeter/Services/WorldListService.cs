using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using Greeter.Models;
using Lumina.Excel.Sheets;

namespace Greeter.Services;

public sealed class WorldListService
{
    private readonly IDataManager dataManager;
    private readonly IPluginLog log;

    public WorldListService(IDataManager dataManager, IPluginLog log)
    {
        this.dataManager = dataManager;
        this.log = log;
        Reload();
    }

    public IReadOnlyList<WorldOption> Worlds { get; private set; } = [];

    public void Reload()
    {
        try
        {
            Worlds = dataManager
                .GetExcelSheet<World>()
                .Where(world => world.IsPublic && !string.IsNullOrWhiteSpace(world.Name.ToString()))
                .Select(world => new WorldOption(
                    world.Name.ToString(),
                    world.DataCenter.IsValid ? world.DataCenter.Value.Name.ToString() : string.Empty))
                .DistinctBy(world => world.Name, StringComparer.OrdinalIgnoreCase)
                .OrderBy(world => world.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (Exception exception)
        {
            Worlds = [];
            log.Error(exception, "Could not load the FFXIV Home World list.");
        }
    }
}

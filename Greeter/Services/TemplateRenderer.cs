using System;
using Greeter.Models;

namespace Greeter.Services;

public static class TemplateRenderer
{
    public static string Render(string template, PlayerKey player, string venueName)
    {
        var text = template ?? string.Empty;
        text = text.Replace("{first}", player.FirstName, StringComparison.OrdinalIgnoreCase);
        text = text.Replace("{last}", player.LastName, StringComparison.OrdinalIgnoreCase);
        text = text.Replace("{fullname}", player.Name, StringComparison.OrdinalIgnoreCase);
        text = text.Replace("{world}", player.World, StringComparison.OrdinalIgnoreCase);
        text = text.Replace("{venue}", venueName, StringComparison.OrdinalIgnoreCase);
        return text.Trim();
    }
}

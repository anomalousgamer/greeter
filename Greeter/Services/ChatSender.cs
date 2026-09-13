using System;
using System.Text;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;
using Greeter.Models;

namespace Greeter.Services;

public sealed unsafe class ChatSender(IPluginLog log)
{
    private const int MaximumUtf8Bytes = 450;

    public bool TrySend(
        GreetingChannel channel,
        string message,
        PlayerKey? recipient,
        out string error)
    {
        error = string.Empty;
        var clean = Sanitize(message);
        if (string.IsNullOrWhiteSpace(clean))
        {
            error = "The rendered greeting was empty.";
            return false;
        }

        if (channel == GreetingChannel.Tell && recipient is not { IsUsable: true })
        {
            error = "A valid character name and Home World are required for /tell.";
            return false;
        }

        var command = channel == GreetingChannel.Tell
            ? $"/tell {recipient!.Value.Name}@{recipient.Value.World} {clean}"
            : $"{GetCommand(channel)} {clean}";
        command = TruncateUtf8(command, MaximumUtf8Bytes);

        try
        {
            var uiModule = UIModule.Instance();
            if (uiModule == null)
            {
                error = "The game chat module is not available.";
                return false;
            }

            using var chatEntry = new Utf8String(command);
            uiModule->ProcessChatBoxEntry(&chatEntry, 0, false);
            return true;
        }
        catch (Exception exception)
        {
            log.Error(exception, "Failed to dispatch a Greeter chat message.");
            error = exception.Message;
            return false;
        }
    }

    private static string GetCommand(GreetingChannel channel) => channel switch
    {
        GreetingChannel.Yell => "/yell",
        GreetingChannel.Shout => "/shout",
        _ => "/say",
    };

    private static string Sanitize(string value) =>
        (value ?? string.Empty)
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Replace('\0', ' ')
            .Trim();

    private static string TruncateUtf8(string value, int maximumBytes)
    {
        if (Encoding.UTF8.GetByteCount(value) <= maximumBytes)
        {
            return value;
        }

        var builder = new StringBuilder(value.Length);
        var byteCount = 0;
        foreach (var rune in value.EnumerateRunes())
        {
            if (byteCount + rune.Utf8SequenceLength > maximumBytes)
            {
                break;
            }

            builder.Append(rune.ToString());
            byteCount += rune.Utf8SequenceLength;
        }

        return builder.ToString().TrimEnd();
    }
}

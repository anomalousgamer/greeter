using System;

namespace Greeter.Models;

[Serializable]
public sealed class GreetingTemplate
{
    public string Text { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;

    public GreetingTemplate()
    {
    }

    public GreetingTemplate(string text)
    {
        Text = text;
    }
}


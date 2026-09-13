using System;

namespace Greeter.Models;

public readonly record struct GreetingActivity(DateTime TimeUtc, string Message);

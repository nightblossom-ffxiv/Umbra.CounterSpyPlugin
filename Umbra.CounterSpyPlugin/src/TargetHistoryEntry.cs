using System;

namespace Umbra.CounterSpyPlugin;

internal sealed class TargetHistoryEntry
{
    public string   Name        { get; set; } = "";
    public string   World       { get; set; } = "";
    public DateTime LastSeenUtc { get; set; }
}

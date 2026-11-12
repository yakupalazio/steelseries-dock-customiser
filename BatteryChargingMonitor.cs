using System;
using System.Collections.Generic;

namespace SteelseriesDockCustomiser;

/// <summary>
/// Detects an actual verified non-charging -> charging transition. An unknown
/// initial battery state never triggers an unsolicited charging overlay.
/// Pollers own the schedule; this monitor never probes external services.
/// </summary>
internal sealed class BatteryChargingMonitor
{
    private readonly Dictionary<string,bool> knownStates=
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<(string Kind,int Percent)> Evaluate(
        BatterySnapshot snapshot,DateTimeOffset now)
    {
        var found=new List<(string,int)>();
        foreach(var (kind,reading) in snapshot.Values)
        {
            if(!BatteryPriority.IsCurrent(reading,now) ||
                reading.Charging is not bool current ||
                reading.Percent is not int percent)
                continue;
            if(knownStates.TryGetValue(kind,out bool wasCharging) &&
                !wasCharging && current)
                found.Add((kind,percent));
            knownStates[kind]=current;
        }
        return found;
    }
}

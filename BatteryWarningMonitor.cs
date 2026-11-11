using System;
using System.Collections.Generic;
using System.Linq;

namespace SteelseriesDockCustomiser;

/// <summary>
/// Detects threshold crossings only on fresh, verified battery readings.
/// No notifications are sent until the owning window presents them.
/// </summary>
internal sealed class BatteryWarningMonitor
{
    private readonly Dictionary<string,(int Band,DateTimeOffset WarnedAt)> sent=
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<(string Kind,int Percent)> Evaluate(
        BatterySnapshot snapshot,BatteryPreferences prefs,DateTimeOffset now)
    {
        if(!prefs.EnableLowBatteryWarnings)return Array.Empty<(string,int)>();
        var alerts=new List<(string,int)>();
        foreach(var (kind,reading) in snapshot.Values)
        {
            if(!BatteryPriority.IsCurrent(reading,now) || reading.IsCharging ||
                !reading.Percent.HasValue)continue;
            int threshold=prefs.LowBatteryThresholds.TryGetValue(kind,out var custom)
                ?Math.Clamp(custom,1,99):Math.Clamp(prefs.LowBatteryWarningPercent,1,99);
            int critical=Math.Min(Math.Clamp(prefs.CriticalBatteryWarningPercent,1,99),
                threshold);
            int value=reading.Percent.Value;
            int band=value<=critical?2:value<=threshold?1:0;
            if(band==0)
            {
                if(value>threshold+5)sent.Remove(kind);
                continue;
            }
            if(sent.TryGetValue(kind,out var previous) &&
               previous.Band>=band &&
               now-previous.WarnedAt<TimeSpan.FromMinutes(
                    Math.Clamp(prefs.WarningRepeatMinutes,5,240)))continue;
            sent[kind]=(band,now);
            alerts.Add((kind,value));
        }
        return alerts;
    }
}

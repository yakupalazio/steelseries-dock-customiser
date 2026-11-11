using System;
using System.Collections.Generic;
using System.Linq;

namespace SteelseriesDockCustomiser;

internal static class BatteryPriority
{
    public static readonly string[] AllKinds =
        ["Mouse","Phone","DualSense","Laptop","Headset","SpareBattery"];

    public static readonly string[] DefaultKinds =
        ["Mouse","Phone","DualSense","Laptop","Headset","SpareBattery"];

    public static string DisplayName(string kind)=>UiLanguage.T(kind switch
    {
        "Mouse"=>"Aerox Mouse",
        "Phone"=>"Galaxy S24+",
        "DualSense"=>"DualSense",
        "Laptop"=>"Laptop",
        "Headset"=>"Nova Pro Kulaklık",
        "SpareBattery"=>"Nova Pro Yedek Pil",
        _=>kind
    });

    public static string[] Sanitize(IEnumerable<string>? priority)
    {
        var result=(priority??[]).Where(AllKinds.Contains)
            .Distinct(StringComparer.Ordinal).ToList();
        foreach(var kind in DefaultKinds)
            if(!result.Contains(kind)) result.Add(kind);
        return result.ToArray();
    }

    /// <summary>
    /// Strict, ordered selection: never display a placeholder or an obsolete
    /// cached battery reading. When a higher priority device comes back online
    /// it automatically displaces the lowest-priority visible device.
    /// </summary>
    public static string[] SelectThree(BatterySnapshot snapshot,
        BatteryPreferences preferences)
        => SelectThreeAt(snapshot,preferences,DateTimeOffset.Now);

    internal static string[] SelectThreeAt(BatterySnapshot snapshot,
        BatteryPreferences preferences,DateTimeOffset now)
        => Sanitize(preferences.PriorityKinds)
            .Where(kind=>kind!="DualSense" || preferences.AutoDualSense)
            .Where(kind=>IsCurrent(snapshot[kind],now))
            .Take(3).ToArray();

    internal static bool IsCurrent(BatteryReading reading,DateTimeOffset now)
    {
        if(!reading.Available || reading.CheckedAt==DateTimeOffset.MinValue)
            return false;
        var age=now-reading.CheckedAt;
        // Readings from Phone Link / ADB / optional controller HID are polled
        // every 60s; GG and laptop are refreshed in the realtime loop.
        double ttl=reading.Kind is "Phone" or "DualSense" ? 90 : 12;
        return age>=TimeSpan.FromSeconds(-5) &&
            age<=TimeSpan.FromSeconds(ttl);
    }
}

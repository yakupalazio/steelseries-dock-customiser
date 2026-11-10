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

    public static string DisplayName(string kind)=>kind switch
    {
        "Mouse"=>"Aerox Mouse",
        "Phone"=>"Galaxy S24+",
        "DualSense"=>"DualSense",
        "Laptop"=>"Laptop",
        "Headset"=>"Nova Pro Kulaklık",
        "SpareBattery"=>"Nova Pro Yedek Pil",
        _=>kind
    };

    public static string[] Sanitize(IEnumerable<string>? priority)
    {
        var result=(priority??[]).Where(AllKinds.Contains)
            .Distinct(StringComparer.Ordinal).ToList();
        foreach(var kind in DefaultKinds)
            if(!result.Contains(kind)) result.Add(kind);
        return result.ToArray();
    }

    /// <summary>
    /// Defaults display Mouse + Phone + Laptop.
    /// DualSense only becomes eligible if its Bluetooth link is positively
    /// connected or its live HID report was obtained. Phone remains an explicit
    /// placeholder if Phone Link is unavailable rather than claiming 0%.
    /// Other peripherals are eligible when a current reliable GG reading exists.
    /// </summary>
    public static string[] SelectThree(BatterySnapshot snapshot,
        BatteryPreferences preferences)
    {
        var active=new List<string>();
        foreach(var kind in Sanitize(preferences.PriorityKinds))
        {
            bool eligible=kind switch
            {
                "Phone" or "Laptop"=>true,
                "DualSense"=>preferences.AutoDualSense &&
                    (snapshot.DualSenseConnected || snapshot["DualSense"].Available),
                _=>snapshot[kind].Available
            };
            if(eligible)active.Add(kind);
            if(active.Count==3)break;
        }
        // Never leave the UI completely empty when source access is intermittent.
        foreach(var kind in DefaultKinds)
        {
            if(active.Count>=3)break;
            if(!active.Contains(kind) && kind!="DualSense")active.Add(kind);
        }
        return active.Take(3).ToArray();
    }
}

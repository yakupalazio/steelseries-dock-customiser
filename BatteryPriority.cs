using System;
using System.Collections.Generic;
using System.Linq;

namespace SteelseriesDockCustomiser;

/// <summary>Stable priority order, with an explicit optional DualSense-over-Laptop rule.</summary>
internal static class BatteryPriority
{
    public static readonly string[] Defaults=[
        "Mouse","Laptop","Phone","DualSense","Headset","SpareBattery"
    ];

    public static string[] Normalize(IEnumerable<string>? candidate)
    {
        var safe=new List<string>();
        foreach(var c in candidate??[])
            if(BatterySources.Kinds.Contains(c) && !safe.Contains(c))
                safe.Add(c);
        foreach(var c in Defaults)
            if(!safe.Contains(c))safe.Add(c);
        return safe.ToArray();
    }

    public static string[] Resolve(BatterySnapshot snapshot,
        IEnumerable<string>? priority,bool dualSenseReplacesLaptop)
    {
        var sorted=Normalize(priority).ToList();
        bool dualSenseConnected=snapshot["DualSense"].Available ||
            snapshot["DualSense"].Source.StartsWith("DualSense HID (bağlı)",
                StringComparison.OrdinalIgnoreCase);
        if(dualSenseReplacesLaptop && dualSenseConnected)
        {
            sorted.Remove("DualSense");
            int index=sorted.IndexOf("Laptop");
            if(index>=0)sorted[index]="DualSense";
        }
        else if(!dualSenseConnected)
            sorted.Remove("DualSense");

        var eligible=sorted.Where(k=>
            k is "Laptop" or "Phone" || snapshot[k].Available ||
            (k=="DualSense" && dualSenseConnected)).ToList();
        // Do not put an unconnected/unknown device in the top three unless
        // no other eligible item is available.
        foreach(var kind in sorted)
            if(!eligible.Contains(kind))eligible.Add(kind);
        return eligible.Take(3).ToArray();
    }
}

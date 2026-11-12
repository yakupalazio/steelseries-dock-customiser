using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace SteelseriesDockCustomiser;

/// <summary>
/// Display-only machine-local device names. Never bundle discovered names,
/// Bluetooth IDs, or a developer's connected-device list in release defaults.
/// Bluetooth enumeration is read-only: no pairing, GATT subscriptions or writes.
/// </summary>
internal static class LocalBatteryDeviceNames
{
    private static readonly SemaphoreSlim Gate=new(1,1);
    private static readonly TimeSpan ScanInterval=TimeSpan.FromSeconds(45);
    private static DateTimeOffset nextScan=DateTimeOffset.MinValue;
    private static IReadOnlyDictionary<string,string> connected=
        new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);

    internal static string DefaultName(string kind)=>kind switch
    {
        "Mouse"=>UiLanguage.English?"Mouse":"Fare",
        "Phone"=>UiLanguage.English?"Phone":"Telefon",
        "DualSense"=>UiLanguage.English?"Controller":"Kontrolcü",
        "Laptop"=>UiLanguage.English?"Laptop":"Dizüstü bilgisayar",
        "Headset"=>UiLanguage.English?"Headset":"Kulaklık",
        "SpareBattery"=>UiLanguage.English?"Spare headset battery":"Yedek kulaklık pili",
        _=>kind.StartsWith("Ble:",StringComparison.OrdinalIgnoreCase)
            ?(UiLanguage.English?"Bluetooth device":"Bluetooth cihazı")
            :kind.StartsWith("Hid:",StringComparison.OrdinalIgnoreCase)
                ?(UiLanguage.English?"USB/HID device":"USB/HID cihazı")
                :BatteryDeviceCatalog.Name(kind)
    };

    internal static string DisplayName(string kind,BatterySnapshot? snapshot=null)
    {
        if(snapshot is not null && snapshot.Values.TryGetValue(kind,out var reading) &&
           BatteryPriority.IsCurrent(reading,DateTimeOffset.Now) &&
           IsUsableName(reading.FriendlyName))
            return reading.FriendlyName.Trim();
        if(connected.TryGetValue(kind,out var local) && IsUsableName(local))
            return local;
        return DefaultName(kind);
    }

    internal static bool IsUsableName(string? name)
    {
        if(string.IsNullOrWhiteSpace(name) || name.Length>120)return false;
        // A product's actual Windows name may contain Unicode. Reject
        // control codes / malformed placeholder names, not non-ASCII text.
        string trimmed=name.Trim();
        return trimmed.Length>1 &&
            !trimmed.Any(char.IsControl) &&
            !trimmed.Equals("Bluetooth device",StringComparison.OrdinalIgnoreCase) &&
            !trimmed.Equals("USB peripheral",StringComparison.OrdinalIgnoreCase);
    }

    internal static string? ClassifyConnectedBluetooth(string name)
    {
        if(!IsUsableName(name))return null;
        string s=name.ToLowerInvariant();
        if(s.Contains("dualsense") || s.Contains("dualshock") ||
           s.Contains("wireless controller") || s.Contains("xbox wireless"))
            return "DualSense";
        if(s.Contains("buds") || s.Contains("earbuds") ||
           s.Contains("watch") || s.Contains("tablet") ||
           s.Contains("galaxy tab") || s.Contains("galaxy book") ||
           s.Contains("headset") || s.Contains("headphones"))
            return null;
        if(s.Contains("iphone") || s.Contains("galaxy s") ||
           s.Contains("galaxy a") || s.Contains("galaxy m") ||
           s.Contains("galaxy f") || s.Contains("galaxy z") ||
           s.Contains("galaxy note") || s.Contains("oneplus") ||
           s.Contains("redmi") || s.Contains("xiaomi") ||
           s.Contains("poco ") || s.Contains("huawei") ||
           s.Contains("oppo") || s.Contains("vivo") ||
           s.Contains("realme") || s.Contains("motorola") ||
           s.Contains("moto g") || s.Contains("nothing phone") ||
           s.Contains("honor ") ||
           Regex.IsMatch(s,@"\bpixel\s+\d{1,2}\b") ||
           s.Contains("pixel phone") || s.Contains("android phone") ||
           s.Contains("smartphone") ||
           Regex.IsMatch(s,@"\bs(?:1[0-9]|2[0-9])(?:\+|\s+ultra|\s+fe)?(?:\b|$)"))
            return "Phone";
        return null;
    }

    internal static IReadOnlyDictionary<string,string> ResolveUnique(
        IEnumerable<(string Name,string Kind)> candidates)
    {
        // A multi-phone PC needs an explicit device association before
        // selecting one name. Never assign an arbitrary other phone's name.
        var output=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        foreach(var group in candidates.Where(item=>IsUsableName(item.Name))
            .GroupBy(item=>item.Kind,StringComparer.OrdinalIgnoreCase))
        {
            string[] distinct=group.Select(item=>item.Name.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if(distinct.Length==1)output[group.Key]=distinct[0];
        }
        return output;
    }

    internal static async Task RefreshAsync()
    {
        if(DateTimeOffset.UtcNow<nextScan)return;
        if(!await Gate.WaitAsync(0))return;
        try
        {
            if(DateTimeOffset.UtcNow<nextScan)return;
            nextScan=DateTimeOffset.UtcNow+ScanInterval;
            var result=new List<(string Name,string Kind)>();
            // Connected-only selectors avoid stale devices left in Windows'
            // paired or cached PnP history.
            await EnumerateConnected(BluetoothDevice
                .GetDeviceSelectorFromConnectionStatus(
                    BluetoothConnectionStatus.Connected),result);
            await EnumerateConnected(BluetoothLEDevice
                .GetDeviceSelectorFromConnectionStatus(
                    BluetoothConnectionStatus.Connected),result);
            connected=ResolveUnique(result);
        }
        catch
        {
            // A failed system refresh must not show another device from an
            // old cache on a shared or freshly paired machine.
            connected=new Dictionary<string,string>(
                StringComparer.OrdinalIgnoreCase);
        }
        finally{Gate.Release();}
    }

    private static async Task EnumerateConnected(string selector,
        List<(string Name,string Kind)> result)
    {
        try
        {
            var task=DeviceInformation.FindAllAsync(selector,
                ["System.Devices.Aep.IsConnected"]).AsTask();
            if(await Task.WhenAny(task,Task.Delay(3500))!=task)return;
            foreach(var device in await task)
            {
                if(device.Properties.TryGetValue(
                    "System.Devices.Aep.IsConnected",out var connection) &&
                    connection is bool active && !active)continue;
                string? kind=ClassifyConnectedBluetooth(device.Name);
                if(kind is not null)result.Add((device.Name,kind));
            }
        }
        catch{/* Missing adapter/service is not evidence of a device. */}
    }
}

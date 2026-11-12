using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HidSharp;
using HidSharp.Reports;

namespace SteelseriesDockCustomiser;

/// <summary>
/// Vendor-neutral HID Battery Strength (Usage Page 0x06, Usage 0x20).
/// Reads advertised INPUT reports only: never sends arbitrary feature/output
/// requests, never pairs devices, and never guesses a vendor-defined byte.
/// </summary>
internal static class StandardHidBatteryProvider
{
    internal const uint BatteryStrengthUsage=0x00060020;
    private static readonly SemaphoreSlim Gate=new(1,1);
    private static readonly TimeSpan DiscoveryTtl=TimeSpan.FromSeconds(60);
    private static DateTimeOffset lastScan=DateTimeOffset.MinValue;
    private static readonly List<HidDevice> CandidateDevices=new();

    public static string StableKind(string path)=>
        "Hid:"+Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(path.ToLowerInvariant())),0,7);

    internal static bool IsStandardBatteryUsage(IEnumerable<uint> usages)=>
        usages.Contains(BatteryStrengthUsage);

    public static async Task<IReadOnlyList<BatteryReading>> ReadAsync()
    {
        if(!await Gate.WaitAsync(0))return Array.Empty<BatteryReading>();
        try{return await Task.Run(ReadSynchronous);}
        catch{return Array.Empty<BatteryReading>();}
        finally{Gate.Release();}
    }

    private static IReadOnlyList<BatteryReading> ReadSynchronous()
    {
        if(DateTimeOffset.UtcNow-lastScan>DiscoveryTtl)
        {
            lastScan=DateTimeOffset.UtcNow;
            CandidateDevices.Clear();
            foreach(var device in DeviceList.Local.GetHidDevices().Take(300))
            {
                try
                {
                    var desc=device.GetReportDescriptor();
                    if(desc.InputReports.Concat(desc.FeatureReports).Any(
                        r=>r.DataItems.Any(item=>!item.IsConstant &&
                            item.IsVariable && IsStandardBatteryUsage(
                                item.Usages.GetAllValues()))))
                        CandidateDevices.Add(device);
                }
                catch { /* Descriptor unavailable, never probe unknown fields */ }
            }
        }
        var readings=new List<BatteryReading>();
        foreach(var device in CandidateDevices.Take(16))
        {
            try
            {
                var desc=device.GetReportDescriptor();
                var inputReports=desc.InputReports
                    .Where(r=>r.DataItems.Any(item=>
                        !item.IsConstant && item.IsVariable &&
                        IsStandardBatteryUsage(item.Usages.GetAllValues())))
                    .ToDictionary(r=>r.ReportID);
                var featureReports=desc.FeatureReports
                    .Where(r=>r.DataItems.Any(item=>
                        !item.IsConstant && item.IsVariable &&
                        IsStandardBatteryUsage(item.Usages.GetAllValues())))
                    .ToArray();
                if(inputReports.Count==0 && featureReports.Length==0)continue;
                if(!device.TryOpen(out HidStream? stream)||stream is null)continue;
                using(stream)
                {
                    stream.ReadTimeout=320;
                    stream.WriteTimeout=400;
                    int? percent=null;
                    string method="Feature";
                    // GetFeature is a read-only standard HID request. Only
                    // descriptors with the assigned Battery Strength usage
                    // are queried; never send proprietary commands.
                    foreach(var report in featureReports.Take(2))
                    {
                        try
                        {
                            int max=Math.Clamp(device.GetMaxFeatureReportLength(),2,512);
                            if(report.Length>max)continue;
                            var data=new byte[max];
                            data[0]=report.ReportID;
                            stream.GetFeature(data);
                            percent=DecodeStandardReport(report,data);
                            if(percent.HasValue)break;
                        }
                        catch{/* Unsupported GetFeature; fall back to input. */}
                    }
                    if(percent is null && inputReports.Count>0)
                    {
                        try
                        {
                            var bytes=new byte[Math.Clamp(
                                device.GetMaxInputReportLength(),2,512)];
                            int count=stream.Read(bytes,0,bytes.Length);
                            if(count>0 && inputReports.TryGetValue(bytes[0],out var report) &&
                               count>=report.Length)
                            {
                                percent=DecodeStandardReport(report,bytes);
                                method="Input";
                            }
                        }
                        catch(TimeoutException){}
                    }
                    if(percent is not int valid)continue;
                    string name=SafeName(device);
                    string kind=StableKind(device.DevicePath);
                    string transport=device.DevicePath.Contains("bthenum",
                        StringComparison.OrdinalIgnoreCase)?"Bluetooth":"USB";
                    string icon=BatteryDeviceClassifier.IconFor(name,transport);
                    BatteryDeviceCatalog.Register(kind,name,icon);
                    readings.Add(new BatteryReading(kind,valid,
                        "USB/Bluetooth HID Battery Strength (0x06/0x20)",
                        "Standart HID "+method+" pil raporu; güncel okuma.",
                        DateTimeOffset.Now)
                    {
                        FriendlyName=name,
                        IconKind=icon
                    });
                }
            }
            catch (TimeoutException){}
            catch (System.IO.IOException){}
            catch (UnauthorizedAccessException){}
            catch (ArgumentException){}
            catch (NotSupportedException){}
            catch { /* Failure is not a battery reading. */ }
        }
        return readings;
    }

    internal static int? DecodeStandardReport(Report report,byte[] data)
    {
        int? percentage=null;
        try
        {
            report.Read(data,0,value=>
            {
                if(!value.IsValid || value.IsNull ||
                   !IsStandardBatteryUsage(value.Usages))return;
                double fraction=value.GetFractionalValue();
                if(double.IsNaN(fraction)||fraction<0||fraction>1)return;
                percentage=(int)Math.Round(fraction*100,
                    MidpointRounding.AwayFromZero);
            });
        }
        catch{return null;}
        return percentage;
    }

    private static string SafeName(HidDevice device)
    {
        try
        {
            string name=device.GetProductName();
            if(!string.IsNullOrWhiteSpace(name))return name;
        }
        catch{}
        return $"HID {device.VendorID:X4}:{device.ProductID:X4}";
    }
}

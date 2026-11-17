using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HidSharp;

namespace SteelseriesDockCustomiser;

/// <summary>
/// Battery-only WLmouse receiver adapter. The public MIT HaloBattery
/// protocol notes describe feature query 00 00 02 02 00 83, matching
/// a1/a2 00 02 02 00 83 charging percent, plus an optional passive
/// 03 00 percent charging heartbeat. This is an independent C# reader.
/// </summary>
internal sealed class WlmouseBatteryProvider:IPeripheralBatteryProvider
{
    public string Protocol=>"WLMOUSE vendor HID 0x83 status (read-only)";
    private static readonly SemaphoreSlim Gate=new(1,1);
    private static readonly Dictionary<int,(string Name,bool QueryVerified)>
        ProductIds=new()
        {
            [0xA887]=("WLMOUSE Beast X",true),
            [0xA868]=("WLMOUSE Beast X Mini Pro",true),
            [0xA880]=("WLMOUSE Beast X Max",true),
            // Positive product identity is documented; a working battery
            // query for these newer firmware revisions is NOT verified.
            // Passive reports only until independently tested.
            [0xA870]=("WLMOUSE Beast X Pro",false),
            [0xA871]=("WLMOUSE Beast X Pro",false),
            [0xA881]=("WLMOUSE Beast Max",false),
            [0xA878]=("WLMOUSE Sword X",false),
            [0xA879]=("WLMOUSE Sword X",false)
        };
    internal static string LastDiagnostic {get;private set;}=
        "No WLmouse receiver scan yet.";

    internal static string? Model(int vid,int pid)=>
        vid==0x36A7&&ProductIds.TryGetValue(pid,out var m)
            ?m.Name:null;

    internal static bool MayQueryBattery(
        int vendorId,int productId,CompxWirelessBatteryProvider.CollectionCaps? caps)
        =>vendorId==0x36A7&&ProductIds.TryGetValue(productId,out var model)&&
            model.QueryVerified&&caps is not null &&
            caps.UsagePage==0xFFFF&&caps.Usage==0 &&
            caps.FeatureLength is >=64 and <=65;

    internal static bool MayObserveHeartbeat(
        int vendorId,int productId,CompxWirelessBatteryProvider.CollectionCaps? caps)
        =>vendorId==0x36A7&&ProductIds.ContainsKey(productId)&&
            caps is not null&&caps.UsagePage==0xFFFF &&
            caps.InputLength is >=64 and <=65;

    internal static byte[] MakePowerQuery(int length=65)
    {
        if(length is <64 or >65)
            throw new ArgumentOutOfRangeException(nameof(length));
        byte[] report=new byte[length];
        // report id 0 followed by 00 00 02 02 00 83
        report[0]=0;
        report[1]=0;
        report[2]=0;
        report[3]=2;
        report[4]=2;
        report[5]=0;
        report[6]=0x83;
        return report;
    }

    internal sealed record PowerReply(int Percent,bool Charging);
    internal static PowerReply? ParseFeature(ReadOnlySpan<byte> data)
    {
        // hidapi on Windows may include or omit the leading report-id zero.
        // Accept only a correctly echoed 0x83 query and a 0..100 percent.
        for(int offset=0;offset<=data.Length-8;offset++)
        {
            if(data[offset] is not (0xA1 or 0xA2)||
               data[offset+1]!=0||data[offset+2]!=2||
               data[offset+3]!=2||data[offset+4]!=0||
               data[offset+5]!=0x83||
               data[offset+6]>2||data[offset+7]>100)
                continue;
            return new(data[offset+7],data[offset+6]!=0);
        }
        return null;
    }

    internal static PowerReply? ParseHeartbeat(ReadOnlySpan<byte> data)
    {
        for(int offset=0;offset<=Math.Min(1,data.Length-4);offset++)
        {
            if(data[offset]!=3||data[offset+1]!=0||
               data[offset+2]>100||data[offset+3]>2)
                continue;
            return new(data[offset+2],data[offset+3]!=0);
        }
        return null;
    }

    private static string Kind(string path)=>
        "Vendor:Wlmouse:"+Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(
                path.ToLowerInvariant())),0,7);

    public async Task<IReadOnlyList<BatteryReading>> ReadAsync()
    {
        if(!await Gate.WaitAsync(0))
            return Array.Empty<BatteryReading>();
        try{return await Task.Run(ReadOnce);}
        catch{return Array.Empty<BatteryReading>();}
        finally{Gate.Release();}
    }

    private static IReadOnlyList<BatteryReading> ReadOnce()
    {
        var batteries=new List<BatteryReading>();
        int candidates=0,collections=0,responded=0;
        foreach(var device in DeviceList.Local.GetHidDevices()
            .Where(d=>Model(d.VendorID,d.ProductID)!=null).Take(40))
        {
            candidates++;
            var caps=CompxWirelessBatteryProvider.ReadCollectionCaps(
                device.DevicePath);
            bool active=MayQueryBattery(device.VendorID,device.ProductID,caps);
            bool passive=MayObserveHeartbeat(
                device.VendorID,device.ProductID,caps);
            if(!active&&!passive)continue;
            collections++;
            if(!device.TryOpen(out HidStream? stream)||stream is null)
                continue;
            using(stream)
            {
                try
                {
                    PowerReply? result=null;
                    if(active)
                    {
                        byte[] query=MakePowerQuery(caps!.FeatureLength);
                        stream.SetFeature(query); // status-only 0x83 inquiry
                        for(int n=0;n<2;n++)
                        {
                            var reply=new byte[caps.FeatureLength];
                            reply[0]=0;
                            stream.GetFeature(reply);
                            result=ParseFeature(reply);
                            if(result is not null)break;
                        }
                    }
                    if(result is null && !active &&
                       caps!.FeatureLength is >=64 and <=65)
                    {
                        // Exact newer WLMOUSE receivers (e.g. Beast X Pro)
                        // do not have a verified 0x83 output/query handshake.
                        // Try a standard HID GET_FEATURE read only; never
                        // send the older mouse's control packet blindly.
                        try
                        {
                            var report=new byte[caps.FeatureLength];
                            report[0]=0;
                            stream.GetFeature(report);
                            result=ParseFeature(report);
                        }
                        catch(Exception ex) when(ex is IOException or
                            TimeoutException or InvalidOperationException or
                            NotSupportedException){}
                    }
                    if(result is null&&passive)
                    {
                        stream.ReadTimeout=180;
                        byte[] raw=new byte[caps!.InputLength];
                        // Some dongles queue unrelated notifications first;
                        // accept only a validated battery heartbeat.
                        for(int attempt=0;attempt<3;attempt++)
                        {
                            try
                            {
                                int count=stream.Read(raw,0,raw.Length);
                                if(count>0)
                                    result=ParseHeartbeat(raw.AsSpan(0,count));
                                if(result is not null)break;
                            }
                            catch(TimeoutException){break;}
                        }
                    }
                    if(result is null)continue;
                    responded++;
                    string name=Model(device.VendorID,device.ProductID)!;
                    string kind=Kind(device.DevicePath);
                    BatteryDeviceCatalog.Register(kind,name,"Mouse");
                    batteries.Add(new BatteryReading(kind,result.Percent,
                        active?"WLmouse 0x83 battery response":
                            "WLmouse passive battery heartbeat",
                        $"Live receiver {device.VendorID:X4}:{device.ProductID:X4}; "+
                        (active?"validated echo":"validated status bytes"),
                        DateTimeOffset.Now,result.Charging)
                    {FriendlyName=name,IconKind="Mouse"});
                }
                catch(Exception ex) when(ex is IOException or
                    InvalidOperationException or UnauthorizedAccessException
                    or TimeoutException or NotSupportedException
                    or ArgumentException)
                { /* Receiver sleeping or held by vendor application. */ }
            }
        }
        LastDiagnostic=$"WLmouse: {candidates} interfaces; "+
            $"{collections} eligible collections; {responded} live replies.";
        return batteries.DistinctBy(x=>x.Kind).ToArray();
    }
}

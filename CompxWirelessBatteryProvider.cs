using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HidSharp;
using Microsoft.Win32.SafeHandles;

namespace SteelseriesDockCustomiser;

/// <summary>
/// Documented CompX power-status inquiry used by selected ATK/VXE/Pulsar
/// wireless receivers. A HID output report is needed for the query, but
/// command 0x04 does NOT set DPI, change firmware, pair, configure or flash
/// the mouse. Never send anything to an unknown VID/PID or HID collection.
/// Protocol facts independently described in HaloBattery/docs/protocols.md,
/// which credits hardware owners and public ATK/Pulsar tooling.
/// </summary>
internal sealed class CompxWirelessBatteryProvider:IPeripheralBatteryProvider
{
    public string Protocol=>"CompX read-only HID power query (0x08/0x04)";

    private const ushort ControlUsagePage=0xFF02;
    private const ushort ControlUsage=0x0002;
    private const byte ReportId=0x08,PowerCommand=0x04;
    private const int FrameLength=17;
    private static readonly SemaphoreSlim Gate=new(1,1);
    private static readonly object DiagnosticLock=new();
    private static string lastDiagnostic=
        "Compx wireless battery reader has not scanned.";

    internal static string LastDiagnostic
    {
        get{lock(DiagnosticLock)return lastDiagnostic;}
        private set{lock(DiagnosticLock)lastDiagnostic=value;}
    }

    internal sealed record SupportedReceiver(
        int VendorId,int ProductId,string Label,string DeviceType,
        bool ConfirmedOnReferenceHardware);

    // Strict positive allowlist: a family-wide VID alone NEVER authorizes a
    // HID output request. "Confirmed" is from third-party hardware reports,
    // not this app's own tests on the user's hardware.
    internal static readonly IReadOnlyList<SupportedReceiver> Receivers=[
        new(0x373B,0x101A,"ATK Mouse 1K Dongle","Mouse",true),
        new(0x373B,0x101B,"ATK Fire X 8K receiver","Mouse",false),
        new(0x373B,0x1085,"ATK/VXE R1 SE+ 2.4G","Mouse",false),
        new(0x373B,0x11D9,"ATK A9 Ultimate 2.4G","Mouse",false),
        new(0x3554,0xF58A,"ATK/VXE R1 Pro Max 2.4G","Mouse",true),
        new(0x3554,0xF58C,"ATK/VXE R1 Pro Max (wired)","Mouse",true),
        new(0x3554,0xF58F,"ATK/VXE R1 SE+ (wired)","Mouse",false),
        new(0x3554,0xF508,"Pulsar X2 V2 Mini 2.4G","Mouse",false),
        new(0x3554,0xF507,"Pulsar X2 V2 Mini (wired)","Mouse",false),
        new(0x3554,0xF505,"VGN Dragonfly 4K receiver","Mouse",true),
        new(0x3554,0xF506,"VGN Dragonfly 4K (wired)","Mouse",false),
        new(0x3554,0xFA09,"CompX keyboard 2.4G receiver","Keyboard",false),
        new(0x3554,0xF523,"Teevolution Terra Pro receiver","Keyboard",false),
        new(0x3554,0xF5D5,"Redragon M991 / K1NG Max receiver","Mouse",true),
        new(0x3554,0xF55E,"Redragon M991 (wired)","Mouse",true),
        new(0x3554,0xF5F7,"Scyrox 8K receiver","Mouse",false),
        new(0x3554,0xF5F6,"Scyrox (wired)","Mouse",false),
        new(0x3710,0x5406,"Pulsar X2 CrazyLight Mini 2.4G","Mouse",true),
        new(0x3710,0x3414,"Pulsar X2 CrazyLight Mini (wired)","Mouse",true),
        new(0x3770,0x0200,"Hitscan Hyperlight 2.4G","Mouse",false),
        new(0x3770,0x0300,"Hitscan Hyperlight 8K","Mouse",true),
        new(0x3770,0x0100,"Hitscan Hyperlight (wired)","Mouse",false),
        new(0x33E4,0x3854,"G-Wolves 8K wireless receiver","Mouse",true),
        new(0x1B1C,0x2B2A,"Corsair Sabre v2 Pro Ultralight","Mouse",false),
        new(0x3554,0xF5DF,"Cougar Revenger Pro 4K receiver","Mouse",false),
        new(0x3554,0xF5DE,"Cougar Revenger Pro 4K (wired)","Mouse",false),
        new(0x3554,0xF5DC,"Cougar Revenger Pro 2.4G","Mouse",false)
    ];

    internal static SupportedReceiver? FindReceiver(int vendorId,int productId)
        =>Receivers.FirstOrDefault(x=>x.VendorId==vendorId&&
            x.ProductId==productId);

    internal static byte ComputeChecksum(ReadOnlySpan<byte> bytes)
    {
        int total=0;
        foreach(byte b in bytes)total=(total+b)&0xFF;
        return unchecked((byte)(0x55-total));
    }

    /// <summary>Creates a status-only report; no configuration commands.</summary>
    internal static byte[] MakePowerQuery()
    {
        var result=new byte[FrameLength];
        result[0]=ReportId;
        result[1]=PowerCommand;
        result[16]=ComputeChecksum(result.AsSpan(0,16));
        return result;
    }

    internal sealed record PowerReply(int Percent,bool OnCable,int? MilliVolts);

    internal static PowerReply? ParsePowerReply(
        ReadOnlySpan<byte> data)
    {
        if(data.Length<FrameLength||data[0]!=ReportId||
           data[1]!=PowerCommand ||
           data[16]!=ComputeChecksum(data[..16]) ||
           data[6]>100)return null;
        int mv=(data[8]<<8)|data[9];
        return new(data[6],data[7]!=0,
            mv is >=2000 and <=6000 ? mv : null);
    }

    [DllImport("kernel32.dll",EntryPoint="CreateFileW",
        CharSet=CharSet.Unicode,SetLastError=true)]
    private static extern SafeFileHandle CreateFile(
        [MarshalAs(UnmanagedType.LPWStr)]string fileName,
        uint desiredAccess,uint shareMode,IntPtr securityAttributes,
        uint creationDisposition,uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("hid.dll",SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool HidD_GetPreparsedData(
        SafeFileHandle device,out IntPtr preparsed);

    [DllImport("hid.dll")]
    private static extern int HidP_GetCaps(
        IntPtr preparsed,IntPtr capabilities);

    [DllImport("hid.dll",SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool HidD_FreePreparsedData(IntPtr preparsed);

    internal sealed record CollectionCaps(
        int UsagePage,int Usage,int InputLength,int OutputLength,
        int FeatureLength=0);

    /// <summary>
    /// Read-only HidP_GetCaps call against this one HID collection. Not an
    /// attempt to open the mouse for control or issue a feature report.
    /// </summary>
    internal static CollectionCaps? ReadCollectionCaps(string devicePath)
    {
        if(!OperatingSystem.IsWindows()||string.IsNullOrWhiteSpace(devicePath))
            return null;
        try
        {
            using var handle=CreateFile(devicePath,0,3,IntPtr.Zero,
                3,0,IntPtr.Zero);
            if(handle.IsInvalid||!HidD_GetPreparsedData(handle,out var pp)||
               pp==IntPtr.Zero)return null;
            try
            {
                IntPtr caps=Marshal.AllocHGlobal(128);
                try
                {
                    for(int i=0;i<128;i+=4)
                        Marshal.WriteInt32(caps,i,0);
                    // HIDP_STATUS_SUCCESS.
                    if(HidP_GetCaps(pp,caps)!=0x00110000)return null;
                    ushort usage=unchecked((ushort)Marshal.ReadInt16(caps,0));
                    ushort page=unchecked((ushort)Marshal.ReadInt16(caps,2));
                    ushort input=unchecked((ushort)Marshal.ReadInt16(caps,4));
                    ushort output=unchecked((ushort)Marshal.ReadInt16(caps,6));
                    ushort feature=unchecked((ushort)Marshal.ReadInt16(caps,8));
                    return new(page,usage,input,output,feature);
                }
                finally{Marshal.FreeHGlobal(caps);}
            }
            finally{HidD_FreePreparsedData(pp);}
        }
        catch{return null;}
    }

    internal static bool IsSafeCollection(SupportedReceiver? known,
        CollectionCaps? caps)=>
        known is not null && caps is not null &&
        caps.UsagePage==ControlUsagePage &&
        caps.Usage==ControlUsage &&
        caps.InputLength>=FrameLength && caps.InputLength<=512 &&
        // The documented output frame is exactly 17 bytes. A different
        // report size is another protocol until verified on hardware.
        caps.OutputLength==FrameLength;

    internal static string StableKind(string path)=>
        "Vendor:Compx:"+Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(path.ToLowerInvariant())),0,7);

    // Enumerate only known vendor/product IDs. Reading the interface
    // metadata does not send any HID command or interfere with vendor apps.
    internal static IReadOnlyList<string> DescribeDetectedInterfaces()
    {
        var rows=new List<string>();
        foreach(var device in DeviceList.Local.GetHidDevices()
            .Where(x=>FindReceiver(x.VendorID,x.ProductID) is not null)
            .Take(80))
        {
            var known=FindReceiver(device.VendorID,device.ProductID)!;
            var caps=ReadCollectionCaps(device.DevicePath);
            string state=caps is null?"caps unavailable":
                $"{caps.UsagePage:X4}:{caps.Usage:X4} input={caps.InputLength} "+
                $"output={caps.OutputLength}";
            rows.Add($"{device.VendorID:X4}:{device.ProductID:X4} "+
                $"{known.Label} | {state} | "+
                (IsSafeCollection(known,caps)?"safe power-query eligible":
                 "no safe power-query collection"));
        }
        return rows;
    }

    public async Task<IReadOnlyList<BatteryReading>> ReadAsync()
    {
        if(!await Gate.WaitAsync(0))
            return Array.Empty<BatteryReading>();
        try{return await Task.Run(ReadSynchronous);}
        catch(Exception e)
        {
            LastDiagnostic="CompX scanner: "+e.GetType().Name;
            return Array.Empty<BatteryReading>();
        }
        finally{Gate.Release();}
    }

    private static IReadOnlyList<BatteryReading> ReadSynchronous()
    {
        var readings=new List<BatteryReading>();
        int candidates=0,eligible=0,responded=0,unavailable=0;
        int queriesSent=0,unsolicited=0,timedOut=0;
        foreach(var device in DeviceList.Local.GetHidDevices()
            .Where(x=>FindReceiver(x.VendorID,x.ProductID) is not null)
            .Take(80))
        {
            candidates++;
            var known=FindReceiver(device.VendorID,device.ProductID);
            var caps=ReadCollectionCaps(device.DevicePath);
            if(!IsSafeCollection(known,caps))continue;
            eligible++;
            if(!device.TryOpen(out HidStream? stream)||stream is null)
            {
                unavailable++;
                continue;
            }
            using(stream)
            {
                try
                {
                    stream.ReadTimeout=240;
                    stream.WriteTimeout=450;
                    // Drain stale input reports before the only approved
                    // status inquiry; never interpret unsolicited events.
                    var input=new byte[caps!.InputLength];
                    for(int i=0;i<2;i++)
                    {
                        stream.ReadTimeout=25;
                        try
                        {
                            if(stream.Read(input,0,input.Length)==0)break;
                        }
                        catch(TimeoutException){break;}
                    }
                    stream.ReadTimeout=260;
                    var query=new byte[caps.OutputLength];
                    MakePowerQuery().CopyTo(query,0);
                    stream.Write(query,0,query.Length);
                    queriesSent++;
                    PowerReply? result=null;
                    for(int attempt=0;attempt<4;attempt++)
                    {
                        int count;
                        try{count=stream.Read(input,0,input.Length);}
                        catch(TimeoutException)
                        {
                            timedOut++;
                            break;
                        }
                        if(count<FrameLength)
                        {
                            unsolicited++;
                            continue;
                        }
                        result=ParsePowerReply(input.AsSpan(0,count));
                        if(result is not null)break;
                        unsolicited++;
                    }
                    if(result is null)continue;
                    responded++;
                    string product;
                    try{product=device.GetProductName();}
                    catch{product="";}
                    if(string.IsNullOrWhiteSpace(product))
                        product=known!.Label;
                    if(product.Length>80)product=product[..80];
                    string kind=StableKind(device.DevicePath);
                    var reading=new BatteryReading(kind,result.Percent,
                        "CompX HID power status (live 0x04)",
                        $"Validated checksum; connected receiver "+
                        $"{device.VendorID:X4}:{device.ProductID:X4}"+
                        (result.MilliVolts is int mv?$"; {mv} mV":""),
                        DateTimeOffset.Now,result.OnCable)
                    {
                        FriendlyName=product,
                        IconKind=known!.DeviceType
                    };
                    BatteryDeviceCatalog.Register(kind,product,
                        known.DeviceType);
                    readings.Add(reading);
                }
                catch(Exception e) when(e is IOException or
                    TimeoutException or UnauthorizedAccessException or
                    InvalidOperationException or ArgumentException or
                    NotSupportedException)
                {
                    unavailable++;
                }
            }
        }
        LastDiagnostic=$"CompX: {candidates} HID interfaces, "+
            $"{eligible} safe 17-byte control collections, "+
            $"{queriesSent} status queries, "+
            $"{responded} valid battery replies"+
            (timedOut>0?$", {timedOut} read timeouts":"")+
            (unsolicited>0?$", {unsolicited} unrelated/invalid frames":"")+
            (unavailable>0?$", {unavailable} busy/inaccessible":"")+
            (queriesSent>0&&responded==0
                ?". Mouse may be sleeping: move it physically and refresh."
                :".");
        return readings.DistinctBy(r=>r.Kind,StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}

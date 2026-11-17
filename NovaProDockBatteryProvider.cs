using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HidSharp;

namespace SteelseriesDockCustomiser;

/// <summary>
/// Fallback for genuine Nova Pro Wireless dock USB IDs 1038:12E0/12E5.
/// Reads a live battery report with the documented 06 B0 status request.
/// This is NOT a firmware/configuration/OLED/mixer command. Do not send
/// requests to non-allowlisted endpoints, even if their brand is SteelSeries.
/// Returned levels are nine-step APPROXIMATIONS, not exact percentages.
/// </summary>
internal sealed class NovaProDockBatteryProvider:IPeripheralBatteryProvider
{
    private const int VendorId=0x1038;
    private static readonly SemaphoreSlim Gate=new(1,1);
    public string Protocol=>"Nova Pro Wireless dock 06 B0 battery status";
    internal static string LastDiagnostic {get;private set;}=
        "Nova Pro dock HID pil okuyucusu henüz denenmedi.";

    internal static bool SupportedIdentity(int vid,int pid,int page,int usage,
        string? path,int inputLength,int outputLength)
    {
        if(vid!=VendorId||pid is not (0x12E0 or 0x12E5)||
           page is not (0xFFC0 or 0xFF00)||usage!=1||
           inputLength is <16 or >128||outputLength is <16 or >128||
           string.IsNullOrWhiteSpace(path))
            return false;
        return path.Contains("mi_03",StringComparison.OrdinalIgnoreCase)||
               path.Contains("mi_04",StringComparison.OrdinalIgnoreCase);
    }

    internal static byte[] MakeStatusRequest(int length)
    {
        if(length is <16 or >128)
            throw new ArgumentOutOfRangeException(nameof(length));
        byte[] result=new byte[length];
        result[0]=0x06;
        result[1]=0xB0;
        return result;
    }

    internal sealed record Status(int? Headset,int? Spare,bool Charging,
        bool HeadsetConnected);
    internal static Status? ParseStatus(ReadOnlySpan<byte> report)
    {
        int start=report.Length>=17&&report[0]==0&&
            report[1]==0x06&&report[2]==0xB0?1:0;
        if(report.Length-start<16||report[start]!=0x06||
           report[start+1]!=0xB0)return null;
        int rawHeadset=report[start+6],rawSpare=report[start+7];
        byte status=report[start+15];
        if(rawHeadset>8||rawSpare>8||
           status is not (0x01 or 0x02 or 0x08))
            return null;
        bool connected=status is 0x02 or 0x08;
        int? headset=connected
            ?(int)Math.Floor(rawHeadset*100.0/8):null;
        // The dongle can report zero when the replacement battery slot is
        // empty; do not confuse absence with 0% remaining.
        int? spare=rawSpare is >=1 and <=8
            ?(int)Math.Floor(rawSpare*100.0/8):null;
        return new(headset,spare,status==0x02,connected);
    }

    public async Task<IReadOnlyList<BatteryReading>> ReadAsync()
    {
        if(!await Gate.WaitAsync(0).ConfigureAwait(false))
            return Array.Empty<BatteryReading>();
        try{return await Task.Run(ReadOnce).ConfigureAwait(false);}
        catch{return Array.Empty<BatteryReading>();}
        finally{Gate.Release();}
    }

    private static IReadOnlyList<BatteryReading> ReadOnce()
    {
        var results=new List<BatteryReading>();
        int found=0,eligible=0;
        string lastError="";
        try
        {
            var candidates=DeviceList.Local.GetHidDevices(VendorId)
                .Where(d=>d.ProductID is 0x12E0 or 0x12E5)
                .OrderByDescending(d=>
                {
                    try{
                        var caps=CompxWirelessBatteryProvider
                            .ReadCollectionCaps(d.DevicePath);
                        return caps?.UsagePage==0xFFC0?2:0;
                    }catch{return 0;}
                }).Take(12).ToArray();
            found=candidates.Length;
            foreach(var device in candidates)
            {
                var caps=CompxWirelessBatteryProvider.ReadCollectionCaps(
                    device.DevicePath);
                if(caps is null||!SupportedIdentity(
                    device.VendorID,device.ProductID,caps.UsagePage,
                    caps.Usage,device.DevicePath,caps.InputLength,
                    caps.OutputLength))continue;
                eligible++;
                try
                {
                    if(!device.TryOpen(out HidStream? stream)||stream is null)
                    {
                        lastError="HID erişimi reddedildi; GG dock'u kullanıyor olabilir.";
                        continue;
                    }
                    using(stream)
                    {
                        stream.ReadTimeout=180;
                        stream.WriteTimeout=300;
                        stream.Write(MakeStatusRequest(caps.OutputLength));
                        Status? status=null;
                        // GG may be on the same collection. Skip its unrelated
                        // 07 25/07 45 notifications; only 06 B0 is a level.
                        byte[] buffer=new byte[caps.InputLength];
                        for(int attempts=0;attempts<6;attempts++)
                        {
                            int count;
                            try{count=stream.Read(buffer,0,buffer.Length);}
                            catch(TimeoutException){break;}
                            if(count<=0)continue;
                            status=ParseStatus(buffer.AsSpan(0,count));
                            if(status is not null)break;
                        }
                        if(status is null)continue;
                        var now=DateTimeOffset.Now;
                        if(status.Headset is int primary)
                            results.Add(new BatteryReading(
                                "Vendor:NovaPro:Headset",primary,
                                "Nova Pro dock HID (yaklaşık, 12,5% adım)",
                                "Dock'un güncel 06 B0 durum raporu; yaklaşık seviye.",
                                now,status.Charging)
                            {
                                FriendlyName="SteelSeries Arctis Nova Pro Wireless",
                                IconKind="Headset"
                            });
                        if(status.Spare is int spare)
                            results.Add(new BatteryReading(
                                "Vendor:NovaPro:SpareBattery",spare,
                                "Nova Pro dock HID (yaklaşık, 12,5% adım)",
                                "Dock şarj yuvası 06 B0 durumu; yaklaşık seviye.",
                                now)
                            {
                                FriendlyName="Arctis Nova Pro Wireless - Spare Battery",
                                IconKind="SpareBattery"
                            });
                        if(results.Count>0)break;
                    }
                }
                catch(Exception ex) when(ex is IOException or
                    UnauthorizedAccessException or TimeoutException or
                    InvalidOperationException or NotSupportedException or
                    ArgumentException)
                {lastError=ex.GetType().Name;}
            }
        }
        catch(Exception ex){lastError=ex.GetType().Name;}
        LastDiagnostic=results.Count>0
            ?"Nova Pro dock'tan canlı yaklaşık pil değeri alındı."
            :found==0
            ?"1038:12E0/12E5 Nova Pro dock HID cihazı bulunamadı."
            :eligible==0
            ?"Nova Pro dock var ama 06 B0 uyumlu HID koleksiyonu bulunamadı."
            :"Dock pil sorgusu yanıtlanmadı"+
                (lastError.Length>0?" ("+lastError+")":"")+".";
        return results;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HidSharp;

namespace SteelseriesDockCustomiser;

internal enum DialView { Volume, ChatMix }

internal readonly record struct DialEvent(DialView View, int? Volume, int? Game, int? Chat)
{
    // The Nova Pro sends native attenuation steps, not a percentage delta.
    // 0 is maximum volume, 56 is minimum volume.
    public int? VolumeAttenuationSteps { get; init; }
}

internal readonly record struct DialSnapshot(
    DialView View, int? Volume, int? Game, int? Chat,
    DateTimeOffset UpdatedAt, string Status)
{
    public bool HasData => Volume.HasValue || Game.HasValue && Chat.HasValue;
    public int? VolumeAttenuationSteps { get; init; }
}

/// <summary>
/// Listens to Nova Pro Wireless unsolicited wheel notifications on HID
/// interface 4. This provider never sends reports, changes audio volumes,
/// enables ChatMix, polls firmware, or claims ownership of the OLED display.
/// SteelSeries GG may already own the input interface; in that case the
/// widget explicitly reports unavailable instead of inventing measurements.
/// </summary>
internal sealed class ArctisDialMonitor : IDisposable
{
    private const int Vendor = 0x1038;
    private readonly object gate = new();
    private CancellationTokenSource? cancellation;
    private readonly List<HidStream> streams = new();
    private Task? worker;
    private DialSnapshot current = new(DialView.Volume, null, null, null,
        DateTimeOffset.MinValue, "Tekerlek bekleniyor");

    /// <summary>Unsolicited hardware event. Handlers must marshal to the UI.</summary>
    public event Action? Updated;

    public DialSnapshot Snapshot
    {
        get { lock (gate) return current; }
    }

    internal static bool TryDecode(ReadOnlySpan<byte> report, out DialEvent value)
    {
        value = default;
        int offset = report.Length > 1 && report[0] == 0 && report[1] == 7 ? 1 : 0;
        if (report.Length < offset + 3 || report[offset] != 0x07) return false;
        switch (report[offset + 1])
        {
            case 0x25 when report[offset + 2] <= 56:
                int volume = (int)Math.Round((56 - report[offset + 2]) * 100d / 56,
                    MidpointRounding.AwayFromZero);
                value = new DialEvent(DialView.Volume, volume, null, null)
                    { VolumeAttenuationSteps = report[offset + 2] };
                return true;
            case 0x45 when report.Length >= offset + 4 &&
                report[offset + 2] <= 100 && report[offset + 3] <= 100:
                value = new DialEvent(DialView.ChatMix, null,
                    report[offset + 2], report[offset + 3]);
                return true;
            default: return false;
        }
    }

    private static bool Supported(HidDevice d) =>
        d.VendorID == Vendor && d.ProductID is 0x12e0 or 0x12e5 or 0x225d;

    private static HidDevice[] FindInputs()
    {
        return DeviceList.Local.GetHidDevices(Vendor)
            .Where(Supported)
            .Where(d => d.DevicePath.Contains("mi_04", StringComparison.OrdinalIgnoreCase))
            .Where(d =>
            {
                try { return d.GetMaxInputReportLength() is >= 4 and <= 128; }
                catch { return false; }
            }).OrderByDescending(d =>
            {
                try { return d.GetMaxFeatureReportLength(); }
                catch { return 0; }
            }).ToArray();
    }

    public static string ReadOnlyDeviceCheck()
    {
        try
        {
            var found = FindInputs();
            if (found.Length == 0)
            {
                var inventory = DeviceList.Local.GetHidDevices(Vendor)
                    .Where(Supported)
                    .Select(d =>
                    {
                        try
                        {
                            var path = d.DevicePath.ToLowerInvariant();
                            var at = path.IndexOf("mi_", StringComparison.Ordinal);
                            var iface = at >= 0 && at + 5 <= path.Length
                                ? path.Substring(at, 5) : "unknown";
                            return $"{iface}:{d.GetMaxInputReportLength()}";
                        }
                        catch { return "unknown"; }
                    });
                return "Tekerlek HID arayüzü bulunamadı (görülen: " +
                    string.Join(",", inventory) + ")";
            }
            return $"Nova Pro Wireless tekerlek HID arayüzleri: {found.Length} (salt-okunur)";
        }
        catch (Exception ex) { return "HID keşfi başarısız: " + ex.GetType().Name; }
    }

    public static string ReadOnlyOpenCheck()
    {
        try
        {
            var devices=FindInputs();
            if(devices.Length==0)return "Nova Pro tekerlek HID bulunamadı";
            int openedCount=0;
            foreach(var device in devices)
            {
                using var opened=device.Open();
                openedCount++;
            }
            return $"Nova Pro tekerlek HID aç/kapat: {openedCount} koleksiyon; komut gönderilmedi";
        }
        catch(Exception ex)
        {
            return "Nova Pro tekerlek HID açılamadı: "+ex.GetType().Name;
        }
    }

    /// <summary>
    /// Diagnostic only: record the complete raw input notifications, even
    /// those that do not match our decoder. Never sends a device command.
    /// </summary>
    internal static void ReadOnlyRawWatch(TimeSpan duration,Action<string> log,
        bool includeAllInterfaces=false)
    {
        var candidates=DeviceList.Local.GetHidDevices(Vendor)
            .Where(Supported).ToArray();
        foreach(var candidate in candidates)
        {
            try
            {
                string path=candidate.DevicePath;
                int at=path.IndexOf("mi_",StringComparison.OrdinalIgnoreCase);
                string iface=at>=0&&at+5<=path.Length?path.Substring(at,5):"unknown";
                log($"HID {iface} input={candidate.GetMaxInputReportLength()} "+
                    $"feature={candidate.GetMaxFeatureReportLength()}");
            }
            catch(Exception ex){log("HID inventory: "+ex.GetType().Name);}
        }
        var found=includeAllInterfaces
            ?candidates.Where(d=>
            {
                try{return d.GetMaxInputReportLength() is >=2 and <=128;}
                catch{return false;}
            }).ToArray()
            :FindInputs();
        if(found.Length==0){log("No candidate input collections");return;}
        int seen=0;
        var watch=System.Diagnostics.Stopwatch.StartNew();
        var expires=DateTimeOffset.UtcNow+duration;
        object logGate=new();
        Task.WaitAll(found.Select((device,index)=>Task.Run(()=>
        {
            try
            {
                using var opened=device.Open();
                opened.ReadTimeout=300;
                var buffer=new byte[Math.Max(65,device.GetMaxInputReportLength())];
                string path=device.DevicePath;
                int at=path.IndexOf("mi_",StringComparison.OrdinalIgnoreCase);
                string iface=at>=0&&at+5<=path.Length?path.Substring(at,5):"unknown";
                lock(logGate)log($"Opened collection {index+1}, {iface}, feature={device.GetMaxFeatureReportLength()}");
                while(DateTimeOffset.UtcNow<expires)
                {
                    try
                    {
                        int count=opened.Read(buffer,0,buffer.Length);
                        if(count<=0)continue;
                        var raw=buffer.AsSpan(0,count);
                        bool decoded=TryDecode(raw,out var ev);
                        int n=Interlocked.Increment(ref seen);
                        if(n<=100)
                        {
                            string row=$"T={watch.ElapsedMilliseconds}ms RAW collection={index+1} packet={n} bytes={count} "+
                                Convert.ToHexString(raw[..Math.Min(24,count)])+
                                (decoded?$" decoded={ev.View} vol={ev.Volume} "+
                                    $"steps={ev.VolumeAttenuationSteps} G={ev.Game} C={ev.Chat}":
                                    " unrecognized");
                            lock(logGate)log(row);
                        }
                    }
                    catch(TimeoutException){ /* no event in this interval */ }
                }
            }
            catch(Exception ex){lock(logGate)log($"Collection {index+1} read failed: {ex.GetType().Name}");}
        })).ToArray());
        log($"Raw packets received: {seen}");
    }

    public void Start()
    {
        lock (gate)
        {
            if (cancellation != null) return;
            var source = new CancellationTokenSource();
            cancellation = source;
            worker = Task.Run(() => Listen(source.Token));
        }
    }

    private void Listen(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var devices = FindInputs();
                if (devices.Length == 0)
                    throw new InvalidOperationException("Tekerlek HID arayüzü yok");
                Task.WaitAll(devices.Select(device =>
                    Task.Run(() => ListenDevice(device, token), token)).ToArray());
            }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested)
                    lock (gate) current = current with
                    {
                        Status = "Tekerlek okunamıyor: " + ex.GetType().Name
                    };
            }
            // Keep checking new HID collections after a disconnection.
            if (token.WaitHandle.WaitOne(TimeSpan.FromSeconds(2))) break;
        }
    }

    private void ListenDevice(HidDevice device, CancellationToken token)
    {
        HidStream? opened = null;
        try
        {
                opened = device.Open();
                opened.ReadTimeout = 650;
                lock (gate)
                {
                    if (token.IsCancellationRequested) return;
                    streams.Add(opened);
                    current = current with { Status = "Tekerlek hareketi bekleniyor" };
                }
                var buffer = new byte[Math.Max(65, device.GetMaxInputReportLength())];
                while (!token.IsCancellationRequested)
                {
                    int count;
                    try { count = opened.Read(buffer, 0, buffer.Length); }
                    catch (TimeoutException) { continue; }
                    if (count <= 0) continue;
                    if (!TryDecode(buffer.AsSpan(0, count), out var ev)) continue;
                    lock (gate)
                    {
                        current = new DialSnapshot(ev.View,
                            ev.Volume ?? current.Volume,
                            ev.Game ?? current.Game,
                            ev.Chat ?? current.Chat,
                            DateTimeOffset.UtcNow, "Dock tekerleği (canlı)")
                            { VolumeAttenuationSteps=ev.VolumeAttenuationSteps??
                                current.VolumeAttenuationSteps };
                    }
                    try{Updated?.Invoke();}
                    catch(ObjectDisposedException){ /* Form is closing. */ }
                    catch(InvalidOperationException){ /* Form handle is gone. */ }
                }
        }
        catch (Exception ex)
            {
                // Closing the stream on user-requested Stop raises an expected
                // read exception; observe it instead of faulting the worker.
                if (!token.IsCancellationRequested)
                    lock (gate) current = current with
                    {
                        Status = "Tekerlek okunamıyor: " + ex.GetType().Name
                    };
            }
        finally
            {
                lock (gate)
                {
                    if (opened!=null)streams.Remove(opened);
                }
                opened?.Dispose();
            }
    }

    public void Stop()
    {
        CancellationTokenSource? stop;
        HidStream[] close;
        lock (gate)
        {
            stop = cancellation;
            cancellation = null;
            close = streams.ToArray();
            streams.Clear();
        }
        stop?.Cancel();
        foreach(var stream in close)stream.Dispose();
        // The worker exits after its read timeout. Do not block the UI thread.
    }

    public void Dispose() => Stop();
}

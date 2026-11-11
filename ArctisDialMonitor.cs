using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HidSharp;

namespace SteelseriesDockCustomiser;

internal enum DialView { Volume, ChatMix }

internal readonly record struct DialEvent(DialView View, int? Volume, int? Game, int? Chat);

internal readonly record struct DialSnapshot(
    DialView View, int? Volume, int? Game, int? Chat,
    DateTimeOffset UpdatedAt, string Status)
{
    public bool HasData => Volume.HasValue || Game.HasValue && Chat.HasValue;
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
    private HidStream? stream;
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
                value = new DialEvent(DialView.Volume, volume, null, null);
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

    private static HidDevice? FindInput()
    {
        return DeviceList.Local.GetHidDevices(Vendor)
            .Where(Supported)
            .Where(d => d.DevicePath.Contains("mi_04", StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault(d =>
            {
                try { return d.GetMaxInputReportLength() is >= 4 and <= 128; }
                catch { return false; }
            });
    }

    public static string ReadOnlyDeviceCheck()
    {
        try
        {
            var found = FindInput();
            if (found is null)
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
            return "Nova Pro Wireless tekerlek HID arayüzü algılandı (salt-okunur)";
        }
        catch (Exception ex) { return "HID keşfi başarısız: " + ex.GetType().Name; }
    }

    public static string ReadOnlyOpenCheck()
    {
        try
        {
            var device=FindInput();
            if(device is null)return "Nova Pro tekerlek HID bulunamadı";
            using var opened=device.Open();
            return "Nova Pro tekerlek HID aç/kapat başarılı; komut gönderilmedi";
        }
        catch(Exception ex)
        {
            return "Nova Pro tekerlek HID açılamadı: "+ex.GetType().Name;
        }
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
            HidStream? opened = null;
            try
            {
                var device = FindInput();
                if (device is null)
                    throw new InvalidOperationException("Tekerlek HID arayüzü yok");
                opened = device.Open();
                opened.ReadTimeout = 650;
                lock (gate)
                {
                    if (token.IsCancellationRequested) return;
                    stream = opened;
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
                            DateTimeOffset.UtcNow, "Dock tekerleği (canlı)");
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
                    if (ReferenceEquals(stream, opened)) stream = null;
                }
                opened?.Dispose();
            }
            if (token.WaitHandle.WaitOne(TimeSpan.FromSeconds(15))) break;
        }
    }

    public void Stop()
    {
        CancellationTokenSource? stop;
        HidStream? close;
        lock (gate)
        {
            stop = cancellation;
            cancellation = null;
            close = stream;
            stream = null;
        }
        stop?.Cancel();
        close?.Dispose();
        // The worker exits after its read timeout. Do not block the UI thread.
    }

    public void Dispose() => Stop();
}

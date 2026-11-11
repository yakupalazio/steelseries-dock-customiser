using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SteelseriesDockCustomiser;

/// <summary>
/// Read-only, throttled Windows SetupAPI fallback diagnostics.
/// DEVPKEY_Bluetooth_BatteryPercent may remain cached after disconnect,
/// so its values are NEVER considered eligible for an OLED percentage until
/// another independently verified live source confirms them.
/// </summary>
internal static class SetupApiBatteryDiagnostics
{
    private static readonly SemaphoreSlim ScanGate=new(1,1);
    private static IReadOnlyList<PnpBattery> cached=Array.Empty<PnpBattery>();
    private static long lastScanTicks;

    public static void Invalidate()
        =>Interlocked.Exchange(ref lastScanTicks,0L);

    public static async Task<IReadOnlyList<PnpBattery>> ReadAsync()
    {
        var since=DateTime.UtcNow.Ticks-Interlocked.Read(ref lastScanTicks);
        if(since>=0 && since<TimeSpan.FromSeconds(60).Ticks)
            return Volatile.Read(ref cached);
        if(!await ScanGate.WaitAsync(0))
            return Volatile.Read(ref cached);
        try
        {
            since=DateTime.UtcNow.Ticks-Interlocked.Read(ref lastScanTicks);
            if(since>=0 && since<TimeSpan.FromSeconds(60).Ticks)
                return Volatile.Read(ref cached);
            var result=await Task.Run<IReadOnlyList<PnpBattery>>(
                ()=>PnpBatteryReader.Read());
            Volatile.Write(ref cached,result);
            Interlocked.Exchange(ref lastScanTicks,DateTime.UtcNow.Ticks);
            return result;
        }
        catch
        {
            // The diagnostics must never block normal OLED rendering.
            Interlocked.Exchange(ref lastScanTicks,DateTime.UtcNow.Ticks);
            return Volatile.Read(ref cached);
        }
        finally { ScanGate.Release(); }
    }
}

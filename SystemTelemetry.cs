using System;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace SteelseriesDockCustomiser;

internal readonly record struct SystemTelemetrySnapshot(
    int? Cpu, int? Memory, int? Ping, DateTimeOffset SampledAt);

/// <summary>
/// Lightweight Windows native counters; no monitoring background service,
/// admin privileges, bundled drivers, or third-party telemetry software.
/// Ping is opt-in: it is only sampled when a Ping widget is actually visible.
/// </summary>
internal sealed class SystemTelemetry
{
    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime {public uint Low,High;}
    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatus
    {
        public uint Size,Load;
        public ulong TotalPhysical,AvailablePhysical,TotalPageFile,
            AvailablePageFile,TotalVirtual,AvailableVirtual,ExtendedAvailable;
    }
    [DllImport("kernel32.dll",SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out FileTime idle,out FileTime kernel,
        out FileTime user);
    [DllImport("kernel32.dll",SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatus buffer);

    private readonly object gate=new();
    private ulong priorIdle,priorTotal;

    private static ulong Number(FileTime value)=>((ulong)value.High<<32)|value.Low;

    public SystemTelemetrySnapshot Read()
    {
        int? cpu=null,memory=null;
        if(GetSystemTimes(out var idle,out var kernel,out var user))
        {
            ulong busy=Number(kernel)+Number(user),idleTicks=Number(idle);
            lock(gate)
            {
                if(priorTotal!=0&&busy>priorTotal&&idleTicks>=priorIdle)
                {
                    ulong totalDelta=busy-priorTotal,idleDelta=idleTicks-priorIdle;
                    cpu=(int)Math.Clamp(Math.Round(100d*(totalDelta-
                        Math.Min(totalDelta,idleDelta))/totalDelta),0,100);
                }
                priorTotal=busy;
                priorIdle=idleTicks;
            }
        }
        var status=new MemoryStatus{Size=(uint)Marshal.SizeOf<MemoryStatus>()};
        if(GlobalMemoryStatusEx(ref status))memory=(int)Math.Clamp(status.Load,0,100);
        return new(cpu,memory,null,DateTimeOffset.UtcNow);
    }

    public static async Task<int?> ReadPingAsync()
    {
        try
        {
            using var ping=new Ping();
            var reply=await ping.SendPingAsync("1.1.1.1",650)
                .ConfigureAwait(false);
            return reply.Status==IPStatus.Success
                ?(int)Math.Min(reply.RoundtripTime,9999):null;
        }
        catch{return null;}
    }
}

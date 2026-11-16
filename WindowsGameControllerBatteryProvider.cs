using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Power;
using Windows.Gaming.Input;
using Windows.System.Power;

namespace SteelseriesDockCustomiser;

/// <summary>
/// Windows' built-in, read-only controller battery API. A connected raw
/// controller may supply a live capacity even when HID Battery Strength and
/// GATT BAS are absent. Unsupported reports never become fake percentages.
/// No raw input, pairing, haptics, feature/output report or calibration.
/// </summary>
internal sealed class WindowsGameControllerBatteryProvider:IPeripheralBatteryProvider
{
    public string Protocol=>"Windows.Gaming.Input RawGameController BatteryReport";
    internal static int? PercentFromReport(BatteryReport? report)
    {
        if(report is null)return null;
        return FromCapacity(report.RemainingCapacityInMilliwattHours,
            report.FullChargeCapacityInMilliwattHours);
    }

    internal static int? FromCapacity(int? remaining,int? full)
    {
        if(remaining is null || full is null ||full<=0||
           remaining<0||remaining>full)return null;
        return (int)Math.Clamp(
            Math.Round(100.0*remaining.Value/full.Value),0,100);
    }

    internal static bool IsDualSense(string name,int vid,int pid)=>
        vid==0x054c&&(pid is 0x0ce6 or 0x0df2||
            name.Contains("DualSense",StringComparison.OrdinalIgnoreCase));

    private static string StableKind(string nonRoamableId,int vid,int pid)
    {
        string identity=vid.ToString("X4")+":"+pid.ToString("X4")+
            ":"+nonRoamableId;
        string hash=Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(identity)),0,7);
        return "Vendor:WinGamepad:"+hash;
    }

    public Task<IReadOnlyList<BatteryReading>> ReadAsync()=>
        Task.Run<IReadOnlyList<BatteryReading>>(ReadNow);

    private static IReadOnlyList<BatteryReading> ReadNow()
    {
        var result=new List<BatteryReading>();
        try
        {
            foreach(var device in RawGameController.RawGameControllers.Take(24))
            {
                try
                {
                    // Do not infer a charge from presence alone. The OS must
                    // return a current report with both capacity fields.
                    int? percent=PercentFromReport(device.TryGetBatteryReport());
                    if(percent is not int value)continue;
                    string name=string.IsNullOrWhiteSpace(device.DisplayName)
                        ?$"Controller {device.HardwareVendorId:X4}:{device.HardwareProductId:X4}"
                        :device.DisplayName.Trim();
                    bool sony=IsDualSense(name,device.HardwareVendorId,
                        device.HardwareProductId);
                    string kind=StableKind(device.NonRoamableId??"",
                        device.HardwareVendorId,device.HardwareProductId);
                    string label=sony?"DualSense Wireless Controller":name;
                    BatteryDeviceCatalog.Register(kind,label,"DualSense");
                    result.Add(new BatteryReading(kind,value,
                        "Windows.Gaming.Input live BatteryReport",
                        "Connected Windows raw game controller; verified capacity",
                        DateTimeOffset.Now)
                    {
                        FriendlyName=label,IconKind="DualSense"
                    });
                }
                catch { /* Device disconnected during enumeration. */ }
            }
        }
        catch { /* Windows may not offer this WinRT API to this session. */ }
        return result;
    }
}

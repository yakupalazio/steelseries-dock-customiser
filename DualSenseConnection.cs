using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace SteelseriesDockCustomiser;

/// <summary>
/// Checks Windows' actual Bluetooth connection state, not stale paired
/// PnP records. Never pairs, connects, changes profiles or reads HID packets.
/// USB-only controllers use the separately opt-in HID battery reporting path.
/// </summary>
internal static class DualSenseConnection
{
    public static async Task<bool> IsConnectedAsync()
    {
        try
        {
            string aqs=BluetoothDevice.GetDeviceSelectorFromConnectionStatus(
                BluetoothConnectionStatus.Connected);
            var devices=await DeviceInformation.FindAllAsync(aqs);
            foreach(var info in devices)
            {
                if(info.Name.Contains("DualSense",StringComparison.OrdinalIgnoreCase) ||
                   info.Name.Contains("Wireless Controller",StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        catch(Exception){}
        return false;
    }
}

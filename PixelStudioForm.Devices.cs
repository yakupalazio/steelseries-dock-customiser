using System;
using System.Windows.Forms;

namespace SteelseriesDockCustomiser;

/// <summary>
/// Event-driven battery refresh without a second discovery loop.
/// USB arrival/removal and BLE service changes only request a bounded,
/// read-only refresh of the same 10-second battery pipeline.
/// </summary>
internal sealed partial class PixelStudioForm
{
    private DateTimeOffset lastDeviceRefresh=DateTimeOffset.MinValue;
    private bool watchingDevices;
    private const int WmDeviceChange=0x0219;
    private const int DeviceArrival=0x8000, DeviceRemoval=0x8004;

    private void BeginBatteryDeviceWatching()
    {
        if(watchingDevices||captureMode||traySmokeMode)return;
        watchingDevices=true;
        BleBatteryProvider.DevicesChanged+=OnBatteryDeviceChanged;
        BleBatteryProvider.StartWatching();
    }

    private void EndBatteryDeviceWatching()
    {
        if(!watchingDevices)return;
        watchingDevices=false;
        BleBatteryProvider.DevicesChanged-=OnBatteryDeviceChanged;
        BleBatteryProvider.StopWatching();
    }

    private void OnBatteryDeviceChanged()
    {
        if(!watchingDevices||IsDisposed||!IsHandleCreated)return;
        try{BeginInvoke((Action)RequestBatteryRefreshFromEvent);}
        catch(InvalidOperationException){}
    }

    private void RequestBatteryRefreshFromEvent()
    {
        if(IsDisposed||!watchingDevices)return;
        var now=DateTimeOffset.UtcNow;
        if(now-lastDeviceRefresh<TimeSpan.FromSeconds(3))return;
        lastDeviceRefresh=now;
        _=RefreshBatteriesAsync();
    }

    protected override void WndProc(ref Message message)
    {
        if(message.Msg==WmDeviceChange &&
           (message.WParam.ToInt64()==DeviceArrival ||
            message.WParam.ToInt64()==DeviceRemoval))
            OnBatteryDeviceChanged();
        base.WndProc(ref message);
    }
}

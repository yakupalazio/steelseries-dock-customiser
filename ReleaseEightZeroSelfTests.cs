using System;
using System.Drawing;
using System.IO;
using System.Linq;
using HidSharp;

namespace SteelseriesDockCustomiser;

internal static class ReleaseEightZeroSelfTests
{
    internal static void Run()
    {
        RobloxPathsAndLogo();
        NovaProDockBattery();
        WindowsBluetoothBatteryProperties();
        PhotoAsset();
        if(Environment.GetEnvironmentVariable("DOCK_TEST_NOVA_HID")=="1")
            ProbeLocalNovaDock();
        Console.WriteLine("PASS: RC8 Roblox Player identity and safe 32x32 fallback, "+
            "Nova Pro Wireless exact dock status frame and battery priority, "+
            "public dock-photo release guard.");
    }

    private static void RobloxPathsAndLogo()
    {
        const string appData=@"C:\Users\tester\AppData\Local";
        const string programFiles=@"C:\Program Files";
        string player=Path.Combine(appData,
            @"Roblox\Versions\version-a8fe3\RobloxPlayerBeta.exe");
        if(!GgGameDetector.IsTrustedRobloxPlayerPath(
            player,"RobloxPlayerBeta",appData,programFiles))
            throw new Exception("Roblox versioned Player not recognized");
        string packaged=Path.Combine(programFiles,
            @"WindowsApps\ROBLOXCorporation.ROBLOX_2.500.0.0_x64__55nm5eh3cm0pr\Roblox.exe");
        if(!GgGameDetector.IsTrustedRobloxPlayerPath(
            packaged,"Roblox",appData,programFiles))
            throw new Exception("Microsoft Store Roblox package not recognized");
        foreach(var (path,name) in new[]{
            (Path.Combine(appData,
                @"Roblox\Versions\version-a8fe3\RobloxStudioBeta.exe"),
                "RobloxStudioBeta"),
            (Path.Combine(appData,
                @"Roblox\Versions\version-a8fe3\RobloxPlayerLauncher.exe"),
                "RobloxPlayerLauncher"),
            (Path.Combine(appData,
                @"Roblox\Logs\RobloxPlayerBeta.exe"),"RobloxPlayerBeta"),
            (Path.Combine(appData,
                @"RandomApp\Versions\version-a8fe3\RobloxPlayerBeta.exe"),
                "RobloxPlayerBeta"),
            (Path.Combine(appData,
                @"Roblox\Versions\fake\RobloxPlayerBeta.exe"),
                "RobloxPlayerBeta"),
            (Path.Combine(programFiles,
                @"WindowsApps\FakePublisher.Random_5_x64__x\Roblox.exe"),
                "Roblox"),
            (Path.Combine(appData,
                @"Roblox\Versions\version-a8fe3\RobloxPlayerBeta.exe"),
                "RobloxStudioBeta")
        })
            if(GgGameDetector.IsTrustedRobloxPlayerPath(
                path,name,appData,programFiles))
                throw new Exception("Unsafe Roblox player identity "+name);
        var game=new GameSnapshot("Roblox","RobloxPlayerBeta",4);
        var icon=GameLogoProvider.RobloxFallbackLogo(game);
        int lit=icon.Pixels.Count(p=>p);
        if(!GameLogoProvider.IsRobloxGame(game)||lit is <80 or >650||
           GameLogoProvider.IsFilledRectangle(icon.Pixels) ||
           icon.Pixels[16*32+16]||
           !icon.Pixels[16*32+4])
            throw new Exception("32x32 Roblox icon is blank/solid");
        var ineligible=new GameSnapshot("Roblox Studio","RobloxStudioBeta",4);
        if(GameLogoProvider.IsRobloxGame(ineligible))
            throw new Exception("Roblox Studio must not enter Game mode");
    }

    private static void NovaProDockBattery()
    {
        if(!NovaProDockBatteryProvider.SupportedIdentity(
             0x1038,0x12E5,0xFFC0,1,
             @"\\?\hid#vid_1038&pid_12e5&mi_04#f",64,64)||
           NovaProDockBatteryProvider.SupportedIdentity(
             0x1038,0x12E5,0x0001,1,
             @"\\?\hid#vid_1038&pid_12e5&mi_04#f",64,64)||
           NovaProDockBatteryProvider.SupportedIdentity(
             0x1038,0x12E5,0xFFC0,1,
             @"\\?\hid#vid_1038&pid_12e5&mi_02#f",64,64)||
           NovaProDockBatteryProvider.SupportedIdentity(
             0x1038,0x225D,0xFFC0,1,
             @"\\?\hid#vid_1038&pid_225d&mi_04#f",64,64))
            throw new Exception("Nova Pro HID allowlist unsafe");
        var cmd=NovaProDockBatteryProvider.MakeStatusRequest(64);
        if(cmd.Length!=64||cmd[0]!=6||cmd[1]!=0xB0||
           cmd.Skip(2).Any(b=>b!=0))
            throw new Exception("Nova battery command not status-only 06 B0");
        var packet=new byte[64];
        packet[0]=6;packet[1]=0xB0;
        packet[6]=7;packet[7]=8;packet[15]=8;
        var status=NovaProDockBatteryProvider.ParseStatus(packet);
        if(status?.Headset!=87||status.Spare!=100||
           status.Charging||!status.HeadsetConnected)
            throw new Exception("Nova Pro nine-step status parsing");
        packet[15]=2;
        if(NovaProDockBatteryProvider.ParseStatus(packet)?.Charging!=true)
            throw new Exception("Nova Pro cable charging not recognized");
        packet[15]=1;
        if(NovaProDockBatteryProvider.ParseStatus(packet)?.Headset!=null)
            throw new Exception("Offline headset must not display percentage");
        packet[15]=8;packet[6]=9;
        if(NovaProDockBatteryProvider.ParseStatus(packet)!=null)
            throw new Exception("Unknown Nova Pro battery code was accepted");
        packet[6]=7;packet[1]=0x93;
        if(NovaProDockBatteryProvider.ParseStatus(packet)!=null)
            throw new Exception("OLED draw frame mistaken for battery");
        packet[1]=0xB0;packet[7]=0;
        if(NovaProDockBatteryProvider.ParseStatus(packet)?.Spare!=null)
            throw new Exception("Empty spare slot mistaken for 0% battery");

        var at=DateTimeOffset.Now;
        var fallback=new BatteryReading("Vendor:NovaPro:Headset",87,
            "Nova Pro dock HID (yaklaşık, 12,5% adım)","status",at);
        var missing=new BatterySnapshot();
        BatterySources.MergeVerifiedVendorReadings(missing,[fallback]);
        if(missing["Headset"].Percent!=87||
           missing.Values.ContainsKey("Vendor:NovaPro:Headset"))
            throw new Exception("Nova fallback not mapped to headset slot");
        var gg=new BatterySnapshot();
        gg.Values["Headset"]=new BatteryReading("Headset",93,
            "SteelSeries GG Engine (canlı)","",at);
        BatterySources.MergeVerifiedVendorReadings(gg,[fallback]);
        if(gg["Headset"].Percent!=93)
            throw new Exception("Approximate Nova HID overwrote exact GG");
    }

    private static void ProbeLocalNovaDock()
    {
        foreach(var device in DeviceList.Local.GetHidDevices(0x1038)
            .Where(x=>x.ProductID is 0x12E0 or 0x12E5).Take(12))
        {
            var caps=CompxWirelessBatteryProvider.ReadCollectionCaps(
                device.DevicePath);
            int at=device.DevicePath.IndexOf("mi_",
                StringComparison.OrdinalIgnoreCase);
            string iface=at>=0 && at+5<=device.DevicePath.Length
                ?device.DevicePath.Substring(at,5):"unknown";
            Console.WriteLine("RC8_NOVA_CAPS="+iface+
                "; pid="+device.ProductID.ToString("X4")+
                "; page="+caps?.UsagePage.ToString("X4")+
                "; usage="+caps?.Usage.ToString("X4")+
                "; input="+caps?.InputLength+
                "; output="+caps?.OutputLength);
        }
        var source=new NovaProDockBatteryProvider();
        var readings=source.ReadAsync().GetAwaiter().GetResult();
        Console.WriteLine("RC8_NOVA_HID_DIAG="+
            NovaProDockBatteryProvider.LastDiagnostic);
        foreach(var reading in readings)
            Console.WriteLine("RC8_NOVA_HID_VALUE="+reading.Kind+
                " "+reading.Display);
    }

    private static void WindowsBluetoothBatteryProperties()
    {
        var parse=ConnectedBluetoothPropertyBatteryProvider.ParsePercent;
        foreach(var input in new object[]{
            (byte)53,(short)53,(ushort)53,53,(uint)53,(long)53,"53"})
            if(parse(input)!=53)
                throw new Exception("Windows BT valid 53% property rejected");
        foreach(var input in new object[]{
            (byte)255,-1,101,(uint)102,"not a percentage",0.58f})
            if(parse(input)!=null)
                throw new Exception("Windows BT invalid property accepted");
        var example=new System.Collections.Generic.Dictionary<string,object>{
            ["System.Devices.BatteryLifePercent"]=(byte)66
        };
        if(ConnectedBluetoothPropertyBatteryProvider
            .ReadProperty(example)!=66)
            throw new Exception("Connected Windows audio battery not parsed");
        example["System.Devices.BatteryLifePercent"]=255;
        if(ConnectedBluetoothPropertyBatteryProvider
            .ReadProperty(example)!=null)
            throw new Exception("Windows audio battery unknown must stay null");
        var snapshot=new BatterySnapshot();
        snapshot.Values["Headset"]=new BatteryReading(
            "Headset",null,"GG","No battery fields",DateTimeOffset.Now);
        string report=BatteryStatusDialog.BuildBatteryDiagnostics(
            snapshot,Array.Empty<ConnectedPeripheral>());
        if(!report.Contains("No battery fields")||
           !report.Contains("Nova Pro dock HID")||
           !report.Contains("WLMOUSE"))
            throw new Exception("Tester battery diagnostics incomplete");
    }

    private static void PhotoAsset()
    {
        string path=Path.Combine(AppContext.BaseDirectory,
            "dock-photo-local.png");
        bool require=Environment.GetEnvironmentVariable(
            "DOCK_REQUIRE_DOCK_PHOTO")=="1";
        if(!File.Exists(path))
        {
            if(require)throw new Exception(
                "Public release MUST include dock-photo-local.png");
            return;
        }
        if(new FileInfo(path).Length<30000)
            throw new Exception("Dock photo is suspiciously small");
        using var photo=Image.FromFile(path);
        if(photo.Width<590||photo.Height<420)
            throw new Exception("Dock photo is too small for OLED alignment");
    }
}

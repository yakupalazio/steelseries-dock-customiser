using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace SteelseriesDockCustomiser;

internal static class Beta5SelfTests
{
    private static bool Lit(Bitmap image,int x,int y)
        =>image.GetPixel(x,y).R>150;

    private static Bitmap DrawDial(string style,int game,int chat,int width=102)
    {
        var state=new LayoutState{
            Widgets=[new DockWidget{
                Kind="VolumeChatMix",X=1,Y=12,DialStyle=style,
                MiniWidth=width,WheelShowGame=true,WheelShowChat=true,
                WheelRingThickness=3
            }]
        };
        var snap=new DialSnapshot(DialView.ChatMix,null,game,chat,
            DateTimeOffset.UtcNow,"Beta5 test");
        return OledRenderer.Render(state,false,false,false,
            protectHeader:true,layoutMode:ScreenMode.Idle,dial:snap);
    }

    public static void Run()
    {
        // A development build must not share Beta 4's save directory or
        // silently send a frame to its actively used hardware.
        if(!StudioProject.FilePath.StartsWith(
                Beta5Sandbox.SettingsDirectory,StringComparison.OrdinalIgnoreCase) ||
           !BatteryPreferences.FilePath.StartsWith(
                Beta5Sandbox.SettingsDirectory,StringComparison.OrdinalIgnoreCase))
            throw new Exception("Beta 5 accidentally shares Beta 4's settings");
        // The owner's personal 2026-11-13 baseline is shipped read-only
        // inside the single EXE. Existing local saves are not overwritten.
        var baseline=FactoryPresetDefaults.LoadStudio();
        var baselineIcons=FactoryPresetDefaults.LoadIcons();
        if(baseline is null ||
           !baselineIcons.TryGetValue("DualSense",out var controllerArt) ||
           controllerArt.Length!=11 || controllerArt[0].Length!=16 ||
           !baseline.Idle.Widgets.Any(w=>w.Kind=="BatterySlot3") ||
           !baseline.Game.Widgets.Any(w=>w.Kind=="BatterySlot3") ||
           !baseline.Media.Widgets.Any(w=>w.Kind=="BatterySlot3"))
            throw new Exception("Saved personal factory default is missing");
        foreach(int left in new[]{85,87,92})
        foreach(string pct in new[]{"70%","100%","--%"})
        {
            var layout=OledRenderer.PlanBatteryRow(left,54,"DualSense",pct);
            if(layout.TextX-left-layout.IconWidth<1 ||
               layout.TextX+layout.TextWidth>Math.Min(128,left+42) ||
               layout.IconY+layout.IconHeight>64)
                throw new Exception("Controller sprite overlaps battery percentage");
            if(pct=="70%" && left==92 &&
               (layout.Compact||layout.TextX<left+controllerArt[0].Length+2))
                throw new Exception("Controller 70% label lacks readable spacing");
        }
        // Render the third battery slot with the saved 16x11 controller
        // sprite. The gap must consist entirely of dark OLED pixels.
        var nowBattery=DateTimeOffset.Now;
        var slotBattery=new BatterySnapshot
        {
            CheckedAt=nowBattery,
            Values=new Dictionary<string,BatteryReading>
            {
                ["Mouse"]=new("Mouse",82,"test","",nowBattery),
                ["Phone"]=new("Phone",69,"test","",nowBattery),
                ["DualSense"]=new("DualSense",70,"test","",nowBattery)
                    {IconKind="DualSense"}
            }
        };
        using(var rendered=OledRenderer.Render(
            new LayoutState{Widgets=[new DockWidget{
                Kind="BatterySlot3",X=92,Y=54}]},
            false,true,false,batteries:slotBattery,
            preferences:new BatteryPreferences{
                PriorityKinds=["Mouse","Phone","DualSense"]
            },protectHeader:true,layoutMode:ScreenMode.Idle))
        {
            var g=OledRenderer.PlanBatteryRow(92,54,"DualSense","70%");
            for(int gx=92+g.IconWidth;gx<g.TextX;gx++)
            for(int gy=53;gy<64;gy++)
                if(Lit(rendered,gx,gy))
                    throw new Exception("Controller overlaps 70 percent in OLED frame");
        }
        // Generic discovery never fakes values for unknown manufacturers:
        // two interfaces in one USB container must be a single device.
        var discoveredAt=DateTimeOffset.Now;
        var catalogSnapshot=new BatterySnapshot{
            CheckedAt=discoveredAt,
            Values=new Dictionary<string,BatteryReading>{
                ["DualSense"]=new("DualSense",70,"Bluetooth HID","",
                    discoveredAt),
                ["Hid:1234567"]=new("Hid:1234567",83,"HID Battery Strength",
                    "",discoveredAt){FriendlyName="ATK X1 Mouse",IconKind="Mouse"}
            }
        };
        var samplePeripherals=new[]{
            (Id:"usb-container-1",Name:"Logitech MX Master 3S",
                Connection:"USB / Dock",Manufacturer:"Logitech"),
            (Id:"usb-container-1",Name:"USB Composite Device",
                Connection:"USB / Dock",Manufacturer:"Logitech"),
            (Id:"bt-container-1",Name:"DualSense Wireless Controller",
                Connection:"Bluetooth",Manufacturer:"Sony"),
            (Id:"usb-container-2",Name:"ATK X1 Mouse",
                Connection:"USB / Dock",Manufacturer:"ATK")
        };
        var rows=ConnectedPeripheralInventory.MergeAndAttachBatteries(
            samplePeripherals,catalogSnapshot);
        if(rows.Count!=3 ||
           rows.Single(r=>r.Name.Contains("Logitech")).Percent is not null ||
           rows.Single(r=>r.Name.Contains("DualSense")).Percent!=70 ||
           rows.Single(r=>r.Name.Contains("ATK")).Percent!=83)
            throw new Exception("Universal device inventory/verified read mismatch");
        if(!StandardHidBatteryProvider.IsStandardBatteryUsage(
                new uint[]{0x00060020}) ||
           StandardHidBatteryProvider.IsStandardBatteryUsage(
                new uint[]{0x00060021}))
            throw new Exception("HID standard Battery Strength detection failed");
        // Synthetic standards-compliant HID descriptor: input and feature
        // values are normalized from their advertised logical 0..100 range.
        byte[] batteryDescriptorPrefix=[
            0x05,0x06,0x09,0x20,0xA1,0x01,
            0x15,0x00,0x25,0x64,0x75,0x08,0x95,0x01,
            0x09,0x20
        ];
        foreach(byte reportTag in new byte[]{0x81,0xB1})
        {
            var descriptor=new HidSharp.Reports.ReportDescriptor(
                [..batteryDescriptorPrefix,reportTag,0x02,0xC0]);
            var report=reportTag==0x81
                ?descriptor.InputReports.Single()
                :descriptor.FeatureReports.Single();
            var packet=new byte[report.Length];
            packet[0]=report.ReportID;
            packet[^1]=73;
            if(StandardHidBatteryProvider.DecodeStandardReport(report,packet)!=73)
                throw new Exception("HID Battery Strength input/feature report misread");
        }
        var hidPriority=new BatteryPreferences{
            PriorityKinds=["Hid:1234567","DualSense","Phone"]
        };
        if(BatteryPriority.SelectThreeAt(catalogSnapshot,hidPriority,
               discoveredAt).FirstOrDefault()!="Hid:1234567")
            throw new Exception("New HID battery cannot enter user priority");
        // Only validation here: no OLED HID sends during smoke tests.
        // Live and static Apply require their own visible UI consent dialog.
        var hardwareStatus=new DockHardwareTest().Validate();
        if(hardwareStatus is null)
            throw new Exception("Hardware validation must return a status");
        var at=DateTimeOffset.UtcNow;
        var low=new BatteryOledAlert.Alert("Mouse",20,"Mouse",false,false,at);
        var critical=new BatteryOledAlert.Alert("Mouse",10,"Mouse",true,true,at);
        if(low.Duration!=TimeSpan.FromSeconds(4)||
           low.Expired(at.AddMilliseconds(3999))||
           !low.Expired(at.AddMilliseconds(4000))||
           critical.Duration!=TimeSpan.FromMilliseconds(5500)||
           !critical.Lit(at.AddMilliseconds(999))||
           critical.Lit(at.AddMilliseconds(1000))||
           critical.Lit(at.AddMilliseconds(1499))||
           !critical.Lit(at.AddMilliseconds(1500))||
           !critical.Expired(at.AddMilliseconds(5500)))
            throw new Exception("Full-screen battery warning timing wrong");
        using(var light=BatteryOledAlert.Render(low,at))
        using(var blink=BatteryOledAlert.Render(critical,at.AddMilliseconds(1100)))
        using(var alarm=BatteryOledAlert.Render(critical,at))
        {
            int white=0,off=0,criticalWhite=0;
            for(int y=0;y<64;y++)for(int x=0;x<128;x++)
            {
                if(Lit(light,x,y))white++;
                if(Lit(blink,x,y))off++;
                if(Lit(alarm,x,y))criticalWhite++;
            }
            if(white<65||criticalWhite<65||off!=0)
                throw new Exception("Battery OLED alert pixels or blink invalid");
        }
        var charging=new BatteryOledAlert.Alert(
            "Phone",57,"Phone",false,false,at){Charging=true};
        if(charging.Duration!=TimeSpan.FromSeconds(4) ||
           !charging.Lit(at.AddMilliseconds(1200)) ||
           charging.Expired(at.AddMilliseconds(3999)) ||
           !charging.Expired(at.AddMilliseconds(4000)))
            throw new Exception("Charging OLED overlay timing failed");
        using(var chargingImage=BatteryOledAlert.Render(charging,at))
        {
            int count=0,lightning=0;
            for(int y=0;y<64;y++)for(int x=0;x<128;x++)
            {
                if(!Lit(chargingImage,x,y))continue;
                count++;
                if(x is >=56 and <=78 && y is >=11 and <=34)lightning++;
            }
            if(count<100||lightning<35)
                throw new Exception("Charging overlay lightning missing");
        }
        var chargerMonitor=new BatteryChargingMonitor();
        BatterySnapshot Charge(bool? attached,int seconds=0)=>new(){
            Values=new Dictionary<string,BatteryReading>{
                ["Phone"]=new("Phone",57,"Phone Link","",
                    at.AddSeconds(seconds),attached)
            }
        };
        if(chargerMonitor.Evaluate(Charge(null),at).Count!=0 ||
           chargerMonitor.Evaluate(Charge(false),at).Count!=0 ||
           chargerMonitor.Evaluate(Charge(true,10),at.AddSeconds(10)).Count!=1 ||
           chargerMonitor.Evaluate(Charge(true,20),at.AddSeconds(20)).Count!=0 ||
           chargerMonitor.Evaluate(Charge(false,30),at.AddSeconds(30)).Count!=0 ||
           chargerMonitor.Evaluate(Charge(true,40),at.AddSeconds(40)).Count!=1)
            throw new Exception("Charging transitions repeated or were missed");
        var initialCharger=new BatteryChargingMonitor();
        if(initialCharger.Evaluate(Charge(true),at).Count!=0)
            throw new Exception("Charged-on-start device displayed false alert");
        if(MiniWheelArtwork.Factory.Length!=MiniWheelArtwork.Width*
            MiniWheelArtwork.Height||
           !MiniWheelArtwork.IsValid(MiniWheelArtwork.Factory)||
           !MiniWheelArtwork.IsValid(
                MiniWheelArtwork.Normalize(WheelArtwork.Factory)))
            throw new Exception("Native mini artwork/legacy conversion invalid");
        using(var miniEditor=new WheelArtworkDialog(null,compact:true))
        using(var fullEditor=new WheelArtworkDialog(null))
        {
            if(miniEditor.EditorGridSize!=(31,29) ||
               fullEditor.EditorGridSize!=(45,42) ||
               !miniEditor.TestHistoryRoundTrip() ||
               !fullEditor.TestHistoryRoundTrip())
                throw new Exception("Wheel editor grid/undo/redo inconsistent");
        }
        for(int deg=0;deg<=WheelArtwork.Sweep;deg+=10)
        {
            double rad=(WheelArtwork.StartAngle+deg)*Math.PI/180d;
            int x=MiniWheelArtwork.CenterX+(int)Math.Round(13d*Math.Cos(rad));
            int y=MiniWheelArtwork.CenterY+(int)Math.Round(13d*Math.Sin(rad));
            if(x<0||x>=MiniWheelArtwork.Width||
               y<0||y>=MiniWheelArtwork.Height||
               MiniWheelArtwork.Factory[y*MiniWheelArtwork.Width+x]!='1')
                throw new Exception("Compact wheel missing outer arc pixel");
        }
        foreach(bool gameSide in new[]{true,false})
        {
            bool[] DrawMiniAt(int offset)
            {
                var image=new bool[8192];
                var mask=new bool[8192];
                MiniWheelArtwork.Draw(image,mask,53,15,50,DialView.ChatMix,
                    showGame:gameSide,showChat:!gameSide,
                    gameDX:gameSide?offset:0,chatDX:gameSide?0:offset);
                return image;
            }
            if(DrawMiniAt(-20).SequenceEqual(DrawMiniAt(20)))
                throw new Exception(
                    "Mini Game/Chat drag offset snapped to a fixed position");
        }
        using(var neutral=DrawDial("Bar",100,100))
        using(var game=DrawDial("Bar",100,0))
        using(var chat=DrawDial("Bar",0,100))
        {
            int center=1+(102-2)/2, y=12+14+3;
            if(!Lit(neutral,center,y) || !Lit(game,center,y) ||
               !Lit(chat,center,y))
                throw new Exception("Flat ChatMix center divider disappeared");
            if(Lit(neutral,center-20,y)||Lit(neutral,center+20,y))
                throw new Exception("Flat neutral ChatMix should have empty halves");
            if(!Lit(game,center-20,y)||Lit(game,center+20,y))
                throw new Exception("Game dominance did not fill left half only");
            if(Lit(chat,center-20,y)||!Lit(chat,center+20,y))
                throw new Exception("Chat dominance did not fill right half only");
        }
        using(var mini=DrawDial("MiniWheel",100,0))
        using(var full=DrawDial("Wheel",100,0))
        {
            int miniCount=0, fullCount=0;
            for(int y=10;y<53;y++)for(int x=0;x<70;x++)
            {
                if(Lit(mini,x,y))miniCount++;
                if(Lit(full,x,y))fullCount++;
            }
            if(miniCount==0||fullCount==0||miniCount>=fullCount)
                throw new Exception("Mini wheel was not smaller than full ring");
        }
        // Compact geometry belongs to its own saved widget; cloning must
        // preserve it without rewriting a user-authored large wheel sprite.
        var miniOriginal=new DockWidget{
            Kind="VolumeChatMix",DialStyle="MiniWheel",
            MiniWheelSize=70,WheelRingThickness=2
        };
        var bigOriginal=new DockWidget{
            Kind="VolumeChatMix",DialStyle="Wheel",
            WheelPixels=WheelArtwork.Factory
        };
        var clonedLayout=new LayoutState{
            Widgets=[miniOriginal,bigOriginal]
        }.DeepClone();
        if(clonedLayout.Widgets[0].MiniWheelSize!=70 ||
           clonedLayout.Widgets[1].WheelPixels!=WheelArtwork.Factory)
            throw new Exception("Compact sizing modified full wheel artwork");
        // Compact size is independent of the original 45x42 pixel artwork.
        var smallWheel=new bool[128*64];
        var smallMask=new bool[128*64];
        var regularWheel=new bool[128*64];
        var regularMask=new bool[128*64];
        MiniWheelArtwork.Draw(smallWheel,smallMask,40,14,50,
            DialView.Volume,showGame:false,showChat:false,sizePercent:70);
        MiniWheelArtwork.Draw(regularWheel,regularMask,40,14,50,
            DialView.Volume,showGame:false,showChat:false,sizePercent:100);
        if(smallWheel.Count(p=>p)==0 ||
           smallWheel.Count(p=>p)>=regularWheel.Count(p=>p))
            throw new Exception("Independent compact wheel size had no effect");
        // Both compact ChatMix icons must remain in their own rotating arc
        // sectors and may never paint onto pre-existing dial pixels.
        foreach(int rotation in new[]{-90,0,90})
        {
            var baseFrame=new bool[128*64];
            var baseMask=new bool[128*64];
            MiniWheelArtwork.Draw(baseFrame,baseMask,50,15,75,
                DialView.ChatMix,rotation,3,false,false);
            foreach(bool gameIcon in new[]{true,false})
            {
                var withIcon=(bool[])baseFrame.Clone();
                var mask=(bool[])baseMask.Clone();
                MiniWheelArtwork.Draw(withIcon,mask,50,15,75,
                    DialView.ChatMix,rotation,3,gameIcon,!gameIcon);
                int added=0;
                double start=WheelArtwork.StartAngle+rotation;
                for(int py=10;py<53;py++)for(int px=0;px<128;px++)
                {
                    int i=py*128+px;
                    if(!withIcon[i] || baseFrame[i])continue;
                    if(baseMask[i])
                        throw new Exception("Mini icon overlapped ring bitmap");
                    double azimuth=Math.Atan2(py-(15+MiniWheelArtwork.CenterY),
                        px-(50+MiniWheelArtwork.CenterX))*180d/Math.PI;
                    double progress=(azimuth-start+7200d)%360d;
                    bool own=gameIcon
                        ?progress>=150d && progress<=300d
                        :progress>=0d && progress<=150d;
                    if(!own)throw new Exception("Mini icon entered the wrong channel");
                    added++;
                }
                if(rotation==0 && added==0)
                    throw new Exception("Mini icon did not render in a clear layout");
            }
        }
        // Full-size wheel: custom offsets and every checked ring rotation
        // must never move either icon into the other channel or over the ring.
        foreach(int rotation in new[]{-180,-90,0,90,180})
        foreach(bool gameIcon in new[]{true,false})
        foreach(int dx in new[]{-45,0,45})
        {
            var baseFrame=new bool[128*64];
            var baseMask=new bool[128*64];
            WheelArtwork.Draw(baseFrame,baseMask,50,13,null,75,
                DialView.ChatMix,gapAngle:rotation,showGame:false,showChat:false);
            var withIcon=(bool[])baseFrame.Clone();
            var mask=(bool[])baseMask.Clone();
            WheelArtwork.Draw(withIcon,mask,50,13,null,75,
                DialView.ChatMix,gapAngle:rotation,
                gameOffsetX:gameIcon?dx:0,chatOffsetX:gameIcon?0:dx,
                showGame:gameIcon,showChat:!gameIcon);
            double start=WheelArtwork.StartAngle+rotation;
            int count=0;
            for(int y=10;y<64;y++)for(int x=0;x<128;x++)
            {
                int i=y*128+x;
                if(!withIcon[i] || baseFrame[i])continue;
                if(baseMask[i])
                    throw new Exception("Large icon painted on wheel");
                double azimuth=Math.Atan2(y-(13+WheelArtwork.CenterY),
                    x-(50+WheelArtwork.CenterX))*180d/Math.PI;
                double progress=(azimuth-start+7200d)%360d;
                bool own=gameIcon
                    ? progress>=150d && progress<=300d
                    : progress>=0d && progress<=150d;
                if(!own)
                    throw new Exception("Large icon crossed its ChatMix arc");
                count++;
            }
            if(rotation==0 && dx==0 && count==0)
                throw new Exception("Large ChatMix icon disappeared");
        }
        // Manual Game/Chat offsets must actually move the logo around its
        // own ring sector; they cannot all collapse to one fixed endpoint.
        {
            var track=new bool[8192];
            var occupied=new bool[8192];
            WheelArtwork.Draw(track,occupied,50,12,null,50,
                DialView.ChatMix,showGame:false,showChat:false);
            foreach(var (name,gameSide,azimuth) in new[]{
                ("Game",true,WheelArtwork.StartAngle+WheelArtwork.Sweep*.75d),
                ("Chat",false,WheelArtwork.StartAngle+WheelArtwork.Sweep*.25d)
            })
            {
                var first=WheelArtwork.EndpointIconPosition(
                    50,12,name,azimuth,occupied,
                    offsetX:-18,startAngle:WheelArtwork.StartAngle,
                    gameSide:gameSide);
                var second=WheelArtwork.EndpointIconPosition(
                    50,12,name,azimuth,occupied,
                    offsetX:18,startAngle:WheelArtwork.StartAngle,
                    gameSide:gameSide);
                if(first.X<0||second.X<0||first==second)
                    throw new Exception(name+" manual drag did not change position");
            }
        }
        string id=BleBatteryProvider.StableKind("test-device-id-1");
        if(!id.StartsWith("Ble:")||id!=BleBatteryProvider.StableKind("test-device-id-1")||
           id==BleBatteryProvider.StableKind("test-device-id-2"))
            throw new Exception("BLE stable ID derivation failed");
        if(BatteryDeviceClassifier.IconFor("Galaxy Buds 3","Bluetooth")!="Earbuds"||
           BatteryDeviceClassifier.IconFor("Wireless Controller","Bluetooth")!="DualSense"||
           BatteryDeviceClassifier.IconFor("Keychron Keyboard","Bluetooth")!="Keyboard"||
           BatteryDeviceClassifier.IconFor("JBL Flip Speaker","Bluetooth")!="Speaker"||
           BatteryDeviceClassifier.IconFor("Unknown","Bluetooth")!="Bluetooth"||
           BatteryDeviceClassifier.IconFor("Unknown","USB")!="Usb")
            throw new Exception("Automatic battery icon classification failed");
        var now=DateTimeOffset.Now;
        var detected=new BatterySnapshot{
            Values=new Dictionary<string,BatteryReading>{
                [id]=new(id,20,"GATT (uncached)","",now){
                    IconKind="Earbuds",FriendlyName="Galaxy Buds 3"}
            }
        };
        if(!BatteryPriority.SelectThree(detected,new BatteryPreferences()).Contains(id))
            throw new Exception("Verified new GATT device did not enter priority");
        var merged=new BatterySnapshot{
            Values=new Dictionary<string,BatteryReading>{
                ["Phone"]=new("Phone",84,"Phone Link","",now){
                    FriendlyName="Galaxy S24+"}
            }
        };
        BatterySources.MergeVerifiedBleReadings(merged,[
            new BatteryReading("Ble:galaxy",40,"GATT","",now){
                FriendlyName="Galaxy S24+"},
            new BatteryReading("Ble:other",72,"GATT","",now){
                FriendlyName="Other Bluetooth Mouse"}
        ]);
        if(merged["Phone"].Percent!=84 || merged.Values.ContainsKey("Ble:galaxy") ||
           merged["Ble:other"].Percent!=72)
            throw new Exception("Known phone BLE/GG source deduplication failed");
        var fallback=new BatterySnapshot();
        BatterySources.MergeVerifiedBleReadings(fallback,[
            new BatteryReading("Ble:aerox",91,"GATT","",now){
                FriendlyName="Aerox 3 Wireless Gen 2"}]);
        if(fallback["Mouse"].Percent!=91 || fallback.Values.ContainsKey("Ble:aerox"))
            throw new Exception("Known mouse BLE fallback did not unify");
        var monitor=new BatteryWarningMonitor();
        var pref=new BatteryPreferences{
            EnableLowBatteryWarnings=true,LowBatteryWarningPercent=20,
            CriticalBatteryWarningPercent=10
        };
        if(monitor.Evaluate(detected,pref,now).Count!=1 ||
           monitor.Evaluate(detected,pref,now.AddSeconds(10)).Count!=0)
            throw new Exception("Low battery warning repeated too frequently");
        if(monitor.Evaluate(new BatterySnapshot{
            Values=new Dictionary<string,BatteryReading>{
                [id]=new(id,5,"GATT (uncached)","",now.AddSeconds(12))}
            },pref,now.AddSeconds(12)).Count!=1)
            throw new Exception("Critical low battery escalation not delivered");
        // User-configured warning interval applies only to fresh readings.
        var repeat=new BatteryWarningMonitor();
        var repeatPref=new BatteryPreferences{
            WarningRepeatMinutes=5,EnableLowBatteryWarnings=true
        };
        BatterySnapshot AtMinutes(int minutes)=>new(){
            Values=new Dictionary<string,BatteryReading>{
                [id]=new(id,19,"GATT (uncached)","",
                    now.AddMinutes(minutes))
            }
        };
        if(repeat.Evaluate(AtMinutes(0),repeatPref,now).Count!=1||
           repeat.Evaluate(AtMinutes(3),repeatPref,now.AddMinutes(3)).Count!=0||
           repeat.Evaluate(AtMinutes(6),repeatPref,now.AddMinutes(6)).Count!=1)
            throw new Exception("Configurable low battery reminder interval failed");
        using(var boldClock=OledRenderer.RenderDateTimeFontSample("08:08",3,true))
        using(var thinClock=OledRenderer.RenderDateTimeFontSample("08:08",1,true))
        {
            // Eight must retain its two counters even with 3px seven segments.
            if(Lit(boldClock,8,16)||Lit(boldClock,8,23)||
               !Lit(boldClock,7,13)||Lit(thinClock,7,13))
                throw new Exception("Bold seven-segment clock lost readable counters");
        }
        Console.WriteLine("PASS: mini radial, split ChatMix, bold fonts, BLE priorities and alerts.");
    }

    public static void RenderExamples(string folder)
    {
        Directory.CreateDirectory(folder);
        using(var neutral=DrawDial("Bar",100,100))
            neutral.Save(Path.Combine(folder,"beta5-flat-neutral.png"),ImageFormat.Png);
        using(var game=DrawDial("Bar",100,0))
            game.Save(Path.Combine(folder,"beta5-flat-game.png"),ImageFormat.Png);
        using(var chat=DrawDial("Bar",0,100))
            chat.Save(Path.Combine(folder,"beta5-flat-chat.png"),ImageFormat.Png);
        using(var mini=DrawDial("MiniWheel",100,50))
            mini.Save(Path.Combine(folder,"beta5-mini-wheel.png"),ImageFormat.Png);
        foreach(var weight in new[]{1,2,3})
        {
            using var clock=OledRenderer.RenderDateTimeFontSample("08:08",weight,true);
            clock.Save(Path.Combine(folder,$"beta5-clock-{weight}.png"),ImageFormat.Png);
            using var date=OledRenderer.RenderDateTimeFontSample("08/11/2026",weight,false);
            date.Save(Path.Combine(folder,$"beta5-date-{weight}.png"),ImageFormat.Png);
        }
    }
}

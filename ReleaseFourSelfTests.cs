using System;
using System.Drawing;
using System.Linq;

namespace SteelseriesDockCustomiser;

/// <summary>RC4/RC5 acceptance checks, no user settings, hardware writes,
/// remote reporting, uploads or workshop network activity.</summary>
internal static class ReleaseFourSelfTests
{
    internal static void Run()
    {
        var library=new LayoutProfileLibrary();
        var original=new LayoutState{Widgets=[
            new(){Kind="Clock",Text="OLD",X=15,Y=30}]};
        var saved=library.Add("Testing profile",ScreenMode.Idle,original);
        Guid stable=saved.Id;
        var replacement=new LayoutState{Widgets=[
            new(){Kind="Clock",Text="NEW",X=55,Y=30}]};
        library.Overwrite(stable,ScreenMode.Idle,replacement);
        replacement.Widgets[0].Text="MUTATED";
        if(saved.Id!=stable||saved.Layout.Widgets[0].Text!="NEW"||
           saved.Layout.Widgets[0].X!=55||
           original.Widgets[0].Text!="OLD")
            throw new Exception("Profile overwrite ID/cloning broken");
        try
        {
            library.Overwrite(stable,ScreenMode.Game,original);
            throw new Exception("Cross-mode profile overwrite was allowed");
        }
        catch(InvalidOperationException){}

        using var old=new Bitmap(128,64);
        using var next=new Bitmap(128,64);
        using(var g=Graphics.FromImage(old))g.Clear(Color.Black);
        using(var g=Graphics.FromImage(next))g.Clear(Color.White);
        foreach(string effect in OledModeTransition.Effects)
        {
            using var start=OledModeTransition.Compose(old,next,0,effect,true);
            using var end=OledModeTransition.Compose(old,next,1,effect,true);
            if(end.GetPixel(10,40).R<200 ||
               start.GetPixel(20,5).R<200)
                throw new Exception("OLED animation endpoints / OEM header failed: "+effect);
            if(effect!="Instant" && start.GetPixel(30,45).R>60)
                throw new Exception("OLED transition started fully white: "+effect);
            using var mid=OledModeTransition.Compose(old,next,.5,effect,true);
            for(int y=0;y<10;y++)for(int x=0;x<128;x++)
                if(mid.GetPixel(x,y).R<200)
                    throw new Exception("Protected OEM header changed");
            if(mid.Width!=128||mid.Height!=64)
                throw new Exception("OLED transition wrong geometry");
        }
        if(OledModeTransition.DurationMs("Slide",100)!=350 ||
           OledModeTransition.DurationMs("Slide",200)!=175 ||
           OledModeTransition.DurationMs("Slide",25)!=1400 ||
           UiPageTransition.AnimationDurationMs<200)
            throw new Exception("RC4 motion speed defaults regressed");

        var share=new SavedLayoutProfile{Name="Safe share",Mode=ScreenMode.Media,
            Layout=new LayoutState{Widgets=[
                new(){Kind="AudioVisualizer",
                    AudioEndpointId="private-device-id",
                    X=15,Y=25},
                new(){Kind="AnimatedGif",
                    GifAssetId="C:\\Users\\SomeUser\\private.gif",
                    X=15,Y=35}
            ]}};
        var prepared=WorkshopProfileContract.Prepare(share,"Example");
        string json=WorkshopProfileContract.Serialize(prepared);
        var decoded=WorkshopProfileContract.Parse(json);
        if(json.Contains("private-device-id")||
           json.Contains("SomeUser")||
           json.Contains("private.gif")||
           prepared.Version!=1 ||
           decoded.Assets.Count!=1)
            throw new Exception($"Future workshop draft: idLeak={json.Contains("private-device-id")}, pathLeak={json.Contains("SomeUser")}, gifLeak={json.Contains("private.gif")}, version={prepared.Version}, assets={decoded.Assets.Count}");
        if(share.Layout.Widgets[0].AudioEndpointId!="private-device-id")
            throw new Exception("Workshop draft mutated the user's saved profile");

        var bug=BugReportService.Compose("Screen frozen","OLED / Dock",
            "Display stopped after sleep; unplug and reconnect.",
            false,null);
        if(bug.OperatingSystem!=null||bug.AttachmentBase64!=null||
           bug.Category!="OLED / Dock")
            throw new Exception("Bug report opt-in privacy");
        try
        {
            _=BugReportService.Compose("x","Invalid category",
                "Description long enough",false,null);
            throw new Exception("Bug reporting accepted invalid input");
        }
        catch(ArgumentException){}
        // Uploaded filenames and JPEG metadata must not pass through to
        // the private bug intake; the user must select each attachment.
        string sample=System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "rc4-private-photo-"+Guid.NewGuid().ToString("N")+".jpg");
        try
        {
            using(var bmp=new Bitmap(24,24))
            {
                using var g=Graphics.FromImage(bmp);
                g.Clear(Color.CornflowerBlue);
                bmp.Save(sample,System.Drawing.Imaging.ImageFormat.Jpeg);
            }
            var clean=BugReportService.Compose("Screenshot test","OLED / Dock",
                "Please inspect this private image",false,sample);
            var encoded=Convert.FromBase64String(clean.AttachmentBase64??"");
            if(clean.AttachmentFileName!="screenshot.png"||
               encoded.Length<8||encoded[0]!=137||encoded[1]!=80||
               (clean.OperatingSystem!=null))
                throw new Exception("Bug screenshot not scrubbed to clean PNG");
        }
        finally{try{System.IO.File.Delete(sample);}catch{}}
        Console.WriteLine(
            "PASS: profile overwrite & rollback isolation, 6 OLED effects, "+
            "variable speed and OEM rows, workshop-ready private-data draft, "+
            "opt-in bug report.");
    }
}

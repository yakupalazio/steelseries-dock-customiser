using System;
using System.Drawing;
using System.Linq;

namespace SteelseriesDockCustomiser;

/// <summary>
/// New RC3 widget data/model and rendering tests. No hardware writes,
/// GG database edits, website requests or user-settings modifications.
/// </summary>
internal static class ReleaseThreeSelfTests
{
    private static bool On(Bitmap bmp,int x,int y)=>
        bmp.GetPixel(x,y).R>150;

    internal static void Run()
    {
        if(WidgetCatalog.ForCategory("Game").Contains("GameLogo")!=true ||
           !StudioProject.IsAllowed(ScreenMode.Game,"GameLogo")||
           StudioProject.IsAllowed(ScreenMode.Media,"GameLogo")||
           StudioProject.IsAllowed(ScreenMode.Idle,"GameLogo")||
           WidgetCatalog.AllowsMultiple("GameLogo"))
            throw new Exception("Game logo mode/category/singleton regression");
        var duplicates=new LayoutState{Widgets=[
            new(){Kind="GameLogo",X=30,Y=14,GameLogoSize=46},
            new(){Kind="GameLogo",X=70,Y=22}
        ]};
        StudioProject.Sanitize(duplicates,ScreenMode.Game,true);
        if(duplicates.Widgets.Count!=1||
           duplicates.Widgets[0].GameLogoSize!=46||
           WidgetCatalog.CanAdd(ScreenMode.Game,"GameLogo",duplicates.Widgets))
            throw new Exception("Game logo duplicate/size sanitization");

        var mediaLeak=new LayoutState{Widgets=[
            new(){Kind="GameLogo",X=30,Y=14}
        ]};
        StudioProject.Sanitize(mediaLeak,ScreenMode.Media,true);
        if(mediaLeak.Widgets.Count!=0)
            throw new Exception("Game art retained in a Media layout");

        var progress=new DockWidget{Kind="Progress",X=13,Y=23,
            ProgressWidth=61,ProgressThickness=11};
        var layout=new LayoutState{Widgets=[progress]};
        StudioProject.Sanitize(layout,ScreenMode.Media,true);
        if(progress.ProgressWidth!=61||progress.ProgressThickness!=11||
           OledRenderer.GetBounds(progress).Width!=61||
           OledRenderer.GetBounds(progress).Height!=11)
            throw new Exception("User-adjusted progress size not saved");
        using(var bmp=OledRenderer.Render(layout,true,true,true,
            protectHeader:true,layoutMode:ScreenMode.Media))
        {
            if(!On(bmp,13,23)||!On(bmp,73,33)||
               !On(bmp,17,29)||On(bmp,74,29)||On(bmp,33,34))
                throw new Exception("Progress bar bounds or thickness wrong");
        }
        var absurd=new LayoutState{Widgets=[
            new(){Kind="Progress",ProgressWidth=500,ProgressThickness=-20},
            new(){Kind="GameLogo",GameLogoSize=300}
        ]};
        StudioProject.Sanitize(absurd,ScreenMode.Game,true);
        if(absurd.Widgets.Count!=1||absurd.Widgets[0].GameLogoSize!=54)
            throw new Exception("New widget ranges / mode clamps failed");

        var pixels=new bool[32*32];
        for(int row=0;row<32;row++)for(int col=0;col<32;col++)
            pixels[row*32+col]=row==col||row+col==31;
        var snapshot=new GameLogoSnapshot("synthetic","Synthetic Game",
            32,pixels,"Synthetic local test");
        var active=new GameSnapshot("Synthetic Game","synthetic",123456);
        var widget=new DockWidget{Kind="GameLogo",X=48,Y=17,
            GameLogoSize=28};
        var preview=new LayoutState{Widgets=[widget]};
        using(var actual=OledRenderer.Render(preview,false,true,false,
            game:active,protectHeader:true,layoutMode:ScreenMode.Game,
            gameLogo:snapshot))
        {
            // Nearest-neighbour 32→28 scaling samples the final source
            // column at 30, not 31. Assert real diagonal coordinates.
            if(!On(actual,48,17)||!On(actual,75,18)||
               !On(actual,49,44)||On(actual,47,17)||On(actual,76,44))
                throw new Exception("Running game's logo geometry is wrong");
        }
        using(var idle=OledRenderer.Render(preview,false,true,false,
            game:GameSnapshot.None,protectHeader:true,
            layoutMode:ScreenMode.Game,gameLogo:snapshot))
        using(var wrong=OledRenderer.Render(preview,false,true,false,
            game:active,protectHeader:true,layoutMode:ScreenMode.Game,
            gameLogo:snapshot with{Executable="another-exe"}))
        {
            for(int y=17;y<45;y++)for(int x=48;x<76;x++)
                if(On(idle,x,y)||On(wrong,x,y))
                    throw new Exception("Game icon leaked for stopped/wrong game");
        }

        using var source=new Bitmap(48,48);
        using(var brush=Graphics.FromImage(source))
        {
            brush.Clear(Color.Transparent);
            brush.FillEllipse(Brushes.White,4,4,40,40);
        }
        var converted=GameLogoProvider.ConvertImage(source);
        if(converted.Length!=1024||converted.Count(x=>x)<300||
           converted[0]||GameLogoProvider.Resolve(GameSnapshot.None)!=null)
            throw new Exception("Offline local logo conversion failed");

        // A headless/off-screen panel verifies that the actual wheel target
        // receives multiple easing frames without requiring mouse hardware.
        using(var probe=new System.Windows.Forms.Form{
            StartPosition=System.Windows.Forms.FormStartPosition.Manual,
            Location=new Point(-5000,-5000),
            ShowInTaskbar=false,ClientSize=new Size(230,140)})
        {
            var content=new System.Windows.Forms.FlowLayoutPanel{
                Dock=System.Windows.Forms.DockStyle.Fill,
                FlowDirection=System.Windows.Forms.FlowDirection.TopDown,
                WrapContents=false,AutoScroll=true};
            probe.Controls.Add(content);
            var labels=new System.Collections.Generic.List<
                System.Windows.Forms.Label>();
            for(int i=0;i<23;i++)
            {
                var label=new System.Windows.Forms.Label{
                    Text="Test row "+i,Width=180,Height=27};
                content.Controls.Add(label);
                labels.Add(label);
            }
            probe.Show();
            System.Windows.Forms.Application.DoEvents();
            UiPolish.Apply(probe);
            int before=Math.Abs(content.AutoScrollPosition.Y);
            if(!UiPolish.RouteWheelForTest(labels[2],-120))
                throw new Exception("Smooth wheel did not capture scroll");
            int immediate=Math.Abs(content.AutoScrollPosition.Y);
            for(int i=0;i<11;i++)
            {
                System.Threading.Thread.Sleep(20);
                System.Windows.Forms.Application.DoEvents();
            }
            int after=Math.Abs(content.AutoScrollPosition.Y);
            if(immediate!=before||after<before+45)
                throw new Exception("Smooth wheel failed eased movement");
            probe.Close();
        }
        Console.WriteLine("PASS: smooth mouse-wheel inertia on nested scroll menus.");
        Console.WriteLine(
            "PASS: 61x11 customizable progress bar, game-only singleton, "+
            "verified-game logo pixel art and safe empty fallback.");
    }
}

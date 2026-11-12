using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Collections.Generic;

namespace SteelseriesDockCustomiser;

/// <summary>
/// Short-lived, full 128x64 monochrome warning layer. All timing and pixel
/// output is deterministic and can be tested without touching a dock.
/// </summary>
internal static class BatteryOledAlert
{
    internal sealed record Alert(string Kind,int Percent,string IconKind,
        bool Critical,bool English,DateTimeOffset Started)
    {
        public bool Charging {get;init;}=false;
        public TimeSpan Duration => TimeSpan.FromMilliseconds(
            !Charging && Critical?5500:4000);
        public bool Expired(DateTimeOffset at)=>at-Started>=Duration;
        public bool Lit(DateTimeOffset at)
        {
            if(Charging||!Critical)return true;
            long millis=Math.Max(0,(long)(at-Started).TotalMilliseconds);
            return millis%1500<1000; // 1 s on, 0.5 s black
        }
    }

    // Original 5x7 pixel alphabet. Do not rasterize a Windows font and
    // subsequently downscale it: that destroys the counters at OLED size.
    private static readonly Dictionary<char,byte[]> Glyphs=new()
    {
        [' ']=[0,0,0,0,0,0,0],
        ['!']=[4,4,4,4,4,0,4],
        ['%']=[17,18,4,8,19,17,0],
        ['0']=[14,17,19,21,25,17,14],
        ['1']=[4,12,4,4,4,4,14],
        ['2']=[14,17,1,2,4,8,31],
        ['3']=[30,1,1,14,1,1,30],
        ['4']=[2,6,10,18,31,2,2],
        ['5']=[31,16,16,30,1,1,30],
        ['6']=[14,16,16,30,17,17,14],
        ['7']=[31,1,2,4,8,8,8],
        ['8']=[14,17,17,14,17,17,14],
        ['9']=[14,17,17,15,1,1,14],
        ['A']=[14,17,17,31,17,17,17],
        ['B']=[30,17,17,30,17,17,30],
        ['C']=[14,17,16,16,16,17,14],
        ['D']=[30,17,17,17,17,17,30],
        ['E']=[31,16,16,30,16,16,31],
        ['F']=[31,16,16,30,16,16,16],
        ['G']=[14,17,16,23,17,17,14],
        ['H']=[17,17,17,31,17,17,17],
        ['I']=[14,4,4,4,4,4,14],
        ['J']=[7,2,2,2,18,18,12],
        ['K']=[17,18,20,24,20,18,17],
        ['L']=[16,16,16,16,16,16,31],
        ['M']=[17,27,21,21,17,17,17],
        ['N']=[17,25,21,19,17,17,17],
        ['O']=[14,17,17,17,17,17,14],
        ['P']=[30,17,17,30,16,16,16],
        ['R']=[30,17,17,30,20,18,17],
        ['S']=[15,16,16,14,1,1,30],
        ['T']=[31,4,4,4,4,4,4],
        ['U']=[17,17,17,17,17,17,14],
        ['V']=[17,17,17,17,17,10,4],
        ['W']=[17,17,17,21,21,21,10],
        ['Y']=[17,17,10,4,4,4,4],
        ['Ş']=[15,16,14,1,30,4,8],
        ['Ü']=[10,0,17,17,17,17,14],
        ['İ']=[4,0,14,4,4,4,14],
        ['Ç']=[14,17,16,16,17,14,4]
    };

    public static Bitmap Render(Alert alert,DateTimeOffset at)
    {
        var pixels=new bool[128*64];
        if(!alert.Expired(at) && alert.Lit(at))
        {
            string icon=alert.IconKind;
            if(string.IsNullOrWhiteSpace(icon))
                icon=alert.Kind switch
                {
                    "SpareBattery"=>"Headset",
                    "DualSense"=>"DualSense",
                    "Phone"=>"Phone",
                    "Mouse"=>"Mouse",
                    "Headset"=>"Headset",
                    "Laptop"=>"Laptop",
                    _=>"Bluetooth"
                };
            if(icon=="SpareBattery")icon="Headset";
            string[] rows;
            try { rows=IconLibrary.GetCurrent(icon); }
            catch { rows=IconLibrary.GetCurrent("Bluetooth"); }
            if(rows.Length>0)
            {
                int scale=Math.Min(3,Math.Max(1,Math.Min(38/rows[0].Length,32/rows.Length)));
                int x=11+(39-rows[0].Length*scale)/2;
                int y=7+(32-rows.Length*scale)/2;
                for(int j=0;j<rows.Length;j++)
                for(int i=0;i<rows[j].Length;i++)
                    if(rows[j][i]=='1')
                        Fill(pixels,x+i*scale,y+j*scale,scale,scale);
            }
            if(alert.Charging)
            {
                DrawBolt(pixels,56,11);
                // 100% uses four characters, 4*12 pixels = 48, and fits.
                DrawText(pixels,alert.Percent+"%",80,17,2);
            }
            else DrawText(pixels,"!"+alert.Percent+"%",64,14,2);
            string caption=alert.Charging
                ?(alert.English?"CHARGING":"ŞARJ OLUYOR")
                :alert.English
                    ?(alert.Critical?"BATTERY CRITICAL":"BATTERY LOW")
                    :(alert.Critical?"ŞARJ KRİTİK":"ŞARJ DÜŞÜK");
            int textWidth=caption.Length*6-1;
            DrawText(pixels,caption,(128-textWidth)/2,47,1);
            // Separation line is content, not part of the OEM indicator.
            Fill(pixels,8,42,112,1);
        }
        var frame=new Bitmap(128,64,PixelFormat.Format24bppRgb);
        for(int y=0;y<64;y++)
        for(int x=0;x<128;x++)
            frame.SetPixel(x,y,pixels[y*128+x]?Color.White:Color.Black);
        return frame;
    }

    private static void DrawBolt(bool[] pixels,int left,int top)
    {
        // A large, original, 1-bit lightning mark for charging events.
        string[] bolt=[
            "......1111",".....1111.","....1111..","...1111...",
            "..11111111",".111111111","....1111..","...1111...",
            "..1111....",".1111.....","1111......"
        ];
        for(int y=0;y<bolt.Length;y++)
        for(int x=0;x<bolt[y].Length;x++)
            if(bolt[y][x]=='1')
                Fill(pixels,left+x*2,top+y*2,2,2);
    }

    private static void DrawText(bool[] pixels,string value,int x,int y,int scale)
    {
        foreach(char glyph in value)
        {
            char ch=char.ToUpperInvariant(glyph);
            if(!Glyphs.TryGetValue(ch,out var rows))rows=Glyphs[' '];
            for(int yy=0;yy<7;yy++)for(int xx=0;xx<5;xx++)
                if((rows[yy]&(1<<(4-xx)))!=0)
                    Fill(pixels,x+xx*scale,y+yy*scale,scale,scale);
            x+=6*scale;
        }
    }

    private static void Fill(bool[] px,int x,int y,int width,int height)
    {
        for(int yy=y;yy<y+height;yy++)
        for(int xx=x;xx<x+width;xx++)
            if(xx>=0&&xx<128&&yy>=0&&yy<64)px[yy*128+xx]=true;
    }
}

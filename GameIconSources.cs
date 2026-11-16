using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Xml.Linq;

namespace SteelseriesDockCustomiser;

/// <summary>
/// Read-only icon source fallback. Store apps may have a blank EXE icon while
/// their package ships a real PNG/ICO; other games may expose only a Shell icon.
/// Only files alongside the verified registered game EXE are inspected.
/// </summary>
internal static class GameIconSources
{
    private const uint ShgfiIcon=0x100;
    private const uint ShgfiLargeIcon=0;
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
    private struct ShellFileInfo
    {
        public IntPtr Icon;
        public int Index;
        public uint Attributes;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)]
        public string DisplayName;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=80)]
        public string TypeName;
    }
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string file,uint attr,
        out ShellFileInfo info,uint size,uint flags);
    [DllImport("user32.dll")]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr icon);

    internal static IEnumerable<string> NearbyArtwork(string executable)
    {
        string? root=Path.GetDirectoryName(executable);
        if(root is null||!Directory.Exists(root))yield break;
        var names=new List<string>
        {
            "minecraftIcon.ico","app.ico","game.ico","icon.ico",
            "LargeLogo.png","Logo.png","Square150x150Logo.png",
            "Square44x44Logo.png","StoreLogo.png"
        };
        string manifest=Path.Combine(root,"AppxManifest.xml");
        if(File.Exists(manifest))
        {
            try
            {
                var doc=XDocument.Load(manifest);
                foreach(var attr in doc.Descendants().Attributes())
                {
                    if(attr.Name.LocalName.EndsWith("Logo",
                        StringComparison.OrdinalIgnoreCase)&&
                        (attr.Value.EndsWith(".png",StringComparison.OrdinalIgnoreCase)
                         ||attr.Value.EndsWith(".ico",StringComparison.OrdinalIgnoreCase)))
                        names.Insert(0,attr.Value);
                }
            }
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException
                or System.Xml.XmlException) { }
        }
        var yielded=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(string relative in names)
        {
            string full=Path.GetFullPath(Path.Combine(root,
                relative.Replace('/','\\')));
            if(!full.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar)
                   +Path.DirectorySeparatorChar,
                   StringComparison.OrdinalIgnoreCase))continue;
            if(File.Exists(full)&&yielded.Add(full))yield return full;
        }
    }

    internal static Bitmap? ReadImage(string path)
    {
        try
        {
            if(!File.Exists(path)||new FileInfo(path).Length>10_000_000)return null;
            if(path.EndsWith(".ico",StringComparison.OrdinalIgnoreCase))
            {
                using var icon=new Icon(path,new Size(64,64));
                return icon.ToBitmap();
            }
            using var image=Image.FromFile(path);
            if(image.Width>2048||image.Height>2048)return null;
            return new Bitmap(image);
        }
        catch(Exception ex) when(ex is IOException or ArgumentException
            or UnauthorizedAccessException or OutOfMemoryException
            or System.Runtime.InteropServices.ExternalException) {return null;}
    }

    internal static Bitmap? ReadEmbedded(string executable)
    {
        try
        {
            using var icon=Icon.ExtractAssociatedIcon(executable);
            return icon?.ToBitmap();
        }
        catch(Exception ex) when(ex is IOException or ArgumentException
            or UnauthorizedAccessException or System.Runtime.InteropServices.ExternalException)
        {return null;}
    }

    internal static Bitmap? ReadShell(string executable)
    {
        try
        {
            int bytes=Marshal.SizeOf<ShellFileInfo>();
            if(SHGetFileInfo(executable,0,out var info,(uint)bytes,
                ShgfiIcon|ShgfiLargeIcon)==IntPtr.Zero||info.Icon==IntPtr.Zero)
                return null;
            try
            {
                using var icon=Icon.FromHandle(info.Icon);
                return icon.ToBitmap();
            }
            finally{DestroyIcon(info.Icon);}
        }
        catch(Exception ex) when(ex is ArgumentException or
            System.Runtime.InteropServices.ExternalException) {return null;}
    }
}

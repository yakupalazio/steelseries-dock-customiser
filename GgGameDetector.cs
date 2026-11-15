using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace SteelseriesDockCustomiser;

/// <summary>
/// Read-only GG Library Scan integration. Matches registered game executable
/// basenames against real running Windows processes (not foreground windows).
/// No process injection, game memory access, GG configuration edits or network.
/// </summary>
internal sealed record GameSnapshot(string Name,string Executable,int ProcessId)
{
    public static GameSnapshot None=>new("","",0);
    public bool IsRunning=>ProcessId>0;
}

internal static class GgGameDetector
{
    private const string Database=@"C:\ProgramData\SteelSeries\GG\db\database.db";
    private static readonly HashSet<string> GenericExecutables=new(StringComparer.OrdinalIgnoreCase)
    {
        "launcher","launch","start","game","steam","epicgameslauncher","unins000",
        "setup","update","bootstrapper","crashreporter","riotclientservices"
    };
    private static List<(string Name,string Exe)> games=[];
    private static DateTimeOffset cachedAt=DateTimeOffset.MinValue;

    public static Task<GameSnapshot> PollAsync()=>Task.Run(Poll);

    public static GameSnapshot Poll()
    {
        if((DateTimeOffset.Now-cachedAt).TotalSeconds>30)
        {
            games=LoadGgLibrary();
            cachedAt=DateTimeOffset.Now;
        }

        Process[] processes=[];
        try
        {
            processes=Process.GetProcesses();
            var running=processes.GroupBy(p=>p.ProcessName,StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g=>g.Key,g=>g.ToList(),StringComparer.OrdinalIgnoreCase);
            // Prefer the newest game instance if several are open.
            var candidates=new List<(GameSnapshot Game,DateTime Started)>();
            foreach(var (name,exe) in games)
            {
                var processName=Path.GetFileNameWithoutExtension(exe);
                if(GenericExecutables.Contains(processName) || processName.Length<3 ||
                   !running.TryGetValue(processName,out var matching))continue;
                foreach(var process in matching)
                {
                    try
                    {
                        if(process.HasExited)continue;
                        // Executable path verification when Windows grants access.
                        // Some anti-cheat processes deny MainModule access; GG's
                        // registered executable name remains a useful fallback.
                        var verified=true;
                        try
                        {
                            string? processPath=process.MainModule?.FileName;
                            if(!string.IsNullOrEmpty(processPath))
                                verified=NamesMatchPath(processPath,exe);
                        }
                        catch(System.ComponentModel.Win32Exception) { }
                        catch(InvalidOperationException) { }
                        if(!verified)continue;
                        DateTime start;
                        try{start=process.StartTime;}catch{start=DateTime.MinValue;}
                        candidates.Add((new GameSnapshot(name,processName,process.Id),start));
                    }
                    catch(InvalidOperationException){}
                    catch(System.ComponentModel.Win32Exception){}
                }
            }
            var selected=candidates.OrderByDescending(c=>c.Started)
                .Select(c=>c.Game).FirstOrDefault();
            // GG is preferred, but not mandatory. A foreground executable
            // beneath a recognized game-library installation is also a
            // useful, read-only game identity when GG missed registration.
            var foreground=DetectForegroundInstalledGame();
            if(foreground.IsRunning)
                return candidates.Select(c=>c.Game).FirstOrDefault(g=>
                    g.ProcessId==foreground.ProcessId)??foreground;
            return selected??GameSnapshot.None;
        }
        catch{return GameSnapshot.None;}
        finally{foreach(var p in processes)p.Dispose();}
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(
        IntPtr hwnd,out uint processId);

    internal static bool IsInstalledGamePath(string? raw)
    {
        if(string.IsNullOrWhiteSpace(raw)||
           !raw.EndsWith(".exe",StringComparison.OrdinalIgnoreCase))
            return false;
        string path=raw.Replace('/','\\').ToLowerInvariant();
        return path.Contains("\\steamapps\\common\\") ||
            path.Contains("\\epic games\\") ||
            path.Contains("\\riot games\\") ||
            path.Contains("\\xboxgames\\") ||
            path.Contains("\\gog galaxy\\games\\");
    }

    private static GameSnapshot DetectForegroundInstalledGame()
    {
        try
        {
            IntPtr handle=GetForegroundWindow();
            if(handle==IntPtr.Zero)return GameSnapshot.None;
            GetWindowThreadProcessId(handle,out uint pid);
            if(pid is 0 or >int.MaxValue)return GameSnapshot.None;
            using var process=Process.GetProcessById((int)pid);
            if(process.HasExited||string.IsNullOrWhiteSpace(
                process.MainWindowTitle))return GameSnapshot.None;
            string? path=process.MainModule?.FileName;
            if(!IsInstalledGamePath(path)||
               GenericExecutables.Contains(process.ProcessName))
                return GameSnapshot.None;
            string title=process.MainWindowTitle.Trim();
            // Never expose a potentially sensitive long window title.
            if(title.Length>80)title=
                Path.GetFileNameWithoutExtension(path)??process.ProcessName;
            return new(title,process.ProcessName,process.Id);
        }
        catch(Exception ex) when(ex is System.ComponentModel.Win32Exception or
            InvalidOperationException or ArgumentException or
            UnauthorizedAccessException or System.IO.IOException)
        {return GameSnapshot.None;}
    }

    /// <summary>
    /// Reuses GG's known local registered game executable. An icon is NOT
    /// supplied by GG; the provider extracts the Windows icon from this path.
    /// </summary>
    internal static string? RegisteredExecutable(GameSnapshot game)
    {
        if(!game.IsRunning)return null;
        var known=(DateTimeOffset.Now-cachedAt).TotalSeconds<40
            ?games:LoadGgLibrary();
        foreach(var (name,exe) in known)
            if(string.Equals(name,game.Name,StringComparison.OrdinalIgnoreCase)&&
               string.Equals(Path.GetFileNameWithoutExtension(exe),
                   game.Executable,StringComparison.OrdinalIgnoreCase)&&
               File.Exists(exe))
                return exe;
        // A Steam/Epic/Riot/XboxGames foreground fallback can supply a
        // real executable icon without GG database access.
        try
        {
            using var process=Process.GetProcessById(game.ProcessId);
            string? path=process.MainModule?.FileName;
            if(IsInstalledGamePath(path)&&
               string.Equals(process.ProcessName,game.Executable,
                   StringComparison.OrdinalIgnoreCase)&&File.Exists(path))
                return path;
        }
        catch(Exception ex) when(ex is System.ComponentModel.Win32Exception or
            InvalidOperationException or ArgumentException or
            UnauthorizedAccessException or System.IO.IOException){}
        return null;
    }

    internal static bool NamesMatchPath(string actual,string registered)
    {
        // GG's Windows library sometimes normalizes/removes whitespace from
        // installation folder components. Compare executable filenames and
        // tolerate normalization differences in parent folders.
        string a=Path.GetFileName(actual.Replace('/','\\'));
        string b=Path.GetFileName(registered.Replace('/','\\'));
        return string.Equals(a,b,StringComparison.OrdinalIgnoreCase);
    }

    internal static List<(string Name,string Exe)> LoadGgLibrary()
    {
        var result=new List<(string,string)>();
        if(!File.Exists(Database))return result;
        IntPtr db=IntPtr.Zero,query=IntPtr.Zero;
        try
        {
            if(SqliteOpen(Database,out db,1,IntPtr.Zero)!=0 || db==IntPtr.Zero)return result;
            const string sql="SELECT name, exe_path FROM library_scan "+
                "WHERE uninstalled=0 AND name IS NOT NULL AND exe_path IS NOT NULL";
            if(SqlitePrepare(db,sql,-1,out query,IntPtr.Zero)!=0 || query==IntPtr.Zero)
                return result;
            int step;
            while(result.Count<1200 && (step=SqliteStep(query))==100)
            {
                string? name=Marshal.PtrToStringUTF8(SqliteColumnText(query,0));
                string? exe=Marshal.PtrToStringUTF8(SqliteColumnText(query,1));
                if(!string.IsNullOrWhiteSpace(name)&&!string.IsNullOrWhiteSpace(exe))
                {
                    exe=exe.Replace('/','\\').Trim();
                    string filename=Path.GetFileNameWithoutExtension(exe);
                    if(filename.Length>=3&&!GenericExecutables.Contains(filename))
                        result.Add((name.Trim(),exe));
                }
            }
        }
        catch(DllNotFoundException){}
        catch(EntryPointNotFoundException){}
        catch(AccessViolationException){}
        finally
        {
            if(query!=IntPtr.Zero)SqliteFinalize(query);
            if(db!=IntPtr.Zero)SqliteClose(db);
        }
        return result;
    }

    [DllImport("winsqlite3.dll",EntryPoint="sqlite3_open_v2",
        CallingConvention=CallingConvention.Cdecl,CharSet=CharSet.Ansi)]
    private static extern int SqliteOpen(string file,out IntPtr db,int flags,IntPtr vfs);
    [DllImport("winsqlite3.dll",EntryPoint="sqlite3_prepare_v2",CallingConvention=CallingConvention.Cdecl,
        CharSet=CharSet.Ansi)]
    private static extern int SqlitePrepare(IntPtr db,string sql,int bytes,out IntPtr stmt,IntPtr tail);
    [DllImport("winsqlite3.dll",EntryPoint="sqlite3_step",CallingConvention=CallingConvention.Cdecl)]
    private static extern int SqliteStep(IntPtr stmt);
    [DllImport("winsqlite3.dll",EntryPoint="sqlite3_column_text",CallingConvention=CallingConvention.Cdecl)]
    private static extern IntPtr SqliteColumnText(IntPtr stmt,int column);
    [DllImport("winsqlite3.dll",EntryPoint="sqlite3_finalize",CallingConvention=CallingConvention.Cdecl)]
    private static extern int SqliteFinalize(IntPtr stmt);
    [DllImport("winsqlite3.dll",EntryPoint="sqlite3_close",CallingConvention=CallingConvention.Cdecl)]
    private static extern int SqliteClose(IntPtr db);
}

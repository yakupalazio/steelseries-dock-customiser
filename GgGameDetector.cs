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
    private static List<(string Name,string Directory)> directoryRegistrations=[];
    private static List<(string Name,string Artwork)> registeredArtwork=[];
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
                        string? processPath=ReadOnlyProcessPath(process);
                        // Maintain GG's executable-name fallback if Windows
                        // protects the process image path from inspection.
                        if(!string.IsNullOrEmpty(processPath) &&
                           !NamesMatchPath(processPath,exe))continue;
                        DateTime start;
                        try{start=process.StartTime;}catch{start=DateTime.MinValue;}
                        candidates.Add((new GameSnapshot(name,processName,process.Id),start));
                    }
                    catch(InvalidOperationException){}
                    catch(System.ComponentModel.Win32Exception){}
                }
            }
            // Some EA App / GG library entries provide a registered game
            // directory but leave exe_path empty. Verify BOTH the live
            // process executable and the matching registered installation
            // directory. A launcher or unrelated EA background process is
            // not treated as a running game.
            // Cheap basename shortlist first; never open hundreds of
            // unrelated process image handles on every three-second poll.
            var eaRegisteredNames=directoryRegistrations
                .Select(x=>NormalizeGameIdentity(x.Name))
                .Where(x=>x.Length>=5)
                .SelectMany(x=>new[]{x,x+"trial"})
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach(var process in processes)
            {
                try
                {
                    if(!eaRegisteredNames.Contains(
                           NormalizeGameIdentity(process.ProcessName))||
                       process.HasExited||
                       GenericExecutables.Contains(process.ProcessName))continue;
                    string? actual=ReadOnlyProcessPath(process);
                    if(string.IsNullOrWhiteSpace(actual)||!File.Exists(actual)||
                       !IsInstalledGamePath(actual))continue;
                    string? recognized=DirectoryRegisteredGameName(
                        actual,directoryRegistrations);
                    if(recognized is null)continue;
                    DateTime started;
                    try{started=process.StartTime;}
                    catch{started=DateTime.MinValue;}
                    candidates.Add((new GameSnapshot(recognized,
                        process.ProcessName,process.Id),started));
                }
                catch(System.ComponentModel.Win32Exception){}
                catch(InvalidOperationException){}
                catch(UnauthorizedAccessException){}
                catch(System.IO.IOException){}
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

    // Read-only Windows API fallback for anti-cheat processes that disallow
    // .NET Process.MainModule despite permitting limited image-path queries.
    // Never opens game memory or asks for VM_READ/write permissions.
    private const uint ProcessQueryLimitedInformation=0x1000;
    [DllImport("kernel32.dll",SetLastError=true)]
    private static extern IntPtr OpenProcess(
        uint access,bool inheritHandle,int processId);
    [DllImport("kernel32.dll",EntryPoint="QueryFullProcessImageNameW",
        CharSet=CharSet.Unicode,SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageName(
        IntPtr process,uint flags,System.Text.StringBuilder path,ref uint chars);
    [DllImport("kernel32.dll",SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

    internal static string? ReadOnlyProcessPath(Process process)
    {
        try
        {
            string? native=process.MainModule?.FileName;
            if(!string.IsNullOrWhiteSpace(native))return native;
        }
        catch(System.ComponentModel.Win32Exception){}
        catch(InvalidOperationException){}
        catch(UnauthorizedAccessException){}
        IntPtr handle=IntPtr.Zero;
        try
        {
            handle=OpenProcess(ProcessQueryLimitedInformation,false,process.Id);
            if(handle==IntPtr.Zero)return null;
            var buffer=new System.Text.StringBuilder(32768);
            uint length=(uint)buffer.Capacity;
            return QueryFullProcessImageName(handle,0,buffer,ref length)
                &&length>0?buffer.ToString():null;
        }
        catch(InvalidOperationException){return null;}
        catch(System.ComponentModel.Win32Exception){return null;}
        finally {if(handle!=IntPtr.Zero)CloseHandle(handle);}
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
            path.Contains("\\gog galaxy\\games\\") ||
            path.Contains("\\ea games\\");
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
            string? path=ReadOnlyProcessPath(process);
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

    // Normalize only for matching a verified live process directory against
    // GG's sometimes whitespace-stripped installation metadata.
    internal static string NormalizeRegisteredPath(string path)=>
        new string(path.Replace('/','\\').ToLowerInvariant()
            .Where(char.IsLetterOrDigit).ToArray());

    internal static string NormalizeGameIdentity(string name)=>
        new string(name.ToLowerInvariant()
            .Where(c=>char.IsAsciiLetterOrDigit(c)).ToArray());

    internal static string? DirectoryRegisteredGameName(string actualExe,
        IEnumerable<(string Name,string Directory)> registrations)
    {
        if(!IsInstalledGamePath(actualExe)||
           !actualExe.EndsWith(".exe",StringComparison.OrdinalIgnoreCase))
            return null;
        string? directory=Path.GetDirectoryName(actualExe);
        if(string.IsNullOrWhiteSpace(directory))return null;
        string folder=NormalizeRegisteredPath(directory);
        string basename=NormalizeGameIdentity(
            Path.GetFileNameWithoutExtension(actualExe));
        if(basename.Length<5||GenericExecutables.Contains(basename))
            return null;
        foreach(var (name,registeredDirectory) in registrations)
        {
            string title=NormalizeGameIdentity(name);
            // Some EA installs ship a genuine *Trial.exe variant next to
            // the full game. It must still match the same registered folder.
            bool gameBinary=basename==title || basename==title+"trial";
            if(title.Length<5||!gameBinary ||
               NormalizeRegisteredPath(registeredDirectory)!=folder)
                continue;
            return name;
        }
        return null;
    }

    internal static string? RegisteredArtwork(GameSnapshot game)
    {
        if(!game.IsRunning)return null;
        if((DateTimeOffset.Now-cachedAt).TotalSeconds>40)
        {
            games=LoadGgLibrary();
            cachedAt=DateTimeOffset.Now;
        }
        const string acceptedRoot=@"C:\ProgramData\SteelSeries\GG\shared\externalAppIcons";
        string root=Path.GetFullPath(acceptedRoot)
            .TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
        foreach(var (name,artwork) in registeredArtwork)
        {
            if(!string.Equals(name,game.Name,StringComparison.OrdinalIgnoreCase))
                continue;
            try
            {
                string full=Path.GetFullPath(artwork);
                if(!full.StartsWith(root,StringComparison.OrdinalIgnoreCase) ||
                   !File.Exists(full)||
                   Path.GetExtension(full).ToLowerInvariant() is not
                       (".png" or ".ico" or ".bmp" or ".jpg" or ".jpeg"))
                    continue;
                return full;
            }
            catch(Exception ex) when(ex is IOException or
                UnauthorizedAccessException or ArgumentException){}
        }
        return null;
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
            string? path=ReadOnlyProcessPath(process);
            // The selected PID already comes from GG or the verified
            // foreground-game detector. Microsoft Store game EXEs live in
            // WindowsApps rather than Steam/Epic/GOG folders; don't reject
            // their readable local executable solely because of that path.
            if(string.Equals(process.ProcessName,game.Executable,
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
        var missingExe=new List<(string Name,string Directory)>();
        var artwork=new List<(string Name,string Artwork)>();
        if(!File.Exists(Database))return result;
        IntPtr db=IntPtr.Zero,query=IntPtr.Zero;
        try
        {
            if(SqliteOpen(Database,out db,1,IntPtr.Zero)!=0 || db==IntPtr.Zero)
                return result;
            const string sql="SELECT name, exe_path, directory, image_path "+
                "FROM library_scan WHERE uninstalled=0 AND name IS NOT NULL";
            if(SqlitePrepare(db,sql,-1,out query,IntPtr.Zero)!=0 ||
               query==IntPtr.Zero)return result;
            int step,count=0;
            while(count++<1200 && (step=SqliteStep(query))==100)
            {
                string? name=Marshal.PtrToStringUTF8(SqliteColumnText(query,0));
                string? exe=Marshal.PtrToStringUTF8(SqliteColumnText(query,1));
                string? directory=Marshal.PtrToStringUTF8(SqliteColumnText(query,2));
                string? image=Marshal.PtrToStringUTF8(SqliteColumnText(query,3));
                if(string.IsNullOrWhiteSpace(name))continue;
                name=name.Trim();
                if(!string.IsNullOrWhiteSpace(image))
                    artwork.Add((name,image.Trim()));
                if(string.IsNullOrWhiteSpace(exe))
                {
                    if(!string.IsNullOrWhiteSpace(directory))
                        missingExe.Add((name,directory.Trim()));
                    continue;
                }
                exe=exe.Replace('/','\\').Trim();
                string filename=Path.GetFileNameWithoutExtension(exe);
                if(filename.Length>=3&&!GenericExecutables.Contains(filename))
                    result.Add((name,exe));
            }
            // Atomically publish both auxiliary snapshots after a successful
            // read. GG database remains strictly read-only.
            directoryRegistrations=missingExe;
            registeredArtwork=artwork;
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

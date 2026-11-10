using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using System.Threading.Tasks;

namespace SteelseriesDockCustomiser;

internal sealed record GameSnapshot(bool Active, string Name, string Executable, DateTimeOffset CheckedAt)
{
    public static GameSnapshot None => new(false,"","",DateTimeOffset.MinValue);
}

/// <summary>
/// Identifies running games by matching active process executable paths with
/// SteelSeries GG Moments' local detected-games library (read-only SQLite).
/// Does not hook a game's process, alter GG profiles or inspect game memory.
/// Poll every ~5s; retain game mode until the actual game process exits.
/// </summary>
internal static class GameDetector
{
    private static readonly string Database=Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "SteelSeries","GG","apps","moments","db","database.db");

    private sealed record KnownGame(string Name,string FullPath,string FileName);
    private static DateTimeOffset lastCatalog=DateTimeOffset.MinValue;
    private static List<KnownGame> known=[];
    private static readonly HashSet<string> NonGameLaunchers=
        new(StringComparer.OrdinalIgnoreCase) {
            "launcher.exe","start.exe","game.exe","unity.exe","steam.exe",
            "epicgameslauncher.exe","leagueclient.exe","leagueclientux.exe",
            "riotclientservices.exe","battle.net.exe","upc.exe"
        };

    internal static string? MatchGame(IReadOnlyList<(string ProcessName,string? FullPath)> processes,
        IReadOnlyList<(string Name,string FullPath)> games)
    {
        foreach(var proc in processes)
        foreach(var game in games)
        {
            string exe=Path.GetFileName(game.FullPath);
            if(string.IsNullOrWhiteSpace(exe)||!exe.EndsWith(".exe",StringComparison.OrdinalIgnoreCase))
                continue;
            string processExe=proc.ProcessName.EndsWith(".exe",StringComparison.OrdinalIgnoreCase)
                ? proc.ProcessName : proc.ProcessName+".exe";
            if(!string.Equals(exe,processExe,StringComparison.OrdinalIgnoreCase))continue;
            if(!string.IsNullOrWhiteSpace(proc.FullPath))
            {
                if(!string.Equals(Path.GetFullPath(proc.FullPath),Path.GetFullPath(game.FullPath),
                    StringComparison.OrdinalIgnoreCase))continue;
            }
            else if(NonGameLaunchers.Contains(exe)) continue; // Do not guess on generic names.
            return game.Name;
        }
        return null;
    }

    public static Task<GameSnapshot> PollAsync() => Task.Run(PollSync);
    private static GameSnapshot PollSync()
    {
        var now=DateTimeOffset.Now;
        try
        {
            if((now-lastCatalog).TotalSeconds>60)
            {
                // Keep the previous known-game list if the GG DB is temporarily locked.
                var fresh=ReadCatalog();
                if(fresh.Count>0)known=fresh;
                lastCatalog=now;
            }
            using var processes=new ProcessCollection(known.Select(x=>
                Path.GetFileNameWithoutExtension(x.FileName)));
            string? game=MatchGame(processes.Processes,
                known.Select(x=>(x.Name,x.FullPath)).ToArray());
            if(game is not null)
            {
                var match=known.FirstOrDefault(x=>x.Name==game);
                return new(true,game,match?.FileName??"",now);
            }
        }
        catch(Exception){ }
        return GameSnapshot.None;
    }

    private static List<KnownGame> ReadCatalog()
    {
        var list=new List<KnownGame>();
        if(!File.Exists(Database))return list;
        var builder=new SqliteConnectionStringBuilder{
            DataSource=Database,Mode=SqliteOpenMode.ReadOnly,Pooling=false,
            Cache=SqliteCacheMode.Private,DefaultTimeout=2
        };
        using var db=new SqliteConnection(builder.ToString());
        db.Open();
        using var command=db.CreateCommand();
        command.CommandText=@"SELECT name,path FROM moments_games
                             WHERE is_installed=1 AND name IS NOT NULL
                               AND path IS NOT NULL LIMIT 500";
        using var reader=command.ExecuteReader();
        while(reader.Read())
        {
            var name=reader.GetString(0).Trim();
            var path=reader.GetString(1).Trim();
            if(name.Length<2||name.Length>110||!Path.IsPathFullyQualified(path)||
               !path.EndsWith(".exe",StringComparison.OrdinalIgnoreCase))continue;
            list.Add(new(name,path,Path.GetFileName(path)));
        }
        return list;
    }

    private sealed class ProcessCollection : IDisposable
    {
        private readonly Process[] processes=Process.GetProcesses();
        private readonly HashSet<string> targetNames;
        public ProcessCollection(IEnumerable<string> names)
        {
            targetNames=new HashSet<string>(names,StringComparer.OrdinalIgnoreCase);
        }
        public IReadOnlyList<(string ProcessName,string? FullPath)> Processes
        {
            get
            {
                var list=new List<(string,string?)>();
                foreach(var p in processes)
                {
                    if(!targetNames.Contains(p.ProcessName))continue;
                    string? path=null;
                    try { path=p.MainModule?.FileName; }catch{}
                    try { list.Add((p.ProcessName,path)); }catch{}
                }
                return list;
            }
        }
        public void Dispose() { foreach(var p in processes)p.Dispose(); }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SteelseriesDockCustomiser;

/// <summary>
/// Private on-disk cache of time-coded lyrics already matched to a song.
/// No lyrics texts or account IDs are included with distribution, and nothing
/// is transmitted by reading/writing this local cache.
/// </summary>
internal static class OfflineLyricsCache
{
    private sealed class CacheEntry
    {
        public string TrackKey {get;set;}="";
        public string Source {get;set;}="";
        public DateTimeOffset SavedAt {get;set;}
        public List<SyncedLyricLine> Lines {get;set;}=[];
    }
    private static string Root=>Path.Combine(
        Beta5Sandbox.SettingsDirectory,"lyrics-cache-v1");

    private static string FileFor(string key,string? directory=null)
    {
        string hash=Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(key)));
        return Path.Combine(directory??Root,hash+".json");
    }

    internal static SyncedLyricsSnapshot? Read(
        string trackKey,string? directory=null)
    {
        if(string.IsNullOrWhiteSpace(trackKey)||trackKey.Length>512)return null;
        try
        {
            string file=FileFor(trackKey,directory);
            if(!File.Exists(file)||new FileInfo(file).Length>220_000)return null;
            var record=JsonSerializer.Deserialize<CacheEntry>(
                File.ReadAllText(file));
            if(record is null||record.TrackKey!=trackKey||
               record.Lines is null||record.Lines.Count is <1 or >750)
                return null;
            TimeSpan prior=TimeSpan.MinValue;
            foreach(var line in record.Lines)
            {
                if(line is null||line.At<TimeSpan.Zero||
                   line.At<prior||line.At>TimeSpan.FromHours(4)||
                   line.Text is null||line.Text.Length>160)return null;
                prior=line.At;
            }
            return new(trackKey,record.Lines,"Offline cache / "+
                ((record.Source??"").Length>60
                    ?record.Source![..60]:record.Source??""));
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException
            or JsonException or ArgumentException or NotSupportedException)
        {return null;}
    }

    internal static bool Store(
        SyncedLyricsSnapshot value,string? directory=null)
    {
        if(value.Lines.Count is <1 or >750||
           string.IsNullOrWhiteSpace(value.TrackKey)||
           value.TrackKey.Length>512||
           value.Lines.Any(x=>x.Text.Length>160))return false;
        string? temp=null;
        try
        {
            string folder=directory??Root;
            Directory.CreateDirectory(folder);
            string path=FileFor(value.TrackKey,folder);
            var entry=new CacheEntry{
                TrackKey=value.TrackKey,
                Source=value.Source,
                SavedAt=DateTimeOffset.UtcNow,
                Lines=value.Lines.ToList()
            };
            string json=JsonSerializer.Serialize(entry);
            if(Encoding.UTF8.GetByteCount(json)>220_000)return false;
            temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            File.WriteAllText(temp,json,Encoding.UTF8);
            File.Move(temp,path,true);
            Prune(folder);
            return true;
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException
            or ArgumentException or NotSupportedException)
        {return false;}
        finally
        {
            try{if(temp is not null&&File.Exists(temp))File.Delete(temp);}
            catch{}
        }
    }

    private static void Prune(string folder)
    {
        try
        {
            var all=new DirectoryInfo(folder).GetFiles("*.json")
                .OrderByDescending(f=>f.LastWriteTimeUtc).ToArray();
            // Approximately 200 KB per track max, but capped at 150 tracks
            // and 12 MB total. Older cached tracks are evicted first.
            long size=0;
            for(int i=0;i<all.Length;i++)
            {
                size+=all[i].Length;
                if(i>=150||size>12_000_000)all[i].Delete();
            }
        }
        catch(IOException){}
        catch(UnauthorizedAccessException){}
    }
}

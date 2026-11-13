using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace SteelseriesDockCustomiser;

internal sealed record SyncedLyricLine(TimeSpan At,string Text);
internal sealed record SyncedLyricsSnapshot(
    string TrackKey,IReadOnlyList<SyncedLyricLine> Lines,string Source)
{
    public static SyncedLyricsSnapshot Empty {get;}=new(
        "",Array.Empty<SyncedLyricLine>(),"");
}

/// <summary>
/// Opt-in time-coded lyrics from an independent public lyrics database.
/// Spotify is used only as a Windows media metadata source; no Spotify
/// lyrics scraping, private API, credentials or copyrighted text is shipped.
/// </summary>
internal static class SyncedLyricsProvider
{
    private static readonly HttpClient Client=CreateClient();
    private static readonly SemaphoreSlim Gate=new(1,1);
    private static readonly Regex Timestamp=new(
        @"\[(?<m>\d{1,3}):(?<s>\d{2})(?:[.](?<fraction>\d{1,3}))?\]",
        RegexOptions.Compiled|RegexOptions.CultureInvariant);
    private static readonly Dictionary<string,(SyncedLyricsSnapshot Result,
        DateTimeOffset Expires)> Cache=new(StringComparer.Ordinal);
    private static DateTimeOffset retryAfter=DateTimeOffset.MinValue;
    private static SyncedLyricsSnapshot current=SyncedLyricsSnapshot.Empty;

    private static HttpClient CreateClient()
    {
        var c=new HttpClient{Timeout=TimeSpan.FromSeconds(5)};
        c.DefaultRequestHeaders.UserAgent.ParseAdd(
            "SteelseriesDockCustomiser/1.1.1 (independent Windows OLED app; github.com)");
        c.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        return c;
    }

    internal static string Key(MediaSnapshot track)=>
        track.Title.Trim().ToLowerInvariant()+"|"+
        track.Artist.Trim().ToLowerInvariant()+"|"+
        Math.Round(track.Duration.TotalSeconds);

    internal static SyncedLyricsSnapshot Current(MediaSnapshot? track)
    {
        if(track?.IsPlaying!=true)return SyncedLyricsSnapshot.Empty;
        var snapshot=Volatile.Read(ref current);
        return snapshot.TrackKey==Key(track)?snapshot:SyncedLyricsSnapshot.Empty;
    }

    internal static async Task EnsureAsync(MediaSnapshot track)
    {
        if(!track.IsPlaying||string.IsNullOrWhiteSpace(track.Title)||
           string.IsNullOrWhiteSpace(track.Artist))return;
        string key=Key(track);
        if(Volatile.Read(ref current).TrackKey==key)return;
        if(!await Gate.WaitAsync(0))return;
        try
        {
            if(current.TrackKey==key)return;
            if(Cache.TryGetValue(key,out var cached)&&
               cached.Expires>DateTimeOffset.UtcNow)
            {
                Volatile.Write(ref current,cached.Result);
                return;
            }
            if(DateTimeOffset.UtcNow<retryAfter)return;
            var found=await FetchAsync(track,key);
            Volatile.Write(ref current,found);
            if(Cache.Count>=48)Cache.Clear();
            Cache[key]=(found,DateTimeOffset.UtcNow+
                (found.Lines.Count>0?TimeSpan.FromHours(2):TimeSpan.FromMinutes(10)));
        }
        finally{Gate.Release();}
    }

    private static async Task<SyncedLyricsSnapshot> FetchAsync(
        MediaSnapshot track,string key)
    {
        try
        {
            var query="track_name="+Uri.EscapeDataString(track.Title)+
                "&artist_name="+Uri.EscapeDataString(track.Artist);
            if(track.Duration.TotalSeconds is >1 and <3600)
                query+="&duration="+
                    Math.Round(track.Duration.TotalSeconds).ToString(
                        CultureInfo.InvariantCulture);
            using var response=await Client.GetAsync(
                new Uri("https://lrclib.net/api/get?"+query),
                HttpCompletionOption.ResponseHeadersRead);
            if(response.StatusCode==(HttpStatusCode)429)
            {
                TimeSpan delay=response.Headers.RetryAfter?.Delta??
                    TimeSpan.FromMinutes(2);
                retryAfter=DateTimeOffset.UtcNow+TimeSpan.FromSeconds(
                    Math.Clamp(delay.TotalSeconds,10,3600));
                return new(key,Array.Empty<SyncedLyricLine>(),"rate-limited");
            }
            if(!response.IsSuccessStatusCode)return new(
                key,Array.Empty<SyncedLyricLine>(),"not-found");
            if(response.Content.Headers.ContentLength is long size&&size>256000)
                return new(key,Array.Empty<SyncedLyricLine>(),"too-large");
            using var stream=await response.Content.ReadAsStreamAsync();
            using var doc=await JsonDocument.ParseAsync(stream,
                new JsonDocumentOptions{MaxDepth=12});
            var root=doc.RootElement;
            string foundTitle=GetText(root,"trackName");
            string foundArtist=GetText(root,"artistName");
            if(!string.Equals(foundTitle.Trim(),track.Title.Trim(),
                   StringComparison.OrdinalIgnoreCase) ||
               !string.Equals(foundArtist.Trim(),track.Artist.Trim(),
                   StringComparison.OrdinalIgnoreCase))
                return new(key,Array.Empty<SyncedLyricLine>(),"mismatch");
            double remoteDuration=GetDouble(root,"duration");
            if(remoteDuration>0&&track.Duration.TotalSeconds>0&&
               Math.Abs(remoteDuration-track.Duration.TotalSeconds)>3)
                return new(key,Array.Empty<SyncedLyricLine>(),"duration-mismatch");
            string lyrics=GetText(root,"syncedLyrics");
            return new(key,Parse(lyrics),"LRCLIB");
        }
        catch(HttpRequestException){return new(key,Array.Empty<SyncedLyricLine>(),"network");}
        catch(TaskCanceledException){return new(key,Array.Empty<SyncedLyricLine>(),"timeout");}
        catch(JsonException){return new(key,Array.Empty<SyncedLyricLine>(),"invalid-json");}
    }

    private static string GetText(JsonElement root,string key)=>
        root.TryGetProperty(key,out var p)&&p.ValueKind==JsonValueKind.String
            ?p.GetString()??"":"";
    private static double GetDouble(JsonElement root,string key)=>
        root.TryGetProperty(key,out var p)&&p.ValueKind==JsonValueKind.Number&&
        p.TryGetDouble(out var v)?v:0;

    internal static IReadOnlyList<SyncedLyricLine> Parse(string? lrc)
    {
        if(string.IsNullOrWhiteSpace(lrc)||lrc.Length>256000)
            return Array.Empty<SyncedLyricLine>();
        var lines=new List<SyncedLyricLine>();
        foreach(var raw in lrc.Replace("\r","").Split('\n').Take(1500))
        {
            var matches=Timestamp.Matches(raw);
            if(matches.Count==0)continue;
            string sentence=Timestamp.Replace(raw,"").Trim();
            if(sentence.Length==0)continue;
            sentence=sentence[..Math.Min(160,sentence.Length)];
            foreach(Match stamp in matches)
            {
                int minutes=int.Parse(stamp.Groups["m"].Value,
                    CultureInfo.InvariantCulture);
                int seconds=int.Parse(stamp.Groups["s"].Value,
                    CultureInfo.InvariantCulture);
                if(seconds>59)continue;
                string fract=stamp.Groups["fraction"].Value.PadRight(3,'0');
                int milliseconds=fract.Length==0?0:int.Parse(
                    fract,CultureInfo.InvariantCulture);
                lines.Add(new(TimeSpan.FromMilliseconds(
                    (minutes*60+seconds)*1000+milliseconds),sentence));
                if(lines.Count>=750)break;
            }
            if(lines.Count>=750)break;
        }
        return lines.OrderBy(x=>x.At).ToArray();
    }

    internal static string AtTime(IReadOnlyList<SyncedLyricLine> lines,
        TimeSpan position,int offsetMs)
    {
        if(lines.Count==0)return "";
        // Negative offset means lead/early; positive means delay/late.
        long at=(long)position.TotalMilliseconds-
            Math.Clamp(offsetMs,-10000,10000);
        int left=0,right=lines.Count-1,match=-1;
        while(left<=right)
        {
            int mid=(left+right)/2;
            if(lines[mid].At.TotalMilliseconds<=at)
            {match=mid;left=mid+1;}
            else right=mid-1;
        }
        return match>=0?lines[match].Text:"";
    }

    internal static TimeSpan PositionNow(MediaSnapshot track)
    {
        if(!track.IsPlaying)return track.Position;
        TimeSpan elapsed=track.SampledAt==DateTimeOffset.MinValue
            ?TimeSpan.Zero:DateTimeOffset.Now-track.SampledAt;
        TimeSpan estimate=track.Position+
            (elapsed>TimeSpan.Zero?elapsed:TimeSpan.Zero);
        // A media player can keep the Playing flag while its timeline
        // transitions at the end of a song. Never exceed its duration.
        return track.Duration>TimeSpan.Zero&&estimate>track.Duration
            ?track.Duration:estimate;
    }
}

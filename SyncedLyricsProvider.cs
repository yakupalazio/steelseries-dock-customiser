using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.IO;
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
        if(Volatile.Read(ref current) is {Lines.Count:>0} currentResult &&
           currentResult.TrackKey==key)return;
        if(!await Gate.WaitAsync(0))return;
        try
        {
            if(current.TrackKey==key&&current.Lines.Count>0)return;
            if(Cache.TryGetValue(key,out var cached)&&
               cached.Expires>DateTimeOffset.UtcNow)
            {
                Volatile.Write(ref current,cached.Result);
                return;
            }
            // Disk-backed song matching survives restarts and connectivity
            // loss. No network access is necessary for a cached track.
            var offline=OfflineLyricsCache.Read(key);
            if(offline is not null)
            {
                Volatile.Write(ref current,offline);
                if(Cache.Count>=48)Cache.Clear();
                Cache[key]=(offline,DateTimeOffset.UtcNow+
                    TimeSpan.FromHours(4));
                return;
            }
            if(DateTimeOffset.UtcNow<retryAfter)return;
            var found=await FetchAsync(track,key);
            Volatile.Write(ref current,found);
            if(found.Lines.Count>0)OfflineLyricsCache.Store(found);
            if(Cache.Count>=48)Cache.Clear();
            Cache[key]=(found,DateTimeOffset.UtcNow+
                (found.Lines.Count>0?TimeSpan.FromHours(2):TimeSpan.FromMinutes(10)));
        }
        finally{Gate.Release();}
    }

    /// <summary>
    /// Explicit per-song offline LRC import. No network/Spotify endpoint,
    /// no file path stored. Import only into the currently playing song.
    /// </summary>
    internal static async Task<bool> ImportLocalLrcAsync(
        MediaSnapshot track,string lrcFile,string? cacheDirectoryForTest=null)
    {
        if(!track.IsPlaying || string.IsNullOrWhiteSpace(track.Title) ||
           string.IsNullOrWhiteSpace(track.Artist) ||
           !string.Equals(Path.GetExtension(lrcFile),".lrc",
               StringComparison.OrdinalIgnoreCase) ||
           !File.Exists(lrcFile)||
           new FileInfo(lrcFile).Length>256_000)return false;
        string source=await File.ReadAllTextAsync(lrcFile);
        var lines=Parse(source);
        if(lines.Count==0)return false;
        string key=Key(track);
        var synced=new SyncedLyricsSnapshot(key,lines,"Local LRC import");
        if(!await Gate.WaitAsync(TimeSpan.FromSeconds(3)))return false;
        try
        {
            if(!OfflineLyricsCache.Store(
                synced,cacheDirectoryForTest))return false;
            Cache[key]=(synced,DateTimeOffset.UtcNow+
                TimeSpan.FromHours(4));
            Volatile.Write(ref current,synced);
            return true;
        }
        finally{Gate.Release();}
    }

    private static async Task<SyncedLyricsSnapshot> FetchAsync(
        MediaSnapshot track,string key)
    {
        try
        {
            string query="track_name="+Uri.EscapeDataString(track.Title)+
                "&artist_name="+Uri.EscapeDataString(track.Artist);
            if(track.Duration.TotalSeconds is >1 and <3600)
                query+="&duration="+
                    Math.Round(track.Duration.TotalSeconds).ToString(
                        CultureInfo.InvariantCulture);
            using(var response=await Client.GetAsync(
                new Uri("https://lrclib.net/api/get?"+query),
                HttpCompletionOption.ResponseHeadersRead))
            {
                if(ApplyRetryAfter(response))
                    return new(key,Array.Empty<SyncedLyricLine>(),"rate-limited");
                if(response.IsSuccessStatusCode)
                {
                    using var doc=await ReadBoundedJson(response);
                    if(doc is not null &&
                       TrySelectTrack(doc.RootElement,track,out var lines))
                        return new(key,lines,"LRCLIB / exact");
                }
            }
            // LRCLIB /api/get requires a near-exact signature. On another PC
            // Windows/Spotify may publish "Remastered" suffixes, multiple
            // featured artists or duration rounded differently. Search for
            // timed records and check identity and duration locally.
            return await SearchFallbackAsync(track,key);
        }
        catch(HttpRequestException){return new(key,Array.Empty<SyncedLyricLine>(),"network");}
        catch(TaskCanceledException){return new(key,Array.Empty<SyncedLyricLine>(),"timeout");}
        catch(JsonException){return new(key,Array.Empty<SyncedLyricLine>(),"invalid-json");}
        catch(InvalidOperationException){return new(key,Array.Empty<SyncedLyricLine>(),"invalid-result");}
    }

    private static bool ApplyRetryAfter(HttpResponseMessage response)
    {
        if(response.StatusCode!=(HttpStatusCode)429)return false;
        TimeSpan delay=response.Headers.RetryAfter?.Delta??
            TimeSpan.FromMinutes(2);
        retryAfter=DateTimeOffset.UtcNow+TimeSpan.FromSeconds(
            Math.Clamp(delay.TotalSeconds,10,3600));
        return true;
    }

    private static async Task<JsonDocument?> ReadBoundedJson(
        HttpResponseMessage response)
    {
        if(response.Content.Headers.ContentLength is long size&&size>256000)
            return null;
        using var stream=await response.Content.ReadAsStreamAsync();
        using var memory=new System.IO.MemoryStream();
        byte[] buffer=new byte[8192];
        int read;
        while((read=await stream.ReadAsync(buffer))>0)
        {
            if(memory.Length+read>256000)return null;
            memory.Write(buffer,0,read);
        }
        memory.Position=0;
        return await JsonDocument.ParseAsync(memory,
            new JsonDocumentOptions{MaxDepth=12});
    }

    /// <summary>Only synced lyrics from a verified song may be displayed.</summary>
    internal static bool TrySelectTrack(JsonElement root,MediaSnapshot track,
        out IReadOnlyList<SyncedLyricLine> lines)
    {
        lines=Array.Empty<SyncedLyricLine>();
        if(root.ValueKind!=JsonValueKind.Object)return false;
        string title=GetText(root,"trackName"),artist=GetText(root,"artistName");
        if(!TitlesMatch(title,track.Title) ||
           !ArtistsMatch(artist,track.Artist))return false;
        double duration=GetDouble(root,"duration");
        if(duration>0&&track.Duration.TotalSeconds>0&&
           Math.Abs(duration-track.Duration.TotalSeconds)>7)return false;
        var parsed=Parse(GetText(root,"syncedLyrics"));
        if(parsed.Count==0)return false;
        lines=parsed;
        return true;
    }

    internal static bool TitlesMatch(string left,string right)
    {
        static string Normalize(string raw)
        {
            // Soundtrack/remaster display suffixes may differ between the
            // Windows media session and LRCLIB. Keep actual song names intact.
            raw=Regex.Replace(raw,
                @"\s*[-–]\s*(?:\d{4}\s*)?remaster(?:ed)?(?:\s+\d{4})?\s*$",
                "",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
            raw=Regex.Replace(raw,
                @"\s*[\[(]\s*(?:\d{4}\s*)?remaster(?:ed)?(?:\s+\d{4})?\s*[\])]",
                "",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
            return new string(raw.ToLowerInvariant()
                .Where(char.IsLetterOrDigit).ToArray());
        }
        string a=Normalize(left),b=Normalize(right);
        return a.Length>=2&&a==b;
    }

    internal static bool ArtistsMatch(string left,string right)
    {
        static string Primary(string name)
        {
            // Multi-artist metadata often includes comma-separated guests.
            name=Regex.Split(name,@"(?:,|;|/|&|\bfeat\.?\b|\bft\.?\b)",
                RegexOptions.IgnoreCase|RegexOptions.CultureInvariant)[0];
            return new string(name.ToLowerInvariant()
                .Where(char.IsLetterOrDigit).ToArray());
        }
        var a=Primary(left);var b=Primary(right);
        return a.Length>=2&&a==b;
    }

    private static async Task<SyncedLyricsSnapshot> SearchFallbackAsync(
        MediaSnapshot track,string key)
    {
        string title=track.Title.Trim();
        string artist=track.Artist.Trim();
        // Some SMTC clients append edition labels not present in LRCLIB.
        // Strip only non-semantic "remaster" suffixes for the query;
        // verified title/artist/duration checks still guard every result.
        string shortTitle=Regex.Replace(title,
            @"\s*[-–]\s*(?:\d{4}\s*)?remaster(?:ed)?(?:\s+\d{4})?\s*$",
            "",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
        shortTitle=Regex.Replace(shortTitle,
            @"\s*[\[(]\s*(?:\d{4}\s*)?remaster(?:ed)?(?:\s+\d{4})?\s*[\])]",
            "",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant).Trim();
        var queries=new List<string>{
            "track_name="+Uri.EscapeDataString(title)+
                "&artist_name="+Uri.EscapeDataString(artist)
        };
        if(shortTitle.Length>1 && shortTitle!=title)
            queries.Add("track_name="+Uri.EscapeDataString(shortTitle)+
                "&artist_name="+Uri.EscapeDataString(artist));
        queries.Add("track_name="+Uri.EscapeDataString(
            shortTitle.Length>1?shortTitle:title));
        var tries=queries.Distinct(StringComparer.Ordinal).Take(3);
        foreach(string query in tries)
        {
            // LRCLIB asks clients not to flood search endpoints.
            await Task.Delay(250);
            using var response=await Client.GetAsync(
                new Uri("https://lrclib.net/api/search?"+query),
                HttpCompletionOption.ResponseHeadersRead);
            if(ApplyRetryAfter(response))
                return new(key,Array.Empty<SyncedLyricLine>(),"rate-limited");
            if(!response.IsSuccessStatusCode)continue;
            using var doc=await ReadBoundedJson(response);
            if(doc is null||doc.RootElement.ValueKind!=JsonValueKind.Array)
                continue;
            IReadOnlyList<SyncedLyricLine>? best=null;
            double lowestDifference=double.MaxValue;
            foreach(var result in doc.RootElement.EnumerateArray().Take(20))
            {
                if(!TrySelectTrack(result,track,out var parsed))continue;
                double duration=GetDouble(result,"duration");
                double diff=duration>0&&track.Duration.TotalSeconds>0
                    ?Math.Abs(duration-track.Duration.TotalSeconds):0;
                if(diff<lowestDifference){best=parsed;lowestDifference=diff;}
            }
            if(best is {Count:>0})
                return new(key,best,"LRCLIB / verified search");
        }
        return new(key,Array.Empty<SyncedLyricLine>(),"not-found");
    }

    private static string GetText(JsonElement root,string key)=>
        root.TryGetProperty(key,out var p)&&p.ValueKind==JsonValueKind.String
            ?p.GetString()??"":"";
    private static double GetDouble(JsonElement root,string key)=>
        root.TryGetProperty(key,out var p)&&p.ValueKind==JsonValueKind.Number&&
        p.TryGetDouble(out var v)?v:0;

    private static bool IsMusicMarker(string text)
    {
        string value=text.Trim().Trim('[',']','(',')').Trim()
            .ToLowerInvariant();
        if(value.Length>0 && value.All(ch=>
            ch is '♪' or '♫' or '♬' or '♩' or ' ' or '.' or '-' or '…'))
            return true;
        return value.Length==0 || value is
            "♪" or "♫" or "🎵" or "🎶" or "♪ ♪" or
            "instrumental" or "instrumental break" or "music" or
            "music playing" or "solo" or "interlude" or
            "enstrümantal" or "müzik" or "gitar solo";
    }

    internal static bool InstrumentalAt(IReadOnlyList<SyncedLyricLine> lines,
        TimeSpan position)
    {
        if(lines.Count==0)return false;
        long ms=Math.Max(0,(long)position.TotalMilliseconds);
        int index=-1,lo=0,hi=lines.Count-1;
        while(lo<=hi)
        {
            int middle=lo+(hi-lo)/2;
            if(lines[middle].At.TotalMilliseconds<=ms)
            {index=middle;lo=middle+1;}
            else hi=middle-1;
        }
        if(index<0)return true; // Instrumental intro until first timecode.
        var line=lines[index];
        if(string.IsNullOrWhiteSpace(line.Text))return true;
        long start=(long)line.At.TotalMilliseconds;
        long next=index+1<lines.Count
            ?(long)lines[index+1].At.TotalMilliseconds:long.MaxValue;
        long expected=Math.Clamp(2500+line.Text.Length*90,6000,9000);
        // Long instrumental bridges or outro without an explicit blank LRC
        // marker: retire the old lyric but NEVER modify subsequent timecodes.
        return ms-start>expected && next-start>expected+4500;
    }

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
            // Empty LRC timestamps and explicit instrumental cues are
            // significant: they STOP the prior line, without shifting any
            // later timestamp. Never count them as spoken lyric lines.
            if(IsMusicMarker(sentence))sentence="";
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
        return match>=0&&!InstrumentalAt(lines,
            TimeSpan.FromMilliseconds(Math.Max(0,at)))
            ?lines[match].Text:"";
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

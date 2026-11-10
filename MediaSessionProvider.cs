using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.Media.Control;

namespace SteelseriesDockCustomiser;

/// <summary>Read-only Windows media session metadata. No media control or remote commands.</summary>
internal sealed record MediaSnapshot(
    bool IsPlaying, bool IsSpotify, string Title, string Artist,
    string SourceAppId, TimeSpan Position, TimeSpan Duration, DateTimeOffset SampledAt)
{
    public static MediaSnapshot Empty => new(false,false,"","","",
        TimeSpan.Zero,TimeSpan.Zero,DateTimeOffset.MinValue);

    public static bool IsSpotifyApp(string? source) =>
        !string.IsNullOrWhiteSpace(source) &&
        source.Contains("spotify",StringComparison.OrdinalIgnoreCase);

    public int ProgressPercent
    {
        get
        {
            if(Duration<=TimeSpan.Zero)return 0;
            var elapsed=Position;
            if(SampledAt!=DateTimeOffset.MinValue && IsPlaying)
                elapsed+=DateTimeOffset.Now-SampledAt;
            return Math.Clamp((int)Math.Round(elapsed.TotalMilliseconds*100.0/Duration.TotalMilliseconds),0,100);
        }
    }
}

internal static class MediaSessionProvider
{
    private static GlobalSystemMediaTransportControlsSessionManager? manager;

    public static async Task<MediaSnapshot> PollAsync()
    {
        try
        {
            manager ??= await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            var sessions=manager.GetSessions();
            // The system-selected session may be paused even when another session is playing.
            var playing=sessions.FirstOrDefault(session =>
                session.GetPlaybackInfo()?.PlaybackStatus ==
                GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing);
            if(playing is null) return MediaSnapshot.Empty;

            var props=await playing.TryGetMediaPropertiesAsync();
            var timeline=playing.GetTimelineProperties();
            TimeSpan duration=timeline.EndTime-timeline.StartTime;
            TimeSpan position=timeline.Position-timeline.StartTime;
            if(duration<TimeSpan.Zero)duration=TimeSpan.Zero;
            if(position<TimeSpan.Zero)position=TimeSpan.Zero;
            var appId=playing.SourceAppUserModelId??"";
            return new MediaSnapshot(
                true,MediaSnapshot.IsSpotifyApp(appId),
                Short(props.Title),Short(props.Artist),Short(appId),
                position,duration,DateTimeOffset.Now);
        }
        catch(Exception) // Unsupported or unavailable Windows media session.
        {
            manager=null;
            return MediaSnapshot.Empty;
        }
    }

    private static string Short(string? s)=>
        string.IsNullOrWhiteSpace(s)?"":s.Trim()[..Math.Min(s.Trim().Length,180)];
}

using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace SteelseriesDockCustomiser;

/// <summary>
/// Public beta feedback hand-off, no GitHub token or direct anonymous upload.
/// Only prepares a local draft/EXIF-free screenshot and opens the official
/// GitHub issue form. The user must sign in, choose the optional attachment
/// and press Submit on GitHub. A successful browser launch is NOT an upload.
/// </summary>
internal static class GitHubFeedbackBridge
{
    internal const string PublicRepository=
        "yakupalazio/steelseries-dock-customiser";
    internal const string IssuesUrl=
        "https://github.com/"+PublicRepository+"/issues";

    internal sealed record Prepared(
        Uri IssueUrl,string LocalDraft,string? CleanScreenshot,
        bool NeedsFullDescriptionPaste);

    internal static string IssueTemplate(BugReportService.Report report)=>
        report.Category=="Pil / Cihaz"?"beta_device.yml":"beta_bug.yml";

    internal static string FullDescription(BugReportService.Report report)=>
        report.Description;

    private static string QueryValue(string text)=>
        Uri.EscapeDataString(text);

    internal static (Uri Url,bool NeedsPaste) ComposeUrl(
        BugReportService.Report report)
    {
        string template=IssueTemplate(report);
        string label=report.Category=="Pil / Cihaz"
            ?"[Device beta] ":"[Beta bug] ";
        string title=label+report.Title;
        string prefix=IssuesUrl+"/new?template="+template+
            "&title="+QueryValue(title)+
            "&summary="+QueryValue(report.Title)+
            "&app_version="+QueryValue(report.AppVersion)+
            "&category="+QueryValue(report.Category);
        string suffix="&environment="+QueryValue(
            report.OperatingSystem??"Not shared");
        // GitHub rejects overly long URLs (414); never silently discard
        // a long report. If prefill is limited, explicitly require manual
        // paste of the complete original from the local app.
        string details=report.Description;
        string candidate=prefix+"&details="+QueryValue(details)+suffix;
        bool trimmed=candidate.Length>6800;
        if(trimmed)
        {
            details=report.Description[..Math.Min(800,
                report.Description.Length)]+
                "\n[Description shortened for browser URL. "+
                "Use 'Copy full description' in Dock Customiser and paste "+
                "the complete text here before submitting.]";
            candidate=prefix+"&details="+QueryValue(details)+suffix;
        }
        return (new Uri(candidate),trimmed);
    }

    internal static Prepared Prepare(BugReportService.Report report,
        string? testFolder=null)
    {
        var (url,needsPaste)=ComposeUrl(report);
        // Unsent draft is explicit. The JSON's user-chosen screenshot bytes
        // stay on this PC and are NEVER sent without user's browser action.
        string draft;
        if(testFolder is null)draft=BugReportService.SavePending(report);
        else
        {
            Directory.CreateDirectory(testFolder);
            draft=Path.Combine(testFolder,
                "unsent-"+Guid.NewGuid().ToString("N")+".json");
            File.WriteAllText(draft,
                System.Text.Json.JsonSerializer.Serialize(report));
        }
        string? screenshot=null;
        if(report.AttachmentBase64 is string base64)
        {
            byte[] bytes=Convert.FromBase64String(base64);
            if(bytes.Length>8_000_000 ||
               bytes.Length<8 ||bytes[0]!=137||bytes[1]!=80||
               bytes[2]!=78||bytes[3]!=71)
                throw new InvalidDataException(
                    "The prepared PNG is invalid or too large.");
            screenshot=Path.ChangeExtension(draft,".png");
            File.WriteAllBytes(screenshot,bytes);
        }
        return new(url,draft,screenshot,needsPaste);
    }

    internal static void OpenBrowser(Prepared prepared)
    {
        if(prepared.IssueUrl.Scheme!=Uri.UriSchemeHttps ||
           prepared.IssueUrl.Host!="github.com" ||
           !prepared.IssueUrl.AbsolutePath.StartsWith(
               "/"+PublicRepository+"/issues/new",
               StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "Untrusted feedback destination.");
        Process.Start(new ProcessStartInfo{
            FileName=prepared.IssueUrl.AbsoluteUri,
            UseShellExecute=true
        })?.Dispose();
        // NEVER call this a successful submission; there is no API token
        // and no automated file upload.
    }
}

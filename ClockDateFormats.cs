namespace SteelseriesDockCustomiser;
internal static class ClockDateFormats
{
    public static readonly string[] Clock = ["HH:mm","HH:mm:ss","hh:mm"];
    public static readonly string[] Date =
        ["dd/MM/yyyy","dd.MM.yyyy","yyyy-MM-dd","dd.MM.yy","MM/dd/yyyy"];

    public static string ClockFormat(string? format) =>
        format is not null && Clock.Contains(format) ? format : "HH:mm";
    public static string DateFormat(string? format) =>
        format is not null && Date.Contains(format) ? format : "dd/MM/yyyy";
}

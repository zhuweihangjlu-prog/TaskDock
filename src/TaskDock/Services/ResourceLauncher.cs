using System.Diagnostics;
using System.Text.RegularExpressions;

namespace TaskDock.Services;

public static partial class ResourceLauncher
{
    public static Uri? FindWebUri(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var match = WebUrlRegex().Match(text);
        if (!match.Success) return null;
        var candidate = match.Value.TrimEnd('.', ',', ';', '，', '。', '；', ')', '）', ']', '】');
        return Uri.TryCreate(candidate, UriKind.Absolute, out var uri) && IsWebUri(uri) ? uri : null;
    }

    public static Uri? NormalizeWebUri(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var candidate = text.Trim();
        if (!candidate.Contains("://", StringComparison.Ordinal)) candidate = $"https://{candidate}";
        return Uri.TryCreate(candidate, UriKind.Absolute, out var uri) && IsWebUri(uri) ? uri : null;
    }

    public static bool OpenWeb(string? text, bool findInsideText = false)
    {
        var uri = findInsideText ? FindWebUri(text) : NormalizeWebUri(text);
        if (uri is null) return false;
        Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        return true;
    }

    public static bool OpenFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return false;
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        return true;
    }

    private static bool IsWebUri(Uri uri) => uri.Scheme is "http" or "https";

    [GeneratedRegex("https?://[^\\s<>\\\"'，。；、）】》！？]+", RegexOptions.IgnoreCase)]
    private static partial Regex WebUrlRegex();
}

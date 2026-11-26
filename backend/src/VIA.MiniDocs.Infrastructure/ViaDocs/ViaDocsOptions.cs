using System.Text.RegularExpressions;

namespace VIA.MiniDocs.Infrastructure.ViaDocs;

internal sealed class ViaDocsOptions
{
    public bool Enabled { get; set; }
    public string? BaseAddress { get; set; }
    public bool InvitationsEnabled { get; set; }
    public int ConnectSeconds { get; set; } = 5;
    public int ShortRequestSeconds { get; set; } = 15;
    public int DownloadHeadersSeconds { get; set; } = 15;
    public int DownloadIdleSeconds { get; set; } = 30;
    public int DownloadSeconds { get; set; } = 120;
    public int UploadSeconds { get; set; } = 300;
    public int UploadIdleSeconds { get; set; } = 60;
    public int RenewalSeconds { get; set; } = 60;
    public int ErrorBytes { get; set; } = 16 * 1024;
    public int AuthBytes { get; set; } = 32 * 1024;
    public int MetadataBytes { get; set; } = 1024 * 1024;
    public int PreviewBytes { get; set; } = 4 * 1024 * 1024;
    public int CacheEntries { get; set; } = 256;
    public int ConcurrentLogins { get; set; } = 16;

    internal Uri? Root => ParseRoot(BaseAddress);

    internal bool IsValid() =>
        (!Enabled || Root is not null) && (!InvitationsEnabled || Enabled) &&
        ConnectSeconds is > 0 and <= 60 && ShortRequestSeconds is > 0 and <= 120 &&
        ConnectSeconds <= ShortRequestSeconds &&
        DownloadHeadersSeconds is > 0 && DownloadIdleSeconds is > 0 && DownloadSeconds is > 0 and <= 600 &&
        DownloadHeadersSeconds <= DownloadSeconds && DownloadIdleSeconds <= DownloadSeconds &&
        UploadSeconds is > 0 and <= 600 && UploadIdleSeconds > 0 && UploadIdleSeconds <= UploadSeconds &&
        RenewalSeconds is > 0 and <= 600 && ErrorBytes is > 0 and <= 16 * 1024 &&
        AuthBytes is > 0 and <= 32 * 1024 && MetadataBytes is > 0 and <= 1024 * 1024 &&
        PreviewBytes is > 0 and <= 4 * 1024 * 1024 && CacheEntries is > 0 and <= 16384 &&
        ConcurrentLogins is > 0 and <= 256 && ConcurrentLogins <= CacheEntries;

    private static Uri? ParseRoot(string? value)
    {
        if (value is null || value.Length > 2048 ||
            !Regex.IsMatch(value, @"\Ahttps://[A-Za-z0-9.\-:\[\]]+(?:/[A-Za-z0-9_~-]+)*/?\z",
                RegexOptions.IgnoreCase | RegexOptions.NonBacktracking) ||
            !Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.HostNameType == UriHostNameType.Unknown)
            return null;
        return new Uri(uri.AbsoluteUri.TrimEnd('/') + "/");
    }
}

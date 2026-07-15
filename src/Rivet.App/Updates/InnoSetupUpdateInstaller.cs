using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;

namespace Rivet.App.Updates;

/// <summary>
/// Downloads the released installer .exe, proves it is the file GitHub says it is, and runs it
/// silently.
///
/// This code downloads something and then executes it, so it is deliberately unforgiving:
/// HTTPS only, github.com hosts only, and the SHA-256 published with the release must match. A
/// release without a checksum is refused rather than trusted — `installer/release.ps1` always
/// publishes one, so a missing checksum means something is wrong, not that we should shrug.
/// </summary>
public sealed class InnoSetupUpdateInstaller : IUpdateInstaller
{
    private static readonly string[] AllowedHosts =
    [
        "github.com",
        "api.github.com",
        "objects.githubusercontent.com",
        "release-assets.githubusercontent.com"
    ];

    private readonly HttpClient _http;
    private readonly ILogger<InnoSetupUpdateInstaller> _logger;

    public InnoSetupUpdateInstaller(ILogger<InnoSetupUpdateInstaller> logger)
    {
        _logger = logger;
        _http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Rivet-Updater");
    }

    public async Task DownloadAndInstallAsync(
        UpdateInfo update,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        EnsureTrusted(update.DownloadUrl);

        if (update.ChecksumUrl is null)
            throw new InvalidOperationException(
                "This release publishes no checksum, so the installer cannot be verified. Update skipped.");

        EnsureTrusted(update.ChecksumUrl);

        var directory = Path.Combine(Path.GetTempPath(), "Rivet-update");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"Rivet-Setup-{update.Version}.exe");

        await DownloadAsync(update.DownloadUrl, path, progress, cancellationToken).ConfigureAwait(false);

        var expected = await ReadChecksumAsync(update.ChecksumUrl, cancellationToken).ConfigureAwait(false);
        var actual = await ComputeSha256Async(path, cancellationToken).ConfigureAwait(false);

        if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(path);
            throw new InvalidOperationException(
                $"The downloaded installer does not match its published checksum and was discarded. " +
                $"Expected {expected}, got {actual}.");
        }

        _logger.LogInformation("Update {Version} verified; launching the installer", update.Version);
        Launch(path);
    }

    private static void EnsureTrusted(Uri uri)
    {
        // An http:// URL, or a host we do not recognise, means someone is feeding us an installer
        // we did not publish. Refuse before a single byte is downloaded.
        if (uri.Scheme != Uri.UriSchemeHttps || !AllowedHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Refusing to download an update from {uri}.");
    }

    private async Task DownloadAsync(
        Uri uri,
        string path,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        using var response = await _http
            .GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? 0L;

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var destination = File.Create(path);

        var buffer = new byte[128 * 1024];
        long copied = 0;
        int read;

        while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            copied += read;

            if (total > 0)
                progress?.Report((double)copied / total);
        }
    }

    private async Task<string> ReadChecksumAsync(Uri uri, CancellationToken cancellationToken)
    {
        var text = await _http.GetStringAsync(uri, cancellationToken).ConfigureAwait(false);

        // Accept both a bare hash and the "<hash>  <filename>" shape that sha256sum writes.
        return text.Trim().Split((char[])[' ', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries)[0];
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }

    /// <summary>
    /// Runs the Inno Setup installer and gets out of the way: it cannot replace Rivet.App.exe
    /// while it is running, so the app must exit. /VERYSILENT installs without prompting — the
    /// user already agreed to this update. The installer's [Run] section relaunches Rivet once the
    /// files are in place, so a silent update ends with the app back open, not silently closed.
    /// </summary>
    private static void Launch(string installerPath)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = installerPath,
            Arguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART",
            UseShellExecute = true
        });

        Environment.Exit(0);
    }
}

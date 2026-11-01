using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Windows.Management.Deployment;

namespace CartonStash.Core.Services;

/// <summary>One file of the pinned WinGet release. The hash comes from GitHub's record of the release.</summary>
internal sealed record AppInstallerFile(Uri Url, string Sha256);

/// <summary>
/// Installs App Installer for the user who asks (006 R3.7). Only files matching the pinned hashes are installed. This uses
/// Windows' package API, not WinGet's, so it doesn't touch the WinGet catalogs (A2).
/// </summary>
/// <param name="addPackage">Installs a bundle with its dependencies. Returns null, or Windows' error text.</param>
internal sealed class AppInstallerSetup(
    HttpClient http, AppInstallerFile bundle, AppInstallerFile dependencies, Architecture architecture,
    Func<Uri, IReadOnlyList<Uri>, CancellationToken, Task<string?>> addPackage)
{
    private const string Release = "https://github.com/microsoft/winget-cli/releases/download/v1.29.380/";

    /// <summary>What the user is told before choosing to set up.</summary>
    public const string DownloadSize = "about 300 MB";

    public static AppInstallerSetup Default() => new(
        new HttpClient(),
        new(new(Release + "Microsoft.DesktopAppInstaller_8wekyb3d8bbwe.msixbundle"), "65DEA9C01CE08EE7B763366B27C0E651F97DB857C11CA9B9C301826C10092F2E"),
        new(new(Release + "DesktopAppInstaller_Dependencies.zip"), "BA875AFE9D190F61218985AC0292A99D1DB710BF93E13C68944CA9D89F0D82D1"),
        RuntimeInformation.OSArchitecture,
        AddPackageAsync);

    /// <summary>Downloads, checks and installs. Returns null on success, or a readable reason.</summary>
    public async Task<string?> RunAsync(IProgress<string> progress, CancellationToken cancellationToken)
    {
        var folder = Path.Combine(Path.GetTempPath(), $"CartonStash-setup-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(folder);
            var bundlePath = Path.Combine(folder, "AppInstaller.msixbundle");
            var zipPath = Path.Combine(folder, "Dependencies.zip");

            if (!await DownloadAsync(bundle, bundlePath, "App Installer", progress, cancellationToken)
                || !await DownloadAsync(dependencies, zipPath, "its dependencies", progress, cancellationToken))
            {
                return "The download didn't match Microsoft's release, so nothing was installed.";
            }

            var extracted = ExtractDependencies(zipPath, folder, architecture.ToString().ToLowerInvariant());
            if (extracted.Count == 0)
            {
                return "This PC's processor type isn't in the release, so nothing was installed.";
            }

            progress.Report("Installing App Installer…");
            var error = await addPackage(new Uri(bundlePath), extracted, cancellationToken);
            return error is null ? null : $"Windows couldn't install App Installer: {error}";
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidDataException or UnauthorizedAccessException or TaskCanceledException)
        {
            return $"App Installer couldn't be downloaded: {ex.Message}";
        }
        finally
        {
            try
            {
                Directory.Delete(folder, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // The temp folder gets cleaned up by Windows later.
            }
        }
    }

    private async Task<bool> DownloadAsync(AppInstallerFile file, string path, string label, IProgress<string> progress, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(file.Url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength;

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var target = File.Create(path);
        var buffer = new byte[81920];
        long done = 0;
        var shown = -1L;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            hash.AppendData(buffer, 0, read);
            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            done += read;
            var percent = total > 0 ? done * 100 / total.Value : 0;
            if (percent != shown)
            {
                shown = percent;
                progress.Report($"Downloading {label}… {percent}%");
            }
        }
        return Convert.ToHexString(hash.GetHashAndReset()) == file.Sha256;
    }

    /// <summary>The release zip has one folder per processor type.</summary>
    private static List<Uri> ExtractDependencies(string zipPath, string folder, string architecture)
    {
        var uris = new List<Uri>();
        using var zip = ZipFile.OpenRead(zipPath);
        foreach (var entry in zip.Entries)
        {
            if (entry.Name.EndsWith(".appx", StringComparison.OrdinalIgnoreCase)
                && entry.FullName.Split('/', '\\').Contains(architecture, StringComparer.OrdinalIgnoreCase))
            {
                var path = Path.Combine(folder, entry.Name); // Name alone: an entry can't escape the folder
                entry.ExtractToFile(path, overwrite: true);
                uris.Add(new Uri(path));
            }
        }
        return uris;
    }

    private static async Task<string?> AddPackageAsync(Uri bundle, IReadOnlyList<Uri> dependencies, CancellationToken cancellationToken)
    {
        var options = new AddPackageOptions();
        foreach (var dependency in dependencies)
        {
            options.DependencyPackageUris.Add(dependency);
        }
        var result = await new PackageManager().AddPackageByUriAsync(bundle, options).AsTask(cancellationToken);
        return result.IsRegistered ? null : result.ErrorText;
    }
}

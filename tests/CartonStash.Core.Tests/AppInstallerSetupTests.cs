using System.IO.Compression;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using CartonStash.Core.Services;

namespace CartonStash.Core.Tests;

/// <summary>Spec 006 amendment R3.4: Set up for me (R3.7).</summary>
public class AppInstallerSetupTests
{
    private static readonly Uri BundleUrl = new("https://example.com/bundle");
    private static readonly Uri ZipUrl = new("https://example.com/zip");
    private static readonly byte[] BundleBytes = [1, 2, 3];

    private sealed class Handler(Func<Uri, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request.RequestUri!));
    }

    private static byte[] Zip(params string[] names)
    {
        using var memory = new MemoryStream();
        using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var name in names)
            {
                using var entry = zip.CreateEntry(name).Open();
                entry.WriteByte(7);
            }
        }
        return memory.ToArray();
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    private static HttpResponseMessage Ok(byte[] bytes) => new(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };

    private sealed class Run
    {
        public List<(string Bundle, IReadOnlyList<string> Dependencies)> Added { get; } = [];

        public string? AddError { get; init; }

        public AppInstallerSetup Setup(byte[] zip, string? bundleHash = null, Func<Uri, HttpResponseMessage>? respond = null) => new(
            new HttpClient(new Handler(respond ?? (uri => Ok(uri == BundleUrl ? BundleBytes : zip)))),
            new(BundleUrl, bundleHash ?? Hash(BundleBytes)),
            new(ZipUrl, Hash(zip)),
            Architecture.X64,
            (bundle, dependencies, _) =>
            {
                Added.Add((bundle.LocalPath, [.. dependencies.Select(d => Path.GetFileName(d.LocalPath))]));
                Assert.True(File.Exists(bundle.LocalPath));
                return Task.FromResult(AddError);
            });
    }

    private static readonly Progress<string> NoProgress = new();

    [Fact]
    public async Task Installs_the_bundle_with_the_dependencies_for_this_processor()
    {
        var run = new Run();

        var error = await run.Setup(Zip("x64/a.appx", "x64/b.appx", "arm64/c.appx", "x64/readme.txt")).RunAsync(NoProgress, default);

        Assert.Null(error);
        var added = Assert.Single(run.Added);
        Assert.Equal(["a.appx", "b.appx"], added.Dependencies);
        Assert.False(File.Exists(added.Bundle)); // cleaned up
    }

    [Fact]
    public async Task Refuses_a_file_that_does_not_match_the_release()
    {
        var run = new Run();

        var error = await run.Setup(Zip("x64/a.appx"), bundleHash: "00").RunAsync(NoProgress, default);

        Assert.Contains("nothing was installed", error);
        Assert.Empty(run.Added);
    }

    [Fact]
    public async Task Reports_a_failed_download()
    {
        var run = new Run();

        var error = await run.Setup(Zip("x64/a.appx"), respond: _ => new HttpResponseMessage(HttpStatusCode.NotFound)).RunAsync(NoProgress, default);

        Assert.Contains("couldn't be downloaded", error);
        Assert.Empty(run.Added);
    }

    [Fact]
    public async Task Reports_when_the_release_has_nothing_for_this_processor()
    {
        var run = new Run();

        var error = await run.Setup(Zip("arm64/c.appx")).RunAsync(NoProgress, default);

        Assert.Contains("processor", error);
        Assert.Empty(run.Added);
    }

    [Fact]
    public async Task Passes_on_the_reason_when_windows_refuses()
    {
        var run = new Run { AddError = "Blocked by policy" };

        var error = await run.Setup(Zip("x64/a.appx")).RunAsync(NoProgress, default);

        Assert.Contains("Blocked by policy", error);
    }
}

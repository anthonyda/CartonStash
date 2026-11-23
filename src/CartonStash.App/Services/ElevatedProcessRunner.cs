using System.ComponentModel;
using System.Diagnostics;
using CartonStash.Core.Models;
using CartonStash.Core.Services;

namespace CartonStash.App.Services;

/// <summary>
/// 017 R1.4, R1.8: runs one operation in a copy of this exe started with administrator rights. That copy has no window
/// and exits when the operation ends; this window keeps showing the operation. The app itself never runs elevated (A5).
/// </summary>
public sealed class ElevatedProcessRunner(ISettings settings, IOperationLogStore logs) : IElevatedRunner
{
    private const int ErrorCancelled = 1223; // the user said No to Windows' prompt

    public async Task<OperationOutcome> RunAsync(ElevatedRequest request, CancellationToken cancellationToken)
    {
        var folder = Path.Combine(Path.GetDirectoryName(logs.Folder) ?? Path.GetTempPath(), "elevated");
        var result = Path.Combine(folder, $"{Guid.NewGuid():N}.json");
        try
        {
            Directory.CreateDirectory(folder);
            var start = new ProcessStartInfo(Environment.ProcessPath!)
            {
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden,
            };
            start.ArgumentList.Add(ElevatedProtocol.Argument);
            start.ArgumentList.Add(ElevatedProtocol.Encode(request with { InstallScopeSetting = settings.Current.InstallScope }));
            start.ArgumentList.Add(result);

            using var process = await Task.Run(() => Process.Start(start), CancellationToken.None);
            if (process is null)
            {
                return ElevatedProtocol.NoResult();
            }
            await process.WaitForExitAsync(CancellationToken.None); // an installer can't be stopped halfway (003 R3.2)
            return File.Exists(result) && ElevatedProtocol.ReadOutcome(await File.ReadAllTextAsync(result, CancellationToken.None)) is { } outcome
                ? outcome
                : ElevatedProtocol.NoResult();
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled)
        {
            return ElevatedProtocol.Declined(); // R1.5
        }
        catch (Exception ex)
        {
            return new OperationOutcome(OperationResult.Failed, $"Couldn't start the update with administrator rights: {ex.Message}");
        }
        finally
        {
            try
            {
                File.Delete(result);
            }
            catch (Exception)
            {
            }
        }
    }

    /// <summary>
    /// The elevated copy (Program, <c>--run-elevated</c>): runs the request through the same installer the queue uses and
    /// writes the outcome. No window, no XAML.
    /// </summary>
    public static int RunHere(string encoded, string resultPath)
    {
        OperationOutcome outcome;
        if (ElevatedProtocol.Decode(encoded) is not { } request)
        {
            outcome = new OperationOutcome(OperationResult.Failed, "The request for administrator rights wasn't valid.");
        }
        else
        {
            try
            {
                var settings = new FixedSettings(new AppSettings { InstallScope = request.InstallScopeSetting });
                var installer = new WinGetPackageInstaller(new CatalogConnection(settings), settings, new RunningPrograms());
                outcome = Task.Run(() => installer.RunAsync(request.Kind, request.Package, request.AcceptAgreements, request.Version, request.LogPath,
                    new Progress<OperationProgress>(), CancellationToken.None, request.Scope, request.ScopeSource)).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                outcome = new OperationOutcome(OperationResult.Failed, ex is WinGetException ? ex.Message : $"Something went wrong: {ex.Message}");
            }
        }
        try
        {
            File.WriteAllText(resultPath, ElevatedProtocol.WriteOutcome(outcome));
            return 0;
        }
        catch (Exception)
        {
            return 1;
        }
    }
}

using WinGetFrontend.Core.Models;

namespace WinGetFrontend.Core.Services;

/// <summary>
/// The app's only entry point into WinGet. View models depend on this interface,
/// so they can be tested against a fake.
/// </summary>
public interface IPackageService
{
    /// <summary>Searches all configured remote sources by name, ID or moniker.</summary>
    Task<IReadOnlyList<PackageInfo>> SearchAsync(string query, int maxResults = 50, CancellationToken cancellationToken = default);
}

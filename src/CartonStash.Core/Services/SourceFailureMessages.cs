using CartonStash.Core.Models;

namespace CartonStash.Core.Services;

/// <summary>Says why a source failed (011 R2.2–R2.4), from WinGet's code alone: its status differs for the same failure (R1.3).</summary>
internal static class SourceFailureMessages
{
    /// <summary>APPINSTALLER_CLI_ERROR_RESTAPI_INTERNAL_ERROR: seen when the Store rejects the market <c>ZZ</c>.</summary>
    private const int RestInternalError = unchecked((int)0x8A15003B);

    private const uint WinHttpFirst = 0x80072EE0;
    private const uint WinHttpLast = 0x80072FFF;
    private const uint WinGetFacility = 0x8A150000;
    private const uint HttpStatusFacility = 0x80190000;
    private const uint FacilityMask = 0xFFFF0000;

    public static string Describe(string source, int hresult, bool regionIsCountry)
    {
        var code = unchecked((uint)hresult);
        if (code is >= WinHttpFirst and <= WinHttpLast)
        {
            return $"'{source}' can't be reached. Check your internet connection or firewall.";
        }
        if (hresult == RestInternalError && !regionIsCountry)
        {
            return $"'{source}' needs a country. Set one in Windows Settings, under Time & language, then Language & region.";
        }
        if ((code & FacilityMask) is WinGetFacility or HttpStatusFacility)
        {
            return $"'{source}' reported a problem. This isn't your connection.";
        }
        return $"'{source}' failed (WinGet reported an error).";
    }

    public static string Join(IEnumerable<SourceFailure> failures) => string.Join(" ", failures.Select(f => f.Reason));

    /// <summary>The Search page's notice, or null when no source is left out (R2.1).</summary>
    public static string? Notice(IReadOnlyList<SourceFailure> failures) =>
        failures.Count == 0 ? null : $"Results don't include: {Join(failures)}";
}

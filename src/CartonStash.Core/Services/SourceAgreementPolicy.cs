using System.Security.Cryptography;
using System.Text;
using CartonStash.Core.Models;

namespace CartonStash.Core.Services;

/// <summary>Decides from the saved answer whether a source with agreements may be used, and whether to ask (006 R3.8, R3.9).</summary>
public static class SourceAgreementPolicy
{
    /// <summary>WinGet's ID for <c>msstore</c>, which stays the same if the source is renamed.</summary>
    internal const string StoreSourceId = "StoreEdgeFD";

    /// <summary>True only for an acceptance of this exact text.</summary>
    public static bool MayUse(SourceAgreementAnswer? answer, string textHash) =>
        answer is { Accepted: true } && answer.TextHash == textHash;

    /// <summary>Ask at start: never answered, or accepted a text that has changed. A decline is not asked again.</summary>
    public static bool NeedsAnswer(SourceAgreementAnswer? answer, string textHash) =>
        answer is null || (answer.Accepted && answer.TextHash != textHash);

    /// <summary>Identifies the agreement text, so a change can be noticed without keeping the text.</summary>
    public static string Hash(IReadOnlyList<Agreement> agreements)
    {
        var text = new StringBuilder();
        foreach (var agreement in agreements)
        {
            text.Append(agreement.Label).Append('\0').Append(agreement.Text).Append('\0').Append(agreement.Url).Append('\0');
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }

    /// <summary>What a source receives, for the dialog (R3.6). The COM API exposes only the agreement list, so this is fixed text.</summary>
    public static string? ReceivesNotice(string sourceId) =>
        sourceId == StoreSourceId ? "This source receives your PC's 2-letter region, such as US." : null;
}

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


    /// <summary>Unanswered sources are asked at start. A changed text puts an acceptance back there; a decline stays a decline.</summary>
    public static AgreementStatus Status(IReadOnlyList<Agreement> agreements, SourceAgreementAnswer? answer)
    {
        if (agreements.Count == 0)
        {
            return AgreementStatus.None;
        }
        if (answer is { Accepted: false })
        {
            return AgreementStatus.Declined;
        }
        return MayUse(answer, Hash(agreements)) ? AgreementStatus.Accepted : AgreementStatus.Unanswered;
    }

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

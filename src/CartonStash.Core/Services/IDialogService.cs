using CartonStash.Core.Models;

namespace CartonStash.Core.Services;

/// <summary>The answer to a source's agreements. Dismissed: closed without choosing, so it's asked again (006 R3.9).</summary>
public enum AgreementAnswer
{
    Accepted,
    Declined,
    Dismissed,
}

/// <summary>Modal questions to the user, implemented by the App with ContentDialogs (003, 004, 006).</summary>
public interface IDialogService
{
    /// <summary>Asks a yes/no question. Returns true when the user chose <paramref name="confirmText"/>.</summary>
    Task<bool> ConfirmAsync(string title, string message, string confirmText, string cancelText = "Cancel");

    /// <summary>
    /// Asks a yes/no question with a checkbox (014 R3.1, R3.2). With <paramref name="optionText"/> null there is no box, and
    /// <c>OptionChecked</c> is false.
    /// </summary>
    Task<(bool Confirmed, bool OptionChecked)> ConfirmWithOptionAsync(string title, string message, string confirmText,
        string? optionText, bool optionChecked);

    /// <summary>Shows agreement texts with Accept / Decline. Returns true when accepted (003 R1.3, 006 R3.3).</summary>
    Task<bool> ShowAgreementsAsync(string title, IReadOnlyList<Agreement> agreements);

    /// <summary>Shows a source's agreements and what it receives, with Accept and Decline (006 R3.6, R3.12).</summary>
    Task<AgreementAnswer> ShowSourceAgreementAsync(SourceInfo source, string? receivesNotice);

    /// <summary>Shows an operation's log as plain text, with Copy and Open folder (009 R2.2, R2.4, R2.6).</summary>
    Task ShowLogAsync(string title, string text, string folder);
}

/// <summary>Puts text on the clipboard (006 R4.2).</summary>
public interface IClipboardService
{
    void SetText(string text);
}

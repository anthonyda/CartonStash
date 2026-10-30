using CartonStash.Core.Models;

namespace CartonStash.Core.Services;

/// <summary>Modal questions to the user, implemented by the App with ContentDialogs (003, 004, 006).</summary>
public interface IDialogService
{
    /// <summary>Asks a yes/no question. Returns true when the user chose <paramref name="confirmText"/>.</summary>
    Task<bool> ConfirmAsync(string title, string message, string confirmText, string cancelText = "Cancel");

    /// <summary>Shows agreement texts with Accept / Decline. Returns true when accepted (003 R1.3, 006 R3.3).</summary>
    Task<bool> ShowAgreementsAsync(string title, IReadOnlyList<Agreement> agreements);

    /// <summary>Shows an operation's log as plain text, with Copy and Open folder (009 R2.2, R2.4, R2.6).</summary>
    Task ShowLogAsync(string title, string text, string folder);
}

/// <summary>Puts text on the clipboard (006 R4.2).</summary>
public interface IClipboardService
{
    void SetText(string text);
}

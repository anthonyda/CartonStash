using CartonStash.Core.Models;

namespace CartonStash.Core.Services;

/// <summary>
/// Asks about source agreements at start and whenever the user chooses Read and accept (006 R3.4 to R3.6, R3.9), and keeps
/// the list of sources not in use for the pages that show it (R3.10). Use it from the UI thread.
/// </summary>
public sealed class SourceAgreementCheck(IPackageService packages, IDialogService dialogs, InstalledPackagesStore installed)
{
    /// <summary>Sources with agreements that the app isn't using.</summary>
    public IReadOnlyList<SourceInfo> NotAccepted { get; private set; } = [];

    public event EventHandler? Changed;

    /// <summary>Runs before the installed list loads. Reading the sources doesn't connect to them.</summary>
    public async Task RunAsync()
    {
        IReadOnlyList<SourceInfo> sources;
        try
        {
            sources = await packages.GetSourcesAsync();
        }
        catch (Exception ex) when (ex is WinGetException or System.Runtime.InteropServices.COMException)
        {
            return; // Nothing to ask about. CatalogConnection still leaves out what isn't accepted.
        }

        var accepted = false;
        foreach (var source in sources.Where(s => s.AgreementStatus == AgreementStatus.Unanswered))
        {
            accepted |= await AnswerAsync(source) == AgreementAnswer.Accepted;
        }
        Update(await packages.GetSourcesAsync());
        await (accepted ? installed.ReloadAsync() : installed.EnsureLoadedAsync());
    }

    /// <summary>The Read and accept button on Search: every source not in use, in turn (R3.10).</summary>
    public async Task<bool> ReadAndAcceptAsync()
    {
        var accepted = false;
        foreach (var source in NotAccepted.ToList())
        {
            accepted |= await ReadAndAcceptAsync(source) == AgreementAnswer.Accepted;
        }
        return accepted;
    }

    /// <summary>The Read and accept button for one source (R3.10).</summary>
    public async Task<AgreementAnswer> ReadAndAcceptAsync(SourceInfo source)
    {
        var answer = await AnswerAsync(source);
        if (answer != AgreementAnswer.Dismissed)
        {
            Update(await packages.GetSourcesAsync());
        }
        if (answer == AgreementAnswer.Accepted)
        {
            await installed.ReloadAsync();
        }
        return answer;
    }

    /// <summary>Stops using a source. Saved as a decline, so the app doesn't ask again at start (R3.11).</summary>
    public async Task WithdrawAsync(SourceInfo source)
    {
        packages.SetSourceAgreementAnswer(source, accepted: false);
        Update(await packages.GetSourcesAsync());
        await installed.ReloadAsync();
    }

    private async Task<AgreementAnswer> AnswerAsync(SourceInfo source)
    {
        if (source.Agreements.Count == 0)
        {
            source = await packages.ReadSourceAgreementsAsync(source); // declined earlier, so not read until now (R3.8)
        }
        if (source.Agreements.Count == 0)
        {
            await dialogs.ConfirmAsync($"{source.Name} can't be reached", "Its agreements come from the source itself. Check your connection and try again.", "OK", "Close");
            return AgreementAnswer.Dismissed;
        }

        var answer = await dialogs.ShowSourceAgreementAsync(source, SourceAgreementPolicy.ReceivesNotice(source.Id));
        if (answer != AgreementAnswer.Dismissed)
        {
            packages.SetSourceAgreementAnswer(source, answer == AgreementAnswer.Accepted); // Dismissed asks again at the next start (R3.9)
        }
        return answer;
    }

    private void Update(IReadOnlyList<SourceInfo> sources)
    {
        NotAccepted = [.. sources.Where(s => s.AgreementStatus is AgreementStatus.Unanswered or AgreementStatus.Declined)];
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

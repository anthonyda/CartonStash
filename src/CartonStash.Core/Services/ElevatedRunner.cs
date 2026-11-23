using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CartonStash.Core.Models;

namespace CartonStash.Core.Services;

/// <summary>017 R1.4: one operation to run with administrator rights, as the queue would run it.</summary>
public sealed record ElevatedRequest(
    OperationKind Kind,
    PackageInfo Package,
    bool AcceptAgreements,
    string? Version,
    string? LogPath,
    InstallScope? Scope,
    ScopeSource ScopeSource,
    InstallScope InstallScopeSetting = InstallScope.Any);

/// <summary>Runs one operation with administrator rights and returns its outcome (017 R1.4 to R1.6). Never throws.</summary>
public interface IElevatedRunner
{
    Task<OperationOutcome> RunAsync(ElevatedRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// How the app and its elevated copy talk (017 design). The request goes on the command line, set when Windows asks
/// for permission, so nothing can change it afterwards; the outcome comes back in a file.
/// </summary>
public static class ElevatedProtocol
{
    public const string Argument = "--run-elevated";

    public const string DeclinedMessage = "Windows' permission prompt was declined.";

    private static readonly JsonSerializerOptions Json = new() { Converters = { new JsonStringEnumConverter() } };

    public static string Encode(ElevatedRequest request) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request, Json)));

    /// <summary>The request, or null when it isn't one the app could have queued (an install or update of one package).</summary>
    public static ElevatedRequest? Decode(string encoded)
    {
        try
        {
            var request = JsonSerializer.Deserialize<ElevatedRequest>(Encoding.UTF8.GetString(Convert.FromBase64String(encoded)), Json);
            return request is { Kind: OperationKind.Install or OperationKind.Upgrade, Package.Id.Length: > 0 } ? request : null;
        }
        catch (Exception ex) when (ex is FormatException or JsonException or NotSupportedException or ArgumentException)
        {
            return null;
        }
    }

    public static string WriteOutcome(OperationOutcome outcome) => JsonSerializer.Serialize(outcome, Json);

    public static OperationOutcome? ReadOutcome(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<OperationOutcome>(json, Json);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>R1.5: the user said No to Windows' prompt.</summary>
    public static OperationOutcome Declined() => new(OperationResult.Cancelled, DeclinedMessage);

    /// <summary>The elevated copy ended without writing a result.</summary>
    public static OperationOutcome NoResult() =>
        new(OperationResult.Failed, "The update with administrator rights ended without a result. Open the log for details.");
}

/// <summary>Settings fixed for one elevated operation: the scope setting the app had when it asked (006 R2.1).</summary>
public sealed class FixedSettings(AppSettings current) : ISettings
{
    public AppSettings Current { get; } = current;

    public bool WasReset => false;

    public event EventHandler? Changed
    {
        add { }
        remove { }
    }

    public void Update(Func<AppSettings, AppSettings> change)
    {
    }
}

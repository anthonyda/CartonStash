namespace CartonStash.Core.Services;

/// <summary>
/// Reads the arguments a notification was built with (<c>AppNotificationBuilder.AddArgument</c>): <c>key=value</c> pairs
/// separated by <c>;</c>. Used when a click on a notification reaches the open app (010 R2.2, 018 R1.4).
/// </summary>
public static class NotificationArguments
{
    /// <summary>The <c>page</c> value, or null when there is none.</summary>
    public static string? Page(string? argument)
    {
        foreach (var pair in (argument ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var i = pair.IndexOf('=');
            if (i > 0 && pair[..i].Trim() == "page")
            {
                return Uri.UnescapeDataString(pair[(i + 1)..].Trim());
            }
        }
        return null;
    }
}

namespace CartonStash.Core.Services;

/// <summary>The number on the app's taskbar icon (016). It stays after the app closes.</summary>
public interface ITaskbarBadge
{
    /// <summary>Shows <paramref name="count"/>, or clears the badge for 0. Never throws (R3.1).</summary>
    void Show(int count);
}

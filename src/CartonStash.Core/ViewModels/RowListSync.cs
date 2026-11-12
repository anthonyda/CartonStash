using System.Collections.ObjectModel;

namespace CartonStash.Core.ViewModels;

/// <summary>
/// Changes a list the pages show with removals and inserts only, never <c>Clear()</c> (004 R8.1, 002 R9, 001 R4).
/// WinUI's ListView keeps the index it last focused across a reset, and Tab into a shorter list then asks for a row past
/// the end and the app crashes (E_BOUNDS). Removing rows one by one brings that index back in range.
/// </summary>
internal static class RowListSync
{
    /// <summary>Makes <paramref name="target"/> equal <paramref name="wanted"/>; rows in both stay where they are.</summary>
    public static void Sync<T>(ObservableCollection<T> target, IReadOnlyList<T> wanted) where T : class
    {
        var keep = new HashSet<T>(wanted, ReferenceEqualityComparer.Instance);
        for (var i = target.Count - 1; i >= 0; i--)
        {
            if (!keep.Contains(target[i]))
            {
                target.RemoveAt(i);
            }
        }
        for (var i = 0; i < wanted.Count; i++)
        {
            if (i < target.Count && ReferenceEquals(target[i], wanted[i]))
            {
                continue;
            }
            // A row that moved: take it out and put it back, no Move, like the rest.
            var at = IndexOf(target, wanted[i]);
            if (at >= 0)
            {
                target.RemoveAt(at);
            }
            target.Insert(i, wanted[i]);
        }
    }

    /// <summary>Empties <paramref name="target"/> from the end, one removal at a time.</summary>
    public static void RemoveAll<T>(ObservableCollection<T> target)
    {
        for (var i = target.Count - 1; i >= 0; i--)
        {
            target.RemoveAt(i);
        }
    }

    private static int IndexOf<T>(ObservableCollection<T> list, T item) where T : class
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (ReferenceEquals(list[i], item))
            {
                return i;
            }
        }
        return -1;
    }
}

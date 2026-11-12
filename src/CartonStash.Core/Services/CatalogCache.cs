namespace CartonStash.Core.Services;

/// <summary>
/// One cached catalog (BR-15). <see cref="Invalidate"/> raises a generation number; a connection that began before
/// it is handed to its caller but never kept, so a catalog from before an install can't come back as the current one.
/// </summary>
internal sealed class CatalogCache<T> where T : class
{
    private readonly Lock _lock = new();
    private int _generation;
    private T? _value;

    public T? Value
    {
        get
        {
            lock (_lock)
            {
                return _value;
            }
        }
    }

    /// <summary>Call before connecting; pass the result to <see cref="Keep"/>.</summary>
    public int Begin()
    {
        lock (_lock)
        {
            return _generation;
        }
    }

    /// <summary>Keeps the catalog if nothing invalidated the cache since <paramref name="generation"/>. Returns it either way.</summary>
    public T Keep(int generation, T value)
    {
        lock (_lock)
        {
            if (generation == _generation)
            {
                _value = value;
            }
            return value;
        }
    }

    public void Invalidate()
    {
        lock (_lock)
        {
            _generation++;
            _value = null;
        }
    }
}

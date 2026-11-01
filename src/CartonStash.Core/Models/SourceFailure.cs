namespace CartonStash.Core.Models;

/// <summary>A source left out of the current catalogs because it failed on its own (011 R1.1).</summary>
/// <param name="Reason">Readable, with no raw code (R2.7).</param>
/// <param name="HResult">WinGet's error code.</param>
public sealed record SourceFailure(string Source, string Reason, int HResult);

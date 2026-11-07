namespace CartonStash.Core.Models;

/// <summary>
/// Formatted release notes as plain records (spec 012), drawn by the App's NotesView. Both WinGet's text and
/// GitHub's Markdown end up here. Every link in it is http or https.
/// </summary>
public sealed record NotesDocument(IReadOnlyList<NotesBlock> Blocks)
{
    /// <summary>One paragraph per blank-line block, line breaks kept: how the pane showed notes before spec 012 (R1.8).</summary>
    public static NotesDocument PlainText(string text)
    {
        var blocks = new List<NotesBlock>();
        var lines = new List<NotesInline>();
        foreach (var line in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            if (line.Trim().Length == 0)
            {
                Flush();
                continue;
            }
            if (lines.Count > 0)
            {
                lines.Add(new NotesLineBreak());
            }
            lines.Add(new NotesText(line));
        }
        Flush();
        return new NotesDocument(blocks);

        void Flush()
        {
            if (lines.Count > 0)
            {
                blocks.Add(new NotesParagraph([.. lines]));
                lines.Clear();
            }
        }
    }
}

public abstract record NotesBlock;

public sealed record NotesParagraph(IReadOnlyList<NotesInline> Inlines) : NotesBlock;

/// <summary>Level 1 to 6. Only GitHub's notes have headings: WinGet's section titles are bold paragraphs (R1.9).</summary>
public sealed record NotesHeading(int Level, IReadOnlyList<NotesInline> Inlines) : NotesBlock;

public sealed record NotesList(bool Ordered, int Start, IReadOnlyList<NotesListItem> Items) : NotesBlock;

/// <summary><paramref name="Checked"/> is null for a plain item, true or false for a task list item.</summary>
public sealed record NotesListItem(IReadOnlyList<NotesBlock> Blocks, bool? Checked = null);

public sealed record NotesQuote(IReadOnlyList<NotesBlock> Blocks) : NotesBlock;

/// <summary>A code block: monospace, line breaks kept, no wrapping.</summary>
public sealed record NotesCode(string Text) : NotesBlock;

/// <summary>The first row is the header.</summary>
public sealed record NotesTable(IReadOnlyList<IReadOnlyList<NotesCell>> Rows) : NotesBlock;

public sealed record NotesCell(IReadOnlyList<NotesInline> Inlines);

public sealed record NotesRule : NotesBlock;

[Flags]
public enum NotesStyle
{
    None = 0,
    Bold = 1,
    Italic = 2,
    Strike = 4,
    Code = 8,
}

public abstract record NotesInline;

public sealed record NotesText(string Text, NotesStyle Style = NotesStyle.None) : NotesInline;

/// <summary><paramref name="Uri"/> is always http or https: anything else stays text (005 security, 012 R1.4, R2.7).</summary>
public sealed record NotesLink(IReadOnlyList<NotesInline> Inlines, Uri Uri) : NotesInline;

public sealed record NotesLineBreak : NotesInline;

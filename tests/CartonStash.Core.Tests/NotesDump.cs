using System.Text;
using CartonStash.Core.Models;

namespace CartonStash.Core.Tests;

/// <summary>
/// Writes a <see cref="NotesDocument"/> as compact text so tests can compare whole documents (spec 012).
/// Blocks: "P:", "H2:", "-" (list items, two spaces per level), "1." (numbered), "[x]" / "[ ]" (tasks), ">" (quote),
/// "code:", "table:", "rule". Inlines: **bold**, _italic_, ~~strike~~, `code`, [text](uri), ⏎ for a line break.
/// </summary>
internal static class NotesDump
{
    /// <summary>
    /// Compares with an expected text written as a multi-line literal. Those hold the file's own line endings, which are
    /// CRLF on a Windows checkout with autocrlf (CI), while the notes model always uses LF.
    /// </summary>
    public static void Same(string expected, string actual) => Assert.Equal(expected.ReplaceLineEndings("\n"), actual);

    public static string Describe(NotesDocument? doc)
    {
        if (doc is null)
        {
            return "(null)";
        }
        var sb = new StringBuilder();
        foreach (var block in doc.Blocks)
        {
            Block(sb, block, "");
        }
        return sb.ToString().TrimEnd('\n');
    }

    private static void Block(StringBuilder sb, NotesBlock block, string prefix)
    {
        switch (block)
        {
            case NotesParagraph p:
                sb.Append(prefix).Append("P: ").Append(Inlines(p.Inlines)).Append('\n');
                break;
            case NotesHeading h:
                sb.Append(prefix).Append('H').Append(h.Level).Append(": ").Append(Inlines(h.Inlines)).Append('\n');
                break;
            case NotesList list:
                for (var i = 0; i < list.Items.Count; i++)
                {
                    var item = list.Items[i];
                    var marker = item.Checked switch
                    {
                        true => "[x]",
                        false => "[ ]",
                        null => list.Ordered ? $"{list.Start + i}." : "-",
                    };
                    for (var b = 0; b < item.Blocks.Count; b++)
                    {
                        var inner = item.Blocks[b];
                        if (b == 0 && inner is NotesParagraph first)
                        {
                            sb.Append(prefix).Append(marker).Append(' ').Append(Inlines(first.Inlines)).Append('\n');
                        }
                        else
                        {
                            if (b == 0)
                            {
                                sb.Append(prefix).Append(marker).Append('\n');
                            }
                            Block(sb, inner, prefix + "  ");
                        }
                    }
                }
                break;
            case NotesQuote q:
                foreach (var inner in q.Blocks)
                {
                    Block(sb, inner, prefix + "> ");
                }
                break;
            case NotesCode code:
                sb.Append(prefix).Append("code: ").Append(code.Text.Replace("\n", "⏎")).Append('\n');
                break;
            case NotesTable table:
                foreach (var row in table.Rows)
                {
                    sb.Append(prefix).Append("table: ").AppendJoin(" | ", row.Select(cell => Inlines(cell.Inlines))).Append('\n');
                }
                break;
            case NotesRule:
                sb.Append(prefix).Append("rule\n");
                break;
            default:
                throw new InvalidOperationException(block.GetType().Name);
        }
    }

    public static string Inlines(IEnumerable<NotesInline> inlines)
    {
        var sb = new StringBuilder();
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case NotesText t:
                    var text = t.Text;
                    if (t.Style.HasFlag(NotesStyle.Code)) text = $"`{text}`";
                    if (t.Style.HasFlag(NotesStyle.Strike)) text = $"~~{text}~~";
                    if (t.Style.HasFlag(NotesStyle.Italic)) text = $"_{text}_";
                    if (t.Style.HasFlag(NotesStyle.Bold)) text = $"**{text}**";
                    sb.Append(text);
                    break;
                case NotesLink l:
                    sb.Append('[').Append(Inlines(l.Inlines)).Append("](").Append(l.Uri.OriginalString).Append(')');
                    break;
                case NotesLineBreak:
                    sb.Append('⏎');
                    break;
            }
        }
        return sb.ToString();
    }
}

using System.Text;
using CartonStash.Core.Models;

namespace CartonStash.Core.Services;

/// <summary>
/// Formats WinGet's release notes text (spec 012 R1). The tools that publish to winget-pkgs strip most Markdown,
/// so this knows only the few patterns that survive: list items, quotes, and a little inline formatting.
/// A general Markdown parser would read the leftovers wrongly (a shell comment as a heading, `_` in file names as italics).
/// </summary>
public static class WinGetNotesParser
{
    /// <summary>Longest line that can be a section title (R1.9).</summary>
    public const int SectionTitleMaxLength = 40;

    /// <summary>Null for null or blank text. Never throws: anything unexpected gives the plain text (R1.8).</summary>
    public static NotesDocument? Parse(string? text) => Parse(text, ParseLines);

    internal static NotesDocument? Parse(string? text, Func<IReadOnlyList<Line>, NotesDocument> parse)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }
        try
        {
            return parse(Split(text));
        }
        catch (Exception)
        {
            return NotesDocument.PlainText(text);
        }
    }

    internal enum LineKind
    {
        Blank,
        Item,
        Quote,
        Text,
    }

    /// <param name="Indent">Leading spaces, tabs counted as 4.</param>
    /// <param name="Content">The line without its indent and marker.</param>
    internal readonly record struct Line(LineKind Kind, int Indent, string Content);

    private static List<Line> Split(string text)
    {
        var lines = new List<Line>();
        foreach (var raw in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            lines.Add(Classify(raw.Replace("\t", "    ", StringComparison.Ordinal)));
        }
        return lines;
    }

    private static Line Classify(string raw)
    {
        var rest = raw.TrimStart(' ');
        var indent = raw.Length - rest.Length;
        rest = rest.TrimEnd();
        if (rest.Length == 0)
        {
            return new Line(LineKind.Blank, 0, "");
        }
        // R1.1: "- ", "* " or "• ", and some text after it.
        if (rest.Length >= 2 && rest[0] is '-' or '*' or '•' && char.IsWhiteSpace(rest[1]) && rest[2..].Trim().Length > 0)
        {
            return new Line(LineKind.Item, indent, rest[2..].Trim());
        }
        // R1.2: the marker and one space removed.
        if (rest[0] == '>')
        {
            var content = rest[1..];
            return new Line(LineKind.Quote, indent, content.StartsWith(' ') ? content[1..] : content);
        }
        return new Line(LineKind.Text, indent, rest);
    }

    private static NotesDocument ParseLines(IReadOnlyList<Line> lines)
    {
        var blocks = new List<NotesBlock>();
        var paragraph = new List<string>();
        var quote = new List<string>();
        List<ListBuilder>? lists = null; // open lists, outermost first

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            switch (line.Kind)
            {
                case LineKind.Blank:
                    FlushParagraph();
                    FlushQuote();
                    // A blank line between items keeps the list going (Firefox), anything else ends it.
                    if (lists is not null && NextNonBlank(lines, i + 1) is not LineKind.Item)
                    {
                        FlushList();
                    }
                    break;

                case LineKind.Item:
                    FlushParagraph();
                    FlushQuote();
                    AddItem(line);
                    break;

                case LineKind.Quote:
                    FlushParagraph();
                    FlushList();
                    quote.Add(line.Content);
                    break;

                default:
                    // R1.1: an indented line right after an item joins it. A blank line has already closed the list
                    // unless an item follows it, so here the line above is never blank.
                    if (lists is not null && line.Indent > 0)
                    {
                        lists[^1].Items[^1].Lines.Add(line.Content);
                        break;
                    }
                    FlushList();
                    FlushQuote();
                    if (IsSectionTitle(line, i + 1 < lines.Count ? lines[i + 1] : default))
                    {
                        FlushParagraph();
                        blocks.Add(new NotesParagraph(ParseInline(line.Content, NotesStyle.Bold)));
                    }
                    else
                    {
                        paragraph.Add(line.Content);
                    }
                    break;
            }
        }
        FlushParagraph();
        FlushQuote();
        FlushList();
        return new NotesDocument(blocks);

        void AddItem(Line line)
        {
            if (lists is null)
            {
                lists = [new ListBuilder(line.Indent)];
            }
            else if (line.Indent > lists[^1].Indent)
            {
                // Deeper than the item above: a nested list under it.
                var parent = lists[^1].Items[^1];
                parent.Child ??= new ListBuilder(line.Indent);
                lists.Add(parent.Child);
            }
            else
            {
                while (lists.Count > 1 && line.Indent < lists[^1].Indent)
                {
                    lists.RemoveAt(lists.Count - 1);
                }
            }
            lists[^1].Items.Add(new ItemBuilder(line.Content));
        }

        void FlushParagraph()
        {
            if (paragraph.Count > 0)
            {
                blocks.Add(new NotesParagraph(Lines(paragraph)));
                paragraph.Clear();
            }
        }

        void FlushQuote()
        {
            if (quote.Count == 0)
            {
                return;
            }
            // A bare ">" line separates paragraphs inside the quote.
            var inner = new List<NotesBlock>();
            var current = new List<string>();
            foreach (var text in quote)
            {
                if (text.Trim().Length == 0)
                {
                    if (current.Count > 0)
                    {
                        inner.Add(new NotesParagraph(Lines(current)));
                        current.Clear();
                    }
                }
                else
                {
                    current.Add(text);
                }
            }
            if (current.Count > 0)
            {
                inner.Add(new NotesParagraph(Lines(current)));
            }
            if (inner.Count > 0)
            {
                blocks.Add(new NotesQuote(inner));
            }
            quote.Clear();
        }

        void FlushList()
        {
            if (lists is not null)
            {
                blocks.Add(lists[0].Build());
                lists = null;
            }
        }
    }

    private static LineKind? NextNonBlank(IReadOnlyList<Line> lines, int from)
    {
        for (var i = from; i < lines.Count; i++)
        {
            if (lines[i].Kind != LineKind.Blank)
            {
                return lines[i].Kind;
            }
        }
        return null;
    }

    /// <summary>R1.9: short, no final . ! or ?, and the very next line is a list item.</summary>
    private static bool IsSectionTitle(Line line, Line next) =>
        next.Kind == LineKind.Item
        && line.Content.Length <= SectionTitleMaxLength
        && line.Content[^1] is not ('.' or '!' or '?');

    /// <summary>R1.5: lines joined with line breaks, each formatted on its own (R1.3).</summary>
    private static List<NotesInline> Lines(IReadOnlyList<string> lines)
    {
        var inlines = new List<NotesInline>();
        for (var i = 0; i < lines.Count; i++)
        {
            if (i > 0)
            {
                inlines.Add(new NotesLineBreak());
            }
            inlines.AddRange(ParseInline(lines[i]));
        }
        return inlines;
    }

    /// <summary>
    /// R1.3 and R1.4, on one line: `code`, **bold**, [text](http…), and bare http(s) addresses.
    /// Anything unmatched stays exactly as written.
    /// </summary>
    internal static List<NotesInline> ParseInline(string s, NotesStyle style = NotesStyle.None, bool links = true)
    {
        var output = new List<NotesInline>();
        var text = new StringBuilder();
        var i = 0;
        while (i < s.Length)
        {
            var c = s[i];
            if (c == '`')
            {
                var end = s.IndexOf('`', i + 1);
                if (end > i + 1)
                {
                    Flush();
                    output.Add(new NotesText(s[(i + 1)..end], style | NotesStyle.Code));
                    i = end + 1;
                    continue;
                }
            }
            else if (c == '*' && (style & NotesStyle.Bold) == 0 && i + 1 < s.Length && s[i + 1] == '*')
            {
                var end = s.IndexOf("**", i + 2, StringComparison.Ordinal);
                if (end > i + 2)
                {
                    Flush();
                    output.AddRange(ParseInline(s[(i + 2)..end], style | NotesStyle.Bold, links));
                    i = end + 2;
                    continue;
                }
            }
            else if (c == '[' && links && TryLink(s, i, style, out var link, out var next))
            {
                Flush();
                output.Add(link);
                i = next;
                continue;
            }
            else if (c is 'h' or 'H' && links && (i == 0 || !char.IsLetterOrDigit(s[i - 1])) && TryBareUrl(s, i, style, out var bare, out var after))
            {
                Flush();
                output.Add(bare);
                i = after;
                continue;
            }
            text.Append(c);
            i++;
        }
        Flush();
        return output;

        void Flush()
        {
            if (text.Length > 0)
            {
                output.Add(new NotesText(text.ToString(), style));
                text.Clear();
            }
        }
    }

    /// <summary>[text](url), only when url is http or https; otherwise the whole thing stays text (R1.3).</summary>
    private static bool TryLink(string s, int start, NotesStyle style, out NotesInline link, out int next)
    {
        link = null!;
        next = start;
        var close = s.IndexOf(']', start + 1);
        if (close <= start + 1 || close + 1 >= s.Length || s[close + 1] != '(')
        {
            return false;
        }
        var end = s.IndexOf(')', close + 2);
        if (end < 0 || WebLinks.SafeUri(s[(close + 2)..end].Trim()) is not { } uri)
        {
            return false;
        }
        link = new NotesLink(ParseInline(s[(start + 1)..close], style, links: false), uri);
        next = end + 1;
        return true;
    }

    /// <summary>R1.4: up to whitespace or &lt; &gt; " ' `, without trailing punctuation or an unmatched ")".</summary>
    private static bool TryBareUrl(string s, int start, NotesStyle style, out NotesInline link, out int next)
    {
        link = null!;
        next = start;
        var rest = s.AsSpan(start);
        if (!rest.StartsWith("https://", StringComparison.OrdinalIgnoreCase) && !rest.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        var end = start;
        while (end < s.Length && !char.IsWhiteSpace(s[end]) && s[end] is not ('<' or '>' or '"' or '\'' or '`'))
        {
            end++;
        }
        while (end > start)
        {
            var last = s[end - 1];
            if (last is '.' or ',' or ';' or ':' or '!' or '?')
            {
                end--;
            }
            else if (last == ')' && Count(s, start, end, '(') < Count(s, start, end, ')'))
            {
                end--;
            }
            else
            {
                break;
            }
        }
        var url = s[start..end];
        if (WebLinks.SafeUri(url) is not { Host.Length: > 0 } uri)
        {
            return false;
        }
        link = new NotesLink([new NotesText(url, style)], uri);
        next = end;
        return true;
    }

    private static int Count(string s, int start, int end, char c)
    {
        var n = 0;
        for (var i = start; i < end; i++)
        {
            if (s[i] == c)
            {
                n++;
            }
        }
        return n;
    }

    private sealed class ListBuilder(int indent)
    {
        public int Indent { get; } = indent;
        public List<ItemBuilder> Items { get; } = [];

        public NotesList Build() => new(false, 1, [.. Items.Select(item => item.Build())]);
    }

    private sealed class ItemBuilder(string first)
    {
        public List<string> Lines { get; } = [first];
        public ListBuilder? Child { get; set; }

        public NotesListItem Build()
        {
            var blocks = new List<NotesBlock> { new NotesParagraph(WinGetNotesParser.Lines(Lines)) };
            if (Child is not null)
            {
                blocks.Add(Child.Build());
            }
            return new NotesListItem(blocks);
        }
    }
}

using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Extensions.EmphasisExtras;
using Markdig.Extensions.Tables;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using CartonStash.Core.Models;

namespace CartonStash.Core.Services;

/// <summary>
/// GitHub release notes (real GitHub Markdown) to a <see cref="NotesDocument"/> (spec 012 R2.4 to R2.7).
/// Images become links and are never loaded, HTML is reduced to its text, and only http(s) links survive.
/// </summary>
public static partial class GitHubMarkdown
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UsePipeTables()
        .UseTaskLists()
        .UseEmphasisExtras(EmphasisExtraOptions.Strikethrough)
        .UseAutoLinks()
        .Build();

    /// <param name="page">The release page: relative links resolve against it (R2.7).</param>
    /// <param name="title">The release title. A first heading that repeats it is left out: the pane shows the title already.</param>
    public static NotesDocument Parse(string? body, Uri page, string? title = null)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return new NotesDocument([]);
        }
        var blocks = new Converter(page).Blocks(Markdown.Parse(body, Pipeline));
        if (title is not null
            && blocks.Count > 0
            && blocks[0] is NotesHeading heading
            && string.Equals(PlainText(heading.Inlines).Trim(), title.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            blocks.RemoveAt(0);
        }
        return new NotesDocument(blocks);
    }

    internal static string PlainText(IEnumerable<NotesInline> inlines)
    {
        var sb = new StringBuilder();
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case NotesText t:
                    sb.Append(t.Text);
                    break;
                case NotesLink l:
                    sb.Append(PlainText(l.Inlines));
                    break;
                case NotesLineBreak:
                    sb.Append(' ');
                    break;
            }
        }
        return sb.ToString();
    }

    private sealed class Converter(Uri page)
    {
        /// <summary>Inside &lt;script&gt; or &lt;style&gt;: their text is dropped too (R2.6).</summary>
        private bool _skipping;

        public List<NotesBlock> Blocks(ContainerBlock container)
        {
            var blocks = new List<NotesBlock>();
            for (var i = 0; i < container.Count; i++)
            {
                Block(container[i], blocks);
                // An unclosed <script> or <style> mustn't hide the rest of the notes.
                _skipping = false;
            }
            return blocks;
        }

        private void Block(Block block, List<NotesBlock> output)
        {
            switch (block)
            {
                case HeadingBlock heading:
                    output.Add(new NotesHeading(Math.Clamp(heading.Level, 1, 6), Inlines(heading.Inline)));
                    break;

                case ParagraphBlock paragraph:
                    AddParagraph(output, Inlines(paragraph.Inline));
                    break;

                case ListBlock list:
                    output.Add(List(list));
                    break;

                case QuoteBlock quote:
                    output.Add(new NotesQuote(Blocks(quote)));
                    break;

                case CodeBlock code: // fenced and indented
                    output.Add(new NotesCode(code.Lines.ToString().TrimEnd('\n', '\r')));
                    break;

                case Table table:
                    output.Add(Table(table));
                    break;

                case ThematicBreakBlock:
                    output.Add(new NotesRule());
                    break;

                case HtmlBlock html:
                    var inlines = new List<NotesInline>();
                    Html(html.Lines.ToString(), inlines);
                    AddParagraph(output, inlines);
                    break;

                case LinkReferenceDefinitionGroup:
                    break; // already resolved into the links that use them

                case ContainerBlock other:
                    output.AddRange(Blocks(other));
                    break;

                case LeafBlock leaf:
                    AddParagraph(output, [new NotesText(leaf.Lines.ToString())]);
                    break;
            }
        }

        private static void AddParagraph(List<NotesBlock> output, List<NotesInline> inlines)
        {
            Tidy(inlines);
            if (inlines.Count > 0)
            {
                output.Add(new NotesParagraph(inlines));
            }
        }

        private NotesList List(ListBlock list)
        {
            var items = new List<NotesListItem>();
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i] is not ListItemBlock item)
                {
                    continue;
                }
                // A task list item starts with a TaskList inline in its first paragraph.
                bool? done = item.Count > 0 && item[0] is ParagraphBlock { Inline.FirstChild: TaskList task } ? task.Checked : null;
                items.Add(new NotesListItem(Blocks(item), done));
            }
            var start = list.IsOrdered && int.TryParse(list.OrderedStart, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : 1;
            return new NotesList(list.IsOrdered, start, items);
        }

        private NotesTable Table(Table table)
        {
            var rows = new List<IReadOnlyList<NotesCell>>();
            for (var r = 0; r < table.Count; r++)
            {
                if (table[r] is not TableRow row)
                {
                    continue;
                }
                var cells = new List<NotesCell>();
                for (var c = 0; c < row.Count; c++)
                {
                    var inlines = new List<NotesInline>();
                    if (row[c] is TableCell cell)
                    {
                        for (var b = 0; b < cell.Count; b++)
                        {
                            if (cell[b] is LeafBlock { Inline: { } inline })
                            {
                                inlines.AddRange(Inlines(inline));
                            }
                        }
                    }
                    Tidy(inlines);
                    cells.Add(new NotesCell(inlines));
                }
                rows.Add(cells);
            }
            return new NotesTable(rows);
        }

        private List<NotesInline> Inlines(ContainerInline? container, NotesStyle style = NotesStyle.None, bool inLink = false)
        {
            var output = new List<NotesInline>();
            for (var inline = container?.FirstChild; inline is not null; inline = inline.NextSibling)
            {
                if (_skipping && inline is not HtmlInline)
                {
                    continue;
                }
                switch (inline)
                {
                    case LiteralInline literal:
                        output.Add(new NotesText(literal.Content.ToString(), style));
                        break;

                    case EmphasisInline emphasis:
                        output.AddRange(Inlines(emphasis, style | Emphasis(emphasis), inLink));
                        break;

                    case CodeInline code:
                        output.Add(new NotesText(code.Content, style | NotesStyle.Code));
                        break;

                    case LineBreakInline:
                        // GitHub shows a line break inside a release paragraph as <br> (checked 2026-11-07, ripgrep 15.2.0).
                        output.Add(new NotesLineBreak());
                        break;

                    case LinkInline { IsImage: true } image:
                        output.Add(Image(PlainText(Inlines(image, style, inLink: true)), image.Url, style, inLink));
                        break;

                    case LinkInline link:
                        var text = Inlines(link, style, inLink: true);
                        if (!inLink && Resolve(link.GetDynamicUrl?.Invoke() ?? link.Url) is { } uri && text.Count > 0)
                        {
                            output.Add(new NotesLink(text, uri));
                        }
                        else
                        {
                            output.AddRange(text);
                        }
                        break;

                    case AutolinkInline auto:
                        output.Add(!auto.IsEmail && !inLink && Resolve(auto.Url) is { } autoUri
                            ? new NotesLink([new NotesText(auto.Url, style)], autoUri)
                            : new NotesText(auto.Url, style));
                        break;

                    case HtmlEntityInline entity:
                        output.Add(new NotesText(entity.Transcoded.ToString(), style));
                        break;

                    case HtmlInline html:
                        Html(html.Tag, output, style, inLink);
                        break;

                    case TaskList:
                        break; // shown as the item's check box

                    case ContainerInline other:
                        output.AddRange(Inlines(other, style, inLink));
                        break;
                }
            }
            return output;
        }

        private static NotesStyle Emphasis(EmphasisInline emphasis) => emphasis.DelimiterChar switch
        {
            '~' => NotesStyle.Strike,
            _ => emphasis.DelimiterCount switch
            {
                1 => NotesStyle.Italic,
                2 => NotesStyle.Bold,
                _ => NotesStyle.Bold | NotesStyle.Italic,
            },
        };

        /// <summary>R2.5: never loaded, only a link to it, named by its alt text.</summary>
        private NotesInline Image(string alt, string? url, NotesStyle style, bool inLink)
        {
            alt = alt.Trim();
            var label = new NotesText(alt.Length > 0 ? $"Image: {alt}" : "Image", style);
            return !inLink && Resolve(url) is { } uri ? new NotesLink([label], uri) : label;
        }

        /// <summary>R2.7: relative to the release page, then http(s) only.</summary>
        private Uri? Resolve(string? url)
        {
            if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url.Trim(), UriKind.RelativeOrAbsolute, out var uri))
            {
                return null;
            }
            if (!uri.IsAbsoluteUri && !Uri.TryCreate(page, uri, out uri))
            {
                return null;
            }
            return WebLinks.IsWeb(uri) ? uri : null;
        }

        /// <summary>
        /// R2.6: HTML is never rendered. Tags are dropped and the text between them kept, entities decoded; &lt;br&gt;
        /// and block tags break the line, &lt;img&gt; is an image link, and &lt;script&gt; / &lt;style&gt; lose their text.
        /// </summary>
        private void Html(string html, List<NotesInline> output, NotesStyle style = NotesStyle.None, bool inLink = false)
        {
            var text = new StringBuilder();
            var i = 0;
            while (i < html.Length)
            {
                if (string.CompareOrdinal(html, i, "<!--", 0, 4) == 0)
                {
                    var close = html.IndexOf("-->", i + 4, StringComparison.Ordinal);
                    i = close < 0 ? html.Length : close + 3;
                    continue;
                }
                if (html[i] == '<' && html.IndexOf('>', i + 1) is var end and > 0 && TagName(html[(i + 1)..end]) is { } name)
                {
                    Flush();
                    var tag = html[(i + 1)..end];
                    var closing = tag.StartsWith('/');
                    if (name is "script" or "style")
                    {
                        _skipping = !closing;
                    }
                    else if (!_skipping && name == "br")
                    {
                        output.Add(new NotesLineBreak());
                    }
                    else if (!_skipping && name == "img" && !closing)
                    {
                        output.Add(Image(Attribute(tag, "alt") ?? "", Attribute(tag, "src"), style, inLink));
                    }
                    else if (!_skipping && BlockTags.Contains(name))
                    {
                        output.Add(new NotesLineBreak());
                    }
                    i = end + 1;
                    continue;
                }
                if (!_skipping)
                {
                    text.Append(html[i]);
                }
                i++;
            }
            Flush();

            void Flush()
            {
                if (text.Length > 0)
                {
                    var decoded = WhiteSpace().Replace(WebUtility.HtmlDecode(text.ToString()), " ");
                    if (decoded.Length > 0)
                    {
                        output.Add(new NotesText(decoded, style));
                    }
                    text.Clear();
                }
            }
        }

        private static readonly HashSet<string> BlockTags =
            ["p", "div", "details", "summary", "li", "ul", "ol", "h1", "h2", "h3", "h4", "h5", "h6", "tr", "table", "blockquote", "pre", "hr", "dl", "dt", "dd"];

        private static string? TagName(string tag)
        {
            var match = TagNamePattern().Match(tag);
            return match.Success ? match.Groups[1].Value.ToLowerInvariant() : null;
        }

        private static string? Attribute(string tag, string name)
        {
            foreach (Match match in AttributePattern().Matches(tag))
            {
                if (string.Equals(match.Groups[1].Value, name, StringComparison.OrdinalIgnoreCase))
                {
                    var value = match.Groups[2].Success ? match.Groups[2].Value
                        : match.Groups[3].Success ? match.Groups[3].Value
                        : match.Groups[4].Value;
                    return WebUtility.HtmlDecode(value);
                }
            }
            return null;
        }
    }

    /// <summary>Leading, trailing and doubled line breaks and empty texts go; text next to a break is trimmed.</summary>
    private static void Tidy(List<NotesInline> inlines)
    {
        inlines.RemoveAll(inline => inline is NotesText { Text.Length: 0 });
        for (var i = 0; i < inlines.Count; i++)
        {
            if (inlines[i] is NotesLineBreak && (i == 0 || i == inlines.Count - 1 || inlines[i - 1] is NotesLineBreak))
            {
                inlines.RemoveAt(i);
                i = Math.Max(-1, i - 2);
            }
        }
        for (var i = 0; i < inlines.Count; i++)
        {
            if (inlines[i] is not NotesText text)
            {
                continue;
            }
            var trimmed = text.Text;
            if (i == 0 || inlines[i - 1] is NotesLineBreak)
            {
                trimmed = trimmed.TrimStart();
            }
            if (i == inlines.Count - 1 || inlines[i + 1] is NotesLineBreak)
            {
                trimmed = trimmed.TrimEnd();
            }
            if (trimmed.Length == 0)
            {
                inlines.RemoveAt(i);
                i = Math.Max(-1, i - 2);
            }
            else if (trimmed.Length != text.Text.Length)
            {
                inlines[i] = text with { Text = trimmed };
            }
        }
        if (inlines.Count > 0 && (inlines[0] is NotesLineBreak || inlines[^1] is NotesLineBreak))
        {
            Tidy(inlines);
        }
    }

    [GeneratedRegex(@"^/?([A-Za-z][A-Za-z0-9]*)(?=[\s/>]|$)")]
    private static partial Regex TagNamePattern();

    [GeneratedRegex("""([A-Za-z_:][-A-Za-z0-9_:.]*)\s*=\s*(?:"([^"]*)"|'([^']*)'|([^\s"'>]+))""")]
    private static partial Regex AttributePattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhiteSpace();
}

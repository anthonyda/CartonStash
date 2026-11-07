using CartonStash.Core.Models;
using CartonStash.Core.Services;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Windows.System;
using Windows.UI.Text;

namespace CartonStash.App.Controls;

/// <summary>
/// Draws a <see cref="NotesDocument"/> (spec 012 R1, R2.4 to R2.8). Built in code from the document's records:
/// one selectable RichTextBlock per paragraph, so a screen reader reads blocks in order (R1.7). Only theme brushes,
/// so light, dark and high contrast work (Q5).
/// </summary>
public sealed partial class NotesView : UserControl
{
    public static readonly DependencyProperty DocumentProperty =
        DependencyProperty.Register(nameof(Document), typeof(NotesDocument), typeof(NotesView), new PropertyMetadata(null, OnDocumentChanged));

    private static readonly FontFamily Mono = new("Cascadia Mono, Consolas, Courier New");

    public NotesView()
    {
        InitializeComponent();
        Viewport.IsTallChanged += (_, _) => UpdateToggle();
    }

    public NotesDocument? Document
    {
        get => (NotesDocument?)GetValue(DocumentProperty);
        set => SetValue(DocumentProperty, value);
    }

    private static void OnDocumentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (NotesView)d;
        view.Blocks.Children.Clear();
        if (e.NewValue is NotesDocument doc)
        {
            foreach (var block in doc.Blocks)
            {
                view.Blocks.Children.Add(view.Block(block));
            }
        }
        // A new document starts collapsed (design: Collapsing).
        view.Viewport.IsCollapsed = true;
        view.UpdateToggle();
    }

    private void Toggle_Click(object sender, RoutedEventArgs e)
    {
        Viewport.IsCollapsed = !Viewport.IsCollapsed;
        UpdateToggle();
    }

    private void UpdateToggle()
    {
        Toggle.Visibility = Viewport.IsTall ? Visibility.Visible : Visibility.Collapsed;
        Toggle.Content = Viewport.IsCollapsed ? "Show more" : "Show less";
        AutomationProperties.SetName(Toggle, Viewport.IsCollapsed ? "Show more release notes" : "Show less release notes");
    }

    private UIElement Block(NotesBlock block) => block switch
    {
        NotesParagraph p => Rich(p.Inlines),
        NotesHeading h => Heading(h),
        NotesList list => List(list),
        NotesQuote quote => new Border { Style = NamedStyle("QuoteStyle"), Child = Stack(quote.Blocks, 8) },
        NotesCode code => Code(code),
        NotesTable table => Table(table),
        NotesRule => new Border { Style = NamedStyle("RuleStyle") },
        _ => new TextBlock(),
    };

    private StackPanel Stack(IReadOnlyList<NotesBlock> blocks, double spacing)
    {
        var panel = new StackPanel { Spacing = spacing };
        foreach (var block in blocks)
        {
            panel.Children.Add(Block(block));
        }
        return panel;
    }

    private static RichTextBlock Rich(IReadOnlyList<NotesInline> inlines)
    {
        var paragraph = new Paragraph();
        Add(paragraph.Inlines, inlines);
        var rich = new RichTextBlock { IsTextSelectionEnabled = true, TextWrapping = TextWrapping.Wrap };
        rich.Blocks.Add(paragraph);
        return rich;
    }

    /// <summary>Notes headings sit under the pane's package name (level 2), so H1 is level 3.</summary>
    private static RichTextBlock Heading(NotesHeading heading)
    {
        var rich = Rich(heading.Inlines);
        rich.FontWeight = FontWeights.SemiBold;
        rich.FontSize = heading.Level switch { 1 => 20, 2 => 18, 3 => 16, _ => 14 };
        rich.Margin = new Thickness(0, 4, 0, 0);
        AutomationProperties.SetHeadingLevel(rich, (AutomationHeadingLevel)Math.Min(heading.Level + 2, 9));
        return rich;
    }

    private StackPanel List(NotesList list)
    {
        var panel = new StackPanel { Spacing = 4 };
        for (var i = 0; i < list.Items.Count; i++)
        {
            var item = list.Items[i];
            var marker = new TextBlock
            {
                Text = item.Checked switch
                {
                    true => "☑",
                    false => "☐",
                    null => list.Ordered ? $"{list.Start + i}." : "•",
                },
                MinWidth = 16,
                Margin = new Thickness(0, 0, 6, 0),
            };
            if (item.Checked is { } done)
            {
                AutomationProperties.SetName(marker, done ? "Done" : "Not done");
            }
            var content = Stack(item.Blocks, 4);
            Grid.SetColumn(content, 1);

            var row = new Grid { ColumnDefinitions = { new ColumnDefinition { Width = GridLength.Auto }, new ColumnDefinition() } };
            row.Children.Add(marker);
            row.Children.Add(content);
            panel.Children.Add(row);
        }
        return panel;
    }

    private Border Code(NotesCode code) => new()
    {
        Style = NamedStyle("CodeStyle"),
        Child = Sideways(new TextBlock
        {
            Text = code.Text,
            FontFamily = Mono,
            IsTextSelectionEnabled = true,
            TextWrapping = TextWrapping.NoWrap,
        }),
    };

    private ScrollViewer Table(NotesTable table)
    {
        var grid = new Grid { Style = NamedStyle("TableStyle") };
        var columns = table.Rows.Count == 0 ? 0 : table.Rows.Max(r => r.Count);
        for (var c = 0; c < columns; c++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        }
        for (var r = 0; r < table.Rows.Count; r++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (var c = 0; c < columns; c++)
            {
                var text = Rich(c < table.Rows[r].Count ? table.Rows[r][c].Inlines : []);
                text.TextWrapping = TextWrapping.NoWrap;
                if (r == 0)
                {
                    text.FontWeight = FontWeights.SemiBold; // the header row
                }
                var cell = new Border { Style = NamedStyle("CellStyle"), Child = text };
                Grid.SetRow(cell, r);
                Grid.SetColumn(cell, c);
                grid.Children.Add(cell);
            }
        }
        return Sideways(grid);
    }

    /// <summary>Wide code and tables scroll sideways inside the pane instead of widening it.</summary>
    private static ScrollViewer Sideways(UIElement content) => new()
    {
        Content = content,
        HorizontalScrollMode = ScrollMode.Auto,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
        VerticalScrollMode = ScrollMode.Disabled,
        VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
        Padding = new Thickness(0, 0, 0, 4),
    };

    private static void Add(InlineCollection target, IEnumerable<NotesInline> inlines)
    {
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case NotesText text:
                    var run = new Run { Text = text.Text };
                    if (text.Style.HasFlag(NotesStyle.Bold))
                    {
                        run.FontWeight = FontWeights.SemiBold;
                    }
                    if (text.Style.HasFlag(NotesStyle.Italic))
                    {
                        run.FontStyle = FontStyle.Italic;
                    }
                    if (text.Style.HasFlag(NotesStyle.Strike))
                    {
                        run.TextDecorations = TextDecorations.Strikethrough;
                    }
                    if (text.Style.HasFlag(NotesStyle.Code))
                    {
                        run.FontFamily = Mono;
                    }
                    target.Add(run);
                    break;

                case NotesLink link when WebLinks.IsWeb(link.Uri):
                    var hyperlink = new Hyperlink();
                    Add(hyperlink.Inlines, link.Inlines);
                    var label = LinkText(link.Inlines);
                    AutomationProperties.SetName(hyperlink, $"{label} (opens browser)");
                    ToolTipService.SetToolTip(hyperlink, $"{link.Uri.AbsoluteUri} (opens browser)");
                    var uri = link.Uri;
                    // 005 R1.3: the default browser, never embedded content.
                    hyperlink.Click += async (_, _) => await Launcher.LaunchUriAsync(uri);
                    target.Add(hyperlink);
                    break;

                case NotesLink link:
                    Add(target, link.Inlines); // never reached: Core only makes web links
                    break;

                case NotesLineBreak:
                    target.Add(new LineBreak());
                    break;
            }
        }
    }

    private static string LinkText(IEnumerable<NotesInline> inlines) =>
        string.Concat(inlines.Select(i => i switch
        {
            NotesText t => t.Text,
            NotesLink l => LinkText(l.Inlines),
            _ => " ",
        }));

    private Style NamedStyle(string key) => (Style)Resources[key];
}

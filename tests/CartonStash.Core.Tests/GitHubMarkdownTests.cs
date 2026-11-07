using CartonStash.Core.Models;
using CartonStash.Core.Services;

namespace CartonStash.Core.Tests;

/// <summary>Spec 012 R2.4 to R2.7.</summary>
public class GitHubMarkdownTests
{
    private static readonly Uri Page = new("https://github.com/owner/repo/releases/tag/v1.0");

    private static string Parse(string body, string? title = null) => NotesDump.Describe(GitHubMarkdown.Parse(body, Page, title));

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    [Fact]
    public void Headings_keep_their_level() // R2.4
    {
        Assert.Equal("H1: One\nH2: Two\nH6: Six\nH2: Setext", Parse("# One\n## Two\n###### Six\n\nSetext\n---"));
    }

    [Fact]
    public void Emphasis_strikethrough_and_inline_code() // R2.4
    {
        Assert.Equal("P: _it_ **bold** **_both_** ~~gone~~ `code`", Parse("*it* **bold** ***both*** ~~gone~~ `code`"));
    }

    [Fact]
    public void Line_breaks_inside_a_paragraph_are_kept_as_GitHub_shows_them() // R2.4
    {
        Assert.Equal("P: one⏎two\nP: three", Parse("one\ntwo\n\nthree"));
    }

    [Fact]
    public void Fenced_code_keeps_its_lines() // R2.4
    {
        Assert.Equal("code: gh pr create⏎  --attach ./a.png", Parse("```sh\ngh pr create\n  --attach ./a.png\n```"));
    }

    [Fact]
    public void Nested_numbered_and_task_lists() // R2.4
    {
        Assert.Equal("""
            - one
              1. a
              2. b
            - two
            [x] done
            [ ] todo
            """, Parse("- one\n  1. a\n  2. b\n- two\n\n- [x] done\n- [ ] todo"));
        // A numbered list can start anywhere when it doesn't interrupt a paragraph (CommonMark, as on GitHub).
        Assert.Equal("3. three\n4. four", Parse("3. three\n4. four"));
    }

    [Fact]
    public void Quote_and_rule() // R2.4
    {
        Assert.Equal("> P: quoted\nrule\nP: after", Parse("> quoted\n\n---\n\nafter"));
    }

    [Fact]
    public void Pipe_table_with_a_header_row() // R2.4
    {
        Assert.Equal("""
            table: Name | Hash
            table: **x64** | `ABC`
            """, Parse("| Name | Hash |\n| --- | --- |\n| **x64** | `ABC` |"));
    }

    [Fact]
    public void Inline_and_reference_links() // R2.4
    {
        Assert.Equal(
            "P: [inline](https://example.com/a) and [ref](https://example.com/r) and [https://example.com/auto](https://example.com/auto)",
            Parse("[inline](https://example.com/a) and [ref][r] and https://example.com/auto\n\n[r]: https://example.com/r"));
    }

    [Fact]
    public void Images_are_links_never_pictures() // R2.5
    {
        Assert.Equal(
            "P: [Image: Screenshot](https://example.com/s.png) [Image](https://example.com/t.png)",
            Parse("![Screenshot](https://example.com/s.png) ![](https://example.com/t.png)"));
    }

    [Fact]
    public void A_badge_inside_a_link_is_one_link() // R2.5
    {
        Assert.Equal("P: [Image: build](https://example.com/ci)", Parse("[![build](https://example.com/b.svg)](https://example.com/ci)"));
    }

    [Fact]
    public void Html_block_keeps_its_text_only() // R2.6
    {
        Assert.Equal(
            "P: Full list⏎Fixed &lt;things&gt; & more",
            Parse("<details>\n<summary>Full list</summary>\n\nFixed &amp;lt;things&amp;gt; &amp; more\n</details>").Replace("\nP: ", "⏎", StringComparison.Ordinal));
    }

    [Fact]
    public void Html_img_and_br() // R2.5, R2.6
    {
        Assert.Equal(
            "P: [Image: Logo](https://example.com/logo.png)⏎Line two",
            Parse("<p align=\"center\"><img src=\"https://example.com/logo.png\" alt=\"Logo\" width=\"200\"><br>Line two</p>"));
    }

    [Fact]
    public void Inline_br_breaks_the_line_and_other_tags_vanish() // R2.6
    {
        Assert.Equal("P: one⏎two **three** four", Parse("one<br>two <kbd>**three**</kbd> four"));
    }

    [Fact]
    public void Script_and_style_lose_their_text() // R2.6
    {
        Assert.Equal("P: before\nP: after", Parse("before\n\n<script>\nalert(1)\n</script>\n\n<style>p { color: red }</style>\n\nafter"));
    }

    [Fact]
    public void An_unclosed_script_hides_only_its_own_block() // R2.6
    {
        // An inline <script> with no end tag. (A <script> block runs to its end tag, or the end of the notes, in CommonMark.)
        Assert.Equal("P: a\nP: after", Parse("a <script> b\n\nafter"));
    }

    [Fact]
    public void Html_comments_are_dropped() // R2.6
    {
        Assert.Equal("P: shown", Parse("<!-- hidden -->\n\nshown"));
    }

    [Theory] // R2.7
    [InlineData("[a](../tag/v0.9)", "P: [a](https://github.com/owner/repo/releases/tag/v0.9)")]
    [InlineData("[a](/owner/repo/issues/1)", "P: [a](https://github.com/owner/repo/issues/1)")]
    [InlineData("[a](#notes)", "P: [a](https://github.com/owner/repo/releases/tag/v1.0#notes)")]
    [InlineData("[a](javascript:alert(1))", "P: a")]
    [InlineData("[a](file:///c:/x)", "P: a")]
    [InlineData("[a](ms-settings:privacy)", "P: a")]
    [InlineData("<user@example.com>", "P: user@example.com")]
    public void Links_resolve_against_the_page_and_must_be_web(string body, string expected) =>
        Assert.Equal(expected, Parse(body));

    [Fact]
    public void A_first_heading_that_repeats_the_title_is_dropped()
    {
        Assert.Equal("P: Body", Parse("# Release v1.0\n\nBody", title: "Release v1.0"));
        Assert.Equal("H1: Something else\nP: Body", Parse("# Something else\n\nBody", title: "Release v1.0"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  \n")]
    public void Empty_body_gives_an_empty_document(string? body) =>
        Assert.Empty(GitHubMarkdown.Parse(body, Page).Blocks);

    [Fact]
    public void PowerToys_release() // R2.4 to R2.7, fixture saved 2026-11-07
    {
        var doc = GitHubMarkdown.Parse(Fixture("powertoys-v0.102.3101.md"),
            new Uri("https://github.com/microsoft/PowerToys/releases/tag/v0.102.3101"), "Release v0.102.3101.0");
        var text = NotesDump.Describe(doc);

        Assert.IsNotType<NotesHeading>(doc.Blocks[0]); // the title heading is left out
        Assert.StartsWith("P: This hotfix release improves File Explorer thumbnail generation", text);
        Assert.Contains("[v0.102.2933.0 release notes](https://github.com/microsoft/PowerToys/releases/tag/v0.102.2933.0)", text);
        Assert.Contains("H2: Installer Hashes", text);
        var table = Assert.Single(doc.Blocks.OfType<NotesTable>());
        Assert.Equal(["Description", "Filename", "SHA256"], table.Rows[0].Select(c => NotesDump.Inlines(c.Inlines)));
        Assert.True(table.Rows.Count > 2);
        // Reference-style links in the table resolve to the installers.
        Assert.Contains("[PowerToysUserSetup-0.102.3101.0-x64.exe](https://github.com/microsoft/PowerToys/releases/download/", text);
    }

    [Fact]
    public void Ripgrep_release() // R2.4, fixture saved 2026-11-07
    {
        var text = Parse(Fixture("ripgrep-15.2.0.md"));
        Assert.Contains("gitignore matching as well as⏎some performance", text);
        Assert.Contains("> P: In case you haven't heard of it before", text);
        Assert.Contains("[BUG #3212](https://github.com/BurntSushi/ripgrep/pull/3212)", text);
    }
}

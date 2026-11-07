using CartonStash.Core.Models;
using CartonStash.Core.Services;

namespace CartonStash.Core.Tests;

/// <summary>Spec 012 R1. The fixtures are real ReleaseNotes values from winget-pkgs (2026-11-06), shortened.</summary>
public class WinGetNotesParserTests
{
    private static string Parse(string text) => NotesDump.Describe(WinGetNotesParser.Parse(text));

    [Fact]
    public void Git_section_title_and_bullets() // R1.1, R1.5, R1.9
    {
        const string git = """
            Changes since Git for Windows v2.56.0(3) (October 22nd 2026)
            This is another urgent security bug fix release, addressing CVE-2026-111497.
            Bug Fixes
            - The executables included in the 32-bit MinGit had corrupt code-signatures, which has been fixed.
            - Upgrades the included brotli component to v1.2.1 to address CVE-2026-111497.
            """;
        Assert.Equal("""
            P: Changes since Git for Windows v2.56.0(3) (October 22nd 2026)⏎This is another urgent security bug fix release, addressing CVE-2026-111497.
            P: **Bug Fixes**
            - The executables included in the 32-bit MinGit had corrupt code-signatures, which has been fixed.
            - Upgrades the included brotli component to v1.2.1 to address CVE-2026-111497.
            """, Parse(git));
    }

    [Fact]
    public void Ripgrep_quote_wrapped_items_and_colon_titles() // R1.1, R1.2, R1.9
    {
        const string ripgrep = """
            Sponsorship is appreciated!
            This release fixes a number of bugs.
            > In case you haven't heard of it before, ripgrep is a line-oriented search
            > tool that recursively searches the current directory for a regex pattern.
            Platform support:
            - aarch64-unknown-linux-musl is now included in the release binaries for
              ripgrep.
            Bug fixes:
            - BUG #3320,
              BUG #3376:
              Fix gitignore matching bug when searching across multiple directories.
            """;
        Assert.Equal("""
            P: Sponsorship is appreciated!⏎This release fixes a number of bugs.
            > P: In case you haven't heard of it before, ripgrep is a line-oriented search⏎tool that recursively searches the current directory for a regex pattern.
            P: **Platform support:**
            - aarch64-unknown-linux-musl is now included in the release binaries for⏎ripgrep.
            P: **Bug fixes:**
            - BUG #3320,⏎BUG #3376:⏎Fix gitignore matching bug when searching across multiple directories.
            """, Parse(ripgrep));
    }

    [Fact]
    public void Bat_mentions_and_quotes_stay_as_written() // R1.3
    {
        const string bat = """
            v0.26.1
            Features
            - Add paging to '-h' and '--help' see PR #3478 (@MuntasirSZN)
            Syntaxes
            - Update quadlet syntax mapping to include *.{build,pod} files #3484 (@cyqsimon)
            """;
        Assert.Equal("""
            P: v0.26.1
            P: **Features**
            - Add paging to '-h' and '--help' see PR #3478 (@MuntasirSZN)
            P: **Syntaxes**
            - Update quadlet syntax mapping to include *.{build,pod} files #3484 (@cyqsimon)
            """, Parse(bat));
    }

    [Fact]
    public void Firefox_blank_lines_between_items_keep_one_list() // R1.1, R1.5
    {
        const string firefox = """
            Version 159.0, first offered to Release channel users on October 27, 2026

            New
            - Sharing on macOS now uses the native macOS share picker.

            - Saved passwords are now available from the sidebar by default.
            """;
        Assert.Equal("""
            P: Version 159.0, first offered to Release channel users on October 27, 2026
            P: **New**
            - Sharing on macOS now uses the native macOS share picker.
            - Saved passwords are now available from the sidebar by default.
            """, Parse(firefox));
    }

    [Fact]
    public void GitHub_cli_hash_lines_are_not_headings() // R1.3
    {
        const string gh = """
            Attach images and videos to issues and pull requests
            The repeatable --attach flag uploads local images and videos:
            # Attach files when creating or editing an issue
            gh issue create --attach './repro.png#The error state'
            """;
        Assert.Equal("""
            P: Attach images and videos to issues and pull requests⏎The repeatable --attach flag uploads local images and videos:⏎# Attach files when creating or editing an issue⏎gh issue create --attach './repro.png#The error state'
            """, Parse(gh));
    }

    [Fact]
    public void Obsidian_title_not_above_a_list_stays_plain() // R1.9
    {
        const string obsidian = """
            No longer broken
            - Fixed Global Search reporting inaccurate result count.
            - Bases: file.hasTag('#tag') is no longer case-sensitive.
            Developers
            The installer has been updated to use Electron v37.6.0.
            """;
        Assert.Equal("""
            P: **No longer broken**
            - Fixed Global Search reporting inaccurate result count.
            - Bases: file.hasTag('#tag') is no longer case-sensitive.
            P: Developers⏎The installer has been updated to use Electron v37.6.0.
            """, Parse(obsidian));
    }

    [Fact]
    public void Nested_items_and_back_out() // R1.1
    {
        const string text = """
            - one
              - one.a
                - one.a.i
              - one.b
            - two
            * three
            • four
            """;
        Assert.Equal("""
            - one
              - one.a
                - one.a.i
              - one.b
            - two
            - three
            - four
            """, Parse(text));
    }

    [Fact]
    public void Text_after_a_list_ends_it() // R1.1
    {
        Assert.Equal("""
            - item
            P: After the list.
            """, Parse("- item\nAfter the list."));
    }

    [Fact]
    public void Blank_line_then_text_ends_the_list() // R1.1, R1.5
    {
        Assert.Equal("""
            - item
            P: Indented later.
            """, Parse("- item\n\n  Indented later."));
    }

    [Fact]
    public void Quote_lines_form_one_quote_and_a_bare_marker_splits_paragraphs() // R1.2
    {
        Assert.Equal("""
            > P: first⏎second
            > P: third
            """, Parse("> first\n> second\n>\n> third"));
    }

    [Fact]
    public void Paired_bold_and_code_are_formatted() // R1.3
    {
        Assert.Equal("P: Use **winget** with `--id` now", Parse("Use **winget** with `--id` now"));
    }

    [Theory] // R1.3: anything unpaired stays exactly as written
    [InlineData("a * b")]
    [InlineData("**no close")]
    [InlineData("a ` b")]
    [InlineData("snake_case_name and _x_")]
    [InlineData("****")]
    [InlineData("[#7222, #10896]")]
    [InlineData("[x](file:///c:/windows)")]
    [InlineData("[x](ms-settings:privacy)")]
    [InlineData("[x](not a url)")]
    [InlineData("---")]
    public void Unpaired_markers_stay_as_written(string text) => Assert.Equal($"P: {text}", Parse(text));

    [Fact]
    public void Markdown_link_with_web_address() // R1.3
    {
        Assert.Equal("P: See [the **notes**](https://example.com/n) now", Parse("See [the **notes**](https://example.com/n) now"));
    }

    [Theory] // R1.4
    [InlineData("See https://example.com/a.", "P: See [https://example.com/a](https://example.com/a).")]
    [InlineData("(https://example.com/a)", "P: ([https://example.com/a](https://example.com/a))")]
    [InlineData("https://en.wikipedia.org/wiki/Foo_(bar)).", "P: [https://en.wikipedia.org/wiki/Foo_(bar)](https://en.wikipedia.org/wiki/Foo_(bar))).")]
    [InlineData("http://example.com/x, then", "P: [http://example.com/x](http://example.com/x), then")]
    [InlineData("ftp://example.com/x", "P: ftp://example.com/x")]
    [InlineData("nothttps://example.com", "P: nothttps://example.com")]
    [InlineData("https://", "P: https://")]
    public void Bare_web_addresses_become_links(string text, string expected) => Assert.Equal(expected, Parse(text));

    [Fact]
    public void Code_hides_other_formatting() // R1.3
    {
        Assert.Equal("P: `**not bold** https://x.com`", Parse("`**not bold** https://x.com`"));
    }

    [Theory] // R1.9
    [InlineData("This title is exactly forty-one characters\n- item", false)]
    [InlineData("This title is exactly forty characters!!\n- item", false)]
    [InlineData("A sentence.\n- item", false)]
    [InlineData("Really?\n- item", false)]
    [InlineData("Fixed\n\n- item", false)]
    [InlineData("This title is exactly forty characters ok\n- item", false)]
    [InlineData("This title is exactly forty characters o\n- item", true)]
    [InlineData("Fixed\n- item", true)]
    public void Section_titles(string text, bool bold)
    {
        var first = Assert.IsType<NotesParagraph>(WinGetNotesParser.Parse(text)!.Blocks[0]);
        var style = Assert.IsType<NotesText>(first.Inlines[0]).Style;
        Assert.Equal(bold, style.HasFlag(NotesStyle.Bold));
    }

    [Fact]
    public void Tabs_count_as_four_spaces() // R1.1
    {
        Assert.Equal("- one\n  - two", Parse("- one\n\t- two"));
    }

    [Fact]
    public void Crlf_line_endings() => Assert.Equal("- one\n- two", Parse("- one\r\n- two\r\n"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  \n ")]
    public void Nothing_to_show(string? text) => Assert.Null(WinGetNotesParser.Parse(text));

    [Fact]
    public void A_parser_failure_gives_the_plain_text() // R1.8
    {
        var doc = WinGetNotesParser.Parse("- **one**\n\ntwo", _ => throw new InvalidOperationException());
        Assert.Equal("P: - **one**\nP: two", NotesDump.Describe(doc));
    }
}

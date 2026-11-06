using CartonStash.Core.Services;

namespace CartonStash.Core.Tests;

/// <summary>003 amendment R7 (T10): which running programs a portable package's folder holds (R7.2).</summary>
public class RunningProgramsTests
{
    [Theory]
    [InlineData(@"C:\Apps\Foo\foo.exe", @"C:\Apps\Foo", true)]
    [InlineData(@"C:\Apps\Foo\bin\foo.exe", @"C:\Apps\Foo", true)]
    [InlineData(@"C:\Apps\Foo\foo.exe", @"C:\Apps\Foo\", true)]
    [InlineData(@"c:\apps\foo\FOO.EXE", @"C:\Apps\Foo", true)]
    [InlineData(@"C:\Apps\FooBar\foo.exe", @"C:\Apps\Foo", false)]
    [InlineData(@"C:\Apps\foo.exe", @"C:\Apps\Foo", false)]
    [InlineData(@"C:\Apps\Foo\..\Bar\bar.exe", @"C:\Apps\Foo", false)]
    [InlineData(@"C:\Apps\Foo\foo.exe", "", false)]
    [InlineData("", @"C:\Apps\Foo", false)]
    public void Folder_holds_only_paths_inside_it(string path, string folder, bool expected) // R7.2
    {
        Assert.Equal(expected, RunningPrograms.IsIn(path, folder));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Finds_the_test_process_in_its_own_folder() // R7.2
    {
        var self = Environment.ProcessPath!;

        var names = new RunningPrograms().FileNamesIn(Path.GetDirectoryName(self)!);

        Assert.Contains(Path.GetFileName(self), names, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(names.Count, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Finds_nothing_in_an_empty_folder() // R7.3
    {
        var empty = Directory.CreateTempSubdirectory("cartonstash-r7-").FullName;
        try
        {
            Assert.Empty(new RunningPrograms().FileNamesIn(empty));
        }
        finally
        {
            Directory.Delete(empty);
        }
    }
}

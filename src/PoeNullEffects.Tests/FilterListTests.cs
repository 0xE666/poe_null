using PoeNullEffects.Core;

namespace PoeNullEffects.Tests;

public class FilterListTests : IDisposable
{
    private readonly string _tempDir;

    public FilterListTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"poe_null_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void Load_ParsesLines_IgnoresBlanksAndComments()
    {
        var path = Path.Combine(_tempDir, "list.txt");
        File.WriteAllText(path, "blood\n\n# comment\nrain\n  fog  \n");

        var list = FilterList.Load(path);

        Assert.Equal(3, list.Entries.Count);
        Assert.Contains("blood", list.Entries);
        Assert.Contains("rain", list.Entries);
        Assert.Contains("fog", list.Entries);
    }

    [Fact]
    public void Matches_ReturnsTrueForSubstringMatch()
    {
        var path = Path.Combine(_tempDir, "list.txt");
        File.WriteAllText(path, "blood\nrain\n");

        var list = FilterList.Load(path);

        Assert.True(list.Matches("Metadata/Particles/blood_splatter.pet"));
        Assert.True(list.Matches("Metadata/Particles/Rain/heavy_rain.pet"));
        Assert.False(list.Matches("Metadata/Particles/lightning_orb.pet"));
    }

    [Fact]
    public void Matches_IsCaseInsensitive()
    {
        var path = Path.Combine(_tempDir, "list.txt");
        File.WriteAllText(path, "Blood\n");

        var list = FilterList.Load(path);

        Assert.True(list.Matches("metadata/particles/blood_splatter.pet"));
    }

    [Fact]
    public void Load_ReturnsEmptyForMissingFile()
    {
        var list = FilterList.Load(Path.Combine(_tempDir, "nope.txt"));

        Assert.Empty(list.Entries);
        Assert.False(list.Matches("anything"));
    }
}

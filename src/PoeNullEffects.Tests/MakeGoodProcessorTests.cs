using PoeNullEffects.Core;

namespace PoeNullEffects.Tests;

public class MakeGoodProcessorTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(6)]
    public void GetCategoriesForLevel_ReturnsValidCategories(int level)
    {
        var categories = MakeGoodProcessor.GetPreservedCategoriesForLevel(level);

        Assert.NotNull(categories);
        if (level > 0)
        {
            var lowerCategories = MakeGoodProcessor.GetPreservedCategoriesForLevel(level - 1);
            Assert.True(categories.Count >= lowerCategories.Count);
        }
    }

    [Fact]
    public void Level0_PreservesNothing()
    {
        var categories = MakeGoodProcessor.GetPreservedCategoriesForLevel(0);
        Assert.Empty(categories);
    }

    [Fact]
    public void Level6_PreservesEverything()
    {
        var categories = MakeGoodProcessor.GetPreservedCategoriesForLevel(6);
        Assert.Equal(MakeGoodProcessor.AllCategories.Count, categories.Count);
    }
}

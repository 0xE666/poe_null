using PoeNullEffects.Core;
using PoeNullEffects.Core.Models;

namespace PoeNullEffects.Tests;

public class ConfigTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _configPath;

    public ConfigTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"poe_null_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _configPath = Path.Combine(_tempDir, "config.ini");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void Load_DefaultValues_WhenFileDoesNotExist()
    {
        var config = Config.Load(_configPath);

        Assert.Equal("", config.GgpkPath);
        Assert.Equal(NullMode.AllExcept, config.NullParticlesMethod);
        Assert.Equal(0, config.KeepEmitters);
        Assert.Equal(2, config.MakeGoodValue);
    }

    [Fact]
    public void Load_ParsesExistingFile()
    {
        File.WriteAllText(_configPath,
            "ggpkPath=C:\\Games\\PoE\\Content.ggpk\n" +
            "nullParticlesMethod=2\n" +
            "keepEmitters=1\n" +
            "makeGoodValue=5\n");

        var config = Config.Load(_configPath);

        Assert.Equal(@"C:\Games\PoE\Content.ggpk", config.GgpkPath);
        Assert.Equal(NullMode.Only, config.NullParticlesMethod);
        Assert.Equal(1, config.KeepEmitters);
        Assert.Equal(5, config.MakeGoodValue);
    }

    [Fact]
    public void Save_WritesAllValues()
    {
        var config = Config.Load(_configPath);
        config.GgpkPath = @"D:\PoE2\Content.ggpk";
        config.NullParticlesMethod = NullMode.All;
        config.KeepEmitters = 1;
        config.MakeGoodValue = 4;

        config.Save();

        var reloaded = Config.Load(_configPath);
        Assert.Equal(@"D:\PoE2\Content.ggpk", reloaded.GgpkPath);
        Assert.Equal(NullMode.All, reloaded.NullParticlesMethod);
        Assert.Equal(1, reloaded.KeepEmitters);
        Assert.Equal(4, reloaded.MakeGoodValue);
    }

    [Fact]
    public void Save_CreatesDirectoryIfNeeded()
    {
        var nestedPath = Path.Combine(_tempDir, "sub", "dir", "config.ini");
        var config = Config.Load(nestedPath);
        config.GgpkPath = "test";

        config.Save();

        Assert.True(File.Exists(nestedPath));
    }
}

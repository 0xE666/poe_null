using PoeNullEffects.Core;

namespace PoeNullEffects.Tests;

public class BackupManagerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly BackupManager _manager;

    public BackupManagerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"poe_null_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _manager = new BackupManager(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void BackupFile_StoresData_GetBackupReturnsIt()
    {
        var data = new byte[] { 1, 2, 3, 4, 5 };
        _manager.BackupFile("Metadata/Particles/fire.pet", data);

        var result = _manager.GetBackup("Metadata/Particles/fire.pet");

        Assert.NotNull(result);
        Assert.Equal(data, result);
    }

    [Fact]
    public void GetBackup_ReturnsNull_WhenNotBackedUp()
    {
        var result = _manager.GetBackup("nonexistent.pet");

        Assert.Null(result);
    }

    [Fact]
    public void BackupFile_DoesNotOverwriteExisting()
    {
        var original = new byte[] { 1, 2, 3 };
        var modified = new byte[] { 9, 8, 7 };

        _manager.BackupFile("test.pet", original);
        _manager.BackupFile("test.pet", modified);

        var result = _manager.GetBackup("test.pet");
        Assert.Equal(original, result);
    }

    [Fact]
    public void HasBackup_ReturnsTrueWhenExists()
    {
        _manager.BackupFile("test.pet", [1, 2, 3]);

        Assert.True(_manager.HasBackup("test.pet"));
        Assert.False(_manager.HasBackup("other.pet"));
    }

    [Fact]
    public void SaveAndLoad_PersistsToDisk()
    {
        _manager.BackupFile("a.pet", [10, 20]);
        _manager.BackupFile("b.pet", [30, 40, 50]);
        _manager.Save();

        var loaded = new BackupManager(_tempDir);
        loaded.Load();

        Assert.Equal(new byte[] { 10, 20 }, loaded.GetBackup("a.pet"));
        Assert.Equal(new byte[] { 30, 40, 50 }, loaded.GetBackup("b.pet"));
    }

    [Fact]
    public void Clear_RemovesAllBackups()
    {
        _manager.BackupFile("a.pet", [1]);
        _manager.Clear();

        Assert.False(_manager.HasBackup("a.pet"));
    }
}

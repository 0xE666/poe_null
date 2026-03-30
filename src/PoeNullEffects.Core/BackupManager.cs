using System.Text;

namespace PoeNullEffects.Core;

/// <summary>
/// Stores original file data before modifications.
/// Binary format: [int32 count] then for each entry:
///   [int32 pathByteLen] [utf8 path] [int32 dataLen] [data bytes]
/// </summary>
public class BackupManager
{
    private readonly string _backupDir;
    private readonly string _backupFile;
    private readonly Dictionary<string, byte[]> _backups = new(StringComparer.OrdinalIgnoreCase);

    public BackupManager(string backupDir)
    {
        _backupDir = backupDir;
        _backupFile = Path.Combine(backupDir, "backup.bin");
    }

    public int Count => _backups.Count;

    public void BackupFile(string path, byte[] originalData)
    {
        _backups.TryAdd(path, originalData);
    }

    public byte[]? GetBackup(string path)
    {
        return _backups.GetValueOrDefault(path);
    }

    public bool HasBackup(string path)
    {
        return _backups.ContainsKey(path);
    }

    public IReadOnlyDictionary<string, byte[]> GetAllBackups()
    {
        return _backups;
    }

    public void Clear()
    {
        _backups.Clear();
        if (File.Exists(_backupFile))
            File.Delete(_backupFile);
    }

    public void Save()
    {
        Directory.CreateDirectory(_backupDir);

        using var fs = File.Create(_backupFile);
        using var writer = new BinaryWriter(fs);

        writer.Write(_backups.Count);

        foreach (var (path, data) in _backups)
        {
            var pathBytes = Encoding.UTF8.GetBytes(path);
            writer.Write(pathBytes.Length);
            writer.Write(pathBytes);
            writer.Write(data.Length);
            writer.Write(data);
        }
    }

    public void Load()
    {
        _backups.Clear();

        if (!File.Exists(_backupFile))
            return;

        using var fs = File.OpenRead(_backupFile);
        using var reader = new BinaryReader(fs);

        var count = reader.ReadInt32();

        for (var i = 0; i < count; i++)
        {
            var pathLen = reader.ReadInt32();
            var pathBytes = reader.ReadBytes(pathLen);
            var path = Encoding.UTF8.GetString(pathBytes);

            var dataLen = reader.ReadInt32();
            var data = reader.ReadBytes(dataLen);

            _backups[path] = data;
        }
    }
}

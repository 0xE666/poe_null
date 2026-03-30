namespace PoeNullEffects.Core.Models;

public class BackupEntry
{
    public required string Path { get; init; }
    public required byte[] OriginalData { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

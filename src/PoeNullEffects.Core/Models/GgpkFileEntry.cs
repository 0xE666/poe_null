namespace PoeNullEffects.Core.Models;

public class GgpkFileEntry
{
    public required string Path { get; init; }
    public long Size { get; init; }
    public bool IsParticle => Path.EndsWith(".pet", StringComparison.OrdinalIgnoreCase);
    public int EmitterCount { get; set; }
    public bool IsNulled { get; set; }
}

namespace PoeNullEffects.Core;

public class ShadowManager
{
    private static readonly string[] ShadowPathPatterns =
    [
        "Metadata/Terrain/Shadows",
        "Shaders/Shadow",
        "shadow_cast",
        "shadow_receive"
    ];

    private readonly IGgpkService _ggpk;
    private readonly BackupManager _backup;

    public ShadowManager(IGgpkService ggpk, BackupManager backup)
    {
        _ggpk = ggpk;
        _backup = backup;
    }

    public int Disable(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        var files = FindShadowFiles();
        var modified = 0;

        for (var i = 0; i < files.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var file = files[i];

            try
            {
                _backup.BackupFile(file.Path, _ggpk.ReadFile(file.Path));
                _ggpk.WriteFile(file.Path, ParticleNuller.NullPayload);
                modified++;
            }
            catch { }

            progress?.Report((i + 1) * 100 / files.Count);
        }

        return modified;
    }

    public int Enable(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        var files = FindShadowFiles();
        var restored = 0;

        for (var i = 0; i < files.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var file = files[i];

            var original = _backup.GetBackup(file.Path);
            if (original != null)
            {
                try
                {
                    _ggpk.WriteFile(file.Path, original);
                    restored++;
                }
                catch { }
            }

            progress?.Report((i + 1) * 100 / files.Count);
        }

        return restored;
    }

    private List<Models.GgpkFileEntry> FindShadowFiles()
    {
        return _ggpk.GetAllFiles()
            .Where(f => ShadowPathPatterns.Any(p =>
                f.Path.Contains(p, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }
}

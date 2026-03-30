namespace PoeNullEffects.Core;

public class CorpseManager
{
    private static readonly string[] CorpsePathPatterns =
    [
        "corpse",
        "death_effect",
        "ragdoll",
        "gore",
        "body_parts"
    ];

    private readonly IGgpkService _ggpk;
    private readonly BackupManager _backup;

    public CorpseManager(IGgpkService ggpk, BackupManager backup)
    {
        _ggpk = ggpk;
        _backup = backup;
    }

    public int Disable(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        var files = FindCorpseFiles();
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
        var files = FindCorpseFiles();
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

    private List<Models.GgpkFileEntry> FindCorpseFiles()
    {
        return _ggpk.GetAllFiles()
            .Where(f => CorpsePathPatterns.Any(p =>
                f.Path.Contains(p, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }
}

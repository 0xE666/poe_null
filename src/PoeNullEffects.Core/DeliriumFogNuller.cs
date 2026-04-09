namespace PoeNullEffects.Core;

/// <summary>
/// Nulls delirium fog effect files in the GGPK.
/// Delirium fog lives under Metadata/Effects/Environment/League_Affliction
/// and the Nightmare shader/material paths — NOT in .pet particle files.
///
/// Strategy: only null .aoc files (environment configs that trigger the fog).
/// This prevents the engine from loading the .epk/.mat/.fxgraph at all,
/// avoiding crashes from invalid payloads in those formats.
/// </summary>
public class DeliriumFogNuller
{
    private readonly IGgpkService _ggpk;

    /// <summary>
    /// Null payload for .aoc files — empty UTF-16LE definition the engine accepts.
    /// Same payload used by ParticleNuller for .pet files.
    /// </summary>
    private static readonly byte[] AocNullPayload = [0xFF, 0xFE, 0x30, 0x00, 0x0D, 0x00, 0x0A, 0x00];

    /// <summary>
    /// Empty payload for .epk files — just a UTF-16LE BOM + empty line.
    /// EPK parser chokes on "0" token, but accepts an empty file with just BOM.
    /// </summary>
    private static readonly byte[] EpkNullPayload = [0xFF, 0xFE, 0x0D, 0x00, 0x0A, 0x00];

    /// <summary>
    /// The screen-space fog overlay shaders + the world-space fog shaders.
    /// We replace them with the game's own "invisible.fxgraph" shader
    /// so the render pass executes but outputs nothing visible.
    /// </summary>
    private static readonly string[] FogFiles =
    [
        "metadata/effects/environment/league_affliction/afflictionfog.fxgraph",
        "metadata/effects/environment/league_affliction/afflictionscreenspacefog.fxgraph",
        "metadata/effects/environment/league_azmeri/afflictionfog.fxgraph",
        "metadata/effects/environment/league_azmeri/afflictionscreenspacefog.fxgraph",
    ];

    /// <summary>
    /// A known-good "invisible" shader graph from the game itself.
    /// Used as a replacement so the render pass runs but outputs nothing.
    /// </summary>
    private const string InvisibleShaderPath =
        "metadata/effects/graphs/leagues/affliction/basematerial/invisible.fxgraph";

    public DeliriumFogNuller(IGgpkService ggpk)
    {
        _ggpk = ggpk;
    }

    public List<string> FindFogFiles()
    {
        var allFiles = _ggpk.GetAllFiles();
        var fogFileSet = new HashSet<string>(FogFiles, StringComparer.OrdinalIgnoreCase);
        var found = new List<string>();

        foreach (var file in allFiles)
        {
            if (fogFileSet.Contains(file.Path))
                found.Add(file.Path);
        }

        return found;
    }

    /// <summary>
    /// Debug: find ALL .aoc files containing "affliction" or "delirium" in path.
    /// Used to discover which files actually control the fog.
    /// </summary>
    public List<string> FindAllAfflictionFiles()
    {
        var allFiles = _ggpk.GetAllFiles();
        var results = new List<string>();

        foreach (var file in allFiles)
        {
            if (file.Path.Contains("affliction", StringComparison.OrdinalIgnoreCase) ||
                file.Path.Contains("delirium", StringComparison.OrdinalIgnoreCase) ||
                file.Path.Contains("Nightmare", StringComparison.OrdinalIgnoreCase))
            {
                // show .aoc, .fxgraph, and key shader files
                if (file.Path.EndsWith(".aoc", StringComparison.OrdinalIgnoreCase) ||
                    file.Path.EndsWith(".fxgraph", StringComparison.OrdinalIgnoreCase) ||
                    file.Path.EndsWith(".hlsl", StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(file.Path);
                }
            }
        }

        return results;
    }

    public (int nulled, int total) Execute(BackupManager backupManager, IProgress<int>? progress = null)
    {
        var fogFiles = FindFogFiles();
        if (fogFiles.Count == 0) return (0, 0);

        // Read the game's own "invisible" shader to use as replacement
        byte[] invisiblePayload;
        try
        {
            invisiblePayload = _ggpk.ReadFile(InvisibleShaderPath);
        }
        catch
        {
            // Fallback: can't read invisible shader
            return (0, fogFiles.Count);
        }

        var nulled = 0;

        for (var i = 0; i < fogFiles.Count; i++)
        {
            var path = fogFiles[i];
            try
            {
                if (!backupManager.HasBackup(path))
                    backupManager.BackupFile(path, _ggpk.ReadFile(path));

                // Replace fog shader with the invisible shader
                _ggpk.WriteFile(path, invisiblePayload);
                nulled++;
            }
            catch { }

            progress?.Report((i + 1) * 100 / fogFiles.Count);
        }

        if (nulled > 0)
            _ggpk.SaveIndex();

        return (nulled, fogFiles.Count);
    }
}

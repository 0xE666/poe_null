namespace PoeNullEffects.Core;

public class MakeGoodProcessor
{
    /// <summary>
    /// All effect categories that can be preserved, ordered by visual importance.
    /// Level N preserves the first N categories from this list.
    /// </summary>
    public static readonly IReadOnlyList<EffectCategory> AllCategories =
    [
        new("Player Skills", ["skill/", "player_effect", "projectile/"]),
        new("Auras & Buffs", ["aura/", "buff/", "herald/", "arctic_armour", "tempest_shield"]),
        new("Item Effects", ["item/", "weapon_effect", "armour_effect"]),
        new("Monster Skills", ["monster/", "boss/"]),
        new("Environment", ["environment/", "ambient/", "weather/"]),
        new("Ground Effects", ["ground_effect", "fire_ground", "cold_ground", "lightning_ground"]),
        new("Misc Particles", ["misc/", "ui/", "generic/"]),
    ];

    private readonly IGgpkService _ggpk;
    private readonly BackupManager _backup;

    public MakeGoodProcessor(IGgpkService ggpk, BackupManager backup)
    {
        _ggpk = ggpk;
        _backup = backup;
    }

    /// <summary>
    /// Returns the categories to PRESERVE at the given quality level.
    /// Level 0 = preserve nothing (max performance).
    /// Level 6 = preserve everything (near-vanilla).
    /// </summary>
    public static IReadOnlyList<EffectCategory> GetPreservedCategoriesForLevel(int level)
    {
        level = Math.Clamp(level, 0, 6);

        if (level == 0)
            return [];

        // Map levels 1-6 evenly across categories
        var count = (int)Math.Ceiling((double)level / 6 * AllCategories.Count);
        return AllCategories.Take(count).ToList();
    }

    /// <summary>
    /// Apply the Make Good level. Nulls particles not in preserved categories,
    /// restores particles that are in preserved categories.
    /// </summary>
    public int Apply(int level, IProgress<int>? progress = null, CancellationToken ct = default)
    {
        var preserved = GetPreservedCategoriesForLevel(level);
        var particles = _ggpk.GetParticleFiles();
        var modified = 0;

        for (var i = 0; i < particles.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var particle = particles[i];

            var shouldPreserve = preserved.Any(cat =>
                cat.PathPatterns.Any(p =>
                    particle.Path.Contains(p, StringComparison.OrdinalIgnoreCase)));

            if (shouldPreserve)
            {
                // Restore from backup if available
                var original = _backup.GetBackup(particle.Path);
                if (original != null)
                {
                    _ggpk.WriteFile(particle.Path, original);
                    modified++;
                }
            }
            else
            {
                // Backup and null
                try
                {
                    if (!_backup.HasBackup(particle.Path))
                        _backup.BackupFile(particle.Path, _ggpk.ReadFile(particle.Path));
                    _ggpk.WriteFile(particle.Path, ParticleNuller.NullPayload);
                    modified++;
                }
                catch { /* skip files that can't be read/written */ }
            }

            progress?.Report((i + 1) * 100 / particles.Count);
        }

        return modified;
    }
}

public record EffectCategory(string Name, string[] PathPatterns);

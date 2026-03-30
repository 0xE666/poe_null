using System.Text;
using PoeNullEffects.Core.Models;

namespace PoeNullEffects.Core;

public class ParticleNuller
{
    /// <summary>
    /// The null particle payload: UTF-16LE BOM + "0\r\n"
    /// This is an empty particle definition the game engine accepts.
    /// </summary>
    public static readonly byte[] NullPayload = [0xFF, 0xFE, 0x30, 0x00, 0x0D, 0x00, 0x0A, 0x00];

    private readonly IGgpkService _ggpk;

    public ParticleNuller(IGgpkService ggpk)
    {
        _ggpk = ggpk;
    }

    /// <summary>
    /// Execute particle nulling. Returns the number of files modified.
    /// </summary>
    public int Execute(NullMode mode, FilterList? filterList, bool keepEmitters,
        IProgress<int>? progress = null, CancellationToken ct = default)
    {
        var particles = _ggpk.GetParticleFiles();
        var modified = 0;

        for (var i = 0; i < particles.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var particle = particles[i];
            var shouldNull = mode switch
            {
                NullMode.All => true,
                NullMode.AllExcept => filterList == null || !filterList.Matches(particle.Path),
                NullMode.Only => filterList != null && filterList.Matches(particle.Path),
                _ => false
            };

            if (!shouldNull)
                continue;

            // keepEmitters only works for raw .pet files, not bundles
            var isBundle = particle.Path.EndsWith(".bundle.bin", StringComparison.OrdinalIgnoreCase);
            var payload = (keepEmitters && !isBundle)
                ? BuildKeepEmittersPayload(particle.Path)
                : NullPayload;

            try
            {
                _ggpk.WriteFile(particle.Path, payload);
                modified++;
            }
            catch { /* skip files that can't be written */ }

            progress?.Report((i + 1) * 100 / particles.Count);
        }

        if (modified > 0)
            _ggpk.SaveIndex();

        return modified;
    }

    /// <summary>
    /// Reads the particle file, strips all but one emitter, returns the modified data.
    /// Falls back to NullPayload if parsing fails.
    /// </summary>
    private byte[] BuildKeepEmittersPayload(string path)
    {
        try
        {
            var data = _ggpk.ReadFile(path);
            var text = Encoding.Unicode.GetString(data);
            var lines = text.Split('\n');

            var result = new List<string>();
            var emitterCount = 0;

            foreach (var line in lines)
            {
                var trimmed = line.Trim();

                if (trimmed.StartsWith("emitter", StringComparison.OrdinalIgnoreCase))
                {
                    emitterCount++;
                    if (emitterCount == 1)
                        result.Add(line);
                    continue;
                }

                if (emitterCount <= 1)
                    result.Add(line);
            }

            var resultText = string.Join("\n", result);
            return Encoding.Unicode.GetPreamble()
                .Concat(Encoding.Unicode.GetBytes(resultText))
                .ToArray();
        }
        catch
        {
            return NullPayload;
        }
    }
}

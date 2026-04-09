namespace PoeNullEffects.Core.Memory;

public class FogPatcher : IDisposable
{
    private ProcessMemory? _pm;
    private readonly List<PatchSite> _patches = [];
    private bool _patched;

    public bool IsAttached => _pm != null;
    public bool IsPatched => _patched;
    public int PatchCount => _patches.Count;

    /// <summary>
    /// Known byte signatures for delirium fog rendering instructions.
    /// Each entry: (pattern, nopLength, description).
    /// null bytes = wildcard.
    /// </summary>
    private static readonly (byte?[] Pattern, int NopLen, string Desc)[] FogSignatures =
    [
        // movss [reg+offset], xmm - fog opacity/density write (delirium overlay)
        // F3 0F 11 ?? ?? ?? ?? ?? followed by delirium-specific context
        // Pattern: minss xmm1, [rip+??] — fog intensity clamp (similar to zoom clamp)
        (
            [0xF3, 0x0F, 0x5D, null, null, null, null, null,   // minss xmmN, [rip+offset]
             0xF3, 0x0F, 0x11],                                  // movss (store result)
            8, "fog intensity clamp (minss)"
        ),

        // Delirium fog alpha blend — typical pattern:
        // mulss xmm, [rip+offset] for fog density multiplier
        (
            [0xF3, 0x0F, 0x59, null, null, null, null, null,   // mulss xmmN, [rip+offset]
             0xF3, 0x0F, 0x11, null, null, null, null, null,   // movss [reg+offset], xmmN
             0x0F, 0x28],                                        // movaps (fog blend continue)
            16, "fog density multiply + store"
        ),

        // Delirium mist render — conditional fog draw call
        // Common pattern: comiss + ja/jae that gates the fog render pass
        (
            [0x0F, 0x2F, null, null, null, null, null,          // comiss xmmN, [rip+offset]
             0x0F, 0x87],                                        // ja (skip if above — fog threshold)
            0, "fog render threshold check (comiss + ja)"       // special: patch the ja to jmp
        ),
    ];

    public bool Attach()
    {
        _pm = ProcessMemory.AttachPoE();
        return _pm != null;
    }

    /// <summary>
    /// Scan for all known fog signatures and record patch sites.
    /// </summary>
    public int Scan()
    {
        if (_pm == null) return 0;
        _patches.Clear();

        foreach (var (pattern, nopLen, desc) in FogSignatures)
        {
            var addrs = _pm.PatternScanAll(pattern);
            foreach (var addr in addrs)
            {
                // Read the original bytes before we touch anything
                var patchLen = nopLen > 0 ? nopLen : pattern.Length;
                var original = _pm.ReadBytes(addr, patchLen);
                if (original == null) continue;

                _patches.Add(new PatchSite
                {
                    Address = addr,
                    OriginalBytes = original,
                    PatchLength = patchLen,
                    Description = desc,
                    IsConditionalJump = nopLen == 0
                });
            }
        }

        return _patches.Count;
    }

    /// <summary>
    /// Apply NOP patches to all found fog sites.
    /// </summary>
    public int Patch()
    {
        if (_pm == null || _patches.Count == 0) return 0;

        var count = 0;
        foreach (var site in _patches)
        {
            byte[] patch;
            if (site.IsConditionalJump)
            {
                // For conditional jumps, change ja/jae to jmp (always skip fog render)
                // 0F 87 xx xx xx xx -> 90 E9 xx xx xx xx (nop + jmp)
                patch = new byte[site.PatchLength];
                Array.Copy(site.OriginalBytes, patch, site.PatchLength);
                // Find the conditional jump opcode (0F 87 = ja, 0F 83 = jae)
                for (var i = 0; i < patch.Length - 1; i++)
                {
                    if (patch[i] == 0x0F && (patch[i + 1] == 0x87 || patch[i + 1] == 0x83))
                    {
                        patch[i] = 0x90;     // NOP the 0F prefix
                        patch[i + 1] = 0xE9; // jmp rel32 (unconditional)
                        break;
                    }
                }
            }
            else
            {
                // NOP the instruction
                patch = new byte[site.PatchLength];
                Array.Fill(patch, (byte)0x90);
            }

            if (_pm.WriteBytes(site.Address, patch))
                count++;
        }

        if (count > 0) _patched = true;
        return count;
    }

    /// <summary>
    /// Restore original bytes.
    /// </summary>
    public int Restore()
    {
        if (_pm == null || _patches.Count == 0) return 0;

        var count = 0;
        foreach (var site in _patches)
        {
            if (_pm.WriteBytes(site.Address, site.OriginalBytes))
                count++;
        }

        if (count > 0) _patched = false;
        return count;
    }

    public IReadOnlyList<PatchSite> GetPatchSites() => _patches;

    public void Dispose()
    {
        _pm?.Dispose();
        _pm = null;
        GC.SuppressFinalize(this);
    }

    public class PatchSite
    {
        public nint Address { get; init; }
        public byte[] OriginalBytes { get; init; } = [];
        public int PatchLength { get; init; }
        public string Description { get; init; } = "";
        public bool IsConditionalJump { get; init; }
    }
}

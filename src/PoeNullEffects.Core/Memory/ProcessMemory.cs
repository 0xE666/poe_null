using System.Diagnostics;
using static PoeNullEffects.Core.Memory.NativeMethods;

namespace PoeNullEffects.Core.Memory;

public class ProcessMemory : IDisposable
{
    private nint _handle;
    private readonly Process _process;

    public int ProcessId => _process.Id;
    public string ProcessName => _process.ProcessName;

    private ProcessMemory(Process process, nint handle)
    {
        _process = process;
        _handle = handle;
    }

    public static ProcessMemory? Attach(string processName)
    {
        var procs = Process.GetProcessesByName(processName);
        if (procs.Length == 0) return null;

        var proc = procs[0];
        var access = PROCESS_VM_READ | PROCESS_VM_WRITE | PROCESS_VM_OPERATION | PROCESS_QUERY_INFORMATION;
        var handle = OpenProcess(access, false, proc.Id);
        if (handle == 0) return null;

        return new ProcessMemory(proc, handle);
    }

    public static ProcessMemory? AttachPoE()
    {
        string[] names = ["PathOfExileSteam", "PathOfExile_x64Steam", "PathOfExile", "PathOfExile_x64"];
        foreach (var name in names)
        {
            var pm = Attach(name);
            if (pm != null) return pm;
        }
        return null;
    }

    public byte[]? ReadBytes(nint address, int size)
    {
        var buffer = new byte[size];
        return ReadProcessMemory(_handle, address, buffer, size, out _) ? buffer : null;
    }

    public bool WriteBytes(nint address, byte[] data)
    {
        // Ensure the region is writable
        VirtualProtectEx(_handle, address, data.Length, PAGE_EXECUTE_READWRITE, out var oldProtect);
        var result = WriteProcessMemory(_handle, address, data, data.Length, out _);
        VirtualProtectEx(_handle, address, data.Length, oldProtect, out _);
        return result;
    }

    /// <summary>
    /// Scan executable memory regions of the main module for a byte pattern.
    /// Supports wildcard bytes (null in the pattern array).
    /// </summary>
    public nint PatternScan(byte?[] pattern)
    {
        var module = _process.MainModule;
        if (module == null) return 0;

        var baseAddr = module.BaseAddress;
        var size = module.ModuleMemorySize;
        return PatternScanRegion(baseAddr, size, pattern);
    }

    /// <summary>
    /// Scan all executable memory regions for a byte pattern.
    /// </summary>
    public List<nint> PatternScanAll(byte?[] pattern)
    {
        var results = new List<nint>();
        var module = _process.MainModule;
        if (module == null) return results;

        var baseAddr = module.BaseAddress;
        var size = module.ModuleMemorySize;

        // Read in chunks
        const int chunkSize = 4 * 1024 * 1024; // 4MB
        for (var offset = 0; offset < size; offset += chunkSize - pattern.Length)
        {
            var readSize = Math.Min(chunkSize, size - offset);
            var buffer = ReadBytes(baseAddr + offset, readSize);
            if (buffer == null) continue;

            var pos = 0;
            while (pos < buffer.Length - pattern.Length)
            {
                var idx = FindPattern(buffer, pattern, pos);
                if (idx < 0) break;
                results.Add(baseAddr + offset + idx);
                pos = idx + 1;
            }
        }

        return results;
    }

    private nint PatternScanRegion(nint baseAddr, int size, byte?[] pattern)
    {
        const int chunkSize = 4 * 1024 * 1024; // 4MB
        for (var offset = 0; offset < size; offset += chunkSize - pattern.Length)
        {
            var readSize = Math.Min(chunkSize, size - offset);
            var buffer = ReadBytes(baseAddr + offset, readSize);
            if (buffer == null) continue;

            var idx = FindPattern(buffer, pattern, 0);
            if (idx >= 0)
                return baseAddr + offset + idx;
        }
        return 0;
    }

    private static int FindPattern(byte[] data, byte?[] pattern, int start)
    {
        var limit = data.Length - pattern.Length;
        for (var i = start; i <= limit; i++)
        {
            var match = true;
            for (var j = 0; j < pattern.Length; j++)
            {
                if (pattern[j].HasValue && data[i + j] != pattern[j].Value)
                {
                    match = false;
                    break;
                }
            }
            if (match) return i;
        }
        return -1;
    }

    public void Dispose()
    {
        if (_handle != 0)
        {
            CloseHandle(_handle);
            _handle = 0;
        }
        GC.SuppressFinalize(this);
    }
}

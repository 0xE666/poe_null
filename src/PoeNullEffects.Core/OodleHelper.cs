using System.Reflection;
using System.Runtime.InteropServices;

namespace PoeNullEffects.Core;

public static class OodleHelper
{
    private static bool _initialized;
    private static IntPtr _wrapperHandle;
    public static string? LoadResult { get; private set; }

    [DllImport("oo2core", CallingConvention = CallingConvention.Cdecl, EntryPoint = "OodleWrapper_SetContext")]
    private static extern unsafe void NativeSetContext(byte* fileData, long fileSize,
        int dataOffset, int chunkCount, int* chunkSizes);

    [DllImport("oo2core", CallingConvention = CallingConvention.Cdecl, EntryPoint = "OodleWrapper_ClearContext")]
    private static extern void NativeClearContext();

    public static bool IsWrapper { get; private set; }

    public static unsafe void SetBundleContext(byte[] fileData, GCHandle pinnedHandle)
    {
        if (!IsWrapper) return;

        if (fileData.Length < 64) return;

        int headSize = BitConverter.ToInt32(fileData, 8);
        int chunkCount = BitConverter.ToInt32(fileData, 36);
        int dataOffset = 12 + headSize;

        if (fileData.Length < 60 + chunkCount * 4) return;

        try
        {
            byte* ptr = (byte*)pinnedHandle.AddrOfPinnedObject();
            int* chunkSizesPtr = (int*)(ptr + 60);
            NativeSetContext(ptr, fileData.Length, dataOffset, chunkCount, chunkSizesPtr);
        }
        catch
        {
            IsWrapper = false;
        }
    }

    public static void ClearBundleContext()
    {
        if (!IsWrapper) return;
        try { NativeClearContext(); } catch { }
    }

    public static void Initialize(string? gameDirectory = null)
    {
        if (_initialized) return;
        _initialized = true;

        // Extract embedded Oodle DLLs to a temp directory
        var extractDir = ExtractEmbeddedDlls();

        var candidates = new List<string>();

        // Extracted DLLs first (embedded in the exe)
        if (extractDir != null)
            candidates.Add(extractDir);

        var exePath = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(exePath))
            candidates.Add(Path.GetDirectoryName(exePath)!);

        candidates.Add(AppDomain.CurrentDomain.BaseDirectory);

        if (!string.IsNullOrEmpty(gameDirectory))
            candidates.Add(gameDirectory);

        candidates.Add(@"C:\Program Files (x86)\Grinding Gear Games\Path of Exile");
        candidates.Add(@"C:\Program Files\Grinding Gear Games\Path of Exile 2");

        string[] dllNames = ["oo2core.dll", "oo2core_8_win64.dll", "oo2core_9_win64.dll",
                             "oo2core_7_win64.dll", "oodle-data-shared.dll"];

        foreach (var dir in candidates)
        {
            if (!Directory.Exists(dir)) continue;

            foreach (var dll in dllNames)
            {
                var fullPath = Path.Combine(dir, dll);
                if (File.Exists(fullPath))
                {
                    if (NativeLibrary.TryLoad(fullPath, out var handle))
                    {
                        _wrapperHandle = handle;
                        IsWrapper = NativeLibrary.TryGetExport(handle, "OodleWrapper_SetContext", out _);
                        LoadResult = $"Loaded: {fullPath}" + (IsWrapper ? " (wrapper)" : " (native)");
                        SetupResolver(fullPath);
                        return;
                    }
                }
            }

            try
            {
                foreach (var file in Directory.GetFiles(dir, "oo2core*.dll"))
                {
                    if (NativeLibrary.TryLoad(file, out var handle))
                    {
                        _wrapperHandle = handle;
                        IsWrapper = NativeLibrary.TryGetExport(handle, "OodleWrapper_SetContext", out _);
                        LoadResult = $"Loaded: {file}" + (IsWrapper ? " (wrapper)" : " (native)");
                        SetupResolver(file);
                        return;
                    }
                }
            }
            catch { }
        }

        LoadResult = "NOT FOUND. Searched: " + string.Join(", ", candidates.Where(Directory.Exists));
    }

    /// <summary>
    /// Extract embedded Oodle DLLs from the assembly's resources to a temp directory.
    /// Returns the directory path, or null if no embedded DLLs found.
    /// </summary>
    private static string? ExtractEmbeddedDlls()
    {
        var entryAssembly = Assembly.GetEntryAssembly();
        if (entryAssembly == null) return null;

        var resourceNames = entryAssembly.GetManifestResourceNames();
        var oodleDlls = new[] { "oo2core.dll", "oodle-data-shared.dll" };

        var found = oodleDlls.Where(name => resourceNames.Contains(name)).ToList();
        if (found.Count == 0) return null;

        // Extract to a stable temp directory (not random per run)
        var extractDir = Path.Combine(Path.GetTempPath(), "PoeNullEffects_oodle");
        Directory.CreateDirectory(extractDir);

        foreach (var dllName in found)
        {
            var targetPath = Path.Combine(extractDir, dllName);

            // Only extract if not already there or size differs
            using var stream = entryAssembly.GetManifestResourceStream(dllName);
            if (stream == null) continue;

            if (File.Exists(targetPath) && new FileInfo(targetPath).Length == stream.Length)
                continue;

            using var fs = File.Create(targetPath);
            stream.CopyTo(fs);
        }

        return extractDir;
    }

    private static void SetupResolver(string dllPath)
    {
        try
        {
            var libBundle3 = Assembly.Load("LibBundle3");
            NativeLibrary.SetDllImportResolver(libBundle3, (name, assembly, searchPath) =>
            {
                if (name.StartsWith("oo2core", StringComparison.OrdinalIgnoreCase))
                {
                    if (NativeLibrary.TryLoad(dllPath, out var handle))
                        return handle;
                }
                return IntPtr.Zero;
            });
        }
        catch { }
    }
}

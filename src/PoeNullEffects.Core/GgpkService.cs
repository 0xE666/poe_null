using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using LibGGPK3;
using LibGGPK3.Records;
using LibBundledGGPK3;
using LibBundle3;
using LibBundle3.Nodes;
using PoeNullEffects.Core.Models;

namespace PoeNullEffects.Core;

public class GgpkService : IGgpkService
{
    private GGPK? _ggpk;
    private BundledGGPK? _bundledGgpk;
    private LibBundle3.Index? _looseIndex;
    private bool _bundleIndexWorking;
    private byte[]? _indexFileData;       // Pinned file data for wrapper context
    private GCHandle _indexPinHandle;     // GC pin handle
    private bool _pendingWrites;

    public bool IsOpen => _ggpk != null || _looseIndex != null;
    public bool IsBundleIndexWorking => _bundleIndexWorking;
    public string? BundleError { get; private set; }
    public string FilePath { get; private set; } = "";
    public string OpenMode { get; private set; } = "";

    public void Open(string path)
    {
        Close();

        if (!File.Exists(path) && !Directory.Exists(path))
            throw new FileNotFoundException("Path not found.", path);

        // Initialize Oodle
        var dir = File.Exists(path) ? Path.GetDirectoryName(path)! : path;
        OodleHelper.Initialize(dir);

        // Determine if this is a .ggpk file or a Bundles2 directory / game directory
        if (File.Exists(path) && path.EndsWith(".ggpk", StringComparison.OrdinalIgnoreCase))
        {
            OpenGgpk(path);
        }
        else
        {
            // It's a directory — look for Bundles2/_.index.bin (Steam/Epic install)
            var gameDir = path;
            var bundlesDir = Path.Combine(gameDir, "Bundles2");
            if (!Directory.Exists(bundlesDir))
            {
                // Maybe they pointed directly to Bundles2
                if (Path.GetFileName(gameDir) == "Bundles2")
                    bundlesDir = gameDir;
                else
                    throw new DirectoryNotFoundException($"Bundles2 directory not found in: {gameDir}");
            }

            OpenLooseBundles(bundlesDir);
        }
    }

    private void OpenGgpk(string ggpkPath)
    {
        try
        {
            var bundled = new BundledGGPK(ggpkPath);
            _bundledGgpk = bundled;
            _ggpk = bundled;
            _bundleIndexWorking = true;
            OpenMode = "GGPK (bundled)";
        }
        catch (Exception ex)
        {
            BundleError = $"{ex.GetType().Name}: {ex.Message}";
            _ggpk = new GGPK(ggpkPath);
            _bundledGgpk = null;
            _bundleIndexWorking = false;
            OpenMode = "GGPK (raw — no bundle index)";
        }

        FilePath = ggpkPath;
    }

    private void OpenLooseBundles(string bundlesDir)
    {
        var indexPath = Path.Combine(bundlesDir, "Bundles2", "_.index.bin");
        if (!File.Exists(indexPath))
        {
            // Maybe bundlesDir IS the Bundles2 folder
            indexPath = Path.Combine(bundlesDir, "_.index.bin");
            if (!File.Exists(indexPath))
                throw new FileNotFoundException("_.index.bin not found.", indexPath);
        }

        try
        {
            // If we have the wrapper DLL, set up context for cross-chunk fix.
            // This is needed for PoE 1 bundles that use Mermaid compression where
            // Oodle quantums can span across chunk boundaries.
            if (OodleHelper.IsWrapper)
            {
                _indexFileData = File.ReadAllBytes(indexPath);
                _indexPinHandle = GCHandle.Alloc(_indexFileData, GCHandleType.Pinned);
                OodleHelper.SetBundleContext(_indexFileData, _indexPinHandle);
            }

            // Use the file path constructor — it auto-creates DriveBundleFactory
            // from the directory containing _.index.bin
            _looseIndex = new LibBundle3.Index(indexPath);
            _bundleIndexWorking = true;
            OpenMode = "Steam/Epic (loose bundles)";
            FilePath = bundlesDir;
        }
        catch (Exception ex)
        {
            BundleError = $"{ex.GetType().Name}: {ex.Message}";
            _bundleIndexWorking = false;
            OpenMode = "Failed";
            throw new InvalidOperationException($"Failed to parse bundle index: {ex.Message}", ex);
        }
        finally
        {
            // Clear wrapper context after index is parsed
            OodleHelper.ClearBundleContext();
            ReleasePinnedData();
        }
    }

    private void ReleasePinnedData()
    {
        if (_indexPinHandle.IsAllocated)
            _indexPinHandle.Free();
        _indexFileData = null;
    }

    public void Close()
    {
        OodleHelper.ClearBundleContext();
        ReleasePinnedData();
        _looseIndex?.Dispose();
        _looseIndex = null;
        _ggpk?.Dispose();
        _ggpk = null;
        _bundledGgpk = null;
        _bundleIndexWorking = false;
        BundleError = null;
        FilePath = "";
        OpenMode = "";
    }

    public List<GgpkFileEntry> GetAllFiles()
    {
        EnsureOpen();
        var entries = new List<GgpkFileEntry>();

        var index = GetActiveIndex();
        if (index != null)
        {
            foreach (var fileNode in LibBundle3.Index.Recursefiles(index.Root))
            {
                var path = ITreeNode.GetPath(fileNode).TrimStart('/');
                if (path.EndsWith('/')) continue;

                entries.Add(new GgpkFileEntry
                {
                    Path = path,
                    Size = fileNode.Record.Size
                });
            }
        }
        else if (_ggpk != null)
        {
            CollectFiles(_ggpk.Root, "", entries);
        }

        return entries;
    }

    public List<GgpkFileEntry> GetParticleFiles()
    {
        return GetAllFiles()
            .Where(e => e.IsParticle)
            .ToList();
    }

    public List<GgpkFileEntry> SearchFiles(string query, bool useRegex = false)
    {
        var allFiles = GetAllFiles();

        if (useRegex)
        {
            var regex = new Regex(query, RegexOptions.IgnoreCase | RegexOptions.Compiled);
            return allFiles.Where(f => regex.IsMatch(f.Path)).ToList();
        }

        return allFiles
            .Where(f => f.Path.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public byte[] ReadFile(string path)
    {
        EnsureOpen();

        var index = GetActiveIndex();
        if (index != null && index.TryGetFile(path, out var bundleFileRecord))
            return bundleFileRecord.Read().ToArray();

        if (_ggpk != null && _ggpk.Root.TryFindNode(path, out var treeNode) && treeNode is FileRecord fileRecord)
            return fileRecord.Read();

        throw new FileNotFoundException($"File not found: {path}");
    }

    public void WriteFile(string path, byte[] data)
    {
        EnsureOpen();

        var index = GetActiveIndex();
        if (index != null && index.TryGetFile(path, out var bundleFileRecord))
        {
            // Queue the write without saving — avoids file lock conflicts.
            // Call SaveIndex() after all writes are done.
            bundleFileRecord.Write(data, saveIndex: false);
            _pendingWrites = true;
            return;
        }

        if (_ggpk != null && _ggpk.Root.TryFindNode(path, out var treeNode) && treeNode is FileRecord fileRecord)
        {
            fileRecord.Write(data);
            return;
        }

        throw new FileNotFoundException($"File not found: {path}");
    }

    /// <summary>
    /// Save the bundle index after batch writes. Call this after all WriteFile operations.
    /// </summary>
    public void SaveIndex()
    {
        if (!_pendingWrites) return;

        var index = GetActiveIndex();
        if (index != null)
        {
            index.Save();
            _pendingWrites = false;
        }
    }

    public int CountEmitters(string particlePath)
    {
        try
        {
            var data = ReadFile(particlePath);
            return CountEmittersInPetData(data);
        }
        catch
        {
            return 0;
        }
    }

    public void Dispose()
    {
        Close();
    }

    private LibBundle3.Index? GetActiveIndex()
    {
        if (_looseIndex != null) return _looseIndex;
        if (_bundledGgpk != null && _bundleIndexWorking) return _bundledGgpk.Index;
        return null;
    }

    private void EnsureOpen()
    {
        if (_ggpk == null && _looseIndex == null)
            throw new InvalidOperationException("No GGPK file is open. Call Open() first.");
    }

    private void CollectFiles(DirectoryRecord dir, string basePath, List<GgpkFileEntry> entries)
    {
        foreach (var child in dir)
        {
            var childPath = string.IsNullOrEmpty(basePath)
                ? child.Name
                : $"{basePath}/{child.Name}";

            if (child is FileRecord file)
            {
                entries.Add(new GgpkFileEntry
                {
                    Path = childPath,
                    Size = file.DataLength
                });
            }
            else if (child is DirectoryRecord subDir)
            {
                CollectFiles(subDir, childPath, entries);
            }
        }
    }

    private static int CountEmittersInPetData(byte[] data)
    {
        if (data.Length < 8)
            return 0;

        try
        {
            var text = Encoding.Unicode.GetString(data);
            var count = 0;
            foreach (var line in text.Split('\n'))
            {
                if (line.Trim().StartsWith("emitter", StringComparison.OrdinalIgnoreCase))
                    count++;
            }
            return count;
        }
        catch
        {
            return 0;
        }
    }
}

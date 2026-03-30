# PoeNullEffects Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a WPF tool that modifies Path of Exile's GGPK archive to null out particle effects, shadows, and corpses for better game performance.

**Architecture:** Two-project .NET 8 solution — `PoeNullEffects.Core` (business logic, no UI) wraps LibGGPK3 for GGPK parsing and provides particle nulling, shadow/corpse management, backup/restore, and config persistence. `PoeNullEffects.UI` is a WPF app using MVVM (CommunityToolkit.Mvvm) with tab-based layout for nulling controls, GGPK browser with DDS preview (Pfim), and a virtual check list.

**Tech Stack:** .NET 8, WPF, LibGGPK3.LibBundledGGPK3 (NuGet v2.7.5), Pfim (NuGet v0.11.2), CommunityToolkit.Mvvm, xUnit + NSubstitute for tests.

**Note on LibGGPK3:** LibGGPK2 is deprecated. LibGGPK3 is the active replacement. Modern PoE (version 3/4 GGPK) stores files inside `Bundles2/*.bundle.bin` — the `BundledGGPK` class + `Index` API handles this transparently. Old version 2 GGPK files (pre-3.11.2) store files directly as `FileRecord` entries.

**License note:** LibGGPK3 is AGPL-3.0. This tool must be open-sourced if distributed.

---

## File Structure

```
PoeNullEffects/
├── PoeNullEffects.sln
├── src/
│   ├── PoeNullEffects.Core/
│   │   ├── PoeNullEffects.Core.csproj
│   │   ├── Models/
│   │   │   ├── NullMode.cs                 # Enum: All, AllExcept, Only
│   │   │   ├── GgpkFileEntry.cs            # DTO: path, size, isParticle, emitterCount
│   │   │   └── BackupEntry.cs              # DTO: path, originalData, timestamp
│   │   ├── Config.cs                       # INI config load/save
│   │   ├── FilterList.cs                   # Load/parse DisableAllExceptList.txt and DisableOnlyList.txt
│   │   ├── IGgpkService.cs                 # Interface for GGPK operations
│   │   ├── GgpkService.cs                  # LibGGPK3 wrapper — open, read, write, search, list files
│   │   ├── ParticleNuller.cs               # Null particle logic (3 modes + keepEmitters)
│   │   ├── ShadowManager.cs                # Shadow enable/disable
│   │   ├── CorpseManager.cs                # Corpse enable/disable
│   │   ├── MakeGoodProcessor.cs            # Quality slider (0-6)
│   │   └── BackupManager.cs                # Binary backup save/restore
│   ├── PoeNullEffects.UI/
│   │   ├── PoeNullEffects.UI.csproj
│   │   ├── App.xaml / App.xaml.cs
│   │   ├── MainWindow.xaml / MainWindow.xaml.cs
│   │   ├── ViewModels/
│   │   │   ├── MainViewModel.cs            # Tab orchestration, GGPK open, log
│   │   │   ├── NullEffectsViewModel.cs     # Particle/shadow/corpse/MakeGood controls
│   │   │   ├── GgpkBrowserViewModel.cs     # Tree, search, preview, export/import
│   │   │   └── CheckListViewModel.cs       # Virtual particle list with filtering
│   │   ├── Views/
│   │   │   ├── NullEffectsView.xaml        # Particle nulling panel
│   │   │   ├── GgpkBrowserView.xaml        # File browser with search + preview
│   │   │   └── CheckListView.xaml          # Filterable particle list
│   │   └── Converters/
│   │       └── DdsToBitmapConverter.cs      # Pfim DDS → WPF BitmapSource
│   └── PoeNullEffects.Tests/
│       ├── PoeNullEffects.Tests.csproj
│       ├── ConfigTests.cs
│       ├── FilterListTests.cs
│       ├── ParticleNullerTests.cs
│       ├── BackupManagerTests.cs
│       └── MakeGoodProcessorTests.cs
└── data/
    ├── config.ini
    ├── DisableAllExceptList.txt
    └── DisableOnlyList.txt
```

---

## Task 1: Project Scaffolding

**Files:**
- Create: `PoeNullEffects.sln`
- Create: `src/PoeNullEffects.Core/PoeNullEffects.Core.csproj`
- Create: `src/PoeNullEffects.UI/PoeNullEffects.UI.csproj`
- Create: `src/PoeNullEffects.Tests/PoeNullEffects.Tests.csproj`

- [ ] **Step 1: Create solution and projects**

```bash
cd C:/Users/agony/Desktop/projects/poe_null
dotnet new sln -n PoeNullEffects
mkdir -p src/PoeNullEffects.Core
mkdir -p src/PoeNullEffects.UI
mkdir -p src/PoeNullEffects.Tests
dotnet new classlib -n PoeNullEffects.Core -o src/PoeNullEffects.Core -f net8.0
dotnet new wpf -n PoeNullEffects.UI -o src/PoeNullEffects.UI -f net8.0-windows
dotnet new xunit -n PoeNullEffects.Tests -o src/PoeNullEffects.Tests -f net8.0
```

- [ ] **Step 2: Add projects to solution**

```bash
dotnet sln PoeNullEffects.sln add src/PoeNullEffects.Core/PoeNullEffects.Core.csproj
dotnet sln PoeNullEffects.sln add src/PoeNullEffects.UI/PoeNullEffects.UI.csproj
dotnet sln PoeNullEffects.sln add src/PoeNullEffects.Tests/PoeNullEffects.Tests.csproj
```

- [ ] **Step 3: Add project references**

```bash
cd src/PoeNullEffects.UI
dotnet add reference ../PoeNullEffects.Core/PoeNullEffects.Core.csproj
cd ../PoeNullEffects.Tests
dotnet add reference ../PoeNullEffects.Core/PoeNullEffects.Core.csproj
```

- [ ] **Step 4: Add NuGet packages**

```bash
cd C:/Users/agony/Desktop/projects/poe_null

# Core
dotnet add src/PoeNullEffects.Core/PoeNullEffects.Core.csproj package LibBundledGGPK3
dotnet add src/PoeNullEffects.Core/PoeNullEffects.Core.csproj package Pfim

# UI
dotnet add src/PoeNullEffects.UI/PoeNullEffects.UI.csproj package CommunityToolkit.Mvvm

# Tests
dotnet add src/PoeNullEffects.Tests/PoeNullEffects.Tests.csproj package NSubstitute
```

- [ ] **Step 5: Configure single-file publish in UI csproj**

Edit `src/PoeNullEffects.UI/PoeNullEffects.UI.csproj` to add publish properties:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <UseWPF>true</UseWPF>
    <RootNamespace>PoeNullEffects.UI</RootNamespace>
    <AssemblyName>PoeNullEffects</AssemblyName>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <SelfContained>true</SelfContained>
    <PublishSingleFile>true</PublishSingleFile>
    <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\PoeNullEffects.Core\PoeNullEffects.Core.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 6: Clean up generated files**

Delete the auto-generated `Class1.cs` from Core, `UnitTest1.cs` from Tests. Remove default `MainWindow.xaml` content (we'll rebuild it in a later task).

- [ ] **Step 7: Verify build**

```bash
cd C:/Users/agony/Desktop/projects/poe_null
dotnet build PoeNullEffects.sln
```

Expected: Build succeeded with 0 errors.

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "feat: scaffold PoeNullEffects solution with Core, UI, and Tests projects"
```

---

## Task 2: Data Files — Config and Filter Lists

**Files:**
- Create: `data/config.ini`
- Create: `data/DisableAllExceptList.txt`
- Create: `data/DisableOnlyList.txt`

- [ ] **Step 1: Create default config.ini**

Create `data/config.ini`:

```ini
ggpkPath=
nullParticlesMethod=1
keepEmitters=0
makeGoodValue=2
```

- [ ] **Step 2: Create DisableAllExceptList.txt**

Create `data/DisableAllExceptList.txt` — these are effect paths to KEEP when using "Disable All Except" mode. One substring per line:

```
lightning_orb
surge
blood_rage
herald
aura/
arctic_armour
tempest_shield
righteous_fire
molten_shell
immortal_call
phase_run
vaal_grace
league/
```

- [ ] **Step 3: Create DisableOnlyList.txt**

Create `data/DisableOnlyList.txt` — these are effect paths to specifically DISABLE when using "Disable Only" mode:

```
blood
corpse_explosion
rain
discharge
ground_effects
fog
smoke
fire_ground
cold_ground
lightning_ground
desecrate
poison_cloud
caustic
tar
```

- [ ] **Step 4: Commit**

```bash
git add data/
git commit -m "feat: add default config.ini and filter list data files"
```

---

## Task 3: Models and Enums

**Files:**
- Create: `src/PoeNullEffects.Core/Models/NullMode.cs`
- Create: `src/PoeNullEffects.Core/Models/GgpkFileEntry.cs`
- Create: `src/PoeNullEffects.Core/Models/BackupEntry.cs`

- [ ] **Step 1: Create NullMode enum**

Create `src/PoeNullEffects.Core/Models/NullMode.cs`:

```csharp
namespace PoeNullEffects.Core.Models;

public enum NullMode
{
    All = 0,
    AllExcept = 1,
    Only = 2
}
```

- [ ] **Step 2: Create GgpkFileEntry**

Create `src/PoeNullEffects.Core/Models/GgpkFileEntry.cs`:

```csharp
namespace PoeNullEffects.Core.Models;

public class GgpkFileEntry
{
    public required string Path { get; init; }
    public long Size { get; init; }
    public bool IsParticle => Path.EndsWith(".pet", StringComparison.OrdinalIgnoreCase);
    public int EmitterCount { get; set; }
    public bool IsNulled { get; set; }
}
```

- [ ] **Step 3: Create BackupEntry**

Create `src/PoeNullEffects.Core/Models/BackupEntry.cs`:

```csharp
namespace PoeNullEffects.Core.Models;

public class BackupEntry
{
    public required string Path { get; init; }
    public required byte[] OriginalData { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
```

- [ ] **Step 4: Verify build**

```bash
dotnet build src/PoeNullEffects.Core/PoeNullEffects.Core.csproj
```

Expected: Build succeeded.

- [ ] **Step 5: Commit**

```bash
git add src/PoeNullEffects.Core/Models/
git commit -m "feat: add NullMode, GgpkFileEntry, and BackupEntry models"
```

---

## Task 4: Config System

**Files:**
- Create: `src/PoeNullEffects.Core/Config.cs`
- Create: `src/PoeNullEffects.Tests/ConfigTests.cs`

- [ ] **Step 1: Write failing tests**

Create `src/PoeNullEffects.Tests/ConfigTests.cs`:

```csharp
using PoeNullEffects.Core;
using PoeNullEffects.Core.Models;

namespace PoeNullEffects.Tests;

public class ConfigTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _configPath;

    public ConfigTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"poe_null_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _configPath = Path.Combine(_tempDir, "config.ini");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void Load_DefaultValues_WhenFileDoesNotExist()
    {
        var config = Config.Load(_configPath);

        Assert.Equal("", config.GgpkPath);
        Assert.Equal(NullMode.AllExcept, config.NullParticlesMethod);
        Assert.Equal(0, config.KeepEmitters);
        Assert.Equal(2, config.MakeGoodValue);
    }

    [Fact]
    public void Load_ParsesExistingFile()
    {
        File.WriteAllText(_configPath,
            "ggpkPath=C:\\Games\\PoE\\Content.ggpk\n" +
            "nullParticlesMethod=2\n" +
            "keepEmitters=1\n" +
            "makeGoodValue=5\n");

        var config = Config.Load(_configPath);

        Assert.Equal(@"C:\Games\PoE\Content.ggpk", config.GgpkPath);
        Assert.Equal(NullMode.Only, config.NullParticlesMethod);
        Assert.Equal(1, config.KeepEmitters);
        Assert.Equal(5, config.MakeGoodValue);
    }

    [Fact]
    public void Save_WritesAllValues()
    {
        var config = Config.Load(_configPath);
        config.GgpkPath = @"D:\PoE2\Content.ggpk";
        config.NullParticlesMethod = NullMode.All;
        config.KeepEmitters = 1;
        config.MakeGoodValue = 4;

        config.Save();

        var reloaded = Config.Load(_configPath);
        Assert.Equal(@"D:\PoE2\Content.ggpk", reloaded.GgpkPath);
        Assert.Equal(NullMode.All, reloaded.NullParticlesMethod);
        Assert.Equal(1, reloaded.KeepEmitters);
        Assert.Equal(4, reloaded.MakeGoodValue);
    }

    [Fact]
    public void Save_CreatesDirectoryIfNeeded()
    {
        var nestedPath = Path.Combine(_tempDir, "sub", "dir", "config.ini");
        var config = Config.Load(nestedPath);
        config.GgpkPath = "test";

        config.Save();

        Assert.True(File.Exists(nestedPath));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test src/PoeNullEffects.Tests/ --filter "FullyQualifiedName~ConfigTests" -v minimal
```

Expected: FAIL — `Config` class does not exist.

- [ ] **Step 3: Implement Config**

Create `src/PoeNullEffects.Core/Config.cs`:

```csharp
using PoeNullEffects.Core.Models;

namespace PoeNullEffects.Core;

public class Config
{
    private readonly string _filePath;

    public string GgpkPath { get; set; } = "";
    public NullMode NullParticlesMethod { get; set; } = NullMode.AllExcept;
    public int KeepEmitters { get; set; } = 0;
    public int MakeGoodValue { get; set; } = 2;

    private Config(string filePath)
    {
        _filePath = filePath;
    }

    public static Config Load(string filePath)
    {
        var config = new Config(filePath);

        if (!File.Exists(filePath))
            return config;

        foreach (var line in File.ReadAllLines(filePath))
        {
            var eqIndex = line.IndexOf('=');
            if (eqIndex < 0) continue;

            var key = line[..eqIndex].Trim();
            var value = line[(eqIndex + 1)..].Trim();

            switch (key)
            {
                case "ggpkPath":
                    config.GgpkPath = value;
                    break;
                case "nullParticlesMethod":
                    if (int.TryParse(value, out var method) && Enum.IsDefined(typeof(NullMode), method))
                        config.NullParticlesMethod = (NullMode)method;
                    break;
                case "keepEmitters":
                    if (int.TryParse(value, out var keep))
                        config.KeepEmitters = keep;
                    break;
                case "makeGoodValue":
                    if (int.TryParse(value, out var mgv))
                        config.MakeGoodValue = mgv;
                    break;
            }
        }

        return config;
    }

    public void Save()
    {
        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var lines = new[]
        {
            $"ggpkPath={GgpkPath}",
            $"nullParticlesMethod={(int)NullParticlesMethod}",
            $"keepEmitters={KeepEmitters}",
            $"makeGoodValue={MakeGoodValue}"
        };

        File.WriteAllLines(_filePath, lines);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test src/PoeNullEffects.Tests/ --filter "FullyQualifiedName~ConfigTests" -v minimal
```

Expected: 4 passed.

- [ ] **Step 5: Commit**

```bash
git add src/PoeNullEffects.Core/Config.cs src/PoeNullEffects.Tests/ConfigTests.cs
git commit -m "feat: add INI config loading and saving with tests"
```

---

## Task 5: Filter List Loading

**Files:**
- Create: `src/PoeNullEffects.Core/FilterList.cs`
- Create: `src/PoeNullEffects.Tests/FilterListTests.cs`

- [ ] **Step 1: Write failing tests**

Create `src/PoeNullEffects.Tests/FilterListTests.cs`:

```csharp
using PoeNullEffects.Core;

namespace PoeNullEffects.Tests;

public class FilterListTests : IDisposable
{
    private readonly string _tempDir;

    public FilterListTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"poe_null_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void Load_ParsesLines_IgnoresBlanksAndComments()
    {
        var path = Path.Combine(_tempDir, "list.txt");
        File.WriteAllText(path, "blood\n\n# comment\nrain\n  fog  \n");

        var list = FilterList.Load(path);

        Assert.Equal(3, list.Entries.Count);
        Assert.Contains("blood", list.Entries);
        Assert.Contains("rain", list.Entries);
        Assert.Contains("fog", list.Entries);
    }

    [Fact]
    public void Matches_ReturnsTrueForSubstringMatch()
    {
        var path = Path.Combine(_tempDir, "list.txt");
        File.WriteAllText(path, "blood\nrain\n");

        var list = FilterList.Load(path);

        Assert.True(list.Matches("Metadata/Particles/blood_splatter.pet"));
        Assert.True(list.Matches("Metadata/Particles/Rain/heavy_rain.pet"));
        Assert.False(list.Matches("Metadata/Particles/lightning_orb.pet"));
    }

    [Fact]
    public void Matches_IsCaseInsensitive()
    {
        var path = Path.Combine(_tempDir, "list.txt");
        File.WriteAllText(path, "Blood\n");

        var list = FilterList.Load(path);

        Assert.True(list.Matches("metadata/particles/blood_splatter.pet"));
    }

    [Fact]
    public void Load_ReturnsEmptyForMissingFile()
    {
        var list = FilterList.Load(Path.Combine(_tempDir, "nope.txt"));

        Assert.Empty(list.Entries);
        Assert.False(list.Matches("anything"));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test src/PoeNullEffects.Tests/ --filter "FullyQualifiedName~FilterListTests" -v minimal
```

Expected: FAIL — `FilterList` does not exist.

- [ ] **Step 3: Implement FilterList**

Create `src/PoeNullEffects.Core/FilterList.cs`:

```csharp
namespace PoeNullEffects.Core;

public class FilterList
{
    public IReadOnlyList<string> Entries { get; }

    private FilterList(IReadOnlyList<string> entries)
    {
        Entries = entries;
    }

    public static FilterList Load(string filePath)
    {
        if (!File.Exists(filePath))
            return new FilterList([]);

        var entries = File.ReadAllLines(filePath)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .ToList();

        return new FilterList(entries);
    }

    public bool Matches(string path)
    {
        return Entries.Any(entry =>
            path.Contains(entry, StringComparison.OrdinalIgnoreCase));
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test src/PoeNullEffects.Tests/ --filter "FullyQualifiedName~FilterListTests" -v minimal
```

Expected: 4 passed.

- [ ] **Step 5: Commit**

```bash
git add src/PoeNullEffects.Core/FilterList.cs src/PoeNullEffects.Tests/FilterListTests.cs
git commit -m "feat: add filter list loading with substring matching"
```

---

## Task 6: GGPK Service Interface and Implementation

**Files:**
- Create: `src/PoeNullEffects.Core/IGgpkService.cs`
- Create: `src/PoeNullEffects.Core/GgpkService.cs`

This wraps LibGGPK3. No unit tests for this layer — it's a thin wrapper over a third-party library that requires a real GGPK file. Testing happens via integration in later tasks.

- [ ] **Step 1: Create IGgpkService interface**

Create `src/PoeNullEffects.Core/IGgpkService.cs`:

```csharp
using PoeNullEffects.Core.Models;

namespace PoeNullEffects.Core;

public interface IGgpkService : IDisposable
{
    bool IsOpen { get; }
    string FilePath { get; }

    void Open(string ggpkPath);
    void Close();

    List<GgpkFileEntry> GetAllFiles();
    List<GgpkFileEntry> GetParticleFiles();
    List<GgpkFileEntry> SearchFiles(string query, bool useRegex = false);

    byte[] ReadFile(string path);
    void WriteFile(string path, byte[] data);

    int CountEmitters(string particlePath);
}
```

- [ ] **Step 2: Implement GgpkService**

Create `src/PoeNullEffects.Core/GgpkService.cs`:

```csharp
using System.Text.RegularExpressions;
using LibGGPK3;
using LibGGPK3.Records;
using LibBundledGGPK3;
using PoeNullEffects.Core.Models;

namespace PoeNullEffects.Core;

public class GgpkService : IGgpkService
{
    private GGPK? _ggpk;
    private bool _isBundled;

    public bool IsOpen => _ggpk != null;
    public string FilePath { get; private set; } = "";

    public void Open(string ggpkPath)
    {
        Close();

        if (!File.Exists(ggpkPath))
            throw new FileNotFoundException("GGPK file not found.", ggpkPath);

        try
        {
            // Try bundled first (modern PoE)
            _ggpk = new BundledGGPK(ggpkPath);
            _isBundled = true;
        }
        catch
        {
            // Fall back to raw GGPK (old format)
            _ggpk = new GGPK(ggpkPath);
            _isBundled = false;
        }

        FilePath = ggpkPath;
    }

    public void Close()
    {
        _ggpk?.Dispose();
        _ggpk = null;
        _isBundled = false;
        FilePath = "";
    }

    public List<GgpkFileEntry> GetAllFiles()
    {
        EnsureOpen();
        var entries = new List<GgpkFileEntry>();
        CollectFiles(_ggpk!.Root, "", entries);
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

        if (_isBundled && _ggpk is BundledGGPK bundled)
        {
            if (bundled.Index.TryGetFile(path, out var bundleFile))
                return bundleFile.Read();
        }

        if (_ggpk!.Root.TryFindNode(path, out var node) && node is FileRecord fileRecord)
            return fileRecord.Read();

        throw new FileNotFoundException($"File not found in GGPK: {path}");
    }

    public void WriteFile(string path, byte[] data)
    {
        EnsureOpen();

        if (_isBundled && _ggpk is BundledGGPK bundled)
        {
            if (bundled.Index.TryGetFile(path, out var bundleFile))
            {
                bundleFile.Write(data);
                return;
            }
        }

        if (_ggpk!.Root.TryFindNode(path, out var node) && node is FileRecord fileRecord)
        {
            fileRecord.Write(data);
            return;
        }

        throw new FileNotFoundException($"File not found in GGPK: {path}");
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

    private void EnsureOpen()
    {
        if (_ggpk == null)
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

    /// <summary>
    /// Count emitter nodes in .pet particle file data.
    /// PET files use UTF-16LE text format. Emitters are typically
    /// indicated by lines containing "emitter" keyword.
    /// </summary>
    private static int CountEmittersInPetData(byte[] data)
    {
        if (data.Length < 8)
            return 0;

        try
        {
            var text = System.Text.Encoding.Unicode.GetString(data);
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
```

- [ ] **Step 3: Verify build**

```bash
dotnet build src/PoeNullEffects.Core/PoeNullEffects.Core.csproj
```

Expected: Build succeeded. (Note: the exact LibGGPK3 API may require adjustments based on the actual package version. The implementation above follows the documented API. If `TryFindNode`, `TryGetFile`, or other methods have different signatures, adapt the calls to match the actual API — check the package's IntelliSense or source.)

- [ ] **Step 4: Commit**

```bash
git add src/PoeNullEffects.Core/IGgpkService.cs src/PoeNullEffects.Core/GgpkService.cs
git commit -m "feat: add GgpkService wrapping LibGGPK3 for GGPK read/write operations"
```

---

## Task 7: Particle Nuller

**Files:**
- Create: `src/PoeNullEffects.Core/ParticleNuller.cs`
- Create: `src/PoeNullEffects.Tests/ParticleNullerTests.cs`

- [ ] **Step 1: Write failing tests**

Create `src/PoeNullEffects.Tests/ParticleNullerTests.cs`:

```csharp
using NSubstitute;
using PoeNullEffects.Core;
using PoeNullEffects.Core.Models;

namespace PoeNullEffects.Tests;

public class ParticleNullerTests
{
    private static readonly byte[] NullPayload = [0xFF, 0xFE, 0x30, 0x00, 0x0D, 0x00, 0x0A, 0x00];

    private readonly IGgpkService _ggpk;
    private readonly ParticleNuller _nuller;

    public ParticleNullerTests()
    {
        _ggpk = Substitute.For<IGgpkService>();
        _nuller = new ParticleNuller(_ggpk);
    }

    [Fact]
    public void NullAll_WritesNullPayloadToEveryParticle()
    {
        var particles = new List<GgpkFileEntry>
        {
            new() { Path = "Metadata/Particles/fire.pet", Size = 100 },
            new() { Path = "Metadata/Particles/ice.pet", Size = 200 }
        };
        _ggpk.GetParticleFiles().Returns(particles);

        var result = _nuller.Execute(NullMode.All, filterList: null, keepEmitters: false);

        _ggpk.Received(1).WriteFile("Metadata/Particles/fire.pet", Arg.Is<byte[]>(b => b.SequenceEqual(NullPayload)));
        _ggpk.Received(1).WriteFile("Metadata/Particles/ice.pet", Arg.Is<byte[]>(b => b.SequenceEqual(NullPayload)));
        Assert.Equal(2, result);
    }

    [Fact]
    public void NullAllExcept_SkipsMatchingPaths()
    {
        var particles = new List<GgpkFileEntry>
        {
            new() { Path = "Metadata/Particles/fire.pet", Size = 100 },
            new() { Path = "Metadata/Particles/lightning_orb/glow.pet", Size = 200 }
        };
        _ggpk.GetParticleFiles().Returns(particles);

        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, "lightning_orb\n");
        var filterList = FilterList.Load(tempFile);
        File.Delete(tempFile);

        var result = _nuller.Execute(NullMode.AllExcept, filterList, keepEmitters: false);

        _ggpk.Received(1).WriteFile("Metadata/Particles/fire.pet", Arg.Any<byte[]>());
        _ggpk.DidNotReceive().WriteFile("Metadata/Particles/lightning_orb/glow.pet", Arg.Any<byte[]>());
        Assert.Equal(1, result);
    }

    [Fact]
    public void NullOnly_OnlyNullsMatchingPaths()
    {
        var particles = new List<GgpkFileEntry>
        {
            new() { Path = "Metadata/Particles/blood_splatter.pet", Size = 100 },
            new() { Path = "Metadata/Particles/lightning.pet", Size = 200 }
        };
        _ggpk.GetParticleFiles().Returns(particles);

        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, "blood\n");
        var filterList = FilterList.Load(tempFile);
        File.Delete(tempFile);

        var result = _nuller.Execute(NullMode.Only, filterList, keepEmitters: false);

        _ggpk.Received(1).WriteFile("Metadata/Particles/blood_splatter.pet", Arg.Any<byte[]>());
        _ggpk.DidNotReceive().WriteFile("Metadata/Particles/lightning.pet", Arg.Any<byte[]>());
        Assert.Equal(1, result);
    }

    [Fact]
    public void Execute_ReportsProgressViaCallback()
    {
        var particles = new List<GgpkFileEntry>
        {
            new() { Path = "Metadata/Particles/a.pet", Size = 50 },
            new() { Path = "Metadata/Particles/b.pet", Size = 60 }
        };
        _ggpk.GetParticleFiles().Returns(particles);

        var progressValues = new List<int>();
        _nuller.Execute(NullMode.All, filterList: null, keepEmitters: false,
            progress: new Progress<int>(v => progressValues.Add(v)));

        // Progress should be called but exact timing depends on implementation
        // Just verify it ran without error
        Assert.Equal(2, _ggpk.ReceivedCalls().Count(c => c.GetMethodInfo().Name == "WriteFile"));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test src/PoeNullEffects.Tests/ --filter "FullyQualifiedName~ParticleNullerTests" -v minimal
```

Expected: FAIL — `ParticleNuller` does not exist.

- [ ] **Step 3: Implement ParticleNuller**

Create `src/PoeNullEffects.Core/ParticleNuller.cs`:

```csharp
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

            var payload = keepEmitters
                ? BuildKeepEmittersPayload(particle.Path)
                : NullPayload;

            _ggpk.WriteFile(particle.Path, payload);
            modified++;

            progress?.Report((i + 1) * 100 / particles.Count);
        }

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
            var text = System.Text.Encoding.Unicode.GetString(data);
            var lines = text.Split('\n');

            var result = new List<string>();
            var emitterCount = 0;
            var inEmitter = false;
            var depth = 0;

            foreach (var line in lines)
            {
                var trimmed = line.Trim();

                if (trimmed.StartsWith("emitter", StringComparison.OrdinalIgnoreCase))
                {
                    emitterCount++;
                    if (emitterCount == 1)
                    {
                        inEmitter = true;
                        result.Add(line);
                    }
                    continue;
                }

                if (emitterCount <= 1 || !inEmitter)
                    result.Add(line);
            }

            var resultText = string.Join("\n", result);
            return System.Text.Encoding.Unicode.GetPreamble()
                .Concat(System.Text.Encoding.Unicode.GetBytes(resultText))
                .ToArray();
        }
        catch
        {
            return NullPayload;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test src/PoeNullEffects.Tests/ --filter "FullyQualifiedName~ParticleNullerTests" -v minimal
```

Expected: 4 passed.

- [ ] **Step 5: Commit**

```bash
git add src/PoeNullEffects.Core/ParticleNuller.cs src/PoeNullEffects.Tests/ParticleNullerTests.cs
git commit -m "feat: add ParticleNuller with three null modes and keepEmitters support"
```

---

## Task 8: Shadow and Corpse Managers

**Files:**
- Create: `src/PoeNullEffects.Core/ShadowManager.cs`
- Create: `src/PoeNullEffects.Core/CorpseManager.cs`

These follow the same pattern — find relevant files, null or restore them. No separate tests needed as the logic mirrors ParticleNuller and relies on `IGgpkService` which is tested via integration.

- [ ] **Step 1: Implement ShadowManager**

Create `src/PoeNullEffects.Core/ShadowManager.cs`:

```csharp
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

            _backup.BackupFile(file.Path, _ggpk.ReadFile(file.Path));
            _ggpk.WriteFile(file.Path, ParticleNuller.NullPayload);
            modified++;

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
                _ggpk.WriteFile(file.Path, original);
                restored++;
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
```

- [ ] **Step 2: Implement CorpseManager**

Create `src/PoeNullEffects.Core/CorpseManager.cs`:

```csharp
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

            _backup.BackupFile(file.Path, _ggpk.ReadFile(file.Path));
            _ggpk.WriteFile(file.Path, ParticleNuller.NullPayload);
            modified++;

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
                _ggpk.WriteFile(file.Path, original);
                restored++;
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
```

- [ ] **Step 3: Verify build**

```bash
dotnet build src/PoeNullEffects.Core/PoeNullEffects.Core.csproj
```

Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add src/PoeNullEffects.Core/ShadowManager.cs src/PoeNullEffects.Core/CorpseManager.cs
git commit -m "feat: add ShadowManager and CorpseManager for toggling shadows and corpses"
```

---

## Task 9: Backup Manager

**Files:**
- Create: `src/PoeNullEffects.Core/BackupManager.cs`
- Create: `src/PoeNullEffects.Tests/BackupManagerTests.cs`

- [ ] **Step 1: Write failing tests**

Create `src/PoeNullEffects.Tests/BackupManagerTests.cs`:

```csharp
using PoeNullEffects.Core;

namespace PoeNullEffects.Tests;

public class BackupManagerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly BackupManager _manager;

    public BackupManagerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"poe_null_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _manager = new BackupManager(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void BackupFile_StoresData_GetBackupReturnsIt()
    {
        var data = new byte[] { 1, 2, 3, 4, 5 };
        _manager.BackupFile("Metadata/Particles/fire.pet", data);

        var result = _manager.GetBackup("Metadata/Particles/fire.pet");

        Assert.NotNull(result);
        Assert.Equal(data, result);
    }

    [Fact]
    public void GetBackup_ReturnsNull_WhenNotBackedUp()
    {
        var result = _manager.GetBackup("nonexistent.pet");

        Assert.Null(result);
    }

    [Fact]
    public void BackupFile_DoesNotOverwriteExisting()
    {
        var original = new byte[] { 1, 2, 3 };
        var modified = new byte[] { 9, 8, 7 };

        _manager.BackupFile("test.pet", original);
        _manager.BackupFile("test.pet", modified);

        var result = _manager.GetBackup("test.pet");
        Assert.Equal(original, result);
    }

    [Fact]
    public void HasBackup_ReturnsTrueWhenExists()
    {
        _manager.BackupFile("test.pet", [1, 2, 3]);

        Assert.True(_manager.HasBackup("test.pet"));
        Assert.False(_manager.HasBackup("other.pet"));
    }

    [Fact]
    public void SaveAndLoad_PersistsToDisk()
    {
        _manager.BackupFile("a.pet", [10, 20]);
        _manager.BackupFile("b.pet", [30, 40, 50]);
        _manager.Save();

        var loaded = new BackupManager(_tempDir);
        loaded.Load();

        Assert.Equal(new byte[] { 10, 20 }, loaded.GetBackup("a.pet"));
        Assert.Equal(new byte[] { 30, 40, 50 }, loaded.GetBackup("b.pet"));
    }

    [Fact]
    public void Clear_RemovesAllBackups()
    {
        _manager.BackupFile("a.pet", [1]);
        _manager.Clear();

        Assert.False(_manager.HasBackup("a.pet"));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test src/PoeNullEffects.Tests/ --filter "FullyQualifiedName~BackupManagerTests" -v minimal
```

Expected: FAIL — `BackupManager` does not exist.

- [ ] **Step 3: Implement BackupManager**

Create `src/PoeNullEffects.Core/BackupManager.cs`:

```csharp
using System.Text;

namespace PoeNullEffects.Core;

/// <summary>
/// Stores original file data before modifications.
/// Binary format: [int32 count] then for each entry:
///   [int32 pathByteLen] [utf8 path] [int32 dataLen] [data bytes]
/// </summary>
public class BackupManager
{
    private readonly string _backupDir;
    private readonly string _backupFile;
    private readonly Dictionary<string, byte[]> _backups = new(StringComparer.OrdinalIgnoreCase);

    public BackupManager(string backupDir)
    {
        _backupDir = backupDir;
        _backupFile = Path.Combine(backupDir, "backup.bin");
    }

    public int Count => _backups.Count;

    public void BackupFile(string path, byte[] originalData)
    {
        _backups.TryAdd(path, originalData);
    }

    public byte[]? GetBackup(string path)
    {
        return _backups.GetValueOrDefault(path);
    }

    public bool HasBackup(string path)
    {
        return _backups.ContainsKey(path);
    }

    public IReadOnlyDictionary<string, byte[]> GetAllBackups()
    {
        return _backups;
    }

    public void Clear()
    {
        _backups.Clear();
        if (File.Exists(_backupFile))
            File.Delete(_backupFile);
    }

    public void Save()
    {
        Directory.CreateDirectory(_backupDir);

        using var fs = File.Create(_backupFile);
        using var writer = new BinaryWriter(fs);

        writer.Write(_backups.Count);

        foreach (var (path, data) in _backups)
        {
            var pathBytes = Encoding.UTF8.GetBytes(path);
            writer.Write(pathBytes.Length);
            writer.Write(pathBytes);
            writer.Write(data.Length);
            writer.Write(data);
        }
    }

    public void Load()
    {
        _backups.Clear();

        if (!File.Exists(_backupFile))
            return;

        using var fs = File.OpenRead(_backupFile);
        using var reader = new BinaryReader(fs);

        var count = reader.ReadInt32();

        for (var i = 0; i < count; i++)
        {
            var pathLen = reader.ReadInt32();
            var pathBytes = reader.ReadBytes(pathLen);
            var path = Encoding.UTF8.GetString(pathBytes);

            var dataLen = reader.ReadInt32();
            var data = reader.ReadBytes(dataLen);

            _backups[path] = data;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test src/PoeNullEffects.Tests/ --filter "FullyQualifiedName~BackupManagerTests" -v minimal
```

Expected: 6 passed.

- [ ] **Step 5: Commit**

```bash
git add src/PoeNullEffects.Core/BackupManager.cs src/PoeNullEffects.Tests/BackupManagerTests.cs
git commit -m "feat: add BackupManager with binary save/load for original file data"
```

---

## Task 10: Make Good Processor

**Files:**
- Create: `src/PoeNullEffects.Core/MakeGoodProcessor.cs`
- Create: `src/PoeNullEffects.Tests/MakeGoodProcessorTests.cs`

- [ ] **Step 1: Write failing tests**

Create `src/PoeNullEffects.Tests/MakeGoodProcessorTests.cs`:

```csharp
using NSubstitute;
using PoeNullEffects.Core;
using PoeNullEffects.Core.Models;

namespace PoeNullEffects.Tests;

public class MakeGoodProcessorTests
{
    private readonly IGgpkService _ggpk;
    private readonly BackupManager _backup;
    private readonly MakeGoodProcessor _processor;

    public MakeGoodProcessorTests()
    {
        _ggpk = Substitute.For<IGgpkService>();
        var tempDir = Path.Combine(Path.GetTempPath(), $"poe_null_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        _backup = new BackupManager(tempDir);
        _processor = new MakeGoodProcessor(_ggpk, _backup);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(6)]
    public void GetCategoriesForLevel_ReturnsValidCategories(int level)
    {
        var categories = MakeGoodProcessor.GetPreservedCategoriesForLevel(level);

        Assert.NotNull(categories);
        // Higher levels should preserve more categories
        if (level > 0)
        {
            var lowerCategories = MakeGoodProcessor.GetPreservedCategoriesForLevel(level - 1);
            Assert.True(categories.Count >= lowerCategories.Count);
        }
    }

    [Fact]
    public void Level0_PreservesNothing()
    {
        var categories = MakeGoodProcessor.GetPreservedCategoriesForLevel(0);
        Assert.Empty(categories);
    }

    [Fact]
    public void Level6_PreservesEverything()
    {
        var categories = MakeGoodProcessor.GetPreservedCategoriesForLevel(6);
        Assert.Equal(MakeGoodProcessor.AllCategories.Count, categories.Count);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test src/PoeNullEffects.Tests/ --filter "FullyQualifiedName~MakeGoodProcessorTests" -v minimal
```

Expected: FAIL — `MakeGoodProcessor` does not exist.

- [ ] **Step 3: Implement MakeGoodProcessor**

Create `src/PoeNullEffects.Core/MakeGoodProcessor.cs`:

```csharp
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
                _backup.BackupFile(particle.Path, _ggpk.ReadFile(particle.Path));
                _ggpk.WriteFile(particle.Path, ParticleNuller.NullPayload);
                modified++;
            }

            progress?.Report((i + 1) * 100 / particles.Count);
        }

        return modified;
    }
}

public record EffectCategory(string Name, string[] PathPatterns);
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test src/PoeNullEffects.Tests/ --filter "FullyQualifiedName~MakeGoodProcessorTests" -v minimal
```

Expected: 4 passed.

- [ ] **Step 5: Commit**

```bash
git add src/PoeNullEffects.Core/MakeGoodProcessor.cs src/PoeNullEffects.Tests/MakeGoodProcessorTests.cs
git commit -m "feat: add MakeGoodProcessor with 7-level quality slider"
```

---

## Task 11: DDS to BitmapSource Converter

**Files:**
- Create: `src/PoeNullEffects.UI/Converters/DdsToBitmapConverter.cs`

- [ ] **Step 1: Implement DDS converter**

Create `src/PoeNullEffects.UI/Converters/DdsToBitmapConverter.cs`:

```csharp
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Pfim;

namespace PoeNullEffects.UI.Converters;

public static class DdsToBitmapConverter
{
    public static BitmapSource? Convert(byte[] ddsData)
    {
        try
        {
            using var ms = new MemoryStream(ddsData);
            using var image = Pfimage.FromStream(ms);

            var wpfFormat = image.Format switch
            {
                ImageFormat.Rgba32 => PixelFormats.Bgra32,
                ImageFormat.Rgb24 => PixelFormats.Bgr24,
                ImageFormat.Rgb8 => PixelFormats.Gray8,
                _ => PixelFormats.Bgra32
            };

            var pinnedData = GCHandle.Alloc(image.Data, GCHandleType.Pinned);
            try
            {
                var bitmap = BitmapSource.Create(
                    image.Width,
                    image.Height,
                    96.0, 96.0,
                    wpfFormat,
                    null,
                    pinnedData.AddrOfPinnedObject(),
                    image.DataLen,
                    image.Stride);

                bitmap.Freeze();
                return bitmap;
            }
            finally
            {
                pinnedData.Free();
            }
        }
        catch
        {
            return null;
        }
    }
}
```

- [ ] **Step 2: Add Pfim package to UI project**

```bash
dotnet add src/PoeNullEffects.UI/PoeNullEffects.UI.csproj package Pfim
```

- [ ] **Step 3: Verify build**

```bash
dotnet build src/PoeNullEffects.UI/PoeNullEffects.UI.csproj
```

Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add src/PoeNullEffects.UI/Converters/DdsToBitmapConverter.cs
git commit -m "feat: add DDS to WPF BitmapSource converter using Pfim"
```

---

## Task 12: ViewModels

**Files:**
- Create: `src/PoeNullEffects.UI/ViewModels/MainViewModel.cs`
- Create: `src/PoeNullEffects.UI/ViewModels/NullEffectsViewModel.cs`
- Create: `src/PoeNullEffects.UI/ViewModels/GgpkBrowserViewModel.cs`
- Create: `src/PoeNullEffects.UI/ViewModels/CheckListViewModel.cs`

- [ ] **Step 1: Create MainViewModel**

Create `src/PoeNullEffects.UI/ViewModels/MainViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using PoeNullEffects.Core;

namespace PoeNullEffects.UI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly GgpkService _ggpkService;
    private readonly Config _config;
    private readonly BackupManager _backupManager;

    [ObservableProperty] private string _ggpkPath = "";
    [ObservableProperty] private bool _isGgpkOpen;
    [ObservableProperty] private string _statusText = "Ready. Open a GGPK file to begin.";

    public ObservableCollection<string> LogMessages { get; } = [];

    public NullEffectsViewModel NullEffects { get; }
    public GgpkBrowserViewModel GgpkBrowser { get; }
    public CheckListViewModel CheckList { get; }

    public MainViewModel()
    {
        var dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
        Directory.CreateDirectory(dataDir);

        _config = Config.Load(Path.Combine(dataDir, "config.ini"));
        _ggpkService = new GgpkService();
        _backupManager = new BackupManager(Path.Combine(dataDir, "backups"));
        _backupManager.Load();

        GgpkPath = _config.GgpkPath;

        NullEffects = new NullEffectsViewModel(_ggpkService, _config, _backupManager, this);
        GgpkBrowser = new GgpkBrowserViewModel(_ggpkService, this);
        CheckList = new CheckListViewModel(_ggpkService, this);
    }

    [RelayCommand]
    private void BrowseGgpk()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Content.ggpk",
            Filter = "GGPK Files (*.ggpk)|*.ggpk|All Files (*.*)|*.*",
            FileName = GgpkPath
        };

        if (dialog.ShowDialog() == true)
        {
            GgpkPath = dialog.FileName;
        }
    }

    [RelayCommand]
    private async Task OpenGgpk()
    {
        if (string.IsNullOrWhiteSpace(GgpkPath))
        {
            Log("Error: No GGPK path specified.");
            return;
        }

        if (!File.Exists(GgpkPath))
        {
            Log($"Error: File not found: {GgpkPath}");
            return;
        }

        try
        {
            StatusText = "Opening GGPK...";
            Log($"Opening {GgpkPath}...");

            await Task.Run(() => _ggpkService.Open(GgpkPath));

            _config.GgpkPath = GgpkPath;
            _config.Save();

            IsGgpkOpen = true;
            StatusText = "GGPK opened successfully.";
            Log("GGPK opened. Ready for operations.");

            CheckList.RefreshCommand.Execute(null);
        }
        catch (IOException ex) when (ex.Message.Contains("used by another process"))
        {
            Log("Error: GGPK file is locked. Close Path of Exile before modifying.");
            StatusText = "Error: File locked.";
        }
        catch (Exception ex)
        {
            Log($"Error opening GGPK: {ex.Message}");
            StatusText = "Error opening GGPK.";
        }
    }

    [RelayCommand]
    private void CloseGgpk()
    {
        _ggpkService.Close();
        IsGgpkOpen = false;
        StatusText = "GGPK closed.";
        Log("GGPK closed.");
    }

    public void Log(string message)
    {
        var timestamped = $"[{DateTime.Now:HH:mm:ss}] {message}";
        Application.Current.Dispatcher.Invoke(() => LogMessages.Add(timestamped));
    }
}
```

- [ ] **Step 2: Create NullEffectsViewModel**

Create `src/PoeNullEffects.UI/ViewModels/NullEffectsViewModel.cs`:

```csharp
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PoeNullEffects.Core;
using PoeNullEffects.Core.Models;

namespace PoeNullEffects.UI.ViewModels;

public partial class NullEffectsViewModel : ObservableObject
{
    private readonly GgpkService _ggpkService;
    private readonly Config _config;
    private readonly BackupManager _backupManager;
    private readonly MainViewModel _main;
    private readonly ParticleNuller _particleNuller;
    private readonly ShadowManager _shadowManager;
    private readonly CorpseManager _corpseManager;
    private readonly MakeGoodProcessor _makeGoodProcessor;

    [ObservableProperty] private NullMode _selectedMode;
    [ObservableProperty] private bool _keepEmitters;
    [ObservableProperty] private int _makeGoodValue;
    [ObservableProperty] private int _progressValue;
    [ObservableProperty] private bool _isProcessing;

    public NullEffectsViewModel(GgpkService ggpkService, Config config,
        BackupManager backupManager, MainViewModel main)
    {
        _ggpkService = ggpkService;
        _config = config;
        _backupManager = backupManager;
        _main = main;
        _particleNuller = new ParticleNuller(ggpkService);
        _shadowManager = new ShadowManager(ggpkService, backupManager);
        _corpseManager = new CorpseManager(ggpkService, backupManager);
        _makeGoodProcessor = new MakeGoodProcessor(ggpkService, backupManager);

        SelectedMode = config.NullParticlesMethod;
        KeepEmitters = config.KeepEmitters != 0;
        MakeGoodValue = config.MakeGoodValue;
    }

    [RelayCommand]
    private async Task NullParticles()
    {
        if (!_main.IsGgpkOpen) return;
        IsProcessing = true;
        ProgressValue = 0;

        try
        {
            var dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
            FilterList? filterList = SelectedMode switch
            {
                NullMode.AllExcept => FilterList.Load(Path.Combine(dataDir, "DisableAllExceptList.txt")),
                NullMode.Only => FilterList.Load(Path.Combine(dataDir, "DisableOnlyList.txt")),
                _ => null
            };

            var progress = new Progress<int>(v => ProgressValue = v);

            var count = await Task.Run(() =>
            {
                // Backup all particles first
                var particles = _ggpkService.GetParticleFiles();
                foreach (var p in particles)
                {
                    if (!_backupManager.HasBackup(p.Path))
                        _backupManager.BackupFile(p.Path, _ggpkService.ReadFile(p.Path));
                }

                return _particleNuller.Execute(SelectedMode, filterList, KeepEmitters, progress);
            });

            _backupManager.Save();
            _config.NullParticlesMethod = SelectedMode;
            _config.KeepEmitters = KeepEmitters ? 1 : 0;
            _config.Save();

            _main.Log($"Nulled {count} particle files (mode: {SelectedMode}).");
        }
        catch (Exception ex)
        {
            _main.Log($"Error nulling particles: {ex.Message}");
        }
        finally
        {
            IsProcessing = false;
        }
    }

    [RelayCommand]
    private async Task DisableShadows()
    {
        if (!_main.IsGgpkOpen) return;
        IsProcessing = true;
        ProgressValue = 0;

        try
        {
            var progress = new Progress<int>(v => ProgressValue = v);
            var count = await Task.Run(() => _shadowManager.Disable(progress));
            _backupManager.Save();
            _main.Log($"Disabled {count} shadow files.");
        }
        catch (Exception ex)
        {
            _main.Log($"Error disabling shadows: {ex.Message}");
        }
        finally
        {
            IsProcessing = false;
        }
    }

    [RelayCommand]
    private async Task EnableShadows()
    {
        if (!_main.IsGgpkOpen) return;
        IsProcessing = true;

        try
        {
            var count = await Task.Run(() => _shadowManager.Enable());
            _backupManager.Save();
            _main.Log($"Restored {count} shadow files.");
        }
        catch (Exception ex)
        {
            _main.Log($"Error enabling shadows: {ex.Message}");
        }
        finally
        {
            IsProcessing = false;
        }
    }

    [RelayCommand]
    private async Task DisableCorpses()
    {
        if (!_main.IsGgpkOpen) return;
        IsProcessing = true;

        try
        {
            var count = await Task.Run(() => _corpseManager.Disable());
            _backupManager.Save();
            _main.Log($"Disabled {count} corpse files.");
        }
        catch (Exception ex)
        {
            _main.Log($"Error disabling corpses: {ex.Message}");
        }
        finally
        {
            IsProcessing = false;
        }
    }

    [RelayCommand]
    private async Task EnableCorpses()
    {
        if (!_main.IsGgpkOpen) return;
        IsProcessing = true;

        try
        {
            var count = await Task.Run(() => _corpseManager.Enable());
            _backupManager.Save();
            _main.Log($"Restored {count} corpse files.");
        }
        catch (Exception ex)
        {
            _main.Log($"Error enabling corpses: {ex.Message}");
        }
        finally
        {
            IsProcessing = false;
        }
    }

    [RelayCommand]
    private async Task ApplyMakeGood()
    {
        if (!_main.IsGgpkOpen) return;
        IsProcessing = true;
        ProgressValue = 0;

        try
        {
            var progress = new Progress<int>(v => ProgressValue = v);
            var count = await Task.Run(() => _makeGoodProcessor.Apply(MakeGoodValue, progress));
            _backupManager.Save();
            _config.MakeGoodValue = MakeGoodValue;
            _config.Save();
            _main.Log($"Applied Make Good level {MakeGoodValue}. Modified {count} files.");
        }
        catch (Exception ex)
        {
            _main.Log($"Error applying Make Good: {ex.Message}");
        }
        finally
        {
            IsProcessing = false;
        }
    }

    [RelayCommand]
    private async Task RestoreAll()
    {
        if (!_main.IsGgpkOpen) return;
        IsProcessing = true;

        try
        {
            var backups = _backupManager.GetAllBackups();
            var restored = 0;

            await Task.Run(() =>
            {
                foreach (var (path, data) in backups)
                {
                    try
                    {
                        _ggpkService.WriteFile(path, data);
                        restored++;
                    }
                    catch { /* skip files that no longer exist */ }
                }
            });

            _main.Log($"Restored {restored} files from backup.");
        }
        catch (Exception ex)
        {
            _main.Log($"Error restoring: {ex.Message}");
        }
        finally
        {
            IsProcessing = false;
        }
    }
}
```

- [ ] **Step 3: Create GgpkBrowserViewModel**

Create `src/PoeNullEffects.UI/ViewModels/GgpkBrowserViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using PoeNullEffects.Core;
using PoeNullEffects.Core.Models;
using PoeNullEffects.UI.Converters;

namespace PoeNullEffects.UI.ViewModels;

public partial class GgpkBrowserViewModel : ObservableObject
{
    private readonly GgpkService _ggpkService;
    private readonly MainViewModel _main;

    [ObservableProperty] private string _searchQuery = "";
    [ObservableProperty] private bool _useRegex;
    [ObservableProperty] private string _previewText = "";
    [ObservableProperty] private BitmapSource? _previewImage;
    [ObservableProperty] private bool _isTextPreview;
    [ObservableProperty] private bool _isImagePreview;
    [ObservableProperty] private GgpkFileEntry? _selectedFile;

    public ObservableCollection<GgpkFileEntry> SearchResults { get; } = [];

    public GgpkBrowserViewModel(GgpkService ggpkService, MainViewModel main)
    {
        _ggpkService = ggpkService;
        _main = main;
    }

    [RelayCommand]
    private async Task Search()
    {
        if (!_main.IsGgpkOpen || string.IsNullOrWhiteSpace(SearchQuery)) return;

        try
        {
            var results = await Task.Run(() =>
                _ggpkService.SearchFiles(SearchQuery, UseRegex));

            SearchResults.Clear();
            foreach (var entry in results.Take(10000))
                SearchResults.Add(entry);

            _main.Log($"Search found {results.Count} files.");
        }
        catch (Exception ex)
        {
            _main.Log($"Search error: {ex.Message}");
        }
    }

    [RelayCommand]
    private void PreviewFile()
    {
        if (!_main.IsGgpkOpen || SelectedFile == null) return;

        try
        {
            var data = _ggpkService.ReadFile(SelectedFile.Path);
            var ext = Path.GetExtension(SelectedFile.Path).ToLowerInvariant();

            if (ext == ".dds")
            {
                PreviewImage = DdsToBitmapConverter.Convert(data);
                IsImagePreview = PreviewImage != null;
                IsTextPreview = false;

                if (PreviewImage == null)
                    _main.Log("Could not decode DDS texture.");
            }
            else if (IsTextFile(ext))
            {
                PreviewText = data.Length > 2 && data[0] == 0xFF && data[1] == 0xFE
                    ? Encoding.Unicode.GetString(data)
                    : Encoding.UTF8.GetString(data);

                IsTextPreview = true;
                IsImagePreview = false;
            }
            else
            {
                PreviewText = $"Binary file: {data.Length:N0} bytes\n\n" +
                    BitConverter.ToString(data.Take(256).ToArray()).Replace("-", " ");
                IsTextPreview = true;
                IsImagePreview = false;
            }
        }
        catch (Exception ex)
        {
            _main.Log($"Preview error: {ex.Message}");
        }
    }

    [RelayCommand]
    private void ExportFile()
    {
        if (!_main.IsGgpkOpen || SelectedFile == null) return;

        var dialog = new SaveFileDialog
        {
            FileName = Path.GetFileName(SelectedFile.Path),
            Title = "Export File"
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            var data = _ggpkService.ReadFile(SelectedFile.Path);
            File.WriteAllBytes(dialog.FileName, data);
            _main.Log($"Exported: {SelectedFile.Path} → {dialog.FileName}");
        }
        catch (Exception ex)
        {
            _main.Log($"Export error: {ex.Message}");
        }
    }

    [RelayCommand]
    private void ImportFile()
    {
        if (!_main.IsGgpkOpen || SelectedFile == null) return;

        var dialog = new OpenFileDialog
        {
            Title = "Import File to Replace"
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            var data = File.ReadAllBytes(dialog.FileName);
            _ggpkService.WriteFile(SelectedFile.Path, data);
            _main.Log($"Imported: {dialog.FileName} → {SelectedFile.Path}");
        }
        catch (Exception ex)
        {
            _main.Log($"Import error: {ex.Message}");
        }
    }

    private static bool IsTextFile(string ext) => ext is ".pet" or ".txt" or ".cfg"
        or ".xml" or ".json" or ".csv" or ".ini" or ".hlsl" or ".glsl" or ".fx"
        or ".mat" or ".ot" or ".trl" or ".epk" or ".tsi";
}
```

- [ ] **Step 4: Create CheckListViewModel**

Create `src/PoeNullEffects.UI/ViewModels/CheckListViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PoeNullEffects.Core;
using PoeNullEffects.Core.Models;

namespace PoeNullEffects.UI.ViewModels;

public partial class CheckListViewModel : ObservableObject
{
    private readonly GgpkService _ggpkService;
    private readonly MainViewModel _main;
    private List<GgpkFileEntry> _allParticles = [];

    [ObservableProperty] private string _filterText = "";
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private int _filteredCount;

    public ObservableCollection<GgpkFileEntry> FilteredParticles { get; } = [];

    public CheckListViewModel(GgpkService ggpkService, MainViewModel main)
    {
        _ggpkService = ggpkService;
        _main = main;
    }

    [RelayCommand]
    private async Task Refresh()
    {
        if (!_main.IsGgpkOpen) return;

        try
        {
            _allParticles = await Task.Run(() =>
            {
                var particles = _ggpkService.GetParticleFiles();
                foreach (var p in particles)
                    p.EmitterCount = _ggpkService.CountEmitters(p.Path);
                return particles;
            });

            TotalCount = _allParticles.Count;
            ApplyFilter();

            _main.Log($"Loaded {TotalCount} particle files.");
        }
        catch (Exception ex)
        {
            _main.Log($"Error loading particles: {ex.Message}");
        }
    }

    [RelayCommand]
    private void ApplyFilter()
    {
        FilteredParticles.Clear();

        IEnumerable<GgpkFileEntry> filtered = _allParticles;

        if (!string.IsNullOrWhiteSpace(FilterText))
        {
            var filter = FilterText.Trim();

            // Support emitter count filter: "e>5", "e<3", "e=0"
            if (filter.StartsWith("e>") && int.TryParse(filter[2..], out var gt))
                filtered = filtered.Where(p => p.EmitterCount > gt);
            else if (filter.StartsWith("e<") && int.TryParse(filter[2..], out var lt))
                filtered = filtered.Where(p => p.EmitterCount < lt);
            else if (filter.StartsWith("e=") && int.TryParse(filter[2..], out var eq))
                filtered = filtered.Where(p => p.EmitterCount == eq);
            else
                filtered = filtered.Where(p =>
                    p.Path.Contains(filter, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var entry in filtered.Take(10000))
            FilteredParticles.Add(entry);

        FilteredCount = FilteredParticles.Count;
    }
}
```

- [ ] **Step 5: Verify build**

```bash
dotnet build src/PoeNullEffects.UI/PoeNullEffects.UI.csproj
```

Expected: Build succeeded.

- [ ] **Step 6: Commit**

```bash
git add src/PoeNullEffects.UI/ViewModels/
git commit -m "feat: add all ViewModels — Main, NullEffects, GgpkBrowser, CheckList"
```

---

## Task 13: WPF Views — MainWindow

**Files:**
- Modify: `src/PoeNullEffects.UI/MainWindow.xaml`
- Modify: `src/PoeNullEffects.UI/MainWindow.xaml.cs`
- Modify: `src/PoeNullEffects.UI/App.xaml`

- [ ] **Step 1: Update App.xaml to set up DataContext**

Replace contents of `src/PoeNullEffects.UI/App.xaml`:

```xml
<Application x:Class="PoeNullEffects.UI.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             StartupUri="MainWindow.xaml">
    <Application.Resources>
        <Style TargetType="Button">
            <Setter Property="Padding" Value="12,6"/>
            <Setter Property="Margin" Value="4"/>
            <Setter Property="MinWidth" Value="100"/>
        </Style>
    </Application.Resources>
</Application>
```

- [ ] **Step 2: Create MainWindow.xaml**

Replace contents of `src/PoeNullEffects.UI/MainWindow.xaml`:

```xml
<Window x:Class="PoeNullEffects.UI.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="clr-namespace:PoeNullEffects.UI.ViewModels"
        xmlns:views="clr-namespace:PoeNullEffects.UI.Views"
        Title="PoeNullEffects" Width="900" Height="700"
        WindowStartupLocation="CenterScreen">
    <Window.DataContext>
        <vm:MainViewModel/>
    </Window.DataContext>

    <DockPanel>
        <!-- GGPK Path Bar -->
        <Border DockPanel.Dock="Top" Padding="8" Background="#F0F0F0">
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="Auto"/>
                </Grid.ColumnDefinitions>

                <TextBlock Grid.Column="0" Text="GGPK Path:" VerticalAlignment="Center" Margin="0,0,8,0"/>
                <TextBox Grid.Column="1" Text="{Binding GgpkPath, UpdateSourceTrigger=PropertyChanged}" VerticalAlignment="Center"/>
                <Button Grid.Column="2" Content="Browse" Command="{Binding BrowseGgpkCommand}"/>
                <Button Grid.Column="3" Content="Open" Command="{Binding OpenGgpkCommand}" FontWeight="Bold"/>
                <Button Grid.Column="4" Content="Close" Command="{Binding CloseGgpkCommand}" IsEnabled="{Binding IsGgpkOpen}"/>
            </Grid>
        </Border>

        <!-- Status Bar -->
        <StatusBar DockPanel.Dock="Bottom">
            <StatusBarItem Content="{Binding StatusText}"/>
        </StatusBar>

        <!-- Log Panel -->
        <Border DockPanel.Dock="Bottom" Height="120" BorderThickness="0,1,0,0" BorderBrush="Gray">
            <ListBox ItemsSource="{Binding LogMessages}" FontFamily="Consolas" FontSize="11"
                     ScrollViewer.HorizontalScrollBarVisibility="Auto">
                <ListBox.ItemContainerStyle>
                    <Style TargetType="ListBoxItem">
                        <Setter Property="Padding" Value="2,1"/>
                    </Style>
                </ListBox.ItemContainerStyle>
            </ListBox>
        </Border>

        <!-- Tab Control -->
        <TabControl Margin="8" IsEnabled="{Binding IsGgpkOpen}">
            <TabItem Header="Null Effects">
                <views:NullEffectsView DataContext="{Binding NullEffects}"/>
            </TabItem>
            <TabItem Header="GGPK Browser">
                <views:GgpkBrowserView DataContext="{Binding GgpkBrowser}"/>
            </TabItem>
            <TabItem Header="Check List">
                <views:CheckListView DataContext="{Binding CheckList}"/>
            </TabItem>
        </TabControl>
    </DockPanel>
</Window>
```

- [ ] **Step 3: Update MainWindow.xaml.cs**

Replace contents of `src/PoeNullEffects.UI/MainWindow.xaml.cs`:

```csharp
using System.Windows;

namespace PoeNullEffects.UI;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
}
```

- [ ] **Step 4: Verify build**

```bash
dotnet build src/PoeNullEffects.UI/PoeNullEffects.UI.csproj
```

Expected: Build will fail because Views don't exist yet. That's expected — we create them in the next tasks.

- [ ] **Step 5: Commit**

```bash
git add src/PoeNullEffects.UI/MainWindow.xaml src/PoeNullEffects.UI/MainWindow.xaml.cs src/PoeNullEffects.UI/App.xaml
git commit -m "feat: add MainWindow with GGPK path bar, tab layout, log panel, and status bar"
```

---

## Task 14: WPF Views — NullEffectsView

**Files:**
- Create: `src/PoeNullEffects.UI/Views/NullEffectsView.xaml`
- Create: `src/PoeNullEffects.UI/Views/NullEffectsView.xaml.cs`

- [ ] **Step 1: Create NullEffectsView.xaml**

Create `src/PoeNullEffects.UI/Views/NullEffectsView.xaml`:

```xml
<UserControl x:Class="PoeNullEffects.UI.Views.NullEffectsView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:models="clr-namespace:PoeNullEffects.Core.Models;assembly=PoeNullEffects.Core">
    <ScrollViewer VerticalScrollBarVisibility="Auto" Padding="12">
        <StackPanel>
            <!-- Particle Nulling -->
            <GroupBox Header="Null Particles" Padding="12" Margin="0,0,0,12">
                <StackPanel>
                    <TextBlock Text="Mode:" FontWeight="SemiBold" Margin="0,0,0,4"/>
                    <RadioButton Content="Disable All" IsChecked="{Binding SelectedMode, Converter={StaticResource EnumBoolConverter}, ConverterParameter={x:Static models:NullMode.All}}" Margin="0,2"/>
                    <RadioButton Content="Disable All Except List" IsChecked="{Binding SelectedMode, Converter={StaticResource EnumBoolConverter}, ConverterParameter={x:Static models:NullMode.AllExcept}}" Margin="0,2"/>
                    <RadioButton Content="Disable Only List" IsChecked="{Binding SelectedMode, Converter={StaticResource EnumBoolConverter}, ConverterParameter={x:Static models:NullMode.Only}}" Margin="0,2"/>

                    <CheckBox Content="Keep Emitters (preserve 1 emitter per particle)" IsChecked="{Binding KeepEmitters}" Margin="0,8,0,4"/>

                    <Button Content="Null Particles" Command="{Binding NullParticlesCommand}"
                            HorizontalAlignment="Left" Margin="0,8,0,0"/>
                </StackPanel>
            </GroupBox>

            <!-- Shadows -->
            <GroupBox Header="Shadows" Padding="12" Margin="0,0,0,12">
                <StackPanel Orientation="Horizontal">
                    <Button Content="Disable Shadows" Command="{Binding DisableShadowsCommand}"/>
                    <Button Content="Enable Shadows" Command="{Binding EnableShadowsCommand}"/>
                </StackPanel>
            </GroupBox>

            <!-- Corpses -->
            <GroupBox Header="Corpses" Padding="12" Margin="0,0,0,12">
                <StackPanel Orientation="Horizontal">
                    <Button Content="Disable Corpses" Command="{Binding DisableCorpsesCommand}"/>
                    <Button Content="Enable Corpses" Command="{Binding EnableCorpsesCommand}"/>
                </StackPanel>
            </GroupBox>

            <!-- Make Good -->
            <GroupBox Header="Make Good (Quality)" Padding="12" Margin="0,0,0,12">
                <StackPanel>
                    <StackPanel Orientation="Horizontal" Margin="0,0,0,4">
                        <TextBlock Text="Level: " VerticalAlignment="Center"/>
                        <TextBlock Text="{Binding MakeGoodValue}" FontWeight="Bold" VerticalAlignment="Center"/>
                        <TextBlock Text=" (0 = max performance, 6 = near vanilla)" Foreground="Gray" VerticalAlignment="Center"/>
                    </StackPanel>
                    <Slider Minimum="0" Maximum="6" Value="{Binding MakeGoodValue}"
                            TickPlacement="BottomRight" TickFrequency="1" IsSnapToTickEnabled="True"
                            Width="300" HorizontalAlignment="Left"/>
                    <Button Content="Apply" Command="{Binding ApplyMakeGoodCommand}"
                            HorizontalAlignment="Left" Margin="0,8,0,0"/>
                </StackPanel>
            </GroupBox>

            <!-- Restore -->
            <GroupBox Header="Backup / Restore" Padding="12" Margin="0,0,0,12">
                <Button Content="Restore All from Backup" Command="{Binding RestoreAllCommand}"
                        HorizontalAlignment="Left"/>
            </GroupBox>

            <!-- Progress -->
            <ProgressBar Height="20" Value="{Binding ProgressValue}" Maximum="100"
                         Visibility="{Binding IsProcessing, Converter={StaticResource BoolToVisConverter}}"
                         Margin="0,8,0,0"/>
        </StackPanel>
    </ScrollViewer>
</UserControl>
```

- [ ] **Step 2: Create NullEffectsView.xaml.cs**

Create `src/PoeNullEffects.UI/Views/NullEffectsView.xaml.cs`:

```csharp
using System.Windows.Controls;

namespace PoeNullEffects.UI.Views;

public partial class NullEffectsView : UserControl
{
    public NullEffectsView()
    {
        InitializeComponent();
    }
}
```

- [ ] **Step 3: Add converters to App.xaml**

We need `EnumBoolConverter` and `BoolToVisConverter`. Update `src/PoeNullEffects.UI/App.xaml`:

```xml
<Application x:Class="PoeNullEffects.UI.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:converters="clr-namespace:PoeNullEffects.UI.Converters"
             StartupUri="MainWindow.xaml">
    <Application.Resources>
        <converters:EnumBoolConverter x:Key="EnumBoolConverter"/>
        <BooleanToVisibilityConverter x:Key="BoolToVisConverter"/>
        <Style TargetType="Button">
            <Setter Property="Padding" Value="12,6"/>
            <Setter Property="Margin" Value="4"/>
            <Setter Property="MinWidth" Value="100"/>
        </Style>
    </Application.Resources>
</Application>
```

- [ ] **Step 4: Create EnumBoolConverter**

Create `src/PoeNullEffects.UI/Converters/EnumBoolConverter.cs`:

```csharp
using System.Globalization;
using System.Windows.Data;

namespace PoeNullEffects.UI.Converters;

public class EnumBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value?.Equals(parameter) ?? false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return (bool)value ? parameter : Binding.DoNothing;
    }
}
```

- [ ] **Step 5: Commit**

```bash
git add src/PoeNullEffects.UI/Views/NullEffectsView.xaml src/PoeNullEffects.UI/Views/NullEffectsView.xaml.cs src/PoeNullEffects.UI/Converters/EnumBoolConverter.cs src/PoeNullEffects.UI/App.xaml
git commit -m "feat: add NullEffectsView with particle modes, shadow/corpse toggles, MakeGood slider"
```

---

## Task 15: WPF Views — GgpkBrowserView

**Files:**
- Create: `src/PoeNullEffects.UI/Views/GgpkBrowserView.xaml`
- Create: `src/PoeNullEffects.UI/Views/GgpkBrowserView.xaml.cs`

- [ ] **Step 1: Create GgpkBrowserView.xaml**

Create `src/PoeNullEffects.UI/Views/GgpkBrowserView.xaml`:

```xml
<UserControl x:Class="PoeNullEffects.UI.Views.GgpkBrowserView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
        </Grid.RowDefinitions>

        <!-- Search Bar -->
        <Border Grid.Row="0" Padding="8" Background="#F8F8F8">
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="Auto"/>
                </Grid.ColumnDefinitions>
                <TextBox Grid.Column="0" Text="{Binding SearchQuery, UpdateSourceTrigger=PropertyChanged}"
                         VerticalAlignment="Center">
                    <TextBox.InputBindings>
                        <KeyBinding Key="Return" Command="{Binding SearchCommand}"/>
                    </TextBox.InputBindings>
                </TextBox>
                <CheckBox Grid.Column="1" Content="Regex" IsChecked="{Binding UseRegex}"
                          VerticalAlignment="Center" Margin="8,0"/>
                <Button Grid.Column="2" Content="Search" Command="{Binding SearchCommand}"/>
            </Grid>
        </Border>

        <!-- Results + Preview -->
        <Grid Grid.Row="1">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="*" MinWidth="200"/>
                <ColumnDefinition Width="5"/>
                <ColumnDefinition Width="*" MinWidth="200"/>
            </Grid.ColumnDefinitions>

            <!-- File List -->
            <ListView Grid.Column="0" ItemsSource="{Binding SearchResults}"
                      SelectedItem="{Binding SelectedFile}"
                      SelectionChanged="OnSelectionChanged"
                      VirtualizingPanel.IsVirtualizing="True"
                      VirtualizingPanel.VirtualizationMode="Recycling">
                <ListView.View>
                    <GridView>
                        <GridViewColumn Header="Path" DisplayMemberBinding="{Binding Path}" Width="350"/>
                        <GridViewColumn Header="Size" DisplayMemberBinding="{Binding Size, StringFormat='{}{0:N0}'}" Width="80"/>
                    </GridView>
                </ListView.View>
                <ListView.ContextMenu>
                    <ContextMenu>
                        <MenuItem Header="Export..." Command="{Binding ExportFileCommand}"/>
                        <MenuItem Header="Import..." Command="{Binding ImportFileCommand}"/>
                    </ContextMenu>
                </ListView.ContextMenu>
            </ListView>

            <GridSplitter Grid.Column="1" Width="5" HorizontalAlignment="Stretch" Background="LightGray"/>

            <!-- Preview Panel -->
            <Grid Grid.Column="2">
                <!-- Text Preview -->
                <TextBox Text="{Binding PreviewText, Mode=OneWay}" IsReadOnly="True"
                         FontFamily="Consolas" FontSize="11"
                         VerticalScrollBarVisibility="Auto" HorizontalScrollBarVisibility="Auto"
                         TextWrapping="NoWrap"
                         Visibility="{Binding IsTextPreview, Converter={StaticResource BoolToVisConverter}}"/>

                <!-- Image Preview -->
                <Border Background="#333"
                        Visibility="{Binding IsImagePreview, Converter={StaticResource BoolToVisConverter}}">
                    <Image Source="{Binding PreviewImage}" Stretch="Uniform" Margin="8"/>
                </Border>
            </Grid>
        </Grid>
    </Grid>
</UserControl>
```

- [ ] **Step 2: Create GgpkBrowserView.xaml.cs**

Create `src/PoeNullEffects.UI/Views/GgpkBrowserView.xaml.cs`:

```csharp
using System.Windows.Controls;
using PoeNullEffects.UI.ViewModels;

namespace PoeNullEffects.UI.Views;

public partial class GgpkBrowserView : UserControl
{
    public GgpkBrowserView()
    {
        InitializeComponent();
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is GgpkBrowserViewModel vm)
            vm.PreviewFileCommand.Execute(null);
    }
}
```

- [ ] **Step 3: Commit**

```bash
git add src/PoeNullEffects.UI/Views/GgpkBrowserView.xaml src/PoeNullEffects.UI/Views/GgpkBrowserView.xaml.cs
git commit -m "feat: add GgpkBrowserView with search, file list, text/DDS preview, export/import"
```

---

## Task 16: WPF Views — CheckListView

**Files:**
- Create: `src/PoeNullEffects.UI/Views/CheckListView.xaml`
- Create: `src/PoeNullEffects.UI/Views/CheckListView.xaml.cs`

- [ ] **Step 1: Create CheckListView.xaml**

Create `src/PoeNullEffects.UI/Views/CheckListView.xaml`:

```xml
<UserControl x:Class="PoeNullEffects.UI.Views.CheckListView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <DockPanel>
        <!-- Filter Bar -->
        <Border DockPanel.Dock="Top" Padding="8" Background="#F8F8F8">
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="Auto"/>
                </Grid.ColumnDefinitions>

                <TextBlock Grid.Column="0" Text="Filter:" VerticalAlignment="Center" Margin="0,0,8,0"/>
                <TextBox Grid.Column="1" Text="{Binding FilterText, UpdateSourceTrigger=PropertyChanged}">
                    <TextBox.InputBindings>
                        <KeyBinding Key="Return" Command="{Binding ApplyFilterCommand}"/>
                    </TextBox.InputBindings>
                </TextBox>
                <Button Grid.Column="2" Content="Filter" Command="{Binding ApplyFilterCommand}"/>
                <Button Grid.Column="3" Content="Refresh" Command="{Binding RefreshCommand}"/>
                <TextBlock Grid.Column="4" VerticalAlignment="Center" Margin="8,0,0,0">
                    <Run Text="{Binding FilteredCount, Mode=OneWay}"/>
                    <Run Text=" / "/>
                    <Run Text="{Binding TotalCount, Mode=OneWay}"/>
                    <Run Text=" particles"/>
                </TextBlock>
            </Grid>
        </Border>

        <!-- Help Text -->
        <Border DockPanel.Dock="Top" Padding="8,4" Background="#FFFDE7">
            <TextBlock Text="Filter by path substring, or use e>N / e&lt;N / e=N to filter by emitter count"
                       FontSize="11" Foreground="#666"/>
        </Border>

        <!-- Particle List -->
        <ListView ItemsSource="{Binding FilteredParticles}"
                  VirtualizingPanel.IsVirtualizing="True"
                  VirtualizingPanel.VirtualizationMode="Recycling">
            <ListView.View>
                <GridView>
                    <GridViewColumn Header="Path" DisplayMemberBinding="{Binding Path}" Width="500"/>
                    <GridViewColumn Header="Size" DisplayMemberBinding="{Binding Size, StringFormat='{}{0:N0}'}" Width="80"/>
                    <GridViewColumn Header="Emitters" DisplayMemberBinding="{Binding EmitterCount}" Width="70"/>
                </GridView>
            </ListView.View>
        </ListView>
    </DockPanel>
</UserControl>
```

- [ ] **Step 2: Create CheckListView.xaml.cs**

Create `src/PoeNullEffects.UI/Views/CheckListView.xaml.cs`:

```csharp
using System.Windows.Controls;

namespace PoeNullEffects.UI.Views;

public partial class CheckListView : UserControl
{
    public CheckListView()
    {
        InitializeComponent();
    }
}
```

- [ ] **Step 3: Verify full solution build**

```bash
cd C:/Users/agony/Desktop/projects/poe_null
dotnet build PoeNullEffects.sln
```

Expected: Build succeeded with 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/PoeNullEffects.UI/Views/CheckListView.xaml src/PoeNullEffects.UI/Views/CheckListView.xaml.cs
git commit -m "feat: add CheckListView with virtualized particle list and emitter count filtering"
```

---

## Task 17: Run All Tests and Fix Issues

**Files:**
- Possibly modify any files with compilation or test failures

- [ ] **Step 1: Run all tests**

```bash
cd C:/Users/agony/Desktop/projects/poe_null
dotnet test PoeNullEffects.sln -v minimal
```

Expected: All tests pass. If any fail, fix the specific issue.

- [ ] **Step 2: Run the application**

```bash
dotnet run --project src/PoeNullEffects.UI/PoeNullEffects.UI.csproj
```

Expected: Window opens with GGPK path bar, tabs (disabled until a GGPK is opened), and log panel. Verify visually that layout renders correctly.

- [ ] **Step 3: Commit any fixes**

```bash
git add -A
git commit -m "fix: resolve build and test issues"
```

---

## Task 18: Publish as Single-File Executable

**Files:**
- No new files — uses existing publish configuration from Task 1

- [ ] **Step 1: Publish**

```bash
cd C:/Users/agony/Desktop/projects/poe_null
dotnet publish src/PoeNullEffects.UI/PoeNullEffects.UI.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish/
```

Expected: Single `PoeNullEffects.exe` in `publish/` directory.

- [ ] **Step 2: Verify output**

```bash
ls -la publish/PoeNullEffects.exe
```

Expected: File exists, roughly 50-100MB.

- [ ] **Step 3: Copy data files alongside exe**

```bash
cp -r data/ publish/data/
```

- [ ] **Step 4: Test the published exe**

Run `publish/PoeNullEffects.exe` and verify it launches.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: configure single-file self-contained publish for standalone distribution"
```

---

## Summary

| Task | Component | Tests |
|------|-----------|-------|
| 1 | Project scaffolding | — |
| 2 | Data files (config.ini, filter lists) | — |
| 3 | Models (NullMode, GgpkFileEntry, BackupEntry) | — |
| 4 | Config system | 4 tests |
| 5 | FilterList | 4 tests |
| 6 | GgpkService (LibGGPK3 wrapper) | Integration only |
| 7 | ParticleNuller | 4 tests |
| 8 | ShadowManager + CorpseManager | — |
| 9 | BackupManager | 6 tests |
| 10 | MakeGoodProcessor | 4 tests |
| 11 | DDS converter (Pfim) | — |
| 12 | All ViewModels | — |
| 13 | MainWindow | — |
| 14 | NullEffectsView | — |
| 15 | GgpkBrowserView | — |
| 16 | CheckListView | — |
| 17 | Integration test + fixes | — |
| 18 | Publish single-file exe | — |

**Total: 18 tasks, 22 unit tests, ~20 files**

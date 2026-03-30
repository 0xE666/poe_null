# PoeNullEffects — Design Spec

## Overview

A WPF (.NET 8) replica of [poeNullParticles](https://github.com/ajaxvs/poeNullParticles) — a Path of Exile performance tool that modifies the game's GGPK archive to null out particle effects, shadows, and corpses. Supports both PoE 1 (`Content.ggpk`) and PoE 2 (`.ggpk2`) formats via LibGGPK2. Distributed as a single-file self-contained executable.

## Goals

- Working tool that modifies PoE's GGPK/GGPK2 to remove particles and improve FPS
- Full feature parity with the original (particle nulling, shadows, corpses, GGPK browser, backup, Make Good slider)
- Single `.exe` output — no runtime install required

## Architecture

### Solution Structure

```
PoeNullEffects/
├── PoeNullEffects.sln
├── src/
│   ├── PoeNullEffects.Core/          # Business logic (no UI dependency)
│   │   ├── GgpkService.cs            # Wraps LibGGPK2 — open, read, modify, search
│   │   ├── ParticleNuller.cs         # Null particle logic (3 modes + keepEmitters)
│   │   ├── ShadowManager.cs          # Shadow enable/disable
│   │   ├── CorpseManager.cs          # Corpse enable/disable
│   │   ├── MakeGoodProcessor.cs      # Quality slider logic (0-6)
│   │   ├── BackupManager.cs          # Save/restore original file data
│   │   ├── Config.cs                 # Settings persistence (INI-style)
│   │   └── Models/
│   │       ├── NullMode.cs           # Enum: All, AllExcept, Only
│   │       ├── GgpkEntry.cs          # DTO for file entries
│   │       └── BackupEntry.cs        # DTO for backup records
│   └── PoeNullEffects.UI/            # WPF app
│       ├── App.xaml
│       ├── MainWindow.xaml           # Tab-based layout
│       ├── Views/
│       │   ├── NullEffectsView.xaml  # Particle nulling panel
│       │   ├── GgpkBrowserView.xaml  # Search/browse/preview/export/import
│       │   └── CheckListView.xaml    # Virtual list of particle files
│       ├── ViewModels/
│       │   ├── MainViewModel.cs
│       │   ├── NullEffectsViewModel.cs
│       │   ├── GgpkBrowserViewModel.cs
│       │   └── CheckListViewModel.cs
│       └── Converters/
│           └── DdsToBitmapConverter.cs
└── data/
    ├── config.ini
    ├── DisableAllExceptList.txt
    └── DisableOnlyList.txt
```

### Key Dependency

**LibGGPK2** (NuGet) handles GGPK + GGPK2 file format parsing. All access goes through `GgpkService` — a wrapper that isolates the rest of the app from the library. If LibGGPK2 ever needs to be swapped, only `GgpkService.cs` changes.

### Pattern

MVVM with WPF data binding. Core logic has zero UI dependencies — ViewModels reference Core services.

## Feature Specifications

### 1. Particle Nulling

**What it does:** Walks all `.pet` files under `Metadata/Particles/` in the GGPK and replaces their content with a null payload.

**Null payload:** 8 bytes — `FF FE 30 00 0D 00 0A 00` (UTF-16LE BOM + "0\r\n"). This is an empty particle definition the game engine accepts without crashing.

**Three modes:**

| Mode | Enum | Behavior |
|------|------|----------|
| Disable All | `NullMode.All` | Nulls every `.pet` file |
| Disable All Except List | `NullMode.AllExcept` | Nulls everything except paths matching entries in `DisableAllExceptList.txt` |
| Disable Only List | `NullMode.Only` | Only nulls `.pet` files matching entries in `DisableOnlyList.txt` |

**Filter list format:** One path substring per line. A `.pet` file matches if its full path contains any line from the list (case-insensitive).

**Keep Emitters toggle:** When enabled, instead of full null, preserves 1 emitter node per particle file. Requires parsing the `.pet` file structure to identify and strip all but one emitter. When disabled, writes the 8-byte null payload.

### 2. Shadow Management

Finds shadow-related effect/shader files in the GGPK and nulls or restores them. Targets files under paths containing `shadow` in the Metadata/Shaders and related directories.

### 3. Corpse Management

Same approach as shadows — identifies corpse-related assets (death effects, ragdoll, corpse models) and nulls or restores them.

### 4. Make Good Slider (0-6)

A quality/performance tradeoff slider:
- **Level 0:** Maximum removal — all particles, shadows, corpses nulled
- **Level 6:** Near-vanilla — most effects preserved, only the heaviest removed

Each level defines a whitelist of effect categories to preserve. Higher levels add more categories back. The slider updates the active filter list and re-applies nulling.

### 5. GGPK Browser

- **Tree view** of the GGPK directory structure (left panel)
- **File list** for the selected directory (right panel)
- **Search:** Text substring or regex against full file paths
- **Preview panel:**
  - Text files (`.pet`, `.txt`, `.cfg`, etc.) rendered as text
  - `.dds` textures rendered to a WPF `Image` control using Pfim library for DDS decoding
  - Other binary files show hex dump or size info
- **Export:** Save any file from the GGPK to a local path
- **Import:** Replace any file in the GGPK with a file from disk

### 6. Check List

A virtualized list of all `.pet` particle files in the GGPK:
- Columns: path, file size, emitter count
- **Filter by path:** Text substring match
- **Filter by emitter count:** Syntax like `e>5` (files with more than 5 emitters)
- Checkbox selection for manual inclusion/exclusion

### 7. Backup & Restore

- Before any modification, the original bytes of every affected file are saved to a binary backup file in `data/backups/`
- Backup keyed by GGPK file path — supports incremental backups (only new files added)
- **Restore:** One-click restore writes all backed-up original bytes back to the GGPK
- Backup file format: Simple binary — count + entries of (path_length, path_utf8, data_length, data_bytes)

### 8. Configuration

INI file at `data/config.ini`:

```ini
ggpkPath=C:\Program Files\Path of Exile\Content.ggpk
nullParticlesMethod=1
keepEmitters=0
makeGoodValue=2
```

- `ggpkPath`: Last-used path to the GGPK file
- `nullParticlesMethod`: 0=All, 1=AllExcept, 2=Only
- `keepEmitters`: 0=full null, 1=preserve one emitter
- `makeGoodValue`: 0-6 quality slider position

Loaded on startup, saved on any change.

## Error Handling

| Scenario | Behavior |
|----------|----------|
| GGPK file locked by running PoE | Error dialog: "Close Path of Exile before modifying" |
| Invalid/corrupt GGPK | Graceful failure with file details and specific error |
| Write failure mid-operation | Restore from backup, report which files were affected |
| No backup exists when restore requested | Disable restore button, show info message |

All long operations (nulling, backup, restore, search) run on background threads with a progress bar and cancel button.

## Build & Distribution

- **Framework:** .NET 8, WPF
- **Target:** `win-x64`
- **Publish command:** `dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true`
- **Output:** Single `.exe` (~50-80MB with runtime bundled) + `data/` folder with config and filter lists
- **No installer** — extract and run

## NuGet Dependencies

| Package | Purpose |
|---------|---------|
| LibGGPK2 | GGPK/GGPK2 file format parsing |
| Pfim | DDS texture decoding for preview |
| CommunityToolkit.Mvvm | MVVM helpers (ObservableObject, RelayCommand) |

# PoeNullEffects

A Path of Exile particle/effects removal tool for better FPS. Supports both PoE 1 and PoE 2, Steam and standalone installs.

Inspired by [poeNullParticles](https://github.com/ajaxvs/poeNullParticles) — built from scratch with full open-source GGPK bundle support.

## Features

- **Null particle effects** — Remove particle effects to reduce visual clutter and improve FPS
  - Disable All, Disable All Except List, Disable Only List modes
  - Editable filter lists (`data/DisableAllExceptList.txt`, `data/DisableOnlyList.txt`)
- **Quick presets** — Max Performance, Balanced, Restore Vanilla
- **Make Good slider** (0-6) — Fine-tune the performance/quality tradeoff
- **Disable shadows and corpses**
- **GGPK file browser** — Search, preview (text + DDS textures), export/import
- **Particle check list** — Browse all particle files with emitter count filtering
- **Backup/restore** — All modifications are backed up and reversible
- **Auto-detect** — Finds PoE 1/2 installs (Steam, standalone, Epic)
- **Launch PoE** button — Closes the tool and launches the game via Steam

## Requirements

- Windows 10/11 (x64)
- .NET 10 runtime (bundled in self-contained builds)
- `oo2core.dll` — Oodle decompression library (required for reading PoE bundles)
  - A wrapper DLL + `oodle-data-shared.dll` are included for bundle decompression
- Path of Exile must be **closed** before modifying game files

## Usage

1. Run `PoeNullEffects.exe` (will prompt for admin access — needed to write to Program Files)
2. Click **PoE 1** or **PoE 2** to auto-detect your install, or Browse manually
3. Click **Open**
4. Use **Quick Presets** or the advanced controls to null particles
5. Click **Launch PoE** to start the game

### Quick Presets

| Preset | Effect |
|--------|--------|
| Max Performance | Removes ALL particle effects |
| Balanced | Keeps skill/aura effects, removes ground clutter (Make Good 3) |
| Restore Vanilla | Restores all files from backup |

### After a PoE Patch

Steam will re-verify and restore modified files when PoE updates. Just re-run the tool after patching.

To manually restore: use **Restore All from Backup** or Steam's "Verify integrity of game files".

## Building from Source

```bash
dotnet build PoeNullEffects.slnx
dotnet test PoeNullEffects.slnx
dotnet publish src/PoeNullEffects.UI/PoeNullEffects.UI.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish/
```

Copy `data/`, `oo2core.dll`, and `oodle-data-shared.dll` to the publish directory.

## Architecture

```
PoeNullEffects/
├── src/
│   ├── PoeNullEffects.Core/     # Business logic (no UI dependency)
│   │   ├── GgpkService.cs       # LibGGPK3 wrapper — open, read, write, search
│   │   ├── ParticleNuller.cs    # Null particle logic (3 modes)
│   │   ├── ShadowManager.cs     # Shadow enable/disable
│   │   ├── CorpseManager.cs     # Corpse enable/disable
│   │   ├── MakeGoodProcessor.cs # Quality slider (0-6)
│   │   ├── BackupManager.cs     # Binary backup save/restore
│   │   ├── OodleHelper.cs       # Oodle DLL resolver
│   │   └── GgpkPathFinder.cs   # Auto-detect PoE installs
│   ├── PoeNullEffects.UI/       # WPF app (MVVM)
│   └── PoeNullEffects.Tests/    # xUnit tests
└── data/                        # Config + filter lists
```

## Dependencies

- [LibGGPK3](https://github.com/aianlinb/LibGGPK3) (AGPL-3.0) — GGPK/bundle parsing
- [Pfim](https://github.com/nickbabcock/Pfim) — DDS texture decoding
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) — MVVM framework
- [ooz](https://github.com/zao/ooz) — Open-source Oodle decompression (wrapper DLL)

## License

This project uses LibGGPK3 which is licensed under AGPL-3.0. Any distribution must comply with that license.

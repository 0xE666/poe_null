using System.IO;
using System.Windows;
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

    // === Presets ===

    [RelayCommand]
    private async Task PresetMaxPerformance()
    {
        if (!_main.IsGgpkOpen) return;
        SelectedMode = NullMode.All;
        MakeGoodValue = 0;
        _main.Log("Applying preset: Max Performance (null all particles)...");
        await NullParticles();
    }

    [RelayCommand]
    private async Task PresetBalanced()
    {
        if (!_main.IsGgpkOpen) return;
        MakeGoodValue = 3;
        _main.Log("Applying preset: Balanced (Make Good level 3)...");
        await ApplyMakeGood();
    }

    [RelayCommand]
    private async Task PresetVanilla()
    {
        if (!_main.IsGgpkOpen) return;
        _main.Log("Applying preset: Restore Vanilla...");
        await RestoreAll();
    }

    // === Core Operations ===

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

            var (count, total) = await Task.Run(() =>
            {
                var particles = _ggpkService.GetParticleFiles();
                foreach (var p in particles)
                {
                    if (!_backupManager.HasBackup(p.Path))
                        _backupManager.BackupFile(p.Path, _ggpkService.ReadFile(p.Path));
                }

                var nulled = _particleNuller.Execute(SelectedMode, filterList, KeepEmitters, progress);
                return (nulled, particles.Count);
            });

            _backupManager.Save();
            _config.NullParticlesMethod = SelectedMode;
            _config.KeepEmitters = KeepEmitters ? 1 : 0;
            _config.Save();

            _main.Log($"Nulled {count:N0} / {total:N0} particle files (mode: {SelectedMode}).");
            _main.UpdateStats(count, total);
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
            _main.Log($"Disabled {count:N0} shadow files.");
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
            _main.Log($"Restored {count:N0} shadow files.");
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
            _main.Log($"Disabled {count:N0} corpse files.");
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
            _main.Log($"Restored {count:N0} corpse files.");
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
            _main.Log($"Applied Make Good level {MakeGoodValue}. Modified {count:N0} files.");
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

        var backupCount = _backupManager.Count;
        if (backupCount == 0)
        {
            _main.Log("No backups to restore.");
            return;
        }

        var result = MessageBox.Show(
            $"Restore {backupCount:N0} files to their original state?\nThis will undo all particle, shadow, and corpse modifications.",
            "Confirm Restore", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return;

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
                    catch { }
                }
                _ggpkService.SaveIndex();
            });

            _main.Log($"Restored {restored:N0} files from backup.");
            _main.UpdateStats(0, 0);
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

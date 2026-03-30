using System.Diagnostics;
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
    [ObservableProperty] private string _logText = "";
    [ObservableProperty] private string _statsText = "";

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
    private void FindPoe1()
    {
        var path = GgpkPathFinder.FindPoe1();
        if (path != null)
        {
            GgpkPath = path;
            Log($"Found PoE 1: {path}");
        }
        else
        {
            Log("Could not auto-detect PoE 1 install. Use Browse to select manually.");
        }
    }

    [RelayCommand]
    private void FindPoe2()
    {
        var path = GgpkPathFinder.FindPoe2();
        if (path != null)
        {
            GgpkPath = path;
            Log($"Found PoE 2: {path}");
        }
        else
        {
            Log("Could not auto-detect PoE 2 install. Use Browse to select manually.");
        }
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
            Log("Error: No path specified.");
            return;
        }

        if (!File.Exists(GgpkPath) && !Directory.Exists(GgpkPath))
        {
            Log($"Error: Path not found: {GgpkPath}");
            return;
        }

        // Warn if PoE is running
        if (IsPoERunning())
        {
            Log("WARNING: Path of Exile is currently running. Close it before modifying game files.");
            var result = MessageBox.Show(
                "Path of Exile is currently running.\nModifying game files while the game is open can cause crashes.\n\nContinue anyway?",
                "PoE Running", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;
        }

        try
        {
            StatusText = "Opening...";
            Log($"Opening {GgpkPath}...");

            var gameDir = File.Exists(GgpkPath) ? Path.GetDirectoryName(GgpkPath)! : GgpkPath;
            OodleHelper.Initialize(gameDir);
            Log($"Oodle DLL: {OodleHelper.LoadResult}");

            await Task.Run(() => _ggpkService.Open(GgpkPath));

            _config.GgpkPath = GgpkPath;
            _config.Save();

            IsGgpkOpen = true;

            if (_ggpkService.IsBundleIndexWorking)
            {
                StatusText = $"Opened ({_ggpkService.OpenMode}).";
                Log($"Opened via {_ggpkService.OpenMode}. Bundle index active.");
            }
            else
            {
                StatusText = $"Opened ({_ggpkService.OpenMode}).";
                Log($"WARNING: Bundle index failed: {_ggpkService.BundleError}");
                Log("Particle nulling may not work without bundle index.");
            }

            CheckList.RefreshCommand.Execute(null);
        }
        catch (IOException ex) when (ex.Message.Contains("used by another process"))
        {
            Log("Error: File is locked. Close Path of Exile and Steam before modifying.");
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
        StatsText = "";
        Log("GGPK closed.");
    }

    [RelayCommand]
    private void LaunchPoe()
    {
        if (IsPoERunning())
        {
            Log("PoE is already running.");
            Application.Current.Shutdown();
            return;
        }

        _ggpkService.Close();
        IsGgpkOpen = false;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "steam://rungameid/238960",
                UseShellExecute = true
            });
        }
        catch
        {
            var exePaths = new[]
            {
                Path.Combine(GgpkPath, "PathOfExile_x64Steam.exe"),
                Path.Combine(GgpkPath, "PathOfExileSteam.exe"),
                Path.Combine(GgpkPath, "PathOfExile.exe"),
            };

            foreach (var exe in exePaths)
            {
                if (File.Exists(exe))
                {
                    Process.Start(new ProcessStartInfo { FileName = exe, UseShellExecute = true });
                    break;
                }
            }
        }

        Application.Current.Shutdown();
    }

    public void UpdateStats(int nulled, int total)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            StatsText = total > 0 ? $"{nulled:N0} / {total:N0} particles nulled" : "";
        });
    }

    public void Log(string message)
    {
        var timestamped = $"[{DateTime.Now:HH:mm:ss}] {message}";
        Application.Current.Dispatcher.Invoke(() =>
        {
            LogText = string.IsNullOrEmpty(LogText) ? timestamped : LogText + Environment.NewLine + timestamped;
        });
    }

    private static bool IsPoERunning()
    {
        var names = new[] { "PathOfExile", "PathOfExile_x64", "PathOfExileSteam", "PathOfExile_x64Steam" };
        foreach (var name in names)
        {
            if (Process.GetProcessesByName(name).Length > 0)
                return true;
        }
        return false;
    }
}

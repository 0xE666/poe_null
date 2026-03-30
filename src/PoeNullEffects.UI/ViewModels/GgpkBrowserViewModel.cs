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
            _main.Log($"Exported: {SelectedFile.Path} -> {dialog.FileName}");
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
            _main.Log($"Imported: {dialog.FileName} -> {SelectedFile.Path}");
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

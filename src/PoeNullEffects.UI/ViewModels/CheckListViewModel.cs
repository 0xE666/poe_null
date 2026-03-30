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

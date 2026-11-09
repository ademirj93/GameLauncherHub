using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GameLauncherHub.Domain.Interfaces;
using GameLauncherHub.Domain.Models;

namespace GameLauncherHub.UI.ViewModels;

/// <summary>
/// ViewModel da janela de auditoria e histórico de operações do Hub.
/// </summary>
public partial class HistoryViewModel : ObservableObject
{
    private readonly IOperationHistoryService _historyService;
    private readonly List<OperationLog> _allLogs = new();

    public ObservableCollection<OperationLog> FilteredLogs { get; } = new();

    [ObservableProperty]
    private string _selectedFilter = "Todos";

    public event Action? RequestClose;

    public HistoryViewModel(IOperationHistoryService historyService)
    {
        _historyService = historyService;
    }

    public async Task InitializeAsync()
    {
        await LoadHistoryAsync();
    }

    [RelayCommand]
    public async Task LoadHistoryAsync()
    {
        var logs = await _historyService.GetRecentLogsAsync(200);
        _allLogs.Clear();
        _allLogs.AddRange(logs);

        ApplyFilter();
    }

    [RelayCommand]
    private void SetFilter(string filter)
    {
        SelectedFilter = filter;
        ApplyFilter();
    }

    [RelayCommand]
    private async Task ClearHistoryAsync()
    {
        var result = System.Windows.MessageBox.Show(
            "Tem certeza de que deseja limpar todo o histórico de operações registrado?",
            "Confirmar Limpeza",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (result == System.Windows.MessageBoxResult.Yes)
        {
            await _historyService.ClearHistoryAsync();
            await LoadHistoryAsync();
        }
    }

    [RelayCommand]
    private void Close()
    {
        RequestClose?.Invoke();
    }

    private void ApplyFilter()
    {
        FilteredLogs.Clear();

        var query = _allLogs.AsEnumerable();

        if (SelectedFilter != "Todos")
        {
            query = query.Where(l => l.OperationType.ToString().Equals(SelectedFilter, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var log in query)
        {
            FilteredLogs.Add(log);
        }
    }
}


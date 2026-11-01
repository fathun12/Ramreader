using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ramreader.App.Services;
using Ramreader.Core.Memory;
using Ramreader.Core.Models;

namespace Ramreader.App.ViewModels;

public partial class ProcessViewModel : ObservableObject
{
    private IReadOnlyList<ProcessInfo> _all = Array.Empty<ProcessInfo>();

    public GameSession Session { get; }

    public ObservableCollection<ProcessInfo> Processes { get; } = new();

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    private ProcessInfo? _selectedProcess;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string _errorMessage = "";

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public ProcessViewModel(GameSession session)
    {
        Session = session;
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    [RelayCommand]
    private void Refresh()
    {
        _all = ProcessLister.GetRunning();
        ApplyFilter();
    }

    private bool CanConnect() => SelectedProcess is not null;

    [RelayCommand(CanExecute = nameof(CanConnect))]
    private void Connect()
    {
        try
        {
            Session.Connect(SelectedProcess!.Id);
            ErrorMessage = "";
        }
        catch (InvalidOperationException ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private void Disconnect() => Session.Disconnect();

    private void ApplyFilter()
    {
        int? selectedId = SelectedProcess?.Id;

        Processes.Clear();
        foreach (var process in _all.Where(Matches))
            Processes.Add(process);

        SelectedProcess = Processes.FirstOrDefault(p => p.Id == selectedId);
    }

    private bool Matches(ProcessInfo p)
    {
        if (string.IsNullOrWhiteSpace(SearchText)) return true;
        return p.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
            || p.WindowTitle.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
    }
}
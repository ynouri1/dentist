using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ortho.Application.Patients;
using Ortho.Domain.Entities;

namespace Ortho.UI.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly PatientService _patients;

    public ObservableCollection<Patient> Results { get; } = [];

    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private Patient? _selectedPatient;
    [ObservableProperty] private PatientFormViewModel? _form;
    [ObservableProperty] private string _statusMessage = "";

    public MainViewModel(PatientService patients)
    {
        _patients = patients;
        _ = RefreshAsync();
    }

    partial void OnSearchTextChanged(string value) => _ = RefreshAsync();

    partial void OnSelectedPatientChanged(Patient? value)
    {
        if (value is not null)
            Form = PatientFormViewModel.From(value);
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        var found = await _patients.SearchAsync(SearchText);
        Results.Clear();
        foreach (var patient in found)
            Results.Add(patient);
    }

    [RelayCommand]
    private void NewPatient()
    {
        SelectedPatient = null;
        Form = new PatientFormViewModel();
        StatusMessage = "";
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (Form is null)
            return;

        try
        {
            Form.Error = null;
            var saved = Form.Id is { } id
                ? await _patients.UpdateAsync(id, Form.ToDraft())
                : await _patients.CreateAsync(Form.ToDraft());

            StatusMessage = $"Dossier {saved.FileNumber} enregistré.";
            await RefreshAsync();
            Form = PatientFormViewModel.From(saved);
        }
        catch (ValidationException ex)
        {
            Form.Error = ex.Message;
        }
    }
}

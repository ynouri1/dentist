using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ortho.Application.Abstractions;
using Ortho.Application.Documents;
using Ortho.Application.Patients;
using Ortho.Domain.Entities;
using Ortho.Infrastructure.Backup;

namespace Ortho.UI.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly PatientService _patients;
    private readonly DocumentService _documents;
    private readonly BackupService _backup;
    private readonly ICurrentUser _currentUser;

    public ObservableCollection<Patient> Results { get; } = [];
    public ObservableCollection<Consultation> Consultations { get; } = [];
    public ObservableCollection<PatientDocument> Documents { get; } = [];

    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private Patient? _selectedPatient;
    [ObservableProperty] private PatientFormViewModel? _form;
    [ObservableProperty] private string _statusMessage = "";

    [ObservableProperty] private DateTimeOffset? _newConsultationDate = DateTimeOffset.Now;
    [ObservableProperty] private string _newConsultationReason = "";
    [ObservableProperty] private string _newConsultationNotes = "";

    [ObservableProperty] private int _importCategoryIndex = (int)DocumentCategory.PhotoIntraOrale;
    [ObservableProperty] private PatientDocument? _selectedDocument;
    [ObservableProperty] private Bitmap? _preview;

    public string CurrentUserDisplay => _currentUser.User?.DisplayName ?? _currentUser.Name;

    /// <summary>Branché par App pour afficher l'écran de verrouillage.</summary>
    public Action? LockRequested { get; set; }

    public bool HasSavedPatient => Form?.Id is not null;

    public MainViewModel(PatientService patients, DocumentService documents, BackupService backup, ICurrentUser currentUser)
    {
        _patients = patients;
        _documents = documents;
        _backup = backup;
        _currentUser = currentUser;
        _ = RefreshAsync();
    }

    partial void OnSearchTextChanged(string value) => _ = RefreshAsync();

    partial void OnFormChanged(PatientFormViewModel? value) => OnPropertyChanged(nameof(HasSavedPatient));

    partial void OnSelectedPatientChanged(Patient? value)
    {
        if (value is null)
            return;
        Form = PatientFormViewModel.From(value);
        _ = LoadRecordAsync(value.Id);
    }

    partial void OnSelectedDocumentChanged(PatientDocument? value) => _ = LoadPreviewAsync(value);

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
        Consultations.Clear();
        Documents.Clear();
        Preview = null;
        StatusMessage = "";
    }

    [RelayCommand]
    private void Lock() => LockRequested?.Invoke();

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

    [RelayCommand]
    private async Task AddConsultationAsync()
    {
        if (Form?.Id is not { } patientId)
            return;

        try
        {
            await _patients.AddConsultationAsync(
                patientId,
                (NewConsultationDate ?? DateTimeOffset.Now).DateTime,
                NewConsultationReason,
                NewConsultationNotes);

            NewConsultationReason = "";
            NewConsultationNotes = "";
            NewConsultationDate = DateTimeOffset.Now;
            StatusMessage = "Consultation ajoutée.";
            await LoadRecordAsync(patientId);
        }
        catch (ValidationException ex)
        {
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task BackupAsync()
    {
        try
        {
            var targetDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Ortho Sauvegardes");
            var archive = await Task.Run(() => _backup.ExportTo(targetDirectory));
            StatusMessage = $"Sauvegarde créée : {archive}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Échec de la sauvegarde : {ex.Message}";
        }
    }

    /// <summary>Appelé par la vue après le sélecteur de fichiers.</summary>
    public async Task ImportDocumentAsync(Stream content, string fileName)
    {
        if (Form?.Id is not { } patientId)
            return;

        try
        {
            var document = await _documents.ImportAsync(
                patientId, content, fileName, (DocumentCategory)ImportCategoryIndex);
            StatusMessage = $"Importé : {document.FileName}";
            await LoadRecordAsync(patientId);
        }
        catch (ValidationException ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private async Task LoadRecordAsync(Guid patientId)
    {
        var patient = await _patients.GetAsync(patientId);

        Consultations.Clear();
        Documents.Clear();
        SelectedDocument = null;
        Preview = null;

        if (patient is null)
            return;

        foreach (var consultation in patient.Consultations)
            Consultations.Add(consultation);
        foreach (var document in patient.Documents)
            Documents.Add(document);
    }

    private async Task LoadPreviewAsync(PatientDocument? document)
    {
        Preview = null;
        if (document is null || !DocumentService.IsImage(document))
            return;

        try
        {
            await using var stream = await _documents.OpenAsync(document);
            Preview = new Bitmap(stream);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Aperçu impossible : {ex.Message}";
        }
    }
}

using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Globalization;
using Ortho.Application.Abstractions;
using Ortho.Application.Documents;
using Ortho.Application.Imaging;
using Ortho.Application.Patients;
using Ortho.Domain.Entities;
using Ortho.Infrastructure.Backup;
using Ortho.UI.Localization;

namespace Ortho.UI.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly PatientService _patients;
    private readonly DocumentService _documents;
    private readonly ImagingService _imaging;
    private readonly BackupService _backup;
    private readonly ICurrentUser _currentUser;

    public ObservableCollection<Patient> Results { get; } = [];
    public ObservableCollection<Consultation> Consultations { get; } = [];
    public ObservableCollection<PatientDocument> Documents { get; } = [];
    public ObservableCollection<MedicalImage> Images { get; } = [];

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

    [ObservableProperty] private MedicalImage? _selectedImage;
    [ObservableProperty] private Bitmap? _imagePreview;
    [ObservableProperty] private string _calibrationKnownMm = "";
    [ObservableProperty] private string _calibrationMeasuredPx = "";

    public string SelectedImageCalibration => SelectedImage switch
    {
        null => "",
        { IsCalibrated: false } => L.Get("CalibrationNone"),
        { CalibrationSource: CalibrationSource.Manual } img
            => L.F("CalibrationManual", img.PixelSpacingXMm!.Value.ToString("F4", CultureInfo.CurrentCulture)),
        var img => L.F("CalibrationDicom", img.PixelSpacingXMm!.Value.ToString("F4", CultureInfo.CurrentCulture)),
    };

    public string CurrentUserDisplay => _currentUser.User?.DisplayName ?? _currentUser.Name;

    /// <summary>Branché par App pour afficher l'écran de verrouillage.</summary>
    public Action? LockRequested { get; set; }

    public bool HasSavedPatient => Form?.Id is not null;

    public MainViewModel(
        PatientService patients, DocumentService documents, ImagingService imaging,
        BackupService backup, ICurrentUser currentUser)
    {
        _patients = patients;
        _documents = documents;
        _imaging = imaging;
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

    partial void OnSelectedImageChanged(MedicalImage? value)
    {
        OnPropertyChanged(nameof(SelectedImageCalibration));
        _ = LoadImagePreviewAsync(value);
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
        Consultations.Clear();
        Documents.Clear();
        Images.Clear();
        Preview = null;
        ImagePreview = null;
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

            StatusMessage = L.F("StatusSaved", saved.FileNumber);
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
            StatusMessage = L.Get("StatusConsultationAdded");
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
            StatusMessage = L.F("StatusBackupCreated", archive);
        }
        catch (Exception ex)
        {
            StatusMessage = L.F("StatusBackupFailed", ex.Message);
        }
    }

    /// <summary>Viewer 2D de l'image sélectionnée ; null si aucune sélection.</summary>
    public ImageViewerViewModel? CreateViewerViewModel()
        => SelectedImage is { } image ? new ImageViewerViewModel(image, _imaging) : null;

    /// <summary>Suppression confirmée par la vue (dialogue) au préalable.</summary>
    public async Task DeleteSelectedImageAsync()
    {
        if (SelectedImage is not { } image || Form?.Id is not { } patientId)
            return;

        await _imaging.DeleteImageAsync(image.Id);
        StatusMessage = L.F("StatusImageDeleted", image.FileName);
        await LoadRecordAsync(patientId);
    }

    /// <summary>Suppression confirmée par la vue (dialogue) au préalable.</summary>
    public async Task DeleteSelectedDocumentAsync()
    {
        if (SelectedDocument is not { } document || Form?.Id is not { } patientId)
            return;

        await _documents.DeleteAsync(document.Id);
        StatusMessage = L.F("StatusDocumentDeleted", document.FileName);
        await LoadRecordAsync(patientId);
    }

    /// <summary>Appelé par la vue après le sélecteur de fichiers (onglet Imagerie).</summary>
    public async Task ImportImageAsync(Stream content, string fileName)
    {
        if (Form?.Id is not { } patientId)
            return;

        try
        {
            var image = await _imaging.ImportAsync(patientId, content, fileName);
            StatusMessage = L.F("StatusImageImported", image.FileName);
            await LoadRecordAsync(patientId);
        }
        catch (ImageDecodeException ex)
        {
            StatusMessage = L.F("StatusImageImportFailed", ex.Message);
        }
    }

    [RelayCommand]
    private async Task CalibrateAsync()
    {
        if (SelectedImage is not { } image)
            return;

        if (!TryParsePositive(CalibrationKnownMm, out var knownMm) ||
            !TryParsePositive(CalibrationMeasuredPx, out var measuredPx))
        {
            StatusMessage = L.Get("CalibrationInvalidInput");
            return;
        }

        try
        {
            var updated = await _imaging.CalibrateManuallyAsync(image.Id, knownMm, measuredPx);
            StatusMessage = L.F("StatusCalibrated",
                updated.PixelSpacingXMm!.Value.ToString("F4", CultureInfo.CurrentCulture));
            if (Form?.Id is { } patientId)
                await LoadRecordAsync(patientId);
        }
        catch (ValidationException ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private static bool TryParsePositive(string text, out double value)
        => double.TryParse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
           && value > 0;

    /// <summary>Appelé par la vue après le sélecteur de fichiers.</summary>
    public async Task ImportDocumentAsync(Stream content, string fileName)
    {
        if (Form?.Id is not { } patientId)
            return;

        try
        {
            var document = await _documents.ImportAsync(
                patientId, content, fileName, (DocumentCategory)ImportCategoryIndex);
            StatusMessage = L.F("StatusImported", document.FileName);
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

        var previouslySelectedImage = SelectedImage?.Id;

        Consultations.Clear();
        Documents.Clear();
        Images.Clear();
        SelectedDocument = null;
        Preview = null;

        if (patient is null)
            return;

        foreach (var consultation in patient.Consultations)
            Consultations.Add(consultation);
        foreach (var document in patient.Documents)
            Documents.Add(document);
        foreach (var image in patient.Images)
            Images.Add(image);

        SelectedImage = Images.FirstOrDefault(i => i.Id == previouslySelectedImage);
    }

    private async Task LoadImagePreviewAsync(MedicalImage? image)
    {
        ImagePreview = null;
        if (image is null)
            return;

        try
        {
            await using var stream = await _imaging.OpenDisplayAsync(image);
            ImagePreview = new Bitmap(stream);
        }
        catch (Exception ex)
        {
            StatusMessage = L.F("StatusPreviewFailed", ex.Message);
        }
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
            StatusMessage = L.F("StatusPreviewFailed", ex.Message);
        }
    }
}

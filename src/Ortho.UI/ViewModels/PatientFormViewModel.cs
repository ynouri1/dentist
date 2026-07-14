using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Ortho.Application.Patients;
using Ortho.Domain.Entities;

namespace Ortho.UI.ViewModels;

public partial class PatientFormViewModel : ViewModelBase
{
    /// <summary>Null tant que le patient n'a pas été enregistré.</summary>
    public Guid? Id { get; private set; }

    [ObservableProperty] private string _fileNumber = "";
    [ObservableProperty] private string _firstName = "";
    [ObservableProperty] private string _lastName = "";
    [ObservableProperty] private DateTimeOffset? _birthDate;
    [ObservableProperty] private int _sexIndex;
    [ObservableProperty] private string _phone = "";
    [ObservableProperty] private string _email = "";
    [ObservableProperty] private string _address = "";
    [ObservableProperty] private string _notes = "";
    [ObservableProperty] private string? _error;

    public string Title => Id is null
        ? Localization.L.Get("FormTitleNew")
        : Localization.L.F("FormTitleRecord", FileNumber);

    public static PatientFormViewModel From(Patient patient) => new()
    {
        Id = patient.Id,
        FileNumber = patient.FileNumber,
        FirstName = patient.FirstName,
        LastName = patient.LastName,
        BirthDate = patient.BirthDate is { } d
            ? new DateTimeOffset(d.ToDateTime(TimeOnly.MinValue))
            : null,
        SexIndex = (int)patient.Sex,
        Phone = patient.Phone ?? "",
        Email = patient.Email ?? "",
        Address = patient.Address ?? "",
        Notes = patient.Notes ?? "",
    };

    public PatientDraft ToDraft() => new()
    {
        FirstName = FirstName,
        LastName = LastName,
        BirthDate = BirthDate is { } d ? DateOnly.FromDateTime(d.Date) : null,
        Sex = (Sex)SexIndex,
        Phone = Phone,
        Email = Email,
        Address = Address,
        Notes = Notes,
    };
}

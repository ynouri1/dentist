using System.Globalization;
using System.Resources;

namespace Ortho.UI.Localization;

/// <summary>
/// Accès typé aux chaînes de l'interface. Le français est la ressource neutre ;
/// l'arabe arrivera via un satellite Strings.ar.resx (prévoir le passage en RTL).
/// </summary>
public static class L
{
    private static readonly ResourceManager Resources =
        new("Ortho.UI.Resources.Strings", typeof(L).Assembly);

    public static string Get(string key)
        => Resources.GetString(key, CultureInfo.CurrentUICulture) ?? key;

    public static string F(string key, params object?[] args)
        => string.Format(CultureInfo.CurrentUICulture, Get(key), args);

    public static string AppTitle => Get(nameof(AppTitle));

    public static string LoginWindowTitle => Get(nameof(LoginWindowTitle));
    public static string LoginTitleFirstRun => Get(nameof(LoginTitleFirstRun));
    public static string LoginTitleUnlock => Get(nameof(LoginTitleUnlock));
    public static string LoginTitleDefault => Get(nameof(LoginTitleDefault));
    public static string LoginSubmitFirstRun => Get(nameof(LoginSubmitFirstRun));
    public static string LoginSubmitUnlock => Get(nameof(LoginSubmitUnlock));
    public static string LoginSubmitDefault => Get(nameof(LoginSubmitDefault));
    public static string LabelUsername => Get(nameof(LabelUsername));
    public static string LabelDisplayName => Get(nameof(LabelDisplayName));
    public static string DisplayNamePlaceholder => Get(nameof(DisplayNamePlaceholder));
    public static string LabelRole => Get(nameof(LabelRole));
    public static string RolePraticien => Get(nameof(RolePraticien));
    public static string RoleAssistante => Get(nameof(RoleAssistante));
    public static string LabelPassword => Get(nameof(LabelPassword));
    public static string LabelPasswordConfirmation => Get(nameof(LabelPasswordConfirmation));
    public static string ErrorPasswordMismatch => Get(nameof(ErrorPasswordMismatch));
    public static string ErrorBadCredentials => Get(nameof(ErrorBadCredentials));

    public static string SearchPlaceholder => Get(nameof(SearchPlaceholder));
    public static string NewPatient => Get(nameof(NewPatient));
    public static string BackupData => Get(nameof(BackupData));
    public static string Lock => Get(nameof(Lock));
    public static string TabRecord => Get(nameof(TabRecord));
    public static string TabConsultations => Get(nameof(TabConsultations));
    public static string TabDocuments => Get(nameof(TabDocuments));
    public static string LabelLastName => Get(nameof(LabelLastName));
    public static string LabelFirstName => Get(nameof(LabelFirstName));
    public static string LabelBirthDate => Get(nameof(LabelBirthDate));
    public static string LabelSex => Get(nameof(LabelSex));
    public static string SexUnknown => Get(nameof(SexUnknown));
    public static string SexMale => Get(nameof(SexMale));
    public static string SexFemale => Get(nameof(SexFemale));
    public static string LabelPhone => Get(nameof(LabelPhone));
    public static string LabelEmail => Get(nameof(LabelEmail));
    public static string LabelAddress => Get(nameof(LabelAddress));
    public static string LabelNotes => Get(nameof(LabelNotes));
    public static string Save => Get(nameof(Save));

    public static string NewConsultation => Get(nameof(NewConsultation));
    public static string ReasonPlaceholder => Get(nameof(ReasonPlaceholder));
    public static string ClinicalNotesPlaceholder => Get(nameof(ClinicalNotesPlaceholder));
    public static string Add => Get(nameof(Add));

    public static string CategoryDocument => Get(nameof(CategoryDocument));
    public static string CategoryIntraOral => Get(nameof(CategoryIntraOral));
    public static string CategoryExtraOral => Get(nameof(CategoryExtraOral));
    public static string CategoryRadiography => Get(nameof(CategoryRadiography));
    public static string CategoryExam => Get(nameof(CategoryExam));
    public static string ImportFiles => Get(nameof(ImportFiles));
    public static string ImportDialogTitle => Get(nameof(ImportDialogTitle));
    public static string ImportFilterName => Get(nameof(ImportFilterName));
}

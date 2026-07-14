using CommunityToolkit.Mvvm.ComponentModel;

namespace Ortho.UI.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _greeting = "Ortho — Logiciel d'orthodontie";
}

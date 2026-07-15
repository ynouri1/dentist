using Avalonia.Controls;
using Ortho.UI.ViewModels;

namespace Ortho.UI.Views;

public partial class SuperpositionWindow : Window
{
    // Constructeur requis par le chargeur XAML (designer) uniquement.
    public SuperpositionWindow() : this(null!)
    {
    }

    public SuperpositionWindow(SuperpositionViewModel viewModel)
    {
        InitializeComponent();
        if (viewModel is null)
            return;

        DataContext = viewModel;
        viewModel.Updated += () => Overlay.SetData(
            viewModel.ReferenceBitmap, viewModel.ReferenceSegments, viewModel.OverlaySegments);
    }
}

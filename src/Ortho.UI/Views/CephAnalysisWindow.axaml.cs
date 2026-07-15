using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Ortho.UI.Controls;
using Ortho.UI.ViewModels;

namespace Ortho.UI.Views;

public partial class CephAnalysisWindow : Window
{
    private readonly CephAnalysisViewModel _viewModel;

    // Constructeur requis par le chargeur XAML (designer) uniquement.
    public CephAnalysisWindow() : this(null!)
    {
    }

    public CephAnalysisWindow(CephAnalysisViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        if (viewModel is null)
            return;

        DataContext = viewModel;

        viewModel.StateChanged += PushState;
        Canvas.LandmarkPlaced += async point => await viewModel.PlaceAsync(point);
        Canvas.LandmarkMoved += async (code, point) => await viewModel.MoveAsync(code, point);
    }

    private void PushState()
    {
        if (_viewModel.Bitmap is { } bitmap)
            Canvas.SetImage(bitmap);
        Canvas.SetState(
            _viewModel.GetPlacedLandmarks(),
            _viewModel.GetTraceLines(),
            _viewModel.HasCurrentLandmark);
    }

    private void OnBrightnessChanged(object? sender, RangeBaseValueChangedEventArgs e)
        => Canvas?.SetValue(CephCanvasControl.BrightnessProperty, e.NewValue);

    private void OnContrastChanged(object? sender, RangeBaseValueChangedEventArgs e)
        => Canvas?.SetValue(CephCanvasControl.ContrastProperty, e.NewValue);
}

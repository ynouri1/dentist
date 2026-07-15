using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Ortho.UI.Controls;
using Ortho.UI.Localization;
using Ortho.UI.ViewModels;

namespace Ortho.UI.Views;

public partial class ImageViewerWindow : Window
{
    private readonly ImageViewerViewModel _viewModel;

    // Constructeur requis par le chargeur XAML (designer) uniquement.
    public ImageViewerWindow() : this(null!)
    {
    }

    public ImageViewerWindow(ImageViewerViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        if (viewModel is null)
            return;

        DataContext = viewModel;

        viewModel.Loaded += () =>
        {
            if (viewModel.Bitmap is { } bitmap)
                Viewer.SetImage(bitmap);
            PushAnnotations();
        };
        viewModel.Annotations.CollectionChanged += (_, _) => PushAnnotations();
        Viewer.AnnotationCompleted += async (type, points) => await viewModel.AddAnnotationAsync(type, points);
        Viewer.CalibrationMeasured += async pixels =>
        {
            var answer = await InputDialog.ShowAsync(
                this, L.Get("CalibrationPromptTitle"), L.Get("CalibrationPromptMessage"));
            if (!string.IsNullOrWhiteSpace(answer))
                await viewModel.CalibrateAsync(answer, pixels);
        };
    }

    private void PushAnnotations()
        => Viewer.SetAnnotations(new List<AnnotationDisplay>(_viewModel.Annotations));

    private void OnToolChanged(object? sender, RoutedEventArgs e)
    {
        if (Viewer is null || sender is not RadioButton { IsChecked: true })
            return;
        Viewer.CancelPending();
        Viewer.Tool = sender switch
        {
            RadioButton rb when rb == ToolDistance => ViewerTool.Distance,
            RadioButton rb when rb == ToolAngle => ViewerTool.Angle,
            RadioButton rb when rb == ToolLine => ViewerTool.Line,
            RadioButton rb when rb == ToolArrow => ViewerTool.Arrow,
            RadioButton rb when rb == ToolText => ViewerTool.Text,
            RadioButton rb when rb == ToolCalibrate => ViewerTool.Calibrate,
            _ => ViewerTool.Pan,
        };
    }

    private void OnRotateLeft(object? sender, RoutedEventArgs e) => Viewer.RotateBy(-90);

    private void OnRotateRight(object? sender, RoutedEventArgs e) => Viewer.RotateBy(90);

    private void OnFitView(object? sender, RoutedEventArgs e) => Viewer.FitToView();

    private void OnBrightnessChanged(object? sender, RangeBaseValueChangedEventArgs e)
        => Viewer?.SetValue(ImageViewerControl.BrightnessProperty, e.NewValue);

    private void OnContrastChanged(object? sender, RangeBaseValueChangedEventArgs e)
        => Viewer?.SetValue(ImageViewerControl.ContrastProperty, e.NewValue);
}

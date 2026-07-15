using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Ortho.UI.Localization;
using Ortho.UI.ViewModels;

namespace Ortho.UI.Views;

public partial class MainWindow : Window
{
    private static readonly TimeSpan InactivityDelay = TimeSpan.FromMinutes(15);
    private readonly DispatcherTimer _inactivityTimer;

    public MainWindow()
    {
        InitializeComponent();

        // Verrouillage automatique : toute interaction (souris, clavier) réarme le compte à rebours.
        _inactivityTimer = new DispatcherTimer { Interval = InactivityDelay };
        _inactivityTimer.Tick += (_, _) =>
        {
            if (IsVisible && DataContext is MainViewModel viewModel)
                viewModel.LockCommand.Execute(null);
        };
        AddHandler(PointerPressedEvent, ResetInactivityTimer, handledEventsToo: true);
        AddHandler(PointerMovedEvent, ResetInactivityTimer, handledEventsToo: true);
        AddHandler(KeyDownEvent, ResetInactivityTimer, handledEventsToo: true);
        _inactivityTimer.Start();
    }

    private void ResetInactivityTimer(object? sender, RoutedEventArgs e)
    {
        _inactivityTimer.Stop();
        _inactivityTimer.Start();
    }

    protected override void OnClosed(EventArgs e)
    {
        _inactivityTimer.Stop();
        base.OnClosed(e);
    }

    private async void OnImportImagesClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
            return;

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = L.ImagingDialogTitle,
            AllowMultiple = true,
            FileTypeFilter =
            [
                new FilePickerFileType(L.ImagingFilterName)
                {
                    Patterns = ["*.dcm", "*.dicom", "*.jpg", "*.jpeg", "*.png", "*.tif", "*.tiff"],
                },
                FilePickerFileTypes.All,
            ],
        });

        foreach (var file in files)
        {
            await using var stream = await file.OpenReadAsync();
            await viewModel.ImportImageAsync(stream, file.Name);
        }
    }

    private async void OnImportClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
            return;

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = L.ImportDialogTitle,
            AllowMultiple = true,
            FileTypeFilter =
            [
                new FilePickerFileType(L.ImportFilterName)
                {
                    Patterns = ["*.jpg", "*.jpeg", "*.png", "*.tif", "*.tiff", "*.dcm", "*.pdf"],
                },
                FilePickerFileTypes.All,
            ],
        });

        foreach (var file in files)
        {
            await using var stream = await file.OpenReadAsync();
            await viewModel.ImportDocumentAsync(stream, file.Name);
        }
    }
}

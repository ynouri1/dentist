using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Ortho.UI.ViewModels;

namespace Ortho.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private async void OnImportClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
            return;

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Importer des documents",
            AllowMultiple = true,
            FileTypeFilter =
            [
                new FilePickerFileType("Images et documents")
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

using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Ortho.UI.Localization;

namespace Ortho.UI.Views;

/// <summary>Petit dialogue modal Supprimer/Annuler, construit en code.</summary>
public static class ConfirmDialog
{
    public static async Task<bool> ShowAsync(Window owner, string message)
    {
        var confirm = new Button { Content = L.Get("ConfirmDelete") };
        var cancel = new Button { Content = L.Get("Cancel"), IsCancel = true };

        var dialog = new Window
        {
            Title = L.Get("ConfirmDeleteTitle"),
            Width = 440,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = new StackPanel
            {
                Margin = new Thickness(24),
                Spacing = 16,
                Children =
                {
                    new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 8,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Children = { cancel, confirm },
                    },
                },
            },
        };

        confirm.Click += (_, _) => dialog.Close(true);
        cancel.Click += (_, _) => dialog.Close(false);

        return await dialog.ShowDialog<bool?>(owner) == true;
    }
}

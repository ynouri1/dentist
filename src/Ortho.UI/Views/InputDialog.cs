using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Ortho.UI.Localization;

namespace Ortho.UI.Views;

/// <summary>Dialogue modal de saisie d'une valeur, construit en code.</summary>
public static class InputDialog
{
    /// <summary>Retourne le texte saisi, ou null si annulé.</summary>
    public static async Task<string?> ShowAsync(Window owner, string title, string message)
    {
        var input = new TextBox();
        var ok = new Button { Content = L.Get("Ok"), IsDefault = true };
        var cancel = new Button { Content = L.Get("Cancel"), IsCancel = true };

        var dialog = new Window
        {
            Title = title,
            Width = 420,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = new StackPanel
            {
                Margin = new Thickness(24),
                Spacing = 12,
                Children =
                {
                    new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                    input,
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 8,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Children = { cancel, ok },
                    },
                },
            },
        };

        ok.Click += (_, _) => dialog.Close(input.Text);
        cancel.Click += (_, _) => dialog.Close(null);
        dialog.Opened += (_, _) => input.Focus();

        return await dialog.ShowDialog<string?>(owner);
    }
}

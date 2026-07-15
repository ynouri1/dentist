using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Ortho.UI.Localization;

namespace Ortho.UI.Views;

/// <summary>Dialogue modal d'information (un seul bouton), construit en code.</summary>
public static class InfoDialog
{
    public static async Task ShowAsync(Window owner, string title, string message, string? highlight = null)
    {
        var ok = new Button
        {
            Content = L.Get("Ok"),
            IsDefault = true,
            HorizontalAlignment = HorizontalAlignment.Right,
        };

        var content = new StackPanel
        {
            Margin = new Thickness(24),
            Spacing = 14,
            Children = { new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap } },
        };
        if (highlight is not null)
        {
            content.Children.Add(new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(30, 128, 128, 128)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(12),
                Child = new SelectableTextBlock
                {
                    Text = highlight,
                    FontSize = 18,
                    FontWeight = FontWeight.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                },
            });
        }
        content.Children.Add(ok);

        var dialog = new Window
        {
            Title = title,
            Width = 460,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = content,
        };
        ok.Click += (_, _) => dialog.Close();
        await dialog.ShowDialog(owner);
    }
}

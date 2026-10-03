using System.Windows;

namespace NextMind.Desktop.App;

/// <summary>
/// The app's only dialog: a text prompt ("Zone name") or a confirmation. Topmost and explicitly activated, because the
/// zones themselves are non-activating windows and the process is usually not in the foreground when the dialog opens.
/// </summary>
public partial class PromptWindow : Window
{
    private readonly bool _needsText;

    private PromptWindow(string title, string message, string? initialText, string okText, bool confirmOnly)
    {
        InitializeComponent();
        Title = title;
        MessageText.Text = message;
        OkButton.Content = okText;
        _needsText = !confirmOnly;

        if (confirmOnly)
        {
            Input.Visibility = Visibility.Collapsed;
            CancelButton.IsDefault = true; // Enter must never confirm a destructive-looking action
        }
        else
        {
            Input.Text = initialText ?? string.Empty;
            OkButton.IsDefault = true;
        }

        Loaded += (_, _) =>
        {
            Activate();
            if (_needsText)
            {
                Input.Focus();
                Input.SelectAll();
            }
        };
    }

    public string? Text => Input.Text;

    /// <summary>Returns the typed text, or null if cancelled or empty.</summary>
    public static string? AskText(string title, string label, string initial, string okText)
    {
        var dialog = new PromptWindow(title, label, initial, okText, confirmOnly: false);
        return dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.Text) ? dialog.Text.Trim() : null;
    }

    public static bool Confirm(string title, string message, string okText)
    {
        var dialog = new PromptWindow(title, message, null, okText, confirmOnly: true);
        return dialog.ShowDialog() == true;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (_needsText && string.IsNullOrWhiteSpace(Input.Text))
        {
            Input.Focus();
            return;
        }

        DialogResult = true;
    }
}

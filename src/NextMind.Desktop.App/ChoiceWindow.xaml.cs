using System.Windows;
using System.Windows.Controls;

namespace NextMind.Desktop.App;

/// <summary>
/// A message with any number of buttons, for the decisions M3 needs ("Move into Zone / Add as Reference / Cancel", "Return items and delete Zone",
/// error notices). Topmost and explicitly activated because zones are non-activating. Closing the window counts as the cancel button.
/// </summary>
public partial class ChoiceWindow : Window
{
    private int _chosen = -1;

    private ChoiceWindow(string title, string message, IReadOnlyList<string> buttons, int defaultIndex, int cancelIndex)
    {
        InitializeComponent();
        Title = title;
        MessageText.Text = message;

        for (var i = 0; i < buttons.Count; i++)
        {
            var index = i;
            var button = new Button
            {
                Content = buttons[i],
                Padding = new Thickness(14, 7, 14, 7),
                Margin = new Thickness(0, i == 0 ? 0 : 8, 0, 0),
                HorizontalContentAlignment = HorizontalAlignment.Left,
                IsDefault = i == defaultIndex,
                IsCancel = i == cancelIndex,
            };
            button.Click += (_, _) =>
            {
                _chosen = index;
                DialogResult = true;
            };
            Buttons.Children.Add(button);
        }

        Loaded += (_, _) => Activate();
    }

    /// <summary>Returns the index of the clicked button, or -1 if the window was closed (treat as cancel).</summary>
    public static int Ask(string title, string message, IReadOnlyList<string> buttons, int defaultIndex, int cancelIndex)
    {
        var window = new ChoiceWindow(title, message, buttons, defaultIndex, cancelIndex);
        return window.ShowDialog() == true ? window._chosen : -1;
    }

    public static void Inform(string title, string message) => Ask(title, message, ["OK"], 0, 0);
}

using System.Windows;
using System.Windows.Input;
using NextMind.Desktop.Core.Config;
using NextMind.Desktop.Core.Widgets;

namespace NextMind.Desktop.App;

public partial class NoteEditorWindow : Window
{
    private NoteEditorWindow(string initialText)
    {
        InitializeComponent();
        Input.MaxLength = WidgetLimits.MaxNoteLength;
        Input.Text = QuickNoteDraft.PrepareForSave(initialText);
        Loaded += (_, _) =>
        {
            Activate();
            Input.Focus();
            Input.CaretIndex = Input.Text.Length;
        };
    }

    public static string? Ask(string initialText)
    {
        var dialog = new NoteEditorWindow(initialText);
        return dialog.ShowDialog() == true ? QuickNoteDraft.PrepareForSave(dialog.Input.Text) : null;
    }

    private void Save_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            DialogResult = true;
            e.Handled = true;
        }
    }
}
